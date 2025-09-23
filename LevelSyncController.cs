//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using System;
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class LevelSyncController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.LevelSyncView; } }
    public override void SetModel() { SetModel(new LevelSyncViewModel()); }
    private LevelSyncView View { get { return base.BaseView as LevelSyncView; } }
    private LevelSyncViewModel Model { get { return GetModel<LevelSyncViewModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool istest = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetEventLevelUp(OnEventLevelUp);
        Model.SetEventLevelSyncSlotUnit(OnEventClickSlotUnit);
        Model.SetMainCharacterSlots();
        Model.SetLevelSyncLevel();
        Model.SetLeveSyncSlotCount();
    }

    public override async UniTask LoadingProcess()
    {
        await RequestGet();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async void Refresh()
    {
        Model.SetMainCharacterSlots();
        Model.SetLevelSyncLevel();
        await View.RefreshAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "LevelSync/LevelSyncView";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        istest = true;
    }

    public async UniTask OnEventLevelUp(int index)
    {
        Character character = Model.GetMainCharacterInfo(index);

        if (character == null)
            return;

        BaseController characterLevelUpController = UIManager.Instance.GetController(UIType.CharacterLevelUpPopup);
        CharacterLevelUpPopupModel model = characterLevelUpController.GetModel<CharacterLevelUpPopupModel>();

        model.SetTargetCharacter(character);
        model.SetEventExitAsync(() =>
        {
            RequestGet().Forget();
            Refresh();
        });

        await UIManager.Instance.EnterAsync(characterLevelUpController);
    }


    public async void OnEventClickSlotUnit(LevelSyncSlotUnitModel model)
    {
        Model.SetSelectedSlotId(model.SlotIndex);
        Model.SetSelectedCharacterId(model.CharacterId);

        switch (model.CurrentState)
        {
            case LevelSyncSlotUnitModel.StateLevelSyncSlot.Lock:
                {
                    MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MIGRATION_NOTICE_NEED_CONDITION);
                    break;
                }
            case LevelSyncSlotUnitModel.StateLevelSyncSlot.Registered:
                {
                    BaseController levelSyncMessageController = UIManager.Instance.GetController(UIType.LevelSyncMessagePopup);
                    LevelSyncMessagePopupModel popupModel = levelSyncMessageController.GetModel<LevelSyncMessagePopupModel>();

                    popupModel.SetMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MIGRATION_NOTICE_SLOT_CLEAR);
                    popupModel.SetCharacter(Model.CharacterModel.GetGoodsById(model.CharacterId));
                    popupModel.SetState(model.CurrentState);
                    popupModel.SetSyncLevel(Model.SyncLevel);
                    popupModel.SetEventFinishedConfirm(async () =>
                    {
                        await UnregisterProcess(model.SlotIndex);
                        await RequestGet();
                        Refresh();
                    });

                    UIManager.Instance.EnterAsync(levelSyncMessageController).Forget();
                    break;
                }
            case LevelSyncSlotUnitModel.StateLevelSyncSlot.Opened:
                {
                    BaseController levelSyncSelectController = UIManager.Instance.GetController(UIType.CharacterSelectPopup);
                    CharacterSelectPopupModel popupModel = levelSyncSelectController.GetModel<CharacterSelectPopupModel>();

                    popupModel.SetExceptedCharacterList(Model.GetNotSelectableCharacters());
                    popupModel.SetEventConfirmFinished(EnterSelectedPopup);
                    UIManager.Instance.EnterAsync(levelSyncSelectController).Forget();

                    break;
                }
            case LevelSyncSlotUnitModel.StateLevelSyncSlot.Cooltime:
                {
                    User user = PlayerManager.Instance.MyPlayer.User;
                    CurrencyUnitModel currencyModel = Model.GetCoolTimeCurrencyUnitModels(user, model.GetCoolTime());
                    string description = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_MIGRATION_NOTICE_UNLOCK);

                    MessageBoxManager.ShowCurrencyMessageBox(description, currencyModel, true, async () =>
                    {
                        if (PlayerManager.Instance.MyPlayer.User.CurrencyModel.CheckCompareByInt(CompareType.GreaterEqual, currencyModel.CurrencyType, currencyModel.Value))
                        {
                            await CooltimeSlotProcess();
                            model.SetState(LevelSyncSlotUnitModel.StateLevelSyncSlot.Opened);
                            await RequestGet();
                            Refresh();
                        }
                        else
                        {
                            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_LEVELSYNCVIEW_COOLTIME_NOT_ENOUGH_MATERIAL);
                        }
                    });
                    break;
                }
            case LevelSyncSlotUnitModel.StateLevelSyncSlot.NextOpen:
                {
                    User user = PlayerManager.Instance.MyPlayer.User;
                    CurrencyUnitModel currencyModel = Model.GetOpenSlotCurrencyUnitModels(user);
                    string description = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_MIGRATION_NOTICE_SLOT_OPEN);

                    MessageBoxManager.ShowCurrencyMessageBox(description, currencyModel, true, async () =>
                    {
                        if (PlayerManager.Instance.MyPlayer.User.CurrencyModel.CheckCompareByInt(CompareType.GreaterEqual, currencyModel.CurrencyType, currencyModel.Value))
                        {
                            await RequestOpenSlot();
                            model.SetState(LevelSyncSlotUnitModel.StateLevelSyncSlot.Opened);
                            await RequestGet();
                            Refresh();
                        }
                        else
                        {
                            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_LEVELSYNCVIEW_NEXTOPEN_NOT_ENOUGH_MATERIAL);
                        }
                    });
                    break;
                }
        }
    }

    private async UniTask RequestGet()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.LevelSyncGet);

        if (await process.OnNetworkAsyncRequest())
            process.OnNetworkResponse(Model);
        
    }

    private async UniTask RequestOpenSlot()
    {
        BaseProcess levelSyncOpenProcess = NetworkManager.Web.GetProcess(WebProcess.LevelSyncOpen);

        if (await levelSyncOpenProcess.OnNetworkAsyncRequest())
        {
            levelSyncOpenProcess.OnNetworkResponse(Model);
            View.RefreshHaveCurrency();
        }
    }

    private async UniTask UnregisterProcess(int slotIndex)
    {
        await RequestUnregister(slotIndex);

        await UIManager.Instance.BackToTarget(UIType.LevelSyncView);
    }

    private async UniTask RequestUnregister(int levelSyncId)
    {
        BaseProcess unregisterProcess = NetworkManager.Web.GetProcess(WebProcess.LevelSyncUnregister);
        unregisterProcess.SetPacket(new UnregisterLevelSyncInDto(levelSyncId));

        if(await unregisterProcess.OnNetworkAsyncRequest())
            unregisterProcess.OnNetworkResponse(Model);
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await base.Exit(onEventExtra);
    }

    private void EnterSelectedPopup(BaseThumbnailUnitModel model)
    {
        if (model == null)
            return;

        BaseController popup = UIManager.Instance.GetController(UIType.LevelSyncMessagePopup);
        LevelSyncMessagePopupModel popupModel = popup.GetModel<LevelSyncMessagePopupModel>();
        popupModel.SetMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_MIGRATION_NOTICE_REGIST);
        popupModel.SetCharacter(Model.CharacterModel.GetGoodsById(model.Id));
        popupModel.SetState(LevelSyncSlotUnitModel.StateLevelSyncSlot.Opened);
        popupModel.SetSyncLevel(Model.SyncLevel);
        popupModel.SetEventFinishedConfirm(async() =>
        {
            UIManager.Instance.Exit(UIType.CharacterSelectPopup).Forget();

            RegisterProcess(model.Id).Forget();
            await RequestGet();
            Refresh();
        });
        UIManager.Instance.EnterAsync(popup).Forget();
    }

    private async UniTask RegisterProcess(int id)
    {
        await RequestRegister(Model.SelectedSlotId, id);

        await UIManager.Instance.BackToTarget(UIType.LevelSyncView);
    }

    private async UniTask RequestRegister(int levelSyncId, int characterId)
    {
        BaseProcess levelSyncRegisterProcess = NetworkManager.Web.GetProcess(WebProcess.LevelSyncRegister);
        RegisterLevelSyncInDto registerLevelSyncInDto = new RegisterLevelSyncInDto(levelSyncId, characterId);
        levelSyncRegisterProcess.SetPacket(registerLevelSyncInDto);

        if(await levelSyncRegisterProcess.OnNetworkAsyncRequest())
            levelSyncRegisterProcess.OnNetworkResponse(Model);
    }

    private async UniTask CooltimeSlotProcess()
    {
        await RequestCoolTime(Model.SelectedSlotId);
        View.RefreshHaveCurrency();
    }

    private async UniTask RequestCoolTime(int slotId)
    {
        BaseProcess levelSyncResetCooltimeProcess = NetworkManager.Web.GetProcess(WebProcess.LevelSyncResetCooltime);
        levelSyncResetCooltimeProcess.SetPacket(new ResetCooltimeLevelSyncInDto(slotId));

        if (await levelSyncResetCooltimeProcess.OnNetworkAsyncRequest())
            levelSyncResetCooltimeProcess.OnNetworkResponse();
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.LevelSyncOpenSlot:
                {
                    var unit = View.GetFirstSlotUnit(LevelSyncSlotUnitModel.StateLevelSyncSlot.Opened);
                    if (unit != null)
                        return unit.gameObject;
                    else
                        return null;
                }

            case TutorialExplain.LevelSyncUnlockSlot:
                {
                    var unit = View.GetFirstSlotUnit(LevelSyncSlotUnitModel.StateLevelSyncSlot.NextOpen);
                    if (unit != null)
                        return unit.gameObject;
                    else
                        return null;
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }

    }
    #endregion Coding rule : Function
}