using IronJade.Table.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class BattleWaveInfoNew
{
    public Team TeamEnemy { get; private set; }

    public bool IsBossWave { get; private set; }


    public List<int> BreakInMonsterID = new List<int>();
    public int BreakInMonsterTimeSet = -1;

    public BattleWaveInfoNew()
    {
        if (TeamEnemy == null)
            TeamEnemy = new Team(0, 0, IntDefine.MAX_TEAM_ENEMY_CHARACTER_SLOT_COUNT);

    }

    public void Set(Team team)
    {
        TeamEnemy.Copy(team);
    }

    public void Reset(Team team)
    {
        TeamEnemy.Reset();
        TeamEnemy.Copy(team);
    }


    public void SetBossWave(bool isBossWave)
    {
        IsBossWave = isBossWave;
    }



    public void SetBreakInMonster(MonsterGroupTableData monsterGroup)
    {
        BreakInMonsterID.Clear();
        BreakInMonsterTimeSet = -1;

        if (monsterGroup.IsNull())
            return;

        int maxCount = monsterGroup.GetBREAK_IN_MONSTERCount();

        if (maxCount > 0)
        {
            MonsterTable monsterTable = TableManager.Instance.GetTable<MonsterTable>();

            for (int iCount = 0; iCount < maxCount; iCount++)
            {
                int charID = monsterGroup.GetBREAK_IN_MONSTER(iCount);
                MonsterTableData monsterTableData = monsterTable.GetDataByID(charID);

                if (monsterTableData.IsNull())
                    continue;

                BreakInMonsterID.Add(charID);
            }

            BalanceTable balanceTable = TableManager.Instance.GetTable<BalanceTable>();
            BalanceTableData balanceTableData = balanceTable.GetDataByID(monsterGroup.GetBREAK_IN_TIMESET());

            if (!balanceTableData.IsNull())
            {
                BreakInMonsterTimeSet = monsterGroup.GetBREAK_IN_TIMESET();
            }
        }
    }
}


