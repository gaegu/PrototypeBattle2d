using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class CosmosCustomClip : PlayableAsset
{
    public CustomActionType actionType = CustomActionType.None;

    [SerializeField] public CustomActionData actionData = new CustomActionData();

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CosmosCustomBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.actionType = actionType;
        behaviour.actionData = actionData;

        return playable;
    }
}

/// <summary>
/// 모든 액션에서 공용으로 사용할 데이터 클래스
/// </summary>
[Serializable]
public class CustomActionData
{
    public TargetPositionPreset positionPreset = TargetPositionPreset.Center;
    public Vector3 customPosition = Vector3.zero;

    public KnockbackIntensity knockbackIntensity = KnockbackIntensity.Normal;
    public float intensity = 1f;  // 넉백 강도, 쉐이크 강도 등 공용

    public float duration = 0.3f;  // 모든 액션의 지속시간 공용

    public VisibilityMode visibilityMode = VisibilityMode.Show;
    public FlashColorPreset flashColorPreset = FlashColorPreset.White;
    public Color customColor = Color.white;

    public float multiplier = 1f;  // 데미지 배율 등
    public string stringParam = "";  // 버프ID, 기타 문자열 파라미터

    // 필요시 확장 가능한 공용 필드들
    public bool boolParam = false;
    public int intParam = 0;
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
}