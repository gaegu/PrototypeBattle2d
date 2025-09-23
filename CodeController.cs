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
using IronJade.UI.Core;
using UnityEngine;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CodeController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CodeView; } }
    private CodeView View { get { return base.BaseView as CodeView; } }
    protected CodeViewModel Model { get; private set; }
    public CodeController() { Model = GetModel<CodeViewModel>(); }
    public CodeController(BaseModel baseModel) : base(baseModel) { }
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
        ObserverManager.AddObserver(CommonObserverID.TimeRefresh, this);
        ObserverManager.AddObserver(CommonObserverID.DailyRefreshData, this);

        Model.SetOnClickEnterBattle(OnClickEnterBattle);
        Model.SetOnEventPresetCharacterSlot(OnEventPresetCharacterSlotSelect);
        Model.SetOnClickEnemyInfo(OnClickEnemyInfo);
        Model.SetOnClickFastBattle(OnClickFastBattle);
        Model.SetOnClickRewardInfo(OnClickRewardInfo);
        Model.SetOnClickClose(OnClickClose);
        Model.SetOnClickButtonDimCalculate(OnClickButtonDimCalculate);
        Model.SetOnClickBattleRecord(OnClickBattleRecord);

        Model.SetOnClickElementAdvantage(OnClickElementalAdvantagePopup);
        Model.SetOnClickWeakPoint(OnClickWeakPointPopup);
    }

    private void UpdateModel()
    {
        User user = PlayerManager.Instance.MyPlayer.User;

        if (user != null)
        {
            var codeDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<CodeDungeonGroupModel>();
            var codeDungeonModel = codeDungeonGroupModel.DungeonModelDic[Model.CurrentDifficulty];

            Model.SetCodeDungeonModel(codeDungeonModel);
            Model.InitByData();
            Model.InitByUser(user);

            CheckCaculating();

            IronJade.Debug.Log("Update Code View Model");
        }
    }

    public override async UniTask LoadingProcess()
    {
        UpdateModel();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override async UniTask BackProcess()
    {
        if (View)
            View.RefreshParticles();

        await Process();
    }

    public override async void Refresh()
    {
        await PlayShowAsync();
    }

    public override async UniTask PlayShowAsync()
    {
        SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_CODE_BGM);

        if (View)
            View.PlayShowAnimation();
    }

    public override async UniTask PlayBackShowAsync()
    {
        await PlayShowAsync();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/Code/CodeView";
    }

    public void SetRemainTimeText()
    {
        DateTime nowTime = NetworkManager.Web.ServerTimeUTC;
        DateTime endTime = TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);

        Model.SetRemainTimeText(UtilModel.String.GetRemainTimeLocalizationText(nowTime, endTime));
    }

    public void ShowStageInfoPopup()
    {
        BaseController stageInfoController = UIManager.Instance.GetController(UIType.StageInfoWindow);

        StageInfoPopupModel stageInfoPopupModel = stageInfoController.GetModel<StageInfoPopupModel>();
        stageInfoPopupModel.SetDungeonData();

        UIManager.Instance.EnterAsync(stageInfoController).Forget();
    }

    private void CheckCaculating()
    {
        if (View == null)
            return;

        if (PrologueManager.Instance.IsProgressing)
            return;

        if (NetworkManager.Web.ServerTimeUTC.Hour == 19 && NetworkManager.Web.ServerTimeUTC.Minute > 50)
        {
            Model.SetIsCalculating(true);
            View.UpdateButtonDimCalculate();
        }
        else if (Model.IsCalculating)
        {
            Model.SetIsCalculating(false);
            View.UpdateButtonDimCalculate();
        }
    }

    private void CheckRemainTime()
    {
        if (View == null)
            return;

        SetRemainTimeText();

        View.UpdateRemainTimeText();
    }

    private void OnEventPresetCharacterSlotSelect(int selectPresetSlot)
    {
        if (Model.TeamGroup.PresetNumber == selectPresetSlot)
            return;

        // 프리셋 변경
        User user = PlayerManager.Instance.MyPlayer.User;
        user.TeamModel.GetTeamGroupByType(Model.DeckType).SetPresetNumber(selectPresetSlot);

        Model.SetUser(user);
        Model.SetTeamGroup(user);

        View.RefreshPreset();
    }

    public void OnClickEnterBattle(bool isPractice)
    {
        //입장 횟수 초과..
        if (Model.Ticket == null || (Model.Ticket.Count == 0 && !isPractice))
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_NOTICE_ENTER_NOT_TICKET);
            return;
        }

        if (Model.IsCalculating)
            return;

        Team team = Model.TeamGroup.GetCurrentTeam();

        if (team == null || team.IsAllEmptySlot())
        {
            MessageBoxManager.ShowYesBox(TableManager.Instance.GetLocalization(LocalizationDefine.UI_LABEL_BATTLEINFO_NEED_BATTLETEAM_SET)).Forget();
            return;
        }

        if (Model.IsPrologueSequence)
        {
            Model.OnPrologueSequenceComplete();
            return;
        }

        BattleTeamGeneratorModel generatorModel = new BattleTeamGeneratorModel();
        List<WaveInfo> waveInfos = generatorModel.CreateWaveInfosByDungeonId(team, Model.CurrentDungeonData);

        BattleResultCodeInfoModel battleResultCodeRedInfoModel = new BattleResultCodeInfoModel();
        battleResultCodeRedInfoModel.SetOnRequestReward(RequestClearRewardGoods);
        battleResultCodeRedInfoModel.SetOnClickNextBattle(() => { OnClickEnterBattle(isPractice); });
        battleResultCodeRedInfoModel.SetOnGetRemainCountText(() => { return Model.RemainTicketCountText; });
        //battleResultCodeRedInfoModel.SetOnGetRewardProgressLevel(Model.GetRewardProgressLevelByDamage);
        battleResultCodeRedInfoModel.SetOnGetRewardProgressLevel(Model.GetRewardProgressLevelByKillCount);      //250619 LJH: CBT용 코드보상 킬카운트로 지급하도록 처리
        //battleResultCodeRedInfoModel.SetOnGetTotalDamage(Model.GetReplaceDamageByTotalDamage);
        //battleResultCodeRedInfoModel.SetOnGetTotalDamage(Model.CurrentKillCount);
        battleResultCodeRedInfoModel.SetBossThumbnailPath(Model.BossThumbnailPath);
        battleResultCodeRedInfoModel.SetBossName(Model.BossName);
        battleResultCodeRedInfoModel.SetIsPractice(isPractice);

        BattleInfo battleInfo = new BattleInfo();
        battleInfo.SetDungeon(Model.CurrentDungeonData.GetID());
        battleInfo.SetTeamPlayer(team);
        battleInfo.SetDungeonSetData();
        battleInfo.SetWaveInfos(waveInfos);
        battleInfo.SetBattleResultInfoModel(battleResultCodeRedInfoModel);
        battleInfo.SetCodeTableData(Model.CurrentCodeTableData);
        battleInfo.SetBattlePowerBalance();

        if (isPractice)
            EnterPractice(battleInfo).Forget();
        else
            RequestCodeEnter(battleInfo).Forget();
    }

    private async UniTask<bool> RequestCodeEnter(BattleInfo battleInfo)
    {
        BaseProcess codeEnterProcess = NetworkManager.Web.GetProcess(WebProcess.CodeEnter);

        CodeEnterRequest request = codeEnterProcess.GetRequest<CodeEnterRequest>();
        request.SetCodeEnterDto(Model.CurrentCodeTableData.GetID(), Model.CurrentDungeonData.GetID());

        if (await codeEnterProcess.OnNetworkAsyncRequest())
        {
            if (battleInfo != null)
            {
                TownFlowModel townFlowModel = FlowManager.Instance.GetCurrentFlow().GetModel<TownFlowModel>();
                townFlowModel.SetBattleInfo(battleInfo);
                await FlowManager.Instance.ChangeStateProcess(FlowState.Battle);
            }

            return true;
        }

        return false;
    }

    private async UniTask EnterPractice(BattleInfo battleInfo)
    {
        TownFlowModel townFlowModel = FlowManager.Instance.GetCurrentFlow().GetModel<TownFlowModel>();
        townFlowModel.SetBattleInfo(battleInfo);
        await FlowManager.Instance.ChangeStateProcess(FlowState.Battle);
    }

    private async UniTaskVoid RequestFastBattle()
    {
        BaseController rewardController = UIManager.Instance.GetController(UIType.RewardPopup);
        RewardPopupModel rewardPopupModel = rewardController.GetModel<RewardPopupModel>();

        IReadOnlyList<Goods> goods = await RequestClearRewardGoods(Model.CurrentKillCount);

        UpdateModel();
        View.ShowAsync().Forget();

        if (goods != null && goods.Count > 0)
        {
            rewardPopupModel.SetThumbnailItemUnitModels(goods);
            UIManager.Instance.EnterAsync(rewardController).Forget();
        }
    }

    private async UniTask<IReadOnlyList<Goods>> RequestClearRewardGoods(BattleInfo battleInfo)
    {
        return await RequestClearRewardGoods(battleInfo.RaidTotalKillCount);
    }

    private async UniTask<IReadOnlyList<Goods>> RequestClearRewardGoods(int killCount)
    {
        CodeClearInDto codeRedClearInDto = new CodeClearInDto
        {
            killCount = killCount,
            dataCodeId = Model.CurrentCodeTableData.GetID(),
            dataDungeonId = Model.CurrentDungeonData.GetID(),
        };

        CodeClearProcess codeRedClearProcess = NetworkManager.Web.GetProcess<CodeClearProcess>();
        codeRedClearProcess.SetPacket(codeRedClearInDto);

        if (await codeRedClearProcess.OnNetworkAsyncRequest())
        {
            var goodsGenerator = new GoodsGeneratorModel(PlayerManager.Instance.MyPlayer.User);
            var codeClearResponse = codeRedClearProcess.GetResponse<CodeClearResponse>();
            var goods = goodsGenerator.GetObtainGoodsByGoodsDto(codeClearResponse.data.goods);

            codeRedClearProcess.OnNetworkResponse();
            return goods;
        }

        return null;
    }

    #region OnClick

    public async void OnClickFastBattle()
    {
        //입장 횟수 초과
        if (Model.Ticket.Count == 0)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_NOTICE_ENTER_NOT_TICKET);
            return;
        }

        //전투 기록 없음
        if (Model.TodayEnterCount == 0)
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_NOTICE_ENTER_FIRST_SCORE);
            return;
        }

        string msg = TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_NOTICE_REWARD_LAST_SCORE,
            Model.GetRewardProgressLevelByKillCount(Model.CurrentKillCount) + 1,
            Model.CurrentKillCount);

        await MessageBoxManager.ShowYesNoBox(msg, onEventConfirm: () =>
        {
            RequestFastBattle().Forget();
        });
    }

    public void OnClickRewardInfo()
    {
        BaseController rewardScrollController = UIManager.Instance.GetController(UIType.RewardScrollPopup);
        RewardScrollPopupModel rewardScrollPopupModel = rewardScrollController.GetModel<RewardScrollPopupModel>();

        if (Model.GetCurrentRewardTableDatas() == null)
            return;

        if (!rewardScrollPopupModel.IsInit(UIType.CodeView))
        {
            rewardScrollPopupModel.Clear();

            string[] rewardRangeByDamageArray = Model.GetRewardRangeString();

            if (rewardRangeByDamageArray == null)
            {
                IronJade.Debug.LogError("Failed to get rewardRangeByDamageArray");
                return;
            }

            rewardScrollPopupModel.InitParentUIType(UIType.CodeView);

            rewardScrollPopupModel.SetUnitColumnNames(new string[] {
                TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_STAGE),
                TableManager.Instance.GetLocalization(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_REWARD),});

            rewardScrollPopupModel.SetTitleText(TableManager.Instance.
                GetLocalization(LocalizationDefine.UI_LABEL_BATTLEINFO_REWARDINFO));

            var rewardScrollUnitModels = rewardScrollPopupModel.CreateRewardScrollUnitModels(
                    Model.GetCurrentRewardTableDatas(),
                    rewardNameType: RewardTitleType.LocalizationText,
                    rewardNameLocal: LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_STAGE_CLEAR,
                    rewardName_sub: rewardRangeByDamageArray,
                    onSelectIndexReward: OnSelectReward,
                    randomRewardInfo: Model.CreateRandomRewardInfos()
                    );

            rewardScrollPopupModel.AddSingleTab(rewardScrollUnitModels);
        }

        UIManager.Instance.EnterAsync(rewardScrollController).Forget();
    }

    public void OnClickEnemyInfo()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.EnemyInfoDetailPopup);
        EnemyInfoDetailPopupModel model = controller.GetModel<EnemyInfoDetailPopupModel>();

        model.SetEnemyInfoDetailModel(Model.BossData.GetID());
        model.SetIsCode(true);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnClickButtonDimCalculate()
    {
        MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_SINGLERAID_NOTICE_ENTER_NOT_SETTLEMENT);
    }

    private void OnClickBattleRecord()
    {
        BaseController battleRecordController = UIManager.Instance.GetController(UIType.BattleRecordPopup);
        BattleRecordPopupModel model = battleRecordController.GetModel<BattleRecordPopupModel>();

        model.SetBattleRecordType(BattleRecordPopupModel.RecordType.Code);
        model.SetCodeTableDataId(Model.CurrentCodeTableData.GetID());
        model.SetBossThumbnailPath(Model.CurrentCodeTableData.GetBOSS_ENEMY_INFO_THUMBNAIL());
        //model.SetOnGetDamageByRatio(Model.GetKillCount);

        UIManager.Instance.EnterAsync(battleRecordController).Forget();
    }


    private void OnClickElementalAdvantagePopup()
    {
        BaseController elementalAffinityPopup = UIManager.Instance.GetController(UIType.ElementAdvantageInfoPopup);
        var model = elementalAffinityPopup.GetModel<ElementAdvantageInfoPopupModel>();
        model.SetDungeonData(Model.CurrentDungeonData);

        UIManager.Instance.EnterAsync(elementalAffinityPopup).Forget();
    }

    private void OnClickWeakPointPopup()
    {
        BaseController weakPointPopup = UIManager.Instance.GetController(UIType.WeakPointPopup);
        var model = weakPointPopup.GetModel<WeakPointPopupModel>();
        model.SetUser(Model.User);
        model.SetDungeonTableData(Model.CurrentDungeonData);

        UIManager.Instance.EnterAsync(weakPointPopup).Forget();
    }


    public void OnClickClose()
    {
        Exit().Forget();
    }

    public ThumbnailSelectType OnSelectReward(BaseThumbnailUnitModel baseThumbnailUnitModel, int index)
    {
        IronJade.Debug.Log(index);

        switch (baseThumbnailUnitModel.Goods)
        {
            case Item:
                OnSelectRewardItem(baseThumbnailUnitModel, index);
                break;

            default:
                OnClickGoods(baseThumbnailUnitModel);
                break;
        }

        return ThumbnailSelectType.None;
    }

    private void OnSelectRewardItem(BaseThumbnailUnitModel baseThumbnailUnitModel, int index)
    {
        Item item = (Item)baseThumbnailUnitModel.Goods;

        CodeDamageRewardTableData codeDamageRewaraData = TableManager.Instance.GetTable<CodeDamageRewardTable>()
            .GetDataByID(Model.CurrentCodeTableData.GetCODE_DAMAGE_REWARD(index));

        RewardTableData rewardTableData = TableManager.Instance.GetTable<RewardTable>()
            .GetDataByID(codeDamageRewaraData.GetBONUS_EQUIPMENT_REWARD());

        bool isRandomReward = !rewardTableData.IsNull() &&
            rewardTableData.GetGOODS_VALUECount() > 0 &&
            rewardTableData.GetGOODS_VALUE(0) == item.DataId;

        if (item.ItemType == ItemType.Consume)
        {
            if (isRandomReward)
            {
                OnClickConsumeRandomBox(baseThumbnailUnitModel, rewardTableData, codeDamageRewaraData);
            }
            else
            {
                OnClickConsume(item);
            }
        }
        else
        {
            OnClickGoods(baseThumbnailUnitModel);
        }
    }

    private void OnClickGoods(BaseThumbnailUnitModel baseThumbnailUnitModel)
    {
        if (UIManager.Instance.CheckOpenCurrentUI(UIType.ItemToolTipPopup))
            return;

        BaseController itemToolTipController = UIManager.Instance.GetController(UIType.ItemToolTipPopup);
        ItemToolTipPopupModel model = itemToolTipController.GetModel<ItemToolTipPopupModel>();
        model.SetGoods(baseThumbnailUnitModel.Goods);
        model.SetThumbnail();

        UIManager.Instance.EnterAsync(itemToolTipController).Forget();
    }

    private void OnClickConsume(Item item)
    {
        if (UIManager.Instance.CheckOpenCurrentUI(UIType.ConsumeItemPopup))
            return;

        BaseController consumeItemPopup = UIManager.Instance.GetController(UIType.ConsumeItemPopup);
        ConsumeItemPopupModel model = consumeItemPopup.GetModel<ConsumeItemPopupModel>();
        model.SetItem(item);
        model.SetIsPreivew(true);
        model.SetConsumeItemModel();

        UIManager.Instance.EnterAsync(consumeItemPopup).Forget();
    }

    private void OnClickConsumeRandomBox(BaseThumbnailUnitModel baseThumbnailUnitModel, RewardTableData additionalRewardTableData, CodeDamageRewardTableData codeDamageRewardTableData)
    {
        if (UIManager.Instance.CheckOpenCurrentUI(UIType.ConsumeItemPopup))
            return;

        BaseController consumeItemPopup = UIManager.Instance.GetController(UIType.ConsumeItemPopup);
        ConsumeItemPopupModel model = consumeItemPopup.GetModel<ConsumeItemPopupModel>();
        model.SetItem((Item)baseThumbnailUnitModel.Goods);

        float itemBonusProb = codeDamageRewardTableData.GetBONUS_ITEM_RATE() / 1000;

        model.SetIsPreivew(true);
        model.SetConsumeItemModel();

        if (model.RandomRewardModel != null)
        {
            if (itemBonusProb > 0)
                model.RandomRewardModel.AddThubmbnailRewards(additionalRewardTableData);

            model.RandomRewardModel.SetProbabilityInfoGroupUnitModel(CreateRandomBonusRewardProbGroupModel(codeDamageRewardTableData));
        }

        model.SetOnClickShowProbability(OnClickProbabilityInfo);

        UIManager.Instance.EnterAsync(consumeItemPopup).Forget();
    }

    private ProbabilityInfoGroupUnitModel CreateRandomBonusRewardProbGroupModel(CodeDamageRewardTableData codeDamageRewardTableData)
    {
        float equipBonusProb = codeDamageRewardTableData.GetBONUS_EQUIPMENT_RATE() / 1000;
        float itemBonusProb = codeDamageRewardTableData.GetBONUS_ITEM_RATE() / 1000;
        float totalProb = equipBonusProb + itemBonusProb;

        if (totalProb <= 0)
            return null;

        ProbabilityInfoPopupModel model = new ProbabilityInfoPopupModel();

        ProbabilityInfoGroupUnitModel equipProbGroup = new ProbabilityInfoGroupUnitModel();

        // 확률정보 장비 , 아이템 각각 만들고 합쳐서 보여주기
        if (equipBonusProb > 0)
        {
            var gachaProbabilityInfoGroupGenerator = new GachaEquipmentProbInfoGroupGenerator();
            var gachaEquipementData = TableManager.Instance.GetTable<GachaEquipmentTable>().GetDataByID(codeDamageRewardTableData.GetBONUS_EQUIPMENT_GACHA_REWARD());
            equipProbGroup = gachaProbabilityInfoGroupGenerator.Generate(gachaEquipementData, equipBonusProb);
        }

        ProbabilityInfoGroupUnitModel itemProbGroup = new ProbabilityInfoGroupUnitModel();

        if (itemBonusProb > 0)
        {
            var itemProbabilityInfoGroupGenerator = new GachaItemProbInfoGroupGenerator();
            var itemRewardData = TableManager.Instance.GetTable<RewardTable>().GetDataByID(codeDamageRewardTableData.GetBONUS_ITEM_REWARD());
            itemProbGroup = itemProbabilityInfoGroupGenerator.Generate(itemRewardData, itemBonusProb);
        }

        var unionProbabilityInfoGroup = model.UnionProbabilityInfoGroup(new ProbabilityInfoGroupUnitModel[] { equipProbGroup, itemProbGroup });

        return unionProbabilityInfoGroup;
    }

    private void OnClickProbabilityInfo(List<ProbabilityInfoGroupUnitModel> prob)
    {
        BaseController baseController = UIManager.Instance.GetController(UIType.ProbabilityInfoPopup);
        ProbabilityInfoPopupModel model = baseController.GetModel<ProbabilityInfoPopupModel>();

        model.Clear();
        model.SetProbabilityInfoGroups(prob);

        UIManager.Instance.EnterAsync(baseController).Forget();
    }

    #endregion

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case CommonObserverID.TimeRefresh:
                CheckCaculating();
                CheckRemainTime();
                break;

            //던전이 바뀌면 갱신
            case CommonObserverID.DailyRefreshData:
                UpdateModel();
                View.ShowAsync().Forget();
                break;
        }
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.TimeRefresh, this);
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);
        TokenPool.Cancel(GetHashCode());

        return await base.Exit(onEventExtra);
    }

    #endregion Coding rule : Function
}