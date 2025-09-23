//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class UserProfileEditController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.UserProfileEditPopup; } }
    public override void SetModel() { SetModel(new UserProfileEditPopupModel()); }
    private UserProfileEditPopup View { get { return base.BaseView as UserProfileEditPopup; } }
    private UserProfileEditPopupModel Model { get { return GetModel<UserProfileEditPopupModel>(); } }
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
        Model.SetCategoryType(UserProfileEditPopupModel.CategoryType.Character);
        Model.SetEventChangeCategory(OnEventChangeCategory);
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetSortingModels(OnEventSorting, OnEventSortingOrder);
        Model.SetEventFilter(OnEventFilter);
        Model.SetFilterModel(CharacterFilterType.CharacterManager);
        Model.SetLeaderCharacter(Model.User.CharacterModel.GetLeaderCharacter());
        Model.SetThumbnailCharacterUnitModel();
        Model.SortCharacterThumbnail();
        Model.SetSelectThumbnailCharacter(Model.GetThumbnailCharacterUnitModelByLeaderCharacter());

        Model.SetThumbnailLikeAbilityFrameUnitModel();
        Model.SetSelectLikeAbilityFrameModel(Model.GetLikeAbilityFrameModel());
        Model.SetEventLikeAbilityFrameSelect(OnEvnetLikeAbilityFrameSelect);

        Model.SetEventOpenCharacterEffectAlert(OnEventOpenCharacterEffectAlertPopup);
        Model.SetEventConfirm(OnEventConfirm);
    }

    public override async UniTask LoadingProcess()
    {
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/UserProfileEditPopup";
    }

    private void OnEventChangeCategory(int index)
    {
        UserProfileEditPopupModel.CategoryType changeCategory = (UserProfileEditPopupModel.CategoryType)index;

        if (Model.Category == changeCategory)
            return;

        Model.SetCategoryType(changeCategory);

        View.ShowCategory();
    }

    protected void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.SortCharacterThumbnail();

        View.ShowCharacterScroll(isReset: true);
    }

    protected void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortCharacterThumbnail();

        View.ShowCharacterScroll(isReset: true);
    }


    protected void OnEventFilter()
    {
        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnEventChangeFilter);
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }


    protected virtual void OnEventChangeFilter()
    {
        if (!Model.FilterModel.CheckChangeFilter())
            return;

        if (Model.CheckEmptyCharacterList())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERPROFILEEDITPOPUP_FILTER_LIST_EMPTY);
            return;
        }

        Model.FilterModel.SaveFilter();
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetThumbnailCharacterUnitModel();
        Model.SetSelectThumbnailCharacter(Model.GetThumbnailCharacterUnitModelByLeaderCharacter());

        View.ShowAsync().Forget();
    }

    protected virtual ThumbnailSelectType OnEventCharacterSelect(BaseThumbnailUnitModel model)
    {
        Model.SetSelectThumbnailCharacter(model);

        View.UpdateThumbnail().Forget();
        View.ShowCharacterScroll(isReset: false);

        return ThumbnailSelectType.None;
    }

    private void OnEvnetLikeAbilityFrameSelect(ThumbnailLikeAbilityFrameUnitModel model)
    {
        Model.SetSelectLikeAbilityFrameModel(model);

        View.UpdateLikeAbilityFrame();
    }

    private void OnEventOpenCharacterEffectAlertPopup()
    {
        if ((CharacterTier)Model.SelectThumbnailCharacterUnitModel.Tier < CharacterTier.X)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_USERINFO_EFFECT_CANNOT_APPLY);
        }
        else
        {
            MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_USERINFO_NOT_ACHIEVE_EFFECT_CONDITION).Forget();
        }
    }

    private void OnEventConfirm()
    {
        RequestUserLeaderCharacterUpdate().Forget();
    }

    private async UniTask RequestUserLeaderCharacterUpdate()
    {
        var leaderIdSet = Model.GetUserLeaderId();

        await PlayerManager.Instance.RequestChangeLeader(leaderIdSet.characterId, leaderIdSet.likeAbilityDataId, async () =>
        {
            await PlayerManager.Instance.LoadMyPlayerCharacterObject();

            View.OnClickClose();
        });
    }

    #endregion Coding rule : Function
}