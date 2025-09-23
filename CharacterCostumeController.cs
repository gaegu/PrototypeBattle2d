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
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterCostumeController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CharacterCostumeView; } }
    public override void SetModel() { SetModel(new CharacterCostumeViewModel()); }
    private CharacterCostumeView View { get { return base.BaseView as CharacterCostumeView; } }
    private CharacterCostumeViewModel Model { get { return GetModel<CharacterCostumeViewModel>(); } }
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
        Model.SetEventSelectCostume(OnEventSelectCostume);
        Model.SetEventArrow(OnEventArrow);
        Model.SetEventApply(OnEventEquip);
        Model.SetEventEquipped(OnEventUnEquip);
        Model.SetEventShortCut(OnEventShortCut);
        Model.SetEventPurchase(OnEventPurchase);
        Model.SetEventApply(OnEventEquip);
        Model.SetEventUnavailable(OnEventUnavailable);

        Model.SetCostumeViewModel(PlayerManager.Instance.MyPlayer.User);
        Model.SelectCostume(Model.TargetCharacter.WearCostumeDataId);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterCostumeView";
    }

    private void OnEventSelectCostume(int dataId)
    {
        Model.SelectCostume(dataId);
        //AdditiverPrefabManager에서 모델링 변경
        View.RefreshAsync().Forget();
        View.SnapCurrentCostume();

        var introduceUnit = AdditivePrefabManager.Instance.IntroduceUnit;
        introduceUnit.Model.OnEventCostume(dataId);
    }

    private void OnEventArrow(bool isNext)
    {
        int selectIndex;
        
        if (isNext)
            selectIndex = Model.CurrentIndex + 1 >= Model.CostumeList.Count ? 0 : Model.CurrentIndex + 1;
        else
            selectIndex = Model.CurrentIndex - 1 <= -1 ? Model.CostumeList.Count - 1 : Model.CurrentIndex - 1;

        OnEventSelectCostume(Model.CostumeList[selectIndex].DataId);
    }

    private void OnEventEquip(int dataId)
    {
        if (dataId > 0)
            RequestEquipCostume(dataId).Forget();
        else
            RequestUnEquipCostume().Forget();
    }

    private void OnEventUnEquip()
    {
        MessageBoxManager.ShowToastMessage("@이미 착용중입니다");
    }

    private void OnEventShortCut(int costumeDataId)
    {
        CostumeTable costumeTable = TableManager.Instance.GetTable<CostumeTable>();
        CostumeTableData costumeData = costumeTable.GetDataByID(costumeDataId);

        switch ((CostumeType)costumeData.GetCOSTUME_TYPE())
        {
            case CostumeType.Reeltape:
                {
                    ContentsOpenManager.Instance.OpenContents(ContentsType.GachaShop).Forget();
                    break;
                }
        }
    }

    private void OnEventPurchase(int costumeDataId)
    {

    }

    private void OnEventUnavailable(int costumeDataId)
    {
        CostumeTable costumeTable = TableManager.Instance.GetTable<CostumeTable>();
        CostumeTableData costumeData = costumeTable.GetDataByID(costumeDataId);

        switch ((CostumeType)costumeData.GetCOSTUME_TYPE())
        {
            //추후 타입별로 메세지가 달라진다면 추가.
            default:
                {
                    MessageBoxManager.ShowToastMessage("@현재 획득이 불가능한 릴테이프입니다."); ;
                    break;
                }
        }
    }

    private async UniTask RequestEquipCostume(int costumeDataId)
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.CostumeEquip);
        process.SetPacket(new EquipCostumeInDto(Model.TargetCharacter.Id, costumeDataId));

        if (await process.OnNetworkAsyncRequest())
        {
            await process.OnNetworkAsyncResponse();
            Model.UpdateCostumeState();
            await View.RefreshAsync();
        }
    }

    private async UniTask RequestUnEquipCostume()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.CostumeUnequip);
        process.SetPacket(new UnequipCostumeInDto(Model.TargetCharacter.Id));

        if (await process.OnNetworkAsyncRequest())
        {
            await process.OnNetworkAsyncResponse();
            Model.UpdateCostumeState();
            await View.RefreshAsync();
        }
    }
    #endregion Coding rule : Function
}