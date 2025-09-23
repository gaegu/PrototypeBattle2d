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

public class NaviiChatController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.NaviiChatView; } }
    private NaviiChatView View { get { return base.BaseView as NaviiChatView; } }
    protected NaviiChatViewModel Model { get; private set; }
    public NaviiChatController() { Model = GetModel<NaviiChatViewModel>(); }
    public NaviiChatController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetEventDeletePosting(OnEventDeletePosting);
        Model.SetEventProfile(OnEventProfile);
        Model.SetEventSelect(OnEventSelect);
        Model.SetEventHold(OnEventHold);

        Model.SetEventFriend(OnEventFriend);
        Model.SetEventUserProfile(OnEventUserProfile);

        Model.ClearPostingUnitMode();
    }

    public override async UniTask LoadingProcess()
    {
        TableManager.Instance.LoadTable<CharacterPostingTable>();

        // 피드 정보를 얻고 프로필 정보를 셋팅한다.
        await RequestPostingNotificationsGet();

        // 첫 번째 피드 (전체 피드) 선택
        await SelectNaviiChatProcess(Model.GetThumbnailNaviiChatUnitModelBySlotIndex(0), false);
    }

    public override async UniTask Process()
    {
        SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_NAVIICHAT_BGM);

        await View.ShowAsync();
    }

    public override void Refresh()
    {
        View.RefreshAsync().Forget();
    }

    public override async UniTask<bool> Back(Func<UISubState, UniTask> onEventExtra = null)
    {
        //모든 나비챗 관련 UI를 제거한다.
        UIManager.Instance.RemoveStackByTarget(UIType.NaviiChatFollowingView);
        UIManager.Instance.RemoveStackByTarget(UIType.NaviiChatDmView);
        UIManager.Instance.RemoveStackByTarget(UIType.NaviiChatProfileView);

        return await base.Back(onEventExtra);
    }

    public override async UniTask PlayShowAsync()
    {
        if (NaviiChatManager.Instance.IsContainNaviiChatViewGroup(UIManager.Instance.GetBackUIType()))
        {
            await View.SkipPlayShowAsync();
        }
        else
        {
            await base.PlayShowAsync();
        }
    }

    public override async UniTask PlayBackShowAsync()
    {
        await View.SkipPlayShowAsync();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/NaviiChatView";
    }

    private void OnEventDeletePosting(int id)
    {
        // 게시물 지운 후 갱신
        Model.RemovePosting(id);

        View.ShowPosting(isReset: false).Forget();
    }

    /// <param name="id">userId / characterDataId</param>
    private void OnEventProfile(bool isCharacter, int id)
    {
        if (isCharacter)
        {
            UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView);

            BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
            NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

            viewModel.SetCharacterProfile(PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsByDataId(id));

            UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
        }
        else
        {
            if (PlayerManager.Instance.MyPlayer.User.UserId == id)
            {
                UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView);

                BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
                NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

                viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

                UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
            }
            else
            {
                // Open Friend Profile
            }
        }
    }

    private ThumbnailSelectType OnEventSelect(BaseThumbnailUnitModel model)
    {
        if (!Model.CheckProfile(model))
            return ThumbnailSelectType.None;

        Model.SaveFeed(model);

        SelectNaviiChatProcess(model as ThumbnailNaviiChatUnitModel, true).Forget();

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventHold(BaseThumbnailUnitModel model)
    {
        return ThumbnailSelectType.None;
    }

    private async UniTask SelectNaviiChatProcess(ThumbnailNaviiChatUnitModel model, bool isUiRefresh)
    {
        Model.ClearPostingUnitMode();

        switch (model.UserType)
        {
            case NaviiChatUserType.Friend:
                {
                    if (!await RequestPostingOtherUserGet(model))
                        return;
                    break;
                }

            case NaviiChatUserType.MyUser:
                {
                    if (!await RequestPostingGet())
                        return;
                    break;
                }

            case NaviiChatUserType.Character:
                {
                    if (!await RequestPostingMyCharacterGet(model))
                        return;
                    break;
                }
        }

        Model.SetSelectThumbnailNaviiChatUnitModel(model);
        Model.SetSelect();

        if (isUiRefresh)
        {
            View.ShowPosting(isReset: true).Forget();
            View.ShowCharacterListScroll(isReset: false);
        }
    }

    /// <summary>
    /// 피드 정보 불러오기
    /// </summary>
    private async UniTask RequestPostingNotificationsGet()
    {
        BaseProcess postingNotificationsGetProcess = NetworkManager.Web.GetProcess(WebProcess.PostingNotificationsGet);

        if (await postingNotificationsGetProcess.OnNetworkAsyncRequest())
        {
            postingNotificationsGetProcess.OnNetworkResponse(Model);
        }
    }

    /// <summary>
    /// 전체 포스팅 얻어오기
    /// </summary>
    private async UniTask<bool> RequestPostingGet()
    {
        BaseProcess postingGet = NetworkManager.Web.GetProcess(WebProcess.PostingGet);

        postingGet.SetPacket(new GetPostingInDto(100, string.Empty));

        if (await postingGet.OnNetworkAsyncRequest())
        {
            postingGet.OnNetworkResponse(Model);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 내 캐릭터 포스팅 얻어오기
    /// </summary>
    private async UniTask<bool> RequestPostingMyCharacterGet(ThumbnailNaviiChatUnitModel model)
    {
        BaseProcess postingGet = NetworkManager.Web.GetProcess(WebProcess.PostingMyCharacterGet);

        postingGet.SetPacket(new GetMyCharacterPostingInDto(model.DataId));

        if (await postingGet.OnNetworkAsyncRequest())
        {
            postingGet.OnNetworkResponse(Model);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 상대 유저 포스팅 얻어오기
    /// </summary>
    private async UniTask<bool> RequestPostingOtherUserGet(ThumbnailNaviiChatUnitModel model)
    {
        BaseProcess postingGet = NetworkManager.Web.GetProcess(WebProcess.PostingOtherUserGet);

        postingGet.SetPacket(new GetOtherUserPostingInDto(model.UserId, 50, string.Empty));

        if (await postingGet.OnNetworkAsyncRequest())
        {
            postingGet.OnNetworkResponse(Model);
            return true;
        }

        return false;
    }

    private void OnEventFriend()
    {
        if (UIManager.Instance.CheckBackUI(UIType.NaviiChatFollowingView))
        {
            UIManager.Instance.BackToTarget(UIType.NaviiChatFollowingView).Forget();
            return;
        }

        UIManager.Instance.EnterAsync(UIType.NaviiChatFollowingView).Forget();
    }

    private void OnEventUserProfile()
    {
        UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView);

        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventDm()
    {
        BaseController followerController = UIManager.Instance.GetController(UIType.FollowerView);
        FollowerViewModel model = followerController.GetModel<FollowerViewModel>();
        model.SetCategoryType(FollowerViewModel.CategoryType.Chatting);
        UIManager.Instance.EnterAsync(followerController).Forget();
    }

    private void OnEventLike()  // 이름 바꿔야 함 (스키닝 중이라 나중에)
    {
        BaseController followHistoryController = UIManager.Instance.GetController(UIType.FollowHistoryView);
        UIManager.Instance.EnterAsync(followHistoryController).Forget();
    }

    public override async UniTask PlayHideAsync()
    {
        if (NaviiChatManager.Instance.IsContainNaviiChatViewGroup(UIManager.Instance.GetCurrentUIType()))
            return;

        await base.PlayHideAsync();
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.NaviiChatFriends:
                {
                    OnEventFriend();
                    await TutorialManager.WaitUntilEnterUI(UIType.NaviiChatFollowingView);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}