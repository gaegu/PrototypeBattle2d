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

public class MailController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.MailView; } }
    public override void SetModel() { SetModel(new MailViewModel()); }
    private MailView View { get { return base.BaseView as MailView; } }
    private MailViewModel Model { get { return GetModel<MailViewModel>(); } }
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
        Model.SetApplication(true);
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetMailLocalData(LocalDataManager.Instance.GetLocalData<MailLocalData>());
        Model.SetEventDeleteAllMail(OnEventDeleteAllMail);
        Model.SetEventMailAllGet(OnEventMailAllGet);
        Model.SetEventReceiveMail(OnEventReceiveMail);
        Model.SetEventNoticeMail(OnEventNoticeMail);
        Model.SetEventDetailMail(OnEventDetailMail);
        Model.SetEventMailRefresh(OnEventMailRefresh);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestMailGet();
    }

    public override async UniTask LoadingBackProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Mail/MailView";
    }

    // 읽은 메일 삭제
    private async UniTask OnEventDeleteAllMail()
    {
        if (!Model.CheckDeleteMail())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MAIL_NOT_DELETE);
            return;
        }

        // 읽은 우편을 삭제하겠냐?
        await MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_MAIL_DELETE_MAIL_READ,
            onEventConfirm: () =>
            {
                Model.RemoveAllLocalMail();
                View.ShowAsync().Forget();

                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MAIL_NOTICE_DELETE_MAIL);
            });
    }

    // 모두 받기
    private async UniTask OnEventMailAllGet()
    {
        if (!Model.CheckReceiveMail())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MAIL_NOT_CONFIRM);
            return;
        }

        await RequestMailReceiveAll();
        OnEventMailRefresh();
    }

    // 메일슬롯 갱신
    private async UniTask OnEventRefreshMailList(MailUnitModel mailUnitModel)
    {
        // 삭제된 경우
        if (mailUnitModel.IsDeleted)
        {
            Model.RemoveMailUnitModel(mailUnitModel);

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MAIL_NOTICE_DELETE_MAIL);
        }
        // 보상을 받은 경우
        else
        {
            Model.SetReceiveMailUnitModel(mailUnitModel);
        }

        await RequestMailReceive(mailUnitModel);
        OnEventMailRefresh();
    }

    // 매일 받기
    private async UniTask OnEventReceiveMail(MailUnitModel mailUnitModel)
    {
        // if (mailUnitModel.IsLocalMail)
        //     return;

        await RequestMailReceive(mailUnitModel);
        OnEventMailRefresh();
    }

    private async UniTask OnEventNoticeMail(MailUnitModel mailUnitModel)
    {
        if (await RequestNotice(mailUnitModel))
            await View.RefreshAsync();

        BaseController mailDetailController = UIManager.Instance.GetController(UIType.MailDetailPopup);
        MailDetailPopupModel model = mailDetailController.GetModel<MailDetailPopupModel>();
        model.SetMailUnitModel(mailUnitModel);
        model.SetEventRefreshMailList(OnEventRefreshMailList);
        await UIManager.Instance.EnterAsync(mailDetailController);
    }

    private async UniTask OnEventDetailMail(MailUnitModel mailUnitModel)
    {
        if (mailUnitModel.StateType == MailUnitModel.MailStateType.Talk)
        {
            await RequestMailReceive(mailUnitModel);
            OnEventMailRefresh();
        }

        BaseController mailDetailController = UIManager.Instance.GetController(UIType.MailDetailPopup);
        MailDetailPopupModel model = mailDetailController.GetModel<MailDetailPopupModel>();
        model.SetMailUnitModel(mailUnitModel);
        model.SetEventRefreshMailList(OnEventRefreshMailList);
        await UIManager.Instance.EnterAsync(mailDetailController);
    }

    // 메일목록 갱신
    private void OnEventMailRefresh()
    {
        RequestMailGet(() => { View.ShowAsync().Forget(); }).Forget();
    }

    private async UniTask RequestMailGet(Action callBack = null)
    {
        BaseProcess mailGetProcess = NetworkManager.Web.GetProcess(WebProcess.MailGet);

        if (await mailGetProcess.OnNetworkAsyncRequest())
        {
            mailGetProcess.OnNetworkResponse(Model);

            if (callBack != null)
                callBack.Invoke();
        }
    }

    private async UniTask RequestMailReceive(MailUnitModel mailUnitModel)
    {
        Tuple<int, int>[] rewardItemTuple = mailUnitModel.GetRewardItems();

        if (!Model.User.IsItemInput(rewardItemTuple))
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(""));
            return;
        }

        BaseProcess mailReceiveProcess = NetworkManager.Web.GetProcess(WebProcess.MailReceive);

        mailReceiveProcess.SetPacket(Model.GetReceiveMailsInDto(mailUnitModel));

        if (await mailReceiveProcess.OnNetworkAsyncRequest())
        {
            Model.SetReceiveMailUnitModel(mailUnitModel);
            RedDotManager.Instance.Setting(ContentsType.Mail, new RedDot(Model.CheckRedDot()));

            mailReceiveProcess.OnNetworkResponse();
        }
    }

    private async UniTask<bool> RequestNotice(MailUnitModel mailUnitModel)
    {
        if (mailUnitModel.IsLocalMail)
            return false;

        BaseProcess mailReceiveProcess = NetworkManager.Web.GetProcess(WebProcess.MailReceive);

        mailReceiveProcess.SetPacket(Model.GetReceiveMailsInDto(mailUnitModel));

        if (await mailReceiveProcess.OnNetworkAsyncRequest())
        {
            Model.SetReceiveMailUnitModel(mailUnitModel);
            RedDotManager.Instance.Setting(ContentsType.Mail, new RedDot(Model.CheckRedDot()));
            return true;
        }
        return false;
    }

    private async UniTask RequestMailReceiveAll()
    {
        BaseProcess mailReceiveProcess = NetworkManager.Web.GetProcess(WebProcess.MailReceive);

        mailReceiveProcess.SetPacket(Model.GetAllReceiveMailsInDto());

        if (await mailReceiveProcess.OnNetworkAsyncRequest())
        {
            Model.SetReceiveAllMailUnitModels();
            RedDotManager.Instance.Setting(ContentsType.Mail, new RedDot(Model.CheckRedDot()));

            mailReceiveProcess.OnNetworkResponse();
        }
    }
    #endregion Coding rule : Function
}