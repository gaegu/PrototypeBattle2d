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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterSkillLevelUpController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.CharacterSkillLevelUpPopup; } }
    private CharacterSkillLevelUpPopup View { get { return base.BaseView as CharacterSkillLevelUpPopup; } }
    protected CharacterSkillLevelUpPopupModel Model { get; private set; }
    public CharacterSkillLevelUpController() { Model = GetModel<CharacterSkillLevelUpPopupModel>(); }
    public CharacterSkillLevelUpController(BaseModel baseModel) : base(baseModel) { }
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

        Model.SetCharacterSkillDetailSlotUnitModels();
        Model.SetThumbnailMaterialUnitModels();
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterSkillLevelUpPopup";
    }

    private void OnEventLevelUp()
    {
        if (!Model.CheckMaterialCount())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHARACTERSKILLLEVELUPPOPUP_NOMATERIAL);
            return;
        }

        RequestCharacterSkillLevelUp().Forget();
    }

    private async UniTask RequestCharacterSkillLevelUp()
    {
        CharacterSkillLevelUpProcess characterSkillLevelUp = NetworkManager.Web.GetProcess<CharacterSkillLevelUpProcess>();

        characterSkillLevelUp.Request.SetLevelUpCharacterSkillInDto(new LevelUpCharacterSkillInDto(Model.CharacterId, Model.DataSkillId));

        if (await characterSkillLevelUp.OnNetworkAsyncRequest())
        {
            characterSkillLevelUp.OnNetworkResponse();

            if (Model.CheckMaxLevel())
            {
                Exit().Forget();
                return;
            }

            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetCharacterSkillDetailSlotUnitModels();
            Model.SetThumbnailMaterialUnitModels();

            View.ShowAsync().Forget();
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.CharacterSkillLevelUpConfirm:
                {
                    OnEventLevelUp();
                    await UniTask.NextFrame();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}