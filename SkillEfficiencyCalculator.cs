// =====================================================
// SkillEfficiencyCalculator.cs
// 스킬 효율 계산 및 밸런싱 시스템 - Scripts/Battle/Skills/Core 폴더에 저장
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkillSystem
{
    /// <summary>
    /// 스킬 효율성을 계산하고 적절한 티어를 추천하는 시스템
    /// </summary>
    public static class SkillEfficiencyCalculator
    {
        // =====================================================
        // 효율 점수 기준값 (100점 = 티어 3 기준)
        // =====================================================

        // 티어별 기준 점수
        private static readonly Dictionary<int, EfficiencyRange> TierRanges = new Dictionary<int, EfficiencyRange>
        {
            { 0, new EfficiencyRange { Min = 0, Max = 30, RecommendedCooldown = 0 } },      // 티어 0: 매우 약함
            { 1, new EfficiencyRange { Min = 30, Max = 50, RecommendedCooldown = 0 } },     // 티어 1: 약함
            { 2, new EfficiencyRange { Min = 50, Max = 80, RecommendedCooldown = 2 } },     // 티어 2: 보통
            { 3, new EfficiencyRange { Min = 80, Max = 120, RecommendedCooldown = 3 } },    // 티어 3: 표준
            { 4, new EfficiencyRange { Min = 120, Max = 180, RecommendedCooldown = 4 } },   // 티어 4: 강함
            { 5, new EfficiencyRange { Min = 180, Max = 999, RecommendedCooldown = 5 } }    // 티어 5: 매우 강함
        };

        // 효과별 기본 점수 (밸런싱 기준)
        private static readonly Dictionary<EffectType, float> BaseEffectScores = new Dictionary<EffectType, float>
        {
            // 데미지 계열
            { EffectType.Damage, 0.5f },           // 100% 데미지 = 50점
            { EffectType.TrueDamage, 0.8f },       // 100% 고정 데미지 = 80점 (더 강력)
            
            // 회복 계열
            { EffectType.Heal, 0.6f },             // 100% 회복 = 60점
            { EffectType.HealOverTime, 0.4f },     // 100% 지속 회복 = 40점 (턴당)
            { EffectType.LifeSteal, 0.7f },        // 100% 흡혈 = 70점
            
            // 버프/디버프
            { EffectType.Buff, 0.8f },             // 1% 버프 = 0.8점
            { EffectType.Debuff, 1.0f },           // 1% 디버프 = 1점 (적에게 적용이라 더 가치있음)
            
            // 상태이상
            { EffectType.StatusEffect, 15f },      // 기본 15점 (상태별 추가 계산)
            
            // 보호
            { EffectType.Shield, 0.5f },           // 100% 보호막 = 50점
            { EffectType.Reflect, 1.2f },          // 1% 반사 = 1.2점
            { EffectType.DamageReduction, 1.5f },  // 1% 피해 감소 = 1.5점
            { EffectType.Immunity, 25f },          // 면역 = 턴당 25점
            { EffectType.Invincible, 40f },        // 무적 = 턴당 40점
            
            // 해제
            { EffectType.Dispel, 10f },            // 해제 1개 = 10점
            
            // 특수
            { EffectType.Summon, 30f },            // 소환 = 30점
            { EffectType.Transform, 35f },         // 변신 = 35점
            { EffectType.Special, 20f }            // 특수 효과 = 20점
        };

        // 상태이상별 추가 점수
        private static readonly Dictionary<StatusType, float> StatusScores = new Dictionary<StatusType, float>
        {
            { StatusType.Stun, 20f },          // 기절: 매우 강력
            { StatusType.Silence, 15f },       // 침묵: 강력
            { StatusType.Taunt, 12f },         // 도발: 중간
            { StatusType.Freeze, 18f },        // 동결: 강력
            { StatusType.Burn, 0.3f },         // 화상: 틱당 데미지%
            { StatusType.Poison, 0.3f },       // 중독: 틱당 데미지%
            { StatusType.Bleed, 0.35f },       // 출혈: 틱당 데미지%
            { StatusType.Sleep, 16f },         // 수면: 강력
            { StatusType.Confuse, 14f },       // 혼란: 중간
            { StatusType.Petrify, 22f },       // 석화: 매우 강력
            { StatusType.Blind, 10f },         // 실명: 약함
            { StatusType.Slow, 8f },           // 둔화: 약함
            { StatusType.Weaken, 10f },        // 약화: 약함
            { StatusType.Curse, 12f }          // 저주: 중간
        };

        // 타겟 타입별 배수
        private static readonly Dictionary<TargetType, float> TargetMultipliers = new Dictionary<TargetType, float>
        {
            { TargetType.Self, 0.9f },             // 자신: 0.9배
            { TargetType.EnemySingle, 1.0f },      // 적 단일: 1배
            { TargetType.EnemyAll, 2.5f },         // 적 전체: 2.5배
            { TargetType.EnemyRow, 1.8f },         // 적 전/후열: 1.8배
            { TargetType.AllySingle, 1.0f },       // 아군 단일: 1배
            { TargetType.AllyAll, 2.2f },          // 아군 전체: 2.2배
            { TargetType.AllyRow, 1.6f },          // 아군 전/후열: 1.6배
            { TargetType.Random, 0.7f },           // 무작위: 0.7배 (불확실성)
            { TargetType.Multiple, 1.3f },         // 다수: 1.3배 × 타겟 수
            { TargetType.Lowest, 1.1f },           // 가장 낮은: 1.1배
            { TargetType.Highest, 1.1f }           // 가장 높은: 1.1배
        };

        // =====================================================
        // 메인 계산 함수
        // =====================================================

        /// <summary>
        /// 스킬의 전체 효율 점수를 계산
        /// </summary>
        public static SkillEfficiencyResult CalculateEfficiency(AdvancedSkillData skill)
        {
            var result = new SkillEfficiencyResult
            {
                SkillName = skill.skillName,
                CurrentTier = skill.tier,
                CurrentCooldown = skill.cooldown // MP 대신 Cooldown 사
            };

            float totalScore = 0;
            var breakdown = new List<EfficiencyBreakdown>();

            // 각 효과별 점수 계산
            foreach (var effect in skill.effects)
            {
                var effectScore = CalculateEffectScore(effect);
                totalScore += effectScore.Score;
                breakdown.Add(effectScore);
            }

            // 시너지 보너스 계산
            float synergyBonus = CalculateSynergyBonus(skill.effects);
            if (synergyBonus > 0)
            {
                totalScore += synergyBonus;
                breakdown.Add(new EfficiencyBreakdown
                {
                    EffectName = "시너지 보너스",
                    BaseScore = synergyBonus,
                    FinalScore = synergyBonus,
                    Details = "효과 조합 보너스"
                });
            }

            // 카테고리별 보정
            float categoryMultiplier = GetCategoryMultiplier(skill.category);
            totalScore *= categoryMultiplier;

            // 최종 점수 및 추천 설정
            result.TotalScore = totalScore;
            result.EfficiencyBreakdown = breakdown;
            result.RecommendedTier = GetRecommendedTier(totalScore);
            result.RecommendedCooldown = GetRecommendedCooldown(totalScore); // Mana 대신 Cooldown

            // 밸런스 평가
            // result.BalanceStatus = EvaluateBalance(skill.tier, skill.manaCost, totalScore);
            result.BalanceMessage = GetBalanceMessage(result.BalanceStatus, skill.tier, result.RecommendedTier);

            // 개선 제안
            result.Suggestions = GenerateSuggestions(skill, result);

            return result;
        }

        /// <summary>
        /// 개별 효과의 점수 계산
        /// </summary>
        private static EfficiencyBreakdown CalculateEffectScore(AdvancedSkillEffect effect)
        {
            var breakdown = new EfficiencyBreakdown
            {
                EffectName = effect.GetShortDescription(),
                EffectType = effect.type
            };

            float baseScore = 0;

            switch (effect.type)
            {
                case EffectType.Damage:
                case EffectType.TrueDamage:
                    baseScore = BaseEffectScores[effect.type] * effect.value;
                    if (effect.canCrit) baseScore *= 1.2f; // 치명타 가능시 20% 보너스
                    breakdown.Details = $"데미지 {effect.value}%";
                    break;

                case EffectType.Heal:
                case EffectType.HealOverTime:
                    baseScore = BaseEffectScores[effect.type] * effect.value;
                    if (effect.type == EffectType.HealOverTime)
                        baseScore *= effect.duration; // 지속 회복은 턴수 곱하기
                    breakdown.Details = $"회복 {effect.value}%";
                    break;

                case EffectType.LifeSteal:
                    baseScore = BaseEffectScores[effect.type] * effect.value;
                    breakdown.Details = $"흡혈 {effect.value}%";
                    break;

                case EffectType.Buff:
                case EffectType.Debuff:
                    baseScore = BaseEffectScores[effect.type] * effect.value * effect.duration;
                    breakdown.Details = $"{GetStatName(effect.statType)} {effect.value}% × {effect.duration}턴";
                    break;

                case EffectType.StatusEffect:
                    baseScore = BaseEffectScores[effect.type];
                    if (StatusScores.ContainsKey(effect.statusType))
                    {
                        if (effect.statusType == StatusType.Burn ||
                            effect.statusType == StatusType.Poison ||
                            effect.statusType == StatusType.Bleed)
                        {
                            // DoT 효과는 데미지 × 턴수
                            baseScore = StatusScores[effect.statusType] * effect.value * effect.duration;
                        }
                        else
                        {
                            // 일반 상태이상은 기본 점수 × 턴수
                            baseScore = StatusScores[effect.statusType] * effect.duration;
                        }
                    }
                    breakdown.Details = $"{GetStatusName(effect.statusType)} {effect.duration}턴";
                    break;

                case EffectType.Shield:
                    baseScore = BaseEffectScores[effect.type] * effect.value;
                    baseScore *= (1 + effect.duration * 0.1f); // 지속시간 보너스
                    breakdown.Details = $"보호막 {effect.value}% ({effect.duration}턴)";
                    break;

                case EffectType.Reflect:
                    baseScore = BaseEffectScores[effect.type] * effect.value * effect.duration;
                    breakdown.Details = $"반사 {effect.value}% × {effect.duration}턴";
                    break;

                case EffectType.DamageReduction:
                    baseScore = BaseEffectScores[effect.type] * effect.value * effect.duration;
                    breakdown.Details = $"피해 감소 {effect.value}% × {effect.duration}턴";
                    break;

                case EffectType.Immunity:
                case EffectType.Invincible:
                    baseScore = BaseEffectScores[effect.type] * effect.duration;
                    breakdown.Details = $"{effect.type} {effect.duration}턴";
                    break;

                case EffectType.Dispel:
                    baseScore = BaseEffectScores[effect.type] * effect.dispelCount;
                    breakdown.Details = $"{effect.dispelType} 해제 {effect.dispelCount}개";
                    break;

                case EffectType.Summon:
                case EffectType.Transform:
                case EffectType.Special:
                    baseScore = BaseEffectScores[effect.type];
                    if (effect.type == EffectType.Transform)
                        baseScore *= (1 + effect.duration * 0.1f);
                    breakdown.Details = effect.specialEffect;
                    break;
            }

            breakdown.BaseScore = baseScore;

            // 발동 확률 적용
            float chanceMultiplier = effect.chance;
            breakdown.ChanceMultiplier = chanceMultiplier;

            // 타겟 타입 배수 적용
            float targetMultiplier = TargetMultipliers.ContainsKey(effect.targetType) ?
                TargetMultipliers[effect.targetType] : 1f;

            if (effect.targetType == TargetType.Multiple)
            {
                targetMultiplier *= effect.targetCount;
            }

            breakdown.TargetMultiplier = targetMultiplier;

            // 조건부 효과 페널티
            if (effect.hasCondition)
            {
                chanceMultiplier *= 0.7f; // 조건부는 30% 페널티
                breakdown.Details += $" [조건부]";
            }

            // 중첩 가능 보너스
            if (effect.isStackable)
            {
                baseScore *= 1.2f; // 중첩 가능시 20% 보너스
                breakdown.Details += $" [중첩×{effect.maxStacks}]";
            }

            // 최종 점수 계산
            breakdown.FinalScore = baseScore * chanceMultiplier * targetMultiplier;
            breakdown.Score = breakdown.FinalScore;

            return breakdown;
        }

        /// <summary>
        /// 효과 조합 시너지 보너스 계산
        /// </summary>
        private static float CalculateSynergyBonus(List<AdvancedSkillEffect> effects)
        {
            float bonus = 0;

            // 데미지 + 상태이상 콤보
            bool hasDamage = effects.Any(e => e.type == EffectType.Damage);
            bool hasStatusEffect = effects.Any(e => e.type == EffectType.StatusEffect);
            if (hasDamage && hasStatusEffect)
            {
                bonus += 10; // 데미지와 상태이상 조합
            }

            // 버프 + 회복 콤보
            bool hasHeal = effects.Any(e => e.type == EffectType.Heal);
            bool hasBuff = effects.Any(e => e.type == EffectType.Buff);
            if (hasHeal && hasBuff)
            {
                bonus += 8; // 회복과 버프 조합
            }

            // 디버프 + 데미지 콤보
            bool hasDebuff = effects.Any(e => e.type == EffectType.Debuff);
            if (hasDamage && hasDebuff)
            {
                bonus += 12; // 공격적 조합
            }

            // 보호막 + 반사 콤보
            bool hasShield = effects.Any(e => e.type == EffectType.Shield);
            bool hasReflect = effects.Any(e => e.type == EffectType.Reflect);
            if (hasShield && hasReflect)
            {
                bonus += 15; // 방어적 조합
            }

            // 3개 이상 효과 보너스
            if (effects.Count >= 3)
            {
                bonus += effects.Count * 3; // 효과당 3점 추가
            }

            return bonus;
        }

        /// <summary>
        /// 카테고리별 배수
        /// </summary>
        private static float GetCategoryMultiplier(SkillCategory category)
        {
            return category switch
            {
                SkillCategory.Active => 1.0f,
                SkillCategory.Passive => 0.8f,         // 패시브는 20% 감소
                SkillCategory.SpecialActive => 1.3f,   // 스페셜은 30% 증가
                SkillCategory.SpecialPassive => 1.1f,  // 스페셜 패시브는 10% 증가
                _ => 1.0f
            };
        }

        /// <summary>
        /// 점수에 따른 추천 티어
        /// </summary>
        private static int GetRecommendedTier(float score)
        {
            foreach (var tier in TierRanges.OrderByDescending(t => t.Key))
            {
                if (score >= tier.Value.Min)
                {
                    return tier.Key;
                }
            }
            return 0;
        }

        // <summary>
        /// 점수에 따른 추천 쿨다운 (MP 대신 Cooldown)
        /// </summary>
        private static int GetRecommendedCooldown(float score)
        {
            int tier = GetRecommendedTier(score);
            if (TierRanges.ContainsKey(tier))
            {
                int baseCooldown = TierRanges[tier].RecommendedCooldown;

                // 점수가 티어 상한에 가까우면 쿨다운 +1
                float tierMax = TierRanges[tier].Max;
                if (score > tierMax * 0.8f && baseCooldown < 5)
                {
                    baseCooldown++;
                }

                return Mathf.Clamp(baseCooldown, 0, 10);
            }
            return 3; // 기본값
        }

        /// <summary>
        /// 밸런스 상태 평가
        /// </summary>
        private static BalanceStatus EvaluateBalance(int currentTier, int currentCooldown, float score)
        {
            int recommendedTier = GetRecommendedTier(score);
            int recommendedCooldown = GetRecommendedCooldown(score);

            int tierDiff = currentTier - recommendedTier;
            int cooldownDiff = currentCooldown - recommendedCooldown;

            // 티어 차이가 2 이상이면 심각한 불균형
            if (Math.Abs(tierDiff) >= 2)
            {
                return tierDiff > 0 ? BalanceStatus.SeverelyOvertiered : BalanceStatus.SeverelyUndertiered;
            }

            // 티어 차이가 1이면 약간의 불균형
            if (tierDiff > 0)
            {
                return BalanceStatus.Overtiered;
            }
            else if (tierDiff < 0)
            {
                return BalanceStatus.Undertiered;
            }

            // 쿨다운 차이 체크
            if (cooldownDiff > 1)
            {
                return BalanceStatus.Overcooled; // 쿨다운이 너무 김
            }
            else if (cooldownDiff < -1)
            {
                return BalanceStatus.Undercooled; // 쿨다운이 너무 짧음
            }

            return BalanceStatus.Balanced;
        }

        /// <summary>
        /// 밸런스 메시지 생성
        /// </summary>
        private static string GetBalanceMessage(BalanceStatus status, int currentTier, int recommendedTier)
        {
            return status switch
            {
                BalanceStatus.Balanced => "✅ 적절한 밸런스입니다.",
                BalanceStatus.Overtiered => $"⚠️ 효과에 비해 티어가 높습니다. (현재: T{currentTier}, 추천: T{recommendedTier})",
                BalanceStatus.Undertiered => $"⚠️ 효과에 비해 티어가 낮습니다. (현재: T{currentTier}, 추천: T{recommendedTier})",
                BalanceStatus.SeverelyOvertiered => $"❌ 심각하게 약합니다! (현재: T{currentTier}, 추천: T{recommendedTier})",
                BalanceStatus.SeverelyUndertiered => $"❌ 심각하게 강합니다! (현재: T{currentTier}, 추천: T{recommendedTier})",
                BalanceStatus.Overcooled => "⚠️ 쿨다운이 너무 깁니다.",
                BalanceStatus.Undercooled => "⚠️ 쿨다운이 너무 짧습니다.",
                _ => ""
            };
        }

        /// <summary>
        /// 개선 제안 생성
        /// </summary>
        private static List<string> GenerateSuggestions(AdvancedSkillData skill, SkillEfficiencyResult result)
        {
            var suggestions = new List<string>();

            // 언더티어 스킬 제안
            if (result.BalanceStatus == BalanceStatus.Undertiered ||
                result.BalanceStatus == BalanceStatus.SeverelyUndertiered)
            {
                suggestions.Add($"💡 티어를 {result.RecommendedTier}로 올리거나 효과를 약화시키세요.");

                // 구체적인 약화 제안
                foreach (var effect in skill.effects)
                {
                    if (effect.type == EffectType.Damage && effect.value > 150)
                    {
                        suggestions.Add($"• 데미지를 150% 이하로 조정");
                    }
                    if (effect.type == EffectType.StatusEffect && effect.duration > 3)
                    {
                        suggestions.Add($"• 상태이상 지속시간을 3턴 이하로 조정");
                    }
                }
            }

            // 오버티어 스킬 제안
            if (result.BalanceStatus == BalanceStatus.Overtiered ||
                result.BalanceStatus == BalanceStatus.SeverelyOvertiered)
            {
                suggestions.Add($"💡 티어를 {result.RecommendedTier}로 낮추거나 효과를 강화시키세요.");
                suggestions.Add("• 추가 효과를 넣거나 수치를 상향 조정");
                suggestions.Add("• 타겟을 '전체'로 변경 고려");
            }

            // 쿨다운 제안
            if (result.CurrentCooldown != result.RecommendedCooldown)
            {
                suggestions.Add($"💡 쿨다운을 {result.RecommendedCooldown}턴으로 조정 권장");
            }


            // 시너지 제안
            if (skill.effects.Count == 1)
            {
                suggestions.Add("💡 추가 효과를 넣어 시너지를 만들어보세요.");
            }

            return suggestions;
        }

        // 헬퍼 메서드들
        private static string GetStatName(StatType stat)
        {
            return stat switch
            {
                StatType.Attack => "공격력",
                StatType.Defense => "방어력",
                StatType.Speed => "속도",
                StatType.CritRate => "치명타율",
                StatType.CritDamage => "치명타 피해",
                _ => stat.ToString()
            };
        }

        private static string GetStatusName(StatusType status)
        {
            return status switch
            {
                StatusType.Stun => "기절",
                StatusType.Silence => "침묵",
                StatusType.Taunt => "도발",
                StatusType.Burn => "화상",
                StatusType.Poison => "중독",
                StatusType.Bleed => "출혈",
                _ => status.ToString()
            };
        }
    }

    // =====================================================
    // 데이터 구조체
    // =====================================================

    /// <summary>
    /// 효율 계산 결과
    /// </summary>
    public class SkillEfficiencyResult
    {
        public string SkillName { get; set; }
        public float TotalScore { get; set; }
        public int CurrentTier { get; set; }
        public int RecommendedTier { get; set; }

        public int CurrentCooldown { get; set; }      
        public int RecommendedCooldown { get; set; }
        public BalanceStatus BalanceStatus { get; set; }
        public string BalanceMessage { get; set; }
        public List<EfficiencyBreakdown> EfficiencyBreakdown { get; set; }
        public List<string> Suggestions { get; set; }

        public Color GetStatusColor()
        {
            return BalanceStatus switch
            {
                BalanceStatus.Balanced => Color.green,
                BalanceStatus.Overtiered => new Color(1f, 0.5f, 0f), // Orange
                BalanceStatus.Undertiered => new Color(1f, 0.5f, 0f),
                BalanceStatus.SeverelyOvertiered => Color.red,
                BalanceStatus.SeverelyUndertiered => Color.red,
                BalanceStatus.Overcooled => Color.yellow,
                BalanceStatus.Undercooled => Color.yellow,
                _ => Color.white
            };
        }

        public string GetGradeText()
        {
            if (TotalScore >= 200) return "SSS";
            if (TotalScore >= 180) return "SS";
            if (TotalScore >= 150) return "S";
            if (TotalScore >= 120) return "A";
            if (TotalScore >= 90) return "B";
            if (TotalScore >= 60) return "C";
            if (TotalScore >= 30) return "D";
            return "F";
        }

        public Color GetGradeColor()
        {
            string grade = GetGradeText();
            return grade switch
            {
                "SSS" => new Color(1f, 0.84f, 0f),     // Gold
                "SS" => new Color(0.75f, 0f, 1f),      // Purple
                "S" => new Color(1f, 0.27f, 0f),       // Orange Red
                "A" => new Color(0f, 0.5f, 1f),        // Blue
                "B" => new Color(0f, 0.8f, 0f),        // Green
                "C" => new Color(0.5f, 0.5f, 0.5f),    // Gray
                "D" => new Color(0.6f, 0.4f, 0.2f),    // Brown
                _ => new Color(0.3f, 0.3f, 0.3f)       // Dark Gray
            };
        }
    }

    /// <summary>
    /// 효과별 점수 분석
    /// </summary>
    public class EfficiencyBreakdown
    {
        public string EffectName { get; set; }
        public EffectType EffectType { get; set; }
        public float BaseScore { get; set; }
        public float ChanceMultiplier { get; set; } = 1f;
        public float TargetMultiplier { get; set; } = 1f;
        public float FinalScore { get; set; }
        public float Score { get; set; }
        public string Details { get; set; }
    }

    /// <summary>
    /// 티어별 효율 범위
    /// </summary>
    public class EfficiencyRange
    {
        public float Min { get; set; }
        public float Max { get; set; }
        public int RecommendedCooldown { get; set; }  // RecommendedMana 대체
    }

    /// <summary>
    /// 밸런스 상태
    /// </summary>
    public enum BalanceStatus
    {
        Balanced,               // 균형잡힘
        Overtiered,            // 티어가 높음 (약함)
        Undertiered,           // 티어가 낮음 (강함)
        SeverelyOvertiered,    // 심각하게 약함
        SeverelyUndertiered,   // 심각하게 강함
        Overcooled,              //쿨다운이 너무 김
        Undercooled            //쿨다운이 너무 짧음
    }
}