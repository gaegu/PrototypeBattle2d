
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.ComponentModel;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using System;



#if UNITY_EDITOR
using UnityEditor;
#endif
[TrackColor(1f, 0.5f, 0f)]
[TrackClipType(typeof(CosmosEffectClip))]
[DisplayName("*COSMOS* Track/Add Effect Track")]
public class CosmosEffectTrack : TrackAsset
{
    // 이펙트 소스 타입
    public enum EffectSourceType
    {
        Character,  // 캐릭터 전용
        Common      // 공용
    }

    [SerializeField, HideInInspector]
    private GameObject templatePrefab; // 호환성을 위해 유지 (나중에 제거 가능)

    [SerializeField, HideInInspector]
    private string templateAddressableKey; // 선택된 Addressable 키

    [SerializeField, HideInInspector]
    private EffectSourceType effectSource = EffectSourceType.Character;

    [SerializeField, HideInInspector]
    private string cachedCharacterName = "";

    // 프로퍼티
    public string SelectedAddressableKey => templateAddressableKey;
    public EffectSourceType EffectSource => effectSource;
    public string CachedCharacterName => cachedCharacterName;

    /// <summary>
    /// 이펙트 선택 (리스트에서 선택시 호출)
    /// </summary>
    public void SetSelectedEffect(string addressableKey, GameObject prefab = null)
    {
        templateAddressableKey = addressableKey;
        templatePrefab = prefab; // 미리보기용 (옵션)

        // Track 이름 업데이트
        if (!string.IsNullOrEmpty(addressableKey))
        {
            string effectName = System.IO.Path.GetFileNameWithoutExtension(addressableKey);
            this.name = effectName + "_Track";
        }

#if UNITY_EDITOR
        ApplyTemplateToAllClips();
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// 이펙트 소스 변경
    /// </summary>
    public void SetEffectSource(EffectSourceType source)
    {
        effectSource = source;
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// 캐릭터명 설정
    /// </summary>
    public void SetCharacterName(string characterName)
    {
        cachedCharacterName = characterName;
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// 기존 메서드 호환성 유지
    /// </summary>
    public void SetTemplatePrefab(GameObject prefab, string addressableKey)
    {
        templatePrefab = prefab;
        templateAddressableKey = addressableKey;

        if (prefab != null)
        {
            this.name = prefab.name + "_Track";
        }

#if UNITY_EDITOR
        ApplyTemplateToAllClips();
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// 모든 Clip에 템플릿 적용
    /// </summary>
    private void ApplyTemplateToAllClips()
    {
        var clips = GetClips();
        foreach (var clip in clips)
        {
            var effectClip = clip.asset as CosmosEffectClip;
            if (effectClip != null)
            {
                effectClip.effectPrefab = templatePrefab;
                effectClip.effectAddressableKey = templateAddressableKey;

                if (!string.IsNullOrEmpty(templateAddressableKey))
                {
                    clip.displayName = System.IO.Path.GetFileNameWithoutExtension(templateAddressableKey);
                }

#if UNITY_EDITOR
                EditorUtility.SetDirty(effectClip);
#endif
            }
        }
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);

        var effectClip = clip.asset as CosmosEffectClip;
        if (effectClip != null && !string.IsNullOrEmpty(templateAddressableKey))
        {
            effectClip.effectPrefab = templatePrefab;
            effectClip.effectAddressableKey = templateAddressableKey;
            clip.displayName = System.IO.Path.GetFileNameWithoutExtension(templateAddressableKey);

#if UNITY_EDITOR
            EditorUtility.SetDirty(effectClip);
#endif
        }
    }

    // 기존 메서드들 호환성 유지
    public GameObject GetTemplatePrefab() => templatePrefab;
    public string GetTemplateAddressableKey() => templateAddressableKey;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return base.CreateTrackMixer(graph, go, inputCount);
    }

    public void AutoBindPrefab(PlayableDirector director)
    {
        if (templatePrefab != null && director != null)
        {
            var currentBinding = director.GetGenericBinding(this);
            if (currentBinding == null)
            {
                GameObject bindTarget = GameObject.Find(templatePrefab.name);
                if (bindTarget == null)
                {
                    bindTarget = GameObject.Instantiate(templatePrefab);
                    bindTarget.name = templatePrefab.name;
                    bindTarget.SetActive(false);
                }
                director.SetGenericBinding(this, bindTarget);
            }
        }
    }
}

/// <summary>
/// EffectTrack용 Custom Editor
/// </summary>
#if UNITY_EDITOR
[CustomEditor(typeof(CosmosEffectTrack))]
public class CosmosEffectTrackEditor : Editor
{
    // 캐시 필드
    private List<string> availableEffectKeys = new List<string>();
    private List<string> availableEffectNames = new List<string>();
    private Dictionary<string, GameObject> preloadedPrefabs = new Dictionary<string, GameObject>();

    // UI 상태
    private bool isSearching = false;
    private string searchFilter = "";
    private int selectedIndex = -1;
    private bool showEffectList = true;
    private Vector2 scrollPosition;

    // 캐시 관리
    private string lastSearchedCharacter = "";
    private CosmosEffectTrack.EffectSourceType lastSearchedSource;
    private bool needsRefresh = true;

    private void OnEnable()
    {
        var track = (CosmosEffectTrack)target;

        // Timeline에서 캐릭터명 추출
        ExtractCharacterNameFromTimeline(track);

        // 초기 검색
        needsRefresh = true;
    }

    public override void OnInspectorGUI()
    {
        var track = (CosmosEffectTrack)target;

        EditorGUILayout.LabelField("Effect Track Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 캐릭터명 추출이 필요한 경우
        if (string.IsNullOrEmpty(track.CachedCharacterName) &&
            track.EffectSource == CosmosEffectTrack.EffectSourceType.Character)
        {
            ExtractCharacterNameFromTimeline(track);
        }

        // Effect Source 선택
        DrawEffectSourceSelection(track);

        // 캐릭터명 표시 (Character 모드일 때만)
        if (track.EffectSource == CosmosEffectTrack.EffectSourceType.Character)
        {
            DrawCharacterInfo(track);
        }

        EditorGUILayout.Space();

        // 검색 필터
        DrawSearchFilter();

        // 리스트 갱신이 필요한 경우
        if (needsRefresh || HasSourceChanged(track))
        {
            RefreshEffectList();
            needsRefresh = false;
        }

        // 로딩 상태 또는 리스트 표시
        if (isSearching)
        {
            EditorGUILayout.HelpBox("Searching effects...", MessageType.Info);
        }
        else
        {
            DrawEffectList(track);
        }

        EditorGUILayout.Space();

        // 선택된 이펙트 정보
        DrawSelectedEffectInfo(track);

        // 액션 버튼들
        DrawActionButtons(track);
    }

    #region UI Drawing Methods

    private void DrawEffectSourceSelection(CosmosEffectTrack track)
    {
        EditorGUI.BeginChangeCheck();
        var newSource = (CosmosEffectTrack.EffectSourceType)EditorGUILayout.EnumPopup(
            "Effect Source", track.EffectSource);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(track, "Change Effect Source");
            track.SetEffectSource(newSource);
            track.SetSelectedEffect("", null); // 소스 변경시 선택 초기화
            needsRefresh = true;
        }
    }

    private void DrawCharacterInfo(CosmosEffectTrack track)
    {
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = false;
        EditorGUILayout.TextField("Character", track.CachedCharacterName);
        GUI.enabled = true;

        if (GUILayout.Button("Detect", GUILayout.Width(60)))
        {
            ExtractCharacterNameFromTimeline(track);
            needsRefresh = true;
        }

        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(track.CachedCharacterName))
        {
            EditorGUILayout.HelpBox(
                "Character name not detected. Timeline should be named as 'CharacterName_Timeline'",
                MessageType.Warning);
        }
    }

    private void DrawSearchFilter()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));

        EditorGUI.BeginChangeCheck();
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (EditorGUI.EndChangeCheck())
        {
            FilterEffectList();
        }

        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            searchFilter = "";
            FilterEffectList();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEffectList(CosmosEffectTrack track)
    {
        if (availableEffectKeys.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No effects found. Check Addressable labels:\n" +
                (track.EffectSource == CosmosEffectTrack.EffectSourceType.Character
                    ? $"Required labels: [{track.CachedCharacterName}, fx, battle]"
                    : "Required labels: [shared, fx, battle]"),
                MessageType.Warning);
            return;
        }

        showEffectList = EditorGUILayout.Foldout(showEffectList,
            $"Available Effects ({availableEffectKeys.Count})", true);

        if (!showEffectList) return;

        // 현재 선택된 항목 찾기
        if (selectedIndex == -1 && !string.IsNullOrEmpty(track.SelectedAddressableKey))
        {
            selectedIndex = availableEffectKeys.IndexOf(track.SelectedAddressableKey);
        }

        // 스크롤 가능한 리스트
        float listHeight = Mathf.Min(availableEffectKeys.Count * 22f, 200f);
        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Height(listHeight));

        for (int i = 0; i < availableEffectKeys.Count; i++)
        {
            bool isSelected = (i == selectedIndex);

            // 선택된 항목 하이라이트
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 이펙트 이름 버튼
            string displayName = availableEffectNames[i];
            if (isSelected) displayName = "▶ " + displayName;

            // ✨ 프리팹 아이콘 추가 (선택사항)
            displayName = "🎯 " + displayName;  // 또는 다른 아이콘

            if (GUILayout.Button(displayName, EditorStyles.label))
            {
                SelectEffect(track, i);
            }

            // 미리보기 버튼
            if (GUILayout.Button("👁", GUILayout.Width(25)))
            {
                PreviewEffect(availableEffectKeys[i]);
            }

            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectedEffectInfo(CosmosEffectTrack track)
    {
        if (string.IsNullOrEmpty(track.SelectedAddressableKey)) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Selected Effect", EditorStyles.boldLabel);

        string effectName = System.IO.Path.GetFileNameWithoutExtension(track.SelectedAddressableKey);
        EditorGUILayout.LabelField("Name:", effectName);

        GUI.enabled = false;
        EditorGUILayout.TextField("Addressable Key:", track.SelectedAddressableKey);
        GUI.enabled = true;

        var clips = track.GetClips().Count();
        EditorGUILayout.LabelField($"Applied to: {clips} clips");

        EditorGUILayout.EndVertical();
    }

    private void DrawActionButtons(CosmosEffectTrack track)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔄 Refresh List"))
        {
            needsRefresh = true;
        }

        GUI.enabled = !string.IsNullOrEmpty(track.SelectedAddressableKey);
        if (GUILayout.Button("Apply to All Clips"))
        {
            ApplyToAllClips(track);
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Core Logic

    private void ExtractCharacterNameFromTimeline(CosmosEffectTrack track)
    {
        // PlayableDirector 찾기
        var directors = UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        foreach (var director in directors)
        {
            if (director.playableAsset == null) continue;

            var timeline = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
            if (timeline == null) continue;

            // 이 Timeline에 현재 Track이 포함되어 있는지 확인
            bool hasThisTrack = timeline.GetOutputTracks().Any(t => t == track);
            if (!hasThisTrack) continue;

            string timelineName = timeline.name;

            // "캐릭터명_Timeline" 패턴에서 캐릭터명 추출
            if (timelineName.EndsWith("_Timeline"))
            {
                string characterName = timelineName.Replace("_Timeline", "");
                track.SetCharacterName(characterName);
                Debug.Log($"[EffectTrack] Detected character: {characterName}");
                return;
            }

            // GameObject 이름에서 시도
            string goName = director.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
            {
                track.SetCharacterName(goName);
                Debug.Log($"[EffectTrack] Using GameObject name: {goName}");
                return;
            }
        }

        Debug.LogWarning("[EffectTrack] Could not detect character name");
    }

    private async void RefreshEffectList()
    {
        var track = (CosmosEffectTrack)target;
        isSearching = true;

        // 캐시 키 업데이트
        lastSearchedCharacter = track.CachedCharacterName;
        lastSearchedSource = track.EffectSource;

        try
        {
            // 라벨 구성
            List<object> searchKeys = new List<object>();

            if (track.EffectSource == CosmosEffectTrack.EffectSourceType.Character)
            {
                if (string.IsNullOrEmpty(track.CachedCharacterName))
                {
                    isSearching = false;
                    availableEffectKeys.Clear();
                    availableEffectNames.Clear();
                    Repaint();
                    return;
                }

                // 캐릭터 라벨 조합
                searchKeys.Add(track.CachedCharacterName.ToLower());
                searchKeys.Add("fx");
                searchKeys.Add("battle");
            }
            else
            {
                // 공용 라벨 조합
                searchKeys.Add("shared");
                searchKeys.Add("fx");
                searchKeys.Add("battle");
            }

            Debug.Log($"[EffectTrack] Searching with labels: {string.Join(", ", searchKeys)}");

            // Addressable 검색
            var handle = Addressables.LoadResourceLocationsAsync(
                searchKeys,
                Addressables.MergeMode.Intersection,
                typeof(GameObject));

            var locations = await handle.Task;

            // 결과 처리
            availableEffectKeys.Clear();
            availableEffectNames.Clear();
            preloadedPrefabs.Clear();

            foreach (var location in locations)
            {
                string key = location.PrimaryKey;

                // ✨ 프리팹 필터링 추가 - "_prefab"으로 끝나는 것만
                if (!key.EndsWith("_prefab"))
                    continue;

                string name = System.IO.Path.GetFileNameWithoutExtension(key);

                if (name.EndsWith("_prefab"))
                {
                    name = name.Replace("_prefab", "");  // 표시할 때는 _prefab 제거
                }


                availableEffectKeys.Add(key);
                availableEffectNames.Add(name);
            }

            // 정렬
            if (availableEffectKeys.Count > 0)
            {
                var sorted = availableEffectKeys
                    .Zip(availableEffectNames, (k, n) => new { Key = k, Name = n })
                    .OrderBy(x => x.Name)
                    .ToList();

                availableEffectKeys = sorted.Select(x => x.Key).ToList();
                availableEffectNames = sorted.Select(x => x.Name).ToList();
            }

            Debug.Log($"[EffectTrack] Found {availableEffectKeys.Count} effects");

            // 현재 선택 항목 찾기
            if (!string.IsNullOrEmpty(track.SelectedAddressableKey))
            {
                selectedIndex = availableEffectKeys.IndexOf(track.SelectedAddressableKey);
            }

            Addressables.Release(handle);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EffectTrack] Search failed: {e.Message}");
        }
        finally
        {
            isSearching = false;
            Repaint();
        }
    }

    private void FilterEffectList()
    {
        if (string.IsNullOrEmpty(searchFilter))
        {
            needsRefresh = true;
            return;
        }

        // 필터링은 현재 리스트에서만 수행
        var filtered = availableEffectKeys
            .Zip(availableEffectNames, (k, n) => new { Key = k, Name = n })
            .Where(x => x.Name.ToLower().Contains(searchFilter.ToLower()) ||
                        x.Key.ToLower().Contains(searchFilter.ToLower()))  // ✨ Key에서도 검색
            .ToList();

        availableEffectKeys = filtered.Select(x => x.Key).ToList();
        availableEffectNames = filtered.Select(x => x.Name).ToList();

        Repaint();
    }

    private void SelectEffect(CosmosEffectTrack track, int index)
    {
        if (index < 0 || index >= availableEffectKeys.Count) return;

        Undo.RecordObject(track, "Select Effect");

        selectedIndex = index;
        string selectedKey = availableEffectKeys[index];

        // Prefab 로드 시도 (옵션)
        GameObject prefab = null;
        if (preloadedPrefabs.ContainsKey(selectedKey))
        {
            prefab = preloadedPrefabs[selectedKey];
        }

        track.SetSelectedEffect(selectedKey, prefab);
        ApplyToAllClips(track);

        RefreshTimelineWindow();
    }

    private void ApplyToAllClips(CosmosEffectTrack track)
    {
        var clips = track.GetClips();
        int updatedCount = 0;

        foreach (var clip in clips)
        {
            var effectClip = clip.asset as CosmosEffectClip;
            if (effectClip != null)
            {
                effectClip.effectAddressableKey = track.SelectedAddressableKey;
                effectClip.effectPrefab = track.GetTemplatePrefab();

                string effectName = System.IO.Path.GetFileNameWithoutExtension(track.SelectedAddressableKey);
                clip.displayName = effectName;

                EditorUtility.SetDirty(effectClip);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[EffectTrack] Updated {updatedCount} clips");
        }
    }

    private bool HasSourceChanged(CosmosEffectTrack track)
    {
        return track.CachedCharacterName != lastSearchedCharacter ||
               track.EffectSource != lastSearchedSource;
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

    private async void PreviewEffect(string addressableKey)
    {
        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
            var prefab = await handle.Task;

            if (prefab != null)
            {
                var preview = GameObject.Instantiate(prefab);
                preview.name = $"[PREVIEW] {prefab.name}";

                // 3초 후 자동 삭제
                var startTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += () =>
                {
                    if (EditorApplication.timeSinceStartup - startTime > 3.0)
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
            Debug.LogError($"[EffectTrack] Preview failed: {e.Message}");
        }
    }

    #endregion
}

// 메뉴 확장
public static class EffectClipMenuExtensions
{
    [MenuItem("*COSMOS*/Timeline/Effect Clip")]
    public static void CreateEffectClip()
    {
        var clip = ScriptableObject.CreateInstance<CosmosEffectClip>();

        if (Selection.activeObject is CosmosEffectTrack track)
        {
            if (!string.IsNullOrEmpty(track.SelectedAddressableKey))
            {
                clip.effectPrefab = track.GetTemplatePrefab();
                clip.effectAddressableKey = track.GetTemplateAddressableKey();
            }
        }

        ProjectWindowUtil.CreateAsset(clip, "New Effect Clip.asset");
    }
}

#endif
