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

public class ArenaMatchSelectController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ArenaMatchSelectPopup; } }
    public override void SetModel() { SetModel(new ArenaMatchSelectPopupModel()); }
    private ArenaMatchSelectPopup View { get { return base.BaseView as ArenaMatchSelectPopup; } }
    private ArenaMatchSelectPopupModel Model { get { return GetModel<ArenaMatchSelectPopupModel>(); } }
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
        Model.SetEventReset(OnEventReset);
        Model.SetEventChallenge(OnEventChallenge);
        Model.SetEventCharge(OnEventCharge);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestArenaMatchGet();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        Model.MyPlayer.RefreshTeam(PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(DeckType.ArenaAttack).GetCurrentTeam());
        View.Refresh();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Arena/ArenaMatchSelectPopup";
    }

    private void OnEventChallenge(ArenaPlayerInfoUnitModel model)
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ArenaPrepareView);
        ArenaPrepareViewModel arenaModel = controller.GetModel<ArenaPrepareViewModel>();
        arenaModel.SetEnemyPlayerModel(model);
        arenaModel.SetCurrentDeckType(DeckType.ArenaAttack);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventReset()
    {
        
    }

    private void OnEventCharge()
    {
        ItemTable itemTable = TableManager.Instance.GetTable<ItemTable>();
        ChargeItemTable chargeItemTable = TableManager.Instance.GetTable<ChargeItemTable>();
        ItemTableData itemData = itemTable.GetDataByID(Model.ArenaTicket.DataId);
        ChargeItemTableData chargeItemData = chargeItemTable.GetDataByID(itemData.GetCHARGE_ITEM());
        
        int buyCount = PlayerManager.Instance.MyPlayer.User.ArenaModel.TicketBuyCount;
        int chargeCount = chargeItemData.GetFREE_CHARGE_COUNT();
        if ((ChargeType)chargeItemData.GetBUY_TYPE() == ChargeType.Limit)
        {
            int limitCount = chargeItemData.GetMAX_COUNT();
            if (limitCount == buyCount)
            {
                MessageBoxManager.ShowToastMessage("@오늘 하루 구매 횟수 모두 사용");
                return;
            }
        }

        string message = "@다음 재화를 소모하여 아레나 입장 횟수를 충전하시겠습니까?";
        Action onEventBuyTicket = () =>{ RequestChargeTicket(chargeCount).Forget(); };
        MessageBoxManager.ShowTicketChargeMessageBox(message, Model.ArenaTicket.DataId, buyCount, onEventBuyTicket);
    }

    private async UniTask RequestArenaMatchGet()
    {
        //임시
        Model.SetMatchSelectPopup(PlayerManager.Instance.MyPlayer.User, default);
        return;
    }

    private async UniTask RequestChargeTicket(int count)
    {
        BaseProcess ticketBuyProcess = NetworkManager.Web.GetProcess(WebProcess.BuyTicket);
        ticketBuyProcess.SetPacket(new BuyTicketInDto(Model.ArenaTicket.Id, count));
        if (await ticketBuyProcess.OnNetworkAsyncRequest())
        {
            ticketBuyProcess.OnNetworkResponse();

            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_INFINITYCIRCUIT_NOTICE_TICKETRECHARGED);
        }
    }

    #endregion Coding rule : Function
}