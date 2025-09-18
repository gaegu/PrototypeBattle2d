using System;
using UnityEngine;

namespace BattleCharacterSystem
{
    // =====================================================
    // 기존 프로젝트의 enum들과 호환을 위한 추가 정의
    // 기존 enum이 있다면 그것을 사용하고, 없는 것만 여기 정의
    // =====================================================

    /// <summary>
    /// 몬스터 타입
    /// </summary>
    [Serializable]
    public enum MonsterType
    {
        Normal = 0,       // 일반
        Elite = 1,        // 엘리트
        MiniBoss = 2,     // 중간보스
        Boss = 3,         // 보스
        Special = 4       // 특수
    }

    /// <summary>
    /// 몬스터 행동 패턴
    /// </summary>
    [Serializable]
    public enum MonsterPattern
    {
        Aggressive = 0,   // 공격형
        Defensive = 1,    // 방어형
        Support = 2,      // 지원형
        Balanced = 3,     // 균형형
        Special = 4       // 특수 패턴
    }

    /// <summary>
    /// 진형 위치 선호도
    /// </summary>
    [Serializable]
    public enum FormationPreference
    {
        FrontOnly = 0,    // 앞열 고정
        BackOnly = 1,     // 뒷열 고정
        PreferFront = 2,  // 앞열 선호
        PreferBack = 3,   // 뒷열 선호
        Flexible = 4      // 유연함
    }

    /// <summary>
    /// 데이터 타입 구분
    /// </summary>
    [Serializable]
    public enum BattleDataType
    {
        Character = 0,
        Monster = 1,
        Boss = 2
    }

    /// <summary>
    /// 면역 타입 확장 (몬스터 전용)
    /// </summary>
    [Flags]
    [Serializable]
    public enum ImmunityTypeExt
    {
        None = 0,
        Physical = 1 << 0,           // 물리 면역
        Magical = 1 << 1,            // 마법 면역
        PowerElement = 1 << 2,       // Power 속성 면역
        PlasmaElement = 1 << 3,      // Plasma 속성 면역
        ChemicalElement = 1 << 4,    // Chemical 속성 면역
        BioElement = 1 << 5,         // Bio 속성 면역
        NetworkElement = 1 << 6,     // Network 속성 면역
        ElectricalElement = 1 << 7   // Electrical 속성 면역
    }
}