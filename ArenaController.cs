//=========================================================================================================
#pragma warning disable CS1998
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================
public class ArenaController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.ArenaView; } }
    private ArenaView View { get { return base.BaseView as ArenaView; } }
    protected ArenaViewModel Model { get; private set; }
    public ArenaController() { Model = GetModel<ArenaViewModel>(); }
    public ArenaController(BaseModel baseModel) : base(baseModel) { }
    //=========================================================================================================
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool checkTimeRefresh;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override async void Enter()
    {
        checkTimeRefresh = false;
        ObserverManager.AddObserver(CommonObserverID.TimeRefresh, this);

        Model.SetEventContentsShop(OnEventContentsShop);
        Model.SetEventDefenceDeck(OnEventDefenceDeck);
        Model.SetEventHistory(OnEventHistory);
        Model.SetEventRanking(OnEventRanking);
        Model.SetEventRewardList(OnEventRewardList);
        Model.SetEvetnMatchSelect(OnEventMatchSelect);
    }

    public override void BackEnter()
    {

    }

    public override async UniTask LoadingProcess()
    {
        //await RequestArenaSeasonGet();

        await RequestArenaScore();
    }

    public override async UniTask Process()
    {
        checkTimeRefresh = true;
        await View.ShowAsync();
        await UniTask.WaitUntil(() => { return !View.IsPlayingAnimation; });
    }

    public override void Refresh()
    {
        Model.PlayerInfoUnit.RefreshTeam(PlayerManager.Instance.MyPlayer.User.TeamModel.GetTeamGroupByType(DeckType.ArenaAttack).GetCurrentTeam());
        View.Refresh();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.TimeRefresh);
        checkTimeRefresh = false;

        TokenPool.Cancel(GetHashCode());

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Arena/ArenaView";
    }

    private async UniTask RequestArenaSeasonGet()
    {
        BaseProcess arenaSeasonGetProcess = NetworkManager.Web.GetProcess(WebProcess.ArenaSeasonGet);

        if (await arenaSeasonGetProcess.OnNetworkAsyncRequest())
            arenaSeasonGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestArenaScore()
    {
        DateTime currentTime = NetworkManager.Web.ServerTimeUTC;
        if (ArenaManager.Instance.ArenaSeasonInfoModel.IsOpen(currentTime))
        {

        }
        else
        {
            Model.SetClosedArenaViewModel(PlayerManager.Instance.MyPlayer.User);
            return;
        }
    }

    private void UpdateArenaView()
    {
        if (View == null)
            return;

        if (!Model.IsOpen)
            return;

        DateTime currentTime = NetworkManager.Web.ServerTimeUTC;
        UpdateRemainTime(currentTime);
        View.UpdateView();
    }

    private bool CheckArenaCalculatingTime()
    {
        DateTime currentTime = NetworkManager.Web.ServerTimeUTC;
        DateTime resetTime = TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);
        DateTime calculatingStartTime = resetTime.AddMinutes(-10);

        return calculatingStartTime <= currentTime && currentTime < resetTime;
    }

    private bool CheckArenaOpen()
    {
        return true;        //서버 작업 완료까지 임시
        if (!ArenaManager.Instance.CheckOpen())
        {
            System.Action onExitArena = () =>
            {
                if (UIManager.Instance.CheckOpenCurrentView(UIType.ArenaView))
                {
                    Exit().Forget();
                }
                else
                {
                    UIManager.Instance.RemoveToTarget(UIType.ArenaView);
                    UIType currentUI = UIManager.Instance.GetCurrentViewNavigator().Controller.UIType;
                    UIManager.Instance.Exit(currentUI).Forget();
                    ObserverManager.RemoveObserver(CommonObserverID.TimeRefresh);
                    checkTimeRefresh = false;
                }
            };
            string message = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_ARENA_NOTICE_SEASON_END);
            MessageBoxManager.ShowYesBox(message, onEventConfirm: onExitArena, onEventClose: onExitArena).Forget();
            return false;
        }

        return true;
    }

    private void UpdateRemainTime(DateTime currentTime)
    {
        TimeSpan remainTime = Model.EndTime - currentTime;
        string textRemainTime;

        if (remainTime.TotalDays > 2)
            textRemainTime = UtilModel.String.GetRemainTimeLocalizationText(currentTime, Model.EndTime, TimeStringTextType.One);
        else if (remainTime.TotalDays > 1)
            textRemainTime = UtilModel.String.GetRemainTimeLocalizationText(currentTime, Model.EndTime, TimeStringTextType.Two);
        else if (remainTime.TotalMinutes > 1)
            textRemainTime = UtilModel.String.GetRemainTimeToString(currentTime, Model.EndTime, TimeStringFormType.hh_mm_ss);
        else
            textRemainTime = UtilModel.String.GetRemainTimeLocalizationText(new TimeSpan(0, 1, 0), TimeStringTextType.One);

        Model.SetTextRemainTime(textRemainTime);
    }

    private void OnEventContentsShop()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ContentsShopView);
        ContentsShopViewModel contentsShopModel = controller.GetModel<ContentsShopViewModel>();
        contentsShopModel.SetCurrentShop(ContentsShopCategory.Arena);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventDefenceDeck()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.TeamUpdateView);
        TeamUpdateViewModel model = controller.GetModel<TeamUpdateViewModel>();
        model.SetCurrentDeckType(DeckType.ArenaDefence);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventHistory()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ArenaHistoryPopup);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventRanking()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ArenaRankingPopup);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventRewardList()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ArenaRewardListPopup);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventMatchSelect()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ArenaMatchSelectPopup);
        ArenaMatchSelectPopupModel model = controller.GetModel<ArenaMatchSelectPopupModel>();

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    public void HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case CommonObserverID.TimeRefresh:
                {
                    if (!checkTimeRefresh)
                        return;

                    if (View != null && View.IsPlayingAnimation)
                        return;

                    if (!CheckArenaOpen())
                        return;

                    UpdateArenaView();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}