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
using IronJade.Observer.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ReferenceCheckDetailController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ReferenceCheckDetailView; } }
    public override void SetModel() { SetModel(new ReferenceCheckDetailViewModel()); }
    private ReferenceCheckDetailView View { get { return base.BaseView as ReferenceCheckDetailView; } }
    private ReferenceCheckDetailViewModel Model { get { return GetModel<ReferenceCheckDetailViewModel>(); } }
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
        Model.SetCharacterQuestProgressModel(MissionManager.Instance.GetProgressModel<CharacterQuestProgressModel>());
        Model.SetQuestList(OnEventReward, OnEventAccept, OnEventEpisode, OnEventDetail);

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

            Model.SetQuestList(OnEventReward, OnEventAccept, OnEventEpisode, OnEventDetail);
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

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Story/ReferenceCheckDetailView";
    }

    private void OnEventReward(int dataCharacterQuestGroupId)
    {
        var rewardInfoController = UIManager.Instance.GetController(UIType.StageDungeonRewardInfoPopup);
        var rewardInfoModel = rewardInfoController.GetModel<StageDungeonRewardInfoPopupModel>();
        rewardInfoModel.SetCharacterQUestReward(dataCharacterQuestGroupId);
        UIManager.Instance.EnterAsync(rewardInfoController).Forget();
    }

    private async void OnEventAccept(int dataCharacterQuestGroupId)
    {
        StoryQuest storyQuest = Model.GetTimelineMission(dataCharacterQuestGroupId);

        if (Model.GetEpisodeState(dataCharacterQuestGroupId) == EpisodeState.Lock)
        {
            MessageBoxManager.ShowToastMessage(Model.GetErrorMessage(dataCharacterQuestGroupId));
            return;
        }

        if (Model.GetEpisodeState(dataCharacterQuestGroupId) != EpisodeState.UnAccept)
            return;

        if (await MissionManager.Instance.RequestAccept(storyQuest))
        {
            Model.SetAcceptReferenceCheckUnitModel(dataCharacterQuestGroupId);
            await View.ShowAsync();

            OnEventEpisode(dataCharacterQuestGroupId);
        }
    }

    private void OnEventEpisode(int dataCharacterQuestGroupId)
    {
        StoryQuest storyQuest = Model.GetTimelineMission(dataCharacterQuestGroupId);

        if (storyQuest == null)
        {
            MessageBoxManager.ShowToastMessage(Model.GetErrorMessage(dataCharacterQuestGroupId));
            return;
        }

        if (Model.GetEpisodeState(dataCharacterQuestGroupId) == EpisodeState.Complete)
            return;

        TownMiniMapManager.Instance.OnEventOpenTimelinePopup(storyQuest).Forget();
    }

    private async void OnEventDetail(string bigThumbnailPath, EpisodeState progressState)
    {
        BaseController referenceCheckIllustController = UIManager.Instance.GetController(UIType.ReferenceCheckIllustPopup);
        var referenceCheckIllustModel = referenceCheckIllustController.GetModel<ReferenceCheckIllustPopupModel>();
        referenceCheckIllustModel.SetThumbnailPath(bigThumbnailPath);
        referenceCheckIllustModel.SetProgressState(progressState);
        await UIManager.Instance.EnterAsync(referenceCheckIllustController);
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