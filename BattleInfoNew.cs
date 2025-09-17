
using System;
using System.Collections.Generic;
using System.Linq;
using IronJade.Observer.Core;
using IronJade.Table.Data;
using UnityEngine;

public class BattleInfoNew : IObserverParam
{
    public static System.Func<int, Team, BattleResultInfoModel, int, int, BattleInfoNew> CreateBattleInfo;

    #region Coding rule : Property
    public int DungeonID { get; private set; }
    public DungeonTableData DungeonData { get; private set; }
    public DungeonSetTableData DungeonSetData { get; private set; }
    public DungeonType BattleDungeonType { get; private set; }
    public BattleTypeNew BattleType { get; private set; }
    public DungeonDifficulty DungeonDifficulty { get; private set; }
    public int DungeonPower { get; private set; }
    public int DungeonCorrectionLevel { get; private set; }
    public float DungeonLimitTime { get; private set; }
    public BattleResultInfoModel BattleResultInfoModel { get; private set; }
    public Team TeamPlayer { get; private set; }

    public string[] BattleResultWinVoice { get; private set; }
    public string[] BattleResultLoseVoice { get; private set; }

    public List<BattleWaveInfoNew> ListWaveInfo { get { return waveInfos.ToList(); } }


    public ElementType WeakElement { get; private set; }

    public float[] PowerBalanceValue { get; private set; } = new float[IntDefine.MAX_MANY_VS_MANY_ARENA_ROUNDS];


    public int CurrentWave { get; private set; }
    public int TotalWave
    {
        get
        {
            return waveInfos.Length;
        }
    }

    public BattleWaveInfoNew CurrentWaveInfo
    {
        get
        {
            if (waveInfos == null)
                return null;

            return CurrentWave >= 0 && CurrentWave < waveInfos.Length ? waveInfos[CurrentWave] : null;
        }
    }

    public bool IsLastWave
    {
        get
        {
            return CurrentWave == waveInfos.Length - 1;
        }
    }

    public bool IsUseBattleTime
    {
        get
        {
            return DungeonLimitTime > 0f;
        }
    }

    public bool IsArena
    {
        get
        {
            return BattleDungeonType == DungeonType.Arena;
        }
    }


    public bool IsElementAdvantage
    {
        get
        {
            if (DungeonData.IsNull())
                return false;

            return (ElementType)DungeonData.GetWEAK_ELEMENT() != ElementType.None;
        }
    }
    public bool UsePowerAdvantage { get; private set; }     //해당 던전에서 Power Advantage 적용 여부.

    public int BattleTimeLimit { get; private set; }

    public CodeTableData CodeData { get; private set; }


    // 전투 진입 시 트랜지션 (전투로 들어올 때)
    public TransitionType DungeonStartTransition
    {
        get
        {
            return (Transition)DungeonData.GetSTART_TRANSITION() switch
            {
                Transition.LOGO => TransitionType.Rotation,
                Transition.DOOR => TransitionType.Door,
                Transition.CAR => TransitionType.Car,
                Transition.ELEVATOR => TransitionType.Elevator,
                Transition.TRAM => TransitionType.Tram,
                _ => TransitionType.Rotation,
            };

        }
    }
    // 전투 종료 시 트랜지션 (로비로 나갈 때)
    public TransitionType DungeonEndTransition
    {
        get
        {
            return (Transition)DungeonData.GetEND_TRANSITION() switch
            {
                Transition.LOGO => TransitionType.Rotation,
                Transition.DOOR => TransitionType.Door,
                Transition.CAR => TransitionType.Car,
                Transition.ELEVATOR => TransitionType.Elevator,
                Transition.TRAM => TransitionType.Tram,
                _ => TransitionType.Rotation,
            };

        }
    }
    public bool IsBattleTransition
    {
        get
        {
            return !string.IsNullOrEmpty(DungeonData.GetBATTLE_TRANSITION_PATH());
        }
    }
    #endregion Coding rule : Property

    #region Coding rule : Value
    private BattleWaveInfoNew[] waveInfos;
    private Action<bool> loadingCallBack;
    private Action<bool> processCallBack;
    #endregion Coding rule : Value

    #region Coding rule : Function
    #endregion Coding rule : Function

    public void Clear()
    {
        if (TeamPlayer != null)
        {
            TeamPlayer.Clear();
            TeamPlayer = null;
        }

        SetDungeon(0);
    }

    public void InitCurrentWave()
    {
        CurrentWave = 0;
    }

    public void SetDungeon(int dungeonID)
    {
        DungeonID = dungeonID;
        DungeonTable dungeonTable = TableManager.Instance.GetTable<DungeonTable>();
        DungeonData = dungeonTable.GetDataByID(dungeonID);

        InitCurrentWave();

        BattleDungeonType = (DungeonType)DungeonData.GetDUNGEON_TYPE();
        
        
        //BattleType = (BattleType)DungeonData.GetBATTLE_TYPE();

        DungeonLimitTime = DungeonData.GetDUNGEON_LIMIT_TIME();
        DungeonDifficulty = (DungeonDifficulty)DungeonData.GetDUNGEON_DIFFICULTY();
        DungeonCorrectionLevel = DungeonData.GetDUNGEON_CORRECTION_LEVEL();
        DungeonPower = DungeonData.GetPOWER();


        WeakElement = (ElementType)DungeonData.GetWEAK_ELEMENT();

        UsePowerAdvantage = DungeonData.GetPOWER_ADVANTAGE_ACTIVATE_TYPE() == 1 ? true : false;
    }

    public void SetDungeonSetData()
    {
        if (TeamPlayer == null)
            return;

        int allyCount = TeamPlayer.GetCharacterCount();

        DungeonSetTable dungeonSetTable = TableManager.Instance.GetTable<DungeonSetTable>();
        DungeonSetData = dungeonSetTable.GetDataByID(DungeonData.GetDUNGEON_SET());

        if (DungeonSetData.IsNull())
        {
            IronJade.Debug.LogError($"[BattleInfo SetDungeonSetData] DungeonSetData is Null / dataID = {DungeonSetData.GetID()}");
            return;
        }
    }

   

    public void SetLoadingCallBack(Action<bool> callBack)
    {
        loadingCallBack = callBack;
    }

    public void SetProcessCallBack(Action<bool> callBack)
    {
        processCallBack = callBack;
    }


    public void SetTeamPlayer(Team team)
    {
        if (TeamPlayer == null)
            TeamPlayer = new Team(0, 0);

        TeamPlayer.Copy(team);
    }

    public void SetBattlePowerBalance()
    {
        CharacterBalanceTable characterBalanceTable = TableManager.Instance.GetTable<CharacterBalanceTable>();

        TeamPlayer.UpdateTeamPower();

        if (BattleDungeonType != DungeonType.Arena)
        {
            //Complare Power Penalty
            if (TeamPlayer.Power < DungeonPower)
            {
                CharacterBalanceTableData powerPenaltyTableData = characterBalanceTable.GetDataByID((int)CharacterBalanceDefine.BALANCE_CHARACTER_COMMBAT_POWER_PENALTY_ALLY);

                if (powerPenaltyTableData.IsNull())
                    return;

                SetPowerPenalty(powerPenaltyTableData);
                return;
            }
            else
            {
                SetPowerBalanceValue(0, 1f);
            }

            //던전에 따라 Power Advantage적용 안하는던전이 있다.
            if (!UsePowerAdvantage)
                return;

            //Compare Power Advantage
            CharacterBalanceTableData powerDiffernceTableData = characterBalanceTable.GetDataByID((int)CharacterBalanceDefine.BALANCE_CHARACTER_COMMBAT_POWER_ADVANTAGE_DIFFERENCE);
            if (powerDiffernceTableData.IsNull())
                return;

            float advantagePowerValue = (float)powerDiffernceTableData.GetINDEX(0);
            float dungeonPowerValue = DungeonData.GetPOWER();

            if (TeamPlayer.Power >= advantagePowerValue * dungeonPowerValue)
            {
                CharacterBalanceTableData powerAdvantageTableData = characterBalanceTable.GetDataByID((int)CharacterBalanceDefine.BALANCE_CHARACTER_COMMBAT_POWER_ADVANTAGE_DIFFERENCE);
                if (powerAdvantageTableData.IsNull())
                    return;

                SetPowerAdvantage(powerAdvantageTableData);
            }
        }
    }

    /// <summary>
    /// 아레나 외 모든 스테이지에서는 팀 전투력이 권장전투력보다 높을때 어드벤티지 적용.
    /// </summary>
    public void SetPowerAdvantage(CharacterBalanceTableData characterBalanceTableData)
    {
        float advantageValue = (float)characterBalanceTableData.GetINDEX(0);
        SetPowerBalanceValue(0, advantageValue);
    }

    /// <summary>
    /// 아레나 외 모든 스테이지에서는 팀 전투력이 권장전투력보다 작을때만 패널티 적용.
    /// </summary>
    public void SetPowerPenalty(CharacterBalanceTableData characterBalanceTableData)
    {
        float fPowerPer = (float)TeamPlayer.Power / DungeonPower;

        if (fPowerPer <= 0.5009f)
        {
            SetPowerBalanceValue(0, 1f - 0.9f);
        }
        else
        {
            float fCalcValue = (1f - fPowerPer) / 0.001f;

            int nCalcValue = (int)Mathf.Ceil(fCalcValue);

            if (characterBalanceTableData.GetINDEX_COUNT() >= nCalcValue)
            {
                float penaltyValue = (float)characterBalanceTableData.GetINDEX(nCalcValue - 1) * 0.0001f;

                SetPowerBalanceValue(0, 1f - penaltyValue);
            }
            else
            {
                float penaltyValue = 0.43f + (0.001f * (nCalcValue - 158));

                SetPowerBalanceValue(0, 1f - penaltyValue);
            }
        }
    }

    public void SetPowerBalanceValue(int nIndex, float value)
    {
        if (PowerBalanceValue.Length <= nIndex)
            return;

        PowerBalanceValue[nIndex] = value;
    }



    public void SetWaveInfos(List<BattleWaveInfoNew> infos)
    {
        waveInfos = infos.ToArray();
    }

    public BattleWaveInfoNew GetWaveInfo(int waveIndex)
    {
        if (waveInfos == null)
            return null;

        return waveIndex >= 0 && waveIndex < waveInfos.Length ? waveInfos[waveIndex] : null;
    }

    public void SetNextWave()
    {
        CurrentWave += 1;
    }

    public void SetCurrentWave(int wave)
    {
        CurrentWave = wave;
    }


  /* public void AddHitCount(bool isAlly)
    {
        if (CurrentWaveInfo == null)
            return;

        CurrentWaveInfo.AddHitCount(isAlly);
    }

    public void AddWeakHitCount(bool isAlly)
    {
        if (CurrentWaveInfo == null)
            return;

        CurrentWaveInfo.AddWeakHitCount(isAlly);
    }*/


    public void SetBattleDungeonType(DungeonType dungeonType)
    {
        if (BattleDungeonType == dungeonType)
            return;

        BattleDungeonType = dungeonType;
    }

    public void SetBattleResultInfoModel(BattleResultInfoModel battleResultInfoModel)
    {
        BattleResultInfoModel = battleResultInfoModel;
    }


    public void SetDungeonLimitTime(float time)
    {
        DungeonLimitTime = time;
    }


    public void SetCodeTableData(CodeTableData codeTableData)
    {
        CodeData = codeTableData;
    }


    public void LoadingCallBakcInvoke(bool isBool)
    {
        if (loadingCallBack != null)
            loadingCallBack.Invoke(isBool);
    }

    public void ProcessCallBackInfo(bool isBool)
    {
        if (processCallBack != null)
            processCallBack.Invoke(isBool);
    }


    /// <summary>
    /// 던전 마스터 그룹에 있는 던전인가 확인용.
    /// </summary>
    /// <returns></returns>
    public bool IsDungeonMasterInDungeon()
    {
        // 우선 현재 던전에 던전 마스터 그룹이 있는지 확인.
        if (DungeonData.IsNull())
            return false;

        int dungeonMasterGroup = DungeonData.GetDUNGEON_MASTER_GROUP();

        DungeonMasterTable dungeonMasterTable = TableManager.Instance.GetTable<DungeonMasterTable>();
        DungeonMasterTableData dungeonMasterTableData = dungeonMasterTable.GetDataByID(dungeonMasterGroup);

        if (dungeonMasterTableData.IsNull())
            return false;

        for (int i = 0; i < dungeonMasterTableData.GetCHAIN_DUNGEONCount(); ++i)
        {
            if (dungeonMasterTableData.GetCHAIN_DUNGEON(i) == DungeonID)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 체크해야되는 던전들 뽑아보자.
    /// </summary>
    /// <param name="isCheckFromNowDungeon"></param>
    /// <returns></returns>
    public List<int> GetDungeonMasterAllDungeon(bool isCheckFromNowDungeon)
    {
        List<int> dungeonList = new List<int>();

        if (DungeonData.IsNull())
            return dungeonList;

        int dungeonMasterGroup = DungeonData.GetDUNGEON_MASTER_GROUP();

        DungeonMasterTable dungeonMasterTable = TableManager.Instance.GetTable<DungeonMasterTable>();
        DungeonMasterTableData dungeonMasterTableData = dungeonMasterTable.GetDataByID(dungeonMasterGroup);

        if (dungeonMasterTableData.IsNull())
            return dungeonList;

        bool isCheckStart = false;

        for (int i = 0; i < dungeonMasterTableData.GetCHAIN_DUNGEONCount(); ++i)
        {
            int dungeonID = dungeonMasterTableData.GetCHAIN_DUNGEON(i);

            if (isCheckFromNowDungeon)
            {
                if (dungeonID == DungeonID)
                {
                    isCheckStart = true;
                }
            }
            else
            {
                isCheckStart = true;
            }

            if (isCheckStart)
            {
                dungeonList.Add(dungeonID);
            }
        }

        return dungeonList;
    }
    /// <summary>
    /// 연결된 다음 던전이 있는지 여부 체크
    /// </summary>
    public bool CheckNextDungeon()
    {
        DungeonMasterTable dungeonMasterTable = TableManager.Instance.GetTable<DungeonMasterTable>();
        var find = dungeonMasterTable.Find(match =>
        {
            for (int i = 0; i < match.GetCHAIN_DUNGEONCount(); ++i)
            {
                if (match.GetCHAIN_DUNGEON(i) == DungeonID)
                    return true;
            }

            return false;
        });

        if (find.IsNull())
            return false;

        // 이 로직들은 던전데이터가 개선되면 없어질 애들
        for (int i = 0; i < find.GetCHAIN_DUNGEONCount(); ++i)
        {
            if (find.GetCHAIN_DUNGEON(i) == DungeonID)
            {
                if ((i + 1) < find.GetCHAIN_DUNGEONCount())
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 연결된 다음 던전 ID
    /// </summary>
    /// <returns></returns>
    public int GetChainDungeonDataId()
    {
        DungeonMasterTable dungeonMasterTable = TableManager.Instance.GetTable<DungeonMasterTable>();
        var find = dungeonMasterTable.Find(match =>
        {
            for (int i = 0; i < match.GetCHAIN_DUNGEONCount(); ++i)
            {
                if (match.GetCHAIN_DUNGEON(i) == DungeonID)
                    return true;
            }

            return false;
        });

        if (find.IsNull())
            return 0;

        // 이 로직들은 던전데이터가 개선되면 없어질 애들
        for (int i = 0; i < find.GetCHAIN_DUNGEONCount(); ++i)
        {
            if (find.GetCHAIN_DUNGEON(i) == DungeonID)
            {
                if ((i + 1) < find.GetCHAIN_DUNGEONCount())
                {
                    return find.GetCHAIN_DUNGEON(i + 1);
                }
                else
                {
                    return 0;
                }
            }
        }

        return 0;
    }
}
