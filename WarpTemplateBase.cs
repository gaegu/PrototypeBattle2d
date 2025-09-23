using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using UnityEngine;

public abstract class WarpTemplateBase : TransitionTemplate
{
    protected TownFlowModel.WarpInfo warpInfo;

    public WarpTemplateBase(TownFlowModel.WarpInfo warpInfo, TownFlowModel model)
    {
        this.warpInfo = warpInfo;
        this.model = model;
        this.transitionType = TransitionManager.ConvertTransitionType(warpInfo.transition);
    }

    protected override async UniTask BeforeTransition()
    {
        // 워프 전 공통 처리
        model.SetWarpProcess(true);
        TownSceneManager.Instance.TownInputSupport.SetInput(false);
        PlayerManager.Instance.MyPlayer.SetInteracting(true);

        await base.BeforeTransition();
    }

    protected override async UniTask DoWork()
    {
        // 워프 실행 공통 로직
        await OnEventWarpHome();
        await ExecuteWarp();
    }

    protected override async UniTask AfterTransition()
    {
        // 워프 후 공통 처리
        model.SetWarpProcess(false);
        PlayerManager.Instance.MyPlayer.SetInteracting(false);
        TownSceneManager.Instance.ShowFieldMapName(model.CurrentFiledMapDefine);
        ObserverManager.NotifyObserver(FlowObserverID.OnEndChangeScene, null);

        await base.AfterTransition();
    }

    private async UniTask OnEventWarpHome()
    {
        UIManager.Instance.Home();
        TownObjectManager.Instance.SetConditionRoad();
        await UtilModel.Resources.UnloadUnusedAssets(true);
    }

    protected abstract UniTask ExecuteWarp();  // 각 워프 타입별 구현
}