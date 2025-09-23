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
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ReferenceCheckController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ReferenceCheckView; } }
    public override void SetModel() { SetModel(new ReferenceCheckViewModel()); }
    private ReferenceCheckView View { get { return base.BaseView as ReferenceCheckView; } }
    private ReferenceCheckViewModel Model { get { return GetModel<ReferenceCheckViewModel>(); } }
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
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetCharacterQuestProgressModel(MissionManager.Instance.GetProgressModel<CharacterQuestProgressModel>());
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetFilterModel(OnEventFilter);
        Model.SetSortingModels(OnEventSorting, OnEventSortingOrder);
        Model.SetThumbnailCharacterUnitModel();
        Model.SortCharacterThumbnail();
    }

    public override void BackEnter()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await UIManager.Instance.Exit(this);
    }

    public override async UniTask LoadingProcess()
    {
        PlayTownBGM();
    }

    public override async UniTask Process()
    {
        PlayAsync(CharacterCollectionViewModel.PlayableState.ShowOnce).Forget();

        await View.ShowAsync();
    }

    public override async UniTask BackProcess()
    {
        await View.ShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Story/ReferenceCheckView";
    }

    private void PlayTownBGM()
    {
        FieldMapTable fieldMapTable = TableManager.Instance.GetTable<FieldMapTable>();
        FieldMapTableData fieldMapTableData = fieldMapTable.GetDataByID((int)PlayerManager.Instance.MyPlayer.CurrentFieldMap);

        if (fieldMapTableData.IsNull())
            return;

        //GroundType groundType = (GroundType)fieldMapTableData.GetGROUND_TYPE();
        SoundManager.PlayTownBGM(GroundType.MidGround).Forget();
    }

    private ThumbnailSelectType OnEventCharacterSelect(BaseThumbnailUnitModel model)
    {
        var characterModel = model as BaseThumbnailCharacterUnitModel;
        if (!characterModel.IsHave)
            return ThumbnailSelectType.None;

        BaseController controller = UIManager.Instance.GetController(UIType.ReferenceCheckDetailView);
        var viewModel = controller.GetModel<ReferenceCheckDetailViewModel>();
        viewModel.SetCharacter(model.Goods as Character);
        UIManager.Instance.EnterAsync(controller).Forget();

        return ThumbnailSelectType.None;
    }

    /// <summary>
    /// 정렬 적용 (정렬 타입 변경)
    /// </summary>
    private async void OnEventSorting(Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.SortCharacterThumbnail();

        await View.ShowCharacterScroll();
    }

    /// <summary>
    /// 정렬 적용 (오름차순/내림차순)
    /// </summary>
    private async void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortCharacterThumbnail();

        await View.ShowCharacterScroll();
    }

    /// <summary>
    /// 필터 팝업 오픈
    /// </summary>
    private void OnEventFilter()
    {
        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnEventChangeFilter);
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }

    /// <summary>
    /// 필터 적용
    /// </summary>
    private async void OnEventChangeFilter()
    {
        if (!Model.FilterModel.CheckChangeFilter())
            return;

        Model.FilterModel.SaveFilter();
        Model.SetThumbnailCharacterUnitModel();
        Model.SortCharacterThumbnail();

        await View.ShowCharacterScroll();
    }
    #endregion Coding rule : Function
}