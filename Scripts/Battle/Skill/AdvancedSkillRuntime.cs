using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SkillSystem;
using IronJade.Observer.Core;
using System;
using static BattleFormularHelper;
using Cysharp.Threading.Tasks;

/// <summary>
/// AdvancedSkillData 기반 스킬 런타임 베이스 클래스
/// 하이브리드 접근: 90%는 이 클래스로 처리, 10%는 상속으로 특수 구현
/// </summary>
/// 


// =====================================================
// 2. 버프/디버프 관리를 위한 데이터 클래스
// =====================================================
public class BuffDebuffInstance
{
    public AdvancedSkillEffect Effect { get; set; }
    public BattleActor Source { get; set; }
    public BattleActor Target { get; set; }
    public int CurrentStacks { get; set; }
    public int RemainingTurns { get; set; }
    public float CurrentValue { get; set; }  // 현재 적용된 값
    public float OriginalValue { get; set; } // 원본 값
    public string InstanceId { get; set; }

    public BuffDebuffInstance()
    {
        InstanceId = System.Guid.NewGuid().ToString();
        CurrentStacks = 1;
    }
}

public class StatusEffectInstance
{
    public SkillSystem.StatusType Type { get; set; }
    public BattleActor Source { get; set; }
    public BattleActor Target { get; set; }
    public int RemainingTurns { get; set; }
    public float Value { get; set; }  // DoT 데미지, 속도 감소량 등
    public bool IsActive { get; set; }
    public string InstanceId { get; set; }

    // DoT 관련
    public int TotalDamageDealt { get; set; }
    public int TickCount { get; set; }

    public StatusEffectInstance()
    {
        InstanceId = System.Guid.NewGuid().ToString();
        IsActive = true;
    }
}




public partial class AdvancedSkillRuntime : IObserver
{
    #region Core Properties
    public BattleActor Sender { get; protected set; }
    public BattleActor Receiver { get; protected set; }
    public AdvancedSkillData SkillData { get; protected set; }

    // 현재 처리 중인 효과
    public AdvancedSkillEffect CurrentEffect { get; protected set; }

    // 서브스킬 관리
    protected List<AdvancedSkillRuntime> subSkills = new List<AdvancedSkillRuntime>();


    // 버프/디버프 인스턴스 추적
    private static Dictionary<BattleActor, List<BuffDebuffInstance>> activeBuffDebuffs =
        new Dictionary<BattleActor, List<BuffDebuffInstance>>();


    // 활성 상태이상 추적
    private static Dictionary<BattleActor, List<StatusEffectInstance>> activeStatusEffects =
        new Dictionary<BattleActor, List<StatusEffectInstance>>();



    #endregion

    #region Time Management (Turn-based)
    public int RemainTurn { get; protected set; }
    public bool IsExpired => RemainTurn <= 0 || CheckReleaseCondition();
    #endregion

    #region Skill Properties
    public int SkillID => SkillData?.skillId ?? 0;
    public string SkillName => SkillData?.skillName ?? "Unknown";
    public bool IsOneShot => SkillData?.effects.All(e => e.duration <= 0) ?? true;
    public bool IsStackable => SkillData?.effects.Any(e => e.maxStacks > 1) ?? false;
    public int CurrentStacks { get; protected set; } = 1;
    public SkillSystem.SkillCategory Category => SkillData?.category ?? SkillSystem.SkillCategory.Active;
    public SkillSystem.TriggerType TriggerType => SkillData?.triggerType ?? SkillSystem.TriggerType.Always;
    public SkillSystem.TargetType MainTargetType => SkillData?.effects.FirstOrDefault()?.targetType ?? SkillSystem.TargetType.Self;

    public BPUpgradeLevel UpgradeLevel { get; set; } = BPUpgradeLevel.Base;
    #endregion

    #region Virtual Methods for Override (특수 스킬용)

    /// <summary>
    /// 초기화 시 호출 - 오버라이드 가능
    /// </summary>
    protected virtual void OnInitialize() { }

    /// <summary>
    /// 시작 시 추가 처리 - 오버라이드 가능
    /// </summary>
    protected virtual void OnStartExtended() { }

    /// <summary>
    /// 종료 시 추가 처리 - 오버라이드 가능
    /// </summary>
    protected virtual void OnEndExtended() { }

    /// <summary>
    /// 턴 업데이트 시 추가 처리 - 오버라이드 가능
    /// </summary>
    protected virtual void OnTurnUpdateExtended() { }

    /// <summary>
    /// 커스텀 효과 처리 - 특수 효과는 여기서 구현
    /// </summary>
    protected virtual bool HandleCustomEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        // SkillDisrupt 처리
        if (effect.type == SkillSystem.EffectType.Special && effect.specialEffect == "SkillDisrupt")
        {
            ApplySkillDisrupt(effect, target);
            return true;
        }

        // KillBuff 처리
        if (effect.type == SkillSystem.EffectType.Special && effect.specialEffect == "OnKillBuff")
        {
            Debug.Log($"[KillBuff] {Sender.name}에게 처치 시 버프 효과 등록");
            return true;
        }


        // CritNullify 처리
        if (effect.type == SkillSystem.EffectType.Special && effect.specialEffect == "CritNullify")
        {
            // value가 확률 (50 = 50%)
            float nullifyChance = effect.value > 1 ? effect.value / 100f : effect.value;

            // 대상에게 치명타 무효화 확률 설정
            target.BattleActorInfo.CritNullifyChance = nullifyChance;

            Debug.Log($"[CritNullify] {target.name}에게 {nullifyChance * 100}% 치명타 무효화 부여 ({effect.duration}턴)");

            // 지속시간이 있으면 버프처럼 관리
            if (effect.duration > 0)
            {
                // 버프 인스턴스로 관리 (종료 시 제거를 위해)
                RegisterCritNullifyBuff(target, effect);
            }

            return true;
        }


        // PeriodicBuff 처리 추가
        if (effect.specialEffect != null && effect.specialEffect.Contains("Periodic"))
        {
            // 주기 체크
            int period = 3;  // 기본값
            var match = System.Text.RegularExpressions.Regex.Match(effect.specialEffect, @"Periodic(\d+)");
            if (match.Success)
            {
                period = int.Parse(match.Groups[1].Value);
            }

            // 현재 턴이 주기에 맞는지 체크
            int currentTurn = BattleProcessManagerNew.Instance?.turnOrderSystem?.CurrentTurn ?? 0;
            if (currentTurn % period == 0)
            {
                // 버프 적용
                if (effect.type == SkillSystem.EffectType.Buff)
                {
                    ApplyBuff(effect, target);
                }
                return true;
            }
            return false;  // 주기가 아니면 스킵
        }





        return false;
    }




    // CritNullify 버프 등록 헬퍼 메서드
    private void RegisterCritNullifyBuff(BattleActor target, AdvancedSkillEffect effect)
    {
        // activeBuffDebuffs에 등록하여 지속시간 관리
        if (!activeBuffDebuffs.ContainsKey(target))
            activeBuffDebuffs[target] = new List<BuffDebuffInstance>();

        var instance = new BuffDebuffInstance
        {
            Effect = effect,
            Source = Sender,
            Target = target,
            RemainingTurns = effect.duration,
            CurrentValue = effect.value
        };

        activeBuffDebuffs[target].Add(instance);
    }

    /// <summary>
    /// 커스텀 타겟 결정 - 특수한 타겟팅은 여기서 구현
    /// </summary>
    protected virtual List<BattleActor> GetCustomTargets(SkillSystem.TargetType targetType)
    {
        // null 반환 시 기본 타겟팅 사용
        return null;
    }

    #endregion

    #region Initialization
    public virtual void Initialize(BattleActor sender, BattleActor receiver, AdvancedSkillData data)
    {
        Sender = sender;
        Receiver = receiver;
        SkillData = data;

        // 턴 기반 지속시간 설정
        var maxDuration = data.effects.Max(e => e.duration);
        RemainTurn = Mathf.CeilToInt(maxDuration);

        // 커스텀 초기화
        OnInitialize();
    }
    #endregion

    #region Lifecycle Methods
    public virtual void OnStart()
    {
        Debug.Log($"[AdvancedSkill] Starting {SkillName} from {Sender?.name} to {Receiver?.name}");

        // 즉시 효과 실행
        bool isExecute = ExecuteInstantEffects();
        if( isExecute == false )
        {
            Debug.LogError($"ExecuteInstantEffects {SkillName} Failed#### ");
        }

        // 지속 효과 등록
        if (!IsOneShot)
        {
            AddObserverIds();
        }

        // 커스텀 시작 처리
        OnStartExtended();
    }

    public virtual void OnTurnUpdate()
    {
        if (IsExpired)
            return;

        RemainTurn--;

        // 턴 기반 효과 처리
        ProcessTurnBasedEffects();

        // 커스텀 턴 업데이트
        OnTurnUpdateExtended();
    }

    public virtual void OnEnd()
    {
        Debug.Log($"[AdvancedSkill] Ending {SkillName}");

        // 종료 시 효과 처리
        ProcessEndEffects();

        // 옵저버 해제
        RemoveObserverIds();

        // 커스텀 종료 처리
        OnEndExtended();

        // 서브스킬 정리
        foreach (var subSkill in subSkills)
        {
            subSkill.OnEnd();
        }
        subSkills.Clear();
    }

    public virtual void ForceEnd()
    {
        RemainTurn = 0;
        OnEnd();
    }
    #endregion

    #region Effect Execution
    protected virtual bool ExecuteInstantEffects()
    {
        bool isExecuted = false;
        foreach (var effect in SkillData.effects)
        {
            // 버프/디버프는 duration과 관계없이 즉시 실행
            bool shouldExecute = effect.duration <= 0 ||
                                effect.type == SkillSystem.EffectType.Buff ||
                                effect.type == SkillSystem.EffectType.Debuff ||
                                effect.type == SkillSystem.EffectType.StatusEffect ||
                                effect.type == SkillSystem.EffectType.Shield;

            if (shouldExecute)
            {
                ExecuteSingleEffect(effect);
                isExecuted = true;
            }
        }
        return isExecuted;
    }

    protected virtual void ProcessTurnBasedEffects()
    {
        foreach (var effect in SkillData.effects.Where(e => e.duration > 0))
        {
            ExecuteSingleEffect(effect);
        }
    }

    protected virtual void ProcessEndEffects()
    {
        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.Shield ||
                effect.type == SkillSystem.EffectType.Buff ||
                effect.type == SkillSystem.EffectType.Debuff ||
                (effect.type == SkillSystem.EffectType.Special && 
                effect.specialEffect == "CritNullify" || effect.specialEffect == "SkillDisrupt"))
            {
                RemoveEffect(effect);
            }
        }
    }

    protected virtual void ExecuteSingleEffect(AdvancedSkillEffect effect)
    {
        CurrentEffect = effect;

        // 타겟 결정
        var targets = ResolveTargets(effect.targetType);

        foreach (var target in targets)
        {
            // 확률 체크
            if (effect.chance < 1f && UnityEngine.Random.value > effect.chance)
                continue;

            // 커스텀 효과 처리 체크
            if (HandleCustomEffect(effect, target))
                continue; // 커스텀 처리됨

            // 기본 효과 적용
            ApplyEffectToTarget(effect, target);
        }
    }

    protected virtual void RemoveEffect(AdvancedSkillEffect effect)
    {
        var targets = ResolveTargets(effect.targetType);

        foreach (var target in targets)
        {
            // SkillDisrupt 제거
            if (effect.type == SkillSystem.EffectType.Special && effect.specialEffect == "SkillDisrupt")
            {
                // 침묵/봉인 상태 제거
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.CannotUseSkill);
                Debug.Log($"[SkillDisrupt] Skill disruption removed from {target.name}");
            }

            // CritNullify 제거
            if (effect.type == SkillSystem.EffectType.Special && effect.specialEffect == "CritNullify")
            {
                target.BattleActorInfo.CritNullifyChance = 0;
                Debug.Log($"[CritNullify] {target.name}의 치명타 무효화 제거");
            }
            else
            {
                RemoveEffectFromTarget(effect, target);
            }
        }
    }
    #endregion

    #region Target Resolution
    protected virtual List<BattleActor> ResolveTargets(SkillSystem.TargetType targetType)
    {
        // 커스텀 타겟팅 체크
        var customTargets = GetCustomTargets(targetType);
        if (customTargets != null)
            return customTargets;

        // 기본 타겟팅
        return GetDefaultTargets(targetType);
    }

    private List<BattleActor> GetDefaultTargets(SkillSystem.TargetType targetType)
    {
        var targets = new List<BattleActor>();
        var battleManager = BattleProcessManagerNew.Instance;
        var targetSystem = BattleTargetSystem.Instance;

        if (battleManager == null)
            return targets;

        bool isAlly = Sender.BattleActorInfo.IsAlly;
        var allies = battleManager.GetAllyActors();
        var enemies = battleManager.GetEnemyActors();

        switch (targetType)
        {
            case SkillSystem.TargetType.Self:
                targets.Add(Sender);
                break;

            case SkillSystem.TargetType.EnemySingle:
                if (Receiver != null && !Receiver.IsDead)
                    targets.Add(Receiver);
                else if (targetSystem != null)
                {
                    var defaultTarget = targetSystem.FindTargetByAggro(isAlly ? enemies : allies);
                    if (defaultTarget != null)
                        targets.Add(defaultTarget);
                }
                break;

            case SkillSystem.TargetType.EnemyAll:
                targets = isAlly ? enemies : allies;
                break;

            case SkillSystem.TargetType.EnemyRow:
                targets = GetRowTargets(Receiver, !isAlly);
                break;

            case SkillSystem.TargetType.AllySingle:
                if (Receiver != null && Receiver.BattleActorInfo.IsAlly == isAlly)
                    targets.Add(Receiver);
                else
                    targets.Add(Sender);
                break;

            case SkillSystem.TargetType.AllyAll:
                targets = isAlly ? allies : enemies;
                break;

            case SkillSystem.TargetType.AllyRow:
                targets = GetRowTargets(Sender, isAlly);
                break;

            case SkillSystem.TargetType.Random:
                if (targetSystem != null)
                {
                    var enemyList = isAlly ? enemies : allies;
                    var randomTarget = targetSystem.FindRandomTarget(enemyList);
                    if (randomTarget != null)
                        targets.Add(randomTarget);
                }
                break;

            case SkillSystem.TargetType.Lowest:
                if (targetSystem != null)
                {
                    var enemyList = isAlly ? enemies : allies;
                    var lowestTarget = targetSystem.FindTargetByLowestHP(enemyList);
                    if (lowestTarget != null)
                        targets.Add(lowestTarget);
                }
                break;

            case SkillSystem.TargetType.Highest:
                if (targetSystem != null)
                {
                    var enemyList = isAlly ? enemies : allies;
                    var highestTarget = targetSystem.FindTargetByHighestHP(enemyList);
                    if (highestTarget != null)
                        targets.Add(highestTarget);
                }
                break;
        }

        return targets.Where(t => t != null && !t.IsDead).ToList();
    }

    private List<BattleActor> GetRowTargets(BattleActor baseTarget, bool isAlly)
    {
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager?.battlePosition == null || baseTarget == null)
            return new List<BattleActor>();

        var actors = isAlly ? battleManager.GetAllyActors() : battleManager.GetEnemyActors();

        bool isFront = battleManager.battlePosition.IsFrontRow(
            baseTarget.BattleActorInfo.SlotIndex,
            isAlly
        );

        var slots = isFront ?
            battleManager.battlePosition.GetFrontRowSlots(isAlly) :
            battleManager.battlePosition.GetBackRowSlots(isAlly);

        return actors.Where(a => a != null && !a.IsDead &&
            slots.Contains(a.BattleActorInfo.SlotIndex)).ToList();
    }
    #endregion

    #region Effect Application
    protected virtual void ApplyEffectToTarget(AdvancedSkillEffect effect, BattleActor target)
    {
        Debug.Log($"[AdvancedSkill] Applying {effect.type} to {target.name}");

        switch (effect.type)
        {
            case SkillSystem.EffectType.Damage:
                ApplyDamage(effect, target);
                break;

            case SkillSystem.EffectType.TrueDamage:
                ApplyTrueDamage(effect, target);
                break;

            case SkillSystem.EffectType.FixedDamage:  //
                ApplyFixedDamage(effect, target);
                break;

            case SkillSystem.EffectType.LifeSteal:  // 새로 추가
                ApplyLifeSteal(effect, target);
                break;

            case SkillSystem.EffectType.Heal:
                ApplyHeal(effect, target);
                break;

            case SkillSystem.EffectType.HealOverTime:
                ApplyHeal(effect, target);
                break;

            case SkillSystem.EffectType.Buff:
                ApplyBuff(effect, target);
                break;

            case SkillSystem.EffectType.Debuff:
                ApplyDebuff(effect, target);
                break;

            case SkillSystem.EffectType.StatusEffect:
                ApplyStatusEffect(effect, target);
                break;

            case SkillSystem.EffectType.Shield:
                ApplyShield(effect, target);
                break;

            case SkillSystem.EffectType.Dispel:
                ApplyDispel(effect, target);
                break;

            case SkillSystem.EffectType.Reflect:
                ApplyReflect(effect, target);
                break;

            case SkillSystem.EffectType.Special:
                ApplySpecialEffect(effect, target);
                break;

            case SkillSystem.EffectType.DamageReduction:
                ApplyDamageReduction(effect, target);
                break;
        }
    }

    protected virtual void RemoveEffectFromTarget(AdvancedSkillEffect effect, BattleActor target)
    {
        switch (effect.type)
        {
            case SkillSystem.EffectType.Buff:
                RemoveBuff(effect, target);
                break;

            case SkillSystem.EffectType.Debuff:
                RemoveDebuff(effect, target);
                break;

            case SkillSystem.EffectType.Shield:
                RemoveShield(effect, target);
                break;

            case SkillSystem.EffectType.StatusEffect:
                RemoveStatusEffect(effect, target);
                break;
        }
    }


    /// <summary>
    /// 런타임 중 스킬 데이터 업데이트 (BP 업그레이드용)
    /// </summary>
    public void UpdateSkillData(AdvancedSkillData newData)
    {
        if (newData == null) return;

        // 기본 정보는 유지하고 효과만 업데이트
        var previousEffects = SkillData.effects;
        SkillData = newData;

        // 이미 적용 중인 효과가 있다면 새 데이터로 갱신
        if (CurrentEffect != null)
        {
            var effectIndex = previousEffects.IndexOf(CurrentEffect);
            if (effectIndex >= 0 && effectIndex < newData.effects.Count)
            {
                CurrentEffect = newData.effects[effectIndex];
            }
        }

        Debug.Log($"[AdvancedSkillRuntime] Updated skill data for {SkillName}");
    }


    #endregion


    //테이크 데미지 처리 . 

    #region Standard Effect Implementations
    protected virtual void ApplyDamage(AdvancedSkillEffect effect, BattleActor target)
    {

        // 스킬 계수를 새로운 메서드로 가져오기
        float skillPower = effect.GetSkillPower();

        // 데미지 옵션 설정
        var damageOptions = BattleFormularHelper.DamageOption.None;

        // 방어 무시 적용
        if (effect.defenseIgnorePercent > 0)
        {
            // 부분 방어 무시는 Context의 ExtraParams로 전달
            // 완전 무시일 경우만 플래그 설정
            if (effect.defenseIgnorePercent >= 100)
            {
                damageOptions |= BattleFormularHelper.DamageOption.IgnoreDefense;
            }
        }

        // 쉴드 무시 적용
        if (effect.ignoreShield)
        {
            damageOptions |= BattleFormularHelper.DamageOption.IgnoreShield;
        }

        var context = new BattleFormularHelper.BattleContext
        {
            Attacker = Sender,
            Defender = target,
            SkillPower = (int)skillPower,
            AttackType = Sender.BattleActorInfo.AttackType,
            IsSkillAttack = true,
            DamageOptions = damageOptions
        };

        // 추가 파라미터 설정 (부분 방어 무시 등)
        if (effect.defenseIgnorePercent > 0 && effect.defenseIgnorePercent < 100)
        {
            context.ExtraParams["DefenseIgnorePercent"] = effect.defenseIgnorePercent;
        }

        // 속성 보너스 배율
        if (effect.elementalBonusMultiplier != 1.0f)
        {
            context.ExtraParams["ElementalBonusMultiplier"] = effect.elementalBonusMultiplier;
        }

        // 크리티컬 가능 여부
        context.ExtraParams["CanCritical"] = effect.canCrit;

        // BattleFormularHelper에서 데미지 계산
        var result = BattleFormularHelper.CalculateDamage(context);

        // 디버그 로그
        Debug.Log($"[SkillDamage] {Sender.name} uses {SkillName} on {target.name}: " +
                 $"SkillPower={skillPower}%, DefIgnore={effect.defenseIgnorePercent}%, " +
                 $"BaseDamage={result.OriginalDamage}, FinalDamage={result.FinalDamage}");

        // 타겟에게 데미지 적용
        target.TakeDamageWithResult(result, Sender).Forget();
    }

    protected virtual void ApplyTrueDamage(AdvancedSkillEffect effect, BattleActor target)
    {
        var damage = CalculateDamageValue(effect.value);

        var result = new BattleFormularHelper.DamageResult
        {
            FinalDamage = damage,
            OriginalDamage = damage,
            Judgement = BattleFormularHelper.JudgementType.Hit,
            IsCritical = false,
            IsMissed = false,
            IsDodged = false,
            IsBlocked = false,
            IsResisted = false,
            IsImmune = false
        };

        target.TakeDamageWithResult(result, Sender).Forget();
    }

    protected virtual void ApplyHeal(AdvancedSkillEffect effect, BattleActor target)
    {
        // 힐 차단 상태 체크
        if (IsHealBlocked(target))
        {
            Debug.Log($"[Heal] {target.name} is heal blocked!");
            ShowHealBlockedEffect(target);
            return;
        }

        // 기본 힐량 계산
        float healPower = effect.GetHealPower();
        int baseHealAmount = CalculateBaseHealAmount(effect, healPower);

        // 힐 보너스 적용
        float finalHealAmount = ApplyHealModifiers(baseHealAmount, effect, target);

        // 크리티컬 힐 체크
        bool isCriticalHeal = false;
        if (effect.canCriticalHeal && CheckCriticalHeal())
        {
            isCriticalHeal = true;
            finalHealAmount *= GetCriticalHealMultiplier();
        }

        // 힐 감소 효과 적용 (ignoreHealReduction이 false일 때만)
        if (!effect.ignoreHealReduction)
        {
            finalHealAmount = ApplyHealReduction(finalHealAmount, target);
        }

        // 고정 보너스 추가
        finalHealAmount += effect.bonusHealFlat;

        // 최종 힐량 (정수로 변환)
        int healAmount = Mathf.RoundToInt(finalHealAmount);

        // 오버힐 처리
        if (!effect.allowOverheal)
        {
            // 일반적인 경우: 최대 HP까지만 회복
            int maxHealable = target.BattleActorInfo.MaxHp - target.BattleActorInfo.Hp;
            healAmount = Mathf.Min(healAmount, maxHealable);
        }
        else
        {
            // 오버힐 허용: 지정된 최대치까지 회복 가능
            int overhealMax = Mathf.RoundToInt(target.BattleActorInfo.MaxHp * (effect.overhealMaxPercent / 100f));
            int currentHp = target.BattleActorInfo.Hp;
            int maxAllowedHeal = overhealMax - currentHp;
            healAmount = Mathf.Min(healAmount, maxAllowedHeal);
        }

        // 실제 힐 적용
        if (healAmount > 0)
        {
            target.BattleActorInfo.Heal(healAmount);

            // 디버그 로그
            Debug.Log($"[Heal] {Sender.name} heals {target.name} for {healAmount} " +
                     $"(Power: {healPower}%, Critical: {isCriticalHeal}, Overheal: {effect.allowOverheal})");

            // 힐 이펙트 표시
            ShowHealEffect(target, healAmount, isCriticalHeal).Forget();

            // 힐 이벤트 발생
            TriggerHealEvent(target, healAmount, isCriticalHeal);
        }
    }


    /// <summary>
    /// 기본 힐량 계산
    /// </summary>
    protected virtual int CalculateBaseHealAmount(AdvancedSkillEffect effect, float healPower)
    {
        int baseAmount = 0;

        switch (effect.healBase)
        {
            case HealBase.MaxHP:
                // 대상의 최대 HP 기준
                baseAmount = Mathf.RoundToInt(Receiver.BattleActorInfo.MaxHp * (healPower / 100f));
                break;

            case HealBase.Attack:
                // 시전자의 공격력 기준
                baseAmount = Mathf.RoundToInt(Sender.BattleActorInfo.Attack * (healPower / 100f));
                break;

            case HealBase.Fixed:
                // 고정값
                baseAmount = Mathf.RoundToInt(healPower);
                break;

            case HealBase.LostHP:
                // 잃은 HP 기준
                int lostHp = Receiver.BattleActorInfo.MaxHp - Receiver.BattleActorInfo.Hp;
                baseAmount = Mathf.RoundToInt(lostHp * (healPower / 100f));
                break;
        }

        return baseAmount;
    }

    /// <summary>
    /// 힐 모디파이어 적용
    /// </summary>
    protected virtual float ApplyHealModifiers(int baseAmount, AdvancedSkillEffect effect, BattleActor target)
    {
        float healAmount = baseAmount;

        // 시전자의 힐 파워 스탯 적용
        float healPowerStat = Sender.BattleActorInfo.GetStatFloat(CharacterStatType.HealPower);
        if (healPowerStat != 0)
        {
            healAmount *= (1f + healPowerStat / 100f);
            Debug.Log($"[Heal] Heal Power stat bonus: {healPowerStat}%");
        }

        // 대상의 받는 힐 증가 스탯 적용
        float healReceivedStat = target.BattleActorInfo.GetStatFloat(CharacterStatType.HealReceived);
        if (healReceivedStat != 0)
        {
            healAmount *= (1f + healReceivedStat / 100f);
            Debug.Log($"[Heal] Heal Received stat bonus: {healReceivedStat}%");
        }

        // 랜덤 분산 (±5%)
        float variance = UnityEngine.Random.Range(0.95f, 1.05f);
        healAmount *= variance;

        return healAmount;
    }

    /// <summary>
    /// 힐 감소 효과 적용
    /// </summary>
    protected virtual float ApplyHealReduction(float healAmount, BattleActor target)
    {
        // 힐 감소 디버프 체크
        float healReduction = target.BattleActorInfo.GetStatFloat(CharacterStatType.HealReduction);
        if (healReduction > 0)
        {
            healAmount *= (1f - healReduction / 100f);
            Debug.Log($"[Heal] Heal reduction applied: -{healReduction}%");
        }

        return healAmount;
    }

    /// <summary>
    /// 크리티컬 힐 체크
    /// </summary>
    protected virtual bool CheckCriticalHeal()
    {
        // 시전자의 크리티컬 확률 사용 (또는 별도의 크리티컬 힐 확률)
        float critChance = Sender.BattleActorInfo.CriRate;
        return UnityEngine.Random.Range(0f, 100f) < critChance;
    }

    /// <summary>
    /// 크리티컬 힐 배율
    /// </summary>
    protected virtual float GetCriticalHealMultiplier()
    {
        // 크리티컬 데미지 스탯의 절반 사용 (밸런스를 위해)
        return 1f + (Sender.BattleActorInfo.CriDmg - 1f) * 0.5f;
    }

    /// <summary>
    /// 힐 차단 상태 체크
    /// </summary>
    protected virtual bool IsHealBlocked(BattleActor target)
    {
        // StatusType.HealBlock 또는 HealBlock 스탯 체크
        return target.BattleActorInfo.GetStatFloat(CharacterStatType.HealBlock) >= 100f;
    }

    /// <summary>
    /// 힐 이펙트 표시
    /// </summary>
    protected virtual async UniTask ShowHealEffect(BattleActor target, int healAmount, bool isCritical)
    {
        if (BattleUIManager.Instance != null)
        {
            // UI에 힐 숫자 표시
            // BattleUIManager.Instance.ShowHealNumber(target, healAmount, isCritical);
        }

        if (BattleEffectManager.Instance != null)
        {
            string effectId = isCritical ? "CriticalHealEffect" : "HealEffect";
            await BattleEffectManager.Instance.PlayEffect(effectId, target);
        }
    }

    /// <summary>
    /// 힐 차단 이펙트 표시
    /// </summary>
    protected virtual void ShowHealBlockedEffect(BattleActor target)
    {
        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.HealBlocked);
        }
    }

    /// <summary>
    /// 힐 이벤트 발생
    /// </summary>
    protected virtual void TriggerHealEvent(BattleActor target, int healAmount, bool isCritical)
    {
        // 이벤트 시스템이 있다면 힐 이벤트 발생
        var healData = new HealEventData(BattleEventType.AfterHeal)
        {
            Source = Sender,
            Target = target,
            HealAmount = healAmount,
            IsCritical = isCritical
        };

        BattleEventManager.Instance?.TriggerEvent(BattleEventType.AfterHeal, healData);
    }





    protected virtual void ApplyBuff(AdvancedSkillEffect effect, BattleActor target)
    {
        // 기존 버프 체크 (중첩 처리)
        var existingBuff = FindExistingBuffDebuff(target, effect, true);

        if (existingBuff != null && effect.canStack)
        {
            // 중첩 처리
            HandleStacking(existingBuff, effect, target, true);
        }
        else if (existingBuff != null && !effect.canStack)
        {
            // 중첩 불가 - 지속시간 갱신 또는 더 강한 효과로 교체
            HandleNonStacking(existingBuff, effect, target, true);
        }
        else
        {
            // 새로운 버프 적용
            ApplyNewBuffDebuff(effect, target, true);
        }

        // 시각 효과
        ShowBuffEffect(target, effect).Forget();

        // 이벤트 발생
        TriggerBuffDebuffEvent(target, effect, true, true);
    }


    /// <summary>
    /// 개선된 디버프 적용
    /// </summary>
    protected virtual void ApplyDebuff(AdvancedSkillEffect effect, BattleActor target)
    {
        // 디버프 저항 체크
        if (!effect.ignoreResistance && CheckDebuffResistance(target, effect))
        {
            Debug.Log($"[Debuff] {target.name} resisted {effect.statType} debuff!");
            ShowResistEffect(target);
            return;
        }

        // 디버프 면역 체크
        if (CheckDebuffImmunity(target, effect))
        {
            Debug.Log($"[Debuff] {target.name} is immune to {effect.statType} debuff!");
            ShowImmuneEffect(target);
            return;
        }

        // 기존 디버프 체크
        var existingDebuff = FindExistingBuffDebuff(target, effect, false);

        if (existingDebuff != null && effect.canStack)
        {
            // 중첩 처리
            HandleStacking(existingDebuff, effect, target, false);
        }
        else if (existingDebuff != null && !effect.canStack)
        {
            // 중첩 불가 - 지속시간 갱신 또는 더 강한 효과로 교체
            HandleNonStacking(existingDebuff, effect, target, false);
        }
        else
        {
            // 새로운 디버프 적용
            ApplyNewBuffDebuff(effect, target, false);
        }

        // 시각 효과
        ShowDebuffEffect(target, effect).Forget();

        // 이벤트 발생
        TriggerBuffDebuffEvent(target, effect, false, true);
    }



    /// <summary>
    /// 새로운 버프/디버프 적용
    /// </summary>
    private void ApplyNewBuffDebuff(AdvancedSkillEffect effect, BattleActor target, bool isBuff)
    {
        // 적용할 값 계산
        float applyValue = CalculateBuffDebuffValue(effect, target, isBuff);

        // CharacterStatType 변환
        var charStatType = ConvertToCharacterStatType(effect.statType);

        if (charStatType != CharacterStatType.None)
        {
            // 실제 스탯 변경
            target.BattleActorInfo.AddStatBuffValue(charStatType, applyValue);

            // 인스턴스 생성 및 저장
            var instance = new BuffDebuffInstance
            {
                Effect = effect,
                Source = Sender,
                Target = target,
                CurrentStacks = 1,
                RemainingTurns = effect.duration,
                CurrentValue = applyValue,
                OriginalValue = applyValue
            };

            // 저장
            if (!activeBuffDebuffs.ContainsKey(target))
                activeBuffDebuffs[target] = new List<BuffDebuffInstance>();

            activeBuffDebuffs[target].Add(instance);

            Debug.Log($"[{(isBuff ? "Buff" : "Debuff")}] Applied {effect.statType} " +
                     $"{(effect.isFixedValue ? applyValue.ToString("F0") : applyValue.ToString("P"))} " +
                     $"to {target.name} for {effect.duration} turns");
        }
        else
        {
            Debug.LogError("### 버프 적용안댐 ApplyNewBuffDebuff ");
        }
    }

    /// <summary>
    /// 중첩 처리
    /// </summary>
    private void HandleStacking(BuffDebuffInstance existing, AdvancedSkillEffect newEffect,
                                BattleActor target, bool isBuff)
    {
        if (existing.CurrentStacks < newEffect.maxStackCount)
        {
            // 스택 증가
            existing.CurrentStacks++;

            // 추가 효과 계산
            float additionalValue = CalculateStackValue(newEffect, existing.CurrentStacks);

            // 스탯 업데이트
            var charStatType = ConvertToCharacterStatType(newEffect.statType);
            if (charStatType != CharacterStatType.None)
            {
                target.BattleActorInfo.AddStatBuffValue(charStatType, additionalValue);
                existing.CurrentValue += additionalValue;
            }

            // 지속시간 갱신
            if (newEffect.refreshDurationOnStack)
            {
                existing.RemainingTurns = newEffect.duration;
            }

            Debug.Log($"[Stack] {existing.Effect.statType} stacked to {existing.CurrentStacks}x " +
                     $"on {target.name} (Total: {existing.CurrentValue})");
        }
        else
        {
            Debug.Log($"[Stack] {existing.Effect.statType} already at max stacks " +
                     $"({newEffect.maxStackCount}) on {target.name}");

            // 최대 스택이어도 지속시간은 갱신
            if (newEffect.refreshDurationOnStack)
            {
                existing.RemainingTurns = newEffect.duration;
            }
        }
    }

    /// <summary>
    /// 중첩 불가 처리
    /// </summary>
    private void HandleNonStacking(BuffDebuffInstance existing, AdvancedSkillEffect newEffect,
                                   BattleActor target, bool isBuff)
    {
        float newValue = CalculateBuffDebuffValue(newEffect, target, isBuff);

        // 더 강한 효과로 교체
        if (Mathf.Abs(newValue) > Mathf.Abs(existing.CurrentValue))
        {
            // 기존 효과 제거
            var charStatType = ConvertToCharacterStatType(existing.Effect.statType);
            if (charStatType != CharacterStatType.None)
            {
                target.BattleActorInfo.RemoveStatBuffValue(charStatType, existing.CurrentValue);
            }

            // 새 효과 적용
            existing.Effect = newEffect;
            existing.CurrentValue = newValue;
            existing.OriginalValue = newValue;
            existing.RemainingTurns = newEffect.duration;

            if (charStatType != CharacterStatType.None)
            {
                target.BattleActorInfo.AddStatBuffValue(charStatType, newValue);
            }

            Debug.Log($"[Replace] {newEffect.statType} replaced with stronger effect on {target.name}");
        }
        else
        {
            // 지속시간만 갱신
            existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, newEffect.duration);
            Debug.Log($"[Refresh] {newEffect.statType} duration refreshed on {target.name}");
        }
    }

    /// <summary>
    /// 버프/디버프 값 계산
    /// </summary>
    private float CalculateBuffDebuffValue(AdvancedSkillEffect effect, BattleActor target, bool isBuff)
    {
        float value;

        if (effect.isFixedValue)
        {
            // 고정값
            value = effect.value;
        }
        else
        {
            // 퍼센트 (0.3 = 30%)
            value = effect.value / 100f;
        }

        // 디버프는 음수로
        if (!isBuff)
        {
            value = -Mathf.Abs(value);
        }

        return value;
    }

    /// <summary>
    /// 스택 값 계산
    /// </summary>
    private float CalculateStackValue(AdvancedSkillEffect effect, int currentStacks)
    {
        float baseValue = effect.isFixedValue ? effect.value : (effect.value / 100f);

        // 스택 배율 적용
        float stackValue = baseValue * effect.stackMultiplier;

        return stackValue;
    }

    /// <summary>
    /// 디버프 저항 체크
    /// </summary>
    private bool CheckDebuffResistance(BattleActor target, AdvancedSkillEffect effect)
    {
        // 효과 저항 스탯 확인
        float resistance = target.BattleActorInfo.GetStatFloat(CharacterStatType.EffectResist);
        float hitChance = Sender.BattleActorInfo.GetStatFloat(CharacterStatType.EffectHit);

        float resistChance = resistance - hitChance;
        resistChance = Mathf.Clamp(resistChance, 0, 90);  // 최대 90% 저항

        return UnityEngine.Random.Range(0f, 100f) < resistChance;
    }

    /// <summary>
    /// 디버프 면역 체크
    /// </summary>
    private bool CheckDebuffImmunity(BattleActor target, AdvancedSkillEffect effect)
    {
        // 보스는 특정 디버프에 면역
        if (target.BattleActorInfo.IsBoss)
        {
            // 보스가 면역인 스탯 타입 정의
            var bossImmuneStats = new List<StatType>
            {
                StatType.Speed,  // 속도 감소 면역
                StatType.Attack  // 공격력 감소 면역 (예시)
            };

            return bossImmuneStats.Contains(effect.statType);
        }

        return false;
    }

    /// <summary>
    /// 기존 버프/디버프 찾기
    /// </summary>
    private BuffDebuffInstance FindExistingBuffDebuff(BattleActor target, AdvancedSkillEffect effect, bool isBuff)
    {
        if (!activeBuffDebuffs.ContainsKey(target))
            return null;

        return activeBuffDebuffs[target].Find(x =>
            x.Effect.statType == effect.statType &&
            x.Effect.IsBuffType() == isBuff);
    }

    /// <summary>
    /// 버프 제거
    /// </summary>
    protected virtual void RemoveBuff(AdvancedSkillEffect effect, BattleActor target)
    {
        if (!activeBuffDebuffs.ContainsKey(target))
            return;

        var instance = activeBuffDebuffs[target].Find(x =>
            x.Effect.statType == effect.statType && x.Effect.IsBuffType());

        if (instance != null)
        {
            // 스탯 복구
            var charStatType = ConvertToCharacterStatType(effect.statType);
            if (charStatType != CharacterStatType.None)
            {
                target.BattleActorInfo.RemoveStatBuffValue(charStatType, instance.CurrentValue);
            }

            // 인스턴스 제거
            activeBuffDebuffs[target].Remove(instance);

            Debug.Log($"[Buff] Removed {effect.statType} from {target.name}");

            // 이벤트 발생
            TriggerBuffDebuffEvent(target, effect, true, false);
        }
    }

    /// <summary>
    /// 디버프 제거
    /// </summary>
    protected virtual void RemoveDebuff(AdvancedSkillEffect effect, BattleActor target)
    {
        if (!activeBuffDebuffs.ContainsKey(target))
            return;

        var instance = activeBuffDebuffs[target].Find(x =>
            x.Effect.statType == effect.statType && x.Effect.IsDebuffType());

        if (instance != null)
        {
            // 스탯 복구
            var charStatType = ConvertToCharacterStatType(effect.statType);
            if (charStatType != CharacterStatType.None)
            {
                target.BattleActorInfo.RemoveStatBuffValue(charStatType, instance.CurrentValue);
            }

            // 인스턴스 제거
            activeBuffDebuffs[target].Remove(instance);

            Debug.Log($"[Debuff] Removed {effect.statType} from {target.name}");

            // 이벤트 발생
            TriggerBuffDebuffEvent(target, effect, false, false);
        }
    }

    /// <summary>
    /// 시각 효과 표시
    /// </summary>
    private async UniTask ShowBuffEffect(BattleActor target, AdvancedSkillEffect effect)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("BuffEffect", target);
        }
    }

    private async UniTask ShowDebuffEffect(BattleActor target, AdvancedSkillEffect effect)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("DebuffEffect", target);
        }
    }

    private void ShowResistEffect(BattleActor target)
    {
        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Resist);
        }
    }

    private void ShowImmuneEffect(BattleActor target)
    {
        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.ShowBattleText(target, BattleTextType.Immune);
        }
    }

    /// <summary>
    /// 버프/디버프 이벤트 발생
    /// </summary>
    private void TriggerBuffDebuffEvent(BattleActor target, AdvancedSkillEffect effect,
                                        bool isBuff, bool isApply)
    {
        var eventType = isApply ?
            (isBuff ? BattleEventType.BuffApplied : BattleEventType.DebuffApplied) :
            BattleEventType.StatusRemoved;

        var eventData = new StatusEventData(eventType)
        {
            Actor = target,
            StatusName = effect.statType.ToString(),
            Duration = effect.duration,
            IsPositive = isBuff
        };

        BattleEventManager.Instance?.TriggerEvent(eventType, eventData);
    }








    protected virtual void ApplyStatusEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        Debug.Log($"[StatusEffect] Applying {effect.statusType} to {target.name}");

        // CC 저항 체크
        if (effect.IsCC() && !effect.ignoreCCResist)
        {
            if (CheckCCResistance(target, effect))
            {
                Debug.Log($"[StatusEffect] {target.name} resisted {effect.statusType}!");
                ShowResistEffect(target);
                return;
            }
        }

        // 보스 면역 체크
        if (target.BattleActorInfo.IsBoss && !effect.ignoreBossImmunity)
        {
            if (IsBossImmuneToStatus(effect.statusType))
            {
                Debug.Log($"[StatusEffect] Boss is immune to {effect.statusType}!");
                ShowImmuneEffect(target);
                return;
            }
        }

        // 기존 상태이상 체크 (중첩 처리)
        var existing = FindExistingStatusEffect(target, effect.statusType);

        if (existing != null && effect.statusCanStack)
        {
            HandleStatusStacking(existing, effect, target);
        }
        else if (existing != null && !effect.statusCanStack)
        {
            HandleStatusRefresh(existing, effect, target);
        }
        else
        {
            ApplyNewStatusEffect(effect, target);
        }

        // 시각 효과
        ShowStatusEffectVisual(target, effect).Forget();
    }


    /// <summary>
    /// 새로운 상태이상 적용
    /// </summary>
    private void ApplyNewStatusEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        // 인스턴스 생성
        var instance = new StatusEffectInstance
        {
            Type = effect.statusType,
            Source = Sender,
            Target = target,
            RemainingTurns = effect.duration,
            Value = effect.value
        };

        // 저장
        if (!activeStatusEffects.ContainsKey(target))
            activeStatusEffects[target] = new List<StatusEffectInstance>();

        activeStatusEffects[target].Add(instance);

        // 즉시 효과 적용
        ApplyStatusInstantEffect(effect, target);

        // DoT라면 첫 틱 데미지
        if (effect.IsDoT())
        {
            ApplyDoTDamage(instance, effect);
        }

        Debug.Log($"[StatusEffect] {effect.statusType} applied to {target.name} for {effect.duration} turns");
    }

    /// <summary>
    /// 상태이상 즉시 효과 (행동 제한 등)
    /// </summary>
    private void ApplyStatusInstantEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        switch (effect.statusType)
        {
            case SkillSystem.StatusType.Stun:
            case SkillSystem.StatusType.Freeze:
            case SkillSystem.StatusType.Petrify:
                target.SetStunned(true);
                target.BattleActorInfo.AddStatusFlag(StatusFlag.CannotAct);
                Debug.Log($"[StatusEffect] {target.name} - Added StatusFlag.CannotAct (Stun/Freeze/Petrify)");

                break;

            case SkillSystem.StatusType.Silence:
            case SkillSystem.StatusType.SkillSeal:
                target.BattleActorInfo.AddStatusFlag(StatusFlag.CannotUseSkill);
                Debug.Log($"{target.name} is silenced!");
                Debug.Log($"[StatusEffect] {target.name} - Added StatusFlag.CannotUseSkill (Silence)");

                break;

            case SkillSystem.StatusType.Sleep:
                target.SetStunned(true);
                target.BattleActorInfo.AddStatusFlag(StatusFlag.Sleeping);
                Debug.Log($"[StatusEffect] {target.name} - Added StatusFlag.CannotAct + Sleeping");

                break;

            case SkillSystem.StatusType.Taunt:
                // 도발: 타겟 강제 변경
                ApplyTauntEffect(target);
                break;

            case SkillSystem.StatusType.Blind:
                // 실명: 명중률 대폭 감소
                target.BattleActorInfo.AddStatBuffValue(CharacterStatType.Hit, -50f);
                Debug.Log($"[StatusEffect] {target.name} - Reduced hit rate (Blind)");

                break;

            case SkillSystem.StatusType.Slow:
                // 둔화: 속도 감소
                float slowAmount = effect.value > 0 ? -effect.value : -30f;
                target.BattleActorInfo.AddStatBuffValue(CharacterStatType.TurnSpeed, slowAmount / 100f);
                Debug.Log($"[StatusEffect] {target.name} - Reduced speed by {slowAmount}% (Slow)");

                break;

            case SkillSystem.StatusType.Confuse:
                target.BattleActorInfo.AddStatusFlag(StatusFlag.Confused);
                Debug.Log($"[StatusEffect] {target.name} - Added StatusFlag.Confused");

                break;

            case SkillSystem.StatusType.Fear:
                target.BattleActorInfo.AddStatusFlag(StatusFlag.Feared);
                Debug.Log($"[StatusEffect] {target.name} - Added StatusFlag.Feared");

                // 일정 확률로 행동 불가
                break;
            default:
                Debug.LogWarning($"[StatusEffect] Unhandled status type: {effect.statusType}");
                break;
        }
    }

    /// <summary>
    /// 상태이상 제거
    /// </summary>
    protected virtual void RemoveStatusEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        if (!activeStatusEffects.ContainsKey(target))
            return;

        var instance = activeStatusEffects[target].Find(x => x.Type == effect.statusType);

        if (instance != null)
        {
            // 상태 제거
            RemoveStatusInstantEffect(effect, target);

            // 인스턴스 제거
            activeStatusEffects[target].Remove(instance);

            Debug.Log($"[StatusEffect] {effect.statusType} removed from {target.name}");
        }
    }

    /// <summary>
    /// 상태이상 즉시 효과 제거
    /// </summary>
    private void RemoveStatusInstantEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        switch (effect.statusType)
        {
            case SkillSystem.StatusType.Stun:
            case SkillSystem.StatusType.Freeze:
            case SkillSystem.StatusType.Petrify:
                target.SetStunned(false);
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.CannotAct);
                break;

            case SkillSystem.StatusType.Silence:
            case SkillSystem.StatusType.SkillSeal:
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.CannotUseSkill);
                break;

            case SkillSystem.StatusType.Sleep:
                target.SetStunned(false);
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.Sleeping);
                break;

            case SkillSystem.StatusType.Blind:
                // 실명 해제: 명중률 복구
                target.BattleActorInfo.RemoveStatBuffValue(CharacterStatType.Hit, -50f);
                break;

            case SkillSystem.StatusType.Slow:
                // 둔화 해제: 속도 복구
                float slowAmount = effect.value > 0 ? -effect.value : -30f;
                target.BattleActorInfo.RemoveStatBuffValue(CharacterStatType.TurnSpeed, slowAmount / 100f);
                break;

            case SkillSystem.StatusType.Confuse:
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.Confused);
                break;

            case SkillSystem.StatusType.Fear:
                target.BattleActorInfo.RemoveStatusFlag(StatusFlag.Feared);
                break;
        }
    }

    /// <summary>
    /// DoT 데미지 적용
    /// </summary>
    private void ApplyDoTDamage(StatusEffectInstance instance, AdvancedSkillEffect effect)
    {
        int damage = CalculateDoTDamage(effect, instance.Target);

        if (damage > 0)
        {
            // DoT는 방어 무시가 일반적
            var result = new BattleFormularHelper.DamageResult
            {
                FinalDamage = damage,
                OriginalDamage = damage,
                Judgement = BattleFormularHelper.JudgementType.Hit,
                Options = BattleFormularHelper.DamageOption.IgnoreDefense
            };

            instance.Target.TakeDamageWithResult(result, instance.Source).Forget();

            instance.TotalDamageDealt += damage;
            instance.TickCount++;

            Debug.Log($"[DoT] {instance.Type} deals {damage} damage to {instance.Target.name} " +
                     $"(Tick: {instance.TickCount}, Total: {instance.TotalDamageDealt})");
        }
    }

    /// <summary>
    /// DoT 데미지 계산
    /// </summary>
    private int CalculateDoTDamage(AdvancedSkillEffect effect, BattleActor target)
    {
        float damage = 0;

        switch (effect.dotCalcType)
        {
            case SkillSystem.DotCalculationType.AttackBased:
                // 시전자 공격력의 X%
                damage = Sender.BattleActorInfo.Attack * (effect.value / 100f);
                break;

            case SkillSystem.DotCalculationType.MaxHpBased:
                // 대상 최대 HP의 X%
                damage = target.BattleActorInfo.MaxHp * (effect.value / 100f);
                break;

            case SkillSystem.DotCalculationType.CurrentHpBased:
                // 대상 현재 HP의 X%
                damage = target.BattleActorInfo.Hp * (effect.value / 100f);
                break;

            case SkillSystem.DotCalculationType.FixedDamage:
                // 고정 데미지
                damage = effect.value;
                break;
        }

        // 속성 보정 (화상은 화속성, 독은 독속성 등)
        if (effect.statusType == SkillSystem.StatusType.Burn)
        {
            // 화속성 저항 체크
            float fireResist = target.BattleActorInfo.GetStatFloat(CharacterStatType.ElementalRes);
            damage *= (1f - fireResist / 100f);
        }

        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    /// <summary>
    /// 매 턴 상태이상 처리 (ProcessTurnBasedEffects에서 호출)
    /// </summary>
    public static void ProcessAllStatusEffects()
    {
        foreach (var kvp in activeStatusEffects)
        {
            var target = kvp.Key;
            var effects = kvp.Value.ToList(); // 복사본으로 작업

            foreach (var instance in effects)
            {
                if (!instance.IsActive) continue;

                // 턴 감소
                instance.RemainingTurns--;

                // DoT 처리
                if (IsDoTStatus(instance.Type))
                {
                    // 임시 effect 생성 (데이터 전달용)
                    var tempEffect = new AdvancedSkillEffect
                    {
                        statusType = instance.Type,
                        value = instance.Value
                    };

                    var runtime = new AdvancedSkillRuntime();
                    runtime.Sender = instance.Source;
                    runtime.ApplyDoTDamage(instance, tempEffect);
                }

                // 종료 체크
                if (instance.RemainingTurns <= 0)
                {
                    instance.IsActive = false;
                    // RemoveStatusEffect 호출
                }
            }

            // 비활성 효과 제거
            kvp.Value.RemoveAll(x => !x.IsActive);
        }
    }

    /// <summary>
    /// CC 저항 체크
    /// </summary>
    private bool CheckCCResistance(BattleActor target, AdvancedSkillEffect effect)
    {
        float ccResist = target.BattleActorInfo.GetStatFloat(CharacterStatType.Tenacity);
        float ccEnhance = Sender.BattleActorInfo.GetStatFloat(CharacterStatType.CCEnhance);

        float resistChance = ccResist - ccEnhance;
        resistChance = Mathf.Clamp(resistChance, 0, 80); // 최대 80% 저항

        return UnityEngine.Random.Range(0f, 100f) < resistChance;
    }

    /// <summary>
    /// 보스 면역 체크
    /// </summary>
    private bool IsBossImmuneToStatus(SkillSystem.StatusType statusType)
    {
        // 보스가 면역인 상태이상 목록
        var bossImmuneList = new List<SkillSystem.StatusType>
        {
            SkillSystem.StatusType.Stun,
            SkillSystem.StatusType.Freeze,
            SkillSystem.StatusType.Petrify,
            SkillSystem.StatusType.Sleep,
            SkillSystem.StatusType.Fear
        };

        return bossImmuneList.Contains(statusType);
    }

    /// <summary>
    /// 도발 효과 적용
    /// </summary>
    private void ApplyTauntEffect(BattleActor taunter)
    {
        var battleManager = BattleProcessManagerNew.Instance;
        if (battleManager == null) return;

        bool isAlly = taunter.BattleActorInfo.IsAlly;
        var enemies = isAlly ?
            battleManager.GetEnemyActors() :
            battleManager.GetAllyActors();

        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                // 강제 타겟 설정
                enemy.SetForcedTarget(taunter);
                Debug.Log($"[Taunt] {enemy.name} is taunted by {taunter.name}");
            }
        }
    }

    /// <summary>
    /// 기존 상태이상 찾기
    /// </summary>
    private StatusEffectInstance FindExistingStatusEffect(BattleActor target, SkillSystem.StatusType type)
    {
        if (!activeStatusEffects.ContainsKey(target))
            return null;

        return activeStatusEffects[target].Find(x => x.Type == type && x.IsActive);
    }

    /// <summary>
    /// 상태이상 중첩 처리
    /// </summary>
    private void HandleStatusStacking(StatusEffectInstance existing, AdvancedSkillEffect newEffect, BattleActor target)
    {
        // DoT의 경우 데미지 증가
        if (newEffect.IsDoT())
        {
            existing.Value += newEffect.value * 0.5f; // 50% 추가 데미지
            existing.RemainingTurns = Math.Max(existing.RemainingTurns, newEffect.duration);
            Debug.Log($"[StatusEffect] {newEffect.statusType} stacked on {target.name}");
        }
    }

    /// <summary>
    /// 상태이상 갱신 처리
    /// </summary>
    private void HandleStatusRefresh(StatusEffectInstance existing, AdvancedSkillEffect newEffect, BattleActor target)
    {
        // 지속시간 갱신
        existing.RemainingTurns = Math.Max(existing.RemainingTurns, newEffect.duration);

        // 더 강한 효과로 교체
        if (newEffect.value > existing.Value)
        {
            existing.Value = newEffect.value;
        }

        Debug.Log($"[StatusEffect] {newEffect.statusType} refreshed on {target.name}");
    }

    /// <summary>
    /// DoT 상태인지 체크
    /// </summary>
    private static bool IsDoTStatus(SkillSystem.StatusType type)
    {
        return type == SkillSystem.StatusType.Poison ||
               type == SkillSystem.StatusType.Burn ||
               type == SkillSystem.StatusType.Bleed ||
               type == SkillSystem.StatusType.Bleeding;
    }

    /// <summary>
    /// 시각 효과
    /// </summary>
    private async UniTask ShowStatusEffectVisual(BattleActor target, AdvancedSkillEffect effect)
    {
        string effectId = $"{effect.statusType}Effect";

        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect(effectId, target);
        }

        // UI 아이콘 표시
       /* if (target.actorTag != null)
        {
            // target.actorTag.AddStatusIcon(effect.statusType);
        }*/

    }


    protected virtual void ApplyShield(AdvancedSkillEffect effect, BattleActor target)
    {
        // Shield 기준값 결정
        int baseValue = 0;

        switch (effect.shieldBase)
        {
            case SkillSystem.ShieldBase.MaxHP:
                baseValue = target.BattleActorInfo.MaxHp;
                break;
            case SkillSystem.ShieldBase.Defense:
                baseValue = target.BattleActorInfo.Defence;
                break;
            case SkillSystem.ShieldBase.Attack:
                baseValue = Sender.BattleActorInfo.Attack;
                break;
            default:
                baseValue = target.BattleActorInfo.MaxHp;
                break;
        }

        // Shield 양 계산
        int shieldAmount = Mathf.RoundToInt(baseValue * (effect.value / 100f));

        // Shield 추가 (누적 또는 교체)
        if (effect.shieldCanStack)
        {
            // 누적
            target.BattleActorInfo.Shield += shieldAmount;
            Debug.Log($"[Shield] Added {shieldAmount} shield to {target.name}. Total: {target.BattleActorInfo.Shield}");
        }
        else
        {
            // 더 큰 값으로 교체
            if (shieldAmount > target.BattleActorInfo.Shield)
            {
                target.BattleActorInfo.Shield = shieldAmount;
                Debug.Log($"[Shield] Replaced shield on {target.name} with {shieldAmount}");
            }
            else
            {
                Debug.Log($"[Shield] Shield not applied (existing shield is stronger)");
            }
        }

        // Shield 이펙트
        ShowShieldEffect(target, shieldAmount).Forget();
    }

    protected virtual void RemoveShield(AdvancedSkillEffect effect, BattleActor target)
    {
        // 특정 Shield 제거 또는 전체 제거
        if (effect.shieldRemoveAmount > 0)
        {
            // 부분 제거
            int removed = Mathf.Min(target.BattleActorInfo.Shield, effect.shieldRemoveAmount);
            target.BattleActorInfo.Shield -= removed;
            Debug.Log($"[Shield] Removed {removed} shield from {target.name}. Remaining: {target.BattleActorInfo.Shield}");
        }
        else
        {
            // 전체 제거
            int previousShield = target.BattleActorInfo.Shield;
            target.BattleActorInfo.Shield = 0;
            Debug.Log($"[Shield] Removed all shield ({previousShield}) from {target.name}");
        }
    }

    // Shield 이펙트 표시
    private async UniTask ShowShieldEffect(BattleActor target, int amount)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("ShieldApply", target);
        }

        // UI 표시
        if (BattleUIManager.Instance != null)
        {
            // Shield 수치 표시
            // BattleUIManager.Instance.ShowShieldText(target.transform.position, $"+{amount}", Color.cyan);
        }
    }

    protected virtual void ApplyDispel(AdvancedSkillEffect effect, BattleActor target)
    {
        if (target.SkillManager == null)
        {
            Debug.LogWarning($"[Dispel] {target.name} has no SkillManager");
            return;
        }

        int dispelCount = effect.dispelCount > 0 ? effect.dispelCount : 1;
        int actualRemoved = 0;

        Debug.Log($"[Dispel] Attempting to dispel {dispelCount} effects of type {effect.dispelType} from {target.name}");

        switch (effect.dispelType)
        {
            case SkillSystem.DispelType.Buff:
                actualRemoved = target.SkillManager.RemoveBuffs(dispelCount, effect.dispelPriority);
                break;

            case SkillSystem.DispelType.Debuff:
                actualRemoved = target.SkillManager.RemoveDebuffs(dispelCount, effect.dispelPriority);
                break;

            case SkillSystem.DispelType.StatusEffect:
                actualRemoved = target.SkillManager.RemoveStatusEffects(dispelCount, effect.dispelPriority);
                break;

            case SkillSystem.DispelType.All:
                // 모든 효과 제거 (debuff 우선)
                int debuffsRemoved = target.SkillManager.RemoveDebuffs(dispelCount / 2, effect.dispelPriority);
                int buffsRemoved = target.SkillManager.RemoveBuffs(dispelCount - debuffsRemoved, effect.dispelPriority);
                actualRemoved = debuffsRemoved + buffsRemoved;
                break;

            case SkillSystem.DispelType.Shield:
                // Shield만 특별 처리
                if (target.BattleActorInfo.Shield > 0)
                {
                    int previousShield = target.BattleActorInfo.Shield;
                    target.BattleActorInfo.Shield = 0;
                    actualRemoved = 1;
                    Debug.Log($"[Dispel] Removed shield ({previousShield}) from {target.name}");
                }
                break;

            default:
                Debug.LogWarning($"[Dispel] Unknown dispel type: {effect.dispelType}");
                break;
        }

        // Dispel 결과 로그
        if (actualRemoved > 0)
        {
            Debug.Log($"[Dispel] Successfully removed {actualRemoved} effects from {target.name}");

            // Dispel 성공 이펙트
            ShowDispelEffect(target, actualRemoved).Forget();
        }
        else
        {
            Debug.Log($"[Dispel] No effects to remove from {target.name}");
        }
    }

    // Dispel 이펙트 표시
    private async UniTask ShowDispelEffect(BattleActor target, int count)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect("DispelEffect", target);
        }

        if (BattleUIManager.Instance != null)
        {
            // UI 텍스트 표시
            /*BattleUIManager.Instance.ShowBattleText(
                target.transform.position + Vector3.up * 2f,
                $"Dispel x{count}",
                Color.magenta
            );*/
        }
    }


    protected virtual void ApplySpecialEffect(AdvancedSkillEffect effect, BattleActor target)
    {
        // 기본적으로는 로그만 출력
        // 특수 효과는 상속 클래스에서 HandleCustomEffect로 처리
        Debug.Log($"[AdvancedSkill] Special effect: {effect.specialEffect}");


        // TextDisplay 처리
        if (effect.specialEffect != null && effect.specialEffect.StartsWith("TextDisplay:"))
        {
            string displayText = effect.specialEffect.Substring("TextDisplay:".Length);
            if (string.IsNullOrEmpty(displayText))
                displayText = "수리 완료!";

            // BattleUIManager 호출
            if (BattleUIManager.Instance != null)
            {
               // BattleUIManager.Instance.ShowCustomText(target, displayText, Color.green);
            }
        }
        
        else
        {
            // 기본 로그
            Debug.Log($"[AdvancedSkill] Special effect: {effect.specialEffect}");
        }


    }
    #endregion

    #region Calculation Helpers
    protected virtual int CalculateDamageValue(float baseValue)
    {
        var attackPower = Sender.BattleActorInfo.Attack;
        return Mathf.RoundToInt(attackPower * (baseValue / 100f));
    }

    protected virtual int CalculateHealValue(float baseValue)
    {
        var maxHp = Receiver.BattleActorInfo.MaxHp;
        return Mathf.RoundToInt(maxHp * (baseValue / 100f));
    }

    protected virtual int CalculateShieldValue(float baseValue)
    {
        var maxHp = Receiver.BattleActorInfo.MaxHp;
        return Mathf.RoundToInt(maxHp * (baseValue / 100f));
    }

    protected virtual float CalculateStatModifier(SkillSystem.StatType statType, float baseValue)
    {
        // 특수한 스탯들 처리
        switch (statType)
        {
            case SkillSystem.StatType.MaxHP:
                // HP는 퍼센트로 처리
                return baseValue / 100f;

            case SkillSystem.StatType.HealBlock:
                // HealBlock은 100이면 완전 차단
                return baseValue;

            case SkillSystem.StatType.HealReduction:
                // HealReduction은 퍼센트
                return baseValue;

            default:
                // 기본적으로 퍼센트로 처리
                return baseValue / 100f;
        }
    }

    protected CharacterStatType ConvertToCharacterStatType(SkillSystem.StatType statType)
    {
        return statType switch
        {
            // 기본 스탯
            SkillSystem.StatType.Attack => CharacterStatType.Attack,
            SkillSystem.StatType.Defense => CharacterStatType.Defence,
            SkillSystem.StatType.Speed => CharacterStatType.TurnSpeed,

            // 크리티컬 관련
            SkillSystem.StatType.CritRate => CharacterStatType.CriRate,
            SkillSystem.StatType.CritDamage => CharacterStatType.CriDmg,

            // 명중/회피
            SkillSystem.StatType.Accuracy => CharacterStatType.Hit,
            SkillSystem.StatType.Evasion => CharacterStatType.Dodge,

            // 효과 적중/저항
            SkillSystem.StatType.EffectHit => CharacterStatType.SkillHit,
            SkillSystem.StatType.EffectResist => CharacterStatType.SkillResist,

            // HP
            SkillSystem.StatType.MaxHP => CharacterStatType.MaxHp,

            // 힐 관련 (매핑 필요)
            SkillSystem.StatType.HealBlock => CharacterStatType.HealBlock,
            SkillSystem.StatType.HealReduction => CharacterStatType.HealReduction,

            _ => CharacterStatType.None
        };
    }

    /// <summary>
    /// 역변환: CharacterStatType을 SkillSystem.StatType으로
    /// </summary>
    protected SkillSystem.StatType ConvertToSkillStatType(CharacterStatType charStatType)
    {
        return charStatType switch
        {
            CharacterStatType.Attack => SkillSystem.StatType.Attack,
            CharacterStatType.Defence => SkillSystem.StatType.Defense,
            CharacterStatType.TurnSpeed => SkillSystem.StatType.Speed,
            CharacterStatType.CriRate => SkillSystem.StatType.CritRate,
            CharacterStatType.CriDmg => SkillSystem.StatType.CritDamage,
            CharacterStatType.Hit => SkillSystem.StatType.Accuracy,
            CharacterStatType.Dodge => SkillSystem.StatType.Evasion,
            CharacterStatType.SkillHit => SkillSystem.StatType.EffectHit,
            CharacterStatType.SkillResist => SkillSystem.StatType.EffectResist,
            CharacterStatType.MaxHp => SkillSystem.StatType.MaxHP,
            CharacterStatType.HealBlock => SkillSystem.StatType.HealBlock,
            CharacterStatType.HealReduction => SkillSystem.StatType.HealReduction,
            _ => SkillSystem.StatType.None
        };
    }



    #endregion

    #region Observer Pattern
    protected virtual BattleNewObserverID[] UseObserverIds => null;

    public void HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        OnHandleMessage(observerMessage, observerParam);
    }

    protected virtual void OnHandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (observerMessage is BattleNewObserverID battleId )
        {
            if (Category == SkillSystem.SkillCategory.Passive)
            {
                if (ShouldTrigger(battleId))
                {
                    ExecuteEffects();
                }
            }
            else
            {
                if (battleId == BattleNewObserverID.Kill)
                {
                    // Kill 이벤트 파라미터에서 처치한 액터 확인
                    if (observerParam is BattleObserverParam battleParam && battleParam.Attacker == Sender)
                    {
                        ProcessKillBuff();
                    }

                }
            }
        }

        // Kill 이벤트 처리
       /* if (observerMessage is BattleNewObserverID battleId && battleId == BattleNewObserverID.Kill)
        {
            // Kill 이벤트 파라미터에서 처치한 액터 확인
            if (observerParam is BattleObserverParam battleParam && battleParam.Attacker == Sender)
            {
                ProcessKillBuff();
            }
        }


        // 패시브 스킬 트리거 처리
        if (Category == SkillSystem.SkillCategory.Passive &&
            observerMessage is BattleNewObserverID battleId)
        {
            if (ShouldTrigger(battleId))
            {
                ExecuteEffects();
            }
        }*/
    }
    private void ProcessKillBuff()
    {
        Debug.Log($"[KillBuff] {Sender.name}이(가) 적을 처치! 버프 발동");

        foreach (var effect in SkillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.Special && effect.specialEffect == "OnKillBuff")
            {
                // 처치 시 버프 효과 적용
                ApplyKillBuffEffects(effect);
            }
        }
    }


    private void ApplyKillBuffEffects(AdvancedSkillEffect effect)
    {
        //이거 현재는 임시다. 
        // 버프 효과 결정 (effect.value로 버프 강도 결정)
        var buffEffect = new AdvancedSkillEffect
        {
            type = SkillSystem.EffectType.Buff,
            targetType = SkillSystem.TargetType.Self,
            statType = effect.statType != StatType.None ? effect.statType : StatType.Attack,
            value = effect.value > 0 ? effect.value : 20,  // 기본 20% 증가
            duration = effect.duration > 0 ? effect.duration : 2,  // 기본 2턴
            chance = 1.0f
        };

        // 버프 적용
        ApplyBuff(buffEffect, Sender);

        // 시각 효과
        ShowKillBuffEffect(Sender).Forget();
    }


    private async UniTask ShowKillBuffEffect(BattleActor target)
    {
        if (BattleEffectManager.Instance != null)
        {
            await BattleEffectManager.Instance.PlayEffect(
                "KillBuff_Activate",
                target
                //target.transform.position,
               // 1.5f
            );
        }
    }


    private bool ShouldTrigger(BattleNewObserverID battleId)
    {
        return TriggerType switch
        {
            SkillSystem.TriggerType.OnAttack => battleId == BattleNewObserverID.Attack,
            SkillSystem.TriggerType.OnHit => battleId == BattleNewObserverID.Hit,
            SkillSystem.TriggerType.OnDamaged => battleId == BattleNewObserverID.Damaged,
            SkillSystem.TriggerType.OnKill => battleId == BattleNewObserverID.Kill,
            SkillSystem.TriggerType.OnTurnStart => battleId == BattleNewObserverID.TurnStart,
            SkillSystem.TriggerType.OnTurnEnd => battleId == BattleNewObserverID.TurnEnd,
            _ => false
        };
    }

    public void AddObserverIds()
    {
        if (UseObserverIds == null || UseObserverIds.Length == 0)
            return;

        foreach (var id in UseObserverIds)
        {
            ObserverManager.AddObserver(id, this);
        }
    }

    public void RemoveObserverIds()
    {
        if (UseObserverIds == null || UseObserverIds.Length == 0)
            return;

        foreach (var id in UseObserverIds)
        {
            ObserverManager.RemoveObserver(id, this);
        }
    }

    protected void ExecuteEffects()
    {
        foreach (var effect in SkillData.effects)
        {
            ExecuteSingleEffect(effect);
        }
    }

    #endregion



    #region Condition Checks
    public virtual bool CheckStartCondition(BattleActor sender, BattleActor receiver)
    {
        if (sender == null || receiver == null)
            return false;

        if (sender.IsDead || receiver.IsDead)
            return false;

        if (SkillData.triggerChance < 1f)
        {
            if (UnityEngine.Random.value > SkillData.triggerChance)
                return false;
        }

        if (!string.IsNullOrEmpty(SkillData.triggerCondition))
        {
            return EvaluateTriggerCondition(SkillData.triggerCondition);
        }

        return true;
    }

    public virtual bool CheckEndCondition()
    {
        return IsExpired;
    }

    protected virtual bool CheckReleaseCondition()
    {
        if (Sender != null && Sender.IsDead)
            return true;

        if (MainTargetType != SkillSystem.TargetType.Self &&
            Receiver != null && Receiver.IsDead)
            return true;

        return false;
    }

    private bool EvaluateTriggerCondition(string condition)
    {
        if (condition.Contains("HP"))
        {
            var parts = condition.Split(' ');
            if (parts.Length >= 3 && float.TryParse(parts[2], out float threshold))
            {
                float hpPercent = (Receiver.BattleActorInfo.Hp / (float)Receiver.BattleActorInfo.MaxHp) * 100;

                return parts[1] switch
                {
                    "<" => hpPercent < threshold,
                    ">" => hpPercent > threshold,
                    "<=" => hpPercent <= threshold,
                    ">=" => hpPercent >= threshold,
                    _ => false
                };
            }
        }

        return true;
    }
    #endregion

    #region Stack Management
    public void AddStack()
    {
        if (!IsStackable)
            return;

        var maxStacks = SkillData.effects.Max(e => e.maxStacks);
        CurrentStacks = Mathf.Min(CurrentStacks + 1, maxStacks);

        RefreshDuration();
    }

    public void RefreshDuration()
    {
        var maxDuration = SkillData.effects.Max(e => e.duration);
        RemainTurn = Mathf.CeilToInt(maxDuration);
    }

    public void SetRemainTurn(int turn)
    {
        RemainTurn = turn;
    }

    public void ExtendDuration(int additionalTurns)
    {
        RemainTurn += additionalTurns;
    }
    #endregion

    #region StatusType Compatibility
    public SkillSystem.StatusType GetStatusType()
    {
        if (SkillData?.effects == null || SkillData.effects.Count == 0)
            return SkillSystem.StatusType.None;

        var firstEffect = SkillData.effects[0];

        /*if (firstEffect.type == SkillSystem.EffectType.StatusEffect)
            return ConvertToStatusType(firstEffect.statusType);

        return ConvertEffectToStatusType(firstEffect.type);*/

        return firstEffect.statusType;
    }


    public EffectGroupType GetEffectGroupType()
    {
        if (SkillData?.effects == null || SkillData.effects.Count == 0)
            return EffectGroupType.None;

        var firstEffect = SkillData.effects[0];

        return firstEffect.type switch
        {
            SkillSystem.EffectType.Buff => EffectGroupType.Buff,
            SkillSystem.EffectType.Debuff => EffectGroupType.Debuff,
            SkillSystem.EffectType.StatusEffect when IsNegativeStatus(firstEffect.statusType) => EffectGroupType.Debuff,
            SkillSystem.EffectType.StatusEffect => EffectGroupType.Buff,
            _ => EffectGroupType.None
        };
    }

    private bool IsNegativeStatus(SkillSystem.StatusType status)
    {
        return status == SkillSystem.StatusType.Stun ||
               status == SkillSystem.StatusType.Poison ||
               status == SkillSystem.StatusType.Bleeding ||
               status == SkillSystem.StatusType.Silence ||
               status == SkillSystem.StatusType.Blind ||
               status == SkillSystem.StatusType.Slow ||
               status == SkillSystem.StatusType.Weaken ||
               status == SkillSystem.StatusType.Curse;
    }
    #endregion

    #region Debug
    public override string ToString()
    {
        return $"AdvancedSkill[{SkillName}(ID:{SkillID}), Turns:{RemainTurn}, Stacks:{CurrentStacks}]";
    }

    public void PrintDebugInfo()
    {
        Debug.Log($"=== {SkillName} Debug Info ===");
        Debug.Log($"ID: {SkillID}");
        Debug.Log($"Category: {Category}");
        Debug.Log($"Sender: {Sender?.name ?? "null"}");
        Debug.Log($"Receiver: {Receiver?.name ?? "null"}");
        Debug.Log($"Remaining Turns: {RemainTurn}");
        Debug.Log($"Effects: {SkillData.effects.Count}");

        foreach (var effect in SkillData.effects)
        {
            Debug.Log($"  - {effect.type}: {effect.value} for {effect.duration} turns");
        }
    }
    #endregion
}