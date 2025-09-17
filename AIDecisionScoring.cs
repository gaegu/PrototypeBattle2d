using SkillSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattleAI
{
    /// <summary>
    /// AI 의사결정 점수 계산 시스템
    /// </summary>
    public class AIDecisionScoring
    {
        /// <summary>
        /// 일반 공격 평가
        /// </summary>
        public AIDecision EvaluateAttack(
            BattleActor attacker,
            BattleActor target,
            AIDecisionWeights weights)
        {
            var decision = new AIDecision
            {
                ActionType = "attack",
                Target = target,
                BPUse = 0
            };

            float score = 0f;

            // 1. 데미지 효율성
            float estimatedDamage = CalculateEstimatedDamage(attacker, target, 0);
            float damageRatio = estimatedDamage / Math.Max(target.BattleActorInfo.Hp, 1);
            score += damageRatio * 100f * weights.damageEfficiency;

            // 2. 처치 가능성
            if (estimatedDamage >= target.BattleActorInfo.Hp)
            {
                score += 200f * weights.killPotential;
                decision.Reasoning = "Can kill target";
            }

            // 3. 타겟 위협도
            float threatLevel = CalculateThreatLevel(target);
            score += threatLevel * weights.targetThreat;

            // 4. 속성 상성
            float elementalBonus = GetElementalAdvantage(attacker, target);
            score *= elementalBonus * weights.elementalAdvantage;

            // 5. 타겟 역할 보너스
            score += GetTargetRoleBonus(target);

            // 6. 크리티컬 가능성
            float critChance = attacker.BattleActorInfo.CriRate / 100f;
            score += critChance * 50f;

            decision.Score = score;
            return decision;
        }

        /// <summary>
        /// BP 공격 평가
        /// </summary>
        public AIDecision EvaluateBPAttack(
            BattleActor attacker,
            BattleActor target,
            AIDecisionWeights weights,
            int bpAmount = 0)
        {
            if (bpAmount == 0)
                bpAmount = attacker.BattleActorInfo.Bp;

            var decision = new AIDecision
            {
                ActionType = "bp_attack",
                Target = target,
                BPUse = bpAmount
            };

            float score = 0f;

            // 1. BP 연속 공격 데미지 계산
            float totalDamage = 0f;
            for (int i = 0; i < bpAmount; i++)
            {
                totalDamage += CalculateEstimatedDamage(attacker, target, bpAmount);
            }

            float damageRatio = totalDamage / Math.Max(target.BattleActorInfo.Hp, 1);
            score += damageRatio * 100f * weights.damageEfficiency;

            // 2. 오버킬 페널티 (자원 낭비 방지)
            float overkillRatio = (totalDamage - target.BattleActorInfo.Hp) / Math.Max(totalDamage, 1);
            if (overkillRatio > 0.3f)
            {
                score *= (1f - overkillRatio * 0.5f);
                decision.Reasoning = "Overkill penalty applied";
            }

            // 3. 확실한 처치 보너스
            if (totalDamage >= target.BattleActorInfo.Hp * 1.2f)
            {
                score += 250f * weights.killPotential;
                decision.Reasoning = "Guaranteed kill";
            }

            // 4. BP 효율성 (BP가 많을수록 효율 감소)
            float bpEfficiency = 1f / (1f + bpAmount * 0.2f);
            score *= bpEfficiency * weights.resourceEfficiency;

            // 5. 타이밍 보너스 (중요한 적 처치)
            if (IsHighPriorityTarget(target))
            {
                score += 100f * bpAmount;
            }

            decision.Score = score;
            return decision;
        }

        /// <summary>
        /// 스킬 평가
        /// </summary>
        public List<AIDecision> EvaluateSkills(
            BattleActor attacker,
            BattleActor target,
            AIDecisionWeights weights)
        {
            var decisions = new List<AIDecision>();

            // 사용 가능한 스킬 목록 가져오기
            var availableSkills = GetAvailableSkills(attacker);

            foreach (var skill in availableSkills)
            {
                // MP 체크
                if (attacker.CooldownManager.CanUseSkill(skill.cooldown) == false )
                    continue;

                var decision = new AIDecision
                {
                    ActionType = "skill",
                    Target = target,
                    SkillId = skill.skillId
                };

                float score = 0f;

                // 1. 스킬 데미지/효과 평가
                foreach (var effect in skill.effects)
                {
                    score += EvaluateSkillEffect(effect, attacker, target, weights);
                }




                // 2. 쿨다운 효율성 평가 (MP 효율성 대신)
                // 쿨다운이 긴 스킬은 더 신중하게 사용
                float cooldownPenalty = 1f;
                if (skill.cooldown > 0)
                {
                    // 쿨다운이 길수록 페널티 증가
                    cooldownPenalty = 1f / (1f + skill.cooldown * 0.1f);

                    // 하지만 강력한 스킬이면 페널티 감소
                    if (score > 200f)
                    {
                        cooldownPenalty = Math.Max(cooldownPenalty, 0.8f);
                    }
                }
                score *= cooldownPenalty * weights.resourceEfficiency;


                // 3. 타이밍 평가
                // 전투 초반에는 쿨다운이 긴 강력한 스킬 선호
                // 전투 후반에는 쿨다운이 짧은 스킬 선호
               /* if (IsEarlyBattle())
                {
                    if (skill.cooldown >= 3 && score > 150f)
                    {
                        score *= 1.2f; // 초반 강력 스킬 보너스
                        decision.Reasoning = "Strong opening skill";
                    }
                }
                else if (IsLateBattle())
                {
                    if (skill.cooldown <= 1)
                    {
                        score *= 1.3f; // 후반 속전속결 보너스
                        decision.Reasoning = "Quick cooldown for late battle";
                    }
                }*/


                // 4. AOE 보너스
                if (IsAOESkill(skill))
                {
                    int targetCount = CountPossibleTargets(skill, target);
                    score *= targetCount;
                    decision.Reasoning = $"AOE skill hitting {targetCount} targets";
                }

                // 5. 상태이상 가치
                if (HasStatusEffect(skill))
                {
                    score += EvaluateStatusEffectValue(skill, target) * weights.comboSynergy;
                }

                decision.Score = score;
                decisions.Add(decision);
            }

            return decisions;
        }

        /// <summary>
        /// 예측 기반 공격 평가 (VeryHigh AI)
        /// </summary>
        public AIDecision EvaluateAttackWithPrediction(
            BattleActor attacker,
            BattleActor target,
            AIDecisionWeights weights,
            TurnPrediction prediction)
        {
            var decision = EvaluateAttack(attacker, target, weights);

            // 예측 보정
            if (prediction != null)
            {
                // 다음 턴에 이 타겟이 행동하는가?
                if (prediction.NextActors.Contains(target))
                {
                    decision.Score *= 1.3f; // 곧 행동할 적 우선
                }

                // 이 타겟이 아군에게 큰 위협인가?
                if (prediction.ThreatLevel.ContainsKey(target) && prediction.ThreatLevel[target] > 0.7f)
                {
                    decision.Score *= 1.2f;
                }

                // 다음 턴에 아군이 연계 가능한가?
                if (prediction.CanCombo)
                {
                    decision.Score *= 1.1f;
                }
            }

            return decision;
        }

        /// <summary>
        /// 전략적 BP 사용 평가 (VeryHigh AI)
        /// </summary>
        public AIDecision EvaluateBPWithStrategy(
            BattleActor attacker,
            BattleActor target,
            int bpAmount,
            AIDecisionWeights weights,
            BattleContext context)
        {
            var decision = EvaluateBPAttack(attacker, target, weights, bpAmount);

            // 전략적 보정
            // 1. 현재 이기고 있으면 BP 아껴두기
            if (context.IsWinning && !IsHighPriorityTarget(target))
            {
                decision.Score *= 0.7f;
            }

            // 2. 위기 상황에서는 BP 적극 사용
            if (context.IsCriticalSituation)
            {
                decision.Score *= 1.5f;
            }

            // 3. 라운드 후반에 BP 사용 선호
            if (context.CurrentRound > 5)
            {
                decision.Score *= 1.2f;
            }

            // 4. 연속 처치 가능성
            if (CanChainKill(attacker, target, bpAmount))
            {
                decision.Score *= 1.4f;
                decision.Reasoning = "Chain kill possible";
            }

            return decision;
        }

        #region Helper Methods

        private float CalculateEstimatedDamage(BattleActor attacker, BattleActor target, int usedBP)
        {
            var context = new BattleFormularHelper.BattleContext
            {
                Attacker = attacker,
                Defender = target,
                SkillPower = 100,
                UsedBP = usedBP
            };

            var result = BattleFormularHelper.CalculateDamage(context);

            // 평균 데미지 계산 (크리티컬 고려)
            float critChance = attacker.BattleActorInfo.CriRate / 100f;
            float normalDamage = result.FinalDamage;
            float critDamage = normalDamage * (attacker.BattleActorInfo.CriDmg / 100f);

            return normalDamage * (1f - critChance) + critDamage * critChance;
        }

        private float CalculateThreatLevel(BattleActor target)
        {
            float threat = 0f;

            // 1. DPS 계산
            float dps = target.BattleActorInfo.Attack * (target.BattleActorInfo.TurnSpeed / 100f);
            threat += dps / 100f;

            // 2. 남은 HP 비율 (낮을수록 위협도 감소)
            threat *= target.BattleActorInfo.HpRatio;

            // 3. 역할별 위협도
            string role = GetCharacterRole(target);
            threat += GetRoleThreat(role);

            // 4. 버프 상태
            if (HasBuff(target))
            {
                threat *= 1.2f;
            }

            return threat;
        }

        private float GetElementalAdvantage(BattleActor attacker, BattleActor target)
        {
            // BattleFormularHelper의 속성 상성 활용
            var element1 = attacker.BattleActorInfo.Element;
            var element2 = target.BattleActorInfo.Element;

            return BattleFormularHelper.GetElementalMultiplier(element1, element2);
        }

        private float GetTargetRoleBonus(BattleActor target)
        {
            string role = GetCharacterRole(target);
            return role switch
            {
                "Healer" => 50f,
                "Support" => 40f,
                "DPS" => 30f,
                "Tank" => 20f,
                _ => 25f
            };
        }

        private bool IsHighPriorityTarget(BattleActor target)
        {
            string role = GetCharacterRole(target);

            // 힐러, 서포터, HP 30% 이하
            return role == "Healer" ||
                   role == "Support" ||
                   target.BattleActorInfo.HpRatio < 0.3f;
        }

        private List<AdvancedSkillData> GetAvailableSkills(BattleActor actor)
        {
            // 스킬 매니저에서 사용 가능한 스킬 가져오기
            var skills = new List<AdvancedSkillData>();

            // 테스트용 기본 스킬 추가
           // if (actor.BattleActorInfo.Mp >= 10)
            {
                skills.Add(CreateTestSkill(1001, "Fire Ball", 10));
            }

         //   if (actor.BattleActorInfo.Mp >= 20)
            {
                skills.Add(CreateTestSkill(1002, "Lightning Strike", 20));
            }

            return skills;
        }

        private AdvancedSkillData CreateTestSkill(int id, string name, int mpCost)
        {
            return new AdvancedSkillData
            {
                skillId = id,
                skillName = name,
                cooldown = 0,
                effects = new List<AdvancedSkillEffect>
                {
                    new AdvancedSkillEffect
                    {
                        type = SkillSystem.EffectType.Damage,
                        value = 150f,
                        chance = 1f
                    }
                }
            };
        }

        private float EvaluateSkillEffect(
            AdvancedSkillEffect effect,
            BattleActor attacker,
            BattleActor target,
            AIDecisionWeights weights)
        {
            float score = 0f;

            switch (effect.type)
            {
                case SkillSystem.EffectType.Damage:
                    float damage = attacker.BattleActorInfo.Attack * (effect.value / 100f);
                    score = damage / Math.Max(target.BattleActorInfo.MaxHp, 1) * 100f;
                    break;

                case SkillSystem.EffectType.Heal:
                    float healAmount = attacker.BattleActorInfo.MaxHp * (effect.value / 100f);
                    float healNeed = attacker.BattleActorInfo.MaxHp - attacker.BattleActorInfo.Hp;
                    score = (healAmount / Math.Max(healNeed, 1)) * 50f * weights.survivalPriority;
                    break;

                case SkillSystem.EffectType.Buff:
                    // 버프도 적에게는 음수
                    score = effect.value * effect.duration * 5f;
                    break;

                case SkillSystem.EffectType.Debuff:
                    score = effect.value * effect.duration * 6f;
                    break;

                case SkillSystem.EffectType.StatusEffect:
                    score = EvaluateStatusValue(effect.statusType) * effect.duration;
                    break;
                default:
                    score = 20f; // 기본 점수
                    break;
            }


            // 타겟 타입에 따른 보정
            if (effect.targetType == SkillSystem.TargetType.EnemyAll || effect.targetType == SkillSystem.TargetType.AllyAll)
            {
                score *= 2.5f; // 전체 대상 보너스
            }


            return score * effect.chance; // 확률 적용
        }


        /// <summary>
        /// 쿨다운 관리 전략 헬퍼 함수들
        /// </summary>
       /* private bool IsEarlyBattle()
        {
            // 전투 시작 3턴 이내
            return currentTurn <= 3;
        }

        private bool IsLateBattle()
        {
            // 적 팀 생존자가 2명 이하거나 아군 생존자가 2명 이하
            return GetAliveEnemyCount() <= 2 || GetAliveAllyCount() <= 2;
        }*/


        private bool IsCriticalSituation(BattleActor attacker, BattleActor target)
        {
            // 위급 상황 판단
            return attacker.BattleActorInfo.Hp < attacker.BattleActorInfo.MaxHp * 0.3f ||
                   target.BattleActorInfo.Hp < target.BattleActorInfo.MaxHp * 0.2f;
        }


        private float EvaluateStatusValue(SkillSystem.StatusType status)
        {
            return status switch
            {
                SkillSystem.StatusType.Stun => 100f,
                SkillSystem.StatusType.Silence => 80f,
                SkillSystem.StatusType.Slow => 60f,
                SkillSystem.StatusType.Poison => 50f,
                SkillSystem.StatusType.Burn => 45f,
                SkillSystem.StatusType.Freeze => 90f,
                _ => 30f
            };
        }

        private bool IsAOESkill(AdvancedSkillData skill)
        {
            return skill.effects.Any(e =>
                e.targetType == SkillSystem.TargetType.EnemyAll ||
                e.targetType == SkillSystem.TargetType.EnemyRow);
        }

        private int CountPossibleTargets(AdvancedSkillData skill, BattleActor primaryTarget)
        {
            // 스킬의 타겟 타입에 따라 계산
            if (skill.effects.Any(e => e.targetType == SkillSystem.TargetType.EnemyAll))
            {
                // 전체 공격이면 적 수 반환
                // 실제로는 BattleProcessManagerNew에서 가져와야 함
                return 5;
            }
            else if (skill.effects.Any(e => e.targetType == SkillSystem.TargetType.EnemyRow))
            {
                // 열 공격이면 같은 열의 적 수 반환
                return 3;
            }

            return 1;
        }

        private bool HasStatusEffect(AdvancedSkillData skill)
        {
            return skill.effects.Any(e => e.type == SkillSystem.EffectType.StatusEffect);
        }

        private float EvaluateStatusEffectValue(AdvancedSkillData skill, BattleActor target)
        {
            float value = 0f;

            foreach (var effect in skill.effects.Where(e => e.type == SkillSystem.EffectType.StatusEffect))
            {
                // 이미 같은 상태이상이 있으면 가치 감소
                if (HasStatusEffect(target, effect.statusType))
                {
                    value += EvaluateStatusValue(effect.statusType) * 0.3f;
                }
                else
                {
                    value += EvaluateStatusValue(effect.statusType);
                }
            }

            return value;
        }

        private bool CanChainKill(BattleActor attacker, BattleActor firstTarget, int bpAmount)
        {
            // BP 공격으로 첫 타겟 처치 후 다른 적도 처치 가능한지
            float firstDamage = CalculateEstimatedDamage(attacker, firstTarget, bpAmount);

            if (firstDamage >= firstTarget.BattleActorInfo.Hp)
            {
                // 실제 구현에서는 남은 BP로 다른 적 처치 가능한지 체크
                // 여기서는 간단히 false 반환
                return false;
            }

            return false;
        }

        private string GetCharacterRole(BattleActor actor)
        {
            // BattleCharInfoNew에서 역할 가져오기
            // 임시로 스탯 기반 판단
            var info = actor.BattleActorInfo;

            if (info.Attack > info.Defence * 2)
                return "DPS";
            if (info.Defence > info.Attack * 2)
                return "Tank";


            return "Support";
        }

        private float GetRoleThreat(string role)
        {
            return role switch
            {
                "Healer" => 100f,
                "Support" => 80f,
                "DPS" => 60f,
                "Tank" => 40f,
                _ => 50f
            };
        }

        private bool HasBuff(BattleActor actor)
        {
            // 실제로는 actor의 버프 상태 체크
            // 임시로 false 반환
            return false;
        }

        private bool HasStatusEffect(BattleActor target, SkillSystem.StatusType statusType)
        {
            // 실제로는 target의 상태이상 체크
            if (target.SkillManager != null)
            {
                return target.SkillManager.HasSkill(statusType);
            }
            return false;
        }

        #endregion
    }
}