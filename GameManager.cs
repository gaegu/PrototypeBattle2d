using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using FMODUnity;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    protected static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(GameManager).Name;
                GameObject manager = GameObject.Find(className);
                instance = manager.GetComponent<GameManager>();

                if (instance == null)
                    instance = manager.AddComponent<GameManager>();
            }

            return instance;
        }
    }

    protected GameManager() { }


    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public ServerType ServerType { get { return serverType; } }
    public string Version { get { return version; } }
    #endregion Coding rule : Property

    #region Coding rule : Value
    [Header("서버 지정 (에디터에서만)")]
    [SerializeField]
    protected ServerType serverType = ServerType.none;
    [SerializeField]
    protected string version = "0.2.0.0";
    protected string versionCode = "0";

    [Header("튜토리얼 활성화/비활성화")]
    public bool isTutorial = false;

    [Header("전투입력가이드 활성화/비활성화")]
    public bool enableBattleInputGuide = false;

    [Header("랜덤 nid로 로그인")]
    [SerializeField]
    private bool randomNid = false;

    [Header("도입부 진입 시 계정 생성부터 바로 시작")]
    [SerializeField]
    private bool startPrologueFromCreateNickname = false;

    [Header("개발 전용")]
    [SerializeField]
    protected GameObject cheatManagerObject;
    [SerializeField]
    private GameObject selectServerPopup;
    [SerializeField]
    private bool isTestCameraByNpcTalk;
    [Header("가레나 빌드 플로우 테스트")]
    [SerializeField]
    private bool isTestGarenaBuildFlow = false;

    private ServerType selectServer;

    private float lastEscapeTime = 0f;
    private readonly float escapeInputDelay = 0.5f; // 전투 esc 연타 이슈가 있어서 딜레이 추가

#if UNITY_EDITOR
    [Header("디버그용 전투캐릭Move 확인용")]
    public bool loadBattleStance = false;
    [Header("전투 로그 수집")]
    public bool trackBattleLog = false;
    [Header("로컬 테이블")]
    public bool isLocalTable = false;

    [Header("테스트용 Nid를 입력하세요.")]
    public string testNid = string.Empty;
    public Test.UITestSupport uITestSupport;

    public bool IsTestMode { get; private set; } = false;

    [Header("데이터 툴 경로")]
    public string DataToolPath;
#endif
    #endregion Coding rule : Value

    [System.Serializable]
    public struct BuildEnvironment
    {
        public string serverType;
        public string version;
        public string versionCode;
        public bool fullBuild;

        public ServerType GetServerType()
        {
            return serverType switch
            {
                "dev1" => ServerType.dev1,
                "develop1" => ServerType.dev1,
                "dev2" => ServerType.dev2,
                "qa1" => ServerType.qa1,
                "qa2" => ServerType.qa2,
                "staging" => ServerType.staging,
                "production" => ServerType.production,
                "garena-dev" => ServerType.garena_dev,
                "garena-sg" => ServerType.garena_sg,
                _ => ServerType.dev1
            };
        }
    }


    #region Coding rule : Function
    public virtual void Awake()
    {
        if (!Application.isPlaying)
            return;

        // Bank를 모두 로드한 이후에 활성화된다.
        SoundManager.EnableFMOD(false);
        BattleManager.Instance.Initialize();

#if CHEAT
        SRDebug.Init();
#endif

#if DEBUG_LOG
        Debug.unityLogger.logEnabled = true;
#else
#if UNITY_EDITOR
       IronJade.Debug.Log("Unity 로그 출력이 비활성화되었습니다. 로그를 보려면 Scripting Define Symbols에 'DEBUG_LOG'를 추가하세요.");
#endif
        Debug.unityLogger.logEnabled = false;
#endif

#if !UNITY_EDITOR
        isTutorial = true;
        enableBattleInputGuide = true;
#endif

        Application.runInBackground = true;

        QualitySettings.streamingMipmapsActive = false;

#if UNITY_ANDROID || UNITY_IOS
        QualitySettings.vSyncCount = 1;
        IronJade.Debug.Log($"[GameManager Awake] 1 Application.targetFrameRate = {Application.targetFrameRate}");
        Application.targetFrameRate = 60;
        IronJade.Debug.Log($"[GameManager Awake] 2 Application.targetFrameRate = {Application.targetFrameRate}");
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#else
        QualitySettings.vSyncCount = 1;
#endif
        DontDestroyOnLoad(this);

#if UNITY_EDITOR
        // 테스트용으로 사용됩니다. 추후 삭제 예정입니다. by 김경한
        TownObjectManager.Instance.IsTestCameraByNpcTalk = isTestCameraByNpcTalk;
#endif

        Initialize();
        //RenderScale();
        DeviceRenderQuality.Setup();

        LocalDataManager.Instance.Prepare();
        RedDotManager.Instance.Prepare();
    }

    private void Start()
    {
        QualityController.Instance.Run();
        GameSettingManager.Instance.Run();
        LocalDataManager.Instance.Run();
        RedDotManager.Instance.Run();
        // 이게 항상 제일 하단
        PlatformManager.Instance.Run();

        if (serverType == ServerType.garena_sg && !GameSettingManager.Instance.ExistsOptionData())
        {
            if (Config.Version.Contains("0.14.0"))
            {
                // 영어
                GameSettingManager.Instance.SaveLanguageOption(SettingLanguageType.English, SettingLanguageType.English);
            }
            else
            {
                // 한국어
                GameSettingManager.Instance.SaveLanguageOption(SettingLanguageType.Korean, SettingLanguageType.Korean);
            }
        }

        // UI 사운드 처리가 기존에는 Active를 껐다켰다 해서 실행했는데
        // 지금은 Canvas의 enabled를 껐다켰다 하기 때문에 별도로 호출해준다.
        BaseController.OnEventPlaySound = (baseView, uiType) =>
        {
            if (baseView.CheckSafeNull())
                return;

            // 꺼져있는 경우 OnEnable에 소리 나오기 때문에 중복호출을 방지함
            if (!baseView.isActiveAndEnabled)
            {
                IronJade.Debug.LogError("[FMOD] - baseView.isActiveAndEnabled 재생취소");
                return;
            }

            // 현재 UI가 아니라면 호출 X
            if (!UIManager.Instance.CheckOpenCurrentUI(uiType))
            {
                IronJade.Debug.LogError($"[FMOD] - CheckOpenCurrentUI 재생취소 {uiType}");
                return;
            }

            var studioEventEmitters = baseView.GetComponents<StudioEventEmitter>();
            if (studioEventEmitters.CheckSafeNull())
                return;

            for (int i = 0; i < studioEventEmitters.Length; ++i)
            {
                if (studioEventEmitters[i].CheckSafeNull())
                    continue;

                if (studioEventEmitters[i].IsPlaying())
                {
                    IronJade.Debug.LogError("[FMOD] - StudioEventEmitter.IsPlaying() 재생취소");
                    continue;
                }

                studioEventEmitters[i].Play();
            }
        };
    }

    //     public void RenderScale()
    //     {
    // #if UNITY_ANDROID || UNITY_IOS
    //         // 현재 사용 중인 Render Pipeline Asset을 가져옵니다.
    //         var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    //
    //         if (urpAsset == null)
    //         {
    //            IronJade.Debug.LogError("현재 Universal Render Pipeline Asset이 설정되지 않았습니다.");
    //             return;
    //         }
    //
    //         urpAsset.renderScale = 0.75f;
    // #endif
    //     }

    private void OnDestroy()
    {
        TokenPool.CancelAll();
    }

    public async UniTask<ServerType> ShowSelectServerPopup()
    {
        selectServer = ServerType.none;

        selectServerPopup.SafeSetActive(true);

        await UniTask.WaitUntil(() => { return selectServer != ServerType.none; });

        selectServerPopup.SafeSetActive(false);
        PlayerPrefsWrapper.SetInt(StringDefine.KEY_PLAYER_PREFS_SELECT_SERVER, (int)selectServer);
        return selectServer;
    }

    public void SetServer(int serverTypeIndex)
    {
        selectServer = (ServerType)serverTypeIndex;
    }

    public void SetTimeStringFormats()
    {
        IronJade.Debug.Log($"[Check Time String Formats] - {TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_TIMETYPE_HMS)}");

        UtilModel.String.SetDefaultTimeStringFormats(new string[]
        {
            "{0:D2}{1:D2}{2:D2}",
            "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}",
            "{0:D4}-{1:D2}-{2:D2}",
            "{0:D2}:{1:D2}",
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_TIMETYPE_HM),
            "{0:D2}:{1:D2}:{2:D2} {3}", //{3} : AM/PM
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_TIMETYPE_HMS),
        });
        UtilModel.String.SetRemainTimeStringFormats(new string[]
        {
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_DAY_HOUR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_HOUR_MINUTE),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_MINUTE_SECOND),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_DAY_MONTHS_01),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_DAY_DAYS_01),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_DAY_HOUR_01),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_DAY_MINUTES_01),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TIME_REMAIN_SECOND),
        });

        UtilModel.String.SetElapsedTimeStringFormats(new string[]
        {
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ELAPSEDTIME_MONTH),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ELAPSEDTIME_DAY),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ELAPSEDTIME_HOUR),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ELAPSEDTIME_MINUTE),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ELAPSEDTIME_MOMENT_NOW),
        });

        UtilModel.String.SetDateTimeStringFormats(new string[]
        {
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_DATETIME_TODAY),
        });
    }

    public void ReturnToLogo()
    {
        FlowManager.Instance.ChangeFlow(FlowType.LogoFlow, isStack: false).Forget();
    }

    public async UniTask OpenApplicationQuitPopup()
    {
        await MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_NETWORK_END_GAME,
                                             LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_QUIT,
                                             LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_CANCEL,
                                             onEventConfirm: UnityEngine.Application.Quit);
    }

    /// <summary>
    /// 전투냐 아니냐에 따라 랜더피쳐 끄고 켜기
    /// </summary>
    public void ActiveRendererFeatures(bool isBattle)
    {
        //var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        //if (urpAsset == null)
        //    return;

        //FieldInfo propertyInfo = urpAsset.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        //ScriptableRendererData[] rendererDatas = (ScriptableRendererData[])propertyInfo.GetValue(urpAsset);

        //if (rendererDatas == null)
        //    return;

        //for (int i = 0; i < rendererDatas.Length; ++i)
        //{
        //    var rendererData = rendererDatas[i];

        //    //// 배틀/타운 구분이 있는 랜더러만 껐다 켰다 해준다.
        //    //for (int j = 0; j < rendererData.rendererFeatures.Count; ++j)
        //    //{
        //    //    bool isBattleFeature = rendererData.rendererFeatures[j].name.Contains("Battle");

        //    //    if (isBattleFeature)
        //    //    {
        //    //        isPossibleData = true;
        //    //        break;
        //    //    }
        //    //}

        //    //if (!isPossibleData)
        //    //    continue;

        //    for (int j = 0; j < rendererData.rendererFeatures.Count; ++j)
        //    {
        //        bool isBattleFeature = rendererData.rendererFeatures[j].name.Contains("Battle");

        //        if (!isBattleFeature)
        //            continue;

        //        rendererData.rendererFeatures[j].SetActive(isBattle);
        //    }
        //}
    }

    protected virtual void Initialize()
    {
        SetStaticData();

        if (PrologueManager.Instance != null && startPrologueFromCreateNickname)
            PrologueManager.Instance.StartFromMleOfficeIndex();

        // Factory 등록
        FlowManager.RegisterFlowFactory(new TownFlowFactory());

        FlowManager.Instance.ChangeFlow(FlowType.LogoFlow, isStack: false).Forget();

        UpdateAsync().Forget();
    }

    protected virtual void SetStaticData()
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
        CameraManager.Instance.SetActiveTownCameras(false);
        PlatformManager.Instance.Prepare();
    }

    protected virtual void SetNetworkManager()
    {
#if UNITY_EDITOR
        // 가끔식 이유 없이 리퀘스트가 실패해서 게임 시작할 때 그냥 캐시를 날림
        UnityWebRequest.ClearCookieCache();
#endif

        NetworkManager.Web.SetEventLoading(OnEventLoadingUI);
        NetworkManager.Web.SetEventMessage(OnEventMessage);
        NetworkManager.Web.SetEventPayLoad(OnEventPayLoad);

        WebProcessBridge webProcessBridge;
        NetworkManager.Web.SetTypeToProcess(webProcessBridge.GetTypeToWebProcess());
    }

    protected virtual void SetSteamManager()
    {
#if UNITY_STANDALONE
        if (Config.LoginPlatform != LoginPlatform.steam)
            return;

        GameObject steamManager = new GameObject(typeof(SteamManager).ToString());
        steamManager.AddComponent<SteamManager>();
#endif
    }

    protected virtual void OnEventMessage(IronJade.Server.Web.Core.WebErrorCode webErrorCode, string message, System.Action onEventConfirm)
    {
        switch (webErrorCode)
        {
            case WebErrorCode.SESSION_NOT_FOUND:
                {
                    message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ERRORCODE_SESSION_NOT_FOUND);
                    MessageBoxManager.ShowNetworkErrorBox(message, false, ReturnToLogo).Forget();
                    break;
                }

            case WebErrorCode.SESSION_RE_LOGIN:
                {
                    message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_ERRORCODE_SESSION_RE_LOGIN);
                    MessageBoxManager.ShowNetworkErrorBox(message, false, ReturnToLogo).Forget();
                    break;
                }

            default:
                {
                    MessageBoxManager.ShowNetworkErrorBox(message, true, onEventConfirm).Forget();
                    break;
                }
        }
    }

    protected void OnEventPayLoad(IResponseEntity responseEntity)
    {
        if (responseEntity == null)
            return;

        if (responseEntity.payLoad.achievements == null)
            return;

        for (int i = 0; i < responseEntity.payLoad.achievements.Length; ++i)
        {
            var achievement = responseEntity.payLoad.achievements[i];
        }
    }

    protected virtual async UniTask UpdateAsync()
    {
        while (!destroyCancellationToken.IsCancellationRequested)
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                    UIManager.Instance.ShowTouchEffect(touch.position).Forget();
            }
            else if (Input.GetMouseButtonDown(0))
            {
                UIManager.Instance.ShowTouchEffect(Input.mousePosition).Forget();
            }


            if (Input.GetKeyDown(KeyCode.Escape))
            {
                float currentTime = Time.time;

                if (currentTime - lastEscapeTime < escapeInputDelay)
                {
                    await UniTask.NextFrame(destroyCancellationToken);
                    continue;
                }

                lastEscapeTime = currentTime;

                // 튜토리얼 진행 예정이면 뒤로 갈 수 없다.
                if (TutorialManager.Instance.CheckTutorialPlaying() || TutorialManager.Instance.HasPlayableTutorial)
                {
                    await UniTask.NextFrame(destroyCancellationToken);
                    continue;
                }

                //트랜지션 중에는 뒤로 가기 불가
                if (TransitionManager.IsPlaying || TransitionManager.IsLoading)
                {
                    await UniTask.NextFrame(destroyCancellationToken);
                    continue;
                }

                // 프롤로그 중에 뒤로 갈 수 없다.
                if (PrologueManager.Instance.IsProgressing)
                {
                    await UniTask.NextFrame(destroyCancellationToken);
                    continue;
                }

                // 스토리 재생 중에 뒤로 갈 수 없다.
                if (StorySceneManager.Instance.IsPlaying)
                {
                    await UniTask.NextFrame(destroyCancellationToken);
                    continue;
                }

                //전투중
                if (BattleProcessManager.Instance != null)
                {
                    //현재 처형씬, 인트로 재생중
                    if (BattleProcessManager.Instance.IsPlayingExecution || BattleProcessManager.Instance.IsPlayingIntro)
                    {
                        await UniTask.NextFrame(destroyCancellationToken);
                        continue;
                    }

                    if (DeviceRenderQuality.UseLowMemoryMode)
                    {
                        if (Time.frameCount % 300 == 0)
                        {
                            IronJade.Debug.Log("[UseLowMemoryMode] Triggering GC.Collect()");
                            System.GC.Collect();
                        }
                    }
                }

                bool isBack = await FlowManager.Instance.BackToFlow();

                if (isBack)
                {
                    await UniTask.NextFrame(destroyCancellationToken);
                    continue;
                }

                var messageBox = UIManager.Instance.GetNetworkErrorUIController();
                if (messageBox != null)
                {
                    await UIManager.Instance.Exit(messageBox);
                    continue;
                }

                // 게임 종료 팝업
                await OpenApplicationQuitPopup();

                await UniTask.NextFrame(destroyCancellationToken);
                continue;
            }

            //if (Input.GetKeyUp(KeyCode.Print))
            //{
            //    if (FlowManager.Instance.CheckInGame())
            //    {
            //        await RequestPostingUserUploadImageKeyCreate();
            //        continue;
            //    }
            //}


            await UniTask.NextFrame(destroyCancellationToken);
        }
    }

    protected virtual void SetCulture()
    {
        //System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
    }

    protected virtual void SetResourcesUtill()
    {
        UtilModel.Resources.SetBuiltInScenes(new HashSet<string>()
        {
            StringDefine.SCENE_ROOT,
            StringDefine.SCENE_ROOT_LOADING,
            StringDefine.SCENE_LOGO,
            StringDefine.SCENE_INTRO,
            // 어드레서블로 변경됨
            //StringDefine.SCENE_TOWN,
            //StringDefine.SCENE_BATTLE,
        });
    }

    protected virtual void SetFlowManager()
    {
        Dictionary<FlowType, System.Type> flowBridge = new Dictionary<FlowType, System.Type>()
        {
            { FlowType.LogoFlow, typeof(LogoFlow) },
            { FlowType.IntroFlow, typeof(IntroFlow) },
            { FlowType.TownFlow, typeof(TownFlow) },
            { FlowType.BattleFlow, typeof(BattleFlow) },
            { FlowType.ExtraStoryFlow, typeof(ExtraStoryFlow) },
        };

        FlowManager.Instance.SetTypeToFlows(flowBridge);
    }

    protected virtual void SetUIManager()
    {
        UIManager.Instance.SafeSetParent(transform);

        UIBridge uIBridge;
        UIManager.Instance.SetTypeToControllers(uIBridge.GetTypeToControllers());
#if UNITY_EDITOR
        TransitionManager.TransitionOutEditor();     //에디터 재생 종료하며 끄는거 방지
#endif
    }

    protected virtual void SetTimeManager()
    {
        TimeManager timeManager = TimeManager.Instance;
    }

    protected virtual void SetGameSettingManager()
    {
    }

    protected virtual void SetCheatManager()
    {
        if (cheatManagerObject == null)
            return;
#if CHEAT
        cheatManagerObject.SafeSetActive(true);
#else
        Destroy(cheatManagerObject);
#endif

    }

    protected virtual void SetVideoManager()
    {
        if (VideoManager.Instance == null)
            return;

        VideoManager.Instance.ShowSkipButton(false);

        if (UIManager.Instance)
            UIManager.Instance.ShowCanvas(IronJade.UI.Core.CanvasType.Video, false);
    }

    protected virtual void SetConfig()
    {
        #region Device Config Setting
        DeviceConfig deviceConfig = new DeviceConfig();
        deviceConfig.SetDeviceType(SystemInfo.deviceType.ToString());

        string path = Application.persistentDataPath;

#if UNITY_EDITOR
        if (randomNid)
        {
            deviceConfig.SetNid(GenerateRandomNid());
        }
        else
        {
            if (uITestSupport != null && !string.IsNullOrEmpty(uITestSupport.TestNid))
                deviceConfig.SetNid(uITestSupport.TestNid);
            else
            {
                if (serverType == ServerType.dev1)
                {
                    deviceConfig.SetNid(string.IsNullOrEmpty(testNid) ?
                        SystemInfo.deviceUniqueIdentifier : testNid);
                }
                else
                {
                    deviceConfig.SetNid(string.IsNullOrEmpty(testNid) ?
                        $"{ServerType}_{SystemInfo.deviceUniqueIdentifier}" : testNid);
                }
            }
        }

#else
#if UNITY_IOS
            UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
        string loadBuildEnvironmentJson = UtilModel.Resources.LoadTextAsset($"Text/BuildEnvironment");
        BuildEnvironment buildEnvironment = UtilModel.Json.FromJson<BuildEnvironment>(loadBuildEnvironmentJson, typeof(BuildEnvironment));
        serverType = buildEnvironment.GetServerType();
        version = buildEnvironment.version;
        versionCode = buildEnvironment.versionCode;

        deviceConfig.SetNid($"{ServerType}_{SystemInfo.deviceUniqueIdentifier}");

#if DEBUG_LOG
        // DEBUG_LOG 활성화일때만 센트리를 활성화한다.(구글 또는 애플 검수 넣을때 http주소가 코드에 있으면 리젝당할수 있음)
        try
        {
            if (serverType <= ServerType.qa2)
            {
                Sentry.Unity.SentryUnity.Init(options =>
                {
                    options.Enabled = true;
                    options.Dsn = "http://700f1b326de8f4f6dc39227a6b001c3a@10.10.100.141:8000/2";
                    //options.Debug = true;
                    options.Release = $"{version}({versionCode}) {serverType}";
                    options.Environment = $"{Application.platform}";
                    options.MaxBreadcrumbs = 5;
                    options.DeduplicateMode = Sentry.DeduplicateMode.All;
                    
                   IronJade.Debug.Log($"Sentry initialized.");
                });

                Sentry.SentrySdk.CaptureMessage("Sentry initialized.");
            }
        }
        catch (System.Exception e)
        {
           IronJade.Debug.LogError($"Sentry initialization failed. Exception:{e}");
        }
#endif
#endif
        deviceConfig.SetDevicePath(path);

        Config.SetDeviceConfig(deviceConfig);
        Config.SetVersion(Version);
        Config.SetVersionCode(versionCode);
        Config.SetTutorial(isTutorial);
        Config.SetEnableBattleInputGuide(enableBattleInputGuide);

#if GARENA_BUILD
        Config.SetGarenaBattleBuild(true);
#elif UNITY_EDITOR
        Config.SetGarenaBattleBuild(isTestGarenaBuildFlow);
#endif
        #endregion

        SetLoginPlatform();
    }

    private void SetLoginPlatform()
    {
#if USE_STEAM
        Config.SetLoginPlatform(LoginPlatform.steam); 
#else
        Config.SetLoginPlatform(LoginPlatform.guest);
#endif
    }

    protected virtual void OnEventLoadingUI(bool isLoading, bool isLoadingIcon)
    {
        TransitionManager.NetworkLoadingUI(isLoading, isLoadingIcon);
    }

    private async UniTask RequestPostingUserUploadImageKeyCreate()
    {
        BaseProcess postingUserUploadImageKeyCreateProcess = NetworkManager.Web.GetProcess(WebProcess.PostingUserUploadImageKeyCreate);

        if (await postingUserUploadImageKeyCreateProcess.OnNetworkAsyncRequest())
        {
            await postingUserUploadImageKeyCreateProcess.OnNetworkAsyncResponse();
        }
    }

    public static void SaveBuildEnvironment(string server, string version, string versionCode, bool fullBuild)
    {
        string loadBuildEnvironmentJson = UtilModel.Resources.LoadTextAsset($"Text/BuildEnvironment");
        BuildEnvironment buildEnvironment = UtilModel.Json.FromJson<BuildEnvironment>(loadBuildEnvironmentJson, typeof(BuildEnvironment));
        buildEnvironment.serverType = server;
        buildEnvironment.version = version;
        buildEnvironment.versionCode = versionCode;
        buildEnvironment.fullBuild = fullBuild;
        UtilModel.Resources.Save(buildEnvironment, $"{Application.dataPath}/IronJade/Resources/Text/BuildEnvironment.json");
    }

#if UNITY_EDITOR
    public void SetTestMode(bool testMode)
    {
        IsTestMode = testMode;
    }

    [ContextMenu("TestCode")]
    public async void TestCode()
    {
        ThumbnailGeneratorModel thumbnailGeneratorModel = new ThumbnailGeneratorModel();
        var model = thumbnailGeneratorModel.GetCharacterUnitModelByDataId((int)CharacterDefine.CHARACTER_BEAT);
        IronJade.Debug.Log($"Start {System.DateTime.Now}");
        ThumbnailCharacterUnit thumbnailCharacterUnit = await UtilModel.Resources.LoadAsync<ThumbnailCharacterUnit>("UI/Contents/Common/Thumbnail/ThumbnailCharacterUnit", this);
        IronJade.Debug.Log($"Load {System.DateTime.Now}");
        thumbnailCharacterUnit.SetModel(model);
        //await thumbnailCharacterUnit.ShowAsync();
        IronJade.Debug.Log($"Show {System.DateTime.Now}");
        ////string buildPath = "D:/Project/Blink9_TheExos_Client";
        //string BuildTarget = "android";
        //string bundleVersion = "0.9.0.1";
        //string buildPath = $"build/assetbundle/{serverType}/{BuildTarget}/{bundleVersion}";
        //string resultPath = Application.dataPath.Replace("/Assets", $"/{buildPath}");
        //string remotePath = $"fgn-cdn.nerdystar.io/assetbundle/{serverType}/{BuildTarget}/{bundleVersion}";
        //UploadBundlesToGCS(resultPath, remotePath);
    }

    public static void UploadBundlesToGCS(string buildPath, string gcsPath)
    {
        // gsutil 명령어 생성
        // rsync는 폴더에
        string command = $"-NoProfile -Command cd {buildPath}; gsutil -m rsync -d -r . gs://{gcsPath}";

        IronJade.Debug.Log($"UploadBundlesToGCS command: {command}");

#if UNITY_IOS
        // 명령어 실행
        var processInfo = new System.Diagnostics.ProcessStartInfo("/bin/bash", command)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
#else
        // 명령어 실행
        var processInfo = new System.Diagnostics.ProcessStartInfo("powershell.exe", command)
        {
            CreateNoWindow = false,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
#endif

        try
        {
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(processInfo))
            {
                // 표준 출력과 오류를 실시간으로 읽어서 Unity 콘솔에 출력
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        IronJade.Debug.Log("stdout: " + e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        IronJade.Debug.LogError("stderr: " + e.Data);
                    }
                };

                // 비동기적으로 표준 출력을 읽기 시작
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                int exitCode = process.ExitCode;

                if (exitCode == 0)
                {
                    IronJade.Debug.Log("Bundles successfully uploaded to Google Cloud Storage.");
                }
                else
                {
                    IronJade.Debug.LogError("Failed to upload bundles to GCS. Exit code: " + exitCode);
                }
            }
        }
        catch (System.Exception e)
        {
            IronJade.Debug.LogError("Error uploading bundles to GCS: " + e.Message);
        }
    }
#endif

    public static string GenerateRandomNid(int length = 15)
    {
        string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        System.Random random = new System.Random();
        StringBuilder result = new StringBuilder(length);

        for (int i = 0; i < length; i++)
            result.Append(chars[random.Next(chars.Length)]);

        return result.ToString();
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        float width = Screen.width * 0.2f;
        float height = width / 3;

        if (TownObjectManager.Instance.IsTestCameraByNpcTalk &&
            GUI.Button(new Rect(Screen.width - width, Screen.height - height, width, height), "대화 카메라 PING"))
        {
            UnityEditor.EditorGUIUtility.PingObject(CameraManager.Instance.TalkCamera);
        }
    }
#endif
    #endregion Coding rule : Function
}