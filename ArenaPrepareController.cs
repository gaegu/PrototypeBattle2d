//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ArenaPrepareController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ArenaPrepareView; } }
    public override void SetModel() { SetModel(new ArenaPrepareViewModel()); }
    private ArenaPrepareView View { get { return base.BaseView as ArenaPrepareView; } }
    private ArenaPrepareViewModel Model { get { return GetModel<ArenaPrepareViewModel>(); } }
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
        Model.SetEventChangePresetNumber(OnEventChangePresetNumber);
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetEventCharacterHold(OnEventCharacterHold);
        Model.SetEventTeamFormationDragAndDrop(OnEventTeamFormationDragAndDrop);
        Model.SetEventPresetCharacterSlot(OnEventPresetCharacterSlotSelect);
        Model.SetEventPresetCharacterHold(OnEventPresetCharacterHold);
        Model.SetEventReset(OnEventReset);
        Model.SetEventBattle(OnEventBattle);

        Model.SortingModel.SetEventSorting(OnEventSorting);
        Model.SortingModel.SetEventSortingOrder(OnEventSortingOrder);
        Model.SortingModel.SetSortingType(typeof(CharacterSortingType));
        Model.SortingModel.SetOrderType(SortingOrderType.Descending);
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, CharacterSortingType.Power);

        Model.SetMyInfo(PlayerManager.Instance.MyPlayer.User);

        Model.SetSelectIncludedThumbnailCharacter();
        Model.SetSelectCharacter(null);

        Model.SortCharacterThumbnail();
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

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Arena/ArenaPrepareView";
    }


    private void OnEventChangePresetNumber(int index)
    {
        if (Model.TeamGroup.PresetNumber == index)
            return;

        TaskEventChangePreset(index).Forget();
    }

    private async UniTask TaskEventChangePreset(int index)
    {
        // 기존 프리셋에 선택된 정보들 해제
        Model.SetUnSelectThumbnailCharacter();
        Model.SetUnSelectIncludedThumbnailCharacter();

        // 현재 팀그룹 새로고침, 프리셋 변경
        Model.SetTeamGroup();
        Model.SetOpenPresetNumber(index);

        // 현재 프리셋에 선택 정보 설정
        Model.SetSelectIncludedThumbnailCharacter();
        Model.SetSelectCharacter(null);
        Model.SortCharacterThumbnail();

        View.RefreshPreset();
        View.ShowCharacterScroll(isReset: false);
    }


    private ThumbnailSelectType OnEventCharacterSelect(BaseThumbnailUnitModel model)
    {
        Character character = Model.User.CharacterModel.GetGoodsById(model.Id);

        // 1. 슬롯에 같은 캐릭터가 있는지 확인
        TeamUpdateViewModel.SelectCharacterType presetChangeType = Model.GetSelectCharacterType(character.Id);

        switch (presetChangeType)
        {
            case TeamUpdateViewModel.SelectCharacterType.Add:
                Model.SetSelectCharacter(character);
                Model.AddPresetCharacter(Model.GetEmptySlotIndex());
                Model.SetSelectIncludedThumbnailCharacter();
                Model.SetSelectCharacter(null);

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);

                //MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPDATEVIEW_NOTICE_SAVE_COMPLETE);
                break;

            case TeamUpdateViewModel.SelectCharacterType.Remove:
                Model.SetSelectCharacter(character);
                Model.RemovePresetCharacterByKey();
                Model.SetUnSelectThumbnailCharacter();
                Model.SetSelectCharacter(null);

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);

                //MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPDATEVIEW_NOTICE_SAVE_COMPLETE);
                break;

            case TeamUpdateViewModel.SelectCharacterType.SelectChange:
                Model.SetSelectCharacter(character);
                Model.SetImpossibleThumbnailCharacter();
                Model.SetSelectThumbnailCharacter();

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);

                //MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPDATEVIEW_NOTICE_SAVE_COMPLETE);
                break;

            case TeamUpdateViewModel.SelectCharacterType.Cancel:
                Model.InitSelectStateAllThumbnailCharacter();
                Model.SetSelectIncludedThumbnailCharacter();
                Model.SetSelectCharacter(null);

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);
                break;

            default:
                break;
        }

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventCharacterHold(BaseThumbnailUnitModel model)
    {
        if (model == null)
            return ThumbnailSelectType.None;

        if (model.Goods == null)
            return ThumbnailSelectType.None;

        BaseController characterDetailController = UIManager.Instance.GetController(UIType.CharacterDetailView);
        CharacterDetailViewModel viewModel = characterDetailController.GetModel<CharacterDetailViewModel>();
        viewModel.SetUser(Model.User);
        viewModel.SetCharacter(model.Goods as Character);
        viewModel.SetIntroduceButton(true);
        UIManager.Instance.EnterAsync(characterDetailController).Forget();

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventPresetCharacterHold(BaseThumbnailUnitModel model)
    {
        return OnEventCharacterHold(model);
    }

    private void OnEventPresetCharacterSlotSelect(int selectPresetSlot)
    {
        TeamUpdateViewModel.SelectPresetCharacterType selectPresetCharacterType = Model.CheckChangePresetCharacter(selectPresetSlot);

        switch (selectPresetCharacterType)
        {
            case TeamUpdateViewModel.SelectPresetCharacterType.Remove:
                {
                    Model.SetUnSelectIncludedThumbnailCharacter();
                    Model.RemovePresetCharacterByIndex(selectPresetSlot);
                    Model.SetSelectCharacter(null);
                    Model.SetSelectIncludedThumbnailCharacter();
                    Model.SortCharacterThumbnail();

                    View.RefreshPreset();
                    View.ShowCharacterScroll(isReset: false);
                    break;
                }

            case TeamUpdateViewModel.SelectPresetCharacterType.Change:
                {
                    // 기존 프리셋에 선택된 정보들 해제
                    Model.InitSelectStateAllThumbnailCharacter();
                    Model.RemovePresetCharacterByIndex(selectPresetSlot);

                    // 현재 프리셋에 선택 캐릭터 정보 설정
                    Model.AddPresetCharacter(selectPresetSlot);
                    Model.SetSelectCharacter(null);
                    Model.SetSelectIncludedThumbnailCharacter();
                    Model.SortCharacterThumbnail();

                    View.RefreshPreset();
                    View.ShowCharacterScroll(isReset: false);
                    break;
                }

            default:
                {
                    return;
                }
        }
        //MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPDATEVIEW_NOTICE_SAVE_COMPLETE);
    }

    private void OnEventTeamFormationDragAndDrop(int dragIndex, int dropIndex)
    {
        Model.SetSelectCharacter(null);
        Model.ChangePresetCharacter(dragIndex, dropIndex);

        View.RefreshPreset();
        View.ShowCharacterScroll(isReset: false);

        //MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPDATEVIEW_NOTICE_SAVE_COMPLETE);
    }

    private void OnEventSorting(System.Enum sortingType)
    {
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, sortingType);
        Model.SortCharacterThumbnail();

        View.ShowCharacterScroll(isReset: true);
    }

    private void OnEventSortingOrder(SortingOrderType orderType)
    {
        Model.SortingModel.SetOrderType(orderType);
        Model.SortCharacterThumbnail();

        View.ShowCharacterScroll(isReset: true);
    }

    private void OnEventReset()
    {
        if (Model.IsAllEmptySlot())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.UI_LABEL_TEAMUPGRADEVIEW_NEED_TEAMSET);
            return;
        }

        // 현재 프리셋에 선택된 정보들 해제
        Model.InitSelectStateAllThumbnailCharacter();
        Model.SetSelectCharacter(null);
        // 프리셋 리셋
        Model.ResetCurrentTeam();

        // 리셋된 프리셋에 선택 정보 설정
        Model.SetSelectIncludedThumbnailCharacter();
        Model.SortCharacterThumbnail();

        View.RefreshPreset();
        View.ShowCharacterScroll(isReset: true);

        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPDATEVIEW_NOTICE_RESET);
    }


    private void OnEventBattle()
    {
        TaskEnterBattle().Forget();
    }

    private async UniTask TaskEnterBattle()
    {
        if (Model.TeamGroup.GetCurrentTeam().IsAllEmptySlot())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_ARENA_TAG_EMPTY_TEAM);
            return;
        }

        if (Model.CheckChangeTeam(Model.TeamGroup.PresetNumber))
            await RequestManageDeck(Model.TeamGroup.PresetNumber);

    }

    private async UniTask RequestManageDeck(int presetNumber)
    {
        BaseProcess deckManageProcess = NetworkManager.Web.GetProcess(WebProcess.DeckManage);
        ManageDeckInDto dto = new ManageDeckInDto();
        Team changedTeam = Model.TeamGroup.GetTeamByIndex(presetNumber);

        dto.presetNumber = presetNumber;

        dto.slots = new int[IntDefine.MAX_TEAM_CHARACTER_SLOT_COUNT];
        for (int i = 0; i < dto.slots.Length; i++)
            dto.slots[i] = changedTeam.GetCharacterByIndex(i) == null ? 0 : changedTeam.GetCharacterByIndex(i).Id;

        dto.type = DeckType.ArenaAttack;

        deckManageProcess.SetPacket(dto);

        if (await deckManageProcess.OnNetworkAsyncRequest())
        {
            deckManageProcess.OnNetworkResponse();
            View.RefreshPreset();

            UIType arenaUI = UIType.ArenaView;
            if (UIManager.Instance.CheckOpenUI(arenaUI))
                UIManager.Instance.GetController(arenaUI).Refresh();
        }
    }
    #endregion Coding rule : Function
}