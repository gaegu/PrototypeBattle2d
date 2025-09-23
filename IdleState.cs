using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class IdleState : TownStateBase
{
    public override string StateName => "Idle";

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Entering idle state");
        await UniTask.Yield();
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // Idle에서는 모든 상태로 전환 가능
        return true;
    }
}