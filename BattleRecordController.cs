//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Linq;
using System.Net;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class BattleRecordController : BaseController
{

    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.BattleRecordPopup; } }
    public override void SetModel() { SetModel(new BattleRecordPopupModel()); }
    private BattleRecordPopup View { get { return base.BaseView as BattleRecordPopup; } }
    private BattleRecordPopupModel Model { get { return GetModel<BattleRecordPopupModel>(); } }

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        Model.SetOnClickClose(OnClickClose);
        Model.SetOnClickBackToRecordList(OnClickBackToRecordList);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestBattleRecord();
    }

    private async UniTask RequestBattleRecord()
    {
        switch (Model.BattleRecordType)
        {
            case BattleRecordPopupModel.RecordType.Code:
                await RequestCodeRedRankGet();
                break;

            default:
                break;
        }
    }

    private async UniTask RequestCodeRedRankGet()
    {
        BaseProcess codeRedRankGetProcess = NetworkManager.Web.GetProcess(WebProcess.CodeRankGet);

        codeRedRankGetProcess.SetPacket(new CodeRankGetInDto(Model.CodeTableDataId, Model.StartRank, Model.ShowCount));

        if (await codeRedRankGetProcess.OnNetworkAsyncRequest())
        {
            var response = codeRedRankGetProcess.GetResponse<CodeRankGetResponse>();

            CreateBattleRecordPopupUnits(response.data);
        }
    }

    private void CreateBattleRecordPopupUnits(CodeRankInfoDto[] dto)
    {
        Model.ClearBattleRecordPopupUnitModelList();
        Model.SetSelectRecordIndex(0);
        Model.SetState(BattleRecordPopupModel.State.RecordList);

        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        BattleTeamGeneratorModel teamGeneratorModel = new BattleTeamGeneratorModel();

        if (dto == null)
            return;

        Array.Sort(dto, (x, y) => y.maxKillCount.CompareTo(x.maxKillCount));

        for (int i = 0; i < dto.Length; i++)
        {
            var rankInfo = dto[i];

            BattleRecordPopupUnitModel battleRecordPopupUnitModel = new BattleRecordPopupUnitModel();

            battleRecordPopupUnitModel.SetTotalDamageText(rankInfo.maxKillCount.ToString());
            battleRecordPopupUnitModel.SetUserNameText(rankInfo.nickName);

            // 유저 레벨 Set
            User dummyUser = new User(0, false);
            dummyUser.SetExp(rankInfo.exp);
            battleRecordPopupUnitModel.SetUserLevelText($"Lv {dummyUser.AccountLevel}");

            // 대표 캐릭터 Set
            int leaderCharacterDataId = rankInfo.dataCharacterId;

            if (leaderCharacterDataId == 0)
                leaderCharacterDataId = IntDefine.DEFAULT_LEADER_CHARACTER_ID;

            CharacterTableData characterTableData = characterTable.GetDataByID(leaderCharacterDataId);
            battleRecordPopupUnitModel.SetIndex(i);
            battleRecordPopupUnitModel.SetUserCharacterThumbnailPath(ThumbnailGeneratorModel.GetCharacterThumbnailByData(characterTableData, CharacterThumbnailType.Rectangle));
            battleRecordPopupUnitModel.SetOnClickBattleRecord(OnClickBattleRecord);

            // 덱 Set
            if (rankInfo.deck != null)
            {
                TeamGroup teamGroup = new TeamGroup();

                Team team = teamGeneratorModel.CreateTeamByDto(rankInfo.deck);
                teamGroup.AddTeam(team);

                battleRecordPopupUnitModel.SetTeamGroup(teamGroup);
            }

            Model.AddBattleRecordPopupUnitModel(battleRecordPopupUnitModel);
        }

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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Battle/BattleRecordPopup";
    }

    #region OnClick
    public void OnClickClose()
    {
        Exit().Forget();
    }

    public void OnClickBattleRecord(int battleRecordIndex)
    {
        Model.SetSelectRecordIndex(battleRecordIndex);

        Model.SetState(BattleRecordPopupModel.State.DetailRecord);
        View.ShowAsync().Forget();
    }

    public void OnClickBackToRecordList()
    {
        Model.SetState(BattleRecordPopupModel.State.RecordList);
        View.ShowAsync().Forget();
    }
    #endregion

    #endregion Coding rule : Function
}