//=========================================================================================================
#pragma warning disable CS1998
using System;
//using System.Collections;
using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class DispatchController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.DispatchView; } }
    public override void SetModel() { SetModel(new DispatchViewModel()); }
    private DispatchView View { get { return base.BaseView as DispatchView; } }
    private DispatchViewModel Model { get { return GetModel<DispatchViewModel>(); } }
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
        Model.SetResetTime(TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily));
        Model.SetEventRefreshDispatch(OnEventRefreshDispatch);
        Model.SetEventBulkDispatch(OnEventBulkDispatch);
        Model.SetEventClaimRewardAll(OnEventClaimRewardAll);
        Model.SetEventClose(OnEventClose);
        Model.SetEventShowDispatchSendPopup(ShowDispatchSendPopup);
        Model.SetEventShowDispatchStatusPopup(ShowDispatchStatusPopup);
        Model.SetEventShowPercentInfo(ShowDispatchPercentInfoPopup);
        Model.SetEventClaimReward((ids) => RequestClaimReward(ids, SpecialEffectType.None).Forget());

        SetMembership();

        ObserverManager.AddObserver(CommonObserverID.TimeRefresh, this);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await RequestGetAllDispatches();

        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dispatch/DispatchView";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.TimeRefresh, this);

        return await base.Exit(onEventExtra);
    }

    public void OnEventRefreshDispatch()
    {
        if (Model.CheckExistReadyDispatch(NetworkManager.Web.ServerTimeUTC) == false)
        {
            MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_NOTICE_NO_CHANGE_DISPATCH).Forget();
            return;
        }

        ShowRefreshPopup();
    }

    public async void OnEventBulkDispatch()
    {
        if (!Model.IsOnAutoSelectAll)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_DISPATCHVIEW_LOCK_BULK_SEND_DISPATCH);
            return;
        }

        if (Model.CheckExistDispatch() == false)
        {
            await MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_NOTICE_IMPOSSIBLE_DISPATCH);
            return;
        }

        ShowDispatchBulkSendPopup();
    }

    public void OnEventClaimRewardAll()
    {
        if (!Model.IsOnClaimRewardAll)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_DISPATCHVIEW_LOCK_CLAIM_REWARD_ALL);
            return;
        }

        var completedDispatchIds = Model.GetAllCompletedDispatchIds(NetworkManager.Web.ServerTimeUTC);

        if (completedDispatchIds.Length == 0)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_NOTICE_NO_SUCCESS_DISPATCH);
            return;
        }

        RequestClaimReward(completedDispatchIds, SpecialEffectType.DispatchAllReceive).Forget();
    }

    public void OnEventClose()
    {
        Exit().Forget();
    }

    private void OnUpdateDispatch(DispatchItemDto dispatchDto)
    {
        Model.UpdateDispatchModels(new DispatchItemDto[] { dispatchDto }, NetworkManager.Web.ServerTimeUTC);

        View.SetDispatchList();
    }

    private void OnUpdateDispatches(DispatchItemDto[] dispatchDtos)
    {
        Model.UpdateDispatchModels(dispatchDtos, NetworkManager.Web.ServerTimeUTC);

        View.SetDispatchList();
    }

    private void SetMembership()
    {
        MembershipModel membershipModel = PlayerManager.Instance.MyPlayer.User.MembershipModel;

        Model.SetDispatchGradeRange(membershipModel.GetEffectValue(SpecialEffectType.DispatchRankUpgrade));
        Model.SetIsOnAutoSelectAll(membershipModel.GetEffectValue(SpecialEffectType.DispatchAllAutoSelect) > 0);
        Model.SetIsOnClaimRewardAll(membershipModel.GetEffectValue(SpecialEffectType.DispatchAllReceive) > 0);
        Model.SetDispatchCount(membershipModel.GetEffectValue(SpecialEffectType.DispatchMaxCount));
    }

    private void ShowDispatchBulkSendPopup()
    {
        var baseController = UIManager.Instance.GetController(UIType.DispatchBulkSendPopup);
        var popupModel = baseController.GetModel<DispatchBulkSendPopupModel>();
        var nowTime = NetworkManager.Web.ServerTimeUTC;

        popupModel.SetEventUpdateDispatches(OnUpdateDispatches);
        popupModel.InitializePossibleDispatches(Model.GetDispatchPossibleCharacters(), Model.DispatchInfoUnitModels, nowTime);

        UIManager.Instance.EnterAsync(baseController);
    }

    private void ShowRefreshPopup()
    {
        BaseController messageBoxCurrencyPopupController = UIManager.Instance.GetController(UIType.MessageBoxCurrencyPopup);
        MessageBoxCurrencyPopupModel model = messageBoxCurrencyPopupController.GetModel<MessageBoxCurrencyPopupModel>();
        CostTable costTable = TableManager.Instance.GetTable<CostTable>();
        CostTableData costTableData = costTable.GetDataByID((int)CostDefine.COST_DISPATCH_RESET);

        int needCashCount = costTableData.GetGOODS_COUNT(0);

        CurrencyUnitModel currencyModel = CurrencyGeneratorModel.GetCurrencyUnitModel(GoodsType.Currency, (int)CurrencyDefine.CURRENCY_CASH, needCashCount);
        currencyModel.UpdateMyCount(PlayerManager.Instance.MyPlayer.User);

        string description = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_CHANGE_DESC);
        string title = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_CHANGE);

        model.SetCostValueTemplate(() => OnEventRefresh(needCashCount), description, currencyModel, title);

        UIManager.Instance.EnterAsync(messageBoxCurrencyPopupController);
    }

    private void ShowDispatchSendPopup(DispatchInfoUnitModel infoModel)
    {
        var baseController = UIManager.Instance.GetController(UIType.DispatchSendPopup);

        var popupModel = baseController.GetModel<DispatchSendPopupModel>();
        popupModel.SetEventUpdateDispatches(OnUpdateDispatches);
        popupModel.SetData(infoModel);

        UIManager.Instance.EnterAsync(baseController);
    }

    private void ShowDispatchStatusPopup(DispatchInfoUnitModel infoModel)
    {
        var baseController = UIManager.Instance.GetController(UIType.DispatchStatusPopup);

        var popupModel = baseController.GetModel<DispatchStatusPopupModel>();
        popupModel.SetEventUpdateDispatch(OnUpdateDispatch);
        popupModel.SetData(PlayerManager.Instance.MyPlayer.User, infoModel);

        UIManager.Instance.EnterAsync(baseController);
    }

    private void ShowDispatchPercentInfoPopup()
    {
        var baseController = UIManager.Instance.GetController(UIType.DispatchPercentInfoPopup);

        UIManager.Instance.EnterAsync(baseController);
    }

    private async UniTask RequestGetAllDispatches()
    {
        var dispatchGetProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchGet);

        if (await dispatchGetProcess.OnNetworkAsyncRequest())
        {
            dispatchGetProcess.OnNetworkResponse();

            var response = dispatchGetProcess.GetResponse<DispatchGetResponse>();

            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetDispatchModels(response.data.contents.items, NetworkManager.Web.ServerTimeUTC);
        }
    }

    private async UniTask RequestClaimReward(long[] ids, SpecialEffectType specialEffectType)
    {
        var dispatchClaimRewardProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchClaimReward);

        var request = dispatchClaimRewardProcess.GetRequest<DispatchClaimRewardRequest>();

        request.SetDispatchIds(ids);
        request.SetSpecialEffectType(specialEffectType);
        request.SetRewardPopupFlag(true);

        if (await dispatchClaimRewardProcess.OnNetworkAsyncRequest())
        {
            dispatchClaimRewardProcess.OnNetworkResponse();

            var response = dispatchClaimRewardProcess.GetResponse<DispatchClaimRewardResponse>();

            Model.SetDispatchModels(response.data.dispatch.contents.items, NetworkManager.Web.ServerTimeUTC);

            View.SetDispatchList();
        }
    }

    private void OnEventRefresh(int needCashCount)
    {
        if (PlayerManager.Instance.MyPlayer.User.CurrencyModel.CheckCompareByInt(CompareType.GreaterEqual, CurrencyType.Cash, needCashCount))
        {
            RequestRefreshAllDispatches().Forget();
        }
        else
        {
            ShowMessageBoxByLackingCurrency();
        }
    }

    private async UniTask RequestRefreshAllDispatches()
    {
        var dispatchChangeProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchChange);

        if (await dispatchChangeProcess.OnNetworkAsyncRequest())
        {
            dispatchChangeProcess.OnNetworkResponse();

            var response = dispatchChangeProcess.GetResponse<DispatchChangeResponse>();

            Model.SetDispatchModels(response.data.dispatch.contents.items, NetworkManager.Web.ServerTimeUTC);

            View.SetDispatchList();
        }
    }

    private void ShowMessageBoxByLackingCurrency()
    {
        string errorMessage = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_LACKING_GOODS);
        MessageBoxManager.ShowYesBox(errorMessage);
    }

    private void OnUpdateTimer()
    {
        if (!View)
            return;

        TimeSpan remainTimeUntilReset = Model.ResetTime - NetworkManager.Web.ServerTimeUTC;

        string remainTimeText = UtilModel.String.GetRemainTimeToString(remainTimeUntilReset, TimeStringFormType.hh_mm_ss);

        View.SetRefreshTimer(remainTimeText);

        Model.OnEventRefreshDispatchTime();

        if (remainTimeUntilReset.TotalMilliseconds <= double.Epsilon)
        {
            Model.SetResetTime(TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily));

            MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_DISPATCHVIEW_CHANGE_LIST_TIME_OVER).Forget();

            RequestRefreshAllDispatches().Forget();
        }
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case CommonObserverID.TimeRefresh:
                OnUpdateTimer();
                break;
        }
    }
    #endregion Coding rule : Function
}