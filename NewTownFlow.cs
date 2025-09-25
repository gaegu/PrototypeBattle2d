// NewTownFlow.cs 수정
using AC;
using Cinemachine;
using Cysharp.Threading.Tasks;
using DG.DemiEditor;
using IronJade.Camera.Core;
using IronJade.Flow.Core;
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class NewTownFlow : BaseFlow, ITownFlow, IObserver
{
    #region Core Properties
    public TownFlowModel Model { get { return GetModel<TownFlowModel>(); } }
    #endregion

    #region Dependencies
    private readonly IServiceContainer serviceContainer;
    private readonly ITownSceneService townSceneService;
    private readonly IPlayerService playerService;
    private readonly ITownObjectService townObjectService;
    private readonly IMissionService missionService;
    private readonly IResourceService resourceService;
    private readonly INetworkService networkService;

    #endregion


    #region State Machine
    private TownStateMachine stateMachine;
    #endregion


    #region Modules
    private ITownLoadingModule loadingModule;
    private ITownWarpModule warpModule;

    private Dictionary<Type, ITownFlowModule> modules;
    #endregion

    #region Module Management
    private void InitializeModules()
    {
        // 모듈 생성
        loadingModule = new TownLoadingModule(serviceContainer);
        warpModule = new TownWarpModule(serviceContainer);

        // Dictionary에 등록
        modules = new Dictionary<Type, ITownFlowModule>
        {
            { typeof(ITownLoadingModule), loadingModule },
            { typeof(ITownWarpModule), warpModule },
        };

        // 모든 모듈 초기화
        foreach (var module in modules.Values)
        {
            if (module != null)
                module.Initialize(Model);
        }

        Debug.Log($"[NewTownFlow] Initialized {modules.Count} modules");
    }


    #endregion

    #region Constructors
    // 기본 생성자 (프로덕션용)
    public NewTownFlow() : this(new TownServiceContainer())
    {

    }

    // 의존성 주입 생성자 (테스트용)
    public NewTownFlow(IServiceContainer container)
    {
        serviceContainer = container;

        // 서비스 가져오기
        townSceneService = container.GetService<ITownSceneService>();
        playerService = container.GetService<IPlayerService>();
        townObjectService = container.GetService<ITownObjectService>();
        missionService = container.GetService<IMissionService>();
        resourceService = container.GetService<IResourceService>();
        // 네트워크 서비스 추가
        networkService = container.GetService<INetworkService>();

        Debug.Log("[NewTownFlow] Initialized with dependency injection");
    }
    #endregion


    #region Observer Management
    private readonly Enum[] townObserverIds =
    {
        FlowObserverID.PlayCutscene,
        FlowObserverID.PlayStoryToon,
        ViewObserverID.Refresh,
        CharacterObserverID.CharacterMove,
        CharacterObserverID.AutoMoveCharacter,
        FlowObserverID.PlayCinemachineTimeline,
        ObserverOverlayCameraID.ChangeStack,
    };

    private void AddTownObserverIds()
    {
        foreach (var observerId in townObserverIds)
        {
            ObserverManager.AddObserver(observerId, this);
            Debug.Log($"[NewTownFlow] Observer registered: {observerId}");
        }
    }

    private void RemoveTownObserverIds()
    {
        foreach (var observerId in townObserverIds)
        {
            ObserverManager.RemoveObserver(observerId, this);
            Debug.Log($"[NewTownFlow] Observer removed: {observerId}");
        }
    }
    #endregion




    #region BaseFlow Implementation
    public override void Enter()
    {
        Debug.Log("[NewTownFlow] Enter - Using Modular Architecture");
        // 모듈 초기화
        InitializeModules();

        // State Machine 초기화 (모듈 전달)
        stateMachine = new TownStateMachine(Model, serviceContainer);

        AddTownObserverIds();

        // 기본 설정
        Model.SetCurrentUI(UIType.None);
        Model.SetCurrentViewUI(UIType.None);

        if (string.IsNullOrEmpty(Model.CurrentScene))
            Model.SetCurrentScene(FieldMapDefine.FIELDMAP_MID_INDISTREET_MLEOFFICEINDOOR_01.ToString());

        GameManager.Instance.ActiveRendererFeatures(false);
        UIManager.Instance.SetEventUIProcess(OnEventHome, OnEventChangeState);



    }


    private async UniTask FirstTownFlowTransition()
    {

      //  Transform parent = BackgroundSceneManager.Instance.TownObjectParent.transform;

      //  await LoadTownTransitionTimeline(parent);

        bool isTouchScreen = false;

        UIManager.Instance.ShowTouchScreen(() => { isTouchScreen = true; });
        TransitionManager.LoadingUI(false, false);

        await UniTask.WaitUntil(() => isTouchScreen);

        UIManager.Instance.HideTouchScreen();

        await TransitionManager.Out(TransitionType.Intro);

       // await PlayTownTransitionTimeline();
    }



    public override async UniTask<bool> Back()
    {
        Debug.Log("[NewTownFlow] Back");
        return await UIManager.Instance.BackAsync();
    }

    public override async UniTask Exit()
    {
        // TownFlow의 Exit 로직 참고하여 작성
        QualitySettings.streamingMipmapsActive = false;

        // StateMachine 정리
        stateMachine?.Dispose();

        // 타운 오브젝트 정리
        TownObjectManager.Instance.DestroyTownObject();
        TownObjectManager.Instance.ClearTownObjects();
        TownObjectManager.Instance.UnloadAllTownNpcInfoGroup();

        // 플레이어 정리
        PlayerManager.Instance.CancelLeaderCharacterPosition();
        PlayerManager.Instance.DestroyTownMyPlayerCharacter();
        PlayerManager.Instance.DestroyTownOtherPlayer();

        // 씬 언로드
        await UtilModel.Resources.UnLoadSceneAsync(Model.CurrentScene);
        await UtilModel.Resources.UnLoadSceneAsync(StringDefine.SCENE_TOWN);

        // UI 정리
        await UIManager.Instance.Exit(UIType.DialogPopup);
        await UIManager.Instance.Exit(UIType.StageInfoWindow);
        UIManager.Instance.SavePrevStackNavigator();
        UIManager.Instance.DeleteAllUI();

        // Additive Prefab 언로드
        await AdditivePrefabManager.Instance.UnLoadAll();
        await CameraManager.Instance.ShowBackgroundRenderTexture(false);

        // 옵저버 해제
        RemoveTownObserverIds();

        // 메모리 해제
        await UtilModel.Resources.UnloadUnusedAssets(true);

    }

    public override async UniTask LoadingProcess(Func<UniTask> onEventExitPrevFlow)
    {
        Debug.Log("[NewTownFlow] LoadingProcess - Using Modules");

        Model.SetLoading(true);

        // 트랜지션 타입 결정
        TransitionType transitionType = TransitionType.Rotation;
        if (Model.IsBattleBack)
        {
            transitionType = Model.BattleInfo.DungeonEndTransition;
        }

        // IntroFlow에서 온 경우가 아니면 In
        bool isEnterFromIntroFlow = FlowManager.Instance.CheckFlow(FlowType.IntroFlow) ||
                                    FlowManager.Instance.CheckPrevFlow(FlowType.IntroFlow);

        if (!isEnterFromIntroFlow)
        {
            await TransitionManager.In(transitionType);
        }

        try
        {
            if (loadingModule != null)
            {
                await loadingModule.ProcessLoading(onEventExitPrevFlow);

                // UI 진입
                var uiService = serviceContainer?.GetService<IUIService>();
                if (uiService != null)
                {
                    await uiService.EnterAsync(UIType.LobbyView);
                }
            }
            else
            {
                // Fallback
                Debug.LogWarning("[NewTownFlow] LoadingModule not available");
                await onEventExitPrevFlow();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NewTownFlow] LoadingProcess failed: {e}");
        }
        finally
        {
            Model.SetLoading(false);

            if (!isEnterFromIntroFlow)
            {
                await TransitionManager.Out(transitionType);
            }
            else
            {
                await FirstTownFlowTransition();
            }


        }
    }

    public override async UniTask ChangeStateProcess(Enum state)
    {
        Debug.Log($"[NewTownFlow] ChangeStateProcess: {state}");

        FlowState flowState = (FlowState)state;
        Dictionary<string, object> parameters = PrepareStateParameters(flowState);
        await stateMachine.TransitionTo(flowState, parameters);

    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        // 비동기 처리가 필요한 경우를 위해 래핑
        HandleMessageAsync(observerMessage, observerParam).Forget();
    }

    private HashSet<Enum> processingMessages = new HashSet<Enum>();

    private async UniTask HandleMessageAsync(Enum observerMessage, IObserverParam observerParam)
    {
        if (!processingMessages.Add(observerMessage))
        {
            Debug.LogWarning($"[NewTownFlow] Recursive: {observerMessage}");
            return;
        }

        try
        {
            Debug.Log($"[NewTownFlow] HandleMessage: {observerMessage}");

            switch (observerMessage)
            {
            case FlowObserverID.PlayCutscene:
            case FlowObserverID.PlayStoryToon:
                await HandleCutsceneMessage(observerParam);
                break;

            case ViewObserverID.Refresh:
                await HandleRefreshMessage();
                break;

            case CharacterObserverID.CharacterMove:
                await HandleCharacterMoveMessage(observerParam);
                break;

            case CharacterObserverID.AutoMoveCharacter:
                await HandleAutoMoveMessage(observerParam);
                break;

            case FlowObserverID.PlayCinemachineTimeline:
                await HandleTimelineMessage(observerParam);
                break;

            case ObserverOverlayCameraID.ChangeStack:
                HandleCameraChangeMessage(observerParam);
                break;

            default:
                Debug.LogWarning($"[NewTownFlow] Unhandled observer message: {observerMessage}");
                break;
            }
        }
        finally
        {
            processingMessages.Remove(observerMessage);
        }
    }


    private Dictionary<string, object> PrepareStateParameters(FlowState state)
    {
        var parameters = new Dictionary<string, object>();

        switch (state)
        {
            case FlowState.None:
                // Idle 상태는 특별한 파라미터 필요 없음
                break;

            case FlowState.Warp:
                // WarpInfo는 struct이므로 직접 전달
                parameters["WarpInfo"] = Model.Warp;
                parameters["IsForMission"] = Model.Warp.isForMission;
                break;

            case FlowState.Battle:
                // BattleInfo 전달
                if (Model.BattleInfo != null)
                {
                    parameters["BattleInfo"] = Model.BattleInfo;
                    parameters["IsBattleBack"] = Model.IsBattleBack;
                }
                break;

            case FlowState.BeforeMissionUpdate:
                // BeforeMissionInfo struct 전달
                parameters["MissionType"] = Model.BeforeMission.missionType;
                parameters["Missions"] = Model.BeforeMission.missions;
                parameters["WebProcess"] = Model.BeforeMission.webProcess;
                break;

            case FlowState.AfterMissionUpdate:
                // AfterMissionInfo struct 전달
                parameters["Goods"] = Model.AfterMission.goods;
                parameters["IsChangeEpisode"] = Model.AfterMission.isChangeEpisode;
                parameters["IsRefreshUI"] = Model.AfterMission.isRefreshUI;
                parameters["StateMachine"] = stateMachine;  // 에피소드 워프용
                break;

            case FlowState.Tutorial:
                // TutorialInfo struct 전달
                parameters["TutorialExplain"] = Model.Tutorial.tutorialExplain;
                break;

            case FlowState.Home:
                // Home은 현재 UI 정보 전달
                parameters["CurrentUI"] = Model.CurrentUI;
                parameters["CurrentViewUI"] = Model.CurrentViewUI;
                break;

            default:
                Debug.LogWarning($"[NewTownFlow] No parameters defined for state: {state}");
                break;
        }

        // 공통 파라미터 추가
        parameters["CurrentScene"] = Model.CurrentScene;
        parameters["CurrentFieldMap"] = Model.CurrentFiledMapDefine;

        return parameters;
    }


    public override async UniTask TutorialStepAsync(Enum type)
    {
        Debug.Log($"[NewTownFlow] TutorialStep: {type}");
        // TODO: Tutorial module
    }
    #endregion

    #region Event Handlers
    private async UniTask OnEventHome()
    {
        Debug.Log("[NewTownFlow] OnEventHome");
        UIManager.Instance.Home();
        TownObjectManager.Instance.SetConditionRoad();
        await resourceService.UnloadUnusedAssets(true);
    }

    private async UniTask OnEventChangeState(UIState state, UISubState subState,
                                              UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] OnEventChangeState - State:{state}, SubState:{subState}, Prev:{prevUIType}, Current:{uiType}");

        // 자동이동 중지 (모든 UI 전환 시)
        StopAutoMove();

        try
        {
            switch (state)
            {
                case UIState.Enter:
                    await HandleUIEnter(subState, prevUIType, uiType);
                    break;

                case UIState.Exit:
                case UIState.Back:
                    await HandleUIExit(subState, prevUIType, uiType);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NewTownFlow] OnEventChangeState failed: {e}");
        }
    }
    #endregion

    // Observer Handlers
    #region Observer Message Handlers

    private async UniTask HandleUIEnter(UISubState subState, UIType prevUIType, UIType uiType)
    {
        switch (subState)
        {
            case UISubState.Enter:
                await HandleEnterStart(prevUIType, uiType);
                break;

            case UISubState.BeforeLoading:
                await HandleEnterBeforeLoading(prevUIType, uiType);
                break;

            case UISubState.AfterLoading:
                await HandleEnterAfterLoading(prevUIType, uiType);
                break;

            case UISubState.Finished:
                await HandleEnterFinished(prevUIType, uiType);
                break;
        }
    }

    private async UniTask HandleEnterStart(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Enter Start: {prevUIType} -> {uiType}");

        // 미션 업데이트
        MissionManager.Instance.UpdateMission();

        // ManualAlarm 팝업 처리
        if (uiType != UIType.ManualAlarmPopup && uiType != UIType.LobbyView)
        {
            if (UIManager.Instance.CheckOpenUI(UIType.ManualAlarmPopup))
            {
                var controller = UIManager.Instance.GetController(UIType.ManualAlarmPopup);
                await UIManager.Instance.Exit(controller);
            }
        }
    }

    private async UniTask HandleEnterBeforeLoading(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Enter BeforeLoading: {uiType}");

        bool isAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, uiType);
        TransitionType transitionType = GetEnterTransitionType(UIState.Enter, prevUIType, uiType);

        // 모바일 화면 회전 처리
#if UNITY_ANDROID || UNITY_IOS
    await HandleMobileScreenOrientation(prevUIType, uiType, transitionType);
#else
        await TransitionManager.In(transitionType);
#endif

        // Additive Application UI 처리
        if (isAdditiveApplication)
        {
            TownSceneManager.Instance.ShowTownCanvas();
        }

        // 로비 메뉴 처리
        if (uiType != UIType.LobbyView && !MissionManager.Instance.IsWait)
        {
            await TownSceneManager.Instance.PlayLobbyMenu(false);
        }

        // Additive Prefab 로딩
        await AdditivePrefabManager.Instance.LoadApplicationUnit(uiType);

        // 카메라 상태 설정
        CameraManager.Instance.SetAdditivePrefabCameraState(uiType);

        // Application Frame 활성화
        UIManager.Instance.EnableApplicationFrame(uiType);

        // 해상도 변경
        UIManager.Instance.ChangeResolution(uiType);
    }

    private async UniTask HandleEnterAfterLoading(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Enter AfterLoading: {uiType}");

        // 튜토리얼 설정
        TutorialManager.Instance.SetPlayableTutorial(uiType);

        // 플레이어 위치 업데이트
        PlayerManager.Instance.UpdateLeaderCharacterPosition();

        if (uiType < UIType.MaxView) // View인 경우
        {
            await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();

            if( BackgroundSceneManagerNew.Instance != null )
                await BackgroundSceneManagerNew.Instance.ChangeViewAsync(uiType, prevUIType);

            if (TownSceneManager.Instance != null)
                await TownSceneManager.Instance.ChangeViewAsync(uiType, prevUIType);

            if (TownObjectManager.Instance != null )
                TownObjectManager.Instance.SetConditionRoad();

            // Dialog 종료 시 퀘스트 갱신
            if (prevUIType == UIType.DialogPopup)
            {
                await MissionManager.Instance.StartRequestUpdateStoryQuestProcess(true);
            }
        }
        else // Popup인 경우
        {
            if (BackgroundSceneManagerNew.Instance != null)
                await BackgroundSceneManagerNew.Instance.ChangePopupAsync(uiType, prevUIType);

            if( TownSceneManager.Instance != null )
                await TownSceneManager.Instance.ChangePopupAsync(uiType, prevUIType);
        }
    }

    private async UniTask HandleEnterFinished(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Enter Finished: {uiType}");


        TransitionType transitionType = GetEnterTransitionType(UIState.Enter, prevUIType, uiType);

        // 튜토리얼 팝업 최상위 처리
        if (UIManager.Instance.CheckOpenUI(UIType.TutorialPopup))
        {
            BaseController tutorialController = UIManager.Instance.GetNoneStackController(UIType.TutorialPopup);
            tutorialController.GetPrefab().transform.SetAsLastSibling();
        }

        // 모바일 특별 처리
#if UNITY_ANDROID || UNITY_IOS
    if (GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode() && !DeviceRenderQuality.IsTablet)
    {
        if (!MissionManager.Instance.IsWait && !TutorialManager.Instance.CheckTutorialPlaying())
        {
            // 바로가기로 StageDungeon 진입한 경우
            if (!prevUIType == UIType.LobbyView && uiType == UIType.StageDungeonView)
            {
                transitionType = TransitionType.Rotation;
            }
        }
    }
#endif

        await TransitionManager.Out(transitionType);

        // 타 Flow에서 로비로 온 경우
        if (uiType == UIType.LobbyView && prevUIType == UIType.None)
        {
            return;
        }

        // 튜토리얼 체크 및 실행
        CheckAndPlayTutorial();
    }


    private async UniTask HandleUIExit(UISubState subState, UIType prevUIType, UIType uiType)
    {
        switch (subState)
        {
            case UISubState.Enter:
                HandleExitStart(prevUIType, uiType);
                break;

            case UISubState.BeforeLoading:
                await HandleExitBeforeLoading(prevUIType, uiType);
                break;

            case UISubState.AfterLoading:
                await HandleExitAfterLoading(prevUIType, uiType);
                break;

            case UISubState.Finished:
                await HandleExitFinished(prevUIType, uiType);
                break;
        }
    }

    private void HandleExitStart(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Exit Start: {prevUIType} -> {uiType}");

        // 미션 업데이트
        MissionManager.Instance.UpdateMission();
    }

    private async UniTask HandleExitBeforeLoading(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Exit BeforeLoading");

        TransitionType transitionType = GetExitTransitionType(UIState.Exit, prevUIType, uiType);

#if UNITY_ANDROID || UNITY_IOS
    await HandleMobileExitTransition(prevUIType, uiType, transitionType);
#else
        await TransitionManager.In(transitionType);
#endif

        // Additive Application 로드
        await AdditivePrefabManager.Instance.LoadApplicationUnit(uiType);

        // 해상도 변경
        UIManager.Instance.ChangeResolution(uiType);
    }

    private async UniTask HandleExitAfterLoading(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Exit AfterLoading");

        // 튜토리얼 설정
        TutorialManager.Instance.SetPlayableTutorial(uiType);

        // 플레이어 위치 업데이트
        PlayerManager.Instance.UpdateLeaderCharacterPosition();

        if (uiType < UIType.MaxView) // View인 경우
        {
            UIManager.Instance.EnableApplicationFrame(uiType);
            await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
            CameraManager.Instance.SetAdditivePrefabCameraState(uiType);
            await BackgroundSceneManagerNew.Instance.ChangeViewAsync(uiType, prevUIType);
            await TownSceneManager.Instance.ChangeViewAsync(uiType, prevUIType);
            TownObjectManager.Instance.SetConditionRoad();

            // Lobby로 돌아온 경우
            if (uiType == UIType.LobbyView)
            {
                await UtilModel.Resources.UnloadUnusedAssets(true);
                await RequestDataFromNetwork();

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
        else // Popup인 경우
        {
            UIManager.Instance.EnableApplicationFrame(uiType);
            await AdditivePrefabManager.Instance.UnLoadCloseUIAsync();
            await BackgroundSceneManagerNew.Instance.ChangePopupAsync(uiType, prevUIType);
            await TownSceneManager.Instance.ChangePopupAsync(uiType, prevUIType);
        }

        // 모바일 화면 회전 복원
#if UNITY_ANDROID || UNITY_IOS
    await HandleMobileOrientationRestore(prevUIType, uiType);
#endif
    }

    private async UniTask HandleExitFinished(UIType prevUIType, UIType uiType)
    {
        Debug.Log($"[NewTownFlow] UI Exit Finished");

        bool isAdditiveApplication = AdditivePrefabManager.Instance.CheckUIType(AdditiveType.App, uiType);


        TransitionType transitionType = GetExitTransitionType(UIState.Exit, prevUIType, uiType);

        // 튜토리얼 팝업 처리
        if (UIManager.Instance.CheckOpenUI(UIType.TutorialPopup))
        {
            BaseController tutorialController = UIManager.Instance.GetNoneStackController(UIType.TutorialPopup);
            tutorialController.GetPrefab().transform.SetAsLastSibling();
        }

        // 미션 대기 중 처리
        if (MissionManager.Instance.IsWait)
        {
            await TransitionManager.Out(transitionType);
            CheckAndPlayTutorial();
            return;
        }

        // 미션 후처리
        if (!MissionManager.Instance.CheckHasWarpProcessByBehaviorBuffer() &&
            MissionManager.Instance.CheckHasDelayed())
        {
            await MissionManager.Instance.StartAutoProcess(transitionType);
            TransitionManager.LoadingUI(false, false);
            CheckAndPlayTutorial();
        }
        else
        {
            await TransitionManager.Out(transitionType);
            CheckAndPlayTutorial();
        }
    }



    private async UniTask HandleCutsceneMessage(IObserverParam observerParam)
    {
        if (TownSceneManager.Instance == null) return;

        CutsceneParam cutsceneParam = observerParam as CutsceneParam;
        if (cutsceneParam == null) return;

        Debug.Log($"[NewTownFlow] Cutscene: Show={cutsceneParam.IsShow}, IsTown={cutsceneParam.IsTownCutscene}");

        // 컷씬 상태 처리
        TownSceneManager.Instance.PlayCutsceneState(cutsceneParam.IsShow);

        if (BackgroundSceneManagerNew.Instance != null)
        {
            BackgroundSceneManagerNew.Instance.PlayCutsceneState(
                cutsceneParam.IsShow,
                cutsceneParam.IsTownCutscene
            );
        }

        // 컷씬 종료 후 타운 갱신
        if (!cutsceneParam.IsShow && cutsceneParam.IsTownRefresh)
        {
            await TownObjectManager.Instance.RefreshProcess();
        }
    }

    private async UniTask HandleRefreshMessage()
    {
        Debug.Log($"[NewTownFlow] Refresh requested for UI: {Model.CurrentUI}");

        if (TownSceneManager.Instance != null)
        {
            await TownSceneManager.Instance.RefreshAsync(Model.CurrentUI);
        }
    }

    private async UniTask HandleCharacterMoveMessage(IObserverParam observerParam)
    {
        BoolParam boolParam = observerParam as BoolParam;
        if (boolParam == null) return;

        bool isMove = boolParam.Value1;
        Debug.Log($"[NewTownFlow] Character move: {isMove}");

        // 상호작용 중이면 무시
        if (PlayerManager.Instance.MyPlayer.IsInteracting)
            return;

        // 로비 메뉴 표시/숨기기
        await TownSceneManager.Instance.PlayLobbyMenu(!isMove);

        // 카메라 업데이트
        OnEventChangeLiveVirtualCinemachineCamera();

        // 자동이동 중이었다면 중지
        if (isMove && PlayerManager.Instance.MyPlayer.TownPlayer.CheckPathfind())
        {
            StopAutoMove();
        }

        // 미션 자동이동 해제
        MissionManager.Instance.SetAutoMove(false);
    }

    private async UniTask HandleAutoMoveMessage(IObserverParam observerParam)
    {
        if (observerParam is not CharacterParam characterParam) return;

        Debug.Log($"[NewTownFlow] Auto move state: {characterParam.AutoMoveState}");

        switch (characterParam.AutoMoveState)
        {
            case CharacterAutoMoveState.Move:
                // 자동이동 시작
                await TownSceneManager.Instance.PlayLobbyMenu(false);
                break;

            case CharacterAutoMoveState.Stop:
                // 자동이동 중지
                StopAutoMove();
                await TownSceneManager.Instance.PlayLobbyMenu(true);
                break;

            case CharacterAutoMoveState.End:
                // 자동이동 완료
                await HandleAutoMoveComplete(characterParam);
                break;
        }
    }

    private async UniTask HandleTimelineMessage(IObserverParam observerParam)
    {
        CinemachineTimelineParam timelineParam = observerParam as CinemachineTimelineParam;
        if (timelineParam == null) return;

        Debug.Log($"[NewTownFlow] Timeline: {timelineParam.TimelineName}, Play={timelineParam.IsPlay}");

        if (timelineParam.IsPlay)
        {
            // 타임라인 재생
            await CinemachineTimelineManager.Instance.Play();
        }
        else
        {
            // 타임라인 중지
            CinemachineTimelineManager.Instance.ClearChilds();

        }
    }

    private void HandleCameraChangeMessage(IObserverParam observerParam)
    {
        OverlayCameraParam cameraParam = observerParam as OverlayCameraParam;
        if (cameraParam == null) return;

        Debug.Log($"[NewTownFlow] Camera stack change: {cameraParam.CameraType}");

        // 카메라 스택 변경 처리
        if (cameraParam.IsAdd)
        {
           // CameraManager.Instance.AddOverlayCamera(cameraParam.CameraType);
        }
        else
        {
          //  CameraManager.Instance.RemoveOverlayCamera(cameraParam.CameraType);
        }
    }

    private async UniTask HandleAutoMoveComplete(CharacterParam param)
    {
        Debug.Log($"[NewTownFlow] Auto move completed to target: {param.AutoMoveTargetId}");

        // 목표 도달 시 처리
        if (!string.IsNullOrEmpty(param.AutoMoveTargetId.ToString()))
        {
            // NPC 상호작용 체크
            ITownSupport target = TownObjectManager.Instance.GetTownObjectByEnumId(param.AutoMoveTargetId.ToString());
            if (target != null && target.TownObjectType == TownObjectType.Npc)
            {
                // NPC 대화 시작
              //  await OnEventCheckInteraction(new TownTalkInteraction(true,
             //       new TownTalkTarget(target.EnumId, target.DataId)));
            }
        }

        // 로비 메뉴 복원
        await TownSceneManager.Instance.PlayLobbyMenu(true);
    }

    #endregion


    #region Interaction System

    private async UniTask OnEventCheckInteraction(TownTalkInteraction talkInteraction)
    {
        Debug.Log($"[NewTownFlow] OnEventCheckInteraction - IsInteraction: {talkInteraction.IsTalk}");

        if (!talkInteraction.IsTalk)
        {
            // 상호작용 종료
            await HandleInteractionEnd();
            return;
        }

        // 상호작용 시작
        await HandleInteractionStart(talkInteraction.TalkTarget);
    }

    private async UniTask HandleInteractionEnd()
    {
        Debug.Log("[NewTownFlow] Interaction ended");

        // 플레이어 상호작용 상태 해제
        if (playerService != null && playerService.MyPlayer != null)
        {
            playerService.MyPlayer.SetInteracting(false);
        }
        else
        {
            PlayerManager.Instance.MyPlayer?.SetInteracting(false);
        }

        // 입력 활성화
        if (townSceneService?.TownInputSupport != null)
        {
            townSceneService.TownInputSupport.SetInput(true);
        }
        else
        {
            TownSceneManager.Instance.TownInputSupport?.SetInput(true);
        }

        // 로비 메뉴 표시
        if (!MissionManager.Instance.IsWait)
        {
            if (townSceneService != null)
            {
                await townSceneService.PlayLobbyMenu(true);
            }
            else
            {
                await TownSceneManager.Instance.PlayLobbyMenu(true);
            }
        }
    }

    private async UniTask HandleInteractionStart(TownTalkTarget talkTarget)
    {
        Debug.Log($"[NewTownFlow] Starting interaction with: {talkTarget.targetDataId}");

        // 플레이어 상호작용 상태 설정
        if (playerService != null && playerService.MyPlayer != null)
        {
            playerService.MyPlayer.SetInteracting(true);
        }
        else
        {
            PlayerManager.Instance.MyPlayer?.SetInteracting(true);
        }

        // 입력 비활성화
        if (townSceneService?.TownInputSupport != null)
        {
            townSceneService.TownInputSupport.SetInput(false);
        }
        else
        {
            TownSceneManager.Instance.TownInputSupport?.SetInput(false);
        }

        // 타운 오브젝트 가져오기
        ITownSupport townObject = null;
        if (townObjectService != null)
        {
            townObject = !string.IsNullOrEmpty(talkTarget.targetEnumId)
                ? townObjectService.GetTownObjectByEnumId(talkTarget.targetEnumId)
                : townObjectService.GetTownObjectByDataId(talkTarget.targetDataId);
        }
        else
        {
            townObject = !string.IsNullOrEmpty(talkTarget.targetEnumId)
                ? TownObjectManager.Instance.GetTownObjectByEnumId(talkTarget.targetEnumId)
                : TownObjectManager.Instance.GetTownObjectByDataId(talkTarget.targetDataId);
        }

        if (townObject == null)
        {
            Debug.LogWarning($"[NewTownFlow] Town object not found: {talkTarget.targetEnumId}");
            await HandleInteractionEnd();
            return;
        }

        // 오브젝트 타입별 처리
        await ProcessInteractionByType(townObject, talkTarget);
    }

    private async UniTask ProcessInteractionByType(ITownSupport townObject, TownTalkTarget talkTarget)
    {
        Debug.Log($"[NewTownFlow] Processing interaction - Type: {townObject.TownObjectType}");

        switch (townObject.TownObjectType)
        {
            case TownObjectType.Npc:
                await ProcessNpcInteraction(townObject, talkTarget);
                break;

            case TownObjectType.WarpPoint:
                await ProcessWarpInteraction(townObject);
                break;

            case TownObjectType.OtherPlayer:
              //  await ProcessObjectInteraction(townObject);
                break;

            case TownObjectType.Environment:
           //     await ProcessSignBoardInteraction(townObject);
                break;

            default:
                Debug.LogWarning($"[NewTownFlow] Unsupported interaction type: {townObject.TownObjectType}");
                await HandleInteractionEnd();
                break;
        }
    }

    private async UniTask ProcessNpcInteraction(ITownSupport townObject, TownTalkTarget talkTarget)
    {
        Debug.Log($"[NewTownFlow] NPC Interaction: {townObject.EnumId}");

        // NPC 대화 타입 확인
        NpcInteractionTable interactionTable = TableManager.Instance.GetTable<NpcInteractionTable>();
        NpcInteractionTableData interactionData = interactionTable.GetDataByID(townObject.DataId);

        if (interactionData.IsNull())
        {
            Debug.LogWarning($"[NewTownFlow] NPC interaction data not found: {townObject.DataId}");
            await HandleInteractionEnd();
            return;
        }

       /* InteractionType triggerType = (InteractionType)interactionData.GetINTERACTION_TYPE()();

        switch (triggerType)
        {
            case InteractionType.:
                await OpenNpcDialog(townObject, interactionData);
                break;

            case InteractionTrigger.Shop:
                await OpenShop(interactionData);
                break;

            case InteractionTrigger.Warp:
                await ProcessNpcWarp(interactionData);
                break;

            case InteractionTrigger.Mission:
                await ProcessMissionNpc(townObject, interactionData);
                break;

            case InteractionTrigger.Function:
                await ProcessFunctionNpc(interactionData);
                break;

            default:
                Debug.LogWarning($"[NewTownFlow] Unknown NPC trigger type: {triggerType}");
                await HandleInteractionEnd();
                break;
        }*/

    }

    private async UniTask OpenNpcDialog(ITownSupport townObject, NpcInteractionTableData interactionData)
    {
        Debug.Log($"[NewTownFlow] Opening NPC dialog");

        // 로비 메뉴 숨기기
        if (townSceneService != null)
        {
            await townSceneService.PlayLobbyMenu(false);
        }
        else
        {
            await TownSceneManager.Instance.PlayLobbyMenu(false);
        }

        // 대화 팝업 열기
        var dialogController = UIManager.Instance.GetController(UIType.DialogPopup);
        var dialogModel = dialogController.GetModel<DialogPopupModel>();

        // 대화 데이터 설정
       /* dialogModel.SetNpcData(townObject.DataId, interactionData);
        dialogModel.SetDialogId(interactionData.GetDIALOG_ID());

        // 미션 연동
        if (MissionManager.Instance.IsExistMissionBehavior)
        {
            dialogModel.SetMissionDialog(true);
            dialogModel.SetMissionBehavior(MissionManager.Instance.MissionBehaviorBuffer);
        }*/


        await UIManager.Instance.EnterAsync(dialogController);
    }

    private async UniTask ProcessNpcWarp(NpcInteractionTableData interactionData)
    {
        Debug.Log($"[NewTownFlow] Processing NPC warp");

        // 워프 정보 생성
        var warpInfo = new TownFlowModel.WarpInfo(
            interactionData.GetWARP_TARGET_FIELD_MAP_ID().ToString(),
            interactionData.GetWARP_POINT(),
            TownObjectType.WarpPoint,
            (Transition)interactionData.GetEND_TRANSITION()
        );

        // State Machine을 통해 워프 상태로 전환
        if (stateMachine != null)
        {
            var parameters = new Dictionary<string, object>
            {
                ["WarpInfo"] = warpInfo
            };
            await stateMachine.TransitionTo(FlowState.Warp, parameters);
        }
    }

    private async UniTask ProcessMissionNpc(ITownSupport townObject, NpcInteractionTableData interactionData)
    {
        Debug.Log($"[NewTownFlow] Processing mission NPC");

        // 미션 매니저에서 처리
        if (missionService != null)
        {
            await missionService.StartRequestUpdateStoryQuestProcess(true);
        }


        // 미션 대화 처리
        await OpenNpcDialog(townObject, interactionData);
    }

    private async UniTask ProcessFunctionNpc(NpcInteractionTableData interactionData)
    {
        Debug.Log($"[NewTownFlow] Processing function NPC: {interactionData.GetINTERACTION_TYPE()}");

 
        var warpFieldMap = (FieldMapDefine)interactionData.GetWARP_TARGET_FIELD_MAP_ID();
        var warpTransition = (Transition)interactionData.GetEND_TRANSITION();
        var warpTargetObjectType = TownObjectType.None;
        int warpTargetDataId = interactionData.GetWARP_TARGET_NPC_ID(0);
        var warpTargetKey = string.Empty;


    /*    switch (warpTargetObjectType)
        {
            case NpcFunctionType.Guild:
                await UIManager.Instance.EnterAsync(UIType.GuildView);
                break;

            case NpcFunctionType.Gacha:
                await UIManager.Instance.EnterAsync(UIType.GachaView);
                break;

            case NpcFunctionType.Craft:
                await UIManager.Instance.EnterAsync(UIType.CraftView);
                break;

            case NpcFunctionType.Housing:
                await UIManager.Instance.EnterAsync(UIType.HousingView);
                break;

            default:
                Debug.LogWarning($"[NewTownFlow] Unknown function type: {functionType}");
                await HandleInteractionEnd();
                break;
        }*/

    }

    private async UniTask ProcessWarpInteraction(ITownSupport townObject)
    {
        Debug.Log($"[NewTownFlow] Processing warp point: {townObject.EnumId}");

        // 워프 포인트 데이터 가져오기
       /* WarpPointTable warpTable = TableManager.Instance.GetTable<WarpPointTable>();
        WarpPointTableData warpData = warpTable.GetDataByID(townObject.DataId);

        if (warpData.IsNull())
        {
            Debug.LogWarning($"[NewTownFlow] Warp data not found: {townObject.DataId}");
            await HandleInteractionEnd();
            return;
        }

        // 워프 정보 생성
        var warpInfo = new TownFlowModel.WarpInfo(
            warpData.GetTARGET_FIELD_MAP().ToString(),
            warpData.GetTARGET_WARP_POINT(),
            TownObjectType.WarpPoint,
            (Transition)warpData.GetTRANSITION()
        );

        // State Machine을 통해 워프 상태로 전환
        if (stateMachine != null)
        {
            var parameters = new Dictionary<string, object>
            {
                ["WarpInfo"] = warpInfo
            };
            await stateMachine.TransitionTo(FlowState.Warp, parameters);
        }*/


    }



    // 자동이동 관련 메서드들
    #region Auto Move System

    private bool isStoppingAutoMove = false;

    private void StopAutoMove()
    {
        if (isStoppingAutoMove) return;
        isStoppingAutoMove = true;

        try
        {
            Debug.Log("[NewTownFlow] Stopping auto move");

            // 플레이어가 없으면 리턴
            if (PlayerManager.Instance.MyPlayer?.TownPlayer?.CheckSafeNull() ?? true)
                return;

            // 자동이동 중지 파라미터 생성
            CharacterParam characterParam = new CharacterParam();
            characterParam.SetAutoMoveState(CharacterAutoMoveState.Stop);

            // Observer 알림
            ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
        }
        finally
        {
            isStoppingAutoMove = false;
        }
    }


    private void OnEventChangeLiveVirtualCinemachineCamera()
    {
        try
        {
            if (PlayerManager.Instance?.MyPlayer?.TownPlayer?.TownObject == null)
            {
                return;
            }

            bool isMoveInput = PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.MoveData.IsMoveInput;
            TownInputSupport townInputSupport = TownSceneManager.Instance?.TownInputSupport;

            if (townInputSupport == null)
            {
                
                return;
            }

            // Virtual Joystick 타겟 업데이트
            townInputSupport.SetVirtualJoystickLookAtTarget(
                CameraManager.Instance.TownCamera,
                CameraManager.Instance.RecomposerPan
            );

            if( CameraManager.Instance.GetBrainCamera().ActiveVirtualCamera != null )
            {
              //  CameraManager.Instance.GetBrainCamera().ActiveVirtualCamera.Follow = PlayerManager.Instance?.MyPlayer?.TownPlayer?.TownObject.Transform;

                CinemachineClearShot clearShotCamera = CameraManager.Instance.GetBrainCamera().ActiveVirtualCamera as CinemachineClearShot;
                clearShotCamera.LiveChild.Follow = PlayerManager.Instance?.MyPlayer?.TownPlayer?.TownObject.Transform;
            }



        }
        catch (Exception e)
        {
            Debug.LogError($"[NewTownFlow] Camera update failed: {e}");
        }
    }

    #endregion


    // Transition Type 결정
    private TransitionType GetEnterTransitionType(UIState state, UIType prevUIType, UIType uiType)
    {
        // 팝업은 트랜지션 없음
        if (uiType > UIType.MaxView)
            return TransitionType.None;

        // 스위핑 진입
        if (uiType == UIType.StageDungeonView)
            return TransitionType.Tram;

        // 넷마이닝, 도감, 하우징 진입
        bool isRotation = uiType == UIType.NetMiningView ||
                          uiType == UIType.CharacterIntroduceView ||
                          uiType == UIType.HousingSimulationView ||
                          (prevUIType == UIType.CharacterIntroduceView &&
                           uiType == UIType.CharacterDetailView);

        if (isRotation)
            return TransitionType.Rotation;

        return TransitionType.None;
    }

    private TransitionType GetExitTransitionType(UIState state, UIType prevUIType, UIType uiType)
    {
        // NPC 대화 -> 로비 특별 처리
        if (prevUIType == UIType.DialogPopup && uiType == UIType.LobbyView)
        {
            if (PrologueManager.Instance.IsProgressing)
                return TransitionType.None;

            var result = MissionManager.Instance.GetExpectedResultByStoryQuest();
            if (!result.HasWarp && (result.HasEndStory ||
                MissionManager.Instance.CheckHasDelayedStory(false)))
            {
                return TransitionManager.ConvertTransitionType(result.Transition);
            }
        }

        if (prevUIType > UIType.MaxView)
            return TransitionType.None;

        if (prevUIType == UIType.StageDungeonView)
            return TransitionType.Tram;

        bool isRotation = prevUIType == UIType.NetMiningView ||
                          prevUIType == UIType.CharacterIntroduceView ||
                          prevUIType == UIType.HousingSimulationView ||
                          (prevUIType == UIType.CharacterDetailView &&
                           uiType == UIType.CharacterIntroduceView);

        if (isRotation)
            return TransitionType.Rotation;

        return TransitionType.None;
    }


    // 튜토리얼 체크 및 실행
    private void CheckAndPlayTutorial()
    {
        if (TutorialManager.Instance.HasPlayableTutorial && !MissionManager.Instance.IsWait)
        {
            // 딜레이 프로세스 완료 후 튜토리얼 실행
            System.Action taskTutorial = async () =>
            {
                await UniTask.WaitUntil(() => !MissionManager.Instance.CheckHasDelayed());
                await UniTask.WaitUntil(() => !MissionManager.Instance.IsWait);
                TutorialManager.Instance.PlayNextTutorial();
            };
            taskTutorial.Invoke();
        }
    }

    // NewTownFlow.cs - 네트워크 요청 시스템
    #region Network Requests

    private async UniTask RequestDataFromNetwork()
    {
        // 오프라인 모드면 스킵
        if (Model.IsOffline)
        {
            Debug.Log("[NewTownFlow] Offline mode - skipping network requests");
            return;
        }

        Debug.Log("[NewTownFlow] Starting network requests");

        try
        {
            // 1. 시즌 업데이트 체크
            await CheckAndUpdateSeason();

            // 2. 넷마이닝 (자동전투) 정보
            await RequestNetMining();

            // 3. 레드닷 업데이트
            await RequestReddot();

            // 4. 이벤트 정보 (10분 단위 갱신)
            await CheckAndUpdateEvents();

            // 5. 공지사항 업데이트
            StartNoticeUpdate();

            Debug.Log("[NewTownFlow] Network requests completed");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NewTownFlow] Network request failed: {e}");
            // 네트워크 실패해도 게임은 계속 진행
        }
    }

    private async UniTask CheckAndUpdateSeason()
    {
        // 현재 시즌과 서버 시즌 비교
        if (PlayerManager.Instance.MyPlayer.User.SeasonType != SeasonType.First)
        {
            await RequestUpdateSeason(SeasonType.First);
        }
    }

    private async UniTask RequestUpdateSeason(SeasonType seasonType)
    {
        Debug.Log($"[NewTownFlow] Updating season to: {seasonType}");

        BaseProcess seasonUpdateProcess = NetworkManager.Web.GetProcess(WebProcess.UserSeasonUpdate);
        seasonUpdateProcess.SetPacket(new UpdateUserSeasonInDto(seasonType));

        if (await seasonUpdateProcess.OnNetworkAsyncRequest())
        {
            seasonUpdateProcess.OnNetworkResponse();
            Debug.Log($"[NewTownFlow] Season updated successfully");
        }
        else
        {
            Debug.LogWarning($"[NewTownFlow] Season update failed");
        }
    }

    #endregion

    #region NetMining (Auto Battle)

    private async UniTask RequestNetMining()
    {
        // 컨텐츠 오픈 체크
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.AutoBattle))
        {
            Debug.Log("[NewTownFlow] AutoBattle content not open yet");
            return;
        }

        Debug.Log("[NewTownFlow] Requesting NetMining status");

        BaseProcess autoBattleGetProcess = NetworkManager.Web.GetProcess(WebProcess.AutoBattleGet);

        if (await autoBattleGetProcess.OnNetworkAsyncRequest())
        {
            autoBattleGetProcess.OnNetworkResponse();

            // 자동전투 결과 처리
            AutoBattleGetResponse response = autoBattleGetProcess.GetResponse<AutoBattleGetResponse>();
            if( response.data.CheckSafeNull() == false )
            {
                await ProcessNetMiningResult(response.data);
            }
        }
    }

    private async UniTask ProcessNetMiningResult(AutoBattleDto data)
    {
        Debug.Log($"[NewTownFlow] NetMining result - Status: {data.fastRewardAt}, Rewards: {data.fastRewardCount}");

        // 자동전투 완료 상태면 보상 처리
        if (data.fastRewardCount > 0)
        {
            // 보상 팝업 표시
            //await ShowNetMiningRewards(data.re);
        }

        // 자동전투 UI 갱신
       // ObserverManager.NotifyObserver(NetMiningObserverID.Update, new NetMiningParam { Data = data });
    }

    private async UniTask ShowNetMiningRewards(List<Goods> rewards)
    {
        // 넷마이닝 보상 팝업
        var controller = UIManager.Instance.GetController(UIType.RewardPopup);
        var model = controller.GetModel<RewardPopupModel>();
       // model.SetRewards(rewards);
      //  model.SetTitle("Auto Battle Complete!");

        await UIManager.Instance.EnterAsync(controller);
    }

    #endregion

    #region Red Dot System

    private async UniTask RequestReddot()
    {
        Debug.Log("[NewTownFlow] Updating red dots");

        // 병렬로 레드닷 정보 요청
        var tasks = new List<UniTask>
    {
        MailRedDot(),
       // GuildRedDot(),
      //  DispatchRedDot(),
       // FriendRedDot(),
      //  EventRedDot()
    };

        await UniTask.WhenAll(tasks);

        // 레드닷 UI 갱신
        RedDotManager.Instance.Notify();
    }

    private async UniTask MailRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Mail))
            return;

        BaseProcess mailGetProcess = NetworkManager.Web.GetProcess(WebProcess.MailGet);
        mailGetProcess.SetLoading(false, false); // 로딩 UI 표시 안함

        if (await mailGetProcess.OnNetworkAsyncRequest())
        {
            mailGetProcess.OnNetworkResponse();

            /*MailGetResponse response = mailGetProcess.GetResponse<MailGetResponse>();
            if (response != null && response.data != null)
            {
                // 읽지 않은 메일 개수 설정
                int unreadCount = response.data.Count(mail => !mail.IsRead);
                RedDotManager.Instance.SetRedDot(RedDotType.Mail, unreadCount > 0, unreadCount);
            }*/
        }
    }

    private async UniTask GuildRedDot()
    {
        // 길드 가입 체크
        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.IsJoinedGuild)
            return;

        int guildId = PlayerManager.Instance.MyPlayer.User.GuildModel.GuildId;

        // 길드 멤버 정보
        BaseProcess memberGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildMemberGet);
        memberGetProcess.SetPacket(new GetGuildMemberInDto(guildId));

        memberGetProcess.SetLoading(false, false);

        if (await memberGetProcess.OnNetworkAsyncRequest())
        {
            memberGetProcess.OnNetworkResponse();
        }

        if (!PlayerManager.Instance.MyPlayer.User.GuildModel.HasAuthority)
            return;

        BaseProcess signupGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildReceiveSignupGet);

        if (await signupGetProcess.OnNetworkAsyncRequest())
        {
            signupGetProcess.OnNetworkResponse();

            // ===== 수정: 주석 처리된 부분 =====
            //GuildReceiveSignupGetResponse response = signupGetProcess.GetResponse<GuildReceiveSignupGetResponse>();
            //PlayerManager.Instance.MyPlayer.User.GuildModel.SetNewContents(response.data.LongLength > 0);
        }
    }


    private async UniTask DispatchRedDot()
    {
        if (!ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.Dispatch))
            return;

        BaseProcess dispatchGetProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchGet);
        dispatchGetProcess.SetLoading(false, false);

        if (await dispatchGetProcess.OnNetworkAsyncRequest())
        {
            dispatchGetProcess.OnNetworkResponse();
        }
    }


    #endregion

    #region Events and Notices

    private async UniTask CheckAndUpdateEvents()
    {
        // 10분 단위로만 이벤트 정보 갱신
        if (!TimeManager.Instance.CheckTenMinuteDelayAPI())
        {
            Debug.Log("[NewTownFlow] Event update skipped - within 10 minute window");
            return;
        }

        Debug.Log("[NewTownFlow] Updating event information");

        // 이벤트 패스 정보
        await EventManager.Instance.PacketEventPassGet();

        // 진행 중인 이벤트 목록
        await UpdateActiveEvents();

    }

    private async UniTask UpdateActiveEvents()
    {
        BaseProcess eventListProcess = NetworkManager.Web.GetProcess(WebProcess.EventPassGet);

        if (await eventListProcess.OnNetworkAsyncRequest())
        {
            eventListProcess.OnNetworkResponse();

            EventPassGetResponse response = eventListProcess.GetResponse<EventPassGetResponse>();
            if (response != null && response.data != null)
            {
                // 이벤트 매니저에 저장
               // EventManager.Instance.UpdateEventList(response.data);

                // 새 이벤트 알림
                CheckNewEvents(response.data);
            }
        }
    }

    private void CheckNewEvents(GetEventPassOutDto[] events)
    {
        foreach (var eventData in events)
        {
            // 오늘 시작한 이벤트 체크
            if (IsEventStartedToday(eventData))
            {
                ShowNewEventNotification(eventData);
            }
        }
    }

    private bool IsEventStartedToday(GetEventPassOutDto eventData)
    {
        // DateTime today = TimeManager.Instance.GetServerTime().Date;
        //  DateTime eventStart = DateTime.Parse(eventData.StartTime).Date;
        //  return today == eventStart;
        return false;
    }

    private void ShowNewEventNotification(GetEventPassOutDto eventData)
    {
        /*string message = $"New Event: {eventData.Title}";
        MessageBoxManager.ShowToastMessage(message, 3000);

        // 이벤트 배너 팝업 표시
        if (eventData.HasBanner && !string.IsNullOrEmpty(eventData.BannerUrl))
        {
            ShowEventBannerPopup(eventData).Forget();
        }*/

    }

    private async UniTask ShowEventBannerPopup(GetEventPassOutDto eventData)
    {
        var controller = UIManager.Instance.GetController(UIType.EventPopup);
        /*var model = controller.GetModel<EventPassModel>();
        model.SetEventListUnitModel(eventData);
        */

        await UIManager.Instance.EnterAsync(controller);
    }

    private void StartNoticeUpdate()
    {
        Debug.Log("[NewTownFlow] Starting notice update");

        // 공지사항 매니저 시작 (비동기로 진행)
        NoticeManager.Instance.StartNoticeUpdate();

        // 긴급 공지 체크
     //  CheckEmergencyNotice().Forget();
    }

    private async UniTask CheckEmergencyNotice()
    {
      /*  BaseProcess emergencyNoticeProcess = NetworkManager.Web.GetProcess(WebProcess.EmergencyNoticeGet);

        if (await emergencyNoticeProcess.OnNetworkAsyncRequest())
        {
            emergencyNoticeProcess.OnNetworkResponse();

            EmergencyNoticeGetResponse response = emergencyNoticeProcess.GetResponse<EmergencyNoticeGetResponse>();
            if (response != null && response.data != null && response.data.HasEmergency)
            {
                // 긴급 공지 즉시 표시
                await ShowEmergencyNotice(response.data);
            }
        }*/

    }
    #endregion

    #endregion
}