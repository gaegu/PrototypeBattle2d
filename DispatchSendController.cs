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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class DispatchSendController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.DispatchSendPopup; } }
    private DispatchSendPopup View { get { return base.BaseView as DispatchSendPopup; } }
    protected DispatchSendPopupModel Model { get; private set; }
    public DispatchSendController() { Model = GetModel<DispatchSendPopupModel>(); }
    public DispatchSendController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private DispatchSelectableCharacterUnitModel currentSelectedCharacterModel = null;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventStartDispatch(OnStartDispatch);
        Model.SetEventSelectCharacter(OnSelectCharacter);
        Model.SetEventAutoSelectCharacter(OnAutoSelectCharacters);
        Model.SetEventSelectUnit(OnEventSelectUnit);
        Model.SetEventClose(() =>
        {
            Exit().Forget();
        });

        InitializeFirstSelect();

        SetMembership();
    }

    private void InitializeFirstSelect()
    {
        currentSelectedCharacterModel = Model.GetSelectableCharacterUnitModel(0);

        if (currentSelectedCharacterModel == null)
            return;

        currentSelectedCharacterModel.SetIsSelect(true);
        Model.SetFilteredThumbnailCharacter(currentSelectedCharacterModel.Index);
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

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        Model.ClearFilteredThumbnailCharacter();
        Model.ClearSelectCharacter();

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dispatch/DispatchSendPopup";
    }

    private void OnEventSelectUnit(DispatchSelectableCharacterUnitModel unitModel)
    {
        // 같은 것을 선택했다면
        if (currentSelectedCharacterModel == unitModel)
        {
            if (unitModel.RegisteredCharacter == null)
                return;

            Model.RemoveSelectCharacter(unitModel.Index);
            View.RefreshSelectCharacter(unitModel.Index);
        }
        else
        {
            if (currentSelectedCharacterModel != null)
            {
                currentSelectedCharacterModel.SetIsSelect(false);
                View.RefreshSelectCharacter(currentSelectedCharacterModel.Index);
            }

            unitModel.SetIsSelect(true);
            Model.RemoveSelectCharacter(unitModel.Index);
            View.RefreshSelectCharacter(unitModel.Index);

            currentSelectedCharacterModel = unitModel;
        }

        UpdateFilterCharaters();
    }

    private void OnSelectCharacter(BaseThumbnailUnitModel model)
    {
        if (currentSelectedCharacterModel == null)
            return;

        var character = PlayerManager.Instance.MyPlayer.User.CharacterModel.GetGoodsById(model.Id);

        Model.RemoveSelectCharacter(currentSelectedCharacterModel.Index);

        SetSelectCharacter(character);
    }

    private void OnAutoSelectCharacters()
    {
        if (!Model.IsOnAutoSelect)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_DISPATCHSENDPOPUP_LOCK_AUTO_SELECT_CHARACTER);
            return;
        }

        Model.SetAutoSelectCharacters();

        View.InitializeSelectCharacters().Forget();

        UpdateFilterCharaters();
    }

    private void SetSelectCharacter(Character character)
    {
        if (currentSelectedCharacterModel == null)
            return;

        Model.SetSelectCharacter(character, currentSelectedCharacterModel.Index);

        View.RefreshSelectCharacter(currentSelectedCharacterModel.Index);

        UpdateFilterCharaters();
    }

    private void OnStartDispatch()
    {
        if (Model.IsAllSelectedCharacters() == false)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_DISPATCH_NOTICE_NEED_CONDITION);
            return;
        }

        RequestStartDispatch().Forget();
    }

    private async UniTask RequestStartDispatch()
    {
        var dispatchStartProcess = NetworkManager.Web.GetProcess(WebProcess.DispatchStart);
        var request = dispatchStartProcess.GetRequest<DispatchStartRequest>();

        request.Clear();
        request.AddDispatch(Model.DispatchId, Model.GetSelectedCharacterIds());
        request.SetSpecialEffectType(SpecialEffectType.None);

        if (await dispatchStartProcess.OnNetworkAsyncRequest())
        {
            dispatchStartProcess.OnNetworkResponse();

            var response = dispatchStartProcess.GetResponse<DispatchStartResponse>();

            Model.UpdateDispatches(response.data.contents.items);
        }

        Exit().Forget();
    }

    private void SetMembership()
    {
        MembershipModel membershipModel = PlayerManager.Instance.MyPlayer.User.MembershipModel;

        Model.SetIsOnAutoSelect(membershipModel.GetEffectValue(SpecialEffectType.DispatchAutoSelect) > 0);
    }

    private void UpdateFilterCharaters()
    {
        if (currentSelectedCharacterModel == null)
            return;

        Model.SetFilteredThumbnailCharacter(currentSelectedCharacterModel.Index);

        View.UpdateFilteredCharacters();
    }
    #endregion Coding rule : Function
}