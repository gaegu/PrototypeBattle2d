#pragma warning disable CS1998
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.Camera.Core;
using IronJade.Flow.Core;
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TownFlow : BaseFlow, IObserver, ITownFlow
{
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    // 아래 코드는 필수 코드 입니다.
    public TownFlowModel Model { get { return GetModel<TownFlowModel>(); } }
    //=========================================================================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #region Town ObserverIDs
    private Enum[] townObserverIds =
    {
        FlowObserverID.PlayCutscene,
        FlowObserverID.PlayStoryToon,
        ViewObserverID.Refresh,
        CharacterObserverID.CharacterMove,
        FlowObserverID.PlayCinemachineTimeline,
        ObserverOverlayCameraID.ChangeStack,
    };
    #endregion

    private bool isEnterFromIntroFlow = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
#if UNITY_EDITOR
        IronJade.Debug.Log("Lobby : Enter");
#endif
        AddTownObserverIds();

        Model.SetCurrentUI(UIType.None);
        Model.SetCurrentViewUI(UIType.None);
        GameManager.Instance.ActiveRendererFeatures(false);

        CameraManager.Instance.RestoreCharacterCameraCullingMask();

        isEnterFromIntroFlow = FlowManager.Instance.CheckFlow(FlowType.IntroFlow) ||
                               FlowManager.Instance.CheckPrevFlow(FlowType.IntroFlow);

        if (isEnterFromIntroFlow)
            SetFirstTownFlowTransition();
    }

    private void SetFirstTownFlowTransition()
    {
        // 도입부는 별도 처리
        if (PlayerManager.Instance.UserSetting == null || PrologueManager.Instance.IsProgressing)
            return;

        if (!PlayerManager.Instance.CheckMleofficeIndoorFiled())
            return;

        // 비트가 일어나는 타임라인이 재생되어야해서 리더 캐릭터 비트로 설정함
        Model.SetTempLeaderCharacterDataId((int)CharacterDefine.CHARACTER_BEAT);

        // 처음에는 무조건 사무실 시작
        var LeaderCharacterPosition = PlayerManager.Instance.UserSetting.GetUserSettingData<LeaderCharacterPositionUserSetting>();
        LeaderCharacterPosition.SetScenePath(FieldMapDefine.FIELDMAP_MID_INDISTREET_MLEOFFICEINDOOR_01.ToString());

        FlowLoadingSequenceInfo loadingSequenceInfo = new FlowLoadingSequenceInfo();
        loadingSequenceInfo.SetAddLoadingScene(false);
        loadingSequenceInfo.SetAfterLoadingTask(FirstTownFlowTransition);
        
        Model.SetLoadingSequenceInfo(loadingSequenceInfo);
    }

    public override async UniTask<bool> Back()
    {
        var currentNavigator = UIManager.Instance.GetCurrentNavigator();
        UIType currentUIType = UIType.None;

        if (currentNavigator != null)
            currentUIType = currentNavigator.Controller.UIType;

        bool isBlock = UIManager.Instance.CheckBlockEscapeUI(currentUIType);
        if (isBlock)
            return true;

        bool isExit = UIManager.Instance.CheckExitByEscapeUITypes(currentUIType);
        if (isExit)
            return false;

        bool isBack = await UIManager.Instance.BackAsync();

        return isBack;
    }

    public override async UniTask Exit()
    {
        QualitySettings.streamingMipmapsActive = false;

        // 타운 오브젝트를 모두 제거한다.
        TownObjectManager.Instance.DestroyTownObject();
        TownObjectManager.Instance.ClearTownObjects();
        TownObjectManager.Instance.UnloadAllTownNpcInfoGroup();

        // 플레이어 관련 로직을 전부 초기화한다.
        PlayerManager.Instance.CancelLeaderCharacterPosition();
        PlayerManager.Instance.DestroyTownMyPlayerCharacter();
        PlayerManager.Instance.DestroyTownOtherPlayer();

        // 실시간 서버 연결을 해제한다.
#if YOUME
        ChattingManager.Instance.DisconnectChannel(ChattingChannelType.Public);
#endif

        // 현재 씬들을 닫는다.
        await UtilModel.Resources.UnLoadSceneAsync(Model.CurrentScene);
        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_TOWN);

        // 현재 UI들을 저장 해놓고 모든 UI를 닫는다.
        // Dialog가 있다면 별도로 꺼준다.
        await UIManager.Instance.Exit(UIType.DialogPopup);
        await UIManager.Instance.Exit(UIType.StageInfoWindow);

        UIManager.Instance.SavePrevStackNavigator();
        UIManager.Instance.DeleteAllUI();

        // 모든 에디티브 프리팹 언로드
        await AdditivePrefabManager.Instance.UnLoadAll();

        await CameraManager.Instance.ShowBackgroundRenderTexture(false);

        // 데이터 정리
        RemoveTownObserverIds();
        RemoveTables();

        // 메모리 해제
        await UtilModel.Resources.UnloadUnusedAssets(true);
    }

    #region Loading Process
    public override async UniTask TestLoadingProcess(params object[] values)
    {
        LoadTables();

        TownObjectManager.Instance.LoadAllTownNpcInfoGroup();

        bool isNetwork = (bool)values[0];

        if (isNetwork)
        {
            var LeaderCharacterPosition = PlayerManager.Instance.UserSetting.GetUserSettingData<LeaderCharacterPositionUserSetting>();
#if UNITY_EDITOR
            LeaderCharacterPosition.IsTest = true;
#endif

            Model.SetCurrentScene(LeaderCharacterPosition.Scene);
            Model.SetVolatilityScene(string.Empty);
        }

        // fmod 뱅크 로드
        await SoundManager.PrepareTownBanks();

        GameSettingManager.Instance.Test_LoadSoundSetting();

        await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_TOWN, LoadSceneMode.Additive);

        if (isNetwork)
        {
            await RequestDataFromNetwork();
        }
        else
        {
            await PlayerManager.Instance.LoadDummyUserSetting();
        }

        await TransitionManager.LoadAddressableTransitionsAsync();

        // 0. 배경에 있는 오브젝트들을 등록한다.
        BackgroundSceneManager.Instance.AddTownObjects(Model.CurrentFiledMapDefine);

        TownSceneManager.Instance.SetEventInteraction(OnEventCheckInteraction);

        // 1. 배경씬을 생성한다.
        // 2. 배경씬을 켜준다.
        BackgroundSceneManager.Instance.ShowTownGroup(true);


        // 3. 플레이어를 생성한다.
        await PlayerManager.Instance.CreateMyPlayerUnit();

        // 4. 플레이어 캐릭터 프리팹을 생성한다.
        await PlayerManager.Instance.LoadMyPlayerCharacterObject();

        // 5. 플레이어의 위치를 갱신한다.
#if UNITY_EDITOR
        if (TestModeGameManager.Instance != null)
        {
            // 6. 플레이어 캐릭터를 켜준다.
            await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

            // 시작 오브젝트의 위치가 퀘스트에 따라 바뀔 수 있어서 위치 수정
            await TownObjectManager.Instance.StartProcessAsync();

            PlayerManager.Instance.SetMyPlayerSpawn(TestModeGameManager.Instance.StartTownObjectType, TestModeGameManager.Instance.StartTownObjectEnumId, isNetwork);
        }
        else
        {
            // 6. 플레이어 캐릭터를 켜준다.
            await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

            PlayerManager.Instance.SetMyPlayerSpawn(TownObjectType.WarpPoint, StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT, isNetwork);

            await TownObjectManager.Instance.StartProcessAsync();
        }
#else
        PlayerManager.Instance.SetMyPlayerSpawn(TownObjectType.WarpPoint, StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT, isNetwork);
        
        // 6. 플레이어 캐릭터를 켜준다.
        await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

        await TownObjectManager.Instance.StartProcessAsync();
#endif

        CameraManager.Instance.RestoreDofTarget();

        UIManager.Instance.SetEventUIProcess(OnEventHome, OnEventChangeState);
        UIManager.Instance.HideLoginUI();

        TownObjectManager.Instance.SetConditionRoad();

        // 데코레이터 공장을 가동한다.
        BackgroundSceneManager.Instance.OperateTownDecoratorFactory();

        TownSceneManager.Instance.ShowTown(isShow: true);

        await UIManager.Instance.EnterAsync(UIType.LobbyView);

        await TownMiniMapManager.Instance.ShowTownMinimap();

        // 10. 카메라 활성화
        CameraManager.Instance.SetActiveTownCameras(true);

        // 11. Input 활성화
        TownSceneManager.Instance.TownInputSupport.SetInput(true);
        TownSceneManager.Instance.SetTownInput(CameraManager.Instance.TownCamera);

        TownSceneManager.Instance.PlayBGM();
    }

    public override async UniTask LoadingProcess(System.Func<UniTask> onEventExitPrevFlow)
    {
        Model.SetLoading(true);
        bool isOpenExStage = false;

        System.Func<UniTask> waitCommonProcess = async () =>
        {
            // 이게 Enter에 있으면 도입부에서 번들 받을 때 메시지박스가 켜지는 순간에
            // 타운이 아직 없는 상태라서 OnEventChangeState 함수에서 에러가 발생한다. (아직 타운 관련 리소스들이 없는 상태라서)
            UIManager.Instance.SetEventUIProcess(OnEventHome, OnEventChangeState);

            // 도입부에서는이전 UI 모두 초기화
            if (PrologueManager.Instance.IsProgressing)
                UIManager.Instance.RemovePrevStackNavigator();

            LoadTables(); // 테이블

            // 모든 NPC 컨디션 정보를 불러온다.
            TownObjectManager.Instance.LoadAllTownNpcInfoGroup();

            // 그림자 설정
            GameSettingManager.Instance.GraphicSettingModel.Shadow(GameSettingManager.Instance.GraphicSettingModel
                .OptionData.shadow);

            // 임시 캐릭터 설정 (사무실에서 비트가 일어날 때 리더가 비트여야해서)
            CheckTempLeaderCharacter();

            TownObjectType spawnTownObjectType = TownObjectType.WarpPoint;
            string spawnTargetEnumId = StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT;
            bool isLeaderCharacterPosition = true;

            // 마을에서 사용하는 FMOD 뱅크를 준비
            await SoundManager.PrepareTownBanks();

            // =============================================
            // ================== 씬 로딩 ==================
            if (!UtilModel.Resources.CheckLoadedScene(StringDefine.SCENE_TOWN))
                await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_TOWN, LoadSceneMode.Additive);

            // 이전 Flow 닫기
            await onEventExitPrevFlow();

            await UniTask.WhenAll(UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_ROOT),
                                  UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_LOGO),
                                  UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_INTRO),
                                  UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_BATTLE));

            // 미션 관련 진행 업데이트 (캐릭터 변경, 씬 설정 등이 있으므로 배경 설정 전에 호출합니다.)
            await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(false);

            var leaderCharacterPosition = PlayerManager.Instance.UserSetting.GetUserSettingData<LeaderCharacterPositionUserSetting>();
            var targetDataFieldMapEnumId = string.Empty;
            if (Model.IsVolatilityScene)
            {
                // 잠깐 이동할 씬이 있으면
                targetDataFieldMapEnumId = Model.VolatilityScene;
            }
            else if (MissionManager.Instance.IsExistMissionBehavior && MissionManager.Instance.MissionBehaviorBuffer.IsChangeScene)
            {
                // 미션에 해당하는 씬으로 이동
                targetDataFieldMapEnumId = MissionManager.Instance.MissionBehaviorBuffer.warpFieldMap.ToString();
                spawnTownObjectType = MissionManager.Instance.MissionBehaviorBuffer.warpTargetObjectType;
                spawnTargetEnumId = MissionManager.Instance.MissionBehaviorBuffer.warpTargetKey;
                isLeaderCharacterPosition = false;

                PlayerManager.Instance.SetTempLeaderCharacter(MissionManager.Instance.MissionBehaviorBuffer.dataCharacterId);
                PlayerManager.Instance.MyPlayer.User.CharacterModel.SetMissionCharacterDataId(MissionManager.Instance.MissionBehaviorBuffer.dataCharacterId);
            }
            else
            {
                // 유저정보에 저장된 씬으로 이동
                targetDataFieldMapEnumId = leaderCharacterPosition.Scene;
            }

            Model.SetCurrentScene(targetDataFieldMapEnumId);
            Model.SetVolatilityScene(string.Empty);

            if (string.IsNullOrEmpty(Model.CurrentScene))
                Model.SetCurrentScene(FieldMapDefine.FIELDMAP_MID_INDISTREET_MLEOFFICEINDOOR_01.ToString());

            Scene scene = await UtilModel.Resources.LoadSceneAsync(Model.CurrentScene, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            // =============================================
            // =============================================

            // 필수 서버 API를 호출한다. (미션, 기믹, 우편 등등)
            await RequestDataFromNetwork();

            // 배경 씬을 활성화하고 NPC 정보를 로드한다.
            BackgroundSceneManager.Instance.ShowTownGroup(true);
            BackgroundSceneManager.Instance.AddTownObjects(Model.CurrentFiledMapDefine);

            // 플레이어를 생성한다.
            PlayerManager.Instance.MyPlayer.SetInteracting(true);
            await PlayerManager.Instance.CreateMyPlayerUnit();
            await PlayerManager.Instance.LoadMyPlayerCharacterObject();
            await PlayerManager.Instance.MyPlayer.TownPlayer.OnEventCollisionExit(null);
            await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

            // Town 정보를 설정한다.
            TownSceneManager.Instance.SetEventInteraction(OnEventCheckInteraction); // 상호작용 이벤트 등록
            MissionManager.Instance.SetEventPlayLobbyMenu(TownSceneManager.Instance.PlayLobbyMenu);
            MissionManager.Instance.SetEventInput(TownSceneManager.Instance.SetActiveTownInput);

            var surfaces = GameObject.FindObjectsOfType<NavMeshSurface>();

            // 플레이어의 위치를 이동시키고 활성화 한다.
            PlayerManager.Instance.MyPlayer.SetCurrentFieldMap(Model.CurrentScene, Model.CurrentFiledMapDefine);
            PlayerManager.Instance.SetMyPlayerSpawn(spawnTownObjectType, spawnTargetEnumId, isLeaderCharacterPosition);
            await PlayerManager.Instance.MyPlayer.TownPlayer.RefreshProcess();

            // 플레이어의 위치 정보를 서버에 저장한다.
            leaderCharacterPosition.SetScenePath(targetDataFieldMapEnumId);
            leaderCharacterPosition.SetPosition(PlayerManager.Instance.MyPlayer.TownPlayer);
            await PlayerManager.Instance.UserSetting.SetUserSettingData(UserSettingModel.Save.Server, leaderCharacterPosition);

            // Town 관련 UI를 처리한다.
            await TownMiniMapManager.Instance.ShowTownMinimap(); // 미니맵을 보여준다.
            await TownSceneManager.Instance.RefreshAsync(Model.CurrentUI);

            // 배경 씬의 NPC와 Deco를 활성화한다.
            BackgroundSceneManager.Instance.SetCinemachineFollowTarget();   // 배경에 있는 시네머신 카메라 타겟에 플레이어를 넣는다.
            BackgroundSceneManager.Instance.OperateTownDecoratorFactory();
            TownObjectManager.Instance.OperateDecoratorFactory();// 데코레이터 공장을 가동한다.
            await TownObjectManager.Instance.StartProcessAsync();// NPC 가동
            TownObjectManager.Instance.SetConditionRoad();

            // 플레이어 입력을 풀어준다.
            TownSceneManager.Instance.TownInputSupport.SetInput(true);
            TownSceneManager.Instance.SetTownInput(CameraManager.Instance.TownCamera);
            CameraManager.Instance.RestoreDofTarget();
            CameraManager.Instance.SetLiveVirtualCamera();
            OnEventChangeLiveVirtualCinemachineCamera();

            // 추적 퀘스트 아이콘을 보여준다.
            MissionManager.Instance.ResetMissionBehavior();
            PlayerManager.Instance.ResetTempLeaderCharacter();

            // 사운드 리스너의 위치 (FMOD로 전부 교체되면 의미 제거해야함)
            Camera townCamera = CameraManager.Instance.GetCamera(GameCameraType.TownCharacter);
            SoundManager.SetAudioListenerFollowTarget(townCamera.transform);

            await TownSceneManager.Instance.UpdateLobbyMenu();

            // 카메라 블랜딩을 하고 브레인을 끈 상태가 기본이 되도록 한다.
            // 이 후 Brain을 켜고 끄고 판단은 UI의 State가 변경될 때 한다.
            CameraManager.Instance.SetActiveTownCameras(true);
            GameSettingManager.Instance.SetTargetFrame(true);
            await CameraManager.Instance.WaitCinemachineClearShotBlend();
            CameraManager.Instance.SetEnableBrain(false);
            PlayerManager.Instance.MyPlayer.SetInteracting(false);

            
            // 로비 UI 로드 (미션 갱신 전에 있어야 전투 후 되돌아왔을 때 보상 팝업이 꼬이지 않는다.)
            bool isPrevStack = UIManager.Instance.CheckPrevStackNavigator();

            // 볼륨 설정
            ChangeVolume(isPrevStack);

            if (isPrevStack) // 이전 UI가 있을 때
            {
                await UIManager.Instance.LoadPrevStackNavigator();

                if (Model.IsBattleBack && UIManager.Instance.CheckOpenCurrentView(UIType.StageDungeonView))
                {
                    var exStageGroupModel = DungeonManager.Instance.GetDungeonGroupModel<ExStageGroupModel>();
                    var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
                    var stageDungeonTableData = exStageGroupModel.GetCurrentExStageTableData(stageDungeonGroupModel);

                    if (Model.BattleInfo.ContentsDataID == stageDungeonTableData.GetCONDITION_STAGE_DUNGEON())
                        isOpenExStage = true;
                }
            }
            else
            {
                await UIManager.Instance.EnterAsync(UIType.LobbyView);
            }

            if (Model.TeamUpdateViewModel != null)
            {
                var teamUpdateController = UIManager.Instance.GetController(UIType.TeamUpdateView);
                var viewModel = teamUpdateController.GetModel<TeamUpdateViewModel>();
                viewModel.SetApplication(false);
                viewModel.SetUser(PlayerManager.Instance.MyPlayer.User);
                viewModel.SetDungeonTableData(Model.TeamUpdateViewModel.DungeonData);
                viewModel.SetCurrentDeckType(Model.TeamUpdateViewModel.CurrentDeckType);
                viewModel.SetOpenPresetNumber(0);
                viewModel.SetFixedTeam(Model.TeamUpdateViewModel.FixedTeam);
                await UIManager.Instance.EnterAsync(teamUpdateController);

                Model.SetTeamUpdateViewModel(null);
            }

            // 전투에서 돌아온게 아니면 BGM 재생
            if (!Model.IsBattleBack)
                TownSceneManager.Instance.PlayBGM();

            //전투에서 돌아온 경우, 로비가 아닌 경우, BGM이 달린 UI가 아니라면 BGM 재생.
            //임시처리입니다. 추후 UI 전용 BGM을 관리하는 시스템을 만들어야 할 것 같아요
            if (!UIManager.Instance.CheckOpenCurrentUI(UIType.LobbyView))
            {
                switch (UIManager.Instance.GetCurrentNavigator().Controller.UIType)
                {
                    case UIType.InfinityDungeonView:
                    case UIType.InfinityDungeonLobbyView:

                    case UIType.CodeHubView:
                    case UIType.CodeView:
                        break;

                    default:
                        TownSceneManager.Instance.PlayBGM();
                        break;
                }
            }

            MissionManager.Instance.NotifyStoryQuestTracking();

            // 카메라 활성화
            CameraManager.Instance.SetActiveTownCameras(true);

            PlayerManager.Instance.MyPlayer.SetInteracting(false);
        };

        System.Func<UniTask> waitNoneSequenceInfoProcess = async () =>
        {
            // 미션이나 별도의 처리가 없다면 LOGO가 기본 트랜지션
            TransitionType transitionType = TransitionType.Rotation;

            if (Model.IsBattleBack)
            {
                // 던전에서 되돌아 왔을 때 트랜지션 처리
                transitionType = Model.BattleInfo.DungeonEndTransition;
            }

            // introFlow에서 진입한 경우에는 글리치만 보여줌
            // 03.14 우진님 요청
            if (!isEnterFromIntroFlow)
                await TransitionManager.In(transitionType);

            await waitCommonProcess();

            TransitionManager.ShowGlitch(false);

            // 미션 과정 중 워프가 있는지 확인. true 가 되면 안됩니다.
            if (MissionManager.Instance.CheckHasWarpProcessByBehaviorBuffer())
            {
                IronJade.Debug.LogError("Mission Process: TownFlow 로딩 중 WarpProcess가 있습니다.");
                MissionManager.Instance.ResetMissionBehavior();
            }

            // 미션 후처리가 필요한 로직이 있으면 진행, 워프 제외
            if (UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView))
            {
                bool hasEndStory = MissionManager.Instance.CheckHasDelayedStory(false);

                if (hasEndStory)
                {
                    await MissionManager.Instance.StartAutoProcess(transitionType, false);

                    // 종료 스토리가 있었으면 끝난 후 BGM 재생
                    TownSceneManager.Instance.PlayBGM();
                }
                else
                {
                    if (!isEnterFromIntroFlow)
                        await TransitionManager.Out(transitionType);

                    // 종료 스토리가 없으면 BGM 재생
                    TownSceneManager.Instance.PlayBGM();

                    // 워프없이 미션 후처리 과정이 있는 경우
                    if (!MissionManager.Instance.CheckHasWarpProcessByBehaviorBuffer() &&
                        MissionManager.Instance.CheckHasDelayed())
                    {
                        await MissionManager.Instance.StartAutoProcess(transitionType);
                    }
                }

                if (!UIManager.Instance.CheckOpenUI(UIType.PortraitView))
                    await ContentsOpenManager.Instance.ShowNewContentsAlarm();

                if (TutorialManager.Instance.HasPlayableTutorial && !MissionManager.Instance.IsWait)
                {
                    // MissionManager가 None을 찍고나서 다음 프로세스를 진행하는 경우가 있어서 딜레이 프로세스가 완료된 이후 튜토리얼이 진행되도록 수정
                    System.Action taskTutorial = async () =>
                    {
                        await UniTask.WaitUntil(() => !MissionManager.Instance.CheckHasDelayed());
                        await UniTask.WaitUntil(() => !MissionManager.Instance.IsWait);
                        TutorialManager.Instance.PlayNextTutorial();
                    };
                    taskTutorial.Invoke();
                }
            }
            else
            {
                // 다른 UI 로 돌아오면 BGM 재생
                if (UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView))
                    TownSceneManager.Instance.PlayBGM();

                if (!isEnterFromIntroFlow)
                    await TransitionManager.Out(transitionType);
            }
        };

        System.Func<UniTask> waitSequenceInfoProcess = async () =>
        {
            var loadingSequenceInfo = Model.LoadingSequenceInfo;

            Application.backgroundLoadingPriority = ThreadPriority.Low;

            if (loadingSequenceInfo.BeforeLoadingTask != null)
                await loadingSequenceInfo.BeforeLoadingTask();

            if (Model.LoadingSequenceInfo.WhenAllTask == null)
            {
                await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_TOWN, loadingSequenceInfo.LoadSceneMode);
                await onEventExitPrevFlow(); // 이전 Flow 닫기
                await FlowManager.Instance.UnloadFlowScene(FlowType.IntroFlow);
                await FlowManager.Instance.UnloadFlowScene(FlowType.BattleFlow);
            }
            else
            {
                await UniTask.WhenAll(loadingSequenceInfo.WhenAllTask(),
                    UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_TOWN, loadingSequenceInfo.LoadSceneMode),
                    onEventExitPrevFlow(),// 이전 Flow 닫기
                    FlowManager.Instance.UnloadFlowScene(FlowType.IntroFlow),
                    FlowManager.Instance.UnloadFlowScene(FlowType.BattleFlow));
            }

            await waitCommonProcess();

            if (loadingSequenceInfo.AfterLoadingTask != null)
                await loadingSequenceInfo.AfterLoadingTask();
        };

        if (Model.LoadingSequenceInfo == null)
            await waitNoneSequenceInfoProcess();
        else
            await waitSequenceInfoProcess();

        // 타운으로 첫 진입 시에만 스폰 연출 (이후에는 캐릭터 교체될 때 마다)
        if (Model.IsBattleBack)
        {
            // 미션이 아닐 때 로비 UI를 켜준다.
            if (!MissionManager.Instance.IsWait)
            {
                if (UIManager.Instance.CheckOpenCurrentUI(UIType.LobbyView))
                    await TownSceneManager.Instance.PlayLobbyMenu(true);
            }
        }
        else
        {
            //if (!PlayerManager.Instance.CheckMleofficeIndoorFiled())
            //    PlayerManager.Instance.ShowMyPlayerSpawnEffect(true);

            // 미션이 아닐 때 로비 UI를 켜준다.
            if (!MissionManager.Instance.IsWait)
                await TownSceneManager.Instance.PlayLobbyMenu(true);
        }

        TownSceneManager.Instance.ShowFieldMapName(Model.CurrentFiledMapDefine, isDelay: false);
        Model.SetBattleInfo(null);
        Model.SetLoading(false);

        if (isOpenExStage)
            ObserverManager.NotifyObserver(DungeonObserverID.FocusExStage, null);

        //2D 임시
        //CameraManager.Instance.GetBrainCamera().enabled = false;
        //CameraManager.Instance.GetBrainCamera().transform.SetPositionAndRotation(new Vector3(0, 1.5f, -5), new Quaternion(0, 0, 0, 0));
    }

    private void CheckTempLeaderCharacter()
    {
        if (Model.TempCharacterDataId == 0)
            PlayerManager.Instance.ResetTempLeaderCharacter();
        else
            PlayerManager.Instance.SetTempLeaderCharacter(Model.TempCharacterDataId);
    }

    /// <summary>
    /// 타운플로우에 첫 진입 시, 비트가 사무실에서 일어나는 연출
    /// </summary>
    private async UniTask FirstTownFlowTransition()
    {
        Transform parent = BackgroundSceneManager.Instance.TownObjectParent.transform;

        await LoadTownTransitionTimeline(parent);

        bool isTouchScreen = false;

        UIManager.Instance.ShowTouchScreen(() => { isTouchScreen = true; });
        TransitionManager.LoadingUI(false, false);

        await UniTask.WaitUntil(() => { return isTouchScreen; });

        UIManager.Instance.HideTouchScreen();

        TransitionManager.Out(TransitionType.Intro).Forget();

        await PlayTownTransitionTimeline();
    }

    /// <summary>
    /// 인게임 NPC를 바인딩해서 쓰기 때문에 타운 로딩 완료 후에 실행해야한다.
    /// </summary>
    private async UniTask LoadTownTransitionTimeline(Transform parent)
    {
        // 타임라인에 등장하는 NPC 도입부 애니메이션 미리 세팅
        int[] preloadCharacterNpcIds = new int[]
        {
            (int)NpcDefine.CHAR_SAZANG,
            (int)NpcDefine.CHAR_JOA,
            (int)NpcDefine.CHAR_JOHNCOOPER,
            (int)NpcDefine.CHAR_RU_I,
            (int)NpcDefine.CHAR_FRANK,
        };

        foreach (int id in preloadCharacterNpcIds)
        {
            TownNpcSupport support = TownObjectManager.Instance.GetTownObjectByDataId(id) as TownNpcSupport;

            if (support == null)
            {
                IronJade.Debug.LogError($"Character Pre Load Failed! : {id}");
                continue;
            }

            // 사무실에 해당 NPC가 현재 진행중인 퀘스트의 대상이라면
            if (TownObjectManager.Instance.IsMissionRelatedNpc(FieldMapDefine.FIELDMAP_MID_INDISTREET_MLEOFFICEINDOOR_01, support.DataId))
            {
                // VisibleState가 Visible일 때만 로드
                // 예외 케이스 : 존쿠퍼의 레퍼런스체크 진행중에는 존 쿠퍼가 사무실에 있으면 안되서 Invisible임
                if (support.CheckCondition() && support.TownObject.Town3DModel == null)
                    await support.LoadTownObject();
            }
            else
            {
                // 그 외에는 무조건 로드
                // 조건 문의 => 허헌준님
                if (support.TownObject.Town3DModel == null)
                    await support.LoadTownObject();
            }
        }

        await CinemachineTimelineManager.Instance.PrepareCinemachineTimelineUnit
            (StringDefine.PATH_MLE_OFFICE_CINEMACHINE_TIMELINE, Vector3.zero, Quaternion.identity, parent);

        if (!CinemachineTimelineManager.Instance.IsReady)
            return;

        await CinemachineTimelineManager.Instance.Prewarm();
    }

    private async UniTask PlayTownTransitionTimeline()
    {
        if (!CinemachineTimelineManager.Instance.IsReady)
            return;

        await CinemachineTimelineManager.Instance.Play();
    }
    #endregion

    /// <summary>
    /// TownFlow의 상태 처리
    /// </summary>
    public override async UniTask ChangeStateProcess(System.Enum state)
    {
        FlowState flowState = (FlowState)state;

        switch (flowState)
        {
            case FlowState.None:
                {
                    break;
                }

            case FlowState.Warp:
                {
                    IronJade.Debug.Log("WarpProcess()");

                    if (Model.IsWarpProcess)
                        return;

                    // 자동이동 중이었는지 체크
                    bool isAutoMove = MissionManager.Instance.IsAutoMove;

                    if (Model.Warp.isForMission)
                        await OnMissionWarpProcess();
                    else
                        await OnDefaultWarpProcess();

                    // 자동이동 중이었다면 워프 이후에 자동이동 해줌
                    if (isAutoMove)
                        MissionManager.Instance.OnEventAutoMove(false);

                    await TownSceneManager.Instance.UpdateLobbyMenu();
                    break;
                }

            case FlowState.Battle:
                {
                    await OnBattleProcess();
                    break;
                }

            case FlowState.BeforeMissionUpdate:
                {
                    await OnUpdateMission();

                    if (TownSceneManager.Instance == null)
                        break;

                    await TownMiniMapManager.Instance.UpdateMission(Model.BeforeMission);
                    ObserverManager.NotifyObserver(MissionObserverID.Update, null); // 기타 UI들 갱신
                    break;
                }

            case FlowState.AfterMissionUpdate:
                {
                    await TownSceneManager.Instance.UpdateMission(Model.AfterMission, OnEventChangeEpisodeWarp);
                    break;
                }

            case FlowState.Tutorial:
                {
                    await TutorialStepAsync(Model.Tutorial.tutorialExplain);
                    break;
                }

            case FlowState.Home:
                {
                    await UIManager.Instance.BackToTarget(UIType.LobbyView, false);
                    break;
                }
        }
    }

    private async UniTask OnEventChangeEpisodeWarp(StoryQuest storyQuest)
    {
        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
        StoryQuestTableData storyQuestData = storyQuestTable.GetDataByID(storyQuest.NextDataQuestId);

        if (storyQuestData.IsNull())
            return;

        if (storyQuestData.GetQUEST_START_WARP_POINT() == 0)
            return;

        NpcInteractionTable interactionTable = TableManager.Instance.GetTable<NpcInteractionTable>();
        NpcInteractionTableData interactionData = interactionTable.GetDataByID(storyQuestData.GetQUEST_START_WARP_POINT());
        if (interactionData.IsNull())
            return;

        var warpFieldMap = (FieldMapDefine)interactionData.GetWARP_TARGET_FIELD_MAP_ID();
        var warpTransition = (Transition)interactionData.GetEND_TRANSITION();
        var warpTargetObjectType = TownObjectType.None;
        int warpTargetDataId = interactionData.GetWARP_TARGET_NPC_ID(0);
        var warpTargetKey = string.Empty;

        switch ((CommonTownObjectType)interactionData.GetWARP_TARGET_NPC_TYPE())
        {
            case CommonTownObjectType.Npc:
                {
                    warpTargetObjectType = TownObjectType.Npc;
                    warpTargetKey = ((NpcDefine)warpTargetDataId).ToString();
                }
                break;

            case CommonTownObjectType.Gimmick:
                {
                    warpTargetObjectType = TownObjectType.Gimmick;
                    warpTargetKey = ((GimmickDefine)warpTargetDataId).ToString();
                }
                break;

            case CommonTownObjectType.Trigger:
                {
                    warpTargetObjectType = TownObjectType.Trigger;
                    warpTargetKey = ((TriggerDefine)warpTargetDataId).ToString();
                }
                break;

            default:
                {
                    if (warpFieldMap > 0)
                    {
                        warpTargetObjectType = TownObjectType.WarpPoint;

                        if (!string.IsNullOrEmpty(interactionData.GetWARP_POINT()))
                            warpTargetKey = interactionData.GetWARP_POINT();
                        else
                            warpTargetKey = StringDefine.TOWN_OBJECT_TARGET_PORTAL_DEFAULT;
                    }
                    else
                    {
                        warpTargetObjectType = TownObjectType.None;
                        warpTargetKey = null;
                    }
                }
                break;
        }

        if (storyQuestData.GetCHANGE_CHARACTER() > 0)
            PlayerManager.Instance.SetTempLeaderCharacter(storyQuestData.GetCHANGE_CHARACTER());

        Model.SetWarp(new TownFlowModel.WarpInfo(warpFieldMap.ToString(), warpTargetKey, warpTargetObjectType, Transition.None, false));
        await FlowManager.Instance.ChangeStateProcess(FlowState.Warp);
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;
        await TownSceneManager.Instance.TutorialStepAsync(stepType);
    }

    #region 이벤트
    /// <summary>
    /// 미션 처리가 있을 때의 워프 로직
    /// </summary>
    private async UniTask OnMissionWarpProcess()
    {
        if (Model.IsWarpProcess)
        {
            IronJade.Debug.LogError("아직 워프 중인데?");
            return;
        }

        // 워프 시작
        Model.SetWarpProcess(true);
        TownSceneManager.Instance.TownInputSupport.SetInput(false); // 플레이어의 입력을 막는다.

        // 플레이어를 상호작용 상태로 만듬
        PlayerManager.Instance.MyPlayer.SetInteracting(true);

        // 워프할 때 오브젝트들을 갱신하므로 미션 관련 갱신 로직은 취소함.
        MissionManager.Instance.ResetDelayedNotifyUpdateQuest();

        // 미션 워프 상태 추가
        MissionManager.Instance.SetWaitWarpProcessState(true);

        // 미션 프로세스 시작
        bool hasQuestEndStory = MissionManager.Instance.CheckHasDelayedStory(false);

        // 퀘스트의 종료 스토리가 있으면 보여준 후 화면을 가린다.
        if (MissionManager.Instance.IsShowWarpNextStory)
        {
            MissionManager.Instance.ShowWarpNextStory(false);
            System.Func<UniTask> onEventLoading = async () =>
            {
                // 모든 UI를 날린다.
                await OnEventWarpHome();
                await OnWarpProcess();
            };
            await StorySceneManager.Instance.PlayMissionStory(true, onEventLoading);
        }
        else if (hasQuestEndStory)
        {
            System.Func<UniTask> onEventLoading = async () =>
            {
                // 모든 UI를 날린다.
                await OnEventWarpHome();
                await OnWarpProcess();
            };
            await StorySceneManager.Instance.PlayMissionStory(false, onEventLoading);
        }
        else
        {
            // 없으면, 바로 화면을 가린다.
            TransitionType transitionType = TransitionManager.ConvertTransitionType(Model.Warp.transition);
            await TransitionManager.In(transitionType);

            // 모든 UI를 날린다.
            await OnEventWarpHome();
            await OnWarpProcess();

            await TransitionManager.Out(transitionType);
        }

        // 미션 관련 상태 확인
        bool hasQuestStartStory = MissionManager.Instance.CheckHasDelayedStory(true);
        bool hasQuestResultPopup = MissionManager.Instance.CheckHasDelayedPopup();

        // 미션 결과 팝업이 있으면 트랜지션 후 보여준다.
        if (hasQuestResultPopup)
        {
            await MissionManager.Instance.StartDelayedPopupProcess();
        }

        // 미션의 시작 스토리가 있는 경우 상황에 맞게 트랜지션 조작 후 보여준다.
        if (hasQuestStartStory)
        {
            await StorySceneManager.Instance.PlayMissionStory(true, async () =>
            {
            });
        }

        MissionManager.Instance.ResetMissionBehavior();

        // 상호작용 끝
        await TownSceneManager.Instance.TalkInteracting(false);

        // 워프 종료
        Model.SetWarpProcess(false);
        ObserverManager.NotifyObserver(FlowObserverID.OnEndChangeScene, null);
        TownSceneManager.Instance.ShowFieldMapName(Model.CurrentFiledMapDefine);

        // 미션 워프 상태 제거
        MissionManager.Instance.SetWaitWarpProcessState(false);

        // 플레이어를 상호작용 상태로 만듬
        PlayerManager.Instance.MyPlayer.SetInteracting(false);

        if (MissionManager.Instance.CheckHasDelayedInstantTalk())
        {
            await MissionManager.Instance.SendDelayedInstantMessage();
            await TownSceneManager.Instance.ChangeViewAsync(UIType.LobbyView, UIType.None);
            TutorialManager.Instance.SetPlayableTutorial(UIType.LobbyView);

            // MissionManager가 None을 찍고나서 다음 프로세스를 진행하는 경우가 있어서 딜레이 프로세스가 완료된 이후 튜토리얼이 진행되도록 수정
            System.Action taskTutorial = async () =>
            {
                await UniTask.WaitUntil(() => !MissionManager.Instance.CheckHasDelayed());
                await UniTask.WaitUntil(() => !MissionManager.Instance.IsWait);
                TutorialManager.Instance.PlayNextTutorial();

                TownSceneManager.Instance.TownInputSupport.SetInput(true);
            };
            taskTutorial.Invoke();
        }

        await TownSceneManager.Instance.PlayLobbyMenu(true);
    }

    /// <summary>
    /// 기본 워프 로직
    /// </summary>
    private async UniTask OnDefaultWarpProcess()
    {
        TransitionType transitionType = TransitionManager.ConvertTransitionType(Model.Warp.transition);
        await TransitionManager.In(transitionType);

        // 플레이어를 상호작용 상태로 만듬
        PlayerManager.Instance.MyPlayer.SetInteracting(true);

        // 모든 UI를 날린다.
        await OnEventWarpHome();
        await OnWarpProcess();

        await TransitionManager.Out(transitionType);

        // 워프 종료
        Model.SetWarpProcess(false);
        ObserverManager.NotifyObserver(FlowObserverID.OnEndChangeScene, null);
        TownSceneManager.Instance.ShowFieldMapName(Model.CurrentFiledMapDefine);

        // 플레이어를 상호작용 상태로 만듬
        PlayerManager.Instance.MyPlayer.SetInteracting(false);
        await TownSceneManager.Instance.PlayLobbyMenu(true);
    }

    /// <summary>
    /// 워프 처리 (씬 이동 또는 위치 이동)
    /// </summary>
    private async UniTask OnWarpProcess()
    {
        TownObjectType targetTownObjectType = Model.Warp.targetTownObjectType;
        string targetDataFieldMapEnumId = Model.Warp.targetDataFieldMapEnumId;
        string targetDataEnumId = Model.Warp.targetDataEnumId;
        bool isChangeBackground = !Model.CurrentFiledMapDefine.ToString().Equals(targetDataFieldMapEnumId);

        if (isChangeBackground)
        {
            // 임시 리더캐릭터 리셋
            PlayerManager.Instance.ResetTempLeaderCharacter();

            // 모든 TownObject의 태그를 날린다.
            TownObjectManager.Instance.OnEventClearTownTag();

            // 모든 TownObject를 날린다.
            TownObjectManager.Instance.ClearTownObjects();

            // 타운 씬을 메인으로 바꾸고 배경 씬을 새롭게 로드한다.
            // 배경 씬이 로드되면 배경 씬을 메인으로 바꾼다. (그래픽 효과는 메인 씬에서만 작동한다)
            SceneManager.SetActiveScene(UtilModel.Resources.GetScene(StringDefine.SCENE_TOWN));
            await UtilModel.Resources.UnLoadSceneAsync(Model.CurrentScene);

            // 어드레서블 정리 추가
            await UtilModel.Resources.UnloadUnusedAssets(true);

            Model.SetCurrentScene(targetDataFieldMapEnumId);
            Scene scene = await UtilModel.Resources.LoadSceneAsync(Model.CurrentScene, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }

        // 필수 서버 API를 호출한다. (미션, 기믹, 우편 등등)
        await RequestDataFromNetwork();

        if (isChangeBackground)
        {
            // 배경 씬을 활성화하고 NPC 정보를 로드한다.
            BackgroundSceneManager.Instance.ShowTownGroup(true);
            BackgroundSceneManager.Instance.AddTownObjects(Model.CurrentFiledMapDefine);
        }

        // 플레이어를 생성한다.
        await PlayerManager.Instance.LoadMyPlayerCharacterObject();
        await PlayerManager.Instance.MyPlayer.TownPlayer.OnEventCollisionExit(null);
        await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

        // Town 정보를 설정한다.
        TownSceneManager.Instance.SetEventInteraction(OnEventCheckInteraction); // 상호작용 이벤트 등록

        // 배경 씬의 NPC와 Deco를 활성화한다.
        BackgroundSceneManager.Instance.SetCinemachineFollowTarget();   // 배경에 있는 시네머신 카메라 타겟에 플레이어를 넣는다.

        if (isChangeBackground)
        {
            BackgroundSceneManager.Instance.OperateTownDecoratorFactory();
            TownObjectManager.Instance.OperateDecoratorFactory();// 데코레이터 공장을 가동한다.
            await TownObjectManager.Instance.StartProcessAsync();// NPC 가동
        }
        else
        {
            await TownObjectManager.Instance.RefreshProcess();// NPC 가동
        }

        TownObjectManager.Instance.SetConditionRoad();

        // 플레이어의 위치를 이동시키고 활성화 한다.
        PlayerManager.Instance.MyPlayer.SetCurrentFieldMap(Model.CurrentScene, Model.CurrentFiledMapDefine);
        PlayerManager.Instance.SetMyPlayerSpawn(targetTownObjectType, targetDataEnumId, isLeaderCharacterPosition: false, isUpdateTargetPosition: true);
        await PlayerManager.Instance.MyPlayer.TownPlayer.RefreshProcess();

        // 플레이어의 위치 정보를 서버에 저장한다.
        var LeaderCharacterPosition = PlayerManager.Instance.UserSetting.GetUserSettingData<LeaderCharacterPositionUserSetting>();
        LeaderCharacterPosition.SetScenePath(targetDataFieldMapEnumId);
        LeaderCharacterPosition.SetPosition(PlayerManager.Instance.MyPlayer.TownPlayer);
        await PlayerManager.Instance.UserSetting.SetUserSettingData(UserSettingModel.Save.Server, LeaderCharacterPosition);

        // Town 관련 UI를 처리한다.
        await TownMiniMapManager.Instance.ShowTownMinimap(); // 미니맵을 보여준다.
        await TownSceneManager.Instance.RefreshAsync(Model.CurrentUI);

        // 추적 퀘스트 아이콘을 보여준다.
        MissionManager.Instance.NotifyStoryQuestTracking();

        // 플레이어 입력을 풀어준다.
        TownSceneManager.Instance.TownInputSupport.SetInput(true);
        TownSceneManager.Instance.SetTownInput(CameraManager.Instance.TownCamera);
        OnEventChangeLiveVirtualCinemachineCamera();

        if (isChangeBackground)
        {
            // BGM을 켜준다.
            TownSceneManager.Instance.PlayBGM();

            // 현재 필드에 해당하는 볼륨을 켜준다.
            ChangeVolume();
        }

        // 상호작용 시작
        await TownSceneManager.Instance.TalkInteracting(false);

        // 카메라 블랜딩을 기다린다.
        await CameraManager.Instance.WaitCinemachineClearShotBlend();
    }

    /// <summary>
    /// 전투 진입 처리
    /// </summary>
    private async UniTask OnBattleProcess()
    {
        BattleFlowModel battleFlowModel = new BattleFlowModel();
        battleFlowModel.SetBattleInfo(Model.BattleInfo);
        await FlowManager.Instance.ChangeFlow(FlowType.BattleFlow, battleFlowModel, isStack: false);
    }
    /// <summary>
    /// Home 처리
    /// </summary>
    private async UniTask OnEventHome()
    {
        // 홈 처리
        StopAutoMove();
        UIManager.Instance.Home();
        TownObjectManager.Instance.SetConditionRoad();
        await UtilModel.Resources.UnloadUnusedAssets(true);
        await RequestDataFromNetwork();
    }

    /// <summary>
    /// Warp할 때 Home 처리
    /// </summary>
    private async UniTask OnEventWarpHome()
    {
        StopAutoMove();
        UIManager.Instance.Home();
        TownObjectManager.Instance.SetConditionRoad();
        await UtilModel.Resources.UnloadUnusedAssets(true);
    }

    /// <summary>
    /// UI 전환 시 상태 처리
    /// </summary>
    private async UniTask OnEventChangeState(UIState state, UISubState subState, UIType prevUIType, UIType uiType)
    {
        // 로비로 진입할 때
        // 1. 미션 컨디션 체크
        // 2. 튜토리얼 컨디션 체크
        // 3. 기타 컨디션 체크

        IronJade.Debug.LogError($"OnEventChangeState, state: {state}, subState: {subState}, prevUIType: {prevUIType}, uiType: {uiType}");

        StopAutoMove();

        switch (state)
        {
            // 입장할 때와 퇴장할 때로 선 구분
            case UIState.Enter:
                {
                    switch (subState)
                    {
                        case UISubState.Enter:
                            {
                                MissionManager.Instance.UpdateMission();

                                if (uiType != UIType.ManualAlarmPopup && uiType != UIType.LobbyView)
                                {
                                    if (UIManager.Instance.CheckOpenUI(UIType.ManualAlarmPopup))
                                    {
                                        var controller = UIManager.Instance.GetController(UIType.ManualAlarmPopup);
                                        UIManager.Instance.Exit(controller).Forget();
                                    }
                                }
                                break;
                            }

                        case UISubState.BeforeLoading:
                            {
                                bool isAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, uiType);
                                TransitionType transitionType = GetEnterTransitionType(state, prevUIType, uiType);

#if UNITY_ANDROID || UNITY_IOS
                                SettingViewModeType settingViewModeType = GameSettingManager.Instance.GraphicSettingModel.GetOrientationViewModeType();

                                if (GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode() && !DeviceRenderQuality.IsTablet)
                                {
                                    // 미션 보상 중이 아닐 때
                                    if (!MissionManager.Instance.IsWait && !TutorialManager.Instance.CheckTutorialPlaying())
                                    {
                                        if (uiType == UIType.ApplicationPopup || uiType < UIType.MaxView)
                                        {
                                            bool isApplicationContents = UIManager.Instance.CheckApplication(uiType);
                                            bool isAdditiveStageDungeon = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.StageDungeonBackground, uiType);
                                            bool isAdditiveNetMining = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.NetMiningProgress, uiType);

                                            // 현재 UI가 휴대폰이면 자유롭게 전환 가능
                                            if (isAdditiveApplication ||
                                                uiType == UIType.CharacterIntroduceView ||
                                                (isApplicationContents && !isAdditiveStageDungeon && !isAdditiveNetMining))
                                            {
                                                Screen.autorotateToLandscapeRight = false;
                                                Screen.autorotateToPortraitUpsideDown = false;
                                                settingViewModeType = SettingViewModeType.Auto;
                                            }
                                            else
                                            {
                                                // 직전 UI가 휴대폰인데 현재 UI가 스위핑이면 Rotation 트랜지션
                                                bool isPrevAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, prevUIType);
                                                if (isPrevAdditiveApplication && (uiType == UIType.StageDungeonView || uiType == UIType.NetMiningView))
                                                {
                                                    transitionType = TransitionType.Rotation;
                                                    settingViewModeType = SettingViewModeType.Horizontal;
                                                }
                                            }
                                        }
                                    }
                                }

                                await TransitionManager.In(transitionType);

                                if (settingViewModeType == SettingViewModeType.Auto)
                                    Screen.orientation = ScreenOrientation.AutoRotation;
                                else
                                    await GameSettingManager.Instance.GraphicSettingModel.TemporaryViewMode(settingViewModeType);
#else
                                await TransitionManager.In(transitionType);
#endif

                                if (isAdditiveApplication)
                                    TownSceneManager.Instance.ShowTownCanvas();

                                if (uiType != UIType.LobbyView)
                                {
                                    if (!MissionManager.Instance.IsWait)
                                        await TownSceneManager.Instance.PlayLobbyMenu(false);
                                }

                                await AdditivePrefabManager.Instance.LoadApplicationUnit(uiType);

                                CameraManager.Instance.SetAdditivePrefabCameraState(uiType);
                                UIManager.Instance.EnableApplicationFrame(uiType);
                                UIManager.Instance.ChangeResolution(uiType);
                                break;
                            }

                        case UISubState.AfterLoading:
                            {
                                TutorialManager.Instance.SetPlayableTutorial(uiType);
                                PlayerManager.Instance.UpdateLeaderCharacterPosition();

                                if (uiType < UIType.MaxView)
                                {
                                    await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
                                    await BackgroundSceneManager.Instance.ChangeViewAsync(uiType, prevUIType);
                                    await TownSceneManager.Instance.ChangeViewAsync(uiType, prevUIType);
                                    TownObjectManager.Instance.SetConditionRoad();

                                    // npc 대화 종료할 때 퀘스트 갱신 유무 체크
                                    if (prevUIType == UIType.DialogPopup)
                                        await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(true);
                                }
                                else
                                {
                                    await BackgroundSceneManager.Instance.ChangePopupAsync(uiType, prevUIType);
                                    await TownSceneManager.Instance.ChangePopupAsync(uiType, prevUIType);
                                }
                                break;
                            }

                        case UISubState.Finished:
                            {
                                if (UIManager.Instance.CheckOpenUI(UIType.LobbyView))
                                {
                                    // 현재 필드에 해당하는 볼륨을 켜준다.
                                    ChangeVolume();
                                }

                                TransitionType transitionType = GetEnterTransitionType(state, prevUIType, uiType);

                                //튜토리얼이 켜져 있다면 최상위 depth로 올려준다. (UI 이동 도중에 팝업이 올라온 뒤 UISubState.Finished가 호출된다.
                                if (UIManager.Instance.CheckOpenUI(UIType.TutorialPopup))
                                {
                                    BaseController tutorialController = UIManager.Instance.GetNoneStackController(UIType.TutorialPopup);
                                    tutorialController.GetPrefab().transform.SetAsLastSibling();
                                }

#if UNITY_ANDROID || UNITY_IOS
                                if (GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode() && !DeviceRenderQuality.IsTablet)
                                {
                                    // 미션 보상 중이 아닐 때
                                    if (!MissionManager.Instance.IsWait && !TutorialManager.Instance.CheckTutorialPlaying())
                                    {
                                        // 직전 UI가 로비가 아니고 현재 UI가 스위핑인 경우 바로가기를 통해 이동한 것으로 Rotation 트랜지션
                                        bool isPrevLobbyView = prevUIType == UIType.LobbyView;
                                        if (!isPrevLobbyView && uiType == UIType.StageDungeonView)
                                            transitionType = TransitionType.Rotation;
                                    }
                                }
#endif

                                await TransitionManager.Out(transitionType);

                                //타 Flow에서 로비로 온 경우
                                if (uiType == UIType.LobbyView && prevUIType == UIType.None)
                                    break;

                                if (TutorialManager.Instance.HasPlayableTutorial && !MissionManager.Instance.IsWait)
                                {
                                    // MissionManager가 None을 찍고나서 다음 프로세스를 진행하는 경우가 있어서 딜레이 프로세스가 완료된 이후 튜토리얼이 진행되도록 수정
                                    System.Action taskTutorial = async () =>
                                    {
                                        await UniTask.WaitUntil(() => !MissionManager.Instance.CheckHasDelayed());
                                        await UniTask.WaitUntil(() => !MissionManager.Instance.IsWait);
                                        TutorialManager.Instance.PlayNextTutorial();
                                    };
                                    taskTutorial.Invoke();
                                }
                                break;
                            }
                    }
                    break;
                }

            default:
                {
                    switch (subState)
                    {
                        case UISubState.Enter:
                            {
                                MissionManager.Instance.UpdateMission();
                                break;
                            }

                        case UISubState.BeforeLoading:
                            {
                                TransitionType transitionType = GetExitTransitionType(state, prevUIType, uiType);

#if UNITY_ANDROID || UNITY_IOS
                                if (GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode() && !DeviceRenderQuality.IsTablet)
                                {
                                    bool isApplicationContents = UIManager.Instance.CheckApplication(uiType);
                                    bool isAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, uiType);

                                    if (isApplicationContents && isAdditiveApplication)
                                    {
                                        if (transitionType == TransitionType.Tram)
                                            transitionType = TransitionType.Rotation;
                                    }

                                    // 미션 보상 중이 아닐 때
                                    if (!MissionManager.Instance.IsWait)
                                    {
                                        // 현재 UI가 휴대폰일 때
                                        if (isAdditiveApplication)
                                        {
                                            // 직전 UI가 스위핑이면 Rotation 트랜지션
                                            if (prevUIType == UIType.StageDungeonView)
                                                transitionType = TransitionType.Rotation;
                                        }
                                    }
                                }
#endif
                                await TransitionManager.In(transitionType);

                                await AdditivePrefabManager.Instance.LoadApplicationUnit(uiType);
                                UIManager.Instance.ChangeResolution(uiType);
                                break;
                            }

                        case UISubState.AfterLoading:
                            {
                                TutorialManager.Instance.SetPlayableTutorial(uiType);
                                PlayerManager.Instance.UpdateLeaderCharacterPosition();

                                if (uiType < UIType.MaxView)
                                {
                                    IronJade.Debug.Log($"[TEST] IsShowNextStory1111 : {MissionManager.Instance.IsShowWarpNextStory}, WaitState :{MissionManager.Instance.WaitState}");

                                    UIManager.Instance.EnableApplicationFrame(uiType);
                                    await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
                                    CameraManager.Instance.SetAdditivePrefabCameraState(uiType);
                                    await BackgroundSceneManager.Instance.ChangeViewAsync(uiType, prevUIType);

                                    // 리소스 언로드 때문에 렉 걸려서 타이밍을 수정함
                                    await TownSceneManager.Instance.ChangeViewAsync(uiType, prevUIType);
                                    TownObjectManager.Instance.SetConditionRoad();

                                    if (uiType == UIType.LobbyView)
                                    {
                                        await UtilModel.Resources.UnloadUnusedAssets(true);
                                        await RequestDataFromNetwork();

                                        IronJade.Debug.Log($"[TEST] IsShowNextStory2222 : {MissionManager.Instance.IsShowWarpNextStory}, WaitState :{MissionManager.Instance.WaitState}");

                                        if (!MissionManager.Instance.IsWait &&
                                            !MissionManager.Instance.IsShowWarpNextStory &&
                                            prevUIType != UIType.DialogPopup)
                                        {
                                            await TownSceneManager.Instance.PlayLobbyMenu(true);
                                            await PlayerManager.Instance.LoadMyPlayerCharacterObject();
                                            await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);
                                            PlayerManager.Instance.ShowMyPlayerSpawnEffect(false);
                                            CameraManager.Instance.RestoreDofTarget();
                                        }
                                    }
                                }
                                else
                                {
                                    UIManager.Instance.EnableApplicationFrame(uiType);
                                    await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
                                    await BackgroundSceneManager.Instance.ChangePopupAsync(uiType, prevUIType);
                                    await TownSceneManager.Instance.ChangePopupAsync(uiType, prevUIType);
                                }

#if UNITY_ANDROID || UNITY_IOS
                                if (GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode() && !DeviceRenderQuality.IsTablet)
                                {
                                    // 미션 보상 중이 아닐 때
                                    if (!MissionManager.Instance.IsWait && !TutorialManager.Instance.CheckTutorialPlaying())
                                    {
                                        bool isAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, uiType);

                                        if (uiType == UIType.ApplicationPopup || uiType < UIType.MaxView)
                                        {
                                            bool isApplicationContents = UIManager.Instance.CheckApplication(uiType);
                                            bool isAdditiveStageDungeon = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.StageDungeonBackground, uiType);
                                            bool isAdditiveNetMining = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.NetMiningProgress, uiType);

                                            // 현재 UI가 휴대폰이면 세로모드
                                            if (isAdditiveApplication ||
                                                uiType == UIType.CharacterIntroduceView ||
                                                (isApplicationContents && !isAdditiveStageDungeon && !isAdditiveNetMining))
                                            {
                                                Screen.autorotateToLandscapeRight = false;
                                                Screen.autorotateToPortraitUpsideDown = false;
                                                Screen.orientation = ScreenOrientation.AutoRotation;
                                            }
                                            else
                                            {
                                                await GameSettingManager.Instance.GraphicSettingModel.TemporaryViewMode(SettingViewModeType.Horizontal);
                                            }
                                        }
                                        else if (UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView) && !isAdditiveApplication)
                                        {
                                            await GameSettingManager.Instance.GraphicSettingModel.TemporaryViewMode(SettingViewModeType.Horizontal);
                                        }
                                    }
                                }
#endif
                                break;
                            }

                        case UISubState.Finished:
                            {
                                bool isAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, uiType);
                                if (UIManager.Instance.CheckOpenUI(UIType.LobbyView) || isAdditiveApplication)
                                {
                                    // 현재 필드에 해당하는 볼륨을 켜준다.
                                    ChangeVolume();
                                }

                                TransitionType transitionType = GetExitTransitionType(state, prevUIType, uiType);

                                //튜토리얼이 켜져 있다면 최상위 depth로 올려준다. (UI 이동 도중에 팝업이 올라온 뒤 UISubState.Finished가 호출된다.
                                if (UIManager.Instance.CheckOpenUI(UIType.TutorialPopup))
                                {
                                    BaseController tutorialController = UIManager.Instance.GetNoneStackController(UIType.TutorialPopup);
                                    tutorialController.GetPrefab().transform.SetAsLastSibling();
                                }

#if UNITY_ANDROID || UNITY_IOS
                                if (GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode() && !DeviceRenderQuality.IsTablet)
                                {
                                    // 미션 보상 중이 아닐 때
                                    if (!MissionManager.Instance.IsWait)
                                    {
                                        bool isApplicationContents = UIManager.Instance.CheckApplication(uiType);

                                        // 직전 UI가 스위핑이고 현재 UI가 Lobby가 아니라면 바로가기를 통해 이동했다가 복귀한 것으로 Rotation 트랜지션
                                        bool isPrevStageDungeonView = prevUIType == UIType.StageDungeonView;
                                        bool isLobbyView = uiType == UIType.LobbyView;
                                        if (isPrevStageDungeonView && !isLobbyView)
                                            transitionType = TransitionType.Rotation;
                                    }
                                }
#endif

                                if (MissionManager.Instance.IsWait)
                                {
                                    IronJade.Debug.Log($"[TEST] MissionManager.Instance.IsWait");

                                    await TransitionManager.Out(transitionType);

                                    if (TutorialManager.Instance.HasPlayableTutorial)
                                    {
                                        // MissionManager가 None을 찍고나서 다음 프로세스를 진행하는 경우가 있어서 딜레이 프로세스가 완료된 이후 튜토리얼이 진행되도록 수정
                                        System.Action taskTutorial = async () =>
                                        {
                                            await UniTask.WaitUntil(() => !MissionManager.Instance.CheckHasDelayed());
                                            await UniTask.WaitUntil(() => !MissionManager.Instance.IsWait);
                                            TutorialManager.Instance.PlayNextTutorial();
                                        };
                                        taskTutorial.Invoke();
                                    }
                                    return;
                                }

                                // 워프없이 미션 후처리 과정이 있는 경우
                                if (!MissionManager.Instance.CheckHasWarpProcessByBehaviorBuffer() &&
                                    MissionManager.Instance.CheckHasDelayed())
                                {
                                    await MissionManager.Instance.StartAutoProcess(transitionType);
                                    TransitionManager.LoadingUI(false, false);

                                    if (TutorialManager.Instance.HasPlayableTutorial && !MissionManager.Instance.IsWait)
                                        TutorialManager.Instance.PlayNextTutorial();
                                }
                                else
                                {
                                    await TransitionManager.Out(transitionType);

                                    if (TutorialManager.Instance.HasPlayableTutorial && !MissionManager.Instance.IsWait)
                                        TutorialManager.Instance.PlayNextTutorial();
                                }
                                break;
                            }
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// UI 입장 시 상황별 트랜지션 타입을 얻는다.
    /// </summary>
    private TransitionType GetEnterTransitionType(UIState state, UIType prevUIType, UIType uiType)
    {
        // 팝업으로 이동하는 경우 트랜지션을 보여주지 않는다.
        if (uiType > UIType.MaxView)
            return TransitionType.None;

        // 1. 스위핑으로 입장
        bool isTram = uiType == UIType.StageDungeonView;
        if (isTram)
            return TransitionType.Tram;

        // 1. 넷마이닝으로 입장
        // 2. 도감 뷰모드로 입장
        // 3. 도감 뷰모드에서 커넥트로 입장
        bool isRotation = uiType == UIType.NetMiningView ||
                          uiType == UIType.CharacterIntroduceView ||
                          uiType == UIType.HousingSimulationView ||
                          (prevUIType == UIType.CharacterIntroduceView && uiType == UIType.CharacterDetailView);

        if (isRotation)
            return TransitionType.Rotation;

        return TransitionType.None;
    }

    /// <summary>
    /// UI 퇴장 시 상황별 트랜지션 타입을 얻는다.
    /// </summary>
    private TransitionType GetExitTransitionType(UIState state, UIType prevUIType, UIType uiType)
    {
        // 툰스크린 추가 조건
        // Npc 대화에서 로비로 진입할 때 미션 업데이트 후 워프 없이 종료 스토리가 있는 경우
        if (prevUIType == UIType.DialogPopup && uiType == UIType.LobbyView)
        {
            //프롤로그 대화(사장과의 대화)에서는 Dialog->Lobby로 넘어가도 트랜지션이 없다.
            if (PrologueManager.Instance.IsProgressing)
                return TransitionType.None;

            var result = MissionManager.Instance.GetExpectedResultByStoryQuest();
            if (!result.HasWarp && (result.HasEndStory || MissionManager.Instance.CheckHasDelayedStory(false)))
                return TransitionManager.ConvertTransitionType(result.Transition);// TransitionType.Rotation;
        }

        // 직전 UI가 팝업이면 트랜지션을 보여주지 않는다.
        if (prevUIType > UIType.MaxView)
            return TransitionType.None;

        // 1. 스위핑에서 퇴장
        bool isTram = prevUIType == UIType.StageDungeonView;
        if (isTram)
            return TransitionType.Tram;

        // 1. 넷마이닝에서 퇴장
        // 2. 도감 뷰모드에서 퇴장
        bool isRotation = prevUIType == UIType.NetMiningView ||
                          prevUIType == UIType.CharacterIntroduceView ||
                          prevUIType == UIType.HousingSimulationView ||
                          (prevUIType == UIType.CharacterDetailView && uiType == UIType.CharacterIntroduceView);

        if (isRotation)
            return TransitionType.Rotation;

        return TransitionType.None;
    }

    /// <summary>
    /// 카메라 전환 시 이벤트
    /// </summary>
    protected void OnEventChangeLiveVirtualCinemachineCamera()
    {
        try
        {
            bool isMoveInput = PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.MoveData.IsMoveInput;
            TownInputSupport townInputSupport = TownSceneManager.Instance.TownInputSupport;

            //if (isMoveInput)
            //    return;

            townInputSupport.SetVirtualJoystickLookAtTarget(CameraManager.Instance.TownCamera,
                                                            CameraManager.Instance.RecomposerPan);

        }
        catch
        {
        }
    }

    /// <summary>
    /// 미션 업데이트
    /// </summary>
    private async UniTask OnUpdateMission()
    {
        if (BackgroundSceneManager.Instance == null || !BackgroundSceneManager.Instance.CheckActiveTown())
            return;

        // 미션 업데이트 프로세스 진행
        MissionManager.Instance.SetWaitUpdateTownState(true);

        try
        {
            if (PlayerManager.Instance.MyPlayer.TownPlayer == null)
            {
                MissionManager.Instance.SetWaitUpdateTownState(false);
                return;
            }

            // 타운 오브젝트 갱신
            await TownObjectManager.Instance.RefreshProcess(true);
            await PlayerManager.Instance.MyPlayer.TownPlayer.RefreshProcess();

            // TownObject들의 갱신이 완료된 후
            // 행동 트리 갱신 타이밍을 맞추기 위해 한 프레임 쉰다.
            // (행동트리를 통해 TownObject의 위치가 갱신되는데, 자동이동 시 한 프레임 느림)
            await UniTask.NextFrame();
        }
        finally
        {
            MissionManager.Instance.SetWaitUpdateTownState(false);
        }
    }

    /// <summary>
    /// 상호작용 (조건에 따라 일반 또는 프롤로그 상호작용 시작)
    /// </summary>
    protected async UniTask OnEventCheckInteraction(TownTalkInteraction townTalkInteraction)
    {
        if (Model.IsOffline && PrologueManager.Instance.IsProgressing)
            await OnEventPrologueInteraction(townTalkInteraction);
        else
            await OnEventTalkInteraction(townTalkInteraction);
    }

    /// <summary>
    /// 상호작용 (일반)
    /// </summary>
    private async UniTask OnEventTalkInteraction(TownTalkInteraction townTalkInteraction)
    {
        // 캐릭터가 움직이고 있으면 패스
        if (PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.MoveData.IsMoveInput)
            return;

        // 플레이어를 상호작용 상태로 만듬
        PlayerManager.Instance.MyPlayer.SetInteracting(townTalkInteraction.IsTalk);

        // 상호작용 시작
        await TownSceneManager.Instance.TalkInteracting(townTalkInteraction.IsTalk);

        if (!townTalkInteraction.IsTalk)
            return;

        await MissionManager.Instance.SetTrackingDataId(BaseStoryQuest.TrackingDataId);

        await PlayerManager.Instance.MyPlayer.TownPlayer.RefreshProcess();

        // 상호작용 중에 갑자기 다른 팝업(메시지박스 라던지)이 뜨면 상호작용 취소
        if (!UIManager.Instance.CheckOpenCurrentUI(UIType.LobbyView))
        {
            PlayerManager.Instance.MyPlayer.SetInteracting(false);
            await TownSceneManager.Instance.TalkInteracting(false);
            return;
        }

        ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByEnumId(townTalkInteraction.TalkTarget.targetEnumId);

        switch (townSupport.TownObjectType)
        {
            case TownObjectType.Npc:
                {
                    if (townSupport.TownObjectWarpType == TownObjectWarpType.None ||
                        townSupport.TownObjectWarpType == TownObjectWarpType.JohnCooper)
                    {
                        var interactions = townSupport.GetCurrentInteractions();
                        BaseTownInteraction firstInteraction = null;// interactions != null ? interactions.Count > 0 ? interactions[0] : null : null;
                        bool isInstantInteraction = false;// interactions != null && interactions.Count == 1 && firstInteraction.IsInstant;

                        if (interactions != null)
                        {
                            IronJade.Debug.Log($"상호작용 {townSupport.EnumId}, {interactions.Count}");

                            if (interactions.Count == 0)
                            {
                                PlayerManager.Instance.MyPlayer.SetInteracting(false);
                                return;
                            }

                            firstInteraction = interactions[0];
                            isInstantInteraction = interactions.Count == 1 && firstInteraction.IsInstant;
                        }

                        if (isInstantInteraction)
                        {
                            await OnEventStartInteraction(townSupport, firstInteraction);
                            PlayerManager.Instance.MyPlayer.SetInteracting(false);

                            // 현재 로직 상 전투 진입 후 TownSceneManager가 제거되서 에러난다.
                            if (TownSceneManager.Instance != null)
                                await TownSceneManager.Instance.TalkInteracting(false);
                        }
                        else
                        {
                            // 대화창 오픈
                            BaseController baseController = UIManager.Instance.GetController(UIType.DialogPopup);
                            DialogPopupModel model = baseController.GetModel<DialogPopupModel>();
                            model.SetTalkInteraction(townTalkInteraction);
                            model.SetEventInteraction(OnEventTalkInteraction);

                            if (townSupport.TownObjectWarpType == TownObjectWarpType.JohnCooper)
                            {
                                model.SetEventBeforeEnterBattle(async () => { await townSupport.PlayWarpTimelineAsync(TownWarpTimelineState.Exit); });
                            }
                            else
                            {
                                model.SetEventBeforeEnterBattle(null);
                            }

                            await UIManager.Instance.EnterAsync(baseController);

                            PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.SetLookAnimator(townSupport, true);
                            townSupport.TownObject.SetLookAnimator(PlayerManager.Instance.MyPlayer.TownPlayer, true);
                            townSupport.StartTalk();

                            // 대화 카메라로 전환한다.
                            CameraManager.Instance.SetTalkCamera(true);
                            PlayerManager.Instance.MyPlayer.SetTalking(true);

                            if (townSupport.TownObjectWarpType == TownObjectWarpType.JohnCooper)
                            {
                                // 임시 땜빵용, 추후 수정 필요
                                await UniTask.WaitUntil(() => model.State == DialogPopupModel.DialogState.ReadyForInteraction);

                                townSupport.PlayWarpTimelineAsync(TownWarpTimelineState.Open).Forget();
                            }
                        }
                    }
                    // 워프형 npc 의 경우
                    else
                    {
                        await townSupport.PlayWarpTimelineAsync(TownWarpTimelineState.Open);

                        if (townSupport.GetCurrentInteractions().Count > 0)
                        {
                            BaseTownInteraction townInteraction = townSupport.GetCurrentInteractions()[0];

                            if (!string.IsNullOrEmpty(townInteraction.WarpFieldMapEnumId))
                            {
                                bool isMissionWarp = false;

                                if (townInteraction.InteractionType == TownInteractionType.Quest)
                                {
                                    await MissionManager.Instance.AddCountOnCondition(new QuestCondition.TalkNpc(townInteraction.ProgressingDataId, townSupport.DataId, 1));
                                    await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(false);

                                    isMissionWarp = true;
                                }

                                MissionManager.Instance.SetAutoMove(false);

                                string targetDataFieldMapEnumId = townInteraction.WarpFieldMapEnumId;
                                string targetDataEnumId = townInteraction.WarpTargetKey;
                                TownObjectType targetTownObjectType = townInteraction.WarpTargetObjectType;
                                Transition transition = townInteraction.WarpTransition;
                                Model.SetWarp(new TownFlowModel.WarpInfo(targetDataFieldMapEnumId, targetDataEnumId, targetTownObjectType, transition, isMissionWarp));
                                await FlowManager.Instance.ChangeStateProcess(FlowState.Warp);
                            }
                            else
                            {
                                // 여기로 들어오면 안됩니다.
                                TownSceneManager.Instance.TalkInteracting(false).Forget();
                                PlayerManager.Instance.MyPlayer.SetInteracting(false);

                                IronJade.Debug.LogError($"Not found warp location. (NpcInteraction DataID: {townInteraction.DataId})");
                            }
                        }
                        else
                            IronJade.Debug.LogError("Not found interaction.");
                    }
                    break;
                }
        }
    }

    /// <summary>
    /// 상호작용 (프롤로그 상호작용)
    /// </summary>
    private async UniTask OnEventPrologueInteraction(TownTalkInteraction townTalkInteraction)
    {
        ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByEnumId(townTalkInteraction.TalkTarget.targetEnumId);

        if (townSupport == null)
            return;

        int targetId = townTalkInteraction.TalkTarget.targetDataId;

        // 현재 시퀀스의 인터랙션 타겟과 동일하다면
        if (PrologueManager.Instance.IsCurrentInteractionTarget(targetId))
        {
            var interactionInfo = PrologueManager.Instance.GetInteractionTargetInfo();

            PrologueInteractionType interactionType = interactionInfo.InteractionType;

            switch (interactionType)
            {
                case PrologueInteractionType.NPCInteraction:
                    await OnEventTalkInteraction(townTalkInteraction);

                    // 닫힐 때 까지 대기
                    await UniTask.WaitUntil(() => { return !UIManager.Instance.CheckOpenUI(UIType.DialogPopup); });
                    await PrologueManager.Instance.CompleteInteractionAsync(targetId);

                    break;

                default:
                    await PrologueManager.Instance.CompleteInteractionAsync(targetId);
                    break;
            }
        }
    }

    private async UniTask OnEventStartInteraction(ITownSupport townSupport, BaseTownInteraction townInteraction)
    {
        if (Model.IsInstantTalkProcess)
            return;

        Model.SetInstantTalkProcess(true);

        if (townInteraction == null)
        {
            IronJade.Debug.LogError("townInteraction is null");
            return;
        }

        if (townSupport == null)
        {
            IronJade.Debug.LogError("townSupport is null");
            return;
        }

        switch (townInteraction.InteractionType)
        {
            case TownInteractionType.ContentsOpen:
                {
                    await ContentsOpenManager.Instance.ShowContents(townInteraction.ContentsOpenDataId);
                    break;
                }

            case TownInteractionType.Warp:
                {
                    if (string.IsNullOrEmpty(townInteraction.WarpFieldMapEnumId))
                        break;

                    MissionManager.Instance.SetAutoMove(false);

                    string targetDataFieldMapEnumId = townInteraction.WarpFieldMapEnumId;
                    string targetDataEnumId = townInteraction.WarpTargetKey;
                    TownObjectType targetTownObjectType = townInteraction.WarpTargetObjectType;
                    Transition transition = townInteraction.WarpTransition;
                    Model.SetWarp(new TownFlowModel.WarpInfo(targetDataFieldMapEnumId, targetDataEnumId, targetTownObjectType, transition));
                    await FlowManager.Instance.ChangeStateProcess(FlowState.Warp);
                    break;
                }

            case TownInteractionType.Battle:
                {
                    OnEventStartBattle(townInteraction.DungeonId, townInteraction.ProgressingDataId).Forget();
                    break;
                }

            case TownInteractionType.Prologue:
                // 현재 아무 동작 없음
                break;

            default:
                await StartLocalizationAction(townInteraction, townSupport);
                break;
        }

        Model.SetInstantTalkProcess(false);
    }

    /// <summary>
    /// Npc 인터랙션 진행을 위한 전투 진행
    /// </summary>
    private async UniTask OnEventStartBattle(int dataDungeonId, int progressQuestDataId)
    {
        if (dataDungeonId == 0)
        {
            IronJade.Debug.LogError("Cannot proceed because the dungeon data id is 0.");
            return;
        }

        System.Func<UniTask> onEventEnterPopup = async () =>
        {
            BaseController stageInfoController = UIManager.Instance.GetController(UIType.StageInfoWindow);
            StageInfoPopupModel stageInfoPopupModel = stageInfoController.GetModel<StageInfoPopupModel>();
            stageInfoPopupModel.SetCurrentDeckType(DeckType.Story);
            stageInfoPopupModel.SetUser(PlayerManager.Instance.MyPlayer.User);
            stageInfoPopupModel.SetPopupByDungeonDataId(dataDungeonId);
            stageInfoPopupModel.SetHideAfterStart(true);
            stageInfoPopupModel.SetListOpen(true);
            stageInfoPopupModel.SetExStage(false);
            await UIManager.Instance.EnterAsync(stageInfoController);
        };

        DungeonTable dungeonTable = TableManager.Instance.GetTable<DungeonTable>();
        DungeonTableData dungeonData = dungeonTable.GetDataByID(dataDungeonId);

        // 던전에 팀원이 완전 고정인 경우 StageInfoPopup을 스킵하고 즉시 전투 입장.
        // 고정캐릭터 사용 시 팀편성 스킵 - 25.01.17 전현구/염하정
        if (PlayerManager.Instance.CheckFixedAllyTeam(dungeonData))
        {
            if (PlayerManager.Instance.CheckFixedAllyHaveTeam(dungeonData))
            {
                await onEventEnterPopup();
            }
            else
            {
                BattleTeamGeneratorModel battleTeamGenerator = new BattleTeamGeneratorModel();
                Team fixedTeam = battleTeamGenerator.CreateFixedAllyTeamByDungeonData(dungeonData, PlayerManager.Instance.MyPlayer.User);

                BattleInfo battleInfo = BattleInfo.CreateBattleInfo(
                    dungeonData.GetID(),
                    fixedTeam,
                    BattleResultInfoModel.CreateBattleResultInfoModel(),
                    progressQuestDataId,
                    0);

                TownFlowModel townFlowModel = FlowManager.Instance.GetCurrentFlow().GetModel<TownFlowModel>();
                townFlowModel.SetBattleInfo(battleInfo);
                await FlowManager.Instance.ChangeStateProcess(FlowState.Battle);
            }
        }
        else
        {
            await onEventEnterPopup();
        }
    }

    private async UniTask StartLocalizationAction(BaseTownInteraction interaction, ITownSupport townSupport)
    {
        if (interaction == null)
            return;

        bool checkEnd = false;
        Queue<string> interactionIds = new Queue<string>();
        HashSet<string> checkInteractionIds = new HashSet<string>();

        string localizedText = TableManager.Instance.GetLocalization(interaction.InteractionLocalization);
        string interactionEnumId = null;

        // 모든 선택지 탐색
        do
        {
            if (interactionIds.Count > 0)
            {
                interactionEnumId = interactionIds.Dequeue();
                localizedText = TableManager.Instance.GetLocalization(interactionEnumId);
            }

            if (!CheckConvertToScript(localizedText, out DialogScript[] scripts, out DialogTurningPoint[] turningPoints))
                continue;

            foreach (var turningPoint in turningPoints)
            {
                switch (turningPoint.type)
                {
                    case TownInteractionTurningPointType.Talk:
                        {
                            if (checkInteractionIds.Contains(turningPoint.extendedValue))
                                continue;

                            checkInteractionIds.Add(turningPoint.extendedValue);
                            interactionIds.Enqueue(turningPoint.extendedValue);
                        }
                        break;
                    default:
                        {
                            checkEnd = true;
                            await OnEventNextTurningPoint(interaction, turningPoint, townSupport);
                        }
                        return;
                }
            }
        } while (interactionIds.Count > 0);

        // 만약, 마지막 선택지를 찾을 수 없다면 종료
        if (!checkEnd)
        {
            IronJade.Debug.LogError($"마지막 선택지를 찾지 못 했습니다. QuestId:{interaction.ProgressingDataId}, EnumId:{interactionEnumId}, DataId:{interaction.DataId}, Interaction Count:{interactionIds.Count}");
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIAZTION_INTERACTION_DATA_ERROR);
        }
    }

    private bool CheckConvertToScript(string localizedText, out DialogScript[] scripts, out DialogTurningPoint[] turningPoints)
    {
        scripts = null;
        turningPoints = null;

        if (string.IsNullOrEmpty(localizedText))
            return false;

        ScriptConvertModel convertModel = new ScriptConvertModel();

        scripts = convertModel.GetScripts<DialogScript>(localizedText, out List<string> remainTexts);
        turningPoints = convertModel.GetScripts<DialogTurningPoint>(ref remainTexts);

        bool existScripts = scripts != null && scripts.Length > 0;
        bool existTurningPoints = turningPoints != null && turningPoints.Length > 0;

        return existScripts || existTurningPoints;
    }

    private async UniTask OnEventNextTurningPoint(BaseTownInteraction interaction, DialogTurningPoint dialogTurningPoint, ITownSupport townSupport)
    {
        switch (dialogTurningPoint.type)
        {
            case TownInteractionTurningPointType.Battle:
                {
                    if (int.TryParse(dialogTurningPoint.extendedValue, out int dataDungeonId))
                        await OnEventStartBattle(dataDungeonId, interaction.ProgressingDataId);
                    else
                        IronJade.Debug.LogError($"던전 데이터로 변환할 수 없습니다: {dialogTurningPoint.extendedValue}");

                    break;
                }

            case TownInteractionTurningPointType.Questtalk:
                {
                    await TurningQuestProcess(interaction, dialogTurningPoint, townSupport);
                    break;
                }
        }
    }

    private async UniTask TurningQuestProcess(BaseTownInteraction interaction, DialogTurningPoint dialogTurningPoint, ITownSupport townSupport)
    {
        if (interaction == null)
            return;

        if (interaction.InteractionType == TownInteractionType.Quest)
        {
            MissionContentType contentType = interaction.ProgressingMissionType;
            int dataId = interaction.ProgressingDataId;
            MissionProgressState progressState = interaction.ProgressingMissionCheckState;
            bool changeProgress = false;

            if (dataId != 0)
            {
                BaseMission mission = MissionManager.Instance.GetMission(contentType, dataId);

                if (mission == null)
                {
                    IronJade.Debug.LogError($"Mission ({dataId}) is null");
                    return;
                }

                if (progressState != mission.GetMissionProgressState())
                    throw new Exception($"This is an invalid state. DataID: {mission.DataId} => expect: {progressState}, real: {mission.GetMissionProgressState()}");

                switch (progressState)
                {
                    // 미션을 받는다.
                    case MissionProgressState.UnAccepted:
                        {
                            await MissionManager.Instance.RequestAccept(mission);
                            changeProgress = true;
                        }
                        break;

                    // 진행 중이면 진행도를 추가한다.
                    case MissionProgressState.Progressing:
                        {
                            if (townSupport != null)
                            {
                                await MissionManager.Instance.AddCountOnCondition(new QuestCondition.TalkNpc(dataId, townSupport.DataId, 1));
                                changeProgress = true;
                            }
                        }
                        break;

                    // 클리어를 했다면 보상을 받는다.
                    case MissionProgressState.RewardReady:
                        {
                            await MissionManager.Instance.RequestReward(mission);
                            changeProgress = true;
                        }
                        break;

                    // 여기로 들어오면 에러
                    case MissionProgressState.Completed:
                        throw new Exception("This is an incorrect entry. The mission status you are trying to check cannot be completed.");
                }
            }
            else
            {
                IronJade.Debug.LogError($"This is an invalid value. => MissionType: {contentType}, DataId: {dataId}");
            }

            if (changeProgress)
            {
                await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(true);
                await MissionManager.Instance.StartAutoProcess();
            }
        }
    }
    #endregion

    #region 네트워크 처리
    private async UniTask RequestDataFromNetwork()
    {
        if (Model.IsOffline)
            return;

        // 임시 배치
        if (PlayerManager.Instance.MyPlayer.User.SeasonType != SeasonType.First)
            await RequestUpdateSeason(SeasonType.First);

        await RequestNetMining();
        await RequestReddot();

        // 이벤트 정보는 굳이 올때마다 호출하지 말고 10분 단위로 계산해서 갱신하자
        if (TimeManager.Instance.CheckTenMinuteDelayAPI())
            await EventManager.Instance.PacketEventPassGet();

        NoticeManager.Instance.StartNoticeUpdate();
    }

    private async UniTask RequestNetMining()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.AutoBattle))
            return;

        BaseProcess autoBattleGetProcess = NetworkManager.Web.GetProcess(WebProcess.AutoBattleGet);

        if (await autoBattleGetProcess.OnNetworkAsyncRequest())
            autoBattleGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestReddot()
    {
        await MailRedDot();
        //await GuildRedDot();
        //await DispatchRedDot();

        RedDotManager.Instance.Notify();
    }

    private async UniTask RequestUpdateSeason(SeasonType seasonType)
    {
        BaseProcess seasonUpdateProcess = NetworkManager.Web.GetProcess(WebProcess.UserSeasonUpdate);
        seasonUpdateProcess.SetPacket(new UpdateUserSeasonInDto(seasonType));

        if (await seasonUpdateProcess.OnNetworkAsyncRequest())
            seasonUpdateProcess.OnNetworkResponse();
    }

    private async UniTask MailRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Mail))
            return;

        BaseProcess mailGetProcess = NetworkManager.Web.GetProcess(WebProcess.MailGet);
        mailGetProcess.SetLoading(false, false);

        if (await mailGetProcess.OnNetworkAsyncRequest())
            mailGetProcess.OnNetworkResponse();
    }

    private async UniTask DispatchRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Dispatch))
            return;

        BaseProcess dispatchGetProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchGet);
        dispatchGetProcess.SetLoading(false, false);

        if (await dispatchGetProcess.OnNetworkAsyncRequest())
            dispatchGetProcess.OnNetworkResponse();
    }

    private async UniTask GuildRedDot()
    {
        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.IsJoinedGuild)
            return;

        int guildId = PlayerManager.Instance.MyPlayer.User.GuildModel.GuildId;
        BaseProcess memberGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildMemberGet);
        memberGetProcess.SetPacket(new GetGuildMemberInDto(guildId));
        if (await memberGetProcess.OnNetworkAsyncRequest())
            memberGetProcess.OnNetworkResponse();

        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.HasAuthority)
            return;

        BaseProcess signupGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildReceiveSignupGet);
        if (await signupGetProcess.OnNetworkAsyncRequest())
        {
            signupGetProcess.OnNetworkResponse();
            //GuildReceiveSignupGetResponse response = signupGetProcess.GetResponse<GuildReceiveSignupGetResponse>();
            //PlayerManager.Instance.MyPlayer.User.GuildModel.SetNewContents(response.data.LongLength > 0);
        }
    }
    #endregion

    #region 설정
    /// <summary>
    /// 옵저버 등록
    /// </summary>
    private void AddTownObserverIds()
    {
        foreach (var observerId in townObserverIds)
            ObserverManager.AddObserver(observerId, this);
    }

    /// <summary>
    /// 옵저버 해제
    /// </summary>
    private void RemoveTownObserverIds()
    {
        foreach (var observerId in townObserverIds)
            ObserverManager.RemoveObserver(observerId, this);
    }

    /// <summary>
    /// 테이블 로드
    /// </summary>
    protected void LoadTables()
    {
        // 필요한건 나중에 다시 정리해서 기입
        TableManager.Instance.LoadTable<ChattingEmojiTable>();
        TableManager.Instance.LoadTable<NpcConditionTable>();
        TableManager.Instance.LoadTable<NpcInteractionTable>();

        IronJade.Debug.Log("Load Table Complete");
    }

    /// <summary>
    /// 테이블 언로드
    /// </summary>
    private void RemoveTables()
    {
        TableManager.Instance.RemoveTable<NpcConditionTable>();
        TableManager.Instance.RemoveTable<NpcInteractionTable>();
    }

    /// <summary>
    /// 볼륨 설정
    /// </summary>
    private void ChangeVolume(bool isPrevStack = false)
    {
        if (isPrevStack)
        {
            if (UIManager.Instance.CheckOpenCurrentView(UIType.CharacterIntroduceView))
                return;
        }

        var fieldMapTable = TableManager.Instance.GetTable<FieldMapTable>();
        var fieldMapTableData = fieldMapTable.GetDataByID((int)Model.CurrentFiledMapDefine);
        if (fieldMapTableData.IsNull())
        {
            CameraManager.Instance.ChangeVolumeType(VolumeType.Volume_Town_Type1_Films);
            return;
        }

        CameraManager.Instance.ChangeVolumeType(fieldMapTableData.GetSCENE_PATH(), VolumeType.Volume_Town_Type1_Films);
        CameraManager.Instance.ChangeFreeLockVolume();
    }
    #endregion

    #region 자동이동
    private void StopAutoMove()
    {
        IronJade.Debug.Log("TownFlow.StopAutoMove");

        // 아직 플레이어가 없는 상태에서 호출 되었으면 리턴
        if (PlayerManager.Instance.MyPlayer.TownPlayer.CheckSafeNull())
            return;

        CharacterParam characterParam = new CharacterParam();
        characterParam.SetAutoMoveState(CharacterAutoMoveState.Stop);
        ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
    }
    #endregion

    async void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case FlowObserverID.PlayStoryToon:
            case FlowObserverID.PlayCutscene:
                {
                    if (TownSceneManager.Instance == null)
                        return;

                    CutsceneParam cutsceneParam = (CutsceneParam)observerParam;
                    TownSceneManager.Instance.PlayCutsceneState(cutsceneParam.IsShow);

                    if (BackgroundSceneManager.Instance != null)
                        BackgroundSceneManager.Instance.PlayCutsceneState(cutsceneParam.IsShow, cutsceneParam.IsTownCutscene);

                    if (!cutsceneParam.IsShow && cutsceneParam.IsTownRefresh)
                        TownObjectManager.Instance.RefreshProcess().Forget();
                    break;
                }

            case ViewObserverID.Refresh:
                {
                    TownSceneManager.Instance.RefreshAsync(Model.CurrentUI).Forget();
                    break;
                }


            case CharacterObserverID.CharacterMove:
                {
                    BoolParam boolParam = (BoolParam)observerParam;
                    bool isMove = boolParam.Value1;

                    if (PlayerManager.Instance.MyPlayer.IsInteracting)
                        return;

                    await TownSceneManager.Instance.PlayLobbyMenu(!isMove);
                    OnEventChangeLiveVirtualCinemachineCamera();

                    if (isMove && PlayerManager.Instance.MyPlayer.TownPlayer.CheckPathfind())
                        StopAutoMove();

                    MissionManager.Instance.SetAutoMove(false);
                    break;
                }

            case FlowObserverID.PlayCinemachineTimeline:
                {
                    BoolParam boolParam = (BoolParam)observerParam;

                    TownSceneManager.Instance.SetActiveTownInput(!boolParam.Value1);
                    TownSceneManager.Instance.SetUITouchEnable(!boolParam.Value1);

                    if (!boolParam.Value1)
                        TownSceneManager.Instance.RefreshAsync(UIManager.Instance.GetCurrentUIType()).Forget();
                    break;
                }

            case ObserverOverlayCameraID.ChangeStack:
                {
                    IronJade.Debug.Log($"BaseCamera : {OverlayCameraStackController.Instance.BaseCamera}");
                    break;
                }
        }
    }

    #endregion Coding rule : Function
}
