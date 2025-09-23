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
using IronJade.LowLevel.Server.Web;
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class SkillMaterialDungeonStageInfoController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.SkillMaterialDungeonStageInfoView; } }
    public override void SetModel() { SetModel(new SkillMaterialDungeonStageInfoViewModel()); }
    private SkillMaterialDungeonStageInfoView View { get { return base.BaseView as SkillMaterialDungeonStageInfoView; } }
    private SkillMaterialDungeonStageInfoViewModel Model { get { return GetModel<SkillMaterialDungeonStageInfoViewModel>(); } }
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

        Model.SetEventOpenThumbnailDetail(OnEventOpenThumbnailDetail);

        Model.SetBossThumbnailPath();
        Model.SetBuffElementTypeList();
        Model.SetTeamGroup();
        Model.SetTeam();
        //Model.SetTeamElementBuffIcon();
        Model.SetTicket();
        Model.SetEnemyInfo();
        Model.SetRewards();

        Model.SetEventClose(OnEventClose);
        Model.SetEventOpenTeamUpdate(OnEventOpenTeamUpdate);
        Model.SetEventEnemyInfoPopup(OnEventEnemyInfoPopup);
        Model.SetEventEnterDungeon(OnEventEnterDungeon);
        Model.SetEventFastBattleDungeon(OnEventFastBattleDungeon);
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
        Model.SetTicket();
        View.ShowAsync().Forget();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);

        return await base.Exit(onEventExtra);
    }


    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/SkillMaterialDungeon/SkillMaterialDungeonStageInfoView";
    }

    private async UniTask RefreshSkillMaterialDungeon()
    {
        await RequestSkillMaterialDungeonGet();

        Model.SetTicket();

        await View.ShowAsync();
    }

    private void ShowToastMessageBox(string message)
    {
        MessageBoxManager.ShowToastMessage(message);
    }

    private void OnConfirmFastBattle(int count)
    {
        RequestFastBattle(count).Forget();
    }

    public void OnEventClose()
    {
        Exit().Forget();
    }

    public void OnEventOpenTeamUpdate()
    {
        BaseController teamUpdateController = UIManager.Instance.GetController(UIType.TeamUpdateView);
        TeamUpdateViewModel viewModel = teamUpdateController.GetModel<TeamUpdateViewModel>();
        viewModel.SetUser(Model.User);
        viewModel.SetDungeonTableData(Model.CurrentDungeonData);
        viewModel.SetCurrentDeckType(DeckType.SkillMaterialDungeon);
        viewModel.SetOpenPresetNumber(Model.TeamGroup.PresetNumber);
        viewModel.SetFixedTeam(Model.FixedTeam);
        UIManager.Instance.EnterAsync(teamUpdateController).Forget();
    }

    public void OnEventEnemyInfoPopup()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.EnemyInfoPopup);
        EnemyInfoPopupModel model = controller.GetModel<EnemyInfoPopupModel>();
        model.SetShowSkillCategoryList(new List<SkillCategory>
        {
            SkillCategory.Active1,
            SkillCategory.Active2,
            SkillCategory.Passive1,
            SkillCategory.Passive2
        });
        model.SetEnemyInfoPopup(Model.CurrentDungeonData);
        model.SelectWave(0);
        UIManager.Instance.EnterAsync(controller).Forget();
    }


    public ThumbnailSelectType OnEventOpenThumbnailDetail(BaseThumbnailUnitModel unitmodel)
    {
        var thumbnailUnitModel = Model.GetStageEnemyUnitModel(unitmodel.Id);

        if (thumbnailUnitModel == null)
            return ThumbnailSelectType.None;

        BaseController enemyInfoDetailPopup = UIManager.Instance.GetController(UIType.EnemyInfoDetailPopup);
        EnemyInfoDetailPopupModel model = enemyInfoDetailPopup.GetModel<EnemyInfoDetailPopupModel>();

        model.SetEnemyInfoDetailModel(thumbnailUnitModel.DataId);
        UIManager.Instance.EnterAsync(enemyInfoDetailPopup).Forget();

        return ThumbnailSelectType.None;
    }

    public void OnEventEnterDungeon()
    {
        if (Model.Ticket.Count == 0)
        {
            ShowToastMessageBox(Model.TextLackTicket);
            return;
        }

        if (Model.Team.IsAllEmptySlot())
        {
            ShowToastMessageBox(Model.TextEmptyTeam);
            return;
        }

        RequestSkillMaterialDungeonEnter().Forget();
    }

    public void OnEventFastBattleDungeon()
    {
        if (!Model.IsFastBattleEnterable)
        {
            ShowToastMessageBox(Model.TextNeedTodayStageClear);
            return;
        }

        if (Model.Team.IsAllEmptySlot())
        {
            ShowToastMessageBox(Model.TextEmptyTeam);
            return;
        }

        if (Model.Ticket.Count == 0)
        {
            ShowToastMessageBox(Model.TextLackTicket);
            return;
        }

        BaseController useNumberPopup = UIManager.Instance.GetController(UIType.UseNumberPopup);
        UseNumberPopupModel useNumberPopupModel = useNumberPopup.GetModel<UseNumberPopupModel>();
        useNumberPopupModel.SetFastBattle((int)Model.Ticket.Count, Model.TextChooseFastBattleCount, OnConfirmFastBattle);
        var chargeItemTable = TableManager.Instance.GetTable<ChargeItemTable>();
        var tableData = chargeItemTable.GetDataByID((int)ChargeItemDefine.CHARGEITEM_DUNGEON_TICKET_SKILL_MATERIAL_DUNGEON);
        string textHoldCount = string.Format("{0} {1}",
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_SKILLMATERIALDUNGEON_MY_TICKET_COUNT),
            TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_TEXT_ETC_PROGRESS, (int)Model.Ticket.Count, tableData.GetMAX_COUNT()));
        useNumberPopupModel.SetTextTicketCount(textHoldCount);
        UIManager.Instance.EnterAsync(useNumberPopup).Forget();
    }

    private async UniTask RequestSkillMaterialDungeonGet()
    {
        SkillMaterialDungeonGetProcess skillMaterialDungeonGetProcess = NetworkManager.Web.GetProcess<SkillMaterialDungeonGetProcess>();
        if (await skillMaterialDungeonGetProcess.OnNetworkAsyncRequest())
            skillMaterialDungeonGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestSkillMaterialDungeonEnter()
    {
        BaseProcess getEnterProcess = NetworkManager.Web.GetProcess(WebProcess.SkillMaterialDungeonEnter);
        EnterSkillMaterialsDungeonInDto dungeonEnterDto = new EnterSkillMaterialsDungeonInDto(Model.CurrentDungeonData.GetID());
        getEnterProcess.SetPacket(dungeonEnterDto);

        if (await getEnterProcess.OnNetworkAsyncRequest())
        {
            getEnterProcess.OnNetworkResponse();

            Team team = Model.TeamGroup.GetCurrentTeam();
            await BattleManager.Instance.EnterSkillMaterialDungeonBattle(Model.CurrentGroupData.GetID(), Model.CurrentDungeonData.GetID(), team, true);
        }
    }

    private async UniTask<IReadOnlyList<Goods>> RequestClearSkillMaterialDungeon()
    {
        ClearSkillMaterialsDungeonInDto reqeustDto = new ClearSkillMaterialsDungeonInDto
        {
            dataSkillMaterialsDungeonId = Model.CurrentGroupData.GetID(),
            dataDungeonId = Model.CurrentDungeonData.GetID()
        };

        SkillMaterialDungeonClearProcess clearProcess = NetworkManager.Web.GetProcess<SkillMaterialDungeonClearProcess>();
        clearProcess.Request.SetPacket(reqeustDto);

        if (await clearProcess.OnNetworkAsyncRequest())
        {
            GoodsGeneratorModel goodsGenerator = new GoodsGeneratorModel(Model.User);
            IReadOnlyList<Goods> goods = goodsGenerator.GetObtainGoodsByGoodsDto(clearProcess.Response.data.goods);

            clearProcess.OnNetworkResponse();

            Item ticket = PlayerManager.Instance.MyPlayer.User.GetGoodsByTypeValue<Item>(Model.Ticket.GoodsType, Model.Ticket.DataId);
            Model.SetTicket(ticket);
            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            //View.ShowAsync().Forget();

            return goods;
        }
        else
        {
            await UniTask.WaitWhile(() => { return UIManager.Instance.CheckOpenCurrentUI(UIType.MessageBoxPopup); });
            return null;
        }
    }

    private async UniTask RequestFastBattle(int count)
    {
        InstantClearSkillMaterialsDungeonInDto reqeustDto = new InstantClearSkillMaterialsDungeonInDto
        {
            dataDungeonId = Model.CurrentDungeonData.GetID(),
            clearCount = count
        };

        SkillMaterialDungeonFastBattleProcess instantClearProcess = NetworkManager.Web.GetProcess<SkillMaterialDungeonFastBattleProcess>();
        instantClearProcess.Request.SetPacket(reqeustDto);
        if (await instantClearProcess.OnNetworkAsyncRequest())
        {
            GoodsGeneratorModel goodsGenerator = new GoodsGeneratorModel(Model.User);
            BaseController rewardController = UIManager.Instance.GetController(UIType.RewardPopup);
            RewardPopupModel rewardPopupModel = rewardController.GetModel<RewardPopupModel>();
            IReadOnlyList<Goods> goodsModel = goodsGenerator.GetObtainGoodsByGoodsDto(instantClearProcess.GetResponse<SkillMaterialDungeonFastBattleResponse>().data.goods);
            rewardPopupModel.SetThumbnailItemUnitModels(goodsModel);
            UIManager.Instance.EnterAsync(rewardController).Forget();

            instantClearProcess.OnNetworkResponse();

            Refresh();
        }
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case CommonObserverID.DailyRefreshData:
                {
                    RefreshSkillMaterialDungeon();
                    break;
                }
        }
    }
    #endregion Coding rule : Function
}