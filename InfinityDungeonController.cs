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
using IronJade.Observer.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class InfinityDungeonController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.InfinityDungeonView; } }
    private InfinityDungeonView View { get { return base.BaseView as InfinityDungeonView; } }
    protected InfinityDungeonViewModel Model { get; private set; }
    public InfinityDungeonController() { Model = GetModel<InfinityDungeonViewModel>(); }
    public InfinityDungeonController(BaseModel baseModel) : base(baseModel) { }
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
        ObserverManager.AddObserver(CommonObserverID.DailyRefreshData, this);

        Model.SetEventRanking(OnEventRanking);
        Model.SetEventChangeCircuit(OnEventChangeCircuit);
        Model.SetEventSelectStage(OnEventSelectDungeon);
    }

    public override async UniTask LoadingProcess()
    {
        SetInfinityDungeonInfo();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async UniTask PlayShowAsync()
    {
        await base.PlayShowAsync();
    }

    public override async void Refresh()
    {
        SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_INFINITYCIRCUIT_BGM);
        await View.ShowAsync();
        await View.SnapCurrentScroll();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);
        return await base.Exit(onEventExtra);
    }

    public override void BackEnter()
    {
        Refresh();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/InfinityDungeon/InfinityDungeonView";
    }

    private void SetInfinityDungeonInfo()
    {
        InfinityCircuitTable infinityCircuitTable = TableManager.Instance.GetTable<InfinityCircuitTable>();

        Dictionary<InfinityCircuitType, LimitedOpenDungeonGroupModel> dungeonGroupList = new Dictionary<InfinityCircuitType, LimitedOpenDungeonGroupModel>();

        DateTime resetTime = TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);
        DayOfWeek currentDayWeek = TimeManager.Instance.GetCurrentTimeDayWeek();

        InfinityDungeonGroupModel infinityDungeonModel = DungeonManager.Instance.GetDungeonGroupModel<InfinityDungeonGroupModel>();
        infinityDungeonModel.UpdateDungeonOpen(resetTime, currentDayWeek);

        foreach (var model in infinityDungeonModel.GetInfinityDungeonGroupModels())
        {
            InfinityCircuitTableData infinityCircuitData = infinityCircuitTable.GetDataByID(model.Key);
            bool isOpenLevel = ContentsOpenManager.Instance.CheckStageDungeonCondition(infinityCircuitData.GetCONDITION_STAGE_DUNGEON());

            if (isOpenLevel && model.Value.IsOpen)
            {
                InfinityCircuitType type = (InfinityCircuitType)infinityCircuitData.GetDUNGEON_TYPE();
                dungeonGroupList.Add(type, model.Value);
            }
        }

        Model.SetInfinityDungeonViewModel(dungeonGroupList, PlayerManager.Instance.MyPlayer.User);
    }

    private void OnEventRanking()
    {
        ContentsOpenManager.Instance.OpenContents(ContentsType.Ranking).Forget();
    }

    private void OnEventChangeCircuit(bool isNext)
    {
        Model.SetCurrentCircuitType(Model.GetPrevNextCircuitType(isNext));
        Model.SetCurrentDungeonList();
        Model.SetCurrentDungeonTicket(PlayerManager.Instance.MyPlayer.User);
        View.ShowAsync().Forget();
    }

    private void OnEventSelectDungeon(int dungeonId)
    {
        DeckType deckType = Model.CurrentCircuitType switch
        {
            InfinityCircuitType.General => DeckType.InfinityDungeonGeneral,
            InfinityCircuitType.Black => DeckType.InfinityDungeonBlack,
            InfinityCircuitType.Blue => DeckType.InfinityDungeonBlue,
            InfinityCircuitType.Prism => DeckType.InfinityDungeonPrism,
            _ => DeckType.InfinityDungeonGeneral,
        };

        BaseController stageInfoController = UIManager.Instance.GetController(UIType.DungeonInfoView);
        DungeonInfoViewModel dungeonInfoViewModel = stageInfoController.GetModel<DungeonInfoViewModel>();
        dungeonInfoViewModel.SetCurrentDeckType(deckType);
        dungeonInfoViewModel.SetPopupByDungeonDataId(dungeonId);
        dungeonInfoViewModel.SetParentDungeonDataId(Model.CurrentCircuitDataId);
        dungeonInfoViewModel.SetHideAfterStart(true);
        dungeonInfoViewModel.SetListOpen(true);
        if (Model.CurrentDungeonDataId < dungeonId)
            dungeonInfoViewModel.SetBlockEnter(true, LocalizationDefine.LOCALIZATION_INFINITYCIRCUITVIEW_NOT_REACH);
        else if (Model.CurrentDungeonDataId > dungeonId)
            dungeonInfoViewModel.SetBlockEnter(true, LocalizationDefine.LOCALIZATION_INFINITYCIRCUITVIEW_ALREADY_CLEAR);
        else if (Model.CurrentCircuitType != InfinityCircuitType.General && Model.Ticket.Count == 0)
            dungeonInfoViewModel.SetBlockEnter(true, LocalizationDefine.LOCALIZATION_INFINITYCIRCUITVIEW_NO_TICKET);
        else
            dungeonInfoViewModel.SetBlockEnter(false, LocalizationDefine.None);
        UIManager.Instance.EnterAsync(stageInfoController).Forget();
    }

    private void EnterBattle()
    {
        //Team team = Model.TeamGroup.GetCurrentTeam();

        //BattleTeamGeneratorModel waveGenerator = new BattleTeamGeneratorModel();
        //List<WaveInfo> waveInfos = waveGenerator.CreateWaveInfosByDungeonId(team, Model.CurrentDungeon);

        //DungeonTable dungeonTable = TableManager.Instance.GetTable<DungeonTable>();
        //DungeonTableData nextDungeonData = dungeonTable.GetDataByID(Model.CurrentDungeon.GetNEXT_DUNGEON_ID());

        //BattleResultInfinityDungeonInfoModel resultInfoModel = new BattleResultInfinityDungeonInfoModel();
        //resultInfoModel.SetFuncGetReward(RequestClearRewardGoods);
        //resultInfoModel.SetOnClickNextBattle(OnClickStartBattle);
        //resultInfoModel.SetOnClickCharge(OnClickCharge);
        //resultInfoModel.SetCurrentFloor(Model.CurrentDungeonIndex + 1);
        //resultInfoModel.SetIsGeneralDungeon(Model.IsGeneralDungeon);
        //resultInfoModel.SetCheckNextDungeon(!nextDungeonData.IsNull());
        //resultInfoModel.SetFuncGetTicketCount(() => { return (int)Model.GetTicket().Count; });

        //BattleInfo battleInfo = new BattleInfo();
        //battleInfo.SetDungeon(Model.CurrentDungeon.GetID());
        //battleInfo.SetTeamPlayer(team);
        //battleInfo.SetWaveInfos(waveInfos);
        //battleInfo.SetBattleResultInfoModel(resultInfoModel);
        //battleInfo.SetPowerPenalty();

        //ObserverManager.NotifyObserver(FlowObserverID.BattleEnter, battleInfo);
    }


    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case CommonObserverID.DailyRefreshData:
                {
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}