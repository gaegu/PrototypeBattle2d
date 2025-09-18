using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

// 통합된 전투 상태 열거형
public enum BattleState
{
    None,
    Initialize,
    BattleStart,
    TurnStart,
    CharacterMove,
    CommandSelect,
    ActionExecute,
    CharacterReturn,
    TurnEnd,
    BattleResult,
    BattleEnd
}

// 턴 정보를 담는 구조체
public struct TurnInfo
{
    public bool IsAllyTurn;
    public int CharacterIndex;
    public BattleActor Actor;
    public BattleActor Target;
    public string Command;

    public TurnInfo(bool isAlly, int index, BattleActor actor)
    {
        IsAllyTurn = isAlly;
        CharacterIndex = index;
        Actor = actor;
        Target = null;
        Command = "";
    }
}

// 상태 전환 이벤트 인자
public class StateTransitionEventArgs : EventArgs
{
    public BattleState FromState { get; set; }
    public BattleState ToState { get; set; }
    public TurnInfo TurnInfo { get; set; }
}

// 통합된 전투 상태 머신
public class UnifiedBattleStateMachine
{
    private BattleState _currentState = BattleState.None;
    private BattleState _previousState = BattleState.None;
    private TurnInfo _currentTurn;
    private CancellationTokenSource _cts;

    // 이벤트
    public event EventHandler<StateTransitionEventArgs> OnStateChanged;
    public event EventHandler<TurnInfo> OnTurnChanged;

    // 상태 getter
    public BattleState CurrentState => _currentState;
    public BattleState PreviousState => _previousState;
    public TurnInfo CurrentTurn => _currentTurn;

    // 상태 전환 딕셔너리 (유효한 전환만 허용)
    private readonly Dictionary<BattleState, HashSet<BattleState>> _validTransitions = new Dictionary<BattleState, HashSet<BattleState>>
    {
        { BattleState.None, new HashSet<BattleState> { BattleState.Initialize } },
        { BattleState.Initialize, new HashSet<BattleState> { BattleState.BattleStart } },
        { BattleState.BattleStart, new HashSet<BattleState> { BattleState.TurnStart } },
        { BattleState.TurnStart, new HashSet<BattleState> { BattleState.CharacterMove, BattleState.BattleResult } },
        { BattleState.CharacterMove, new HashSet<BattleState> { BattleState.CommandSelect, BattleState.ActionExecute } },
        { BattleState.CommandSelect, new HashSet<BattleState> { BattleState.ActionExecute } },
        { BattleState.ActionExecute, new HashSet<BattleState> { BattleState.CharacterReturn } },
        { BattleState.CharacterReturn, new HashSet<BattleState> { BattleState.TurnEnd } },
        { BattleState.TurnEnd, new HashSet<BattleState> { BattleState.TurnStart, BattleState.BattleResult } },
        { BattleState.BattleResult, new HashSet<BattleState> { BattleState.BattleEnd } },
        { BattleState.BattleEnd, new HashSet<BattleState> { BattleState.None } }
    };

    public UnifiedBattleStateMachine()
    {
        _cts = new CancellationTokenSource();
    }

    // 상태 전환
    public bool TransitionTo(BattleState newState)
    {
        if (!CanTransitionTo(newState))
        {
            Debug.LogWarning($"Invalid state transition from {_currentState} to {newState}");
            return false;
        }

        _previousState = _currentState;
        _currentState = newState;

        var args = new StateTransitionEventArgs
        {
            FromState = _previousState,
            ToState = _currentState,
            TurnInfo = _currentTurn
        };

        OnStateChanged?.Invoke(this, args);

        Debug.Log($"[State Transition] {_previousState} → {_currentState}");

        return true;
    }

    // 전환 가능 여부 체크
    public bool CanTransitionTo(BattleState newState)
    {
        if (!_validTransitions.ContainsKey(_currentState))
            return false;

        return _validTransitions[_currentState].Contains(newState);
    }

    // 턴 정보 설정
    public void SetTurnInfo(TurnInfo turnInfo)
    {
        _currentTurn = turnInfo;
        OnTurnChanged?.Invoke(this, turnInfo);
    }

    // 명령 설정
    public void SetCommand(string command)
    {
        _currentTurn.Command = command;
    }

    // 타겟 설정
    public void SetTarget(BattleActor target)
    {
        _currentTurn.Target = target;
    }

    // 리셋
    public void Reset()
    {
        _currentState = BattleState.None;
        _previousState = BattleState.None;
        _currentTurn = default(TurnInfo);
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    public CancellationToken GetCancellationToken() => _cts.Token;
}