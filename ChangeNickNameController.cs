//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ChangeNickNameController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ChangeNickNamePopup; } }
    public override void SetModel() { SetModel(new ChangeNickNamePopupModel()); }
    private ChangeNickNamePopup View { get { return base.BaseView as ChangeNickNamePopup; } }
    private ChangeNickNamePopupModel Model { get { return GetModel<ChangeNickNamePopupModel>(); } }
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
        SetCurrency();
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await base.Back(onEventExtra);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/ChangeNickNamePopup";
    }

    private void SetCurrency()
    {
        CostTable costTable = TableManager.Instance.GetTable<CostTable>();
        CostTableData costTableData = costTable.GetDataByID((int)CostDefine.COST_USER_INFORMATION_NICKNAME_CHANGE);
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetCurrency(new Currency(CurrencyType.Cash, (int)CurrencyDefine.CURRENCY_CASH, costTableData.GetGOODS_COUNT(0)));
        Model.SetEnough(PlayerManager.Instance.CheckEnough(Model.Currency.Type, Model.Currency.Count, false));
        Model.SetTextColor(Color.black, Color.red);

        Model.SetEventConfirm(OnEventConfirm);
        Model.SetEventClose(OnEventClose);
        Model.SetTemplate();
    }

    private void OnEventConfirm(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            // 닉네임은 최소 2자 이상
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_MIN_WORD, IntDefine.MIN_NICK_NAME_INPUT));
            return;
        }

        if (input.Contains(' '))
        {
            // 금지된 문자
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, ' ')).Forget();
            return;
        }

        if (PlayerManager.Instance.MyPlayer.User.NickName.Equals(input))
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_CHANGENICKNAMEPOPUP_SAME_NICKNAME);
            return;
        }

        input = TableManager.Instance.GetNicknamForbiddenEndWord(input);

        if (Model.CheckMaxLength(input))
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_MAX_WORD, IntDefine.MAX_NICK_NAME_INPUT));
            return;
        }

        if (Model.CheckMinLength(input))
        {
            MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_MIN_WORD, IntDefine.MIN_NICK_NAME_INPUT));
            return;
        }

        if (TableManager.Instance.CheckForbiddenWord(input, out string result))
        {
            // {금칙어} 단어는 사용할 수 없습니다.
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, result)).Forget();
            return;
        }

        Model.SetInput(input);

        if (!PlayerManager.Instance.CheckEnough(Model.Currency.Type, Model.Currency.Count))
            return;

        RequestNickNameUpdate().Forget();
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }


    private async UniTask RequestNickNameUpdate()
    {
        UserNickNameUpdateProcess userNickNameUpdateProcess = NetworkManager.Web.GetProcess<UserNickNameUpdateProcess>();

        userNickNameUpdateProcess.SetPacket(new UserCreateNickNameInDto(Model.Input));

        if (await userNickNameUpdateProcess.OnNetworkAsyncRequest())
        {
            userNickNameUpdateProcess.OnNetworkResponse();

            if (Model.OnEventNextProcess == null)
            {
                //??
                Exit().Forget();
                return;
            }

            Exit().Forget();

            //??
            Model.OnEventNextProcess();
        }
    }

    #endregion Coding rule : Function
}