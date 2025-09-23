//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;          // 데이터 테이블
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ContentsShopController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ContentsShopView; } }
    private ContentsShopView View { get { return base.BaseView as ContentsShopView; } }
    protected ContentsShopViewModel Model { get; private set; }
    public ContentsShopController() { Model = GetModel<ContentsShopViewModel>(); }
    public ContentsShopController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool skipLoading = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        skipLoading = false;
        ObserverManager.AddObserver(CommonObserverID.DailyRefreshData, this);
        ObserverManager.AddObserver(CommonObserverID.TimeRefresh, this);

        Model.SetUser(PlayerManager.Instance.MyPlayer.User);

        Model.SetOnClickRenew(OnEventClickRenew);
        Model.SetOnEventPurchase(OnEventPurchase);
        Model.SetOnClickContentsShopCategory(OnEventClickShopCategory);
    }

    public override async UniTask LoadingProcess()
    {
        if (skipLoading)
            return;

        await RequestContentsShopGet();
    }

    public override async UniTask Process()
    {
        ChangeCategory(Model.CurrentShopCategory);
        SetRenewTime();
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);
        ObserverManager.RemoveObserver(CommonObserverID.TimeRefresh, this);

        return await base.Exit(onEventExtra);
    }

    public override void BackEnter()
    {
        skipLoading = true;
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Shop/ContentsShopView";
    }

    private void ChangeCategory(ContentsShopCategory categoryType)
    {
        Model.SetCurrentShop(categoryType);
        SetShopOpenCondition(categoryType);
    }

    private void SetShopOpenCondition(ContentsShopCategory categoryType)
    {
        ContentsShopTable contentsShopTable = TableManager.Instance.GetTable<ContentsShopTable>();
        ContentsShopTableData contentsShopData = contentsShopTable.Find(x => (ContentsShopCategory)x.GetCATEGORY_TYPE() == categoryType);

        bool isOpen = ContentsOpenManager.Instance.CheckStageDungeonCondition(contentsShopData.GetCONDITION_STAGE_DUNGEON());

        if (isOpen)
        {
            if (categoryType == ContentsShopCategory.Guild)
            {
                bool isJoinGuild = PlayerManager.Instance.MyPlayer.User.GuildModel.IsJoinedGuild;
                string textJoinGuild = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_CONTENTSHOP_OPEN_CONDITION_JOIN_GAO);
                Model.SetOnEventGotoShopOpenContents(() => { ContentsOpenManager.Instance.OpenContents(ContentsType.Guild).Forget(); });
                Model.SetCurrentShopOpenInfo(isJoinGuild, textJoinGuild);
            }
            else
            {
                Model.SetCurrentShopOpenInfo(true, string.Empty);
            }
        }
        else
        {
            (int, int) openStage = ContentsOpenManager.Instance.GetChapterStageNumber(contentsShopData.GetCONDITION_STAGE_DUNGEON());
            string textOpenCondition = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_NOTICE_CONTENTS_OPEN_CONDITION_MAIN_STAGE, openStage.Item1, openStage.Item2);
            Model.SetOnEventGotoShopOpenContents(() => { ContentsOpenManager.Instance.OpenContents(ContentsType.StageDungeon).Forget(); });
            Model.SetCurrentShopOpenInfo(isOpen, textOpenCondition);
        }
    }

    private void SetRenewTime()
    {
        Model.SetRenewRemainTime(UtilModel.String.GetRemainTimeToString(TimeManager.Instance.GetRemainResetTime(CheckResetTimeType.Daily), TimeStringFormType.hh_mm_ss));
        View.ShowRenewTime();
    }

    private async UniTask RefreshContentsShop()
    {
        SetContentsShop();
        View.ShowAsync().Forget();
    }

    private CurrencyUnitModel GetCurrentShopRenewCurrency()
    {
        CostTable costTable = TableManager.Instance.GetTable<CostTable>();
        CostTableData costData = costTable.GetDataByID(Model.GetCurrentCategoryResetCostDataId());

        CurrencyTable currencyTable = TableManager.Instance.GetTable<CurrencyTable>();
        CurrencyTableData currencyData = currencyTable.GetDataByID(costData.GetGOODS_VALUE(0));
        CurrencyType type = (CurrencyType)currencyData.GetCURRENCY_TYPE();

        int renewCost = costData.GetGOODS_COUNT(0);
        CurrencyUnitModel model = CurrencyGeneratorModel.GetCurrencyUnitModel(type, renewCost);
        return model;
    }

    private void OnEventClickShopCategory(ContentsShopCategory categoryType)
    {
        if (Model.CurrentShopCategory == categoryType)
            return;

        ChangeCategory(categoryType);
        View.ShowAsync().Forget();
    }

    private void OnEventClickRenew()
    {
        if (!Model.IsRenewable)
            return;

        CurrencyUnitModel currencyModel = GetCurrentShopRenewCurrency();
        Action eventFinishedConfirm = () =>
        {
            if (PlayerManager.Instance.MyPlayer.User.CurrencyModel.CheckCompareByInt(CompareType.GreaterEqual, currencyModel.CurrencyType, currencyModel.Value))
            {
                RequestContentsShopRefresh().Forget();
            }
            else
            {
                MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_MSG_CONTENTSSHOP_PRODUCT_RENEWAL_LACK_GOODS).Forget();
            }
        };
        MessageBoxManager.ShowCurrencyMessageBox(Model.TextRenewMessage, currencyModel, showMyCurrency: true, eventFinishedConfirm);
    }

    private void OnEventPurchase(int shopId, long productId, int count)
    {
        RequestBuyProduct(shopId, productId, count).Forget();
        View.ShowAsync().Forget();
    }

    private void SetContentsShop()
    {
        Model.SetContentsShop(ShopManager.Instance.ContentsShopInfoModel);
        Model.UpdateCurrencyList();
    }

    private void UpdateContentsShop(int shopId, List<ContentsShopModel> shopInfos)
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.UpdateContentsShopByCategory(shopId, shopInfos);
        Model.UpdateCurrencyList();
    }

    private async UniTask RequestContentsShopGet()
    {
        BaseProcess contentsShopGetProcess = NetworkManager.Web.GetProcess(WebProcess.ContentsShopGet);
        if (await contentsShopGetProcess.OnNetworkAsyncRequest())
        {
            contentsShopGetProcess.OnNetworkResponse();
            SetContentsShop();
        }
    }

    private async UniTask RequestContentsShopRefresh()
    {
        ContentsShopRefreshProcess refreshProcess = NetworkManager.Web.GetProcess<ContentsShopRefreshProcess>();
        RefreshShopDataInDto dto = new RefreshShopDataInDto(Model.GetCurrentCategoryUnit().Id);
        refreshProcess.Request.SetRefreshDto(dto);

        if (await refreshProcess.OnNetworkAsyncRequest())
        {
            refreshProcess.OnNetworkResponse();

            ContentsShopInfoModel infoModel = ShopManager.Instance.ContentsShopInfoModel;
            UpdateContentsShop(Model.GetCurrentCategoryUnit().Id, infoModel.GetShopInfoByCategory(Model.CurrentShopCategory));
            View.ShowAsync().Forget();
        }
        else
        {
            await MessageBoxManager.ShowYesBox(refreshProcess.Response.message);
        }
    }

    private async UniTask RequestBuyProduct(int shopCategory, long productId, int count)
    {
        int contentsShopDataId = ShopManager.Instance.ContentsShopInfoModel.GetContentsShopDataId((ContentsShopCategory)shopCategory);
        ContentsShopBuyProcess buyProcess = NetworkManager.Web.GetProcess<ContentsShopBuyProcess>();
        BuyShopProductInDto buyShopProductInDto = new BuyShopProductInDto(contentsShopDataId, productId, count);
        buyProcess.Request.SetBuyShopProductDto(buyShopProductInDto);

        if (await buyProcess.OnNetworkAsyncRequest())
        {
            buyProcess.OnNetworkResponse();

            ContentsShopInfoModel infoModel = ShopManager.Instance.ContentsShopInfoModel;
            UpdateContentsShop(Model.GetCurrentCategoryUnit().Id, infoModel.GetShopInfoByCategory(Model.CurrentShopCategory));
            View.ShowAsync().Forget();
        }
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case CommonObserverID.TimeRefresh:
                {
                    SetRenewTime();
                }
                break;
            case CommonObserverID.DailyRefreshData:
                {
                    RefreshContentsShop().Forget();
                }
                break;
        }
    }
    #endregion Coding rule : Function
}