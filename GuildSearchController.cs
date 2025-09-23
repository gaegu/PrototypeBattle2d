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
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GuildSearchController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.GuildSearchView; } }
    private GuildSearchView View { get { return base.BaseView as GuildSearchView; } }
    protected GuildSearchViewModel Model { get; private set; }
    public GuildSearchController() { Model = GetModel<GuildSearchViewModel>(); }
    public GuildSearchController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventRefreshGuildList(OnEventRefreshGuildList);
        Model.SetEventShowCreateGuild(OnEventShowCreateGuild);
        Model.SetEventShowInformation(OnEventShowInformation);
        Model.SetEventSeachGuild((guildName) =>
        {
            OnEventSearchGuild(guildName).Forget();
        });
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await RequestGetRecommendGuilds();

        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Guild/GuildSearchView";
    }

    private async void OnEventRefreshGuildList(bool isRecommend)
    {
        if (isRecommend)
        {
            if (Model.CheckRequestTimer(UnityEngine.Time.realtimeSinceStartup) == false)
                return;

            await RequestGetRecommendGuilds();
        }

        await View.ShowGuildList();
    }

    private async void OnEventShowInformation()
    {
        BaseController informationPopup = UIManager.Instance.GetController(UIType.GuildInformationPopup);
        await UIManager.Instance.EnterAsync(informationPopup);
    }

    private void OnEventShowCreateGuild()
    {
        BaseController createGuildPopup = UIManager.Instance.GetController(UIType.GuildCreatePopup);
        GuildCreatePopupModel guildCreatePopupModel = createGuildPopup.GetModel<GuildCreatePopupModel>();
        guildCreatePopupModel.SetEventSuccessCreateGuild(UniTask.Action(OnEventSuccessCreateGuild));
        UIManager.Instance.EnterAsync(createGuildPopup);
    }

    private async UniTaskVoid OnEventSuccessCreateGuild()
    {
        BaseController guildMainView = UIManager.Instance.GetController(UIType.GuildMainView);

        await UIManager.Instance.EnterAsync(guildMainView);

        await Exit();
    }

    private async UniTask OnEventSearchGuild(string guildName)
    {
        if (Model.CheckRequestTimer(UnityEngine.Time.realtimeSinceStartup) == false)
            return;

        if (string.IsNullOrEmpty(guildName))
            return;

        await RequestSearchGuild(guildName);
    }

    public async UniTask RequestGetRecommendGuilds()
    {
        BaseProcess guildRecommendGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildRecommendGet);

        if (await guildRecommendGetProcess.OnNetworkAsyncRequest())
        {
            guildRecommendGetProcess.OnNetworkResponse();

            GuildRecommendGetResponse response = guildRecommendGetProcess.GetResponse<GuildRecommendGetResponse>();
            Model.SetGuildDatas(response.GetGuildDatas());
        }
        else
        {
            GoBackAndRenew().Forget();
        }
    }

    public async UniTask RequestSearchGuild(string guildName)
    {
        BaseProcess guildSearchProcess = NetworkManager.Web.GetProcess(WebProcess.GuildSearch);

        guildSearchProcess.SetPacket(new SearchGuildInDto(guildName));

        if (await guildSearchProcess.OnNetworkAsyncRequest())
        {
            guildSearchProcess.OnNetworkResponse(Model);
        }
        else
        {
            Model.ResetGuildDatas();
            await View.ShowGuildList();
        }
    }

    private async UniTask GoBackAndRenew()
    {
        BaseProcess guildGetProcess = NetworkManager.Web.GetProcess(WebProcess.GuildGet);
        if (await guildGetProcess.OnNetworkAsyncRequest())
            guildGetProcess.OnNetworkResponse();

        while (UIManager.Instance.CheckOpenCurrentUI(UIType.ApplicationPopup) == false)
            await UIManager.Instance.BackAsync();
    }
    #endregion Coding rule : Function
}