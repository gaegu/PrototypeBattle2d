#pragma warning disable CS1998
using Cysharp.Threading.Tasks;      
using IronJade.UI.Core;
using System;
using UnityEngine;
using static TermsAgreementPopupModel;

public class TermsAgreementController : BaseController
{
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.TermsAgreementPopup; } }
    public override void SetModel() { SetModel(new TermsAgreementPopupModel()); }
    private TermsAgreementPopup View { get { return base.BaseView as TermsAgreementPopup; } }
    private TermsAgreementPopupModel Model { get { return GetModel<TermsAgreementPopupModel>(); } }
    #region Coding rule : Property
    #endregion Coding rule : Property
    
    #region Coding rule : Value
    #endregion Coding rule : Value
    
    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetOnClickToggle(OnClickToggle);
        Model.SetOnClickStart(OnStart);
        Model.SetOnClickAgreeAllStart(() => { OnClickAgreeAllStart().Forget(); });
        Model.SetOnClickShowDesciption(OnClickShowDescription);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "TermsAgreementPopup";
    }


    private void OnStart()
    {
        bool pushNoti = Model.IsAgreed(AgreementType.PushNotification);
        bool nightPushNoti = Model.IsAgreed(AgreementType.NightPushNotification);

        GameSettingManager.Instance.SaveAlarmOption(pushNoti, pushNoti, nightPushNoti, pushNoti);

        Exit(OnExit).Forget();
    }

    private void OnClickToggle(int agreementType)
    {
        AgreementType type = (AgreementType)agreementType;

        Model.SetAgree(type, !Model.IsAgreed(type));

        View.EnableStartDim(!Model.CheckStartable());

        IronJade.Debug.Log($"OnClickToggle {type} / Enable Start : {Model.CheckStartable()}");
    }

    private async UniTask OnClickAgreeAllStart()
    {
        for (AgreementType type = AgreementType.TermsOfService;
            type <= AgreementType.NightPushNotification; type++)
        {
            Model.SetAgree(type, true);
            View.SetToggle(type, true);
        }

        await UniTask.Delay(500);

        OnStart();
    }

    private void OnAgreeAndClose(AgreementType agreementType)
    {
        View.SetToggle(agreementType, true);
    }

    private string GetDetailDescription(AgreementType type)
    {
        if (type == AgreementType.TermsOfService || type == AgreementType.PersonalInfomation)
        {
            string termsUrlFormat = "https://storage.googleapis.com/fgn-cdn.nerdystar.io/environment/terms/{0}_{1}.html";
            string temrsUrl = string.Format(termsUrlFormat, type.ToString(), GameSettingManager.Instance.GameSetting.TextLanguageType);
            string urlWithCacheBypass = $"{temrsUrl}?t={System.DateTime.Now.Ticks}";

            return urlWithCacheBypass;
        }

        return string.Empty;
    }

    public void OnClickShowDescription(AgreementType agreementType)
    {
        var url = GetDetailDescription(agreementType);

        if (string.IsNullOrEmpty(url))
            return;

        Application.OpenURL(url);
    }


    private async UniTask OnExit(UISubState state)
    {
        if (state == UISubState.Finished && Model.OnExitAsync != null)
            await Model.OnExitAsync();
    }

    #region obsolete
    public void OnClickShowDescription_Old(AgreementType agreementType)
    {
        string title;
        //string description;
        switch (agreementType)
        {
            case AgreementType.TermsOfService:
                title = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_SERVICE_TERMS);
                break;

            case AgreementType.PersonalInfomation:
                title = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_PRIVACY_TERMS);
                break;

            default:
                return;
        }

        var controller = UIManager.Instance.GetController(UIType.TermsDescriptionPopup);
        var model = controller.GetModel<TermsDescriptionPopupModel>();

        model.SetTermsTitleText(title);
        model.SetOnAgreeAndClose(() => OnAgreeAndClose(agreementType));

        UIManager.Instance.EnterAsync(controller).Forget();
    }
    #endregion
    #endregion Coding rule : Function
}