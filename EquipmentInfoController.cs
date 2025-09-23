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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등) 
//=========================================================================================================

public class EquipmentInfoController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.EquipmentInfoPopup; } }
    private EquipmentInfoPopup View { get { return base.BaseView as EquipmentInfoPopup; } }
    protected EquipmentInfoPopupModel Model { get; private set; }
    public EquipmentInfoController() { Model = GetModel<EquipmentInfoPopupModel>(); }
    public EquipmentInfoController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventUnequip(OnEventUnequip);
        Model.SetEventEquipToCharacter(OnEventEquipToCharacter);
        Model.SetEventReplaceEquippedEquipment(OnEventReplaceEquippedEquipment);
        Model.SetEventLevelUp(OnEventLevelUp);
        Model.SetEventTierUp(OnEventTierUp);
        Model.SetEventClose(OnEventClose);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    public override void Refresh()
    {
        Equipment equipment = PlayerManager.Instance.MyPlayer.User.EquipmentModel.GetGoodsById(Model.TargetEquipment.Id);

        Model.SetEquipment(equipment);

        View.ShowAsync().Forget();

        if (UIManager.Instance.CheckOpenUI(UIType.InventoryView))
        {
            var inventoryController = UIManager.Instance.GetController(UIType.InventoryView);

            if (inventoryController != null)
                inventoryController.Refresh();
        }
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Equipment/EquipmentInfoPopup";
    }



    private void OnEventUnequip()
    {
        if (Model.TargetEquipment.EquipmentTier >= EquipmentTier.Tier10)
        {
            MessageBoxManager.ShowToastMessage(Model.CannotUnEquipMessage);
            return;
        }

        RequestCharacterEquipmentUnEquip().Forget();
    }

    /// <summary>
    /// 해당 장비를 다른 캐릭터에 장착 (인벤 진입)
    /// </summary>
    private void OnEventEquipToCharacter()
    {
        var sameClassCharacters = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetCharactersByClassType(Model.TargetEquipment.ClassType);

        if (Model.TargetEquipment.Type == EquipmentType.SlotHeadKnuckle)
        {
            if (!PlayerManager.Instance.MyPlayer.User.CharacterModel.CheckGoodsByDataId(Model.TargetEquipment.LinkedCharacterDataId))
            {
                MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_MSG_NO_CHARACTER_TO_EQUIP));
                return;
            }
        }
        else if (sameClassCharacters == null || sameClassCharacters.Count == 0)
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_MSG_NO_CHARACTER_TO_EQUIP));
            return;
        }

        BaseController equipmentEquipController = UIManager.Instance.GetController(UIType.EquipmentManagementPopup);
        EquipmentManagementPopupModel model = equipmentEquipController.GetModel<EquipmentManagementPopupModel>();

        model.SetEquipmentManagementType(EquipmentManagementPopupModel.EquipmentManagementType.EquipToCharacter);
        model.SetTargetEquipment(Model.TargetEquipment);
        model.SetEquipmentType(Model.EquipmentType);

        UIManager.Instance.EnterAsync(equipmentEquipController).Forget();
    }

    /// <summary>
    /// 캐릭터가 착용한 장비 교체 (커넥트 진입)
    /// </summary>
    private void OnEventReplaceEquippedEquipment()
    {
        var sameTypeEquipments = PlayerManager.Instance.MyPlayer.User.EquipmentModel.GetEquipmentsByType(Model.EquipmentType);

        if (sameTypeEquipments == null || sameTypeEquipments.Count == 0)
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_EQUIPMENTMANAGEMENTPOPUP_NOTFOUND_EQUIPMENT));
            return;
        }

        BaseController equipmentEquipController = UIManager.Instance.GetController(UIType.EquipmentManagementPopup);
        EquipmentManagementPopupModel model = equipmentEquipController.GetModel<EquipmentManagementPopupModel>();

        model.SetEquipmentManagementType(EquipmentManagementPopupModel.EquipmentManagementType.ReplaceCharacterEquipment);
        model.SetCharacterId(Model.CharacterId);
        model.SetCharacter(PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsById(model.CharacterId));
        model.SetTargetEquipment(Model.TargetEquipment);
        model.SetEquipmentType(Model.EquipmentType);

        UIManager.Instance.EnterAsync(equipmentEquipController).Forget();
    }

    private void OnEventLevelUp()
    {
        if (Model.CheckMaxLevel())
            return;

        BaseController equipmentLevelUpController = UIManager.Instance.GetController(UIType.EquipmentLevelUpPopup);
        EquipmentLevelUpPopupModel model = equipmentLevelUpController.GetModel<EquipmentLevelUpPopupModel>();
        model.SetTargetEquipment(Model.TargetEquipment);

        UIManager.Instance.EnterAsync(equipmentLevelUpController).Forget();
    }

    private void OnEventTierUp()
    {
        if (!Model.CheckMaxLevel())
            return;

        BaseController equipmentTierUpController = UIManager.Instance.GetController(UIType.EquipmentTierUpPopup);
        EquipmentTierUpPopupModel model = equipmentTierUpController.GetModel<EquipmentTierUpPopupModel>();
        model.SetTargetEquipment(Model.TargetEquipment);

        UIManager.Instance.EnterAsync(equipmentTierUpController).Forget();
    }

    private async UniTask RequestCharacterEquipmentUnEquip()
    {
        BaseProcess characterEquipmentUnequipProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterEquipmentUnequip);

        characterEquipmentUnequipProcess.SetPacket(new UnequipEquipmentInDto(Model.CharacterId, new int[] { Model.TargetEquipment.Id }));

        if (await characterEquipmentUnequipProcess.OnNetworkAsyncRequest())
        {
            characterEquipmentUnequipProcess.OnNetworkResponse();

            MessageBoxManager.ShowToastMessage(Model.UnEquipMessage);

            var backController = UIManager.Instance.GetController(UIManager.Instance.GetBackUIType());

            if (backController != null)
                backController.Refresh();

            SoundManager.SfxFmod.Play(StringDefine.FMOD_EVENT_UI_MENU_SFX, StringDefine.FMOD_DEFAULT_PARAMETER, 25);

            await Exit();
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain explainType = (TutorialExplain)type;

        switch (explainType)
        {
            case TutorialExplain.EquipmentManagementEquip:
                {
                    OnEventLevelUp();
                    await UIManager.Instance.EnterAsync(UIType.EquipmentLevelUpPopup);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}