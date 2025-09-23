//=========================================================================================================
#pragma warning disable CS1998
using System;
//using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ChainLinkWishListController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ChainLinkWishListPopup; } }
    public override void SetModel() { SetModel(new ChainLinkWishListPopupModel()); }
    private ChainLinkWishListPopup View { get { return base.BaseView as ChainLinkWishListPopup; } }
    private ChainLinkWishListPopupModel Model { get { return GetModel<ChainLinkWishListPopupModel>(); } }
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

        Model.SetOnChangeWishList(OnChangeWishList);
        Model.SetOnClickCancelPickDim(OnClickCancelPickDim);
        Model.SetOnClickCharacterThumbListUnit(OnClickCharacterThumbListUnit);
        Model.SetOnPickThumbWishList(OnPickThumbWishList);
        Model.SetOnUnSelectThumbWishList(OnUnSelectThumbWishList);
        Model.SetOnClickFilter(OnClickFilter);
        Model.SetOnClickReset(OnClickReset);
        Model.SetOnClickSave(OnClickSave);
        Model.SetOnClickCancel(OnClickCancel);
        Model.SetOnClickApply(OnClickApply);
        Model.SetSortingModels(OnEventSorting, OnEventSortingOrder);

        Model.InitializeThumbWishListUnitModels(GetWishListInfo());
        Model.SetCurrentState(ChainLinkWishListPopupModel.State.Show);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetPrevCharacterDataIds();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    private IReadOnlyDictionary<LicenseType, int[]> GetWishListInfo()
    {
        ChainLinkInfoModel chainLinkInfoModel = ShopManager.Instance.GetScheduledShopInfoModel<ChainLinkInfoModel>();
        IReadOnlyDictionary<LicenseType, int[]> wishListInfo = null;

        if (chainLinkInfoModel != null)
            wishListInfo = chainLinkInfoModel.GetWishListInfo(Model.DataGachaShopId);

        return wishListInfo;
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "ChainLink/ChainLinkWishListPopup";
    }

    private async UniTask RequestWishListCharacterManage(Action onEventFinish)
    {
        BaseProcess wishListCharacterManageProcess = NetworkManager.Web.GetProcess<WishListCharacterManageProcess>();

        ManageWishListInDto wishListDto = new ManageWishListInDto
        {
            dataGachaShopId = Model.DataGachaShopId,
            contents = CreateNewWishListInfoDic()
        };

        var request = wishListCharacterManageProcess.GetRequest<WishListCharacterManageRequest>();

        if (request == null)
        {
            onEventFinish?.Invoke();
            return;
        }

        request.SetPacket(wishListDto);

        if (await wishListCharacterManageProcess.OnNetworkAsyncRequest())
        {
            wishListCharacterManageProcess.OnNetworkResponse();

            Model.InitializeThumbWishListUnitModels(GetWishListInfo());
            Model.SetPrevCharacterDataIds();

            MessageBoxManager.ShowToastMessage(Model.SaveMessage);

            await View.ShowAsync();
        }

        onEventFinish?.Invoke();
    }

    private Dictionary<int, int[]> CreateNewWishListInfoDic()
    {
        Dictionary<int, int[]> newWishListInfo = new Dictionary<int, int[]>();

        for (LicenseType licenseType = LicenseType.Black; licenseType < LicenseType.Max; licenseType++)
        {
            newWishListInfo.Add((int)licenseType, new int[5]);

            for (int i = 0; i < newWishListInfo[(int)licenseType].Length; i++)
            {
                if (!Model.ThumbWishListDic.ContainsKey(licenseType))
                    continue;

                if (i >= Model.ThumbWishListDic[licenseType].Length)
                    continue;

                if (Model.ThumbWishListDic[licenseType][i].ThumbnailCharacterUnitModel == null)
                    continue;

                newWishListInfo[(int)licenseType][i] = Model.ThumbWishListDic[licenseType][i].ThumbnailCharacterUnitModel.DataId;
            }
        }

        return newWishListInfo;
    }

    private void OnChangeWishList(ThumbnailWishListUnitModel model)
    {
        Model.SetLicenseType(model.LicenseType);
        Model.SetCurrentState(ChainLinkWishListPopupModel.State.Change);
        Model.SetCharacterThumbModels();

        View.ShowAsync().Forget();
    }

    private void OnPickThumbWishList(ThumbnailWishListUnitModel model)
    {
        Model.RemoveSelectedCharacterDataId(model.ThumbnailCharacterUnitModel.DataId);
        model.SetThumbnailCharacterUnitModel(null);

        if (Model.SelectThumbWishListUnitModel(Model.CurrentLicenseType, Model.GetCurrentWishListIndex(), Model.PickThumbCharModel.DataId))
        {
            Model.UpdateSelectThumbnailCharacterUnitModels();
            Model.SetCurrentState(ChainLinkWishListPopupModel.State.Change);
            Model.SetPick(null);

            View.CanclePickDim();

            View.ShowAsync().Forget();
        }
    }

    private void OnUnSelectThumbWishList(ThumbnailWishListUnitModel model)
    {
        int dataId = model.ThumbnailCharacterUnitModel.DataId;
        model.SetThumbnailCharacterUnitModel(null);

        UpdateUnSelect(dataId);
    }

    private void OnUnSelectThumbWishList(int dataId)
    {
        var thumbWishListModel = Model.ThumbWishListDic[Model.CurrentLicenseType].Find(x =>
                                                                                    x.ThumbnailCharacterUnitModel != null &&
                                                                                    x.ThumbnailCharacterUnitModel.DataId == dataId);

        if (thumbWishListModel != null)
            thumbWishListModel.SetThumbnailCharacterUnitModel(null);

        UpdateUnSelect(dataId);
    }

    private void UpdateUnSelect(int dataId)
    {
        Model.RemoveSelectedCharacterDataId(dataId);
        Model.UpdateSelectThumbnailCharacterUnitModels();

        Model.SortThumbWishListDic();

        View.ShowAsync().Forget();
    }

    protected void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.UpdateSelectThumbnailCharacterUnitModels();

        if (Model.CurrentState == ChainLinkWishListPopupModel.State.Pick)
        {
            View.UpdateChangeCharacterThumbScroll().Forget();
        }
        else
        {
            View.ShowAsync().Forget();
        }
    }

    protected void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.UpdateSelectThumbnailCharacterUnitModels();

        if (Model.CurrentState == ChainLinkWishListPopupModel.State.Pick)
        {
            View.UpdateChangeCharacterThumbScroll().Forget();
        }
        else
        {
            View.ShowAsync().Forget();
        }
    }

    private ThumbnailSelectType OnSelect(BaseThumbnailUnitModel model)
    {
        if (Model.GetCurrentWishListIndex() < 0)
            return ThumbnailSelectType.None;

        if (!Model.SelectThumbWishListUnitModel(Model.CurrentLicenseType, Model.GetCurrentWishListIndex(), model.DataId))
            return ThumbnailSelectType.None;

        Model.UpdateSelectThumbnailCharacterUnitModel(model as ThumbnailCharacterUnitModel);

        View.ShowAsync().Forget();

        return ThumbnailSelectType.Select;
    }

    private ThumbnailSelectType OnPickCharacterThumbListUnit(BaseThumbnailUnitModel model)
    {
        for (int i = 0; i < Model.ThumbWishListDic[Model.CurrentLicenseType].Count(); i++)
        {
            var thumbWishListUnitModel = Model.ThumbWishListDic[Model.CurrentLicenseType][i];

            if (thumbWishListUnitModel == null)
                continue;

            var thumCharModel = thumbWishListUnitModel.ThumbnailCharacterUnitModel;
            thumCharModel.SelectModel.SetSelectState(ThumbnailSelectState.Default);
            thumCharModel.SelectModel.SetDimType(ThumbnailDimType.Select);
        }

        model.SelectModel.SetSelectState(ThumbnailSelectState.Default);
        model.SelectModel.SetDimType(ThumbnailDimType.Select);

        Model.SetPick(model as ThumbnailCharacterUnitModel);
        Model.SetCurrentState(ChainLinkWishListPopupModel.State.Pick);

        View.ShowAsync().Forget();

        return ThumbnailSelectType.Select;
    }

    private void OnClickCancelPickDim()
    {
        for (int i = 0; i < Model.ThumbWishListDic[Model.CurrentLicenseType].Count(); i++)
        {
            var thumbWishListUnitModel = Model.ThumbWishListDic[Model.CurrentLicenseType][i];

            if (thumbWishListUnitModel == null)
                continue;

            var thumCharModel = thumbWishListUnitModel.ThumbnailCharacterUnitModel;

            if (thumCharModel.SelectModel != null)
                thumCharModel.SelectModel.SetSelectState(ThumbnailSelectState.None);
        }

        Model.UpdateSelectThumbnailCharacterUnitModels();
        Model.SetPick(null);
        Model.SetCurrentState(ChainLinkWishListPopupModel.State.Change);

        View.ShowAsync().Forget();
    }

    private ThumbnailSelectType OnClickCharacterThumbListUnit(BaseThumbnailUnitModel model)
    {
        if (Model.IsSelectCharacterDataIds(model.DataId))
        {
            OnUnSelectThumbWishList(model.DataId);

            return ThumbnailSelectType.None;
        }
        else
        {
            if (Model.CheckWishListFull())
            {
                return OnPickCharacterThumbListUnit(model);
            }
            else
            {
                return OnSelect(model);
            }
        }
    }

    private void OnClickApply()
    {
        OnCancel(false);
    }

    private void OnClickFilter()
    {
        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnChangeFilter);
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }

    private void OnChangeFilter()
    {
        if (!Model.FilterModel.CheckChangeFilter())
            return;

        Model.FilterModel.SaveFilter();
        Model.SetCharacterThumbModels();

        if (Model.CurrentState == ChainLinkWishListPopupModel.State.Pick)
        {
            View.UpdateChangeCharacterThumbScroll().Forget();
        }
        else
        {
            View.ShowAsync().Forget();
        }
    }

    private void OnClickReset()
    {
        Model.SetResetThumbWishListModel();

        if (Model.CurrentState == ChainLinkWishListPopupModel.State.Pick)
        {
            OnClickCancelPickDim();
        }
        else
        {
            View.ShowAsync().Forget();
        }
    }

    private void OnClickCancel()
    {
        OnCancel(false);
    }

    private void OnCancel(bool isRestore)
    {
        switch (Model.CurrentState)
        {
            case ChainLinkWishListPopupModel.State.Show:
                Exit().Forget();
                break;

            case ChainLinkWishListPopupModel.State.Change:
                Model.SetLicenseType(LicenseType.None);
                Model.SetCurrentState(ChainLinkWishListPopupModel.State.Show);
                Model.SetPick(null);

                if (isRestore)
                    Model.InitializeThumbWishListUnitModels(GetWishListInfo());

                View.ShowAsync().Forget();
                break;
        }
    }

    private void OnClickSave()
    {
        if (Model.CheckChanged())
        {
            MessageBoxManager.ShowYesNoBox(Model.NotSaveMessage, onEventConfirm: () =>
            {
                RequestWishListCharacterManage(() => OnCancel(false)).Forget();
            },
            onEventClose: () => OnCancel(true)).Forget();
        }
        else
        {
            OnCancel(true);
        }
    }
    #endregion Coding rule : Function
}