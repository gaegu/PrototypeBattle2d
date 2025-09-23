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
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterTranscendenceController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.CharacterTranscendencePopup; } }
    private CharacterTranscendencePopup View { get { return base.BaseView as CharacterTranscendencePopup; } }
    protected CharacterTranscendencePopupModel Model { get; private set; }
    public CharacterTranscendenceController() { Model = GetModel<CharacterTranscendencePopupModel>(); }
    public CharacterTranscendenceController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetWaitingNetwork(false);

        Model.SetBackEnter(false);
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetTranscendenceInfo();

        Model.SetEventTranscendence(OnEventTranscendence);
    }

    public override async UniTask LoadingProcess()
    {
        TableManager.Instance.LoadTable<CharacterBalanceTable>();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        Model.SetWaitingNetwork(false);
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetTranscendenceInfo();
        View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterTranscendencePopup";
    }

    private void OnEventTranscendence()
    {
        if (Model.IsWaitingNetwork)
            return;

        if (Model.IsBlockByMaxTranscendence)
            return;

        if (Model.IsLackMaterial)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_CONNECT_TRANSENDENCE_ITEM_NOT);
            return;
        }

        if (Model.IsRankTranscendence)
        {
            MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_CONNECT_TRANSENDENCE_GRADEUP_MARERIAL_HARD, onEventConfirm: () =>
            {
                RequestCharacterTranscendence().Forget();
            }).Forget();
        }
        else
        {
            MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_CONNECT_TRANSENDENCE_GRADEUP_MARERIAL_NORMAL, onEventConfirm: () =>
            {
                RequestCharacterTranscendence().Forget();
            }).Forget();
        }
    }

    private async UniTask RequestCharacterTranscendence()
    {
        if (Model.IsBlockByMaxTranscendence)
        {
            if (Model.IsMaxTranscendence)
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_CONNECT_TRANSENDENCE_GRADE_MAX_NORMAL);
            else if (Model.IsMaxRankTranscendence)
                MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_CONNECT_TRANSENDENCE_GRADE_MAX_HARD);

            return;
        }

        Character beforeCharacter = Model.TargetCharacter;
        BaseController resultPopupController = UIManager.Instance.GetController(UIType.CharacterTranscendenceResultView);
        CharacterTranscendenceResultViewModel model = resultPopupController.GetModel<CharacterTranscendenceResultViewModel>();
        model.SetBeforeCharacterInfo(beforeCharacter);

        CharacterTranscendProcess characterTranscendenceProcess = NetworkManager.Web.GetProcess<CharacterTranscendProcess>();
        characterTranscendenceProcess.Request.SetTranscendCharacterInDto(new TranscendCharacterInDto(Model.TargetCharacter.Id));

        Model.SetWaitingNetwork(true);
        if (await characterTranscendenceProcess.OnNetworkAsyncRequest())
        {
            characterTranscendenceProcess.OnNetworkResponse();
            Character afterCharacter = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsById(Model.TargetCharacter.Id);

            Model.SetWaitingNetwork(false);
            Model.SetBackEnter(true);
            model.SetAfterCharacterInfo(afterCharacter);
            UIManager.Instance.EnterAsync(resultPopupController).Forget();
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain explainType = (TutorialExplain)type;

        switch (explainType)
        {
            case TutorialExplain.CharacterTranscendenceLevelup:
                {
                    await RequestCharacterTranscendence();
                    //TutorialManager.Instance.OnEventTutorialTouchActive(true);
                    //await UniTask.WaitUntil(() => UIManager.Instance.CheckOpenCurrentUI(UIType.CharacterTranscendenceResultPopup));
                    //await UniTask.WaitUntil(() => UIManager.Instance.CheckOpenCurrentUI(UIType.CharacterTranscendencePopup));
                    //TutorialManager.Instance.OnEventTutorialTouchActive(false);
                    break;
                }

            case TutorialExplain.CharacterTranscendenceBack:
                {
                    await Exit();
                    await UniTask.WaitUntil(() => !UIManager.Instance.GetCurrentNavigator().BaseView.IsPlayingAnimation);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}