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

public class ConsumeItemController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ConsumeItemPopup; } }
    public override void SetModel() { SetModel(new ConsumeItemPopupModel()); }
    private ConsumeItemPopup View { get { return base.BaseView as ConsumeItemPopup; } }
    private ConsumeItemPopupModel Model { get { return GetModel<ConsumeItemPopupModel>(); } }
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
        Model.SetEventUseItem(OnEventUseItem);
        Model.SetFuncGetNetminingDataId(GetNetminingDataId);

        Model.SetConsumeItemModel();
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

    public override UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        Model.ClearModel();

        onEventExtra = async (state) =>
        {
            if (state == UISubState.Finished)
            {
                IronJade.Debug.Log("[Consume Item] Close ConsumeItemPopup");
                var controller = UIManager.Instance.GetController(UIType.InventoryView);
                if (controller != null)
                {
                    IronJade.Debug.Log("[Consume Item] Refresh InventoryView");
                    controller.Refresh();
                }
            }
        };

        return base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Inventory/ConsumeItemPopup";
    }

    private int GetNetminingDataId()
    {
        AutoBattleTableData currentNetminingData = NetMiningManager.Instance.GetCurrentAutoBattleData();

        return currentNetminingData.GetID();
    }

    private void OnEventUseItem()
    {
        if (Model.IsPreview)
        {
            Exit().Forget();
            return;
        }

        if (Model.GetCurrentCount() == 0)
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_INVENTORY_NOTICE_NOT_ENOUGH_ITEM));
            return;
        }
        else
        {
            if (Model.ConsumeType == UseItemType.SelectBox)
            {
                RequestUseItemWithReward(Model.GetUseItemInfo());
            }
            else
            {
                RequestUstItemWithCount(Model.GetCurrentCount());
            }

            var backController = UIManager.Instance.GetController(UIManager.Instance.GetBackUIType());

            if (backController != null)
                backController.Refresh();

            Exit().Forget();
        }
    }

    private async void RequestUstItemWithCount(int count)
    {
        BaseProcess consumeItemProcess = NetworkManager.Web.GetProcess(WebProcess.ConsumeItemUse);
        consumeItemProcess.SetPacket(new UseConsumeItemInDto(Model.Item.Id, count));

        if (await consumeItemProcess.OnNetworkAsyncRequest())
        {
            consumeItemProcess.OnNetworkResponse();
            RedDotManager.Instance.Notify();
        }
    }

    private async void RequestUseItemWithReward(SelectedRewardDto[] selectedRewards)
    {
        int totalCount = 0;
        foreach (var parameterItem in selectedRewards)
            totalCount += parameterItem.rewardCount;

        BaseProcess consumeItemProcess = NetworkManager.Web.GetProcess(WebProcess.ConsumeItemUse);
        consumeItemProcess.SetPacket(new UseConsumeItemInDto(Model.Item.Id, totalCount, selectedRewards));

        if (await consumeItemProcess.OnNetworkAsyncRequest())
        {
            consumeItemProcess.OnNetworkResponse();
            RedDotManager.Instance.Notify();
        }
    }
    #endregion Coding rule : Function
}