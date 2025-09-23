//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using Cysharp.Threading.Tasks;
using IronJade; // UniTask 관련 클래스 모음
using IronJade.UI.Core;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CharacterManagerController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CharacterManagerView; } }
    public override void SetModel() { SetModel(new CharacterManagerViewModel()); }
    private CharacterManagerView View { get { return base.BaseView as CharacterManagerView; } }
    private CharacterManagerViewModel Model { get { return GetModel<CharacterManagerViewModel>(); } }
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
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);

        Model.SetEventDetail(OnEventDetail);
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetEventCharacterHold(OnEventCharacterHold);
        Model.SetEventChangeLeader(OnEventChangeLeader);
        Model.SetEventFilter(OnEventFilter);
        Model.SetSortingModels(OnEventSorting, OnEventSortingOrder);
        Model.SetEventRanking(OnEventRanking);

        Model.SetFilterModel(CharacterFilterType.CharacterManager);
        Model.SetThumbnailCharacterUnitModel();
        Model.SortCharacterThumbnail();

        Model.SetRankingOpen(ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_RANKING));
        Model.SetSelectThumbnailCharacter(Model.GetThumbnailCharacterUnitModelBySlotIndex(0));

        Model.SetSelectIndex();
    }

    public override void BackEnter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetThumbnailCharacterUnitModel();
        Model.SortCharacterThumbnail();
    }

    public override async UniTask BackProcess()
    {
        await View.RefreshAsync();
    }

    public override async UniTask LoadingProcess()
    {
        ForcedSelectCharacterByTutorial();

        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterSignature);

        var signatureUnit = AdditivePrefabManager.Instance.SignatureUnit;
        signatureUnit.Model.SetCharacter(Model.SelectCharacter);
        signatureUnit.Model.SetUIType(UIType.CharacterManagerView);
        await signatureUnit.ShowAsync();
    }

    public override async UniTask LoadingBackProcess()
    {
        if (AdditivePrefabManager.Instance.SignatureUnit == null)
        {
            await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.CharacterSignature);
            var signatureUnit = AdditivePrefabManager.Instance.SignatureUnit;
            signatureUnit.Model.SetCharacter(Model.SelectCharacter);
            signatureUnit.Model.SetUIType(UIType.CharacterManagerView);
        }

        await AdditivePrefabManager.Instance.SignatureUnit.ShowAsync();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await base.Exit(onEventExtra);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        isWaitChangeCharacter = false;

        if (Model.CheckLevelUp())
        {
            Model.UpdateThumbnailCharacterUnitModel();
            View.ShowAsync().Forget();
        }
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Character/CharacterManagerView";
    }

    protected virtual async UniTask ShowDetailView()
    {
        ThumbnailCharacterUnitModel thumbnailCharacterUnitModel = Model.SelectThumbnailCharacterUnitModel as ThumbnailCharacterUnitModel;

        if (thumbnailCharacterUnitModel == null)
        {
           IronJade.Debug.LogError("thumbnailCharacterUnitModel is Null");
            return;
        }

        Character character = thumbnailCharacterUnitModel.Character;

        if (character == null)
            return;

        BaseController characterDetailController = UIManager.Instance.GetController(UIType.CharacterDetailView);
        CharacterDetailViewModel viewModel = characterDetailController.GetModel<CharacterDetailViewModel>();
        viewModel.SetCharacter(character);
        viewModel.SetEventBackToCharacterManager(OnEventLoadSelectCharacter);
        viewModel.SetCharacterList(Model.GetSortCharacterLists());
        viewModel.SetIntroduceButton(true);

        await UIManager.Instance.EnterAsync(characterDetailController);
    }

    protected async void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.SortCharacterThumbnail();

        await View.ShowCharacterScroll(isReset: true);
    }

    protected async void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortCharacterThumbnail();

        await View.ShowCharacterScroll(isReset: true);
    }

    private void OnEventRanking()
    {
        ContentsOpenManager.Instance.OpenContents(ContentsType.Ranking).Forget();
    }

    protected virtual ThumbnailSelectType OnEventCharacterSelect(BaseThumbnailUnitModel model)
    {
        if (Model.SelectThumbnailCharacterUnitModel == model)
            return ThumbnailSelectType.None;

        TaskSelectCharacter(model).Forget();
        View.CharacterThumbnailRefresh();

        return ThumbnailSelectType.None;
    }

    /// <summary> 타 UI에서 변경한 현재 선택 캐릭터를 넘겨받는 함수</summary>
    private void OnEventLoadSelectCharacter(int characterId)
    {
        Model.SetSelectThumbnailCharacter(characterId);
        Model.SetSelectIndex();
    }

    private async UniTask TaskSelectCharacter(BaseThumbnailUnitModel model)
    {
        if (isWaitChangeCharacter)
            return;

        isWaitChangeCharacter = true;

        Model.SetSelectThumbnailCharacter(model);
        Model.SetSelectIndex();

        View.ShowDetailButton();
        View.ShowLeaderIcon();
        View.OnPlaySelectCharacterAnimation();

        //캐릭터 선택 연출 재생
        var signatureUnit = AdditivePrefabManager.Instance.SignatureUnit;
        signatureUnit.Model.SetCharacter(Model.SelectCharacter);
        await signatureUnit.ShowAsync();

        isWaitChangeCharacter = false;
    }

    public override async UniTask PlayHideAsync()
    {
        base.PlayHideAsync().Forget();

        CameraManager.Instance.RestoreCharacterCameraCullingMask();
    }

    protected ThumbnailSelectType OnEventCharacterHold(BaseThumbnailUnitModel model)
    {
        return ThumbnailSelectType.None;
    }

    private void OnEventChangeLeader()
    {
        RequestChangeLeader().Forget();
    }

    private async UniTask RequestChangeLeader()
    {
        await PlayerManager.Instance.RequestChangeLeader(Model.SelectCharacter.Id, 0, async () =>
        {
            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetSelectThumbnailCharacter(Model.SelectThumbnailCharacterUnitModel);
            Model.SetSelectIndex();

            View.ShowLeaderIcon();
        });
    }

    protected void OnEventFilter()
    {
        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnEventChangeFilter);
        model.SetEventIsEmpty(() =>
        {
            return Model.CheckEmptyCharacterList();
        });
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }

    protected virtual void OnEventChangeFilter()
    {
        if (!Model.FilterModel.CheckChangeFilter())
            return;

        Model.FilterModel.SaveFilter();

        Model.SetThumbnailCharacterUnitModel();
        Model.SetSelectThumbnailCharacter(Model.GetThumbnailCharacterUnitModelBySlotIndex(0));
        Model.SetSelectIndex();

        TaskSelectCharacter(Model.SelectThumbnailCharacterUnitModel).Forget();
        View.SortingList();

        View.ShowHaveCount();
    }

    protected virtual void OnEventDetail()
    {
        if (View.IsPlayingAnimation)
            return;

        if (!Model.IsValidSelectCharacter)
            return;

        ShowDetailView().Forget();
    }

    /// <summary> 커넥트에서 시작되는 튜토리얼인 경우 튜토리얼에서 사용될 캐릭터를 고정합니다. </summary>
    private void ForcedSelectCharacterByTutorial()
    {
        if (TutorialManager.Instance.CheckTutorialPlaying())
        {
            var firstModel = Model.GetTutorialThumbnailCharacter();

            if (firstModel == null)
                firstModel = Model.GetThumbnailCharacterUnitModelBySlotIndex(0);

            Model.SetSelectThumbnailCharacter(firstModel);
        }
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.CharacterManagerViewDetail:
                {
                    await ShowDetailView();
                    await TutorialManager.WaitUntilEnterUI(UIType.CharacterDetailView);
                    break;
                }

            case TutorialExplain.CharacterManagerBack:
                {
                    await Exit();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}