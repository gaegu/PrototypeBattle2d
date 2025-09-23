//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class InfinityDungeonLobbyController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.InfinityDungeonLobbyView; } }
    private InfinityDungeonLobbyView View { get { return base.BaseView as InfinityDungeonLobbyView; } }
    protected InfinityDungeonLobbyViewModel Model { get; private set; }
    public InfinityDungeonLobbyController() { Model = GetModel<InfinityDungeonLobbyViewModel>(); }
    public InfinityDungeonLobbyController(BaseModel baseModel) : base(baseModel) { }
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
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);
        Model.SetRankingOpen(ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_RANKING));

        UpdateRemainTime();

        Model.SetOnClickDungeon(OnClickDungeon);
        Model.SetEventRanking(OnEventRanking);
    }

    public override void BackEnter()
    {
        Model.SetInfinityDungeonModel(GetInfinityDungeonModel());
        Model.SetDungeons();
        View.ShowAsync().Forget();
    }

    public override async UniTask LoadingProcess()
    {
        await RequestInfinityDungeon();
        Model.SetInfinityDungeonModel(GetInfinityDungeonModel());
        Model.SetDungeons();
    }

    public override async UniTask Process()
    {
        SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_INFINITYCIRCUIT_BGM);

        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/InfinityDungeon/InfinityDungeonLobbyView";
    }

    private InfinityDungeonGroupModel GetInfinityDungeonModel()
    {
        DateTime resetTime = TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);
        DayOfWeek currentDayWeek = TimeManager.Instance.GetCurrentTimeDayWeek();

        InfinityDungeonGroupModel infinityDungeonModel = DungeonManager.Instance.GetDungeonGroupModel<InfinityDungeonGroupModel>();
        infinityDungeonModel.UpdateDungeonOpen(resetTime, currentDayWeek);

        foreach (var model in infinityDungeonModel.GetInfinityDungeonGroupModels())
        {
            bool isOpenLevel = CheckContentsOpen(model.Key);
            infinityDungeonModel.UpdateDungeonOpenLevel(model.Key, isOpenLevel);
        }

        return infinityDungeonModel;
    }

    private void OnClickDungeon(InfinityCircuitType type)
    {
        BaseController controller = UIManager.Instance.GetController(UIType.InfinityDungeonView);
        InfinityDungeonViewModel model = controller.GetModel<InfinityDungeonViewModel>();

        LimitedOpenDungeonGroupModel dungeonGroupModel = Model.InfinityDungeonModel.GetDungeonGroupModelByDataId(Model.GetInfinityDungeonDataId(type));
        model.SetCurrentCircuitType(type);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventRanking()
    {
        ContentsOpenManager.Instance.OpenContents(ContentsType.Ranking).Forget();
    }

    private void UpdateRemainTime()
    {
        TimeSpan remainResetTime = TimeManager.Instance.GetRemainResetTime(CheckResetTimeType.Daily);
        Model.SetResetTime(UtilModel.String.GetRemainTimeLocalizationText(remainResetTime));
    }

    private async UniTask RequestInfinityDungeon()
    {
        InfinityCircuitGetProcess infinityCircuitGetProcess = NetworkManager.Web.GetProcess<InfinityCircuitGetProcess>();
        if (await infinityCircuitGetProcess.OnNetworkAsyncRequest())
            infinityCircuitGetProcess.OnNetworkResponse();
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case CommonObserverID.TimeRefresh:
                {
                    UpdateRemainTime();
                    break;
                }
            case CommonObserverID.DailyRefreshData:
                {
                    Model.SetInfinityDungeonModel(GetInfinityDungeonModel());
                    Model.SetDungeons();
                    View.ShowAsync().Forget();
                    MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_INFINITYCIRCUITLOBBYVIEW_DAY_CHANGED).Forget();
                    break;
                }
        }
    }

    private bool CheckContentsOpen(int infinityDungeonDataId)
    {
        InfinityCircuitTable infinityCircuitTable = TableManager.Instance.GetTable<InfinityCircuitTable>();
        InfinityCircuitTableData infinityCircuitData = infinityCircuitTable.GetDataByID(infinityDungeonDataId);
        return ContentsOpenManager.Instance.CheckStageDungeonCondition(infinityCircuitData.GetCONDITION_STAGE_DUNGEON());
    }
    #endregion Coding rule : Function
}