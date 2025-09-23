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

public class FriendController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.FriendView; } }
    private FriendView View { get { return base.BaseView as FriendView; } }
    protected FriendViewModel Model { get; private set; }
    public FriendController() { Model = GetModel<FriendViewModel>(); }
    public FriendController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventCategory(OnEventCategory);
        Model.SetEventSearch(OnEventSearch);
        Model.SetEventRefresh(OnEventRefresh);
        Model.SetChangedSearchInput(OnChangedSearchInput);
        Model.SetEventSelectDeleteState(OnEventSelectDeleteState);
        Model.SetEventDeleteFriend(OnEventDeleteFriend);
        Model.SetEventBlockManagement(OnEventBlockManagement);
        Model.SetEventFriendPointShare(OnEventFriendPointShare);
        Model.SetEventHome(OnEventHome);
        Model.SetEventBack(OnEventBack);
        Model.SetEventFriend(OnEventFriend);
        Model.SetEventUserProfile(OnEventUserProfile);
        Model.SetEventFriendRequestSubTab(OnEventFriendRequestSubTab);
        Model.SetEventDm(OnEventDm);
        Model.SetEventFollow(OnEventFollow);
        Model.SetEventReceiveFriendPoint(OnEventReceiveFriendPoint);
        Model.SetEventSendFriendPoint(OnEventSendFriendPoint);
        Model.SetEventDeleteFriendMarking(OnEventDeleteFriendMarking);
        Model.SetEventCancelFollow(OnEventCnacelFollow);
        Model.SetEventFavoriteCancel(OnEventFavoriteCancel);
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetEventCharacterHold(OnEventCharacterHold);

        Model.SetViewModeState(FriendViewModel.ViewModeState.Recommend);
        Model.SetSearch(false);
    }

    public override void BackEnter()
    {
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        NaviiChatManager.ExitAllNaviiChatView(UIType.FriendView);
        return await Exit(onEventExtra);
    }

    public override async UniTask LoadingProcess()
    {
        // 현재 뷰 상태에 맞는 친구 목록을 불러오자
        await RequestOtherUserRecommend();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/FriendView";
    }

    private void OnEventCategory(int category)
    {
        FriendViewModel.ViewModeState viewModeState = (FriendViewModel.ViewModeState)category;

        if (viewModeState == Model.ViewMode)
            return;

        Model.SetViewModeState(viewModeState);
        Model.SetSearch(false);

        ProcessFriendGet().Forget();
    }

    private void OnEventSearch(string search)
    {
        RequestFriendGetNickName(search).Forget();
    }

    private void OnEventRefresh()
    {
        Model.SetSearch(false);

        ProcessFriendGet().Forget();
    }

    private void OnChangedSearchInput(string search)
    {
        bool isEmptySearch = string.IsNullOrEmpty(search);

        if (isEmptySearch)
        {
            if (Model.IsSearch)
            {
                Model.SetSearch(false);
                View.ShowSearchFriend().Forget();
                return;
            }
        }

        Model.SetSearch(!isEmptySearch);
    }

    private void OnEventSelectDeleteState()
    {
        Model.SetSelectDeleteMenu(!Model.IsSelectDeleteMenu);
        Model.RefreshFriendListUnitModelsByDeleteMenu();

        View.ShowFriendScroll(isReset: false);
        View.ShowBottomMenu();
    }

    private void OnEventDeleteFriend()
    {
        RequestFriendDelete().Forget();
    }

    private void OnEventBlockManagement()
    {
        BaseController friendBlockListController = UIManager.Instance.GetController(UIType.FriendBlockListPopup);
        UIManager.Instance.EnterAsync(friendBlockListController).Forget();
    }

    private void OnEventFriendPointShare()
    {
        RequestFriendShare().Forget();
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
        NaviiChatManager.ExitAllNaviiChatView(UIType.FriendView);

        Exit().Forget();
    }

    private void OnEventFriend()
    {
    }

    private void OnEventUserProfile()
    {
        UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView);

        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventFriendRequestSubTab(int index)
    {
        Model.SetViewModeState(index == 0 ? FriendViewModel.ViewModeState.Request : FriendViewModel.ViewModeState.RequestAccept);
        Model.SetSearch(false);

        ProcessFriendGet().Forget();
    }

    private void OnEventDm(FriendListUnitModel model)
    {

    }

    /// <summary>
    /// 친구 신청
    /// </summary>
    private void OnEventFollow(FriendListUnitModel model)
    {
        BalanceTable balanceTable = TableManager.Instance.GetTable<BalanceTable>();

        if (Model.ViewMode == FriendViewModel.ViewModeState.RequestAccept)
        {
            int maxFriendAcceptCount = (int)balanceTable.GetDataByID((int)BalanceDefine.BALANCE_MAX_ACCEPT_FRIEND).GetINDEX(0);

            if (Model.GetViewModeCount(FriendViewModel.ViewModeState.Friend) == maxFriendAcceptCount)
            {
                MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization("LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_MAX"));
                return;
            }
            RequestFriendAccept(model).Forget();
        }
        else
        {
            int maxFriendRequestCount = (int)balanceTable.GetDataByID((int)BalanceDefine.BALANCE_MAX_REQUEST_FRIEND).GetINDEX(0);
            if (Model.GetViewModeCount(FriendViewModel.ViewModeState.Friend) == maxFriendRequestCount ||
                Model.GetViewModeCount(FriendViewModel.ViewModeState.Request) == maxFriendRequestCount)
            {
                MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization("LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_REQUEST_MAX"));
                return;
            }

            RequestFriendRequest(model).Forget();
        }
    }

    /// <summary>
    /// 우정포인트 받기
    /// </summary>
    private void OnEventReceiveFriendPoint(FriendListUnitModel model)
    {
        if (!model.IsRecvFriendPoint)
            return;

        RequestFriendPointReceive(isRefreshUI: true, new ReceiveFriendPointInDto(model.Friend.UserId)).Forget();
    }

    /// <summary>
    /// 우정포인트 보내기
    /// </summary>
    private void OnEventSendFriendPoint(FriendListUnitModel model)
    {
        if (!model.IsSendFriendPoint)
            return;

        RequestFriendPointSend(isRefreshUI: true, new SendFriendPointInDto(model.Friend.UserId)).Forget();
    }

    /// <summary>
    /// 친구 삭제 선택/해제
    /// </summary>
    private void OnEventDeleteFriendMarking(FriendListUnitModel model)
    {
        model.SetDeleteMarking(!model.IsDeleteMarking);
    }

    /// <summary>
    /// 팔로우 신청 취소
    /// </summary>
    private void OnEventCnacelFollow(FriendListUnitModel model)
    {
        RequestFriendRequestCancel(model).Forget();
    }

    /// <summary>
    /// 즐겨찾기 취소
    /// </summary>
    private void OnEventFavoriteCancel(FriendListUnitModel model)
    {
        RequestFavoriteDelete(model).Forget();
    }

    private void OnEventCharacterSelect(FriendListUnitModel model)
    {
        UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView);

        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetFriendProfile(model.Friend);
        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventCharacterHold(FriendListUnitModel model)
    {
    }

    private async UniTask ProcessFriendGet()
    {
        if (Model.ViewMode == FriendViewModel.ViewModeState.Recommend)
        {
            await RequestOtherUserRecommend();
        }
        else if (Model.ViewMode == FriendViewModel.ViewModeState.Favorite)
        {
            await RequestFavoriteGet();
        }
        else
        {
            await RequestFriendGet();
        }

        View.ShowAsync().Forget();
    }

    private async UniTask<bool> RequestFriendGet()
    {
        FriendGetProcess friendGetProcess = NetworkManager.Web.GetProcess<FriendGetProcess>();

        friendGetProcess.Request.SetGetFriendInDto(new GetFriendInDto(Model.RequestFriendState));

        if (await friendGetProcess.OnNetworkAsyncRequest())
        {
            Model.SetSelectDeleteMenu(false);
            Model.SetFriendListUnitModels(friendGetProcess.GetFriends());
            return true;
        }

        return false;
    }

    private async UniTask<bool> RequestFavoriteGet()
    {
        BaseProcess favoriteGet = NetworkManager.Web.GetProcess(WebProcess.OtherUserFavoriteGet);

        if (await favoriteGet.OnNetworkAsyncRequest())
        {
            favoriteGet.OnNetworkResponse(Model);

            return true;
        }

        return false;
    }

    private async UniTask<bool> RequestFavoriteDelete(FriendListUnitModel model)
    {
        BaseProcess favoriteDelete = NetworkManager.Web.GetProcess(WebProcess.OtherUserFavoriteDelete);

        favoriteDelete.SetPacket(new DeleteUserFavoriteInDto(model.Friend.UserId));

        if (await favoriteDelete.OnNetworkAsyncRequest())
        {
            favoriteDelete.OnNetworkResponse(Model);

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_NAVIICHAT_REMOVE_USER);
            return true;
        }

        return false;
    }

    private async UniTask<bool> RequestOtherUserRecommend()
    {
        BaseProcess otherUserRecommendProcess = NetworkManager.Web.GetProcess(WebProcess.OtherUserRecommendations);

        if (await otherUserRecommendProcess.OnNetworkAsyncRequest())
        {
            otherUserRecommendProcess.OnNetworkResponse(Model);

            return true;
        }

        Model.SetFriendListUnitModels(null);

        return false;
    }

    private async UniTask RequestFriendDelete()
    {
        BaseProcess friendDeleteProcess = NetworkManager.Web.GetProcess(WebProcess.FriendDelete);

        friendDeleteProcess.SetPacket(Model.GetDeleteFriendInDto());

        if (await friendDeleteProcess.OnNetworkAsyncRequest())
        {
            Model.SetSelectDeleteMenu(false);
            Model.RefreshFriendListUnitModelsByDelete();

            View.ShowFriendScroll(isReset: true);
            View.ShowBottomMenu();

            if (Model.ViewMode == FriendViewModel.ViewModeState.RequestAccept)
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_REJECT);
            else
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_CANCEL);
        }
    }

    private async UniTask RequestFriendRequestCancel(FriendListUnitModel model)
    {
        BaseProcess friendDeleteProcess = NetworkManager.Web.GetProcess(WebProcess.FriendDelete);

        friendDeleteProcess.SetPacket(new DeleteFriendInDto(model.Friend.UserId));

        if (await friendDeleteProcess.OnNetworkAsyncRequest())
        {
            friendDeleteProcess.OnNetworkResponse(Model);

            View.ShowFriendScroll(isReset: false);
            View.ShowBottomMenu();

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_CANCEL);
        }
    }

    private async UniTask RequestFriendGetNickName(string search)
    {
        if (search == string.Empty)
        {
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_NAVIICHAT_FRIEND_POPUP_18));
            return;
        }

        if (search == PlayerManager.Instance.MyPlayer.User.NickName)
        {
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_NAVIICHAT_FRIEND_POPUP_19));
            return;
        }

        BaseProcess friendGetNickNameProcess = NetworkManager.Web.GetProcess(WebProcess.OtherUserNicknameGet);

        friendGetNickNameProcess.SetPacket(new GetSearchNickNameInDto(search));

        if (await friendGetNickNameProcess.OnNetworkAsyncRequest())
        {
            friendGetNickNameProcess.OnNetworkResponse(Model);

            View.ShowSearchFriend().Forget();
        }
    }

    private async UniTask RequestFriendShare()
    {
        bool isCheckDailyPointGet = Model.CheckDailyPointGet();
        bool isCheckSendFriendPoint = Model.CheckSendFriendPoint();

        if (isCheckDailyPointGet)
            await RequestFriendPointReceive(isRefreshUI: false, Model.GetAllReceiveFriendPointInDto());

        if (isCheckSendFriendPoint)
            await RequestFriendPointSend(isRefreshUI: true, Model.GetAllSendFriendPointInDto());

        if (!isCheckDailyPointGet && !isCheckSendFriendPoint)
        {
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization("LOCALIZATION_UI_LABEL_NAVIICHAT_NOT_RECEIVE_SENT_POINT"));
        }
    }

    private async UniTask RequestFriendPointReceive(bool isRefreshUI, ReceiveFriendPointInDto receiveFriendPointInDto)
    {
        if (receiveFriendPointInDto.friendUserIds.Count == 0)
            return;

        FriendPointReceiveProcess friendPointReceiveProcess = NetworkManager.Web.GetProcess<FriendPointReceiveProcess>();

        friendPointReceiveProcess.Request.SetReceiveFriendPointInDto(receiveFriendPointInDto);

        if (await friendPointReceiveProcess.OnNetworkAsyncRequest())
        {
            Model.RefreshFriendListUnitModelsByReceivePoint(friendPointReceiveProcess.Response.data.friends);

            friendPointReceiveProcess.OnNetworkResponse();

            if (isRefreshUI)
            {
                View.ShowFriendScroll(isReset: false);
                View.ShowBottomMenu();
            }

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_NAVIICHAT_RECEIVE_POINT);
        }
    }

    private async UniTask RequestFriendPointSend(bool isRefreshUI, SendFriendPointInDto sendFriendPointInDto)
    {
        if (sendFriendPointInDto.friendUserIds.Count == 0)
            return;

        FriendPointSendProcess friendPointSendProcess = NetworkManager.Web.GetProcess<FriendPointSendProcess>();

        friendPointSendProcess.Request.SetSendFriendPointInDto(sendFriendPointInDto);

        if (await friendPointSendProcess.OnNetworkAsyncRequest())
        {
            Model.RefreshFriendListUnitModelsBySendPoint(sendFriendPointInDto);

            if (isRefreshUI)
            {
                View.ShowFriendScroll(isReset: false);
                View.ShowBottomMenu();
            }

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_NAVIICHAT_SENT_POINT);
        }
    }

    private async UniTask RequestFriendRequest(FriendListUnitModel model)
    {
        BaseProcess friendRequestProcess = NetworkManager.Web.GetProcess(WebProcess.FriendRequest);

        friendRequestProcess.SetPacket(new CreateFriendInDto(model.Friend.UserId));

        if (await friendRequestProcess.OnNetworkAsyncRequest())
        {
            friendRequestProcess.OnNetworkResponse(Model);

            View.ShowRecommendScroll(isReset: false);
            View.ShowSearchFriend().Forget();
            View.ShowBottomMenu();

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_REQUEST_SUCCESS);
        }
    }

    private async UniTask RequestFriendAccept(FriendListUnitModel model)
    {
        BaseProcess friendAcceptProcess = NetworkManager.Web.GetProcess(WebProcess.FriendAccept);

        friendAcceptProcess.SetPacket(new AcceptFriendInDto(model.Friend.UserId));

        if (await friendAcceptProcess.OnNetworkAsyncRequest())
        {
            friendAcceptProcess.OnNetworkResponse(Model);

            View.ShowFriendScroll(false);
            View.ShowBottomMenu();

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEXT_MSG_FRIEND_ACCEPT);
        }
    }
    #endregion Coding rule : Function
}