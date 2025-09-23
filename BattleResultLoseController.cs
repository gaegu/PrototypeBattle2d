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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class BattleResultLoseController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.BattleResultLosePopup; } }
    private BattleResultLosePopup View { get { return base.BaseView as BattleResultLosePopup; } }
    protected BattleResultLosePopupModel Model { get; private set; }
    public BattleResultLoseController() { Model = GetModel<BattleResultLosePopupModel>(); }
    public BattleResultLoseController(BaseModel baseModel) : base(baseModel) { }
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
        SetContentsOpen().Forget();
        Model.SetEventExit(OnEventExit);
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        Action backAction = Model.HasOnEventBack() ? Model.OnEventGoBack : Model.OnEventGoHome;
        backAction();
        return true;
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await base.Exit(onEventExtra);
    }

    public override async UniTask LoadingProcess()
    {
        await TaskSetResultInfo();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();

        //UIManager.Instance.SetViewBlurImage(false, true);
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Battle/BattleResultLosePopup";
    }

    private async UniTask SetContentsOpen()
    {
        Model.SetCharacterManagerAvailable(true);
        //Model.SetCharacterManagerAvailable(await ContentsOpenManager.Instance.OpenContents(ContentsType.Character));
        Model.SetContentsShopAvailable(ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.ContentsShop));
        Model.SetGachaShopAvailable(ContentsOpenManager.Instance.CheckContentsOpen(ContentsType.GachaShop));
    }

    private void OnEventExit(System.Action onEventAfterAction)
    {
        if (CheckPlayingAnimation())
            return;

        Exit(async (state) =>
        {
            if (state == UISubState.Finished)
                onEventAfterAction?.Invoke();
        }).Forget();
    }

    private async UniTask TaskSetResultInfo()
    {
        BattleResultInfoModel resultInfo = Model.BattleResultInfoModel;

        if (resultInfo == null)
            return;

        switch (resultInfo)
        {
            case BattleResultStageDungeonInfoModel:
                {
                    Model.SetEventPrepareRetry(resultInfo.OnClickRetryBattle);
                    break;
                }
            default:
                {
                    Model.SetEventPrepareRetry(null);
                    break;
                }
        }
    }

    public override async UniTask PlayHideAsync()
    {
        base.PlayHideAsync().Forget();

        // 임의로 맞춤.. 피디님 요청
        await UniTask.Delay(IntDefine.PHONE_DOWN_DELAY);
    }
    #endregion Coding rule : Function
}