// TownLoadingModule.cs (새 파일)
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TownLoadingModule : ITownLoadingModule
{
    private TownFlowModel model;
    private IServiceContainer serviceContainer;
    public bool IsActive { get; private set; }

    public TownLoadingModule(IServiceContainer container = null)
    {
        serviceContainer = container;
    }

    public void Initialize(TownFlowModel flowModel)
    {
        model = flowModel;
    }
    public void SetServiceContainer(IServiceContainer container)
    {
        serviceContainer = container;
    }


    // ProcessLoading 메서드 내부 수정
    public async UniTask ProcessLoading(System.Func<UniTask> onEventExitPrevFlow)
    {
        Debug.Log("[TownLoadingModule] Starting loading process");
        IsActive = true;

        var resourceService = serviceContainer?.GetService<IResourceService>();
        var playerService = serviceContainer?.GetService<IPlayerService>();
        var townObjectService = serviceContainer?.GetService<ITownObjectService>();
        var townSceneService = serviceContainer?.GetService<ITownSceneService>();
        var networkService = serviceContainer?.GetService<INetworkService>();

        try
        {
            // 1. 타운 씬 로드
            if (resourceService != null && !resourceService.CheckLoadedScene(StringDefine.SCENE_TOWN))
            {
                await resourceService.LoadSceneAsync(StringDefine.SCENE_TOWN, LoadSceneMode.Additive);
            }

            // 2. 현재 씬 로드 (휘발성 씬 우선)
            string sceneToLoad = model.IsVolatilityScene ? model.VolatilityScene : model.CurrentScene;
            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                string scenePath = model.GetScenePath(sceneToLoad);
                if (resourceService != null && !resourceService.CheckLoadedScene(scenePath))
                {
                    await resourceService.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                }
            }

            // 3. 이전 Flow 종료
            if (onEventExitPrevFlow != null)
            {
                await onEventExitPrevFlow();
            }

            // 4. 불필요한 씬 언로드
            if (resourceService != null)
            {
                var scenesToUnload = new List<string>
                {
                    StringDefine.SCENE_ROOT,
                    StringDefine.SCENE_LOGO,
                    StringDefine.SCENE_INTRO,
                    StringDefine.SCENE_BATTLE
                };

                foreach (var scene in scenesToUnload)
                {
                    if (resourceService.CheckLoadedScene(scene))
                    {
                        await resourceService.UnLoadSceneAsync(scene);
                    }
                }
            }

            // 5. 네트워크 데이터 요청
            if (networkService != null && !model.IsOffline)
            {
                await RequestInitialData(networkService);
            }

            // 6. 플레이어 생성
            if (playerService != null)
            {
                await playerService.CreateMyPlayerUnit();
                await playerService.LoadMyPlayerCharacterObject();
            }

            // 7. NPC 로딩
            if (townObjectService != null)
            {
                townObjectService.LoadAllTownNpcInfoGroup();
                await townObjectService.StartProcessAsync();
                townObjectService.SetConditionRoad();
            }

            // 8. UI 초기화
            if (townSceneService != null)
            {
                await townSceneService.PlayLobbyMenu(true);
            }

            // 9. 메모리 정리
            if (resourceService != null)
            {
                await resourceService.UnloadUnusedAssets(true);
            }

            Debug.Log("[TownLoadingModule] Loading process completed");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TownLoadingModule] Loading failed: {e}");
            throw;
        }
        finally
        {
            IsActive = false;
            model.SetLoading(false);
        }
    }

    private async UniTask RequestInitialData(INetworkService networkService)
    {
        Debug.Log("[TownLoadingModule] Requesting initial network data");

        var tasks = new List<UniTask>();

        // 레드닷 정보
        tasks.Add(networkService.RequestReddot());

        // 넷마이닝 정보
        tasks.Add(networkService.RequestNetMining());

        // 이벤트 체크
        tasks.Add(networkService.CheckAndUpdateEvents());

        await UniTask.WhenAll(tasks);
    }

    public async UniTask CleanUp()
    {
        IsActive = false;
        await UniTask.Yield();
    }
}