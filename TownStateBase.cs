using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class TownStateBase : ITownState
{
    public abstract string StateName { get; }
    public virtual bool IsInterruptible => true;
    public virtual int Priority => 0;

    protected TownStateContext stateContext;

    public async UniTask Enter(TownStateContext context)
    {
        stateContext = context;
        Debug.Log($"[{StateName}] Enter");

        try
        {
            await OnEnter(context);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{StateName}] Enter failed: {e}");
            throw;
        }
    }

    public virtual async UniTask Execute(TownStateContext context)
    {
        await OnExecute(context);
    }

    public virtual async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");

        try
        {
            await OnExit();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{StateName}] Exit failed: {e}");
            throw;
        }
        finally
        {
            stateContext = null;
        }
    }

    public abstract bool CanTransitionTo(FlowState nextState);

    protected abstract UniTask OnEnter(TownStateContext context);
    protected virtual async UniTask OnExecute(TownStateContext context)
    {
        await UniTask.Yield();
    }
    protected virtual async UniTask OnExit()
    {
        await UniTask.Yield();
    }
}