using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.ComponentModel;

#if UNITY_EDITOR
using UnityEditor;
#endif

[TrackColor(0.5f, 0.8f, 0.3f)]  // 초록색 계열
[TrackClipType(typeof(AnimationPlayableAsset))]  // Unity 기본 Animation Clip 사용
[DisplayName("*COSMOS* Track/Add Animation Track")]
public class CosmosAnimationTrack : AnimationTrack
{
    // 캐릭터 소스 타입
    public enum CharacterSourceType
    {
        Auto,       // Timeline 이름에서 자동 감지
        Manual,     // 수동 선택
        Scene       // Scene에서 찾기
    }

    // 바인딩 상태
    public enum BindingStatus
    {
        NotBound,       // 바인딩 안됨
        Searching,      // 검색 중
        Loading,        // 로딩 중
        Bound,          // 정상 바인딩
        Missing,        // 인스턴스 없음
        Error           // 에러
    }

    [SerializeField, HideInInspector]
    private CharacterSourceType characterSource = CharacterSourceType.Auto;

    [SerializeField, HideInInspector]
    private string selectedCharacterKey = "";  // Addressable 키

    [SerializeField, HideInInspector]
    private string cachedCharacterName = "";  // 감지된 캐릭터명

    [SerializeField, HideInInspector]
    private GameObject characterPrefab;  // 로드된 프리팹 (캐시)

    [SerializeField, HideInInspector]
    private BindingStatus currentBindingStatus = BindingStatus.NotBound;

    [SerializeField, HideInInspector]
    private string bindingErrorMessage = "";

    // Runtime 필드 (Serialize 안함)
    private GameObject boundCharacterInstance;
    private Animator boundAnimator;

    #region Properties

    public CharacterSourceType CharacterSource => characterSource;
    public string SelectedCharacterKey => selectedCharacterKey;
    public string CachedCharacterName => cachedCharacterName;
    public GameObject CharacterPrefab => characterPrefab;
    public BindingStatus CurrentBindingStatus => currentBindingStatus;
    public string BindingErrorMessage => bindingErrorMessage;
    public GameObject BoundCharacterInstance => boundCharacterInstance;
    public Animator BoundAnimator => boundAnimator;

    #endregion

    #region Public Methods

    /// <summary>
    /// 캐릭터 소스 설정
    /// </summary>
    public void SetCharacterSource(CharacterSourceType source)
    {
        if (characterSource == source) return;

        characterSource = source;
        currentBindingStatus = BindingStatus.NotBound;
        bindingErrorMessage = "";

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        Debug.Log($"[AnimTrack] Character source changed to: {source}");
    }

    /// <summary>
    /// 캐릭터명 설정
    /// </summary>
    public void SetCharacterName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;

        cachedCharacterName = name;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        Debug.Log($"[AnimTrack] Character name set to: {name}");
    }

    /// <summary>
    /// 선택된 캐릭터 키 설정
    /// </summary>
    public void SetSelectedCharacter(string addressableKey, GameObject prefab = null)
    {
        selectedCharacterKey = addressableKey;
        characterPrefab = prefab;

        // 키에서 캐릭터명 추출
        if (!string.IsNullOrEmpty(addressableKey))
        {
            // "Char_Hero_prefab" → "Hero"
            string name = ExtractCharacterNameFromKey(addressableKey);
            if (!string.IsNullOrEmpty(name))
            {
                cachedCharacterName = name;

            }
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        Debug.Log($"[AnimTrack] Selected character: {addressableKey}");
    }

    /// <summary>
    /// 바인딩 상태 업데이트
    /// </summary>
    public void UpdateBindingStatus(BindingStatus status, string errorMessage = "")
    {
        currentBindingStatus = status;
        bindingErrorMessage = errorMessage;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// 인스턴스 바인딩 설정
    /// </summary>
    public void SetBoundInstance(GameObject instance)
    {
        boundCharacterInstance = instance;

        if (instance != null)
        {
            boundAnimator = instance.GetComponent<Animator>();
            if (boundAnimator == null)
            {
                boundAnimator = instance.GetComponentInChildren<Animator>();
            }

            currentBindingStatus = BindingStatus.Bound;
            bindingErrorMessage = "";

            Debug.Log($"[AnimTrack] Bound to instance: {instance.name}");
        }
        else
        {
            boundAnimator = null;
            currentBindingStatus = BindingStatus.NotBound;
            Debug.Log("[AnimTrack] Instance binding cleared");
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// 바인딩 클리어
    /// </summary>
    public void ClearBinding()
    {
        boundCharacterInstance = null;
        boundAnimator = null;
        currentBindingStatus = BindingStatus.NotBound;
        bindingErrorMessage = "";

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        Debug.Log("[AnimTrack] Binding cleared");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Addressable 키에서 캐릭터명 추출
    /// </summary>
    private string ExtractCharacterNameFromKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";

        // "Char_Hero_prefab" 패턴 처리
        if (key.StartsWith("Char_") && key.EndsWith("_prefab"))
        {
            string name = key.Replace("Char_", "").Replace("_prefab", "");
            return name;
        }

        // "Character_Hero" 패턴 처리
        if (key.StartsWith("Character_"))
        {
            return key.Replace("Character_", "");
        }

        // 기본: 파일명에서 추출
        return System.IO.Path.GetFileNameWithoutExtension(key);
    }

    #endregion

    #region Track Overrides

    /// <summary>
    /// Track Mixer 생성 (애니메이션 블렌딩 처리)
    /// </summary>
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        // 자동 바인딩 시도
        TryAutoBinding(go);

        // 기본 Mixer 생성
        return base.CreateTrackMixer(graph, go, inputCount);
    }

    /// <summary>
    /// 자동 바인딩 시도
    /// </summary>
    private void TryAutoBinding(GameObject director)
    {
        if (characterSource != CharacterSourceType.Auto) return;
        if (currentBindingStatus == BindingStatus.Bound) return;
        if (string.IsNullOrEmpty(cachedCharacterName)) return;

        // Editor에서만 자동 바인딩 시도
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    var editorType = System.Type.GetType("AnimationTrackEditor");
                    if (editorType != null)
                    {
                        Debug.Log("[AnimTrack] Auto-binding will be handled by Editor");
                    }
                }
            };
        }
#endif
    }

    /// <summary>
    /// Clip 생성시 호출
    /// </summary>
    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);

        // 기본 설정
        clip.displayName = "Animation";

        // 캐릭터명이 있으면 표시
        if (!string.IsNullOrEmpty(cachedCharacterName))
        {
            clip.displayName = $"{cachedCharacterName} Animation";
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(clip.asset);
#endif
    }

    #endregion

    #region Validation

    /// <summary>
    /// Track 유효성 검증
    /// </summary>
    public bool ValidateTrack()
    {
        bool isValid = true;
        List<string> errors = new List<string>();

        // 캐릭터 설정 확인
        if (string.IsNullOrEmpty(cachedCharacterName) && characterSource == CharacterSourceType.Auto)
        {
            errors.Add("Character name not detected");
            isValid = false;
        }

        // 바인딩 확인
        if (currentBindingStatus == BindingStatus.Error)
        {
            errors.Add($"Binding error: {bindingErrorMessage}");
            isValid = false;
        }

        // Animator 확인
        if (boundCharacterInstance != null && boundAnimator == null)
        {
            errors.Add("Character has no Animator component");
            isValid = false;
        }

        if (!isValid)
        {
            Debug.LogWarning($"[AnimTrack] Validation failed: {string.Join(", ", errors)}");
        }

        return isValid;
    }

    #endregion
}

#if UNITY_EDITOR

/// <summary>
/// AnimationTrack 헬퍼 클래스 (Editor 전용)
/// </summary>
public static class AnimationTrackHelper
{
    /// <summary>
    /// Timeline에서 캐릭터명 추출
    /// </summary>
    public static string ExtractCharacterNameFromTimeline(UnityEngine.Timeline.TimelineAsset timeline)
    {
        if (timeline == null) return "";

        string timelineName = timeline.name;

        // "캐릭터명_Timeline" 패턴
        if (timelineName.EndsWith("_Timeline"))
        {
            return timelineName.Replace("_Timeline", "");
        }

        // "캐릭터명Timeline" 패턴
        if (timelineName.EndsWith("Timeline"))
        {
            return timelineName.Replace("Timeline", "");
        }

        // 기본: Timeline 이름 그대로
        return timelineName;
    }

    /// <summary>
    /// Scene에서 캐릭터 찾기
    /// </summary>
    public static GameObject FindCharacterInScene(string characterName)
    {
        if (string.IsNullOrEmpty(characterName)) return null;

        // 1. 정확한 이름으로 찾기
        GameObject exact = GameObject.Find(characterName);
        if (exact != null && IsValidCharacter(exact))
        {
            return exact;
        }

        // 2. 다양한 패턴으로 찾기
        string[] patterns = new string[]
        {
            characterName,
            $"{characterName}_Timeline",
            $"{characterName}(Clone)",
            $"Char_{characterName}",
            $"Character_{characterName}"
        };

        foreach (string pattern in patterns)
        {
            GameObject found = GameObject.Find(pattern);
            if (found != null && IsValidCharacter(found))
            {
                return found;
            }
        }

        // 3. Animator 컴포넌트로 찾기
        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.InstanceID);
        foreach (var animator in animators)
        {
            if (animator.gameObject.name.Contains(characterName))
            {
                return animator.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// 유효한 캐릭터인지 확인
    /// </summary>
    private static bool IsValidCharacter(GameObject obj)
    {
        // Animator 컴포넌트 확인
        return obj.GetComponent<Animator>() != null ||
               obj.GetComponentInChildren<Animator>() != null;
    }

    /// <summary>
    /// Addressable 키 생성
    /// </summary>
    public static string GenerateAddressableKey(string characterName, bool isBattle = false)
    {
        if (string.IsNullOrEmpty(characterName)) return "";

        // 기본: "Char_캐릭터명_prefab"
        string key = $"Char_{characterName}_prefab";

        // Battle 전용
        if (isBattle)
        {
            key = $"Char_{characterName}_battle_prefab";
        }

        return key;
    }
}

#endif