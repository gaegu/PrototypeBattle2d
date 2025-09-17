//=========================================================================================================
//using System;
//using System.Collections;
using System.Collections.Generic;
using IronJade.Table.Data;          // 데이터 테이블
using UnityEngine;
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class ArenaBattleInfoNew : BattleInfoNew
{
    //============================================================
    // 불필요한 부분은 지우고 사용하시면 됩니다.
    //============================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public string EnemyNickName { get; private set; }
    public string EnemyIllust { get; private set; }
    public ArenaModel EnemyArenaModel { get; private set; }
    public int EnemyPower { get; private set; }
    public string MatchToken { get; private set; }
    public int ArenaHistoryId { get; private set; }

    public List<Team> MyTeams { get; private set; }     // 5vs5 전용
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public void SetEnemyInfo(string nickName, string thumbnailIllust, ArenaModel arenaModel, int power)
    {
        EnemyNickName = nickName;
        EnemyIllust = thumbnailIllust;
        EnemyArenaModel = arenaModel;
        EnemyPower = power;
    }

    public void SetMyTeams(List<Team> myTeams)
    {
        MyTeams = myTeams;
    }

    public void SetArenaHistoryId(int id)
    {
        ArenaHistoryId = id;
    }

    public void SetMatchToken(string matchToken)
    {
        MatchToken = matchToken;
    }

    /// <summary>
    /// 아레나 는 팀 전투력이 상대 팀 전투력보다 작을때만 패널티 적용.
    /// </summary>
    public void SetAreanPowerPenalty()
    {
        CharacterBalanceTable characterBalanceTable = TableManager.Instance.GetTable<CharacterBalanceTable>();
        CharacterBalanceTableData characterBalanceTableData = characterBalanceTable.GetDataByID((int)CharacterBalanceDefine.BALANCE_CHARACTER_COMMBAT_POWER_PENALTY_ALLY);

        if (characterBalanceTableData.IsNull())
            return;

        if (BattleDungeonType == DungeonType.Arena)
        {
            TeamPlayer.UpdateTeamPower();

            if (CurrentWaveInfo != null)
            {
                CurrentWaveInfo.TeamEnemy.UpdateTeamPower();

                if (TeamPlayer.Power < CurrentWaveInfo.TeamEnemy.Power)
                {
                    float fPowerPer = (float)TeamPlayer.Power / CurrentWaveInfo.TeamEnemy.Power;

                    if (fPowerPer <= 0.8459f)
                    {
                        float fCalcValue = ((1f - fPowerPer) - 0.1541f) / 0.001f + 1f;

                        int nCalcValue = (int)Mathf.Floor(fCalcValue);

                        float penaltyValue = 0.2f + ((nCalcValue - 1) * 0.0028f);

                        if (penaltyValue > 0.9f)
                            penaltyValue = 0.9f;

                        SetPowerBalanceValue(0, penaltyValue);
                    }
                }
            }
        }
    }
    #endregion Coding rule : Function
}
