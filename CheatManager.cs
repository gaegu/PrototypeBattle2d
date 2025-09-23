#if CHEAT
#pragma warning disable CS4014
//=========================================================================================================
//using System;
//using System.Collections;
//using System.Collections.Generic;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using MagicaCloth2;
using QFSW.QC;
using QuestCondition;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AdaptivePerformance;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

[ExecuteInEditMode]
[DisallowMultipleComponent]
public class CheatManager : MonoBehaviour
{
    private static CheatManager instance;

    public static CheatManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(CheatManager).Name;
                GameObject manager = GameObject.Find(className);
                instance = manager.GetComponent<CheatManager>();

                if (instance == null)
                    instance = new CheatManager();
            }

            return instance;
        }
    }

    private CheatManager()
    {
    }
    //=================================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //=================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    public enum CheatMenu
    {
        Main,
        HousingSimulation,
        StoryQuest,
        ContentsOpen,
        Tutorial,
        Sweeping,
        TestGacha,
    }

    #region Coding rule : Property

    #endregion Coding rule : Property

    #region Coding rule : Value

    [SerializeField] private GameObject profilerPrefab = null;
    [SerializeField] private Text[] fpsTexts = null;
    [SerializeField] private Text[] renderScaleTexts = null;
    [SerializeField] private Text[] timeScaleTexts = null;
    [SerializeField] private GameObject[] showObjects = null;
    [SerializeField] private GameObject extendButton = null;
    [SerializeField] private GameObject hideButton = null;

    [Header("치트용 팝업")]
    [SerializeField] // Cheat의 경우 다른 Popup처럼 관리될 필요가 없음
    private CheatPopup cheatPopup = null;

    [Header("스위핑 치트 팝업")]
    [SerializeField]
    private GameObject sweepingCheatPopUp = null;
    [SerializeField]
    private InputField chapterInputField = null;
    [SerializeField]
    private InputField stageInputField = null;
    [SerializeField]
    private Toggle isHardModeToggle = null;


    [Header("던전마스터 진행도 팝업")]
    [SerializeField]
    private GameObject dungeonMasterPopUp = null;
    [SerializeField]
    private Text dungeonMasterProgress = null;
    private CheatPopupModel cheatPopupModel;


    [Header("메뉴")]
    [SerializeField]
    private GameObject[] cheatMenu;
    private int frameCount;
    private float prevTime;
    private float fps;
    private GameObject profiler = null;
    private float deltaTime = 0.0f;
    private WaitForSecondsRealtime cachedSeconds = new WaitForSecondsRealtime(0.5f);
    private Image extendButtonImage = null;
#if UNITY_ANDROID
    private IAdaptivePerformance adaptivePerformance = null;
#endif

    private CheatMenu CurrentCheatMenu = CheatMenu.Main;


    // private ProfilerRecorder verticesRecorder;
    // private ProfilerRecorder trianglesRecorder;
    // private ProfilerRecorder drawCallsRecorder;
    // private ProfilerRecorder setPassCallsRecorder;
    // private ProfilerRecorder systemMemoryRecorder;
    // private ProfilerRecorder totalMemoryRecorder;
    // private ProfilerRecorder gcMemoryRecorder;
    // private ProfilerRecorder mainThreadTimeRecorder;
    // private StringBuilder profilingSB = new StringBuilder(500);

    private float[] timeScale = new float[4] { 1f, 2f, 4f, 8f };
    private int timeScaleIndex = 0;
    private Volume[] allVolumes;
    private bool isVolumeOn = true;
    public bool IsEffectRenderOn { get { return isEffectRenderOn; } }
    private bool isEffectRenderOn = true;
    public bool IsEffectLoadOn { get { return isEffectLoadOn; } }
    private bool isEffectLoadOn = true;
    public bool IsEffectActiveOn { get { return isEffectActiveOn; } }
    private bool isEffectActiveOn = true;
    public bool IsEffectNoHitOn { get { return isEffectNoHitOn; } }
    private bool isEffectNoHitOn = true;
    public bool IsEffectAttackOn { get { return isEffectAttackOn; } }
    private bool isEffectAttackOn = true;

    public bool IsEternalLifeOn { get { return isEternalLifeOn; } }
    private bool isEternalLifeOn = false;

    public bool IsTimeScaleOn { get { return isTimeScaleOn; } }
    private bool isTimeScaleOn = true;
    public bool IsEnemyOneOn { get { return isEnemyOneOn; } }
    private bool isEnemyOneOn = false;

    public bool IsHitStopOn { get { return isHitStopOn; } }
    private bool isHitStopOn = true;

    public bool IsFrame60 { get { return isFrame60; } }
    private bool isFrame60 = true;


    public bool IsDebugUIOn { get { return isDebugUIOn; } }
    private bool isDebugUIOn = false;

    public bool IsDamageCheatOn { get { return isDamageCheatOn; } set { isDamageCheatOn = value; } }
    private bool isDamageCheatOn = false;

    public bool IsBattleCharacterDebugInfoOn { get { return isBattleCharacterDebugInfoOn; } set { isBattleCharacterDebugInfoOn = value; } }
    private bool isBattleCharacterDebugInfoOn = false;
    public bool IsBattleCharacterDebugQueueInfoOn { get { return isBattleCharacterDebugQueueInfoOn; } set { isBattleCharacterDebugQueueInfoOn = value; } }
    private bool isBattleCharacterDebugQueueInfoOn = false;

    public bool IsBattleLineDebugInfoOn { get { return isBattleLineDebugInfoOn; } set { isBattleLineDebugInfoOn = value; } }
    private bool isBattleLineDebugInfoOn = false;

    public bool IsCriticalOn { get { return isCriticalOn; } set { isCriticalOn = value; } }
    private bool isCriticalOn = true;

    public bool IsIgnoreDamage { get { return isIgnoreDamage; } set { isIgnoreDamage = value; } }
    private bool isIgnoreDamage = false;
    public bool IsPowerOverwhelming { get { return isPowerOverwhelming; } set { isPowerOverwhelming = value; } }
    private bool isPowerOverwhelming = false;

    public float CheatTimeScale { get { return cheatTimeScale; } set { cheatTimeScale = value; } }
    private float cheatTimeScale = 1;
    public bool IsOnDebugAttackSpeed { get { return isOnDebugAttackSpeed; } set { isOnDebugAttackSpeed = value; } }
    private bool isOnDebugAttackSpeed = true;
    public bool IsOnDebugQueue { get { return isOnDebugQueue; } set { isOnDebugQueue = value; } }
    private bool isOnDebugQueue = true;
    public bool IsOnDebugPosition { get { return isOnDebugPosition; } set { isOnDebugPosition = value; } }
    private bool isOnDebugPosition = true;
    public bool IsOnDebugFormation { get { return isOnDebugFormation; } set { isOnDebugFormation = value; } }
    private bool isOnDebugFormation = true;
    public bool IsOnDebugToken { get { return isOnDebugToken; } set { isOnDebugToken = value; } }
    private bool isOnDebugToken = true;

    private float ufeTimeScale;

    private const string OUTLINE_RENDERER_FEATURE_NAME = "Outline Pass";

    #endregion Coding rule : Value

    #region Coding rule : Function

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        showObjects[0].SafeSetActive(false);
        for (int i = 0; i < cheatMenu.Length; i++)
            cheatMenu[i].SafeSetActive((CheatMenu)i == CheatMenu.Main);
        extendButton.SafeSetActive(true);
        hideButton.SafeSetActive(false);
        extendButtonImage = extendButton.GetComponent<Image>();
#if UNITY_ANDROID
        adaptivePerformance = Holder.Instance;
#endif

        StartCoroutine(ShowFpsCoroutine());
        StartCoroutine(ShowBattleInfoCoroutine());
        InitializePillarBoxBorderOnOff();

        frameCount = 0;
        prevTime = 0.0f;

        cheatPopupModel = new CheatPopupModel();
        cheatPopup.SetModel(cheatPopupModel);
    }

    // kosuchoi - outline치트 RuntimeInspector로 변경. 나중에 살릴예정

    // private void Start()
    // {
    //     var urpAssetData = GetRendererData(0);
    //
    //     if (urpAssetData != null)
    //     {
    //         var outlineRendererFeature = FindRendererFeatureByName(urpAssetData, OUTLINE_RENDERER_FEATURE_NAME);
    //         outlineOnOff.SafeSetActive(outlineRendererFeature != null && !outlineRendererFeature.isActive);
    //     }
    // }

    // private void OnEnable()
    // {
    //     verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
    //     trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
    //     drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
    //     setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
    //
    //     systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
    //     totalMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
    //     gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
    //     mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
    //
    //     OnEnableLogWriter();
    // }

    private void Update()
    {
#if UNITY_EDITOR
        // 에디터일 때는 F6 키로 프로파일러를 켜준다.
        // 로그뷰어는 자체적으로 ` 키로 켜주도록 되어 있다.
        if (Input.GetKeyDown(KeyCode.F6))
        {
            if (profiler == null)
            {
                profiler = Instantiate(profilerPrefab, transform);
                profiler.SafeSetActive(true);
            }
            else
            {
                profiler.SafeSetActive(!profiler.activeSelf);
            }
        }

#else
        if (Input.touchCount == 5)
        {
            if (profiler == null)
            {
                profiler = Instantiate(profilerPrefab, transform);
                profiler.SafeSetActive(true);
            }
            else
            {
                profiler.SafeSetActive(!profiler.activeSelf);
            }
        }
#endif
        string currentRenderPipeLine = QualitySettings.renderPipeline.name;


        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        frameCount++;
        float time = Time.realtimeSinceStartup - prevTime;

        if (time >= 0.5f)
        {
            fps = frameCount / time;

            frameCount = 0;
            prevTime = Time.realtimeSinceStartup;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Delete All EditorPrefs")]
    private void DeleteAllEditorPrefs()
    {
        UnityEditor.EditorPrefs.DeleteAll();
        GameManager.Instance.ReturnToLogo();
    }
#endif

    private IEnumerator ShowFpsCoroutine()
    {
        while (true)
        {
            ShowFps();
            ShowRenderScale();
            ShowTimeScale();
            //ShowProfiling();
            ShowThermalStatus();
            yield return cachedSeconds;
        }
    }

    private IEnumerator ShowBattleInfoCoroutine()
    {
        while (true)
        {
            ShowBattleInfo();

            yield return new WaitForEndOfFrame();
        }
    }

    private void ShowBattleInfo()
    {
        if (SceneManager.GetActiveScene().name != "BattlePrototype")
        {
            ufeTimeScale = 0f;
            return;
        }

        if (UFE.Instance == null)
        {
            ufeTimeScale = 0f;
            return;
        }


        if (UFE.Instance.P1ControlsScript == null)
        {
            ufeTimeScale = 0f;
            return;
        }

        FPLibrary.Fix64 enemyGravity = UFE.Instance.P1ControlsScript.Target == null
            ? 0f
            : UFE.Instance.P1ControlsScript.Target.Physics.AppliedGravity;

        ufeTimeScale = UFE.Instance.TimeScale.AsFloat();
    }

    private void ShowFps()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == false)
            return;
#endif
        if (fpsTexts == null)
            return;

        for (int i = 0; i < fpsTexts.Length; i++)
            fpsTexts[i].SafeSetText(fps.ToString("f1") + " fps");
    }

    private void ShowRenderScale()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == false)
            return;
#endif
        if (renderScaleTexts == null)
            return;

        string text = DeviceRenderQuality.CurrRenderScale.ToString("f2");
        for (int i = 0; i < renderScaleTexts.Length; i++)
            renderScaleTexts[i].SafeSetText(text);
    }

    private void ShowTimeScale()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == false)
            return;
#endif
        if (timeScaleTexts == null)
            return;


        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene != null && (activeScene.name == "BattlePrototype" || activeScene.name == "Battle"))
        {
            for (int i = 0; i < timeScaleTexts.Length; i++)
                timeScaleTexts[i].SafeSetText($"x{ufeTimeScale.ToString("f2")}");
        }
        else
        {
            for (int i = 0; i < timeScaleTexts.Length; i++)
                timeScaleTexts[i].SafeSetText($"x{Time.timeScale.ToString("f2")}");
        }

    }

    private void ShowThermalStatus()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying == false)
            return;
#endif

#if UNITY_IOS
        {
            Color color;
            switch (iOSNative.ProcessInfoBridge.ThermalState)
            {
                case iOSNative.ThermalState.Nominal:
                    color = new Color(0.75f, 1.0f, 0.75f);
                    break;

                case iOSNative.ThermalState.Fair:
                    color = new Color(1.0f, 1.0f, 0.75f);
                    break;

                case iOSNative.ThermalState.Serious:
                case iOSNative.ThermalState.Critical:
                    color = new Color(1.0f, 0.75f, 0.75f);
                    break;

                default:
                    color = Color.white;
                    break;
            }

            extendButtonImage.color = color;
        }
#endif

#if UNITY_ANDROID
        if (adaptivePerformance != null && adaptivePerformance.ThermalStatus != null)
        {
            Color color;
            switch (adaptivePerformance.ThermalStatus.ThermalMetrics.WarningLevel)
            {
                case WarningLevel.NoWarning:
                    color = new Color(0.75f, 1.0f, 0.75f);
                    break;

                case WarningLevel.ThrottlingImminent:
                    color = new Color(1.0f, 1.0f, 0.75f);
                    break;

                case WarningLevel.Throttling:
                    color = new Color(1.0f, 0.75f, 0.75f);
                    break;

                default:
                    color = Color.white;
                    break;
            }

            extendButtonImage.color = color;
        }
#endif
    }

    // private void OnDisable()
    // {
    //     totalMemoryRecorder.Dispose();
    //     systemMemoryRecorder.Dispose();
    //     gcMemoryRecorder.Dispose();
    //     mainThreadTimeRecorder.Dispose();
    //
    //     setPassCallsRecorder.Dispose();
    //     drawCallsRecorder.Dispose();
    //     verticesRecorder.Dispose();
    //     trianglesRecorder.Dispose();
    //
    //     OnDisableLogWriter();
    // }

    // private void ShowProfiling()
    // {
    //     profilingSB.Clear();
    //
    //     if (setPassCallsRecorder.Valid)
    //         profilingSB.AppendLine(
    //             $"SetPass Calls: {UtilModel.String.GetLargeNumberText(setPassCallsRecorder.LastValue)}");
    //
    //     if (drawCallsRecorder.Valid)
    //         profilingSB.AppendLine($"Draw Calls: {UtilModel.String.GetLargeNumberText(drawCallsRecorder.LastValue)}");
    //
    //     if (verticesRecorder.Valid)
    //         profilingSB.AppendLine($"Vertices: {UtilModel.String.GetLargeNumberText(verticesRecorder.LastValue)}");
    //
    //     if (trianglesRecorder.Valid)
    //         profilingSB.AppendLine($"Triangles: {UtilModel.String.GetLargeNumberText(trianglesRecorder.LastValue)}");
    //
    //     if (gcMemoryRecorder.Valid)
    //         profilingSB.AppendLine($"GC: {gcMemoryRecorder.LastValue / (1024f * 1024f)} MB");
    //
    //     if (systemMemoryRecorder.Valid)
    //         profilingSB.AppendLine($"System: {systemMemoryRecorder.LastValue / (1024f * 1024f)} MB");
    //
    //     profilingSB.AppendLine($"Texture: {Texture.currentTextureMemory / 1024f / 1024f} MB");
    //
    //     profilingSB.AppendLine($"GPU Memory: {SystemInfo.graphicsMemorySize / 1024f / 1024f} MB");
    //     profilingSB.AppendLine($"GPU Allocated Memory: {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1024f / 1024f} MB");
    //
    //
    //
    //     //if (totalMemoryRecorder.Valid)
    //     //    profilingSB.AppendLine($"Total: {totalMemoryRecorder.LastValue / (1024f * 1024f)} MB");
    // }

    #region OnClick Cheat

    private void Extend()
    {
        hideButton.SafeSetActive(true);
        extendButton.SafeSetActive(false);

        foreach (var obj in showObjects)
            obj.SafeSetActive(true);
    }

    private void Hide()
    {
        hideButton.SafeSetActive(false);
        extendButton.SafeSetActive(true);

        foreach (var obj in showObjects)
            obj.SafeSetActive(false);
    }

    private void RoundVitory()
    {
        Vitory(false);
    }

    private void MatchVitory()
    {
        Vitory(true);
    }

    private void Vitory(bool isMatchEnd = false)
    {
        if (UFE.Instance != null && UFE.Instance.GameRunning)
            UFE.Instance.EndMatchByCheat(true, isMatchEnd).Forget();
    }

    private void RoundDefeat()
    {
        Defeat(false);
    }

    private void MatchDefeat()
    {
        Defeat(true);
    }

    private void Defeat(bool isMatchEnd = false)
    {
        if (UFE.Instance != null && UFE.Instance.GameRunning)
            UFE.Instance.EndMatchByCheat(false, isMatchEnd).Forget();
    }

    private void ItemSurrencyCreate()
    {
        if (!showObjects[0].activeSelf)
        {
            Extend();
        }

        if (!cheatPopup.isShow())
        {
            cheatPopupModel.SetTitleName("재화 전체 생성").SetCallBack((callBackValue) =>
            {
                int itemCount = int.Parse(callBackValue);
                ItemSurrencyCreate(itemCount);
            });

            cheatPopup.Show();
        }
        else
        {
            cheatPopup.Hide();
        }
    }

    private async Task ItemSurrencyCreate(int count)
    {
        BaseProcess clearProcess = NetworkManager.Web.GetProcess(WebProcess.ItemCurrencyCreate);
        clearProcess.SetPacket(new CreateCurrencyInDto(PlayerManager.Instance.MyPlayer.User.UserId, count));

        if (await clearProcess.OnNetworkAsyncRequest())
        {
            clearProcess.OnNetworkResponse();
        }
    }

    private void CharacterAllMaxLevel()
    {
        CharactersAllMaxLevel();
    }

    private async Task CharactersAllMaxLevel()
    {
        BaseProcess clearProcess = NetworkManager.Web.GetProcess(WebProcess.CharactersAllMaxLevel);
        clearProcess.SetPacket(new BaseAdminDto(PlayerManager.Instance.MyPlayer.User.UserId));

        if (await clearProcess.OnNetworkAsyncRequest())
        {
            CharactersAllMaxLevelResponse response = clearProcess.GetResponse<CharactersAllMaxLevelResponse>();

            clearProcess.OnNetworkResponse();
        }
    }

    // private void NextBattleTutorial()
    // {
    //     BattleTutorialManager.Instance.TutorialController.Command_TutorialDequeue();
    // }

    // private void EndBattleTutorial()
    // {
    //     BattleTutorialManager.Instance.TutorialController.Command_BattleTutorialEnd();
    // }

    private void GetAllItems()
    {
        if (!showObjects[0].activeSelf)
        {
            Extend();
        }

        if (!cheatPopup.isShow())
        {
            cheatPopupModel.SetTitleName("아이템 전체 생성").SetCallBack((callBackValue) =>
            {
                int itemCount = int.Parse(callBackValue);
                AllItem(itemCount);
            });

            cheatPopup.Show();
        }
        else
        {
            cheatPopup.Hide();
        }
    }

    public async Task AllItem(int itemCount)
    {
        BaseProcess clearProcess = NetworkManager.Web.GetProcess(WebProcess.ItemsAll);
        clearProcess.SetPacket(new CreateAllItemsDto(PlayerManager.Instance.MyPlayer.User.UserId, itemCount));

        if (await clearProcess.OnNetworkAsyncRequest())
        {
            ItemsAllResponse response = clearProcess.GetResponse<ItemsAllResponse>();

            GoodsGeneratorModel goodsGeneratorModel = new GoodsGeneratorModel(PlayerManager.Instance.MyPlayer.User);

            List<Item> ticket = goodsGeneratorModel.CreateItemByItemDto(response.data);

            var itemModel = PlayerManager.Instance.MyPlayer.User.ItemModel;
            int ticketCount = ticket.Count;

            for (int i = 0; i < ticketCount; ++i)
            {
                itemModel.ChangeGoods(ticket[i]);
            }

            clearProcess.OnNetworkResponse(); //이전 User정보를 활용해 획득한 리턴값 goods를 생성해야 해서 goods가 생성된 이후 NetworkResponse 호출
        }
    }

    public void OnClickClose()
    {
        extendButton.SafeSetActive(false);
        hideButton.SafeSetActive(false);

        foreach (var obj in showObjects)
            obj.SafeSetActive(false);
    }

    /// <summary>
    /// 유니티 기본 사용
    /// </summary>

#if UNITY_EDITOR
    [UnityEditor.MenuItem("4GROUND9/Find Volume", false, 9998)]
#endif
    public static void FindVolume()
    {
#if UNITY_EDITOR
        System.Type type = typeof(Volume);

        List<GameObject> foundObjects = new List<GameObject>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();

                foreach (var rootObject in rootObjects)
                {
                    if (!rootObject.activeInHierarchy)
                        continue;

                    Component[] components = rootObject.GetComponentsInChildren(type, true);

                    foreach (var component in components)
                    {
                        if (!component.gameObject.activeInHierarchy)
                            continue;

                        foundObjects.Add(component.gameObject);
                    }
                }
            }
        }

        if (foundObjects.Count > 0)
            UnityEditor.Selection.objects = foundObjects.ToArray();
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("4GROUND9/Find RenderPipelineAsset", false, 9999)]
#endif
    private static void FocusRenderPipelineAsset()
    {
#if UNITY_EDITOR
        string assetName = QualitySettings.renderPipeline.name;

        string[] assetGUIDs = UnityEditor.AssetDatabase.FindAssets(assetName);

        if (assetGUIDs.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);

            Object asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(path);

            if (asset != null)
            {
                UnityEditor.EditorGUIUtility.PingObject(asset);
                UnityEditor.Selection.activeObject = asset;
            }
            else
            {
                IronJade.Debug.Log("RenderPipelineAsset not found at path: " + path);
            }
        }
        else
        {
            IronJade.Debug.Log("No RenderPipelineAsset found with name: " + assetName);
        }
#endif
    }
    private void InitializePillarBoxBorderOnOff()
    {
        //#if UNITY_EDITOR
        //        bool isOn = UnityEditor.EditorPrefs.GetBool(StringDefine.KEY_PLAYER_PREFS_CHEAT_PILLARBOX_BORDER_ON_OFF);
        //        pillarBoxBorderOnOff.SafeSetText(isOn ? "PillarBox\nBorderOn" : "PillarBox\nBorderOff");
        //        pillarBoxBorderOnOff.SafeSetActive(isOn);
        //#endif
    }

    public ScriptableRendererData GetRendererData(int rendererIndex = 0)
    {
        // 현재 사용 중인 Render Pipeline Asset을 가져옵니다.
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urpAsset == null)
        {
            IronJade.Debug.LogError("현재 Universal Render Pipeline Asset이 설정되지 않았습니다.");
            return null;
        }

        FieldInfo propertyInfo = urpAsset.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        ScriptableRendererData[] rendererDatas = (ScriptableRendererData[])propertyInfo.GetValue(urpAsset);
        if (rendererDatas == null || rendererDatas.Length <= 0) return null;
        if (rendererIndex < 0 || rendererDatas.Length <= rendererIndex) return null;

        return rendererDatas[rendererIndex];
    }

    public void BattleUI()
    {
        var ui = BattleSceneManager.Instance.transform.GetChild(6);
        ui.SafeSetActive(!ui.gameObject.activeSelf);

    }

    public void OnClickFindVolume2()
    {
        allVolumes = FindObjectsOfType<Volume>();
    }

    public void SetPostProcessingEnabled(bool enable)
    {
        allVolumes = FindObjectsOfType<Volume>();

        // 모든 Volume의 enabled 상태를 설정
        foreach (Volume volume in allVolumes)
        {
            volume.enabled = enable;
        }
    }

    // 전투용.
    public void OnClickEnemyOneOnOff()
    {
        isEnemyOneOn = !isEnemyOneOn;
    }

    public void OnClickHitStopOnOff()
    {
        isHitStopOn = !isHitStopOn;
    }

    public void OnClickCriticalOnOff()
    {
        isCriticalOn = !isCriticalOn;
    }

    public void OnClickIgnoreDamageOnOff()
    {
        isIgnoreDamage = !isIgnoreDamage;
    }
    public void OnClickPowerOverwhelmingOnOff()
    {
        isPowerOverwhelming = !isPowerOverwhelming;
    }

    public void OnClickFrameChange()
    {
        isFrame60 = !isFrame60;
        SetFrameRate(isFrame60);
    }

    public void CheckNowFrameRate()
    {
        if (Application.targetFrameRate == 60)
        {
            isFrame60 = true;
        }
        else
        {
            isFrame60 = false;
        }
    }

    private void SetFrameRate(bool is60Frame)
    {
        IronJade.Debug.Log($"[CheatManager SetFrameRate] 1 Application.targetFrameRate = {Application.targetFrameRate} / is60Frame = {is60Frame}");
        if (is60Frame)
        {
            //   QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            BattleHelper.SetUseTargetFrame60(true);
        }
        else
        {
            //     QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

            BattleHelper.SetUseTargetFrame60(false);
        }

        IronJade.Debug.Log($"[CheatManager SetFrameRate] 2 Application.targetFrameRate = {Application.targetFrameRate} / is60Frame = {is60Frame}");
    }

    public void ReSelectServer()
    {
        PlayerPrefsWrapper.DeleteKey(StringDefine.KEY_PLAYER_PREFS_SELECT_SERVER);

        if (!string.IsNullOrEmpty(Config.Device.Nid))
        {
            string lastLoginRegionKey = string.Format(StringDefine.KEY_PLAYER_PREFS_LAST_LOGIN_REGION, Config.Device.Nid);
            PlayerPrefsWrapper.DeleteKey(lastLoginRegionKey);
        }

        Hide();
        GameManager.Instance.ReturnToLogo();
    }

    public void ResetDailyAnimation()
    {
        PlayerPrefsWrapper.DeleteKey(StringDefine.KEY_PLAYER_PREFS_PLAY_NETMINING_ANIMATION_DATE);
        PlayerPrefsWrapper.DeleteKey(StringDefine.KEY_PLAYER_PREFS_PLAY_STAGEDUNGEON_ANIMATION_DATE);
    }

    #endregion

    #region ContentsOpen
    #endregion ContentsOpen
    public void OnClickResetUser()
    {
        ResetUser().Forget();
    }

    public void OnClickDebugUI()
    {
        isDebugUIOn = !isDebugUIOn;
    }

    public void OnClickDeleteCache()
    {
#if UNITY_EDITOR
        DeleteAllEditorPrefs();
#endif
    }

    private async UniTask ResetUser()
    {
        try
        {
            BaseProcess resetProcess = NetworkManager.Web.GetProcess<ResetProcess>();
            resetProcess.SetRetry(false);

            // 계정없어도 쏜다.
            if (await resetProcess.OnNetworkAsyncRequestForce())
            {
                resetProcess.OnNetworkResponse();

                await TransitionManager.In(TransitionType.Rotation);

                TownObjectManager.Instance.CancelDecoratorFactory();

                PrologueManager.Instance.ResetPrologue(true);
                PrologueManager.Instance.ResetAllIndex();

                if (PlayerManager.Instance.MyPlayer != null)
                    await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(false);
            }
        }
        catch (System.Exception e)
        {
            IronJade.Debug.Log(e);
            throw;
        }
        finally
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private async void ChainDungeonSelect()
    {
        var controller = UIManager.Instance.GetController(UIType.GarenaBuildBattleResultPopup);
        var model = controller.GetModel<GarenaBuildBattleResultPopupModel>();
        await UIManager.Instance.EnterAsync(controller);
    }

    public void OnClickCutsceneInteractionSuccessMode(bool value)
    {
        if (CutsceneManager.Instance.IsPlaying)
            CutsceneManager.Instance.IsInteractAlwaysSuccess = value;
    }

    #region HousingSimulation

    [Space(5)]
    [Header("플러피 타운")]
    [SerializeField]
    private TMP_Dropdown housingSpawnDataIds;
    [SerializeField]
    private InputField housingCurrencyInput;

    private System.Array housingAvatarDefines = null;
    private int housingSpawnDataId;

    public void HousingGroup()
    {
        if (!showObjects[0].activeSelf)
            Extend();

        ChangeMenu(CheatMenu.HousingSimulation);
    }

    private void InitializeHousingButtons()
    {
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

        if (housingAvatarDefines == null)
            housingAvatarDefines = System.Enum.GetValues(typeof(HousingAvatarDefine));

        for (int i = 0; i < housingAvatarDefines.Length; i++)
        {
            HousingAvatarDefine define = (HousingAvatarDefine)housingAvatarDefines.GetValue(i);

            options.Add(new TMP_Dropdown.OptionData(define.ToString()));
        }

        housingSpawnDataIds.options = options;
    }

    public void OnValueChangedHousingAvatarDataId(int index)
    {
        if (housingAvatarDefines == null)
            return;

        if (housingAvatarDefines.Length <= index)
            return;

        housingSpawnDataId = (int)housingAvatarDefines.GetValue(index);
    }

    public void OnClickHousingActorSpawn()
    {
        ObserverManager.NotifyObserver(HousingObserverID.CheatSpawnActor, new IntParam(housingSpawnDataId));

        housingSpawnDataId = 0;
        housingSpawnDataIds.value = 0;
    }

    public async void OnClickHousingAddCurrency()
    {
        int addCurrency = int.Parse(housingCurrencyInput.text);
        int userId = PlayerManager.Instance.MyPlayer.User.UserId;

        BaseProcess housingCoinAddProcess = NetworkManager.Web.GetProcess(WebProcess.HousingCoinAdd);
        housingCoinAddProcess.SetPacket(new CheatHousingInDto(userId, addCurrency));

        if (await housingCoinAddProcess.OnNetworkAsyncRequest())
        {
            housingCoinAddProcess.OnNetworkResponse();

            if (UIManager.Instance.CheckOpenUI(UIType.HousingSimulationView))
                ObserverManager.NotifyObserver(HousingObserverID.ProductUpdate, null);

            ChangeMenu(CheatMenu.Main);

            return;
        }
    }

    public async void OnClickHousingReset()
    {
        int userId = PlayerManager.Instance.MyPlayer.User.UserId;

        BaseProcess housingResetProcess = NetworkManager.Web.GetProcess(WebProcess.HousingReset);
        housingResetProcess.SetPacket(new BaseAdminDto(userId));

        if (await housingResetProcess.OnNetworkAsyncRequest())
        {
            if (UIManager.Instance.CheckOpenUI(UIType.HousingSimulationView))
            {
                var housingSimulationVIew = UIManager.Instance.GetController(UIType.HousingSimulationView);

                housingSimulationVIew.Exit().Forget();
            }

#if UNITY_EDITOR
            if (GameManager.Instance.IsTestMode)
            {
                UnityEditor.EditorApplication.isPlaying = false;
                Application.Quit();
            }
            else
#endif
            {
                ChangeMenu(CheatMenu.Main);
            }
        }
    }

    #endregion

    #region StoryQuest

    [Space(5)]
    [Header("퀘스트")]
    [SerializeField]
    private ScrollRect questScroll;
    [SerializeField]
    private ToggleGroup questButtonToggleGroup;

    private bool isInitializedQuestButton = false;
    private int selectedQuestDataId = 0;

    public void QuestGroup()
    {
        if (!showObjects[0].activeSelf)
            Extend();

        ChangeMenu(CheatMenu.StoryQuest);
    }

    private void InitializeQuestButtons()
    {
        if (questScroll == null)
            return;

        RectTransform questContent = questScroll.content;

        if (questContent.childCount == 0)
        {
            IronJade.Debug.LogError("퀘스트 버튼 오브젝트가 없습니다.");
            return;
        }

        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
        CheatStoryQuestSelectSupport baseQuestButton = questContent.GetChild(0).GetComponent<CheatStoryQuestSelectSupport>();

        if (!isInitializedQuestButton)
        {
            // 진행 가능한 퀘스트 리스트 생성
            for (int i = 0; i < storyQuestTable.GetDataTotalCount(); i++)
            {
                StoryQuestTableData storyQuestTableData = storyQuestTable.GetDataByIndex(i);
                EpisodeGroupTable episodeGroupTable = TableManager.Instance.GetTable<EpisodeGroupTable>();
                EpisodeGroupTableData episodeGroupTableData = episodeGroupTable.GetDataByID(storyQuestTableData.GetEPISODE());
                int dataId = storyQuestTableData.GetID();
                string groupName = TableManager.Instance.GetLocalization(episodeGroupTableData.GetNAME_EPISODE_GROUP());
                string questName = TableManager.Instance.GetLocalization(episodeGroupTableData.GetNAME_EPISODE_TITLE());
                string text = $"({storyQuestTableData.GetENUM_ID()})\n{groupName}\n{questName}";
                CheatStoryQuestSelectSupport questButton = null;

                if (i >= questContent.childCount)
                    questButton = UtilModel.Resources.Instantiate(baseQuestButton, questContent);
                else
                    questButton = questContent.GetChild(i).GetComponent<CheatStoryQuestSelectSupport>();

                questButton.SafeSetActive(true);
                questButton.Set(questButtonToggleGroup, dataId, text);
                questButton.SetEventSelect((value) =>
                {
                    selectedQuestDataId = value;
                });
            }

            isInitializedQuestButton = true;
        }

        // 진행 중인 퀘스트 선택 및 스크롤 포커싱
        LayoutRebuilder.ForceRebuildLayoutImmediate(questContent);

        float focusOffset = (questScroll.transform as RectTransform).rect.height * 0.5f;
        float focusHeight = 0f;
        int progressingQuestIndex = 0;
        StoryQuestProgressModel progressModel = MissionManager.Instance.GetProgressModel<StoryQuestProgressModel>();
        BaseMission quest = progressModel.GetTrackingMission<BaseMission>();

        if (quest == null)
        {
            quest = MissionManager.Instance.GetMissions(MissionContentType.StoryQuest)
                .Where(mission => mission.GetMissionProgressState() != MissionProgressState.UnAccepted)
                .FirstOrDefault();
        }

        int compareDataId = quest != null ? quest.DataId : progressModel.LastCompletedQuestDataID;

        if (compareDataId > 0)
        {
            for (int i = 0; i < questContent.childCount; i++)
            {
                CheatStoryQuestSelectSupport questButton = questContent.GetChild(i).GetComponent<CheatStoryQuestSelectSupport>();

                if (questButton.DataId == compareDataId)
                {
                    progressingQuestIndex = i;
                    break;
                }
            }
        }

        if (progressingQuestIndex < questContent.childCount)
        {
            CheatStoryQuestSelectSupport questButton = questContent.GetChild(progressingQuestIndex).GetComponent<CheatStoryQuestSelectSupport>();

            selectedQuestDataId = questButton.DataId;
            questButton.Select();

            focusHeight = -questButton.RectTransform.anchoredPosition.y - focusOffset;
        }

        focusHeight = Mathf.Max(focusHeight, 0);
        focusHeight = Mathf.Min(focusHeight, questContent.rect.height - (focusOffset * 2f));

        questContent.anchoredPosition = new Vector2(questContent.anchoredPosition.x, focusHeight);
    }

    public void OnClickChangeQuest()
    {
        MissionManager.Instance.ChangeStoryQuestDataId_Cheat(selectedQuestDataId).Forget();
    }
    #endregion

    #region ContentsOpen
    [Space(5)]
    [Header("컨텐츠 입장")]
    [SerializeField]
    private Transform contentsOpenButtonParent;
    [SerializeField]
    private GameObject contentsOpenButtonItem;

    private void ContentsOpenGroup()
    {
        if (!showObjects[0].activeSelf)
            Extend();

        ChangeMenu(CheatMenu.ContentsOpen);
    }

    private void InitializeContentsOpenButtons()
    {
        if (contentsOpenButtonParent.childCount > 0)
            return;

        ContentsOpenTable contentsOpenTable = TableManager.Instance.GetTable<ContentsOpenTable>();

        for (int i = 0; i < contentsOpenTable.GetDataTotalCount(); i++)
        {
            ContentsOpenTableData contentsData = contentsOpenTable.GetDataByIndex(i);

            GameObject contentsOpenButton = UtilModel.Resources.Instantiate(contentsOpenButtonItem, contentsOpenButtonParent);
            contentsOpenButton.SafeSetActive(true);

            CheatContentsButtonSupport buttonSupport = contentsOpenButton.GetComponent<CheatContentsButtonSupport>();
            string buttonText = TableManager.Instance.GetLocalization(contentsData.GetNAME());
            System.Action<ContentsType> onEventEnter = (x) =>
            {
                Hide();
                SRDebug.Instance.HideDebugPanel();
                ContentsOpenManager.Instance.OpenContents(x).Forget();
            };
            buttonSupport.Set((ContentsType)contentsData.GetCONTENTS_TYPE(), buttonText, onEventEnter);
        }
    }
    #endregion ContentsOpen

    #region ContentsTutorial
    [Space(5)]
    [Header("튜토리얼")]
    [SerializeField]
    private Transform tutorialButtonParent;
    [SerializeField]
    private GameObject tutorialButtonItem;

    private void TutorialGroup()
    {
        if (!showObjects[0].activeSelf)
            Extend();

        ChangeMenu(CheatMenu.Tutorial);
    }

    private void InitializeTutorialButtons()
    {
        if (tutorialButtonParent.childCount > 0)
            return;

        TutorialTable tutorialTable = TableManager.Instance.GetTable<TutorialTable>();

        for (int i = 0; i < tutorialTable.GetDataTotalCount(); i++)
        {
            TutorialTableData tutorialData = tutorialTable.GetDataByIndex(i);
            if (!TutorialManager.CheckCheatPlayableTutorial((ContentsTutorialView)tutorialData.GetTUTORIAL_VIEW()))
                continue;

            GameObject tutorialButton = UtilModel.Resources.Instantiate(tutorialButtonItem, tutorialButtonParent);
            tutorialButton.SafeSetActive(true);

            CheatTutorialSelectSupport buttonSupport = tutorialButton.GetComponent<CheatTutorialSelectSupport>();
            string buttonText = ((TutorialDefine)tutorialData.GetID()).ToString();
            System.Action<int> onEventEnter = (x) =>
            {
                Hide();
                SRDebug.Instance.HideDebugPanel();
                TutorialManager.Instance.ForcedPlayTutorial(x).Forget();
            };
            buttonSupport.Set(tutorialData.GetID(), buttonText, onEventEnter);
        }
    }
    #endregion ContentsTutorial

    #region SweepingStage
    public async void OnClickUpdateSweepingStageCheat()
    {
        int chapter = int.Parse(chapterInputField.text);
        int stage = int.Parse(stageInputField.text);
        bool isHardMode = isHardModeToggle.isOn;
        StageDungeonTableData[] tableData = TableManager.Instance.GetTable<StageDungeonTable>().FindAll(x => x.GetSTAGE_DUNGEON_CHAPTER() == chapter && x.GetSTAGE_DUNGEON_STAGE() == stage);

        if (tableData == null)
        {
            IronJade.Debug.LogError("UpdateStageDungeonCheat Error: DataId is null");
            return;
        }

        BaseProcess stageDungeonProcess = NetworkManager.Web.GetProcess<UpdateStageDungeonCheatProcess>();
        stageDungeonProcess.SetPacket(new UpdateStageDungeonCheatDto(PlayerManager.Instance.MyPlayer.User.UserId, tableData[isHardMode ? 1 : 0].GetID()));

        if (await stageDungeonProcess.OnNetworkAsyncRequest())
        {
            stageDungeonProcess.OnNetworkResponse();

            BaseProcess stageDungeonGetProcess = NetworkManager.Web.GetProcess<StageDungeonGetProcess>();
            if (await stageDungeonGetProcess.OnNetworkAsyncRequest())
            {
                stageDungeonGetProcess.OnNetworkResponse();
            }

            showObjects[0].SafeSetActive(false);
            ChangeMenu(CheatMenu.Main);

            return;
        }
    }

    #endregion

    #region DungeonMaster
    private void InitializeDungeonMasterPopUp()
    {
        if (dungeonMasterPopUp.activeSelf)
        {
            dungeonMasterPopUp.SafeSetActive(false);
        }
        else
        {
            dungeonMasterPopUp.SafeSetActive(true);
            UpdateDungeonMasterPopup();
        }
    }

    public void UpdateDungeonMasterPopup()
    {
        if (!dungeonMasterPopUp.activeSelf)
            return;

        DungeonMasterTable dungeonMasterTable = TableManager.Instance.GetTable<DungeonMasterTable>();
        var find = dungeonMasterTable.Find(match =>
        {
            for (int i = 0; i < match.GetCHAIN_DUNGEONCount(); ++i)
            {
                if (match.GetCHAIN_DUNGEON(i) == BattleProcessManager.Instance.BattleInfo.DungeonID)
                    return true;
            }

            return false;
        });
        if (find.IsNull())
            dungeonMasterProgress.SafeSetText("DungeonMaster Id is Null");
        else
        {
            int index = -1;

            for (int i = 0; i < find.GetCHAIN_DUNGEONCount(); i++)
            {
                if (find.GetCHAIN_DUNGEON(i) == BattleProcessManager.Instance.BattleInfo.DungeonID)
                    index = i;
            }

            if (index != -1)
                dungeonMasterProgress.SafeSetText($"DungeonMaster: {index + 1}/{find.GetCHAIN_DUNGEONCount()}");
        }
    }
    #endregion

    #region GachaTest
    [Space(5)]
    [Header("체인링크 테스트")]
    [SerializeField]
    private Transform testGachaCharacterParent;
    [SerializeField]
    private Transform testGachaReeltapeParent;
    [SerializeField]
    private GameObject testGachaButton;
    [SerializeField]
    private Text textCurrentGachaList;

    private List<int> testGahcaId;
    private bool testGachaHaveHighTier = false;
    private bool canAddGachaData { get { return testGahcaId.Count < 10; } }
    private void TestGachaGroup()
    {
        if (!showObjects[0].activeSelf)
            Extend();

        ChangeMenu(CheatMenu.TestGacha);
    }

    private void InitializeTestGachaButton()
    {
        if (testGahcaId == null)
            testGahcaId = new List<int>();

        testGahcaId.Clear();

        testGachaHaveHighTier = false;

        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        ReeltapeTable reeltapeTable = TableManager.Instance.GetTable<ReeltapeTable>();

        if (testGachaCharacterParent.childCount == 0)
        {
            for (int i = 0; i < characterTable.GetDataTotalCount(); i++)
            {
                CharacterTableData characterData = characterTable.GetDataByIndex(i);
                if (characterData.GetCHARACTER_TYPE() != (int)CharacterType.PlayerCharacter)
                    continue;

                GameObject characterButton = UtilModel.Resources.Instantiate(testGachaButton, testGachaCharacterParent);
                characterButton.SafeSetActive(true);

                CheatTestGachaButtonSupport buttonSupport = characterButton.GetComponent<CheatTestGachaButtonSupport>();
                string buttonText = TableManager.Instance.GetLocalization(characterData.GetNAME());
                System.Action<int> onEventAddCharacter = (id) =>
                {
                    if (canAddGachaData)
                    {
                        if (!testGachaHaveHighTier)
                            testGachaHaveHighTier = characterData.GetTIER() >= (int)CharacterTier.X;
                        testGahcaId.Add(id);
                        UpdateCurrentGachaText();
                    }
                };
                buttonSupport.Set(characterData.GetID(), characterData.GetTIER(), buttonText, onEventAddCharacter);
            }
        }

        if (testGachaReeltapeParent.childCount == 0)
        {
            for (int i = 0; i < reeltapeTable.GetDataTotalCount(); i++)
            {
                ReeltapeTableData reeltapeData = reeltapeTable.GetDataByIndex(i);

                GameObject reeltapeButton = UtilModel.Resources.Instantiate(testGachaButton, testGachaReeltapeParent);
                reeltapeButton.SafeSetActive(true);

                CheatTestGachaButtonSupport buttonSupport = reeltapeButton.GetComponent<CheatTestGachaButtonSupport>();
                string buttonText = TableManager.Instance.GetLocalization(reeltapeData.GetNAME());
                System.Action<int> onEventAddCharacter = (id) =>
                {
                    if (canAddGachaData)
                    {
                        if (!testGachaHaveHighTier)
                            testGachaHaveHighTier = reeltapeData.GetTIER() >= (int)CharacterTier.X;
                        testGahcaId.Add(id);
                        UpdateCurrentGachaText();
                    }
                };
                buttonSupport.Set(reeltapeData.GetID(), reeltapeData.GetTIER(), buttonText, onEventAddCharacter);
            }
        }

        UpdateCurrentGachaText();
    }

    private void UpdateCurrentGachaText()
    {
        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        ReeltapeTable reeltapeTable = TableManager.Instance.GetTable<ReeltapeTable>();

        StringBuilder sb = new StringBuilder();
        sb.Append("Current List : ");

        for (int i = 0; i < testGahcaId.Count; i++)
        {
            CharacterTableData characterData = characterTable.GetDataByID(testGahcaId[i]);
            ReeltapeTableData reeltapeData = reeltapeTable.GetDataByID(testGahcaId[i]);
            if (!characterData.IsNull())
                sb.Append(TableManager.Instance.GetLocalization(characterData.GetNAME()) + " ");
            if (!reeltapeData.IsNull())
                sb.Append(TableManager.Instance.GetLocalization(reeltapeData.GetNAME()) + " ");
        }

        if (!testGachaHaveHighTier)
            sb.Append("[X티어 이상 캐릭터/릴테이프를 반드시 포함시켜주세요.]");

        textCurrentGachaList.SafeSetText(sb.ToString());
    }

    public void OnEventTaskTestGacha()
    {
        if (!testGachaHaveHighTier)
            return;

        Hide();
        SRDebug.Instance.HideDebugPanel();
        TestGachaAsync().Forget();
    }

    public void OnEventResetTestGacha()
    {
        testGahcaId.Clear();
        testGachaHaveHighTier = false;
        UpdateCurrentGachaText();
    }

    private async UniTask TestGachaAsync()
    {
        await UIManager.Instance.BackToTarget(UIType.LobbyView);
        await UIManager.Instance.EnterAsync(UIType.ChainLinkView);

        ChainLinkController controller = UIManager.Instance.GetController(UIType.ChainLinkView) as ChainLinkController;
        controller.OnEventCheatGachaTest(testGahcaId);

    }
    #endregion GachaTest

    private void ChangeMenu(CheatMenu targetMenu)
    {
        CurrentCheatMenu = targetMenu;

        for (int i = 0; i < cheatMenu.Length; i++)
            cheatMenu[i].SafeSetActive(CurrentCheatMenu == (CheatMenu)i);

        OnSelectMenu(targetMenu);
    }

    private void OnSelectMenu(CheatMenu menu)
    {
        switch (menu)
        {
            case CheatMenu.StoryQuest:
                {
                    InitializeQuestButtons();
                }
                break;

            case CheatMenu.ContentsOpen:
                {
                    InitializeContentsOpenButtons();
                }
                break;

            case CheatMenu.Tutorial:
                {
                    InitializeTutorialButtons();
                }
                break;

            case CheatMenu.HousingSimulation:
                {
                    InitializeHousingButtons();
                }
                break;

            case CheatMenu.TestGacha:
                {
                    InitializeTestGachaButton();
                }
                break;
        }
    }

    [ContextMenu("TEST")]
    public void Test()
    {
        // Texture 메모리 사용량 계산
        Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>();
        long totalTextureMemory = 0;
        foreach (var texture in textures)
        {
            long textureMemory = Profiler.GetRuntimeMemorySizeLong(texture);
            //Debug.Log($"Texture: {texture.name}, Memory: {textureMemory / 1024f / 1024f} MB");
            totalTextureMemory += textureMemory;
        }

        IronJade.Debug.Log($"Total Texture Memory: {totalTextureMemory / 1024f / 1024f} MB");

        // Mesh 메모리 사용량 계산
        Mesh[] meshes = Resources.FindObjectsOfTypeAll<Mesh>();
        long totalMeshMemory = 0;
        foreach (var mesh in meshes)
        {
            long meshMemory = Profiler.GetRuntimeMemorySizeLong(mesh);
            //Debug.Log($"Mesh: {mesh.name}, Memory: {meshMemory / 1024f / 1024f} MB");
            totalMeshMemory += meshMemory;
        }

        IronJade.Debug.Log($"Total Mesh Memory: {totalMeshMemory / 1024f / 1024f} MB");

        // AudioClip 메모리 사용량 계산
        AudioClip[] audioClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        long totalAudioMemory = 0;
        foreach (var audioClip in audioClips)
        {
            long audioMemory = Profiler.GetRuntimeMemorySizeLong(audioClip);
            //Debug.Log($"AudioClip: {audioClip.name}, Memory: {audioMemory / 1024f / 1024f} MB");
            totalAudioMemory += audioMemory;
        }

        IronJade.Debug.Log($"Total AudioClip Memory: {totalAudioMemory / 1024f / 1024f} MB");

        // GameObject 메모리 사용량 계산
        GameObject[] gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        long totalGObjMemory = 0;
        foreach (var aobj in gameObjects)
        {
            long audioMemory = Profiler.GetRuntimeMemorySizeLong(aobj);
            //Debug.Log($"AudioClip: {audioClip.name}, Memory: {audioMemory / 1024f / 1024f} MB");
            totalGObjMemory += audioMemory;
        }

        IronJade.Debug.Log($"Total AudioClip Memory: {totalGObjMemory / 1024f / 1024f} MB");

        IronJade.Debug.Log(
             $"Total Memory: {(totalTextureMemory + totalAudioMemory + totalMeshMemory + totalGObjMemory) / 1024f / 1024f} MB");
    }

    private async void SendAllMail()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.SendAllMail);
        process.SetPacket(new BaseAdminDto(PlayerManager.Instance.MyPlayer.User.UserId));

        if (await process.OnNetworkAsyncRequest())
        {
            SendAllMailResponse response = process.GetResponse<SendAllMailResponse>();

            process.OnNetworkResponse();
        }
    }

    #endregion Coding rule : Function



    #region Coding rule : Command Function 

    [Command]
    public void Command_VitoryRound()
    {
        RoundVitory();
    }

    [Command]
    public void Command_VitoryMatch()
    {
        MatchVitory();
    }

    [Command]
    public void Command_DefeatRound()
    {
        RoundDefeat();
    }

    [Command]
    public void Command_DefeatMatch()
    {
        MatchDefeat();
    }

    [Command]
    public void Command_FindVolume()
    {
        FindVolume();
    }

    [Command]
    public void Command_BattleUI()
    {
        BattleUI();
    }

    [Command]
    public void Command_HousingGroup()
    {
        HousingGroup();
    }

    [Command]
    public void Command_TestGacha()
    {
        TestGachaGroup();
    }

    // [Command]
    // public void Command_NextBattleTutorial()
    // {
    //     NextBattleTutorial();
    // }

    // [Command]
    // public void Command_EndBattleTutorial()
    // {
    //     EndBattleTutorial();
    // }

    [Command]
    public void Command_GetAllItems()
    {
        GetAllItems();
    }

    [Command]
    public void Command_CharacterAllMaxLevel()
    {
        CharacterAllMaxLevel();
    }

    [Command]
    public void Command_ItemSurrencyCreate()
    {
        ItemSurrencyCreate();
    }

    [Command]
    public void Command_ReSelectServer()
    {
        ReSelectServer();
    }

    [Command]
    public void Command_ResetDailyAnimation()
    {
        ResetDailyAnimation();
    }

    [Command]
    public void Command_FocusRenderPipelineAsset()
    {
        FocusRenderPipelineAsset();
    }

    [Command]
    public void Command_Hide()
    {
        Hide();
    }

    [Command]
    public void Command_QuestGroup()
    {
        QuestGroup();
    }

    [Command]
    public void Command_ContentsOpenGroup()
    {
        ContentsOpenGroup();
    }

    [Command]
    public void Command_CashShop()
    {
        CashShop();
    }

    private async UniTask CashShop()
    {
        CashShopGetProcess cashShopGetProcess = NetworkManager.Web.GetProcess<CashShopGetProcess>();

        if (await cashShopGetProcess.OnNetworkAsyncRequest())
        {
            cashShopGetProcess.OnNetworkResponse();

            var data = cashShopGetProcess.Response.data;

            BaseController controller = UIManager.Instance.GetController(UIType.CashShopView);
            controller.GetModel<CashShopViewModel>().SetCashShopOutDto(data);
            UIManager.Instance.EnterAsync(controller).Forget();
        }
    }

    [Command]
    public void Command_ActiveFMOD(bool isActive)
    {
        //var runtimeManager = FindObjectOfType<FMODUnity.RuntimeManager>();
        //if (runtimeManager != null)
        {
            try
            {
                FMODUnity.RuntimeManager.IsEnabled = isActive;
            }
            catch
            {

            }
            //runtimeManager.gameObject.SetActive(isActive);
        }

    }

    [Command]
    public void Command_TutorialGroup()
    {
        TutorialGroup();
    }

    [Command]
    public void Command_VSync()
    {
        ToggleVSync();
    }

    [Header("Quality 세팅")]
    public int toggleVSync = 0;

    public void ToggleVSync()
    {
        QualitySettings.vSyncCount = toggleVSync == 1 ? 0 : 1;
        toggleVSync = QualitySettings.vSyncCount;
    }

    [Command]
    public void Command_GroundPassMissionClear()
    {
        EventMissionClear().Forget();
    }

    [Command]
    public void Command_UpdateStageDungeonCheat()
    {
        if (!showObjects[0].activeSelf)
            Extend();

        ChangeMenu(CheatMenu.Sweeping);
    }

    [Command]
    public void Command_ShowDungeonMasterProgress()
    {
        InitializeDungeonMasterPopUp();
    }

    [Command]
    public void Command_SendAllMail()
    {
        SendAllMail();
    }

    public async UniTask EventMissionClear()
    {
        BaseProcess clearProcess = NetworkManager.Web.GetProcess(WebProcess.RewardMission);
        clearProcess.SetPacket(new MissionClearInDto(1346, 22, new string[] { "7100042" }));

        if (await clearProcess.OnNetworkAsyncRequest())
        {
            EventPassMissionClearResponse response = clearProcess.GetResponse<EventPassMissionClearResponse>();
        }
    }

    public void Command_Extend()
    {
        if (!SRDebug.Instance.IsDebugPanelVisible)
            SRDebug.Instance.ShowDebugPanel();
        else
            SRDebug.Instance.HideDebugPanel();

    }

    public void Command_ChainDungeonSelect()
    {
        ChainDungeonSelect();
    }

    #endregion Coding rule : Command Function



    private List<string> logMessages = new List<string>();

    private void OnEnableLogWriter()
    {
        //Application.logMessageReceived += HandleLog;
    }

    private void OnDisableLogWriter()
    {
        //Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (BattleProcessManager.Instance == null || BattleRaidProcessManager.Instance == null)
            return;

        string message = $"[{System.DateTime.Now}] {type}: {logString}";
        logMessages.Add(message);

        if (type == LogType.Exception)
        {
            logMessages.Add($"Stack Trace: {stackTrace}");
        }
    }

    public void Command_SaveLogsToFile()
    {
        try
        {
            string fileName = $"FGN_Battle_Log_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            string logPath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

            System.IO.File.WriteAllLines(logPath, logMessages);

            IronJade.Debug.Log($"Logs saved to: {logPath}");

            // 저장 후 로그 초기화 (선택사항)
            // logMessages.Clear();
        }
        catch (System.Exception e)
        {
            IronJade.Debug.LogError($"Failed to save logs: {e.Message}");
        }
    }

    private static ScriptableRendererFeature FindRendererFeatureByName(ScriptableRendererData urpAsset, string featureName)
    {
        for (int i = 0; i < urpAsset.rendererFeatures.Count; i++)
        {
            if (urpAsset.rendererFeatures[i].name == featureName)
            {
                return urpAsset.rendererFeatures[i];
            }
        }

        return null;
    }
}
#endif