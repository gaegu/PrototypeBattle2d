using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;
using UnityEngine;

public class BattleNewState : ServicedTownStateBase
{
    public override string StateName => "Battle";
    public BattleNewState(IServiceContainer container = null) : base(container)
    {
    }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        var battleInfo = context.GetParameter<BattleInfo>("BattleInfo");

        // 전투는 복잡하므로 서비스 사용
        if (PlayerService != null)
        {
            // 서비스를 통한 처리
           // await PlayerService.SavePlayerState();
        }

        // BattleFlow 전환
        await FlowManager.Instance.ChangeFlow(FlowType.BattleFlow);
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
        // 전투 중에는 Home으로만 전환 가능
        return nextState == FlowState.Home;
    }
}