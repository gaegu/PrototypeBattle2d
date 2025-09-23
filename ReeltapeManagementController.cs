//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine.TextCore.Text;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ReeltapeManagementController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ReeltapeManagementPopup; } }
    public override void SetModel() { SetModel(new ReeltapeManagementPopupModel()); }
    private ReeltapeManagementPopup View { get { return base.BaseView as ReeltapeManagementPopup; } }
    private ReeltapeManagementPopupModel Model { get { return GetModel<ReeltapeManagementPopupModel>(); } }
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
        Model.SetEventSelect(OnEventSelectChip);
        Model.SetEventEquip(OnEventEquip);
        Model.SetEventUnEquip(OnEventUnEquip);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetReeltapePopupModel(PlayerManager.Instance.MyPlayer.User);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }
    
    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(Func<UISubState, UniTask> onEventExtra = null)
    {
        var backController = UIManager.Instance.GetController(UIManager.Instance.GetBackUIType());

        if (backController != null)
            backController.Refresh();

        Model.ClearReeltapeModel();

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Reeltape/ReeltapeManagementPopup";
    }

    private ThumbnailSelectType OnEventSelectChip(BaseThumbnailUnitModel model)
    {
        if (Model.ChooseType == ReeltapeManagementPopupModel.ListType.Character)
        {
            var characterModel = (model as ThumbnailCharacterUnitModel);
            Model.SetTargetCharacter(characterModel.Character);
            Model.UpdateSelectCharacter();
        }
        else
        {
            var reeltapeModel = (model as ThumbnailReeltapeUnitModel);
            Model.SetSelectReeltape(reeltapeModel.Reeltape);
            Model.UpdateSelectReeltape();
        }

        View.RefreshAsync().Forget();

        return ThumbnailSelectType.None;
    }

    private void OnEventEquip()
    {
        if (Model.SelectReeltape != null)
        {
            System.Action onEventConfirm = () => { RequestEquip(Model.TargetCharacter.Id, Model.SelectReeltape.Id).Forget(); };
            if (Model.SelectReeltape.WearCharacterId > 0)
            {
                MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_REELTAPEMANAGEMENTPOPUP_ALREADY_EQUIPPED, onEventConfirm: onEventConfirm).Forget();
            }
            else
            {
                onEventConfirm();
            }
        }
        else
        {
            if (Model.EquippedReeltape != null)
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_REELTAPEMANAGEMENTPOPUP_SELECT_REELTAPE_REPLACE);
            else
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_REELTAPEMANAGEMENTPOPUP_SELECT_REELTAPE_EQUIP);

        }
    }

    private void OnEventUnEquip()
    {
        RequestUnEquip().Forget();
    }

    private async UniTask RequestEquip(int characterId, int reeltapeId)
    {
        Func<bool, Task> onEventEquip = async (isChangeCostume) =>
        {
            BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.ReeltapeEquip);
            process.SetPacket(new EquipReeltapeInDto(characterId, reeltapeId));
            if (await process.OnNetworkAsyncRequest())
            {
                process.OnNetworkResponse();

                if (isChangeCostume)
                {
                    await RequestChangeCostume(characterId);
                }

                Exit().Forget();
            }
        };

        ReeltapeTable reeltapeTable = TableManager.Instance.GetTable<ReeltapeTable>();
        ReeltapeTableData reeltapeData = reeltapeTable.GetDataByID(Model.SelectReeltape.DataId);
        bool isLinkedCharacter = reeltapeData.GetLINKED_CHARACTER() == Model.TargetCharacter.DataId;

        if (isLinkedCharacter)
        {

            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_REELTAPEMANAGEMENTPOPUP_CONFIRM_APPEARANCE_CHANGE, Model.TargetCharacter.Name);
            string confirmButton = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_REELTAPEMANAGEMENTPOPUP_CHANGE_APPEARANCE);
            string cancelButton = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_REELTAPEMANAGEMENTPOPUP_KEEP_APPEARANCE);

            await MessageBoxManager.ShowYesNoBox(message, confirmButton, cancelButton, onEventConfirm: () => { onEventEquip?.Invoke(true); }, onEventCancel: () => { onEventEquip?.Invoke(false); });
        }
        else
        {
            onEventEquip?.Invoke(false);
        }
    }

    private async UniTask RequestUnEquip()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.ReeltapeUnEquip);
        process.SetPacket(new UnequipReeltapeInDto(Model.EquippedReeltape.WearCharacterId, Model.EquippedReeltape.Id));

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();
            await RequestUnequipCostume(Model.TargetCharacter.Id);
            Exit().Forget();
        }
    }

    private async UniTask RequestChangeCostume(int characterId)
    {
        CostumeTable costumeTable = TableManager.Instance.GetTable<CostumeTable>();
        CostumeTableData costumeData = costumeTable.Find(x => x.GetREELTAPE() == Model.SelectReeltape.DataId);

        BaseProcess equipCostumeProcess = NetworkManager.Web.GetProcess(WebProcess.CostumeEquip);
        equipCostumeProcess.SetPacket(new EquipCostumeInDto(characterId, costumeData.GetID()));

        if (await equipCostumeProcess.OnNetworkAsyncRequest())
        {
            await equipCostumeProcess.OnNetworkAsyncResponse();
            await AdditivePrefabManager.Instance.SignatureUnit.RefreshByCostume();
        }
    }

    private async UniTask RequestUnequipCostume(int characterId)
    {
        CostumeTable costumeTable = TableManager.Instance.GetTable<CostumeTable>();
        CostumeTableData costumeData = costumeTable.Find(x => x.GetREELTAPE() == Model.EquippedReeltape.DataId);

        int costumeDataId = Model.TargetCharacter.WearCostumeDataId;
        if (costumeDataId != costumeData.GetID())
            return;

        BaseProcess equipCostumeProcess = NetworkManager.Web.GetProcess(WebProcess.CostumeUnequip);
        equipCostumeProcess.SetPacket(new UnequipCostumeInDto(characterId));

        if (await equipCostumeProcess.OnNetworkAsyncRequest())
        {
            await equipCostumeProcess.OnNetworkAsyncResponse();
            await AdditivePrefabManager.Instance.SignatureUnit.RefreshByCostume();
        }
    }
    #endregion Coding rule : Function
}