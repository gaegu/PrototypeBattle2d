using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using SkillSystem;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class UseSkillCommandWithBP : UseSkillCommand
{
    private bool usesBP;
    private int bpAmount;

    public UseSkillCommandWithBP(AdvancedSkillData skillData, List<BattleActor> targets, bool usesBP = false, int bpAmount = 0)
        : base(skillData, targets)
    {
        this.usesBP = usesBP;
        this.bpAmount = bpAmount;
    }

    public override bool CanExecute(CommandContext context)
    {
        if (!base.CanExecute(context))
            return false;

        // BP 체크
        if (usesBP && context.Actor.BPManager != null)
        {
            if (context.Actor.BPManager.CurrentBP < bpAmount)
            {
                Debug.Log($"[UseSkillCommandWithBP] Not enough BP. Need {bpAmount}, have {context.Actor.BPManager.CurrentBP}");
                return false;
            }
        }

        return true;
    }

    public override async UniTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // BP 사용 및 업그레이드
        if (usesBP && context.Actor.BPManager != null)
        {
            context.Actor.BPManager.UpgradeSkillAuto(skillData.skillId);

            // 업그레이드된 스킬 데이터로 교체
            skillData = context.Actor.BPManager.GetUpgradedSkillData(skillData.skillId);
        }

        // 기본 실행
        return await base.ExecuteAsync(context, cancellationToken);
    }
}