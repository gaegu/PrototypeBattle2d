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
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ReeltapeInfoController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ReeltapeInfoPopup; } }
    public override void SetModel() { SetModel(new ReeltapeInfoPopupModel()); }
    private ReeltapeInfoPopup View { get { return base.BaseView as ReeltapeInfoPopup; } }
    private ReeltapeInfoPopupModel Model { get { return GetModel<ReeltapeInfoPopupModel>(); } }
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
        Model.SetEventEquip(OnEventEquip);
        Model.SetEventUnEquip(OnEventUnEquip);
        Model.SetEventLevelUp(OnEventLevelUp);
        Model.SetEventTranscendence(OnEventTranscendence);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetReeltapeInfoModel();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }
    
    public override void Refresh()
    {
        Model.SetReeltapeInfoModel();
        View.RefreshAsync().Forget();
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Reeltape/ReeltapeInfoPopup";
    }

    private void OnEventEquip()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ReeltapeManagementPopup);
        ReeltapeManagementPopupModel model = controller.GetModel<ReeltapeManagementPopupModel>();

        model.SetChooseType(ReeltapeManagementPopupModel.ListType.Character);
        model.SetSelectReeltape(Model.Reeltape);
        model.SetEquippedReeltape(null);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventUnEquip()
    {
        RequestUnEquip().Forget();
    }

    private async UniTask RequestUnEquip()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.ReeltapeUnEquip);
        process.SetPacket(new UnequipReeltapeInDto(Model.Reeltape.WearCharacterId, Model.Reeltape.Id));

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();

            Exit().Forget();
        }
    }

    private void OnEventLevelUp()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ReeltapeLevelUpPopup);

        ReeltapeLevelUpPopupModel model = controller.GetModel<ReeltapeLevelUpPopupModel>();
        model.SetTargetReeltape(Model.Reeltape);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventTranscendence()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ReeltapeTranscendencePopup);

        ReeltapeTranscendencePopupModel model = controller.GetModel<ReeltapeTranscendencePopupModel>();
        model.SetTargetReeltape(Model.Reeltape);

        UIManager.Instance.EnterAsync(controller).Forget();
    }
    #endregion Coding rule : Function
}