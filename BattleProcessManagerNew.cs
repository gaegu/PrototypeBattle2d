// BattleProcessManagerNew 수정 부분
using IronJade.Table.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;
using BattleAI;
using System.Threading.Tasks;

public partial class BattleProcessManagerNew : MonoBehaviour
{
    private static BattleProcessManagerNew instance;
    public static BattleProcessManagerNew Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<BattleProcessManagerNew>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject manager = new GameObject("BattleProcessManagerNew");
                    instance = manager.AddComponent<BattleProcessManagerNew>();
                }
            }
            return instance;
        }
    }


    [Header("===== 테스트용 데이터 ID =====")]
    [SerializeField] private int[] characterIds = new int[] { 1001, 1002, 1003, 0, 0 };
    [SerializeField] private string[] characterKeys = new string[] { "", "", "", "", "" };


    [SerializeField] private int monsterGroupId = 5001;


    [Header("전투 시스템")]
    public BattleObjectPoolSupport battleObjectPoolSupport;
    public BattlePositionNew battlePosition;

    [Header("캐릭터 정보")]
    protected BattleCharInfoNew[] AllyInfos;
    protected BattleCharInfoNew[] EnemyInfos;

    [Header("AI 정보")]
    public BattleAIIntegration aiIntegration;

    private int selectedSkillId = 0;          // 선택된 스킬 ID
    private BattleActor forcedTarget = null;  // 강제 설정된 타겟

    private int forcedSkillId = 0;
    private bool isForceCommandActive = false;

    /// <summary>
    /// 선택된 스킬 ID 설정
    /// </summary>
    public void SetSelectedSkillId(int skillId)
    {
        selectedSkillId = skillId;
        Debug.Log($"[Battle] Selected skill ID set to: {skillId}");
    }




    public bool IsWaitingForCommand()
    {
        return waitingForUICommand;
    }
    public BattleActor GetCurrentTurnActor()
    {
        return stateMachine?.CurrentTurn.Actor;
    }


    /// <summary>
    /// 강제 명령 설정 (Auto 즉시 실행용)
    /// </summary>
    public void ForceCommand(string command, BattleActor target = null, int skillId = 0)
    {
        if (waitingForUICommand)
        {
            selectedCommand = command;
            isForceCommandActive = true;

            forcedTarget = target;      // 타겟 저장
            forcedSkillId = skillId;    // 스킬 ID 저장


            Debug.Log($"[Battle] Forced command: {command}");
        }

    }


    // 전투 정보
    public BattleInfoNew BattleInfo { get; private set; } = new BattleInfoNew();

    // 캐릭터 리스트
    private List<BattleActor> allActors = new List<BattleActor>();
    private List<BattleActor> allyActors = new List<BattleActor>();
    private List<BattleActor> enemyActors = new List<BattleActor>();

    // 통합 상태 머신
    private UnifiedBattleStateMachine stateMachine;

    // 턴 관리
    private TurnOrderSystem.TurnOrderInfo currentTurnInfo;

    // UI 관련
    private const float COMMAND_WAIT_TIME = 30000000f;
    private const float AUTO_ATTACK_DELAY = 0.5f;
    private string selectedCommand = "";
    private bool waitingForUICommand = false;



    // 턴 순서 시스템 - public으로 변경
    public TurnOrderSystem turnOrderSystem { get; private set; }

    // 전투 종료 관련 플래그
    private bool isBattleEnded = false;
    private bool isRestarting = false;

    // 커맨드 히스토리
    private CommandHistory commandHistory = new CommandHistory();


    // 기존 필드에 추가
    [Header("진형 시스템")]
    [SerializeField] private FormationType initialAllyFormation = FormationType.OffensiveBalance;

    // Getter 메서드 추가 (BattlePositionNew에서 접근용)
    public List<BattleActor> GetAllyActors() => allyActors;
    public List<BattleActor> GetEnemyActors() => enemyActors;

    public List<BattleActor> GetAllBattleActors() => allActors;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // 상태 머신 초기화
        stateMachine = new UnifiedBattleStateMachine();
        stateMachine.OnStateChanged += OnBattleStateChanged;

        // AI Integration 컴포넌트 추가
        if (GetComponent<BattleAIIntegration>() == null)
        {
            gameObject.AddComponent<BattleAIIntegration>();
        }

        // AI 시스템 초기화
        this.InitializeAI();

        // 이벤트 구독
        SubscribeToEvents();

        // 턴 순서 시스템 초기화
        turnOrderSystem = new TurnOrderSystem();


    }

    private void OnDestroy()
    {
        stateMachine?.Dispose();
        UnsubscribeFromEvents();
    }

    private void OnEnable()
    {

        StartBattle().Forget();

    }

    // 이벤트 구독
    private void SubscribeToEvents()
    {
        var eventManager = BattleEventManager.Instance;
        eventManager.Subscribe(BattleEventType.AfterDamage, OnDamageDealt);
        eventManager.Subscribe(BattleEventType.CharacterDeath, OnCharacterDeath);
        eventManager.Subscribe(BattleEventType.CommandSelected, OnCommandSelectedEvent);
    }

    private void UnsubscribeFromEvents()
    {
        var eventManager = BattleEventManager.Instance;
        eventManager.Unsubscribe(BattleEventType.AfterDamage, OnDamageDealt);
        eventManager.Unsubscribe(BattleEventType.CharacterDeath, OnCharacterDeath);
        eventManager.Unsubscribe(BattleEventType.CommandSelected, OnCommandSelectedEvent);
    }

    // 전투 시작
    public async UniTaskVoid StartBattle()
    {
        try
        {

            var token = stateMachine.GetCancellationToken();

            // ✨ AddressableManager 초기화 대기 추가
            await WaitForAddressableManager(token);

            // 플래그 초기화
            isBattleEnded = false;
            isRestarting = false;


            // === AI 초기화 추가 (InitializeBattle 이후) ===
            if (aiIntegration != null)
            {
                aiIntegration.InitializeAIProfiles();
            }


            // 전투 시작 이벤트
            var battleStartData = new BattleFlowEventData(BattleEventType.BattleStart);
            BattleEventManager.Instance.TriggerEvent(BattleEventType.BattleStart, battleStartData);

            // 초기화
            stateMachine.TransitionTo(BattleState.Initialize);
            await ProcessState(BattleState.Initialize, token);

            stateMachine.TransitionTo(BattleState.BattleStart);
            await ProcessState(BattleState.BattleStart, token);


            // ========== 추가: START BATTLE UI 표시 (1.5초) ==========
            if (BattleUIManager.Instance != null)
            {
                Debug.Log("[Battle] Showing START BATTLE UI...");
                await BattleUIManager.Instance.ShowStartBattle();
            }
            // ======================================================


            // 턴 순서 시스템 초기화
            turnOrderSystem.Initialize(allyActors, enemyActors);

            // 초기 UI 업데이트
            UpdateBattleUI();
            UpdateTimelineUI();

            // 메인 전투 루프
            while (!isBattleEnded && !token.IsCancellationRequested)
            {
                // 매 턴마다 전투 종료 체크
                if (CheckAndHandleBattleEnd())
                {
                    break;
                }

                // 현재 라운드가 끝났는지 체크
                if (turnOrderSystem.IsRoundComplete())
                {
                    var roundEndData = new BattleFlowEventData(BattleEventType.RoundEnd)
                    {
                        TurnCount = turnOrderSystem.CurrentRound
                    };
                    BattleEventManager.Instance.TriggerEvent(BattleEventType.RoundEnd, roundEndData);
                    Debug.Log($"[Battle] Round {turnOrderSystem.CurrentRound} Complete!");
                }

                // 다음 턴 가져오기
                var turnInfo = turnOrderSystem.GetNextTurn();

                if (turnInfo.Actor == null || turnInfo.Actor.IsDead)
                {
                    if (!turnOrderSystem.HasAliveCharacters())
                    {
                        Debug.Log("[Battle] No alive characters remaining");
                        break;
                    }
                    continue;
                }

                // 현재 턴 정보 변환
                currentTurnInfo = GetCurrentTurnOrderInfo(turnInfo);

                // 새 라운드 시작 체크
                if (turnOrderSystem.GetRoundProgress().current == 1)
                {
                    var roundStartData = new BattleFlowEventData(BattleEventType.RoundStart)
                    {
                        TurnCount = turnOrderSystem.CurrentRound
                    };
                    BattleEventManager.Instance.TriggerEvent(BattleEventType.RoundStart, roundStartData);
                    UpdateBattleUI();
                    UpdateTimelineUI();
                }

                stateMachine.SetTurnInfo(turnInfo);



                // 턴 시작 이벤트
                var turnStartData = new TurnEventData(
                    BattleEventType.TurnStart,
                    turnInfo.Actor,
                    turnInfo.IsAllyTurn,
                    turnInfo.CharacterIndex
                );
                BattleEventManager.Instance.TriggerEvent(BattleEventType.TurnStart, turnStartData);

                UpdateBattleUI();
                UpdateTimelineUI();


                // UI 업데이트
                if (BattleUIManager.Instance != null)
                {
                    BattleUIManager.Instance.HighlightCurrentTurn(turnInfo.Actor);
                    if (turnOrderSystem.GetRoundProgress().current > 1)
                    {
                       // BattleUIManager.Instance.AnimateTurnTransition();
                    }
                }

                // 턴 처리
                stateMachine.TransitionTo(BattleState.TurnStart);
                await ProcessTurn(turnInfo, token);


                if (BattleEventManager.Instance != null)
                {
                    // 턴 종료 이벤트
                    var turnEndData = new TurnEventData(
                    BattleEventType.TurnEnd,
                    turnInfo.Actor,
                    turnInfo.IsAllyTurn,
                    turnInfo.CharacterIndex
                    );

                    BattleEventManager.Instance.TriggerEvent(BattleEventType.TurnEnd, turnEndData);
                }

                UpdateBattleUI();
                UpdateTimelineUI();

                if (token.IsCancellationRequested) break;
            }

            // 전투가 종료되지 않았다면 최종 체크
            if (!isBattleEnded)
            {
                CheckAndHandleBattleEnd();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Battle cancelled");
        }
    }


    // ✨ 새로운 헬퍼 메서드 추가
    private async UniTask WaitForAddressableManager(CancellationToken token)
    {
        // AddressableManager가 있고 초기화되지 않은 경우 대기
        if (GameCore.Addressables.AddressableManager.Instance != null)
        {
            int waitCount = 0;
            while (!GameCore.Addressables.AddressableManager.Instance.IsInitialized && waitCount < 100) // 최대 10초 대기
            {
                await UniTask.Delay(100, cancellationToken: token);
                waitCount++;

                if (waitCount % 10 == 0) // 1초마다 로그
                {
                    Debug.Log($"[Battle] Waiting for AddressableManager initialization... ({waitCount * 100}ms)");
                }
            }

            if (!GameCore.Addressables.AddressableManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[Battle] AddressableManager initialization timeout - proceeding anyway");
            }
            else
            {
                Debug.Log("[Battle] AddressableManager ready");
            }
        }
    }


    // 전투 종료 체크
    private bool CheckAndHandleBattleEnd()
    {
        if (isBattleEnded) return true;

        // 살아있는 캐릭터 확인
        var aliveAllies = allyActors.Where(a => a != null && !a.IsDead).ToList();
        var aliveEnemies = enemyActors.Where(e => e != null && !e.IsDead).ToList();

        bool allAlliesDead = aliveAllies.Count == 0;
        bool allEnemiesDead = aliveEnemies.Count == 0;

        if (allAlliesDead || allEnemiesDead)
        {
            isBattleEnded = true;
            bool isVictory = allEnemiesDead;

            Debug.Log($"[Battle End] Result: {(isVictory ? "Victory" : "Defeat")}");
            Debug.Log($"[Battle End] Alive Allies: {aliveAllies.Count}, Alive Enemies: {aliveEnemies.Count}");

            HandleBattleEnd(isVictory).Forget();
            return true;
        }

        return false;
    }

    // 전투 종료 처리
    private async UniTaskVoid HandleBattleEnd(bool isVictory)
    {
        stateMachine.TransitionTo(BattleState.BattleResult);


        // ========== 추가: Victory/Defeat UI 표시 ==========
        if (BattleUIManager.Instance != null)
        {
            _ = BattleUIManager.Instance.ShowBattleEnd(isVictory);
        }
        // ================================================




        var battleEndData = new BattleFlowEventData(BattleEventType.BattleEnd)
        {
            IsVictory = isVictory,
            TurnCount = turnOrderSystem?.CurrentTurn ?? 0,
            BattleTime = Time.time
        };
        BattleEventManager.Instance.TriggerEvent(BattleEventType.BattleEnd, battleEndData);

        // 승리/패배 애니메이션
        if (isVictory)
        {
            await PlayVictoryAnimation();
        }
        else
        {
            await PlayDefeatAnimation();
        }

        stateMachine.TransitionTo(BattleState.BattleEnd);
    }

    // 전투 재시작
    public void RestartBattle()
    {
        if (isRestarting) return;
        StartCoroutine(RestartBattleCoroutine());
    }

    private IEnumerator RestartBattleCoroutine()
    {
        isRestarting = true;
        Debug.Log("[Battle] Restarting battle...");

        // 현재 전투 정리
        CleanupCurrentBattle();
        yield return new WaitForSeconds(0.5f);

        // 액터 재생성
        yield return ResetBattleActors();

        // 시스템 리셋
        ResetBattleSystems();

        // 새 전투 시작
        isRestarting = false;
        StartBattle().Forget();
    }

    private void CleanupCurrentBattle()
    {
        // 상태 머신 리셋
        stateMachine?.Reset();

        // 턴 시스템 리셋
        turnOrderSystem = new TurnOrderSystem();

        // 이벤트 정리
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.ClearHistory();
        }

        // UI 정리
        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.HideBattleUI();
        }

        // 기존 액터 제거
        foreach (var actor in allyActors)
        {
            if (actor != null)
                Destroy(actor.gameObject);
        }
        allyActors.Clear();

        foreach (var actor in enemyActors)
        {
            if (actor != null)
                Destroy(actor.gameObject);
        }
        enemyActors.Clear();

        allActors.Clear();
    }

    private IEnumerator ResetBattleActors()
    {
        Debug.Log("[Battle] Resetting battle actors...");

        // 캐릭터 정보 재초기화
        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        GoodsGeneratorModel goodsGenerator = new GoodsGeneratorModel();

        // 아군 정보 초기화
        for (int i = 0; i < AllyInfos.Length; i++)
        {
            if (AllyInfos[i] != null)
            {
                var characterData = characterTable.GetDataByID((int)AllyInfos[i].CharacterDataEnum);
                if (!characterData.IsNull())
                {
                    var character = goodsGenerator.CreateBattleDummyCharacterByData(characterData, 1);
                    character.SetId(i);

                    AllyInfos[i].InitializeCharacterInfo(true, i, i, character, BattleInfo, 0);

                    // 속성 재설정
                    if (AllyInfos[i].Element == EBattleElementType.None)
                    {
                        EBattleElementType[] testElements = {
                            EBattleElementType.Power,
                            EBattleElementType.Plasma,
                            EBattleElementType.Bio,
                            EBattleElementType.Chemical
                        };
                        AllyInfos[i].SetElement(testElements[i % testElements.Length]);
                    }
                }
            }
        }

        // 적군 정보 초기화
        for (int i = 0; i < EnemyInfos.Length; i++)
        {
            if (EnemyInfos[i] != null)
            {
                var characterData = characterTable.GetDataByID((int)EnemyInfos[i].CharacterDataEnum);
                if (!characterData.IsNull())
                {
                    var character = goodsGenerator.CreateBattleDummyCharacterByData(characterData, 1);
                    character.SetId(i);

                    EnemyInfos[i].InitializeCharacterInfo(false, i, i, character, BattleInfo, 0);

                    // 속성 재설정
                    if (EnemyInfos[i].Element == EBattleElementType.None)
                    {
                        EBattleElementType[] testElements = {
                            EBattleElementType.Power,
                            EBattleElementType.Plasma,
                            EBattleElementType.Bio,
                            EBattleElementType.Chemical,
                            EBattleElementType.Electrical,
                            EBattleElementType.Network
                        };
                        EnemyInfos[i].SetElement(testElements[UnityEngine.Random.Range(0, testElements.Length)]);
                    }
                }
            }
        }

        // 액터 재생성
        yield return InitBattleActors().ToCoroutine();
    }

    private void ResetBattleSystems()
    {
        isBattleEnded = false;
        selectedCommand = "";
        waitingForUICommand = false;
        currentTurnInfo = null;
        commandHistory?.Clear();
        Debug.Log("[Battle] Battle systems reset complete");
    }


    private async UniTask ProcessTurn(TurnInfo turnInfo, CancellationToken token)
    {
        if (turnInfo.Actor == null || turnInfo.Actor.IsDead)
            return;

        // === 1. GuardianStone 브레이크 상태 체크 ===
        if (!turnInfo.Actor.CanAct())
        {
            string reason = "";

            // 상태 이유 확인
            if (turnInfo.Actor.BattleActorInfo != null &&
                turnInfo.Actor.BattleActorInfo.HasStatusFlag(StatusFlag.CannotAct))
            {
                // 어떤 상태이상인지 확인
                if (turnInfo.Actor.SkillManager != null)
                {
                    if (turnInfo.Actor.SkillManager.HasSkill(SkillSystem.StatusType.Stun))
                        reason = "STUNNED";
                    else if (turnInfo.Actor.SkillManager.HasSkill(SkillSystem.StatusType.Freeze))
                        reason = "FROZEN";
                    else if (turnInfo.Actor.SkillManager.HasSkill(SkillSystem.StatusType.Petrify))
                        reason = "PETRIFIED";
                    else
                        reason = "INCAPACITATED";
                }
            }
            else if (turnInfo.Actor.GuardianStone?.IsBreakState == true)
            {
                reason = "BREAK STATE";
            }

            Debug.Log($"[Turn] {turnInfo.Actor.name} cannot act ({reason}), skipping turn");

            // 턴 스킵 시각 효과
            if (BattleUIManager.Instance != null && !string.IsNullOrEmpty(reason))
            {
                // 상태 표시 UI (필요시 구현)
                // BattleUIManager.Instance.ShowStatusMessage(turnInfo.Actor, reason);
            }


            Debug.Log($"[Turn] {turnInfo.Actor.name} is in BREAK state, skipping turn");
            turnInfo.Actor.OnTurnEnd();
            await UniTask.Delay(500, cancellationToken: token);
            return;
        }

        // === 2. 턴 시작 처리 ===
        stateMachine.TransitionTo(BattleState.TurnStart);
        await ProcessState(BattleState.TurnStart, token);

        // BP 증가 처리
        ProcessTurnEnd(turnInfo.Actor);

        // MP 충전 처리 (Type1용 - 자신의 턴에만)
     //   turnInfo.Actor.BattleActorInfo.ChargeMPOnTurn();

        // 스킬 업데이트
        if (turnInfo.Actor.SkillManager != null)
        {
            turnInfo.Actor.SkillManager.OnTurnUpdate();
        }

        Debug.Log($"[Turn] {(turnInfo.IsAllyTurn ? "Ally" : "Enemy")} {turnInfo.Actor.name} - Index: {turnInfo.CharacterIndex}");

        // === 3. 캐릭터 이동 ===
        stateMachine.TransitionTo(BattleState.CharacterMove);
        await ProcessState(BattleState.CharacterMove, token);
        await MoveToAttackPosition(turnInfo, token);

        if (token.IsCancellationRequested) return;

        // === 4. AI 제어 여부 확인 ===
        var aiIntegration = GetComponent<BattleAIIntegration>();
        bool isAIControlled = false;

        if (aiIntegration != null)
        {
            isAIControlled = aiIntegration.ShouldAIControl(turnInfo.Actor);
            Debug.Log($"[Turn] {turnInfo.Actor.name} AI Control: {isAIControlled}");
        }

        // === 5. 커맨드 선택 및 타겟 설정 ===
        if (isAIControlled)
        {
            // AI 제어 모드
            await ProcessAIControlledTurn(turnInfo, aiIntegration, token);
        }
        else if (turnInfo.IsAllyTurn)
        {
            // 플레이어 수동 제어
            await ProcessPlayerControlledTurn(turnInfo, token);
        }
        else
        {
            // 적 기본 AI (Auto가 꺼진 경우)
            await ProcessDefaultEnemyTurn(turnInfo, token);
        }

        if (token.IsCancellationRequested) return;

        // === 6. 액션 실행 ===
        stateMachine.TransitionTo(BattleState.ActionExecute);
        await ProcessState(BattleState.ActionExecute, token);
        await ExecuteAction(turnInfo, token);

        // === 7. 캐릭터 복귀 ===
        if (turnInfo.Actor != null && !turnInfo.Actor.IsDead)
        {
            stateMachine.TransitionTo(BattleState.CharacterReturn);
            await ProcessState(BattleState.CharacterReturn, token);
            await ReturnToStartPosition(turnInfo, token);
        }

        // === 8. 턴 종료 ===
        stateMachine.TransitionTo(BattleState.TurnEnd);
        await ProcessState(BattleState.TurnEnd, token);

        if (turnInfo.Actor != null && turnInfo.Actor.SkillManager != null)
        {
            turnInfo.Actor.SkillManager.OnTurnUpdate();
        }

        // === 9. 승리/패배 체크 ===
        if (CheckBattleEnd())
        {
            stateMachine.TransitionTo(BattleState.BattleResult);
            await ProcessState(BattleState.BattleResult, token);
        }
    }

    /// <summary>
    /// AI 제어 턴 처리 (UI 없이 바로 실행)
    /// </summary>
    private async UniTask ProcessAIControlledTurn(TurnInfo turnInfo, BattleAIIntegration aiIntegration, CancellationToken token)
    {
        Debug.Log($"[AI Turn] Processing AI turn for {turnInfo.Actor.name}");

        // 타겟 리스트 준비
        var enemies = turnInfo.IsAllyTurn ?
            enemyActors.Where(e => !e.IsDead).ToList() :
            allyActors.Where(a => !a.IsDead).ToList();

        var allies = turnInfo.IsAllyTurn ?
            allyActors.Where(a => !a.IsDead).ToList() :
            enemyActors.Where(e => !e.IsDead).ToList();

        // AI 결정 요청
        var decision = await BattleAIManager.Instance.MakeDecision(
            turnInfo.Actor, enemies, allies, token);

        // 결정이 유효한지 확인
        if (decision == null)
        {
            Debug.LogWarning($"[AI Turn] No decision made, defaulting to attack");
            decision = new AIDecision
            {
                ActionType = "attack",
                Target = enemies.FirstOrDefault(),
                BPUse = 0,
                SkillId = 0
            };
        }

        // 타겟이 없으면 첫 번째 적 선택
        if (decision.Target == null)
        {
            decision.Target = enemies.FirstOrDefault();
            if (decision.Target == null)
            {
                Debug.LogError($"[AI Turn] No valid targets!");
                return;
            }
        }

        // === 커맨드 설정 ===
        string command = decision.ActionType.Replace("bp_", "");
        stateMachine.SetCommand(command);
        stateMachine.SetTarget(decision.Target);

        // === BP 처리 ===
        if (decision.BPUse > 0)
        {
            pendingBPUse = decision.BPUse;
            turnInfo.Actor.BattleActorInfo.SetBPThisTurn(decision.BPUse);
            Debug.Log($"[AI Turn] Using {decision.BPUse} BP");
        }

        // === 스킬 ID 처리 ===
        if (decision.SkillId > 0)
        {
            selectedSkillId = decision.SkillId;
            Debug.Log($"[AI Turn] Selected skill ID: {decision.SkillId}");
        }

        // UI 숨기기
        HideBattleUI();

        // 자연스러운 딜레이
        await UniTask.Delay(300, cancellationToken: token);

        Debug.Log($"[AI Turn] Decision: {command} -> {decision.Target.name}");
    }

    /// <summary>
    /// 플레이어 수동 제어 턴 처리
    /// </summary>
    private async UniTask ProcessPlayerControlledTurn(TurnInfo turnInfo, CancellationToken token)
    {
        Debug.Log($"[Player Turn] Processing manual turn for {turnInfo.Actor.name}");

        bool commandConfirmed = false;

        while (!commandConfirmed && !token.IsCancellationRequested)
        {
            // 커맨드 선택 상태
            stateMachine.TransitionTo(BattleState.CommandSelect);

            // === ForceCommand로 타겟이 이미 설정된 경우 수정 ===
            if (isForceCommandActive)
            {
                Debug.Log($"[Player Turn] Using forced command");

                // 스킬 ID 설정
                if (forcedSkillId > 0)
                {
                    selectedSkillId = forcedSkillId;
                }

                // 타겟 설정
                if (forcedTarget != null)
                {
                    stateMachine.SetTarget(forcedTarget);
                }

                commandConfirmed = true;

                // 타겟 선택 UI 취소
                if (BattleTargetSystem.Instance != null)
                {
                    BattleTargetSystem.Instance.CancelTargetSelection();
                }

                // 플래그 리셋
                isForceCommandActive = false;
                forcedTarget = null;
                forcedSkillId = 0;

                break;  // 타겟 선택 UI 건너뛰기
            }


            // 플레이어 입력 대기
            await WaitForCommand(turnInfo.Actor, token);

            stateMachine.SetCommand(selectedCommand);

            // 타겟이 필요한 커맨드인 경우
            if (!isForceCommandActive && (selectedCommand == "attack" || selectedCommand.StartsWith("skill")))
            {

                HideBattleUI();

                // 기본 타겟 설정
                BattleActor defaultTarget = GetDefaultTargetForAttacker(turnInfo.Actor, enemyActors);

                // 타겟 선택 UI
                if (BattleTargetSystem.Instance != null && defaultTarget != null)
                {
                    BattleActor selectedTarget = await BattleTargetSystem.Instance.StartTargetSelection(
                        enemyActors, defaultTarget, turnInfo.Actor);

                    if (selectedTarget == null)
                    {
                        // 타겟 선택 취소 - 커맨드 재선택
                        selectedCommand = "";
                        ShowBattleUI(turnInfo.Actor);
                        continue;
                    }

                    stateMachine.SetTarget(selectedTarget);
                    commandConfirmed = true;
                }
                else
                {
                    // 타겟 시스템이 없으면 기본 타겟 사용
                    stateMachine.SetTarget(defaultTarget);
                    commandConfirmed = true;
                }
            }
            else if (selectedCommand == "defend" || selectedCommand == "skip")
            {
                HideBattleUI();
                commandConfirmed = true;
            }
        }

        Debug.Log($"[Player Turn] Command confirmed: {selectedCommand}");
    }


    /// <summary>
    /// 적 기본 AI 턴 처리 (Auto가 꺼진 경우)
    /// </summary>
    private async UniTask ProcessDefaultEnemyTurn(TurnInfo turnInfo, CancellationToken token)
    {
        Debug.Log($"[Enemy Turn] Processing default enemy turn for {turnInfo.Actor.name}");

        // 기본 딜레이
        await UniTask.Delay((int)(AUTO_ATTACK_DELAY * 1000), cancellationToken: token);

        // 기본 공격 선택
        stateMachine.SetCommand("attack");

        // AI 타겟 선택 (기존 로직 사용)
        var target = GetAITargetBasedOnRange(turnInfo.Actor, allyActors);

        if (target == null)
        {
            // 타겟이 없으면 첫 번째 살아있는 아군
            target = allyActors.FirstOrDefault(a => !a.IsDead);
        }

        stateMachine.SetTarget(target);

        Debug.Log($"[Enemy Turn] Default action: attack -> {target?.name}");
    }


    // ========== 7. EnableAutoBattle 메서드 추가 ==========
    public void EnableAutoBattle(bool enable)
    {
        if (aiIntegration == null)
        {
            aiIntegration = GetComponent<BattleAIIntegration>();
            if (aiIntegration == null)
            {
                Debug.LogError("[Battle] BattleAIIntegration component not found!");
                return;
            }
        }

        aiIntegration.ToggleAutoBattle(enable);

        // 아군과 적 Auto도 함께 토글

        aiIntegration.ToggleAllyAuto(enable);

    }


    /// <summary>
    /// 기본 타겟 가져오기 (공격자 기준)
    /// </summary>
    private BattleActor GetDefaultTargetForAttacker(BattleActor attacker, List<BattleActor> enemies)
    {
        // BattleTargetSystem이 있으면 사용
        if (BattleTargetSystem.Instance != null)
        {
            // 기존 메서드 호출
            return BattleTargetSystem.Instance.GetDefaultTargetForAlly(enemies, attacker.BattleActorInfo.SlotIndex);
        }

        // 없으면 첫 번째 살아있는 적
        return enemies.FirstOrDefault(e => !e.IsDead);
    }

    /// <summary>
    /// AI 타겟 선정 (사거리 기반)
    /// </summary>
    private BattleActor GetAITargetBasedOnRange(BattleActor attacker, List<BattleActor> targets)
    {
        // 기존 AI 타겟 선정 로직 사용
        // 또는 BattleTargetSystem 사용
        if (BattleTargetSystem.Instance != null)
        {
            return BattleTargetSystem.Instance.FindTargetByAggro(targets);
        }

        // 기본: 첫 번째 살아있는 타겟
        return targets.FirstOrDefault(t => !t.IsDead);
    }


    /// <summary>
    /// 전투 종료 체크
    /// </summary>
    private bool CheckBattleEnd()
    {
        bool allAlliesDead = allyActors.All(a => a.IsDead);
        bool allEnemiesDead = enemyActors.All(e => e.IsDead);

        if (allAlliesDead || allEnemiesDead)
        {
            isBattleEnded = true;
            Debug.Log($"[Battle] Battle ended! Victory: {allEnemiesDead}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 공격 위치로 이동
    /// </summary>
    private async UniTask MoveToAttackPosition(TurnInfo turnInfo, CancellationToken token)
    {
        if (turnInfo.Actor == null || turnInfo.Actor.IsDead)
            return;

        // 이동 애니메이션
        turnInfo.Actor.SetAnimation(BattleActorAnimation.Walk);

        // 실제 이동 처리
        Vector3 attackPos = battlePosition.GetAttackPosition(
            turnInfo.Actor.BattleActorInfo.SlotIndex,
            turnInfo.IsAllyTurn);

        float moveTime = 0.5f;
        float elapsed = 0;
        Vector3 startPos = turnInfo.Actor.transform.position;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            turnInfo.Actor.transform.position = Vector3.Lerp(startPos, attackPos, t);
            await UniTask.Yield(token);
        }

        turnInfo.Actor.SetAnimation(BattleActorAnimation.Idle);
    }

    /// <summary>
    /// 시작 위치로 복귀
    /// </summary>
    private async UniTask ReturnToStartPosition(TurnInfo turnInfo, CancellationToken token)
    {
        if (turnInfo.Actor == null || turnInfo.Actor.IsDead)
            return;

        // 복귀 애니메이션
        turnInfo.Actor.SetAnimation(BattleActorAnimation.Walk);

        // 실제 이동 처리
        Vector3 standPos = battlePosition.GetStandPosition(
            turnInfo.Actor.BattleActorInfo.SlotIndex,
            turnInfo.IsAllyTurn);

        float moveTime = 0.5f;
        float elapsed = 0;
        Vector3 startPos = turnInfo.Actor.transform.position;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            turnInfo.Actor.transform.position = Vector3.Lerp(startPos, standPos, t);
            await UniTask.Yield(token);
        }

        turnInfo.Actor.SetAnimation(BattleActorAnimation.Idle);
    }

    /// <summary>
    /// 후열 타겟 가져오기
    /// </summary>
    private List<BattleActor> GetBackRowTargets(List<BattleActor> actors)
    {
        var backRowTargets = new List<BattleActor>();

        foreach (var actor in actors)
        {
            if (actor == null || actor.IsDead)
                continue;

            var frontSlots = battlePosition.GetFrontRowSlots(actor.BattleActorInfo.IsAlly);
            if (!frontSlots.Contains(actor.BattleActorInfo.SlotIndex))
            {
                backRowTargets.Add(actor);
            }
        }

        return backRowTargets;
    }


    private async UniTask PlayVictoryAnimation()
    {
        Debug.Log("[Battle] Playing victory animation");
        foreach (var ally in allyActors.Where(a => !a.IsDead))
        {
            ally.SetState(BattleActorState.Victory);
        }
        await UniTask.Delay(2000);
    }

    private async UniTask PlayDefeatAnimation()
    {
        Debug.Log("[Battle] Playing defeat animation");
        foreach (var enemy in enemyActors.Where(e => !e.IsDead))
        {
            enemy.SetState(BattleActorState.Victory);
        }
        await UniTask.Delay(2000);
    }

    // 나머지 메서드들은 기존 코드와 동일...
    private async UniTask InitBattleActors()
    {
        // 진형 설정
        battlePosition.SetFormation(initialAllyFormation);  // 아군/적군 동시 설정


        allyActors.Clear();
        enemyActors.Clear();
        allActors.Clear();

        if ( AllyInfos != null && EnemyInfos != null)
        {
            // 아군 액터 생성
            for (int i = 0; i < AllyInfos.Length; i++)
            {
                if (AllyInfos[i] == null) continue;

                var ally = await battleObjectPoolSupport.CreateBattleActor(AllyInfos[i]);
                if (ally != null)
                {
                    ally.Init(AllyInfos[i]);

                    Vector3 position = battlePosition.GetStandPosition(i, true);
                    ally.SetPosition(position);

                    allyActors.Add(ally);
                    allActors.Add(ally);
                }
            }

            // 적군 액터 생성
            for (int i = 0; i < EnemyInfos.Length; i++)
            {
                if (EnemyInfos[i] == null) continue;

                var enemy = await battleObjectPoolSupport.CreateBattleActor(EnemyInfos[i]);
                if (enemy != null)
                {
                    enemy.Init(EnemyInfos[i]);

                    Vector3 position = battlePosition.GetStandPosition(i, false);
                    enemy.SetPosition(position);

                    enemyActors.Add(enemy);
                    allActors.Add(enemy);
                }
            }
        }

        // 전투 시작 시 진형 버프 스킬 적용
        await ApplyFormationBuffSkills();
    }


    //이거 나중에 바꾸자. 
    // 직업 타입 결정 (임시 로직 - 실제 데이터에 맞게 수정 필요)
    private CharacterJobType DetermineJobType(BattleCharInfoNew charInfo)
    {
        // 캐릭터 데이터나 클래스에 따라 직업 결정
        // 예시 로직
        //if (charInfo.CharacterData != null)
        {
            string className = charInfo.CharacterData.GetNAME_ENG().ToLower();

            if (className.Contains("tank") || className.Contains("guard"))
                return CharacterJobType.Defender;
            else if (className.Contains("heal") || className.Contains("support"))
                return CharacterJobType.Support;
            else if (className.Contains("special") || className.Contains("unique"))
                return CharacterJobType.Special;
            else
                return CharacterJobType.Attacker;
        }

        // 기본값
      //  return CharacterJobType.Attacker;
    }


    // 진형 버프를 스킬로 적용
    private void ApplyFormationBuffAsSkill(BattleActor actor, bool isAlly)
    {
        //  var formation = battlePosition.GetCurrentFormation(isAlly);
        //  if (formation == null) return;

        // 진형 버프를 StatusEffectSet으로 생성
        /* var buffEffect = CreateFormationBuffEffect(formation, actor.BattleActorInfo.JobType);
         if (buffEffect != null)
         {
             actor.SkillManager.ReceiveStatusEffect(actor, buffEffect);
         }*/

        // StatusEffectSet 대신 스킬 ID 직접 사용
     //   int formationBuffSkillId = GetFormationBuffSkillId(formation);
    //    actor.SkillManager.ApplySkill(formationBuffSkillId, actor, actor);

    }




    // 진형 버프 적용
    private void ApplyFormationBuff(BattleActor actor, float buffMultiplier)
    {
        if (actor == null || actor.BattleActorInfo == null) return;

        // 스탯에 버프 적용
        var info = actor.BattleActorInfo;

        // FormationBuff 필드에 저장 (실제 스탯 계산 시 사용)
        info.FormationAtkBuff = buffMultiplier;
        info.FormationDefBuff = buffMultiplier;
        info.FormationSpeedBuff = buffMultiplier;

        Debug.Log($"[Formation] {actor.name} received {(buffMultiplier - 1) * 100:F0}% buff");
    }

    // 진형 버프 스킬 적용
    private async UniTask ApplyFormationBuffSkills()
    {
        // 아군 진형 버프
        var formation = battlePosition.GetCurrentFormation();
        if (formation != null)
        {
            // 아군 진형 버프
            await ApplyFormationSkill(formation, allyActors, true);

            // 적군 진형 버프
            await ApplyFormationSkill(formation, enemyActors, false);
        }
    }

    // 진형 스킬 적용
    private async UniTask ApplyFormationSkill(FormationData formation, List<BattleActor> actors, bool isAlly)
    {
        // 진형별 특수 스킬 효과
        switch (formation.formationType)
        {
            case FormationType.Offensive:
                // 공격형: 전체 공격력 추가 증가
                foreach (var actor in actors)
                {
                    if (actor != null && !actor.IsDead)
                    {
                        actor.BattleActorInfo.FormationAtkBuff *= 1.1f;
                    }
                }
                Debug.Log($"[Formation Skill] Offensive formation bonus applied to {(isAlly ? "allies" : "enemies")}");
                break;

            case FormationType.Defensive:
                // 방어형: 전체 방어력 추가 증가
                foreach (var actor in actors)
                {
                    if (actor != null && !actor.IsDead)
                    {
                        actor.BattleActorInfo.FormationDefBuff *= 1.15f;
                    }
                }
                Debug.Log($"[Formation Skill] Defensive formation bonus applied to {(isAlly ? "allies" : "enemies")}");
                break;

            case FormationType.OffensiveBalance:
                // 공격밸런스: 앞열 방어력, 뒷열 공격력 증가
                ApplyBalancedFormationBuff(actors, isAlly, true);
                break;

            case FormationType.DefensiveBalance:
                // 방어밸런스: 앞열 체력, 뒷열 속도 증가
                ApplyBalancedFormationBuff(actors, isAlly, false);
                break;
        }

        await UniTask.Delay(100); // 시각적 효과를 위한 딜레이
    }

    // 밸런스 진형 버프
    private void ApplyBalancedFormationBuff(List<BattleActor> actors, bool isAlly, bool isOffensive)
    {
        var frontRows = battlePosition.GetFrontRowSlots(isAlly);
        var backRows = battlePosition.GetBackRowSlots(isAlly);

        foreach (var actor in actors)
        {
            if (actor == null || actor.IsDead) continue;

            int slot = actor.BattleActorInfo.SlotIndex;

            if (frontRows.Contains(slot))
            {
                if (isOffensive)
                {
                    // 앞열: 방어력 증가
                    actor.BattleActorInfo.FormationDefBuff *= 1.1f;
                }
                else
                {
                    // 앞열: 체력 증가 (MaxHp 필드가 없다면 다른 방식으로)
                    // HP 버프는 별도 처리 필요
                    actor.BattleActorInfo.FormationDefBuff *= 1.15f; // 방어력으로 대체
                }
            }
            else if (backRows.Contains(slot))
            {
                if (isOffensive)
                {
                    // 뒷열: 공격력 증가
                    actor.BattleActorInfo.FormationAtkBuff *= 1.1f;
                }
                else
                {
                    // 뒷열: 속도 증가
                    actor.BattleActorInfo.FormationSpeedBuff *= 1.1f;
                }
            }
        }
    }


    // 헬퍼 메서드들
    private TurnOrderSystem.TurnOrderInfo GetCurrentTurnOrderInfo(TurnInfo turnInfo)
    {
        var allCharacters = turnOrderSystem.GetRemainingTurnsInRound();
        foreach (var character in allCharacters)
        {
            if (character.Actor == turnInfo.Actor)
                return character;
        }
        return new TurnOrderSystem.TurnOrderInfo(turnInfo.Actor, turnInfo.Actor.BattleActorInfo, turnInfo.IsAllyTurn, turnInfo.CharacterIndex);
    }

    private void UpdateBattleUI()
    {
        if (BattleUIManager.Instance != null && turnOrderSystem != null)
        {
            var progress = turnOrderSystem.GetRoundProgress();
            BattleUIManager.Instance.UpdateBattleInfo(turnOrderSystem.CurrentRound, turnOrderSystem.CurrentTurn, progress);
   
        }
    }


    /// <summary>
    /// 타임라인 업데이트 요청 (TurnSpeed 변경 시)
    /// </summary>
    public void RequestTimelineUpdate()
    {
        UpdateTimelineUI();
    }

    private void UpdateTimelineUI()
    {
        if (BattleUIManager.Instance != null && turnOrderSystem != null)
        {
            var remainingTurns = turnOrderSystem.GetRemainingTurnsInRound();

            // 항상 다음 라운드 미리보기 가져오기
            var nextRoundPreview = new List<TurnOrderSystem.TurnOrderInfo>();
            try
            {
                nextRoundPreview = turnOrderSystem.GetNextRoundPreview();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Timeline] Failed to get next round preview: {e.Message}");
                nextRoundPreview = new List<TurnOrderSystem.TurnOrderInfo>();
            }



            //작업중. 
            BattleUIManager.Instance.UpdateTurnTimeline(currentTurnInfo, remainingTurns, nextRoundPreview);
     
        }
    }

    // 이벤트 핸들러들
    private void OnDamageDealt(BattleEventData data)
    {
        if (data is DamageEventData damageData)
        {
            Debug.Log($"[Event] Damage dealt: {damageData.Damage} to {damageData.Target.name}");
            if (BattleUIManager.Instance != null)
            {
             
                
                /*BattleUIManager.Instance.ShowDamageEffect(
                    damageData.Target.BattleActorInfo.SlotIndex,
                    damageData.Damage,
                    damageData.Target.transform,
                    BattleDamageEffectType.Normal
                );*/


            }
        }
    }

    private void OnCharacterDeath(BattleEventData data)
    {
        if (data is StatusEventData statusData)
        {
            Debug.Log($"[Event] Character died: {statusData.Actor.name}");
            turnOrderSystem?.RemoveCharacter(statusData.Actor);
            UpdateBattleUI();
            UpdateTimelineUI();
            if (!isBattleEnded)
            {
                CheckAndHandleBattleEnd();
            }
        }
    }

    private void OnCommandSelectedEvent(BattleEventData data)
    {
        if (data is UIEventData uiData && uiData.Parameters.ContainsKey("command"))
        {
            selectedCommand = uiData.Parameters["command"] as string;
            Debug.Log($"[Event] Command selected via event: {selectedCommand}");
        }
    }

    private void OnBattleStateChanged(object sender, StateTransitionEventArgs e)
    {
        Debug.Log($"[State Event] {e.FromState} → {e.ToState}");
    }



    // 런타임 진형 변경 메서드 (스킬용)
    public async UniTask ChangeFormationDuringBattle(FormationType newFormation, bool isAlly)
    {
        Debug.Log($"[Formation Change] Changing {(isAlly ? "ally" : "enemy")} formation to {newFormation}");

        // 진형 변경
        battlePosition.ChangeFormationDuringBattle(newFormation, 1.5f);

        // 버프 재계산
        var actors = isAlly ? allyActors : enemyActors;
        foreach (var actor in actors)
        {
            if (actor != null && !actor.IsDead)
            {
                // 기존 버프 제거 (필요시)
                // RemoveFormationBuff(actor);

                // 새 버프 적용
                CharacterJobType jobType = actor.BattleActorInfo.JobType;
                float newBuff = battlePosition.GetFormationBuff(jobType, actor.BattleActorInfo.SlotIndex, isAlly);
                ApplyFormationBuff(actor, newBuff);
            }
        }

        // 새 진형 스킬 적용
        var formation = battlePosition.GetCurrentFormation();
        if (formation != null)
        {
            await ApplyFormationSkill(formation, allyActors, true);
            await ApplyFormationSkill(formation, enemyActors, false);
        }

        await UniTask.Delay(1500); // 애니메이션 대기
    }

    // 타겟 선택 확장 (다중 타겟용)
    public List<BattleActor> GetTargetsByType(ETargetType targetType, bool isAlly)
    {
        var actors = isAlly ? enemyActors : allyActors;

        switch (targetType)
        {
            case ETargetType.Single:
                // 기존 단일 타겟 로직
                return new List<BattleActor> { GetFirstAliveEnemy() };

            case ETargetType.FrontRow:
                return battlePosition.GetFrontRowActors(actors, !isAlly);

            case ETargetType.BackRow:
                return battlePosition.GetBackRowActors(actors, !isAlly);

            case ETargetType.All:
                return battlePosition.GetAllActors(actors);

            default:
                return new List<BattleActor>();
        }
    }



    private async UniTask WaitForCommand(BattleActor currentActor, CancellationToken token)
    {
        waitingForUICommand = true;
        selectedCommand = "";

        // Force Command가 이미 설정된 경우 즉시 처리
        if (isForceCommandActive)
        {
            Debug.Log($"[Battle] Using forced command: {selectedCommand}");

            // 스킬 ID 설정
            if (forcedSkillId > 0)
            {
                selectedSkillId = forcedSkillId;
            }

            // 타겟이 있으면 stateMachine에 설정
            if (forcedTarget != null)
            {
                stateMachine.SetTarget(forcedTarget);
            }

            // 플래그 리셋
            isForceCommandActive = false;
            forcedTarget = null;
            forcedSkillId = 0;

            waitingForUICommand = false;
            return;
        }

        ShowBattleUI(currentActor);
        float timer = 0f;

        while (string.IsNullOrEmpty(selectedCommand) && timer < COMMAND_WAIT_TIME)
        {
            if (token.IsCancellationRequested) break;

            // AI Integration 체크
            var aiIntegration = GetComponent<BattleAIIntegration>();
            if (aiIntegration != null)
            {
                // Auto가 켜졌고 즉시 실행 결정이 있는 경우 - 우선 처리
                if (aiIntegration.HasImmediateDecision())
                {
                    var decision = aiIntegration.GetImmediateDecision();

                    // AI 결정 적용
                    stateMachine.SetCommand(decision.ActionType.Replace("bp_", ""));
                    stateMachine.SetTarget(decision.Target);

                    if (decision.BPUse > 0)
                    {
                        pendingBPUse = decision.BPUse;
                        currentActor.BattleActorInfo.SetBPThisTurn(decision.BPUse);
                    }

                    if (decision.SkillId > 0)
                    {
                        selectedSkillId = decision.SkillId;
                    }

                    selectedCommand = decision.ActionType.Replace("bp_", "");


                    isForceCommandActive = true;

                    Debug.Log($"[Battle] Immediate AI decision applied: {selectedCommand}");
                    break;  // 루프 탈출
                }

                // 일반 Auto 체크 (즉시 실행이 아닌 경우)
                if (aiIntegration.ShouldAIControl(currentActor) && !aiIntegration.HasImmediateDecision())
                {
                    // AI 결정 요청
                    var enemies = currentActor.BattleActorInfo.IsAlly ?
                        enemyActors.Where(e => !e.IsDead).ToList() :
                        allyActors.Where(a => !a.IsDead).ToList();

                    var allies = currentActor.BattleActorInfo.IsAlly ?
                        allyActors.Where(a => !a.IsDead).ToList() :
                        enemyActors.Where(e => !e.IsDead).ToList();

                    var decision = await BattleAIManager.Instance.MakeDecision(
                        currentActor, enemies, allies, token);

                    if (decision != null)
                    {
                        // 결정 적용
                        stateMachine.SetCommand(decision.ActionType.Replace("bp_", ""));
                        stateMachine.SetTarget(decision.Target);

                        if (decision.BPUse > 0)
                        {
                            pendingBPUse = decision.BPUse;
                            currentActor.BattleActorInfo.SetBPThisTurn(decision.BPUse);
                        }

                        if (decision.SkillId > 0)
                        {
                            selectedSkillId = decision.SkillId;
                        }

                        selectedCommand = decision.ActionType.Replace("bp_", "");

                        Debug.Log($"[Battle] AI decision applied: {selectedCommand}");
                        break;  // 루프 탈출
                    }
                }
            }

            // 타이머 업데이트
            timer += Time.deltaTime;
            if (BattleUIManager.Instance != null)
            {
                BattleUIManager.Instance.UpdateTimer(COMMAND_WAIT_TIME - timer, COMMAND_WAIT_TIME);
            }
            await UniTask.Yield(token);
        }

        // 시간 초과시 기본 공격
        if (string.IsNullOrEmpty(selectedCommand))
        {
            selectedCommand = "attack";
            Debug.Log("[Battle] Command timeout - defaulting to attack");
        }

        HideBattleUI();
        waitingForUICommand = false;


    }


    public void OnUICommandSelected(string command)
    {
        if (waitingForUICommand)
        {
            // BP 커맨드 처리 추가
            if (command.Contains("_bp_"))
            {
                string[] parts = command.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int bp))
                {
                    pendingBPUse = bp;
                    selectedCommand = parts[0];  // "attack" or "skill"

                    // 현재 액터의 BP 차감
                    var currentActor = stateMachine.CurrentTurn.Actor;
                    if (currentActor?.BattleActorInfo != null)
                    {
                        currentActor.BattleActorInfo.ReduceBP();
                        currentActor.BattleActorInfo.SetBPThisTurn( bp );
                    }

                    BattleUIManager.Instance.UpdateBPButton(currentActor);
                }
            }
            else
            {
                pendingBPUse = 0;
                selectedCommand = command;
            }
        }
    }

    private void ShowBattleUI(BattleActor currentActor = null)
    {
        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.ShowBattleUI(OnUICommandSelected, currentActor);
        }
    }

    private void HideBattleUI()
    {
        if (BattleUIManager.Instance != null)
        {
            BattleUIManager.Instance.HideBattleUI();
        }
    }


    private BattleActor GetFirstAliveEnemy()
    {
        return enemyActors.FirstOrDefault(e => e != null && !e.IsDead);
    }

    private BattleActor GetRandomAliveAlly()
    {
        var aliveAllies = allyActors.Where(a => a != null && !a.IsDead).ToList();
        return aliveAllies.Count > 0 ?
            aliveAllies[UnityEngine.Random.Range(0, aliveAllies.Count)] : null;
    }



    /// <summary>
    /// 타겟 선택 시 강제 타겟 체크
    /// </summary>
    private BattleActor GetTargetWithForcedCheck(BattleActor attacker, List<BattleActor> potentialTargets)
    {
        // 강제 타겟 체크
        if (attacker.HasForcedTarget)
        {
            var forced = attacker.GetCurrentTarget();
            if (forced != null && potentialTargets.Contains(forced))
            {
                Debug.Log($"[Battle] Using forced target: {forced.name}");
                return forced;
            }
        }

        // 일반 타겟 선택
        return attacker.SelectTarget(potentialTargets);
    }

    /// <summary>
    /// 전투 상태 체크 시 강제 타겟 업데이트
    /// </summary>
    private void UpdateAllForcedTargets()
    {
        // 모든 캐릭터의 강제 타겟 상태 체크
        foreach (var actor in allyActors.Concat(enemyActors))
        {
            if (actor != null && !actor.IsDead)
            {
                actor.OnTurnEndForcedTargetCheck();
            }
        }
    }


}

