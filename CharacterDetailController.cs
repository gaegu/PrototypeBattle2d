//=========================================================================================================
#pragma warning disable CS1998
using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterDetailController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CharacterDetailView; } }
    private CharacterDetailView View { get { return base.BaseView as CharacterDetailView; } }
    protected CharacterDetailViewModel Model { get; private set; }
    public CharacterDetailController() { Model = GetModel<CharacterDetailViewModel>(); }
    public CharacterDetailController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isWaitChangeCharacter;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        isWaitChangeCharacter = false;

        Model.Init();
        Model.SetOnEventChangeNextCharacter(ChanageNextCharacter);
        Model.SetOnEventChangeBackCharacter(ChanageBackCharacter);
        Model.SetEventDoll(OnEventDoll);
        Model.SetEventCharacterIntroduceView(OnEventCharacterIntroduceView);
        Model.SetEventChangePage(OnEventChangePage);

        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetFuncGetRedDot(GetTranscendenceable);

        ObserverManager.AddObserver(ViewObserverID.Refresh, this);
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (Model.PageType == CharacterDetailViewModel.DetailPageType.Reeltape)
        {
            Model.OnEventChangePage(CharacterDetailViewModel.DetailPageType.Character);
            return true;
        }

        isWaitChangeCharacter = false;
        ObserverManager.RemoveObserver(ViewObserverID.Refresh, this);
        Model.ClearChangeCharacterStat();

        if (UIManager.Instance.CheckBackUI(UIType.CharacterManagerView))
        {
            var signatureUnit = AdditivePrefabManager.Instance.SignatureUnit;
            signatureUnit.Model.SetUIType(UIType.CharacterManagerView);

            return await base.Exit(async (state) =>
            {
                if (state == UISubState.AfterLoading)
                    Model.OnEventBackToCharacterManager(Model.Character.Id);

                await OnEventExtra(onEventExtra, state);
            });
        }
        else
        {
            return await base.Exit(onEventExtra);
        }
    }

    //public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    //{
    //    if (UIManager.Instance.CheckBackUI(UIType.CharacterManagerView))
    //    {
    //        var signatureUnit = AdditivePrefabManager.Instance.SignatureUnit;
    //        signatureUnit.Model.SetUIType(UIType.CharacterManagerView);

    //        return await base.Back(async (state) =>
    //        {
    //            if (state == UISubState.AfterLoading)
    //                Model.OnEventBackToCharacterManager(Model.Character.Id);

    //            await onEventExtra(state);
    //        });
    //    }
    //    else
    //    {
    //        return await base.Back(onEventExtra);
    //    }
    //}

    public override async UniTask PlayHideAsync()
    {
        await base.PlayHideAsync();

        if (UIManager.Instance.CheckBackUI(UIType.CharacterManagerView))
        {
            AdditivePrefabManager.Instance.SignatureUnit.ShowAsync().Forget();
        }

        //영웅정보가 아닌 타 루트로 온 경우 카메라 컬링마스크 롤백
        if (!UIManager.Instance.CheckBackUI(UIType.CharacterManagerView))
            CameraManager.Instance.RestoreCharacterCameraCullingMask();
    }

    public override void BackEnter()
    {
        View.RefreshAsync().Forget();
    }

    public override async UniTask LoadingProcess()
    {
        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterSignature);
        var signatureUnit = AdditivePrefabManager.Instance.SignatureUnit;
        signatureUnit.Model.SetCharacter(Model.Character);
        signatureUnit.Model.SetUIType(UIType.CharacterDetailView);
        await signatureUnit.ShowAsync();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();

        Model.SetCachingStats();
    }

    public override async UniTask PlayShowAsync()
    {
        base.PlayShowAsync().Forget();
        AdditivePrefabManager.Instance.SignatureUnit.ShowAsync().Forget();

        await UniTask.WhenAll(UniTask.WaitWhile(() => base.CheckPlayingAnimation()));
        //UniTask.WaitWhile(() => AdditivePrefabManager.Instance.CheckPlaying(AdditiveType.CharacterSignature)));
    }

    public override UniTask PlayBackShowAsync()
    {
        return PlayShowAsync();
    }

    public override void Refresh()
    {
        Model.SetChangeCharacterStat();
        Model.SetCachingStats();

        View.RefreshAsync().Forget(); //View.ShowAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterDetailView";
    }

    public void ChanageNextCharacter()
    {
        if (!Model.IsCharacterList)
            return;

        if (isWaitChangeCharacter)
            return;

        isWaitChangeCharacter = true;

        Model.SetSelectIndex(Model.SelectInex + 1);
        Model.SetCharacter(Model.CharacterList[Model.SelectInex]);

        View.RefreshAsync().Forget();
        BackGroundLoad().Forget();
    }

    public void ChanageBackCharacter()
    {
        if (!Model.IsCharacterList)
            return;

        if (isWaitChangeCharacter)
            return;

        isWaitChangeCharacter = true;

        Model.SetSelectIndex(Model.SelectInex - 1);
        Model.SetCharacter(Model.CharacterList[Model.SelectInex]);

        View.RefreshAsync().Forget();
        BackGroundLoad().Forget();
    }

    public async UniTask BackGroundLoad()
    {
        await LoadingProcess();

        isWaitChangeCharacter = false;
    }

    private bool GetTranscendenceable(Character character)
    {
        return PlayerManager.Instance.MyPlayer.User.CheckTranscendenceable(character);
    }

    private void OnEventDoll()
    {
        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_COMMON_TO_BE_DEVELOPED);
    }

    private void OnEventCharacterIntroduceView()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.CharacterIntroduceView);
        var viewModel = controller.GetModel<CharacterIntroduceViewModel>();
        viewModel.SetUser(PlayerManager.Instance.MyPlayer.User);
        viewModel.DateSetting(Model.Character);
        viewModel.SetDetailButton(false);

        UIManager.Instance.EnterAsync(controller, onEventExtra: async (state) =>
        {
            if (state == UISubState.AfterLoading)
                await AdditivePrefabManager.Instance.UnLoadAsync(AdditiveType.CharacterSignature);
        }).Forget();
    }

    private void OnEventChangePage(CharacterDetailViewModel.DetailPageType type)
    {
        Model.SetPageType(type);
        TaskChangePageAsync(type).Forget();
    }

    private async UniTask TaskChangePageAsync(CharacterDetailViewModel.DetailPageType type)
    {
        if (type == CharacterDetailViewModel.DetailPageType.Reeltape)
        {
            await View.PlayHideAsync();
            await View.RefreshAsync();
            await View.PlayAsync(CharacterDetailViewModel.PlayableState.CharacterToReeltape);
        }
        else
        {
            await View.PlayAsync(CharacterDetailViewModel.PlayableState.ReeltapeToCharacter);
            await View.RefreshAsync();
            await View.PlayShowAsync();
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.CharacterDetailLevelUp:
                {
                    View.GrowthInfoUnit.OnClickLevelUp(null);
                    await TutorialManager.WaitUntilEnterUI(UIType.CharacterLevelUpPopup);
                    break;
                }

            case TutorialExplain.CharacterDetailEquipmentSlot:
                {
                    View.ReinforceMentUnit.GetEquipmentSlotUnit(EquipmentType.Slot1).OnClickSlot();
                    await TutorialManager.WaitUntilEnterUI(UIType.EquipmentManagementPopup);
                    break;
                }

            case TutorialExplain.CharacterDetailIntroduce:
                {
                    View.OnClickCharacterIntroduceView();
                    break;
                }

            case TutorialExplain.CharacterDetailHeadKnuckleTab:
                {
                    View.ReinforceMentUnit.OnClickHeadKnuckle();
                    break;
                }

            case TutorialExplain.CharacterDetailHeadKnuckleSlot:
                {
                    View.ReinforceMentUnit.HeadKnuckleSlotUnit.OnClickSlot();
                    await TutorialManager.WaitUntilEnterUI(UIType.EquipmentManagementPopup);
                    break;
                }
        }
    }

    public override async UniTask<GameObject> GetTutorialFocusObject(string stepKey)
    {
        await UniTask.WaitUntil(() => { return !View.IsPlayingAnimation; });

        TutorialExplain stepType = (TutorialExplain)Enum.Parse(typeof(TutorialExplain), stepKey);

        switch (stepType)
        {
            case TutorialExplain.CharacterDetailSkillSlot:
                {
                    return View.ReinforceMentUnit.GetSkillItemObject(0).gameObject;
                }

            default:
                return await base.GetTutorialFocusObject(stepKey);
        }
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case ViewObserverID.Refresh:
                {
                    Model.SetChangeCharacterStat();
                    Model.SetCachingStats();

                    View.RefreshAsync().Forget();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}