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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class TimelineLobbyController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.TimelineLobbyView; } }
    public override void SetModel() { SetModel(new TimelineLobbyViewModel()); }
    private TimelineLobbyView View { get { return base.BaseView as TimelineLobbyView; } }
    private TimelineLobbyViewModel Model { get { return GetModel<TimelineLobbyViewModel>(); } }
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
        Model.SetEventEpisode(OnEventEpisode);
        Model.SetEventEpisodeList(OnEventEpisodeList);
        Model.SetEventListOpen(OnEventListOpen);
        Model.SetEventSelectChapter(OnEventSelectChapter);

        Model.SetCategory(TimelineLobbyViewModel.CategoryType.Timeline);
        Model.SetStoryQuestProgressModel(MissionManager.Instance.GetProgressModel<StoryQuestProgressModel>());
        Model.SetEpisode();
        Model.SetEpisodeList();
        Model.SetEpisodeListOpen(-1);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async UniTask<bool> Back(Func<UISubState, UniTask> onEventExtra = null)
    {
        if (Model.Category == TimelineLobbyViewModel.CategoryType.Episode)
        {
            Model.SetCategory(TimelineLobbyViewModel.CategoryType.Timeline);
            await View.ShowAsync();
            return true;
        }

        return await base.Back(onEventExtra);
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_WINDOWS + "Quest/StoryLobbyView";
    }

    /// <summary>
    /// 에피소드 팝업 오픈
    /// </summary>
    public void OnEventEpisode(int dataEpisodeGroupId)
    {
        StoryQuest storyQuest = Model.GetTimelineMission(dataEpisodeGroupId);

        if (storyQuest == null)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_TIMELINELOBBYVIEW_LOCK_TIMELINE);
            return;
        }

        TownMiniMapManager.Instance.OnEventOpenTimelinePopup(storyQuest).Forget();
    }

    /// <summary>
    /// 에피소드 목록 오픈
    /// </summary>
    public void OnEventEpisodeList(int episodeNumber)
    {
        Model.SetCategory(TimelineLobbyViewModel.CategoryType.Episode);
        Model.SetEpisodeListOpen(-1);
        Model.SetEpisodeListOpen(episodeNumber);

        View.ShowAsync().Forget();
    }

    /// <summary>
    /// 에피소드 그룹 목록을 오픈한다.
    /// </summary>
    public void OnEventListOpen(int episodeNumber)
    {
        Model.SetEpisodeListOpen(episodeNumber);
        View.ShowEpisode().Forget();
    }

    /// <summary>
    /// 챕터 선택 이벤트 (ExtraStorySelectChapterUnit과 동일한 기능)
    /// </summary>
    public void OnEventSelectChapter(int questDataId)
    {
        Model.SetSelectChapter(questDataId);
        View.ShowEpisode().Forget();
    }
    #endregion Coding rule : Function
}