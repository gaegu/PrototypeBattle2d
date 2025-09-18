using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using BattleAI;

/// <summary>
/// BattleProcessManagerNew와 AI 시스템 통합
/// </summary>
public class BattleAIIntegration : MonoBehaviour
{
    public enum AIRole
    {
        Tank,
        DamageDealer,
        Support,
        Balanced
    }


    [Header("Auto Battle Settings")]
    [SerializeField] private bool isAutoBattleEnabled = false;
    [SerializeField] private bool allyAutoEnabled = false;
    [SerializeField] private bool enemyAutoEnabled = true;

    [Header("AI Difficulty Override")]
    [SerializeField] private bool overrideEnemyAI = false;
    [SerializeField] private AIIntelligenceLevel enemyAILevel = AIIntelligenceLevel.Medium;

    [Header("Debug")]
    [SerializeField] private bool showAIDecisions = true;
    [SerializeField] private bool logDetailedScores = false;

    private BattleAIManager aiManager;
    private BattleProcessManagerNew battleManager;

    // AI 결정 캐시
    private Dictionary<BattleActor, AIDecision> pendingDecisions = new Dictionary<BattleActor, AIDecision>();

    // 즉시 실행 플래그
    private bool forceAutoExecute = false;
    private AIDecision immediateDecision = null;

    private void Awake()
    {
        aiManager = BattleAIManager.Instance;
        battleManager = GetComponent<BattleProcessManagerNew>();
    }

    /// <summary>
    /// Auto Battle 토글
    /// </summary>
    public void ToggleAutoBattle(bool enable)
    {
        bool wasDisabled = !isAutoBattleEnabled;
        isAutoBattleEnabled = enable;

        if (enable)
        {
            Debug.Log("[AI] Auto Battle Enabled");

            // 현재 대기 중인 경우 즉시 AI 실행
            if (wasDisabled && battleManager.IsWaitingForCommand())
            {
                var currentActor = battleManager.GetCurrentTurnActor();
                if (currentActor != null && !currentActor.IsDead)
                {
                    // 현재 상태가 CommandSelect인지 확인
                    var stateMachine = battleManager.GetStateMachine();
                    if (stateMachine != null && stateMachine.CurrentState == BattleState.CommandSelect)
                    {
                        Debug.Log("[AI] Currently in CommandSelect state - executing immediate AI");
                        ExecuteImmediateAI();
                    }
                }
            }
        }
        else
        {
            Debug.Log("[AI] Auto Battle Disabled");
            pendingDecisions.Clear();
            immediateDecision = null;
            forceAutoExecute = false;
        }
    }

    /// <summary>
    /// 아군 Auto 토글
    /// </summary>
    public void ToggleAllyAuto(bool enable)
    {
        bool wasDisabled = !allyAutoEnabled;
        allyAutoEnabled = enable;

        // 현재 아군 턴이고 대기 중이면 즉시 실행
        if (enable && wasDisabled && battleManager.IsWaitingForCommand())
        {
            var currentActor = battleManager.GetCurrentTurnActor();
            if (currentActor != null && currentActor.BattleActorInfo.IsAlly)
            {
                var stateMachine = battleManager.GetStateMachine();
                if (stateMachine != null && stateMachine.CurrentState == BattleState.CommandSelect)
                {
                    ExecuteImmediateAI();
                }
            }
        }
    }

    /// <summary>
    /// 즉시 AI 실행 (수정된 버전)
    /// </summary>
    private async void ExecuteImmediateAI()
    {
        Debug.Log("[AI] Executing immediate AI decision...");

        var currentActor = battleManager.GetCurrentTurnActor();
        if (currentActor == null || currentActor.IsDead)
        {
            Debug.LogWarning("[AI] No valid actor for immediate AI");
            return;
        }

        // AI 결정 생성
        var enemies = currentActor.BattleActorInfo.IsAlly ?
            battleManager.GetEnemyActors() :
            battleManager.GetAllyActors();

        var allies = currentActor.BattleActorInfo.IsAlly ?
            battleManager.GetAllyActors() :
            battleManager.GetEnemyActors();

        // 생존한 캐릭터만 필터링
        enemies = enemies.Where(e => !e.IsDead).ToList();
        allies = allies.Where(a => !a.IsDead).ToList();

        try
        {
            // AI 결정 요청
            immediateDecision = await aiManager.MakeDecision(currentActor, enemies, allies);

            if (immediateDecision == null)
            {
                Debug.LogWarning("[AI] No decision made, defaulting to attack");
                immediateDecision = new AIDecision
                {
                    ActionType = "attack",
                    Target = enemies.FirstOrDefault(),
                    BPUse = 0,
                    SkillId = 0
                };
            }

            // 타겟이 없으면 첫 번째 적 선택
            if (immediateDecision.Target == null)
            {
                immediateDecision.Target = enemies.FirstOrDefault();
                if (immediateDecision.Target == null)
                {
                    Debug.LogError("[AI] No valid targets!");
                    return;
                }
            }

            // 플래그 설정
            forceAutoExecute = true;


            // ForceCommand 호출하여 즉시 명령 설정
            string command = immediateDecision.ActionType.Replace("bp_", "");
            battleManager.ForceCommand(
                command,
                immediateDecision.Target,
                immediateDecision.SkillId
            );

            // BP 설정
            if (immediateDecision.BPUse > 0)
            {
                battleManager.PendingBPUse = immediateDecision.BPUse;
            }


            Debug.Log($"[AI] Immediate decision prepared: {immediateDecision.ActionType} -> {immediateDecision.Target.name}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AI] Error in immediate AI execution: {e.Message}");
            forceAutoExecute = false;
            immediateDecision = null;
        }
    }

    /// <summary>
    /// AI가 행동을 결정해야 하는지 확인
    /// </summary>
    public bool ShouldAIControl(BattleActor actor)
    {
        if (!isAutoBattleEnabled)
            return false;

        bool isAlly = actor.BattleActorInfo.IsAlly;

        // 아군인 경우
        if (isAlly)
        {
            return allyAutoEnabled;
        }
        // 적인 경우
        else
        {
            return enemyAutoEnabled;
        }
    }

    /// <summary>
    /// 즉시 실행 체크
    /// </summary>
    public bool HasImmediateDecision()
    {
        return forceAutoExecute && immediateDecision != null;
    }

    /// <summary>
    /// 즉시 실행 결정 가져오기
    /// </summary>
    public AIDecision GetImmediateDecision()
    {
        var decision = immediateDecision;
        immediateDecision = null;
        forceAutoExecute = false;
        return decision;
    }

    /// <summary>
    /// AI Decision을 Command로 변환
    /// </summary>
    private string ConvertAIDecisionToCommand(AIDecision decision)
    {
        if (decision.BPUse > 0)
        {
            return $"{decision.ActionType.Replace("bp_", "")}_bp_{decision.BPUse}";
        }
        return decision.ActionType.Replace("bp_", "");
    }

    /// <summary>
    /// AI 행동 결정 요청 (BattleProcessManagerNew에서 호출)
    /// </summary>
    public async UniTask<TurnInfo> GetAIDecision(
        TurnInfo turnInfo,
        CancellationToken token = default)
    {
        var actor = turnInfo.Actor;

        if (!ShouldAIControl(actor))
        {
            // AI 제어가 아니면 수동 입력 대기
            return await WaitForPlayerInput(turnInfo, token);
        }

        // 적과 아군 목록 가져오기
        var enemies = turnInfo.IsAllyTurn ?
            battleManager.GetEnemyActors() :
            battleManager.GetAllyActors();

        var allies = turnInfo.IsAllyTurn ?
            battleManager.GetAllyActors() :
            battleManager.GetEnemyActors();

        // 생존한 캐릭터만 필터링
        enemies = enemies.Where(e => !e.IsDead).ToList();
        allies = allies.Where(a => !a.IsDead).ToList();

        // AI 난이도 오버라이드 처리
        if (overrideEnemyAI && !actor.BattleActorInfo.IsAlly)
        {
            SetTemporaryAILevel(actor, enemyAILevel);
        }

        // AI 결정 요청
        var decision = await aiManager.MakeDecision(actor, enemies, allies, token);

        // 결정을 TurnInfo로 변환
        turnInfo = ApplyAIDecision(turnInfo, decision);

        // 시각적 피드백
        if (showAIDecisions)
        {
            ShowAIDecisionUI(actor, decision);
        }

        return turnInfo;
    }

    /// <summary>
    /// AI 결정을 TurnInfo에 적용
    /// </summary>
    private TurnInfo ApplyAIDecision(TurnInfo turnInfo, AIDecision decision)
    {
        if (decision == null)
        {
            Debug.LogWarning("[AI] Null decision, using default attack");
            decision = new AIDecision
            {
                ActionType = "attack",
                Target = null,
                BPUse = 0,
                SkillId = 0
            };
        }

        turnInfo.Target = decision.Target;
        turnInfo.Command = decision.ActionType switch
        {
            "attack" => "attack",
            "bp_attack" => "attack",  // BP 사용은 별도 처리
            "skill" => "skill",
            "bp_skill" => "skill",
            _ => "attack"
        };

        // BP 사용 설정
        if (decision.ActionType.Contains("bp_"))
        {
            // BattleProcessManagerNew의 pendingBPUse 설정
            // battleManager.SetPendingBP(decision.BPUse);

            battleManager.PendingBPUse = decision.BPUse;

            turnInfo.Actor.BattleActorInfo.SetBPThisTurn(decision.BPUse);
        }

        // 스킬 ID 설정
        if (decision.ActionType.Contains("skill"))
        {
            // Context에 스킬 ID 저장
            StoreSkillDecision(turnInfo.Actor, decision.SkillId);

            // BattleProcessManagerNew에 스킬 ID 전달 (메서드가 있다면)
            if (decision.SkillId > 0)
            {
                // battleManager.SetSelectedSkillId(decision.SkillId);
                Debug.Log($"[AI] Would set skill ID: {decision.SkillId}");
            }
        }

        return turnInfo;
    }

    /// <summary>
    /// 수동 입력 대기 (AI 미사용시)
    /// </summary>
    private async UniTask<TurnInfo> WaitForPlayerInput(TurnInfo turnInfo, CancellationToken token)
    {
        // 기존 UI 입력 대기 로직
        // BattleUIManager 등과 연동
        await UniTask.Yield(token);
        return turnInfo;
    }

    /// <summary>
    /// AI 레벨 임시 설정
    /// </summary>
    private void SetTemporaryAILevel(BattleActor actor, AIIntelligenceLevel level)
    {
        if (actor.BattleActorInfo.AIProfile == null)
        {
            actor.BattleActorInfo.AIProfile = new CharacterAIProfile();
        }

        actor.BattleActorInfo.AIProfile.intelligenceLevel = level;
    }

    /// <summary>
    /// 스킬 결정 저장
    /// </summary>
    private void StoreSkillDecision(BattleActor actor, int skillId)
    {
        // 실제 구현에서는 스킬 매니저와 연동
        Debug.Log($"[AI] {actor.name} will use skill ID: {skillId}");
    }

    /// <summary>
    /// AI 결정 UI 표시
    /// </summary>
    private void ShowAIDecisionUI(BattleActor actor, AIDecision decision)
    {
        if (decision == null) return;

        // UI 매니저와 연동하여 표시
        string message = $"{actor.name}: {decision.ActionType}";

        if (decision.Target != null)
        {
            message += $" → {decision.Target.name}";
        }

        if (decision.BPUse > 0)
        {
            message += $" (BP x{decision.BPUse})";
        }

        if (logDetailedScores)
        {
            message += $" [Score: {decision.Score:F2}]";
            message += $" [{decision.Reasoning}]";
        }

        Debug.Log($"[AI Decision] {message}");

        // 실제 UI에 표시
        // BattleUIManager.Instance?.ShowAIDecision(message);
    }

    #region Helper Methods for BattleProcessManagerNew

    /// <summary>
    /// 전투 시작 시 AI 프로필 초기화
    /// </summary>
    public void InitializeAIProfiles()
    {
        // 모든 캐릭터의 AI 프로필 확인 및 초기화
        var allActors = battleManager.GetAllyActors()
            .Concat(battleManager.GetEnemyActors())
            .ToList();

        foreach (var actor in allActors)
        {
            if (actor == null) continue;

            if (actor.BattleActorInfo.AIProfile == null)
            {
                // 기본 AI 프로필 생성
                actor.BattleActorInfo.AIProfile = CreateDefaultAIProfile(actor);
            }
        }

        Debug.Log($"[AI] Initialized AI profiles for {allActors.Count} actors");
    }

    /// <summary>
    /// 기본 AI 프로필 생성 - CharacterAIProfile 실제 변수 사용
    /// </summary>
    private CharacterAIProfile CreateDefaultAIProfile(BattleActor actor)
    {
        var profile = new CharacterAIProfile();

        // 캐릭터 타입에 따른 기본 설정
        string characterType = actor.BattleActorInfo.CharacterType;

        switch (characterType)
        {
            case "Tank":
                profile.personality = AIPersonality.Defensive;
                profile.aggressiveness = 0.3f;
                profile.skillPreference = 0.4f;
                profile.teamworkFocus = 0.5f;
                profile.resourceConservation = 0.6f;
                break;

            case "DPS":
                profile.personality = AIPersonality.Aggressive;
                profile.aggressiveness = 0.8f;
                profile.skillPreference = 0.6f;
                profile.teamworkFocus = 0.3f;
                profile.resourceConservation = 0.4f;
                profile.prioritizeBackline = true;
                profile.focusLowestHP = false;
                break;

            case "Healer":
                profile.personality = AIPersonality.Supportive;
                profile.aggressiveness = 0.2f;
                profile.skillPreference = 0.8f;
                profile.teamworkFocus = 0.9f;
                profile.resourceConservation = 0.7f;
                break;

            case "Support":
                profile.personality = AIPersonality.Tactical;
                profile.aggressiveness = 0.4f;
                profile.skillPreference = 0.7f;
                profile.teamworkFocus = 0.8f;
                profile.resourceConservation = 0.5f;
                break;

            default:
                profile.personality = AIPersonality.Balanced;
                profile.aggressiveness = 0.5f;
                profile.skillPreference = 0.5f;
                profile.teamworkFocus = 0.5f;
                profile.resourceConservation = 0.5f;
                break;
        }

        // 추가 특수 행동 설정
        profile.chainAttackMaster = false;  // 연계 공격 특화
        profile.bpThreshold = 2;           // BP 사용 임계값
        profile.saveBPForKill = true;      // 킬 확정용 BP 보존
        //profile.preferAoE = false;          // 광역 스킬 선호

        // 캐릭터별 특수 설정
      //  profile.role = GetAIRoleFromCharacterType(characterType);

        // 아군/적에 따른 기본 지능 레벨
        if (actor.BattleActorInfo.IsAlly)
        {
            profile.intelligenceLevel = AIIntelligenceLevel.Medium;
        }
        else
        {
            // 적은 전투 난이도에 따라 설정
            profile.intelligenceLevel = GetEnemyAILevelByDifficulty();
        }

        return profile;
    }

    /// <summary>
    /// 캐릭터 타입에서 AI 역할 추출
    /// </summary>
    private AIRole GetAIRoleFromCharacterType(string characterType)
    {
        switch (characterType)
        {
            case "Tank":
                return AIRole.Tank;
            case "DPS":
                return AIRole.DamageDealer;
            case "Healer":
            case "Support":
                return AIRole.Support;
            default:
                return AIRole.Balanced;
        }
    }

    /// <summary>
    /// 난이도별 적 AI 레벨
    /// </summary>
    private AIIntelligenceLevel GetEnemyAILevelByDifficulty()
    {
        // 실제로는 게임 난이도 설정과 연동
        // 임시로 Medium 반환
        return AIIntelligenceLevel.Medium;
    }

    #endregion
}

/// <summary>
/// BattleProcessManagerNew 확장 - AI 통합
/// </summary>
public static class BattleProcessManagerAIExtension
{
    private static BattleAIIntegration aiIntegration;

    /// <summary>
    /// AI Integration 컴포넌트 가져오기
    /// </summary>
    private static BattleAIIntegration GetAIIntegration(this BattleProcessManagerNew manager)
    {
        if (aiIntegration == null)
        {
            aiIntegration = manager.GetComponent<BattleAIIntegration>();
            if (aiIntegration == null)
            {
                aiIntegration = manager.gameObject.AddComponent<BattleAIIntegration>();
            }
        }
        return aiIntegration;
    }

    /// <summary>
    /// AI를 통한 커맨드 선택 (CommandSelect 상태에서 호출)
    /// </summary>
    public static async UniTask<TurnInfo> ProcessAICommand(
        this BattleProcessManagerNew manager,
        TurnInfo turnInfo,
        CancellationToken token = default)
    {
        var ai = manager.GetAIIntegration();
        return await ai.GetAIDecision(turnInfo, token);
    }

    /// <summary>
    /// Auto Battle 활성화
    /// </summary>
    public static void EnableAutoBattle(this BattleProcessManagerNew manager, bool enable)
    {
        var ai = manager.GetAIIntegration();
        ai.ToggleAutoBattle(enable);
    }

    /// <summary>
    /// 전투 시작 시 AI 초기화
    /// </summary>
    public static void InitializeAI(this BattleProcessManagerNew manager)
    {
        var ai = manager.GetAIIntegration();
        ai.InitializeAIProfiles();
    }

    /// <summary>
    /// BP 설정 (AI 전용)
    /// </summary>
    public static void SetPendingBP(this BattleProcessManagerNew manager, int bp)
    {
        // BattleProcessManagerNew.Effect.cs의 pendingBPUse 설정
        // 실제 구현에서는 public property로 노출 필요
        manager.PendingBPUse = bp;
        Debug.Log($"[AI] Setting pending BP: {bp}");
    }
}

/// <summary>
/// BattleCharInfoNew 확장 - AI Profile
/// </summary>
public partial class BattleCharInfoNew
{
    [Header("AI 설정")]
    [SerializeField] private CharacterAIProfile aiProfile;

    public CharacterAIProfile AIProfile
    {
        get
        {
            if (aiProfile == null)
                aiProfile = new CharacterAIProfile();
            return aiProfile;
        }
        set => aiProfile = value;
    }

    /// <summary>
    /// 캐릭터 타입 (역할)
    /// </summary>
    public string CharacterType
    {
        get
        {
            // 실제로는 캐릭터 데이터 테이블에서 가져옴
            // 임시 구현
            if (Attack > Defence * 2) return "DPS";
            if (Defence > Attack * 2) return "Tank";
            return "Support";
        }
    }

    // ===== HasBuff/HasDebuff 메서드 추가 (AI가 사용) =====
    public bool HasBuff()
    {
        // 실제 버프 체크 로직
        return false; // 임시
    }

    public bool HasDebuff()
    {
        // 실제 디버프 체크 로직
        return false; // 임시
    }
}