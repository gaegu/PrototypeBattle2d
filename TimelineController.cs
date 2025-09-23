//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Threading.Tasks;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.UI.Core;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class TimelineController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.TimelinePopup; } }
    public override void SetModel() { SetModel(new TimelinePopupModel()); }
    private TimelinePopup View { get { return base.BaseView as TimelinePopup; } }
    private TimelinePopupModel Model { get { return GetModel<TimelinePopupModel>(); } }
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
        Model.SetEventAutoMove(OnEventAutoMove);
        Model.SetEventAccept(OnEventAccept);
        Model.SetEpisodeInfo();
        Model.SetEpisodeStory();
        Model.SetEpisodeMission();
        Model.SetEpisodeReward();

        ObserverManager.AddObserver(ViewObserverID.Refresh, this);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async void Refresh()
    {
        try
        {
            if (View.CheckSafeNull())
            {
                ObserverManager.RemoveObserver(ViewObserverID.Refresh, this);
                return;
            }

            await View.ShowAsync();
        }
        catch
        {
            ObserverManager.RemoveObserver(ViewObserverID.Refresh, this);
        }
    }

    public override async UniTask<bool> Exit(Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(ViewObserverID.Refresh, this);

        return await base.Exit(HideControllabelMinimapUnit);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_WINDOWS + "Quest/StoryQuestInfoPopup";
    }

    private async void OnEventAccept()
    {
        StoryQuest storyQuest = Model.Mission;

        if (!storyQuest.CheckProgressState(OpenConditionProgressState.UnAccepted))
            return;

        if (await MissionManager.Instance.RequestAccept(storyQuest))
        {
            Model.SetEpisodeInfo();
            ObserverManager.NotifyObserver(ViewObserverID.Refresh, null);
        }
    }

    private async void OnEventAutoMove()
    {
        // 자동이동 하겠냐 물어본다.
        // 한다고 하면 같은 필드이면 마을로 보내버리고 아니면 워프

        bool isCheckBoxVisible = MissionManager.Instance.CheckQuestAutoMoveWarningWeeklyReset(false);
        bool isChangeTarget = Model.Mission.DataId != BaseStoryQuest.TrackingDataId;

        if (isChangeTarget && isCheckBoxVisible)
        {
            MessageBoxManager.ShowYesNoCheckBox(LocalizationDefine.LOCALIZATION_TIMELINEPOPUP_AUTOMOVE_CHECK,
                                                LocalizationDefine.LOCALIZATION_TIMELINEPOPUP_AUTOMOVE_CHECKBOX,
                onEventConfirm: async () =>
                {
                    await MissionManager.Instance.SetTrackingDataId(Model.Mission.DataId);
                    MissionManager.Instance.OnEventAutoMove(true);
                },
                onEventCheckBox: (value) =>
                {
                    if (value)
                    {
                        MissionManager.Instance.CheckQuestAutoMoveWarningWeeklyReset(true);
                    }
                }).Forget();
            return;
        }

        await MissionManager.Instance.SetTrackingDataId(Model.Mission.DataId);
        MissionManager.Instance.OnEventAutoMove(true);
    }

    public async UniTask HideControllabelMinimapUnit(UISubState subState)
    {
        if (subState == UISubState.AfterLoading)
            await View.HideControllableMinimapUnit();
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case ViewObserverID.Refresh:
                {
                    Refresh();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}