using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BattleCharacterSystem;
using System.Linq;
using System.Threading.Tasks;
using CharacterSystem;
using BattleCharacterSystem.Timeline;
using Cosmos.Timeline.Playback.Editor;
using System.Diagnostics;

/// <summary>
/// Battle Character Preview Window
/// 캐릭터, 몬스터, 몬스터 그룹의 3D 프리뷰를 제공하는 에디터 윈도우
/// </summary>
public class BattlePreviewWindow : EditorWindow
{
    #region Enums & Constants

    // 프리뷰 타입
    public enum PreviewType
    {
        Character,
        Monster,
        MonsterGroup,
        Effect,
        Comparison  // 비교 모드
    }

    // 카메라 프리셋
    public enum CameraPreset
    {
        Default,
        CloseUp,
        FullBody,
        Action,
        Group
    }

    private const float ROTATION_SPEED = 20f;
    private const float MIN_CAMERA_DISTANCE = 1f;
    private const float MAX_CAMERA_DISTANCE = 20f;

    #endregion

    #region Fields

    // 프리뷰 타입
    private PreviewType currentPreviewType = PreviewType.Character;
    private CameraPreset currentCameraPreset = CameraPreset.Default;

    // 프리뷰 대상
    private BattleCharacterDataSO previewCharacter;
    private BattleMonsterDataSO previewMonster;
    private MonsterGroupDataSO previewGroup;

    // 프리뷰 인스턴스
    private GameObject previewInstance;
    private GameObject stageInstance;
    private PreviewRenderUtility previewRenderUtility;

    // 카메라 설정
    private Vector2 previewRotation = new Vector2(0, 0);
    private float previewDistance = 5f;
    private bool autoRotate = false;
    private float rotationSpeed = ROTATION_SPEED;
    private float fieldOfView = 30f;
    private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);


    // 2D 스프라이트 관련
    private CharacterSpriteAnimator currentSpriteAnimator;
    private bool is2DMode = false;
    private string[] spriteAnimationKeys;
    private int selectedSpriteAnimation = 0;
    private bool isPlayingSprite = false;




    // 애니메이션 컨트롤
    private Animator currentAnimator;
    private RuntimeAnimatorController originalController;
    private string[] availableAnimations;
    private int selectedAnimation = 0;
    private bool loopAnimation = true;
    private float animationSpeed = 1f;
    private bool isPlaying = false;

    // 이펙트 테스트
    private string effectPrefabPath = "";
    private GameObject currentEffect;
    private float effectScale = 1f;
    private Vector3 effectOffset = Vector3.zero;
    private bool attachEffectToModel = false;

    // UI 상태
    private Vector2 scrollPos;
    private bool showAnimationControls = true;
    private bool showEffectControls = false;
    private bool showGroupFormation = false;

    // 진형 프리뷰 (그룹용)
    private List<GameObject> groupMonsterInstances = new List<GameObject>();
    private FormationType previewFormation = FormationType.DefensiveBalance;
    private bool showFormationLines = true;
    private float formationSpacing = 1.5f;

    // 조명 설정
    private List<Light> previewLights = new List<Light>();
    private float mainLightIntensity = 1f;
    private float ambientIntensity = 0.5f;
    private Color mainLightColor = Color.white;
    private float rimLightIntensity = 0.3f;

    // 스크린샷
    private int screenshotWidth = 1920;
    private int screenshotHeight = 1080;
    private string screenshotPath = "Assets/Screenshots/";
    private bool transparentBackground = false;

    // 비교 모드
    private GameObject comparisonInstance;
    private BattleCharacterDataSO comparisonCharacter;
    private BattleMonsterDataSO comparisonMonster;
    private bool showComparison = false;

    // 에러 처리
    private string lastError = "";
    private bool hasError = false;



    // BattlePreviewWindow.cs에 추가할 필드
    private TimelineDataSO currentPlayingTimeline;
    private float timelinePlaybackTime = 0f;
    private bool isTimelinePlaying = false;
    private List<TimelineDataSO.ITimelineEvent> pendingEvents = new List<TimelineDataSO.ITimelineEvent>();
    private Dictionary<string, GameObject> spawnedEffects = new Dictionary<string, GameObject>();





    #endregion

    #region Unity Lifecycle

    [MenuItem("*COSMOS*/Battle/Preview Window")]
    public static void ShowWindow()
    {
        var window = GetWindow<BattlePreviewWindow>("Battle Preview");
        window.minSize = new Vector2(900, 700);
        window.Show();
    }

    public static void ShowWindow(BattleCharacterDataSO character = null,
                                 BattleMonsterDataSO monster = null,
                                 MonsterGroupDataSO group = null)
    {
        var window = GetWindow<BattlePreviewWindow>("Battle Preview");
        window.minSize = new Vector2(900, 700);

        if (character != null)
        {
            window.SetPreviewTarget(character);
        }
        else if (monster != null)
        {
            window.SetPreviewTarget(monster);
        }
        else if (group != null)
        {
            window.SetPreviewTarget(group);
        }

        window.Show();
    }

    private void OnEnable()
    {
        InitializePreview();
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    { 
        // AnimationMode 정리
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        EditorApplication.update -= OnEditorUpdate;
        CleanupPreview();

        StopTimelinePlayback();
        ClearSpawnedEffects();

        // PlaybackAdapter 정리 추가
        if (playbackAdapter != null)
        {
            DestroyImmediate(playbackAdapter.gameObject);
            playbackAdapter = null;
        }
    }

    private void OnDestroy()
    {
        CleanupPreview();
    }

    private void OnEditorUpdate()
    {
        if (autoRotate && previewInstance != null)
        {
            Repaint();
        }

        // 2D 애니메이션 업데이트
        if (is2DMode && currentSpriteAnimator != null && isPlayingSprite)
        {
            currentSpriteAnimator.OnUpdateAnimation();
            Repaint();
        }
    }

    #endregion

    #region Initialization

    private void InitializePreview()
    {
        try
        {
            if (previewRenderUtility == null)
            {
                previewRenderUtility = new PreviewRenderUtility();
                ConfigureCamera();

                // ✅ 追加ライト設定
                if (previewRenderUtility.lights.Length > 0)
                {
                    previewRenderUtility.lights[0].intensity = 1.5f;
                    previewRenderUtility.lights[0].color = Color.white;
                }
            }

            hasError = false;
            lastError = "";
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[BattlePreviewWindow] Initialization failed: {e.Message}");
            hasError = true;
            lastError = e.Message;
        }
    }

    private void ConfigureCamera()
    {
        if (previewRenderUtility?.camera == null) return;

        previewRenderUtility.camera.backgroundColor = backgroundColor;
        previewRenderUtility.camera.fieldOfView = fieldOfView;
        previewRenderUtility.camera.nearClipPlane = 0.3f;
        previewRenderUtility.camera.farClipPlane = 100f;
        previewRenderUtility.camera.clearFlags = transparentBackground ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;

        // ✅ Culling Mask를 Everything으로 설정
        previewRenderUtility.camera.cullingMask = -1; // Everything

        // ✅ URP용 추가 설정
        previewRenderUtility.camera.clearFlags = CameraClearFlags.SolidColor;
        previewRenderUtility.camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    }

    #endregion

    #region Cleanup

    private void CleanupPreview()
    {
        try
        {
            // PlaybackAdapter 정리 (여기도 추가)
            if (playbackAdapter != null)
            {
                DestroyImmediate(playbackAdapter.gameObject);
                playbackAdapter = null;
            }

            // 프리뷰 인스턴스 정리
            SafeDestroy(previewInstance);
            SafeDestroy(comparisonInstance);
            SafeDestroy(stageInstance);
            SafeDestroy(currentEffect);

            // 그룹 몬스터 정리
            foreach (var instance in groupMonsterInstances)
            {
                SafeDestroy(instance);
            }
            groupMonsterInstances.Clear();

            // 조명 정리
            foreach (var light in previewLights)
            {
                if (light != null)
                    SafeDestroy(light.gameObject);
            }
            previewLights.Clear();

            // PreviewRenderUtility 정리
            if (previewRenderUtility != null)
            {
                previewRenderUtility.Cleanup();
                previewRenderUtility = null;
            }

            // 강제 GC
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[BattlePreviewWindow] Cleanup failed: {e.Message}");
        }
    }

    private void SafeDestroy(Object obj)
    {
        if (obj != null)
        {
            DestroyImmediate(obj);
        }
    }

    private void CleanupCurrentModel()
    {
        SafeDestroy(previewInstance);
        previewInstance = null;
        currentAnimator = null;
        availableAnimations = null;

        // 2D 관련 정리 추가
        currentSpriteAnimator = null;
        is2DMode = false;
        spriteAnimationKeys = null;
        isPlayingSprite = false;
    }

    #endregion

    #region Model Loading

    private async void LoadCharacterModel()
    {
        if (previewCharacter == null) return;

        CleanupCurrentModel();

        try
        {
            GameObject prefab = await LoadPrefabAsync(previewCharacter);

            if (prefab != null)
            {
                previewInstance = Instantiate(prefab);
                SetupPreviewInstance(previewInstance);

                // 2D 스프라이트 체크 추가
                CheckAndSetup2DMode();

                if (!is2DMode)
                {
                    ExtractAnimations();
                }

                ApplyCameraPreset(is2DMode ? CameraPreset.Default : CameraPreset.FullBody);
            }
            else
            {
                CreateFallbackModel(Color.blue, "Character");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[LoadCharacterModel] Failed: {e.Message}");
            CreateFallbackModel(Color.blue, "Character");
        }
    }

    private void CheckAndSetup2DMode()
    {
        if (previewInstance == null) return;

        // Character 자식 오브젝트에서 CharacterSpriteAnimator 찾기
        Transform characterTransform = previewInstance.transform.Find("Character");
        if (characterTransform != null)
        {
            currentSpriteAnimator = characterTransform.GetComponent<CharacterSpriteAnimator>();
            if (currentSpriteAnimator != null)
            {
                is2DMode = true;
                ExtractSpriteAnimations();

                // 스프라이트 애니메이션 초기화
#if UNITY_EDITOR
                currentSpriteAnimator.InitializeByEditor();
#endif

                // 기본 애니메이션 로드
                _ = LoadSpriteAnimations();
            }
        }
        else
        {
            is2DMode = false;
            currentSpriteAnimator = null;
        }
    }

    private async Task LoadSpriteAnimations()
    {
        if (currentSpriteAnimator != null)
        {
            // baseKey 설정 (캐릭터 이름 기반)
            string baseKey = previewCharacter.CharacterName;
            currentSpriteAnimator.Initialize(baseKey);

            // 애니메이션 로드
            await currentSpriteAnimator.LoadAnimationsAsync();
        }
    }

    private void ExtractSpriteAnimations()
    {
        if (currentSpriteAnimator == null) return;

        List<string> keys = new List<string>();
        foreach (var sheet in currentSpriteAnimator.spriteSheets)
        {
            if (!string.IsNullOrEmpty(sheet.key))
            {
                keys.Add(sheet.key);
            }
        }

        spriteAnimationKeys = keys.ToArray();
        if (spriteAnimationKeys.Length == 0)
        {
            spriteAnimationKeys = new string[] { "Idle", "Walk", "Blink" };
        }
    }


    private async void LoadMonsterModel()
    {
        if (previewMonster == null) return;

        CleanupCurrentModel();

        try
        {
            GameObject prefab = null;

            // UseExistingCharacter 체크
            if (IsUsingExistingCharacter(previewMonster))
            {
                prefab = await LoadExistingCharacterPrefab(previewMonster);
            }
            else
            {
                prefab = await LoadMonsterPrefabAsync(previewMonster);
            }

            if (prefab != null)
            {
                previewInstance = Instantiate(prefab);
                SetupPreviewInstance(previewInstance);
                ExtractAnimations();

                // 몬스터 특수 효과
                if (previewMonster.HasGuardianStone)
                {
                    AddGuardianStoneVisuals();
                }

                // 보스는 카메라 거리 조정
                if (previewMonster.IsBoss)
                {
                    ApplyCameraPreset(CameraPreset.Group);
                }
                else
                {
                    ApplyCameraPreset(CameraPreset.FullBody);
                }
            }
            else
            {
                CreateFallbackModel(Color.red, "Monster");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[LoadMonsterModel] Failed: {e.Message}");
            CreateFallbackModel(Color.red, "Monster");
        }
    }

    private bool IsUsingExistingCharacter(BattleMonsterDataSO monster)
    {
        // MonsterDataSO에 UseExistingCharacter 프로퍼티가 있는지 체크
        var type = monster.GetType();
        var prop = type.GetProperty("UseExistingCharacter");
        if (prop != null)
        {
            return (bool)prop.GetValue(monster);
        }
        return false;
    }

    private async Task<GameObject> LoadExistingCharacterPrefab(BattleMonsterDataSO monster)
    {
        // BaseCharacterId 가져오기
        var type = monster.GetType();
        var prop = type.GetProperty("BaseCharacterId");
        if (prop != null)
        {
            int charId = (int)prop.GetValue(monster);
            var charData = FindCharacterById(charId);
            if (charData != null)
            {
                return await LoadPrefabAsync(charData);
            }
        }
        return null;
    }

    private async Task<GameObject> LoadPrefabAsync(BattleCharacterDataSO character)
    {
        GameObject prefab = null;

        // 1. AssetDatabase로 시도 (에디터)
        string[] searchPaths = {
            $"Assets/Resources/{character.PrefabPath}{character.CharacterResourceName}.prefab",
            $"Assets/Cosmos/ResourcesAddressable/Prefabs/{character.CharacterResourceName}.prefab",
            $"Assets/Cosmos/ResourcesAddressable/Character/{character.CharacterName}/Prefabs/{character.CharacterResourceName}.prefab",
            $"Assets/{character.PrefabPath}{character.CharacterResourceName}.prefab"
        };

        foreach (var path in searchPaths)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                UnityEngine.Debug.Log($"[LoadPrefab] Found at: {path}");
                return prefab;
            }
        }

        // 2. Resources.Load 시도
        string[] resourcePaths = {
            $"{character.PrefabPath}{character.CharacterResourceName}",
            $"Character/{character.CharacterResourceName}",
            $"Prefabs/{character.CharacterResourceName}"
        };

        foreach (var path in resourcePaths)
        {
            prefab = Resources.Load<GameObject>(path);
            if (prefab != null)
            {
                UnityEngine.Debug.Log($"[LoadPrefab] Found in Resources: {path}");
                return prefab;
            }
        }

        // 3. GUID로 검색
        string[] guids = AssetDatabase.FindAssets($"{character.CharacterResourceName} t:Prefab");
        if (guids.Length > 0)
        {
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    UnityEngine.Debug.Log($"[LoadPrefab] Found by GUID: {path}");
                    return prefab;
                }
            }
        }

        UnityEngine.Debug.LogWarning($"[LoadPrefab] Failed to find: {character.CharacterResourceName}");
        return null;
    }

    private async Task<GameObject> LoadMonsterPrefabAsync(BattleMonsterDataSO monster)
    {
        GameObject prefab = null;

        // GetActualResourceName 메서드가 있는지 체크
        string resourceName = monster.MonsterResourceName;
        var methodInfo = monster.GetType().GetMethod("GetActualResourceName");
        if (methodInfo != null)
        {
            resourceName = (string)methodInfo.Invoke(monster, null);
        }

        // 1. AssetDatabase로 시도
        string[] searchPaths = {
            $"Assets/Resources/Monster/{resourceName}.prefab",
            $"Assets/Cosmos/ResourcesAddressable/Prefabs/Monster/{resourceName}.prefab",
            $"Assets/Cosmos/ResourcesAddressable/Monster/{monster.MonsterName}/Prefabs/{resourceName}.prefab"
        };

        foreach (var path in searchPaths)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                UnityEngine.Debug.Log($"[LoadMonsterPrefab] Found at: {path}");
                return prefab;
            }
        }

        // 2. Resources 시도
        prefab = Resources.Load<GameObject>($"Monster/{resourceName}");
        if (prefab != null) return prefab;

        // 3. GUID 검색
        string[] guids = AssetDatabase.FindAssets($"{resourceName} t:Prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return prefab;
    }

    private BattleCharacterDataSO FindCharacterById(int id)
    {
        string[] guids = AssetDatabase.FindAssets("t:BattleCharacterDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<BattleCharacterDataSO>(path);
            if (data != null && data.CharacterId == id)
            {
                return data;
            }
        }
        return null;
    }

    private void SetupPreviewInstance(GameObject instance)
    {
        if (instance == null) return;

        instance.hideFlags = HideFlags.DontSave;
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        // 모든 자식 오브젝트도 숨김 플래그 설정
        foreach (Transform child in instance.GetComponentsInChildren<Transform>())
        {
            child.gameObject.hideFlags = HideFlags.DontSave;
        }

        // Layer 설정
        SetLayerRecursively(instance, LayerMask.NameToLayer("Default"));

        // Particle System 정지
        var particles = instance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void CreateFallbackModel(Color color, string label)
    {
        previewInstance = new GameObject($"Fallback_{label}");
        previewInstance.hideFlags = HideFlags.DontSave;

        // 큐브 몸체
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(previewInstance.transform);
        body.transform.localPosition = new Vector3(0, 0.5f, 0);
        body.hideFlags = HideFlags.DontSave;

        var renderer = body.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }

        // 라벨
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(previewInstance.transform);
        labelObj.transform.localPosition = new Vector3(0, 1.5f, 0);
        labelObj.hideFlags = HideFlags.DontSave;

        var textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = label;
        textMesh.fontSize = 20;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = Color.white;
    }

    // BattlePreviewWindow.cs - Part 2
    // 이 부분을 Part 1의 끝에 이어서 붙여주세요

    #region Animation System

    private void ExtractAnimations()
    {
        if (previewInstance == null) return;

        currentAnimator = previewInstance.GetComponent<Animator>();

        if (currentAnimator != null && currentAnimator.runtimeAnimatorController != null)
        {
            originalController = currentAnimator.runtimeAnimatorController;
            var clips = originalController.animationClips;
            availableAnimations = clips.Select(c => c.name).Distinct().ToArray();

            if (availableAnimations.Length == 0)
            {
                ExtractAnimatorStates();
            }
        }
        else
        {
            // Legacy Animation 체크
            var animation = previewInstance.GetComponent<Animation>();
            if (animation != null)
            {
                List<string> animNames = new List<string>();
                foreach (AnimationState state in animation)
                {
                    animNames.Add(state.name);
                }
                availableAnimations = animNames.ToArray();
            }
        }

        // 기본 애니메이션 목록이 없으면 표준 목록 사용
        if (availableAnimations == null || availableAnimations.Length == 0)
        {
            availableAnimations = new string[]
            {
                "Idle", "Walk", "Run", "Attack", "Hit", "Death",
                "Skill_01", "Skill_02", "Victory", "Defeat"
            };
        }
    }

    private void ExtractAnimatorStates()
    {
        if (currentAnimator == null || currentAnimator.runtimeAnimatorController == null) return;

        List<string> states = new List<string>();

        // 기본 상태들
        string[] commonStates = { "Idle", "Attack", "Hit", "Death", "Skill", "Victory" };
        foreach (var state in commonStates)
        {
            states.Add(state);
        }

        availableAnimations = states.ToArray();
    }

    private void PlayAnimation(string animationName)
    {
        if (previewInstance == null) return;

        StopAnimation();

        if (currentAnimator != null)
        {
            // State Hash로 재생 시도
            int stateHash = Animator.StringToHash(animationName);

            // 여러 방법으로 재생 시도
            try
            {
                // 1. Play 메서드
                currentAnimator.Play(animationName, 0, 0f);
            }
            catch
            {
                try
                {
                    // 2. Trigger 시도
                    currentAnimator.SetTrigger(animationName);
                }
                catch
                {
                    // 3. CrossFade 시도
                    currentAnimator.CrossFade(animationName, 0.1f);
                }
            }

            // Speed 설정
            currentAnimator.speed = animationSpeed;
            isPlaying = true;
        }
        else
        {
            // Legacy Animation
            var animation = previewInstance.GetComponent<Animation>();
            if (animation != null)
            {
                if (animation[animationName] != null)
                {
                    animation[animationName].wrapMode = loopAnimation ? WrapMode.Loop : WrapMode.Once;
                    animation[animationName].speed = animationSpeed;
                    animation.Play(animationName);
                    isPlaying = true;
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Animation '{animationName}' not found");
                }
            }
        }
    }

    private void StopAnimation()
    {
        if (previewInstance == null) return;

        if (currentAnimator != null)
        {
            currentAnimator.StopPlayback();
            currentAnimator.speed = 0f;
        }
        else
        {
            var animation = previewInstance.GetComponent<Animation>();
            if (animation != null)
            {
                animation.Stop();
            }
        }

        isPlaying = false;
    }

    #endregion

    #region Special Effects

    private void AddGuardianStoneVisuals()
    {
        if (previewMonster == null || !previewMonster.HasGuardianStone) return;

        var stones = previewMonster.GuardianStoneElements;
        float angleStep = 360f / stones.Length;
        float radius = 1.5f;
        float height = 1f;
        float rotationSpeed = 30f;

        for (int i = 0; i < stones.Length; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                Mathf.Cos(angle) * radius,
                height,
                Mathf.Sin(angle) * radius
            );

            var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.name = $"GuardianStone_{stones[i]}";
            stone.transform.SetParent(previewInstance.transform);
            stone.transform.localPosition = position;
            stone.transform.localScale = Vector3.one * 0.3f;
            stone.hideFlags = HideFlags.DontSave;

            var renderer = stone.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = GetElementColor(stones[i]);
                mat.SetFloat("_Metallic", 0.8f);
                mat.SetFloat("_Glossiness", 0.9f);
                renderer.material = mat;
            }

            // 회전 애니메이션 (선택적)
            var rotator = stone.AddComponent<SimpleRotator>();
            rotator.rotationSpeed = rotationSpeed;
        }
    }

    private Color GetElementColor(EBattleElementType element)
    {
        switch (element)
        {
            case EBattleElementType.Power: return new Color(1f, 0.2f, 0.2f);
            case EBattleElementType.Plasma: return new Color(0.2f, 0.2f, 1f);
            case EBattleElementType.Chemical: return new Color(0.2f, 1f, 0.2f);
            case EBattleElementType.Bio: return new Color(1f, 0.2f, 1f);
            case EBattleElementType.Network: return new Color(0.2f, 1f, 1f);
            case EBattleElementType.Electrical: return new Color(1f, 1f, 0.2f);
            default: return Color.white;
        }
    }

    private void LoadEffect()
    {
        if (string.IsNullOrEmpty(effectPrefabPath)) return;

        ClearEffect();

        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(effectPrefabPath);
        if (effectPrefab != null)
        {
            currentEffect = Instantiate(effectPrefab);
            currentEffect.hideFlags = HideFlags.DontSave;
            currentEffect.transform.position = effectOffset;
            currentEffect.transform.localScale = Vector3.one * effectScale;

            if (attachEffectToModel && previewInstance != null)
            {
                currentEffect.transform.SetParent(previewInstance.transform);
            }
        }
    }

    private void PlayEffect()
    {
        if (currentEffect == null) return;

        var particleSystems = currentEffect.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            ps.Play();
        }
    }

    private void ClearEffect()
    {
        SafeDestroy(currentEffect);
        currentEffect = null;
    }

    #endregion

    #region Group Formation

    private void LoadGroupModels()
    {
        if (previewGroup == null) return;

        // 기존 인스턴스 정리
        foreach (var instance in groupMonsterInstances)
        {
            SafeDestroy(instance);
        }
        groupMonsterInstances.Clear();

        // 몬스터 그룹 로드
        for (int i = 0; i < 5; i++)
        {
            var slot = previewGroup.MonsterSlots[i];
            if (!slot.isEmpty && slot.monsterData != null)
            {
                LoadGroupMonsterInstance(slot.monsterData, i);
            }
            else
            {
                groupMonsterInstances.Add(null);
            }
        }

        UpdateGroupFormation();
        ApplyCameraPreset(CameraPreset.Group);
    }

    private async void LoadGroupMonsterInstance(BattleMonsterDataSO monsterData, int slotIndex)
    {
        GameObject prefab = null;

        // 몬스터 프리팹 로드
        if (IsUsingExistingCharacter(monsterData))
        {
            prefab = await LoadExistingCharacterPrefab(monsterData);
        }
        else
        {
            prefab = await LoadMonsterPrefabAsync(monsterData);
        }

        GameObject instance = null;
        if (prefab != null)
        {
            instance = Instantiate(prefab);
            SetupPreviewInstance(instance);

            // 스케일 조정 (그룹에서는 약간 작게)
            instance.transform.localScale = Vector3.one * 0.8f;
        }
        else
        {
            // 대체 모델
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.GetComponent<Renderer>().material.color =
                monsterData.IsBoss ? Color.magenta : Color.red;
        }

        instance.hideFlags = HideFlags.DontSave;

        // 슬롯에 맞게 리스트 업데이트
        while (groupMonsterInstances.Count <= slotIndex)
        {
            groupMonsterInstances.Add(null);
        }
        groupMonsterInstances[slotIndex] = instance;

        UpdateGroupFormation();
    }

    private void UpdateGroupFormation()
    {
        if (groupMonsterInstances.Count != 5) return;

        // 진형에 따른 위치 배치
        Vector3[] positions = GetFormationPositions(previewFormation);

        for (int i = 0; i < 5; i++)
        {
            if (groupMonsterInstances[i] != null)
            {
                groupMonsterInstances[i].transform.position = positions[i];
                groupMonsterInstances[i].transform.rotation = Quaternion.identity;
            }
        }
    }

    private Vector3[] GetFormationPositions(FormationType formation)
    {
        Vector3[] positions = new Vector3[5];
        float spacing = formationSpacing;

        switch (formation)
        {
            case FormationType.Offensive: // 1-4
                positions[0] = new Vector3(-2, 0, 0);
                positions[1] = new Vector3(1, 0, spacing * 1.5f);
                positions[2] = new Vector3(1, 0, spacing * 0.5f);
                positions[3] = new Vector3(1, 0, -spacing * 0.5f);
                positions[4] = new Vector3(1, 0, -spacing * 1.5f);
                break;

            case FormationType.Defensive: // 4-1
                positions[0] = new Vector3(-1, 0, spacing * 1.5f);
                positions[1] = new Vector3(-1, 0, spacing * 0.5f);
                positions[2] = new Vector3(-1, 0, -spacing * 0.5f);
                positions[3] = new Vector3(-1, 0, -spacing * 1.5f);
                positions[4] = new Vector3(2, 0, 0);
                break;

            case FormationType.OffensiveBalance: // 2-3
                positions[0] = new Vector3(-1.5f, 0, spacing);
                positions[1] = new Vector3(-1.5f, 0, -spacing);
                positions[2] = new Vector3(1.5f, 0, spacing * 1.5f);
                positions[3] = new Vector3(1.5f, 0, 0);
                positions[4] = new Vector3(1.5f, 0, -spacing * 1.5f);
                break;

            case FormationType.DefensiveBalance: // 3-2
                positions[0] = new Vector3(-1.5f, 0, spacing * 1.5f);
                positions[1] = new Vector3(-1.5f, 0, 0);
                positions[2] = new Vector3(-1.5f, 0, -spacing * 1.5f);
                positions[3] = new Vector3(1.5f, 0, spacing);
                positions[4] = new Vector3(1.5f, 0, -spacing);
                break;
        }

        return positions;
    }

    #endregion

    #region Camera Controls

    private void ApplyCameraPreset(CameraPreset preset)
    {
        currentCameraPreset = preset;

        // 2D 모드일 때는 거리 조정
        if (is2DMode)
        {
            previewDistance = 3f;
            fieldOfView = 60f;
            previewRotation = Vector2.zero;
            ConfigureCamera();
            return;
        }

        switch (preset)
        {
            case CameraPreset.CloseUp:
                previewDistance = 2f;
                fieldOfView = 20f;
                previewRotation = new Vector2(0, 10);
                break;

            case CameraPreset.FullBody:
                previewDistance = 5f;
                fieldOfView = 30f;
                previewRotation = new Vector2(30, 10);
                break;

            case CameraPreset.Action:
                previewDistance = 4f;
                fieldOfView = 40f;
                previewRotation = new Vector2(45, 15);
                break;

            case CameraPreset.Group:
                previewDistance = 8f;
                fieldOfView = 35f;
                previewRotation = new Vector2(30, 20);
                break;

            default:
                previewDistance = 5f;
                fieldOfView = 30f;
                previewRotation = Vector2.zero;
                break;
        }

        ConfigureCamera();
    }

    #endregion

    #region UI Rendering

    private void OnGUI()
    {
        // 에러 표시
        if (hasError)
        {
            EditorGUILayout.HelpBox($"Error: {lastError}", MessageType.Error);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            // 왼쪽 패널 - 컨트롤
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                DrawControlPanel();
            }

            // 구분선
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // 오른쪽 패널 - 프리뷰
            using (new EditorGUILayout.VerticalScope())
            {
                DrawPreviewPanel();
            }
        }
    }

    private void DrawControlPanel()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 프리뷰 타입 선택
        DrawPreviewTypeSelection();
        EditorGUILayout.Space(10);

        // 대상 선택
        DrawTargetSelection();
        EditorGUILayout.Space(10);

        // 카메라 컨트롤
        DrawCameraControls();
        EditorGUILayout.Space(10);

        // 애니메이션 컨트롤
        if (showAnimationControls && previewInstance != null)
        {
            DrawAnimationControls();
            EditorGUILayout.Space(10);

            DrawTimelineControls();
            EditorGUILayout.Space(10);
        }


        // 이펙트 컨트롤
        if (showEffectControls)
        {
            DrawEffectControls();
            EditorGUILayout.Space(10);
        }

        // 그룹 진형 컨트롤
        if (currentPreviewType == PreviewType.MonsterGroup && showGroupFormation)
        {
            DrawGroupFormationControls();
            EditorGUILayout.Space(10);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPreviewTypeSelection()
    {
        EditorGUILayout.LabelField("Preview Type", EditorStyles.boldLabel);
        var newType = (PreviewType)EditorGUILayout.EnumPopup(currentPreviewType);
        if (newType != currentPreviewType)
        {
            currentPreviewType = newType;
            CleanupCurrentModel();
        }
    }

    private void DrawTargetSelection()
    {
        EditorGUILayout.LabelField("Target Selection", EditorStyles.boldLabel);

        switch (currentPreviewType)
        {
            case PreviewType.Character:
                var newChar = EditorGUILayout.ObjectField("Character", previewCharacter,
                    typeof(BattleCharacterDataSO), false) as BattleCharacterDataSO;
                if (newChar != previewCharacter)
                {
                    SetPreviewTarget(newChar);
                }
                break;

            case PreviewType.Monster:
                var newMonster = EditorGUILayout.ObjectField("Monster", previewMonster,
                    typeof(BattleMonsterDataSO), false) as BattleMonsterDataSO;
                if (newMonster != previewMonster)
                {
                    SetPreviewTarget(newMonster);
                }
                break;

            case PreviewType.MonsterGroup:
                var newGroup = EditorGUILayout.ObjectField("Monster Group", previewGroup,
                    typeof(MonsterGroupDataSO), false) as MonsterGroupDataSO;
                if (newGroup != previewGroup)
                {
                    SetPreviewTarget(newGroup);
                }
                break;
        }

        // 프리팹 경로 표시
        DrawResourceInfo();
    }

    private void DrawResourceInfo()
    {
        if (previewCharacter != null && currentPreviewType == PreviewType.Character)
        {
            EditorGUILayout.HelpBox(
                $"Prefab: {previewCharacter.PrefabPath}{previewCharacter.CharacterResourceName}\n" +
                $"Addressable: {previewCharacter.AddressableKey}",
                MessageType.Info
            );
        }
        else if (previewMonster != null && currentPreviewType == PreviewType.Monster)
        {
            string resourceName = previewMonster.MonsterResourceName;
            var methodInfo = previewMonster.GetType().GetMethod("GetActualResourceName");
            if (methodInfo != null)
            {
                resourceName = (string)methodInfo.Invoke(previewMonster, null);
            }

            EditorGUILayout.HelpBox(
                $"Prefab: {previewMonster.PrefabPath}{resourceName}\n" +
                $"Addressable: {previewMonster.AddressableKey}",
                MessageType.Info
            );
        }
    }

    private void DrawCameraControls()
    {
        EditorGUILayout.LabelField("Camera Controls", EditorStyles.boldLabel);

        // 카메라 프리셋
        currentCameraPreset = (CameraPreset)EditorGUILayout.EnumPopup("Preset", currentCameraPreset);
        if (GUILayout.Button("Apply Preset"))
        {
            ApplyCameraPreset(currentCameraPreset);
        }

        EditorGUILayout.Space(5);

        previewDistance = EditorGUILayout.Slider("Distance", previewDistance, MIN_CAMERA_DISTANCE, MAX_CAMERA_DISTANCE);
        fieldOfView = EditorGUILayout.Slider("Field of View", fieldOfView, 10f, 60f);

        autoRotate = EditorGUILayout.Toggle("Auto Rotate", autoRotate);
        if (autoRotate)
        {
            rotationSpeed = EditorGUILayout.Slider("Rotation Speed", rotationSpeed, 1f, 100f);
        }

        backgroundColor = EditorGUILayout.ColorField("Background", backgroundColor);
        transparentBackground = EditorGUILayout.Toggle("Transparent", transparentBackground);

        if (GUILayout.Button("Reset Camera"))
        {
            previewRotation = Vector2.zero;
            previewDistance = 5f;
            fieldOfView = 30f;
            ConfigureCamera();
        }
    }

    private void DrawAnimationControls()
    {
        showAnimationControls = EditorGUILayout.Foldout(showAnimationControls, "Animation Controls", true);
        if (!showAnimationControls) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 2D 모드 체크
        if (is2DMode && currentSpriteAnimator != null)
        {
            // 스프라이트 애니메이션 컨트롤
            EditorGUILayout.LabelField("Sprite Animation", EditorStyles.boldLabel);

            if (spriteAnimationKeys != null && spriteAnimationKeys.Length > 0)
            {
                selectedSpriteAnimation = EditorGUILayout.Popup("Animation", selectedSpriteAnimation, spriteAnimationKeys);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(isPlayingSprite ? "Stop" : "Play"))
            {
                if (isPlayingSprite)
                {
                    // 정지는 없음 (계속 재생)
                    isPlayingSprite = false;
                }
                else
                {
                    PlaySpriteAnimation(spriteAnimationKeys[selectedSpriteAnimation]);
                }
            }

            if (GUILayout.Button("Reset"))
            {
                PlaySpriteAnimation("Idle");
            }
            EditorGUILayout.EndHorizontal();

            // Quick Play
            EditorGUILayout.LabelField("Quick Play:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var key in spriteAnimationKeys.Take(4))
            {
                if (GUILayout.Button(key, GUILayout.Width(60)))
                {
                    PlaySpriteAnimation(key);
                }
            }
            EditorGUILayout.EndHorizontal();

            // 프레임 정보
            if (currentSpriteAnimator.IsLoaded)
            {
                EditorGUILayout.LabelField($"Current: {currentSpriteAnimator.CurrentKey}");
            }
        }
        else
        {
            showAnimationControls = EditorGUILayout.Foldout(showAnimationControls, "Animation Controls", true);
            if (!showAnimationControls) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (availableAnimations != null && availableAnimations.Length > 0)
            {
                selectedAnimation = EditorGUILayout.Popup("Animation", selectedAnimation, availableAnimations);
            }
            else
            {
                EditorGUILayout.LabelField("No animations found");
            }

            loopAnimation = EditorGUILayout.Toggle("Loop", loopAnimation);
            animationSpeed = EditorGUILayout.Slider("Speed", animationSpeed, 0.1f, 3f);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isPlaying ? Color.red : Color.green;
            if (GUILayout.Button(isPlaying ? "Stop" : "Play"))
            {
                if (isPlaying)
                {
                    StopAnimation();
                }
                else if (availableAnimations != null && availableAnimations.Length > 0)
                {
                    PlayAnimation(availableAnimations[selectedAnimation]);
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reset"))
            {
                StopAnimation();
                if (availableAnimations != null && availableAnimations.Length > 0)
                {
                    PlayAnimation("Idle");
                }
            }
            EditorGUILayout.EndHorizontal();

            // Quick Play buttons
            EditorGUILayout.LabelField("Quick Play:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            string[] quickAnims = { "Idle", "Attack", "Hit", "Skill" };
            foreach (var anim in quickAnims)
            {
                if (GUILayout.Button(anim, GUILayout.Width(60)))
                {
                    PlayAnimation(anim);
                }
            }
            EditorGUILayout.EndHorizontal();

        }
        EditorGUILayout.EndVertical();
    }


    private PreviewPlaybackAdapter playbackAdapter;

    private void InitializePlaybackSystem()
    {
        // 기존 것이 있으면 먼저 정리
        if (playbackAdapter != null)
        {
            DestroyImmediate(playbackAdapter.gameObject);
            playbackAdapter = null;
        }

        if (previewInstance == null)
        {
            UnityEngine.Debug.LogWarning("[BattlePreview] No preview instance to bind PlaybackSystem");
            return;
        }

          
        var adapterGO = new GameObject("PlaybackAdapter");
        adapterGO.hideFlags = HideFlags.DontSave;
        playbackAdapter = adapterGO.AddComponent<PreviewPlaybackAdapter>();

        playbackAdapter.Initialize(previewInstance, previewCharacter);
    }

    private void LoadTimeline(TimelineDataSO timeline)
    {
        if (timeline == null)
        {
            UnityEngine.Debug.LogWarning("[BattlePreview] Timeline is null");
            return;
        }

        // PlaybackAdapter가 없으면 초기화
        if (playbackAdapter == null)
        {
            InitializePlaybackSystem();
        }

        // Timeline 로드
        currentPlayingTimeline = timeline;
        timelinePlaybackTime = 0f;

        // PlaybackAdapter를 통해 재생
        if (playbackAdapter != null)
        {
            playbackAdapter.PlayTimeline(timeline);
            UnityEngine.Debug.Log($"[BattlePreview] Loaded timeline: {timeline.timelineName}");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[BattlePreview] PlaybackAdapter not initialized");
        }
    }


    // Fields 섹션에 추가
    private int currentFrame = 0;
    private int totalFrames = 0;
    private float frameRate = 30f; // 기본 30fps


    private void DrawTimelineControls()
    {
        // 기존 코드를 대체
        if (playbackAdapter == null)
        {
            InitializePlaybackSystem();
        }

        var timelineInfo = playbackAdapter.GetTimelineInfo();

        EditorGUILayout.LabelField($"Timeline: {timelineInfo.Name}");
        EditorGUILayout.LabelField($"Duration: {timelineInfo.Duration:F2}s");

        // 재생 버튼
        if (GUILayout.Button(timelineInfo.IsPlaying ? "⏸ Pause" : "▶ Play"))
        {
            if (timelineInfo.IsPlaying)
                playbackAdapter.Pause();
            else
                playbackAdapter.Resume();
        }


        
        // Timeline 선택
        if (previewCharacter.TimelineSettings != null)
        {
            if (GUILayout.Button("Attack1"))
                playbackAdapter.PlayTimeline(previewCharacter.TimelineSettings.attack1Timeline);
            if (GUILayout.Button("ActiveSkill1"))
                playbackAdapter.PlayTimeline(previewCharacter.TimelineSettings.activeSkill1Timeline);
            if (GUILayout.Button("PassiveSkill1"))
                playbackAdapter.PlayTimeline(previewCharacter.TimelineSettings.passiveSkill1Timeline);

        }

        // 진행바
        float newTime = EditorGUILayout.Slider("Time",
            playbackAdapter.CurrentTime, 0f, timelineInfo.Duration);
        if (newTime != playbackAdapter.CurrentTime)
        {
            playbackAdapter.Seek(newTime);
        }

        // 이벤트 리스트
        foreach (var evt in playbackAdapter.GetEventList())
        {
            GUI.color = evt.HasExecuted ? Color.gray : Color.white;
            EditorGUILayout.LabelField($"[{evt.Time:F2}s] {evt.Type}: {evt.Description}");
        }


        if (previewCharacter != null)
        {
            string characterName = previewCharacter.CharacterName;
            string animationsPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations";

            using (new EditorGUILayout.HorizontalScope())
            {
                // 가능한 Timeline 패턴들
                string[] timelinePatterns = { "Attack1", "ActiveSkill1", "PassiveSkill1" };

                foreach (string pattern in timelinePatterns)
                {
                    string timelinePath = $"{animationsPath}/{characterName}_{pattern}_Timeline_Data.asset";
                    if (System.IO.File.Exists(timelinePath))
                    {
                        if (GUILayout.Button(pattern, GUILayout.Width(100)))
                        {
                            var timeline = AssetDatabase.LoadAssetAtPath<TimelineDataSO>(timelinePath);
                            if (timeline != null)
                            {
                                LoadTimeline(timeline);
                            }
                        }
                    }
                }
            }
        }


        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Timeline Playback", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (currentPlayingTimeline != null)
            {
                // 총 프레임 수 계산
                totalFrames = Mathf.RoundToInt(currentPlayingTimeline.duration * frameRate);

                EditorGUILayout.LabelField($"Current: {currentPlayingTimeline.timelineName}");
                EditorGUILayout.LabelField($"Duration: {currentPlayingTimeline.duration:F2}s ({totalFrames} frames @ {frameRate}fps)");


                // 재생 컨트롤
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = isTimelinePlaying ? Color.red : Color.green;
                    if (GUILayout.Button(isTimelinePlaying ? "⏸ Stop" : "▶ Play"))
                    {
                        if (isTimelinePlaying)
                            StopTimelinePlayback();
                        else
                            StartTimelinePlayback();
                    }
                    GUI.backgroundColor = Color.white;

                    if (GUILayout.Button("↺ Reset"))
                    {
                        ResetTimelinePlayback();
                    }

                    // 프레임 단위 이동
                    if (GUILayout.Button("◀", GUILayout.Width(30)))
                    {
                        SeekToFrame(currentFrame - 1);
                    }
                    if (GUILayout.Button("▶", GUILayout.Width(30)))
                    {
                        SeekToFrame(currentFrame + 1);
                    }
                }

                // 프레임 슬라이더
                EditorGUILayout.Space(5);
                EditorGUI.BeginChangeCheck();
                int newFrame = EditorGUILayout.IntSlider("Frame", currentFrame, 0, totalFrames);

                if (EditorGUI.EndChangeCheck())
                {
                    SeekToFrame(newFrame);
                }

                // 시간 표시 (읽기 전용)
                float currentTime = currentFrame / frameRate;
                EditorGUILayout.LabelField($"Time: {currentTime:F3}s");

                // FPS 설정
                frameRate = EditorGUILayout.FloatField("Frame Rate", frameRate);
            }
        }


        GUI.color = Color.white;
    }


    // 프레임으로 이동
    private void SeekToFrame(int frame)
    {
        if (currentPlayingTimeline == null) return;

        // 프레임 범위 제한
        frame = Mathf.Clamp(frame, 0, totalFrames);
        currentFrame = frame;

        // 프레임을 시간으로 변환
        float time = frame / frameRate;
        timelinePlaybackTime = time;

        // PlaybackAdapter 시크
        if (playbackAdapter != null)
        {
            playbackAdapter.Seek(time);
        }

        // Track 애니메이션 적용
        ApplyTrackAnimationAtFrame(frame);

        // 이벤트 상태 업데이트
        pendingEvents.Clear();
        pendingEvents.AddRange(currentPlayingTimeline.GetAllEventsSorted().Where(e => e.TriggerTime > time));
    }

    // 프레임 기반 애니메이션 적용
    private void ApplyTrackAnimationAtFrame(int frame)
    {
        if (currentPlayingTimeline == null || previewInstance == null) return;

        float time = frame / frameRate;

        // Track 애니메이션 샘플링
        if (currentPlayingTimeline.trackAnimations != null)
        {
            foreach (var trackAnim in currentPlayingTimeline.trackAnimations)
            {
                if (trackAnim.animationClip != null)
                {
                    SampleAnimationAtFrame(trackAnim.animationClip, frame, frameRate);
                }
            }
        }

        // 일반 애니메이션 이벤트 처리
        foreach (var animEvent in currentPlayingTimeline.animationEvents)
        {
            if (animEvent.extractedClip != null && time >= animEvent.triggerTime)
            {
                float relativeTime = time - animEvent.triggerTime;
                if (relativeTime >= 0 && relativeTime <= animEvent.extractedClip.length)
                {
                    int relativeFrame = Mathf.RoundToInt(relativeTime * frameRate);
                    SampleAnimationAtFrame(animEvent.extractedClip, relativeFrame, frameRate);
                }
            }
        }
    }

    // 특정 프레임으로 애니메이션 샘플링
    private void SampleAnimationAtFrame(AnimationClip clip, int frame, float fps)
    {
        if (clip == null || previewInstance == null) return;

        // 프레임을 시간으로 변환
        float time = frame / fps;
        time = Mathf.Clamp(time, 0f, clip.length);

        // AnimationMode 사용
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(previewInstance, clip, time);
        AnimationMode.EndSampling();

        // Scene View 갱신
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Repaint();
        }

        Repaint();
    }


    // 새로운 메서드 추가
    private void ApplyTrackAnimationAtTime(float time)
    {
        if (currentPlayingTimeline == null || previewInstance == null) return;

        // Track 애니메이션이 있는지 확인
        if (currentPlayingTimeline.trackAnimations != null && currentPlayingTimeline.trackAnimations.Count > 0)
        {
            foreach (var trackAnim in currentPlayingTimeline.trackAnimations)
            {
                if (trackAnim.animationClip != null)
                {
                    // AnimationClip을 특정 시간으로 샘플링
                    SampleAnimation(trackAnim.animationClip, time);
                }
            }
        }

        // 일반 애니메이션 이벤트도 처리
        foreach (var animEvent in currentPlayingTimeline.animationEvents)
        {
            if (animEvent.extractedClip != null && time >= animEvent.triggerTime)
            {
                // 이벤트 시작 시점부터의 상대 시간 계산
                float relativeTime = time - animEvent.triggerTime;
                if (relativeTime >= 0 && relativeTime <= animEvent.extractedClip.length)
                {
                    SampleAnimation(animEvent.extractedClip, relativeTime);
                }
            }
        }
    }






    // AnimationClip 샘플링 메서드
    private void SampleAnimation(AnimationClip clip, float time)
    {
        if (clip == null || previewInstance == null) return;

        // AnimationMode 사용 (Editor 전용)
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        // 클립을 특정 시간으로 샘플링
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(previewInstance, clip, time);
        AnimationMode.EndSampling();

        // Scene View 갱신
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.Repaint();
        }

        Repaint();
    }

    // SeekTimeline 메서드 수정
    private void SeekTimeline(float time)
    {
        timelinePlaybackTime = time;

        if (playbackAdapter != null)
        {
            playbackAdapter.Seek(time);
        }

        // 이벤트 상태 업데이트
        pendingEvents.Clear();
        if (currentPlayingTimeline != null)
        {
            pendingEvents.AddRange(currentPlayingTimeline.GetAllEventsSorted().Where(e => e.TriggerTime > time));
        }
    }












    private List<TimelineDataSO> FindCharacterTimelines(string characterName)
    {
        var timelines = new List<TimelineDataSO>();
        string animationsPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations";

        if (AssetDatabase.IsValidFolder(animationsPath))
        {
            string[] guids = AssetDatabase.FindAssets("t:TimelineDataSO", new[] { animationsPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineDataSO>(path);
                if (timeline != null)
                {
                    timelines.Add(timeline);
                }
            }
        }

        return timelines;
    }

    public void SetPreviewTarget(BattleCharacterDataSO character)
    {
        previewCharacter = character;
        currentPreviewType = PreviewType.Character;
        LoadCharacterModel();

        // 자동으로 Timeline 로드
        if (character != null)
        {
            var timelines = FindCharacterTimelines(character.CharacterName);
            if (timelines.Count > 0)
            {
                UnityEngine.Debug.Log($"[BattlePreview] Found {timelines.Count} timelines for {character.CharacterName}");
                // 첫 번째 Timeline 자동 로드 (옵션)
                if (timelines.Count > 0)
                {
                    LoadTimeline(timelines[0]);  
                }
            }
        }
    }

    private void StartTimelinePlayback()
    {
        if (currentPlayingTimeline == null)
        {
            UnityEngine.Debug.LogWarning("[BattlePreview] No timeline to play");
            return;
        }

        isTimelinePlaying = true;

        // PlaybackAdapter가 있으면 사용
        if (playbackAdapter != null)
        {
            playbackAdapter.Resume();
        }
        else if (currentAnimator != null)
        {
            // Fallback: 직접 애니메이션 재생
            currentAnimator.speed = animationSpeed;
        }

        // 이벤트 준비
        pendingEvents.Clear();
        pendingEvents.AddRange(currentPlayingTimeline.GetAllEventsSorted().Where(e => e.TriggerTime > timelinePlaybackTime));

        // Update 루프 시작
        EditorApplication.update += UpdateTimelinePlayback;

        UnityEngine.Debug.Log($"[BattlePreview] Started timeline playback: {currentPlayingTimeline.timelineName}");
    }


    private void StopTimelinePlayback()
    {
        isTimelinePlaying = false;

        // Update 루프 정지
        EditorApplication.update -= UpdateTimelinePlayback;

        // PlaybackAdapter 정지
        if (playbackAdapter != null)
        {
            playbackAdapter.Pause();
        }

        // AnimationMode 정리
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        ClearSpawnedEffects();

        UnityEngine.Debug.Log("[BattlePreview] Stopped timeline playback");
    }

    private void ResetTimelinePlayback()
    {
        StopTimelinePlayback();

        currentFrame = 0;
        timelinePlaybackTime = 0f;

        SeekToFrame(0);

        if (playbackAdapter != null)
        {
            playbackAdapter.Reset();
        }

        pendingEvents.Clear();
        if (currentPlayingTimeline != null)
        {
            pendingEvents.AddRange(currentPlayingTimeline.GetAllEventsSorted());
        }
    }


    private void UpdateTimelinePlayback()
    {
        if (!isTimelinePlaying || currentPlayingTimeline == null) return;

        // 프레임 업데이트
        currentFrame += Mathf.RoundToInt(animationSpeed);

        // Timeline 종료 체크
        if (currentFrame >= totalFrames)
        {
            currentFrame = totalFrames;
            StopTimelinePlayback();
            return;
        }

        // 현재 프레임으로 이동
        SeekToFrame(currentFrame);

        Repaint();
    }

    private void ExecuteTimelineEvent(TimelineDataSO.ITimelineEvent evt)
    {
        if (previewInstance == null) return;

        // Animation Event
        if (evt is TimelineDataSO.AnimationEvent animEvt)
        {
            if (currentAnimator != null)
            {
                currentAnimator.CrossFade(animEvt.animationStateName, animEvt.crossFadeDuration);
            }
            UnityEngine.Debug.Log($"[Timeline] Play Animation: {animEvt.animationStateName}");
        }
        // Effect Event
        else if (evt is TimelineDataSO.EffectEvent effectEvt)
        {
            SpawnEffect(effectEvt);
            UnityEngine.Debug.Log($"[Timeline] Spawn Effect: {effectEvt.effectAddressableKey}");
        }
        // Sound Event
        else if (evt is TimelineDataSO.SoundEvent soundEvt)
        {
            UnityEngine.Debug.Log($"[Timeline] Play Sound: {soundEvt.soundEventPath}");
        }
    }

    private void SpawnEffect(TimelineDataSO.EffectEvent effectEvt)
    {
        // 간단한 이펙트 프리뷰 (실제 Addressable 로드 대신 더미 오브젝트)
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = $"Effect_{effectEvt.effectAddressableKey}";
        effect.transform.localScale = Vector3.one * 0.3f;

        // 머티리얼 색상 설정
        var renderer = effect.GetComponent<Renderer>();
        renderer.material.color = new Color(1f, 0.5f, 0f, 0.5f);

        // 위치 설정
        if (previewInstance != null)
        {
            effect.transform.position = previewInstance.transform.position + effectEvt.positionOffset;

            if (effectEvt.attachToActor)
            {
                effect.transform.SetParent(previewInstance.transform);
            }
        }

        // 저장 (나중에 정리용)
        string key = $"{effectEvt.effectAddressableKey}_{Time.time}";
        spawnedEffects[key] = effect;

        // 자동 제거
        DelayedDestroyEffect(key, effectEvt.duration);
    }

    private async void DelayedDestroyEffect(string key, float delay)
    {
        await System.Threading.Tasks.Task.Delay((int)(delay * 1000));

        if (spawnedEffects.ContainsKey(key))
        {
            if (spawnedEffects[key] != null)
                DestroyImmediate(spawnedEffects[key]);
            spawnedEffects.Remove(key);
        }
    }

    private void ClearSpawnedEffects()
    {
        foreach (var effect in spawnedEffects.Values)
        {
            if (effect != null)
                DestroyImmediate(effect);
        }
        spawnedEffects.Clear();
    }




    private void PlaySpriteAnimation(string key)
    {
        if (currentSpriteAnimator != null)
        {
            currentSpriteAnimator.SetAnimation(key, true);
            isPlayingSprite = true;

            // 선택된 인덱스 업데이트
            for (int i = 0; i < spriteAnimationKeys.Length; i++)
            {
                if (spriteAnimationKeys[i] == key)
                {
                    selectedSpriteAnimation = i;
                    break;
                }
            }
        }
    }



    private void DrawEffectControls()
    {
        showEffectControls = EditorGUILayout.Foldout(showEffectControls, "Effect Controls", true);
        if (!showEffectControls) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        effectPrefabPath = EditorGUILayout.TextField("Effect Path", effectPrefabPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("Select Effect Prefab", "Assets", "prefab");
            if (!string.IsNullOrEmpty(path))
            {
                effectPrefabPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        effectScale = EditorGUILayout.Slider("Scale", effectScale, 0.1f, 5f);
        effectOffset = EditorGUILayout.Vector3Field("Offset", effectOffset);
        attachEffectToModel = EditorGUILayout.Toggle("Attach to Model", attachEffectToModel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load Effect"))
        {
            LoadEffect();
        }
        if (GUILayout.Button("Play Effect"))
        {
            PlayEffect();
        }
        if (GUILayout.Button("Clear"))
        {
            ClearEffect();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawGroupFormationControls()
    {
        showGroupFormation = EditorGUILayout.Foldout(showGroupFormation, "Formation Controls", true);
        if (!showGroupFormation) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        previewFormation = (FormationType)EditorGUILayout.EnumPopup("Formation", previewFormation);
        formationSpacing = EditorGUILayout.Slider("Spacing", formationSpacing, 0.5f, 3f);
        showFormationLines = EditorGUILayout.Toggle("Show Lines", showFormationLines);

        if (GUILayout.Button("Update Formation"))
        {
            UpdateGroupFormation();
        }

        // 몬스터별 표시
        if (previewGroup != null)
        {
            EditorGUILayout.LabelField("Monsters:", EditorStyles.miniBoldLabel);
            for (int i = 0; i < 5; i++)
            {
                var slot = previewGroup.MonsterSlots[i];
                if (!slot.isEmpty && slot.monsterData != null)
                {
                    EditorGUILayout.LabelField($"Slot {i}: {slot.monsterData.MonsterName}");
                }
            }
        }

        EditorGUILayout.EndVertical();
    }


    private void DrawPreviewPanel()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);


        // 프리뷰 영역
        Rect previewRect = GUILayoutUtility.GetRect(
            GUIContent.none,
            GUIStyle.none,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true)
        );

        if (Event.current.type == EventType.Repaint)
        {
            RenderPreview(previewRect);
        }

        // 마우스 입력 처리
        HandlePreviewInput(previewRect);

    }

    private void RenderPreview(Rect rect)
    {
        if (previewRenderUtility == null)
        {
            InitializePreview();
            return;
        }


        // 카메라 확인
        if (previewRenderUtility.camera == null)
        {
            UnityEngine.Debug.LogError("[Preview] Camera is null!");
            return;
        }

        // 렌더링 대상 확인
        if (previewInstance != null)
        {
            UnityEngine.Debug.Log($"Preview Instance: {previewInstance.name}, Active: {previewInstance.activeInHierarchy}");
        }


        previewRenderUtility.BeginPreview(rect, GUIStyle.none);


        previewRenderUtility.ambientColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        if (previewRenderUtility.lights.Length > 0)
        {
            previewRenderUtility.lights[0].intensity = 1.5f;
            previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(30f, -30f + previewRotation.x, 0f);
        }



        // 자동 회전
        if (autoRotate)
        {
            previewRotation.x += rotationSpeed * Time.deltaTime;
        }

        // 카메라 위치 설정
        float rad = previewDistance;
        float phi = previewRotation.x * Mathf.PI / 180f;
        float theta = previewRotation.y * Mathf.PI / 180f;

        Vector3 camPos = new Vector3(
            rad * Mathf.Sin(phi) * Mathf.Cos(theta),
            rad * Mathf.Sin(theta),
            rad * Mathf.Cos(phi) * Mathf.Cos(theta)
        );

        previewRenderUtility.camera.transform.position = camPos;
        previewRenderUtility.camera.transform.LookAt(Vector3.zero);

        // 조명 업데이트
        if (previewLights.Count > 0 && previewLights[0] != null)
        {
            previewLights[0].transform.rotation = Quaternion.Euler(30f, -30f + previewRotation.x, 0f);
        }



        // 렌더링
        previewRenderUtility.camera.Render();

        previewRenderUtility.Render();

        Texture resultTexture = previewRenderUtility.EndPreview();
        GUI.DrawTexture(rect, resultTexture);
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event e = Event.current;

        if (!rect.Contains(e.mousePosition))
            return;

        // 마우스 드래그로 회전
        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            previewRotation.x -= e.delta.x;
            previewRotation.y = Mathf.Clamp(previewRotation.y - e.delta.y, -89f, 89f);
            Repaint();
        }

        // 스크롤로 줌
        if (e.type == EventType.ScrollWheel)
        {
            previewDistance = Mathf.Clamp(previewDistance + e.delta.y * 0.5f, MIN_CAMERA_DISTANCE, MAX_CAMERA_DISTANCE);
            Repaint();
        }

        // 우클릭 메뉴
        if (e.type == EventType.ContextClick)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Reset Camera"), false, () => ApplyCameraPreset(CameraPreset.Default));
            menu.ShowAsContext();
            e.Use();
        }
    }


    private void DrawPreviewInfo()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (currentPreviewType == PreviewType.Character && previewCharacter != null)
        {
            EditorGUILayout.LabelField($"Character: {previewCharacter.CharacterName}");
            EditorGUILayout.LabelField($"ID: {previewCharacter.CharacterId}");

            // 2D/3D 모드 표시
            EditorGUILayout.LabelField($"Mode: {(is2DMode ? "2D Sprite" : "3D Model")}");

            if (is2DMode && currentSpriteAnimator != null)
            {
                EditorGUILayout.LabelField($"Sprite Sheets: {currentSpriteAnimator.spriteSheets.Count}");
                if (currentSpriteAnimator.IsLoaded)
                {
                    EditorGUILayout.LabelField($"Playing: {currentSpriteAnimator.CurrentKey}");
                }
            }

            EditorGUILayout.LabelField($"Character: {previewCharacter.CharacterName}");
            EditorGUILayout.LabelField($"ID: {previewCharacter.CharacterId}");
            EditorGUILayout.LabelField($"Tier: {previewCharacter.Tier}, Class: {previewCharacter.CharacterClass}");

            var stats = previewCharacter.GetStatsAtLevel(1);
            EditorGUILayout.LabelField($"HP: {stats.hp}, ATK: {stats.attack}, DEF: {stats.defense}");


        }
        else if (currentPreviewType == PreviewType.Monster && previewMonster != null)
        {
            EditorGUILayout.LabelField($"Monster: {previewMonster.MonsterName}");
            EditorGUILayout.LabelField($"ID: {previewMonster.MonsterId}");
            EditorGUILayout.LabelField($"Type: {previewMonster.MonsterType}, Pattern: {previewMonster.BehaviorPattern}");

            var stats = previewMonster.GetFinalStats(1);
            EditorGUILayout.LabelField($"HP: {stats.hp}, ATK: {stats.attack}, DEF: {stats.defense}");

            if (previewMonster.HasGuardianStone)
            {
                EditorGUILayout.LabelField($"Guardian Stones: {string.Join(", ", previewMonster.GuardianStoneElements)}");
            }
        }
        else if (currentPreviewType == PreviewType.MonsterGroup && previewGroup != null)
        {
            EditorGUILayout.LabelField($"Group: {previewGroup.GroupName}");
            EditorGUILayout.LabelField($"ID: {previewGroup.GroupId}");
            EditorGUILayout.LabelField($"Purpose: {previewGroup.GroupPurpose}");
            EditorGUILayout.LabelField($"Power: {previewGroup.TotalPower}");
            EditorGUILayout.LabelField($"Monsters: {previewGroup.GetActiveMonsterCount()}/5");
        }

        EditorGUILayout.EndVertical();
    }



    #endregion

    #region Public Methods


    public void SetPreviewTarget(BattleMonsterDataSO monster)
    {
        previewMonster = monster;
        currentPreviewType = PreviewType.Monster;
        LoadMonsterModel();
    }

    public void SetPreviewTarget(MonsterGroupDataSO group)
    {
        previewGroup = group;
        currentPreviewType = PreviewType.MonsterGroup;
        showGroupFormation = true;
        LoadGroupModels();
    }

    #endregion
}

// 간단한 회전 컴포넌트 (수호석 애니메이션용)
public class SimpleRotator : MonoBehaviour
{
    public float rotationSpeed = 30f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}


// 공통으로 사용할 수 있는 SafeDestroy 메서드
public static class DestroyUtility
{
    public static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEngine.Object.DestroyImmediate(obj);
        else
#endif
            UnityEngine.Object.Destroy(obj);
    }
}


#endregion