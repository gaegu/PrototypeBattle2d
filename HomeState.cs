using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class HomeState : TownStateBase
{
    public override string StateName => "Home";

    protected override async UniTask OnEnter(TownStateContext context)
    {
        // 단순 작업은 그냥 싱글톤 사용 (점진적 전환)
        UIManager.Instance.Home();
        TownObjectManager.Instance.SetConditionRoad();
        await UtilModel.Resources.UnloadUnusedAssets(true);
        await UIManager.Instance.BackToTarget(UIType.LobbyView, false);
    }

    public override async UniTask Execute(TownStateContext context)
    {
        await UniTask.Yield();
    }

    public override async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        return true;
    }
}