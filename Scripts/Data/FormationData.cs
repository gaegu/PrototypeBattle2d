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

    [Header("위치 설정")]
    [SerializeField]
    public FormationPosition[] positions = new FormationPosition[5];

    [Header("간격 조정")]
    public float frontBackDistance = 5f;
    public float characterSpacing = 3f;

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
        if (positions == null || positions.Length != 5)
        {
            positions = new FormationPosition[5];
        }

        int sideMultiplier = isAlly ? 1 : -1;
        Vector3 basePosition = isAlly ? new Vector3(5, 0, 0) : new Vector3(-5, 0, 0);

        switch (formationType)
        {
            case FormationType.Offensive:
                SetOffensiveFormation(basePosition, sideMultiplier);
                break;
            case FormationType.Defensive:
                SetDefensiveFormation(basePosition, sideMultiplier);
                break;
            case FormationType.OffensiveBalance:
                SetOffensiveBalanceFormation(basePosition, sideMultiplier);
                break;
            case FormationType.DefensiveBalance:
                SetDefensiveBalanceFormation(basePosition, sideMultiplier);
                break;
        }
    }

    private void SetOffensiveFormation(Vector3 basePos, int side)
    {
        positions[0] = new FormationPosition(0, true,
            basePos + new Vector3(frontBackDistance * side, 0, 0),
            Vector3.zero);

        positions[1] = new FormationPosition(1, false,
            basePos + new Vector3(0, 0, characterSpacing * 1.5f),
            Vector3.zero);

        positions[2] = new FormationPosition(2, false,
            basePos + new Vector3(0, 0, characterSpacing * 0.5f),
            Vector3.zero);

        positions[3] = new FormationPosition(3, false,
            basePos + new Vector3(0, 0, -characterSpacing * 0.5f),
           Vector3.zero);

        positions[4] = new FormationPosition(4, false,
            basePos + new Vector3(0, 0, -characterSpacing * 1.5f),
            Vector3.zero);
    }

    private void SetDefensiveFormation(Vector3 basePos, int side)
    {
        positions[0] = new FormationPosition(0, true,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing * 1.5f),
            Vector3.zero);

        positions[1] = new FormationPosition(1, true,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing * 0.5f),
           Vector3.zero);

        positions[2] = new FormationPosition(2, false,
            basePos + new Vector3(0, 0, 0),
           Vector3.zero);

        positions[3] = new FormationPosition(3, true,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing * 0.5f),
           Vector3.zero);

        positions[4] = new FormationPosition(4, true,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing * 1.5f),
            Vector3.zero);
    }

    private void SetOffensiveBalanceFormation(Vector3 basePos, int side)
    {
        positions[0] = new FormationPosition(0, true,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing * 0.75f),
            Vector3.zero);

        positions[1] = new FormationPosition(1, true,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing * 0.75f),
            Vector3.zero);

        positions[2] = new FormationPosition(2, false,
            basePos + new Vector3(0, 0, characterSpacing * 1.5f),
            Vector3.zero);

        positions[3] = new FormationPosition(3, false,
            basePos + new Vector3(0, 0, 0),
            Vector3.zero);

        positions[4] = new FormationPosition(4, false,
            basePos + new Vector3(0, 0, -characterSpacing * 1.5f),
           Vector3.zero);
    }

    private void SetDefensiveBalanceFormation(Vector3 basePos, int side)
    {
        positions[0] = new FormationPosition(0, true,
            basePos + new Vector3(frontBackDistance * side, 0, characterSpacing * 1.2f),
            Vector3.zero);

        positions[1] = new FormationPosition(1, true,
            basePos + new Vector3(frontBackDistance * side, 0, 0),
            Vector3.zero);

        positions[2] = new FormationPosition(2, true,
            basePos + new Vector3(frontBackDistance * side, 0, -characterSpacing * 1.2f),
            Vector3.zero);

        positions[3] = new FormationPosition(3, false,
            basePos + new Vector3(0, 0, characterSpacing * 0.75f),
            Vector3.zero);

        positions[4] = new FormationPosition(4, false,
            basePos + new Vector3(0, 0, -characterSpacing * 0.75f),
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