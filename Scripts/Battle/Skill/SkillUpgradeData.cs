// =====================================================
// SkillUpgradeData.cs
// BP 기반 스킬 업그레이드 시스템 데이터 구조
// =====================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillSystem
{
    /// <summary>
    /// BP 업그레이드 레벨 정의
    /// </summary>
    public enum BPUpgradeLevel
    {
        Base = 0,      // BP 0: 기본 상태
        Upgrade1 = 1,  // BP 1-2: 첫 번째 업그레이드
        Upgrade2 = 2,  // BP 3-4: 두 번째 업그레이드
        MAX = 3        // BP 5: 최대 업그레이드
    }

    /// <summary>
    /// 업그레이드 타입 분류
    /// </summary>
    public enum UpgradeType
    {
        None = 0,
        Numerical,      // 수치 변경 (데미지%, 힐량% 증가 등)
        Mechanical,     // 메커니즘 변경 (단일→범위, 즉발→지속 등)
        Additional,     // 효과 추가 (새로운 버프/디버프 추가)
        Transform       // 완전 변형 (스킬이 완전히 다른 형태로)
    }

    /// <summary>
    /// BP 업그레이드 수정자
    /// 기존 효과를 수정하거나 새로운 효과를 추가
    /// </summary>
    [Serializable]
    public class BPUpgradeModifier
    {
        [Header("업그레이드 정보")]
        public BPUpgradeLevel level;
        public int requiredBP;           // 필요 BP (1, 3, 5)
        public string upgradeName;       // 업그레이드 이름
        public string description;       // 업그레이드 설명
        public UpgradeType type;

        [Header("수치 변경 (Numerical)")]
        public List<ValueModifier> valueModifiers = new List<ValueModifier>();

        [Header("메커니즘 변경 (Mechanical)")]
        public TargetTypeChange targetChange;
        public DurationChange durationChange;
        public bool convertToDamageOverTime;
        public bool addAreaOfEffect;

        [Header("추가 효과 (Additional)")]
        public List<AdvancedSkillEffect> additionalEffects = new List<AdvancedSkillEffect>();

        [Header("완전 변형 (Transform)")]
        public bool isCompleteOverride;
        public AdvancedSkillData transformedSkill;  // MAX 레벨에서 완전히 다른 스킬로 변경

        [Header("시각 효과")]
        public GameObject upgradedEffectPrefab;
        public Color effectColorTint = Color.white;
        public float effectScaleMultiplier = 1f;
    }

    /// <summary>
    /// 수치 수정자
    /// </summary>
    [Serializable]
    public class ValueModifier
    {
        public string targetField;      // 수정할 필드 (예: "damage", "healAmount", "duration")
        public ModifierOperation operation;
        public float value;
        public bool isPercentage;      // true면 %, false면 고정값

        public enum ModifierOperation
        {
            Add,        // 더하기
            Multiply,   // 곱하기
            Override    // 덮어쓰기
        }

        public float ApplyModifier(float originalValue)
        {
            switch (operation)
            {
                case ModifierOperation.Add:
                    return isPercentage ? originalValue * (1 + value / 100f) : originalValue + value;
                case ModifierOperation.Multiply:
                    return originalValue * value;
                case ModifierOperation.Override:
                    return value;
                default:
                    return originalValue;
            }
        }
    }

    /// <summary>
    /// 타겟 타입 변경
    /// </summary>
    [Serializable]
    public class TargetTypeChange
    {
        public bool enabled;
        public TargetType fromType;
        public TargetType toType;
        public int additionalTargets;  // Multiple 타입일 때 추가 타겟 수
    }

    /// <summary>
    /// 지속시간 변경
    /// </summary>
    [Serializable]
    public class DurationChange
    {
        public bool enabled;
        public int additionalTurns;
        public bool makeItPermanent;   // 영구 지속으로 변경
    }

    /// <summary>
    /// BP 업그레이드 경로
    /// 하나의 스킬에 대한 전체 업그레이드 정보
    /// </summary>
    [Serializable]
    public class SkillUpgradePath
    {
        public int baseSkillId;
        public string pathName;
        public string pathDescription;

        [Header("업그레이드 레벨별 수정자")]
        public BPUpgradeModifier upgrade1;  // BP 1-2
        public BPUpgradeModifier upgrade2;  // BP 3-4
        public BPUpgradeModifier upgradeMax; // BP 5

        /// <summary>
        /// 특정 BP 레벨에 해당하는 업그레이드 가져오기
        /// </summary>
        public BPUpgradeModifier GetUpgradeForBP(int bpUsed)
        {
            if (bpUsed >= 5) return upgradeMax;
            if (bpUsed >= 3) return upgrade2;
            if (bpUsed >= 1) return upgrade1;
            return null;
        }

        /// <summary>
        /// BP에 따른 업그레이드 레벨 계산
        /// </summary>
        public BPUpgradeLevel GetUpgradeLevel(int bpUsed)
        {
            if (bpUsed >= 5) return BPUpgradeLevel.MAX;
            if (bpUsed >= 3) return BPUpgradeLevel.Upgrade2;
            if (bpUsed >= 1) return BPUpgradeLevel.Upgrade1;
            return BPUpgradeLevel.Base;
        }

        /// <summary>
        /// 다음 업그레이드에 필요한 BP
        /// </summary>
        public int GetNextUpgradeBPCost(int currentBP)
        {
            if (currentBP >= 5) return 0;      // 이미 MAX
            if (currentBP >= 3) return 5;      // MAX로 가려면 5 필요
            if (currentBP >= 1) return 3;      // Upgrade2로 가려면 3 필요
            return 1;                           // Upgrade1로 가려면 1 필요
        }
    }

    /// <summary>
    /// 업그레이드된 스킬 데이터
    /// 기본 스킬 + 업그레이드 적용 결과
    /// </summary>
    public class UpgradedSkillData
    {
        public AdvancedSkillData baseSkill;
        public AdvancedSkillData currentSkill;  // 업그레이드가 적용된 현재 상태
        public BPUpgradeLevel currentLevel;
        public int totalBPUsed;
        public List<BPUpgradeModifier> appliedModifiers = new List<BPUpgradeModifier>();

        /// <summary>
        /// 업그레이드 적용
        /// </summary>
        public void ApplyUpgrade(BPUpgradeModifier modifier)
        {
            if (modifier == null) return;

            // Transform 타입이면 완전 교체
            if (modifier.isCompleteOverride && modifier.transformedSkill != null)
            {
                currentSkill = modifier.transformedSkill.Clone();
                currentSkill.skillName = $"{baseSkill.skillName} - {modifier.upgradeName}";
                appliedModifiers.Clear();
                appliedModifiers.Add(modifier);
                return;
            }

            // 기존 스킬 복사
            if (currentSkill == null)
                currentSkill = baseSkill.Clone();

            // 수치 변경 적용
            ApplyNumericalChanges(modifier);

            // 메커니즘 변경 적용
            ApplyMechanicalChanges(modifier);

            // 추가 효과 적용
            ApplyAdditionalEffects(modifier);

            // 적용된 수정자 기록
            appliedModifiers.Add(modifier);
        }

        private void ApplyNumericalChanges(BPUpgradeModifier modifier)
        {
            foreach (var valueMod in modifier.valueModifiers)
            {
                foreach (var effect in currentSkill.effects)
                {
                    // 필드에 따라 수정 적용
                    switch (valueMod.targetField.ToLower())
                    {
                        case "damage":
                        case "value":
                            effect.value = valueMod.ApplyModifier(effect.value);
                            break;
                        case "duration":
                            effect.duration = Mathf.RoundToInt(valueMod.ApplyModifier(effect.duration));
                            break;
                        case "chance":
                            effect.chance = valueMod.ApplyModifier(effect.chance);
                            break;
                        case "cooldown":
                            currentSkill.cooldown = Mathf.RoundToInt(valueMod.ApplyModifier(currentSkill.cooldown));
                            break;
                    }
                }
            }
        }

        private void ApplyMechanicalChanges(BPUpgradeModifier modifier)
        {
            // 타겟 타입 변경
            if (modifier.targetChange.enabled)
            {
                foreach (var effect in currentSkill.effects)
                {
                    if (effect.targetType == modifier.targetChange.fromType)
                    {
                        effect.targetType = modifier.targetChange.toType;
                        if (modifier.targetChange.toType == TargetType.Multiple)
                        {
                            effect.targetCount += modifier.targetChange.additionalTargets;
                        }
                    }
                }
            }

            // 지속시간 변경
            if (modifier.durationChange.enabled)
            {
                foreach (var effect in currentSkill.effects)
                {
                    if (modifier.durationChange.makeItPermanent)
                    {
                        effect.duration = 999;  // 영구 지속
                    }
                    else
                    {
                        effect.duration += modifier.durationChange.additionalTurns;
                    }
                }
            }

            // DoT 변환
            if (modifier.convertToDamageOverTime)
            {
                foreach (var effect in currentSkill.effects)
                {
                    if (effect.type == EffectType.Damage)
                    {
                        effect.type = EffectType.StatusEffect;
                        effect.statusType = StatusType.Burn;  // 또는 다른 DoT 타입
                        effect.duration = 3;  // 기본 3턴
                    }
                }
            }
        }

        private void ApplyAdditionalEffects(BPUpgradeModifier modifier)
        {
            // 추가 효과 붙이기
            foreach (var additionalEffect in modifier.additionalEffects)
            {
                currentSkill.effects.Add(additionalEffect.Clone());
            }
        }

        /// <summary>
        /// 현재 업그레이드 상태 문자열
        /// </summary>
        public string GetUpgradeStatusString()
        {
            if (currentLevel == BPUpgradeLevel.Base)
                return "기본";
            if (currentLevel == BPUpgradeLevel.MAX)
                return "MAX";
            return $"Lv.{(int)currentLevel}";
        }

        /// <summary>
        /// 업그레이드 미리보기
        /// </summary>
        public string GetNextUpgradePreview(SkillUpgradePath path)
        {
            var nextLevel = GetNextUpgradeLevel();
            if (nextLevel == BPUpgradeLevel.Base)
                return "최대 업그레이드 상태";

            BPUpgradeModifier nextModifier = null;
            switch (nextLevel)
            {
                case BPUpgradeLevel.Upgrade1:
                    nextModifier = path.upgrade1;
                    break;
                case BPUpgradeLevel.Upgrade2:
                    nextModifier = path.upgrade2;
                    break;
                case BPUpgradeLevel.MAX:
                    nextModifier = path.upgradeMax;
                    break;
            }

            if (nextModifier == null)
                return "업그레이드 정보 없음";

            return $"[BP {nextModifier.requiredBP}] {nextModifier.upgradeName}\n{nextModifier.description}";
        }

        private BPUpgradeLevel GetNextUpgradeLevel()
        {
            switch (currentLevel)
            {
                case BPUpgradeLevel.Base:
                    return BPUpgradeLevel.Upgrade1;
                case BPUpgradeLevel.Upgrade1:
                    return BPUpgradeLevel.Upgrade2;
                case BPUpgradeLevel.Upgrade2:
                    return BPUpgradeLevel.MAX;
                default:
                    return BPUpgradeLevel.Base;
            }
        }
    }
}