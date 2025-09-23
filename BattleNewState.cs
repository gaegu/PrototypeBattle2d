using Cysharp.Threading.Tasks;
using UnityEngine;

public class BattleNewState : ServicedTownStateBase
{
    public override string StateName => "Battle";

    public BattleNewState(IServiceContainer container = null) : base(container) { }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Entering battle");

        var battleInfo = context.GetParameter<BattleInfo>("BattleInfo");
        if (battleInfo == null)
        {
            Debug.LogError($"[{StateName}] No battle info provided");
            return;
        }

        // 전투 씬 로드
        if (ResourceService != null)
        {
            await ResourceService.LoadSceneAsync("Battle", UnityEngine.SceneManagement.LoadSceneMode.Additive);
        }

        // 타운 숨기기
        if (TownSceneService != null)
        {
            await TownSceneService.ShowTown(false);
        }

        // 전투 Flow로 전환
        await FlowManager.Instance.ChangeFlow(FlowType.BattleFlow);
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // 전투 중에는 특정 상태로만 전환
        return nextState == FlowState.BattleResult ||
               nextState == FlowState.None;
    }
}