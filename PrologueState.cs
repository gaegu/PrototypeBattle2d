using Cysharp.Threading.Tasks;
using UnityEngine;

public class PrologueState : ITownState
{
    public string StateName => "Prologue";

    public async UniTask Enter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Enter - Starting prologue");

        // 프롤로그 관련 처리
        if (PrologueManager.Instance.IsProgressing)
        {
            // 프롤로그 진행 로직
            await HandlePrologueSequence(context);
        }
    }

    private async UniTask HandlePrologueSequence(TownStateContext context)
    {
        // 프롤로그 시퀀스 처리
        Debug.Log($"[{StateName}] Handling prologue sequence");
    }

    public async UniTask Execute(TownStateContext context)
    {
        await UniTask.Yield();
    }

    public async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");
    }

    public bool CanTransitionTo(FlowState nextState)
    {
        // 프롤로그 중에는 제한적 전환만 허용
        return nextState == FlowState.None ||
               nextState == FlowState.Tutorial ||
               nextState == FlowState.Home;
    }
}