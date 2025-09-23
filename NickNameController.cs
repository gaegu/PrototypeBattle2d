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
using IronJade.Server.Web.Core;
using IronJade.UI.Core;
using System;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class NickNameController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.NickNamePopup; } }
    public override void SetModel() { SetModel(new NickNamePopupModel()); }
    private NickNamePopup Popup { get { return base.BaseView as NickNamePopup; } }
    private NickNamePopupModel Model { get { return GetModel<NickNamePopupModel>(); } }
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
        Model.SetEventConfirm(OnEventConfirm);
        Model.SetEventClose(OnEventClose);
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        await GameManager.Instance.OpenApplicationQuitPopup();
        return await base.Back(onEventExtra);
    }
    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await Popup.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/NickNamePopup";
    }

    private void OnEventConfirm(string input)
    {
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
            //MessageBoxManager.ShowToastMessage(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, result));
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_INPUT_FORBIDDEN_WORD, result)).Forget();
            return;
        }

        Model.SetInput(input);
        RequestUserCreate().Forget();
    }

    private void OnEventClose()
    {
        // 게임을 종료하곘냐고 물어보자!
        GameManager.Instance.OpenApplicationQuitPopup().Forget();
    }

    private async UniTask RequestUserCreate()
    {
        BaseProcess userCreateProcess = NetworkManager.Web.GetProcess(WebProcess.UserNickNameCreate);
        userCreateProcess.SetPacket(new UserCreateNickNameInDto(Model.Input));

        if (await userCreateProcess.OnNetworkAsyncRequestForce())
        {
            IronJade.Debug.Log($"Nickname Create Success");

            userCreateProcess.OnNetworkResponse();

            if (Model.SkipExit)
            {
                if (Model.OnEventLoginProcess != null)
                    await Model.OnEventLoginProcess();
                return;
            }
            else
            {
                Func<UniTask> loginProcess = Model.OnEventLoginProcess;
                Exit(async (state) =>
                {
                    if (state == UISubState.Finished)
                    {
                        if (loginProcess != null)
                            await loginProcess();
                    }
                }).Forget();
            }
        }
    }
    #endregion Coding rule : Function
}