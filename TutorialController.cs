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

public class TutorialController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.TutorialPopup; } }
    private TutorialPopup View { get { return base.BaseView as TutorialPopup; } }
    protected TutorialPopupModel Model { get; private set; }
    public TutorialController() { Model = GetModel<TutorialPopupModel>(); }
    public TutorialController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventSkip(OnEventSkip);
        Model.SetActive(true);

        TutorialManager.Instance.SetEventTutorialPopupActive(OnEventActiveTutorial);
        TutorialManager.Instance.SetEventTutorialTouchActive(OnEventActiveTutorialTouch);
    }

    public override async UniTask LoadingProcess()
    {
        //특정 UI에서 시작되는 튜토리얼은 해당 UI가 완전히 열릴 때까지 대기
        UIType startUIType = TutorialManager.Instance.GetTutorialStartUITypeByDataId(Model.TutorialDataId);
        if (startUIType != UIType.None)
        {
            await UniTask.WaitUntil(() => { return UIManager.Instance.CheckOpenCurrentUI(startUIType); });
            await UniTask.WaitUntil(() => { return !UIManager.Instance.GetController(startUIType).CheckPlayingAnimation(); });
        }

        //로비 시작 튜토리얼이라면 일단 이동을 막는다.
        if (TutorialManager.Instance.GetTutorialStartUITypeByDataId(Model.TutorialDataId) == UIType.LobbyView)
            TownSceneManager.Instance.SetActiveTownInput(false);
    }

    public override async UniTask Process()
    {
        GetPrefab().transform.SetAsLastSibling();

        await View.ShowAsync();
    }

    public override void Refresh()
    {
        View.ShowAsync().Forget();
    }

    //public override UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    //{
    //    return BackBlock();
    //}

    //public async UniTask<bool> BackBlock()
    //{
    //    return true;
    //}

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Tutorial/TutorialPopup";
    }

    public void OnEventActiveTutorial(bool isActive)
    {
        if (Model.IsActive == isActive)
            return;

        Model.SetActive(isActive);
        Model.SetSkipButton(isActive);
        View.ShowDefaultInfos();
    }

    public void OnEventActiveTutorialTouch(bool isActive)
    {
        Model.SetTouchable(isActive);
        View.ShowDefaultInfos();
    }

    public void OnEventSkip()
    {
        TaskSkipTutorial().Forget();
    }

    private async UniTask TaskSkipTutorial()
    {
        TutorialManager.Instance.SaveTutorial(Model.TutorialDataId);
        await UniTask.WaitUntil(() => TutorialManager.Instance.CheckTutorialClear(Model.TutorialDataId));

        // 다음 재생할 튜토리얼이 있다면 팝업을 종료하지 않고 연속으로 재생한다.
        UIType uIType = UIManager.Instance.GetCurrentUIType();
        TutorialManager.Instance.SetPlayableTutorial(uIType, ignorePlaying: true);
        if (TutorialManager.Instance.HasPlayableTutorial)
        {
            if (uIType == UIType.LobbyView)
                TownSceneManager.Instance.UpdateLobbyMenu().Forget();

            IronJade.Debug.Log("[Tutorial Process] 튜토리얼 연속 재생");
            TutorialManager.Instance.PlayNextTutorial();
        }
        else
        {
            IronJade.Debug.Log("[Tutorial Process] 튜토리얼 종료");
            UIManager.Instance.Exit(UIType.TutorialPopup).Forget();
        }
    }
    #endregion Coding rule : Function
}