#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(CosmosEffectClip))]
public class EffectClipEditor : Editor
{
    private GameObject previewObject;
    private bool isEditingTransform = false;
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private float originalScale;

    public override void OnInspectorGUI()
    {
        var clip = (CosmosEffectClip)target;

        // Track에서 Template 확인
        bool isTemplateManaged = false;
        CosmosEffectTrack parentTrack = null;

        var director = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Playables.PlayableDirector>();
        if (director != null && director.playableAsset != null)
        {
            var timeline = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
            if (timeline != null)
            {
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track is CosmosEffectTrack effectTrack)
                    {
                        if (effectTrack.GetClips().Any(c => c.asset == clip))
                        {
                            parentTrack = effectTrack;
                            isTemplateManaged = effectTrack.GetTemplatePrefab() != null;
                            break;
                        }
                    }
                }
            }
        }

        // Template 관리 알림
        if (isTemplateManaged)
        {
            EditorGUILayout.HelpBox($"Prefab is managed by Track Template: {parentTrack.GetTemplatePrefab().name}", MessageType.Info);
        }

        // Effect Prefab 필드 (Template 관리 시 읽기 전용)
        GUI.enabled = !isTemplateManaged;
        clip.effectPrefab = EditorGUILayout.ObjectField("Effect Prefab",
            clip.effectPrefab, typeof(GameObject), false) as GameObject;
        GUI.enabled = true;

        // Addressable 키 (읽기 전용)
        GUI.enabled = false;
        EditorGUILayout.TextField("Addressable Key", clip.effectAddressableKey);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Transform Settings", EditorStyles.boldLabel);

        // Transform 편집 모드
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !isEditingTransform;
            clip.positionOffset = EditorGUILayout.Vector3Field("Position", clip.positionOffset);
            GUI.enabled = true;

            if (!isEditingTransform)
            {
                if (GUILayout.Button("📍", GUILayout.Width(25)))
                {
                    StartTransformEdit(clip);
                }
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("✓", GUILayout.Width(25)))
                {
                    ApplyTransformEdit(clip);
                }
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("✗", GUILayout.Width(25)))
                {
                    CancelTransformEdit();
                }
                GUI.backgroundColor = Color.white;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !isEditingTransform;
            clip.rotationOffset = EditorGUILayout.Vector3Field("Rotation", clip.rotationOffset);
            GUI.enabled = true;
        }

        clip.scale = EditorGUILayout.FloatField("Scale", clip.scale);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attachment Settings", EditorStyles.boldLabel);
        clip.attachToActor = EditorGUILayout.Toggle("Attach To Actor", clip.attachToActor);
        if (clip.attachToActor)
        {
            clip.attachBoneName = EditorGUILayout.TextField("Bone Name", clip.attachBoneName);
        }


        // 프리셋 버튼들
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Position Presets", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Head"))
                clip.positionOffset = new Vector3(0, 1.5f, 0);
            if (GUILayout.Button("Chest"))
                clip.positionOffset = new Vector3(0, 1f, 0);
            if (GUILayout.Button("Ground"))
                clip.positionOffset = new Vector3(0, 0, 0);
            if (GUILayout.Button("Front"))
                clip.positionOffset = new Vector3(0, 1f, 1f);
        }
    }

    private void StartTransformEdit(CosmosEffectClip clip)
    {
        // 기존 Preview 정리
        if (previewObject != null)
            DestroyImmediate(previewObject);

        // Preview 큐브 생성
        previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewObject.name = "EffectClip_TransformPreview";
        previewObject.transform.position = clip.positionOffset;
        previewObject.transform.rotation = Quaternion.Euler(clip.rotationOffset);
        previewObject.transform.localScale = Vector3.one * 0.5f; // 작은 큐브

        // 머티리얼 색상 설정
        var renderer = previewObject.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(1f, 0.5f, 0f, 0.5f);
        renderer.material = mat;

        // 원본 값 저장
        originalPosition = clip.positionOffset;
        originalRotation = clip.rotationOffset;
        originalScale = clip.scale;

        isEditingTransform = true;
        EditorApplication.update += UpdatePreviewRealtime;
            
        // Scene View 포커스
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Focus();
            SceneView.lastActiveSceneView.LookAt(previewObject.transform.position);
        }


    }

    private void UpdatePreviewRealtime()
    {
        if (!isEditingTransform || previewObject == null) return;

        var clip = (CosmosEffectClip)target;

        // Transform 변경 감지
        if (previewObject.transform.hasChanged)
        {
            clip.positionOffset = previewObject.transform.position;
            clip.rotationOffset = previewObject.transform.rotation.eulerAngles;

            previewObject.transform.hasChanged = false;
            EditorUtility.SetDirty(clip);
            Repaint();
        }
    }



    private void ApplyTransformEdit(CosmosEffectClip clip)
    {
        EditorApplication.update -= UpdatePreviewRealtime;

        if (previewObject != null)
        {
            // Preview 오브젝트의 Transform을 Clip에 적용
            clip.positionOffset = previewObject.transform.position;
            clip.rotationOffset = previewObject.transform.rotation.eulerAngles;

            DestroyImmediate(previewObject);
        }

        isEditingTransform = false;
        EditorUtility.SetDirty(clip);
    }

    private void CancelTransformEdit()
    { // 실시간 업데이트 해제
        EditorApplication.update -= UpdatePreviewRealtime;

        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }

        // 원본 값 복원
        var clip = (CosmosEffectClip)target;
        clip.positionOffset = originalPosition;
        clip.rotationOffset = originalRotation;
        clip.scale = originalScale;

        isEditingTransform = false;
    }

    private void UpdatePreviewFromInspector(CosmosEffectClip clip)
    {
        if (previewObject != null && !EditorGUI.EndChangeCheck())  // ✅ Inspector 값 변경 시에만
        {
            // Transform 편집 중일 때 실시간 표시
            if (isEditingTransform)
            {
                EditorGUILayout.HelpBox("📍 편집 모드\n• 드래그: 위치 이동\n• Shift+드래그: 회전\n• 값이 실시간으로 반영됩니다", MessageType.Info);

                // 실시간 값 표시 (읽기 전용)
                GUI.enabled = false;
                EditorGUILayout.Vector3Field("Current Position", clip.positionOffset);
                EditorGUILayout.Vector3Field("Current Rotation", clip.rotationOffset);
                GUI.enabled = true;
            }
            else
            {
                // 일반 편집 모드
                clip.positionOffset = EditorGUILayout.Vector3Field("Position", clip.positionOffset);
                clip.rotationOffset = EditorGUILayout.Vector3Field("Rotation", clip.rotationOffset);
            }

            // Scene View 갱신
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }
        }
    }

    private void OnDisable()
    {
        // 에디터 닫힐 때 Preview 정리
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }
        isEditingTransform = false;
    }

    private void OnSceneGUI()
    {
        if (!isEditingTransform || previewObject == null) return;

        var clip = (CosmosEffectClip)target;

        // Tools.current를 사용해서 Unity 기본 Transform 툴 활용
        if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDrag)
        {
            // Preview 오브젝트의 현재 Transform을 Clip에 실시간 반영
            if (previewObject.transform.hasChanged)
            {
                Undo.RecordObject(clip, "Edit Effect Transform");

                clip.positionOffset = previewObject.transform.position;
                clip.rotationOffset = previewObject.transform.rotation.eulerAngles;

                EditorUtility.SetDirty(clip);
                previewObject.transform.hasChanged = false;

                // Inspector 즉시 갱신
                Repaint();
            }
        }




        // Handles로 Transform 편집
        EditorGUI.BeginChangeCheck();

        Vector3 newPos = Handles.PositionHandle(previewObject.transform.position, previewObject.transform.rotation);
        Quaternion newRot = Handles.RotationHandle(previewObject.transform.rotation, previewObject.transform.position);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(clip, "Edit Effect Transform");  // ✅ Clip에 Undo 기록
            Undo.RecordObject(previewObject.transform, "Edit Preview Transform");

            previewObject.transform.position = newPos;
            previewObject.transform.rotation = newRot;

            // ✅ Clip 값 실시간 업데이트
            clip.positionOffset = newPos;
            clip.rotationOffset = newRot.eulerAngles;

            EditorUtility.SetDirty(clip);  // ✅ Dirty 마킹
            Repaint();  // Inspector 갱신
        }


        // Rotation Handle (Shift 키 누르면 회전 모드)
        if (Event.current.shift)
        {
            EditorGUI.BeginChangeCheck();
            newRot = Handles.RotationHandle(previewObject.transform.rotation, previewObject.transform.position);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(clip, "Edit Effect Rotation");
                Undo.RecordObject(previewObject.transform, "Rotate Preview");

                previewObject.transform.rotation = newRot;

                // 즉시 Clip에 반영
                clip.rotationOffset = newRot.eulerAngles;

                EditorUtility.SetDirty(clip);
                Repaint();
            }
        }



        // ✅ Preview 오브젝트 항상 보이도록
        Handles.color = new Color(1f, 0.5f, 0f, 0.3f);
        Handles.DrawWireCube(previewObject.transform.position, Vector3.one * 0.5f);

        // 현재 값 표시
        Handles.Label(previewObject.transform.position + Vector3.up * 0.6f,
            $"Pos: {clip.positionOffset}\nRot: {clip.rotationOffset}",
            EditorStyles.helpBox);
    }

    private string ExtractAddressableKey(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        const string addressableRoot = "Assets/Cosmos/ResourcesAddressable/";
        if (path.StartsWith(addressableRoot))
        {
            string key = path.Substring(addressableRoot.Length);
            int extensionIndex = key.LastIndexOf('.');
            if (extensionIndex > 0)
            {
                key = key.Substring(0, extensionIndex);
            }
            return key;
        }

        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    private void ApplyPrefabToTrackClips(CosmosEffectClip sourceClip)
    {
        // Timeline과 Director 찾기
        var director =  UnityEngine.Object.FindAnyObjectByType<UnityEngine.Playables.PlayableDirector>();
        if (director == null || director.playableAsset == null) return;

        var timeline = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
        if (timeline == null) return;

        // 현재 Clip이 속한 Track 찾기
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is CosmosEffectTrack effectTrack)
            {
                var clips = effectTrack.GetClips();
                foreach (var clip in clips)
                {
                    if (clip.asset == sourceClip)
                    {
                        // 이 Track의 템플릿으로 설정
                        effectTrack.SetTemplatePrefab(sourceClip.effectPrefab, sourceClip.effectAddressableKey);

                        // Track 이름 갱신을 위해 Timeline 새로고침
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(effectTrack);
                        var timelineWindow = UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.Timeline.TimelineWindow,Unity.Timeline.Editor"), false, null, false);
                        if (timelineWindow != null)
                        {
                            timelineWindow.Repaint();
                        }
#endif

                        // 자동 바인딩
                        effectTrack.AutoBindPrefab(director);

                        // 다른 Clip들에도 적용
                        foreach (var otherClip in clips)
                        {
                            var otherEffectClip = otherClip.asset as CosmosEffectClip;
                            if (otherEffectClip != null && otherEffectClip != sourceClip)
                            {
                                otherEffectClip.effectPrefab = sourceClip.effectPrefab;
                                otherEffectClip.effectAddressableKey = sourceClip.effectAddressableKey;
                                UnityEditor.EditorUtility.SetDirty(otherEffectClip);
                            }
                        }

                        return;
                    }
                }
            }
        }
    }
}
#endif