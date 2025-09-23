//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class DungeonInfoController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.DungeonInfoView; } }
    public override void SetModel() { SetModel(new DungeonInfoViewModel()); }
    private DungeonInfoView View { get { return base.BaseView as DungeonInfoView; } }
    private DungeonInfoViewModel Model { get { return GetModel<DungeonInfoViewModel>(); } }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isRefreshList = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetViewMode(DungeonInfoViewModel.ViewModeState.TeamFormation);

        Model.SetTeamGroup();

        Model.SetChangeEventPresetNumber(OnEventChangePresetNumber);
        Model.SetEventToggleList(OnEventToggleList);
        Model.SetEventTeamFormationSelectSlot(OnEventTeamFormationSelectSlot);
        Model.SetEventPresetCharacterSlot(OnEventPresetCharacterSlotSelect);
        Model.SetEventClickBattleStart(UniTask.Action(OnEventClickBattleStart));
        Model.SetEventExit(OnEventExit);

        Model.SetThumbnailPresetCharacterUnitModel();
        Model.SetThumbnailCharacterUnitEnemyModel();
        Model.SetThumbnailRewardUnitModel();
    }

    public override void BackEnter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetTeamGroup();

        Model.SetThumbnailPresetCharacterUnitModel();

        isRefreshList = true;
    }

    public override async UniTask LoadingProcess()
    {
    }

    public override async UniTask Process()
    {
        View.ShowAsync().Forget();

        ShowListOpen().Forget();
    }

    public override void Refresh()
    {
        PlayDungeonBgm();
        View.RefreshAsync().Forget();

        if (isRefreshList)
        {
            ShowListOpen().Forget();
            isRefreshList = false;
        }
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        if (CheckPlayingAnimation())
            return true;

        if (Model.OnEventBeforeExit != null)
        {
            await Model.OnEventBeforeExit();
            Model.SetEventBeforeExit(null);
        }

        Model.SetListOpen(false);

        return await base.Exit(onEventExtra);
    }

    private async UniTask ShowListOpen()
    {
        if (!Model.IsListOpen)
            return;

        await UniTask.WaitUntil(() => CheckPlayingAnimation() == false);

        View.ShowListIcon();

        Model.SetViewMode(DungeonInfoViewModel.ViewModeState.StageInfo);
        PlayAsync(DungeonInfoViewModel.PlayableState.TeamFormationToStageInfo).Forget();
    }

    private void OnEventToggleList()
    {
        if (CheckPlayingAnimation())
            return;

        Model.SetListOpen(!Model.IsListOpen);
        View.ShowListIcon();

        switch (Model.ViewMode)
        {
            case DungeonInfoViewModel.ViewModeState.TeamFormation:
                {
                    Model.SetViewMode(DungeonInfoViewModel.ViewModeState.StageInfo);
                    PlayAsync(DungeonInfoViewModel.PlayableState.TeamFormationToStageInfo).Forget();
                    break;
                }

            case DungeonInfoViewModel.ViewModeState.StageInfo:
                {
                    Model.SetViewMode(DungeonInfoViewModel.ViewModeState.TeamFormation);
                    PlayAsync(DungeonInfoViewModel.PlayableState.StageInfoToTeamFormation).Forget();
                    break;
                }
        }
    }

    private void OnEventTeamFormationSelectSlot(int index)
    {
        OnEventPresetCharacterSlotSelect(0);
    }

    private void OnEventChangePresetNumber(int index)
    {
        if (Model.TeamGroup.PresetNumber == index)
            return;

        // 프리셋 변경
        User user = PlayerManager.Instance.MyPlayer.User;
        user.TeamModel.GetTeamGroupByType(Model.CurrentDeckType).SetPresetNumber(index);

        Model.SetUser(user);
        Model.SetTeamGroup();

        // 현재 프리셋에 선택 정보 설정
        Model.SetThumbnailPresetCharacterUnitModel();

        View.RefreshPreset();
    }

    private void OnEventPresetCharacterSlotSelect(int selectPresetSlot)
    {
        if (CheckPlayingAnimation())
            return;

        // 현재 프리셋을 User에 넘겨주고 TeamUpdateView를 열자.
        BaseController teamUpdateController = UIManager.Instance.GetController(UIType.TeamUpdateView);
        TeamUpdateViewModel viewModel = teamUpdateController.GetModel<TeamUpdateViewModel>();
        viewModel.SetApplication(false);
        viewModel.SetUser(Model.User);
        viewModel.SetDungeonTableData(Model.DungeonData);
        viewModel.SetCurrentDeckType(Model.CurrentDeckType);
        viewModel.SetOpenPresetNumber(Model.TeamGroup.PresetNumber);
        UIManager.Instance.EnterAsync(teamUpdateController, onEventExtra: async (state) =>
        {
            if (state == UISubState.AfterLoading)
                View.SafeSetActive(false);
        }).Forget();
    }

    private async UniTaskVoid OnEventClickBattleStart()
    {
        if (!CheckEnterable())
            return;

        bool isHaveStorySceneByDungeon = Model.DungeonData.GetSTART_STORY_VIEWCount() > 0 && Model.DungeonData.GetSTART_STORY_VIEW(0) > 0;

        TransitionType transitionType = (DungeonType)Model.DungeonData.GetDUNGEON_TYPE() switch
        {
            DungeonType.StageDungeon => TransitionType.Tram,
            DungeonType.InfinityCircuit => TransitionType.Rotation,
            _ => TransitionType.Rotation,
        };

        if (Model.IsHideAfterStart)
        {
            if (CheckPlayingAnimation())
                return;

            await NotifyMission();

            await TransitionManager.In(transitionType);

            Exit(async (state) =>
            {
                if (state == UISubState.Finished)
                {
                    Model.SetHideAfterStart(false);
                    await ShowBattleScene(transitionType);
                }
            }).Forget();
        }
        else
        {
            await NotifyMission();

            await ShowBattleScene(transitionType);
        }
    }

    private async void OnEventExit()
    {
        if (CheckPlayingAnimation())
            return;

        Exit().Forget();
    }

    private async UniTask ShowBattleScene(TransitionType endTransitionType)
    {
        Team team = Model.TeamGroup.GetCurrentTeam();
        if (Model.FixedTeam != null)
            team = Model.FixedTeam;

        switch (Model.DungeonType)
        {
            case DungeonType.InfinityCircuit:
                await BattleManager.Instance.EnterInfinityDungeonBattle(Model.ParentDungeonDataId, Model.DungeonData.GetID(), team);
                return;

            case DungeonType.StageDungeon:
                await BattleManager.Instance.EnterStageDungeonBattle(Model.ParentDungeonDataId, Model.DungeonData.GetID(), team);
                return;

            case DungeonType.ExStage:
                await BattleManager.Instance.EnterExStageDungeonBattle(Model.DungeonData.GetID(), 9700001, team);
                return;
            default:
                await BattleManager.Instance.EnterDungeonBattle(Model.DungeonData.GetID(), team, Model.RelatedMissionDataId);
                return;
        }
    }

    private bool CheckEnterable()
    {
        //1. 입장 가능 체크(이전 UI에서 입장가능 여부/입장 불가시 표기 메시지를 받아오는 경우)
        if (Model.IsBlockEnter)
        {
            MessageBoxManager.ShowToastMessage(Model.BlockMessage);
            return false;
        }

        //2. 입장 가능 체크(팀편성 조건 체크)
        Team team = Model.TeamGroup.GetCurrentTeam();

        if (team.IsAllEmptySlot())
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.UI_LABEL_BATTLEINFO_NEED_BATTLETEAM_SET);
            return false;
        }

        return true;
    }

    private async UniTask NotifyMission()
    {
        DungeonType dungeonType = Model.DungeonType;
        int dataDungeonId = Model.DungeonData.GetID();
        QuestCondition.Dungeon.DungeonClearType processType = QuestCondition.Dungeon.DungeonClearType.Try;

        await MissionManager.Instance.AddCountOnCondition(new QuestCondition.Dungeon(dungeonType, dataDungeonId, processType, 1));
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/DungeonInfoView";
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.StageInfoStart:
                {
                    await UniTask.WaitUntil(() => !CheckPlayingAnimation());
                    OnEventClickBattleStart().Forget();
                    break;
                }

            case TutorialExplain.StageInfoTeamUpdate:
                {
                    OnEventPresetCharacterSlotSelect(0);
                    await TutorialManager.WaitUntilEnterUI(UIType.TeamUpdateView);
                    break;
                }
        }
    }

    private void PlayDungeonBgm()
    {
        switch (Model.DungeonType)
        {
            case DungeonType.InfinityCircuit:
                {
                    SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_INFINITYCIRCUIT_BGM);
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}