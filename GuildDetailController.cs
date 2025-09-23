//=========================================================================================================
#pragma warning disable CS1998
using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GuildDetailController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GuildDetailPopup; } }
    private GuildDetailPopup View { get { return base.BaseView as GuildDetailPopup; } }
    protected GuildDetailPopupModel Model { get; private set; }
    public GuildDetailController() { Model = GetModel<GuildDetailPopupModel>(); }
    public GuildDetailController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventRequestSignup(OnEventRequestSignup);
        Model.SetEventAlreadyJoinError(OnEventAlreadyJoinError);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await RequestGuildMembers();

        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Guild/GuildDetailPopup";
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private void OnEventRequestSignup()
    {
        if (!CheckSignupRequreLevel())
        {
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_SIGNUP_NOT_ENOUGH);

            MessageBoxManager.ShowToastMessage(message);
            return;
        }

        if (Model.GuildInfoModel.SignupType == GuildSignupType.Unable)
        {
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GAO_JOIN_UNABLE_LIMITED);

            MessageBoxManager.ShowToastMessage(message);
            return;
        }

        if (!CheckSignupCoolTime())
            return;

        RequestSignup(OnEventFinishedRequestSignup).Forget();
    }

    private bool CheckSignupRequreLevel()
    {
        return PlayerManager.Instance.MyPlayer.User.AccountLevel >= Model.GuildInfoModel.SignupLevel;
    }

    private bool CheckSignupCoolTime()
    {
        BalanceTable balanceTable = TableManager.Instance.GetTable<BalanceTable>();
        BalanceTableData balanceData = balanceTable.GetDataByID((int)BalanceDefine.BALANCE_GUILD_TIME_JOIN_AFTER_QUITBAN);
        int coolTime = (int)balanceData.GetINDEX(0);

        DateTime leavedAt = PlayerManager.Instance.MyPlayer.User.GuildModel.LeavedAt;
        int elapsedMinute = (int)UtilModel.Time.GetElapsedTimeUTC(leavedAt).TotalMinutes;

        if (elapsedMinute < coolTime)
        {
            System.Text.StringBuilder message = new System.Text.StringBuilder();

            switch (PlayerManager.Instance.MyPlayer.User.GuildModel.LeavedReason)
            {
                case GuildModel.GuildLeavedReason.Withdraw:
                    message.Append(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GAO_QUIT_TIME_LEFT));
                    break;

                case GuildModel.GuildLeavedReason.Expel:
                    message.Append(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_BANNED_TIME_LEFT));
                    break;

                case GuildModel.GuildLeavedReason.Disband:
                    message.Append(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GAO_DISMISSED_TIME_LEFT));
                    break;
            }

            message.Append(string.Format(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_COMMON_TIME_COOLDOWN_MIN), coolTime - elapsedMinute));
            MessageBoxManager.ShowToastMessage(message.ToString());

            return false;
        }
        else
        {
            return true;
        }
    }

    private void OnEventFinishedRequestSignup()
    {
        string message;

        switch (Model.GuildInfoModel.SignupType)
        {
            case GuildSignupType.Approval:
                message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_APPLY_DESC);
                MessageBoxManager.ShowYesBox(string.Format(message, Model.GuildInfoModel.GuildName));
                break;

            case GuildSignupType.Free:
                Exit(async (state) =>
                {
                    if (state == UISubState.Finished)
                        ShowGuildMain().Forget();
                }).Forget();
                break;
        }
    }

    private async UniTask ShowGuildMain()
    {
        BaseController guildMainView = UIManager.Instance.GetController(UIType.GuildMainView);

        UIManager.Instance.GetController(UIType.GuildSearchView);

        await UIManager.Instance.EnterAsync(guildMainView);

        string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_SIGNUP_DESC);
        MessageBoxManager.ShowYesBox(string.Format(message, Model.GuildInfoModel.GuildName));

        await UIManager.Instance.Exit(UIType.GuildSearchView);
    }

    private async UniTask RequestSignup(Action onEventFinished = null)
    {
        if (Model.IsTestMode)
        {
            onEventFinished?.Invoke();
            return;
        }

        BaseProcess guildSignupProcess = NetworkManager.Web.GetProcess(WebProcess.GuildSignup);

        guildSignupProcess.SetPacket(new GuildSignupInDto(Model.GuildInfoModel.GuildId));

        if (await guildSignupProcess.OnNetworkAsyncRequest())
        {
            guildSignupProcess.OnNetworkResponse();

            onEventFinished?.Invoke();
        }
        else
        {
            guildSignupProcess.OnNetworkErrorResponse(Model);
        }
    }

    private async UniTask RequestGuildMembers()
    {
        if (Model.IsTestMode)
            return;

        BaseProcess guildMemberGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildMemberGet);

        guildMemberGetProcess.SetPacket(new GetGuildMemberInDto(Model.GuildInfoModel.GuildId));

        if (await guildMemberGetProcess.OnNetworkAsyncRequest())
        {
            guildMemberGetProcess.OnNetworkResponse();

            GuildMemberGetResponse response = guildMemberGetProcess.GetResponse<GuildMemberGetResponse>();

            Model.SetGuildMembers(response.GetGuildMembers());
        }
    }

    private void OnEventAlreadyJoinError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOTICE_GRADE_NON_ACCESS, onEventConfirm: () =>
        {
            TaskBackToSearchView().Forget();
        }).Forget();
    }

    private async UniTask TaskBackToSearchView()
    {
        await UIManager.Instance.EnterAsync(UIType.GuildSearchView);

        UIManager.Instance.Exit(UIType.GuildMainView).Forget();
    }
    #endregion Coding rule : Function
}