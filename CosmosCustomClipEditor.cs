#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(CosmosCustomClip))]
public class CosmosCustomClipEditor : Editor
{
    private SerializedProperty actionTypeProp;
    private SerializedProperty actionDataProp;

    void OnEnable()
    {
        actionTypeProp = serializedObject.FindProperty("actionType");
        actionDataProp = serializedObject.FindProperty("actionData");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var clip = (CosmosCustomClip)target;

        EditorGUILayout.Space();

        // Action Settings 섹션
        EditorGUILayout.LabelField("Action Settings", EditorStyles.boldLabel);

        // ActionType 변경 감지
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(actionTypeProp, new GUIContent("Action Type"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            // Timeline 클립 이름 자동 업데이트
            UpdateTimelineClipName(clip);
        }

        EditorGUILayout.Space();

        // Action Parameters 섹션
        EditorGUILayout.LabelField("Action Parameters", EditorStyles.boldLabel);

        CustomActionType actionType = (CustomActionType)actionTypeProp.enumValueIndex;

        // ActionType에 따라 필요한 필드만 표시
        switch (actionType)
        {
            case CustomActionType.None:
                EditorGUILayout.HelpBox("Select an Action Type", MessageType.Info);
                break;

            case CustomActionType.MoveToPosition:
                DrawMoveToPositionFields();
                break;

            case CustomActionType.Knockback:
                DrawKnockbackFields();
                break;

            case CustomActionType.SetVisibility:
                DrawVisibilityFields();
                break;

            case CustomActionType.FlashEffect:
                DrawFlashFields();
                break;

            case CustomActionType.HitEvent:
                DrawHitEventFields();
                break;

            case CustomActionType.ApplyBuff:
            case CustomActionType.ApplyDebuff:
                DrawBuffDebuffFields();
                break;

            case CustomActionType.TriggerCameraShake:
                DrawCameraShakeFields();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void UpdateTimelineClipName(CosmosCustomClip clip)
    {
        // PlayableDirector와 Timeline 찾기
        var director = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Playables.PlayableDirector>();
        if (director != null && director.playableAsset != null)
        {
            var timeline = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
            if (timeline != null)
            {
                // 모든 트랙 검색
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track is CosmosCustomTrack customTrack)
                    {
                        // 해당 클립 찾기
                        foreach (var timelineClip in customTrack.GetClips())
                        {
                            if (timelineClip.asset == clip)
                            {
                                // 클립 이름 업데이트
                                timelineClip.displayName = clip.actionType.ToString();

                                // Timeline 에디터 갱신
                                EditorUtility.SetDirty(timeline);
                                EditorUtility.SetDirty(customTrack);

                                // Timeline 윈도우 새로고침
                                var timelineWindow = EditorWindow.GetWindow(
                                    System.Type.GetType("UnityEditor.Timeline.TimelineWindow,Unity.Timeline.Editor"),
                                    false,
                                    null,
                                    false
                                );

                                if (timelineWindow != null)
                                {
                                    timelineWindow.Repaint();
                                }

                                return;
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawMoveToPositionFields()
    {
        var positionPreset = actionDataProp.FindPropertyRelative("positionPreset");
        var customPosition = actionDataProp.FindPropertyRelative("customPosition");
        var duration = actionDataProp.FindPropertyRelative("duration");
        var curve = actionDataProp.FindPropertyRelative("curve");

        EditorGUILayout.PropertyField(positionPreset, new GUIContent("Position"));

        if ((TargetPositionPreset)positionPreset.enumValueIndex == TargetPositionPreset.Custom)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(customPosition, new GUIContent("Custom Position"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(duration, new GUIContent("Duration"));
        EditorGUILayout.PropertyField(curve, new GUIContent("Movement Curve"));

        // Position Presets 버튼
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Position Presets", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Center"))
            {
                positionPreset.enumValueIndex = (int)TargetPositionPreset.Center;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Left"))
            {
                positionPreset.enumValueIndex = (int)TargetPositionPreset.Custom;
                customPosition.vector3Value = new Vector3(-2f, 0, 0);
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Right"))
            {
                positionPreset.enumValueIndex = (int)TargetPositionPreset.Custom;
                customPosition.vector3Value = new Vector3(2f, 0, 0);
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Back"))
            {
                positionPreset.enumValueIndex = (int)TargetPositionPreset.Custom;
                customPosition.vector3Value = new Vector3(0, 0, -2f);
                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    private void DrawKnockbackFields()
    {
        var knockbackIntensity = actionDataProp.FindPropertyRelative("knockbackIntensity");
        var intensity = actionDataProp.FindPropertyRelative("intensity");
        var duration = actionDataProp.FindPropertyRelative("duration");

        EditorGUILayout.PropertyField(knockbackIntensity, new GUIContent("Intensity"));

        if ((KnockbackIntensity)knockbackIntensity.enumValueIndex == KnockbackIntensity.Custom)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(intensity, new GUIContent("Custom Force"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(duration, new GUIContent("Duration"));
    }

    private void DrawVisibilityFields()
    {
        var visibilityMode = actionDataProp.FindPropertyRelative("visibilityMode");
        var duration = actionDataProp.FindPropertyRelative("duration");

        EditorGUILayout.PropertyField(visibilityMode, new GUIContent("Mode"));

        var mode = (VisibilityMode)visibilityMode.enumValueIndex;
        if (mode == VisibilityMode.FadeIn || mode == VisibilityMode.FadeOut)
        {
            EditorGUILayout.PropertyField(duration, new GUIContent("Fade Duration"));
        }
    }

    private void DrawFlashFields()
    {
        var flashColorPreset = actionDataProp.FindPropertyRelative("flashColorPreset");
        var customColor = actionDataProp.FindPropertyRelative("customColor");
        var duration = actionDataProp.FindPropertyRelative("duration");

        EditorGUILayout.PropertyField(flashColorPreset, new GUIContent("Color"));

        if ((FlashColorPreset)flashColorPreset.enumValueIndex == FlashColorPreset.Custom)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(customColor, new GUIContent("Custom Color"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(duration, new GUIContent("Duration"));

        // Color Presets 버튼
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Presets", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("White"))
            {
                flashColorPreset.enumValueIndex = (int)FlashColorPreset.White;
                serializedObject.ApplyModifiedProperties();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Red"))
            {
                flashColorPreset.enumValueIndex = (int)FlashColorPreset.Red;
                serializedObject.ApplyModifiedProperties();
            }

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Green"))
            {
                flashColorPreset.enumValueIndex = (int)FlashColorPreset.Green;
                serializedObject.ApplyModifiedProperties();
            }

            GUI.backgroundColor = Color.white;
        }
    }

    private void DrawHitEventFields()
    {
        var multiplier = actionDataProp.FindPropertyRelative("multiplier");
        EditorGUILayout.PropertyField(multiplier, new GUIContent("Damage Multiplier"));

        // Multiplier Presets
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Multiplier Presets", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("0.5x"))
            {
                multiplier.floatValue = 0.5f;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("1x"))
            {
                multiplier.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("1.5x"))
            {
                multiplier.floatValue = 1.5f;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("2x"))
            {
                multiplier.floatValue = 2f;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    private void DrawBuffDebuffFields()
    {
        var stringParam = actionDataProp.FindPropertyRelative("stringParam");
        var duration = actionDataProp.FindPropertyRelative("duration");

        EditorGUILayout.PropertyField(stringParam, new GUIContent("Buff/Debuff ID"));
        EditorGUILayout.PropertyField(duration, new GUIContent("Duration"));
    }

    private void DrawCameraShakeFields()
    {
        var intensity = actionDataProp.FindPropertyRelative("intensity");
        var duration = actionDataProp.FindPropertyRelative("duration");

        EditorGUILayout.PropertyField(intensity, new GUIContent("Shake Intensity"));
        EditorGUILayout.PropertyField(duration, new GUIContent("Shake Duration"));

        // Intensity Presets
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Intensity Presets", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Weak"))
            {
                intensity.floatValue = 0.5f;
                duration.floatValue = 0.2f;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Normal"))
            {
                intensity.floatValue = 1f;
                duration.floatValue = 0.5f;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Strong"))
            {
                intensity.floatValue = 2f;
                duration.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif