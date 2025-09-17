using UnityEngine;

[CreateAssetMenu(fileName = "TimelineConfig", menuName = "Battle/TimelineConfig")]
public class BattleTimelineConfig : ScriptableObject
{
    [Header("슬롯 개수 설정")]
    public int maxCurrentRoundSlots = 10;
    public int maxPreviewSlots = 3;
    public int maxDeadSlots = 5;
    public int maxCounterSlots = 2;

    [Header("크기 설정")]
    [Range(0.5f, 2f)] public float currentTurnScale = 1.2f;
    [Range(0.5f, 2f)] public float normalScale = 1.0f;
    [Range(0.5f, 2f)] public float previewScale = 0.9f;
    [Range(0.5f, 2f)] public float deadScale = 0.5f;
    [Range(0.5f, 2f)] public float counterScale = 0.9f;

    [Header("투명도 설정")]
    [Range(0f, 1f)] public float normalAlpha = 1.0f;
    [Range(0f, 1f)] public float previewAlpha = 0.9f;
    [Range(0f, 1f)] public float deadAlpha = 0.5f;
    [Range(0f, 1f)] public float breakAlpha = 0.7f;

    [Header("애니메이션 시간")]
    public float moveAnimDuration = 0.3f;
    public float fadeAnimDuration = 0.2f;
    public float breakWaitTime = 0.5f;
    public float counterPopDuration = 0.1f;
    public float pulseSpeed = 2f;

    [Header("테두리 색상")]
    public Color allyBorderColor = new Color(0.2f, 0.4f, 1f, 1f);      // 파란색
    public Color enemyBorderColor = new Color(1f, 0.2f, 0.2f, 1f);     // 빨간색
    public Color bossBorderColor = new Color(0.8f, 0.2f, 1f, 1f);      // 보라색
    public Color counterBorderColor = new Color(1f, 0.9f, 0.2f, 1f);   // 노란색

    [Header("브레이크 설정")]
    public float breakRotationAngle = 180f;

    [Header("UI 레이아웃")]
    public float slotSpacing = 110f;
    public float dividerWidth = 50f;
}