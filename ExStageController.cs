//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;
using IronJade;
using IronJade.Table.Data; // UniTask 관련 클래스 모음
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ExStageController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ExStageView; } }
    public override void SetModel() { SetModel(new ExStageViewModel()); }
    private ExStageView View { get { return base.BaseView as ExStageView; } }
    private ExStageViewModel Model { get { return GetModel<ExStageViewModel>(); } }
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
        // 초기화
        // 유저정보
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        // 현재 장착중인 케릭터 정보를가져온다.
        Model.SetTeamGroup();

        // 현재 던전의 데이터를 불러온다. (현재 입장할려고 하는 던전ID를 알아야한다.
        Model.SetExStageTableData();

        Model.SetTotalPower();

        Model.SetTeamModelInfo();

        Model.SetEventPresetCharacterSlot(OnEventPresetCharacterSlotSelect);
        Model.SetEventBattleSceneStart(UniTask.Action(OnEventBattleSceneStart));
    }

    public override void BackEnter()
    {

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
        Model.SetTeamModelInfo();
        _ = View.RefreshAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/ExStage/ExStageView";
    }

    private void OnEventPresetCharacterSlotSelect(int selectPresetSlot)
    {
        // 현재 프리셋을 User에 넘겨주고 TeamUpdateView를 열자.
        BaseController teamUpdateController = UIManager.Instance.GetController(UIType.TeamUpdateView);
        TeamUpdateViewModel viewModel = teamUpdateController.GetModel<TeamUpdateViewModel>();
        viewModel.SetUser(Model.User);
        viewModel.SetCurrentDeckType(DeckType.StageDungeon);
        viewModel.SetOpenPresetNumber(Model.TeamGroup.PresetNumber);
        UIManager.Instance.EnterAsync(teamUpdateController).Forget();

        _ = View.RefreshAsync();
    }


    private async UniTaskVoid OnEventBattleSceneStart()
    {
        Team team = Model.TeamGroup.GetCurrentTeam();

        if (team == null)
        {
            IronJade.Debug.Log("Team is Null");
            return;
        }

        await BattleManager.Instance.EnterExStageDungeonBattle(Model.ExStageTableData.GetCONNECT_DUNGEON(), Model.ExStageTableData.GetID(), team);
    }

    #endregion Coding rule : Function
}