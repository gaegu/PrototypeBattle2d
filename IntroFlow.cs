#pragma warning disable CS1998, CS0162
using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Server.Web.Management;
using IronJade.Table.Data;
using IronJade.UI.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UFE3D;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using static UIManager;

public class IntroFlow : BaseFlow
{
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    // 아래 코드는 필수 코드 입니다.
    public IntroFlowModel Model { get { return GetModel<IntroFlowModel>(); } }
    //=========================================================================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function

    public override void Enter()
    {
        IronJade.Debug.Log("IntroFlow : Enter");

        ClearPlayerPrefs();
        UIManager.Instance.SetEventUIProcess(OnEventHome, OnEventChangeState);
    }
    private const string BASE_PATH = "Assets/IronJade/ResourcesAddressable/Character";
    private async UniTask LoadCharacterInfos()
    {
        string[] characterFolders = Directory.GetDirectories(BASE_PATH).Select(Path.GetFileName).Where(folderName => !folderName.Contains("RabbitKing")).ToArray();
        //"Character\\Shimizu\\Battle\\MoveSets\\Shimizu_Stance_1"
        foreach (string charName in characterFolders)
        {
            string prefabPath = NormalizePath(Path.Combine("Character", charName, "Battle", "MoveSets", $"{charName}_Stance_1"));

            //if (File.Exists(prefabPath))
            {
                //MoveSetData data = UtilModel.Resources.Load<StanceInfo>(prefabPath).ConvertData();
                var stancedata = UtilModel.Resources.Load<StanceInfo>(prefabPath, null);
                if (stancedata == null)
                    continue;

                MoveSetData data = stancedata.ConvertData();

                foreach (var move in data.attackMoves)
                {
                    if (move == null)
                    {
                        IronJade.Debug.LogError($"$$$$$${prefabPath} : 에서 null 이있다!!!!");
                    }
                    else
                        IronJade.Debug.LogError($"{prefabPath} : 에서 {move.name}");
                }
            }
        }
    }

    private string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    public override async UniTask<bool> Back()
    {
        bool isBack = await UIManager.Instance.BackAsync();

        return isBack;
    }

    public override async UniTask Exit()
    {
        IronJade.Debug.Log("IntroFlow : Exit");
        UIManager.Instance.DeleteAllUI();

        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_INTRO);
    }

    public override async UniTask LoadingProcess(System.Func<UniTask> onEventExitPrevFlow)
    {
        // =============================================
        // ================== 씬 로딩 ==================
        Scene scene = await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_INTRO, LoadSceneMode.Additive);

        await onEventExitPrevFlow();

        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_ROOT);
        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_LOGO);
        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_BATTLE);

        SceneManager.SetActiveScene(scene);

        BaseController introController = UIManager.Instance.GetController(UIType.IntroView);
        IntroViewModel introViewModel = introController.GetModel<IntroViewModel>();
        await UIManager.Instance.EnterAsync(introController);
    }

    public override async UniTask ChangeStateProcess(System.Enum state)
    {
        FlowState flowState = (FlowState)state;

        switch (flowState)
        {
            case FlowState.None:
                {
                    // 번들이 없는 상태일 수 있으므로 클립으로 재생한다.
                    SoundManager.Bgm.Play(StringDefine.PATH_BGM_INTRO, loop: true).Forget();
                    TransitionManager.ShowGlitch(true);

                    if (!await ProcessSDKLogin())
                        return;

                    await ProcessLogin();
                    break;
                }
        }
    }

    #region 데이터 다운로드
    /// <summary>
    /// 데이터 다운로드 (버전 체크 후 받을 파일 분류)
    /// </summary>
    private async UniTask RequestDownloadTableAsync(System.Action onSucess, bool withoutUI = false)
    {
        try
        {
#if UNITY_EDITOR
            if (GameManager.Instance.isLocalTable)
            {
                onSucess?.Invoke();
                return;
            }
#endif

            string dataVersionUrl = Config.Server.GetEncryptionGameDataUrl(TableManager.Instance.DataVersionFileName);

            dataVersionUrl = $"{dataVersionUrl}?t={System.DateTime.Now.Ticks}";

            using (UnityWebRequest requestDataVersion = UnityWebRequest.Get(dataVersionUrl))
            {
                requestDataVersion.certificateHandler = new AcceptAllCertificatesSignedWithASpecificKeyPublicKey();

                await requestDataVersion.SendWebRequest();

                if (requestDataVersion.result == UnityWebRequest.Result.Success)
                {
                    DataVersion serverDataVersion = UtilModel.Json.FromJson<DataVersion>(requestDataVersion.downloadHandler.text);
                    DataVersion localDataVersion = null;
                    List<DataFileVersion> downloadFiles = serverDataVersion.dataFileVersion;
                    string loadPath = TableManager.Instance.LoadPath;
                    string dataVersionPath = loadPath + TableManager.Instance.DataVersionFileName;

                    System.Action onEventSuccess = onSucess;

                    if (!Directory.Exists(loadPath))
                        Directory.CreateDirectory(loadPath);

                    if (File.Exists(dataVersionPath))
                    {
                        // 받아 놓은 데이터 버전이 있을 경우
                        string dataVersionText = File.ReadAllText(dataVersionPath);

                        if (!string.IsNullOrEmpty(dataVersionText))
                        {
                            IronJade.Debug.Log($"Try parse : {dataVersionPath} : {dataVersionText}");

                            if (UtilModel.Json.TryFromJson(dataVersionText, out DataVersion checkVersion))
                                localDataVersion = checkVersion;
                        }
                    }

                    // DataFile 정보들을 다운로드 받는다.
                    await RequestDownloadTableFileAsync(serverDataVersion, localDataVersion, dataVersionPath, onEventSuccess);
                }
                else
                {
                    IronJade.Debug.LogError("Data download failed.");

                    if (!withoutUI)
                    {
                        MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_WEB_ERROR_DATADOWNLOAD,
                                                       LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_RETRY,
                                                       LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_QUIT,
                                                       isCloseButton: false,
                                                       onEventConfirm: () =>
                                                       {
                                                           RequestDownloadTableAsync(onSucess).Forget();
                                                       },
                                                       onEventCancel: Application.Quit).Forget();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            IronJade.Debug.LogError(e);
            MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_WEB_ERROR_DATADOWNLOAD,
                                           LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_CONFIRM,
                                           isCloseButton: false,
                                           onEventConfirm: () =>
                                           {
                                               FlowManager.Instance.ChangeFlow(FlowType.LogoFlow, isStack: false).Forget();
                                           }).Forget();
        }
    }

    /// <summary>
    /// 데이터 다운로드 (받을 파일 분류 후 웹 통신)
    /// </summary>
    private async UniTask RequestDownloadTableFileAsync(DataVersion serverDataVersion, DataVersion localDataVersion, string dataVersionPath, System.Action onEventSuccess)
    {
        List<DataFileVersion> dowonloadDataFileVersions = new List<DataFileVersion>();

        // 임시 방어코드
        // 나중에는 데이터버전 파일에 디테일한 정보를 추가해서 파일을 비교 할 수 있어야 한다.
        if (localDataVersion == null || localDataVersion.version != serverDataVersion.version)
        {
            for (int i = 0; i < serverDataVersion.dataFileVersion.Count; ++i)
            {
                var serverDataFileVersion = serverDataVersion.dataFileVersion[i];
                dowonloadDataFileVersions.Add(serverDataFileVersion);
            }
        }
        else
        {
            // 1. 서버와 로컬 데이터가 다른 경우 다운로드 받을 목록에 추가한다.
            // 2. 데이터는 같은데, 파일이 없는 경우 다운로드 받을 목록에 추가한다.
            for (int i = 0; i < serverDataVersion.dataFileVersion.Count; ++i)
            {
                var serverDataFileVersion = serverDataVersion.dataFileVersion[i];
                var findDataFileVersion = localDataVersion == null ? default : localDataVersion.dataFileVersion.Find(match => match.fileName == serverDataFileVersion.fileName);
                string localDataFilePath = TableManager.Instance.LoadPath + serverDataFileVersion.fileName;

                if (string.IsNullOrEmpty(findDataFileVersion.fileName) ||
                    findDataFileVersion.version != serverDataFileVersion.version ||
                    !File.Exists(localDataFilePath))
                {
                    dowonloadDataFileVersions.Add(serverDataFileVersion);
                }
            }
        }

        int progress = 0;
        bool isWait = false;

        for (int i = 0; i < dowonloadDataFileVersions.Count; ++i)
        {
            // 다운로드 실패 시 잠깐 대기한다.
            await UniTask.WaitWhile(() => isWait);

            // 다운로드 프로그래스바 UI를 갱신한다.
            ++progress;
            float value = (float)progress / dowonloadDataFileVersions.Count;
            string text = $"{progress}/{dowonloadDataFileVersions.Count}";

            RefreshLoginUI(UIManager.LoginUIState.Download, text, value);

            bool isSuccessDownload = await RequestDataFileAsync(dowonloadDataFileVersions[i].fileName);

            if (isSuccessDownload)
                continue;

            // 다운로드 실패 메시지, 재시도를 묻는다.
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_ERROR_DATADOWNLOAD, dowonloadDataFileVersions[i].fileName);
            string confirm = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_RETRY);
            string cancel = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_MESSAGEBOXPOPUP_QUIT);

            MessageBoxManager.ShowYesNoBox(message, confirmButton: confirm, cancelButton: cancel, isCloseButton: false, onEventConfirm: () =>
                                           {
                                               isWait = false;
                                           },
                                           onEventCancel: Application.Quit).Forget();

            isWait = true;
        }

        // DataVersion 정보를 다운로드 받는다.
        File.WriteAllText(dataVersionPath, UtilModel.Json.ToJson(serverDataVersion));

        if (onEventSuccess != null)
            onEventSuccess();
    }

    /// <summary>
    /// 데이터 다운로드 (웹 통신)
    /// </summary>
    private async UniTask<bool> RequestDataFileAsync(string fileName)
    {
        string dataFileUrl = Config.Server.GetEncryptionGameDataUrl(fileName);
        dataFileUrl = $"{dataFileUrl}?t={System.DateTime.Now.Ticks}";

        using UnityWebRequest requestDataFile = UnityWebRequest.Get(dataFileUrl);
        await requestDataFile.SendWebRequest();

        if (requestDataFile.result == UnityWebRequest.Result.Success)
        {
            string dataFilePath = TableManager.Instance.LoadPath + fileName;

            File.WriteAllText(dataFilePath, requestDataFile.downloadHandler.text);

            return true;
        }
        else
        {
            IronJade.Debug.LogError($"Game Data Download Failed : {requestDataFile.error}");

            return false;
        }
    }
    #endregion

    #region 테이블 로드
    /// <summary>
    /// 테이블 기본 정보 셋팅
    /// </summary>
    public void InitializeTable()
    {
        TableManager.Instance.SetLanguageType(GameSettingManager.Instance.GetLanguageType());

#if UNITY_EDITOR
        if (GameManager.Instance.isLocalTable)
            TableManager.Instance.SetLoadPath(Config.TableLocalPath);
        else
            TableManager.Instance.SetLoadPath(Config.TableLoadPath);
#else
        TableManager.Instance.SetLoadPath(Config.TableLoadPath);
#endif
        TableManager.Instance.ClearTable();
        TableManager.Instance.LoadForbiddenWord();
        TableManager.Instance.LoadLauncherLocalizationTable();
    }

    /// <summary>
    /// 지금은 모든 테이블을 로드하고 있지만
    /// 나중에는 필요한 곳에서 필요한 부분만 호출 시켜야 한다.
    /// </summary>
    private void LoadTable()
    {
        using var _ = new ExecutionTimeLogger("[ADDRESSABLES_V2] IntroFlow.LoadTable()");

        TableManager.Instance.LoadTable<LocalizationTable>();
        TableManager.Instance.LoadTable<BattleLocalizationTable>();
        TableManager.Instance.LoadTable<TutorialLocalizationTable>();
        TableManager.Instance.LoadTable<ExtraStoryLocalizationTable>();
        TableManager.Instance.LoadTable<AutoBattleTable>();
        TableManager.Instance.LoadTable<CashShopTable>();
        TableManager.Instance.LoadTable<CharacterTable>();
        TableManager.Instance.LoadTable<CharacterResourceTable>();
        TableManager.Instance.LoadTable<CharacterInformationTable>();
        TableManager.Instance.LoadTable<CharacterBalanceTable>();
        TableManager.Instance.LoadTable<CurrencyTable>();
        TableManager.Instance.LoadTable<DungeonTable>();
        TableManager.Instance.LoadTable<DungeonSetTable>();
        TableManager.Instance.LoadTable<StorySceneTable>();
        TableManager.Instance.LoadTable<ItemTable>();
        TableManager.Instance.LoadTable<EquipmentTable>();
        TableManager.Instance.LoadTable<BalanceTable>();
        TableManager.Instance.LoadTable<GachaCharacterTable>();
        TableManager.Instance.LoadTable<GachaEquipmentTable>();
        TableManager.Instance.LoadTable<GachaItemTable>();
        TableManager.Instance.LoadTable<GachaShopTable>();
        TableManager.Instance.LoadTable<GachaCombineShopTable>();
        TableManager.Instance.LoadTable<RewardTable>();
        TableManager.Instance.LoadTable<ContentsShopTable>();
        TableManager.Instance.LoadTable<ProductTable>();
        TableManager.Instance.LoadTable<SkillGroupTable>();
        TableManager.Instance.LoadTable<SkillTable>();
        TableManager.Instance.LoadTable<BreakInTable>();
        TableManager.Instance.LoadTable<BreakInSkillTable>();
        TableManager.Instance.LoadTable<CharacterResourceTable>();
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
        TableManager.Instance.LoadTable<TutorialExplainTable>();
        TableManager.Instance.LoadTable<ContentsOpenTable>();
        TableManager.Instance.LoadTable<BillboardTable>();
        TableManager.Instance.LoadTable<ArenaBotTable>();
        TableManager.Instance.LoadTable<FieldMapTable>();
        TableManager.Instance.LoadTable<CostTable>();
        TableManager.Instance.LoadTable<ClassStatUpgradeTable>();
        TableManager.Instance.LoadTable<ElementStatUpgradeTable>();
        TableManager.Instance.LoadTable<LicenseStatUpgradeTable>();
        TableManager.Instance.LoadTable<StatUpgradeBalanceTable>();
        TableManager.Instance.LoadTable<InstantTalkTable>();
        TableManager.Instance.LoadTable<StoryQuestTable>();
        TableManager.Instance.LoadTable<EventMissionTable>();
        TableManager.Instance.LoadTable<EventMissionPointTable>();
        TableManager.Instance.LoadTable<GuildEmblemTable>();
        TableManager.Instance.LoadTable<StageDungeonTable>();
        TableManager.Instance.LoadTable<ConsumeItemTable>();
        TableManager.Instance.LoadTable<MailTable>();
        TableManager.Instance.LoadTable<ChargeItemTable>();
        TableManager.Instance.LoadTable<DispatchTable>();
        // TableManager.Instance.LoadTable<QuestTable>(); 현재 사용안하는 테이블 주석처리
        TableManager.Instance.LoadTable<GimmickTable>();
        TableManager.Instance.LoadTable<DailyQuestTable>();
        TableManager.Instance.LoadTable<CodeDamageRewardTable>();
        TableManager.Instance.LoadTable<TriggerTable>();
        TableManager.Instance.LoadTable<ExStageTable>();
        TableManager.Instance.LoadTable<ChattingEmojiTable>();
        TableManager.Instance.LoadTable<NpcConditionTable>();
        TableManager.Instance.LoadTable<NpcInteractionTable>();
        TableManager.Instance.LoadTable<DungeonMasterTable>();
        TableManager.Instance.LoadTable<DungeonGimmickTable>();
        TableManager.Instance.LoadTable<DungeonPhaseTable>();
        TableManager.Instance.LoadTable<CostumeTable>();
    }
    #endregion

    #region SDK 프로세스
    /// <summary>
    /// SDK 프로세스
    /// 실제 MSDK를 붙이고나면 내용물을 분류해서 함수를 나누자
    /// </summary>
    private async UniTask<bool> ProcessSDKLogin()
    {
        ServerType serverType = GameManager.Instance.ServerType;

        // 빌드에서는 이미 서버가 선택되었기 때문에 생략
#if UNITY_EDITOR
        if (serverType != ServerType.production &&
            serverType != ServerType.garena_dev &&
            serverType != ServerType.garena_sg &&
            serverType != ServerType.qa1 &&
            serverType != ServerType.dev2)
        {
            if (PlayerPrefsWrapper.HasKey(StringDefine.KEY_PLAYER_PREFS_SELECT_SERVER))
                serverType = (ServerType)PlayerPrefsWrapper.GetInt(StringDefine.KEY_PLAYER_PREFS_SELECT_SERVER);
            else
                serverType = await GameManager.Instance.ShowSelectServerPopup();
        }
#endif

        Config.SetServerType(serverType);

        InitializeTable();

        try
        {
            await GameEnvironment.RequestEnviroment(serverType, Config.Version);
        }
        catch
        {
#if UNITY_EDITOR
            await MessageBoxManager.ShowNetworkErrorBox("에디터 버그 입니다. 유니티를 재실행주세요.", true, Application.Quit);
#else
            var message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_NETWORKERROR_FAILED_ERROR);
            await MessageBoxManager.ShowNetworkErrorBox(message, true, Application.Quit);
#endif

            return false;
        }

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

        return true;
    }
    #endregion

    #region 로그인 프로세스
    /// <summary>
    /// 로그인 프로세스
    /// </summary>
    private async UniTask ProcessLogin()
    {
        // 계정이 있으면 바로 로그인을 하고
        // 계정이 없으면 리전을 선택 후 로그인을 한다.

        // View 없으면 에러 발생
        await EnterIntroView();

        // 선택 가능한 리전을 요청한다.
        // a. 유효한 리전이 존재하면 해당 리전으로 통합 로그인을 진행한다.
        // b. 리전이 없거나 유효하지 않으면:
        //    1) 리전 선택 팝업을 표시한다.
        //    2) 선택된 리전으로 통합 로그인을 진행한다.
        await RequestRegion(RequestUnifiedLogin);

        if (!await RequestLogin())
            return;

        // UserGet 성공 여부에 따라 로그인 분기
        await RequestUserGet(OnUserGetResponse);
    }

    private async UniTask OnUserGetResponse(Action onUserGetResponse, bool isExistNickname = true)
    {
        bool isSuccessUserGet = onUserGetResponse != null;

        /*bool enterTown = isSuccessUserGet && isExistNickname;

        // 도입부 강제 진입
        if (Config.IsGarenaBattleBuild)
        {
            enterTown = false;
            await PrologueManager.Instance.ChangeGarenaBuildSequneceModelGroup();
        }*/

        bool enterTown = true;

        if (enterTown)
        {
            await EnterTownFlow(onUserGetResponse);
        }
        else
        {
            await EnterPrologue(onUserGetResponse);
        }
    }

    private async UniTask<bool> RequestRegion(Func<string, UniTask> onSelectRegion)
    {
        string language = UtilModel.String.GetLocaleFromCurrentCulture();

        // 1. 추천 리전서버 목록을 받아온다.
        BaseProcess accountsRecommendRegionProcess = NetworkManager.Web.GetProcess(WebProcess.AccountsRecommendRegion);
        accountsRecommendRegionProcess.SetPacket(new GetRecommendRegionInDto(language, ""));

        if (await accountsRecommendRegionProcess.OnNetworkAsyncRequest())
        {
            accountsRecommendRegionProcess.OnNetworkResponse();
            var accountsRecommendRegionResponse = accountsRecommendRegionProcess.GetResponse<AccountsRecommendRegionResponse>();

            if (Config.ServerType == ServerType.garena_dev)
            {
                await onSelectRegion("GARENA_DEV");
            }
            else if (Config.ServerType == ServerType.garena_sg)
            {
                await onSelectRegion("GARENA_SG");
            }
            else if (Config.ServerType == ServerType.qa1)
            {
                await onSelectRegion("QA1");
            }
            else if (Config.ServerType == ServerType.dev2)
            {
                await onSelectRegion("DEV2");
            }
            else
            {
                // 2. 추천 리전서버 목록이 단일이면 강제 선택, 그룹이면 선택 팝업을 띄운다.
                if (accountsRecommendRegionResponse.data.recommendRegion.Length == 1)
                {
                    await onSelectRegion(accountsRecommendRegionResponse.data.recommendRegion[0]);
                }
                else
                {
                    string lastLoginRegionKey = string.Format(StringDefine.KEY_PLAYER_PREFS_LAST_LOGIN_REGION, Config.Device.Nid);
                    var lastLoginRegion = PlayerPrefsWrapper.GetString(lastLoginRegionKey, string.Empty);

                    bool isContain = false;

                    foreach (var region in accountsRecommendRegionResponse.data.recommendRegion)
                    {
                        if (region.Equals(lastLoginRegion))
                        {
                            isContain = true;
                            break;
                        }
                    }

                    // 마지막으로 접속했던 리전이 유효하다면 해당 리전으로 로그인 시도한다.
                    if (isContain)
                    {
                        await onSelectRegion(lastLoginRegion);
                    }
                    else
                    {
                        // 3. 추천 리전 목록을 팝업으로 띄우고 선택한 리전으로 접속한다.
                        var selectRegionServerController = UIManager.Instance.GetController(UIType.SelectRegionServerPopup);
                        var selectRegionServerPopupModel = selectRegionServerController.GetModel<SelectRegionServerPopupModel>();
                        selectRegionServerPopupModel.SetRegionList(accountsRecommendRegionResponse.data.recommendRegion);
                        selectRegionServerPopupModel.SetEventSelectRegion(async (index) =>
                        {
                            await onSelectRegion(accountsRecommendRegionResponse.data.recommendRegion[index]);
                        });

                        await UIManager.Instance.EnterAsync(selectRegionServerController);

                        // 4. 리전이 선택되기 전까지는 계속 기다린다.
                        await UniTask.WaitWhile(() => UIManager.Instance.CheckOpenUI(UIType.SelectRegionServerPopup));
                    }
                }
            }


            return true;
        }

        return false;
    }

    /// <summary>
    /// 선택한 리전으로 통합 로그인을 진행한다.
    /// </summary>
    private async UniTask RequestUnifiedLogin(string region)
    {
        var platformToken = await RequestPlatformToken(Config.LoginPlatform);
        string language = UtilModel.String.GetLocaleFromCurrentCulture();

        //// garena SDK 에서 받아온 open_id 리전서버로 전달 - garena SDK 미구축시 nid를 그대로 전달
        //// MSDK를 구현하면 NID 대신 openId를 넣어야 한다.
        BaseProcess unifiedLoginProcess = NetworkManager.Web.GetProcess(WebProcess.UnifiedLogin);
        unifiedLoginProcess.SetPacket(new UnifiedLoginInDto
            (Config.Device.Nid, Config.LoginPlatform.ToString(), platformToken, region, language, "general_user"));

        if (await unifiedLoginProcess.OnNetworkAsyncRequest())
            unifiedLoginProcess.OnNetworkResponse();
    }

    private async UniTask<string> RequestPlatformToken(LoginPlatform loginPlatform)
    {
        switch (loginPlatform)
        {

#if UNITY_STANDALONE
            case LoginPlatform.steam:
                SteamLogin steamLogin = new SteamLogin();
                return await steamLogin.GetPlatformToken();
#endif

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 로그인
    /// </summary>
    private async UniTask<bool> RequestLogin()
    {
#if !UNITY_EDITOR
        string log = string.Empty;
#else
        // 디바이스의 ip (속일 수 있음)
        string localIP = UtilModel.String.GetDeviceIP();
        // 공인 ip (조금 더 정확)
        string publicIP = await UtilModel.String.GetPublicIP();
        // 빌드 버전
        string version = Config.Version;
        // OS 정보
        string os = SystemInfo.operatingSystem;
        // 디바이스 모델명
        string deviceModel = SystemInfo.deviceModel;
        // 디바이스 이름
        string deviceName = SystemInfo.deviceName;
        LoginLog loginLog = new LoginLog(localIP, publicIP, version, os, deviceModel, deviceName);
        string log = UtilModel.Json.ToJson(loginLog);
#endif

        BaseProcess loginProcess = NetworkManager.Web.GetProcess(WebProcess.Login);
        loginProcess.SetPacket(new LoginInDto(Config.Device.Nid,
                                              Config.OpenId,
                                              string.Empty,
                                              Localization.Language.ToString(),
                                              log));

        if (await loginProcess.OnNetworkAsyncRequest())
        {
            loginProcess.OnNetworkResponse();
            return !loginProcess.GetResponse<LoginResponse>().data.IsBlockUser;
        }
        else
        {
            return false;
        }
    }
    #endregion

    #region 유저 로그인 프로세스
    /// <summary>
    /// 유저 정보를 얻는다.
    /// </summary>
    private async UniTask<bool> RequestUserGet(Func<Action, bool, UniTask> onResponse = null)
    {
        UserGetProcess userGetProcess = NetworkManager.Web.GetProcess<UserGetProcess>();

        if (await userGetProcess.OnNetworkAsyncRequest())
        {
            if (onResponse != null)
            {
                await onResponse(userGetProcess.OnNetworkResponse, userGetProcess.IsExistNickname());
                return true;
            }
            else
            {
                userGetProcess.OnNetworkResponse();
                return true;
            }
        }

        if (onResponse != null)
            await onResponse(null, false);

        return false;
    }

    /// <summary>
    /// 유저의 디테일한 정보를 얻는다.
    /// (캐릭터,장비,이벤트 등등)
    /// </summary>
    private async UniTask ProcessUserInfo(bool isUserSetting = true)
    {
        RefreshLoginUI(UIManager.LoginUIState.UserLogin);

        List<System.Func<UniTask>> requestList = new List<System.Func<UniTask>>();
        if (isUserSetting)
            requestList.Add(PlayerManager.Instance.LoadUserSetting);

        //========================================================================
        // 오픈조건 최우선적으로 세팅해야해서 순서 고정
        requestList.Add(RequestCharacterGet);
        requestList.Add(RequestStoryQuestGet);
        requestList.Add(RequestSubStoryQuestGet);
        requestList.Add(RequestCharacterQuestGet);
        requestList.Add(RequestStageDungeonGet);
        requestList.Add(RequestTutorialGet);
        requestList.Add(async () => { SetApplication(); });
        //========================================================================
        requestList.Add(RequestExstageGet);

        requestList.Add(RequestEquipmentGet);
        requestList.Add(RequestReeltapeGet);
        requestList.Add(RequestDeckGet);
        requestList.Add(RequestItemGet);
        requestList.Add(RequestMissionGet);
        requestList.Add(RequestMembership);
        requestList.Add(RequestNaviiChat);
        requestList.Add(RequestCostume);        //코스튬 획득여부와 연관된 컨텐츠를 먼저 전부 호출한 이후에 할 것[현재 Reeltape]
        //RequestLikeAbilityQuestGet,
        //RequestContentsShopGet,
        //RequestArenaSeasonGet,
        requestList.Add(RequestEventPassGet);
        requestList.Add(RequestInfinityDungeonGet);
        requestList.Add(RequestStatUpgradeGet);
        requestList.Add(RequestGuildGet);
        requestList.Add(RequestLevelSyncGet);

        for (int i = 0; i < requestList.Count; ++i)
        {
            float value = (float)i / requestList.Count;
            string text = $"{i}/{requestList.Count}";

            RefreshLoginUI(UIManager.LoginUIState.UserLogin, text, value);

            await requestList[i].Invoke();
        }

        RefreshLoginUI(UIManager.LoginUIState.UserLogin, $"{requestList.Count}/{requestList.Count}", 1.0f);

        await UniTask.NextFrame();
    }

    private async UniTask RequestStoryQuestGet()
    {
        await MissionManager.Instance.RequestGet(MissionContentType.StoryQuest);

        MissionManager.Instance.LoadBehaviorByUserSetting();

        // 새로 시작되는 퀘스트가 있는지 체크
        await MissionManager.Instance.CheckAcceptFirstStoryQuest(true);

        ContentsOpenManager.Instance.UpdateContents(contentsConfirm: new ContentsConfirm(isConfirmAlarm: true, isConfirmApplication: true));
    }

    private async UniTask RequestSubStoryQuestGet()
    {
        await MissionManager.Instance.RequestGet(MissionContentType.SubStoryQuest);

        //MissionManager.Instance.LoadBehaviorByUserSetting();

        // 새로 시작되는 퀘스트가 있는지 체크
        await MissionManager.Instance.CheckAcceptFirstSubStoryQuest(true);

        ContentsOpenManager.Instance.UpdateContents(contentsConfirm: new ContentsConfirm(isConfirmAlarm: true, isConfirmApplication: true));
    }

    private async UniTask RequestCharacterQuestGet()
    {
        await MissionManager.Instance.RequestGet(MissionContentType.CharacterQuest);

        //MissionManager.Instance.LoadBehaviorByUserSetting();

        // 새로 시작되는 퀘스트가 있는지 체크
        await MissionManager.Instance.CheckAcceptFirstCharacterQuest(true);

        ContentsOpenManager.Instance.UpdateContents(contentsConfirm: new ContentsConfirm(isConfirmAlarm: true, isConfirmApplication: true));
    }

    private async UniTask RequestStageDungeonGet()
    {
        DungeonManager.Instance.AddDungeonGroupModel<StageDungeonGroupModel>();
        if (ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.StageDungeon))
        {
            BaseProcess stageDungeonGetProcess = NetworkManager.Web.GetProcess(WebProcess.StageDungeonGet);

            if (await stageDungeonGetProcess.OnNetworkAsyncRequest())
                stageDungeonGetProcess.OnNetworkResponse();
        }
        ContentsOpenManager.Instance.UpdateContents(contentsConfirm: new ContentsConfirm(isConfirmAlarm: true, isConfirmApplication: true));
    }

    private void SetApplication()
    {
        ApplicationManager.Instance.SetApplicationInfos();
    }

    private async UniTask RequestExstageGet()
    {
        if (ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.ExStage))
        {
            BaseProcess exstageGetProcess = NetworkManager.Web.GetProcess(WebProcess.ExStageGet);

            if (await exstageGetProcess.OnNetworkAsyncRequest())
            {
                exstageGetProcess.OnNetworkResponse();
                return;
            }
        }
        DungeonManager.Instance.AddDungeonGroupModel<ExStageGroupModel>();
    }

    private async UniTask RequestCharacterGet()
    {
        BaseProcess characterGetProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterGet);

        if (await characterGetProcess.OnNetworkAsyncRequest())
            characterGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestEquipmentGet()
    {
        BaseProcess equipmentGetProcess = NetworkManager.Web.GetProcess(WebProcess.EquipmentGet);

        if (await equipmentGetProcess.OnNetworkAsyncRequest())
            equipmentGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestReeltapeGet()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.ReeltapeGet);

        if (await process.OnNetworkAsyncRequest())
            process.OnNetworkResponse();
    }

    private async UniTask RequestDeckGet()
    {
        BaseProcess deckGetProcess = NetworkManager.Web.GetProcess(WebProcess.DeckGet);

        if (await deckGetProcess.OnNetworkAsyncRequest())
            deckGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestItemGet()
    {
        BaseProcess itemGetProcess = NetworkManager.Web.GetProcess(WebProcess.ItemGet);

        if (await itemGetProcess.OnNetworkAsyncRequest())
            itemGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestTutorialGet()
    {
        BaseProcess tutorialGetProcess = NetworkManager.Web.GetProcess(WebProcess.TutorialGet);

        if (await tutorialGetProcess.OnNetworkAsyncRequest())
            tutorialGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestMissionGet()
    {
        await MissionManager.Instance.RequestAllTownQuestGet();
    }

    private async UniTask RequestMembership()
    {
        BaseProcess membershipGetProcess = NetworkManager.Web.GetProcess(WebProcess.MembershipGet);

        if (await membershipGetProcess.OnNetworkAsyncRequest())
            membershipGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestNaviiChat()
    {
        NaviiChatManager.Instance.Initialize();
    }

    private async UniTask RequestCostume()
    {
        BaseProcess costumeProcess = NetworkManager.Web.GetProcess(WebProcess.CostumeGet);

        if (await costumeProcess.OnNetworkAsyncRequest())
            costumeProcess.OnNetworkResponse();
    }

    private async UniTask RequestLikeAbilityQuestGet()
    {
        BaseProcess likeAbilityQuestGetProcess = NetworkManager.Web.GetProcess(WebProcess.LikeAbilityQuestGet);

        if (await likeAbilityQuestGetProcess.OnNetworkAsyncRequest())
            likeAbilityQuestGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestContentsShopGet()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_CONTENTSSHOP))
        {
            ShopManager.Instance.ContentsShopInfoModel.InitContentsShop();
            return;
        }

        BaseProcess contentsShopGetProcess = NetworkManager.Web.GetProcess(WebProcess.ContentsShopGet);

        if (await contentsShopGetProcess.OnNetworkAsyncRequest())
            contentsShopGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestArenaSeasonGet()
    {
        BaseProcess arenaSeasonGetProcess = NetworkManager.Web.GetProcess(WebProcess.ArenaSeasonGet);

        if (await arenaSeasonGetProcess.OnNetworkAsyncRequest())
            arenaSeasonGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestEventPassGet()
    {
        await EventManager.Instance.PacketEventPassGet();
    }

    private async UniTask RequestInfinityDungeonGet()
    {
        if (ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.InfinityCircuit))
        {
            BaseProcess infinityDungeonGetProcess = NetworkManager.Web.GetProcess(WebProcess.InfinityCircuitGet);

            if (await infinityDungeonGetProcess.OnNetworkAsyncRequest())
            {
                infinityDungeonGetProcess.OnNetworkResponse();
                return;
            }
        }
        DungeonManager.Instance.AddDungeonGroupModel<InfinityDungeonGroupModel>();
    }

    private async UniTask RequestStatUpgradeGet()
    {
        StatUpgradeManager.Instance.InitStatUpgradeData();

        BaseProcess statUpgadeGetProcess = NetworkManager.Web.GetProcess(WebProcess.StatUpgradeGet);

        if (await statUpgadeGetProcess.OnNetworkAsyncRequest())
            statUpgadeGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestGuildGet()
    {
        BaseProcess guildGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildGet);

        if (await guildGetProcess.OnNetworkAsyncRequest())
            guildGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestLevelSyncGet()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_LEVELSYNC))
            return;

        BaseProcess levelSyncGetProcess = NetworkManager.Web.GetProcess(WebProcess.LevelSyncGet);

        if (await levelSyncGetProcess.OnNetworkAsyncRequest())
            levelSyncGetProcess.OnNetworkResponse();
    }
    #endregion

    #region 어드레서블 프로세스

    /// <summary>
    /// 어드레서블 프로세스
    /// </summary>
    private async UniTask ProcessAddressablePatchWithLoginUI()
    {
        AddressableManager.Instance.SetCatalogPath(Config.Server.AssetBundleUrl);

        await AddressableManager.Instance.OnContentsPatch((text, value) =>
        {
            RefreshLoginUI(UIManager.LoginUIState.Addressable, text, value);
        }, true);

        await UniTask.WaitUntil(() => AddressableManager.Instance.isEndCheckUpdate);

        // 번들에 있는 로컬라이징을 불러온다.
        TableManager.Instance.LoadBundleLocaliztionTable();
        UIManager.Instance.HideLoginUI();

        // 시간 로컬을 세팅한다. 
        GameManager.Instance.SetTimeStringFormats();
    }

    private async UniTask ProcessAddressablePatch(Action<string, float> onEventProgressUI, bool initialize)
    {
        SoundManager.PlayTownBGM(GroundType.MidGround).Forget();

        if (initialize)
            AddressableManager.Instance.SetCatalogPath(Config.Server.AssetBundleUrl);

        await AddressableManager.Instance.OnContentsPatch(onEventProgressUI, initialize);

        await UniTask.WaitUntil(() => AddressableManager.Instance.isEndCheckUpdate);

        // 번들에 있는 로컬라이징을 불러온다.
        if (initialize)
        {
            TableManager.Instance.LoadBundleLocaliztionTable();
            GameManager.Instance.SetTimeStringFormats();
        }

        UIManager.Instance.HideDownloadProgress();
    }

    private async UniTask ProcessPrologueAddressablePatch(Action<string, float> onEventProgressUI, bool initialize, bool isForget)
    {
        if (initialize)
            AddressableManager.Instance.SetCatalogPath(Config.Server.AssetBundleUrl);

        if (isForget)
        {
            AddressableManager.Instance.OnProloguePatch(onEventProgressUI, initialize).Forget();

            await UniTask.WaitUntil(() =>
            {
                // 다운로드 시작할 때까지 대기 (의존성체크할 때 프리징걸리므로 해당 구간동안은 대기함.)
                return AddressableManager.Instance.GetDownloadState() != AddressableManager.DownloadState.None;
            });

            bool isDownloadComplete = AddressableManager.Instance.GetDownloadState() == AddressableManager.DownloadState.Complete;

            if (isDownloadComplete)
            {
                await WaitForEndPrologueAddressable();
            }
            else
            {
                PrologueManager.Instance.SetDownloading(true);
                await UniTask.WaitUntil(() =>
                {
                    return AddressableManager.Instance.GetDownloadState() == AddressableManager.DownloadState.Download;
                });
                WaitForEndPrologueAddressable().Forget();
            }
        }
        else
        {
            await AddressableManager.Instance.OnProloguePatch(onEventProgressUI, initialize);
            await WaitForEndPrologueAddressable();
        }
    }

    private async UniTask WaitForEndPrologueAddressable()
    {
        await UniTask.WaitUntil(() => AddressableManager.Instance.isEndCheckUpdate);

        await PrepareResources();

        OnEndPrologueAddressable();
    }

    private void OnEndPrologueAddressable()
    {
        TableManager.Instance.LoadBundleLocaliztionTable();
        GameManager.Instance.SetTimeStringFormats();

        UIManager.Instance.HideDownloadProgress();
        UIManager.Instance.HideLoginUI();

        if (PrologueManager.Instance.IsDownloading)
            PrologueManager.Instance.SetDownloading(false);

    }

    #endregion

    #region 인트로 UI 처리
    /// <summary>
    /// 이제 다운로드바 등 처리는 UIManager에서 함
    /// (Home에 해당하는 UI는 무조건 맨 처음 생성 되어 있어야 함 IntroView는 로딩프로세스로 옮김)
    /// <returns></returns>
    private async UniTask EnterIntroView()
    {
        RefreshLoginUI(UIManager.LoginUIState.Login);

        UIManager.Instance.ShowVersionInfo(GetVersionInfoText());
    }

    /// <summary>
    /// 로그인 상태의 UI 처리
    /// </summary>
    private void RefreshLoginUI(UIManager.LoginUIState loginState, System.Action onTouchScreen = null)
    {
        if (!FlowManager.Instance.CheckFlow(FlowType.IntroFlow))
            return;

#if UNITY_EDITOR
        var testModeGameManager = GameObject.Find("TestModeGameManager");

        if (testModeGameManager != null)
            return;
#endif

        if (loginState == UIManager.LoginUIState.TouchScreen && onTouchScreen != null)
        {
            UIManager.Instance.ShowTouchScreen(onTouchScreen);
        }
        else
        {
            UIManager.Instance.ShowLoginUIStateText(loginState);
        }

        Debug.LogError("# RefreshLoginUI loginState + " + loginState.ToString());

    }

    /// <summary>
    /// 데이터, 유저정보, 번들 등의 프로그래스 상태의 UI 처리
    /// </summary>
    private void RefreshLoginUI(UIManager.LoginUIState loginUIState, string text, float value)
    {
        if (!FlowManager.Instance.CheckFlow(FlowType.IntroFlow))
            return;

        UIManager.Instance.ShowDownloadProgress(text, value);
        UIManager.Instance.ShowLoginUIStateText(loginUIState);

        Debug.LogError("# 22 RefreshLoginUI loginState + " + loginUIState.ToString());

    }

    private string GetVersionInfoText()
    {
        if (Config.ServerType == ServerType.production)
            return TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INTROVIEW_VERSION, Config.ServerType.LocalizationText(), Config.Version);
        else
            return $"{Config.Device.Nid}\n{TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INTROVIEW_VERSION, Config.ServerType.LocalizationText(), Config.VersionCode)}";
    }
    #endregion

    #region 이벤트 처리
    private async UniTask OnEventHome() { }

    /// <summary>
    /// UI가 바뀌었을 때 처리
    /// </summary>
    private async UniTask OnEventChangeState(UIState state, UISubState subState, UIType prevUIType, UIType uiType)
    {
        switch (subState)
        {
            case UISubState.BeforeLoading:
                {
                    UIManager.Instance.EnableApplicationFrame(uiType);
                    break;
                }
        }
    }
    #endregion

    #region 프롤로그 (프롤로그 관련해서 여기다 채워주시고, 다 채우고나면 이 주석은 제거해주세요)

    private async UniTask<bool> CreateUser()
    {
        BaseProcess userCreateProcess = NetworkManager.Web.GetProcess(WebProcess.UserCreate);

        if (await userCreateProcess.OnNetworkAsyncRequest())
        {
            userCreateProcess.OnNetworkResponse();
            return true;
        }

        return false;
    }

    private async UniTask<bool> ProloguePrepareLogin(Action onUserGetResponse)
    {
        // 데이터 다운로드
        await RequestDownloadTableAsync(null);

        LoadTable();

        if (onUserGetResponse != null)
        {
            onUserGetResponse();
            return true;
        }
        else
        {
            return await CreateUser();
        }
    }

    private async UniTask EnterPrologue(Action onUserGetResponse)
    {
        var prologueBridge = new PrologueBridge();
        var bridgeDic = prologueBridge.GetBridgeDic();

#if UNITY_ANDROID || UNITY_IOS
        if (!GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode())
            await GameSettingManager.Instance.GraphicSettingModel.TemporaryViewMode(SettingViewModeType.Horizontal);
#endif

        PrologueManager.Instance.Initialize(bridgeDic);

        // 사무실 연출부터 시작하는지
        bool isStartFromMLE = PrologueManager.Instance.GetCurrentPrologueSequenceModel().
            PrologueName == PrologueSequenceName.TownFlow_MLE;

        if (isStartFromMLE)
        {
            SoundManager.PlayTownBGM(GroundType.MidGround).Forget();
            TransitionManager.ShowGlitch(false);
            await TransitionManager.In(TransitionType.Intro);
        }

        await PlayerManager.Instance.LoadDummyUserSetting();


        Debug.LogError("## Config.IsGarenaBattleBuild " + Config.IsGarenaBattleBuild);


        // 도입부 특정시퀀스에서 다운로드받을 경우
        if (PrologueManager.Instance.IsAddressableDownloadFromSequence() || Config.IsGarenaBattleBuild)
        {
            // 데이터 다운로드
            if (!await ProloguePrepareLogin(onUserGetResponse))
                return;

            // 영상부터 시작하는 경우 영상 틀고 번들 다운로드
            bool isForget = PrologueManager.Instance.IsDownloadAddressableWithVideo();

#if USE_PAD
            if (isForget)
                await PlayAssetDeliveryManager.Instance.LoadPlayAssetDeliveryBundle();
#endif

            // 도입부 번들만 다운로드
            await ProcessPrologueAddressablePatch(ShowDownloadUI, true, isForget);

            // 사무실 시퀀스에서 나머지 받도록
            PrologueManager.Instance.SetProcessUserInfo(ProcessUserInfo);
            PrologueManager.Instance.SetAddressablePatch(ProcessAddressablePatch);
        }
        else
        {
            // 데이터 다운로드
            if (!await ProloguePrepareLogin(onUserGetResponse))
                return;

            await ProcessUserInfo();

            // 번들 다운로드
            await ProcessAddressablePatchWithLoginUI();

            await PrepareResources();
        }

        TransitionManager.ShowGlitch(false);
        UIManager.Instance.HideLoginUI();

        if (!isStartFromMLE)
        {
            SoundManager.StopBGM();
        }

        
        //여기서 전투로 보내네. 
        PrologueManager.Instance.ProcessPrologueSequence().Forget();
    }
    #endregion

    #region 타이틀 화면
    private async UniTask EnterTownFlow(Action userGetResponse)
    {
        // 글리치 상태에서 시작
        await PlayerManager.Instance.LoadUserSetting();

        // 플레이어의 최종 위치에 따라 인트로 화면을 다르게 해줘야 한다.
        if (PlayerManager.Instance.CheckMleofficeIndoorFiled())
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode())
                await GameSettingManager.Instance.GraphicSettingModel.TemporaryViewMode(SettingViewModeType.Horizontal);
#endif

            SoundManager.PlayTownBGM(GroundType.MidGround).Forget();

            // 사무실에서 종료했을 경우
            // 로컬 정보에 마지막으로 진입한 컨텐츠가 뭐냐에 따라 다르게 해줘야 한다.
            await TransitionManager.In(TransitionType.Intro);
            TransitionManager.ShowGlitch(false);

            // 데이터
            await RequestDownloadTableAsync(null);

            LoadTable();

            // 테이블 로드 후 UserGet 호출해야 에러 안남
            userGetResponse();

            await PrepareLogin(userGetResponse);

            await FlowManager.Instance.ChangeFlow(FlowType.TownFlow, isStack: false);
        }
        else
        {
            // 사무실 밖에서 종료했을 경우
            // 로컬 정보에 마지막으로 진입한 컨텐츠가 뭐냐에 따라 다르게 해줘야 한다.

            // 데이터
            await RequestDownloadTableAsync(null);

            LoadTable();

            // 테이블 로드 후 UserGet 호출해야 에러 안남
            userGetResponse();

            await PrepareLogin(userGetResponse);

            RefreshLoginUI(UIManager.LoginUIState.TouchScreen, () =>
            {
                UIManager.Instance.HideTouchScreen();
                FlowManager.Instance.ChangeFlow(FlowType.TownFlow, isStack: false).Forget();
            });
        }
    }

    private async UniTask PrepareLogin(Action userGetResponse)
    {
        // 이미 만들었을 경우 정상 로그인
        await ProcessUserInfo(false);

        // 여기서 번들 못받았으면 계속 대기함
        await ProcessAddressablePatchWithLoginUI();

        // 번들 못받았는데 이리로 넘어오면 안된다. 강제종료
#if !UNITY_EDITOR && !UNITY_STANDALONE
        if (!AddressableManager.Instance.isEndCheckUpdate)
        {
            Application.Quit();
            return;
        }
#endif

        await PrepareResources();

#if UNITY_EDITOR
        if (GameManager.Instance.loadBattleStance)
            await LoadCharacterInfos();
#endif
    }

    private async UniTask PrepareResources()
    {
        TransitionManager.LoadingUI(true, true);

        await SoundManager.LoadEssentialBanksAsync(GameSettingManager.Instance.GameSetting.VoiceLanguageType);

        GameSettingManager.Instance.LoadSoundSetting();

        SoundManager.LoadAllCharacterBanksAsync().Forget();

        await TransitionManager.LoadAddressableTransitionsAsync();

        await SoundManager.WaitForBankLoad();

        TransitionManager.LoadingUI(false, false);
    }

    #endregion

    private void ShowDownloadUI(string text, float value)
    {
        RefreshLoginUI(UIManager.LoginUIState.Addressable, text, value);
    }

    /// <summary>
    /// 첫 로그인 시 날려야하는 PlayerPrefs
    /// </summary>
    private void ClearPlayerPrefs()
    {
        string[] clearKeys =
        {
            StringDefine.KEY_PLAYER_PREFS_FAVORITE_APP_SHOW_STATE,
        };

        foreach (var key in clearKeys)
            PlayerPrefs.DeleteKey(key);
    }

    public override async UniTask TestLoadingProcess(params object[] values)
    {
        await ProcessSDKLogin();

        // 환경 설정하는 부분이 없어서 추가
        SetEnvironmentByTest();

        LoadTable();

#if UNITY_EDITOR
        TestModeGameManager.Instance.SetTestMode(true);
#endif
        bool isNetwork = (bool)values[0];

        if (isNetwork)
            await RequestDownloadTableAsync(null);

#if UNITY_EDITOR || CHEAT
        TableManager.Instance.EditorTestLoad(Config.TableLocalPath, GameSettingManager.Instance.GetLanguageType());
#endif

        if (isNetwork)
        {
            LoadTable();

            await RequestRegion(RequestUnifiedLogin);

            if (!await RequestLogin())
                return;

            bool isUserGet = await RequestUserGet(null);
            bool isCreatedNickname = false;

            if (!isUserGet)
                isUserGet = await CreateUser();

#if UNITY_EDITOR
            // 마을 씬 테스트 코드
            if (GameManager.Instance.IsTestMode && BackgroundSceneManager.Instance)
                PlayerManager.Instance.MyPlayer.SetCurrentFieldMap(string.Empty, BackgroundSceneManager.Instance.FieldMapDefine);
#endif

            if (isUserGet)
                isCreatedNickname = PlayerManager.Instance.MyPlayer.User.IsCreatedNickName;

            if (isCreatedNickname)
            {
                await ProcessUserInfo();
            }
            else
            {
                BaseController introController = UIManager.Instance.GetController(UIType.IntroView);
                IntroViewModel introViewModel = introController.GetModel<IntroViewModel>();
                await UIManager.Instance.EnterAsync(introController);

                BaseController nickNameController = UIManager.Instance.GetController(UIType.NickNamePopup);
                NickNamePopupModel model = nickNameController.GetModel<NickNamePopupModel>();
                model.SetEventLoginProcess(null);

                await UIManager.Instance.EnterAsync(nickNameController);
                await UniTask.WaitWhile(() => UIManager.Instance.CheckOpenUI(UIType.NickNamePopup));
                // 실제 로직은 피니쉬드에서 호출되지만 테스트모드에서는 타이밍 문제로 따로 호출
                await ProcessUserInfo();
            }
        }
        else
        {
            LoadTable();

            UtilModel.Time.SetTime(() => { return NetworkManager.Web.ServerTimeUTC; }, () => { return NetworkManager.Web.ServerTimeKST; });
            TimeManager.Instance.Initialize();

            int userId = 0;
            User user = new User(userId, isMy: true);
            user.SetMyUserFlag(isMy: true);
            user.CharacterModel.SetLeaderCharacterId(0);

            Player player = new Player();
            player.SetUser(user);

            PlayerManager.Instance.SetPlayer(player);
        }
    }

    private void SetEnvironmentByTest()
    {
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

#if UNITY_STANDALONE
    // 작업 예정
    private bool CheckIntegrity()
    {
        List<string> paths = new List<string>();
        List<string> parameters = new List<string>();

        try
        {
            //paths.Add(Path.GetDirectoryName(Application.dataPath) + "/Exos Heroes.exe");
            paths.Add(Path.GetDirectoryName(Application.dataPath) + "/GameAssembly.dll");
            paths.Add(Path.GetDirectoryName(Application.dataPath) + "/UnityPlayer.dll");

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                    return false;

                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(path))
                    {
                        var hash = sha256.ComputeHash(stream);
                        string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        parameters.Add(hashString);
                    }
                }
            }
        }
        catch (Exception e)
        {
            IronJade.Debug.LogError($"Hash check failed: {e.Message}");
            return false;
        }

        return true;
    }

#endif
    #endregion Coding rule : Function
}