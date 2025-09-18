using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 속성 상성 관계를 관리하는 헬퍼 클래스
/// </summary>
public static class BattleElementHelper
{
    // 속성 상성 관계 정의
    // Key: (공격자 속성, 방어자 속성), Value: 데미지 배율
    private static readonly Dictionary<(EBattleElementType, EBattleElementType), float> ElementAdvantageTable = new Dictionary<(EBattleElementType, EBattleElementType), float>()
    {
        // 순환 상성 관계: Power → Plasma → Bio → Chemical → Power
        { (EBattleElementType.Plasma, EBattleElementType.Power), 1.5f },      
        { (EBattleElementType.Power, EBattleElementType.Chemical), 1.5f },       
        { (EBattleElementType.Chemical, EBattleElementType.Bio), 1.5f },     
        { (EBattleElementType.Bio, EBattleElementType.Plasma), 1.5f }, 
        
        // 역상성 관계 (약함) - 1/1.3 ≈ 0.77
        { (EBattleElementType.Power, EBattleElementType.Plasma), 0.77f },     
        { (EBattleElementType.Chemical, EBattleElementType.Power), 0.77f },       
        { (EBattleElementType.Bio, EBattleElementType.Chemical), 0.77f },    
        { (EBattleElementType.Plasma, EBattleElementType.Bio), 0.77f }, 
        
        // Electrical와 Network는 서로에게 강함
        { (EBattleElementType.Electrical, EBattleElementType.Network), 1.5f },          
        { (EBattleElementType.Network, EBattleElementType.Electrical), 1.5f },          
    };

    /// <summary>
    /// 두 속성 간의 데미지 배율을 계산합니다.
    /// </summary>
    /// <param name="attackerElement">공격자의 속성</param>
    /// <param name="defenderElement">방어자의 속성</param>
    /// <returns>데미지 배율 (1.0 = 보통, 1.5 = 강함, 0.77 = 약함)</returns>
    public static float GetElementMultiplier(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        // None 속성은 상성 없음
        if (attackerElement == EBattleElementType.None || defenderElement == EBattleElementType.None)
            return 1.0f;

        // 같은 속성끼리는 상성 없음
        if (attackerElement == defenderElement)
            return 1.0f;

        // 상성 테이블에서 확인
        if (ElementAdvantageTable.TryGetValue((attackerElement, defenderElement), out float multiplier))
        {
            return multiplier;
        }

        // 정의되지 않은 관계는 상성 없음
        return 1.0f;
    }

    /// <summary>
    /// 속성 상성 관계를 텍스트로 반환합니다.
    /// </summary>
    public static string GetElementRelation(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        float multiplier = GetElementMultiplier(attackerElement, defenderElement);

        if (multiplier > 1.0f)
            return "Strong";  // 강함
        else if (multiplier < 1.0f)
            return "Weak";    // 약함
        else
            return "Normal";  // 보통
    }

    /// <summary>
    /// 속성 상성에 따른 색상을 반환합니다. (UI 표시용)
    /// </summary>
    public static Color GetElementRelationColor(EBattleElementType attackerElement, EBattleElementType defenderElement)
    {
        float multiplier = GetElementMultiplier(attackerElement, defenderElement);

        if (multiplier > 1.0f)
            return Color.green;  // 강함 - 녹색
        else if (multiplier < 1.0f)
            return Color.red;    // 약함 - 빨간색
        else
            return Color.white;  // 보통 - 흰색
    }

    /// <summary>
    /// 특정 속성에 강한 속성을 반환합니다.
    /// </summary>
    public static EBattleElementType GetStrongAgainst(EBattleElementType defenderElement)
    {
        switch (defenderElement)
        {
            case EBattleElementType.Power:
                return EBattleElementType.Plasma;
            case EBattleElementType.Plasma:
                return EBattleElementType.Bio;
            case EBattleElementType.Bio:
                return EBattleElementType.Chemical;
            case EBattleElementType.Chemical:
                return EBattleElementType.Power;
            case EBattleElementType.Electrical:
                return EBattleElementType.Network;
            case EBattleElementType.Network:
                return EBattleElementType.Electrical;
            default:
                return EBattleElementType.None;
        }
    }

    /// <summary>
    /// 특정 속성에 약한 속성을 반환합니다.
    /// </summary>
    public static EBattleElementType GetWeakAgainst(EBattleElementType defenderElement)
    {
        switch (defenderElement)
        {
            case EBattleElementType.Power:
                return EBattleElementType.Chemical;
            case EBattleElementType.Plasma:
                return EBattleElementType.Power;
            case EBattleElementType.Bio:
                return EBattleElementType.Plasma;
            case EBattleElementType.Chemical:
                return EBattleElementType.Bio;
            case EBattleElementType.Electrical:
                return EBattleElementType.Network;  // 광자와 공허는 서로 강함
            case EBattleElementType.Network:
                return EBattleElementType.Electrical;  // 광자와 공허는 서로 강함
            default:
                return EBattleElementType.None;
        }
    }

    /// <summary>
    /// 디버그용 속성 상성 테이블 출력
    /// </summary>
    public static void PrintElementTable()
    {
        Debug.Log("=== Element Advantage Table ===");
        Debug.Log("Cooling → Overheat (x1.5)");
        Debug.Log("Overheat → Nano (x1.5)");
        Debug.Log("Nano → Regeneration (x1.5)");
        Debug.Log("Regeneration → Cooling (x1.5)");
        Debug.Log("Photon ↔ Void (x1.5)");
        Debug.Log("Reverse relations: x0.77 (1/1.3)");
    }
}