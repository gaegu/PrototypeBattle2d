//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GuildMemberManageController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GuildMemberManagePopup; } }
    public override void SetModel() { SetModel(new GuildMemberManagePopupModel()); }
    private GuildMemberManagePopup View { get { return base.BaseView as GuildMemberManagePopup; } }
    private GuildMemberManagePopupModel Model { get { return GetModel<GuildMemberManagePopupModel>(); } }
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
        Model.SetOnEventNotJoinedError(OnEventNotJoinedError);
        Model.SetOnEventUnAuthorizedError(OnEventUnAuthorizedError);
        Model.SetOnEventNotFoundMemberError(OnEventNotFoundMemberError);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetMyGrade(PlayerManager.Instance.MyPlayer.User.GuildModel.MemberGrade);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Guild/GuildMemberManagePopup";
    }

    private void OnEventNotJoinedError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOT_SIGN, onEventConfirm: () =>
        {
            Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                    TaskBackToSearchView().Forget();
            }).Forget();
        }).Forget();
    }

    private void OnEventUnAuthorizedError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOTICE_GRADE_NON_ACCESS_ALLOW, onEventConfirm: () =>
        {
            Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                    TaskCloseAndRefresh().Forget();
            }).Forget();
        }).Forget();
    }

    private void OnEventNotFoundMemberError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_NOTICE_GRADE_NON_ACCESS, onEventConfirm: () =>
        {
            Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                    ObserverManager.NotifyObserver(GuildObserverID.UpdateMembers, null);
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