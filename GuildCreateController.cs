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
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GuildCreateController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GuildCreatePopup; } }
    private GuildCreatePopup View { get { return base.BaseView as GuildCreatePopup; } }
    protected GuildCreatePopupModel Model { get; private set; }
    public GuildCreateController() { Model = GetModel<GuildCreatePopupModel>(); }
    public GuildCreateController(BaseModel baseModel) : base(baseModel) { }
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
        LoadTables();

        Model.SetEventShowSelectEmblem(OnEventShowSelectEmblem);
        Model.SetEventCreateGuild((guildName, symbolId, borderId) =>
        {
            OnEventCreateGuild(guildName, symbolId, borderId).Forget();
        });
        Model.SetEventAlreadyJoinError(OnEventAlreadyJoinError);
        Model.SetEventClose(OnEventClose);

        Model.InitializeThumbnailGuildEmblem();
        Model.InitializeCreateGuildCost();
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

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Guild/GuildCreatePopup";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        RemoveTables();

        return await base.Exit(onEventExtra);
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        Model.SetTestData();
    }

    private void LoadTables()
    {
        TableManager.Instance.LoadTable<GuildEmblemTable>();
    }

    private void RemoveTables()
    {
        if (UIManager.Instance.CheckOpenUI(UIType.GuildMainView) == false)
            TableManager.Instance.RemoveTable<GuildEmblemTable>();
    }

    public void OnEventShowSelectEmblem()
    {
        BaseController selectEmblemPopup = UIManager.Instance.GetController(UIType.GuildSelectEmblemPopup);

        GuildSelectEmblemPopupModel model = selectEmblemPopup.GetModel<GuildSelectEmblemPopupModel>();
        model.SetEventSelectEmblem(UpdateGuildEmblem);
        model.SetSelectSymbolDataId(Model.GetThumbnailGuildEmblemUnitModel().SymbolDataId);
        model.SetSelectBorderDataId(Model.GetThumbnailGuildEmblemUnitModel().BorderDataId);
        model.UpdateSelectSymbol();
        model.UpdateSelectBorder();

        UIManager.Instance.EnterAsync(selectEmblemPopup);
    }

    private void OnEventAlreadyJoinError()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_GUILD_ALREADY_JOINED, onEventConfirm: () =>
        {
            TaskBackToSearchView().Forget();
        }).Forget();
    }

    private async UniTask TaskBackToSearchView()
    {
        await UIManager.Instance.EnterAsync(UIType.GuildMainView);

        UIManager.Instance.Exit(UIType.GuildSearchView).Forget();
    }

    public async UniTask OnEventCreateGuild(string guildName, int symbolId, int borderId)
    {
        if (CheckFilter(guildName) == false)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_GUILD_POPUP_BADWORD);

            return;
        }

        if (CheckHaveEnoughCost() == false)
        {
            //ShowToastMessage(TableManager.Instance.GetLocalization((int)LocalizationDefine.LOCALIZATION_UI_LABEL_GAO_LACK_CURRENCY));
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_COMMON_NOT_ENOUGH_GOODS);

            return;
        }

        if (await RequestCreateGuild(guildName, symbolId, borderId))
        {
            base.Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                {
                    Model.OnEventSuccessCreateGuild?.Invoke();
                    Model.ResetEvent();
                }
            }).Forget();
        }
    }

    private bool CheckHaveEnoughCost()
    {
        Currency haveCurrency = PlayerManager.Instance.MyPlayer.User.CurrencyModel.GetGoodsByType(Model.RequireCostUnitModel.CurrencyType);

        long requireCount = Model.RequireCostUnitModel.Value;

        return haveCurrency.Count >= requireCount;
    }

    private void UpdateGuildEmblem(int emblemSymbolDataId, int emblemBorderDataId)
    {
        Model.SetThumbnailGuildEmblem(emblemSymbolDataId, emblemBorderDataId);

        View.ShowGuildEmblemThumbnail().Forget();
    }

    private bool CheckFilter(string text)
    {
        // TODO: 필터 기능 구현
        return true;
    }

    private async UniTask<bool> RequestCreateGuild(string guildName, int symbolId, int borderId)
    {
        if (Model.IsTestMode)
            return false;

        BaseProcess guildCreateProcess = NetworkManager.Web.GetProcess(WebProcess.GuildCreate);

        guildCreateProcess.SetPacket(new CreateGuildInDto(guildName, symbolId, borderId));

        if (await guildCreateProcess.OnNetworkAsyncRequest())
        {
            guildCreateProcess.OnNetworkResponse();

            return true;
        }

        return false;
    }

    public void OnEventClose()
    {
        Model.ResetEvent();

        Exit().Forget();
    }
    #endregion Coding rule : Function
}