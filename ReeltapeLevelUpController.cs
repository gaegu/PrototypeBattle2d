//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Linq;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ReeltapeLevelUpController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ReeltapeLevelUpPopup; } }
    public override void SetModel() { SetModel(new ReeltapeLevelUpPopupModel()); }
    private ReeltapeLevelUpPopup View { get { return base.BaseView as ReeltapeLevelUpPopup; } }
    private ReeltapeLevelUpPopupModel Model { get { return GetModel<ReeltapeLevelUpPopupModel>(); } }
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
        Model.SetEventSelect(OnEventSelect);
        Model.SetEventHold(OnEventHold);
        Model.SetEventSelectCancel(OnEventSelectCancel);
        Model.SetEventReset(OnEventReset);
        Model.SetEventAutoSelect(OnEventAutoSelect);
        Model.SetEventLevelUp(OnEventLevelUp);
        Model.SetEventGradeUp(OnEventGradeUp);
    }

    public override async UniTask LoadingProcess()
    {
        Model.SetReeltapeUI(PlayerManager.Instance.MyPlayer.User);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }
    
    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(Func<UISubState, UniTask> onEventExtra = null)
    {
        var backController = UIManager.Instance.GetController(UIManager.Instance.GetBackUIType());

        if (backController != null)
            backController.Refresh();

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Reeltape/ReeltapeLevelUpPopup";
    }

    private ThumbnailSelectType OnEventSelect(BaseThumbnailUnitModel model)
    {
        if (Model.ExpectedLevel == Model.TargetReeltape.MaxLevel)
            return ThumbnailSelectType.None;

        Model.SelectMaterial(model);
        View.RefreshAsync().Forget();
        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventHold(BaseThumbnailUnitModel model)
    {
        return ThumbnailSelectType.None;
    }

    private void OnEventSelectCancel(ThumbnailSelectUnitModel model)
    {
        Model.UnSelectMaterial(model);
        View.RefreshAsync().Forget();
    }

    private void OnEventReset()
    {
        Model.ResetMaterial();
        View.RefreshAsync().Forget();
    }

    private void OnEventAutoSelect()
    {
        if (Model.ExpectedLevel == Model.CurrentMaxLevel)
            return;

        Model.AutoSelectMaterial();
        View.RefreshAsync().Forget();
    }

    private void OnEventLevelUp()
    {
        if (!Model.IsSelectMaterial)
            return;

        //강화된 릴테이프가 있는 경우 LOCALIZATION_REELTAPELEVELUPPOPUP_INCLUDE_ENHANCED_REELTAPE 경고메세지 우선 표기

        RequestLevelUp().Forget();
    }

    private async UniTask RequestLevelUp()
    {
        BaseProcess process = NetworkManager.Web.GetProcess(WebProcess.ReeltapeLevelUp);
        process.SetPacket(Model.GetPacket());

        if (await process.OnNetworkAsyncRequest())
        {
            process.OnNetworkResponse();

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_REELTAPELEVELUPPOPUP_LEVELUP_COMPLETE,
                ToastMessagePopupModel.ToastMessageType.Middle, "UI/Contents/MessageBox/ToastMessagePopup/ToastMessagePopup_Effect_LevelUp");

            Model.SetReeltapeUI(PlayerManager.Instance.MyPlayer.User);
            await View.ShowAsync();
        }
    }

    public void OnEventGradeUp()
    {
        if (Model.MaterialLists.Any(x => x.IsLack))
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_REELTAPELEVELUPPOPUP_NOT_ENOUGH_MATERIAL);
            return;
        }

        RequestGradeUp().Forget();
    }

    private async UniTask RequestGradeUp()
    {
        BaseProcess baseProcess = NetworkManager.Web.GetProcess(WebProcess.ReeltapeGradeUp);
        baseProcess.SetPacket(new GradeUpReeltapeInDto(Model.TargetReeltape.Id));

        if (await baseProcess.OnNetworkAsyncRequest())
        {
            baseProcess.OnNetworkResponse();

            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_REELTAPELEVELUPPOPUP_GRADEUP_COMPLETE,
                ToastMessagePopupModel.ToastMessageType.Middle, "UI/Contents/MessageBox/ToastMessagePopup/ToastMessagePopup_Effect_LevelUp");

            Model.SetReeltapeUI(PlayerManager.Instance.MyPlayer.User);
            View.ShowAsync().Forget();
        }
    }
    #endregion Coding rule : Function
}