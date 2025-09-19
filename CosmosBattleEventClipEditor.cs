#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using DG.Tweening;
using BattleCharacterSystem.Timeline;
using System.Collections.Generic;

/// <summary>
/// TimelineDataSO의 Battle Event 편집기
/// </summary>
[CustomEditor(typeof(TimelineDataSO))]
public class TimelineDataSOWithBattleEvents : Editor
{
    private bool showBattleEvents = false;
    private Dictionary<TimelineDataSO.CustomEvent, bool> eventFoldouts = new Dictionary<TimelineDataSO.CustomEvent, bool>();

    public override void OnInspectorGUI()
    {
        // 기본 Inspector 표시
        DrawDefaultInspector();

        var timeline = target as TimelineDataSO;
        if (timeline == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Battle Events 섹션
        showBattleEvents = EditorGUILayout.Foldout(showBattleEvents, "Battle Events", true);

        if (showBattleEvents)
        {
            DrawBattleEventsSection(timeline);
        }
    }

    private void DrawBattleEventsSection(TimelineDataSO timeline)
    {
        EditorGUI.indentLevel++;

        // 총 데미지 퍼센트 계산
        float totalDamagePercent = CalculateTotalDamagePercent(timeline);

        // 총 데미지 표시
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Total Damage:", GUILayout.Width(100));

        var oldColor = GUI.color;
        if (totalDamagePercent > 100f)
            GUI.color = Color.red;
        else if (totalDamagePercent == 100f)
            GUI.color = Color.green;
        else
            GUI.color = Color.yellow;

        EditorGUILayout.LabelField($"{totalDamagePercent:F1}%", EditorStyles.boldLabel);
        GUI.color = oldColor;
        EditorGUILayout.EndHorizontal();

        if (totalDamagePercent > 100f)
        {
            EditorGUILayout.HelpBox("Total damage exceeds 100%! Events will auto-adjust at runtime.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // Battle Event 리스트
        var battleEvents = GetBattleEvents(timeline);

        if (battleEvents.Count == 0)
        {
            EditorGUILayout.LabelField("No battle events", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            foreach (var customEvent in battleEvents)
            {
                DrawBattleEvent(timeline, customEvent);
            }
        }

        EditorGUILayout.Space();

        // 새 이벤트 추가 버튼들
        EditorGUILayout.LabelField("Add New Battle Event:");
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Hit"))
            AddBattleEvent(timeline, CosmosBattleEventClip.EventType.Hit);
        if (GUILayout.Button("+ Camera"))
            AddBattleEvent(timeline, CosmosBattleEventClip.EventType.Camera);
        if (GUILayout.Button("+ Move"))
            AddBattleEvent(timeline, CosmosBattleEventClip.EventType.Move);
        if (GUILayout.Button("+ Tween"))
            AddBattleEvent(timeline, CosmosBattleEventClip.EventType.Tween);

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
    }

    private void DrawBattleEvent(TimelineDataSO timeline, TimelineDataSO.CustomEvent customEvent)
    {
        if (!eventFoldouts.ContainsKey(customEvent))
            eventFoldouts[customEvent] = false;

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        // Foldout
        eventFoldouts[customEvent] = EditorGUILayout.Foldout(
            eventFoldouts[customEvent],
            $"{customEvent.triggerTime:F2}s - {customEvent.eventName.Replace("Battle_", "")}",
            true
        );

        // 삭제 버튼
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            Undo.RecordObject(timeline, "Remove Battle Event");
            timeline.customEvents.Remove(customEvent);
            eventFoldouts.Remove(customEvent);
            EditorUtility.SetDirty(timeline);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();

        if (eventFoldouts[customEvent])
        {
            EditorGUI.indentLevel++;
            DrawBattleEventDetails(timeline, customEvent);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBattleEventDetails(TimelineDataSO timeline, TimelineDataSO.CustomEvent customEvent)
    {
        var param = customEvent.parameters;
        string eventType = customEvent.eventName.Replace("Battle_", "");

        EditorGUI.BeginChangeCheck();

        // 공통 파라미터
        customEvent.triggerTime = EditorGUILayout.FloatField("Trigger Time", customEvent.triggerTime);

        if (param.ContainsKey("duration"))
        {
            float duration = float.Parse(param["duration"]);
            duration = EditorGUILayout.FloatField("Duration", duration);
            param["duration"] = duration.ToString();
        }

        if (param.ContainsKey("target"))
        {
            var targetType = (CosmosBattleEventClip.TargetType)System.Enum.Parse(
                typeof(CosmosBattleEventClip.TargetType), param["target"]);
            targetType = (CosmosBattleEventClip.TargetType)EditorGUILayout.EnumPopup("Target", targetType);
            param["target"] = targetType.ToString();
        }

        // 타입별 파라미터
        switch (eventType)
        {
            case "Hit":
                DrawHitEventParams(param);
                break;
            case "Camera":
                DrawCameraEventParams(param);
                break;
            case "Move":
                DrawMoveEventParams(param);
                break;
            case "Tween":
                DrawTweenEventParams(param);
                break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(timeline);
        }
    }

    private void DrawHitEventParams(Dictionary<string, string> param)
    {
        EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);

        if (param.ContainsKey("hitType"))
        {
            var hitType = (CosmosBattleEventClip.HitType)System.Enum.Parse(
                typeof(CosmosBattleEventClip.HitType), param["hitType"]);
            hitType = (CosmosBattleEventClip.HitType)EditorGUILayout.EnumPopup("Hit Type", hitType);
            param["hitType"] = hitType.ToString();

            if (hitType == CosmosBattleEventClip.HitType.RealHit && param.ContainsKey("damage"))
            {
                float damage = float.Parse(param["damage"]);
                damage = EditorGUILayout.Slider("Damage %", damage, 0f, 100f);
                param["damage"] = damage.ToString();

                EditorGUILayout.HelpBox($"This hit deals {damage:F1}% of total damage", MessageType.Info);
            }
        }

        if (param.ContainsKey("effect"))
        {
            param["effect"] = EditorGUILayout.TextField("Hit Effect Key", param["effect"]);
        }
    }

    private void DrawCameraEventParams(Dictionary<string, string> param)
    {
        EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);

        if (param.ContainsKey("name"))
        {
            param["name"] = EditorGUILayout.TextField("Camera Name", param["name"]);
        }

        if (param.ContainsKey("blend"))
        {
            float blend = float.Parse(param["blend"]);
            blend = EditorGUILayout.FloatField("Blend Time", blend);
            param["blend"] = blend.ToString();
        }

        if (param.ContainsKey("return"))
        {
            bool returnToPrev = bool.Parse(param["return"]);
            returnToPrev = EditorGUILayout.Toggle("Return to Previous", returnToPrev);
            param["return"] = returnToPrev.ToString();
        }
    }

    private void DrawMoveEventParams(Dictionary<string, string> param)
    {
        EditorGUILayout.LabelField("Move Settings", EditorStyles.boldLabel);

        if (param.ContainsKey("position"))
        {
            var position = (CosmosBattleEventClip.MovePosition)System.Enum.Parse(
                typeof(CosmosBattleEventClip.MovePosition), param["position"]);
            position = (CosmosBattleEventClip.MovePosition)EditorGUILayout.EnumPopup("Position", position);
            param["position"] = position.ToString();

            if (position == CosmosBattleEventClip.MovePosition.Custom)
            {
                Vector3 customPos = Vector3.zero;
                if (param.ContainsKey("customX") && param.ContainsKey("customY") && param.ContainsKey("customZ"))
                {
                    customPos.x = float.Parse(param["customX"]);
                    customPos.y = float.Parse(param["customY"]);
                    customPos.z = float.Parse(param["customZ"]);
                }

                customPos = EditorGUILayout.Vector3Field("Custom Position", customPos);

                param["customX"] = customPos.x.ToString();
                param["customY"] = customPos.y.ToString();
                param["customZ"] = customPos.z.ToString();
            }
        }

        if (param.ContainsKey("instant"))
        {
            bool instant = bool.Parse(param["instant"]);
            instant = EditorGUILayout.Toggle("Instant Move", instant);
            param["instant"] = instant.ToString();
        }
    }

    private void DrawTweenEventParams(Dictionary<string, string> param)
    {
        EditorGUILayout.LabelField("Tween Settings", EditorStyles.boldLabel);

        if (param.ContainsKey("tweenType"))
        {
            var tweenType = (CosmosBattleEventClip.TweenType)System.Enum.Parse(
                typeof(CosmosBattleEventClip.TweenType), param["tweenType"]);
            tweenType = (CosmosBattleEventClip.TweenType)EditorGUILayout.EnumPopup("Tween Type", tweenType);
            param["tweenType"] = tweenType.ToString();

            // Tween 타입별 추가 파라미터
            switch (tweenType)
            {
                case CosmosBattleEventClip.TweenType.Shake:
                    if (param.ContainsKey("intensity"))
                    {
                        float intensity = float.Parse(param["intensity"]);
                        intensity = EditorGUILayout.FloatField("Intensity", intensity);
                        param["intensity"] = intensity.ToString();
                    }
                    if (param.ContainsKey("vibrato"))
                    {
                        int vibrato = int.Parse(param["vibrato"]);
                        vibrato = EditorGUILayout.IntField("Vibrato", vibrato);
                        param["vibrato"] = vibrato.ToString();
                    }
                    break;

                case CosmosBattleEventClip.TweenType.Scale:
                case CosmosBattleEventClip.TweenType.Position:
                case CosmosBattleEventClip.TweenType.Rotate:
                    Vector3 tweenValue = Vector3.one;
                    if (param.ContainsKey("valueX") && param.ContainsKey("valueY") && param.ContainsKey("valueZ"))
                    {
                        tweenValue.x = float.Parse(param["valueX"]);
                        tweenValue.y = float.Parse(param["valueY"]);
                        tweenValue.z = float.Parse(param["valueZ"]);
                    }
                    tweenValue = EditorGUILayout.Vector3Field("Value", tweenValue);
                    param["valueX"] = tweenValue.x.ToString();
                    param["valueY"] = tweenValue.y.ToString();
                    param["valueZ"] = tweenValue.z.ToString();
                    break;

                case CosmosBattleEventClip.TweenType.Color:
                    if (param.ContainsKey("color"))
                    {
                        Color color = Color.white;
                        ColorUtility.TryParseHtmlString("#" + param["color"], out color);
                        color = EditorGUILayout.ColorField("Color", color);
                        param["color"] = ColorUtility.ToHtmlStringRGB(color);
                    }
                    break;
            }
        }

        if (param.ContainsKey("ease"))
        {
            var ease = (Ease)System.Enum.Parse(typeof(Ease), param["ease"]);
            ease = (Ease)EditorGUILayout.EnumPopup("Ease", ease);
            param["ease"] = ease.ToString();
        }

        if (param.ContainsKey("loop"))
        {
            bool loop = bool.Parse(param["loop"]);
            loop = EditorGUILayout.Toggle("Loop", loop);
            param["loop"] = loop.ToString();

            if (loop && param.ContainsKey("loopCount"))
            {
                int loopCount = int.Parse(param["loopCount"]);
                loopCount = EditorGUILayout.IntField("Loop Count (-1 = infinite)", loopCount);
                param["loopCount"] = loopCount.ToString();
            }
        }
    }

    private List<TimelineDataSO.CustomEvent> GetBattleEvents(TimelineDataSO timeline)
    {
        if (timeline.customEvents == null) return new List<TimelineDataSO.CustomEvent>();

        return timeline.customEvents
            .Where(e => e.eventName.StartsWith("Battle_"))
            .OrderBy(e => e.triggerTime)
            .ToList();
    }

    private float CalculateTotalDamagePercent(TimelineDataSO timeline)
    {
        if (timeline.customEvents == null) return 0f;

        float total = 0f;
        foreach (var evt in timeline.customEvents)
        {
            if (evt.eventName == "Battle_Hit" &&
                evt.parameters != null &&
                evt.parameters.ContainsKey("damage") &&
                evt.parameters.ContainsKey("hitType"))
            {
                if (evt.parameters["hitType"] == "RealHit" &&
                    float.TryParse(evt.parameters["damage"], out float damage))
                {
                    total += damage;
                }
            }
        }

        return total;
    }

    private void AddBattleEvent(TimelineDataSO timeline, CosmosBattleEventClip.EventType eventType)
    {
        var battleEvent = new CosmosBattleEventClip
        {
            eventType = eventType,
            targetType = CosmosBattleEventClip.TargetType.MainTarget,
            duration = 0.5f
        };

        // 타입별 기본값 설정
        switch (eventType)
        {
            case CosmosBattleEventClip.EventType.Hit:
                battleEvent.hitType = CosmosBattleEventClip.HitType.RealHit;
                battleEvent.damagePercent = Mathf.Max(0, 100f - CalculateTotalDamagePercent(timeline));
                battleEvent.hitEffectKey = "Hit_Default";
                break;

            case CosmosBattleEventClip.EventType.Camera:
                battleEvent.cameraName = "BattleCamera_Main";
                battleEvent.blendTime = 0.5f;
                battleEvent.returnToPrevious = true;
                break;

            case CosmosBattleEventClip.EventType.Move:
                battleEvent.movePosition = CosmosBattleEventClip.MovePosition.Center;
                battleEvent.instant = false;
                break;

            case CosmosBattleEventClip.EventType.Tween:
                battleEvent.tweenType = CosmosBattleEventClip.TweenType.Shake;
                battleEvent.tweenIntensity = 0.5f;
                battleEvent.easeType = Ease.InOutQuad;
                break;
        }

        // 현재 타임라인의 마지막 이벤트 시간 찾기
        float lastEventTime = 0f;
        if (timeline.customEvents != null && timeline.customEvents.Count > 0)
        {
            lastEventTime = timeline.customEvents.Max(e => e.triggerTime);
        }

        var customEvent = battleEvent.ToCustomEvent(lastEventTime + 0.1f);

        if (timeline.customEvents == null)
            timeline.customEvents = new List<TimelineDataSO.CustomEvent>();

        Undo.RecordObject(timeline, $"Add Battle {eventType} Event");
        timeline.customEvents.Add(customEvent);
        EditorUtility.SetDirty(timeline);

        // Foldout 상태 추가
        eventFoldouts[customEvent] = true;
    }
}

/// <summary>
/// Battle Event Quick Add Window
/// </summary>
public class BattleEventQuickAddWindow : EditorWindow
{
    private TimelineDataSO targetTimeline;
    private CosmosBattleEventClip.EventType selectedType = CosmosBattleEventClip.EventType.Hit;
    private float triggerTime = 0.5f;
    private CosmosBattleEventClip tempEvent;

    public static void ShowWindow(TimelineDataSO timeline)
    {
        var window = GetWindow<BattleEventQuickAddWindow>(true, "Add Battle Event", true);
        window.targetTimeline = timeline;
        window.tempEvent = new CosmosBattleEventClip();
        window.maxSize = new Vector2(400, 500);
        window.minSize = new Vector2(300, 400);
        window.ShowModal();
    }

    private void OnGUI()
    {
        if (targetTimeline == null)
        {
            Close();
            return;
        }

        EditorGUILayout.LabelField($"Timeline: {targetTimeline.name}", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        selectedType = (CosmosBattleEventClip.EventType)EditorGUILayout.EnumPopup("Event Type", selectedType);
        triggerTime = EditorGUILayout.FloatField("Trigger Time", triggerTime);

        EditorGUILayout.Space();

        tempEvent.eventType = selectedType;
        tempEvent.duration = EditorGUILayout.FloatField("Duration", tempEvent.duration);
        tempEvent.targetType = (CosmosBattleEventClip.TargetType)EditorGUILayout.EnumPopup("Target", tempEvent.targetType);

        EditorGUILayout.Space();

        // 타입별 설정
        switch (selectedType)
        {
            case CosmosBattleEventClip.EventType.Hit:
                EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
                tempEvent.hitType = (CosmosBattleEventClip.HitType)EditorGUILayout.EnumPopup("Hit Type", tempEvent.hitType);
                if (tempEvent.hitType == CosmosBattleEventClip.HitType.RealHit)
                {
                    tempEvent.damagePercent = EditorGUILayout.Slider("Damage %", tempEvent.damagePercent, 0f, 100f);
                }
                tempEvent.hitEffectKey = EditorGUILayout.TextField("Effect Key", tempEvent.hitEffectKey ?? "");
                tempEvent.hitEffectOffset = EditorGUILayout.Vector3Field("Effect Offset", tempEvent.hitEffectOffset);
                break;

            case CosmosBattleEventClip.EventType.Camera:
                EditorGUILayout.LabelField("Camera Settings", EditorStyles.boldLabel);
                tempEvent.cameraName = EditorGUILayout.TextField("Camera Name", tempEvent.cameraName ?? "Main");
                tempEvent.blendTime = EditorGUILayout.FloatField("Blend Time", tempEvent.blendTime);
                tempEvent.returnToPrevious = EditorGUILayout.Toggle("Return to Previous", tempEvent.returnToPrevious);
                break;

            case CosmosBattleEventClip.EventType.Move:
                EditorGUILayout.LabelField("Move Settings", EditorStyles.boldLabel);
                tempEvent.movePosition = (CosmosBattleEventClip.MovePosition)EditorGUILayout.EnumPopup("Position", tempEvent.movePosition);
                if (tempEvent.movePosition == CosmosBattleEventClip.MovePosition.Custom)
                {
                    tempEvent.customPosition = EditorGUILayout.Vector3Field("Custom Position", tempEvent.customPosition);
                }
                tempEvent.instant = EditorGUILayout.Toggle("Instant", tempEvent.instant);
                break;

            case CosmosBattleEventClip.EventType.Tween:
                EditorGUILayout.LabelField("Tween Settings", EditorStyles.boldLabel);
                tempEvent.tweenType = (CosmosBattleEventClip.TweenType)EditorGUILayout.EnumPopup("Tween Type", tempEvent.tweenType);
                tempEvent.easeType = (Ease)EditorGUILayout.EnumPopup("Ease", tempEvent.easeType);

                switch (tempEvent.tweenType)
                {
                    case CosmosBattleEventClip.TweenType.Shake:
                        tempEvent.tweenIntensity = EditorGUILayout.FloatField("Intensity", tempEvent.tweenIntensity);
                        tempEvent.tweenVibrato = EditorGUILayout.IntField("Vibrato", tempEvent.tweenVibrato);
                        break;
                    case CosmosBattleEventClip.TweenType.Color:
                        tempEvent.tweenColor = EditorGUILayout.ColorField("Color", tempEvent.tweenColor);
                        break;
                    default:
                        tempEvent.tweenValue = EditorGUILayout.Vector3Field("Value", tempEvent.tweenValue);
                        break;
                }

                tempEvent.tweenLoop = EditorGUILayout.Toggle("Loop", tempEvent.tweenLoop);
                if (tempEvent.tweenLoop)
                {
                    tempEvent.tweenLoopCount = EditorGUILayout.IntField("Loop Count", tempEvent.tweenLoopCount);
                }
                break;
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add"))
        {
            if (targetTimeline.customEvents == null)
                targetTimeline.customEvents = new List<TimelineDataSO.CustomEvent>();

            var customEvent = tempEvent.ToCustomEvent(triggerTime);

            Undo.RecordObject(targetTimeline, $"Add Battle {selectedType} Event");
            targetTimeline.customEvents.Add(customEvent);
            EditorUtility.SetDirty(targetTimeline);

            Close();
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}
#endif