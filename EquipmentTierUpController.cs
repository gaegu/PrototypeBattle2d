//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class EquipmentTierUpController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.EquipmentTierUpPopup; } }
    public override void SetModel() { SetModel(new EquipmentTierUpPopupModel()); }
    private EquipmentTierUpPopup View { get { return base.BaseView as EquipmentTierUpPopup; } }
    private EquipmentTierUpPopupModel Model { get { return GetModel<EquipmentTierUpPopupModel>(); } }
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
    public override void InitializeTestModel(params object[] parameters)
    {
        //계정에 승급 가능한 장비가 있는지 확인하고 하나 집어온다.
        List<Equipment> allEquipment = PlayerManager.Instance.MyPlayer.User.EquipmentModel.GetAllEquipmentList();

        Equipment testEquipment = allEquipment.Find(x =>
            TableManager.Instance.GetTable<EquipmentTable>().GetDataByID(x.DataId).GetTIER_UP_EQUIPMENT() != 0 &&
            x.Level == x.MaxLevel
        );

        if (testEquipment == null)
            IronJade.Debug.LogError("승급 가능한 장비가 계정에 없습니다.");

        Model.SetTargetEquipment(testEquipment);
    }

    public override void Enter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);

        Model.Initialize();
        Model.SetOnEventTierUp(OnEventTierUp);
        Model.SetOnEventClose(OnEventClose);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Equipment/EquipmentTierUpPopup";
    }

    private void OnEventTierUp()
    {
        if (Model.TargetEquipment.EquipmentTier < EquipmentTier.Tier6)
        {
            RequestTierUp().Forget();
            return;
        }

        string title = Model.TierUpTitleText;
        string desc = Model.TierUpBindWarningText;

        System.Action callback = () =>
        {
            if (!Model.CheckCurrency)
            {
                MessageBoxManager.ShowToastMessage(Model.TierUpLackMaterialMessage);
                return;
            }

            RequestTierUp().Forget();
        };

        MessageBoxManager.ShowYesBox(title);
    }

    private void OnEventClose()
    {
        UIManager.Instance.BackToTarget(UIType.EquipmentInfoPopup).Forget();
    }

    private async UniTask RequestTierUp()
    {
        EquipmentTierUpProcess process = NetworkManager.Web.GetProcess<EquipmentTierUpProcess>();
        process.Request.SetInDto(Model.TargetEquipment.Id);

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();

            string tierUpResultMessage = process.Response.data.isSuccess ? Model.TierUpSuccessMessage : Model.TierUpFailMessage;
            MessageBoxManager.ShowToastMessage(tierUpResultMessage);

            if (process.Response.data.isSuccess)
            {
                UIManager.Instance.BackToTarget(UIType.EquipmentInfoPopup).Forget();

                OnEventClose();
            }
            else
            {
                Model.UpdateProbability();
                View.UpdateUI();
            }
        }
    }
    #endregion Coding rule : Function
}