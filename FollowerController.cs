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

public class FollowerController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.FollowerView; } }
    private FollowerView View { get { return base.BaseView as FollowerView; } }
    protected FollowerViewModel Model { get; private set; }
    public FollowerController() { Model = GetModel<FollowerViewModel>(); }
    public FollowerController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventHome(OnEventHome);
        Model.SetEventBack(OnEventBack);
        Model.SetEventFriend(OnEventFriend);
        Model.SetEventUserProfile(OnEventUserProfile);
        Model.SetEventCategory(OnEventCategory);
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetEventDm(OnEventDm);
        Model.SetEventFollowAccept(OnEventFollowAccept);
        Model.SetEventRefusalFollow(OnEventRefusalFollow);

        Model.SetViewModeState(FollowerViewModel.ViewModeState.Character);
    }

    public override void BackEnter()
    {
        Model.SetEventHome(OnEventHome);
        Model.SetEventBack(OnEventBack);
        Model.SetEventFriend(OnEventFriend);
        Model.SetEventUserProfile(OnEventUserProfile);
        Model.SetEventCategory(OnEventCategory);
        Model.SetEventDm(OnEventDm);
        Model.SetEventFollowAccept(OnEventFollowAccept);
        Model.SetEventRefusalFollow(OnEventRefusalFollow);
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        NaviiChatManager.ExitAllNaviiChatView(UIType.FollowerView);
        return await Exit(onEventExtra);
    }

    public override async UniTask LoadingProcess()
    {
        TableManager.Instance.LoadTable<LikeAbilityEpisodeTable>();

        await SetViewMode(false);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async void Refresh()
    {
        await SetViewMode(false);

        await View.RefreshAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/FollowerView";
    }

    private void OnEventHome()
    {
        if (NaviiChatManager.Instance.IsContainNaviiChatViewGroup(UIManager.Instance.GetBackUIType()))
        {
            UIManager.Instance.BackToTarget(UIType.NaviiChatView).Forget();
        }
        else
        {
            BaseController controller = UIManager.Instance.GetController(UIType.NaviiChatView);

            UIManager.Instance.EnterAsync(controller).Forget();
        }
    }

    private void OnEventBack()
    {
        NaviiChatManager.ExitAllNaviiChatView(UIType.FollowerView);

        Exit().Forget();
    }

    private void OnEventFriend()
    {
        if (UIManager.Instance.CheckBackUI(UIType.FriendView))
        {
            UIManager.Instance.Back();
            return;
        }

        UIManager.Instance.EnterAsync(UIType.FriendView).Forget();
    }

    private void OnEventUserProfile()
    {
        UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView);

        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventCategory(int category)
    {
        FollowerViewModel.ViewModeState viewMode = (FollowerViewModel.ViewModeState)category;

        if (viewMode == Model.ViewMode)
            return;

        Model.SetViewModeState(viewMode);

        SetViewMode(true).Forget();
    }

    private async UniTask SetViewMode(bool isUI)
    {
        if (Model.ViewMode == FollowerViewModel.ViewModeState.User)
        {
            if (Model.Category == FollowerViewModel.CategoryType.Follower)
            {
                await RequestFriendGet();
            }
            else
            {
                await RequestFriendGet();
                await RequestChatRoomGet();
            }
        }
        else if (Model.ViewMode == FollowerViewModel.ViewModeState.Character)
        {
            var characterModel = PlayerManager.Instance.MyPlayer.User.CharacterModel;
            var likeAbilityQuests = NaviiChatManager.Instance.GetLikeAbilityQuestsByEpisode();

            if (Model.Category == FollowerViewModel.CategoryType.Follower)
                Model.SetFollowerCharacterFriendListUnitModels(characterModel);
            else
                Model.SetFollowingCharacterFriendListUnitModels(characterModel, likeAbilityQuests);
        }

        if (isUI)
            await View.ShowAsync();
    }

    private async UniTask RequestFriendGet()
    {
        FriendGetProcess friendGetProcess = NetworkManager.Web.GetProcess<FriendGetProcess>();

        friendGetProcess.Request.SetGetFriendInDto(new GetFriendInDto(Model.RequestFriendState));

        if (await friendGetProcess.OnNetworkAsyncRequest())
        {
            Model.SetFriendListUnitModels(friendGetProcess.GetFriends());
        }
    }

    private async UniTask RequestChatRoomGet()
    {
        BaseProcess chatRoomGetProcess = NetworkManager.Web.GetProcess(WebProcess.ChatRoomGet);

        chatRoomGetProcess.SetPacket(new GetChatRoomInDto((int)FriendRoomType.DM));

        if (await chatRoomGetProcess.OnNetworkAsyncRequest())
        {
            Model.SetRommFriendListUnitModels((chatRoomGetProcess as ChatRoomGetProcess).GetFriends());
        }
    }

    private void OnEventCharacterSelect(FriendListUnitModel model)
    {
        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.RemoveStackByTarget(UIType.NaviiChatProfileView);
        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventDm(FriendListUnitModel model)
    {
        //유저 DM은 NaviiChatDmView가 아닌 다른 UI로 수정 예정
    }

    /// <summary>
    /// 친구 신청 수락
    /// </summary>
    private void OnEventFollowAccept(FriendListUnitModel model)
    {
        if (model.Friend.IsFollow)
            return;

        RequestCharacterFollowProcess(model).Forget();
    }

    /// <summary>
    /// 친구 신청 거절
    /// </summary>
    private void OnEventRefusalFollow(FriendListUnitModel model)
    {
        RequestFriendRequestCancel(model).Forget();
    }


    private async UniTask RequestCharacterFollowProcess(FriendListUnitModel model)
    {
        CharacterFollowProcess characterFollowProcess = NetworkManager.Web.GetProcess<CharacterFollowProcess>();

        characterFollowProcess.Request.SetFollowCharacterInDto(new FollowCharacterInDto(model.Friend.UserId));

        if (await characterFollowProcess.OnNetworkAsyncRequest())
        {
            characterFollowProcess.OnNetworkResponse();

            Model.SetFriendFollowByUserId(model.Friend.UserId);

            View.ShowFollowerFrendScroll(isReset: false);
        }
    }

    private async UniTask RequestFriendRequestCancel(FriendListUnitModel model)
    {
        BaseProcess friendDeleteProcess = NetworkManager.Web.GetProcess(WebProcess.FriendDelete);

        friendDeleteProcess.SetPacket(new DeleteFriendInDto(model.Friend.UserId));

        if (await friendDeleteProcess.OnNetworkAsyncRequest())
        {
            friendDeleteProcess.OnNetworkResponse(Model);

            View.ShowFollowerFrendScroll(isReset: false);
        }
    }

    #endregion Coding rule : Function
}