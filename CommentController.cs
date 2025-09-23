//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
using IronJade.LowLevel.Server.Web;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//=========================================================================================================

public class CommentController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.CommentPopup; } }
    private CommentPopup View { get { return base.BaseView as CommentPopup; } }
    protected CommentPopupModel Model { get; private set; }
    public CommentController() { Model = GetModel<CommentPopupModel>(); }
    public CommentController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetInput(string.Empty);

        Model.SetEventConfirm(OnEventConfirm);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/CommentPopup";
    }

    private void OnEventConfirm(string input)
    {
        if (Model.CheckMaxLength(input))
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_MAX_WORD, Model.MaxLength));
            return;
        }

        if (Model.CheckMinLength(input))
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_MIN_WORD, Model.MinLength));
            return;
        }

        if (TableManager.Instance.CheckForbiddenWord(input, out string result))
        {
            // {금칙어} 단어는 사용할 수 없습니다.
            //MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, result));
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, result)).Forget();
            return;
        }

        Model.SetInput(input);

        Exit(async (state) =>
        {
            if (state == UISubState.Finished)
                Model?.OnEventCallbackConfirm();
        }).Forget();
    }
    #endregion Coding rule : Function
}