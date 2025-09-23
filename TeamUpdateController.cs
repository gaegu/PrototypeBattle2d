//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//=========================================================================================================

public class TeamUpdateController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.TeamUpdateView; } }
    private TeamUpdateView View { get { return base.BaseView as TeamUpdateView; } }
    protected TeamUpdateViewModel Model { get; private set; }
    public TeamUpdateController() { Model = GetModel<TeamUpdateViewModel>(); }
    public TeamUpdateController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool needRefresh = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetEventReset(OnEventReset);
        Model.SetEventFilter(OnEventFilter);
        Model.SetEventSave(OnEventSave);
        Model.SetEventHome(OnEventHome);
        Model.SetEventBack(OnEventBack);

        Model.SetEventChangePresetNumber(OnEventChangePresetNumber);
        Model.SetEventCharacterSelect(OnEventCharacterSelect);
        Model.SetEventCharacterHold(OnEventCharacterHold); // 스위핑으로 왔을때는 홀드 안되게
        Model.SetEventTeamFormationDragAndDrop(OnEventTeamFormationDragAndDrop);
        Model.SetEventPresetCharacterSlot(OnEventPresetCharacterSlotSelect);
        Model.SetEventPresetCharacterSelect(OnEventPresetCharacterSelect);
        Model.SetEventPresetCharacterHold(OnEventPresetCharacterHold);

        Model.SetFuncCheckChangeAsync(CheckChangedDeckAsync);
        Model.SetEventArenaToggle(OnEventArenaToggle);

        if (Model.CurrentDeckType == DeckType.ArenaDefence)
            Model.SetArenaToggle(true);

        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetTeamGroup();
        Model.SetDungeonFixedTeam();

        Model.SetFilterModel(GetFilterType());

        Model.SortingModel.SetEventAutoFormation(OnEventAutoFormation);
        Model.SortingModel.SetEventSorting(OnEventSorting);
        Model.SortingModel.SetEventSortingOrder(OnEventSortingOrder);
        Model.SortingModel.SetSortingType(typeof(CharacterSortingType));
        Model.SortingModel.SetOrderType(SortingOrderType.Descending);
        Model.SortingModel.SetGoodsSortingValueTypes(GoodsType.Character, CharacterSortingType.Power);

        Model.SetFilteredCharacterModel();
        Model.SetThumbnailCharacterUnitModel();
        Model.SetSelectIncludedThumbnailCharacter();
        Model.SetSelectCharacter(null);

        Model.SortCharacterThumbnail();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        await TeamUpdateExitAsync();

        needRefresh = false;

        return true;
    }

    /// <summary> BACK으로 나간 경우 </summary>
    private async UniTask TeamUpdateExitAsync()
    {
        Func<UniTask> funcShowSaveMessage = async () =>
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPGRADEVIEW_NOTICE_SAVE_COMPLETE);
        };
        Func<UniTask> funcShowCancelMessage = async () =>
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPGRADEVIEW_NOTICE_SAVE_CANCELED);
        };

        // 현재 팀 편성 변경점이 있는지 체크.
        if (Model.CheckChangeTeam())
        {
            // 현재 팀 편성이 덱 타입별 올바른 덱으로 구성되어 있는지 체크
            if (Model.TeamGroup.CheckValidDeck(Model.CurrentDeckType))
            {
                if (await ShowMessageBoxCheckChange())
                {
                    if (Model.IsFixedTeam)
                    {
                        Model.SaveFixedTeam();
                    }
                    else
                    {
                        PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(Model.CurrentDeckType).SetPresetNumber(Model.TeamGroup.PresetNumber);

                        if (NetworkManager.Web.IsOffline)
                        {
                            LocalSave();
                        }
                        else
                        {
                            await RequestManageDeck(Model.TeamGroup);
                        }
                    }

                    UIManager.Instance.Exit(this, async (state) =>
                    {
                        if (state == UISubState.Finished)
                            await funcShowSaveMessage();
                    }).Forget();
                }
                else
                {
                    UIManager.Instance.Exit(this, async (state) =>
                    {
                        if (state == UISubState.Finished)
                            await funcShowCancelMessage();
                    }).Forget();
                }
            }
        }
        else
        {
            //가장 마지막에 세팅한 프리셋으로 번호 변경.
            PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(Model.CurrentDeckType)
                .SetPresetNumber(Model.TeamGroup.PresetNumber);

            await UIManager.Instance.Exit(this, async (state) =>
            {
                if (state == UISubState.Finished)
                    await funcShowCancelMessage();
            });
        }
    }

    private async UniTask<bool> RequestManageDeck(TeamGroup teamGroup)
    {
        return await RequestManageDefaultDeck(teamGroup);
    }

    private async UniTask<bool> RequestManageDefaultDeck(TeamGroup teamGroup)
    {
        BaseProcess deckManageProcess = NetworkManager.Web.GetProcess(WebProcess.DeckManage);
        ManageDeckInDto dto = new ManageDeckInDto();
        Team changedTeam = teamGroup.GetCurrentTeam();

        dto.presetNumber = Model.TeamGroup.PresetNumber;

        dto.slots = new int[IntDefine.MAX_TEAM_CHARACTER_SLOT_COUNT];
        for (int i = 0; i < dto.slots.Length; i++)
            dto.slots[i] = changedTeam.GetCharacterByIndex(i) == null ? 0 : changedTeam.GetCharacterByIndex(i).Id;

        dto.type = Model.CurrentDeckType;

        deckManageProcess.SetPacket(dto);

        if (await deckManageProcess.OnNetworkAsyncRequest())
        {
            deckManageProcess.OnNetworkResponse();
            View.RefreshPreset();

            return true;
        }

        return false;
    }

    private CharacterFilterType GetFilterType()
    {
        switch (Model.CurrentDeckType)
        {
            case DeckType.InfinityDungeonBlack:
            case DeckType.InfinityDungeonBlue:
            case DeckType.InfinityDungeonPrism:
                return CharacterFilterType.TeamUpdateExceptLicense;

            default:
                return CharacterFilterType.CharacterManager;
        }
    }

    public override async UniTask LoadingProcess()
    {
        //재실행시 선택중 상태라면 해제
        if (View != null)
            View.HighlightSelectionOnOff(false);
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetTeamGroup();

        View.ShowAsync().Forget();
        View.HighlightSelectionOnOff(false);

        needRefresh = false;
    }

    public override async UniTask BackProcess()
    {
        if (needRefresh)
            Refresh();
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.TeamUpdateAuto:
                {
                    await OnEventAutoFormation();
                    break;
                }
            case TutorialExplain.TeamUpdateSave:
                {
                    await OnEventSave();
                    await TutorialManager.WaitUntilEnterUI(UIType.StageInfoWindow);      //현재 팀편성 튜토리얼은 StageInfoPopup에서 접근. 추후 타 루트에서 들어오는 팀편성이 추가되면 구분 필요.
                    UIManager.Instance.GetController(UIType.StageInfoWindow).GetPrefab().SafeSetActive(true);        //튜토리얼 진행중 Back으로 Popup이 열린 경우 꺼진상태로 유지되서 추가
                    break;
                }
        }
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_WINDOWS + "TeamUpdate/TeamUpdateView";
    }

    #region OnEvent
    private void OnEventReset()
    {
        if (Model.IsSelectCharacter)
            return;

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

    private void OnEventFilter()
    {
        if (Model.IsSelectCharacter)
            return;

        BaseController filterController = UIManager.Instance.GetController(UIType.FilterPopup);
        FilterPopupModel model = filterController.GetModel<FilterPopupModel>();

        model.SetEventConfirm(OnEventChangeFilter);
        model.SetFilterModel(Model.FilterModel);

        UIManager.Instance.EnterAsync(filterController).Forget();
    }

    private async UniTask OnEventSave()
    {
        if (Model.IsSelectCharacter)
            return;

        if (Model.CheckChangeTeam())
        {
            if (Model.IsFixedTeam)
            {
                Model.SaveFixedTeam();
            }
            else
            {
                if (NetworkManager.Web.IsOffline)
                {
                    LocalSave();
                }
                else if (!await RequestManageDeck(Model.TeamGroup))
                {
                    // 저장 실패 시 원래 팀그룹 상태로 되돌린다.
                    Model.SetTeamGroup();
                    return;
                }
            }
        }

        //팀편성 앱이 아닌 다른 컨텐츠에서 들어왔다면 나가기
        if (Model.CurrentDeckType != DeckType.Story)
        {
            PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(Model.CurrentDeckType).SetPresetNumber(Model.TeamGroup.PresetNumber);
            UIManager.Instance.Exit(this, async (state) =>
            {
                if (state == UISubState.Finished)
                    MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPGRADEVIEW_NOTICE_SAVE_COMPLETE);
            }).Forget();
        }
        else
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPGRADEVIEW_NOTICE_SAVE_COMPLETE);
        }
    }

    private void OnEventHome()
    {
        if (Model.IsSelectCharacter)
            return;

        ObserverManager.NotifyObserver(ViewObserverID.Home, null);
    }

    private void OnEventBack()
    {
        if (Model.IsSelectCharacter)
            return;

        TeamUpdateExitAsync().Forget();
    }

    private void OnEventChangePresetNumber(int index)
    {
        if (Model.IsSelectCharacter)
            return;

        if (Model.TeamGroup.PresetNumber == index)
            return;

        TaskEventChangePreset(index).Forget();
    }

    private ThumbnailSelectType OnEventCharacterSelect(BaseThumbnailUnitModel model)
    {
        if (model == null)
        {
            return ThumbnailSelectType.None;
        }

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
                break;

            case TeamUpdateViewModel.SelectCharacterType.Remove:
                Model.SetSelectCharacter(character);
                Model.RemovePresetCharacterByKey();
                Model.SetUnSelectThumbnailCharacter();
                Model.SetSelectCharacter(null);

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);
                break;

            case TeamUpdateViewModel.SelectCharacterType.SelectChange:
                Model.SetSelectCharacter(character);
                Model.SetImpossibleThumbnailCharacter();
                Model.SetSelectThumbnailCharacter();
                Model.SetSelectThumbnailUnitModel(model);

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);
                View.HighlightSelectionOnOff(true);
                break;

            case TeamUpdateViewModel.SelectCharacterType.Cancel:
                Model.InitSelectStateAllThumbnailCharacter();
                Model.SetSelectIncludedThumbnailCharacter();
                Model.SetSelectCharacter(null);
                Model.SetSelectThumbnailUnitModel(null);

                View.RefreshPreset();
                View.ShowCharacterScroll(isReset: false);
                View.HighlightSelectionOnOff(false);
                break;

            default:
                break;
        }

        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventCharacterHold(BaseThumbnailUnitModel model)
    {
        if (Model.IsSelectCharacter)
            return ThumbnailSelectType.None;

        if (model == null)
            return ThumbnailSelectType.None;

        if (model.Goods == null)
            return ThumbnailSelectType.None;

        if (UIManager.Instance.CheckOpenUI(UIType.StageDungeonView, true))
            return ThumbnailSelectType.None;

        TaskCharacterDetailAsync(model).Forget();

        return ThumbnailSelectType.None;
    }

    private void OnEventTeamFormationDragAndDrop(int dragIndex, int dropIndex)
    {
        if (Model.IsSelectCharacter)
            return;

        Model.SetSelectCharacter(null);
        Model.ChangePresetCharacter(dragIndex, dropIndex);

        View.RefreshPreset();
        View.ShowCharacterScroll(isReset: false);
    }

    private void OnEventPresetCharacterSlotSelect(int selectPresetSlot)
    {
        // 리스트가 열려있을 경우에는 타입에 따라
        // 제거/변경 등
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
                    View.HighlightSelectionOnOff(false);
                    break;
                }

            default:
                {
                    return;
                }
        }
    }

    private ThumbnailSelectType OnEventPresetCharacterSelect(BaseThumbnailUnitModel model)
    {
        return ThumbnailSelectType.None;
    }

    private ThumbnailSelectType OnEventPresetCharacterHold(BaseThumbnailUnitModel model)
    {
        return OnEventCharacterHold(model);
    }

    private void OnEventChangeFilter()
    {
        if (Model.IsSelectCharacter)
            return;

        if (!Model.FilterModel.CheckChangeFilter())
            return;

        Model.FilterModel.SaveFilter();
        Model.SetThumbnailCharacterUnitModel();

        // 현재 프리셋에 선택 정보 설정
        Model.SetSelectIncludedThumbnailCharacter();
        Model.SetSelectCharacter(null);
        Model.SortCharacterThumbnail();

        View.ShowAsync().Forget();
    }

    private async UniTask<bool> CheckChangedDeckAsync()
    {
        if (Model.IsSelectCharacter)
            return false;

        if (Model.CheckChangeTeam())
        {
            // 현재 팀 편성이 덱 타입별 올바른 덱으로 구성되어 있는지 체크
            if (Model.TeamGroup.CheckValidDeck(Model.CurrentDeckType))
            {
                if (await ShowMessageBoxCheckChange())
                    await RequestManageDeck(Model.TeamGroup);
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private void OnEventArenaToggle(bool isAttack)
    {
        DeckType deckType = isAttack ? DeckType.ArenaAttack : DeckType.ArenaDefence;

        Model.SetCurrentDeckType(deckType);

        Model.SetTeamGroup();
        Model.SetFilteredCharacterModel();
        Model.SetThumbnailCharacterUnitModel();
        Model.SetSelectIncludedThumbnailCharacter();
        Model.SetSelectCharacter(null);

        Model.SortCharacterThumbnail();

        View.RefreshDeckType();
    }

    #region SortingModel Event
    // 자동편성 시작
    private async UniTask OnEventAutoFormation()
    {
        // 1. 기존 프리셋에 선택된 정보 해제
        Model.InitSelectStateAllThumbnailCharacter();
        Model.SetSelectCharacter(null);
        View.HighlightSelectionOnOff(false);

        // 자동 편성
        Model.SetAutoFormation();

        // 바뀐게 있을 경우에 저장
        if (Model.CheckChangeTeam())
        {
            if (Model.IsFixedTeam)
            {
                Model.SaveFixedTeam();
            }
            else
            {
                if (NetworkManager.Web.IsOffline)
                {
                    LocalSave();
                }
                else if (!await RequestManageDeck(Model.TeamGroup))
                {
                    // 저장 실패 시 원래 팀그룹 상태로 되돌린다.
                    Model.SetTeamGroup();
                    return;
                }
            }
        }

        // 현재 프리셋에 선택 정보 설정
        Model.SetSelectIncludedThumbnailCharacter();
        Model.SortCharacterThumbnail();

        View.RefreshAutoFormation();

        MessageBoxManager.ShowToastMessage(
            LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPGRADEVIEW_NOTICE_SAVE_COMPLETE);
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

    #endregion SortingModel Event

    #endregion OnEvent
    private async UniTask TaskCharacterDetailAsync(BaseThumbnailUnitModel model)
    {
        // 현재 팀 편성 변경점이 있는지 체크.
        if (Model.CheckChangeTeam())
        {
            // 현재 팀 편성이 덱 타입별 올바른 덱으로 구성되어 있는지 체크
            if (Model.TeamGroup.CheckValidDeck(Model.CurrentDeckType))
            {
                if (await ShowMessageBoxCheckChange())
                    await RequestManageDeck(Model.TeamGroup);
            }
        }

        //UIManager.Instance.AddDelayedTransitionEvent(() => AdditivePrefabManager.Instance.UnLoadAsync().Forget());

        //가장 마지막에 세팅한 프리셋으로 번호 변경.
        PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(Model.CurrentDeckType).SetPresetNumber(Model.TeamGroup.PresetNumber);

        // 캐릭터 상세창으로 이동
        BaseController characterDetailController = UIManager.Instance.GetController(UIType.CharacterDetailView);
        CharacterDetailViewModel viewModel = characterDetailController.GetModel<CharacterDetailViewModel>();
        viewModel.SetUser(Model.User);
        viewModel.SetCharacter(model.Goods as Character);
        viewModel.SetIntroduceButton(true);

        await UIManager.Instance.EnterAsync(characterDetailController);

        needRefresh = true;
    }

    private async UniTask TaskEventChangePreset(int index)
    {
        if (Model.CheckChangeTeam())
        {
            if (Model.TeamGroup.CheckValidDeck(Model.CurrentDeckType))
            {
                if (await ShowMessageBoxCheckChange())
                    await RequestManageDeck(Model.TeamGroup);
            }
        }

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
        View.HighlightSelectionOnOff(false);
        View.ShowCharacterScroll(isReset: false);
    }

    private async UniTask<bool> ShowMessageBoxCheckChange(Action callBack = null)
    {
        TeamGroup changedTeamGroup = new TeamGroup();
        changedTeamGroup.Copy(Model.TeamGroup);

        bool change = false;
        bool isCallBack = false;
        await MessageBoxManager.ShowYesNoBox(LocalizationDefine.LOCALIZATION_UI_LABEL_TEAMUPGRADEVIEW_NOTICE_SAVE,
            onEventConfirm: () =>
            {
                change = true;
                isCallBack = true;
                if (callBack != null)
                    callBack.Invoke();
            },
            onEventCancel: () =>
            {
                change = false;
                isCallBack = true;

                Model.RevertCurrentTeam();
            });

        await UniTask.WaitUntil(() => isCallBack);

        return change;
    }

    private void LocalSave()
    {
        bool isNew = false;

        Team team = PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(Model.CurrentDeckType)
            .GetTeamByIndex(Model.TeamGroup.PresetNumber);

        if (team == null)
        {
            isNew = true;
            team = new Team(Model.TeamGroup.GetCurrentTeam().ID, Model.TeamGroup.PresetNumber);
        }

        team.Clear();

        for (int i = 0; i < IntDefine.MAX_TEAM_CHARACTER_SLOT_COUNT; i++)
        {
            Character character = PlayerManager.Instance.MyPlayer.User.CharacterModel.
                GetGoodsById(Model.TeamGroup.GetCurrentTeam().GetCharacterByIndex(i).Id);

            team.SetCharacter(character, i);
        }

        if (isNew)
            PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(Model.CurrentDeckType).AddTeam(team);

        View.RefreshPreset();
    }
    #endregion Coding rule : Function
}