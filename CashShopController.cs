//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web; // UniTask 관련 클래스 모음
using IronJade.UI.Core;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CashShopController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CashShopView; } }
    private CashShopView View { get { return base.BaseView as CashShopView; } }
    protected CashShopViewModel Model { get; private set; }
    public CashShopController() { Model = GetModel<CashShopViewModel>(); }
    public CashShopController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetLocalData(LocalDataManager.Instance.GetLocalData<CashShopLocalData>());
        Model.SetOnEventBuyItem(BuyItem);
        Model.SetOnEventCampaignRewardGet(CampaignRewardGet);
        Model.SetOnEventSelectCategory(SelectCategory);
        
        Model.Init();
    }
    
    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Shop/CashShopView";
    }

    private void ShowCategory()
    {        
        Model.CategoryUnitModelSetting(Model.SelectMainCategory);
        View.Show();
    }

    private async UniTask CampaignRewardGet(int missionID, Action<bool> callBack)
    {
        Model.RewardMission(missionID);
        
        if(callBack != null)
            callBack.Invoke(true);
    }
    
    // 아이템 구매패킷(야이템, 구매갯수)
    private async UniTask BuyItem(CashShopProductModel productModel, int amount, Action<bool> callBack = null)
    {
        CashShopBuyProcess cashShopBuyProcess = NetworkManager.Web.GetProcess<CashShopBuyProcess>();
        BuyCashShopInDto buyCashShopInDto = new BuyCashShopInDto((int)productModel.CurrentCashShopProductType, 
            productModel.ID, amount);
        var localData = LocalDataManager.Instance.GetLocalData<CashShopLocalData>();

        if (localData.customProductChoseIndex.TryGetValue(productModel.ID, out var customProductChoseIndex))
        {
            buyCashShopInDto.rewardIndexes = customProductChoseIndex.ToArray();
        }
        
        cashShopBuyProcess.Request.SetBuyCashShopInDto(buyCashShopInDto);
        if (await cashShopBuyProcess.OnNetworkAsyncRequest())
        {
            cashShopBuyProcess.OnNetworkResponse();

            var data = cashShopBuyProcess.Response.data;
            PlayerManager.Instance.UpdateUserGoodsModelByGoodsDto(data.goods, isRewardPopup: true, isSum: false).Forget();
            Model.BuyItem(productModel);
        
            if(callBack != null)
                callBack.Invoke(true);
        
            View.ShowAsync().Forget();
        }
    }

    public void SelectCategory(int category)
    {
        if (Model.SelectMainCategory == (CashShopMainCategory)category)
            return;
        
        Model.SetSelectCategory((CashShopMainCategory)category);
        ShowCategory();
    }
    #endregion Coding rule : Function
}