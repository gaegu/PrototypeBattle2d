using Cysharp.Threading.Tasks;
using UnityEngine;

public class HomeState : ServicedTownStateBase
{
    public override string StateName => "Home";

    public HomeState(IServiceContainer container = null) : base(container) { }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Returning to home");

        // UI 초기화
        if (UIService != null)
        {
            UIService.Home();
        }

        // 타운 오브젝트 갱신
        if (TownObjectService != null)
        {
            TownObjectService.SetConditionRoad();
        }

        // 메모리 정리
        if (ResourceService != null)
        {
            await ResourceService.UnloadUnusedAssets(true);
        }
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        return true;
    }
}