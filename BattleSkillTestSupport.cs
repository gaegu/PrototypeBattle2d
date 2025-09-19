#pragma warning disable CS1998
#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using SkillSystem;

/// <summary>
/// BattleSkill 시스템 테스트 도구 - 프로젝트에 맞춘 완전한 구현
/// </summary>
public class BattleSkillTestSupport : MonoBehaviour
{
    #region Enums
    public enum TestMode
    {
        SkillData,          // AdvancedSkillData 직접 사용
        QuickEffect,        // 빠른 효과 테스트
        CustomSkill,        // 커스텀 스킬 생성
        LoadFromDatabase    // 데이터베이스에서 로드
    }

    public enum TargetMode
    {
        Single,             // 단일 타겟
        AllEnemies,         // 모든 적
        AllAllies,          // 모든 아군
        Self,               // 자기 자신
        Random,             // 랜덤 타겟
        FromSkillData       // 스킬 데이터의 타겟 설정 사용
    }

    public enum ExecutionMethod
    {
        DirectSkillManager,     // SkillManager 직접 호출
        ActorUseSkill,         // BattleActor.UseSkill 사용
        CommandSystem,         // UseSkillCommand 사용
        BattleManager          // BattleProcessManagerNew 경유
    }

    public enum LogLevel
    {
        None,
        ErrorOnly,
        Basic,
        Detailed,
        Verbose
    }
    #endregion

    #region Test Settings
    [Header("===== 테스트 설정 =====")]
    [SerializeField] private TestMode testMode = TestMode.SkillData;
    [SerializeField] private TargetMode targetMode = TargetMode.FromSkillData;
    [SerializeField] private ExecutionMethod executionMethod = ExecutionMethod.ActorUseSkill;
    [SerializeField] private LogLevel logLevel = LogLevel.Basic;

    [Header("===== 자동화 옵션 =====")]
    [SerializeField] private bool autoRepeat = false;
    [SerializeField] private float repeatDelay = 1f;
    [SerializeField] private int repeatCount = 3;
    [SerializeField] private bool skipAnimations = false;
    [SerializeField] private bool autoFindActors = false;

    [Header("===== 스킬 데이터 테스트 =====")]
    [SerializeField] private AdvancedSkillData testSkillData;

    [Header("===== 빠른 효과 테스트 =====")]
    [SerializeField] private SkillSystem.EffectType quickEffectType = SkillSystem.EffectType.Damage;
    [SerializeField] private float quickEffectValue = 100f;
    [SerializeField] private int quickEffectDuration = 3;
    [SerializeField] private SkillSystem.StatType quickStatType = SkillSystem.StatType.Attack;
    [SerializeField] private SkillSystem.StatusType quickStatusType = SkillSystem.StatusType.Stun;

    [Header("===== 커스텀 스킬 생성 =====")]
    [SerializeField] private string customSkillName = "Test Skill";
    [SerializeField] private int customManaCost = 10;
    [SerializeField] private List<AdvancedSkillEffect> customEffects = new List<AdvancedSkillEffect>();

    [Header("===== 데이터베이스 로드 =====")]
    [SerializeField] private int databaseSkillId = 1001;
    [SerializeField] private SkillDatabaseAsset skillDatabase;

    [Header("===== 액터 설정 =====")]
    [SerializeField] private BattleActor testSender;
    [SerializeField] private BattleActor testTarget;
    [SerializeField] private List<BattleActor> additionalTargets = new List<BattleActor>();

    [Header("===== 디버그 옵션 =====")]
    [SerializeField] private bool showSkillStatus = true;
    [SerializeField] private bool showActorStats = true;
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private bool logToFile = false;
    [SerializeField] private string logFilePath = "Assets/SkillTestLog.txt";

    [Header("===== 성능 프로파일링 =====")]
    [SerializeField] private bool enableProfiling = false;
    [SerializeField] private int profileSampleCount = 100;
    #endregion

    #region Private Fields
    private List<string> testHistory = new List<string>();
    private Dictionary<string, float> performanceMetrics = new Dictionary<string, float>();
    private int currentRepeatCount = 0;
    private System.Text.StringBuilder logBuilder = new System.Text.StringBuilder();
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        if (autoFindActors)
        {
            FindActorsInScene();
        }

        ValidateSetup();
    }

    private void OnValidate()
    {
        // 로그 레벨 자동 조정
        if (verboseLogging && logLevel < LogLevel.Detailed)
        {
            logLevel = LogLevel.Detailed;
        }
    }
    #endregion

    #region Public Test Methods
    /// <summary>
    /// 스킬 테스트 실행 (메인 메서드)
    /// </summary>
    [ContextMenu("Execute Skill Test")]
    public async void ExecuteTest()
    {
        if (!ValidateSetup())
        {
            LogError("Setup validation failed!");
            return;
        }

        LogStatus("╔══════════════════════════════════════╗");
        LogStatus("║        SKILL TEST STARTED            ║");
        LogStatus("╚══════════════════════════════════════╝");

        if (autoRepeat)
        {
            currentRepeatCount = 0;
            while (currentRepeatCount < repeatCount)
            {
                LogStatus($"\n--- Test Cycle {currentRepeatCount + 1}/{repeatCount} ---");
                await ExecuteSkillTest();
                currentRepeatCount++;

                if (currentRepeatCount < repeatCount)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(repeatDelay));
                }
            }
            LogStatus($"\n=== Completed {repeatCount} test cycles ===");
        }
        else
        {
            await ExecuteSkillTest();
        }

        if (enableProfiling)
        {
            PrintPerformanceMetrics();
        }

        if (logToFile)
        {
            SaveLogToFile();
        }
    }

    /// <summary>
    /// 모든 스킬 제거
    /// </summary>
    [ContextMenu("Clear All Skills")]
    public void ClearAllSkills()
    {
        var actors = GetAllActors();
        foreach (var actor in actors)
        {
            if (actor?.SkillManager != null)
            {
                actor.SkillManager.Clear();
                LogStatus($"Cleared skills from {actor.name}");
            }
        }
    }

    /// <summary>
    /// 액터 상태 리셋
    /// </summary>
    [ContextMenu("Reset Actor States")]
    public void ResetActorStates()
    {
        var actors = GetAllActors();
        foreach (var actor in actors)
        {
            if (actor?.BattleActorInfo != null)
            {
                actor.BattleActorInfo.Hp = actor.BattleActorInfo.MaxHp;
                actor.SetState(BattleActorState.Idle);
                LogStatus($"Reset {actor.name} state");
            }
        }
    }

    /// <summary>
    /// 랜덤 스킬 테스트
    /// </summary>
    [ContextMenu("Test Random Skill")]
    public void TestRandomSkill()
    {
        testSkillData = GenerateRandomSkill();
        LogStatus($"Generated random skill: {testSkillData.skillName}");
        ExecuteTest();
    }

    /// <summary>
    /// 모든 버프 제거
    /// </summary>
    [ContextMenu("Remove All Buffs")]
    public void RemoveAllBuffs()
    {
        if (testTarget?.SkillManager != null)
        {
            testTarget.SkillManager.RemoveBuffs();
            LogStatus($"Removed all buffs from {testTarget.name}");
        }
    }

    /// <summary>
    /// 모든 디버프 제거
    /// </summary>
    [ContextMenu("Remove All Debuffs")]
    public void RemoveAllDebuffs()
    {
        if (testTarget?.SkillManager != null)
        {
            testTarget.SkillManager.RemoveDebuffs();
            LogStatus($"Removed all debuffs from {testTarget.name}");
        }
    }
    #endregion

    #region Core Execution
    /// <summary>
    /// 스킬 테스트 실행 - 메인 로직
    /// </summary>
    private async UniTask ExecuteSkillTest()
    {
        var startTime = Time.realtimeSinceStartup;

        // 1. 스킬 데이터 가져오기
        var skillData = GetTestSkillData();
        if (skillData == null)
        {
            LogError("No skill data available!");
            return;
        }

        LogStatus($"\n▶ Testing skill: {skillData.skillName} (ID: {skillData.skillId})");
        LogDetailed($"  Category: {skillData.category}, Tier: {skillData.tier}, Cooldown: {skillData.cooldown}");

        if (verboseLogging)
        {
            LogSkillDetails(skillData);
        }

        // 2. 타겟 결정
        var targets = GetTestTargets();
        if (targets == null || targets.Count == 0)
        {
            LogError("No valid targets!");
            return;
        }

        LogDetailed($"  Targets: {string.Join(", ", targets.Select(t => t.name))}");

        // 3. Sender 검증
        if (testSender == null)
        {
            LogError("Test sender is null!");
            return;
        }

        // 4. 실행 전 상태 기록
        if (showActorStats)
        {
            LogActorStatsBefore();
        }

        // 5. 실행 방법에 따라 스킬 실행
        bool executionSuccess = false;
        switch (executionMethod)
        {
            case ExecutionMethod.DirectSkillManager:
                executionSuccess = await ExecuteViaSkillManager(skillData, targets);
                break;

            case ExecutionMethod.ActorUseSkill:
                executionSuccess = await ExecuteViaActor(skillData, targets);
                break;

            case ExecutionMethod.CommandSystem:
                executionSuccess = await ExecuteViaCommand(skillData, targets);
                break;

            case ExecutionMethod.BattleManager:
                executionSuccess = await ExecuteViaBattleManager(skillData, targets);
                break;
        }

        if (!executionSuccess)
        {
            LogError($"Skill execution failed using {executionMethod}");
            return;
        }

        // 6. 애니메이션 대기
        if (!skipAnimations)
        {
            await UniTask.Delay(500);
        }

        // 7. 실행 후 상태 확인
        if (showActorStats)
        {
            LogActorStatsAfter();
        }

        if (showSkillStatus)
        {
            PrintSkillStatus();
        }

        // 8. 성능 기록
        if (enableProfiling)
        {
            var elapsed = Time.realtimeSinceStartup - startTime;
            RecordPerformance(skillData.skillName, elapsed);
        }

        // 9. 히스토리 기록
        RecordTestHistory(skillData.skillName);

        LogStatus($"✓ Skill test completed in {Time.realtimeSinceStartup - startTime:F3}s");
    }

    /// <summary>
    /// SkillManager를 통한 직접 실행
    /// </summary>
    private async UniTask<bool> ExecuteViaSkillManager(AdvancedSkillData skillData, List<BattleActor> targets)
    {
        LogStatus($"[Method: Direct SkillManager]");

        try
        {
            foreach (var target in targets)
            {
                if (target == null) continue;

                if (target.SkillManager == null)
                {
                    LogError($"Target {target.name} has no SkillManager!");
                    continue;
                }

                // 직접 스킬 적용
                target.SkillManager.ApplySkill(skillData, testSender, target);
                LogDetailed($"  Applied {skillData.skillName} to {target.name}");
            }

            if (!skipAnimations)
            {
                await UniTask.Delay(300);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Exception in SkillManager execution: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// BattleActor.UseSkill을 통한 실행
    /// </summary>
    private async UniTask<bool> ExecuteViaActor(AdvancedSkillData skillData, List<BattleActor> targets)
    {
        LogStatus($"[Method: BattleActor.UseSkill]");

        try
        {
            // MP 체크
            if (testSender.CooldownManager.CanUseSkill(skillData.skillId) == false )
            {
              //  LogWarning($"Not enough MP! Need: {skillData.manaCost}, Have: {testSender.BattleActorInfo.Mp}");
                testSender.CooldownManager.ResetSkillCooldown(skillData.skillId);

               LogStatus("  Reset Cooldown for testing");
            }

            // 스킬 사용
            if (targets.Count == 1)
            {
                await testSender.UseSkill(skillData, targets[0]);
            }
            else
            {
                await testSender.UseSkillOnGroup(skillData, targets);
            }

            LogDetailed($"  Skill executed via BattleActor");

            if (!skipAnimations)
            {
                await UniTask.Delay(300);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Exception in Actor execution: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Command 시스템을 통한 실행
    /// </summary>
    private async UniTask<bool> ExecuteViaCommand(AdvancedSkillData skillData, List<BattleActor> targets)
    {
        LogStatus($"[Method: Command System]");

        try
        {
            // UseSkillCommand 생성
            var command = new UseSkillCommand(skillData, targets);


            // 변경 후 - 생성자 사용
            var primaryTarget = targets.Count > 0 ? targets[0] : null;
            var context = new CommandContext(testSender, primaryTarget);


            // 실행 가능 여부 체크
            if (!command.CanExecute(context))
            {
                LogError("Command cannot be executed!");

                // 상세 이유 출력
                if (testSender.SkillManager?.HasSkill(SkillSystem.StatusType.Silence) == true)
                {
                    LogError($"  Reason: Actor is silenced");
                }
                if (testSender.SkillManager?.HasSkill(SkillSystem.StatusType.Stun) == true)
                {
                    LogError($"  Reason: Actor is stunned");
                }

                return false;
            }

            // 커맨드 실행
            var result = await command.ExecuteAsync(context);

            if (result.Success)
            {
                LogStatus($"  Command executed: {result.Message}");
                foreach (var effect in result.Effects)
                {
                    LogDetailed($"    - {effect}");
                }
            }
            else
            {
                LogError($"  Command failed: {result.Message}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Exception in Command execution: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// BattleProcessManagerNew를 통한 실행
    /// </summary>
    private async UniTask<bool> ExecuteViaBattleManager(AdvancedSkillData skillData, List<BattleActor> targets)
    {
        LogStatus($"[Method: BattleProcessManagerNew]");

        try
        {
            var battleManager = BattleProcessManagerNew.Instance;
            if (battleManager == null)
            {
                LogError("BattleProcessManagerNew not found!");
                LogStatus("  Falling back to direct skill application...");

                // 폴백: 직접 스킬 적용
                foreach (var target in targets)
                {
                    if (target?.SkillManager != null)
                    {
                        target.SkillManager.ApplySkill(skillData, testSender, target);
                    }
                }

                return true;
            }

            // BattleProcessManagerNew에 스킬 실행 요청
            // 실제 구현에 따라 수정 필요
            LogWarning("BattleProcessManagerNew integration not fully implemented");

            // 임시: 직접 스킬 적용
            foreach (var target in targets)
            {
                if (target?.SkillManager != null)
                {
                    target.SkillManager.ApplySkill(skillData, testSender, target);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Exception in BattleManager execution: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region Data Generation
    /// <summary>
    /// 테스트용 스킬 데이터 가져오기
    /// </summary>
    private AdvancedSkillData GetTestSkillData()
    {
        switch (testMode)
        {
            case TestMode.SkillData:
                return testSkillData;

            case TestMode.QuickEffect:
                return CreateQuickEffectSkill();

            case TestMode.CustomSkill:
                return CreateCustomSkill();

            case TestMode.LoadFromDatabase:
                return LoadSkillFromDatabase();

            default:
                return null;
        }
    }

    /// <summary>
    /// 빠른 효과 스킬 생성
    /// </summary>
    private AdvancedSkillData CreateQuickEffectSkill()
    {
        var skill = new AdvancedSkillData
        {
            skillId = 99999,
            skillName = $"Quick_{quickEffectType}",
            description = "Quick test skill",
            category = SkillSystem.SkillCategory.Active,
            effects = new List<AdvancedSkillEffect>()
        };

        var effect = new AdvancedSkillEffect
        {
            name = quickEffectType.ToString(),
            type = quickEffectType,
            value = quickEffectValue,
            duration = quickEffectDuration,
            chance = 1f,
            targetType = GetTargetTypeFromMode()
        };

        // 효과별 추가 설정
        switch (quickEffectType)
        {
            case SkillSystem.EffectType.Buff:
            case SkillSystem.EffectType.Debuff:
                effect.statType = quickStatType;
                break;

            case SkillSystem.EffectType.StatusEffect:
                effect.statusType = quickStatusType;
                break;

            case SkillSystem.EffectType.Damage:
                effect.damageType = SkillSystem.DamageType.Physical;
                effect.canCrit = true;
                break;

            case SkillSystem.EffectType.Heal:
                effect.healBase = SkillSystem.HealBase.MaxHP;
                break;
        }

        skill.effects.Add(effect);
        return skill;
    }

    /// <summary>
    /// 커스텀 스킬 생성
    /// </summary>
    private AdvancedSkillData CreateCustomSkill()
    {
        return new AdvancedSkillData
        {
            skillId = 99998,
            skillName = customSkillName,
            description = "Custom test skill",
            category = SkillSystem.SkillCategory.Active,
            effects = new List<AdvancedSkillEffect>(customEffects)
        };
    }

    /// <summary>
    /// 데이터베이스에서 스킬 로드
    /// </summary>
    private AdvancedSkillData LoadSkillFromDatabase()
    {
        if (skillDatabase == null)
        {
            LogError("Skill database not assigned!");
            return null;
        }

        var skill = skillDatabase.database.GetSkillById(databaseSkillId);
        if (skill == null)
        {
            LogError($"Skill ID {databaseSkillId} not found in database!");
        }
        return skill;
    }

    /// <summary>
    /// 랜덤 스킬 생성
    /// </summary>
    private AdvancedSkillData GenerateRandomSkill()
    {
        var effectTypes = Enum.GetValues(typeof(SkillSystem.EffectType)) as SkillSystem.EffectType[];

        var skill = new AdvancedSkillData
        {
            skillId = UnityEngine.Random.Range(90000, 99999),
            skillName = $"Random_Skill_{UnityEngine.Random.Range(1000, 9999)}",
            description = "Randomly generated test skill",
            category = UnityEngine.Random.value > 0.5f ?
                SkillSystem.SkillCategory.Active : SkillSystem.SkillCategory.Passive,
            tier = UnityEngine.Random.Range(1, 6),
            effects = new List<AdvancedSkillEffect>()
        };

        // 1-3개의 랜덤 효과 추가
        int effectCount = UnityEngine.Random.Range(1, 4);
        for (int i = 0; i < effectCount; i++)
        {
            var randomEffect = effectTypes[UnityEngine.Random.Range(0, effectTypes.Length)];
            var effect = new AdvancedSkillEffect
            {
                name = $"Effect_{i}",
                type = randomEffect,
                value = UnityEngine.Random.Range(50f, 200f),
                duration = UnityEngine.Random.Range(0, 6),
                chance = UnityEngine.Random.Range(0.5f, 1f),
                targetType = GetRandomTargetType()
            };

            // 타입별 추가 설정
            ConfigureRandomEffect(effect, randomEffect);

            skill.effects.Add(effect);
        }

        return skill;
    }

    /// <summary>
    /// 랜덤 효과 설정
    /// </summary>
    private void ConfigureRandomEffect(AdvancedSkillEffect effect, SkillSystem.EffectType type)
    {
        switch (type)
        {
            case SkillSystem.EffectType.Buff:
            case SkillSystem.EffectType.Debuff:
                var statTypes = Enum.GetValues(typeof(SkillSystem.StatType)) as SkillSystem.StatType[];
                effect.statType = statTypes[UnityEngine.Random.Range(0, statTypes.Length)];
                break;

            case SkillSystem.EffectType.StatusEffect:
                var statusTypes = Enum.GetValues(typeof(SkillSystem.StatusType)) as SkillSystem.StatusType[];
                effect.statusType = statusTypes[UnityEngine.Random.Range(0, statusTypes.Length)];
                break;

            case SkillSystem.EffectType.Damage:
            case SkillSystem.EffectType.TrueDamage:
                effect.damageType = UnityEngine.Random.value > 0.5f ?
                    SkillSystem.DamageType.Physical : SkillSystem.DamageType.Magical;
                effect.canCrit = UnityEngine.Random.value > 0.3f;
                break;
        }
    }
    #endregion

    #region Target Management
    /// <summary>
    /// 테스트 타겟 가져오기
    /// </summary>
    private List<BattleActor> GetTestTargets()
    {
        var targets = new List<BattleActor>();

        switch (targetMode)
        {
            case TargetMode.Single:
                if (testTarget != null)
                    targets.Add(testTarget);
                break;

            case TargetMode.AllEnemies:
                targets.AddRange(GetEnemyActors());
                break;

            case TargetMode.AllAllies:
                targets.AddRange(GetAllyActors());
                break;

            case TargetMode.Self:
                if (testSender != null)
                    targets.Add(testSender);
                break;

            case TargetMode.Random:
                var allActors = GetAllActors();
                if (allActors.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, allActors.Count);
                    targets.Add(allActors[randomIndex]);
                }
                break;

            case TargetMode.FromSkillData:
                if (testTarget != null)
                    targets.Add(testTarget);
                targets.AddRange(additionalTargets);
                break;
        }

        return targets.Where(t => t != null).ToList();
    }

    /// <summary>
    /// 타겟 타입 변환
    /// </summary>
    private SkillSystem.TargetType GetTargetTypeFromMode()
    {
        return targetMode switch
        {
            TargetMode.Single => SkillSystem.TargetType.EnemySingle,
            TargetMode.AllEnemies => SkillSystem.TargetType.EnemyAll,
            TargetMode.AllAllies => SkillSystem.TargetType.AllyAll,
            TargetMode.Self => SkillSystem.TargetType.Self,
            TargetMode.Random => SkillSystem.TargetType.Random,
            _ => SkillSystem.TargetType.EnemySingle
        };
    }

    /// <summary>
    /// 랜덤 타겟 타입
    /// </summary>
    private SkillSystem.TargetType GetRandomTargetType()
    {
        var targetTypes = Enum.GetValues(typeof(SkillSystem.TargetType)) as SkillSystem.TargetType[];
        return targetTypes[UnityEngine.Random.Range(0, targetTypes.Length)];
    }

    /// <summary>
    /// 모든 액터 가져오기
    /// </summary>
    private List<BattleActor> GetAllActors()
    {
        var actors = new List<BattleActor>();

        if (testSender != null) actors.Add(testSender);
        if (testTarget != null) actors.Add(testTarget);
        actors.AddRange(additionalTargets.Where(a => a != null));

        return actors;
    }

    /// <summary>
    /// 적 액터들 가져오기
    /// </summary>
    private List<BattleActor> GetEnemyActors()
    {
        var actors = new List<BattleActor>();
        if (testTarget != null) actors.Add(testTarget);
        actors.AddRange(additionalTargets.Where(a => a != null && a != testSender));
        return actors;
    }

    /// <summary>
    /// 아군 액터들 가져오기
    /// </summary>
    private List<BattleActor> GetAllyActors()
    {
        var actors = new List<BattleActor>();
        if (testSender != null) actors.Add(testSender);
        return actors;
    }

    /// <summary>
    /// 씬에서 액터 자동 찾기
    /// </summary>
    private void FindActorsInScene()
    {
        

        List<BattleActor> allyActors = BattleProcessManagerNew.Instance.GetAllyActors();

        if (testSender == null && allyActors.Count > 0)
        {
            testSender = allyActors[0];
            LogStatus($"Auto-assigned sender: {testSender.name}");
        }

        List<BattleActor> enemyActors = BattleProcessManagerNew.Instance.GetEnemyActors();

        if (testTarget == null && enemyActors.Count > 0)
        {
            testTarget = enemyActors[0];
            LogStatus($"Auto-assigned target: {testTarget.name}");
        }

        if (additionalTargets.Count == 0 && enemyActors.Count > 1)
        {
            for (int i = 2; i < Mathf.Min(enemyActors.Count, 5); i++)
            {
                additionalTargets.Add(enemyActors[i]);
            }
            LogStatus($"Auto-assigned {additionalTargets.Count} additional targets");
        }
    }
    #endregion

    #region Validation
    /// <summary>
    /// 설정 검증
    /// </summary>
    private bool ValidateSetup()
    {
        bool isValid = true;

        if (testSender == null)
        {
            LogError("Test sender is not assigned!");
            isValid = false;
        }
        else if (testSender.SkillManager == null)
        {
            LogWarning("Test sender has no SkillManager - initializing...");
            testSender.InitializeSkillSystem();
        }

        if (targetMode != TargetMode.Self && testTarget == null && additionalTargets.Count == 0)
        {
            LogError("No targets assigned!");
            isValid = false;
        }

        if (testMode == TestMode.SkillData && testSkillData == null)
        {
            LogError("Test skill data is not assigned!");
            isValid = false;
        }

        if (testMode == TestMode.LoadFromDatabase && skillDatabase == null)
        {
            LogError("Skill database is not assigned!");
            isValid = false;
        }

        return isValid;
    }

    /// <summary>
    /// 액터 유효성 검증
    /// </summary>
    private bool ValidateActors()
    {
        if (testSender == null)
        {
            LogError("Sender is null!");
            return false;
        }

        if (testTarget == null && targetMode != TargetMode.Self)
        {
            LogError("Target is null!");
            return false;
        }

        return true;
    }
    #endregion

    #region Debug & Status Display
    /// <summary>
    /// 스킬 상태 출력
    /// </summary>
    private void PrintSkillStatus()
    {
        Debug.Log("\n╔══════════════════════════════════════╗");
        Debug.Log("║         ACTIVE SKILLS STATUS         ║");
        Debug.Log("╚══════════════════════════════════════╝");

        // Sender 상태
        if (testSender != null && testSender.SkillManager != null)
        {
            Debug.Log($"\n▶ {testSender.name} (Sender)");
            PrintActorSkills(testSender);
        }

        // Target 상태
        if (testTarget != null && testTarget.SkillManager != null)
        {
            Debug.Log($"\n▶ {testTarget.name} (Target)");
            PrintActorSkills(testTarget);
        }

        // 추가 타겟들
        foreach (var actor in additionalTargets)
        {
            if (actor != null && actor.SkillManager != null)
            {
                Debug.Log($"\n▶ {actor.name} (Additional)");
                PrintActorSkills(actor);
            }
        }
    }

    /// <summary>
    /// 액터별 스킬 출력
    /// </summary>
    private void PrintActorSkills(BattleActor actor)
    {
        var skills = actor.SkillManager.GetAllActiveSkills();

        if (skills.Count == 0)
        {
            Debug.Log("  No active skills");
            return;
        }

        foreach (var skill in skills)
        {
            Debug.Log($"  • {skill.SkillName}");
            Debug.Log($"    - ID: {skill.SkillID}");
            Debug.Log($"    - Remaining Turns: {skill.RemainTurn}");
            Debug.Log($"    - Stacks: {skill.CurrentStacks}");

            if (verboseLogging && skill.SkillData != null)
            {
                foreach (var effect in skill.SkillData.effects)
                {
                    Debug.Log($"    - Effect: {effect.type} ({effect.value})");
                }
            }
        }
    }

    /// <summary>
    /// 스킬 상세 정보 출력
    /// </summary>
    private void LogSkillDetails(AdvancedSkillData skill)
    {
        LogVerbose($"\n  === Skill Details ===");
        LogVerbose($"  Name: {skill.skillName}");
        LogVerbose($"  ID: {skill.skillId}");
        LogVerbose($"  Description: {skill.description}");
        LogVerbose($"  Category: {skill.category}");
        LogVerbose($"  Class: {skill.characterClass}");
        LogVerbose($"  Tier: {skill.tier}");
        LogVerbose($"  Cooldown: {skill.cooldown}");

        LogVerbose($"\n  Effects ({skill.effects.Count}):");
        foreach (var effect in skill.effects)
        {
            LogVerbose($"    • {effect.name ?? effect.type.ToString()}");
            LogVerbose($"      Type: {effect.type}");
            LogVerbose($"      Value: {effect.value}");
            LogVerbose($"      Duration: {effect.duration}");
            LogVerbose($"      Target: {effect.targetType}");
            LogVerbose($"      Chance: {effect.chance:P}");
        }
    }

    /// <summary>
    /// 액터 상태 출력 (전)
    /// </summary>
    private void LogActorStatsBefore()
    {
        LogDetailed("\n  [Before Execution]");
        LogActorStats(testSender, "Sender");
        LogActorStats(testTarget, "Target");
    }

    /// <summary>
    /// 액터 상태 출력 (후)
    /// </summary>
    private void LogActorStatsAfter()
    {
        LogDetailed("\n  [After Execution]");
        LogActorStats(testSender, "Sender");
        LogActorStats(testTarget, "Target");
    }

    /// <summary>
    /// 액터 상태 출력
    /// </summary>
    private void LogActorStats(BattleActor actor, string label)
    {
        if (actor == null || actor.BattleActorInfo == null) return;

        var info = actor.BattleActorInfo;
        LogDetailed($"    {label}: HP {info.Hp}/{info.MaxHp}, " +
                   $"ATK {info.Atk}, DEF {info.Def}, SPD {info.TurnSpeed}");
    }

    /// <summary>
    /// 성능 메트릭 출력
    /// </summary>
    private void PrintPerformanceMetrics()
    {
        Debug.Log("\n╔══════════════════════════════════════╗");
        Debug.Log("║       PERFORMANCE METRICS            ║");
        Debug.Log("╚══════════════════════════════════════╝");

        foreach (var metric in performanceMetrics.OrderBy(m => m.Key))
        {
            Debug.Log($"  {metric.Key}: {metric.Value:F3}s (avg)");
        }

        if (performanceMetrics.Count > 0)
        {
            var total = performanceMetrics.Values.Sum();
            var avg = performanceMetrics.Values.Average();
            Debug.Log($"  ──────────────────────");
            Debug.Log($"  Total: {total:F3}s");
            Debug.Log($"  Average: {avg:F3}s");
        }
    }
    #endregion

    #region Logging
    private void LogStatus(string message)
    {
        if (logLevel >= LogLevel.Basic)
        {
            Debug.Log($"<color=cyan>[SkillTest]</color> {message}");
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }

    private void LogDetailed(string message)
    {
        if (logLevel >= LogLevel.Detailed)
        {
            Debug.Log($"<color=gray>[SkillTest:Detail]</color> {message}");
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] [Detail] {message}");
        }
    }

    private void LogVerbose(string message)
    {
        if (logLevel >= LogLevel.Verbose)
        {
            Debug.Log($"<color=darkgray>[SkillTest:Verbose]</color> {message}");
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] [Verbose] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (logLevel >= LogLevel.Basic)
        {
            Debug.LogWarning($"[SkillTest] {message}");
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] [WARNING] {message}");
        }
    }

    private void LogError(string message)
    {
        if (logLevel >= LogLevel.ErrorOnly)
        {
            Debug.LogError($"[SkillTest] {message}");
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] {message}");
        }
    }

    private void RecordTestHistory(string skillName)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        testHistory.Add($"{timestamp}: {skillName}");

        if (testHistory.Count > 50)
        {
            testHistory.RemoveAt(0);
        }
    }

    private void RecordPerformance(string skillName, float elapsed)
    {
        if (!performanceMetrics.ContainsKey(skillName))
        {
            performanceMetrics[skillName] = elapsed;
        }
        else
        {
            performanceMetrics[skillName] = (performanceMetrics[skillName] + elapsed) / 2f;
        }
    }

    private void SaveLogToFile()
    {
#if UNITY_EDITOR
        try
        {
            System.IO.File.WriteAllText(logFilePath, logBuilder.ToString());
            Debug.Log($"Log saved to: {logFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save log: {ex.Message}");
        }
#endif
    }
    #endregion

    #region Editor GUI
#if UNITY_EDITOR
    [CustomEditor(typeof(BattleSkillTestSupport))]
    public class BattleSkillTestSupportEditor : Editor
    {
        private bool showQuickActions = true;
        private bool showTestHistory = false;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var support = target as BattleSkillTestSupport;

            EditorGUILayout.Space(10);

            // Quick Actions
            showQuickActions = EditorGUILayout.Foldout(showQuickActions, "Quick Actions", true);
            if (showQuickActions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("▶ Execute Test", GUILayout.Height(30)))
                {
                    support.ExecuteTest();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Clear Skills"))
                {
                    support.ClearAllSkills();
                }
                if (GUILayout.Button("Reset Actors"))
                {
                    support.ResetActorStates();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Random Skill"))
                {
                    support.TestRandomSkill();
                }
                if (GUILayout.Button("Remove Buffs"))
                {
                    support.RemoveAllBuffs();
                }
                if (GUILayout.Button("Remove Debuffs"))
                {
                    support.RemoveAllDebuffs();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            // Test History
            if (support.testHistory != null && support.testHistory.Count > 0)
            {
                EditorGUILayout.Space(5);
                showTestHistory = EditorGUILayout.Foldout(showTestHistory, $"Test History ({support.testHistory.Count})", true);

                if (showTestHistory)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    int startIndex = Mathf.Max(0, support.testHistory.Count - 10);
                    for (int i = startIndex; i < support.testHistory.Count; i++)
                    {
                        EditorGUILayout.LabelField(support.testHistory[i], EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
#endif
    #endregion
}

/// <summary>
/// BattleActor 확장 메서드 (테스트용)
/// </summary>
public static class BattleActorTestExtensions
{
    /// <summary>
    /// 스킬 시스템 초기화
    /// </summary>
    public static void InitializeSkillSystem(this BattleActor actor)
    {
        // Reflection을 사용하여 private 메서드 호출
        var method = typeof(BattleActor).GetMethod("InitializeSkillSystem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(actor, null);
        }
    }

    /// <summary>
    /// 애니메이션 설정
    /// </summary>
    public static void SetAnimation(this BattleActor actor, BattleActorAnimation animation)
    {
        // 실제 애니메이션 시스템과 연동
        // actor.Animator?.SetTrigger(animation.ToString());
    }

    /// <summary>
    /// 스킬 시각 효과 재생
    /// </summary>
    public static void PlaySkillVisualEffect(this BattleActor actor, AdvancedSkillData skillData)
    {
        // 실제 이펙트 시스템과 연동
        Debug.Log($"[Visual] Playing skill effect: {skillData.skillName}");
    }
}
#endif
#pragma warning restore CS1998