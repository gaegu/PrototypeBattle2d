using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class TownStateBase : ITownState
{
    public abstract string StateName { get; }

    // 기본 구현 (서비스 불필요한 State용)
    public virtual async UniTask Enter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Enter");
        await OnEnter(context);
    }

    protected abstract UniTask OnEnter(TownStateContext context);

    public virtual async UniTask Execute(TownStateContext context)
    {
        await UniTask.Yield();
    }

    public virtual async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");
    }

    public virtual bool CanTransitionTo(FlowState nextState)
    {
        return true;
    }
}