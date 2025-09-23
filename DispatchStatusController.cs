//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Threading;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class DispatchStatusController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.DispatchStatusPopup; } }
    private DispatchStatusPopup View { get { return base.BaseView as DispatchStatusPopup; } }
    protected DispatchStatusPopupModel Model { get; private set; }
    public DispatchStatusController() { Model = GetModel<DispatchStatusPopupModel>(); }
    public DispatchStatusController(BaseModel baseModel) : base(baseModel) { }
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
        ObserverManager.AddObserver(CommonObserverID.TimeRefresh, this);

        Model.SetEventClickCancelDispatch(OnEventCancelDispatch);
        Model.SetEventClickClose(OnEventClose);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
        View.UpdateTime(NetworkManager.Web.ServerTimeUTC);
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.TimeRefresh, this);

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dispatch/DispatchStatusPopup";
    }

    private async void OnEventCancelDispatch()
    {
        if (Model.IsCompleted(NetworkManager.Web.ServerTimeUTC))
            return;

        await MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_NOTICE_DISPATCH_CANCEL,
            onEventConfirm: () =>
            {
                RequestCancelDispatch().Forget();
            });
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private async UniTask RequestCancelDispatch()
    {
        var dispatchCancelProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchCancel);
        var request = dispatchCancelProcess.GetRequest<DispatchCancelRequest>();

        request.SetDispatchId(Model.DispatchId);

        if (await dispatchCancelProcess.OnNetworkAsyncRequest())
        {
            dispatchCancelProcess.OnNetworkResponse();

            var response = dispatchCancelProcess.GetResponse<DispatchCancelResponse>();

            Model.OnEventCancelDispatch(response.GetData(Model.DispatchId));
        }

        Exit().Forget();
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case CommonObserverID.TimeRefresh:
                View.UpdateTime(NetworkManager.Web.ServerTimeUTC);
                break;
        }
    }
    #endregion Coding rule : Function
}