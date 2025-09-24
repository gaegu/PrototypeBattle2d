using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TownStateMachine
{
    private readonly Dictionary<FlowState, ITownState> states;
    private ITownState currentState;
    private TownStateContext context;
    private bool isTransitioning = false;

    // 전환 큐 추가 (동시 전환 요청 처리)
    private readonly Queue<TransitionRequest> transitionQueue;
    private CancellationTokenSource transitionCts;

    private readonly IServiceContainer serviceContainer;

    public FlowState CurrentState => context?.State ?? FlowState.None;
    public bool IsTransitioning => isTransitioning;
    public ITownState CurrentStateInstance => currentState;

    // 전환 요청 데이터
    private class TransitionRequest
    {
        public FlowState TargetState { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public bool IsForced { get; set; }
    }

    public TownStateMachine(TownFlowModel model, IServiceContainer container = null)
    {
        context = new TownStateContext { Model = model };
        states = new Dictionary<FlowState, ITownState>();
        transitionQueue = new Queue<TransitionRequest>();
        transitionCts = new CancellationTokenSource();

        serviceContainer = container ?? new TownServiceContainer();

        InitializeStates();
        ProcessTransitionQueue().Forget();
    }

    private void InitializeStates()
    {
        // 1. 기본 상태들
        RegisterState(FlowState.None, new IdleState());
        RegisterState(FlowState.Home, new HomeState());

        // 2. 워프 관련
        RegisterState(FlowState.Warp, new WarpState(serviceContainer));

        // 3. 전투 관련
        RegisterState(FlowState.Battle, new BattleNewState(serviceContainer));
        RegisterState(FlowState.BattleResult, new BattleResultState());

        // 4. 미션 관련
        RegisterState(FlowState.BeforeMissionUpdate, new BeforeMissionUpdateState(serviceContainer));
        RegisterState(FlowState.AfterMissionUpdate, new AfterMissionUpdateState(serviceContainer));

        // 5. 튜토리얼
        RegisterState(FlowState.Tutorial, new TutorialState());

        // 6. 프롤로그
        RegisterState(FlowState.Prologue, new PrologueState());

        Debug.Log($"[TownStateMachine] Initialized with {states.Count} states");

        foreach (var kvp in states)
        {
            Debug.Log($"[TownStateMachine] Registered: {kvp.Key} -> {kvp.Value.StateName}");
        }
    }

    public void RegisterState(FlowState stateType, ITownState state)
    {
        if (!states.ContainsKey(stateType))
        {
            states[stateType] = state;
            Debug.Log($"[TownStateMachine] State registered: {stateType}");
        }
        else
        {
            Debug.LogWarning($"[TownStateMachine] State already registered: {stateType}");
        }
    }

    public async UniTask TransitionTo(FlowState newState, Dictionary<string, object> parameters = null, bool forced = false)
    {
        // 큐에 추가
        transitionQueue.Enqueue(new TransitionRequest
        {
            TargetState = newState,
            Parameters = parameters,
            IsForced = forced
        });
    }

    private async UniTask ProcessTransitionQueue()
    {
        while (!transitionCts.Token.IsCancellationRequested)
        {
            if (transitionQueue.Count > 0 && !isTransitioning)
            {
                var request = transitionQueue.Dequeue();
                await ExecuteTransition(request);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, transitionCts.Token);
        }
    }

    private async UniTask ExecuteTransition(TransitionRequest request)
    {
        if (isTransitioning && !request.IsForced)
        {
            Debug.LogWarning($"[TownStateMachine] Already transitioning, request to {request.TargetState} queued");
            transitionQueue.Enqueue(request); // 다시 큐에 추가
            return;
        }

        // 같은 상태로의 전환 방지
        if (context.State == request.TargetState && !request.IsForced)
        {
            Debug.Log($"[TownStateMachine] Already in state {request.TargetState}, skipping transition");
            return;
        }

        if (!states.ContainsKey(request.TargetState))
        {
            Debug.LogError($"[TownStateMachine] State {request.TargetState} not registered!");
            return;
        }

        isTransitioning = true;

        try
        {
            Debug.Log($"[TownStateMachine] Transitioning: {context.State} -> {request.TargetState}");

            // Validation
            if (currentState != null)
            {
                if (!request.IsForced && !currentState.CanTransitionTo(request.TargetState))
                {
                    Debug.LogWarning($"[TownStateMachine] Cannot transition from {context.State} to {request.TargetState}");
                    return;
                }

                // Exit with timeout
                using (var cts = new CancellationTokenSource())
                {
                    cts.CancelAfterSlim(TimeSpan.FromSeconds(10)); // 10초 타임아웃

                    try
                    {
                        await currentState.Exit().AttachExternalCancellation(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogError($"[TownStateMachine] Exit timeout for state {context.State}");
                    }
                }
            }

            // Context 업데이트
            context.PreviousState = context.State;
            context.State = request.TargetState;

            if (request.Parameters != null)
            {
                context.Parameters = request.Parameters;
            }
            else
            {
                context.Parameters.Clear();
            }

            // 새로운 State Enter
            currentState = states[request.TargetState];

            // Enter with timeout
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfterSlim(TimeSpan.FromSeconds(10));

                try
                {
                    await currentState.Enter(context).AttachExternalCancellation(cts.Token);
                    Debug.Log($"[TownStateMachine] Successfully entered state: {request.TargetState}");
                }
                catch (OperationCanceledException)
                {
                    Debug.LogError($"[TownStateMachine] Enter timeout for state {request.TargetState}");
                    // 타임아웃 시 안전 상태(None)로 복귀
                    await ForceTransitionToSafeState();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TownStateMachine] Transition failed: {e}");
            await ForceTransitionToSafeState();
        }
        finally
        {
            isTransitioning = false;
        }
    }

    private async UniTask ForceTransitionToSafeState()
    {
        Debug.LogWarning("[TownStateMachine] Forcing transition to safe state (None)");

        context.State = FlowState.None;
        currentState = states[FlowState.None];

        try
        {
            await currentState.Enter(context);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TownStateMachine] Failed to enter safe state: {e}");
        }
    }

    public async UniTask Execute()
    {
        if (currentState != null && !isTransitioning)
        {
            try
            {
                await currentState.Execute(context);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TownStateMachine] Execute failed for state {context.State}: {e}");
            }
        }
    }

    public void Dispose()
    {
        transitionCts?.Cancel();
        transitionCts?.Dispose();
        transitionCts = null;

        transitionQueue?.Clear();

        // State 정리
        if (currentState != null)
        {
            currentState = null;
        }

        states?.Clear();
        context = null;
    }

    // 디버그용 메서드
    public void PrintStateInfo()
    {
        Debug.Log($"[TownStateMachine] Current: {CurrentState}, Previous: {context.PreviousState}, Transitioning: {isTransitioning}");
        Debug.Log($"[TownStateMachine] Queue Count: {transitionQueue.Count}");
    }
}