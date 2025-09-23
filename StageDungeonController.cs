//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class StageDungeonController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.StageDungeonView; } }
    private StageDungeonView View { get { return base.BaseView as StageDungeonView; } }
    protected StageDungeonViewModel Model { get; private set; }
    public StageDungeonController() { Model = GetModel<StageDungeonViewModel>(); }
    public StageDungeonController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventClose(OnEventClose);
        Model.SetEventStartStage(OnEventStartStage);
        Model.SetEventRewardGet(OnEventRewardGet);
        Model.SetEventNextChapter(OnEventNextChapter);
        Model.SetEventPreviousChapter(OnEventPreviousChapter);
        Model.SetEventSwitchDifficulty(OnEventSwitchDifficulty);
        Model.SetEventSetStageName(OnEventSetStageName);
        Model.SetEventRanking(OnEventRanking);
        Model.SetEventExStage(OnEventExStage);
        Model.SetRankingOpen(ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_RANKING));

        if (!Model.IsMoveNextStage)
        {
            InitializeData();

            // 처음 입장 시 sd캐릭터 즉시 배치
            Model.ChapterUnitModel.SetPositioningState(true);
        }

        ObserverManager.AddObserver(DungeonObserverID.FocusExStage, this);
        ObserverManager.AddObserver(DungeonObserverID.DungeonExit, this);
    }

    public override async UniTask LoadingProcess()
    {
        await ShowBackground();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        CheckShowNextStage().Forget();
    }

    public override async UniTask PlayShowAsync()
    {
        bool skipAnimation = false;

        if (Model.AnimationTimeCheck)
        {
            skipAnimation = CheckPlayAnimationToday();
        }
        else
        {
            skipAnimation = Model.SkipBackgroundAnimation;
        }

        if (skipAnimation)
        {
            var stageDungeonUnit = AdditivePrefabManager.Instance.StageDungeonUnit;
            stageDungeonUnit.SkipAnimation();
            View.ChangeSfxState(2);
            View.SkipPlayShowAsync(stageDungeonUnit.SkipPlayableTime).Forget();
        }
        else
        {
            PlayerPrefsWrapper.SetString(StringDefine.KEY_PLAYER_PREFS_PLAY_STAGEDUNGEON_ANIMATION_DATE, NetworkManager.Web.ServerTimeUTC.ToString());
            View.ChangeSfxState(1);
            base.PlayShowAsync().Forget();
        }
    }

    public override UniTask PlayBackShowAsync()
    {
        return PlayShowAsync();
    }

    public override async UniTask PlayHideAsync()
    {
        base.PlayHideAsync().Forget();
    }

    private bool CheckPlayAnimationToday()
    {
        string playAt = PlayerPrefsWrapper.GetString(StringDefine.KEY_PLAYER_PREFS_PLAY_STAGEDUNGEON_ANIMATION_DATE);

        if (playAt == string.Empty)
            return false;

        DateTime playTime = DateTime.Now;

        if (DateTime.TryParse(playAt, out playTime) == false)
            return false;

        //DateTime playTime = DateTime.Parse(playAt);
        DateTime refreshTime = TimeManager.Instance.GetDailyResetTimeByDate(playTime);

        return (refreshTime - NetworkManager.Web.ServerTimeUTC).TotalSeconds > 0;
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/StageDungeon/StageDungeonView";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(DungeonObserverID.FocusExStage, this);
        ObserverManager.RemoveObserver(DungeonObserverID.DungeonExit, this);

        Model.SetIsHardMode(false);

        return await base.Exit(async (state) =>
        {
            //그라운드 패스에서 뒤돌아온 경우 UI가 남아있어서 안보이게 처리해야 한다
            if (state == UISubState.AfterLoading)
            {
                if (UIManager.Instance.CheckOpenCurrentUI(UIType.GroundPassPopup))
                    SetCanvasEnable(false);
            }

            await OnEventExtra(onEventExtra, state);
        });
    }

    private async UniTask ShowBackground()
    {
        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.StageDungeonBackground);

        var stageDungeonUnit = AdditivePrefabManager.Instance.StageDungeonUnit;
        stageDungeonUnit.Model.SetHardMode(Model.IsHardMode);
        await stageDungeonUnit.ShowAsync();
    }

    /// <summary>
    /// 스테이지 시작 팝업 호출
    /// </summary>
    private async UniTask ShowStageInfoPopup()
    {
        bool isStageDungeon = UIManager.Instance.CheckOpenCurrentView(UIType.StageDungeonView);
        BaseController stageInfoController = UIManager.Instance.GetController(UIType.StageInfoWindow);
        StageInfoPopupModel stageInfoPopupModel = stageInfoController.GetModel<StageInfoPopupModel>();
        stageInfoPopupModel.SetUser(PlayerManager.Instance.MyPlayer.User);
        stageInfoPopupModel.SetPopupByStageDungeonData(Model.GetCurrentStageData());
        stageInfoPopupModel.SetHideAfterStart(true);
        stageInfoPopupModel.SetListOpen(true);
        stageInfoPopupModel.SetExStage(false);
        stageInfoPopupModel.SetHideBackground(isStageDungeon);
        await UIManager.Instance.EnterAsync(stageInfoController, isPrevGraphicRaycaster: isStageDungeon);
    }

    private async UniTask ShowExStageInfoPopup(int dataDungeonId)
    {
        bool isStageDungeon = UIManager.Instance.CheckOpenCurrentView(UIType.StageDungeonView);
        BaseController stageInfoController = UIManager.Instance.GetController(UIType.StageInfoWindow);
        StageInfoPopupModel stageInfoPopupModel = stageInfoController.GetModel<StageInfoPopupModel>();
        stageInfoPopupModel.SetUser(PlayerManager.Instance.MyPlayer.User);
        stageInfoPopupModel.SetPopupByDungeonDataId(dataDungeonId);
        stageInfoPopupModel.SetHideAfterStart(true);
        stageInfoPopupModel.SetListOpen(true);
        stageInfoPopupModel.SetHideBackground(isStageDungeon);
        stageInfoPopupModel.SetExStage(true);
        await UIManager.Instance.EnterAsync(stageInfoController, isPrevGraphicRaycaster: isStageDungeon);
    }

    private async UniTask CheckShowNextStage()
    {
        if (!Model.IsAfterBattle)
        {
            View.RefreshAsync().Forget();
            return;
        }

        Model.SetIsAfterBattle(false);

        if (!Model.IsMoveNextStage)
        {
            View.ShowAsync().Forget();
            return;
        }

        var exStageGroupModel = DungeonManager.Instance.GetDungeonGroupModel<ExStageGroupModel>();
        var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        if (stageDungeonGroupModel == null)
            DungeonManager.Instance.AddDungeonGroupModel(new StageDungeonGroupModel());

        Model.SetStageDungeonGroupModel(stageDungeonGroupModel, exStageGroupModel);
        Model.SetIsMoveNextStage(false);
        Model.UpdateStageDungeon();
        Model.SetChapter(Model.GetCurrentStageData().GetSTAGE_DUNGEON_CHAPTER());

        await View.ShowAsync();

        InitializeData(false);

        if (Model.IsShowInfoPopup)
        {
            Model.SetIsShowInfoPopup(false);

            await ShowStageInfoPopup();
        }
    }

    private void CheckHardModeOpenCondition()
    {
        var data = Model.GetHardModeConditionStageData();

        //오픈조건이 없으면 
        if (data.IsNull())
        {
            Model.SetIsHardModeOpen(true);
            return;
        }

        var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        bool isClear = stageDungeonGroupModel.CheckClearStage(data.GetID());

        Model.SetIsHardModeOpen(isClear);
    }

    private async void OnEventClose()
    {
        // 기대 결과에 워프의 유무에 따라 워프 로직 or UI 상태 변경 로직에서 처리
        if (MissionManager.Instance.CheckHasWarpProcessByBehaviorBuffer())
        {
            await MissionManager.Instance.StartAutoProcess();
        }
        else
        {
            Exit().Forget();
        }
    }

    private void OnEventStartStage()
    {
        if (Model.IsPrologueSequence)
            return;

        if (!Model.CheckSelectCurrentStage())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_RETRY_CLEAR_STAGE);
            return;
        }

        ShowStageInfoPopup().Forget();
    }

    private void OnEventSelectStage(bool isExStage, int dataStageDungeonId)
    {
        if (View.IsPlayingAnimation)
            return;

        if (isExStage)
        {
            var exStageGroupModel = DungeonManager.Instance.GetDungeonGroupModel<ExStageGroupModel>();
            if (dataStageDungeonId <= exStageGroupModel.ClearDataExStageId)
            {
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_TOASTMESSAGE_ALREADY_CLEARED_TRANSFER);
                return;
            }

            var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
            var openExStageTableData = exStageGroupModel.GetCurrentExStageTableData(stageDungeonGroupModel);
            if (openExStageTableData.IsNull())
                return;

            if (dataStageDungeonId > openExStageTableData.GetID())
            {
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_TOASTMESSAGE_MUST_CLEAR_TRANSFER);
                return;
            }

            ShowExStageInfoPopup(openExStageTableData.GetCONNECT_DUNGEON()).Forget();
        }
        else
        {
            int currentStageDungeonId = Model.GetCurrentStageData().GetID();
            if (currentStageDungeonId != dataStageDungeonId)
            {
                if (CheckLowStage(currentStageDungeonId, dataStageDungeonId))
                    MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_RETRY_CLEAR_STAGE);
                else
                    MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_LOCKED_STAGE);

                return;
            }

            //모든 스테이지를 클리어한 경우
            var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
            if (stageDungeonGroupModel.CheckClearStage(dataStageDungeonId))
            {
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_RETRY_CLEAR_STAGE);
                return;
            }

            if (Model.IsPrologueSequence)
            {
                Model.OnPrologueSequenceComplete();
                return;
            }

            ShowStageInfoPopup().Forget();
        }
    }

    private bool CheckLowStage(int baseDataId, int compareDataId)
    {
        StageDungeonTable stageDungeonTable = TableManager.Instance.GetTable<StageDungeonTable>();
        StageDungeonTableData compareStageData = stageDungeonTable.GetDataByID(compareDataId);
        StageDungeonTableData baseStageData = stageDungeonTable.GetDataByID(baseDataId);

        if (baseStageData.GetSTAGE_DUNGEON_CHAPTER() == compareStageData.GetSTAGE_DUNGEON_CHAPTER())
        {
            return baseStageData.GetSTAGE_DUNGEON_STAGE() >= compareStageData.GetSTAGE_DUNGEON_STAGE();
        }
        else
        {
            return baseStageData.GetSTAGE_DUNGEON_CHAPTER() > compareStageData.GetSTAGE_DUNGEON_CHAPTER();
        }
    }

    private void OnEventRewardGet(int dataStageDungeonId)
    {
        int[] dataStageDungeonIds = Model.ChapterUnitModel.GetLowStageRewardableIds(dataStageDungeonId);

        RequestRewardGet(dataStageDungeonIds, (rewardDataStageDungeonIds) =>
        {
            Model.ChapterUnitModel.SetRewardGet(rewardDataStageDungeonIds);

            View.RefreshAsync().Forget();
        }).Forget();
    }

    private void OnEventNextChapter()
    {
        Model.SetNextChapter();

        View.ShowAsync().Forget();
    }

    private void OnEventPreviousChapter()
    {
        Model.SetPreviousChapter();

        View.ShowAsync().Forget();
    }

    private async void OnEventSwitchDifficulty()
    {
        if (!Model.IsHardModeOpen)
        {
            var data = Model.GetHardModeConditionStageData();
            int chapter = data.GetSTAGE_DUNGEON_CHAPTER();
            int stage = data.GetSTAGE_DUNGEON_STAGE();
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_CHANGE_DIFFICULTY_HARD_LOCK, chapter, stage);

            MessageBoxManager.ShowToastMessage(message);
            return;
        }

        Model.SetIsHardMode(!Model.IsHardMode);

        if (Model.IsHardMode)
        {
            // 하드모드로 체인지
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_CHANGE_DIFFICULTY_HARD));
        }
        else
        {
            // 노말모드로 체인지
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_STAGEDUNGEONVIEW_CHANGE_DIFFICULTY_NORMAL));
        }

        InitializeData(true);
        Model.ChapterUnitModel.SetPositioningState(true);

        await ShowBackground();

        await View.ShowAsync();
    }

    private void OnEventSetStageName(string name)
    {
        View.SetStageName(name);
    }

    private void OnEventRanking()
    {
        ContentsOpenManager.Instance.OpenContents(ContentsType.Ranking).Forget();
    }

    private async void OnEventExStage()
    {
        var exStageGroupModel = DungeonManager.Instance.GetDungeonGroupModel<ExStageGroupModel>();
        var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        var stageDungeonTableData = exStageGroupModel.GetOpenExStageChapter(stageDungeonGroupModel);
        if (stageDungeonTableData.IsNull())
            return;

        Model.SetChapter(stageDungeonTableData.GetSTAGE_DUNGEON_CHAPTER());
        await View.ShowAsync();
        View.MoveTargetStage(stageDungeonTableData.GetID());
    }

    private void InitializeData(bool isUpdate = true)
    {
        var exStageGroupModel = DungeonManager.Instance.GetDungeonGroupModel<ExStageGroupModel>();
        var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        if (stageDungeonGroupModel == null)
            DungeonManager.Instance.AddDungeonGroupModel(new StageDungeonGroupModel());

        Model.SetStageDungeonGroupModel(stageDungeonGroupModel, exStageGroupModel);
        Model.Initialize(OnEventSelectStage);

        if (isUpdate)
            Model.SetChapter(Model.GetCurrentStageData().GetSTAGE_DUNGEON_CHAPTER());

        CheckHardModeOpenCondition();
    }

    private async UniTask RequestRewardGet(int[] dataStageDungeonIds, System.Action<int[]> onEventSuccess = null)
    {
        BaseProcess stageDungeonRewardProcess = NetworkManager.Web.GetProcess(WebProcess.StageDungeonReward);

        StageDungeonRewardRequest request = stageDungeonRewardProcess.GetRequest<StageDungeonRewardRequest>();
        request.SetDataStageDungeonIds(dataStageDungeonIds);

        if (await stageDungeonRewardProcess.OnNetworkAsyncRequest())
        {
            stageDungeonRewardProcess.OnNetworkResponse();

            StageDungeonGroupModel dungeonClearModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
            onEventSuccess?.Invoke(dungeonClearModel.RewardedStageDungeonDataIds.ToArray());
        }
    }

    void IObserver.HandleMessage(System.Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case DungeonObserverID.FocusExStage:
                {
                    OnEventExStage();
                    break;
                }

            case DungeonObserverID.DungeonExit:
                {
                    DungeonExitParam dungeonExitParam = (DungeonExitParam)observerParam;

                    if (!Model.CheckLastStage())
                    {
                        Model.SetIsMoveNextStage(dungeonExitParam.IsWin);

                        if (dungeonExitParam.DungeonExitType == DungeonExitParam.ExitType.GoNext)
                            Model.SetIsShowInfoPopup(true);
                    }

                    Model.SetIsAfterBattle(true);
                }
                break;
        }
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.StageDungeonStageSlot:
                {
                    OnEventStartStage();
                    await TutorialManager.WaitUntilEnterUI(UIType.StageInfoWindow);
                    break;
                }
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.StageDungeonStageSlot:
                {
                    await UniTask.WaitUntil(() => View != null && View.GetStepUnit() != null);
                    return View.GetStepUnit().gameObject;
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }

    }
    #endregion Coing rule : Function
}