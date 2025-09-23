//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterCollectionDetailController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CharacterCollectionDetailView; } }
    private CharacterCollectionDetailView View { get { return base.BaseView as CharacterCollectionDetailView; } }
    protected CharacterCollectionDetailViewModel Model { get; private set; }
    public CharacterCollectionDetailController() { Model = GetModel<CharacterCollectionDetailViewModel>(); }
    public CharacterCollectionDetailController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetEventChangeMode(OnEventChangeMode);
        Model.SetEventButtonSnap(OnEventButtonSnap);
        Model.SetEventSwpie(OnEventSwipe);
        Model.SetEventBgmToggle(OnEventBgmToggle);

        Model.SetViewMode(CharacterCollectionDetailViewModel.ViewModeState.Info);

        Model.SetShowIllust(false);

        CameraManager.Instance.SetFreeLookEnable(false);
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        //AdditivePrefabManager.Instance.UnLoadAsync().Forget();
        //CharacterSignatureUnitModel characterSignatureUnitModel = AdditivePrefabManager.Instance.GetModel<CharacterSignatureUnitModel>();
        //characterSignatureUnitModel.SetUI(Model.ShowUI);

        View.ShowMoveText(false);
        CameraManager.Instance.SetFreeLookEnable(true);
        return await UIManager.Instance.Exit(this);
    }

    public override async UniTask LoadingProcess()
    {
        //if (AdditivePrefabManager.Instance.CheckModel<CharacterSignatureUnitModel>())
        //{
        //    CharacterSignatureUnitModel characterSignatureUnitModel = AdditivePrefabManager.Instance.GetModel<CharacterSignatureUnitModel>();
        //    characterSignatureUnitModel.SetCharacter(Model.Character);
        //    characterSignatureUnitModel.SetUIType(UIType.CharacterCollectionDetailView);
        //    await AdditivePrefabManager.Instance.LoadAsync(characterSignatureUnitModel);
        //}
        //else
        //{
        //    CharacterSignatureUnitModel characterSignatureUnitModel = new CharacterSignatureUnitModel(Model.Character, UIType.CharacterCollectionDetailView);
        //    await AdditivePrefabManager.Instance.LoadAsync(characterSignatureUnitModel);
        //}

        //await AdditivePrefabManager.Instance.ShowAsync();
    }

    public override async UniTask PlayShowAsync()
    {
        base.PlayShowAsync().Forget();
        //AdditivePrefabManager.Instance.PlayAsync().Forget();
    }

    public override async UniTask Process()
    {
        //ShowDetailEnterAnimation().Forget();

        OnEventBgmToggle(true);

        await View.ShowAsync();
    }

    private async UniTask ShowDetailEnterAnimation()
    {
        Model.SetShow3DModel(true);

        await UniTask.Delay(3000);  //임시

        View.OnEventToggle3DView();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterCollectionDetailView";
    }

    private void OnEventChangeMode(CharacterCollectionDetailViewModel.ViewModeState viewModeState)
    {
        if (Model.ViewMode == viewModeState)
            return;

        if (View.IsPlayingAnimation)
            return;

        switch (Model.ViewMode)
        {
            case CharacterCollectionDetailViewModel.ViewModeState.Info:
                {
                    View.PlayAsync(CharacterCollectionDetailViewModel.PlayableState.InfoToView).Forget();
                    break;
                }

            case CharacterCollectionDetailViewModel.ViewModeState.View:
                {
                    View.PlayAsync(CharacterCollectionDetailViewModel.PlayableState.ViewToInfo).Forget();
                    break;
                }
        }

        Model.SetViewMode(viewModeState);
        View.ShowAsync().Forget();
    }

    private void OnEventSwipe(bool isLeft)
    {
        if (isLeft)
            return;

        Exit().Forget();
    }

    private void OnEventBgmToggle(bool isOn)
    {
        if (isOn)
        {
            SoundManager.PlayCharacterBGM(Model.CharacterTableData);
        }
        else
        {
            SoundManager.StopBGM();
        }
    }

    private void OnEventButtonSnap()
    {
        UIManager.Instance.BackAsync().Forget();
    }
    #endregion Coding rule : Function
}