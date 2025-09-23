//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class SettingController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.SettingWindow; } }
    private SettingView View { get { return base.BaseView as SettingView; } }
    protected SettingViewModel Model { get; private set; }
    public SettingController() { Model = GetModel<SettingViewModel>(); }
    public SettingController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetCurrentTab(SettingTabType.Main);
        Model.SetOnClickTab((value) =>
        {
            OnEventChangeTab(value).Forget();
        });
        Model.SetOnEventChangeSetting(OnEventChangeSetting);

        LoadAccount();
        LoadAlarm();
        LoadLanguage();
        //LoadBattle();
        LoadGraphic();
        LoadSound();
        LoadInformation();
        LoadCoupon();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (Model.CurrentTab == SettingTabType.Main)
        {
            return await UIManager.Instance.Exit(this, onEventExtra);
        }
        else if (Model.CurrentTab == SettingTabType.Information)
        {
            CheckSubTab();
        }
        else
        {
            CheckOptionChanged();
        }
        return true;
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
        return StringDefine.PATH_PREFAB_UI_WINDOWS + "Common/SettingWindow";
    }

    private void LoadAccount()
    {
        if (PrologueManager.Instance.IsProgressing)
            return;

        SettingAccountUnitModel accountUnitModel = new SettingAccountUnitModel();
        accountUnitModel.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetAccountUnitModel(accountUnitModel);
    }

    private void LoadAlarm()
    {
        SettingAlarmUnitModel alarmUnitModel = new SettingAlarmUnitModel();
        alarmUnitModel.SetAlarmOption(GameSettingManager.Instance.GameSetting);
        alarmUnitModel.SetOnEventApplyOption(OnEventAlarmApplyOption);
        Model.SetAlarmUnitModel(alarmUnitModel);
    }

    private void LoadLanguage()
    {
        SettingLanguageUnitModel languageUnitModel = new SettingLanguageUnitModel();
        languageUnitModel.SetLanguageOption(GameSettingManager.Instance.GameSetting);
        languageUnitModel.SetOnEventApplyOption(OnEventLanguageApplyOption);
        Model.SetLanguageUnitModel(languageUnitModel);
    }

    private void LoadBattle()
    {
        SettingBattleUnitModel battleUnitModel = new SettingBattleUnitModel();
        battleUnitModel.SetBattleOption(GameSettingManager.Instance.GameSetting);
        battleUnitModel.SetOnEventApplyOption(OnEventBattleApplyOption);
        Model.SetBattleUnitModel(battleUnitModel);
    }

    private void LoadGraphic()
    {
        SettingGraphicUnitModel graphicUnitModel = new SettingGraphicUnitModel();
        graphicUnitModel.SetGameGraphicSettingModel(GameSettingManager.Instance.GraphicSettingModel.OptionData);
        Model.SetGraphicUnitModel(graphicUnitModel);
    }

    private void LoadSound()
    {
        SettingSoundUnitModel soundUnitModel = new SettingSoundUnitModel();

        var soundSeetingModel = GameSettingManager.Instance.SoundSettingModel;
        soundUnitModel.SetSoundOption(soundSeetingModel);
        Model.SetSoundUnitModel(soundUnitModel);
    }

    private void LoadInformation()
    {
        SettingInformationUnitModel informationUnitModel = new SettingInformationUnitModel();
        informationUnitModel.SettingTabType(SettingInformationTabType.Information);
        Model.SetInformationUnitModel(informationUnitModel);
    }

    private void LoadCoupon()
    {
        SettingCouponUnitModel couponUnitModel = new SettingCouponUnitModel();
        Model.SetCouponUnitModel(couponUnitModel);
    }

    private void CheckOptionChanged()
    {
        switch (Model.CurrentTab)
        {
            case SettingTabType.Main:
            case SettingTabType.Account:
            case SettingTabType.Coupon:
                ChangeTab((int)SettingTabType.Main);
                break;
            case SettingTabType.Alarm:
                {
                    if (Model.AlarmUnitModel.IsOptionChanged)
                    {
                        ShowWarningMesssageBox(Model.AlarmUnitModel.OnEventApplyOption, null);
                    }
                    else
                    {
                        ChangeTab((int)SettingTabType.Main);
                    }
                }
                break;
            case SettingTabType.Language:
                {
                    if (Model.LanguageUnitModel.IsOptionChanged)
                    {
                        OnEventLanguageApplyOption();
                    }
                    else
                    {
                        ChangeTab((int)SettingTabType.Main);
                    }
                }
                break;
            case SettingTabType.Battle:
                {
                    if (Model.BattleUnitModel.IsOptionChanged)
                    {
                        ShowWarningMesssageBox(Model.BattleUnitModel.OnEventApplyOption, null);
                    }
                    else
                    {
                        ChangeTab((int)SettingTabType.Main);
                    }
                }
                break;
            case SettingTabType.Graphic:
                {
                    if (Model.GraphicUnitModel.IsDataChange())
                        ShowWarningMesssageBox(Model.GraphicUnitModel.OnEventSaveBack, Model.GraphicUnitModel.OnEventNotSaveBack);
                    else
                    {
                        Model.GraphicUnitModel.OnEventApplyGraphicOption.Invoke();
                        ChangeTab((int)SettingTabType.Main);
                    }
                }
                break;
            case SettingTabType.Sound:
                {
                    Model.SoundUnitModel.OnEventIsOptionChaged.Invoke();
                    if (Model.SoundUnitModel.IsOptionChanged)
                    {
                        ShowWarningMesssageBox(Model.SoundUnitModel.OnEventSaveBack, Model.SoundUnitModel.OnEventNotSaveBack);
                    }
                    else
                    {
                        ChangeTab((int)SettingTabType.Main);
                    }
                }
                break;
            default:
                ChangeTab((int)SettingTabType.Main);
                break;
        }
    }

    private void CheckSubTab()
    {
        if (Model.InformationUnitModel.CurrentTab == SettingInformationTabType.Information)
        {
            ChangeTab((int)SettingTabType.Main);
        }
        else
        {
            View.BackToInformationTab().Forget();
        }
    }

    private void ShowWarningMesssageBox(System.Action onEventApply, System.Action onEventCancel)
    {
        MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_SETTING_POPUP_DESC_1,
                                       LocalizationDefine.LOCALIZATION_UI_LABEL_SETTING_POPUP_DESC_2,
                                       LocalizationDefine.LOCALIZATION_UI_LABEL_SETTING_POPUP_DESC_3,
                                       onEventConfirm: () =>
                                       {
                                           onEventApply?.Invoke();
                                           ChangeTab((int)SettingTabType.Main);
                                       },
                                       onEventCancel: () =>
                                       {
                                           onEventCancel?.Invoke();
                                           ChangeTab((int)SettingTabType.Main);
                                       }).Forget();
    }

    private async UniTask OnEventChangeTab(int index)
    {
        await Task.Delay(200);
        ChangeTab(index);
    }

    private void ChangeTab(int index)
    {
        SettingTabType type = (SettingTabType)index;
        if (Model.CurrentTab == type)
            return;

        Model.SetCurrentTab(type);
        View.ShowCurrentTab();

        switch (type)
        {
            case SettingTabType.Account:
                View.ShowAccount().Forget();
                break;
            case SettingTabType.Alarm:
                View.ShowAlarm().Forget();
                break;
            case SettingTabType.Language:
                View.ShowLanguage().Forget();
                break;
            case SettingTabType.Battle:
                View.ShowBattle().Forget();
                break;
            case SettingTabType.Graphic:
                View.ShowGraphic().Forget();
                break;
            case SettingTabType.Sound:
                View.ShowSound().Forget();
                break;
            case SettingTabType.Information:
                View.ShowInformation().Forget();
                break;
            case SettingTabType.Coupon:
                View.ShowCoupon().Forget();
                break;
            default:
                break;
        }
    }

    private void OnEventChangeSetting()
    {
        ChangeTab((int)Model.CurrentTab);
    }

    private void OnEventAlarmApplyOption()
    {
        View.ApplyAlarmOption().Forget();
        Model.OnEventChangeSetting();
    }

    private void OnEventLanguageApplyOption()
    {
        string localizationKey = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_SETTING_POPUP_DESC_4);
        MessageBoxManager.ShowYesNoBox(localizationKey,
            onEventConfirm: async () =>
            {
                await View.ApplyLanguageOption();
                Model.OnEventChangeSetting();
                Model.SetCurrentTab(SettingTabType.Main);

                // 로고 씬으로 이동한다.
                FlowManager.Instance.ChangeFlow(FlowType.LogoFlow, isStack: false).Forget();
            },
            onEventCancel :() =>
            {
                View.ResetLanguageOption().Forget();
                ChangeTab((int)SettingTabType.Main);
            }, 
            onEventClose: () =>
            {
                View.ResetLanguageOption().Forget();
                ChangeTab((int)SettingTabType.Main);
            }).Forget();

    }

    private void OnEventBattleApplyOption()
    {
        View.ApplyBattleOption().Forget();
        Model.OnEventChangeSetting();
    }

    private void OnEventGraphicApplyOption()
    {
        View.ApplyGraphicOption().Forget();
        Model.OnEventChangeSetting();
    }
    #endregion Coding rule : Function
}