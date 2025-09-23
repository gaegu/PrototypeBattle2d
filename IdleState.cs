using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class IdleState : TownStateBase
{
    public override string StateName => "Idle";

    protected override async UniTask OnEnter(TownStateContext context)
    {
        // 단순 UI 작업만 (싱글톤 직접 사용 OK)
        await UIManager.Instance.EnterAsync(UIType.LobbyView);
        await TownSceneManager.Instance.PlayLobbyMenu(true);
        TownSceneManager.Instance.ShowFieldMapName(context.Model.CurrentFiledMapDefine);
    }

    public override async UniTask Execute(TownStateContext context)
    {
        // Idle 상태에서 주기적으로 실행할 로직
        await UniTask.Yield();
    }

    public override async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");

        // 로비 메뉴 숨기기
        await TownSceneManager.Instance.PlayLobbyMenu(false);
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // Idle에서는 모든 State로 전환 가능
        return true;
    }
}