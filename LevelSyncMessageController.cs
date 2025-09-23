//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class LevelSyncMessageController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.LevelSyncMessagePopup; } }
    private LevelSyncMessagePopup View { get { return base.BaseView as LevelSyncMessagePopup; } }
    protected LevelSyncMessagePopupModel Model { get; private set; }
    public LevelSyncMessageController() { Model = GetModel<LevelSyncMessagePopupModel>(); }
    public LevelSyncMessageController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventCancel(OnEventCancel);
        Model.SetBeforeThumbnailModel();
        Model.SetAfterThumbnailModel();
    }

    public override UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return base.Exit(onEventExtra);
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "LevelSync/LevelSyncMessagePopup";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        Character character = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetLeaderCharacter();

        Model.SetCharacter(character);
    }

    private void OnEventConfirm()
    {
        Exit(async (state) =>
        {
            if (state == UISubState.Finished)
                Model.OnEventFinishedConfirm?.Invoke();
        }).Forget();
    }

    private void OnEventCancel()
    {
        Exit(async (state) =>
        {
            if (state == UISubState.Finished)
                Model.OnEventFinishedCancel?.Invoke();
        }).Forget();
    }
    #endregion Coding rule : Function
}