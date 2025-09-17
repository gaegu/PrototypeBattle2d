using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SkillSystem;
using IronJade.Observer.Core;
using static BattleFormularHelper;
using Cysharp.Threading.Tasks;

/// <summary>
/// 특수 스킬 예시 1: 흡혈 스킬
/// 데미지를 입힌 만큼 회복하는 특수 로직
/// </summary>
public class VampiricSkillRuntime : AdvancedSkillRuntime
{
    private float vampiricRate = 0.3f; // 30% 흡혈

    protected override void OnInitialize()
    {
        // 첫 번째 효과의 value를 흡혈 비율로 사용
        if (SkillData.effects.Count > 0)
        {
            vampiricRate = SkillData.effects[0].value / 100f;
        }
    }

    protected override bool HandleCustomEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        if (effect.type == SkillSystem.EffectType.Special &&
            effect.specialEffect == "Vampiric")
        {
            // 데미지 계산
            var damage = CalculateDamageValue(100);
          //  target.TakeDamage(Sender, damage, DamageOption.None);

            // 흡혈 회복
            int healAmount = Mathf.RoundToInt(damage * vampiricRate);
       //     Sender.Heal(healAmount);

            Debug.Log($"[Vampiric] Dealt {damage} damage, healed {healAmount}");
            return true; // 커스텀 처리 완료
        }

        return false;
    }
}

/// <summary>
/// 특수 스킬 예시 2: 연쇄 번개
/// 타겟에서 타겟으로 튀는 특수 타겟팅
/// </summary>
public class ChainLightningSkillRuntime : AdvancedSkillRuntime
{
    private int chainCount = 3;
    private float damageReduction = 0.7f; // 연쇄마다 70%로 감소

    protected override List<BattleActor> GetCustomTargets(SkillSystem.TargetType targetType)
    {
      //  if (targetType != SkillSystem.TargetType.Special)
     //       return null;

        var targets = new List<BattleActor>();
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null)
            return targets;

        bool isAlly = Sender.BattleActorInfo.IsAlly;
        var enemies = isAlly ?
            battleManager.GetEnemyActors() :
            battleManager.GetAllyActors();

        // 첫 타겟
        BattleActor currentTarget = Receiver;
        if (currentTarget == null || currentTarget.IsDead)
        {
            currentTarget = BattleTargetSystem.Instance?.FindRandomTarget(enemies);
        }

        if (currentTarget == null)
            return targets;

        targets.Add(currentTarget);

        // 연쇄 타겟
        var usedTargets = new HashSet<BattleActor> { currentTarget };

        for (int i = 1; i < chainCount; i++)
        {
            // 가장 가까운 타겟 찾기 (아직 맞지 않은)
            var nextTarget = FindNearestTarget(currentTarget, enemies, usedTargets);
            if (nextTarget == null)
                break;

            targets.Add(nextTarget);
            usedTargets.Add(nextTarget);
            currentTarget = nextTarget;
        }

        return targets;
    }

    protected override void ApplyDamage(AdvancedSkillEffect effect, BattleActor target)
    {
        // 타겟 순서에 따라 데미지 감소
        var targets = ResolveTargets(effect.targetType);
        int index = targets.IndexOf(target);

        float damageMultiplier = Mathf.Pow(damageReduction, index);
        int damage = Mathf.RoundToInt(CalculateDamageValue(effect.value) * damageMultiplier);

       // target.TakeDamage(Sender, damage, DamageOption.None);

        Debug.Log($"[ChainLightning] Hit #{index + 1}: {target.name} for {damage} damage");
    }

    private BattleActor FindNearestTarget(BattleActor from, List<BattleActor> candidates, HashSet<BattleActor> exclude)
    {
        return candidates
            .Where(c => c != null && !c.IsDead && !exclude.Contains(c))
            .OrderBy(c => Vector3.Distance(from.transform.position, c.transform.position))
            .FirstOrDefault();
    }
}

/// <summary>
/// 특수 스킬 예시 3: 복수 스킬
/// HP가 낮을수록 데미지 증가
/// </summary>
public class RevengeSkillRuntime : AdvancedSkillRuntime
{
    protected override void ApplyDamage(AdvancedSkillEffect effect, BattleActor target)
    {
        // HP 비율 계산
        float hpPercent = Sender.BattleActorInfo.Hp / (float)Sender.BattleActorInfo.MaxHp;

        // HP가 낮을수록 데미지 증가 (최대 2배)
        float damageMultiplier = 2f - hpPercent;

        var context = new BattleFormularHelper.BattleContext
        {
            Attacker = Sender,
            Defender = target,
            SkillPower = Mathf.RoundToInt(effect.value * damageMultiplier),
            AttackType = Sender.BattleActorInfo.AttackType,
            IsSkillAttack = true,
            DamageOptions = DamageOption.None
        };

        var result = BattleFormularHelper.CalculateDamage(context);
      //  target.TakeDamage(Sender, result.FinalDamage, DamageOption.None);

        Debug.Log($"[Revenge] HP: {hpPercent:P}, Damage multiplier: {damageMultiplier:F2}");
    }
}

/// <summary>
/// 특수 스킬 예시 4: 카운터 스킬
/// 피격 시 자동 반격
/// </summary>
public class CounterSkillRuntime : AdvancedSkillRuntime
{
    protected override BattleNewObserverID[] UseObserverIds => new[]
    {
        BattleNewObserverID.Damaged,
        BattleNewObserverID.Kill
    };

    protected override void OnHandleMessage(System.Enum observerMessage, IObserverParam observerParam)
    {
        if (observerMessage is BattleNewObserverID battleId &&
            battleId == BattleNewObserverID.Damaged)
        {
            if (observerParam is BattleParam param &&
                param.DamageReceiver == Receiver.BattleActorInfo.ID) // 임시 
            {
                // 공격자에게 반격
                var attacker = GetActorBySlotIndex(param.DamageAttacker);
                if (attacker != null && !attacker.IsDead)
                {
                    PerformCounter(attacker);
                }
            }
        }
    }

    private void PerformCounter(BattleActor attacker)
    {
        // 카운터 확률 체크
        if (UnityEngine.Random.value > SkillData.triggerChance)
            return;

        // 카운터 데미지 (원래 데미지의 50%)
        int counterDamage = CalculateDamageValue(50);
    //    attacker.TakeDamage(Receiver, counterDamage, DamageOption.None);

        Debug.Log($"[Counter] {Receiver.name} counters {attacker.name} for {counterDamage} damage!");
    }

    private BattleActor GetActorBySlotIndex(int slotIndex)
    {
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null)
            return null;

        return null;
      //  var allActors = battleManager.GetAllAliveActors();
      //  return allActors.FirstOrDefault(a =>
       //     a.BattleActorInfo.SlotIndex == slotIndex);
    }
}

/// <summary>
/// 특수 스킬 예시 5: 희생 스킬
/// 자신의 HP를 소모하여 아군 전체 힐
/// </summary>
public class SacrificeSkillRuntime : AdvancedSkillRuntime
{
    protected override void OnStartExtended()
    {
        // HP 20% 소모
        int hpCost = Mathf.RoundToInt(Sender.BattleActorInfo.MaxHp * 0.2f);

        // 자신의 HP가 충분한지 체크
        if (Sender.BattleActorInfo.Hp <= hpCost)
        {
            Debug.Log("[Sacrifice] Not enough HP to cast!");
            ForceEnd();
            return;
        }

        // HP 소모
      //  Sender.TakeDamage(Sender, hpCost, DamageOption.TrueDamage);

        Debug.Log($"[Sacrifice] {Sender.name} sacrificed {hpCost} HP");
    }

    protected override void ApplyHeal(AdvancedSkillEffect effect, BattleActor target)
    {
        // 희생한 HP에 비례한 힐
        float sacrificeBonus = 1f - (Sender.BattleActorInfo.Hp / (float)Sender.BattleActorInfo.MaxHp);
        int healAmount = Mathf.RoundToInt(CalculateHealValue(effect.value) * (1f + sacrificeBonus));

      //  target.Heal(healAmount);

        Debug.Log($"[Sacrifice] Healed {target.name} for {healAmount} (bonus: {sacrificeBonus:P})");
    }
}

/// <summary>
/// 특수 스킬 예시 6: 변신 스킬
/// 스탯을 일시적으로 크게 변경
/// </summary>
public class TransformSkillRuntime : AdvancedSkillRuntime
{
    private Dictionary<CharacterStatType, float> originalStats = new Dictionary<CharacterStatType, float>();

    protected override void OnStartExtended()
    {
        // 원래 스탯 저장
        originalStats[CharacterStatType.Attack] = Sender.BattleActorInfo.Attack;
        originalStats[CharacterStatType.Defence] = Sender.BattleActorInfo.Defence;
      //  originalStats[CharacterStatType.Speed] = Sender.BattleActorInfo.Speed;

        // 변신: 공격력 +100%, 방어력 -50%, 속도 +50%
        Sender.BattleActorInfo.AddStatBuffValue(CharacterStatType.Attack, 1.0f);
        Sender.BattleActorInfo.AddStatBuffValue(CharacterStatType.Defence, -0.5f);
    //    Sender.BattleActorInfo.AddStatBuffValue(CharacterStatType.Speed, 0.5f);

        Debug.Log($"[Transform] {Sender.name} transformed!");
    }

    protected override void OnEndExtended()
    {
        // 원래 스탯으로 복구
        Sender.BattleActorInfo.RemoveStatBuffValue(CharacterStatType.Attack, 1.0f);
        Sender.BattleActorInfo.RemoveStatBuffValue(CharacterStatType.Defence, -0.5f);
   //     Sender.BattleActorInfo.RemoveStatBuffValue(CharacterStatType.Speed, 0.5f);

        Debug.Log($"[Transform] {Sender.name} returned to normal");
    }
}

/// <summary>
/// 특수 스킬 예시 7: 랜덤 효과 스킬
/// 실행할 때마다 다른 효과 발동
/// </summary>
public class RandomEffectSkillRuntime : AdvancedSkillRuntime
{
    private enum RandomEffect
    {
        Damage,
        Heal,
        Stun,
        Buff,
        Debuff
    }

    protected override bool HandleCustomEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        if (effect.type != SkillSystem.EffectType.Special ||
            effect.specialEffect != "Random")
            return false;

        // 랜덤 효과 선택
        var randomEffect = (RandomEffect)UnityEngine.Random.Range(0, 5);

        switch (randomEffect)
        {
            case RandomEffect.Damage:
               // target.TakeDamage(Sender, CalculateDamageValue(150), DamageOption.None);
                Debug.Log($"[Random] Damage effect!");
                break;

            case RandomEffect.Heal:
              //  target.Heal(CalculateHealValue(50));
                Debug.Log($"[Random] Heal effect!");
                break;

            case RandomEffect.Stun:
                target.SetStunned(true);
                Debug.Log($"[Random] Stun effect!");
                break;

            case RandomEffect.Buff:
                target.BattleActorInfo.AddStatBuffValue(CharacterStatType.Attack, 0.5f);
                Debug.Log($"[Random] Buff effect!");
                break;

            case RandomEffect.Debuff:
                target.BattleActorInfo.AddStatBuffValue(CharacterStatType.Defence, -0.3f);
                Debug.Log($"[Random] Debuff effect!");
                break;
        }

        return true;
    }
}

/// <summary>
/// 특수 스킬 예시 8: 쿨다운 리셋 스킬
/// 아군의 스킬 쿨다운을 초기화
/// </summary>
public class CooldownResetSkillRuntime : AdvancedSkillRuntime
{
    protected override bool HandleCustomEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        if (effect.type != SkillSystem.EffectType.Special ||
            effect.specialEffect != "CooldownReset")
            return false;

        // 대상의 모든 스킬 쿨다운 리셋
        // 실제 구현은 스킬 쿨다운 시스템에 따라 달라짐
        Debug.Log($"[CooldownReset] Reset all cooldowns for {target.name}");

        // 스킬 사용 가능 상태로 변경
        if (target.SkillManager != null)
        {
            // 침묵 등 스킬 사용 불가 상태 해제
            var silenceSkill = target.SkillManager.GetSkillByType(SkillSystem.StatusType.Silence);
            if (silenceSkill != null)
            {
                target.SkillManager.RemoveSkill(silenceSkill);
            }
        }

        return true;
    }
}

/// <summary>
/// 특수 스킬 예시 9: 도발 스킬
/// 적들이 자신만 공격하도록 강제
/// </summary>
public class TauntSkillRuntime : AdvancedSkillRuntime
{
    protected override void OnStartExtended()
    {
        // 도발 상태 설정 (BattleTargetSystem에서 체크)
        // StatusType.Provoke로 처리됨

        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null)
            return;

        bool isAlly = Sender.BattleActorInfo.IsAlly;
        var enemies = isAlly ?
            battleManager.GetEnemyActors() :
            battleManager.GetAllyActors();

        // 모든 적의 타겟을 자신으로 강제 설정
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                // 적의 다음 타겟을 자신으로 설정
                Debug.Log($"[Taunt] {enemy.name} is taunted by {Sender.name}");
            }
        }
    }
}

/// <summary>
/// 특수 스킬 예시 10: 부활 스킬
/// 죽은 아군을 되살림
/// </summary>
public class ReviveSkillRuntime : AdvancedSkillRuntime
{
    protected override List<BattleActor> GetCustomTargets(SkillSystem.TargetType targetType)
    {
       // if (targetType != SkillSystem.TargetType.Special)
       //     return null;

        var targets = new List<BattleActor>();
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null)
            return targets;

        bool isAlly = Sender.BattleActorInfo.IsAlly;
        var allies = isAlly ?
            battleManager.GetAllyActors() :
            battleManager.GetEnemyActors();

        // 죽은 아군 찾기
        var deadAlly = allies.FirstOrDefault(a => a != null && a.IsDead);
        if (deadAlly != null)
        {
            targets.Add(deadAlly);
        }

        return targets;
    }

    protected override bool HandleCustomEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        if (effect.type != SkillSystem.EffectType.Special ||
            effect.specialEffect != "Revive")
            return false;

        if (target == null || !target.IsDead)
            return false;

        // 부활 처리
        int reviveHp = Mathf.RoundToInt(target.BattleActorInfo.MaxHp * 0.3f); // 30% HP로 부활
     //   target.BattleActorInfo.SetHp(reviveHp);
        target.SetState(BattleActorState.Idle);

        Debug.Log($"[Revive] {target.name} has been revived with {reviveHp} HP!");

        return true;
    }
}


/// <summary>
/// 추가 공격 스킬 런타임
/// 일정 확률로 추가 공격을 실행
/// </summary>
public class ExtraAttackSkillRuntime : AdvancedSkillRuntime
{
    private int pendingExtraAttacks = 0;
    private float damageModifier = 1.0f;
    private float attackDelay = 0.3f;

    protected override void OnInitialize()
    {
        // ExtraAttack 효과 찾기
        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.ExtraAttack)
            {
                pendingExtraAttacks = effect.extraAttackCount;
                damageModifier = effect.extraAttackDamageModifier;
                attackDelay = effect.extraAttackDelay;
                break;
            }
        }
    }

    public override async void OnStart()
    {
        base.OnStart();

        // 메인 공격 실행
        await ExecuteMainAttack();

        // 추가 공격 처리
        await ProcessExtraAttacks();
    }

    private async UniTask ExecuteMainAttack()
    {
        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.Damage)
            {
                var targets = ResolveTargets(effect.targetType);
                foreach (var target in targets)
                {
                    if (target != null && !target.IsDead)
                    {
                        ApplyDamage(effect, target);
                    }
                }
            }
        }
    }

    private async UniTask ProcessExtraAttacks()
    {
        for (int i = 0; i < pendingExtraAttacks; i++)
        {
            // 추가 공격 확률 체크
            var extraAttackEffect = GetExtraAttackEffect();
            if (extraAttackEffect != null)
            {
                float chance = extraAttackEffect.chance;
                if (UnityEngine.Random.value <= chance)
                {
                    // 딜레이
                    await UniTask.Delay((int)(attackDelay * 1000));

                    // 추가 공격 실행
                    await ExecuteExtraAttack(i + 1);
                }
                else
                {
                    Debug.Log($"[ExtraAttack] 추가 공격 실패 (확률: {chance * 100}%)");
                    break; // 실패하면 연쇄 중단
                }
            }
        }
    }

    private async UniTask ExecuteExtraAttack(int attackNumber)
    {
        Debug.Log($"[ExtraAttack] {Sender.name}의 {attackNumber}번째 추가 공격!");

        // 타겟 재설정 (원래 타겟이 죽었을 수 있음)
        var targets = GetValidTargets();

        foreach (var target in targets)
        {
            if (target != null && !target.IsDead)
            {
                // 추가 공격 데미지 계산 (배율 적용)
                var damageEffect = GetDamageEffect();
                if (damageEffect != null)
                {
                    var modifiedEffect = damageEffect.Clone();
                    modifiedEffect.value *= damageModifier;

                    ApplyDamage(modifiedEffect, target);

                    // 추가 공격 이펙트 재생
                    await ShowExtraAttackEffect(target);
                }
            }
        }
    }

    private async UniTask ShowExtraAttackEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            // 추가 공격 전용 이펙트 또는 기본 공격 이펙트 재사용
            string effectId = "ExtraAttack_Effect";
            await BattleEffectManager.Instance.PlayEffect(
                effectId,
                target
            );
        }
    }

    private List<BattleActor> GetValidTargets()
    {
        // 살아있는 타겟 찾기
        var originalTargets = ResolveTargets(MainTargetType);
        var validTargets = new List<BattleActor>();

        foreach (var target in originalTargets)
        {
            if (target != null && !target.IsDead)
            {
                validTargets.Add(target);
            }
        }

        // 타겟이 모두 죽었으면 새로운 타겟 찾기
        if (validTargets.Count == 0)
        {
            var battleManager = BattleProcessManagerNew.Instance;
            if (battleManager != null)
            {
                var enemies = Sender.BattleActorInfo.IsAlly ?
                    battleManager.GetEnemyActors() :
                    battleManager.GetAllyActors();

                foreach (var enemy in enemies)
                {
                    if (enemy != null && !enemy.IsDead)
                    {
                        validTargets.Add(enemy);
                        break; // 첫 번째 유효한 타겟만
                    }
                }
            }
        }

        return validTargets;
    }

    private AdvancedSkillEffect GetExtraAttackEffect()
    {
        return SkillData.effects.Find(e => e.type == SkillSystem.EffectType.ExtraAttack);
    }

    private AdvancedSkillEffect GetDamageEffect()
    {
        return SkillData.effects.Find(e => e.type == SkillSystem.EffectType.Damage);
    }


}

/// <summary>
/// 조건부 데미지 스킬 런타임 (수정 버전)
/// </summary>
public class ConditionalDamageSkillRuntime : AdvancedSkillRuntime
{
    // ExecuteInstantEffects 오버라이드 (ProcessEffects 대신)
    protected override bool ExecuteInstantEffects()
    {
        bool executed = false;

        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.ConditionalDamage)
            {
                ProcessConditionalDamage(effect);
                executed = true;
            }
            else if (effect.type == SkillSystem.EffectType.Damage)
            {
                // 기본 데미지도 처리
                ExecuteSingleEffect(effect);
                executed = true;
            }
            else if (effect.type == SkillSystem.EffectType.ExtraAttack)
            {
                // ExtraAttack 효과도 처리
                ExecuteSingleEffect(effect);
                executed = true;
            }
        }

        return executed;
    }

    private void ProcessConditionalDamage(AdvancedSkillEffect effect)
    {
        var targets = ResolveTargets(effect.targetType);

        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            // 조건 체크
            bool conditionMet = CheckCondition(effect, target);

            if (conditionMet)
            {
                // 조건 충족 시 강화된 데미지
                ApplyEnhancedDamage(effect, target);
            }
            else
            {
                // 조건 미충족 시 기본 데미지 효과 찾아서 적용
                var normalDamageEffect = SkillData.effects.FirstOrDefault(e => e.type == SkillSystem.EffectType.Damage);
                if (normalDamageEffect != null)
                {
                    ApplyDamage(normalDamageEffect, target);
                }
            }
        }
    }

    private bool CheckCondition(AdvancedSkillEffect effect, BattleActor target)
    {
        if (!effect.hasCondition) return false;

        switch (effect.conditionType)
        {
            case ConditionType.HPBelow:
                return CheckHPBelow(Sender, effect.conditionValue);

            case ConditionType.HPAbove:
                return CheckHPAbove(Sender, effect.conditionValue);

            case ConditionType.TargetHPBelow:
                return CheckHPBelow(target, effect.conditionValue);

            case ConditionType.TargetHPAbove:
                return CheckHPAbove(target, effect.conditionValue);

            case ConditionType.StatusEffect:
                return CheckHasStatusEffect(target);

            case ConditionType.BuffCount:
                return GetBuffCount(target) >= (int)effect.conditionValue;

            case ConditionType.DebuffCount:
                return GetDebuffCount(target) >= (int)effect.conditionValue;

            case ConditionType.HasBuff:
                return HasAnyBuff(target);

            case ConditionType.HasDebuff:
                return HasAnyDebuff(target);

            default:
                return false;
        }
    }

    // 조건 체크 헬퍼 메서드들
    private bool CheckHPBelow(BattleActor actor, float threshold)
    {
        if (actor?.BattleActorInfo == null) return false;
        float hpPercent = (float)actor.BattleActorInfo.Hp / actor.BattleActorInfo.MaxHp * 100;
        return hpPercent <= threshold;
    }

    private bool CheckHPAbove(BattleActor actor, float threshold)
    {
        if (actor?.BattleActorInfo == null) return false;
        float hpPercent = (float)actor.BattleActorInfo.Hp / actor.BattleActorInfo.MaxHp * 100;
        return hpPercent >= threshold;
    }

    private bool CheckHasStatusEffect(BattleActor target)
    {
        if (target?.SkillManager == null) return false;

        // StatusType별로 체크
        var statusSkills = target.SkillManager.GetSkillsByGroup(EffectGroupType.Debuff);
        return statusSkills != null && statusSkills.Count > 0;
    }

    private int GetBuffCount(BattleActor actor)
    {
        if (actor?.SkillManager == null) return 0;
        return actor.SkillManager.GetSkillCountByGroup(EffectGroupType.Buff);
    }

    private int GetDebuffCount(BattleActor actor)
    {
        if (actor?.SkillManager == null) return 0;
        return actor.SkillManager.GetSkillCountByGroup(EffectGroupType.Debuff);
    }

    private bool HasAnyBuff(BattleActor actor)
    {
        return GetBuffCount(actor) > 0;
    }

    private bool HasAnyDebuff(BattleActor actor)
    {
        return GetDebuffCount(actor) > 0;
    }

    // 강화된 데미지 적용
    private void ApplyEnhancedDamage(AdvancedSkillEffect effect, BattleActor target)
    {
        // ConditionalDamage의 value를 스킬 파워로 사용
        var context = new BattleFormularHelper.BattleContext
        {
            Attacker = Sender,
            Defender = target,
            SkillPower = (int)effect.value,
            AttackType = Sender.BattleActorInfo.AttackType,
            IsSkillAttack = true,
            DamageOptions = DamageOption.None
        };

        var result = BattleFormularHelper.CalculateDamage(context);
        target.TakeDamageWithResult(result, Sender).Forget();

        Debug.Log($"[ConditionalDamage] 조건 충족! {target.name}에게 강화 데미지 {result.FinalDamage}");

        // 조건 충족 시각 효과
        ShowConditionMetEffect(target).Forget();
    }

    private async UniTask ShowConditionMetEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            // 크리티컬 이펙트 재사용
            await BattleEffectManager.Instance.PlayEffect(
                "Critical_Hit",
                target//,
             //   1.0f
            );
        }
    }
}

/// <summary>
/// 조건부 버프 스킬 런타임
/// 특정 조건 충족 시에만 버프 발동
/// </summary>
public class ConditionalBuffSkillRuntime : AdvancedSkillRuntime
{
    private Dictionary<BattleActor, BuffDebuffInstance> activeConditionalBuffs = new Dictionary<BattleActor, BuffDebuffInstance>();

    protected override bool ExecuteInstantEffects()
    {
        bool executed = false;

        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.ConditionalBuff)
            {
                // 패시브처럼 조건 감시 시작
                StartMonitoringCondition(effect);
                executed = true;
            }
            else
            {
                // 다른 효과들 처리
                ExecuteSingleEffect(effect);
                executed = true;
            }
        }

        return executed;
    }

    // 매 턴 조건 체크
    protected override void OnTurnUpdateExtended()
    {
        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.ConditionalBuff)
            {
                CheckAndApplyConditionalBuff(effect);
            }
        }
    }

    private void StartMonitoringCondition(AdvancedSkillEffect effect)
    {
        Debug.Log($"[ConditionalBuff] Starting condition monitoring for {effect.statType}");

        // 초기 조건 체크
        CheckAndApplyConditionalBuff(effect);
    }

    private void CheckAndApplyConditionalBuff(AdvancedSkillEffect effect)
    {
        var targets = ResolveTargets(effect.targetType);

        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            bool conditionMet = CheckCondition(effect, target);
            bool buffActive = activeConditionalBuffs.ContainsKey(target);

            if (conditionMet && !buffActive)
            {
                // 조건 충족 - 버프 적용
                ApplyConditionalBuff(effect, target);
            }
            else if (!conditionMet && buffActive)
            {
                // 조건 미충족 - 버프 제거
                RemoveConditionalBuff(effect, target);
            }
        }
    }

    private bool CheckCondition(AdvancedSkillEffect effect, BattleActor target)
    {
        if (!effect.hasCondition) return true;

        switch (effect.conditionType)
        {
            case ConditionType.HPBelow:
                float hpPercent = (float)target.BattleActorInfo.Hp / target.BattleActorInfo.MaxHp * 100;
                return hpPercent <= effect.conditionValue;

            case ConditionType.HPAbove:
                float hpPercentAbove = (float)target.BattleActorInfo.Hp / target.BattleActorInfo.MaxHp * 100;
                return hpPercentAbove >= effect.conditionValue;

            case ConditionType.BuffCount:
                int buffCount = target.SkillManager?.GetSkillCountByGroup(EffectGroupType.Buff) ?? 0;
                return buffCount >= (int)effect.conditionValue;

            case ConditionType.DebuffCount:
                int debuffCount = target.SkillManager?.GetSkillCountByGroup(EffectGroupType.Debuff) ?? 0;
                return debuffCount >= (int)effect.conditionValue;

            case ConditionType.EnemyCount:
                int enemyCount = GetAliveEnemyCount();
                return enemyCount >= (int)effect.conditionValue;

            case ConditionType.AllyCount:
                int allyCount = GetAliveAllyCount();
                return allyCount <= (int)effect.conditionValue;

            default:
                return false;
        }
    }

    private void ApplyConditionalBuff(AdvancedSkillEffect effect, BattleActor target)
    {
        // 버프 값 계산
        float buffValue = CalculateBuffValue(effect, target);

        // CharacterStatType 변환
        var charStatType = ConvertToCharacterStatType(effect.statType);

        if (charStatType != CharacterStatType.None)
        {
            // 스탯 변경 적용
            target.BattleActorInfo.AddStatBuffValue(charStatType, buffValue);

            // 인스턴스 생성 및 저장
            var instance = new BuffDebuffInstance
            {
                Effect = effect,
                Source = Sender,
                Target = target,
                CurrentStacks = 1,
                RemainingTurns = effect.duration > 0 ? effect.duration : 999,
                CurrentValue = buffValue,
                OriginalValue = buffValue
            };

            activeConditionalBuffs[target] = instance;

            Debug.Log($"[ConditionalBuff] Applied {effect.statType} +{buffValue:P} to {target.name} (조건 충족)");

            // 시각 효과
            ShowConditionBuffEffect(target).Forget();
        }
    }

    private void RemoveConditionalBuff(AdvancedSkillEffect effect, BattleActor target)
    {
        if (!activeConditionalBuffs.TryGetValue(target, out var instance))
            return;

        // 스탯 복구
        var charStatType = ConvertToCharacterStatType(effect.statType);
        if (charStatType != CharacterStatType.None)
        {
            target.BattleActorInfo.RemoveStatBuffValue(charStatType, instance.CurrentValue);
        }

        // 인스턴스 제거
        activeConditionalBuffs.Remove(target);

        Debug.Log($"[ConditionalBuff] Removed {effect.statType} from {target.name} (조건 미충족)");
    }

    private float CalculateBuffValue(AdvancedSkillEffect effect, BattleActor target)
    {
        // 고정값이면 그대로, 퍼센트면 /100
        if (effect.isFixedValue)
        {
            return effect.value;
        }
        else
        {
            return effect.value / 100f;
        }
    }

    private int GetAliveEnemyCount()
    {
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null) return 0;

        var enemies = Sender.BattleActorInfo.IsAlly ?
            battleManager.GetEnemyActors() :
            battleManager.GetAllyActors();

        return enemies.Count(e => e != null && !e.IsDead);
    }

    private int GetAliveAllyCount()
    {
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null) return 0;

        var allies = Sender.BattleActorInfo.IsAlly ?
            battleManager.GetAllyActors() :
            battleManager.GetEnemyActors();

        return allies.Count(a => a != null && !a.IsDead);
    }

    private async UniTask ShowConditionBuffEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect(
                "Buff_Condition_Met",
                target
                //target.transform.position,
               // 1.0f
            );
        }
    }

    // 스킬 종료 시 모든 조건부 버프 제거
    protected override void OnEndExtended()
    {
        foreach (var kvp in activeConditionalBuffs.ToList())
        {
            var target = kvp.Key;
            var instance = kvp.Value;

            // 버프 제거
            var charStatType = ConvertToCharacterStatType(instance.Effect.statType);
            if (charStatType != CharacterStatType.None)
            {
                target.BattleActorInfo.RemoveStatBuffValue(charStatType, instance.CurrentValue);
            }
        }

        activeConditionalBuffs.Clear();
    }
}



/// <summary>
/// 최저 HP 대상 치유 스킬 런타임
/// HP가 가장 낮은 아군을 자동으로 선택하여 치유
/// </summary>
public class HealLowestHPSkillRuntime : AdvancedSkillRuntime
{
    protected override bool ExecuteInstantEffects()
    {
        bool executed = false;

        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.Heal &&
                effect.specialEffect == "TargetLowestHP")
            {
                // 최저 HP 대상 치유
                ApplyHealToLowestHP(effect);
                executed = true;
            }
            else
            {
                // 다른 효과 처리
                ExecuteSingleEffect(effect);
                executed = true;
            }
        }

        return executed;
    }

    private void ApplyHealToLowestHP(AdvancedSkillEffect effect)
    {
        // 최저 HP 대상 찾기
        var target = FindLowestHPTarget(effect.targetType);

        if (target == null)
        {
            Debug.LogWarning("[HealLowestHP] No valid target found");
            return;
        }

        Debug.Log($"[HealLowestHP] Selected {target.name} (HP: {target.BattleActorInfo.Hp}/{target.BattleActorInfo.MaxHp})");

        // 치유 적용
        ApplyEnhancedHeal(effect, target);
    }

    private BattleActor FindLowestHPTarget(SkillSystem.TargetType baseTargetType)
    {
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null) return null;

        List<BattleActor> candidates = null;

        // 기본 타겟 타입에 따라 후보군 결정
        switch (baseTargetType)
        {
            case SkillSystem.TargetType.AllySingle:
            case SkillSystem.TargetType.AllyAll:
            case SkillSystem.TargetType.Self:
                // 아군 중에서 선택
                candidates = Sender.BattleActorInfo.IsAlly ?
                    battleManager.GetAllyActors() :
                    battleManager.GetEnemyActors();
                break;

            case SkillSystem.TargetType.EnemySingle:
            case SkillSystem.TargetType.EnemyAll:
                // 적군 중에서 선택 (특수한 경우)
                candidates = Sender.BattleActorInfo.IsAlly ?
                    battleManager.GetEnemyActors() :
                    battleManager.GetAllyActors();
                break;

            case SkillSystem.TargetType.Random:
                // 모든 유닛 중에서 선택
                candidates = new List<BattleActor>();
                candidates.AddRange(battleManager.GetAllyActors());
                candidates.AddRange(battleManager.GetEnemyActors());
                break;
        }

        if (candidates == null || candidates.Count == 0)
            return null;

        // HP 비율이 가장 낮은 대상 찾기
        BattleActor lowestTarget = null;
        float lowestHPRatio = float.MaxValue;

        foreach (var actor in candidates)
        {
            if (actor == null || actor.IsDead) continue;

            // HP 비율 계산
            float hpRatio = (float)actor.BattleActorInfo.Hp / actor.BattleActorInfo.MaxHp;

            // 우선순위 체크
            bool shouldPrioritize = false;

            // 1. 빈사 상태 우선 (HP 20% 이하)
            if (hpRatio <= 0.2f && lowestHPRatio > 0.2f)
            {
                shouldPrioritize = true;
            }
            // 2. 같은 우선순위면 HP 비율이 낮은 대상
            else if (hpRatio < lowestHPRatio)
            {
                shouldPrioritize = true;
            }

            if (shouldPrioritize)
            {
                lowestHPRatio = hpRatio;
                lowestTarget = actor;
            }
        }

        return lowestTarget;
    }

    private void ApplyEnhancedHeal(AdvancedSkillEffect effect, BattleActor target)
    {
        // 기본 치유량 계산
        int baseHealAmount = CalculateHealValue(effect.value);

        // 대상의 HP가 낮을수록 치유량 증가 (선택사항)
        float hpRatio = (float)target.BattleActorInfo.Hp / target.BattleActorInfo.MaxHp;
        float bonusMultiplier = 1f;

        if (hpRatio <= 0.3f)
        {
            // HP 30% 이하면 치유량 50% 증가
            bonusMultiplier = 1.5f;
            Debug.Log($"[HealLowestHP] Critical heal bonus applied (+50%)");
        }

        int finalHealAmount = Mathf.RoundToInt(baseHealAmount * bonusMultiplier);

        // 치유 적용
        ApplyHealWithAmount(target, finalHealAmount);

        // 시각 효과
        ShowTargetedHealEffect(target).Forget();
    }

    private void ApplyHealWithAmount(BattleActor target, int healAmount)
    {
        if (target == null || target.IsDead) return;

        // 치유 차단 체크
        if (IsHealBlocked(target))
        {
            Debug.Log($"[HealLowestHP] {target.name} is heal blocked!");
            ShowHealBlockedEffect(target);
            return;
        }

        // 실제 치유
        int previousHp = target.BattleActorInfo.Hp;
        int maxHp = target.BattleActorInfo.MaxHp;
        int actualHeal = Mathf.Min(healAmount, maxHp - previousHp);

        target.BattleActorInfo.AddHp(actualHeal);

        Debug.Log($"[HealLowestHP] Healed {target.name} for {actualHeal} (HP: {previousHp} -> {target.BattleActorInfo.Hp})");

        // UI 업데이트
        target.UpdateTag();

        // 치유 이벤트 발생
        TriggerHealEvent(target, actualHeal, false);
    }

    private async UniTask ShowTargetedHealEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            // 타겟팅 이펙트
            await BattleEffectManager.Instance.PlayEffect(
                "TargetedHeal_Effect",
                target
               // target.transform.position,
              // 1.5f
            );
        }
    }
}


/// <summary>
/// 재생(Regen) 스킬 런타임
/// 매 턴 지속적으로 치유
/// </summary>
public class RegenSkillRuntime : AdvancedSkillRuntime
{
    private Dictionary<BattleActor, RegenInstance> activeRegens = new Dictionary<BattleActor, RegenInstance>();

    // 재생 인스턴스 클래스
    private class RegenInstance
    {
        public AdvancedSkillEffect Effect { get; set; }
        public BattleActor Target { get; set; }
        public int RemainingTurns { get; set; }
        public float HealPerTurn { get; set; }
        public int TotalHealed { get; set; }
        public bool IsPercentBased { get; set; }
    }

    protected override bool ExecuteInstantEffects()
    {
        bool executed = false;

        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.HealOverTime ||
                (effect.type == SkillSystem.EffectType.Heal && effect.duration > 0))
            {
                // 재생 효과 적용
                ApplyRegen(effect);
                executed = true;
            }
            else
            {
                // 다른 즉시 효과 처리
                ExecuteSingleEffect(effect);
                executed = true;
            }
        }

        return executed;
    }

    // 매 턴 호출되는 메서드
    protected override void ProcessTurnBasedEffects()
    {
        // 모든 활성 재생 효과 처리
        var regenList = activeRegens.Values.ToList();

        foreach (var regen in regenList)
        {
            if (regen.RemainingTurns > 0)
            {
                ApplyRegenTick(regen);
                regen.RemainingTurns--;

                if (regen.RemainingTurns <= 0)
                {
                    RemoveRegen(regen.Target);
                }
            }
        }
    }

    private void ApplyRegen(AdvancedSkillEffect effect)
    {
        var targets = ResolveTargets(effect.targetType);

        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            // 기존 재생 효과 체크
            if (activeRegens.ContainsKey(target))
            {
                // 재생 효과 갱신 또는 중첩
                RefreshOrStackRegen(target, effect);
            }
            else
            {
                // 새로운 재생 효과 적용
                CreateNewRegen(target, effect);
            }
        }
    }

    private void CreateNewRegen(BattleActor target, AdvancedSkillEffect effect)
    {
        var regenInstance = new RegenInstance
        {
            Effect = effect,
            Target = target,
            RemainingTurns = effect.duration,
            HealPerTurn = effect.value,
            IsPercentBased = !effect.isFixedValue,
            TotalHealed = 0
        };

        activeRegens[target] = regenInstance;

        Debug.Log($"[Regen] Applied regeneration to {target.name} for {effect.duration} turns " +
                  $"({effect.value}{(regenInstance.IsPercentBased ? "%" : "")} per turn)");

        // 시각 효과
        ShowRegenApplyEffect(target).Forget();

        // 첫 턴 즉시 치유 (선택사항)
        if (effect.applyFirstTickImmediately)
        {
            ApplyRegenTick(regenInstance);
        }
    }

    private void RefreshOrStackRegen(BattleActor target, AdvancedSkillEffect effect)
    {
        var existing = activeRegens[target];

        if (effect.canStack && existing.Effect.canStack)
        {
            // 중첩 처리
            existing.HealPerTurn += effect.value;
            existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, effect.duration);

            Debug.Log($"[Regen] Stacked regeneration on {target.name} " +
                      $"(Total: {existing.HealPerTurn} per turn)");
        }
        else
        {
            // 더 강한 효과로 교체
            if (effect.value > existing.HealPerTurn)
            {
                existing.Effect = effect;
                existing.HealPerTurn = effect.value;
                existing.RemainingTurns = effect.duration;

                Debug.Log($"[Regen] Upgraded regeneration on {target.name}");
            }
            else
            {
                // 지속시간만 갱신
                existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, effect.duration);
                Debug.Log($"[Regen] Refreshed regeneration duration on {target.name}");
            }
        }
    }

    private void ApplyRegenTick(RegenInstance regen)
    {
        if (regen.Target == null || regen.Target.IsDead)
        {
            RemoveRegen(regen.Target);
            return;
        }

        // 치유 차단 체크
        if (IsHealBlocked(regen.Target))
        {
            Debug.Log($"[Regen] {regen.Target.name} is heal blocked! Regen tick skipped.");
            ShowHealBlockedEffect(regen.Target);
            return;
        }

        // 치유량 계산
        int healAmount = CalculateRegenHeal(regen);

        // 치유 감소 적용
        if (!regen.Effect.ignoreHealReduction)
        {
            healAmount = ApplyHealReductionToRegen(healAmount, regen.Target);
        }

        // 실제 치유
        if (healAmount > 0)
        {
            int previousHp = regen.Target.BattleActorInfo.Hp;
            int maxHp = regen.Target.BattleActorInfo.MaxHp;

            // 오버힐 처리
            if (!regen.Effect.allowOverheal)
            {
                healAmount = Mathf.Min(healAmount, maxHp - previousHp);
            }

            regen.Target.BattleActorInfo.AddHp(healAmount);
            regen.TotalHealed += healAmount;

            Debug.Log($"[Regen] {regen.Target.name} regenerated {healAmount} HP " +
                      $"(Turn {regen.Effect.duration - regen.RemainingTurns + 1}/{regen.Effect.duration})");

            // UI 업데이트
            regen.Target.UpdateTag();

            // 시각 효과
            ShowRegenTickEffect(regen.Target, healAmount).Forget();
        }
    }

    private int CalculateRegenHeal(RegenInstance regen)
    {
        int healAmount = 0;

        if (regen.IsPercentBased)
        {
            // 퍼센트 기반 (최대 HP의 X%)
            healAmount = Mathf.RoundToInt(regen.Target.BattleActorInfo.MaxHp * (regen.HealPerTurn / 100f));
        }
        else
        {
            // 고정값
            healAmount = Mathf.RoundToInt(regen.HealPerTurn);
        }

        // 힐 파워 보너스 적용
        float healPowerBonus = Sender.BattleActorInfo.GetStatFloat(CharacterStatType.HealPower);
        if (healPowerBonus > 0)
        {
            healAmount = Mathf.RoundToInt(healAmount * (1f + healPowerBonus / 100f));
        }

        return healAmount;
    }

    private int ApplyHealReductionToRegen(int healAmount, BattleActor target)
    {
        float healReduction = target.BattleActorInfo.GetStatFloat(CharacterStatType.HealReduction);
        if (healReduction > 0)
        {
            healAmount = Mathf.RoundToInt(healAmount * (1f - healReduction / 100f));
        }
        return healAmount;
    }

    private void RemoveRegen(BattleActor target)
    {
        if (activeRegens.ContainsKey(target))
        {
            var regen = activeRegens[target];
            Debug.Log($"[Regen] Regeneration ended for {target.name} (Total healed: {regen.TotalHealed})");

            activeRegens.Remove(target);

            // 종료 효과
            ShowRegenEndEffect(target).Forget();
        }
    }

    // 시각 효과 메서드들
    private async UniTask ShowRegenApplyEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect(
                "Regen_Apply",
                target
            );
        }
    }

    private async UniTask ShowRegenTickEffect(BattleActor target, int healAmount)
    {
        if (BattleEffectManager.Instance != null)
        {
            // 재생 틱 이펙트
            await BattleEffectManager.Instance.PlayEffect(
                "Regen_Tick",
                target
               // 0.5f
            );
        }

        // 힐 숫자 표시
        if (BattleUIManager.Instance != null)
        {
         /*   BattleUIManager.Instance.ShowDamageNumber(
                target,
                healAmount,
                false,  // 크리티컬 아님
                true    // 힐 표시
            );*/

        }
    }

    private async UniTask ShowRegenEndEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect(
                "Regen_End",
                target
            );
        }
    }

    // 스킬 종료 시 모든 재생 효과 제거
    protected override void OnEndExtended()
    {
        foreach (var target in activeRegens.Keys.ToList())
        {
            RemoveRegen(target);
        }
        activeRegens.Clear();
    }
}
