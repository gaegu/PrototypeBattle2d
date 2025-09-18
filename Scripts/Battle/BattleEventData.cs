using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 이벤트 타입 정의
public enum BattleEventType
{
    // 전투 흐름
    BattleStart,
    BattleEnd,
    TurnStart,
    TurnEnd,
    RoundStart,
    RoundEnd,

    // 액션 관련
    BeforeAction,
    AfterAction,
    ActionCancelled,

    // 데미지/힐 관련
    BeforeDamage,
    AfterDamage,
    BeforeHeal,
    AfterHeal,
    CriticalHit,

    // 상태 변화
    CharacterDeath,
    CharacterRevive,
    BuffApplied,
    DebuffApplied,
    StatusRemoved,

    // UI 관련
    CommandSelected,
    TimerUpdate,
    UIShow,
    UIHide,

    // 특수 이벤트
    BreakOccurred,
    ChainStart,
    ChainEnd,
    SpecialMoveTriggered
}

// 이벤트 데이터 기본 클래스
public abstract class BattleEventData
{
    public float Timestamp { get; set; }
    public BattleEventType EventType { get; set; }

    protected BattleEventData(BattleEventType eventType)
    {
        EventType = eventType;
        Timestamp = Time.time;
    }
}

// 전투 시작/종료 이벤트
public class BattleFlowEventData : BattleEventData
{
    public bool IsVictory { get; set; }
    public int TurnCount { get; set; }
    public float BattleTime { get; set; }

    public BattleFlowEventData(BattleEventType type) : base(type) { }
}

// 턴 이벤트
public class TurnEventData : BattleEventData
{
    public BattleActor Actor { get; set; }
    public bool IsAllyTurn { get; set; }
    public int TurnIndex { get; set; }

    public TurnEventData(BattleEventType type, BattleActor actor, bool isAlly, int index) : base(type)
    {
        Actor = actor;
        IsAllyTurn = isAlly;
        TurnIndex = index;
    }
}

// 액션 이벤트
public class ActionEventData : BattleEventData
{
    public BattleActor Actor { get; set; }
    public BattleActor Target { get; set; }
    public string ActionName { get; set; }
    public CommandResult Result { get; set; }

    public ActionEventData(BattleEventType type) : base(type) { }
}

// 데미지 이벤트
public class DamageEventData : BattleEventData
{
    public BattleActor Source { get; set; }
    public BattleActor Target { get; set; }
    public int Damage { get; set; }
    public int ActualDamage { get; set; } // 방어/버프 적용 후 실제 데미지
    public bool IsCritical { get; set; }
    public ElementType DamageType { get; set; }

    public DamageEventData(BattleEventType type) : base(type) { }
}

// 상태 변화 이벤트
public class StatusEventData : BattleEventData
{
    public BattleActor Actor { get; set; }
    public string StatusName { get; set; }
    public int Duration { get; set; }
    public bool IsPositive { get; set; } // 버프인지 디버프인지

    public StatusEventData(BattleEventType type) : base(type) { }
}

// UI 이벤트
public class UIEventData : BattleEventData
{
    public string UIElement { get; set; }
    public Dictionary<string, object> Parameters { get; set; }

    public UIEventData(BattleEventType type) : base(type)
    {
        Parameters = new Dictionary<string, object>();
    }
}

public class HealEventData : BattleEventData
{
    public BattleActor Source { get; set; }
    public BattleActor Target { get; set; }
    public int HealAmount { get; set; }
    public int ActualHeal { get; set; } // 실제 회복된 양 (오버힐 제외)
    public bool IsCritical { get; set; }
    public bool WasOverheal { get; set; }

    public HealEventData(BattleEventType type) : base(type) { }
}
