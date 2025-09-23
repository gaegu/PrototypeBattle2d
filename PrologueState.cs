using Cysharp.Threading.Tasks;
using UnityEngine;

public class PrologueState : ServicedTownStateBase
{
    public override string StateName => "Prologue";

    public PrologueState(IServiceContainer container = null) : base(container) { }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Starting prologue");

        // 프롤로그 매니저 시작
        /*if (PrologueManager.Instance != null)
        {
            await PrologueManager.Instance.();
        }*/


        // UI 숨기기
        if (TownSceneService != null)
        {
            await TownSceneService.PlayLobbyMenu(false);
        }
    }

    public override async UniTask Exit()
    {
        // UI 복원
        if (TownSceneService != null)
        {
            await TownSceneService.PlayLobbyMenu(true);
        }

        await base.Exit();
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // 프롤로그 중에는 제한적 전환
        return nextState == FlowState.Tutorial ||
               nextState == FlowState.None;
    }
}