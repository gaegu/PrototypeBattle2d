//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Collections.Generic;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Table.Data;
using IronJade.UI.Core;
using NaughtyAttributes;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class GarenaBuildBattleResultController : BaseController
{
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.GarenaBuildBattleResultPopup; } }
    public override void SetModel() { SetModel(new GarenaBuildBattleResultPopupModel()); }
    private GarenaBuildBattleResultPopup View { get { return base.BaseView as GarenaBuildBattleResultPopup; } }
    private GarenaBuildBattleResultPopupModel Model { get { return GetModel<GarenaBuildBattleResultPopupModel>(); } }
    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private string pvModelPath = "ScriptableObjects/Prologue/SequenceModel/2_0_Prologue_Battle_PV";
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        var pvModel = UtilModel.Resources.Load<BattlePrologueSequenceModel>(pvModelPath, null);
        Model.SetStartDungeonId(GetStartId(BattleProcessManager.Instance.BattleInfo.DungeonID));
        Model.SetCharacterLevel(pvModel.SelectableCharacterLevel);
        Model.AddSelectableCharacter(pvModel.SelectableCharacterIds);

        Model.SetOnEventSelect(OnEventSelectCharcter);
        Model.CreateThumbnailCharacterUnitModels();
        Model.CreateChainDungeonListByStartDungeon();
        Model.SetOnSelectDungeon(OnSelectDungeon);
        Model.SetOnStartBattle(OnStartBattle);
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "GarenaBuildBattleResultPopup";
    }

    public void OnSelectDungeon(int dungeonId)
    {
        CheckCharacterLockedDungeon(dungeonId);
        Model.SetSelectDungeonId(dungeonId);
    }

    private ThumbnailSelectType OnEventSelectCharcter(BaseThumbnailUnitModel model)
    {
        if (model is ThumbnailCharacterUnitModel characterUnitModel)
        {
            if (Model.SelectCharacters.Contains(model.DataId))
            {
                Model.RemoveSelectCharacter(characterUnitModel.DataId);
                model.SelectModel.SetSelectState(ThumbnailSelectState.None);

                View.ShowThumbnailCharacterUnits().Forget();
                return ThumbnailSelectType.None;
            }
            else
            {
                Model.AddSelectCharacter(characterUnitModel.DataId);
                model.SelectModel.SetSelectState(ThumbnailSelectState.Default);

                View.ShowThumbnailCharacterUnits().Forget();
                return ThumbnailSelectType.Select;
            }
        }

        return ThumbnailSelectType.None;
    }

    private async UniTask OnStartBattleAsync(UISubState state)
    {
        if (state == UISubState.Finished)
        {
            DungeonTableData dungeonData = TableManager.Instance.GetTable<DungeonTable>().GetDataByID(Model.SelectDungeonId);

            Team team = null;

            BattleTeamGeneratorModel battleTeamGenerator = new BattleTeamGeneratorModel();
            if (dungeonData.GetDEFUALT_ALLY_CHARACTERCount() > 0)
            {
                team = battleTeamGenerator.CreateFixedAllyTeamByDungeonData(dungeonData, PlayerManager.Instance.MyPlayer.User);
            }
            else
            {
                team = battleTeamGenerator.CreateDummyTeamByDataIds(Model.SelectCharacters.ToArray(), Model.CharacterLevel);
            }

            var battleInfo = BattleManager.Instance.GetGarenaBattleBuildNextDungeonBattleInfo(Model.SelectDungeonId, team);
            BattleProcessManager.Instance.ResetUFEByChainDungeon();

            await BattleManager.Instance.LoadingNextBattle(battleInfo, true);
            await BattleManager.Instance.PlayNextBattle(battleInfo);
        }
    }

    private void CheckCharacterLockedDungeon(int dungeonId)
    {
        DungeonTableData dungeonData = TableManager.Instance.GetTable<DungeonTable>().GetDataByID(dungeonId);

        View.ShowCharacterLocked(dungeonData.GetDEFUALT_ALLY_CHARACTERCount() > 0);
    }

    public void OnStartBattle()
    {
        if (!FlowManager.Instance.CheckFlow(FlowType.BattleFlow))
        {
            MessageBoxManager.ShowToastMessage("Not in battle.");
            return;
        }

        StartBattleAsync().Forget();
    }

    private async UniTask StartBattleAsync()
    {
        if (UIManager.Instance.CheckOpenUI(UIType.BattleResultWinPopup))
            await UIManager.Instance.Exit(UIType.BattleResultWinPopup);

        if (UIManager.Instance.CheckOpenUI(UIType.BattleResultLosePopup))
            await UIManager.Instance.Exit(UIType.BattleResultLosePopup);

        await UniTask.NextFrame();

        Exit(OnStartBattleAsync).Forget();
    }

    private int GetStartId(int currentDungeonId)
    {
        int startId = 0;

        DungeonTable dungeonTable = TableManager.Instance.GetTable<DungeonTable>();
        DungeonTableData dungeonTableData = dungeonTable.GetDataByID(currentDungeonId);

        DungeonMasterTable dungeonMasterTable = TableManager.Instance.GetTable<DungeonMasterTable>();
        DungeonMasterTableData dungeonMasterTableData = dungeonMasterTable.GetDataByID(dungeonTableData.GetDUNGEON_MASTER_GROUP());

        startId = dungeonMasterTableData.GetCHAIN_DUNGEON(0);

        return startId;
    }
    #endregion Coding rule : Function
}