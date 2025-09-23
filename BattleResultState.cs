using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using System.Collections.Generic;
using UnityEngine;

public class BattleResultState : ServicedTownStateBase
{
    public override string StateName => "BattleResult";

    public BattleResultState(IServiceContainer container = null) : base(container) { }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Processing battle result");

        // 전투 결과 처리
      /*  var battleResult = context.GetParameter<BattleResult>("Result");

        if (battleResult != null && battleResult.IsVictory)
        {
            // 보상 처리
            var rewards = context.GetParameter<List<Goods>>("Rewards");
            if (rewards != null && rewards.Count > 0)
            {
                await ShowRewardUI(rewards);
            }
        }
      */

        // 타운 복귀
        if (TownSceneService != null)
        {
            await TownSceneService.ShowTown(true);
        }

        // 전투 씬 언로드
        if (ResourceService != null)
        {
            await ResourceService.UnLoadSceneAsync("Battle");
        }
    }

    private async UniTask ShowRewardUI(List<Goods> rewards)
    {
        if (UIService != null)
        {
            var controller = UIService.GetController(UIType.RewardPopup);
            if (controller != null)
            {
                await UIService.EnterAsync(controller);
            }
        }
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        return nextState != FlowState.Battle;
    }
}