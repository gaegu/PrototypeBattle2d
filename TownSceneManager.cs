#pragma warning disable CS1998
//=========================================================================================================
//using System;
//using System.Collections;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.UI;
//=========================================================================================================

public class TownSceneManager : BaseSceneManager, IObserver
{
    private static TownSceneManager instance;
    public static TownSceneManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(TownSceneManager).Name;
                GameObject manager = GameObject.Find(className);

                if (manager == null)
                    return null;

                instance = manager.GetComponent<TownSceneManager>();

                if (instance == null)
                    instance = new TownSceneManager();
            }

            return instance;
        }
    }

    public enum TownCanvasType
    {
        Input,
        Tag3D,
        Tag2D,
        MiniMap,
        UI,
    }

    #region Coding rule : Property
    public TownInputSupport TownInputSupport { get { return townInputSupport; } }
    #endregion Coding rule : Property

    #region Coding rule : Value
    [Header("타운")]
    [SerializeField]
    private GameObject townGroup = null;

    [Header("Town Scene Canvas들")]
    [SerializeField]
    private Canvas[] canvas = null;

    [Header("Npc Tag")]
    [SerializeField]
    private TownTagSupport tagSupport = null;

    [Header("조작")]
    [SerializeField]
    private TownInputSupport townInputSupport = null;

    [Header("UI")]
    [SerializeField]
    private GraphicRaycaster uIRaycaster = null;
    [SerializeField]
    private CanvasScaler uIScaler = null;
    [SerializeField]
    private LobbyMenuUnit lobbyMenuUnit = null;

    [Header("메시지 알림")]
    [SerializeField]
    private TownMessageSupport townMessageSupport = null;

    [Header("자동 이동")]
    [SerializeField]
    private ResourcesLoader autoMoveStateBarUnitLoader = null;

    [Header("퀘스트")]
    [SerializeField]
    private ResourcesLoader questTrackingLoader = null;

    [Header("보상획득 연출")]
    [SerializeField]
    private SimpleRewardScrollUnit simpleRewardScrollUnit = null;

    private Action<CharacterParam> onEventAutoMoveCharacter = null;
    #endregion Coding rule : Value

    #region Coding rule : Function
    private void Awake()
    {
        ObserverManager.AddObserver(TownObserverID.BgmState, this);
        ObserverManager.AddObserver(TownObserverID.ShowProgressBar, this);

        ObserverManager.AddObserver(CharacterObserverID.AutoMoveCharacter, this);

        TutorialManager.Instance.SetFuncGetTutorialFocusObject(GetTutorialFocusObject);
        TutorialManager.Instance.SetEventTutorialEnd(OnEventTutorialEnd);
        TutorialManager.Instance.SetEventNoneButtonUnlockEvent(OnEventNoneButtonUnlockEvent);
        TutorialManager.Instance.SetFuncGetTutorialCondition(GetTutorialCondition);
        TutorialManager.Instance.SetFuncGetFocusObjectScale(GetUICanvasScale);

        MissionManager.Instance.SetEventQuestTracking(OnEventQuestTracking);

        simpleRewardScrollUnit.SafeSetActive(false);
    }

    private void OnDestroy()
    {
        ObserverManager.RemoveObserver(TownObserverID.BgmState, this);
        ObserverManager.RemoveObserver(TownObserverID.ShowProgressBar, this);

        ObserverManager.RemoveObserver(CharacterObserverID.AutoMoveCharacter, this);
    }

    public void ShowTown(bool isShow)
    {
        townGroup.SafeSetActive(isShow);
    }

    public void SetTownInput(Transform inputLookAtTarget)
    {
        townInputSupport.SetVirtualJoystickLookAtTarget(inputLookAtTarget);
        townInputSupport.SetMoveInputOwner(PlayerManager.Instance.MyPlayer.TownPlayer.TownObject);
    }

    public void SetActiveTownInput(bool isInput)
    {
        townInputSupport.SetInput(isInput);
        townInputSupport.SafeSetActive(isInput);
        CameraManager.Instance.SetFreeLookZoomInOut(!isInput);
    }

    public virtual async UniTask ChangeViewAsync(UIType uIType, UIType prevUIType)
    {
        bool isInput = uIType == UIType.LobbyView;
        isInput &= !TutorialManager.Instance.CheckTutorialPlaying();
        bool isOrthographic = !UIManager.Instance.CheckPerspectiveUI(uIType);
        bool isPopupOrthoGraphic = TutorialManager.Instance.CheckTutorialPlaying() ? isOrthographic : true;

        bool isFrontCameraPostProcessing = UIManager.Instance.CheckPostProcessing(uIType);

        IronJade.Debug.Log($"@@@@@@ ChangeViewAsync : {isInput}, {uIType}, {prevUIType}");

        if (CameraManager.Instance.IsFreeLook)
            return;

        // 1. 플레이어 설정
        townInputSupport.SetInput(!MissionManager.Instance.IsWait && isInput);
        townInputSupport.SafeSetActive(isInput);
        await ShowMyPlayer(true);

        // 여기서 하면 버그 생김. TownFlow로 이동
        //GameSettingManager.Instance.SetTargetFrame(isInput);

        // 2. 카메라 설정
        CameraManager.Instance.SetFreeLookZoomInOut(!isInput);
        CameraManager.Instance.SetEventFreeLook(OnEventFreeLook);
        CameraManager.Instance.SetOrthographic(GameCameraType.View, isOrthographic);
        CameraManager.Instance.SetOrthographic(GameCameraType.Popup, isPopupOrthoGraphic);

        // 4. 타운 UI 활성화
        ShowLobbyMenu(uIType);

        switch (uIType)
        {
            case UIType.LobbyView:
                {
                    CameraManager.Instance.RestoreDofTarget();
                    await lobbyMenuUnit.RefreshAsync();
                    CameraManager.Instance.SetEnableBrain(true);
                    TownObjectManager.Instance.OperateDecoratorFactory();

                    // UI 가 LobbyView 로 변경되었을 때 BGM 을 재생한다.
                    if (prevUIType != UIType.None && !StorySceneManager.Instance.IsPlaying)
                    {
                        PlayBGM();
                    }
                    else if (!SoundManager.CheckPlayingBGM())
                    {
                        PlayBGM();
                    }

                    ShowTown(true);
                    ShowTownCanvas(TownCanvasType.Input, TownCanvasType.Tag3D, TownCanvasType.Tag2D, TownCanvasType.UI, TownCanvasType.MiniMap);

                    await TownMiniMapManager.Instance.ShowTownMinimap();

                    CameraManager.Instance.OffVolumeBlur();
                    break;
                }

            case UIType.HousingSimulationView:
            case UIType.StageDungeonView:
                {
                    HideFieldMapName();
                    ShowTown(false);
                    break;
                }

            case UIType.TeamUpdateView:
                {
                    //PlayBGM();        //어느 던전에서 접근했는지에 따라 다른 BGM이 재생되어야 한다.
                    break;
                }

            default:
                {
                    if (uIType == UIType.CharacterIntroduceView)
                        CameraManager.Instance.OffVolumeBlur();
                    else if (uIType == UIType.CharacterCollectionDetailView)
                        CameraManager.Instance.OnVolumeBlur();

                    HideFieldMapName();
                    ShowTown(true);
                    ShowTownCanvas();
                    break;
                }
        }
    }

    public virtual async UniTask ChangePopupAsync(UIType uIType, UIType prevUIType)
    {
        bool isTownContents = true;// UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView);
        bool isInput = uIType == UIType.ContentsGuidePopup || uIType == UIType.ToastMessagePopup;
        bool isOrthographic = !UIManager.Instance.CheckPerspectiveUI(uIType);
        bool isStack = UIManager.Instance.GetController(uIType).GetModel<BaseModel>().IsStack;

        // 1. 플레이어 설정
        townInputSupport.SetInput(!MissionManager.Instance.IsWait && isInput);
        townInputSupport.SafeSetActive(isInput);
        CameraManager.Instance.SetFreeLookZoomInOut(!isInput);

        // stack 이 아닌 경우 input 활성화 여부를 따로 제어할 부분이 없어서 추가
        if (isStack)
        {
            CameraManager.Instance.SetOrthographic(GameCameraType.Popup, isOrthographic);
            HideFieldMapName();
        }

        //(뒤로가기 등으로 Popup이 종속된 View 가 변경된 경우 처리)
        UIType currentViewUIType = UIManager.Instance.GetCurrentViewNavigator().Controller.UIType;
        if (currentViewUIType != prevUIType)
        {
            bool isViewOrthographic = !UIManager.Instance.CheckPerspectiveUI(currentViewUIType);
            CameraManager.Instance.SetOrthographic(GameCameraType.View, isViewOrthographic);
        }

        switch (uIType)
        {
            case UIType.ApplicationPopup:
                {
                    CameraManager.Instance.SetEnableBrain(true);
                    await ShowMyPlayer(isTownContents);

                    ShowTownCanvas();
                    PlayBGM();
                    break;
                }

            case UIType.DialogPopup:

            case UIType.DialogHistoryPopup:
            case UIType.DialogSummaryPopup:
                {
                    ShowTownCanvas(TownCanvasType.Tag3D);
                    break;
                }

            case UIType.EnemyInfoDetailPopup:
            case UIType.ElementAdvantageInfoPopup:
            case UIType.WeakPointPopup:
                {
                    if (UIManager.Instance.CheckOpenUI(UIType.StageInfoWindow))
                    {
                        ShowTownCanvas(TownCanvasType.Tag3D);
                    }
                    else
                    {
                        ShowTownCanvas();
                        //ShowTownCanvas(TownCanvasType.Input, TownCanvasType.Tag3D, TownCanvasType.Tag2D, TownCanvasType.UI, TownCanvasType.MiniMap);      //이걸로 실행하면 뒤쪽에 로비 UI가 켜지니 확인 필요.
                    }
                    break;
                }

            //토스트메세지는 이전 CanvasType를 따라간다.
            case UIType.ToastMessagePopup:
            case UIType.MessageBoxPopup:
            case UIType.NickNamePopup:
                return;

            default:
                {
                    await ShowMyPlayer(isTownContents);

                    ShowTownCanvas();

                    break;
                }
        }

        ShowLobbyMenu(uIType);
    }

    public virtual async UniTask RefreshAsync(UIType uIType)
    {
        if (uIType == UIType.LobbyView)
        {
            ShowLobbyMenu(uIType);
            await UpdateLobbyMenu();
        }
    }

    public async UniTask TutorialStepAsync(TutorialExplain explainType)
    {
        switch (explainType)
        {
            case TutorialExplain.TownMove:
                {
                    TutorialManager.Instance.OnEventTutorialTouchActive(false);
                    townInputSupport.SetInput(false);
                    townInputSupport.SafeSetActive(false);
                    break;
                }

            case TutorialExplain.TownViewModeOn:
                {
                    CameraManager.Instance.SetFreeLookCamera(true);
                    CameraManager.Instance.SetFreeLookZoomInOut(true);
                    await UniTask.Delay(IntDefine.TIME_MILLISECONDS_ONE);
                    break;
                }

            case TutorialExplain.TownViewModeOff:
                {
                    CameraManager.Instance.SetFreeLookCamera(false);
                    CameraManager.Instance.SetFreeLookZoomInOut(true);
                    await UniTask.Delay(IntDefine.TIME_MILLISECONDS_ONE);
                    break;
                }

            case TutorialExplain.TownNPCTimeline:
                {
                    TownTalkUnit townTalkUnit = tagSupport.GetTagObject();
                    if (townTalkUnit != null)
                        townTalkUnit.OnEventTalk();

                    if (TutorialManager.Instance.CurrentTutorialDataId == (int)TutorialDefine.TUTORIAL_FGT_TOWN_TIMELINE_00)
                    {
                        await TutorialManager.WaitUntilEnterUI(UIType.DialogPopup);
                    }
                    break;
                }
            case TutorialExplain.LobbyApplication:
                {
                    BaseController controller = UIManager.Instance.GetController(UIType.ApplicationPopup);
                    await UIManager.Instance.EnterAsync(controller);

                    await TutorialManager.WaitUntilEnterUI(UIType.ApplicationPopup);
                    break;
                }

            case TutorialExplain.LobbyStageDungeon:
                {
                    //첫 스위핑 튜토리얼중엔 강제로 입장연출 재생
                    if (TutorialManager.Instance.CurrentTutorialDataId == (int)TutorialDefine.TUTORIAL_CONTENTS_STAGEDUNGEON_001)
                    {
                        if (PlayerPrefsWrapper.HasKey(StringDefine.KEY_PLAYER_PREFS_PLAY_STAGEDUNGEON_ANIMATION_DATE))
                            PlayerPrefsWrapper.DeleteKey(StringDefine.KEY_PLAYER_PREFS_PLAY_STAGEDUNGEON_ANIMATION_DATE);
                    }

                    BaseController controller = UIManager.Instance.GetController(UIType.StageDungeonView);
                    await UIManager.Instance.EnterAsync(controller);

                    await TutorialManager.WaitUntilEnterUI(UIType.StageDungeonView);
                    break;
                }

            case TutorialExplain.LobbyNetmining:
                {
                    //튜토리얼중엔 강제로 입장연출 재생
                    if (PlayerPrefsWrapper.HasKey(StringDefine.KEY_PLAYER_PREFS_PLAY_NETMINING_ANIMATION_DATE))
                        PlayerPrefsWrapper.DeleteKey(StringDefine.KEY_PLAYER_PREFS_PLAY_NETMINING_ANIMATION_DATE);

                    BaseController controller = UIManager.Instance.GetController(UIType.NetMiningView);
                    await UIManager.Instance.EnterAsync(controller);

                    await TutorialManager.WaitUntilEnterUI(UIType.NetMiningView);
                    break;
                }

            case TutorialExplain.LobbySimpleProfile:
                {
                    BaseController controller = UIManager.Instance.GetController(UIType.UserInfoPopup);
                    UserInfoPopupModel model = controller.GetModel<UserInfoPopupModel>();
                    model.SetUser(PlayerManager.Instance.MyPlayer.User);

                    await UIManager.Instance.EnterAsync(controller);

                    await TutorialManager.WaitUntilEnterUI(UIType.UserInfoPopup);
                    break;
                }

            case TutorialExplain.TownAutoMove:
                {
                    if (lobbyMenuUnit.GetAutoMoveButton().isActiveAndEnabled)
                    {
                        lobbyMenuUnit.GetAutoMoveButton().OnClickAutoMove();
                    }
                    else
                    {
                        //Automove_00 튜토리얼 도중 이미 된 타임라인 조건을 만족한 경우 대화 튜토리얼 즉시 실행
                        if (TutorialManager.Instance.CheckTutorialPlaying() && TutorialManager.Instance.CurrentTutorialDataId == (int)TutorialDefine.TUTORIAL_FGT_TOWN_AUTOMOVE_00)
                            TutorialManager.Instance.PlayTutorial((int)TutorialDefine.TUTORIAL_FGT_TOWN_TIMELINE_00).Forget();
                    }
                    break;
                }

            case TutorialExplain.LobbyGroundPass:
                {
                    await EventManager.Instance.GroundPassShow();
                    await TutorialManager.WaitUntilEnterUI(UIType.GroundPassPopup);
                    break;
                }
        }
    }

    private async UniTask<GameObject> GetTutorialFocusObject(TutorialExplain explainType)
    {
        switch (explainType)
        {
            case TutorialExplain.TownMove:
                {
                    return townInputSupport.VirtualJoystickSupport.IdleObject;
                }

            case TutorialExplain.TownNPCTimeline:
                {
                    townInputSupport.SetInput(false);
                    TownTalkUnit townTalkUnit = tagSupport.GetTagObject();
                    if (townTalkUnit != null)
                        return townTalkUnit.gameObject;

                    return null;
                }

            case TutorialExplain.TownAutoMove:
                {
                    return lobbyMenuUnit.GetAutoMoveButton().gameObject;
                }

            case TutorialExplain.LobbyApplication:
                {
                    return lobbyMenuUnit.ApplicationButton;
                }

            case TutorialExplain.LobbyStageDungeon:
                {
                    return lobbyMenuUnit.StageDungeonButton;
                }

            case TutorialExplain.LobbyNetmining:
                {
                    return lobbyMenuUnit.NetMiningButton.NetMiningButton;
                }

            case TutorialExplain.LobbySimpleProfile:
                {
                    return lobbyMenuUnit.GetSimpleProfileUnit().gameObject;
                }

            case TutorialExplain.LobbyGroundPass:
                {
                    return lobbyMenuUnit.GroundPassButton;
                }
        }
        return null;
    }

    private void OnEventTutorialEnd()
    {
        if (UIManager.Instance.CheckOpenCurrentUI(UIType.LobbyView))
        {
            //프롤로그 진행중에는 튜토리얼이 끝나도 이동 비활성화(자동이동을 컨트롤로 끊으면 안됨)
            if (PrologueManager.Instance.IsProgressing)
                return;

            townInputSupport.SetInput(true);
            townInputSupport.SafeSetActive(true);
        }
    }

    /// <summary> 
    /// 기본적으로 튜토리얼 진행중인동안 특정 기능을 막아놓습니다.
    /// 특정 스텝에서만 해제해야 하는 기능은 이곳에서 처리합니다.
    /// 다시 기능을 잠가야한다면 TutorialStepAsync()에 추가합니다.
    /// </summary>
    private void OnEventNoneButtonUnlockEvent(TutorialExplain explainType)
    {
        switch (explainType)
        {
            case TutorialExplain.TownMove:
                {
                    TutorialManager.Instance.OnEventTutorialTouchActive(true);
                    townInputSupport.SetInput(true);
                    townInputSupport.SafeSetActive(true);
                    break;
                }

            case TutorialExplain.TownViewModeOn:
            case TutorialExplain.TownViewModeOff:
                {
                    CameraManager.Instance.SetFreeLookZoomInOut(false);
                    break;
                }
        }
    }

    public bool GetTutorialCondition(TutorialExplain explainType)
    {
        switch (explainType)
        {
            case TutorialExplain.TownMove:
                {
                    return PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.MoveData.IsMoveInput;
                }

            case TutorialExplain.TownViewModeOn:
                {
                    return CameraManager.Instance.IsFreeLook;
                }

            case TutorialExplain.TownViewModeOff:
                {
                    return !CameraManager.Instance.IsFreeLook;
                }

            default:
                return false;
        }
    }

    private float GetUICanvasScale()
    {
        if (uIScaler.matchWidthOrHeight == 0f)
            return 1f;

        if (Screen.height > Screen.width)
            return 1f;

        return 1f / uIScaler.matchWidthOrHeight;
    }

    private void OnEventQuestTracking(BaseStoryQuest quest, QuestTrackingUIState uiState)
    {
        if (uiState == QuestTrackingUIState.Hide)
        {
            HideQuestTrackingAsync().Forget();
            return;
        }

        if (quest == null)
            return;

        var target = quest.GetCurrentTarget();
        ITownSupport townSupport = TownObjectManager.Instance.GetTownObjectByDataId(target.TargetType, target.DataId);
        bool isActiveTownObject = townSupport != null &&
            townSupport.CheckCondition();

        if (!isActiveTownObject)
        {
            HideQuestTrackingAsync().Forget();
            return;
        }

        townSupport.UpdateTag();

        ShowQuestTrackingAsync(townSupport.EnumId, quest).Forget();
    }

    public void AddEventAutoMoveCharacter(Action<CharacterParam> onEvent)
    {
        onEventAutoMoveCharacter = onEvent;
    }

    public void RemoveEventAutoMoveCharacter(Action<CharacterParam> onEvent)
    {
        onEventAutoMoveCharacter -= onEvent;
    }

    public void SetState(SceneState state)
    {
        this.state = state;
    }

    public void PlayCutsceneState(bool isShow)
    {
        SetState(isShow ? SceneState.PlayingCutscene : SceneState.None);

        gameObject.SafeSetActive(!isShow);
    }

    public void ShowFieldMapName(FieldMapDefine fieldMapDefine, bool isDelay = true)
    {
        if (VideoManager.Instance.IsPlaying)
            return;

        townMessageSupport.ShowFieldMapName(fieldMapDefine, isDelay).Forget();
    }

    public void HideFieldMapName()
    {
        townMessageSupport.HideFieldMapName();
    }

    public async UniTask ShowMyPlayer(bool isShow)
    {
        IronJade.Debug.Log("ShowMyPlayer");
        try
        {
            if (isShow)
            {
                //await PlayerManager.Instance.LoadMyPlayerCharacterObject();
                await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(true);

                PlayerManager.Instance.ShowOtherTownPlayerGroup(true);
                PlayerManager.Instance.ShowMyTownPlayerGroup(true);

                canvas[(int)TownCanvasType.Input].SafeSetActive(true);
            }
            else
            {
                await PlayerManager.Instance.MyPlayer.TownPlayer.VisibleTownObject(false);
                PlayerManager.Instance.ShowOtherTownPlayerGroup(false);
                PlayerManager.Instance.ShowMyTownPlayerGroup(false);

                //CameraManager.Instance.SetFreeLookTarget(null);

                canvas[(int)TownCanvasType.Input].SafeSetActive(false);
            }
        }
        catch (Exception e)
        {
            IronJade.Debug.Log("ShowMyPlayer Exception!!!: " + e.Message);
        }
    }

    public void PlayBGM()
    {
        FieldMapTable fieldMapTable = TableManager.Instance.GetTable<FieldMapTable>();
        FieldMapTableData fieldMapTableData = fieldMapTable.GetDataByID((int)PlayerManager.Instance.MyPlayer.CurrentFieldMap);

        if (fieldMapTableData.IsNull())
            return;

        GroundType groundType = (GroundType)fieldMapTableData.GetGROUND_TYPE();
        SoundManager.PlayTownBGM(groundType).Forget();
    }

    public void StopBGM()
    {
        IronJade.Debug.Log("Stop All BGM");

        SoundManager.StopBGM();
    }

    /// <summary>
    /// 상호작용 시 호출할 이벤트
    /// </summary>
    public void SetEventInteraction(System.Func<TownTalkInteraction, UniTask> onEventInteraction)
    {
        tagSupport.SetEventInteraction(onEventInteraction);

        TownObjectManager.Instance.SetEventTownInteraction(tagSupport.ClearTownTag,
                                                           tagSupport.ShowTag,
                                                           tagSupport.ShowSmallTalk,
                                                           tagSupport.ShowTalk);
    }

    /// <summary>
    /// 대화 상호작용 시 상태처리 및 UI
    /// </summary>
    public async UniTask TalkInteracting(bool isTalking)
    {
        // 미니맵 캔버스 비활성
        if (isTalking)
        {
            ShowTownCanvas(TownCanvasType.Tag3D);
        }
        else
        {
            if (UIManager.Instance.CheckOpenCurrentUI(UIType.LobbyView))
                ShowTownCanvas(TownCanvasType.Input, TownCanvasType.Tag3D, TownCanvasType.Tag2D, TownCanvasType.UI, TownCanvasType.MiniMap);
        }

        // 조작키를 막음
        townInputSupport.SetInput(!isTalking);

        // 이전 태그들 다 비활성
        tagSupport.Interaction(isTalking);

        // 로비 공용 메뉴를 비활성
        if (isTalking)
            await PlayLobbyMenu(false);
    }

    public async UniTask PlayLobbyMenu(bool isShow)
    {
        await lobbyMenuUnit.PlayLobbyMenu(isShow);
    }

    private void ShowLobbyMenu(UIType uIType)
    {
        lobbyMenuUnit.ShowMenuGroup(uIType == UIType.LobbyView);
    }

    public void ShowProgressBar(float value)
    {
        TownInputSupport.SafeSetActive(value == 0 || value >= 100);

        lobbyMenuUnit.ShowProgressBar(value);
    }

    public async UniTask UpdateLobbyMenu()
    {
        IronJade.Debug.Log("UpdateLobbyMenu");

        lobbyMenuUnit.Model.SetEventInteract(tagSupport.OnEventInteractFirstTalk);
        tagSupport.SetEventActiveInteractButton(lobbyMenuUnit.SetActiveButtons);
        AddEventAutoMoveCharacter(lobbyMenuUnit.OnEventAutoMoveCharacter);

        await lobbyMenuUnit.ShowAsync();
    }

    public void ShowTownCanvas(params TownCanvasType[] canvasType)
    {
        System.Func<int, bool> onEventCheckCanvas = (index) =>
        {
            if (canvasType == null)
                return false;

            for (int i = 0; i < canvasType.Length; ++i)
            {
                if ((int)canvasType[i] == index)
                    return true;
            }

            return false;
        };

        for (int i = 0; i < canvas.Length; ++i)
        {
            if (i == (int)TownCanvasType.UI)
            {
                lobbyMenuUnit.ShowMenuGroup(onEventCheckCanvas(i));
            }
            else
            {
                canvas[i].SafeSetEnable(onEventCheckCanvas(i));
            }
        }
    }

    public void SetUITouchEnable(bool isEnable)
    {
        uIRaycaster.SafeSetEnable(isEnable);
    }

    public async UniTask ShowQuestTrackingAsync(string targetKey, BaseStoryQuest quest)
    {
        var unit = await questTrackingLoader.LoadAsync<TownQuestTrackingGroupUnit>();

        IronJade.Debug.LogError("ShowQuestTrackingAsync");

        if (unit == null)
            return;

        await unit.Wait(async () =>
        {
            TownQuestTrackingGroupUnitModel model = unit.Model;

            await UniTask.WaitWhile(() => model.IsUsingModelCollection);

            if (!model.SetQuestTracking(targetKey, quest))
                return;

            await unit.ShowAsync();
        });
    }

    private async UniTask ShowAutoMoveStateBar(bool isHideStopButton)
    {
        if (autoMoveStateBarUnitLoader == null)
            return;

        AutoMoveStateBarUnit autoMoveStateBarUnit = await autoMoveStateBarUnitLoader.LoadAsync<AutoMoveStateBarUnit>();

        AutoMoveStateBarUnitModel model = new AutoMoveStateBarUnitModel();

        model.SetOnClickAutoMoveStop(OnAutoMoveStop);
        model.SetOnCheckPathFinding(PlayerManager.Instance.CheckMyTownPlayerPathFinding);
        model.SetOnAutoMoveEnd(OnAutoMoveEnd);
        model.SetHideStopButton(isHideStopButton);
        model.SetPositionType(AutoMoveStateBarUnitModel.PositionType.Bottom);
        autoMoveStateBarUnit.SetModel(model);

        await autoMoveStateBarUnit.ShowAsync();

        autoMoveStateBarUnit.SafeSetActive(true);
    }

    public async UniTask HideQuestTrackingAsync()
    {
        if (questTrackingLoader.CheckNullTarget())
            return;

        var unit = questTrackingLoader.GetTarget<TownQuestTrackingGroupUnit>();

        await unit.Wait(async () =>
        {
            TownQuestTrackingGroupUnitModel model = unit.Model;

            await UniTask.WaitWhile(() => model.IsUsingModelCollection);

            if (!model.ClearQuestTracking())
                return;

            await unit.ShowAsync();
        });
    }

    private void OnAutoMoveStop()
    {
        if (PrologueManager.Instance.IsProgressing)
        {
            IronJade.Debug.LogError("닉네임 생성 전에는 자동이동 취소를 막아놨습니다. (단방향 강제 진행 꼬이는 경우 방지)");
            return;
        }
        CharacterParam characterParam = new CharacterParam();
        characterParam.SetAutoMoveState(CharacterAutoMoveState.Stop);
        ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
    }

    private void OnAutoMoveEnd()
    {
        CharacterParam characterParam = new CharacterParam();
        characterParam.SetAutoMoveState(CharacterAutoMoveState.End);
        ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
    }

    /// <summary>
    /// 카메라 회전 상태일 때에는 UI들을 끄자
    /// </summary>
    /// 
    private ITownSupport orginalLookAtTarget = null;
    public async void OnEventFreeLook(bool isFreeLook)
    {
        // 스토리가 재생중일때는 패스
        if (StorySceneManager.Instance.IsPlaying)
            return;

        if (UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView))
        {
            //카메라 보도록 
            Transform lookTarget = CameraManager.Instance.GetFreeLookTarget();

            if (isFreeLook)
            {
                ////프리룩 인 
                CameraManager.Instance.SetCinemachineBlendTime(1f);
                StartCoroutine(SetLerpAfterDelay(lookTarget, true, 1f));  //배경 흔들림 보정/ 딜레이 이후 
                orginalLookAtTarget = PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.LookAtTarget;

                townInputSupport.SetInput(false);
                townInputSupport.SafeSetActive(false);
                ShowTownCanvas();

                await PlayLobbyMenu(false);
            }
            else
            {
                //프리룩 아웃 현재는 같은값. 
                CameraManager.Instance.SetCinemachineBlendTime(1f);
                lookTarget.gameObject.GetComponent<FollowTargetSupport>().SetLerp(false); //배경 흔들림 보정 / 이건 바로. 
                townInputSupport.SetInput(true);
                townInputSupport.SafeSetActive(!TutorialManager.Instance.CheckTutorialPlaying());

                ShowTownCanvas(TownCanvasType.Input, TownCanvasType.Tag3D, TownCanvasType.Tag2D, TownCanvasType.UI, TownCanvasType.MiniMap);
                await PlayLobbyMenu(true);
            }
        }
    }

    private IEnumerator SetLerpAfterDelay(Transform target, bool lerpValue, float delay)
    {
        yield return new WaitForSeconds(delay);
        target.gameObject.GetComponent<FollowTargetSupport>().SetLerp(lerpValue);
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case TownObserverID.BgmState:
                {
                    BoolParam boolParam = (BoolParam)observerParam;

                    if (boolParam.Value1)
                        PlayBGM();
                    else
                        StopBGM();

                    break;
                }

            case TownObserverID.ShowProgressBar:
                {
                    FloatParam floatParam = (FloatParam)observerParam;

                    ShowProgressBar(floatParam.Value1);

                    break;
                }

            case CharacterObserverID.AutoMoveCharacter:
                {
                    CharacterParam characterParam = (CharacterParam)observerParam;

                    CharacterAutoMoveState state = characterParam.AutoMoveState;
                    TownObjectType townObjectType = characterParam.AutoMoveTargetType;
                    int townObjectId = characterParam.AutoMoveTargetId;
                    FieldMapDefine fieldMap = (FieldMapDefine)characterParam.AutoMoveTargetInFieldMapId;

                    if (state == CharacterAutoMoveState.Move)
                    {
                        ITownSupport moveTarget = null;
                        if (TutorialManager.Instance.CheckTutorialPlaying() || fieldMap == PlayerManager.Instance.MyPlayer.CurrentFieldMap)
                        {
                            // 중간 경로가 있는지 확인
                            moveTarget = TownObjectManager.Instance.GetMidPointTarget(fieldMap, townObjectId);
                        }
                        else
                        {
                            var leaderCharacterPosition = PlayerManager.Instance.UserSetting.GetUserSettingData<LeaderCharacterPositionUserSetting>();

                            // 한번 갔던 필드만 워프
                            bool isWarp = leaderCharacterPosition.CheckScene(fieldMap);
                            if (!isWarp)
                            {
                                // 중간 경로가 있는지 확인
                                moveTarget = TownObjectManager.Instance.GetMidPointTarget(fieldMap, townObjectId);
                                if (moveTarget == null || !moveTarget.CheckCondition())
                                    isWarp = true;  // 씬이동을 해야 하는데 중간 경로가 없다면 강제 워프 처리
                            }

                            if (isWarp)
                            {
                                string targetDataEnumId = string.Empty;

                                if (townObjectType == TownObjectType.Npc)
                                    targetDataEnumId = ((NpcDefine)townObjectId).ToString();
                                else if (townObjectType == TownObjectType.Trigger)
                                    targetDataEnumId = ((TriggerDefine)townObjectId).ToString();
                                else if (townObjectType == TownObjectType.Gimmick)
                                    targetDataEnumId = ((GimmickDefine)townObjectId).ToString();

                                if (!string.IsNullOrEmpty(targetDataEnumId))
                                {
                                    Transition transition = Transition.LOGO;
                                    var storyQuest = MissionManager.Instance.GetCurrentTimeline();
                                    if (storyQuest != null)
                                    {
                                        StoryQuestTable storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
                                        StoryQuestTableData storyQuestTableData = storyQuestTable.GetDataByID(storyQuest.DataId);
                                        transition = (Transition)storyQuestTableData.GetSTART_TRANSITION();
                                    }

                                    MissionManager.Instance.SetAutoMove(false);

                                    TownFlowModel townFlowModel = FlowManager.Instance.GetCurrentFlow().GetModel<TownFlowModel>();
                                    townFlowModel.SetWarp(new TownFlowModel.WarpInfo(fieldMap.ToString(), targetDataEnumId, townObjectType, transition));
                                    FlowManager.Instance.ChangeStateProcess(FlowState.Warp).Forget();
                                    break;
                                }
                            }
                        }

                        if (moveTarget == null || !moveTarget.CheckCondition())
                        {
                            //gaegu 
                            IronJade.Debug.LogError("#### not exist warpTarget : " + townObjectType + " / " + townObjectId);
                            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_AUTOMOVE_NOT_FOUND_TARGET);
                            break;
                        }

                        townObjectType = moveTarget.TownObjectType;
                        townObjectId = moveTarget.DataId;

                        // 자동이동이 가능하고, 시작 전 등록된 이벤트가 있다면 호출
                        characterParam.OnEventPreAutoMove?.Invoke();
                        MissionManager.Instance.SetAutoMove(true);
                    }

                    CharacterAutoMoveState resultState = PlayerManager.Instance.MyPlayer.TownPlayer.SetPathfind(state, townObjectType, townObjectId);

                    switch (resultState)
                    {
                        case CharacterAutoMoveState.OnUI:
                            {
                                IronJade.Debug.Log($"AutoMove to {townObjectId}(DataId)");
                                ShowAutoMoveStateBar(characterParam.IsHideAutoMoveStopButton).Forget();
                                break;
                            }

                        case CharacterAutoMoveState.OffUI:
                            {
                                break;
                            }
                    }

                    onEventAutoMoveCharacter?.Invoke(characterParam);

                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    public void UpdateJoystickState()
    {
        townInputSupport.SwitchTouchAreaBySettingScreenMode(GameSettingManager.Instance.GraphicSettingModel.IsHorizontalViewMode());
    }

    /// <summary>
    /// 미션 정보가 갱신되었으므로 로비메뉴도 갱신
    /// </summary>
    public async UniTask UpdateMission(TownFlowModel.AfterMissionInfo afterMission, System.Func<StoryQuest, UniTask> onEventChangeEpisodeWarp)
    {
        // 에피소드 전환 연출
        if (afterMission.isChangeEpisode)
        {
            var storyQuestProgressModel = MissionManager.Instance.GetProgressModel(MissionContentType.StoryQuest);
            var mission = storyQuestProgressModel.GetTrackingMission<StoryQuest>();
            var storyQuestTable = TableManager.Instance.GetTable<StoryQuestTable>();
            var episodeGroupTable = TableManager.Instance.GetTable<EpisodeGroupTable>();
            var storyQuestTableData = storyQuestTable.GetDataByID(mission.NextDataQuestId);
            var episodeGroupTableData = episodeGroupTable.GetDataByID(storyQuestTableData.GetEPISODE());
            string episodeGroupNumber = string.Format(StringDefine.TRANSITION_EPISODE_TITLE_NUMBER_KEY, episodeGroupTableData.GetEPISODE_NUMBER());
            string episodeGroupName = Localization.Language == Localization.LanguageType.Eng ? string.Empty : TableManager.Instance.GetLocalization(episodeGroupTableData.GetNAME_EPISODE_TITLE());
            await TransitionManager.InEpisodeOpening(episodeGroupNumber, episodeGroupName);
            await onEventChangeEpisodeWarp(mission);
            await lobbyMenuUnit.RefreshAsync();
            await TransitionManager.OutEpisodeOpening();
        }
        else if (afterMission.goods != null)
        {
            simpleRewardScrollUnit.Model.SetGoods(afterMission.goods);
            await simpleRewardScrollUnit.SetReward();
            simpleRewardScrollUnit.PlayAnimation().Forget();

            await UniTask.Delay(afterMission.goods.Count * 600);
        }
        else if (afterMission.isRefreshUI)
        {
            await UpdateLobbyMenu();
            await PlayLobbyMenu(true);
        }
    }

#if UNITY_EDITOR
    [Space(30), SerializeField]
    private bool isShowTestButton = false;

    private void OnGUI()
    {
        if (!isShowTestButton)
            return;

        var style1 = new GUIStyle(GUI.skin.box);
        style1.normal.textColor = Color.blue;
        style1.fontSize = 40;

        GUI.Box(new Rect(200, 10, 520, 430), "Auto Move Buttons", style1);

        var style = new GUIStyle(GUI.skin.button);
        style.normal.textColor = Color.blue;
        style.fontSize = 40;

        if (GUI.Button(new Rect(200, 70, 250, 100), "NetMining", style))
        {
            AutoMoveTest(NpcDefine.NPC_TEST_04);
        }

        if (GUI.Button(new Rect(460, 70, 250, 100), "ContentsShop", style))
        {
            AutoMoveTest(NpcDefine.NPC_TEST_03);
        }

        if (GUI.Button(new Rect(200, 180, 250, 100), "LevelSync", style))
        {
            AutoMoveTest(NpcDefine.NPC_TEST_01);
        }

        if (GUI.Button(new Rect(460, 180, 250, 100), "PromptUpgrade", style))
        {
            AutoMoveTest(NpcDefine.NPC_TEST_02);
        }

        if (GUI.Button(new Rect(200, 290, 250, 100), "Stop", style))
        {
            AutoMoveStopTest();
        }
    }

    public void AutoMoveTest(NpcDefine npcDefine)
    {
        CharacterParam characterParam = new CharacterParam();
        characterParam.SetAutoMoveTarget(TownObjectType.Npc, (int)npcDefine, (int)PlayerManager.Instance.MyPlayer.CurrentFieldMap);
        characterParam.SetAutoMoveState(CharacterAutoMoveState.Move);
        ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
    }

    public void AutoMoveStopTest()
    {
        CharacterParam characterParam = new CharacterParam();
        characterParam.SetAutoMoveState(CharacterAutoMoveState.Stop);
        ObserverManager.NotifyObserver(CharacterObserverID.AutoMoveCharacter, characterParam);
    }

    public void EditorShowInputCanvas()
    {
        ShowTownCanvas(TownCanvasType.Input);
    }
#endif
    #endregion Coding rule : Function
}