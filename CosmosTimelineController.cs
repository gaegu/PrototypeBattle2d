using BattleCharacterSystem;
using BattleCharacterSystem.Timeline;
using Cinemachine;  // 추가
using Cosmos.Timeline.Playback;
using Cosmos.Timeline.Playback.Editor;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;


/// <summary>
/// Timeline 재생 컨트롤러 윈도우
/// Unity Timeline Editor처럼 타임라인을 재생/제어하는 에디터 윈도우
/// </summary>
public class CosmosTimelineController : EditorWindow
{
    #region Constants
    private const float TOOLBAR_HEIGHT = 25f;
    private const float TIMELINE_HEIGHT = 60f;
    private const float EVENT_MARKER_HEIGHT = 20f;
    private const float MIN_MARKER_WIDTH = 3f;
    private const float FRAME_RATE = 60f;
    private const float TIMELINE_PADDING = 10f;
    #endregion

    #region Fields
    // 타겟 설정
    private BattleCharacterDataSO selectedCharacter;
    //private CharacterTimelineConfig timelineConfig;
    private TimelineDataSO currentTimeline;

    // 재생 관련
    private PreviewPlaybackAdapter playbackAdapter;
    private GameObject previewInstance;
    private bool isPlaying = false;
    private float currentTime = 0f;
    private float duration = 0f;
    private int currentFrame = 0;
    private int totalFrames = 0;

    // Timeline 선택
    private string[] timelineOptions = new string[] { "None" };
    private int selectedTimelineIndex = 0;

    // 재생 속도
    private float[] playbackSpeeds = { 0.25f, 0.5f, 1f, 2f, 4f };
    private string[] speedLabels = { "0.25x", "0.5x", "1x", "2x", "4x" };
    private int selectedSpeedIndex = 2; // Default 1x

    // Loop 모드
    private bool isLooping = false;

    // UI 관련
    private Rect timelineRect;
    private Vector2 scrollPosition;
    private bool isDraggingTimeline = false;

    // 이벤트 표시
    private List<EventMarker> eventMarkers = new List<EventMarker>();
    private EventMarker selectedMarker = null;
    private bool showEventInfo = false;

    // 스타일
    private GUIStyle timelineBackgroundStyle;
    private GUIStyle markerStyle;
    private GUIStyle selectedMarkerStyle;
    private GUIStyle playheadStyle;
    private GUIStyle frameNumberStyle;

    // AnimationClip 재생 관련
    private RuntimeAnimatorController genericController;
    private AnimatorOverrideController overrideController;
    private Animator previewAnimator;
    private Dictionary<string, AnimationClip> cachedAnimationClips = new Dictionary<string, AnimationClip>();

    // 클래스 정의
    private class EventMarker
    {
        public TimelineDataSO.ITimelineEvent eventData;
        public Rect rect;
        public Color color;
        public float startTime;
        public float endTime;
        public string tooltip;
    }
    #endregion

    #region Unity Lifecycle
    [MenuItem("*COSMOS*/Battle/Timeline Controller")]
    public static void ShowWindow()
    {
        var window = GetWindow<CosmosTimelineController>("Timeline Controller");
        window.minSize = new Vector2(800, 400);
        window.Show();
    }

    /// <summary>
    /// BattleCharacterEditorWindow에서 호출하는 메서드
    /// </summary>
    public static void ShowWindowWithCharacter(BattleCharacterDataSO character)
    {
        var window = GetWindow<CosmosTimelineController>("Timeline Controller");
        window.minSize = new Vector2(800, 400);
        window.SelectCharacter(character);
        window.Show();
    }

    private void OnEnable()
    {
        InitializeStyles();
        EditorApplication.update += OnEditorUpdate;

        // Hierarchy에 표시되도록 설정
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;

        // Editor 모드 업데이트 빈도 증가
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
        CleanupPreview();
    }

    // Hierarchy 아이콘 표시 (선택사항)
    private void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj != null && obj.name.StartsWith("[PREVIEW]"))
        {
            // Preview 오브젝트에 아이콘 표시
            Rect iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
            GUI.Label(iconRect, "▶");
        }
        else if (obj != null && obj.name.StartsWith("[EFFECT]"))
        {
            // Effect 오브젝트에 아이콘 표시
            Rect iconRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
            GUI.Label(iconRect, "✨");
        }
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawPlaybackControls();
        DrawTimeline();
        DrawEventInfo();
    }

    private float lastUpdateTime = 0f;
    private bool isManualPlaying = false; // Editor 모드에서 수동 재생 중인지 추적

    private void OnEditorUpdate()
    {
        if (playbackAdapter != null)
        {
            // Editor 모드에서 수동 재생 처리
            if (!Application.isPlaying && isPlaying && currentTimeline != null)
            {
                // 델타 타임 계산
                float deltaTime = Time.realtimeSinceStartup - lastUpdateTime;
                lastUpdateTime = Time.realtimeSinceStartup;

                // 재생 속도 적용
                float adjustedDelta = deltaTime * playbackSpeeds[selectedSpeedIndex];

                // 시간 업데이트
                currentTime += adjustedDelta;

                // 루프 처리
                if (currentTime > duration)
                {
                    if (isLooping)
                    {
                        currentTime = currentTime % duration;
                    }
                    else
                    {
                        currentTime = duration;
                        isPlaying = false;
                    }
                }

                currentFrame = Mathf.RoundToInt(currentTime * FRAME_RATE);

                // 애니메이션과 이펙트 업데이트
                ApplyAnimationsAtTime(currentTime);
                UpdateEffectSimulation(currentTime);
                UpdateCameraEvents(currentTime);  // 추가


                Repaint();
            }
            else if (Application.isPlaying)
            {
                // Play 모드에서는 기존 방식 사용
                currentTime = playbackAdapter.CurrentTime;
                currentFrame = Mathf.RoundToInt(currentTime * FRAME_RATE);
                isPlaying = playbackAdapter.IsPlaying;

                if (isPlaying)
                {
                    Repaint();
                }
            }
        }
    }
    #endregion

    #region Initialization
    private void InitializeStyles()
    {
        timelineBackgroundStyle = new GUIStyle("box");
        timelineBackgroundStyle.normal.background = MakeColorTexture(new Color(0.2f, 0.2f, 0.2f));

        markerStyle = new GUIStyle("box");
        markerStyle.normal.background = MakeColorTexture(new Color(1f, 0.5f, 0f, 0.8f));
        markerStyle.border = new RectOffset(1, 1, 1, 1);

        selectedMarkerStyle = new GUIStyle("box");
        selectedMarkerStyle.normal.background = MakeColorTexture(new Color(1f, 0.8f, 0f, 1f));
        selectedMarkerStyle.border = new RectOffset(2, 2, 2, 2);

        playheadStyle = new GUIStyle();
        playheadStyle.normal.background = MakeColorTexture(Color.white);

        frameNumberStyle = new GUIStyle(EditorStyles.miniLabel);
        frameNumberStyle.alignment = TextAnchor.MiddleCenter;
        frameNumberStyle.normal.textColor = Color.gray;

        // Track 애니메이션용 스타일 추가
        trackMarkerStyle = new GUIStyle("box");
        trackMarkerStyle.normal.background = MakeColorTexture(new Color(1f, 1f, 0f, 0.5f)); // 반투명 노란색
        trackMarkerStyle.border = new RectOffset(1, 1, 1, 1);


    }

    private Texture2D MakeColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(2, 2); // 1x1 대신 2x2로 변경
        Color[] colors = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            colors[i] = color;
        }
        texture.SetPixels(colors);
        texture.Apply();
        texture.filterMode = FilterMode.Point; // 필터 모드 설정
        return texture;
    }
    #endregion



    #region UI Drawing
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Timeline Controller", EditorStyles.boldLabel);

        EditorGUILayout.Space(5);

        // 캐릭터 선택
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Character:", GUILayout.Width(80));

        var newCharacter = EditorGUILayout.ObjectField(selectedCharacter, typeof(BattleCharacterDataSO), false) as BattleCharacterDataSO;
        if (newCharacter != selectedCharacter)
        {
            SelectCharacter(newCharacter);
        }

        EditorGUILayout.EndHorizontal();

        // Timeline 선택
        if (selectedCharacter != null && timelineOptions.Length > 1)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Timeline:", GUILayout.Width(80));

            int newIndex = EditorGUILayout.Popup(selectedTimelineIndex, timelineOptions);
            if (newIndex != selectedTimelineIndex)
            {
                selectedTimelineIndex = newIndex;
                LoadSelectedTimeline();
            }

            EditorGUILayout.EndHorizontal();
        }

        // Timeline 정보
        if (currentTimeline != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Duration: {duration:F2}s", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Total Frames: {totalFrames}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Events: {eventMarkers.Count}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPlaybackControls()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(TOOLBAR_HEIGHT));

        // 처음으로
        if (GUILayout.Button("⏮", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            JumpToStart();
        }

        // 이전 프레임
        if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            PreviousFrame();
        }

        // Play/Pause
        GUI.backgroundColor = isPlaying ? Color.red : Color.green;
        string playButtonText = isPlaying ? "⏸" : "▶";
        if (GUILayout.Button(playButtonText, EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            TogglePlayback();
        }
        GUI.backgroundColor = Color.white;

        // 다음 프레임
        if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            NextFrame();
        }

        // 끝으로
        if (GUILayout.Button("⏭", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            JumpToEnd();
        }

        GUILayout.Space(20);

        // 현재 시간/프레임 표시
        EditorGUILayout.LabelField($"Time: {currentTime:F2}s", GUILayout.Width(80));
        EditorGUILayout.LabelField($"Frame: {currentFrame}/{totalFrames}", GUILayout.Width(120));

        GUILayout.FlexibleSpace();

        // Loop 토글
        bool newLooping = GUILayout.Toggle(isLooping, "Loop", EditorStyles.toolbarButton, GUILayout.Width(50));
        if (newLooping != isLooping)
        {
            isLooping = newLooping;
            if (playbackAdapter != null)
            {
                // Reflection으로 private playbackSystem 접근
                System.Reflection.FieldInfo fieldInfo = typeof(PreviewPlaybackAdapter).GetField("playbackSystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (fieldInfo != null)
                {
                    var playbackSystem = fieldInfo.GetValue(playbackAdapter) as CosmosPlaybackSystem;
                    if (playbackSystem != null)
                    {
                        playbackSystem.Mode = isLooping ? PlaybackMode.Loop : PlaybackMode.Once;
                    }
                }
            }
        }

        // 재생 속도
        EditorGUILayout.LabelField("Speed:", GUILayout.Width(45));
        int newSpeedIndex = EditorGUILayout.Popup(selectedSpeedIndex, speedLabels, EditorStyles.toolbarPopup, GUILayout.Width(60));
        if (newSpeedIndex != selectedSpeedIndex)
        {
            selectedSpeedIndex = newSpeedIndex;
            if (playbackAdapter != null)
            {
                // Reflection으로 private playbackSystem 접근
                System.Reflection.FieldInfo fieldInfo = typeof(PreviewPlaybackAdapter).GetField("playbackSystem",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (fieldInfo != null)
                {
                    var playbackSystem = fieldInfo.GetValue(playbackAdapter) as CosmosPlaybackSystem;
                    if (playbackSystem != null)
                    {
                        playbackSystem.PlaybackSpeed = playbackSpeeds[selectedSpeedIndex];
                    }
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTimeline()
    {
        if (currentTimeline == null)
        {
            EditorGUILayout.HelpBox("Select a character and timeline to begin", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);

        // Timeline 영역 계산
        Rect timelineArea = GUILayoutUtility.GetRect(
            GUIContent.none,
            GUIStyle.none,
            GUILayout.Height(TIMELINE_HEIGHT + 40),
            GUILayout.ExpandWidth(true)
        );

        if (Event.current.type == EventType.Repaint)
        {
            timelineRect = new Rect(
                timelineArea.x + TIMELINE_PADDING,
                timelineArea.y + 20,
                timelineArea.width - TIMELINE_PADDING * 2,
                TIMELINE_HEIGHT
            );
        }

        // 배경
        GUI.Box(timelineRect, GUIContent.none, timelineBackgroundStyle);

        // 프레임 눈금 그리기
        DrawTimelineRuler();

        // 이벤트 마커 그리기
        DrawEventMarkers();

        // 재생 헤드 그리기
        DrawPlayhead();

        // 마우스 입력 처리
        HandleTimelineInput();
    }

    private void DrawTimelineRuler()
    {
        if (duration <= 0) return;

        // 주요 눈금 간격 계산 (1초 단위)
        int majorTickInterval = 60; // 1초 = 60프레임
        int minorTickInterval = 10; // 10프레임 단위

        for (int frame = 0; frame <= totalFrames; frame += minorTickInterval)
        {
            float normalizedPos = (float)frame / totalFrames;
            float xPos = timelineRect.x + normalizedPos * timelineRect.width;

            if (frame % majorTickInterval == 0)
            {
                // 주요 눈금 (1초 단위)
                Handles.color = Color.white;
                Handles.DrawLine(
                    new Vector3(xPos, timelineRect.y - 10),
                    new Vector3(xPos, timelineRect.y)
                );

                // 시간 레이블
                float time = frame / FRAME_RATE;
                GUI.Label(
                    new Rect(xPos - 20, timelineRect.y - 20, 40, 15),
                    $"{time:F1}s",
                    frameNumberStyle
                );
            }
            else
            {
                // 보조 눈금
                Handles.color = Color.gray;
                Handles.DrawLine(
                    new Vector3(xPos, timelineRect.y - 5),
                    new Vector3(xPos, timelineRect.y)
                );
            }
        }
    }


    private GUIStyle trackMarkerStyle;

    private void DrawEventMarkers()
    {
        eventMarkers.Clear();
        if (currentTimeline == null) return;

        // 일반 이벤트 마커 표시
        var events = currentTimeline.GetAllEventsSorted();

        foreach (var evt in events)
        {
            if (evt == null) continue;

            float startTime = evt.TriggerTime;
            float endTime = startTime + GetEventDuration(evt);

            float startX = timelineRect.x + (startTime / duration) * timelineRect.width;
            float endX = timelineRect.x + (endTime / duration) * timelineRect.width;
            float width = Mathf.Max(endX - startX, MIN_MARKER_WIDTH);

            Rect markerRect = new Rect(
                startX,
                timelineRect.y + 35,  // Track 애니메이션 아래에 표시
                width,
                EVENT_MARKER_HEIGHT
            );

            Color markerColor = GetEventColor(evt);
            var marker = new EventMarker
            {
                eventData = evt,
                rect = markerRect,
                color = markerColor,
                startTime = startTime,
                endTime = endTime,
                tooltip = GetEventTooltip(evt)
            };

            eventMarkers.Add(marker);

            // 마커 그리기
            GUIStyle style = (selectedMarker == marker) ? selectedMarkerStyle : markerStyle;
            style.normal.background = MakeColorTexture(markerColor);
            GUI.Box(markerRect, GUIContent.none, style);

            // 이벤트 이름 (공간이 있을 때만)
            if (width > 30)
            {
                GUI.Label(markerRect, GetEventShortName(evt), frameNumberStyle);
            }
        }

        // Track 애니메이션 마커 표시
        // Track 애니메이션 마커 표시
        if (currentTimeline.trackAnimations != null)
        {
            foreach (var trackAnim in currentTimeline.trackAnimations)
            {
                if (trackAnim == null) continue;

                float startTime = trackAnim.startTime;
                float endTime = startTime + trackAnim.duration;

                float startX = timelineRect.x + (startTime / duration) * timelineRect.width;
                float endX = timelineRect.x + (endTime / duration) * timelineRect.width;
                float width = Mathf.Max(endX - startX, MIN_MARKER_WIDTH);

                Rect markerRect = new Rect(
                    startX,
                    timelineRect.y + 5,
                    width,
                    EVENT_MARKER_HEIGHT + 5
                );

                // Track 애니메이션 색상 설정
                Color markerColor;
                if (trackAnim.animationClip != null || !string.IsNullOrEmpty(trackAnim.animationClipAddressableKey))
                {
                    markerColor = new Color(1f, 1f, 0f, 0.8f);  // 노란색
                }
                else
                {
                    markerColor = new Color(0.6f, 0.2f, 0.8f, 0.6f);  // 보라색
                }

                // AnimationClip 이름 안전하게 가져오기
                string clipName = "";
                if (trackAnim.animationClip != null)
                {
                    clipName = trackAnim.animationClip.name;
                }
                else if (!string.IsNullOrEmpty(trackAnim.animationClipAddressableKey))
                {
                    clipName = trackAnim.animationClipAddressableKey;
                }

                var marker = new EventMarker
                {
                    eventData = null,
                    rect = markerRect,
                    color = markerColor,
                    startTime = startTime,
                    endTime = endTime,
                    tooltip = $"Track: {trackAnim.trackName}\nClip: {clipName}"
                };

                eventMarkers.Add(marker);

                trackMarkerStyle.normal.background = MakeColorTexture(markerColor);

                // 마커 박스 그리기
                GUI.Box(markerRect, GUIContent.none, trackMarkerStyle);

                // 텍스트는 별도로 그리기
                var textStyle = new GUIStyle(EditorStyles.label);
                textStyle.alignment = TextAnchor.MiddleCenter;
                textStyle.normal.textColor = Color.white;
                GUI.Label(markerRect, trackAnim.trackName, textStyle);
            }
        }


    }

    private void DrawPlayhead()
    {
        if (duration <= 0) return;

        float normalizedTime = currentTime / duration;
        float xPos = timelineRect.x + normalizedTime * timelineRect.width;

        // 재생 헤드 라인
        Handles.color = Color.red;
        Handles.DrawLine(
            new Vector3(xPos, timelineRect.y - 10),
            new Vector3(xPos, timelineRect.yMax + 10)
        );

        // 재생 헤드 핸들
        Rect handleRect = new Rect(xPos - 5, timelineRect.y - 15, 10, 10);
        GUI.Box(handleRect, GUIContent.none, playheadStyle);
    }

    private void DrawEventInfo()
    {
        if (!showEventInfo || selectedMarker == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Event Information", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        var evt = selectedMarker.eventData;

        EditorGUILayout.LabelField($"Type: {GetEventType(evt)}");
        EditorGUILayout.LabelField($"Start Time: {selectedMarker.startTime:F3}s (Frame: {Mathf.RoundToInt(selectedMarker.startTime * FRAME_RATE)})");
        EditorGUILayout.LabelField($"End Time: {selectedMarker.endTime:F3}s (Frame: {Mathf.RoundToInt(selectedMarker.endTime * FRAME_RATE)})");
        EditorGUILayout.LabelField($"Duration: {(selectedMarker.endTime - selectedMarker.startTime):F3}s");

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("== Specific Info ==", EditorStyles.boldLabel);

        // 이벤트별 상세 정보
        switch (evt)
        {
            case TimelineDataSO.AnimationEvent animEvent:
                EditorGUILayout.LabelField($"Animation: {animEvent.animationStateName}");
                if (!string.IsNullOrEmpty(animEvent.animationClipAddressableKey))
                    EditorGUILayout.LabelField($"Clip Key: {animEvent.animationClipAddressableKey}");
                if (animEvent.extractedClip != null)
                    EditorGUILayout.LabelField($"Extracted Clip: {animEvent.extractedClip.name}");
                EditorGUILayout.LabelField($"Play Mode: {animEvent.playMode}");
                EditorGUILayout.LabelField($"Cross Fade: {animEvent.crossFadeDuration:F2}s");
                break;

            case TimelineDataSO.EffectEvent effectEvent:
                EditorGUILayout.LabelField("-- Effect Event --", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"Effect Key: {effectEvent.effectAddressableKey}");
                EditorGUILayout.LabelField($"Play Mode: {effectEvent.playMode}");
                EditorGUILayout.LabelField($"Duration: {effectEvent.duration:F3}s");
                EditorGUILayout.LabelField($"Position Offset: {effectEvent.positionOffset}");
                EditorGUILayout.LabelField($"Rotation Offset: {effectEvent.rotationOffset}");
                EditorGUILayout.LabelField($"Scale: {effectEvent.scale}");

                if (!string.IsNullOrEmpty(effectEvent.attachBoneName))
                    EditorGUILayout.LabelField($"Attach Bone: {effectEvent.attachBoneName}");

                break;

            case TimelineDataSO.SoundEvent soundEvent:
                EditorGUILayout.LabelField("-- Sound Event --", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"Sound Path: {soundEvent.soundEventPath}");
                EditorGUILayout.LabelField($"Volume: {soundEvent.volume:F2}");
                break;

            case TimelineDataSO.CameraEvent cameraEvent:
                EditorGUILayout.LabelField($"Camera Action: {cameraEvent.actionType}");
                if (cameraEvent.actionType == TimelineDataSO.CameraActionType.VirtualCameraSwitch)
                {
                    EditorGUILayout.LabelField($"Virtual Camera: {cameraEvent.virtualCameraName}");
                    EditorGUILayout.LabelField($"Blend In: {cameraEvent.blendInDuration:F2}s");
                    EditorGUILayout.LabelField($"Blend Out: {cameraEvent.blendOutDuration:F2}s");
                }
                EditorGUILayout.LabelField($"Duration: {cameraEvent.duration:F2}s");
                EditorGUILayout.LabelField($"Intensity: {cameraEvent.intensity}");
                break;

            case TimelineDataSO.CustomEvent customEvent:
                EditorGUILayout.LabelField("-- Custom Event --", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"Event Name: {customEvent.eventName}");

                if (customEvent.parameters != null && customEvent.parameters.Count > 0)
                {
                    EditorGUILayout.LabelField("Parameters:", EditorStyles.miniBoldLabel);
                    foreach (var param in customEvent.parameters)
                    {
                        EditorGUILayout.LabelField($"  {param.Key}: {param.Value}");
                    }
                }
                break;
        }

        EditorGUILayout.EndVertical();
    }
    #endregion

    #region Timeline Input
    private void HandleTimelineInput()
    {
        Event e = Event.current;
        if (duration <= 0) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    // 마커 클릭 체크
                    bool markerClicked = false;
                    foreach (var marker in eventMarkers)
                    {
                        if (marker.rect.Contains(e.mousePosition))
                        {
                            selectedMarker = marker;
                            showEventInfo = true;
                            markerClicked = true;

                            // 해당 이벤트 시간으로 이동
                            SetTime(marker.startTime);
                            e.Use();
                            break;
                        }
                    }

                    // 타임라인 클릭 (마커가 아닌 곳)
                    if (!markerClicked && timelineRect.Contains(e.mousePosition))
                    {
                        isDraggingTimeline = true;
                        UpdateTimeFromMouse(e.mousePosition.x);
                        selectedMarker = null;
                        showEventInfo = false;
                        e.Use();
                    }
                }
                break;

            case EventType.MouseDrag:
                if (isDraggingTimeline && e.button == 0)
                {
                    UpdateTimeFromMouse(e.mousePosition.x);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0)
                {
                    isDraggingTimeline = false;
                }
                break;
        }
    }

    private void UpdateTimeFromMouse(float mouseX)
    {
        float normalizedPos = Mathf.Clamp01((mouseX - timelineRect.x) / timelineRect.width);
        float time = normalizedPos * duration;
        SetTime(time);
    }
    #endregion

    #region Character & Timeline Management
    private void SelectCharacter(BattleCharacterDataSO character)
    {
        if (character == selectedCharacter) return;

        CleanupPreview();

        selectedCharacter = character;
        selectedTimelineIndex = 0;
        currentTimeline = null;

        if (character != null)
        {
            LoadCharacterTimelines();
            CreatePreviewInstance();
        }

        // 시뮬레이션된 Effect를 Pool로 반환
        var resourceProvider = playbackAdapter?.GetComponent<AddressableResourceProvider>();
        if (resourceProvider != null)
        {
            foreach (var kvp in simulatedEffects)
            {
                if (kvp.Value != null)
                {
                    resourceProvider.DestroyEffect(kvp.Value);
                }
            }
        }
        simulatedEffects.Clear();
    }

    private void LoadCharacterTimelines()
    {
        if (selectedCharacter == null) return;

        // TimelineConfig 로드
        List<string> options = new List<string> { "None" };

        // ✅ CharacterDataSO의 TimelineSettings 직접 접근
        if (selectedCharacter.TimelineSettings != null)
        {
            var settings = selectedCharacter.TimelineSettings;

            if (settings.attack1Timeline != null)
                options.Add("Attack1");
            if (selectedCharacter.ActiveSkillTimeline != null)
                options.Add("ActiveSkill1");
            if (selectedCharacter.PassiveSkillTimeline != null)
                options.Add("PassiveSkill1");

            // 커스텀 타임라인 추가
            foreach (var custom in settings.customTimelines)
            {
                if (custom.timeline != null && !string.IsNullOrEmpty(custom.stateName))
                    options.Add(custom.stateName);
            }
        }

        timelineOptions = options.ToArray();
    }

    private void LoadSelectedTimeline()
    {
        if (selectedTimelineIndex <= 0 || timelineOptions.Length <= selectedTimelineIndex)
        {
            currentTimeline = null;
            if (playbackAdapter != null)
            {
                playbackAdapter.Stop();
            }
            return;
        }

        string timelineName = timelineOptions[selectedTimelineIndex];

        // TimelineConfig에서 가져오기
        if (selectedCharacter?.TimelineSettings != null)
        {

            switch (timelineName.ToLower())
            {
                case "activeskill1":
                case "activeskill":
                    currentTimeline = selectedCharacter.ActiveSkillTimeline;
                    break;

                case "passiveskill1":
                case "passiveskill":
                    currentTimeline = selectedCharacter.PassiveSkillTimeline;
                    break;

                default:
                    currentTimeline = selectedCharacter.TimelineSettings.GetTimelineByState(timelineName);
                    break;
            }

        }

        if (currentTimeline != null)
        {
            duration = currentTimeline.duration;
            totalFrames = Mathf.RoundToInt(duration * FRAME_RATE);

            // PlaybackAdapter에 타임라인 설정
            if (playbackAdapter != null)
            {
                playbackAdapter.PlayTimeline(currentTimeline);
                SetTime(0f);
            }
        }
        else
        {
            Debug.LogWarning($"Failed to load timeline: {timelineName}");
        }
    }
    #endregion

    #region Preview Instance
    private void CreatePreviewInstance()
    {
        if (selectedCharacter == null) return;

        CleanupPreview();

        // 프리팹 로드 및 인스턴스 생성
        GameObject prefab = LoadCharacterPrefab();

        if (prefab != null)
        {
            previewInstance = Instantiate(prefab);
            // HideFlags 제거 - Hierarchy에 표시
            previewInstance.name = $"[PREVIEW] {selectedCharacter.CharacterName}";

            // PlaybackAdapter 설정
            SetupPlaybackAdapter();
        }
        else
        {
            Debug.LogError($"Failed to load prefab for character: {selectedCharacter.CharacterName}");
        }
    }

    private GameObject LoadCharacterPrefab()
    {
        if (selectedCharacter == null) return null;

        // 여러 경로 시도
        string resourceName = selectedCharacter.CharacterResourceName;
        string[] searchPaths = {
            $"Assets/Cosmos/ResourcesAddressable/Prefabs/Characters/{resourceName}.prefab",
            $"Assets/Cosmos/ResourcesAddressable/Characters/{resourceName}/{resourceName}.prefab",
            $"Assets/Resources/Characters/{resourceName}.prefab",
            $"Assets/Characters/{resourceName}.prefab"
        };

        // PrefabPath가 있으면 우선 시도
        if (!string.IsNullOrEmpty(selectedCharacter.PrefabPath))
        {
            string customPath = $"Assets/{selectedCharacter.PrefabPath}/{resourceName}.prefab";
            GameObject customPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(customPath);
            if (customPrefab != null) return customPrefab;
        }

        foreach (string path in searchPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                Debug.Log($"Loaded prefab from: {path}");
                return prefab;
            }
        }

        // GUID로 검색
        string[] guids = AssetDatabase.FindAssets($"{resourceName} t:Prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Debug.Log($"Loaded prefab via GUID from: {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        Debug.LogWarning($"Failed to load prefab for {selectedCharacter.CharacterName} (Resource: {resourceName})");
        return null;
    }

    private void SetupPlaybackAdapter()
    {
        if (previewInstance == null) return;

        // Animator 찾기
        previewAnimator = previewInstance.GetComponent<Animator>();
        if (previewAnimator == null)
        {
            previewAnimator = previewInstance.GetComponentInChildren<Animator>();
        }

        // 원본 Controller 저장
        if (previewAnimator != null && previewAnimator.runtimeAnimatorController != null)
        {
            originalController = previewAnimator.runtimeAnimatorController;
        }

        // 기존 컴포넌트 체크
        playbackAdapter = previewInstance.GetComponent<PreviewPlaybackAdapter>();

        if (playbackAdapter == null)
        {
            playbackAdapter = previewInstance.AddComponent<PreviewPlaybackAdapter>();
        }

        // 초기화 - Initialize 메서드 시그니처에 맞게 수정
        playbackAdapter.Initialize(previewInstance, selectedCharacter);

        // PlaybackSystem의 GameObject들도 Hierarchy에 표시되도록 설정
        var playbackSystemObj = playbackAdapter.GetComponent<CosmosPlaybackSystem>();
        if (playbackSystemObj != null && playbackSystemObj.gameObject != null)
        {
            playbackSystemObj.gameObject.name = $"[PLAYBACK] {selectedCharacter.CharacterName}";
            playbackSystemObj.InitializeSystem();
        }

        // EventHandler의 GameObject도 표시
        var eventHandler = playbackAdapter.GetComponent<TimelineEventHandler>();
        if (eventHandler != null && eventHandler.gameObject != null)
        {
            eventHandler.gameObject.name = $"[EVENTS] {selectedCharacter.CharacterName}";
            eventHandler.Initialize();
        }

        // PlaybackSystem 직접 접근을 위해 잠시 대기
        System.Reflection.FieldInfo fieldInfo = typeof(PreviewPlaybackAdapter).GetField("playbackSystem",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (fieldInfo != null)
        {
            var playbackSystem = fieldInfo.GetValue(playbackAdapter) as CosmosPlaybackSystem;
            if (playbackSystem != null)
            {
                // Loop 모드 설정
                playbackSystem.Mode = isLooping ? PlaybackMode.Loop : PlaybackMode.Once;
                // 재생 속도 설정
                playbackSystem.PlaybackSpeed = playbackSpeeds[selectedSpeedIndex];
            }
        }

        // 현재 선택된 타임라인 로드
        if (currentTimeline != null)
        {
            playbackAdapter.PlayTimeline(currentTimeline);
        }
    }

    private void CleanupPreview()
    {
        // AnimationMode 정리
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        // Animator 원래 상태로 복원
        if (previewAnimator != null && originalController != null)
        {
            previewAnimator.runtimeAnimatorController = originalController;
        }

        if (playbackAdapter != null)
        {
            playbackAdapter.Stop();

            // EventHandler의 이펙트 정리
            var eventHandler = playbackAdapter.GetComponent<TimelineEventHandler>();
            if (eventHandler != null)
            {
                eventHandler.CleanupAllEffects();
            }
        }

        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        // 카메라 Priority 복원
        if (currentCameraEvent != null)
        {
            originalCameraPriorities.Clear();
            currentCameraEvent = null;
        }

        // 캐시된 AnimationClip 정리
        cachedAnimationClips.Clear();


        // 시뮬레이션된 Effect를 Pool로 반환
        if (playbackAdapter != null)
        {
            var resourceProvider = playbackAdapter?.GetComponent<AddressableResourceProvider>();
            if (resourceProvider != null)
            {
                foreach (var kvp in simulatedEffects)
                {
                    if (kvp.Value != null)
                    {
                        resourceProvider.DestroyEffect(kvp.Value);
                    }
                }
            }
            simulatedEffects.Clear();
        }


        // 참조 정리
        playbackAdapter = null;
        previewAnimator = null;
        genericController = null;
        overrideController = null;
        originalController = null;


    


        eventMarkers.Clear();
        selectedMarker = null;
        showEventInfo = false;
        isPlaying = false;
        currentTime = 0f;
        currentFrame = 0;
    }

    // 원본 Animator Controller 저장용
    private RuntimeAnimatorController originalController;
    #endregion

    #region Playback Control
    private void TogglePlayback()
    {
        if (currentTimeline == null) return;

        if (!Application.isPlaying)
        {
            // Editor 모드에서 수동 재생
            if (isPlaying)
            {
                isPlaying = false;
                isManualPlaying = false;
            }
            else
            {
                // 끝에 도달했으면 처음부터
                if (currentTime >= duration - 0.01f)
                {
                    currentTime = 0f;
                }

                isPlaying = true;
                isManualPlaying = true;
                lastUpdateTime = Time.realtimeSinceStartup;
            }
        }
        else
        {
            // Play 모드에서는 기존 방식 사용
            if (playbackAdapter == null) return;

            if (isPlaying)
            {
                playbackAdapter.Pause();
            }
            else
            {
                if (currentTime >= duration - 0.01f)
                {
                    playbackAdapter.Seek(0f);
                }
                playbackAdapter.Resume();
            }
        }
    }

    private void SetTime(float time)
    {
        if (playbackAdapter == null || currentTimeline == null) return;

        time = Mathf.Clamp(time, 0f, duration);
        currentTime = time;
        currentFrame = Mathf.RoundToInt(time * FRAME_RATE);

        // Play 모드에서만 playbackAdapter 사용
        if (Application.isPlaying && playbackAdapter != null && currentTimeline != null)
        {
            playbackAdapter.Seek(time);
        }

        // Editor 모드에서는 직접 업데이트
        if (!Application.isPlaying)
        {
            ApplyAnimationsAtTime(time);
            UpdateEffectSimulation(time);
            UpdateCameraEvents(time);  // 추가


            // Scene View 강제 갱신
            UnityEditor.SceneView.RepaintAll();
        }
    }

    // 클래스 상단에 필드 추가
    private Dictionary<string, GameObject> simulatedEffects = new Dictionary<string, GameObject>();

    // 🆕 새로운 메서드: Editor 모드에서 Effect 시뮬레이션
    private void UpdateEffectSimulation(float currentTime)
    {
        if (currentTimeline == null || previewInstance == null) return;

        var eventHandler = playbackAdapter?.GetComponent<TimelineEventHandler>();
        if (eventHandler == null) return;

        // 활성화되어야 할 Effect 추적
        HashSet<string> activeEffectKeys = new HashSet<string>();

        foreach (var effectEvent in currentTimeline.effectEvents)
        {
            bool shouldBeActive = ShouldEffectBeActive(effectEvent, currentTime);
            string effectKey = $"{effectEvent.effectAddressableKey}_{effectEvent.triggerTime}";

            if (shouldBeActive)
            {
                activeEffectKeys.Add(effectKey);

                // 이미 생성된 Effect가 없을 때만 새로 생성
                if (!simulatedEffects.ContainsKey(effectKey))
                {
                    SimulateEffectAtTime(effectEvent, currentTime, effectKey);
                }
                else if (simulatedEffects[effectKey] != null)
                {
                    // 이미 있는 Effect의 시뮬레이션 시간 업데이트
                    UpdateExistingEffectSimulation(simulatedEffects[effectKey], effectEvent, currentTime);
                }
            }
        }

        // 범위를 벗어난 Effect 제거
        List<string> keysToRemove = new List<string>();
        var resourceProvider = playbackAdapter?.GetComponent<AddressableResourceProvider>();

        foreach (var kvp in simulatedEffects)
        {
            if (!activeEffectKeys.Contains(kvp.Key))
            {
                if (kvp.Value != null && resourceProvider != null)
                {
                    // 🔄 Pool로 반환
                    resourceProvider.DestroyEffect(kvp.Value);
                }
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            simulatedEffects.Remove(key);
        }
    }


    private void UpdateExistingEffectSimulation(GameObject effectInstance, TimelineDataSO.EffectEvent effectEvent, float currentTime)
    {
        if (effectInstance == null) return;

        var particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in particleSystems)
        {
            float simulationTime = currentTime - effectEvent.triggerTime;

            if (simulationTime >= 0)
            {
                // 기존 파티클시스템의 시뮬레이션 시간 업데이트
                ps.Simulate(simulationTime, false, true, false);

                if (effectEvent.playMode == TimelineDataSO.EffectPlayMode.Duration)
                {
                    if (simulationTime > effectEvent.duration)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }
    }



    // 카메라 이벤트 처리를 위한 필드 추가
    private Dictionary<string, int> originalCameraPriorities = new Dictionary<string, int>();
    private TimelineDataSO.CameraEvent currentCameraEvent = null;

    private void UpdateCameraEvents(float currentTime)
    {
        if (currentTimeline == null) return;

        // Play 모드에서는 EventHandler가 처리
        if (Application.isPlaying)
        {
            return;
        }


        // CinemachineBrain 찾기
        var brain = Camera.main?.GetComponent<Cinemachine.CinemachineBrain>();
        if (brain == null)
        {
            return;
        }

        // Editor 모드에서 카메라 이벤트 처리
        TimelineDataSO.CameraEvent activeCameraEvent = null;

        foreach (var cameraEvent in currentTimeline.cameraEvents)
        {
            if (cameraEvent.actionType == TimelineDataSO.CameraActionType.VirtualCameraSwitch)
            {
                // 현재 시간이 카메라 이벤트 범위 내인지 확인
                if (currentTime >= cameraEvent.triggerTime &&
                    currentTime <= cameraEvent.triggerTime + cameraEvent.duration)
                {
                    activeCameraEvent = cameraEvent;
                    break;
                }
            }
        }

        // 활성 카메라 이벤트가 변경되었을 때만 처리
        if (activeCameraEvent != currentCameraEvent)
        {
            // 이전 카메라 복원
            if (currentCameraEvent != null)
            {
                RestoreCameraPrioritiesWithBlend(brain,
                currentCameraEvent?.blendOutDuration ?? 0.5f);
            }

           // 새 카메라 활성화
            if (activeCameraEvent != null)
            {
                ApplyCameraEventWithBlend(activeCameraEvent, brain);
            }

            currentCameraEvent = activeCameraEvent;
        }

        // Editor 모드에서 CinemachineBrain 수동 업데이트
        if (brain != null && !Application.isPlaying)
        {
            // 블렌드 진행을 위해 매 프레임 업데이트
            brain.ManualUpdate();
        }


    }

    private void ApplyCameraEventWithBlend(TimelineDataSO.CameraEvent cameraEvent, Cinemachine.CinemachineBrain brain)
    {
        if (string.IsNullOrEmpty(cameraEvent.virtualCameraName)) return;

        var allVcams = UnityEngine.Object.FindObjectsByType<Cinemachine.CinemachineVirtualCamera>(FindObjectsSortMode.InstanceID);


        // 블렌드 시간 설정
        if (brain != null && cameraEvent.blendInDuration > 0)
        {
            brain.m_DefaultBlend.m_Time = cameraEvent.blendInDuration;
            brain.m_DefaultBlend.m_Style = Cinemachine.CinemachineBlendDefinition.Style.EaseInOut;
        }

        // 원본 Priority 저장
        originalCameraPriorities.Clear();
        foreach (var vcam in allVcams)
        {
            originalCameraPriorities[vcam.name] = vcam.Priority;
        }

        // 타겟 카메라 찾기 및 Priority 설정
        foreach (var vcam in allVcams)
        {
            if (vcam.name == cameraEvent.virtualCameraName)
            {
                vcam.Priority = 11;
                Debug.Log($"[Editor Mode] Switching to virtual camera: {cameraEvent.virtualCameraName} with blend: {cameraEvent.blendInDuration}s");
            }
            else
            {
                vcam.Priority = 0;
            }
        }
    }

    private void RestoreCameraPrioritiesWithBlend(Cinemachine.CinemachineBrain brain, float blendDuration)
    {
        // 블렌드 시간 설정
        if (brain != null && blendDuration > 0)
        {
            brain.m_DefaultBlend.m_Time = blendDuration;
        }

        var allVcams = UnityEngine.Object.FindObjectsByType<Cinemachine.CinemachineVirtualCamera>(FindObjectsSortMode.InstanceID);

        foreach (var vcam in allVcams)
        {
            if (originalCameraPriorities.ContainsKey(vcam.name))
            {
                vcam.Priority = originalCameraPriorities[vcam.name];
            }
        }

        originalCameraPriorities.Clear();
    }



    // 🆕 Effect가 활성화되어야 하는지 확인
    private bool ShouldEffectBeActive(TimelineDataSO.EffectEvent effectEvent, float time)
    {
        switch (effectEvent.playMode)
        {
            case TimelineDataSO.EffectPlayMode.OneShot:
                // OneShot은 트리거 직후 짧은 시간 동안만
                return time >= effectEvent.triggerTime &&
                       time <= effectEvent.triggerTime + 0.5f; // 0.5초 정도 유지

            case TimelineDataSO.EffectPlayMode.Duration:
                // Duration은 지정된 시간 동안
                return time >= effectEvent.triggerTime &&
                       time <= effectEvent.triggerTime + effectEvent.duration;

            case TimelineDataSO.EffectPlayMode.Loop:
                // Looping은 트리거 이후 계속
                return time >= effectEvent.triggerTime;

            default:
                return false;
        }
    }

    // 🆕 특정 Effect를 현재 시간에 맞춰 시뮬레이션
    private void SimulateEffectAtTime(TimelineDataSO.EffectEvent effectEvent, float currentTime, string effectKey)
    {   
        
        // 이미 처리 중이거나 생성된 경우 즉시 리턴
        if (simulatedEffects.ContainsKey(effectKey))
            return;

        var resourceProvider = playbackAdapter?.GetComponent<AddressableResourceProvider>();
        if (resourceProvider == null) return;

        // 🔄 Pool에서 Effect 가져오기
        GameObject effectInstance = resourceProvider.InstantiateEffect(
            effectEvent.effectAddressableKey,
            Vector3.zero,
            Quaternion.identity
        );

        if (effectInstance != null)
        {
            effectInstance.name = $"[EFFECT_SIM] {effectEvent.effectAddressableKey}";

            SetupEffectTransform(effectInstance, effectEvent);
            SimulateParticleSystem(effectInstance, effectEvent, currentTime);

            // Dictionary에 추가
            simulatedEffects[effectKey] = effectInstance;
        }

    }

    // 🆕 ParticleSystem을 특정 시간으로 시뮬레이션
    private void SimulateParticleSystem(GameObject effectInstance, TimelineDataSO.EffectEvent effectEvent, float currentTime)
    {
        var particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in particleSystems)
        {
            // 시뮬레이션 시간 계산
            float simulationTime = currentTime - effectEvent.triggerTime;

            if (simulationTime >= 0)
            {
                // ParticleSystem을 특정 시간으로 시뮬레이션
                ps.Simulate(simulationTime, true, true, true);

                // Duration 모드인 경우 시간 체크
                if (effectEvent.playMode == TimelineDataSO.EffectPlayMode.Duration)
                {
                    if (simulationTime > effectEvent.duration)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }
    }

    // 🆕 Effect Transform 설정
    private void SetupEffectTransform(GameObject effect, TimelineDataSO.EffectEvent effectEvent)
    {
        if (previewInstance == null) return;

        Transform attachTarget = previewInstance.transform;

        // Attach Mode에 따른 처리
        if (effectEvent.attachToActor == true &&
            !string.IsNullOrEmpty(effectEvent.attachBoneName))
        {
            // Bone 찾기
            var animator = previewInstance.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                // HumanBodyBones로 찾기
                if (System.Enum.TryParse<HumanBodyBones>(effectEvent.attachBoneName, out var bone))
                {
                    Transform boneTransform = animator.GetBoneTransform(bone);
                    if (boneTransform != null)
                        attachTarget = boneTransform;
                }
            }

            // 일반 Transform으로 찾기
            if (attachTarget == previewInstance.transform)
            {
                Transform found = previewInstance.transform.Find(effectEvent.attachBoneName);
                if (found != null)
                    attachTarget = found;
            }
        }


        //나중에 팔로우 추가. 
       // effect.transform.SetParent(effectEvent.followTarget ? attachTarget : null);
        effect.transform.position = attachTarget.position + effectEvent.positionOffset;
        effect.transform.rotation = attachTarget.rotation * Quaternion.Euler(effectEvent.rotationOffset);

    }

    private void ApplyAnimationsAtTime(float time)
    {
        if (currentTimeline == null || previewInstance == null) return;

        // Animator 확인
        if (previewAnimator == null)
        {
            previewAnimator = previewInstance.GetComponent<Animator>();
            if (previewAnimator == null)
            {
                previewAnimator = previewInstance.GetComponentInChildren<Animator>();
            }
            if (previewAnimator == null) return;
        }

        AnimationClip clipToPlay = null;
        float clipTime = 0f;

        // Track 애니메이션 처리 (전체 타임라인에 걸쳐 재생되는 애니메이션)
        if (currentTimeline.trackAnimations != null && currentTimeline.trackAnimations.Count > 0)
        {
            foreach (var trackAnim in currentTimeline.trackAnimations)
            {
                if (trackAnim == null) continue;

                // AnimationClip null 체크
                if (trackAnim.animationClip != null)
                {
                    // Track 애니메이션은 startTime부터 duration 동안 재생
                    if (time >= trackAnim.startTime && time <= trackAnim.startTime + trackAnim.duration)
                    {
                        clipTime = time - trackAnim.startTime;

                        // AnimationClip이 여전히 유효한지 다시 확인
                        if (trackAnim.animationClip != null)
                        {
                            if (trackAnim.isLooping)
                            {
                                clipTime = clipTime % trackAnim.animationClip.length;
                            }
                            else
                            {
                                clipTime = Mathf.Min(clipTime, trackAnim.animationClip.length);
                            }
                            clipToPlay = trackAnim.animationClip;
                            break; // 첫 번째 유효한 clip 사용
                        }
                    }
                }
                // animationClipAddressableKey가 있으면 로드 시도
                else if (!string.IsNullOrEmpty(trackAnim.animationClipAddressableKey))
                {
                    LoadAndCacheTrackAnimation(trackAnim).Forget();
                }
            }
        }

        // Track 애니메이션이 없으면 Animation 이벤트의 extractedClip 확인
        if (clipToPlay == null)
        {
            var animEvents = currentTimeline.animationEvents;
            if (animEvents != null)
            {
                foreach (var animEvent in animEvents)
                {
                    if (animEvent == null) continue;

                    // extractedClip null 체크
                    if (animEvent.extractedClip != null)
                    {
                        // 이벤트 시작 시점부터의 상대 시간 계산
                        float relativeTime = time - animEvent.TriggerTime;
                        if (relativeTime >= 0)
                        {
                            // extractedClip이 여전히 유효한지 다시 확인
                            if (animEvent.extractedClip != null)
                            {
                                float clipDuration = animEvent.extractedClip.length;

                                if (animEvent.loop)
                                {
                                    // 루프인 경우 계속 재생
                                    clipTime = relativeTime % clipDuration;
                                    clipToPlay = animEvent.extractedClip;
                                    break;
                                }
                                else if (relativeTime <= clipDuration)
                                {
                                    // 루프가 아닌 경우 클립 길이까지만 재생
                                    clipTime = relativeTime;
                                    clipToPlay = animEvent.extractedClip;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        // 찾은 clip을 AnimatorOverrideController로 재생
        if (clipToPlay != null)
        {
            ApplyAnimationClipWithOverride(clipToPlay, clipTime);
        }
    }

    private void ApplyAnimationClipWithOverride(AnimationClip clip, float time)
    {
        if (clip == null || previewAnimator == null) return;

        // GenericController 로드
        if (genericController == null)
        {
            genericController = Resources.Load<RuntimeAnimatorController>("AnimatorControllers/GenericRuntimeAnimatorController");
            if (genericController == null)
            {
                // GenericController가 없으면 AnimationMode 사용 (fallback)
                SampleAnimationFallback(clip, time);
                return;
            }
        }

        // OverrideController 생성/업데이트
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(genericController);
        }

        // Clip 할당
        overrideController["GenericClip"] = clip;

        // Animator에 적용
        if (previewAnimator.runtimeAnimatorController != overrideController)
        {
            previewAnimator.runtimeAnimatorController = overrideController;
        }

        // normalizedTime으로 특정 시점 재생
        float normalizedTime = Mathf.Clamp01(time / clip.length);
        //previewAnimator.Play("GenericClip", 0, normalizedTime);

        SampleAnimationFallback(clip, normalizedTime);

        // Editor에서 즉시 업데이트
        if (!isPlaying)
        {
            previewAnimator.Update(0);
        }

 
    }



    private void SampleAnimationFallback(AnimationClip clip, float time)
    {
        if (clip == null || previewInstance == null) return;

        // Editor에서 AnimationMode 사용 (GenericController가 없을 때 fallback)
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(previewInstance, clip, time);
        AnimationMode.EndSampling();

        // Scene View 갱신
        if (UnityEditor.SceneView.lastActiveSceneView != null)
        {
            UnityEditor.SceneView.lastActiveSceneView.Repaint();
        }
    }

    private async UniTask LoadAndCacheTrackAnimation(TimelineDataSO.TrackAnimation trackAnim)
    {
        string key = trackAnim.animationClipAddressableKey;

        // 이미 캐시되어 있으면 사용
        if (cachedAnimationClips.ContainsKey(key))
        {
            trackAnim.animationClip = cachedAnimationClips[key];
            return;
        }


        AnimationClip clip = await ResourceLoadHelper.LoadAssetAsync<AnimationClip>(key);
        if (clip != null)
        {
            cachedAnimationClips[key] = clip;
            trackAnim.animationClip = clip;
            Debug.Log($"Loaded track animation from: {key}");
            return;
        }

        // 캐릭터 이름을 포함한 경로로 검색
        if (selectedCharacter != null)
        {
            string characterName = selectedCharacter.CharacterName;
            string[] searchPaths = {
                $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations/{key}.anim",
                $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations/{characterName}_{key}.anim",
                $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations/{characterName}_{key}_track.anim"
            };

            foreach (string path in searchPaths)
            {
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    cachedAnimationClips[key] = clip;
                    trackAnim.animationClip = clip;
                    Debug.Log($"Loaded track animation from: {path}");
                    return;
                }
            }
        }

        // GUID로 검색
        string[] guids = AssetDatabase.FindAssets($"{key} t:AnimationClip");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                cachedAnimationClips[key] = clip;
                trackAnim.animationClip = clip;
            }
        }
    }

    private void JumpToStart()
    {
        SetTime(0f);
    }

    private void JumpToEnd()
    {
        if (duration > 0)
        {
            SetTime(duration);
        }
    }

    private void NextFrame()
    {
        if (currentFrame < totalFrames)
        {
            SetTime((currentFrame + 1) / FRAME_RATE);
        }
    }

    private void PreviousFrame()
    {
        if (currentFrame > 0)
        {
            SetTime((currentFrame - 1) / FRAME_RATE);
        }
    }
    #endregion

    #region Helper Methods
    private float GetEventDuration(TimelineDataSO.ITimelineEvent evt)
    {
        return evt switch
        {
            // AnimationEvent에 duration 필드가 없으므로 기본값 사용
            TimelineDataSO.AnimationEvent animEvent => 0.5f,
            TimelineDataSO.EffectEvent effectEvent => effectEvent.duration > 0 ? effectEvent.duration : 1f,
            TimelineDataSO.SoundEvent soundEvent => 0.5f, // 기본 표시 길이
            TimelineDataSO.CameraEvent cameraEvent => cameraEvent.duration > 0 ? cameraEvent.duration : 0.5f,
            TimelineDataSO.CustomEvent customEvent => 0.2f, // 기본 표시 길이
            _ => 0.1f
        };
    }

    private Color GetEventColor(TimelineDataSO.ITimelineEvent evt)
    {
        return evt switch
        {
            TimelineDataSO.AnimationEvent => new Color(0.2f, 0.6f, 1f, 0.8f), // 파란색
            TimelineDataSO.EffectEvent => new Color(1f, 0.5f, 0f, 0.8f), // 주황색
            TimelineDataSO.SoundEvent => new Color(0.2f, 1f, 0.2f, 0.8f), // 초록색
            TimelineDataSO.CameraEvent cameraEvt =>
            cameraEvt.actionType == TimelineDataSO.CameraActionType.VirtualCameraSwitch ?
            new Color(0.5f, 0.8f, 1f, 0.8f) : // 하늘색 (VirtualCamera)
            new Color(1f, 0.2f, 0.8f, 0.8f), // 보라색 (기타 카메라)
            TimelineDataSO.CustomEvent => new Color(1f, 1f, 0.2f, 0.8f), // 노란색
            _ => Color.gray
        };
    }

    private string GetEventType(TimelineDataSO.ITimelineEvent evt)
    {
        return evt switch
        {
            TimelineDataSO.AnimationEvent => "Animation",
            TimelineDataSO.EffectEvent => "Effect",
            TimelineDataSO.SoundEvent => "Sound",
            TimelineDataSO.CameraEvent => "Camera",
            TimelineDataSO.CustomEvent => "Custom",
            _ => "Unknown"
        };
    }

    private string GetEventShortName(TimelineDataSO.ITimelineEvent evt)
    {
        return evt switch
        {
            TimelineDataSO.AnimationEvent animEvent => animEvent.animationStateName,
            TimelineDataSO.EffectEvent effectEvent => System.IO.Path.GetFileNameWithoutExtension(effectEvent.effectAddressableKey),
            TimelineDataSO.SoundEvent soundEvent => "SFX",
            TimelineDataSO.CameraEvent cameraEvent => cameraEvent.actionType == TimelineDataSO.CameraActionType.VirtualCameraSwitch && !string.IsNullOrEmpty(cameraEvent.virtualCameraName) ?
            $"CAM: {cameraEvent.virtualCameraName}" :
            cameraEvent.actionType.ToString(),
            TimelineDataSO.CustomEvent customEvent => customEvent.eventName,
            _ => "?"
        };
    }

    private string GetEventTooltip(TimelineDataSO.ITimelineEvent evt)
    {
        return evt switch
        {
            TimelineDataSO.AnimationEvent animEvent => $"Animation: {animEvent.animationStateName}\nTime: {animEvent.TriggerTime:F2}s",
            TimelineDataSO.EffectEvent effectEvent => $"Effect: {effectEvent.effectAddressableKey}\nTime: {effectEvent.TriggerTime:F2}s",
            TimelineDataSO.SoundEvent soundEvent => $"Sound: {soundEvent.soundEventPath}\nTime: {soundEvent.TriggerTime:F2}s",
            TimelineDataSO.CameraEvent cameraEvent => cameraEvent.actionType == TimelineDataSO.CameraActionType.VirtualCameraSwitch ?
            $"Virtual Camera: {cameraEvent.virtualCameraName}\nTime: {cameraEvent.TriggerTime:F2}s\nDuration: {cameraEvent.duration:F2}s" :
            $"Camera: {cameraEvent.actionType}\nTime: {cameraEvent.TriggerTime:F2}s",
            TimelineDataSO.CustomEvent customEvent => $"Custom: {customEvent.eventName}\nTime: {customEvent.TriggerTime:F2}s",
            _ => ""
        };
    }
    #endregion
}