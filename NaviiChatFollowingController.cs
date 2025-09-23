//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class NaviiChatFollowingController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.NaviiChatFollowingView; } }
    private NaviiChatFollowingView View { get { return base.BaseView as NaviiChatFollowingView; } }
    protected NaviiChatFollowingViewModel Model { get; private set; }
    public NaviiChatFollowingController() { Model = GetModel<NaviiChatFollowingViewModel>(); }
    public NaviiChatFollowingController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventUserProfile(OnEventUserProfile);

        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetEventFavorite(OnEventFavorite);
        Model.SetEventGift(OnEventGift);
        Model.SetEventDM(OnEventDM);

        Model.SortingModel.SetEventSorting(OnEventSorting);
        Model.SortingModel.SetEventSortingOrder(OnEventSortingOrder);
        Model.SortingModel.SetSortingType(typeof(NaviiChatFollowerSortingType));
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, NaviiChatFollowerSortingType.Favorite);
        Model.SortingModel.SetOrderType(SortingOrderType.Descending);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestCharacterDm();
        Model.SetFriendListUnitModels(PlayerManager.Instance.MyPlayer.User.CharacterModel, NaviiChatManager.Instance.GetNewDmCharacterList());
    }

    public override async UniTask Process()
    {
        if (UIManager.Instance.CheckBackUI(UIType.CharacterDetailView))
            SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_NAVIICHAT_BGM);

        await View.ShowAsync();
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (UIManager.Instance.CheckOpenUI(UIType.NaviiChatView, includeDisableUI: true))
        {
            OnEventHome();
            return true;
        }
        else
        {
            NaviiChatManager.ExitAllNaviiChatView(UIType.NaviiChatFollowingView);
            SoundManager.BgmFmod.Stop();
            return await Exit(onEventExtra);
        }
    }

    public override void Refresh()
    {
        Model.SetFriendListUnitModels(PlayerManager.Instance.MyPlayer.User.CharacterModel, NaviiChatManager.Instance.GetNewDmCharacterList());
        View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/NaviiChatFollowingView";
    }

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

    private void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.SortFollowers();

        View.ShowFollowerScroll(isReset: true);
    }

    private void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortFollowers();

        View.ShowFollowerScroll(isReset: true);
    }

    private void OnEventUserProfile()
    {
        UIManager.Instance.RemoveToTarget(UIType.NaviiChatProfileView, exceptUI: UIType.NaviiChatView);

        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetMyProfile(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventCharacterSelect(Character character)
    {
        BaseController naviiChatProfileController = UIManager.Instance.GetController(UIType.NaviiChatProfileView);
        NaviiChatProfileViewModel viewModel = naviiChatProfileController.GetModel<NaviiChatProfileViewModel>();

        viewModel.SetCharacterProfile(character);

        UIManager.Instance.RemoveStackByTarget(UIType.NaviiChatProfileView);
        UIManager.Instance.EnterAsync(naviiChatProfileController).Forget();
    }

    private void OnEventFavorite(Character character)
    {
        RequestFavorite(character).Forget();
    }

    private void OnEventGift(Character character)
    {
        BaseController giftController = UIManager.Instance.GetController(UIType.GiftPopup);
        GiftPopupModel model = giftController.GetModel<GiftPopupModel>();
        model.SetDataCharacterId(character.DataId);
        model.SetGiftTargetType(CharacterLikeAbilityType.Character);

        UIManager.Instance.EnterAsync(giftController).Forget();
    }

    private void OnEventDM(Character character)
    {
        UIManager.Instance.RemoveToTarget(UIType.NaviiChatDmView, exceptUI: UIType.NaviiChatView);
        BaseController controller = UIManager.Instance.GetController(UIType.NaviiChatDmView);
        NaviiChatDmViewModel model = controller.GetModel<NaviiChatDmViewModel>();
        model.SetCharacter(character);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private async UniTask RequestCharacterDm()
    {
        BaseProcess dmGetProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterDmGet);
        if (await dmGetProcess.OnNetworkAsyncRequest())
            dmGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestFavorite(Character character)
    {
        BaseProcess changeFavoriteProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterFavoriteChange);
        changeFavoriteProcess.SetPacket(new ChangeFavoriteInDto(character.Id, !character.IsLikeAbilityFavorite));
        if (await changeFavoriteProcess.OnNetworkAsyncRequest())
        {
            changeFavoriteProcess.OnNetworkResponse();
            Model.SortFollowers();
            await View.ShowAsync();
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.NaviiChatFollowingGift:
                {
                    return View.GetFollowertem(0).GiftButton;
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }

    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.NaviiChatFollowingGift:
                {
                    NaviiChatFollowerUnit unit = View.GetFollowertem(0);
                    unit.OnClickGift();
                    await TutorialManager.WaitUntilEnterUI(UIType.GiftPopup);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}