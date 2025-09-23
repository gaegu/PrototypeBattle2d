// TownStateMachine.cs (새 파일)
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using static ApplicationSlotUnit;

public class TownStateMachine
{
    private readonly Dictionary<FlowState, ITownState> states;
    private ITownState currentState;
    private TownStateContext context;
    private bool isTransitioning = false;

    private readonly IServiceContainer serviceContainer;


    public FlowState CurrentState => context?.State ?? FlowState.None;
    public bool IsTransitioning => isTransitioning;

    public TownStateMachine(TownFlowModel model, IServiceContainer container = null)
    {
        context = new TownStateContext { Model = model };
        states = new Dictionary<FlowState, ITownState>();

        serviceContainer = container ?? new TownServiceContainer();

        InitializeStates();
    }

    private void InitializeStates()
    {
        // ===== FlowState enum과 정확히 매칭되는 State들 등록 =====

        // 1. 기본 상태들
        RegisterState(FlowState.None, new IdleState());
        RegisterState(FlowState.Home, new HomeState());

        // 2. 워프 관련
        RegisterState(FlowState.Warp, new WarpState(serviceContainer));

        // 3. 전투 관련
        RegisterState(FlowState.Battle, new BattleNewState(serviceContainer));


        RegisterState(FlowState.BattleResult, new BattleResultState()); // 새로 추가

        // 4. 미션 관련
        RegisterState(FlowState.BeforeMissionUpdate, new BeforeMissionUpdateState(serviceContainer));
        RegisterState(FlowState.AfterMissionUpdate, new AfterMissionUpdateState(serviceContainer));

        // 5. 튜토리얼
        RegisterState(FlowState.Tutorial, new TutorialState());

        // 6. 프롤로그 관련 (필요시)
        RegisterState(FlowState.Prologue, new PrologueState()); // 새로 추가

        // 7. 기타 상태들 (필요한 경우만)
        // RegisterState(FlowState.Loading, new LoadingState(loadingModule));

        Debug.Log($"[TownStateMachine] Initialized with {states.Count} states");

        // 등록된 State 목록 출력 (디버그용)
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
        }
    }

    public async UniTask TransitionTo(FlowState newState, Dictionary<string, object> parameters = null)
    {
        if (isTransitioning)
        {
            Debug.LogWarning($"[TownStateMachine] Already transitioning, ignoring request to {newState}");
            return;
        }

        if (!states.ContainsKey(newState))
        {
            Debug.LogError($"[TownStateMachine] State {newState} not registered!");
            return;
        }

        isTransitioning = true;

        try
        {
            Debug.Log($"[TownStateMachine] Transitioning: {context.State} -> {newState}");

            // 현재 State Exit
            if (currentState != null)
            {
                if (!currentState.CanTransitionTo(newState))
                {
                    Debug.LogWarning($"[TownStateMachine] Cannot transition from {context.State} to {newState}");
                    return;
                }
                await currentState.Exit();
            }

            // Context 업데이트
            context.PreviousState = context.State;
            context.State = newState;
            if (parameters != null)
                context.Parameters = parameters;

            // 새로운 State Enter
            currentState = states[newState];
            await currentState.Enter(context);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TownStateMachine] Transition failed: {e}");
        }
        finally
        {
            isTransitioning = false;
        }
    }

    public async UniTask Execute()
    {
        if (currentState != null && !isTransitioning)
        {
            await currentState.Execute(context);
        }
    }
}