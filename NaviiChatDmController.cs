//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class NaviiChatDmController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.NaviiChatDmView; } }
    private NaviiChatDmView View { get { return base.BaseView as NaviiChatDmView; } }
    protected NaviiChatDmViewModel Model { get; private set; }
    public NaviiChatDmController() { Model = GetModel<NaviiChatDmViewModel>(); }
    public NaviiChatDmController(BaseModel baseModel) : base(baseModel) { }

    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isBlockNextMessage = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventFinishMessageTween(() => { OnEventFinishMessageTween().Forget(); });
        Model.SetEventNextMessage(OnEventNextMessage);
        Model.SetEventSelectTurningPoint(OnEventSelectTurningPoint);

        Model.SetEventHome(OnEventHome);
        Model.SetEventFollowing(OnEventFollowing);
        Model.SetEventMyProfile(OnEventMyProfile);

    }

    public override async UniTask LoadingProcess()
    {
        await SetEpisodeLists();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        isBlockNextMessage = false;
        View.SetBlockScroll(true);
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (UIManager.Instance.CheckOpenUI(UIType.NaviiChatProfileView, includeDisableUI: true))
        {
            OnEventMyProfile();
            return true;
        }
        else
        {
            OnEventFollowing();
            return true;
        }
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        TokenPool.Cancel(GetHashCode());
        View.ClearEpisodes();

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/NaviiChatDmView";
    }

    private async UniTask OnEventFinishMessageTween()
    {
        View.ForceScroll().Forget();
        await UniTask.Delay(200);
        isBlockNextMessage = false;
        View.SetBlockScroll(true);
    }

    private void OnEventNextMessage()
    {
        if (isBlockNextMessage)
            return;

        if (Model.EpisodeList.Count == 0)
            return;

        if (Model.CurrentEpisode.MaxDmIndex <= Model.CurrentEpisode.CurrentDmIndex)
        {
            var currentEpisodeInfo = NaviiChatManager.Instance.GetDmEpisode(Model.Character, Model.CurrentEpisode.EpisodeDataId);
            if (!currentEpisodeInfo.CheckNextDMExists() && !currentEpisodeInfo.IsRewarded)
            {
                isBlockNextMessage = true;
                RequestEpisodeClear(Model.CurrentEpisode.EpisodeDataId).Forget();
            }
        }
        else
        {
            isBlockNextMessage = true;
            View.SetBlockScroll(false);
            int newIndex = Model.CurrentEpisode.CurrentDmIndex + 1;
            Model.CurrentEpisode.SetCurrentDmIndex(newIndex);
            View.RefreshDm().Forget();

            NaviiChatManager.Instance.SetReadIndex(Model.Character.DataId, Model.CurrentEpisode.EpisodeDataId, newIndex);
            NaviiChatManager.Instance.SaveDmReadData();
        }
    }

    private void OnEventSelectTurningPoint(int index)
    {
        if (isBlockNextMessage)
            return;

        isBlockNextMessage = true;
        View.SetBlockScroll(false);

        NaviiChatDmEpisodeInfoModel episodeInfoModel = NaviiChatManager.Instance.GetCurrentDmEpisode(Model.Character);
        CharacterDMTableData currentDmData = episodeInfoModel.SelectDmLists[episodeInfoModel.SelectDmLists.Count - 1];

        RequestEpisodeChoice(currentDmData.GetNEXT_DIALOG(index), selectIndex: index).Forget();
    }

    private void OnEventHome()
    {
        View.ClearEpisodes();

        if (NaviiChatManager.Instance.IsContainNaviiChatViewGroup(UIManager.Instance.GetBackUIType()))
        {
            if (UIManager.Instance.CheckOpenUI(UIType.NaviiChatView, includeDisableUI: true))
            {
                UIManager.Instance.BackToTarget(UIType.NaviiChatView).Forget();
            }
            else
            {
                //다른 경로로 NaviiChatView를 건너뛰고 온 경우
                BaseController controller = UIManager.Instance.GetController(UIType.NaviiChatView);
                UIManager.Instance.EnterAsync(controller).Forget();
            }
        }
        else
        {
            BaseController controller = UIManager.Instance.GetController(UIType.NaviiChatView);
            UIManager.Instance.EnterAsync(controller).Forget();
        }
    }

    private void OnEventFollowing()
    {
        View.ClearEpisodes();

        UIManager.Instance.RemoveToTarget(UIType.NaviiChatFollowingView, exceptUI: UIType.NaviiChatView);
        if (UIManager.Instance.CheckBackUI(UIType.NaviiChatFollowingView))
        {
            UIManager.Instance.Back();
            return;
        }

        UIManager.Instance.EnterAsync(UIType.NaviiChatFollowingView).Forget();
    }

    private void OnEventMyProfile()
    {
        View.ClearEpisodes();

        UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView, exceptUI: UIType.NaviiChatView);

        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    /// <summary> 모든 에피소드 세팅을 진행합니다. 현재 에피소드가 하나도 안 읽은 에피소드라면 첫 번째 선택지로 START_DIALOG를 호출합니다.</summary>
    private async UniTask SetEpisodeLists()
    {
        var episodeLists = NaviiChatManager.Instance.GetDmEpisodesByCharacter(Model.Character);

        if (episodeLists.Count > 0)
        {
            //마지막 에피소드가 시작되지 않은 상태라면 네트워크 호출
            var currentEpisode = episodeLists[episodeLists.Count - 1];
            if (currentEpisode.SelectDmLists.Count == 0)
            {
                await RequestEpisodeChoice(currentEpisode.GetNextDMData().GetID());
            }

            Model.SetEpisodeList(NaviiChatManager.Instance.GetDmEpisodesByCharacter(Model.Character));
        }
        else
        {
            Model.EpisodeList.Clear();
        }
    }

    /// <param name="selectIndex">최초 실행시 세팅시 -1. 선택지 선택으로 에피소드 업데이트시 0 이상의 값이 세팅됩니다.</param>
    private async UniTask RequestEpisodeChoice(int dmDataId, int selectIndex = -1)
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.CharacterDmChoice);
        process.SetPacket(new ChoiceCharacterDmInDto(dmDataId));

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();

            if (selectIndex != -1)
            {
                Model.UpdateEpisode(selectIndex, NaviiChatManager.Instance.GetCurrentDmEpisode(Model.Character));
                await View.RefreshEpisode();

                isBlockNextMessage = false;
                View.SetBlockScroll(true);
            }
        }
    }

    private async UniTask RequestEpisodeClear(int dmGroupDataId)
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.CharacterDmComplete);
        process.SetPacket(new CompleteCharacterDmInDto(dmGroupDataId));

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();
            Model.CurrentEpisode.SetComplete(true);

            var newEpisode = NaviiChatManager.Instance.GetCurrentDmEpisode(Model.Character);
            if (newEpisode != null)
            {
                await RequestEpisodeChoice(newEpisode.GetNextDMData().GetID());
                Model.AddEpisode(NaviiChatManager.Instance.GetCurrentDmEpisode(Model.Character));
                await View.RefreshDMEpisodes();
            }

            isBlockNextMessage = false;
        }
    }
    #endregion Coding rule : Function
}