using System;
using System.Collections.Generic;
using UnityEngine;
using SkillSystem;
using System.Linq;

/// <summary>
/// 스킬 생성 팩토리
/// 데이터 기반으로 적절한 스킬 런타임 인스턴스 생성
/// </summary>
public static class AdvancedSkillFactory
{
    /// <summary>
    /// 특수 스킬 타입 매핑
    /// specialEffect 문자열과 런타임 클래스 매핑
    /// </summary>
    private static readonly Dictionary<string, Type> SpecialSkillTypes = new Dictionary<string, Type>
    {
        { "Vampiric", typeof(VampiricSkillRuntime) },
        { "ChainLightning", typeof(ChainLightningSkillRuntime) },
        { "Revenge", typeof(RevengeSkillRuntime) },
        { "Counter", typeof(CounterSkillRuntime) },
        { "Sacrifice", typeof(SacrificeSkillRuntime) },
        { "Transform", typeof(TransformSkillRuntime) },
        { "Random", typeof(RandomEffectSkillRuntime) },
        { "CooldownReset", typeof(CooldownResetSkillRuntime) },
        { "Taunt", typeof(TauntSkillRuntime) },
        { "Revive", typeof(ReviveSkillRuntime) },
        { "ConditionalDamage", typeof(ConditionalDamageSkillRuntime ) },
        { "ConditionalBuff", typeof(ConditionalBuffSkillRuntime ) },
        { "HealLowestHP", typeof(HealLowestHPSkillRuntime ) },
    };

    /// <summary>
    /// 스킬 데이터를 기반으로 적절한 런타임 인스턴스 생성
    /// </summary>
    public static AdvancedSkillRuntime CreateSkill(AdvancedSkillData skillData)
    {
        if (skillData == null)
        {
            Debug.LogError("[SkillFactory] Skill data is null!");
            return null;
        }

        // 1. 특수 효과 체크 (Special Effect)
        var specialRuntime = CheckSpecialEffect(skillData);
        if (specialRuntime != null)
            return specialRuntime;

        // 2. 복합 효과 체크 (Multiple Effects)
        if (HasComplexEffects(skillData))
        {
            return CreateComplexSkill(skillData);
        }

        // 3. 단일 효과 최적화 체크
        if (skillData.effects.Count == 1)
        {
            return CreateOptimizedSingleEffectSkill(skillData);
        }

        // 4. 기본 런타임 사용
        return new AdvancedSkillRuntime();
    }

    /// <summary>
    /// 특수 효과 체크 및 생성
    /// </summary>
    private static AdvancedSkillRuntime CheckSpecialEffect(AdvancedSkillData skillData)
    { 

        //일반적인 스킬인데 스페셜 
        // Regen/HealOverTime 효과 체크
        if (skillData.effects.Any(e =>
            e.type == SkillSystem.EffectType.HealOverTime ||
            (e.type == SkillSystem.EffectType.Heal && e.duration > 0)))
        {
            Debug.Log("[SkillFactory] Creating Regen skill");
            return new RegenSkillRuntime();
        }


        //기존 스페셜 
        foreach (var effect in skillData.effects)
        {
            if (effect.type == SkillSystem.EffectType.Special &&
                !string.IsNullOrEmpty(effect.specialEffect))
            {
                if (SpecialSkillTypes.TryGetValue(effect.specialEffect, out Type runtimeType))
                {
                    Debug.Log($"[SkillFactory] Creating special skill: {effect.specialEffect}");
                    return Activator.CreateInstance(runtimeType) as AdvancedSkillRuntime;
                }
            }
        }

        // 스킬 이름으로도 체크 (레거시 지원)
        if (SpecialSkillTypes.TryGetValue(skillData.skillName, out Type namedType))
        {
            Debug.Log($"[SkillFactory] Creating special skill by name: {skillData.skillName}");
            return Activator.CreateInstance(namedType) as AdvancedSkillRuntime;
        }

        return null;
    }

    /// <summary>
    /// 복합 효과 여부 체크
    /// </summary>
    private static bool HasComplexEffects(AdvancedSkillData skillData)
    {
        // 다음 조건 중 하나라도 만족하면 복합 효과로 판단

        // 1. 효과가 3개 이상
        if (skillData.effects.Count >= 3)
            return true;

        // 2. 서로 다른 타입의 효과 조합
        var effectTypes = new HashSet<SkillSystem.EffectType>();
        foreach (var effect in skillData.effects)
        {
            effectTypes.Add(effect.type);
        }
        if (effectTypes.Count >= 2)
            return true;

        // 3. 조건부 트리거가 있는 경우
        if (!string.IsNullOrEmpty(skillData.triggerCondition) &&
            skillData.triggerCondition.Contains("&&"))
            return true;

        return false;
    }

    /// <summary>
    /// 복합 스킬 생성
    /// </summary>
    private static AdvancedSkillRuntime CreateComplexSkill(AdvancedSkillData skillData)
    {
        // 복합 효과의 특정 패턴 체크

        // 데미지 + 상태이상 조합
        if (HasEffectCombo(skillData, SkillSystem.EffectType.Damage, SkillSystem.EffectType.StatusEffect))
        {
            Debug.Log("[SkillFactory] Creating DamageWithStatus skill");
            // 필요시 특화 클래스 생성
            return new AdvancedSkillRuntime();
        }

        // 버프 + 디버프 조합
        if (HasEffectCombo(skillData, SkillSystem.EffectType.Buff, SkillSystem.EffectType.Debuff))
        {
            Debug.Log("[SkillFactory] Creating BuffDebuff skill");
            // 필요시 특화 클래스 생성
            return new AdvancedSkillRuntime();
        }

        // 기본 복합 스킬
        return new AdvancedSkillRuntime();
    }

    /// <summary>
    /// 단일 효과 최적화 스킬 생성
    /// </summary>
    private static AdvancedSkillRuntime CreateOptimizedSingleEffectSkill(AdvancedSkillData skillData)
    {
        if (skillData.effects.Count == 0)
            return new AdvancedSkillRuntime();

        var effect = skillData.effects[0];

        // 단일 효과 타입별 최적화된 런타임 생성
        switch (effect.type)
        {
            case SkillSystem.EffectType.Damage:
                // 단순 데미지는 기본 런타임으로 충분
                return new AdvancedSkillRuntime();

            case SkillSystem.EffectType.Heal:
                // 단순 힐도 기본 런타임으로 충분
                return new AdvancedSkillRuntime();

            case SkillSystem.EffectType.StatusEffect:
                // 특정 상태이상은 특화 클래스 사용
                return CreateStatusEffectSkill(effect.statusType);

            case SkillSystem.EffectType.Buff:
            case SkillSystem.EffectType.Debuff:
                // 버프/디버프는 기본 런타임
                return new AdvancedSkillRuntime();

            default:
                return new AdvancedSkillRuntime();
        }
    }

    /// <summary>
    /// 상태이상 전용 스킬 생성
    /// </summary>
    private static AdvancedSkillRuntime CreateStatusEffectSkill(SkillSystem.StatusType statusType)
    {
        // 특수한 상태이상만 별도 처리
        switch (statusType)
        {
            /*case SkillSystem.StatusType.HI:
                // 은신은 특수 처리 필요할 수 있음
                Debug.Log("[SkillFactory] Creating Hide skill");
                return new AdvancedSkillRuntime();*/

            case SkillSystem.StatusType.Taunt:
                // 도발은 특수 처리
                Debug.Log("[SkillFactory] Creating Taunt skill");
                return new TauntSkillRuntime();

            default:
                // 나머지는 기본 런타임
                return new AdvancedSkillRuntime();
        }
    }

    /// <summary>
    /// 효과 조합 체크 헬퍼
    /// </summary>
    private static bool HasEffectCombo(AdvancedSkillData skillData, SkillSystem.EffectType type1, SkillSystem.EffectType type2)
    {
        bool hasType1 = false;
        bool hasType2 = false;

        foreach (var effect in skillData.effects)
        {
            if (effect.type == type1) hasType1 = true;
            if (effect.type == type2) hasType2 = true;
        }

        return hasType1 && hasType2;
    }

    /// <summary>
    /// 스킬 인스턴스 풀링을 위한 캐시 (선택적)
    /// </summary>
    private static Dictionary<Type, Queue<AdvancedSkillRuntime>> skillPool = new Dictionary<Type, Queue<AdvancedSkillRuntime>>();

    /// <summary>
    /// 풀에서 스킬 인스턴스 가져오기 (성능 최적화)
    /// </summary>
    public static AdvancedSkillRuntime GetFromPool(AdvancedSkillData skillData)
    {
        var runtime = CreateSkill(skillData);
        var runtimeType = runtime.GetType();

        // 풀에서 가져오기 시도
        if (skillPool.TryGetValue(runtimeType, out Queue<AdvancedSkillRuntime> pool) && pool.Count > 0)
        {
            var pooledSkill = pool.Dequeue();
            Debug.Log($"[SkillFactory] Retrieved {runtimeType.Name} from pool");
            return pooledSkill;
        }

        // 풀에 없으면 새로 생성
        return runtime;
    }

    /// <summary>
    /// 사용 완료된 스킬을 풀에 반환
    /// </summary>
    public static void ReturnToPool(AdvancedSkillRuntime skill)
    {
        if (skill == null) return;

        var skillType = skill.GetType();

        if (!skillPool.ContainsKey(skillType))
        {
            skillPool[skillType] = new Queue<AdvancedSkillRuntime>();
        }

        // 최대 풀 크기 제한 (타입별 10개)
        if (skillPool[skillType].Count < 10)
        {
            skillPool[skillType].Enqueue(skill);
            Debug.Log($"[SkillFactory] Returned {skillType.Name} to pool");
        }
    }

    /// <summary>
    /// 스킬 검증 - 데이터가 올바른지 체크
    /// </summary>
    public static bool ValidateSkillData(AdvancedSkillData skillData, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (skillData == null)
        {
            errorMessage = "Skill data is null";
            return false;
        }

        if (string.IsNullOrEmpty(skillData.skillName))
        {
            errorMessage = "Skill name is empty";
            return false;
        }

        if (skillData.effects == null || skillData.effects.Count == 0)
        {
            errorMessage = "Skill has no effects";
            return false;
        }

        // 각 효과 검증
        for (int i = 0; i < skillData.effects.Count; i++)
        {
            var effect = skillData.effects[i];

            // 타겟 타입 검증
            if (effect.targetType == SkillSystem.TargetType.None)
            {
                errorMessage = $"Effect {i} has invalid target type";
                return false;
            }

            // 특수 효과 검증
            if (effect.type == SkillSystem.EffectType.Special && string.IsNullOrEmpty(effect.specialEffect))
            {
                errorMessage = $"Effect {i} is Special type but has no specialEffect defined";
                return false;
            }

            // 상태이상 효과 검증
            if (effect.type == SkillSystem.EffectType.StatusEffect && effect.statusType == SkillSystem.StatusType.None)
            {
                errorMessage = $"Effect {i} is StatusEffect type but has no statusType defined";
                return false;
            }

            // 버프/디버프 검증
            if ((effect.type == SkillSystem.EffectType.Buff || effect.type == SkillSystem.EffectType.Debuff) &&
                effect.statType == SkillSystem.StatType.None)
            {
                errorMessage = $"Effect {i} is Buff/Debuff but has no statType defined";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 스킬 생성 통계 (디버그용)
    /// </summary>
    public static class Statistics
    {
        private static Dictionary<string, int> creationCount = new Dictionary<string, int>();

        public static void RecordCreation(string skillType)
        {
            if (!creationCount.ContainsKey(skillType))
                creationCount[skillType] = 0;
            creationCount[skillType]++;
        }

        public static void PrintStatistics()
        {
            Debug.Log("=== Skill Creation Statistics ===");
            foreach (var kvp in creationCount)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value} times");
            }
        }

        public static void Reset()
        {
            creationCount.Clear();
        }
    }
}