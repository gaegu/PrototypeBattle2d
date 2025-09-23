using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TownWarpModule : ITownWarpModule
{
    private TownFlowModel model;
    private IServiceContainer serviceContainer;

    public bool IsActive { get; private set; }

    public TownWarpModule(IServiceContainer container = null)
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

    public async UniTask ProcessWarp(TownFlowModel.WarpInfo warpInfo)
    {
        Debug.Log($"[TownWarpModule] Processing warp to: {warpInfo.targetDataFieldMapEnumId}");
        IsActive = true;

        var playerService = serviceContainer?.GetService<IPlayerService>();
        var resourceService = serviceContainer?.GetService<IResourceService>();
        var townSceneService = serviceContainer?.GetService<ITownSceneService>();

        try
        {
            // 1. 트랜지션 시작
            if (TransitionManager.Instance != null)
            {
                await TransitionManager.In( TransitionManager.ConvertTransitionType(warpInfo.transition) );
            }


            // 2. 현재 씬 언로드
            string currentScenePath = model.GetScenePath(model.CurrentScene);
            if (resourceService != null && !string.IsNullOrEmpty(currentScenePath))
            {
                await resourceService.UnLoadSceneAsync(currentScenePath);
            }

            // 3. 새로운 씬 로드
            string targetScenePath = model.GetScenePath(warpInfo.targetDataFieldMapEnumId);
            if (resourceService != null && !string.IsNullOrEmpty(targetScenePath))
            {
                await resourceService.LoadSceneAsync(targetScenePath, LoadSceneMode.Additive);
            }

            // 4. 씬 변경 모델 업데이트
            model.SetCurrentScene(warpInfo.targetDataFieldMapEnumId);

            // 5. 플레이어 스폰
            if (playerService != null)
            {
                playerService.SetMyPlayerSpawn(
                    warpInfo.targetTownObjectType,
                    warpInfo.targetDataEnumId,
                    false,
                    true
                );
            }

            // 6. 타운 오브젝트 리프레시
            var townObjectService = serviceContainer?.GetService<ITownObjectService>();
            if (townObjectService != null)
            {
                await townObjectService.RefreshProcess(warpInfo.isForMission);
            }

            // 7. BGM 변경
            if (townSceneService != null)
            {
                townSceneService.PlayBGM();
            }

            // 1. 트랜지션 종료
            if (TransitionManager.Instance != null)
            {
                await TransitionManager.Out(TransitionManager.ConvertTransitionType(warpInfo.transition));
            }


            Debug.Log($"[TownWarpModule] Warp completed to: {warpInfo.targetDataFieldMapEnumId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TownWarpModule] Warp failed: {e}");
            throw;
        }
        finally
        {
            IsActive = false;
            model.SetWarpProcess(false);
        }
    }

    public async UniTask CleanUp()
    {
        IsActive = false;
        await UniTask.Yield();
    }

    public UniTask ProcessMissionWarp(TownFlowModel.WarpInfo warpInfo)
    {
        throw new System.NotImplementedException();
    }

    public UniTask ProcessDefaultWarp(TownFlowModel.WarpInfo warpInfo)
    {
        throw new System.NotImplementedException();
    }
}