using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using SkillSystem;

namespace BattleAI
{
    /// <summary>
    /// AI 지능 레벨
    /// </summary>
    public enum AIIntelligenceLevel
    {
        Low = 0,        // 무작위에 가까운 행동
        Medium = 1,     // 기본적인 전략
        High = 2,       // 효율적인 전략
        VeryHigh = 3    // 최적화된 전략
    }

    /// <summary>
    /// AI 성격 타입
    /// </summary>
    public enum AIPersonality
    {
        Aggressive,     // 공격 위주 - 높은 공격력 적 우선
        Defensive,      // 방어 위주 - 안전한 선택
        Supportive,     // 지원 위주 - 아군 지원 우선
        Balanced,       // 균형형 - 상황에 따라
        Berserker,      // 광전사 - HP 낮을수록 공격적
        Tactical,       // 전략적 - 효율성 중시
        Assassin        // 암살자 - 후방/힐러 우선
    }

    /// <summary>
    /// AI 행동 결정
    /// </summary>
    public class AIDecision
    {
        public string ActionType { get; set; }  // "attack", "skill", "bp_attack", "bp_skill"
        public BattleActor Target { get; set; }
        public int SkillId { get; set; }
        public int BPUse { get; set; }
        public float Score { get; set; }
        public string Reasoning { get; set; }  // 디버깅용
    }

    /// <summary>
    /// 캐릭터 AI 프로필
    /// </summary>
    [System.Serializable]
    public class CharacterAIProfile
    {
        [Header("기본 설정")]
        public AIIntelligenceLevel intelligenceLevel = AIIntelligenceLevel.Medium;
        public AIPersonality personality = AIPersonality.Balanced;

        [Header("행동 성향 (0~1)")]
        [Range(0f, 1f)]
        public float aggressiveness = 0.5f;      // 0: 방어적, 1: 공격적
        [Range(0f, 1f)]
        public float skillPreference = 0.5f;     // 0: 일반공격 선호, 1: 스킬 선호
        [Range(0f, 1f)]
        public float teamworkFocus = 0.5f;       // 0: 개인플레이, 1: 팀플레이
        [Range(0f, 1f)]
        public float resourceConservation = 0.5f; // 0: 자원 낭비적, 1: 자원 절약적

        [Header("특수 행동")]
        public bool prioritizeBackline = false;   // 후방 우선 공격
        public bool focusLowestHP = false;        // 가장 약한 적 집중
        public bool chainAttackMaster = false;    // 연계 공격 특화
        public int[] preferredSkillIds;           // 선호 스킬 ID 목록

        [Header("BP 전략")]
        public int bpThreshold = 2;              // BP 사용 최소 개수
        public bool saveBPForKill = true;        // 킬 확정용 BP 저장
    }

    /// <summary>
    /// AI 의사결정 가중치
    /// </summary>
    [System.Serializable]
    public class AIDecisionWeights
    {
        public float damageEfficiency = 1.0f;      // 데미지 효율성
        public float killPotential = 1.2f;         // 처치 가능성
        public float survivalPriority = 0.8f;      // 생존 우선순위
        public float resourceEfficiency = 0.9f;    // 자원 효율성
        public float elementalAdvantage = 1.1f;    // 속성 상성
        public float targetThreat = 1.0f;          // 타겟 위협도
        public float comboSynergy = 0.7f;          // 콤보 시너지
    }

    /// <summary>
    /// 전투 상황 컨텍스트
    /// </summary>
    public class BattleContext
    {
        public int TotalEnemies { get; set; }
        public int TotalAllies { get; set; }
        public int DeadEnemies { get; set; }
        public int DeadAllies { get; set; }
        public float AverageEnemyHP { get; set; }
        public float AverageAllyHP { get; set; }
        public int CurrentRound { get; set; }
        public bool IsWinning { get; set; }
        public bool IsLosing { get; set; }
        public bool IsCriticalSituation { get; set; }
    }

    /// <summary>
    /// 턴 예측 데이터
    /// </summary>
    public class TurnPrediction
    {
        public List<BattleActor> NextActors { get; set; } = new List<BattleActor>();
        public Dictionary<BattleActor, float> ThreatLevel { get; set; } = new Dictionary<BattleActor, float>();
        public bool CanCombo { get; set; }
        public float WinProbability { get; set; }
    }

    /// <summary>
    /// 메인 AI 매니저
    /// </summary>
    public class BattleAIManager : MonoBehaviour
    {
        private static BattleAIManager instance;
        public static BattleAIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("BattleAIManager");
                    instance = go.AddComponent<BattleAIManager>();
                }
                return instance;
            }
        }

        [Header("AI 설정")]
        [SerializeField] private bool enableAI = true;
        [SerializeField] private bool debugMode = true;
        [SerializeField] private float decisionDelay = 0.5f;  // AI 결정 지연 시간

        [Header("지능 레벨별 가중치")]
        private Dictionary<AIIntelligenceLevel, AIDecisionWeights> intelligenceWeights;

        private AIDecisionScoring decisionScoring;
        private AITargetSelector targetSelector;
        private AIResourceManager resourceManager;
        private AIPatternAnalyzer patternAnalyzer;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            InitializeComponents();
            InitializeWeights();
        }

        private void InitializeComponents()
        {
            decisionScoring = new AIDecisionScoring();
            targetSelector = new AITargetSelector();
            resourceManager = new AIResourceManager();
            patternAnalyzer = new AIPatternAnalyzer();
        }

        private void InitializeWeights()
        {
            intelligenceWeights = new Dictionary<AIIntelligenceLevel, AIDecisionWeights>
            {
                [AIIntelligenceLevel.Low] = new AIDecisionWeights
                {
                    damageEfficiency = 0.5f,
                    killPotential = 0.3f,
                    survivalPriority = 0.2f,
                    resourceEfficiency = 0.1f,
                    elementalAdvantage = 0.2f,
                    targetThreat = 0.3f,
                    comboSynergy = 0f
                },
                [AIIntelligenceLevel.Medium] = new AIDecisionWeights
                {
                    damageEfficiency = 0.8f,
                    killPotential = 0.9f,
                    survivalPriority = 0.6f,
                    resourceEfficiency = 0.5f,
                    elementalAdvantage = 0.7f,
                    targetThreat = 0.7f,
                    comboSynergy = 0.3f
                },
                [AIIntelligenceLevel.High] = new AIDecisionWeights
                {
                    damageEfficiency = 1.0f,
                    killPotential = 1.3f,
                    survivalPriority = 0.9f,
                    resourceEfficiency = 1.0f,
                    elementalAdvantage = 1.2f,
                    targetThreat = 1.1f,
                    comboSynergy = 0.8f
                },
                [AIIntelligenceLevel.VeryHigh] = new AIDecisionWeights
                {
                    damageEfficiency = 1.2f,
                    killPotential = 1.5f,
                    survivalPriority = 1.0f,
                    resourceEfficiency = 1.3f,
                    elementalAdvantage = 1.4f,
                    targetThreat = 1.3f,
                    comboSynergy = 1.2f
                }
            };
        }

        /// <summary>
        /// AI 행동 결정 - 메인 진입점
        /// </summary>
        public async UniTask<AIDecision> MakeDecision(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies,
            CancellationToken token = default)
        {
            if (!enableAI || actor == null || enemies == null || enemies.Count == 0)
            {
                return GetDefaultDecision(actor, enemies);
            }

            // AI 결정 지연 (자연스러움을 위해)
            await UniTask.Delay(TimeSpan.FromSeconds(decisionDelay), cancellationToken: token);

            // AI 프로필 가져오기
            var aiProfile = GetAIProfile(actor);
            var intelligence = aiProfile.intelligenceLevel;

            // 지능 레벨별 결정
            AIDecision decision = intelligence switch
            {
                AIIntelligenceLevel.Low => MakeLowIntelligenceDecision(actor, enemies, allies, aiProfile),
                AIIntelligenceLevel.Medium => MakeMediumIntelligenceDecision(actor, enemies, allies, aiProfile),
                AIIntelligenceLevel.High => MakeHighIntelligenceDecision(actor, enemies, allies, aiProfile),
                AIIntelligenceLevel.VeryHigh => MakeVeryHighIntelligenceDecision(actor, enemies, allies, aiProfile),
                _ => GetDefaultDecision(actor, enemies)
            };

            if (debugMode)
            {
                LogDecision(actor, decision);
            }

            return decision;
        }

        /// <summary>
        /// Low Intelligence - 단순한 행동
        /// </summary>
        private AIDecision MakeLowIntelligenceDecision(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies,
            CharacterAIProfile profile)
        {
            var decision = new AIDecision();
            var random = UnityEngine.Random.Range(0f, 1f);


            
            // 70% 일반 공격, 30% 스킬
            if (random < 0.7f || actor.GetAvailableActiveSkill() != null )
            {
                decision.ActionType = "attack";

                // BP 3개면 무조건 사용
                if (actor.BattleActorInfo.Bp >= 3)
                {
                    decision.ActionType = "bp_attack";
                    decision.BPUse = actor.BattleActorInfo.Bp;
                }
            }
            else
            {
                decision.ActionType = "skill";
                decision.SkillId = GetRandomAvailableSkill(actor);
            }

            // 무작위 타겟 (50%) 또는 가장 HP 높은 적 (50%)
            if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
            {
                decision.Target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
            }
            else
            {
                decision.Target = enemies.OrderByDescending(e => e.BattleActorInfo.Hp).FirstOrDefault();
            }

            decision.Reasoning = "Low AI: Random or highest HP target";
            return decision;
        }

        /// <summary>
        /// Medium Intelligence - 기본 전략
        /// </summary>
        private AIDecision MakeMediumIntelligenceDecision(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies,
            CharacterAIProfile profile)
        {
            var decision = new AIDecision();

            // HP 30% 이하 적 찾기
            var lowHealthEnemies = enemies.Where(e => e.BattleActorInfo.HpRatio < 0.3f).ToList();

            // 1. 처치 가능한 적이 있으면 공격
            if (lowHealthEnemies.Any())
            {
                var target = lowHealthEnemies.OrderBy(e => e.BattleActorInfo.Hp).First();
                var estimatedDamage = EstimateDamage(actor, target);

                if (estimatedDamage >= target.BattleActorInfo.Hp)
                {
                    decision.ActionType = "attack";
                    decision.Target = target;

                    // BP로 확실한 처치
                    if (actor.BattleActorInfo.Bp >= 2)
                    {
                        decision.ActionType = "bp_attack";
                        decision.BPUse = 2;
                    }

                    decision.Reasoning = "Medium AI: Finishing low HP enemy";
                    return decision;
                }
            }

            // 2. 타겟 선택 - targetSelector 사용
            decision.Target = targetSelector.SelectTarget(actor, enemies, AIIntelligenceLevel.Medium, profile);

            // 3. 액션 결정
            // MP 50% 이상이면 스킬 사용 고려
            /*if (actor.BattleActorInfo.Mp >= actor.BattleActorInfo.MaxMp * 0.5f)
            {
                var skillId = GetBestAvailableSkill(actor, decision.Target);
                if (skillId > 0)
                {
                    decision.ActionType = "skill";
                    decision.SkillId = skillId;
                }
                else
                {
                    decision.ActionType = "attack";
                }
            }
            else
            {
                decision.ActionType = "attack";
            }*/

            var availableSkills = GetAvailableSkillsWithCooldown(actor);

            if (availableSkills.Count > 0)
            {
                //var skillId = SelectBestAvailableSkill(availableSkills, decision.Target);
                var skillId = availableSkills[0].skillId;
                if (skillId > 0)
                {
                    decision.ActionType = "skill";
                    decision.SkillId = skillId;
                }
                else
                {
                    decision.ActionType = "attack";
                }
            }
            else
            {
                decision.ActionType = "attack";
            }



            decision.Reasoning = "Medium AI: Basic strategy";
            return decision;
        }

        /// <summary>
        /// High Intelligence - 효율적 전략
        /// </summary>
        private AIDecision MakeHighIntelligenceDecision(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies,
            CharacterAIProfile profile)
        {
            var allDecisions = new List<AIDecision>();
            var weights = intelligenceWeights[AIIntelligenceLevel.High];

            foreach (var enemy in enemies)
            {
                // 일반 공격 평가
                var attackDecision = decisionScoring.EvaluateAttack(actor, enemy, weights);
                allDecisions.Add(attackDecision);

                // BP 공격 평가 (BP 2개 이상)
                if (actor.BattleActorInfo.Bp >= 2)
                {
                    var bpDecision = decisionScoring.EvaluateBPAttack(actor, enemy, weights);
                    allDecisions.Add(bpDecision);
                }


                var availableSkills = GetAvailableSkillsWithCooldown(actor);

                if (availableSkills.Count > 0)
                {
                    var skillDecisions = decisionScoring.EvaluateSkills(actor, enemy, weights);
                    allDecisions.AddRange(skillDecisions);
                }
            }

            // 점수가 가장 높은 행동 선택
            var bestDecision = allDecisions.OrderByDescending(d => d.Score).First();
            bestDecision.Reasoning = $"High AI: Best score {bestDecision.Score:F2}";

            return bestDecision;
        }

        /// <summary>
        /// Very High Intelligence - 최적화 전략
        /// </summary>
        private AIDecision MakeVeryHighIntelligenceDecision(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies,
            CharacterAIProfile profile)
        {
            var weights = intelligenceWeights[AIIntelligenceLevel.VeryHigh];
            var allDecisions = new List<AIDecision>();

            // 1. 현재 상황 분석
            var battleContext = AnalyzeBattleContext(actor, enemies, allies);

            // 2. 다음 턴 예측
            var nextTurnPrediction = PredictNextTurn(actor, enemies, allies);

            // 3. 모든 가능한 행동 조합 평가
            foreach (var enemy in enemies)
            {
                // 일반 공격
                var attackDecision = decisionScoring.EvaluateAttackWithPrediction(
                    actor, enemy, weights, nextTurnPrediction);
                allDecisions.Add(attackDecision);

                // BP 전략적 사용
                if (actor.BattleActorInfo.Bp > 0)
                {
                    for (int bp = 1; bp <= actor.BattleActorInfo.Bp; bp++)
                    {
                        var bpDecision = decisionScoring.EvaluateBPWithStrategy(
                            actor, enemy, bp, weights, battleContext);
                        allDecisions.Add(bpDecision);
                    }
                }


                var availableSkills = GetAvailableSkillsWithCooldown(actor);

                // 스킬 콤보 고려
                if (availableSkills.Count > 0)
                {
                    var comboDecisions = EvaluateSkillCombos(actor, enemy, allies, weights);
                    allDecisions.AddRange(comboDecisions);
                }
            }

            // 4. 팀 시너지 보너스 적용
            ApplyTeamSynergyBonus(allDecisions, allies, battleContext);

            // 5. 최적 행동 선택
            var optimalDecision = allDecisions.OrderByDescending(d => d.Score).First();
            optimalDecision.Reasoning = $"VeryHigh AI: Optimal strategy (Score: {optimalDecision.Score:F2})";

            // 6. 학습 데이터 저장 (옵션)
            if (patternAnalyzer != null)
            {
                patternAnalyzer.RecordDecision(actor, optimalDecision, battleContext);
            }

            return optimalDecision;
        }

        #region Helper Methods

        private List<AdvancedSkillData> GetAvailableSkillsWithCooldown(BattleActor actor)
        {
            var allSkills = actor.SkillManager.GetAllActiveSkills();
            var available = new List<AdvancedSkillData>();

            foreach (var skill in allSkills)
            {
                if (actor.CooldownManager.CanUseSkill(skill.SkillID))
                {
                    available.Add(skill.SkillData);
                }
            }

            return available;
        }


        private CharacterAIProfile GetAIProfile(BattleActor actor)
        {
            // BattleCharInfoNew에서 AI 프로필 가져오기
            if (actor.BattleActorInfo?.AIProfile != null)
            {
                return actor.BattleActorInfo.AIProfile;
            }

            // 기본 프로필 반환
            return new CharacterAIProfile();
        }

        private AIDecision GetDefaultDecision(BattleActor actor, List<BattleActor> enemies)
        {
            return new AIDecision
            {
                ActionType = "attack",
                Target = enemies.FirstOrDefault(e => !e.IsDead),
                BPUse = 0,
                Reasoning = "Default: First available target"
            };
        }

        private float EstimateDamage(BattleActor attacker, BattleActor target)
        {
            // BattleFormularHelper 활용
            var context = new BattleFormularHelper.BattleContext
            {
                Attacker = attacker,
                Defender = target,
                SkillPower = 100,
                UsedBP = 0
            };

            var result = BattleFormularHelper.CalculateDamage(context);
            return result.FinalDamage;
        }

        private int GetRandomAvailableSkill(BattleActor actor)
        {
            // 임시 구현 - 실제로는 스킬 목록에서 선택
            return 1001;
        }

        private int GetBestAvailableSkill(BattleActor actor, BattleActor target)
        {
            // 속성 상성 고려한 스킬 선택
            // 임시 구현
            return 1002;
        }

        private BattleContext AnalyzeBattleContext(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies)
        {
            var context = new BattleContext
            {
                TotalEnemies = enemies.Count,
                TotalAllies = allies.Count,
                DeadEnemies = enemies.Count(e => e.IsDead),
                DeadAllies = allies.Count(a => a.IsDead),
                AverageEnemyHP = enemies.Where(e => !e.IsDead).Average(e => e.BattleActorInfo.HpRatio),
                AverageAllyHP = allies.Where(a => !a.IsDead).Average(a => a.BattleActorInfo.HpRatio),
                CurrentRound = 1, // 실제로는 BattleProcessManagerNew에서 가져옴
                IsWinning = false,
                IsLosing = false,
                IsCriticalSituation = false
            };

            // 상황 판단
            context.IsWinning = context.AverageAllyHP > context.AverageEnemyHP * 1.5f;
            context.IsLosing = context.AverageAllyHP < context.AverageEnemyHP * 0.5f;
            context.IsCriticalSituation = context.AverageAllyHP < 0.3f;

            return context;
        }

        private TurnPrediction PredictNextTurn(
            BattleActor actor,
            List<BattleActor> enemies,
            List<BattleActor> allies)
        {
            var prediction = new TurnPrediction();

            // 속도 기반 다음 턴 예측
            var allActors = enemies.Concat(allies).Where(a => !a.IsDead).ToList();
            prediction.NextActors = allActors
                .OrderByDescending(a => a.BattleActorInfo.TurnSpeed)
                .Take(3)
                .ToList();

            // 위협도 계산
            foreach (var enemy in enemies.Where(e => !e.IsDead))
            {
                float threat = enemy.BattleActorInfo.Attack * (enemy.BattleActorInfo.TurnSpeed / 100f);
                prediction.ThreatLevel[enemy] = threat;
            }

            // 콤보 가능 여부
            prediction.CanCombo = allies.Count(a => !a.IsDead && a.GetAvailableActiveSkill() != null ) >= 2;

            return prediction;
        }

        private List<AIDecision> EvaluateSkillCombos(
            BattleActor actor,
            BattleActor target,
            List<BattleActor> allies,
            AIDecisionWeights weights)
        {
            // 스킬 콤보 평가 - 실제 구현 필요
            return new List<AIDecision>();
        }

        private void ApplyTeamSynergyBonus(
            List<AIDecision> decisions,
            List<BattleActor> allies,
            BattleContext context)
        {
            foreach (var decision in decisions)
            {
                // 팀 시너지 보너스 계산
                float synergyBonus = 1f;

                // 아군이 같은 타겟 공격 중이면 보너스
                var sameTargetAllies = allies.Count(a =>
                    !a.IsDead &&
                    a != decision.Target);

                synergyBonus += sameTargetAllies * 0.1f;

                decision.Score *= synergyBonus;
            }
        }

        private void LogDecision(BattleActor actor, AIDecision decision)
        {
            Debug.Log($"[AI] {actor.name} Decision: {decision.ActionType} " +
                     $"Target: {decision.Target?.name} " +
                     $"BP: {decision.BPUse} " +
                     $"Reason: {decision.Reasoning}");
        }

        #endregion
    }


}