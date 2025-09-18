using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using SkillSystem;
using System.Linq;

/// <summary>
/// 스킬 사용 커맨드 - 새로운 스킬 시스템용
/// </summary>
public class UseSkillCommand : IBattleCommand
{
    public string CommandName => "UseSkill";

    protected AdvancedSkillData skillData;
    protected List<BattleActor> targets;

    /// <summary>
    /// 새로운 스킬 데이터 기반 생성자
    /// </summary>
    public UseSkillCommand(AdvancedSkillData skillData, List<BattleActor> targets)
    {
        this.skillData = skillData;
        this.targets = targets;
    }

    /// <summary>
    /// 스킬 ID 기반 생성자 (호환성)
    /// </summary>
    public UseSkillCommand(int skillId, List<BattleActor> targets)
    {
        // 스킬 ID로 AdvancedSkillData 로드
        this.skillData = LoadSkillDataById(skillId);
        this.targets = targets;
    }

    public virtual bool CanExecute(CommandContext context)
    {
        if (context.Actor == null)
            return false;

        // 스킬 데이터 체크
        if (skillData == null)
        {
            Debug.LogError("[UseSkillCommand] Skill data is null");
            return false;
        }

        // MP 체크
        /* if (context.Actor.BattleActorInfo.Mp < skillData.manaCost)
         {
             Debug.Log($"[UseSkillCommand] Not enough MP. Need: {skillData.manaCost}, Have: {context.Actor.BattleActorInfo.Mp}");
             return false;
         }*/

        // 쿨다운 체크
        if (context.Actor.CooldownManager != null)
        {
            if (!context.Actor.CooldownManager.CanUseSkill(skillData.skillId))
            {
                int remaining = context.Actor.CooldownManager.GetRemainingCooldown(skillData.skillId);
                Debug.Log($"[UseSkillCommand] Skill on cooldown. {remaining} turns remaining");
                return false;
            }
        }



        // 침묵 체크
        if (context.Actor.SkillManager != null)
        {
            if (context.Actor.SkillManager.HasSkill(SkillSystem.StatusType.Silence))
            {
                Debug.Log("[UseSkillCommand] Cannot use skill - Silenced");
                return false;
            }

            // 기절 체크
            if (context.Actor.SkillManager.HasSkill(SkillSystem.StatusType.Stun))
            {
                Debug.Log("[UseSkillCommand] Cannot use skill - Stunned");
                return false;
            }
        }

        // 쿨다운 체크 (구현 필요시)
        // if (IsOnCooldown(skillData.skillId))
        //     return false;

        return true;
    }

    public virtual async UniTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var result = new CommandResult(true, $"Skill {skillData.skillName} executed");
        result.Effects.Add($"Used {skillData.skillName}");

        try
        {
            // MP 소모
            //  context.Actor.BattleActorInfo.UseMP(skillData.manaCost);

            // 쿨다운 시작
            if (context.Actor.CooldownManager != null && skillData.cooldown > 0)
            {
                context.Actor.CooldownManager.UseSkill(skillData);
                result.Effects.Add($"Cooldown: {skillData.cooldown} turns");
            }



            // 스킬 효과 적용
            ApplySkillEffects(context.Actor);

            // 스킬 애니메이션 재생
            await PlaySkillAnimation(context.Actor, cancellationToken);

            // 성공 로그
            Debug.Log($"[UseSkillCommand] {context.Actor.name} used {skillData.skillName}");

            // 결과에 효과 정보 추가
            foreach (var effect in skillData.effects)
            {
                result.Effects.Add(GetEffectDescription(effect));
            }

            // 쿨다운 설정 (구현 필요시)
            // SetCooldown(skillData.skillId, skillData.cooldown);
        }
        catch (System.Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to execute skill: {ex.Message}";
            Debug.LogError($"[UseSkillCommand] Error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// 스킬 효과 적용
    /// </summary>
    private void ApplySkillEffects(BattleActor caster)
    {
        // 타겟 검증
        var validTargets = targets.Where(t => t != null && !t.IsDead).ToList();

        if (validTargets.Count == 0)
        {
            Debug.LogWarning("[UseSkillCommand] No valid targets");
            return;
        }

        // 각 타겟에 스킬 적용
        foreach (var target in validTargets)
        {
            if (caster.SkillManager != null)
            {
                caster.SkillManager.ApplySkill(skillData, caster, target);
            }
            else
            {
                Debug.LogError($"[UseSkillCommand] {caster.name} has no SkillManager");
            }
        }
    }

    /// <summary>
    /// 스킬 애니메이션 재생
    /// </summary>
    private async UniTask PlaySkillAnimation(BattleActor caster, CancellationToken cancellationToken)
    {
        // 스킬 애니메이션 설정
        caster.SetAnimation(BattleActorAnimation.Skill);

        // 애니메이션 시간 대기
        float animationTime = 1.0f; // 기본 1초

        // 스킬 데이터에 애니메이션 시간이 있으면 사용
        // if (skillData.animationDuration > 0)
        //     animationTime = skillData.animationDuration;

        await UniTask.Delay(System.TimeSpan.FromSeconds(animationTime), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 효과 설명 생성
    /// </summary>
    private string GetEffectDescription(AdvancedSkillEffect effect)
    {
        return effect.type switch
        {
            SkillSystem.EffectType.Damage => $"Deal {effect.value}% damage",
            SkillSystem.EffectType.Heal => $"Heal {effect.value}% HP",
            SkillSystem.EffectType.StatusEffect => $"Apply {effect.statusType}",
            SkillSystem.EffectType.Buff => $"Buff {effect.statType} by {effect.value}%",
            SkillSystem.EffectType.Debuff => $"Debuff {effect.statType} by {effect.value}%",
            SkillSystem.EffectType.Shield => $"Grant {effect.value}% shield",
            _ => effect.type.ToString()
        };
    }

    public async UniTask UndoAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        // 스킬 취소는 일반적으로 불가능
        // MP 복구 정도만 가능
       // context.Actor.BattleActorInfo.AddMp(skillData.manaCost);

       // Debug.Log($"[UseSkillCommand] Undone - MP restored: {skillData.manaCost}");

        await UniTask.CompletedTask;
    }

    /// <summary>
    /// 스킬 ID로 데이터 로드 (임시 구현)
    /// </summary>
    private AdvancedSkillData LoadSkillDataById(int skillId)
    {
        // 실제로는 데이터베이스나 ScriptableObject에서 로드
        // 임시로 기본 스킬 생성

        Debug.LogWarning($"[UseSkillCommand] Creating temporary skill data for ID: {skillId}");

        return new AdvancedSkillData
        {
            skillId = skillId,
            skillName = $"Skill_{skillId}",
            description = "Temporary skill",
            category = SkillSystem.SkillCategory.Active,
            effects = new List<AdvancedSkillEffect>
            {
                new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Damage,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    value = 100,
                    chance = 1f
                }
            }
        };
    }
}

/// <summary>
/// 스킬 커맨드 팩토리 - 편의 메서드
/// </summary>
public static class SkillCommandFactory
{
    /// <summary>
    /// 데미지 스킬 커맨드 생성
    /// </summary>
    public static UseSkillCommand CreateDamageSkill(float damagePercent, BattleActor target)
    {
        var skillData = new AdvancedSkillData
        {
            skillId = 9001,
            skillName = "Quick Strike",
            effects = new List<AdvancedSkillEffect>
            {
                new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Damage,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    value = damagePercent,
                    chance = 1f
                }
            }
        };

        return new UseSkillCommand(skillData, new List<BattleActor> { target });
    }

    /// <summary>
    /// 힐 스킬 커맨드 생성
    /// </summary>
    public static UseSkillCommand CreateHealSkill(float healPercent, BattleActor target)
    {
        var skillData = new AdvancedSkillData
        {
            skillId = 9002,
            skillName = "Healing Light",
            effects = new List<AdvancedSkillEffect>
            {
                new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Heal,
                    targetType = SkillSystem.TargetType.AllySingle,
                    value = healPercent,
                    chance = 1f
                }
            }
        };

        return new UseSkillCommand(skillData, new List<BattleActor> { target });
    }

    /// <summary>
    /// 상태이상 스킬 커맨드 생성
    /// </summary>
    public static UseSkillCommand CreateStatusSkill(SkillSystem.StatusType statusType, int duration, BattleActor target)
    {
        var skillData = new AdvancedSkillData
        {
            skillId = 9003,
            skillName = $"Apply {statusType}",
            effects = new List<AdvancedSkillEffect>
            {
                new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.StatusEffect,
                    targetType = SkillSystem.TargetType.EnemySingle,
                    statusType = statusType,
                    duration = duration,
                    chance = 0.8f
                }
            }
        };

        return new UseSkillCommand(skillData, new List<BattleActor> { target });
    }

    /// <summary>
    /// AOE 스킬 커맨드 생성
    /// </summary>
    public static UseSkillCommand CreateAOESkill(float damagePercent, List<BattleActor> targets)
    {
        var skillData = new AdvancedSkillData
        {
            skillId = 9004,
            skillName = "Meteor Storm",
            effects = new List<AdvancedSkillEffect>
            {
                new AdvancedSkillEffect
                {
                    type = SkillSystem.EffectType.Damage,
                    targetType = SkillSystem.TargetType.EnemyAll,
                    value = damagePercent,
                    chance = 1f
                }
            }
        };

        return new UseSkillCommand(skillData, targets);
    }
}