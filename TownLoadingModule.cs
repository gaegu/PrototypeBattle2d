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
            if (resourceService != null )
            {
                await resourceService.LoadSceneAsync(StringDefine.SCENE_TOWN, LoadSceneMode.Additive);
            }


            // 2. 현재 씬 로드 (휘발성 씬 우선)
            string sceneToLoad = model.IsVolatilityScene ? model.VolatilityScene : model.CurrentScene;
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogError("[TownLoadingModule] CRITICAL ERROR: CurrentScene is not set! Using default scene.");
                sceneToLoad = FieldMapDefine.FIELDMAP_MID_INDISTREET_MLEOFFICEINDOOR_01.ToString();
                model.SetCurrentScene(sceneToLoad);
            }

            string scenePath = model.GetScenePath(sceneToLoad);
            Debug.Log($"[TownLoadingModule] Loading scene: {sceneToLoad} -> {scenePath}");

            if (resourceService != null)
            {
                Scene scene = await resourceService.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                await UniTask.NextFrame();
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
                    await resourceService.UnLoadSceneAsync(scene);
                }
            }


            // 5. 네트워크 데이터 요청
            if (networkService != null && !model.IsOffline)
            {
                await RequestInitialData(networkService);
            }

            if (BackgroundSceneManager.Instance != null)
            {
                BackgroundSceneManager.Instance.ShowTownGroup(true);
                BackgroundSceneManager.Instance.AddTownObjects(model.CurrentFiledMapDefine);
            }


            if (TownSceneManager.Instance != null)
            {
                // 상호작용 이벤트는 NewTownFlow에서 설정해야 함
                // TownSceneManager.Instance.SetEventInteraction(OnEventCheckInteraction);
                TownSceneManager.Instance.ShowTownCanvas();
            }


            // 6. 플레이어 생성
            if (playerService != null)
            {
                await playerService.CreateMyPlayerUnit();
                await playerService.LoadMyPlayerCharacterObject();

                playerService.SetMyPlayerSpawn(
            TownObjectType.WarpPoint,
            StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT,
            true
        );

                await playerService.MyPlayer.TownPlayer.VisibleTownObject(true);
            }


            if (BackgroundSceneManager.Instance != null)
            {
                BackgroundSceneManager.Instance.SetCinemachineFollowTarget();
                BackgroundSceneManager.Instance.OperateTownDecoratorFactory();
            }


            // 7. NPC 로딩
            if (townObjectService != null)
            {
                townObjectService.LoadAllTownNpcInfoGroup();

                // await townObjectService.StartProcessAsync();

                townObjectService.SetConditionRoad();
            }

            CameraManager.Instance?.SetActiveTownCameras(true);
            CameraManager.Instance?.RestoreDofTarget();
            CameraManager.Instance?.SetLiveVirtualCamera();


            // 8. UI 초기화
            if (townSceneService != null)
            {
                await townSceneService.PlayLobbyMenu(true);
            }

            if (TownSceneManager.Instance != null)
            {
                TownSceneManager.Instance.TownInputSupport?.SetInput(true);
                TownSceneManager.Instance.SetTownInput(CameraManager.Instance.TownCamera);
            }


            if (townSceneService != null)
            {
                await townSceneService.PlayLobbyMenu(true);
            }

            await CameraManager.Instance.WaitCinemachineClearShotBlend();
            CameraManager.Instance?.SetEnableBrain(false);

            // 15. BGM 재생
            TownSceneManager.Instance?.PlayBGM();


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