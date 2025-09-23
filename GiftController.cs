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
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GiftController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GiftPopup; } }
    public override void SetModel() { SetModel(new GiftPopupModel()); }
    private GiftPopup View { get { return base.BaseView as GiftPopup; } }
    protected GiftPopupModel Model { get { return GetModel<GiftPopupModel>(); } }

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
        Model.SetEventSelect(OnEventSelect);
        Model.SetEventHold(OnEventHold);
        Model.SetEventUnSelect(OnEventUnSelect);
        Model.SetEventAutoSelect(OnEventAutoSelect);
        Model.SetEventClear(OnEventClear);
        Model.SetEventGift(OnEventGift);

        Model.SetGiftPopupModel(PlayerManager.Instance.MyPlayer.User);

        Model.ClearSelectGiftItem();
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
        if (Model.IsFullExp)
        {
            Exit().Forget();
            return;
        }
        Model.SetThumbnailItemList(PlayerManager.Instance.MyPlayer.User.ItemModel);
        Model.ClearSelectGiftItem();

        View.RefreshAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "NaviiChat/GiftPopup";
    }

    private ThumbnailSelectType OnEventSelect(BaseThumbnailUnitModel model)
    {
        if (Model.IsFullExp)
            return ThumbnailSelectType.None;

        Model.SetSelectMaterial(model);

        View.RefreshAsync().Forget();

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventHold(BaseThumbnailUnitModel baseThumbnailUnitModel)
    {
        if (UIManager.Instance.CheckOpenCurrentUI(UIType.ItemToolTipPopup))
            return ThumbnailSelectType.None;

        BaseController itemToolTipController = UIManager.Instance.GetController(UIType.ItemToolTipPopup);
        ItemToolTipPopupModel model = itemToolTipController.GetModel<ItemToolTipPopupModel>();
        model.SetGoods(baseThumbnailUnitModel.Goods);
        model.SetThumbnail();

        UIManager.Instance.EnterAsync(itemToolTipController);

        return ThumbnailSelectType.None;
    }

    private void OnEventUnSelect(ThumbnailSelectUnitModel model)
    {
        Model.SetUnSelectMaterial(model.Id);

        View.RefreshAsync().Forget();
    }

    private async void OnEventClear()
    {
        Model.ClearSelectGiftItem();

        await View.RefreshAsync();
    }

    private async void OnEventAutoSelect()
    {
        Model.SetAutoSelectGiftItem();

        await View.RefreshAsync();
    }

    private async UniTask OnEventGift()
    {
        if (!Model.IsExpChanged || !Model.IsSelectGift)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_MSG_NO_SELECT_INCENTIVE);
            return;
        }

        if (Model.TargetCharacter.LikeAbilityLevel >= Model.TargetCharacter.LikeAbilityMaxLevel)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_GIFTCONTROLLER_MAXCREDIT);
            return;
        }

        await RequestGift();
    }

    private async UniTask RequestGift()
    {
        BaseProcess characterLikeAbilityIncreaseProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterLikeAbilityIncrease);

        int prevLevel = Model.CurrentLevel;
        characterLikeAbilityIncreaseProcess.SetPacket(Model.GetGiftDto());

        if (await characterLikeAbilityIncreaseProcess.OnNetworkAsyncRequest())
        {
            characterLikeAbilityIncreaseProcess.OnNetworkResponse();

            int resultLevel = Model.CurrentLevel;
            if (prevLevel < resultLevel)
            {
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CREDITLEVELUPPOPUP_LEVELUP,
                    ToastMessagePopupModel.ToastMessageType.Middle, StringDefine.PATH_PREFAB_CHARACTER_LIKEABILITY_LEVELUP_EFFECT);

                SoundManager.SfxFmod.Play(StringDefine.FMOD_EVENT_UI_MENU_SFX, StringDefine.FMOD_DEFAULT_PARAMETER, 26);
            }
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.GiftAuto:
                {
                    OnEventAutoSelect();
                    break;
                }

            case TutorialExplain.GiftAward:
                {
                    await OnEventGift();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}