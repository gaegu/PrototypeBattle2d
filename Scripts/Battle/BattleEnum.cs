using System.Collections;
using System.Collections.Generic;


// 타겟 타입 enum 추가
public enum ETargetType
{
    Single,     // 단일 타겟
    FrontRow,   // 앞열 전체
    BackRow,    // 뒷열 전체
    All         // 전체
}



public enum BattleActorEffectType
{
    None,
    Idle,
    Attack1,
    Attack2,
    Attack3,
    Skill1,
    ChargeBP1,
    ChargeBP2,
    ChargeBP3,
    Break,
    Dead,
    Hit,
    Heal
}


public enum BattleActorState
{
    Idle,
    MoveToAttackPoint,
    Attack,
    BackToStartPoint,
    Dead,
    Victory,
}

public enum BattleActorAnimation
{
    Idle,
    Blink,
    Walk,
    Skill,
    Attack,
    Hit,
    Dead
}

public enum BattleActorDirection
{
    None = 0,
    Left,      // 왼쪽
    Right,     // 오른쪽
}




public enum BattleTypeNew
{
    None = 0,
    Arena,
    Raid
}



