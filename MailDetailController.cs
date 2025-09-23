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

public class MailDetailController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.MailDetailPopup; } }
    public override void SetModel() { SetModel(new MailDetailPopupModel()); }
    private MailDetailPopup Popup { get { return base.BaseView as MailDetailPopup; } }
    private MailDetailPopupModel Model { get { return GetModel<MailDetailPopupModel>(); } }
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
        Model.SetEventReceiveMail(OnEventReceiveMail);
        Model.SetEventDeleteMail(OnEventDeleteMail);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await Popup.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Mail/MailDetailPopup";
    }

    private async UniTask OnEventReceiveMail()
    {
        if (!await RequestMailReceive())
            return;

        await Model.OnEventRefreshMailList(Model.UnitModel);
        Model.UnitModel.SetRewardUnitDimd();
        await Popup.ShowAsync();
    }

    private async UniTask OnEventDeleteMail()
    {
        if (Model.UnitModel.StateType == MailUnitModel.MailStateType.Reward)
        {
            // 수령하지 않은 보상이 아직 있다. 받겠냐?
            await MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_MAIL_DELETE_MAIL_UNREAD,
                onEventConfirm: () =>
                {
                    DeleteProcess().Forget();
                });

            return;
        }

        DeleteProcess().Forget();
    }

    private async UniTask DeleteProcess()
    {
        if (Model.UnitModel.IsLocalMail)
        {
            Model.UnitModel.SetDeleted(true);
            await Model.OnEventRefreshMailList(Model.UnitModel);
        }
        else
        {
            if (!await RequestMailReceive())
                return;

            Model.UnitModel.SetDeleted(true);
            await Model.OnEventRefreshMailList(Model.UnitModel);
        }

        await Exit();
    }

    private async UniTask<bool> RequestMailReceive()
    {
        if (Model.UnitModel.IsLocalMail)
            return false;

        BaseProcess mailReceiveProcess = NetworkManager.Web.GetProcess(WebProcess.MailReceive);

        mailReceiveProcess.SetPacket(Model.GetReceiveMailsInDto());

        if (await mailReceiveProcess.OnNetworkAsyncRequest())
        {
            mailReceiveProcess.OnNetworkResponse();

            Model.UnitModel.SetStateType(MailUnitModel.MailStateType.Claimed);
            await Popup.ShowAsync();
            return true;
        }

        return false;
    }

    #endregion Coding rule : Function
}