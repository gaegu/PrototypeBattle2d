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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class MissionController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.MissionView; } }
    private MissionView View { get { return base.BaseView as MissionView; } }
    protected MissionViewModel Model { get; private set; }
    public MissionController() { Model = GetModel<MissionViewModel>(); }
    public MissionController(BaseModel baseModel) : base(baseModel) { }
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
        ObserverManager.AddObserver(CommonObserverID.DailyRefreshData, this);

        Model.SetApplication(true);
        Model.SetEventChangeTab(OnEventChangeTab);
        Model.SetEventAllGetReward(OnEventAllGetReward);
        Model.SetEventClose(OnEventClose);
        Model.SetEventRewardGet(OnEventGetReward);
        Model.SetEventShortcut(OnEventShortcut);
        Model.SetEventActiveRewardButton(OnEventActiveRewardAllButton);
        Model.SetEventUpdateRedDots(UpdateRedDots);
        Model.SetEventResetTimeGet(OnEventDailyResetTimeGet);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestMissionGet();
        await RequestEventMissionGet();
    }

    public override async UniTask Process()
    {
        UpdateMissionList();

        await ShowDefaultView();

        View.SetActiveEventTab();
    }

    public override void Refresh()
    {
        UpdateMissionList();

        View.ShowRewardButton();
        View.SetActiveEventTab();

        UpdateShowingMissions().Forget();
    }

    public override async UniTask<bool> Exit(Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Mission/MissionView";
    }

    private void OnEventChangeTab()
    {
        switch (Model.CurrentContentType)
        {
            case MissionViewModel.ContentType.Daily:
            case MissionViewModel.ContentType.Weekly:
                View.ShowRepeatMission().Forget();
                break;

            case MissionViewModel.ContentType.Achieve:
                View.ShowAchieveMission().Forget();
                break;

            case MissionViewModel.ContentType.Event:
                {
                    var eventInfo = EventManager.Instance.GetEvent(ContentsEventType.EventMission, 0);
                    Model.SetEventTitlePath(eventInfo.desc);
                    View.ShowEventMission().Forget();
                }
                break;
        }

        View.ShowTitle();
        View.ShowRewardButton();
    }

    private async void OnEventAllGetReward()
    {
        if (!Model.CheckExistAllReward(Model.CurrentContentType))
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MISSION_NOTICE_NO_ITEM);
            return;
        }

        Model.OnEventExistPointRewardGet();

        OnEventGetReward(Model.GetCompleteMissionDataIds());
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private async UniTask GetReward(int[] completeMissionDataIds)
    {
        if (completeMissionDataIds == null || completeMissionDataIds.Length == 0)
            return;

        switch (Model.CurrentContentType)
        {
            case MissionViewModel.ContentType.Event:
                await RequestEventMissionReward(completeMissionDataIds);
                break;

            default:
                await RequestMissionReward(completeMissionDataIds);
                break;
        }
    }

    private async void OnEventGetReward(int[] completeMissionDataIds)
    {
        await GetReward(completeMissionDataIds);
    }

    private void OnEventShortcut(BaseMission mission)
    {
        OnEventShortcutAsync(mission).Forget();
    }

    private async UniTask OnEventShortcutAsync(BaseMission mission)
    {
        await ContentsOpenManager.Instance.ShowCutContents(mission.Condition);
    }

    private void OnEventActiveRewardAllButton(bool isActive)
    {
        View.SetActiveRewardAllButton(isActive);
    }

    private DateTime OnEventDailyResetTimeGet()
    {
        return TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);
    }

    private async UniTask RequestMissionGet()
    {
        await MissionManager.Instance.RequestGet(MissionContentType.Mission);
    }

    private async UniTask RequestEventMissionGet()
    {
        await MissionManager.Instance.RequestGet(MissionContentType.EventMission);

        Model.SetIsExistEventMission(EventManager.Instance.CheckOnGoingEvent(ContentsEventType.EventMission));
    }

    private async UniTask RequestMissionReward(int[] missionDataIds)
    {
        BaseProcess missionRewardProcess = NetworkManager.Web.GetProcess(WebProcess.MissionReward);

        missionRewardProcess.SetPacket(new RewardMissionInDto(missionDataIds));

        if (await missionRewardProcess.OnNetworkAsyncRequest())
        {
            await missionRewardProcess.OnNetworkAsyncResponse();

            UpdateMissionList();

            await View.RefreshAsync();
        }
    }

    private async UniTask RequestEventMissionReward(int[] missionDataIds)
    {
        if (CheckEndEventMission())
        {
            MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_MISSION_NOTICE_EVENT_TIME_END).Forget();
            return;
        }

        BaseProcess eventMissionRewardProcess = NetworkManager.Web.GetProcess(WebProcess.EventMissionReward);

        eventMissionRewardProcess.SetPacket(new RewardEventMissionInDto(missionDataIds));

        if (await eventMissionRewardProcess.OnNetworkAsyncRequest())
        {
            await eventMissionRewardProcess.OnNetworkAsyncResponse();

            UpdateMissionList();

            await View.RefreshAsync();
        }
    }

    private bool CheckEndEventMission()
    {
        var eventMissionData = EventManager.Instance.GetEvent(ContentsEventType.EventMission, 0);
        var resetTime = TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);
        var passedTime = resetTime - eventMissionData.EndAt;

        return passedTime.TotalDays >= 1;
    }

    private void UpdateMissionList()
    {
        for (MissionViewModel.ContentType contentType = MissionViewModel.ContentType.Event; contentType < MissionViewModel.ContentType.Max; contentType++)
        {
            IMissionProgressSet missionSet = null;

            switch (contentType)
            {
                case MissionViewModel.ContentType.Event:
                    {
                        if (!EventManager.Instance.CheckOnGoingEvent(ContentsEventType.EventMission))
                            continue;

                        missionSet = MissionManager.Instance.GetProgressModel(MissionContentType.EventMission);
                        break;
                    }

                case MissionViewModel.ContentType.Daily:
                case MissionViewModel.ContentType.Weekly:
                case MissionViewModel.ContentType.Achieve:
                    {
                        missionSet = MissionManager.Instance.GetProgressModel(MissionContentType.Mission);
                        break;
                    }
            }

            Model.SetMissions(contentType, missionSet);
        }

        UpdateRedDots();
    }

    private void UpdateRedDots()
    {
        bool[] rewardReadyMissions = new bool[(int)MissionViewModel.ContentType.Max];

        EventMissionProgressModel eventMissionProgressModel = MissionManager.Instance.GetProgressModel<EventMissionProgressModel>();
        rewardReadyMissions[(int)MissionViewModel.ContentType.Event] = eventMissionProgressModel.CheckExistRewardReady();

        MissionProgressModel missionProgressModel = MissionManager.Instance.GetProgressModel<MissionProgressModel>();
        rewardReadyMissions[(int)MissionViewModel.ContentType.Daily] = missionProgressModel.CheckExistRewardReady(MissionType.Daily);
        rewardReadyMissions[(int)MissionViewModel.ContentType.Weekly] = missionProgressModel.CheckExistRewardReady(MissionType.Weekly);
        rewardReadyMissions[(int)MissionViewModel.ContentType.Achieve] = missionProgressModel.CheckExistRewardReady(MissionType.Once);

        Model.SetRewardReadyMissionCount(rewardReadyMissions);

        View.ShowRedDots();
        View.ShowRewardButton();
    }

    private async UniTask UpdateShowingMissions()
    {
        if (!await View.UpdateShowingMissions())
        {
            View.SetToggleTab(Model.CurrentContentType);

            OnEventChangeTab();
        }
    }

    private async UniTask ShowDefaultView()
    {
        Model.OnEventChangeTabType((int)MissionViewModel.ContentType.Daily);

        await View.ShowAsync();
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.MissionGetRewardAll:
                {
                    if (!Model.CheckExistAllReward(Model.CurrentContentType))
                    {
                        break;
                    }
                    else
                    {
                        Model.OnEventExistPointRewardGet();
                        await GetReward(Model.GetCompleteMissionDataIds());
                        break;
                    }
                }
        }
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case CommonObserverID.DailyRefreshData:
                {
                    Model.SetIsExistEventMission(EventManager.Instance.CheckOnGoingEvent(ContentsEventType.EventMission));
                    View.ShowAsync().Forget();
                    MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_INFINITYCIRCUITLOBBYVIEW_DAY_CHANGED).Forget();        //나중에 공용으로 만들 것
                    break;
                }
        }
    }

    #endregion Coding rule : Function
}