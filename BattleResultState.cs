using Cysharp.Threading.Tasks;
using UnityEngine;

public class BattleResultState : ITownState
{
    public string StateName => "BattleResult";

    public async UniTask Enter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Enter - Processing battle results");

        // 전투 결과 처리 로직
        var battleInfo = context.Model.BattleInfo;
        if (battleInfo != null)
        {
            // 전투 보상 처리
            // UI 표시 등
        }

        // 타운으로 돌아오기
        await TransitionToTown(context);
    }

    private async UniTask TransitionToTown(TownStateContext context)
    {
        // 타운 복귀 로직
        context.Model.SetBattleInfo(null);  // 전투 정보 클리어
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
        // 전투 결과에서는 Home이나 None으로만 전환
        return nextState == FlowState.Home || nextState == FlowState.None;
    }
}