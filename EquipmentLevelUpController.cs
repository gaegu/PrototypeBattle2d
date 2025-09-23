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
using IronJade.UI.Core;
using System;
using System.Collections.Generic;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class EquipmentLevelUpController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.EquipmentLevelUpPopup; } }
    private EquipmentLevelUpPopup View { get { return base.BaseView as EquipmentLevelUpPopup; } }
    protected EquipmentLevelUpPopupModel Model { get; private set; }
    public EquipmentLevelUpController() { Model = GetModel<EquipmentLevelUpPopupModel>(); }
    public EquipmentLevelUpController(BaseModel baseModel) : base(baseModel) { }
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

        Model.SetEventSelect(OnEventSelect);
        Model.SetEventHold(OnEventHold);
        Model.SetEventSelectCancel(OnEventSelectCancel);
        Model.SetEventReset(OnEventReset);
        Model.SetEventAutoSelect(OnEventAutoSelect);
        Model.SetEventLevelUp(OnEventLevelUp);

        Model.SortingModel.SetEventSorting(OnEventSorting);
        Model.SortingModel.SetEventSortingOrder(OnEventSortingOrder);
        Model.SortingModel.SetSortingType(typeof(EquipmentLevelUpMaterialSortingType));
        Model.SortingModel.SetOrderType(SortingOrderType.Ascending);
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Equipment, EquipmentLevelUpMaterialSortingType.Tier);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetEquipmentLevelUpPopupModel();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(Func<UISubState, UniTask> onEventExtra = null)
    {
        var backController = UIManager.Instance.GetController(UIManager.Instance.GetBackUIType());

        if (backController != null)
            backController.Refresh();

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Equipment/EquipmentLevelUpPopup";
    }

    private ThumbnailSelectType OnEventSelect(BaseThumbnailUnitModel model)
    {
        Model.SetSelectMaterial(model);
        View.RefreshMaterialSelect(false);

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventHold(BaseThumbnailUnitModel model)
    {
        return ThumbnailSelectType.None;
    }

    private void OnEventSelectCancel(ThumbnailSelectUnitModel model)
    {
        Model.SetUnSelectItem(model);

        View.RefreshMaterialSelect(false);
    }

    private void OnEventReset()
    {
        Model.SetReset();
        View.RefreshMaterialSelect(true);
    }

    private void OnEventAutoSelect()
    {
        if (Model.CurrentLevelExpectedExp == Model.CurrentLevelMaxExp)
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_EQUIPMENT_MAX_LEVEL));
            return;
        }

        Model.SetAutoSelect();
        View.RefreshMaterialSelect(true);
    }

    private void OnEventLevelUp()
    {
        if (!Model.CheckMaterialSelected())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TYPESTATUPGRADE_NOTICE_NOT_ENOUGH_GOODS);
            return;
        }

        if (!PlayerManager.Instance.CheckEnough(Model.CurrencyUnitModel.CurrencyType, Model.CurrencyUnitModel.Value))
            return;

        ProcessEquipmentLevelUp().Forget();
    }

    public void OnEventClose()
    {
        UIManager.Instance.BackToTarget(UIType.EquipmentInfoPopup).Forget();
    }

    private void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Equipment, sortingType);
        Model.SortMaterial();

        View.ShowMaterialScroll(isReset: true);
    }

    private void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortMaterial();

        View.ShowMaterialScroll(isReset: true);
    }

    private async UniTask ProcessEquipmentLevelUp()
    {
        var dtoInfo = Model.GetLevelUpEquipmentInDto();

        if (dtoInfo.isContainHighTierEquipment)
        {
            await ShowEquipmentLevelUpWarningPopup(dtoInfo.dto);
        }
        else
        {
            await RequestEquipmentLevelUp(dtoInfo.dto);
        }
    }

    /// <summary>
    /// 재료에 TIER9 이상 장비를 추려서 경고 팝업을 띄운다.
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    private async UniTask ShowEquipmentLevelUpWarningPopup(LevelUpEquipmentInDto dto)
    {
        BaseController equipmentLevelUpWarningController = UIManager.Instance.GetController(UIType.EquipmentLevelUpWarningPopup);
        var equipmentLevelUpPopupModel = equipmentLevelUpWarningController.GetModel<EquipmentLevelUpWarningPopupModel>();

        List<ThumbnailEquipmentUnitModel> thumbnailEquipmentUnitModels = new List<ThumbnailEquipmentUnitModel>();
        ThumbnailGeneratorModel thumbnailGeneratorModel = new ThumbnailGeneratorModel(Model.User);

        foreach (int equipmentId in dto.goods.equipmentIds)
        {
            ThumbnailEquipmentUnitModel thumbnailEquipmentUnitModel = thumbnailGeneratorModel.GetEquipmentUnitModelById(equipmentId);

            if (thumbnailEquipmentUnitModel == null)
                continue;

            if (thumbnailEquipmentUnitModel.Equipment.Tier >= (int)EquipmentTier.Tier9)
                thumbnailEquipmentUnitModels.Add(thumbnailEquipmentUnitModel);
        }

        equipmentLevelUpPopupModel.SetHighTerEquipmentList(thumbnailEquipmentUnitModels);
        equipmentLevelUpPopupModel.SetOnEventConfirm(() => RequestEquipmentLevelUp(dto));

        await UIManager.Instance.EnterAsync(equipmentLevelUpWarningController);
    }

    private async UniTask RequestEquipmentLevelUp(LevelUpEquipmentInDto dto)
    {
        EquipmentLevelUpProcess equipmentLevelUpProcess = NetworkManager.Web.GetProcess<EquipmentLevelUpProcess>();
        int currentEquipmentLevel = Model.TargetEquipment.Level;

        equipmentLevelUpProcess.Request.SetLevelUpEquipmentInDto(dto);

        if (await equipmentLevelUpProcess.OnNetworkAsyncRequest())
        {
            equipmentLevelUpProcess.OnNetworkResponse();

            await PlayerManager.Instance.UpdateUserGoodsModelByGoodsDto(equipmentLevelUpProcess.Response.data.goods);
            Equipment equipment = PlayerManager.Instance.MyPlayer.User.EquipmentModel.GetGoodsById(equipmentLevelUpProcess.Response.data.equipment.id);
            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetTargetEquipment(equipment);
            Model.SetEquipmentLevelUpPopupModel();
            await View.ShowAsync();

            if (equipment.Level > currentEquipmentLevel)
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHARACTERLEVELUPPOPUP_LEVELUP);
        }
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;
        switch (stepType)
        {
            case TutorialExplain.EquipmentLevelUpAuto:
                {
                    OnEventAutoSelect();
                    await UniTask.NextFrame();
                    break;
                }

            case TutorialExplain.EquipmentLevelUpConfirm:
                {
                    OnEventLevelUp();
                    await UniTask.NextFrame();
                    break;
                }

            case TutorialExplain.EquipmentLevelUpClose:
                {
                    await Exit();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}