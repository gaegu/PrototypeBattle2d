using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattleAI
{
    /// <summary>
    /// AI 타겟 선택 시스템
    /// </summary>
    public class AITargetSelector
    {
        private Dictionary<BattleActor, float> aggroTable = new Dictionary<BattleActor, float>();

        /// <summary>
        /// 지능 레벨별 타겟 선택
        /// </summary>
        public BattleActor SelectTarget(
            BattleActor attacker,
            List<BattleActor> enemies,
            AIIntelligenceLevel intelligence,
            CharacterAIProfile profile = null)
        {
            if (enemies == null || enemies.Count == 0)
                return null;

            // 죽은 적 제외
            enemies = enemies.Where(e => e != null && !e.IsDead).ToList();
            if (!enemies.Any())
                return null;

            switch (intelligence)
            {
                case AIIntelligenceLevel.Low:
                    return SelectLowIntelligenceTarget(enemies, profile);

                case AIIntelligenceLevel.Medium:
                    return SelectMediumIntelligenceTarget(attacker, enemies, profile);

                case AIIntelligenceLevel.High:
                    return SelectHighIntelligenceTarget(attacker, enemies, profile);

                case AIIntelligenceLevel.VeryHigh:
                    return SelectVeryHighIntelligenceTarget(attacker, enemies, profile);

                default:
                    return enemies.FirstOrDefault();
            }
        }

        private BattleActor SelectLowIntelligenceTarget(
            List<BattleActor> enemies,
            CharacterAIProfile profile)
        {
            // 50% 무작위, 50% 가장 HP 높은 적
            if (UnityEngine.Random.value < 0.5f)
            {
                return enemies[UnityEngine.Random.Range(0, enemies.Count)];
            }
            else
            {
                return enemies.OrderByDescending(e => e.BattleActorInfo.Hp).First();
            }
        }

        private BattleActor SelectMediumIntelligenceTarget(
            BattleActor attacker,
            List<BattleActor> enemies,
            CharacterAIProfile profile)
        {
            // 1. HP 30% 이하 우선
            var lowHPTargets = enemies.Where(e => e.BattleActorInfo.HpRatio < 0.3f).ToList();
            if (lowHPTargets.Any())
            {
                return lowHPTargets.OrderBy(e => e.BattleActorInfo.Hp).First();
            }

            // 2. 힐러/서포터 우선
            var priorityTargets = enemies.Where(e =>
                GetCharacterRole(e) == "Healer" ||
                GetCharacterRole(e) == "Support").ToList();

            if (priorityTargets.Any())
            {
                return priorityTargets.OrderBy(e => e.BattleActorInfo.Hp).First();
            }

            // 3. 프로필 성향 적용
            if (profile?.prioritizeBackline == true)
            {
                var backline = GetBacklineEnemies(enemies);
                if (backline.Any())
                    return backline.First();
            }

            // 4. 기본: 앞열 중 HP 낮은 적
            return enemies.OrderBy(e => e.BattleActorInfo.Hp).First();
        }

        private BattleActor SelectHighIntelligenceTarget(
            BattleActor attacker,
            List<BattleActor> enemies,
            CharacterAIProfile profile)
        {
            var targetScores = new Dictionary<BattleActor, float>();

            foreach (var enemy in enemies)
            {
                float score = 0f;

                // 1. 위협도 계산
                float threat = CalculateThreatScore(enemy, attacker);
                score += threat * 100f;

                // 2. 처치 가능성
                float killPotential = CalculateKillPotential(attacker, enemy);
                score += killPotential * 150f;

                // 3. 속성 상성
                float elemental = GetElementalAdvantage(attacker, enemy);
                score *= elemental;

                // 4. 위치 보너스
                if (IsVulnerablePosition(enemy))
                {
                    score *= 1.2f;
                }

                // 5. 프로필 성향 반영
                if (profile != null)
                {
                    score = ApplyProfileModifiers(score, enemy, profile);
                }

                targetScores[enemy] = score;
            }

            return targetScores.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        private BattleActor SelectVeryHighIntelligenceTarget(
            BattleActor attacker,
            List<BattleActor> enemies,
            CharacterAIProfile profile)
        {
            var targetScores = new Dictionary<BattleActor, float>();

            foreach (var enemy in enemies)
            {
                float score = 0f;

                // 1. 기본 High 레벨 점수
                float baseScore = CalculateHighLevelScore(attacker, enemy);
                score += baseScore;

                // 2. 예측 기반 점수
                float futureScore = PredictFutureValue(attacker, enemy);
                score += futureScore * 50f;

                // 3. 팀 콤보 가능성
                float comboScore = CalculateTeamComboScore(attacker, enemy);
                score += comboScore * 30f;

                // 4. 전략적 중요도
                float strategicValue = GetStrategicValue(enemy);
                score *= strategicValue;

                // 5. 어그로 테이블 반영
                if (aggroTable.ContainsKey(enemy))
                {
                    score *= (1f + aggroTable[enemy] * 0.1f);
                }

                targetScores[enemy] = score;
            }

            // 최고 점수 타겟 선택
            var bestTarget = targetScores.OrderByDescending(kvp => kvp.Value).First();

            // 어그로 업데이트
            UpdateAggro(bestTarget.Key, 10f);

            return bestTarget.Key;
        }

        #region Helper Methods

        private float CalculateThreatScore(BattleActor enemy, BattleActor attacker)
        {
            float threat = 0f;

            // DPS 계산
            float dps = enemy.BattleActorInfo.Attack * (enemy.BattleActorInfo.TurnSpeed / 100f);
            threat += dps / 10f;

            // 남은 HP (낮을수록 위협도 감소)
            threat *= enemy.BattleActorInfo.HpRatio;

            // 역할별 위협도
            threat += GetRoleThreat(GetCharacterRole(enemy));

            // 버프/디버프 상태
            if (HasBuff(enemy))
                threat *= 1.3f;
            if (HasDebuff(enemy))
                threat *= 0.8f;

            return threat;
        }

        private float CalculateKillPotential(BattleActor attacker, BattleActor target)
        {
            var context = new BattleFormularHelper.BattleContext
            {
                Attacker = attacker,
                Defender = target,
                SkillPower = 100,
                UsedBP = 0
            };

            var damage = BattleFormularHelper.CalculateDamage(context);
            float turnsToKill = target.BattleActorInfo.Hp / Math.Max(damage.FinalDamage, 1);

            // 1턴 킬 가능: 100점, 2턴: 50점, 3턴: 25점...
            return 100f / Math.Max(turnsToKill, 1);
        }

        private float GetElementalAdvantage(BattleActor attacker, BattleActor target)
        {
            
            return BattleFormularHelper.GetElementalMultiplier(
                attacker.BattleActorInfo.Element,
                target.BattleActorInfo.Element
            );
        }

        private bool IsVulnerablePosition(BattleActor enemy)
        {
            // 고립되거나 보호받지 못하는 위치인지
            // 뒷열이면서 앞열이 없는 경우
            if (IsBackline(enemy))
            {
                // 앞열이 모두 죽었는지 체크 (실제 구현 필요)
                return false;
            }
            return false;
        }

        private float ApplyProfileModifiers(float baseScore, BattleActor enemy, CharacterAIProfile profile)
        {
            float score = baseScore;

            // 후방 우선 공격
            if (profile.prioritizeBackline && IsBackline(enemy))
            {
                score *= 1.5f;
            }

            // 가장 약한 적 집중
            if (profile.focusLowestHP && enemy.BattleActorInfo.HpRatio < 0.5f)
            {
                score *= 1.3f;
            }

            // 성격별 보정
            switch (profile.personality)
            {
                case AIPersonality.Aggressive:
                    if (GetCharacterRole(enemy) == "DPS")
                        score *= 1.2f;
                    break;

                case AIPersonality.Tactical:
                    if (GetCharacterRole(enemy) == "Support")
                        score *= 1.4f;
                    break;

                case AIPersonality.Assassin:
                    if (IsBackline(enemy))
                        score *= 1.6f;
                    break;
            }

            return score;
        }

        private float CalculateHighLevelScore(BattleActor attacker, BattleActor enemy)
        {
            // High 레벨의 기본 점수 계산
            float threat = CalculateThreatScore(enemy, attacker);
            float kill = CalculateKillPotential(attacker, enemy);
            float elemental = GetElementalAdvantage(attacker, enemy);

            return (threat * 0.5f + kill * 1.5f) * elemental;
        }

        private float PredictFutureValue(BattleActor attacker, BattleActor enemy)
        {
            // 다음 턴 예측 가치
            float value = 0f;

            // 다음 턴에 행동할 예정인가?
            float turnOrder = enemy.BattleActorInfo.TurnSpeed;
            value += turnOrder / 100f * 30f;

            // 특수 스킬 사용 가능한가?
            if (enemy.GetAvailableActiveSkill() != null)
            {
                value += 20f;
            }

            return value;
        }

        private float CalculateTeamComboScore(BattleActor attacker, BattleActor enemy)
        {
            // 팀 콤보 가능성 점수
            // 실제 구현에서는 다른 아군과의 연계 계산
            // 임시로 기본값 반환
            return 10f;
        }

        private float GetStrategicValue(BattleActor enemy)
        {
            // 전략적 가치
            float value = 1f;

            // 역할에 따른 가치
            string role = GetCharacterRole(enemy);
            if (role == "Healer")
                value *= 1.5f;
            else if (role == "Support")
                value *= 1.3f;

            // HP가 매우 낮으면 가치 증가 (확실한 처치)
            if (enemy.BattleActorInfo.HpRatio < 0.2f)
                value *= 1.4f;

            return value;
        }

        private void UpdateAggro(BattleActor target, float amount)
        {
            if (!aggroTable.ContainsKey(target))
                aggroTable[target] = 0f;

            aggroTable[target] += amount;

            // 최대값 제한
            aggroTable[target] = Mathf.Min(aggroTable[target], 100f);
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

        private string GetCharacterRole(BattleActor actor)
        {
            // BattleCharInfoNew에서 역할 판단
            var info = actor.BattleActorInfo;

            if (info.Attack > info.Defence * 2)
                return "DPS";
            if (info.Defence > info.Attack * 2)
                return "Tank";

            return "Support";
        }

        private List<BattleActor> GetBacklineEnemies(List<BattleActor> enemies)
        {
            var battlePosition = BattleProcessManagerNew.Instance.battlePosition;
            if (battlePosition != null)
            {
                var backSlots = battlePosition.GetBackRowSlots(false);
                return enemies.Where(e => backSlots.Contains(e.BattleActorInfo.SlotIndex)).ToList();
            }

            // Fallback: 뒤쪽 2명
            return enemies.Skip(Math.Max(0, enemies.Count - 2)).ToList();
        }

        private bool IsBackline(BattleActor actor)
        {
            var battlePosition = BattleProcessManagerNew.Instance.battlePosition;
            if (battlePosition != null)
            {
                return !battlePosition.IsFrontRow(
                    actor.BattleActorInfo.SlotIndex,
                    actor.BattleActorInfo.IsAlly
                );
            }
            return false;
        }

        private bool HasBuff(BattleActor actor)
        {
            // 실제로는 actor의 버프 상태 체크
            // SkillManager가 있으면 체크
            if (actor.SkillManager != null)
            {
                // 버프 스킬이 있는지 체크하는 로직 필요
                return false;
            }
            return false;
        }

        private bool HasDebuff(BattleActor actor)
        {
            // 실제로는 actor의 디버프 상태 체크
            if (actor.SkillManager != null)
            {
                // 디버프 상태 체크 로직 필요
                return false;
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// AI 자원 관리 시스템
    /// </summary>
    public class AIResourceManager
    {
        /// <summary>
        /// BP 사용 결정
        /// </summary>
        public int DecideBPUsage(
            BattleActor actor,
            BattleActor target,
            AIIntelligenceLevel intelligence,
            CharacterAIProfile profile = null)
        {
            int currentBP = actor.BattleActorInfo.Bp;

            if (currentBP == 0)
                return 0;

            switch (intelligence)
            {
                case AIIntelligenceLevel.Low:
                    // BP 3개면 전부 사용
                    return currentBP >= 3 ? currentBP : 0;

                case AIIntelligenceLevel.Medium:
                    // BP 2개 이상이고 적 HP 낮으면 사용
                    if (currentBP >= 2 && target.BattleActorInfo.HpRatio < 0.5f)
                        return Math.Min(currentBP, 2);
                    return 0;

                case AIIntelligenceLevel.High:
                    return DecideHighLevelBP(actor, target, currentBP, profile);

                case AIIntelligenceLevel.VeryHigh:
                    return DecideOptimalBP(actor, target, currentBP, profile);

                default:
                    return 0;
            }
        }

     

        private int DecideHighLevelBP(
            BattleActor actor,
            BattleActor target,
            int currentBP,
            CharacterAIProfile profile)
        {
            // 킬 확정 계산
            float damage = EstimateBPDamage(actor, target, 1);

            for (int bp = 1; bp <= currentBP; bp++)
            {
                float totalDamage = damage * bp;

                // 킬 확정 가능
                if (totalDamage >= target.BattleActorInfo.Hp * 1.1f)
                {
                    return bp;
                }
            }

            // 프로필 설정 확인
            if (profile != null && profile.bpThreshold > 0)
            {
                if (currentBP >= profile.bpThreshold)
                {
                    return profile.saveBPForKill ? 0 : profile.bpThreshold;
                }
            }

            return 0;
        }

        private int DecideOptimalBP(
            BattleActor actor,
            BattleActor target,
            int currentBP,
            CharacterAIProfile profile)
        {
            // 최적 BP 사용량 계산
            float targetHP = target.BattleActorInfo.Hp;
            float singleDamage = EstimateBPDamage(actor, target, 1);

            // 정확한 킬에 필요한 BP
            int requiredBP = Mathf.CeilToInt(targetHP / Math.Max(singleDamage, 1));

            if (requiredBP <= currentBP)
            {
                // 오버킬 최소화
                return requiredBP;
            }

            // 다음 턴 고려
            if (WillActorActNext(target))
            {
                // 위협적인 적이 다음 턴에 행동하면 최대 BP 사용
                return currentBP;
            }

            // 전략적 보존
            return 0;
        }


        private float EstimateBPDamage(BattleActor attacker, BattleActor target, int bp)
        {
            var context = new BattleFormularHelper.BattleContext
            {
                Attacker = attacker,
                Defender = target,
                SkillPower = 100,
                UsedBP = bp
            };

            var result = BattleFormularHelper.CalculateDamage(context);
            return result.FinalDamage;
        }

        private bool WillActorActNext(BattleActor actor)
        {
            // TurnOrderSystem과 연동하여 다음 턴 예측
            var turnOrder = BattleProcessManagerNew.Instance.turnOrderSystem;
            if (turnOrder != null)
            {
                var nextActors = turnOrder.GetNextActors(3);
                return nextActors.Contains(actor);
            }
            return false;
        }

    }

    /// <summary>
    /// AI 패턴 분석 및 학습
    /// </summary>
    public class AIPatternAnalyzer
    {
        private List<BattleRecord> battleHistory = new List<BattleRecord>();

        public class BattleRecord
        {
            public string ActorId { get; set; }
            public AIDecision Decision { get; set; }
            public BattleContext Context { get; set; }
            public float Result { get; set; } // 행동 결과 점수
            public DateTime Timestamp { get; set; }
        }

        public void RecordDecision(
            BattleActor actor,
            AIDecision decision,
            BattleContext context)
        {
            var record = new BattleRecord
            {
                ActorId = actor.name,
                Decision = decision,
                Context = context,
                Timestamp = DateTime.Now
            };

            battleHistory.Add(record);

            // 최대 1000개 기록 유지
            if (battleHistory.Count > 1000)
            {
                battleHistory.RemoveAt(0);
            }
        }

        public float GetPatternScore(BattleActor actor, AIDecision decision)
        {
            // 과거 유사한 상황에서의 성공률 분석
            var similarRecords = battleHistory
                .Where(r => r.ActorId == actor.name)
                .Where(r => r.Decision.ActionType == decision.ActionType)
                .Take(10)
                .ToList();

            if (!similarRecords.Any())
                return 1f;

            return similarRecords.Average(r => r.Result);
        }

        public void UpdateRecordResult(BattleActor actor, float resultScore)
        {
            // 마지막 기록 업데이트
            var lastRecord = battleHistory
                .Where(r => r.ActorId == actor.name)
                .LastOrDefault();

            if (lastRecord != null)
            {
                lastRecord.Result = resultScore;
            }
        }

        public List<AIDecision> GetSuccessfulPatterns(BattleActor actor, float threshold = 0.7f)
        {
            // 성공적인 패턴 반환
            return battleHistory
                .Where(r => r.ActorId == actor.name)
                .Where(r => r.Result >= threshold)
                .Select(r => r.Decision)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>
    /// TurnOrderSystem 확장 메서드 (AI용)
    /// </summary>
    public static class TurnOrderSystemExtensions
    {
        public static List<BattleActor> GetNextActors(this TurnOrderSystem turnOrder, int count)
        {
            // 실제 구현에서는 TurnOrderSystem의 내부 로직 사용
            // 임시로 빈 리스트 반환
            return new List<BattleActor>();
        }
    }
}