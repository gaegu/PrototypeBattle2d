#pragma warning disable CS0108
//=========================================================================================================
//using System;
//using System.Collections;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
using System.Linq;
using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;
using IronJade.Observer.Core;
using UnityEngine;
using System.Collections.Generic;
//using WorldStreamer2;
using IronJade.UI.Core;
using Unity.AI.Navigation;
using IronJade.Table.Data;

#if UNITY_EDITOR
using AssetKits.ParticleImage;
using UnityEditor;
#endif

//=========================================================================================================

[DisallowMultipleComponent]
[ExecuteInEditMode]
public class BackgroundSceneManager : MonoBehaviour, IObserver
{
    private static BackgroundSceneManager instance;
    public static BackgroundSceneManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(BackgroundSceneManager).Name;
                GameObject manager = GameObject.Find(className);

                if (manager != null)
                    instance = manager.GetComponent<BackgroundSceneManager>();

                return instance;
            }

            return instance;
        }
    }

    private BackgroundSceneManager() { }
    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public FieldMapDefine FieldMapDefine => fieldMapDefine;
    public GameObject TownObjectParent { get => townObjects; }
    #endregion Coding rule : Property

    #region Coding rule : Value

    [SerializeField]
    [Header("타운씬 로드")]
    private bool isTownSceneUnLoad = false;

    [Header("시네머신 타겟")]
    [SerializeField]
    private FollowTargetSupport cinemachineFollowTarget = null;

    [Header("필드맵")]
    [SerializeField]
    private FieldMapDefine fieldMapDefine;

    [Header("그룹")]
    [SerializeField]
    private GameObject townGroup = null;

    [Header("타운 오브젝트들")]
    [SerializeField]
    private BaseTownObjectSupport[] baseTownObjectSupports = null;

    [Header("미니맵")]
    [SerializeField]
    private Transform backgroundTarget;

    [SerializeField]
    private MinimapBackgroundUnit minimapBackgroundUnit;

    [SerializeField]
    [HideInInspector]
    [Header("미니맵 배경 영역 [임의로 수정X]")]
    private Rect minimapBackgroundBoundaryRect;

    [Header("배경 오브젝트")]
    [SerializeField]
    private GameObject environment = null;

    [Header("타운오브젝트")]
    [SerializeField]
    private GameObject townObjects = null;

    [Header("NPC Transfrom")]
    [SerializeField]
    private Transform fixedNpcObjects = null;

    [SerializeField]
    private GameObject[] defaultVolumes;

    [SerializeField]
    private GameObject[] beautifyVolumes;

    [Header("밟을 수 있는 지형")]
    [SerializeField]
    private GameObject[] objectToCanbeGrounded = null;

    [Header("층 구분 구역")]
    [SerializeField]
    private List<Transform> floors = null;

    [Header("데코레이터 공장")]
    [SerializeField]
    private TownDecoratorFactory townDecoratorFactory;

    [Header("미니맵 추가 영역 offset")]
    [SerializeField]
    public float minimapBoundaryOffset;

    [SerializeField]
    private bool isNetwork = true;

    [SerializeField]
    private Transform cinemachineGroup;
    private Transform warpPointParent;

    [SerializeField]
    private Light light = null;
    #endregion Coding rule : Value

    #region Coding rule : Function

    public virtual async UniTask ChangeViewAsync(UIType uIType, UIType prevUIType)
    {
        switch (uIType)
        {
            case UIType.HousingSimulationView:
            case UIType.StageDungeonView:
                {
                    cinemachineGroup.SafeSetActive(false);
                    ShowTownGroup(false);
                    CameraManager.Instance.SetActiveTownCameras(false);
                    break;
                }

            case UIType.CharacterIntroduceView:
            case UIType.CharacterCollectionDetailView:
            case UIType.CharacterCostumeView:
                {
                    ShowTownGroup(false);
                    break;
                }

            default:
                {
                    cinemachineGroup.SafeSetActive(false);
                    ShowTownGroup(true);
                    CameraManager.Instance.SetActiveTownCameras(true);
                    break;
                }
        }

        await UniTask.CompletedTask;
    }

    public virtual async UniTask ChangePopupAsync(UIType uIType, UIType prevViewUIType)
    {
        if (uIType == UIType.ToastMessagePopup)
            return;

        if (UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView))
        {
            cinemachineGroup.SafeSetActive(true);
            ShowTownGroup(true);
            CameraManager.Instance.SetActiveTownCameras(true);
        }
        else if (UIManager.Instance.CheckOpenCurrentView(UIType.HousingSimulationView))
        {
            cinemachineGroup.SafeSetActive(false);
            ShowTownGroup(false);
            CameraManager.Instance.SetActiveTownCameras(false);
        }

        await UniTask.CompletedTask;
    }

    public void AddTownObjects(FieldMapDefine fieldMapEnumId)
    {
        TownObjectManager.Instance.SetEventWarpPointGet(GetWarpPoint);

        if (baseTownObjectSupports == null)
            return;

        for (int i = 0; i < baseTownObjectSupports.Length; ++i)
        {
            var townObject = baseTownObjectSupports[i];

            if (townObject == null)
                continue;

            TownObjectManager.Instance.AddTownObject(townObject.TownObjectType, townObject);
            TownObjectManager.Instance.SetTownNpcInfo(townObject, fieldMapEnumId);
        }

        TownObjectManager.Instance.SetFloors(floors);
        //TownObjectManager.Instance.SetMinimapBackgroundUnitLoader(minimapBackgroundUnitLoader);
    }

    /// <summary>
    /// 데코레이터 공장을 가동한다.
    /// </summary>
    public void OperateTownDecoratorFactory()
    {
        if (townDecoratorFactory != null)
            TownObjectManager.Instance.SetDecoratorFactory(townDecoratorFactory.OnEventOperate, townDecoratorFactory.OnEventCancel);
    }

    public void SetCinemachineFollowTarget()
    {
        if (cinemachineFollowTarget == null)
            return;

        cinemachineFollowTarget.SetFollowTarget(PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.Transform);
        cinemachineFollowTarget.FollowImmediately();
    }

    /// <summary>
    /// 마을 그룹군을 활성/비활성 한다. (배경, NPC, Gimmick 등)
    /// </summary>
    public void ShowTownGroup(bool isActive)
    {
        townGroup?.SafeSetActive(isActive);

        //기본적으로 배경도 타운그룹이랑 따라간다.
        environment?.SafeSetActive(isActive);
        townObjects?.SafeSetActive(isActive);
        //streamerSceneParent.SafeSetActive(isActive);
    }

    /// <summary>
    /// 마을의 고정 배경을 활성/비활성 한다. (씬에 박아 놓고 사용하는 리소스들)
    /// </summary>
    public void ShowEnvironment(bool isActive)
    {
        environment?.SafeSetActive(isActive);
    }

    /// <summary>
    /// 마을 오브젝트들을 활성/비활성 한다.
    /// </summary>
    public void ShowTownObjects(bool isActive)
    {
        townObjects.SafeSetActive(isActive);
    }

    /// <summary>
    /// 마을 심리스 배경을 활성/비활성 한다. (동적으로 생성하는 리소스들)
    /// </summary>
    public void ShowStreamer(bool isActive)
    {
        //streamerSceneParent.SafeSetActive(isActive);
    }

    /// <summary>
    /// 마을 배경을 불러온다.
    /// </summary>
    public async UniTask LoadTownBackground(string path)
    {
        // 현재는 씬에 박혀있음
        // 추후에는 Field Table을 추가해서 상황별 맵을 불러와야 함
        // 기본 : 아무런 상황이 아닐 때
        // 이벤트 : 이벤트 기간 동안
        // 스토리 : 스토리 진행 여부에 따라서 달라질 수도 있을 듯
        await UniTask.CompletedTask;
    }

    public Transform GetWarpPoint(string key)
    {
        Transform objectParent = townObjects != null ? townObjects.transform : transform;

        if (warpPointParent == null)
            warpPointParent = objectParent.Find(StringDefine.TOWN_OBJECT_WARP_POINTS_PARENT);

        if (warpPointParent == null)
            warpPointParent = objectParent;

        return warpPointParent.SafeGetChildByName(key);
    }

    public bool CheckActiveTown()
    {
        return townGroup.SafeGetActiveSelf();
    }

    void IObserver.HandleMessage(System.Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case CharacterObserverID.Change:
                {
                    SetCinemachineFollowTarget();
                    break;
                }

            case TownObserverID.SfxState:
                {
                    BoolParam boolParam = (BoolParam)observerParam;

                    TownObjectManager.Instance.SoundMute(!boolParam.Value1);
                    break;
                }
        }
    }

    public void PlayCutsceneState(bool isShow, bool isTown)
    {
        if (isTown)
        {
            //ShowTownGroup(true);
            TownObjectManager.Instance.RefreshProcess().Forget();
        }
        else
        {
            gameObject.SafeSetActive(!isShow);

            PlayerManager.Instance.ShowMyTownPlayerGroup(!isShow);
            //ShowTownGroup(!isShow);
        }
    }

    /// <summary>
    /// 타임라인에서 호출되는 이벤트 (존쿠퍼)
    /// </summary>
    public void OnEventPlayerOffByTimeline()
    {
        PlayerManager.Instance.ShowMyTownPlayerGroup(false);
    }

    public void OnDisableVolumeObjects()
    {
        //Volume 임시 끄기. gaegu 수정시 나에게 문의 
        UnityEngine.Rendering.Volume[] volumeObject = GetComponentsInChildren<UnityEngine.Rendering.Volume>(true);  // includeInactive를 true로 설정
        foreach (UnityEngine.Rendering.Volume v in volumeObject)
        {
            v.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (instance != null)
            {
                if (instance != this)
                    return;
            }

            if (light == null)
                light = transform.GetComponentInChildren<Light>(true);

            if (!CheckTestScene())
                return;

            TestSceneSetting();
            return;
        }

        if (CheckTestScene())
            return;
#endif

#if UNITY_EDITOR
        if (TestModeGameManager.Instance != null && TestModeGameManager.Instance.IsOfflineTownTest)
            EditorForceOpen();
#endif

        ObserverManager.AddObserver(CharacterObserverID.Change, this);
        ObserverManager.AddObserver(TownObserverID.SfxState, this);

        if (light == null)
            light = transform.GetComponentInChildren<Light>(true);

        CameraManager.Instance.SetCurrentBackgroundLight(light);

        if (light == null)
            light = transform.GetComponentInChildren<Light>(true);

        var BackTown_Nav = transform.SafeGetChildByName("BackTown_Nav");
        if (BackTown_Nav == null)
            return;

        BackTown_Nav.localRotation = Quaternion.identity;
        BackTown_Nav.SafeSetActive(true);
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (RenderSettings.fog == false)
        {
            IronJade.Debug.Log("포그세팅 원복 (true)");
            RenderSettings.fog = true;
        }

        if (CheckTestScene())
            return;
#endif
        ObserverManager.RemoveObserver(CharacterObserverID.Change, this);
        ObserverManager.RemoveObserver(TownObserverID.SfxState, this);
    }

#if CHEAT
    public GameObject[] GetDefaultVolumes()
    {
        return defaultVolumes;
    }

    public GameObject[] GetBeautifyVolumes()
    {
        return beautifyVolumes;
    }
#endif


#if UNITY_EDITOR
    private List<Renderer> renderers = new List<Renderer>(1000);

    /// <summary>
    /// 미니맵 에디터
    /// </summary>
    [HideInInspector]
    public int minimapFloorIndex = -1;


    public string[] minimapFloorString = null;

    /// <summary>
    /// 저장 직전
    /// </summary>
    private static void SceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        var testModeGameManager = GameObject.FindObjectsByType<TestModeGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (testModeGameManager != null)
        {
            for (int i = 0; i < testModeGameManager.Length; ++i)
                DestroyImmediate(testModeGameManager[i].gameObject);
        }

        if (!CheckTestScene())
            return;

        IronJade.Debug.Log("Town Test : SceneSaving");

        if (GameObject.Find("GameManager") != null)
            return;

        var townGroup = GameObject.Find("TownGroup");

        townGroup.SafeSetActive(false);

        if (Instance != null)
        {
            Instance.baseTownObjectSupports = townGroup.transform.GetComponentsInChildren<BaseTownObjectSupport>();
            Instance.townDecoratorFactory = townGroup.transform.GetComponentInChildren<TownDecoratorFactory>();
        }
    }

    /// <summary>
    /// 저장 이후
    /// </summary>
    private static void SceneSaved(UnityEngine.SceneManagement.Scene scene)
    {
        if (!CheckTestScene())
            return;

        IronJade.Debug.Log("Town Test : SceneSaved");


        Instance?.TestSceneSetting();
    }

    /// <summary>
    /// 씬을 닫았을 때 (플레이 모드 진입 시에도 이게 작동한다.)
    /// </summary>
    private void SceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
    {
        if (!CheckTestScene())
            return;

        IronJade.Debug.Log($"Town Test : SceneClosing, Removing : {removingScene}");
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= SceneClosing;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= SceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= SceneSaved;
    }

    /// <summary>
    /// 코드 갱신 시
    /// </summary>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void Reload()
    {
        if (!CheckTestScene())
            return;

        IronJade.Debug.Log("Town Test : Reload");

        if (GameObject.Find("GameManager") != null)
            return;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

        if (scene == null)
            return;

        GameObject testModeGameManager = GameObject.Find("TestModeGameManager");

        if (testModeGameManager == null)
            return;

        Instance.TestSceneSetting();
    }

    private static bool CheckTestScene()
    {
        if (Application.isPlaying)
            return false;

        if (!Application.isEditor)
            return false;

        if (BuildPipeline.isBuildingPlayer)
            return false;

        if (GameObject.Find("BackgroundSceneManager") == null)
            return false;

        return true;
    }

    private void TestSceneSetting()
    {
        if (GameObject.Find("GameManager") != null)
            return;

        GameObject testModeGameManager = GameObject.Find("TestModeGameManager");
        if (testModeGameManager == null)
        {
            var tmgm = UtilModel.Resources.Instantiate<TestModeGameManager>(StringDefine.PATH_TEST_MODE_GAME_MANAGER, this);
            tmgm.SafeSetName("TestModeGameManager");
            tmgm.Awake();
        }

        UnityEditor.SceneManagement.PrefabStage prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

        if (prefabStage != null)
            return;

        ShowTownGroup(true);

        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= SceneClosing;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= SceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= SceneSaved;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += SceneSaving;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += SceneSaved;
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += SceneClosing;

        CameraManager.Instance.SortingCameraStack();
    }

    private void Start()
    {
        TestModeFromBackgroundScene().Forget();
    }

    private async UniTask TestModeFromBackgroundScene()
    {
        if (!Application.isPlaying)
            return;

        if (TestModeGameManager.Instance == null)
            return;

        await TestGamePlay(isNetwork == true && !TestModeGameManager.Instance.IsOfflineTownTest);
    }

    public async UniTask TestGamePlay(bool isNetwork)
    {
        // IntroFlow 처리
        FlowManager.Instance.TestModeCreateFlow(FlowType.IntroFlow, isStack: false);
        BaseFlow introFlow = FlowManager.Instance.CurrentFlow;
        introFlow.Enter();
        await introFlow.TestLoadingProcess(isNetwork);

        // 배경씬 정보 교체
        IronJade.Debug.Log(gameObject.scene.path);

        if (isNetwork)
        {
            var LeaderCharacterPosition = PlayerManager.Instance.UserSetting.GetUserSettingData<LeaderCharacterPositionUserSetting>();
            LeaderCharacterPosition.SetScenePath(fieldMapDefine.ToString());

            var townBillboardSupports = GameObject.FindObjectsByType<TownBillboardSupport>(FindObjectsSortMode.None);

            if (townBillboardSupports != null)
            {
                for (int i = 0; i < townBillboardSupports.Length; ++i)
                {
                    await townBillboardSupports[i].ShowAsync();
                }
            }
        }
        else
        {
            var table = TableManager.Instance.GetTable<FieldMapTable>();

            if (table != null)
            {
                FieldMapTableData fieldMapTableData = table.GetDataByEnumId(fieldMapDefine.ToString());

                if (!fieldMapTableData.IsNull())
                    PlayerManager.Instance.MyPlayer.SetCurrentFieldMap(fieldMapTableData.GetSCENE_PATH(), fieldMapDefine);
            }
        }

        if (isTownSceneUnLoad)
            return;

        // TownFlow 처리
        FlowManager.Instance.TestModeCreateFlow(FlowType.TownFlow, isStack: false);

        BaseFlow townFlow = FlowManager.Instance.CurrentFlow;
        townFlow.Enter();

        if (TestModeGameManager.Instance.IsOfflineTownTest)
        {
            EditorForceOpen();

            TownFlowModel townFlowModel = townFlow.GetModel<TownFlowModel>();
            townFlowModel.SetOffline(true);
            townFlowModel.SetCurrentScene(fieldMapDefine.ToString());
        }

        await townFlow.TestLoadingProcess(isNetwork);

        Camera townCamera = CameraManager.Instance.GetCamera(GameCameraType.TownCharacter);
        Transform lookAtTarget = townCamera.transform;
        TownSceneManager.Instance.TownInputSupport.SetVirtualJoystickLookAtTarget(lookAtTarget);

        DontDestroyOnLoad(TestModeGameManager.Instance.gameObject);
    }

    public void AddMeshCollider()
    {
        MeshRenderer[] groundRenderers = transform.GetComponentsInChildren<MeshRenderer>();

        //배경팀에서 밟을 수 있는 지형은 Ground or Terrain으로 네이밍 하신다고 함.
        GameObject[] rendererGos = groundRenderers.Where(
            x => x.gameObject.name.Contains("Ground") || x.gameObject.name.Contains("Terrain")
            )
            .Select(x => x.gameObject)
            .ToArray();

        foreach (var go in rendererGos)
        {
            if (go != null && go.GetComponent<MeshCollider>() == null)
            {
                go.AddComponent<MeshCollider>();
                IronJade.Debug.Log($"Mesh Collider added : {go.name}");
            }
        }

        EditorUtility.SetDirty(this);
    }

    private void FindRenderer(Transform transform, bool checkTag = false)
    {
        Renderer renderer = transform.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (!renderer.enabled || !renderer.transform.gameObject.activeInHierarchy)
                return;

            if (checkTag && renderer.gameObject.CompareTag(StringDefine.TAG_CULLING_ALPHA))
                return;

            renderers.Add(renderer);
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            FindRenderer(transform.GetChild(i));
        }
    }

    public void CalculateMinimapBoundary()
    {
        FindGroundRenderer(true);
    }

    private void FindGroundRenderer(bool calc)
    {
        Transform checkTransform = null;

        if (backgroundTarget != null)
        {
            checkTransform = backgroundTarget;
        }
        else
        {
            EditorUtility.DisplayDialog("미니맵 영역 갱신", "BackgroundTarget이 없습니다. 씬에서 배경 메쉬를 찾아 할당해주세요.", "확인");
            return;
        }

        renderers.Clear();
        FindRenderer(checkTransform);

        if (calc)
        {
            List<Renderer> groundRenderers = new List<Renderer>();

            for (int i = 0; i < renderers.Count; ++i)
            {
                if (renderers[i].CompareTag("Ground"))
                    groundRenderers.Add(renderers[i]);
            }

            float minX = groundRenderers.Count == 0 ? 0 : groundRenderers.Min(mr => mr.bounds.min.x);
            float minZ = groundRenderers.Count == 0 ? 0 : groundRenderers.Min(mr => mr.bounds.min.z);
            float maxX = groundRenderers.Count == 0 ? 0 : groundRenderers.Max(mr => mr.bounds.max.x);
            float maxZ = groundRenderers.Count == 0 ? 0 : groundRenderers.Max(mr => mr.bounds.max.z);

            float width = maxX - minX;
            float height = maxZ - minZ;

            float largerSide = Mathf.Max(width, height) + minimapBoundaryOffset;
            minX -= minimapBoundaryOffset / 2;
            minZ -= minimapBoundaryOffset / 2;

            // 기존 미니맵 영역을 임시 변수에 저장
            Rect oldRect = minimapBackgroundBoundaryRect;
            minimapBackgroundBoundaryRect = new Rect(minX, minZ, largerSide, largerSide);

            if (minimapBackgroundBoundaryRect.width == 0 && minimapBackgroundBoundaryRect.height == 0)
            {
                EditorUtility.DisplayDialog("미니맵 영역 갱신", "BackgroundTarget 에 Ground 태그가 없거나, 영역의 크기가 0입니다.\n문의 부탁드립니다.", "확인");
                return;
            }

            // 영역이 변경되었으면 씬 저장
            if (oldRect != minimapBackgroundBoundaryRect)
            {
                EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("미니맵 영역 갱신", "미니맵 영역이 변경되어 씬이 저장되었습니다.\n씬 업로드가 필요합니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("미니맵 영역 갱신", "변경 사항이 없습니다.", "확인");
            }
        }
    }

    public async UniTask TestLoadMinimapBackground()
    {
        if (minimapBackgroundUnit == null)
        {
            FieldMapTable fieldMapTable = TableManager.Instance.GetTable<FieldMapTable>();
            FieldMapTableData fieldMapTableData = fieldMapTable.GetDataByID((int)fieldMapDefine);

            string path = StringDefine.PATH_RESOURCES_ADDRESSABLE + StringDefine.PATH_MINIMAP_BACKGROUND_PREFIX + fieldMapDefine.ToString() + ".prefab";
            var backgroundPrefab = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;

            if (backgroundPrefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(backgroundPrefab);
                minimapBackgroundUnit = go == null ? null : go.GetComponent<MinimapBackgroundUnit>();
            }

            if (minimapBackgroundUnit == null)
            {
                IronJade.Debug.LogError("MinimapBackgrounUnit 이 없습니다.");
                return;
            }
        }

        if (minimapBackgroundBoundaryRect.width == 0 || minimapBackgroundBoundaryRect.height == 0)
            IronJade.Debug.LogError($"배경 사이즈 확인 필요 : {minimapBackgroundBoundaryRect.width} / {minimapBackgroundBoundaryRect.height} ");

        await minimapBackgroundUnit.ShowAsync();

        if (floors != null && floors.Count > 0)
        {
            minimapBackgroundUnit.transform.position = new Vector3(minimapBackgroundUnit.transform.position.x,
                                                                   floors[0].transform.position.y,
                                                                   minimapBackgroundUnit.transform.position.z);
        }
    }

    public void EditorMinimapSave()
    {
        FieldMapTable fieldMapTable = TableManager.Instance.GetTable<FieldMapTable>();
        FieldMapTableData fieldMapTableData = fieldMapTable.GetDataByID((int)fieldMapDefine);
    }

    private void OnDrawGizmos()
    {
        if (IsSetRect())
            DrawGizmoRect(minimapBackgroundBoundaryRect);
    }

    private GameObject minimapBoundaryCube = null;

    private void DrawGizmoRect(Rect rect)
    {
        Gizmos.color = Color.red;

        Vector3[] corners =
        {
            new Vector3(rect.xMin, 0, rect.yMin),
            new Vector3(rect.xMax, 0, rect.yMin),
            new Vector3(rect.xMax, 0, rect.yMax),
            new Vector3(rect.xMin, 0, rect.yMax)
        };

        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
    }

    public void AddMinimapBoundaryCube()
    {
        Rect rect = minimapBackgroundBoundaryRect;

        if (minimapBoundaryCube == null)
            minimapBoundaryCube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        float xCenter = rect.xMin + (rect.width / 2);
        float zCenter = rect.yMin + (rect.height / 2);

        minimapBoundaryCube.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        {
            color = Color.black
        };

        minimapBoundaryCube.transform.position = new Vector3(xCenter, -100, zCenter);

        minimapBoundaryCube.transform.localScale = new Vector3(rect.width, 1, rect.height);

        minimapBoundaryCube.name = "minimapTestCube";

        UpdateMinimapBoundaryCubePosition();
    }

    public void UpdateMinimapBoundaryCubePosition()
    {
        if (minimapBoundaryCube == null)
            return;

        // 아래층 영역을 큐브로 가림
        if (floors != null)
        {
            int belowIndex = minimapFloorIndex - 1;
            float yValue = belowIndex >= 0 ? floors[belowIndex].transform.position.y : -100;

            minimapBoundaryCube.transform.position = new Vector3(minimapBoundaryCube.transform.position.x,
                                                                    yValue,
                                                                    minimapBoundaryCube.transform.position.z);
        }
    }

    public (string size, string ratio) UpdateMapSizeInfo()
    {
        if (minimapBackgroundBoundaryRect == null)
            return (string.Empty, string.Empty);

        int width = (int)minimapBackgroundBoundaryRect.width;
        int height = (int)minimapBackgroundBoundaryRect.height;
        int gcdValue = Gcd(width, height);

        int simplifiedWidth = gcdValue == 0 ? 0 : width / gcdValue;
        int simplifiedHeight = gcdValue == 0 ? 0 : height / gcdValue;

        string mapSizeString = $"{minimapBackgroundBoundaryRect.width} / {minimapBackgroundBoundaryRect.height}";
        string mapSizeRatio = $"{simplifiedWidth} : {simplifiedHeight}";

        return (mapSizeString, mapSizeRatio);
    }

    public bool IsSetRect()
    {
        return minimapBackgroundBoundaryRect.width != 0 && minimapBackgroundBoundaryRect.height != 0;
    }

    private int Gcd(int a, int b)
    {
        return b == 0 ? a : Gcd(b, a % b);
    }


    public GameObject copyPrefab;

    [ContextMenu("TEST")]
    public void ssssss()
    {
        for (int i = 0; i < baseTownObjectSupports.Length; ++i)
        {
            switch (baseTownObjectSupports[i])
            {
                case TownNpcSupport townNpcSupport:
                    {
                        if (string.IsNullOrEmpty(townNpcSupport.characterGuids))
                        {
                            townNpcSupport.characterGuids = AssetDatabase.AssetPathToGUID($"Assets/IronJade/ResourcesAddressable/{townNpcSupport.characterPath}.prefab");
                           IronJade.Debug.Log(townNpcSupport.characterGuids);
                        }
                        if (string.IsNullOrEmpty(townNpcSupport.characterAnimatorGuids))
                        {
                            townNpcSupport.characterAnimatorGuids = AssetDatabase.AssetPathToGUID($"Assets/IronJade/ResourcesAddressable/{townNpcSupport.characterAnimatorPath}.overrideController");
                           IronJade.Debug.Log(townNpcSupport.characterAnimatorGuids);
                        }
                        break;
                    }
            }
        }
    }

    [ContextMenu("TEST2")]
    public void ssssss2()
    {
        for (int i = 0; i < baseTownObjectSupports.Length; ++i)
        {
            switch (baseTownObjectSupports[i])
            {
                case TownNpcSupport townNpcSupport:
                    {
                        GameObject prefab = Instantiate(copyPrefab, townNpcSupport.townCharacter.transform);
                        prefab.SafeSetName("NpcTalk");
                        prefab.transform.localPosition = Vector3.zero;
                        prefab.transform.localScale = Vector3.one;
                        prefab.transform.localRotation = Quaternion.identity;
                        break;
                    }
            }
        }
    }

    [ContextMenu("FindSupports")]
    public void TestTTTT()
    {
        baseTownObjectSupports = transform.GetComponentsInChildren<BaseTownObjectSupport>();
        townDecoratorFactory = transform.GetComponentInChildren<TownDecoratorFactory>();

        //AssetDatabase.GUIDToAssetPath(Selection.activeObject);
        //string[] allMaterials = AssetDatabase.FindAssets("t:Material", null);
        ////var beforeShader = Shader.Find("Shader Graphs/ExosCharacterToon");
        //var beforeShader = Shader.Find("Shader Graphs/ToonCharacter_Hair");
        //var afterShader = Shader.Find("Universal Render Pipeline/Lit");
        //for (int i = 0; i < allMaterials.Length; ++i)
        //{
        //    string path = AssetDatabase.GUIDToAssetPath(allMaterials[i]);
        //    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        //    if (material == null)
        //        continue;

        //    if (material.shader != beforeShader)
        //        continue;

        //    material.shader = afterShader;
        //}

        //AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 구역을 찾는다. (기획에서 구역을 정하는 것부터 선행 되어야 함)
    /// </summary>
    private void FindMinimapFloor()
    {
        floors.Clear();

        Transform editorMinimapTools = transform.SafeGetChildByName("FloorGroup");
        Transform floor = editorMinimapTools.SafeGetChildByName("Floor");

        for (int i = 0; i < floor.childCount; ++i)
            floors.Add(floor.GetChild(i));

        minimapFloorString = new string[floors.Count];

        for (int i = 0; i < floors.Count; ++i)
            minimapFloorString[i] = floors[i].name;

        minimapFloorIndex = -1;
    }

    /// <summary>
    /// 걸을 수 있는 길과 아닌 길의 구분 (쉐이더 변경 및 색상 변경)
    /// </summary>
    public void ChangeMeshColor()
    {
        FindGroundRenderer(false);

        // 더미 메테리얼 생성
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        {
            color = new Color(0.3f, 0.8f, 0.3f, 0.5f)
        };
        var obstacleMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
        {
            color = Color.red
        };

        for (int i = 0; i < renderers.Count; ++i)
        {
            if (renderers[i].CompareTag("Ground"))
            {
                // Ground 관련 매쉬들의 색상을 회색으로 바꾼다.
                var groundMats = new List<Material>();
                for (int j = 0; j < renderers[i].sharedMaterials.Length; ++j)
                    groundMats.Add(groundMat);

                renderers[i].materials = groundMats.ToArray();
            }
            else
            {
                // Ground가 아닌 매쉬들의 색상을 빨강으로 바꾼다.
                var obstacleMats = new List<Material>();
                for (int j = 0; j < renderers[i].sharedMaterials.Length; ++j)
                    obstacleMats.Add(obstacleMat);

                renderers[i].materials = obstacleMats.ToArray();
            }
        }
    }

    /// <summary>
    /// 선택한 구역에 맞게 카메라를 이동시킨다.
    /// </summary>
    public void ChangeMinimapFloorCamera(int minimapFloorIndex)
    {
        Camera[] cameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; ++i)
            cameras[i].SafeSetActive(false);

        for (int i = 0; i < floors.Count; ++i)
            floors[i].SafeSetActive(false);

        floors[minimapFloorIndex].SafeSetActive(true);
        var floorCamera = floors[minimapFloorIndex].SafeGetChildByName("EditorMinimapFloorCamera");
        floorCamera.SafeSetActive(true);
        floorCamera.position = new Vector3(minimapBackgroundBoundaryRect.center.x,
                                           floors[minimapFloorIndex].position.y,
                                           minimapBackgroundBoundaryRect.center.y);
    }

    public void UpdateFloor(int floor)
    {
        if (minimapBackgroundUnit != null)
            minimapBackgroundUnit.EditorUpdateFloor(floor);
    }

    public void UpdateFloorString()
    {
        if (floors == null)
            return;

        minimapFloorString = new string[floors.Count];

        for (int i = 0; i < minimapFloorString.Length; i++)
            minimapFloorString[i] = $"{i + 1}F";
    }

    // 미니맵 바운더리
    public void SetMinimapBoundRender()
    {
        SpriteRenderer minimapSpriteRenderer = transform.SafeGetChildByName("EditorMinimapSpriteRenderer").GetComponent<SpriteRenderer>();
        minimapSpriteRenderer.transform.localScale = Vector3.one;

        float spriteSizeX = minimapSpriteRenderer.bounds.size.x;
        float spriteSizeZ = minimapSpriteRenderer.bounds.size.z;
        float xScaleFactor = minimapBackgroundBoundaryRect.width / spriteSizeX;
        float zScaleFactor = minimapBackgroundBoundaryRect.height / spriteSizeZ;

        minimapSpriteRenderer.transform.localScale = new Vector3(xScaleFactor, zScaleFactor, 1);
        minimapSpriteRenderer.transform.position = new Vector3(minimapBackgroundBoundaryRect.center.x,
                                                               minimapSpriteRenderer.transform.position.y,
                                                               minimapBackgroundBoundaryRect.center.y);

        minimapSpriteRenderer.gameObject.SafeSetActive(true);
    }

    public (Rect boundary, Vector3[] floors) GetMinimapBackgroundInfo()
    {
        return new(minimapBackgroundBoundaryRect, floors.Select(x => x.position).ToArray());
    }

    /// <summary>
    /// 테스트용
    /// </summary>
    public void SetIsNetwork(bool isOn)
    {
        isNetwork = isOn;
    }

    public BaseTownObjectSupport[] EditorGetTownObjects()
    {
        return baseTownObjectSupports;
    }

    private void EditorForceOpen()
    {
        foreach (var townObject in baseTownObjectSupports)
        {
            if (townObject is TownTriggerSupport trigger)
                trigger.EditorForceOpen();

            if (townObject is TownNpcSupport npc)
            {
                if (npc.TownObjectWarpType != TownObjectWarpType.None)
                    npc.EditorForceOpen();
            }
        }
    }

    public void EditorShowDeco(bool value)
    {
        townDecoratorFactory.SafeSetActive(value);
    }
#endif

    /// <summary>
    /// 테스트용
    /// </summary>
    //public void TestBG()
    //{
    //    townLodManager.SafeSetActive(!townLodManager.gameObject.SafeGetActiveSelf());
    //}
    #endregion Coding rule : Function
}