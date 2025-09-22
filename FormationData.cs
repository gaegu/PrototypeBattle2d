using System;
using UnityEngine;

/// <summary>
/// 진형 데이터 - ScriptableObject로 저장
/// 파일명과 클래스명이 일치해야 함
/// </summary>
[CreateAssetMenu(fileName = "FormationData", menuName = "Battle/FormationData", order = 1)]
[Serializable]
public class FormationData : ScriptableObject
{
    [Header("진형 기본 정보")]
    public FormationType formationType;
    public string formationName;
    [TextArea(2, 4)]
    public string description;

    [Header("진형 레벨")]
    [Range(1, 10)]
    public int level = 1;

    [Header("버프 효과")]
    [Range(0f, 100f)]
    public float baseBuffPercentage = 10f;

    [Header("아군 위치 설정")]
    [SerializeField]
    public FormationPosition[] allyPositions = new FormationPosition[5];

    [Header("적군 위치 설정")]
    [SerializeField]
    public FormationPosition[] enemyPositions = new FormationPosition[5];

    [Header("간격 조정")]
    public float baseDistance = 5f;  // 중앙(0,0,0)으로부터의 기본 거리 추가
    public float frontBackDistance = 5f;
    public float characterSpacing = 3f;

    [Header("커스텀 공격 위치")]
    public bool useCustomAttackPosition = false;
    public Vector3[] customAttackPositions = new Vector3[5];

    /// <summary>
    /// 실제 버프 효과 계산
    /// </summary>
    public float GetActualBuffPercentage()
    {
        return baseBuffPercentage + (level - 1) * 2f;
    }

    /// <summary>
    /// 진형 위치 초기화
    /// </summary>
    public void InitializePositions(bool isAlly)
    {
        FormationPosition[] targetPositions = isAlly ? allyPositions : enemyPositions;

        if (targetPositions == null || targetPositions.Length != 5)
        {
            targetPositions = new FormationPosition[5];
            if (isAlly)
                allyPositions = targetPositions;
            else
                enemyPositions = targetPositions;
        }

        int sideMultiplier = isAlly ? 1 : -1;
        Vector3 basePosition = isAlly ? new Vector3(baseDistance, 0, 0) : new Vector3(-baseDistance, 0, 0);

        switch (formationType)
        {
            case FormationType.Offensive:
                SetOffensiveFormation(targetPositions, basePosition, sideMultiplier);
                break;
            case FormationType.Defensive:
                SetDefensiveFormation(targetPositions, basePosition, sideMultiplier);
                break;
            case FormationType.OffensiveBalance:
                SetOffensiveBalanceFormation(targetPositions, basePosition, sideMultiplier);
                break;
            case FormationType.DefensiveBalance:
                SetDefensiveBalanceFormation(targetPositions, basePosition, sideMultiplier);
                break;
        }
    }

    /// <summary>
    /// 공격 위치 가져오기
    /// </summary>
    public Vector3 GetAttackPosition(int slotIndex, bool isAlly)
    {
        if (useCustomAttackPosition && slotIndex >= 0 && slotIndex < 5)
        {
            return customAttackPositions[slotIndex];
        }
        return Vector3.zero;
    }

    private void SetOffensiveFormation(FormationPosition[] positions, Vector3 basePos, int side)
    {

        // 앞열 4명
        positions[0] = new FormationPosition(0, false,
                basePos + new Vector3(frontBackDistance * side, 0, characterSpacing * 2),
                Vector3.zero);

        positions[1] = new FormationPosition(1, false,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing),
            Vector3.zero);

        positions[2] = new FormationPosition(2, false,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing),
            Vector3.zero);

        positions[3] = new FormationPosition(3, false,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing * 2),
            Vector3.zero);

        // 뒷열 1명
        positions[4] = new FormationPosition(4, true,
            basePos + new Vector3(0, 0, 0),
            Vector3.zero);
    }


    private void SetDefensiveFormation(FormationPosition[] positions, Vector3 basePos, int side)
    {  
        // 앞열 1명
        positions[0] = new FormationPosition(0, false,  // isFrontRow = true
            basePos + new Vector3(frontBackDistance * side, 0, 0),
            Vector3.zero);

        // 뒷열 4명
        positions[1] = new FormationPosition(1, true,  // isFrontRow = false
            basePos + new Vector3(0, 0, characterSpacing * 2),
            Vector3.zero);

        positions[2] = new FormationPosition(2, true,
            basePos + new Vector3(0, 0, characterSpacing),
            Vector3.zero);

        positions[3] = new FormationPosition(3, true,
            basePos + new Vector3(0, 0, -characterSpacing),
            Vector3.zero);

        positions[4] = new FormationPosition(4, true,
            basePos + new Vector3(0, 0, -characterSpacing * 2),
            Vector3.zero);
    }

    private void SetOffensiveBalanceFormation(FormationPosition[] positions, Vector3 basePos, int side)
    { 
        // 뒷열 3명
        positions[0] = new FormationPosition(0, false,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing * 2),
            Vector3.zero);

        positions[1] = new FormationPosition(1, false,
            basePos + new Vector3(frontBackDistance * side, 0, 0),
            Vector3.zero);

        positions[2] = new FormationPosition(2, false,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing * 2),
            Vector3.zero);

        // 앞열 2명
        positions[3] = new FormationPosition(3, true,
            basePos + new Vector3(0, 0, characterSpacing),
            Vector3.zero);

        positions[4] = new FormationPosition(4, true,
            basePos + new Vector3(0, 0, -characterSpacing),
            Vector3.zero);

    }

    private void SetDefensiveBalanceFormation(FormationPosition[] positions, Vector3 basePos, int side)
    {
        // 뒷열 2명
        positions[0] = new FormationPosition(0, false,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing),
            Vector3.zero);

        positions[1] = new FormationPosition(1, false,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing),
            Vector3.zero);

        // 앞열 3명
        positions[2] = new FormationPosition(2, true,
            basePos + new Vector3(0, 0, characterSpacing * 2),
            Vector3.zero);

        positions[3] = new FormationPosition(3, true,
            basePos + new Vector3(0, 0, 0),
            Vector3.zero);

        positions[4] = new FormationPosition(4, true,
            basePos + new Vector3(0, 0, -characterSpacing * 2),
            Vector3.zero);
       
    }
}

/// <summary>
/// 진형 위치 정보 - Serializable로 직렬화 가능
/// </summary>
[Serializable]
public class FormationPosition
{
    public int slotIndex;
    public bool isFrontRow;
    public Vector3 standPosition;
    public Vector3 attackPosition;

    public FormationPosition()
    {
        slotIndex = 0;
        isFrontRow = false;
        standPosition = Vector3.zero;
        attackPosition = Vector3.zero;
    }

    public FormationPosition(int slot, bool front, Vector3 stand, Vector3 attack)
    {
        slotIndex = slot;
        isFrontRow = front;
        standPosition = stand;
        attackPosition = attack;
    }
}