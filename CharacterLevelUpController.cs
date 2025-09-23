//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Threading;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterLevelUpController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.CharacterLevelUpPopup; } }
    private CharacterLevelUpPopup View { get { return base.BaseView as CharacterLevelUpPopup; } }
    protected CharacterLevelUpPopupModel Model { get; private set; }
    public CharacterLevelUpController() { Model = GetModel<CharacterLevelUpPopupModel>(); }
    public CharacterLevelUpController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetEventLevelUp(OnEventLevelUp);
    }

    public override async UniTask LoadingProcess()
    {
        TableManager.Instance.LoadTable<CharacterBalanceTable>();

        Model.SetLevelUpModel(OnEventSelectNumber);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterLevelUpPopup";
    }

    private bool OnEventSelectNumber(int number)
    {
        Model.ChangeSelectNumber(number);
        View.RefreshAsync().Forget();

        return true;
    }

    private async UniTask OnEventLevelUp()
    {
        if (Model.SelectNumber.Number == 0)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_TOASTMESSAGEPOPUP_NOT_ENOUGH_GOODS);
            return;
        }

        if (Model.TargetCharacter.OriginalLevel == Model.TargetCharacter.MaxLevel)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_TOASTMESSAGEPOPUP_NOT_ENOUGH_GOODS);
            return;
        }

        await RequestCharacterLevelUp();
    }

    private async UniTask RequestCharacterLevelUp()
    {
        BaseProcess characterLevelUpProcess = NetworkManager.Web.GetProcess(WebProcess.CharacterLevelUp);

        if (!characterLevelUpProcess.CheckRequest())
            return;

        characterLevelUpProcess.SetPacket(new LevelUpCharacterInDto(Model.TargetCharacter.Id, Model.NextLevel - Model.CurrentLevel));
        characterLevelUpProcess.SetLoading(false, false);

        if (await characterLevelUpProcess.OnNetworkAsyncRequest())
        {
            characterLevelUpProcess.OnNetworkResponse(Model);
            View.ShowAsync().Forget();

            SoundManager.SfxFmod.Play(StringDefine.FMOD_EVENT_UI_MENU_SFX, StringDefine.FMOD_DEFAULT_PARAMETER, 26);
        }
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.CharacterLevelUpConfirm:
                {
                    await OnEventLevelUp();

                    try
                    {
                        IronJade.Debug.Log("[WaitUntil ToastMessage] WaitUntil ToastMessagePopup Open");
                        await UniTask.WaitUntil(() => UIManager.Instance.CheckOpenUI(UIType.ToastMessagePopup)).Timeout(System.TimeSpan.FromSeconds(3f));
                        IronJade.Debug.Log("[WaitUntil ToastMessage] WaitUntil ToastMessagePopup Close");
                        await UniTask.WaitUntil(() => !UIManager.Instance.CheckOpenUI(UIType.ToastMessagePopup, true)).Timeout(System.TimeSpan.FromSeconds(3f));
                    }
                    catch (TimeoutException)
                    {
                        IronJade.Debug.Log("[WaitUntil ToastMessage] Timeout");
                        await UIManager.Instance.GetNoneStackController(UIType.ToastMessagePopup).Exit();
                    }

                    break;
                }

            case TutorialExplain.CharacterLevelUpBack:
                {
                    await Exit();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}