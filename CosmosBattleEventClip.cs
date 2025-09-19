using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Battle Event Clip - TimelineDataSO의 CustomEvent로 변환되어 사용
/// </summary>
[Serializable]
public class CosmosBattleEventClip
{
    #region Event Type Definition
    public enum EventType
    {
        Hit,        // 데미지 적용
        Camera,     // 카메라 전환
        Move,       // 위치 이동
        Tween       // 트윈 효과
    }

    public enum HitType
    {
        RealHit,    // 실제 데미지
        EffectOnly  // 효과만
    }

    public enum TweenType
    {
        Position,
        Scale,
        Rotate,
        Shake,
        Color
    }

    public enum TargetType
    {
        Caster,         // 시전자
        MainTarget,     // 메인 타겟
        AllTargets,     // 모든 타겟
        AllEnemies,     // 모든 적
        AllAllies,      // 모든 아군
        Specific        // 특정 대상
    }

    public enum MovePosition
    {
        Original,       // 원위치
        Center,         // 중앙
        Front,          // 전방
        Back,           // 후방
        CasterFront,    // 시전자 앞
        TargetFront,    // 타겟 앞
        Custom          // 커스텀 위치
    }
    #endregion

    #region Common Fields
    [Header("Common Settings")]
    public EventType eventType = EventType.Hit;
    public TargetType targetType = TargetType.MainTarget;
    public float duration = 0.5f;
    public float delayBefore = 0f;
    #endregion

    #region Hit Event Fields
    [Header("Hit Event")]
    public HitType hitType = HitType.RealHit;
    [Range(0f, 100f)]
    public float damagePercent = 100f;  // 총 데미지의 비율
    public string hitEffectKey = "";    // 히트 이펙트 Addressable 키
    public Vector3 hitEffectOffset;
    #endregion

    #region Camera Event Fields
    [Header("Camera Event")]
    public string cameraName = "";
    public float blendTime = 0.5f;
    public bool returnToPrevious = true;
    #endregion

    #region Move Event Fields
    [Header("Move Event")]
    public MovePosition movePosition = MovePosition.Center;
    public Vector3 customPosition;
    public bool instant = false;  // 즉시 이동 여부
    #endregion

    #region Tween Event Fields
    [Header("Tween Event")]
    public TweenType tweenType = TweenType.Scale;
    public Ease easeType = Ease.OutQuad;
    public Vector3 tweenValue = Vector3.one;
    public Color tweenColor = Color.white;
    public float tweenIntensity = 1f;  // Shake 강도 등
    public int tweenVibrato = 10;      // Shake 진동 횟수
    public bool tweenLoop = false;
    public int tweenLoopCount = -1;    // -1은 무한
    #endregion

    #region Conversion Methods

    /// <summary>
    /// TimelineDataSO.CustomEvent로 변환
    /// </summary>
    public BattleCharacterSystem.Timeline.TimelineDataSO.CustomEvent ToCustomEvent(float startTime)
    {
        var customEvent = new BattleCharacterSystem.Timeline.TimelineDataSO.CustomEvent
        {
            triggerTime = startTime + delayBefore,
            eventName = $"Battle_{eventType}",
            parameters = new Dictionary<string, string>()
        };

        // 공통 파라미터
        customEvent.parameters["type"] = eventType.ToString();
        customEvent.parameters["target"] = targetType.ToString();
        customEvent.parameters["duration"] = duration.ToString();

        // 이벤트 타입별 파라미터
        switch (eventType)
        {
            case EventType.Hit:
                customEvent.parameters["damage"] = damagePercent.ToString();
                customEvent.parameters["hitType"] = hitType.ToString();
                customEvent.parameters["effect"] = hitEffectKey;
                customEvent.parameters["offsetX"] = hitEffectOffset.x.ToString();
                customEvent.parameters["offsetY"] = hitEffectOffset.y.ToString();
                customEvent.parameters["offsetZ"] = hitEffectOffset.z.ToString();
                break;

            case EventType.Camera:
                customEvent.parameters["name"] = cameraName;
                customEvent.parameters["blend"] = blendTime.ToString();
                customEvent.parameters["return"] = returnToPrevious.ToString();
                break;

            case EventType.Move:
                customEvent.parameters["position"] = movePosition.ToString();
                customEvent.parameters["instant"] = instant.ToString();
                if (movePosition == MovePosition.Custom)
                {
                    customEvent.parameters["customX"] = customPosition.x.ToString();
                    customEvent.parameters["customY"] = customPosition.y.ToString();
                    customEvent.parameters["customZ"] = customPosition.z.ToString();
                }
                break;

            case EventType.Tween:
                customEvent.parameters["tweenType"] = tweenType.ToString();
                customEvent.parameters["ease"] = easeType.ToString();
                customEvent.parameters["valueX"] = tweenValue.x.ToString();
                customEvent.parameters["valueY"] = tweenValue.y.ToString();
                customEvent.parameters["valueZ"] = tweenValue.z.ToString();
                customEvent.parameters["color"] = ColorUtility.ToHtmlStringRGB(tweenColor);
                customEvent.parameters["intensity"] = tweenIntensity.ToString();
                customEvent.parameters["vibrato"] = tweenVibrato.ToString();
                customEvent.parameters["loop"] = tweenLoop.ToString();
                customEvent.parameters["loopCount"] = tweenLoopCount.ToString();
                break;
        }

        return customEvent;
    }

    /// <summary>
    /// CustomEvent에서 복원
    /// </summary>
    public static CosmosBattleEventClip FromCustomEvent(BattleCharacterSystem.Timeline.TimelineDataSO.CustomEvent customEvent)
    {
        if (customEvent == null || !customEvent.eventName.StartsWith("Battle_"))
            return null;

        var clip = new CosmosBattleEventClip();
        var param = customEvent.parameters;

        // 이벤트 타입 파싱
        string eventTypeStr = customEvent.eventName.Replace("Battle_", "");
        if (Enum.TryParse<EventType>(eventTypeStr, out var eventType))
        {
            clip.eventType = eventType;
        }

        // 공통 파라미터
        if (param.ContainsKey("target") && Enum.TryParse<TargetType>(param["target"], out var targetType))
            clip.targetType = targetType;
        if (param.ContainsKey("duration") && float.TryParse(param["duration"], out var duration))
            clip.duration = duration;

        // 타입별 파라미터 복원
        switch (clip.eventType)
        {
            case EventType.Hit:
                if (param.ContainsKey("damage") && float.TryParse(param["damage"], out var damage))
                    clip.damagePercent = damage;
                if (param.ContainsKey("hitType") && Enum.TryParse<HitType>(param["hitType"], out var hitType))
                    clip.hitType = hitType;
                clip.hitEffectKey = param.GetValueOrDefault("effect", "");
                break;

            case EventType.Camera:
                clip.cameraName = param.GetValueOrDefault("name", "Main");
                if (param.ContainsKey("blend") && float.TryParse(param["blend"], out var blend))
                    clip.blendTime = blend;
                if (param.ContainsKey("return") && bool.TryParse(param["return"], out var ret))
                    clip.returnToPrevious = ret;
                break;

            case EventType.Move:
                if (param.ContainsKey("position") && Enum.TryParse<MovePosition>(param["position"], out var movePos))
                    clip.movePosition = movePos;
                if (param.ContainsKey("instant") && bool.TryParse(param["instant"], out var inst))
                    clip.instant = inst;
                break;

            case EventType.Tween:
                if (param.ContainsKey("tweenType") && Enum.TryParse<TweenType>(param["tweenType"], out var tween))
                    clip.tweenType = tween;
                if (param.ContainsKey("ease") && Enum.TryParse<Ease>(param["ease"], out var ease))
                    clip.easeType = ease;
                break;
        }

        return clip;
    }

    #endregion
}