#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CustomEditor(typeof(CosmosAnimationTrack))]
[CanEditMultipleObjects]
public class CosmosAnimationTrackEditor : Editor
{
    // UI 상태
    private bool showCharacterList = false;
    private bool showBindingInfo = true;
    private Vector2 scrollPosition;

    // 캐릭터 리스트 캐시
    private List<string> availableCharacterKeys = new List<string>();
    private List<string> availableCharacterNames = new List<string>();
    private int selectedCharacterIndex = -1;

    // 검색 상태
    private bool isSearching = false;
    private bool isBinding = false;
    private string searchFilter = "";

    // 캐시 관리
    private bool needsRefresh = true;
    private PlayableDirector cachedDirector;
    private TimelineAsset cachedTimeline;

    // 스타일
    private GUIStyle statusStyle;
    private Color successColor = new Color(0.2f, 0.8f, 0.2f);
    private Color warningColor = new Color(0.9f, 0.7f, 0.1f);
    private Color errorColor = new Color(0.9f, 0.2f, 0.2f);





    private void OnEnable()
    {
        var track = (CosmosAnimationTrack)target;

        InitializeStyles();
        FindTimelineContext();

        // Auto 모드일 때 자동 감지
        if (track.CharacterSource == CosmosAnimationTrack.CharacterSourceType.Auto)
        {
            DetectCharacterName();
        }

        needsRefresh = true;

    }
    private void OnDisable()
    {
    }


    public override void OnInspectorGUI()
    {
        var track = (CosmosAnimationTrack)target;
 

        EditorGUILayout.LabelField("Animation Track Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 1. Character Source 선택
        DrawCharacterSourceSection(track);

        EditorGUILayout.Space();

        // 2. Character 선택/표시
        DrawCharacterSelectionSection(track);

        EditorGUILayout.Space();

        // 3. Binding 상태
        DrawBindingStatusSection(track);

        EditorGUILayout.Space();

        // 4. 액션 버튼들
        DrawActionButtons(track);

        // 5. 디버그 정보 (Development Build에서만)
#if UNITY_EDITOR && DEVELOPMENT_BUILD
        DrawDebugInfo(track);
#endif
    }

    private void AutoBindCharacter(CosmosAnimationTrack track, PlayableDirector director)
    {
        // Timeline 이름에서 캐릭터 추출
        string timelineName = director.playableAsset?.name ?? "";
        if (timelineName.EndsWith("_Timeline"))
        {
            string characterName = timelineName.Replace("_Timeline", "");

            // Scene에서 찾기
            GameObject character = GameObject.Find(characterName);
            if (character == null)
                character = GameObject.Find(characterName + "(Clone)");

            if (character != null)
            {
                Animator animator = character.GetComponent<Animator>();
                if (animator != null)
                {
                    director.SetGenericBinding(track, animator);
                    Debug.Log($"Auto-bound to: {character.name}");
                }
            }
        }
    }


    #region UI Drawing Methods

    private void DrawCharacterSourceSection(CosmosAnimationTrack track)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Character Source", EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        var newSource = (CosmosAnimationTrack.CharacterSourceType)EditorGUILayout.EnumPopup(
            "Mode", track.CharacterSource);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(track, "Change Character Source");
            track.SetCharacterSource(newSource);

            // Auto 모드로 변경시 자동 감지
            if (newSource == CosmosAnimationTrack.CharacterSourceType.Auto)
            {
                DetectCharacterName();
            }

            needsRefresh = true;
        }

        // 모드별 설명
        string modeDescription = GetModeDescription(track.CharacterSource);
        EditorGUILayout.HelpBox(modeDescription, MessageType.Info);

        EditorGUILayout.EndVertical();
    }

    private void DrawCharacterSelectionSection(CosmosAnimationTrack track)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Character Selection", EditorStyles.miniBoldLabel);

        switch (track.CharacterSource)
        {
            case CosmosAnimationTrack.CharacterSourceType.Auto:
                DrawAutoModeUI(track);
                break;

            case CosmosAnimationTrack.CharacterSourceType.Manual:
                DrawManualModeUI(track);
                break;

            case CosmosAnimationTrack.CharacterSourceType.Scene:
                DrawSceneModeUI(track);
                break;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAutoModeUI(CosmosAnimationTrack track)
    {
        // 감지된 캐릭터명 표시
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = false;
        string displayName = string.IsNullOrEmpty(track.CachedCharacterName)
            ? "Not Detected"
            : track.CachedCharacterName;
        EditorGUILayout.TextField("Detected", displayName);
        GUI.enabled = true;

        if (GUILayout.Button("Detect", GUILayout.Width(60)))
        {
            DetectCharacterName();
        }

        EditorGUILayout.EndHorizontal();

        // Timeline 정보 표시
        if (cachedTimeline != null)
        {
            EditorGUILayout.LabelField($"Timeline: {cachedTimeline.name}", EditorStyles.miniLabel);
        }

        // 감지 실패시 도움말
        if (string.IsNullOrEmpty(track.CachedCharacterName))
        {
            EditorGUILayout.HelpBox(
                "Character name not detected.\n" +
                "Timeline should be named: 'CharacterName_Timeline'",
                MessageType.Warning);
        }
    }

    private void DrawManualModeUI(CosmosAnimationTrack track)
    {
        // 검색 필터
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));

        EditorGUI.BeginChangeCheck();
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (EditorGUI.EndChangeCheck())
        {
            FilterCharacterList();
        }

        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            searchFilter = "";
            needsRefresh = true;
        }

        EditorGUILayout.EndHorizontal();

        // 리스트 갱신
        if (needsRefresh)
        {
            RefreshCharacterList();
            needsRefresh = false;
        }

        // 로딩 상태
        if (isSearching)
        {
            EditorGUILayout.HelpBox("Searching characters...", MessageType.Info);
        }
        else
        {
            DrawCharacterList(track);
        }
    }

    private void DrawSceneModeUI(CosmosAnimationTrack track)
    {
        // Scene에서 찾기 버튼
        if (GUILayout.Button("Find Characters in Scene"))
        {
            FindCharactersInScene(track);
        }

        // 현재 선택된 캐릭터
        if (!string.IsNullOrEmpty(track.CachedCharacterName))
        {
            EditorGUILayout.LabelField($"Selected: {track.CachedCharacterName}");
        }
    }

    private void DrawCharacterList(CosmosAnimationTrack track)
    {
        if (availableCharacterKeys.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No characters found.\n" +
                "Check Addressable labels: [character, prefab]",
                MessageType.Warning);
            return;
        }

        showCharacterList = EditorGUILayout.Foldout(showCharacterList,
            $"Available Characters ({availableCharacterKeys.Count})", true);

        if (!showCharacterList) return;

        // 현재 선택 찾기
        if (selectedCharacterIndex == -1 && !string.IsNullOrEmpty(track.SelectedCharacterKey))
        {
            selectedCharacterIndex = availableCharacterKeys.IndexOf(track.SelectedCharacterKey);
        }

        // 스크롤 리스트
        float listHeight = Mathf.Min(availableCharacterKeys.Count * 22f, 200f);
        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Height(listHeight));

        for (int i = 0; i < availableCharacterKeys.Count; i++)
        {
            bool isSelected = (i == selectedCharacterIndex);

            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 캐릭터 이름 버튼
            string displayName = availableCharacterNames[i];
            if (isSelected) displayName = "▶ " + displayName;

            if (GUILayout.Button(displayName, EditorStyles.label))
            {
                SelectCharacter(track, i);
            }

            // 미리보기 버튼
            if (GUILayout.Button("👁", GUILayout.Width(25)))
            {
                PreviewCharacter(availableCharacterKeys[i]);
            }

            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawBindingStatusSection(CosmosAnimationTrack track)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Binding Status", EditorStyles.miniBoldLabel);

        // 상태 아이콘과 메시지
        string statusIcon = GetStatusIcon(track.CurrentBindingStatus);
        Color statusColor = GetStatusColor(track.CurrentBindingStatus);

        GUI.color = statusColor;
        EditorGUILayout.LabelField($"{statusIcon} {track.CurrentBindingStatus}", EditorStyles.boldLabel);
        GUI.color = Color.white;

        // 에러 메시지
        if (!string.IsNullOrEmpty(track.BindingErrorMessage))
        {
            EditorGUILayout.HelpBox(track.BindingErrorMessage, MessageType.Error);
        }

        // 바인딩 정보
        if (track.CurrentBindingStatus == CosmosAnimationTrack.BindingStatus.Bound)
        {
            DrawBoundInstanceInfo(track);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBoundInstanceInfo(CosmosAnimationTrack track)
    {
        EditorGUILayout.Space();

        // Prefab 정보
        if (track.CharacterPrefab != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab:", GUILayout.Width(60));
            GUI.enabled = false;
            EditorGUILayout.ObjectField(track.CharacterPrefab, typeof(GameObject), false);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // Instance 정보
        if (track.BoundCharacterInstance != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Instance:", GUILayout.Width(60));
            GUI.enabled = false;
            EditorGUILayout.ObjectField(track.BoundCharacterInstance, typeof(GameObject), true);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // Animator 정보
        if (track.BoundAnimator != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Animator:", GUILayout.Width(60));
            GUI.enabled = false;
            EditorGUILayout.ObjectField(track.BoundAnimator, typeof(Animator), true);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Controller 정보
            if (track.BoundAnimator.runtimeAnimatorController != null)
            {
                EditorGUILayout.LabelField(
                    $"Controller: {track.BoundAnimator.runtimeAnimatorController.name}",
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawActionButtons(CosmosAnimationTrack track)
    {
        EditorGUILayout.BeginHorizontal();

        // Refresh 버튼
        if (GUILayout.Button("🔄 Refresh"))
        {
            if (track.CharacterSource == CosmosAnimationTrack.CharacterSourceType.Auto)
            {
                DetectCharacterName();
            }
            needsRefresh = true;
        }

        // Bind 버튼
        GUI.enabled = !string.IsNullOrEmpty(track.CachedCharacterName) &&
                     track.CurrentBindingStatus != CosmosAnimationTrack.BindingStatus.Bound;
        if (GUILayout.Button("🔗 Bind Character"))
        {
            BindCharacter(track);
        }
        GUI.enabled = true;

        // Clear 버튼
        GUI.enabled = track.CurrentBindingStatus == CosmosAnimationTrack.BindingStatus.Bound;
        if (GUILayout.Button("❌ Clear"))
        {
            ClearBinding(track);
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        // Auto Bind 체크박스
        if (track.CharacterSource == CosmosAnimationTrack.CharacterSourceType.Auto)
        {
            EditorGUILayout.Space();
            bool autoBind = EditorPrefs.GetBool("AnimationTrack_AutoBind", true);

            EditorGUI.BeginChangeCheck();
            autoBind = EditorGUILayout.Toggle("Auto Bind on Play", autoBind);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("AnimationTrack_AutoBind", autoBind);
            }
        }
    }

    #endregion

    #region Core Logic
    private string GetTimelineName()
    {
        var track = (AnimationTrack)target;
        return track.timelineAsset?.name ?? "";
    }

    private string ExtractCharacterName()
    {
        string timelineName = GetTimelineName();

        if (string.IsNullOrEmpty(timelineName))
            return "";

        // 패턴 매칭
        if (timelineName.EndsWith("_Timeline"))
            return timelineName.Substring(0, timelineName.Length - "_Timeline".Length);

        if (timelineName.EndsWith("Timeline"))
            return timelineName.Substring(0, timelineName.Length - "Timeline".Length);

        return timelineName;
    }


    private void FindTimelineContext()
    {
        // PlayableDirector 찾기
        var directors = UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var director in directors)
        {
            if (director.playableAsset == null) continue;

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) continue;

            // 이 Timeline에 현재 Track이 포함되어 있는지 확인
            var track = (AnimationTrack)target;
            bool hasThisTrack = timeline.GetOutputTracks().Any(t => t == track);

            if (hasThisTrack)
            {
                cachedDirector = director;
                cachedTimeline = timeline;
                return;
            }
        }
    }

    private void DetectCharacterName()
    {
        var track = (CosmosAnimationTrack)target;

        if (cachedTimeline == null)
        {
            FindTimelineContext();
        }

        if (cachedTimeline != null)
        {
            string characterName = AnimationTrackHelper.ExtractCharacterNameFromTimeline(cachedTimeline);

            if (!string.IsNullOrEmpty(characterName))
            {
                track.SetCharacterName(characterName);
                Debug.Log($"[AnimTrack] Detected character: {characterName}");

                // 자동 바인딩 시도
                if (EditorPrefs.GetBool("AnimationTrack_AutoBind", true))
                {
                    EditorApplication.delayCall += () => BindCharacter(track);
                }
            }
            else
            {
                Debug.LogWarning($"[AnimTrack] Could not extract character name from: {cachedTimeline.name}");
            }
        }
        else
        {
            Debug.LogWarning("[AnimTrack] Timeline context not found");
        }
    }

    private async void RefreshCharacterList()
    {
        var track = (CosmosAnimationTrack)target;
        isSearching = true;

        try
        {
            // Addressable 라벨 구성
            List<object> searchKeys = new List<object>();
            searchKeys.Add("character");
            searchKeys.Add("prefab");

            Debug.Log($"[AnimTrack] Searching characters with labels: {string.Join(", ", searchKeys)}");

            // Addressable 검색
            var handle = Addressables.LoadResourceLocationsAsync(
                searchKeys,
                Addressables.MergeMode.Intersection,
                typeof(GameObject));

            var locations = await handle.Task;

            // 결과 처리
            availableCharacterKeys.Clear();
            availableCharacterNames.Clear();

            foreach (var location in locations)
            {
                string key = location.PrimaryKey;

                // "_prefab"으로 끝나는 것만
                if (!key.EndsWith("_prefab"))
                    continue;

                // 캐릭터명 추출
                string name = ExtractCharacterNameFromKey(key);

                availableCharacterKeys.Add(key);
                availableCharacterNames.Add(name);
            }

            // 정렬
            if (availableCharacterKeys.Count > 0)
            {
                var sorted = availableCharacterKeys
                    .Zip(availableCharacterNames, (k, n) => new { Key = k, Name = n })
                    .OrderBy(x => x.Name)
                    .ToList();

                availableCharacterKeys = sorted.Select(x => x.Key).ToList();
                availableCharacterNames = sorted.Select(x => x.Name).ToList();
            }

            Debug.Log($"[AnimTrack] Found {availableCharacterKeys.Count} characters");

            Addressables.Release(handle);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AnimTrack] Search failed: {e.Message}");
        }
        finally
        {
            isSearching = false;
            Repaint();
        }
    }

    private void FilterCharacterList()
    {
        if (string.IsNullOrEmpty(searchFilter))
        {
            needsRefresh = true;
            return;
        }

        var filtered = availableCharacterKeys
            .Zip(availableCharacterNames, (k, n) => new { Key = k, Name = n })
            .Where(x => x.Name.ToLower().Contains(searchFilter.ToLower()))
            .ToList();

        availableCharacterKeys = filtered.Select(x => x.Key).ToList();
        availableCharacterNames = filtered.Select(x => x.Name).ToList();

        Repaint();
    }

    private void SelectCharacter(CosmosAnimationTrack track, int index)
    {
        if (index < 0 || index >= availableCharacterKeys.Count) return;

        Undo.RecordObject(track, "Select Character");

        selectedCharacterIndex = index;
        string selectedKey = availableCharacterKeys[index];
        string selectedName = availableCharacterNames[index];

        track.SetSelectedCharacter(selectedKey);
        track.SetCharacterName(selectedName);

        // 자동 바인딩
        if (EditorPrefs.GetBool("AnimationTrack_AutoBind", true))
        {
            BindCharacter(track);
        }
    }

    private void FindCharactersInScene(CosmosAnimationTrack track)
    {
        // Animator를 가진 모든 오브젝트 찾기
        var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.InstanceID);

        if (animators.Length == 0)
        {
            EditorUtility.DisplayDialog("No Characters",
                "No characters with Animator found in scene", "OK");
            return;
        }

        // 선택 메뉴 생성
        GenericMenu menu = new GenericMenu();

        foreach (var animator in animators)
        {
            string name = animator.gameObject.name;
            menu.AddItem(new GUIContent(name), false, () =>
            {
                track.SetCharacterName(name);
                track.SetBoundInstance(animator.gameObject);
                BindToDirector(track, animator);
            });
        }

        menu.ShowAsContext();
    }

    #endregion

    #region Binding Logic

    private async void BindCharacter(CosmosAnimationTrack track)
    {
        if (string.IsNullOrEmpty(track.CachedCharacterName))
        {
            Debug.LogError("[AnimTrack] No character selected");
            return;
        }

        isBinding = true;
        track.UpdateBindingStatus(CosmosAnimationTrack.BindingStatus.Searching);

        try
        {
            // 1. Scene에서 먼저 찾기
            GameObject sceneInstance = AnimationTrackHelper.FindCharacterInScene(track.CachedCharacterName);

            if (sceneInstance != null)
            {
                Debug.Log($"[AnimTrack] Found in scene: {sceneInstance.name}");
                track.SetBoundInstance(sceneInstance);
                BindToDirector(track, sceneInstance.GetComponent<Animator>());
                return;
            }

            // 2. Addressable로 로드
            track.UpdateBindingStatus(CosmosAnimationTrack.BindingStatus.Loading);

            string addressableKey = track.SelectedCharacterKey;
            if (string.IsNullOrEmpty(addressableKey))
            {
                addressableKey = AnimationTrackHelper.GenerateAddressableKey(track.CachedCharacterName);
            }

            Debug.Log($"[AnimTrack] Loading from Addressable: {addressableKey}");

            var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
            var prefab = await handle.Task;

            if (prefab != null)
            {
                // 인스턴스 생성
                GameObject instance = GameObject.Instantiate(prefab);
                instance.name = track.CachedCharacterName + "_Timeline";

                // 비활성화 (Timeline이 제어)
                instance.SetActive(false);

                // 바인딩
                track.SetBoundInstance(instance);
                track.SetSelectedCharacter(addressableKey, prefab);
                BindToDirector(track, instance.GetComponent<Animator>());

                Debug.Log($"[AnimTrack] Created and bound: {instance.name}");
            }
            else
            {
                throw new Exception($"Failed to load prefab: {addressableKey}");
            }

            Addressables.Release(handle);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AnimTrack] Binding failed: {e.Message}");
            track.UpdateBindingStatus(CosmosAnimationTrack.BindingStatus.Error, e.Message);
        }
        finally
        {
            isBinding = false;
            Repaint();
        }
    }

    private void BindToDirector(CosmosAnimationTrack track, Animator animator)
    {
        if (cachedDirector == null || animator == null) return;

        // Track에 Animator 바인딩
        cachedDirector.SetGenericBinding(track, animator);

        track.UpdateBindingStatus(CosmosAnimationTrack.BindingStatus.Bound);

        Debug.Log($"[AnimTrack] Bound to Director: {animator.gameObject.name}");

        // Timeline 창 갱신
        RefreshTimelineWindow();
    }

    private void ClearBinding(CosmosAnimationTrack track)
    {
        if (cachedDirector != null)
        {
            cachedDirector.SetGenericBinding(track, null);
        }

        // 생성된 인스턴스 삭제 (옵션)
        if (track.BoundCharacterInstance != null)
        {
            if (track.BoundCharacterInstance.name.Contains("(Clone)") ||
                track.BoundCharacterInstance.name.Contains("_Timeline"))
            {
                if (EditorUtility.DisplayDialog("Delete Instance?",
                    "Delete the created character instance?", "Yes", "No"))
                {
                    DestroyImmediate(track.BoundCharacterInstance);
                }
            }
        }

        track.ClearBinding();
        RefreshTimelineWindow();
    }

    #endregion

    #region Helper Methods

    private void InitializeStyles()
    {
        statusStyle = new GUIStyle(EditorStyles.boldLabel);
    }

    private string GetModeDescription(CosmosAnimationTrack.CharacterSourceType mode)
    {
        switch (mode)
        {
            case CosmosAnimationTrack.CharacterSourceType.Auto:
                return "Automatically detects character from Timeline name";
            case CosmosAnimationTrack.CharacterSourceType.Manual:
                return "Manually select character from Addressable list";
            case CosmosAnimationTrack.CharacterSourceType.Scene:
                return "Find and bind character from current scene";
            default:
                return "";
        }
    }

    private string GetStatusIcon(CosmosAnimationTrack.BindingStatus status)
    {
        switch (status)
        {
            case CosmosAnimationTrack.BindingStatus.Bound:
                return "✅";
            case CosmosAnimationTrack.BindingStatus.Searching:
                return "🔍";
            case CosmosAnimationTrack.BindingStatus.Loading:
                return "🔄";
            case CosmosAnimationTrack.BindingStatus.Missing:
                return "⚠️";
            case CosmosAnimationTrack.BindingStatus.Error:
                return "❌";
            default:
                return "⭕";
        }
    }

    private Color GetStatusColor(CosmosAnimationTrack.BindingStatus status)
    {
        switch (status)
        {
            case CosmosAnimationTrack.BindingStatus.Bound:
                return successColor;
            case CosmosAnimationTrack.BindingStatus.Missing:
                return warningColor;
            case CosmosAnimationTrack.BindingStatus.Error:
                return errorColor;
            default:
                return Color.gray;
        }
    }

    private string ExtractCharacterNameFromKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";

        // "Char_Hero_prefab" → "Hero"
        if (key.StartsWith("Char_") && key.EndsWith("_prefab"))
        {
            return key.Replace("Char_", "").Replace("_prefab", "");
        }

        // "Character_Hero" → "Hero"
        if (key.StartsWith("Character_"))
        {
            return key.Replace("Character_", "");
        }

        // 기본: 파일명
        return System.IO.Path.GetFileNameWithoutExtension(key);
    }

    private void RefreshTimelineWindow()
    {
        var timelineWindow = EditorWindow.GetWindow(
            System.Type.GetType("UnityEditor.Timeline.TimelineWindow,Unity.Timeline.Editor"),
            false, null, false);

        if (timelineWindow != null)
        {
            timelineWindow.Repaint();
        }
    }

    private async void PreviewCharacter(string addressableKey)
    {
        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
            var prefab = await handle.Task;

            if (prefab != null)
            {
                var preview = GameObject.Instantiate(prefab);
                preview.name = $"[PREVIEW] {prefab.name}";

                // 5초 후 자동 삭제
                var startTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += () =>
                {
                    if (EditorApplication.timeSinceStartup - startTime > 5.0)
                    {
                        if (preview != null)
                            DestroyImmediate(preview);
                    }
                };

                Selection.activeGameObject = preview;
                SceneView.lastActiveSceneView?.Focus();
            }

            Addressables.Release(handle);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AnimTrack] Preview failed: {e.Message}");
        }
    }

    #endregion

    #region Debug

    private void DrawDebugInfo(CosmosAnimationTrack track)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug Info", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Source: {track.CharacterSource}");
        EditorGUILayout.LabelField($"Character Name: {track.CachedCharacterName}");
        EditorGUILayout.LabelField($"Selected Key: {track.SelectedCharacterKey}");
        EditorGUILayout.LabelField($"Status: {track.CurrentBindingStatus}");
        EditorGUILayout.LabelField($"Has Instance: {track.BoundCharacterInstance != null}");
        EditorGUILayout.LabelField($"Has Animator: {track.BoundAnimator != null}");

        if (cachedDirector != null)
        {
            var binding = cachedDirector.GetGenericBinding(track);
            EditorGUILayout.LabelField($"Director Binding: {binding != null}");
        }

        EditorGUILayout.EndVertical();
    }

    #endregion
}





#endif