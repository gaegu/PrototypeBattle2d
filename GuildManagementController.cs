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
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GuildManagementController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GuildManagementPopup; } }
    private GuildManagementPopup View { get { return base.BaseView as GuildManagementPopup; } }
    protected GuildManagementPopupModel Model { get; private set; }
    public GuildManagementController() { Model = GetModel<GuildManagementPopupModel>(); }
    public GuildManagementController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventClose(OnEventClose);
        Model.SetEventShowRequestedJoinList(UniTask.Action(OnEventShowRequestedJoinList));
        Model.SetEventShowSetting(OnEventShowSetting);
        Model.SetEventShowSendMail(OnEventShowSendMail);

        Model.SetOnEventNotJoinedError(OnEventNotJoinedError);
        Model.SetOnEventUnAuthorizedError(OnEventUnAuthorizedError);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (Model.IsChangeMemberList)
        {
            ObserverManager.NotifyObserver(GuildObserverID.UpdateMembers, null);

            Model.SetChangedMemberListFlag(false);
        }

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Guild/GuildManagementPopup";
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private async UniTaskVoid OnEventShowRequestedJoinList()
    {
        if (Model.IsTestMode)
        {
            View.ShowRequestedJoinList().Forget();
            return;
        }

        if (PlayerManager.Instance.MyPlayer.User.GuildModel.MemberGrade <= GuildMemberGrade.SubMaster)
        {
            if (await RequestGetReceiveSignupList())
                View.ShowRequestedJoinList().Forget();
        }
        else
        {
            Model.InitializeRequestedJoinListModel();

            View.ShowRequestedJoinList().Forget();
        }
    }

    private void OnEventShowSetting()
    {
        View.ShowSetting().Forget();
    }

    private void OnEventShowSendMail()
    {
        View.ShowSendMail().Forget();
    }

    private async UniTask<bool> RequestGetReceiveSignupList()
    {
        BaseProcess guildReceiveSignupGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildReceiveSignupGet);

        if (await guildReceiveSignupGetProcess.OnNetworkAsyncRequest())
        {
            guildReceiveSignupGetProcess.OnNetworkResponse();

            GuildReceiveSignupGetResponse response = guildReceiveSignupGetProcess.GetResponse<GuildReceiveSignupGetResponse>();
            Model.SetRequestedJoinList(response.data);

            return true;
        }
        else
        {
            guildReceiveSignupGetProcess.OnNetworkErrorResponse(Model);
            return false;
        }

    }

    private void OnEventNotJoinedError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOT_SIGN, onEventConfirm: () =>
        {
            base.Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                    TaskBackToSearchView().Forget();
            }).Forget();
        }).Forget();
    }

    private void OnEventUnAuthorizedError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOTICE_GRADE_NON_ACCESS, onEventConfirm: () =>
        {
            base.Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                    TaskCloseAndRefresh().Forget();
            }).Forget();
        }).Forget();
    }

    private async UniTask TaskBackToSearchView()
    {
        await UIManager.Instance.EnterAsync(UIType.GuildSearchView);

        UIManager.Instance.Exit(UIType.GuildMainView).Forget();
    }

    private async UniTask TaskCloseAndRefresh()
    {
        BaseProcess guildMemberGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildMemberGet);
        if (await guildMemberGetProcess.OnNetworkAsyncRequest())
        {
            guildMemberGetProcess.OnNetworkResponse();
            ObserverManager.NotifyObserver(GuildObserverID.UpdateGuild, new GuildParam(PlayerManager.Instance.MyPlayer.User.GuildModel.MyGuild));
        }
    }
    #endregion Coding rule : Function
}