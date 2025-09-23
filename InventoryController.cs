//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using System.Collections.Generic;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class InventoryController : BaseController
{
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.InventoryView; } }
    private InventoryView View { get { return base.BaseView as InventoryView; } }
    protected InventoryViewModel Model { get; private set; }
    public InventoryController() { Model = GetModel<InventoryViewModel>(); }
    public InventoryController(BaseModel baseModel) : base(baseModel) { }
    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventChangeTab((goodsType) =>
        {
            OnEventChangeTab(goodsType).Forget();
        });
        Model.SetEventChangeItemTab((itemType) =>
        {
            OnEventChangeItemTab(itemType).Forget();
        });
        Model.SetEventChangeSubTab((value) =>
        {
            OnEventChangeSubTab(value).Forget();
        });
        Model.SetEventSelect(OnEventSelect);
        Model.SetEventHold(OnEventHold);
        Model.SetEventClose(OnEventClose);

        Model.InitializeFilterModel();
        Model.InitializeSortingModel((x) => UpdateScrollBySorting().Forget());
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
        Refresh();
    }

    public override void Refresh()
    {
        // 장비도 레벨업 등으로 소모될 수 있어서 Refresh되게 추가함
        if (Model.CurrentItemType == ItemType.Consume || Model.CurrentGoodsType == GoodsType.Equipment || Model.CurrentGoodsType == GoodsType.Reeltape)
            UpdateItemList(false);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Inventory/InventoryView";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        Model.SetSubCategory(GoodsType.None);
        Model.SetDataFromUserGoods(null);

        return await base.Exit(onEventExtra);
    }

    private ThumbnailSelectType OnEventSelect(BaseThumbnailUnitModel model)
    {
        if (model.Goods == null)
        {
            IronJade.Debug.LogError("Goods 정보가 없습니다.");
            return ThumbnailSelectType.None;
        }

        switch (model.Goods.GoodsType)
        {
            case GoodsType.Item:
                {
                    if ((model.Goods as Item).ItemType == ItemType.Consume)
                    {
                        ShowConsumeItemPopup(model.Goods as Item);
                    }
                    else
                    {
                        ShowItemDetailPopup(model.Goods as Item);
                    }
                    break;
                }

            case GoodsType.Equipment:
                ShowEquipmentEquipPopup(model.Goods as Equipment);
                break;

            case GoodsType.Reeltape:
                ShowReeltapePopup(model.Goods as Reeltape);
                break;
        }

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventHold(BaseThumbnailUnitModel model)
    {
        return ThumbnailSelectType.None;
    }

    private async UniTaskVoid OnEventChangeTab(GoodsType goodsType)
    {
        Model.SetSubCategory(goodsType);

        View.ShowSortingButton().Forget();

        await View.ShowSubCategory();
    }

    private async UniTaskVoid OnEventChangeItemTab(ItemType itemType)
    {
        Model.SetSubCategory(itemType);

        View.ShowSortingButton().Forget();

        await View.ShowSubCategory();
    }

    private async UniTaskVoid OnEventChangeSubTab(int index, bool isUpdate = true)
    {
        Model.ResetFilter();

        if (Model.CurrentGoodsType == GoodsType.Item)
        {
            Model.SetFilter((int)Model.CurrentItemType);
            Model.SetCurrentSubCategoryIndex(index);
        }
        else if (index >= Model.DefaultSubCategoryCount)
        {
            Model.SetFilter(index);
            Model.SetCurrentSubCategoryIndex(index);
        }

        UpdateItemList(isUpdate);
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private void UpdateItemList(bool isReset = true)
    {
        IronJade.Debug.Log($"Inventory Update : {isReset}");

        User user = PlayerManager.Instance.MyPlayer.User;

        Model.SetHasNewConsumeItem(user.ItemModel.CheckUseableCharacterPieceExists());
        Model.SetDataFromUserGoods(user);

        View.UpdateRedDot();
        View.SetRebuildSubCategory();

        UpdateScrollBySorting(isReset).Forget();
    }

    private async UniTask UpdateScrollBySorting(bool isReset = true)
    {
        Model.SortItemThumbnail();
        await UniTask.NextFrame();
        View.ShowItemScroll(isReset).Forget();
    }

    private void ShowItemDetailPopup(Item item)
    {
        if (item == null)
        {
            IronJade.Debug.LogError("아이템 정보가 없습니다.");
            return;
        }

        BaseController itemToolTipController = UIManager.Instance.GetController(UIType.ItemToolTipPopup);
        ItemToolTipPopupModel model = itemToolTipController.GetModel<ItemToolTipPopupModel>();
        model.SetGoods(item);
        model.SetThumbnail();
        UIManager.Instance.EnterAsync(itemToolTipController).Forget();
    }

    private void ShowConsumeItemPopup(Item item)
    {
        if (item == null)
        {
            IronJade.Debug.LogError("아이템 정보가 없습니다.");
            return;
        }

        BaseController consumeItemPopup = UIManager.Instance.GetController(UIType.ConsumeItemPopup);
        ConsumeItemPopupModel model = consumeItemPopup.GetModel<ConsumeItemPopupModel>();

        model.SetItem(item);
        model.SetOnClickShowProbability(OnClickProbabilityInfo);
        //model.SetConsumeItemModel();

        UIManager.Instance.EnterAsync(consumeItemPopup).Forget();
    }

    private void ShowEquipmentEquipPopup(Equipment equipment)
    {
        if (equipment == null)
        {
            IronJade.Debug.LogError("장비 정보가 없습니다.");
            return;
        }

        BaseController equipmentManagementController = UIManager.Instance.GetController(UIType.EquipmentInfoPopup);
        EquipmentInfoPopupModel model = equipmentManagementController.GetModel<EquipmentInfoPopupModel>();

        model.SetShowInfoType(EquipmentInfoPopupModel.InfoType.Default);
        model.SetEquipment(equipment);
        model.SetCharacterId(equipment.WearCharacterId);
        model.SetUser(PlayerManager.Instance.MyPlayer.User);

        UIManager.Instance.EnterAsync(equipmentManagementController).Forget();
    }

    private void ShowReeltapePopup(Reeltape reeltape)
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ReeltapeInfoPopup);
        ReeltapeInfoPopupModel model = controller.GetModel<ReeltapeInfoPopupModel>();

        model.SetReeltape(reeltape);
        model.SetShowEquip(true);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnClickProbabilityInfo(List<ProbabilityInfoGroupUnitModel> probs)
    {
        BaseController baseController = UIManager.Instance.GetController(UIType.ProbabilityInfoPopup);
        ProbabilityInfoPopupModel model = baseController.GetModel<ProbabilityInfoPopupModel>();

        model.Clear();
        model.SetProbabilityInfoGroups(probs);

        UIManager.Instance.EnterAsync(baseController).Forget();
    }
    #endregion Coding rule : Function
}