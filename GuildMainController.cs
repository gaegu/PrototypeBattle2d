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
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GuildMainController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.GuildMainView; } }
    private GuildMainView View { get { return base.BaseView as GuildMainView; } }
    protected GuildMainViewModel Model { get; private set; }
    public GuildMainController() { Model = GetModel<GuildMainViewModel>(); }
    public GuildMainController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventShowSettingPopup(OnEventShowSettingPopup);
        Model.SetEventLeaveGuild(OnEventLeaveGuild);
        Model.SetEventShowMainContent(OnEventShowMainContent);
        Model.SetEventInformation(OnEventShowInformation);
        Model.SetEventNotJoinedError(OnEventNotJoinedError);
        Model.SetEventNoAuthorityError(OnEventNoAuthorityError);
        Model.SetEventShowMemberList(UniTask.Action(OnEventShowMemberList));
        Model.SetEventShowLogList(UniTask.Action(OnEventShowLogList));
        Model.SetCurrentTabType(GuildMainViewModel.TabType.Main);

        ObserverManager.AddObserver(GuildObserverID.UpdateMembers, this);
        ObserverManager.AddObserver(GuildObserverID.UpdateGuild, this);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestGuildGet();
        Model.SetGuildInfo(PlayerManager.Instance.MyPlayer.User.GuildModel.MyGuild);
    }

    public override async UniTask Process()
    {
        bool isInitializedGuildInfo = Model.CheckInitializedGuildInfo();

        View.SetActiveMainView(isInitializedGuildInfo);

        if (isInitializedGuildInfo == false)
            return;

        await RequestGetGuildMembers();

        RedDotManager.Instance.Notify();

        await View.ShowAsync();
    }

    public override void Refresh()
    {
        RedDotManager.Instance.Notify();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(GuildObserverID.UpdateMembers);
        ObserverManager.RemoveObserver(GuildObserverID.UpdateGuild);

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Guild/GuildMainView";
    }

    private void OnEventShowSettingPopup()
    {
        BaseController guildManagementPopup = UIManager.Instance.GetController(UIType.GuildManagementPopup);

        GuildManagementPopupModel managementPopupModel = guildManagementPopup.GetModel<GuildManagementPopupModel>();

        managementPopupModel.SetGuildSettingInfo(Model.GuildInfoModel.GetGuildData());

        UIManager.Instance.EnterAsync(guildManagementPopup);
    }

    private async void OnEventLeaveGuild()
    {
        //SettingUnit에 달려있는 길드 탈퇴 이곳으로 옮기기
        string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_DEACTIVATE_DESC_2);
        await MessageBoxManager.ShowYesNoBox(string.Format(message, Model.GuildInfoModel.GuildName),
            onEventConfirm: () =>
            {
                RequestLeaveGuild().Forget();
            });
    }

    private void OnEventShowInformation()
    {
        BaseController informationPopup = UIManager.Instance.GetController(UIType.GuildInformationPopup);
        UIManager.Instance.EnterAsync(informationPopup);
    }

    private async void OnEventShowMainContent()
    {
        await View.ShowCurrentContent();
    }

    private async UniTaskVoid OnEventShowMemberList()
    {
        if (Model.CheckNetworkRequestTime(GuildMainViewModel.TabType.Member, DateTime.Now))
        {
            if (await RequestGetGuildMembers() == false)
            {
                string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NO_FIND);
                await MessageBoxManager.ShowYesBox(message, onEventConfirm: OnEventCloseGuildMainView);
                return;
            }
        }

        await View.ShowCurrentContent();
    }

    private async UniTaskVoid OnEventShowLogList()
    {
        if (Model.CheckNetworkRequestTime(GuildMainViewModel.TabType.Log, DateTime.Now))
        {
            if (await RequestGetGuildLogs() == false)
            {
                string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NO_FIND);
                await MessageBoxManager.ShowYesBox(message, onEventConfirm: OnEventCloseGuildMainView);
                return;
            }
        }

        await View.ShowCurrentContent();
    }

    private void OnEventCloseGuildMainView()
    {
        Exit().Forget();
    }

    private async UniTask RequestGuildGet()
    {
        BaseProcess guildGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildGet);

        if (await guildGetProcess.OnNetworkAsyncRequest())
            guildGetProcess.OnNetworkResponse();
    }

    private async UniTask<bool> RequestGetGuildMembers()
    {
        BaseProcess guildMemberGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildMemberGet);

        guildMemberGetProcess.SetPacket(new GetGuildMemberInDto(Model.GuildInfoModel.GuildId));

        if (await guildMemberGetProcess.OnNetworkAsyncRequest())
        {
            guildMemberGetProcess.OnNetworkResponse(Model);

            return true;
        }

        return false;
    }

    private async UniTask<bool> RequestGetGuildLogs()
    {
        BaseProcess guildLogGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildLogGet);

        if (await guildLogGetProcess.OnNetworkAsyncRequest())
        {
            guildLogGetProcess.OnNetworkResponse();

            GuildLogGetResponse response = guildLogGetProcess.GetResponse<GuildLogGetResponse>();
            Model.SetGuildLogs(response.data);

            return response.CheckExistMyGuild();
        }

        return false;
    }

    private async UniTask RequestLeaveGuild()
    {
        GuildWithdrawProcess guildWithdrawProcess = NetworkManager.Web.GetProcess<GuildWithdrawProcess>();

        if (await guildWithdrawProcess.OnNetworkAsyncRequest())
        {
            guildWithdrawProcess.OnNetworkResponse();
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_DEACTIVATE_DESC);
            MessageBoxManager.ShowYesBox(string.Format(message, Model.GuildInfoModel.GuildName), onEventConfirm: UniTask.Action(ShowChangeApplicationPopup));
        }
        else if (guildWithdrawProcess.CheckAuthorize() == false)
        {
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_DEACTIVATE_DESC);
            MessageBoxManager.ShowYesBox(string.Format(message, Model.GuildInfoModel.GuildName), onEventConfirm: UniTask.Action(ShowChangeApplicationPopup));
        }
        else
        {
            guildWithdrawProcess.OnNetworkErrorResponse(Model);
        }
    }

    private async UniTaskVoid ShowChangeApplicationPopup()
    {
        while (UIManager.Instance.CheckOpenCurrentUI(UIType.ApplicationPopup) == false)
        {
            await UIManager.Instance.BackAsync();
        }
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case GuildObserverID.UpdateMembers:
                {
                    if (Model.CurrentTabType != GuildMainViewModel.TabType.Member)
                        break;

                    OnEventShowMemberList().Forget();
                    break;
                }

            case GuildObserverID.UpdateGuild:
                {
                    GuildParam guildParam = (GuildParam)observerParam;

                    Model.SetGuildInfo(guildParam.GuildData);
                    Model.SetMyGrade(PlayerManager.Instance.MyPlayer.User.GuildModel.MemberGrade);
                    View.ShowGuildInfo().Forget();
                    View.ShowButtons();
                    break;
                }
        }
    }

    private void OnEventNotJoinedError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOT_SIGN, onEventConfirm: () =>
        {
            TaskBackToSearchView().Forget();
        }).Forget();
    }

    private void OnEventNoAuthorityError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOTICE_GRADE_NON_ACCESS, onEventConfirm: () =>
        {
            TaskRefresh().Forget();
        }).Forget();
    }

    private async UniTask TaskBackToSearchView()
    {
        await UIManager.Instance.EnterAsync(UIType.GuildSearchView);

        UIManager.Instance.Exit(UIType.GuildMainView).Forget();
    }

    private async UniTask TaskRefresh()
    {
        //정보를 다시 받아옴
        BaseProcess guildMemberGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildMemberGet);
        if (await guildMemberGetProcess.OnNetworkAsyncRequest())
        {
            guildMemberGetProcess.OnNetworkResponse(Model);
            View.ShowAsync().Forget();
        }
    }
    #endregion Coding rule : Function
}