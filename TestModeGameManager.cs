using Cysharp.Threading.Tasks;
using IronJade.Table.Data;
using IronJade.UI.Core;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class TestModeGameManager : GameManager
{
    public static new TestModeGameManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(TestModeGameManager).Name;
                GameObject manager = GameObject.Find(className);

                if (manager == null)
                    return null;

                instance = manager.GetComponent<TestModeGameManager>();
                instance.SafeSetName("TestModeGameManager");
            }


            return instance as TestModeGameManager;
        }
    }

    private TestModeGameManager() { }

    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public TownObjectType StartTownObjectType { get => startTownObjectType; }
    public string StartTownObjectEnumId { get => startTownObjectEnumId; }
    public bool IsOfflineTownTest { get => offlineTownTest; }
    #endregion Coding rule : Property

    #region Coding rule : Value
    [Header("전투 - FMOD 비활성화")]
    [SerializeField]
    private bool disableFMOD = true;

    [Header("서버 로그")]
    [SerializeField]
    private bool isShowNetworkLog = true;

    [Header("퀘스트 테스트 - 진행할 퀘스트 Data ID")]
    [SerializeField]
    private int testDataStoryQuestId = 0;

    [Header("퀘스트 테스트 - 시작 위치")]
    [SerializeField]
    private TownObjectType startTownObjectType = TownObjectType.WarpPoint;

    [SerializeField]
    private string startTownObjectEnumId = StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT;

    [Header("오프라인 마을 테스트")]
    [SerializeField]
    private bool offlineTownTest = false;

    [Header("오프라인 마을 테스트 - 시작캐릭터")]
    [SerializeField]
    private CharacterDefine offlineTownTestStartCharacter;

    [SerializeField]
    private GameObject uiCanvasGroup = null;



#if UNITY_EDITOR
    [SerializeField]
    private TownCharacterChangeSupport townCharacterChangeSupport;

    [SerializeField]
    private TownFreeCameraSupport townFreeCameraSupport;
#endif

    private SettingFrameLimitType testFrameLimitType = SettingFrameLimitType.Fps60;
    //   private SettingQualityType testQualityType = SettingQualityType.Low;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Awake()
    {
#if UNITY_EDITOR
        // 빌드할때는 패스. 빌드시간 단축
        if (UnityEditor.BuildPipeline.isBuildingPlayer)
        {
            return;
        }
#endif        

        
       /* if (!Application.isPlaying)
        {
            // 에디터 상태에서 재정렬 안하기로 함 // by철옥 (PD님과 대화)
            //if (CameraManager.Instance != null)
            //{
            //    CameraManager.Instance.SortingCameraStack();
            //}

            return;
        }
         Domain Reloading 안하게 됬을때 순서성 문제 있어서 주석처리함.. 이거 풀고 싶으면 최영준 부르샴.
        */


        if (instance == null)
            instance = this;

        BattleManager.Instance.Initialize();

        // Bank를 모두 로드한 이후에 활성화된다.
        SoundManager.EnableFMOD(false);

        Application.runInBackground = true;

#if UNITY_EDITOR
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#else
#if UNITY_ANDROID || UNITY_IOS
            IronJade.Debug.Log($"[TestModeGameManager Awake] 1 Application.targetFrameRate = {Application.targetFrameRate}");
            Application.targetFrameRate = 60;
            IronJade.Debug.Log($"[TestModeGameManager Awake] 2 Application.targetFrameRate = {Application.targetFrameRate}");
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#elif UNITY_STANDALONE_WIN
            Screen.SetResolution(720, 1280, false);
#endif
#endif

        // 테스트모드에서는 시작시 패치를 완료로 처리 
        UtilModel.Resources.IsAddressablesUpdateComplete = true;

        //DontDestroyOnLoad(this);

        LocalDataManager.Instance.Prepare();

        Initialize();


    }

    private async void Start()
    {
#if UNITY_EDITOR
        // 빌드할때는 패스. 빌드시간 단축
        if (UnityEditor.BuildPipeline.isBuildingPlayer)
        {
            return;
        }
#endif      
        
        if (!disableFMOD)
            await TestLoadFMOD();

        await TestLoadAddressableTransitionsAsync();
    }

    private async UniTask TestLoadAddressableTransitionsAsync()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        await TransitionManager.LoadAddressableTransitionsAsync();
    }

    public async UniTask TestLoadFMOD()
    {
        if (!Application.isPlaying)
            return;

        await SoundManager.LoadEssentialBanksAsync(GameSettingManager.Instance.GameSetting.VoiceLanguageType);
        await SoundManager.LoadAllCharacterBanksAsync();

        if (BattleProcessManager.Instance != null)
        {
            await SoundManager.PrepareBattleBanks();
        }
        else
        {
            await SoundManager.PrepareTownBanks();
        }

        GameSettingManager.Instance.LoadSoundSetting();
    }

    //#if UNITY_EDITOR
    //    private void OnDestroy()
    //    {
    //        if (!Application.isPlaying)
    //        {
    //            CameraManager.Instance.SortingCameraStack();
    //            return;
    //        }
    //    }
    //#endif

    protected override async void Initialize()
    {
        SetStaticData();
        GameSettingManager.Instance.Run();
        LocalDataManager.Instance.Run();
        await SetEnvironment();
        InitializeTable();
        SetTable();
        SetTestQuest();
        SetTownObjects();
        UpdateAsync().Forget();
        SetOfflineTownTest().Forget();

#if UNITY_EDITOR
        // 테스트 때문에 임시로 만듬
        NetworkManager.Web.SetNetworkDebug(isShowNetworkLog);

        if (BackgroundSceneManager.Instance != null && BackgroundSceneManager.Instance.enabled == false)
            IronJade.Debug.LogError("BackgroundSceneManager가 꺼져있습니다..");
#endif
        TransitionManager.LoadingUI(false, false);
    }

    protected override void SetStaticData()
    {
        SetCulture();
        SetConfig();
        SetResourcesUtill();
        SetGameSettingManager();
        SetFlowManager();
        SetUIManager();
        SetTimeManager();
        SetNetworkManager();
        SetCheatManager();
        SetVideoManager();
        PlatformManager.Instance.Prepare();
    }

    protected override void SetConfig()
    {
        #region Device Config Setting
        DeviceConfig deviceConfig = new DeviceConfig();
        deviceConfig.SetDeviceType(SystemInfo.deviceType.ToString());

        string path = Application.persistentDataPath;

#if UNITY_EDITOR
        if (uITestSupport != null && !string.IsNullOrEmpty(uITestSupport.TestNid))
            deviceConfig.SetNid(uITestSupport.TestNid);
        else
            deviceConfig.SetNid(string.IsNullOrEmpty(testNid) ? SystemInfo.deviceUniqueIdentifier : testNid);
#else
        deviceConfig.SetNid(SystemInfo.deviceUniqueIdentifier);
#endif

        deviceConfig.SetDevicePath(path);

        Config.SetDeviceConfig(deviceConfig);
        Config.SetVersion(Version);
        Config.SetVersionCode(versionCode);
        Config.SetTutorial(isTutorial);
        Config.SetEnableBattleInputGuide(enableBattleInputGuide);
        #endregion
    }

    private async UniTask SetOfflineTownTest()
    {
#if UNITY_EDITOR
        if (!offlineTownTest)
        {
            var offlineTestSupport = FindFirstObjectByType<OfflineTownTestSupport>();

            if (offlineTestSupport == null)
            {
                townCharacterChangeSupport.SafeSetActive(false);
                townFreeCameraSupport.SafeSetActive(false);
                return;
            }

            offlineTownTest = offlineTestSupport.CurrentMode == OfflineTownTestSupport.Mode.Town;

            if (offlineTownTest && offlineTestSupport.EnableSound)
                await TestLoadFMOD();
        }

        if (offlineTownTest)
        {
            if (townCharacterChangeSupport != null)
            {
                townCharacterChangeSupport.SetStartCharacter(offlineTownTestStartCharacter);
                await townCharacterChangeSupport.ShowAsync();
            }

            await SetFreeCameraTest(false);
        }
        else
        {
            townCharacterChangeSupport.SafeSetActive(false);
            townFreeCameraSupport.SafeSetActive(true);

            await SetFreeCameraTest(true);
        }
#endif
    }

#if UNITY_EDITOR
    private async UniTask SetFreeCameraTest(bool on)
    {
        if (townFreeCameraSupport != null)
            await townFreeCameraSupport.ShowAsync();

        if (on)
            townFreeCameraSupport.InitializeFreeMoveMode(on);
    }
#endif

    /* private void SetTestModeGraphicOption()
     {
         GameSettingManager.Instance.TestModeSaveGraphicOption(SettingScreenModeType.Vertical, testQualityType, testFrameLimitType, GameSettingManager.Instance.GameSetting.CharacterQuality, GameSettingManager.Instance.GameSetting.EffectQuality);
     }*/

    public void SetFrameLimitType(SettingFrameLimitType settingFrameLimitType)
    {
        IronJade.Debug.Log("SetFrameLimitType settingFrameLimitType = " + settingFrameLimitType);

        testFrameLimitType = settingFrameLimitType;
        //  testQualityType = settingQualityType;

        //     SetTestModeGraphicOption();
    }

    public void SetFrameLimit(int targetFrame)
    {
        GameSettingManager.Instance.GraphicSettingModel.FPSValue(targetFrame);
    }

    public void SetTestDataStoryQuestId(int dataId)
    {
        testDataStoryQuestId = dataId;
    }

    public void SetStartPosition(TownObjectType townObjectType = TownObjectType.WarpPoint, string townObjectEnumId = StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT)
    {
        startTownObjectType = townObjectType;
        startTownObjectEnumId = townObjectEnumId;
    }

    private async UniTask SetEnvironment()
    {
        InitializeTable();

        await GameEnvironment.RequestEnviroment(ServerType, Version);

        NetworkManager.Web.Initialize();
        NetworkManager.Web.SetRegionUrl(GameEnvironment.RegionVariable.regionServerUrl);
        NetworkManager.Web.SetMessage(new string[]
        {
            // 런처 로컬라이징은 구조를 바꿀 예정
            // (메시지 이벤트를 넘기고 Key와 Retry 여부를 넘기도록 할 것)
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_CONNECTION_ERROR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_SERVICE_TEMPORARILY_ERROR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_CRITICAL_SERVER_ERROR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_FAILED_RETRY_ERROR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_FAILED_ERROR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_CRITICAL_ERROR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_EXCEPTION),
        });

        // 선택된 리전에 맞게 도메인 설정
        GameEnvironment.Version version = GameEnvironment.VersionVariable;
        GameEnvironment.Domain domain = GameEnvironment.RegionVariable.GetDomain("DEV1");

        ServerConfig serverConfig = new ServerConfig();
        serverConfig.SetAssetBundleUrl(version.assetBundleUrl);
        serverConfig.SetGameDataUrl(domain.gameDataUrl);
        serverConfig.SetApiServerUrl(domain.gameServerUrl);
        //serverConfig.SetServerType("");
        Config.SetServerConfig(serverConfig);
        Config.SetRegion("DEV1");

        NetworkManager.Web.SetUrl(Config.Server.GameServerUrl);
        NetworkManager.Web.SetCheatUrl(Config.Server.AdminServerUrl);
    }

    public void InitializeTable()
    {
        TableManager.Instance.ClearTable();

#if UNITY_EDITOR || CHEAT
        string path = string.Format(StringDefine.PATH_TABLE, "dev1");
        TableManager.Instance.EditorTestLoad(path, GameSettingManager.Instance.GetLanguageType());
#endif
        //#if UNITY_EDITOR || CHEAT
        //        TableManager.Instance.EditorTestLoad(Config.TableLocalPath);
        //#endif
    }

    private void SetTestQuest()
    {
        if (testDataStoryQuestId <= 0)
            return;

#if UNITY_EDITOR
        TestModeMissionManager.Instance.SetStoryQuest(testDataStoryQuestId);
#endif
    }

    private void SetTable()
    {
        TableManager.Instance.LoadTable<LocalizationTable>();
        TableManager.Instance.LoadTable<BattleLocalizationTable>();
        TableManager.Instance.LoadTable<TutorialLocalizationTable>();
        TableManager.Instance.LoadTable<ExtraStoryLocalizationTable>();
        TableManager.Instance.LoadTable<CashShopTable>();
        TableManager.Instance.LoadTable<CharacterTable>();
        TableManager.Instance.LoadTable<CharacterResourceTable>();
        TableManager.Instance.LoadTable<CharacterBalanceTable>();
        TableManager.Instance.LoadTable<CurrencyTable>();
        TableManager.Instance.LoadTable<DungeonTable>();
        TableManager.Instance.LoadTable<DungeonSetTable>();
        TableManager.Instance.LoadTable<StorySceneTable>();
        TableManager.Instance.LoadTable<ItemTable>();
        TableManager.Instance.LoadTable<EquipmentTable>();
        TableManager.Instance.LoadTable<BalanceTable>();
        TableManager.Instance.LoadTable<GachaCharacterTable>();
        TableManager.Instance.LoadTable<GachaShopTable>();
        TableManager.Instance.LoadTable<GachaCombineShopTable>();
        TableManager.Instance.LoadTable<RewardTable>();
        TableManager.Instance.LoadTable<ContentsShopTable>();
        TableManager.Instance.LoadTable<ProductTable>();
        TableManager.Instance.LoadTable<SkillGroupTable>();
        TableManager.Instance.LoadTable<SkillTable>();
        TableManager.Instance.LoadTable<BreakInSkillTable>();
        TableManager.Instance.LoadTable<BreakInTable>();
        TableManager.Instance.LoadTable<EquipmentBalanceTable>();
        TableManager.Instance.LoadTable<MembershipEffectTable>();
        TableManager.Instance.LoadTable<MembershipGradeTable>();
        TableManager.Instance.LoadTable<MissionTable>();
        TableManager.Instance.LoadTable<MissionPointTable>();
        TableManager.Instance.LoadTable<LikeAbilityEpisodeTable>();
        TableManager.Instance.LoadTable<LikeAbilityDialogTable>();
        TableManager.Instance.LoadTable<NpcTable>();
        TableManager.Instance.LoadTable<TriggerTable>();
        TableManager.Instance.LoadTable<MonsterGroupTable>();
        TableManager.Instance.LoadTable<CodeTable>();
        TableManager.Instance.LoadTable<InfinityCircuitTable>();
        TableManager.Instance.LoadTable<EventPassTable>();
        TableManager.Instance.LoadTable<EnterConditionTable>();
        TableManager.Instance.LoadTable<TutorialTable>();
        TableManager.Instance.LoadTable<ContentsOpenTable>();
        TableManager.Instance.LoadTable<CostTable>();
        TableManager.Instance.LoadTable<EventMissionTable>();
        TableManager.Instance.LoadTable<StageDungeonTable>();
        TableManager.Instance.LoadTable<EventMissionPointTable>();
        TableManager.Instance.LoadTable<CodeDamageRewardTable>();
        TableManager.Instance.LoadTable<StoryQuestTable>();
        TableManager.Instance.LoadTable<FieldMapTable>();
        TableManager.Instance.LoadTable<NpcConditionTable>();
        TableManager.Instance.LoadTable<NpcInteractionTable>();

        SetTimeStringFormats();
    }

    private void SetTownObjects()
    {
        if (!BackgroundSceneManager.Instance)
            return;

        TownObjectManager.Instance.LoadAllTownNpcInfoGroup();

        BackgroundSceneManager.Instance.AddTownObjects(BackgroundSceneManager.Instance.FieldMapDefine);
    }
    #endregion Coding rule : Function
}
