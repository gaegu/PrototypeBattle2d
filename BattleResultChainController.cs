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
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEditor;
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class BattleResultChainController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.BattleResultChainPopup; } }
    public override void SetModel() { SetModel(new BattleResultChainPopupModel()); }
    private BattleResultChainPopup View { get { return base.BaseView as BattleResultChainPopup; } }
    private BattleResultChainPopupModel Model { get { return GetModel<BattleResultChainPopupModel>(); } }
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
        Model.SetEventNextDungeonProcess(OnEventNextDungeonProcess);
        Model.SetEventCancelProcess(OnEventCancelProcess);
        Model.SetProcessing(false);
        Model.SetSimpleRewardScrollUnitModel(null);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask<bool> Back(Func<UISubState, UniTask> onEventExtra = null)
    {
        if (PrologueManager.Instance.IsProgressing)
            return true;

        IronJade.Debug.Log($"[BattleChain] Back IsProcessing : {Model.IsProcessing}");

        Model.SetProcessing(true);
        TokenPool.Cancel(GetHashCode());

        bool isBack = await base.Back(onEventExtra: async (state) =>
        {
            if (state == UISubState.Finished)
                Model.OnEventCancel().Forget();
        });

        return isBack;
    }

    public override async UniTask Process()
    {
        View.ShowStopButton();
        View.ShowSimpleRewardList().Forget();
        View.ShowAfterSeconds(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_BATTLERESULTCHAINPOPUP_AFTERSECONDS));

        WaitAfterSeconds().Forget();
    }

    private async UniTask WaitAfterSeconds()
    {
        int afterSeconds = 500;      //TimeScale 오류로 풀리지 않는 경우를 대비
        
        while (Time.timeScale < 1f && afterSeconds > 0)
        {
            //View.ShowAfterSeconds(TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_BATTLERESULTCHAINPOPUP_AFTERSECONDS));
            afterSeconds--;

            if (await UniTask.Delay(10, cancellationToken: TokenPool.Get(GetHashCode())).SuppressCancellationThrow())
            {
                IronJade.Debug.Log($"[BattleChain] Throw");
                return;
            }
        }

        OnEventNextDungeonProcess();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Battle/BattleResultChainPopup";
    }

    private void OnEventNextDungeonProcess()
    {
        IronJade.Debug.Log($"[BattleChain] OnEventNextDungeonProcess IsProcessing : {Model.IsProcessing}");

        if (Model.IsProcessing)
            return;

        TokenPool.Cancel(GetHashCode());
        Model.SetProcessing(true);

        if (Model.OnEventNextDungeon != null)
            Model.OnEventNextDungeon();
    }

    private void OnEventCancelProcess()
    {
        if (PrologueManager.Instance.IsProgressing)
            return;

        IronJade.Debug.Log($"[BattleChain] OnEventCancelProcess IsProcessing : {Model.IsProcessing}");

        if (Model.IsProcessing)
            return;

        TokenPool.Cancel(GetHashCode());
        Model.SetProcessing(true);

        if (Model.OnEventCancel != null)
            Model.OnEventCancel();
    }

    #endregion Coding rule : Function
}