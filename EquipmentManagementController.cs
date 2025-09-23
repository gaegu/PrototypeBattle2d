//=========================================================================================================
#pragma warning disable CS1998
using System;
using Cysharp.Threading.Tasks;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;
using UnityEngine;

public class EquipmentManagementController : BaseController
{
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.EquipmentManagementPopup; } }
    private EquipmentManagementPopup View { get { return base.BaseView as EquipmentManagementPopup; } }
    protected EquipmentManagementPopupModel Model { get; private set; }
    public EquipmentManagementController() { Model = GetModel<EquipmentManagementPopupModel>(); }
    public EquipmentManagementController(BaseModel baseModel) : base(baseModel) { }

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventSelectEquipment(OnEventSelectEquipment);
        Model.SetEventSelectCharacter(OnEventSelectCharacter);
        Model.SetEventEquip(OnEventEquip);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetSortingModel();
        Model.SetSelectEquipment(null);
        Model.SetEquipmentUnitModelList();
        Model.SetEquipmentDetailUnitModel();
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Equipment/EquipmentManagementPopup";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        Character leaderCharacter = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetLeaderCharacter();
        EquipmentType equipmentType = (EquipmentType)1;
        Equipment equipment = leaderCharacter.GetEquipment(equipmentType);

        Model.SetCharacterId(leaderCharacter.Id);
        Model.SetCharacter(PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsById(leaderCharacter.Id));
        Model.SetEquipmentType(equipmentType);
        Model.SetTargetEquipment(equipment);
    }

    private void OnEventSelectEquipment(Equipment equipment)
    {
        if (Model.SelectEquipment == null || Model.SelectEquipment.Id != equipment.Id)
        {
            Model.SetSelectEquipment(equipment);
            Model.EquipmentDetailUnitModel.UpdateSelectEquipment(equipment);
        }
        else
        {
            Model.SetSelectEquipment(null);
            Model.EquipmentDetailUnitModel.UpdateSelectEquipment(null);
        }

        Model.UpdateEquipmentList();
        View.RefreshAsync().Forget();
    }

    private void OnEventSelectCharacter(Character character)
    {
        if (Model.SelectCharacter == null || Model.SelectCharacter.Id != character.Id)
        {
            Model.SetSelectCharacter(character);
            Model.SetCharacterId(character.Id);

            // 없으면 null 할당
            Equipment equipment = character.GetEquipment(Model.TargetEquipment.Type);

            Model.EquipmentDetailUnitModel.SetWearEquipment(equipment);
            Model.EquipmentDetailUnitModel.UpdateSelectEquipment(Model.TargetEquipment);
        }
        else
        {
            // 해제
            Model.SetSelectCharacter(null);
            Model.SetSelectEquipment(null);

            Model.EquipmentDetailUnitModel.SetWearEquipment(Model.TargetEquipment);
            Model.EquipmentDetailUnitModel.UpdateSelectEquipment(null);
        }

        Model.UpdateEquipmentList();
        View.RefreshAsync().Forget();
    }

    private async void OnEventEquip()
    {
        Equipment equipEquipment = null;

        switch (Model.ManagementType)
        {
            case EquipmentManagementPopupModel.EquipmentManagementType.EquipToCharacter:
                equipEquipment = Model.TargetEquipment;
                break;

            case EquipmentManagementPopupModel.EquipmentManagementType.ReplaceCharacterEquipment:
                equipEquipment = Model.SelectEquipment;
                break;

            default:
                break;
        }


        if (equipEquipment == null)
        {
            IronJade.Debug.LogError($"장착하려는 장비가 NULL이다.");
            return;
        }

        if ((Model.TargetCharacter == null && Model.SelectCharacter == null) || Model.CharacterId == 0 || Model.CharacterId.CheckSafeNull())
        {
            string toastMessage = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_TOASTMESSAGE_NO_SELECT_CHARACTER);
            MessageBoxManager.ShowToastMessage(toastMessage);
            return;
        }

        if (Model.TargetEquipment != null && Model.TargetEquipment.EquipmentTier >= EquipmentTier.Tier10)
        {
            await MessageBoxManager.ShowYesNoBox(Model.EquipmentDestroyWaringMessage, timer: 5, onEventConfirm: () =>
            {
                RequestEquipmentEquip(equipEquipment, Model.TargetEquipment).Forget();
            });
            return;
        }

        RequestEquipmentEquip(equipEquipment).Forget();
    }

    private async UniTask RequestEquipmentEquip(Equipment equipEquipment, Equipment removeEquipment = null)
    {
        BaseProcess characterEquipmentEquipProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterEquipmentEquip);
        int characterId = Model.CharacterId;                                    // 착용할 캐릭터

        bool existPrevEquipment = Model.EquipmentDetailUnitModel.WearEquipment != null;

        characterEquipmentEquipProcess.SetPacket(new EquipEquipmentInDto(characterId, new int[] { equipEquipment.Id }));

        if (await characterEquipmentEquipProcess.OnNetworkAsyncRequest())
        {
            CharacterModel characterModel = PlayerManager.Instance.MyPlayer.User.CharacterModel;
            characterModel.ChangeEquipmentById(equipEquipment.WearCharacterId, equipEquipment.Type, null);

            characterEquipmentEquipProcess.OnNetworkResponse();

            string toastMessage = null;

            //장착했던 장비가 신화 등급이면 제거 (서버에서 따로 안알려줌)
            if (removeEquipment != null)
            {
                toastMessage = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_MSG_DESTORYED);
                PlayerManager.Instance.MyPlayer.User.EquipmentModel.RemoveGoods(removeEquipment);
            }
            else
            {

                toastMessage = existPrevEquipment ?
                    TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_MSG_REPLACED) :
                    TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_MSG_EQUIPPED);
            }

            BaseController equipmentChangeController = UIManager.Instance.GetController(UIType.EquipmentInfoPopup);
            EquipmentInfoPopupModel model = equipmentChangeController.GetModel<EquipmentInfoPopupModel>();

            model.SetShowInfoType(EquipmentInfoPopupModel.InfoType.Default);
            model.SetCharacterId(Model.CharacterId);
            model.SetEquipment(equipEquipment);
            model.SetUser(PlayerManager.Instance.MyPlayer.User);

            SoundManager.SfxFmod.Play(StringDefine.FMOD_EVENT_UI_MENU_SFX, StringDefine.FMOD_DEFAULT_PARAMETER, 24);

            UIManager.Instance.RefreshAllUI();

            await Exit();

            if (!string.IsNullOrEmpty(toastMessage))
                MessageBoxManager.ShowToastMessage(toastMessage);
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.EquipmentManagementTargetItem:
                {
                    return View.GetEquipmentScrollItem(0).TutorialRect;
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }

    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.EquipmentManagementTargetItem:
                {
                    EquipmentEquipUnit unit = View.GetEquipmentScrollItem(0);
                    unit.OnClickEquipment();
                    break;
                }

            case TutorialExplain.EquipmentManagementEquip:
                {
                    OnEventEquip();
                    await TutorialManager.WaitUntilEnterUI(UIType.CharacterDetailView);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}