using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SkillSystem;
using IronJade.Observer.Core;
using System;
using static BattleFormularHelper;
using Cysharp.Threading.Tasks;

public class ReflectInstance
{
    public float ReflectPercent { get; set; }
    public int RemainingTurns { get; set; }
    public BattleActor Source { get; set; }
    public int MaxReflectDamage { get; set; }
    public int TotalReflected { get; set; }
}


public partial class AdvancedSkillRuntime : MonoBehaviour
{
    // Reflectインスタンス管理用
    private Dictionary<BattleActor, List<ReflectInstance>> activeReflects = new Dictionary<BattleActor, List<ReflectInstance>>();



    // FixedDamage 적용 메서드 추가
    protected virtual void ApplyFixedDamage(AdvancedSkillEffect effect, BattleActor target)
    {
        // value를 그대로 데미지로 사용 (퍼센트가 아닌 고정값)
        int fixedDamage = Mathf.RoundToInt(effect.value);

        // 추가 배율이 있는 경우 적용
        if (effect.fixedDamageMultiplier > 0)
        {
            fixedDamage = Mathf.RoundToInt(fixedDamage * effect.fixedDamageMultiplier);
        }

        // 최소/최대값 제한
        if (effect.minFixedDamage > 0)
        {
            fixedDamage = Mathf.Max(effect.minFixedDamage, fixedDamage);
        }
        if (effect.maxFixedDamage > 0)
        {
            fixedDamage = Mathf.Min(effect.maxFixedDamage, fixedDamage);
        }

        Debug.Log($"[FixedDamage] {Sender.name} deals {fixedDamage} fixed damage to {target.name}");

        // 고정 데미지 결과 생성
        var result = new BattleFormularHelper.DamageResult
        {
            FinalDamage = fixedDamage,
            OriginalDamage = fixedDamage,
            Judgement = BattleFormularHelper.JudgementType.Hit,
            IsCritical = false,  // 고정 데미지는 크리티컬 불가
            IsMissed = false,
            IsDodged = false,
            IsBlocked = false,
            IsResisted = false,
            IsImmune = false,
            Options = BattleFormularHelper.DamageOption.IgnoreDefense  // 방어 무시
        };

        // Shield는 고정 데미지도 막을 수 있음 (옵션에 따라)
        if (!effect.ignoreShield && target.BattleActorInfo.Shield > 0)
        {
            int shieldAbsorbed = Mathf.Min(target.BattleActorInfo.Shield, fixedDamage);
            target.BattleActorInfo.Shield -= shieldAbsorbed;
            result.FinalDamage -= shieldAbsorbed;
            result.ShieldAbsorbed = shieldAbsorbed;

            Debug.Log($"[FixedDamage] Shield absorbed {shieldAbsorbed} damage");
        }

        // 데미지 적용
        target.TakeDamageWithResult(result, Sender).Forget();
    }


    // LifeSteal 적용 메서드 추가
    protected virtual void ApplyLifeSteal(AdvancedSkillEffect effect, BattleActor target)
    {
        // 1. Context 설정 (LifeSteal 정보 포함)
        var context = new BattleFormularHelper.BattleContext
        {
            Attacker = Sender,
            Defender = target,
            SkillPower = (int)effect.GetSkillPower(),
            AttackType = Sender.BattleActorInfo.AttackType,
            IsSkillAttack = true,
            DamageOptions = BattleFormularHelper.DamageOption.None,
            ExtraParams = new Dictionary<string, object>
        {
            { "LifeStealPercent", effect.value }  // 흡혈률 전달
        }
        };

        // 2. 데미지 계산 (LifeSteal 포함)
        var damageResult = BattleFormularHelper.CalculateDamage(context);

        // 3. 타겟에 데미지 적용 (TakeDamageWithResult에서 자동으로 흡혈 처리됨)
        target.TakeDamageWithResult(damageResult, Sender).Forget();

        Debug.Log($"[LifeSteal] Applied - Damage: {damageResult.FinalDamage}, Healed: {damageResult.LifeStealAmount}");
    }

    // 흡혈량 계산
    private int CalculateLifeStealAmount(int damageDealt, AdvancedSkillEffect effect)
    {
        if (damageDealt <= 0) return 0;

        // 기본 흡혈률 (effect.value = 흡혈 퍼센트)
        float lifeStealPercent = effect.value;

        // 시전자의 추가 흡혈 스탯 적용 (있다면)
        float additionalLifeSteal = Sender.BattleActorInfo.GetStatFloat(CharacterStatType.LifeSteal);
        lifeStealPercent += additionalLifeSteal;

        // 흡혈량 계산
        int healAmount = Mathf.RoundToInt(damageDealt * (lifeStealPercent / 100f));

        // 최소/최대 흡혈량 제한
        if (effect.minLifeSteal > 0)
        {
            healAmount = Mathf.Max(effect.minLifeSteal, healAmount);
        }
        if (effect.maxLifeSteal > 0)
        {
            healAmount = Mathf.Min(effect.maxLifeSteal, healAmount);
        }

        // 오버힐 제한 옵션
        if (!effect.allowOverheal)
        {
            int maxHeal = Sender.BattleActorInfo.MaxHp - Sender.BattleActorInfo.Hp;
            healAmount = Mathf.Min(healAmount, maxHeal);
        }

        return healAmount;
    }

    // 흡혈 회복 적용
    // 흡혈 회복 적용
    private void ApplyLifeStealHeal(int healAmount, AdvancedSkillEffect effect)
    {
        if (healAmount <= 0) return;

        // 힐 차단 상태 체크 (StatusEffect로 체크)
        if (Sender.SkillManager != null &&
            Sender.SkillManager.HasSkill(SkillSystem.StatusType.HealBlock))
        {
            Debug.Log($"[LifeSteal] {Sender.name} is heal blocked! Life steal failed.");

            // 힐 차단 이펙트 표시
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.ShowBattleText(Sender, BattleTextType.Block);
            }
            return;
        }

        // 힐 감소 체크 및 적용
        float healMultiplier = 1f;

        // activeBuffDebuffs에서 HealReduction 디버프 확인
        if (activeBuffDebuffs.ContainsKey(Sender))
        {
            var healReductionDebuff = activeBuffDebuffs[Sender]
                .FirstOrDefault(x => x.Effect.statType == SkillSystem.StatType.HealReduction);

            if (healReductionDebuff != null)
            {
                float reduction = healReductionDebuff.Effect.value / 100f;
                healMultiplier = Mathf.Max(0, 1f - reduction);
                Debug.Log($"[LifeSteal] Heal reduced by {reduction * 100}%");
            }
        }

        healAmount = Mathf.RoundToInt(healAmount * healMultiplier);

        // HP 회복
        int previousHP = Sender.BattleActorInfo.Hp;
        int maxHP = Sender.BattleActorInfo.MaxHp;

        // 오버힐 제한 체크
        if (!effect.allowOverheal)
        {
            int maxPossibleHeal = maxHP - previousHP;
            healAmount = Mathf.Min(healAmount, maxPossibleHeal);
        }

        // 실제 HP 회복 적용
        Sender.BattleActorInfo.Hp = Mathf.Min(maxHP, previousHP + healAmount);
        int actualHealed = Sender.BattleActorInfo.Hp - previousHP;

        // 시각 효과
        ShowLifeStealEffect(Sender, actualHealed).Forget();

        // 태그 UI 업데이트
        Sender.UpdateTag();

        Debug.Log($"[LifeSteal] {Sender.name} healed {actualHealed} HP (Now: {Sender.BattleActorInfo.Hp}/{maxHP})");
    }

    // LifeSteal 시각 효과 (수정)
    private async UniTask ShowLifeStealEffect(BattleActor target, int amount)
    {
        if (amount <= 0) return;

        // 힐 이펙트 재생
       /* if (BattleEffectManager.Instance != null)
        {
            // LifeStealHeal 이펙트가 없으면 일반 Heal 이펙트 사용
            string effectName = "LifeStealHeal";
            if (!BattleEffectManager.Instance.HasEffect(effectName))
            {
                effectName = "HealEffect";
            }

            await BattleEffectManager.Instance.PlayEffect(effectName, target);
        }

        // 힐 수치 표시
        if (BattleUIManager.Instance != null)
        {
            // ShowHealNumber 메서드가 있는지 확인, 없으면 ShowDamageNumber 사용
            BattleUIManager.Instance.ShowDamageNumber(
                target,
                amount,
                false,  // 크리티컬 아님
                true    // 힐 표시 (녹색)
            );
        }*/


        // 추가 텍스트 표시 (옵션)
        await UniTask.Delay(100);
    }



    // Reflect適用メソッド追加
    protected virtual void ApplyReflect(AdvancedSkillEffect effect, BattleActor target)
    {
        Debug.Log($"[Reflect] Applying {effect.value}% damage reflection to {target.name} for {effect.duration} turns");

        // Reflectインスタンス作成
        var reflectInstance = new ReflectInstance
        {
            ReflectPercent = effect.value,
            RemainingTurns = effect.duration,
            Source = Sender,
            MaxReflectDamage = effect.maxReflectDamage > 0 ? effect.maxReflectDamage : int.MaxValue
        };

        // Reflectバフとして登録
        if (!activeReflects.ContainsKey(target))
            activeReflects[target] = new List<ReflectInstance>();

        activeReflects[target].Add(reflectInstance);

        // ビジュアルエフェクト
        ShowReflectEffect(target, effect).Forget();
    }

    // Reflect削除メソッド
    protected virtual void RemoveReflect(AdvancedSkillEffect effect, BattleActor target)
    {
        if (activeReflects.ContainsKey(target))
        {
            activeReflects[target].Clear();
            Debug.Log($"[Reflect] Removed reflection from {target.name}");
        }
    }

    // Reflectエフェクト表示
    private async UniTask ShowReflectEffect(BattleActor target, AdvancedSkillEffect effect)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("ReflectApply", target);
        }
    }


    // 새 메서드 추가
    protected virtual void ApplyDamageReduction(AdvancedSkillEffect effect, BattleActor target)
    {
        // 999턴은 영구 지속
        if (effect.duration == 999 || effect.duration == 0)
        {
            target.BattleActorInfo.DamageReductionPercent = effect.value;
            Debug.Log($"[Skill] {target.name}에게 피해 감소 {effect.value}% 적용 (방어막 보유시)");
        }
        else
        {
            // 지속시간이 있는 경우는 버프 시스템으로 처리
            Debug.LogWarning("[DamageReduction] Duration-based damage reduction not yet implemented");
        }
    }

    // SkillDisrupt 적용 메서드
    private void ApplySkillDisrupt(AdvancedSkillEffect effect, BattleActor target)
    {
        if (target == null || target.IsDead) return;

        // 방해 타입 결정 (effect.value로 타입 구분)
        int disruptType = (int)effect.value;

        switch (disruptType)
        {
            case 0: // 침묵 (스킬 사용 불가)
                ApplySilence(target, effect.duration);
                break;

            case 1: // 쿨다운 증가
                IncreaseCooldowns(target, effect.duration);
                break;

            case 2: // 쿨다운 리셋 (아군용)
                ResetCooldowns(target);
                break;

            case 3: // 스킬 봉인 (특정 스킬만 사용 불가)
                SealSkills(target, effect.duration);
                break;

            default:
                ApplySilence(target, effect.duration); // 기본값은 침묵
                break;
        }

        Debug.Log($"[SkillDisrupt] Applied skill disruption type {disruptType} to {target.name}");
    }

    // 침묵 적용
    private void ApplySilence(BattleActor target, int duration)
    {
        // StatusFlag 적용
        target.BattleActorInfo.AddStatusFlag(StatusFlag.CannotUseSkill);

        // Silence 상태이상 추가
        var silenceEffect = new AdvancedSkillEffect
        {
            type = SkillSystem.EffectType.StatusEffect,
            statusType = SkillSystem.StatusType.Silence,
            duration = duration,
            targetType = SkillSystem.TargetType.Self
        };

        ApplyStatusEffect(silenceEffect, target);

        Debug.Log($"[SkillDisrupt] {target.name} is silenced for {duration} turns!");

        // 시각 효과
        ShowSkillDisruptEffect(target, "Silence").Forget();
    }

    // 쿨다운 증가
    private void IncreaseCooldowns(BattleActor target, int amount)
    {
        if (target.CooldownManager != null)
        {
            target.CooldownManager.IncreaseCooldowns(amount);
            Debug.Log($"[SkillDisrupt] Increased all cooldowns by {amount} for {target.name}");
        }

        // 시각 효과
        ShowSkillDisruptEffect(target, "CooldownUp").Forget();
    }

    // 쿨다운 리셋 (아군 지원용)
    private void ResetCooldowns(BattleActor target)
    {
        if (target.CooldownManager != null)
        {
            target.CooldownManager.ResetAllCooldowns();
            Debug.Log($"[SkillDisrupt] Reset all cooldowns for {target.name}");
        }

        // 침묵 상태 제거 (있다면)
        if (target.SkillManager != null)
        {
            var silenceSkill = target.SkillManager.GetSkillByType(SkillSystem.StatusType.Silence);
            if (silenceSkill != null)
            {
                target.SkillManager.RemoveSkill(silenceSkill);
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.CannotUseSkill);
            }
        }

        // 시각 효과
        ShowSkillDisruptEffect(target, "CooldownReset").Forget();
    }

    // 스킬 봉인
    private void SealSkills(BattleActor target, int duration)
    {
        // SkillSeal 상태이상 추가
        var sealEffect = new AdvancedSkillEffect
        {
            type = SkillSystem.EffectType.StatusEffect,
            statusType = SkillSystem.StatusType.SkillSeal,
            duration = duration,
            targetType = SkillSystem.TargetType.Self
        };

        ApplyStatusEffect(sealEffect, target);

        // 특정 스킬들만 봉인하려면 여기에 추가 로직
        target.BattleActorInfo.AddStatusFlag(StatusFlag.CannotUseSkill);

        Debug.Log($"[SkillDisrupt] {target.name}'s skills are sealed for {duration} turns!");

        // 시각 효과
        ShowSkillDisruptEffect(target, "SkillSeal").Forget();
    }

    // 시각 효과
    private async UniTask ShowSkillDisruptEffect(BattleActor target, string effectType)
    {
        if (BattleEffectManager.Instance != null)
        {
            string effectName = $"SkillDisrupt_{effectType}";
            await BattleEffectManager.Instance.PlayEffect(
                effectName,
                target
            );
        }

        // UI 텍스트 표시
        if (BattleUIManager.Instance != null)
        {
            BattleTextType textType = effectType switch
            {
                "Silence" => BattleTextType.Silence, // 또는 새로운 타입 추가
                "CooldownUp" => BattleTextType.CooldownUp,
                "CooldownReset" => BattleTextType.CooldownReset,
                "SkillSeal" => BattleTextType.SkillSeal,
                _ => BattleTextType.Resist
            };

            BattleUIManager.Instance.ShowBattleText(target, textType);
        }
    }



}
