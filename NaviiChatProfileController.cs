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
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class NaviiChatProfileController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.NaviiChatProfileView; } }
    public override void SetModel() { SetModel(new NaviiChatProfileViewModel()); }
    private NaviiChatProfileView View { get { return base.BaseView as NaviiChatProfileView; } }
    protected NaviiChatProfileViewModel Model { get { return GetModel<NaviiChatProfileViewModel>(); } }

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
        Model.SetEventRequestFriend(OnEventRequestFriend);
        Model.SetEventDeleteFriend(OnEventDeleteFriend);
        Model.SetEventBlockFriend(OnEventFriendBlock);
        Model.SetEventFavorite(OnEventFavorite);

        Model.SetEventGift(OnEventGift);
        Model.SetEventDm(OnEventDm);

        Model.SetEventHome(OnEventHome);
        Model.SetEventFollowing(OnEventFollowing);
        Model.SetEventMyProfile(OnEventMyProfile);

        Model.SetEventDeletePosting(OnEventDeletePosting);
    }

    public override void BackEnter()
    {
        if (Model.Friend.IsMyUser)
        {
            Model.SetFriend(NaviiChatManager.Instance.GetMyUserFriend());
        }

        base.BackEnter();

        View.ShowAsync().Forget();
    }

    public override async UniTask LoadingProcess()
    {
        TableManager.Instance.LoadTable<CharacterPostingTable>();

        switch (Model.ProfileType)
        {
            case NaviiChatUserType.MyUser:
                {
                    await RequestPostingMyGet();
                    break;
                }

            case NaviiChatUserType.Character:
                {
                    Model.SetLikeAbilityProgress();
                    await RequestPostingMyCharacterGet();
                    break;
                }

            case NaviiChatUserType.Friend:
                {
                    // 내가 상대방을 차단했는지 확인
                    if (!NaviiChatManager.Instance.IsAlreadyBlockUserGet)
                        await RequestBlockUserGet();
                    else
                        Model.SetBlockUser(NaviiChatManager.Instance.CheckBlockUser(Model.Friend.UserId));

                    // 내가 즐겨찾기한 유저인지 확인
                    if (!NaviiChatManager.Instance.IsAlreadyFavoriteUserGet)
                        await RequestFavoriteGet();

                    // 이 유저가 나를 차단 중인지 확인
                    await RequestBlockedUserGet();

                    await RequestPostingOtherUserGet();
                    break;
                }
        }
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        RefreshAsync().Forget();
    }

    private async UniTask RefreshAsync()
    {
        switch (Model.ProfileType)
        {
            case NaviiChatUserType.Character:
                {
                    await RequestPostingMyCharacterGet();
                    Model.SetLikeAbilityProgress();
                    break;
                }
        }

        await View.RefreshAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/NaviiChatProfileView";
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (UIManager.Instance.CheckBackUI(UIType.NaviiChatFollowingView))
        {
            OnEventFollowing();
            return true;
        }
        else if (UIManager.Instance.CheckOpenUI(UIType.NaviiChatView, includeDisableUI: true))
        {
            OnEventHome();
            return true;
        }
        else
        {
            NaviiChatManager.ExitAllNaviiChatView(UIType.NaviiChatProfileView);
            return await Exit(onEventExtra);
        }
    }

    #region OnEvent

    #region 친구
    private void OnEventRequestFriend()
    {
        if (Model.ProfileType != NaviiChatUserType.Friend)
            return;

        RequestFriendRequest().Forget();
    }

    private void OnEventDeleteFriend()
    {
        if (Model.ProfileType != NaviiChatUserType.Friend)
            return;

        RequestFriendDelete().Forget();
    }

    private void OnEventFavorite(bool value)
    {
        RequestFavorite(value).Forget();
    }

    private void OnEventFriendBlock(bool value)
    {
        RequestBlockFriend(value).Forget();
    }
    #endregion 친구

    #region 캐릭터
    private void OnEventGift()
    {
        if (Model.ProfileType != NaviiChatUserType.Character)
            return;

        if (Model.Character.LikeAbilityLevel >= Model.Character.LikeAbilityMaxLevel)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_PROFILECONTROLLER_MAXLEVEL);
            return;
        }

        BaseController baseController = UIManager.Instance.GetController(UIType.GiftPopup);
        GiftPopupModel giftPopupModel = baseController.GetModel<GiftPopupModel>();
        giftPopupModel.SetDataCharacterId(Model.Character.DataId);
        giftPopupModel.SetGiftTargetType(CharacterLikeAbilityType.Character);

        UIManager.Instance.EnterAsync(baseController).Forget();
    }

    private void OnEventDm()
    {
        if (Model.ProfileType == NaviiChatUserType.Character)
        {
            UIManager.Instance.RemoveToTarget(UIType.NaviiChatDmView, exceptUI: UIType.NaviiChatView);
            BaseController naviiChatDmController = UIManager.Instance.GetController(UIType.NaviiChatDmView);
            NaviiChatDmViewModel naviiChatDmModel = naviiChatDmController.GetModel<NaviiChatDmViewModel>();
            naviiChatDmModel.SetCharacter(Model.Character);

            UIManager.Instance.EnterAsync(naviiChatDmController).Forget();
        }
        else if (Model.ProfileType == NaviiChatUserType.Friend)
        {
            if (Model.Friend.FriendState == FriendState.Blocked)
                return;

            //유저 DM은 NaviiChatDmView가 아닌 다른 UI로 수정 예정
        }
    }
    #endregion 캐릭터

    #region 하단 메뉴
    private void OnEventHome()
    {
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
        if (Model.ProfileType == NaviiChatUserType.MyUser)
            return;

        TaskReturnMyProfile().Forget();
    }
    #endregion 하단 메뉴

    private void OnEventDeletePosting(int id)
    {
        // 게시물 지운 후 갱신
        Model.RemovePosting(id);
        View.ShowPosting(isReset: false);
    }
    #endregion OnEvent

    #region Network Request

    #region UI 입장시 호출
    private async UniTask RequestBlockedUserGet()
    {
        BlockUserConfirmProcess blockUserConfirmProcess = NetworkManager.Web.GetProcess<BlockUserConfirmProcess>();
        blockUserConfirmProcess.Request.SetConfirmBlockUserInDto(new ConfirmBlockUserInDto(Model.Friend.UserId));

        if (await blockUserConfirmProcess.OnNetworkAsyncRequest())
            Model.SetBlockedUser(blockUserConfirmProcess.CheckBlocked());
    }

    private async UniTask RequestBlockUserGet()
    {
        BlockUserGetProcess blockUserGetProcess = NetworkManager.Web.GetProcess<BlockUserGetProcess>();

        if (await blockUserGetProcess.OnNetworkAsyncRequest())
        {
            blockUserGetProcess.OnNetworkResponse();
            Model.SetBlockUser(NaviiChatManager.Instance.CheckBlockUser(Model.Friend.UserId));
        }
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

    private async UniTask RequestPostingMyGet()
    {
        BaseProcess postingMyGetProcess = NetworkManager.Web.GetProcess(WebProcess.PostingMyGet);
        postingMyGetProcess.SetPacket(new GetPostingInDto(100, string.Empty));

        if (await postingMyGetProcess.OnNetworkAsyncRequest())
        {
            postingMyGetProcess.OnNetworkResponse(Model);
        }
    }

    private async UniTask RequestPostingMyCharacterGet()
    {
        BaseProcess postingMyCharacterGetProcess = NetworkManager.Web.GetProcess(WebProcess.PostingMyCharacterGet);
        postingMyCharacterGetProcess.SetPacket(new GetCharacterPostingInDto(Model.Character.DataId));

        if (await postingMyCharacterGetProcess.OnNetworkAsyncRequest())
        {
            postingMyCharacterGetProcess.OnNetworkResponse(Model);
        }
    }

    private async UniTask RequestPostingOtherUserGet()
    {
        BaseProcess postingOtherUserGetProcess = NetworkManager.Web.GetProcess(WebProcess.PostingOtherUserGet);
        postingOtherUserGetProcess.SetPacket(new GetOtherUserPostingInDto(Model.Friend.UserId, 0, string.Empty));

        if (await postingOtherUserGetProcess.OnNetworkAsyncRequest())
        {
            postingOtherUserGetProcess.OnNetworkResponse(Model);
        }
    }
    #endregion UI 입장시 호출

    #region 버튼 이벤트로 호출

    private async UniTask RequestFriendRequest()
    {
        BaseProcess friendRequestProcess = NetworkManager.Web.GetProcess(WebProcess.FriendRequest);
        friendRequestProcess.SetPacket(new CreateFriendInDto(Model.Friend.UserId));

        if (await friendRequestProcess.OnNetworkAsyncRequest())
        {
            friendRequestProcess.OnNetworkResponse(Model);
            View.ShowButton();
        }
    }

    private async UniTask RequestFriendDelete()
    {
        BaseProcess friendDeleteProcess = NetworkManager.Web.GetProcess(WebProcess.FriendDelete);
        friendDeleteProcess.SetPacket(new DeleteFriendInDto(Model.Friend.UserId));

        if (await friendDeleteProcess.OnNetworkAsyncRequest())
        {
            friendDeleteProcess.OnNetworkResponse(Model);
            View.ShowButton();
        }
    }

    private async UniTask RequestFavorite(bool value)
    {
        if (value)
        {
            BaseProcess favoriteCreateProcess = NetworkManager.Web.GetProcess(WebProcess.OtherUserFavoriteCreate);
            favoriteCreateProcess.SetPacket(new CreateUserFavoriteInDto(Model.Friend.UserId));

            if (await favoriteCreateProcess.OnNetworkAsyncRequest())
            {
                favoriteCreateProcess.OnNetworkResponse(Model);
                View.ShowButton();
            }
        }
        else
        {
            BaseProcess favoriteDeleteProcess = NetworkManager.Web.GetProcess(WebProcess.OtherUserFavoriteDelete);
            favoriteDeleteProcess.SetPacket(new DeleteUserFavoriteInDto(Model.Friend.UserId));

            if (await favoriteDeleteProcess.OnNetworkAsyncRequest())
            {
                favoriteDeleteProcess.OnNetworkResponse(Model);
                View.ShowButton();
            }
        }
    }

    private async UniTask RequestBlockFriend(bool value)
    {
        if (value)
        {
            BlockUserCreateProcess blockUserCreateProcess = NetworkManager.Web.GetProcess<BlockUserCreateProcess>();
            blockUserCreateProcess.Request.SetCreateBlockUserInDto(new CreateBlockUserInDto(Model.Friend.UserId));

            if (await blockUserCreateProcess.OnNetworkAsyncRequest())
                blockUserCreateProcess.OnNetworkResponse();
        }
        else
        {
            BaseProcess blockUserDeleteProcess = NetworkManager.Web.GetProcess(WebProcess.BlockUserDelete);
            blockUserDeleteProcess.SetPacket(new DeleteBlockUserInDto(Model.Friend.UserId));

            if (await blockUserDeleteProcess.OnNetworkAsyncRequest())
                blockUserDeleteProcess.OnNetworkResponse();
        }

        Model.SetBlockUser(value);
        View.ShowButton();
    }

    /// <summary> CBT 기준 빠졌지만 썸네일 클릭시 UserInfoPopup 띄우는 기능이 있어서 제거하지 않고 보류</summary>
    private async UniTask RequestOtherUserGet()
    {
        BaseProcess otherUserGetProcess = NetworkManager.Web.GetProcess(WebProcess.OtherUserExtraGet);
        otherUserGetProcess.SetPacket(new GetOtherUserInDto(Model.Friend.UserId));

        if (await otherUserGetProcess.OnNetworkAsyncRequest())
        {
            otherUserGetProcess.OnNetworkResponse(Model);
        }
    }
    #endregion 버튼 이벤트로 호출

    #endregion Network Request

    private async UniTask TaskReturnMyProfile()
    {
        Model.SetMyProfile(PlayerManager.Instance.MyPlayer.User);
        await RequestPostingMyGet();

        View.ShowAsync().Forget();
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.NaviiChatProfileFollowing:
                {
                    OnEventFollowing();
                    await TutorialManager.WaitUntilEnterUI(UIType.NaviiChatFollowingView);
                    break;
                }

            case TutorialExplain.NaviiChatProfileGift:
                {
                    OnEventGift();
                    await TutorialManager.WaitUntilEnterUI(UIType.GiftPopup);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}