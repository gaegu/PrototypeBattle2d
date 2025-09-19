#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BattleCharacterSystem.Timeline;

/// <summary>
/// Battle Event를 TimelineDataSO의 CustomEvent로 변환하는 에디터 유틸리티
/// </summary>
public static class CosmosBattleEventConverter
{
    /// <summary>
    /// TimelineDataSO에 Battle Event 추가
    /// </summary>
    [MenuItem("Assets/Timeline/Add Battle Hit Event", false, 20)]
    public static void AddBattleHitEventToTimeline()
    {
        var timeline = Selection.activeObject as TimelineDataSO;
        if (timeline == null)
        {
            Debug.LogWarning("Please select a TimelineDataSO asset");
            return;
        }

        var battleEvent = new CosmosBattleEventClip
        {
            eventType = CosmosBattleEventClip.EventType.Hit,
            targetType = CosmosBattleEventClip.TargetType.MainTarget,
            hitType = CosmosBattleEventClip.HitType.RealHit,
            damagePercent = 100f,
            duration = 0.1f
        };

        var customEvent = battleEvent.ToCustomEvent(0.5f);  // 0.5초 시점에 발생

        if (timeline.customEvents == null)
            timeline.customEvents = new List<TimelineDataSO.CustomEvent>();

        timeline.customEvents.Add(customEvent);

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        Debug.Log($"Added Battle Hit Event to {timeline.name}");
    }

    [MenuItem("Assets/Timeline/Add Battle Camera Event", false, 21)]
    public static void AddBattleCameraEventToTimeline()
    {
        var timeline = Selection.activeObject as TimelineDataSO;
        if (timeline == null)
        {
            Debug.LogWarning("Please select a TimelineDataSO asset");
            return;
        }

        var battleEvent = new CosmosBattleEventClip
        {
            eventType = CosmosBattleEventClip.EventType.Camera,
            cameraName = "BattleCamera_Skill",
            blendTime = 0.5f,
            returnToPrevious = true,
            duration = 1f
        };

        var customEvent = battleEvent.ToCustomEvent(0.2f);  // 0.2초 시점에 발생

        if (timeline.customEvents == null)
            timeline.customEvents = new List<TimelineDataSO.CustomEvent>();

        timeline.customEvents.Add(customEvent);

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        Debug.Log($"Added Battle Camera Event to {timeline.name}");
    }

    [MenuItem("Assets/Timeline/Add Battle Move Event", false, 22)]
    public static void AddBattleMoveEventToTimeline()
    {
        var timeline = Selection.activeObject as TimelineDataSO;
        if (timeline == null)
        {
            Debug.LogWarning("Please select a TimelineDataSO asset");
            return;
        }

        var battleEvent = new CosmosBattleEventClip
        {
            eventType = CosmosBattleEventClip.EventType.Move,
            targetType = CosmosBattleEventClip.TargetType.AllTargets,
            movePosition = CosmosBattleEventClip.MovePosition.Center,
            instant = false,
            duration = 0.5f
        };

        var customEvent = battleEvent.ToCustomEvent(0.3f);

        if (timeline.customEvents == null)
            timeline.customEvents = new List<TimelineDataSO.CustomEvent>();

        timeline.customEvents.Add(customEvent);

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        Debug.Log($"Added Battle Move Event to {timeline.name}");
    }

    [MenuItem("Assets/Timeline/Add Battle Tween Event", false, 23)]
    public static void AddBattleTweenEventToTimeline()
    {
        var timeline = Selection.activeObject as TimelineDataSO;
        if (timeline == null)
        {
            Debug.LogWarning("Please select a TimelineDataSO asset");
            return;
        }

        var battleEvent = new CosmosBattleEventClip
        {
            eventType = CosmosBattleEventClip.EventType.Tween,
            targetType = CosmosBattleEventClip.TargetType.AllTargets,
            tweenType = CosmosBattleEventClip.TweenType.Shake,
            tweenIntensity = 0.5f,
            duration = 0.3f
        };

        var customEvent = battleEvent.ToCustomEvent(0.5f);

        if (timeline.customEvents == null)
            timeline.customEvents = new List<TimelineDataSO.CustomEvent>();

        timeline.customEvents.Add(customEvent);

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        Debug.Log($"Added Battle Tween Event to {timeline.name}");
    }

    // 메뉴 활성화 조건
    [MenuItem("Assets/Timeline/Add Battle Hit Event", true)]
    [MenuItem("Assets/Timeline/Add Battle Camera Event", true)]
    [MenuItem("Assets/Timeline/Add Battle Move Event", true)]
    [MenuItem("Assets/Timeline/Add Battle Tween Event", true)]
    private static bool ValidateTimelineSelected()
    {
        return Selection.activeObject is TimelineDataSO;
    }

    /// <summary>
    /// Battle Event 편집 윈도우
    /// </summary>
    public class BattleEventEditorWindow : EditorWindow
    {
        private TimelineDataSO targetTimeline;
        private Vector2 scrollPos;
        private CosmosBattleEventClip previewEvent;

        [MenuItem("Window/Timeline/Battle Event Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<BattleEventEditorWindow>("Battle Event Editor");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Battle Event Timeline Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetTimeline = EditorGUILayout.ObjectField("Timeline", targetTimeline, typeof(TimelineDataSO), false) as TimelineDataSO;

            if (targetTimeline == null)
            {
                EditorGUILayout.HelpBox("Select a TimelineDataSO to edit battle events", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Battle Events", EditorStyles.boldLabel);

            // 현재 Battle Event들 표시
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));

            if (targetTimeline.customEvents != null)
            {
                var eventsToRemove = new List<TimelineDataSO.CustomEvent>();

                foreach (var customEvent in targetTimeline.customEvents)
                {
                    if (customEvent.eventName.StartsWith("Battle_"))
                    {
                        EditorGUILayout.BeginHorizontal();

                        var eventType = customEvent.eventName.Replace("Battle_", "");
                        EditorGUILayout.LabelField($"{customEvent.triggerTime:F2}s - {eventType}");

                        if (customEvent.parameters.ContainsKey("damage"))
                        {
                            EditorGUILayout.LabelField($"Damage: {customEvent.parameters["damage"]}%");
                        }

                        if (GUILayout.Button("Remove", GUILayout.Width(60)))
                        {
                            eventsToRemove.Add(customEvent);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                foreach (var eventToRemove in eventsToRemove)
                {
                    targetTimeline.customEvents.Remove(eventToRemove);
                    EditorUtility.SetDirty(targetTimeline);
                }
            }

            EditorGUILayout.EndScrollView();

            // 새 이벤트 추가 섹션
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add New Battle Event", EditorStyles.boldLabel);

            if (previewEvent == null)
            {
                previewEvent = new CosmosBattleEventClip();
            }

            previewEvent.eventType = (CosmosBattleEventClip.EventType)EditorGUILayout.EnumPopup("Event Type", previewEvent.eventType);
            previewEvent.targetType = (CosmosBattleEventClip.TargetType)EditorGUILayout.EnumPopup("Target Type", previewEvent.targetType);

            float triggerTime = EditorGUILayout.FloatField("Trigger Time", 0.5f);
            previewEvent.duration = EditorGUILayout.FloatField("Duration", previewEvent.duration);

            // 타입별 추가 필드
            switch (previewEvent.eventType)
            {
                case CosmosBattleEventClip.EventType.Hit:
                    previewEvent.hitType = (CosmosBattleEventClip.HitType)EditorGUILayout.EnumPopup("Hit Type", previewEvent.hitType);
                    previewEvent.damagePercent = EditorGUILayout.Slider("Damage %", previewEvent.damagePercent, 0f, 100f);
                    previewEvent.hitEffectKey = EditorGUILayout.TextField("Effect Key", previewEvent.hitEffectKey);
                    break;

                case CosmosBattleEventClip.EventType.Camera:
                    previewEvent.cameraName = EditorGUILayout.TextField("Camera Name", previewEvent.cameraName);
                    previewEvent.blendTime = EditorGUILayout.FloatField("Blend Time", previewEvent.blendTime);
                    previewEvent.returnToPrevious = EditorGUILayout.Toggle("Return to Previous", previewEvent.returnToPrevious);
                    break;

                case CosmosBattleEventClip.EventType.Move:
                    previewEvent.movePosition = (CosmosBattleEventClip.MovePosition)EditorGUILayout.EnumPopup("Position", previewEvent.movePosition);
                    previewEvent.instant = EditorGUILayout.Toggle("Instant", previewEvent.instant);
                    break;

                case CosmosBattleEventClip.EventType.Tween:
                    previewEvent.tweenType = (CosmosBattleEventClip.TweenType)EditorGUILayout.EnumPopup("Tween Type", previewEvent.tweenType);
                    previewEvent.easeType = (DG.Tweening.Ease)EditorGUILayout.EnumPopup("Ease", previewEvent.easeType);
                    previewEvent.tweenIntensity = EditorGUILayout.FloatField("Intensity", previewEvent.tweenIntensity);
                    break;
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Add Event"))
            {
                if (targetTimeline.customEvents == null)
                    targetTimeline.customEvents = new List<TimelineDataSO.CustomEvent>();

                var customEvent = previewEvent.ToCustomEvent(triggerTime);
                targetTimeline.customEvents.Add(customEvent);

                EditorUtility.SetDirty(targetTimeline);
                AssetDatabase.SaveAssets();

                Debug.Log($"Added {previewEvent.eventType} event at {triggerTime}s");
            }

            // 총 데미지 퍼센트 표시
            if (targetTimeline.customEvents != null)
            {
                float totalDamage = 0f;
                foreach (var evt in targetTimeline.customEvents)
                {
                    if (evt.eventName == "Battle_Hit" && evt.parameters.ContainsKey("damage"))
                    {
                        if (float.TryParse(evt.parameters["damage"], out float damage))
                        {
                            totalDamage += damage;
                        }
                    }
                }

                EditorGUILayout.Space();
                var color = GUI.color;
                GUI.color = totalDamage > 100f ? Color.red : (totalDamage == 100f ? Color.green : Color.yellow);
                EditorGUILayout.LabelField($"Total Damage: {totalDamage}%");
                GUI.color = color;
            }
        }
    }
}
#endif