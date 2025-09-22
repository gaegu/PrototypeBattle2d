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
    ReturnToStartPoint,
    Skill,
    Hit,
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





/// <summary>
/// 커스텀 액션 타입
/// </summary>
public enum CustomActionType
{
    None,

    // Target Position Actions
    MoveToPosition,     // 타겟 위치 이동

    // Target Effect Actions  
    Knockback,          // 밀어내기
    SetVisibility,      // 보이기/숨기기
    FlashEffect,        // 피격 플래시

    // Combat Actions
    HitEvent,           // 데미지 적용 타이밍
    ApplyBuff,          // 버프 적용
    ApplyDebuff,        // 디버프 적용

    // Camera Actions (기존 CameraEvent 연결용)
    TriggerCameraShake, // 카메라 흔들기
}

/// <summary>
/// 타겟 위치 프리셋
/// </summary>
public enum TargetPositionPreset
{
    Center,             // (0, 0, 0)
    Custom,             // 직접 입력
}

/// <summary>
/// 넉백 강도
/// </summary>
public enum KnockbackIntensity
{
    Weak,               // 약하게
    Normal,             // 보통
    Strong,             // 강하게
    Custom,             // 직접 입력
}

/// <summary>
/// 가시성 설정
/// </summary>
public enum VisibilityMode
{
    Show,               // 보이기
    Hide,               // 숨기기
    FadeIn,             // 페이드 인
    FadeOut,            // 페이드 아웃
}


/// <summary>
/// 플래시 효과 색상
/// </summary>
public enum FlashColorPreset
{
    White,              // 흰색 플래시
    Red,                // 빨간색 (데미지)
    Green,              // 초록색 (힐)
    Custom,             // 직접 색상 지정
}