//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.LowLevel.Server.Web;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class RankingDetailController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.RankingDetailView; } }
    public override void SetModel() { SetModel(new RankingDetailViewModel()); }
    private RankingDetailView View { get { return base.BaseView as RankingDetailView; } }
    private RankingDetailViewModel Model { get { return GetModel<RankingDetailViewModel>(); } }
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
        SetCurrencyUnits();

        Model.SetEventGetReward(OnEventGetReward);
        Model.SetEventRewardInfo(OnEventRewardInfo);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestRankingDetail();
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Ranking/RankingDetailView";
    }

    private void SetCurrencyUnits()
    {
        CurrencyGeneratorModel currencyGenerator = new CurrencyGeneratorModel(PlayerManager.Instance.MyPlayer.User);
        CurrencyUnitModel goldUnitModel = currencyGenerator.GetCurrencyUnitModelByType(CurrencyType.Gold);
        CurrencyUnitModel cashUnitModel = currencyGenerator.GetCurrencyUnitModelByType(CurrencyType.Cash);
        Model.SetCurrencyUnitModels(new CurrencyUnitModel[2] { goldUnitModel, cashUnitModel });
    }

    private void OnEventGetReward()
    {
        if (Model.HasRewardExists)
        {
            RequestGetRewards().Forget();
        }
        else
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_RANKINGDETAILVIEW_NO_REWARD);
        }
    }

    private void OnEventRewardInfo()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.RankingMissionPopup);
        controller.GetModel<RankingMissionPopupModel>().SetRankingType(Model.RankingType);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private async UniTask RequestRankingDetail()
    {
        BaseProcess getRankingListProcess = NetworkManager.Web.GetProcess(WebProcess.RankingsListGet);           //기존 : __Process process = NetworkManager.Web.GetProcess<__Process>();
        getRankingListProcess.SetPacket(new GetRankingListInDto(Model.RankingType, 1));                                     //기존에 Request코드에 Request로 넣었는데, 

        if (await getRankingListProcess.OnNetworkAsyncRequest())
        {
            RankingsListGetResponse response = getRankingListProcess.GetResponse<RankingsListGetResponse>();
            Model.SetRankingDetailModel(response.data, PlayerManager.Instance.MyPlayer.User);
        }
    }

    private int GetMyRankingContentsValue(RankingMissionType rankingMission)
    {
        InfinityDungeonGroupModel infinityDungeonModel = DungeonManager.Instance.GetDungeonGroupModel<InfinityDungeonGroupModel>();

        switch (rankingMission)
        {
            case RankingMissionType.StageDungeonHard:
                return DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>().GetClearDataStageDungeonId(true);

            case RankingMissionType.CharacterLicensePointBlack:
                return GetCharacterLicensePoint(LicenseType.Black);

            case RankingMissionType.CharacterLicensePointBlue:
                return GetCharacterLicensePoint(LicenseType.Blue);

            case RankingMissionType.CharacterLicensePointPrism:
                return GetCharacterLicensePoint(LicenseType.Prism);

            case RankingMissionType.InfinityDungeonGeneral:
                return infinityDungeonModel.GetClearedHighestDungeonDataId(InfinityCircuitType.General);

            case RankingMissionType.InfinityDungeonBlack:
                return infinityDungeonModel.GetClearedHighestDungeonDataId(InfinityCircuitType.Black);

            case RankingMissionType.InfinityDungeonBlue:
                return infinityDungeonModel.GetClearedHighestDungeonDataId(InfinityCircuitType.Blue);

            case RankingMissionType.InfinityDungeonPrism:
                return infinityDungeonModel.GetClearedHighestDungeonDataId(InfinityCircuitType.Prism);

            default:
                return 0;
        }
    }

    private int GetCharacterLicensePoint(LicenseType licenseType)
    {
        RankingBalanceTable rankingBalanceTable = TableManager.Instance.GetTable<RankingBalanceTable>();
        int statUpgradePoint = (int)rankingBalanceTable.GetDataByID((int)RankingBalanceDefine.RANKINGBALANCE_POINT_ADDONBLACK).GetINDEX(0);

        //캐릭터 레벨관련 포인트
        CharacterModel characterModel = PlayerManager.Instance.MyPlayer.User.CharacterModel;
        int totalPoint = 0;
        for (int i = 0; i < characterModel.Count; i++)
        {
            Character character = characterModel.GetGoodsByIndex(i);
            if (character.License == licenseType)
                totalPoint += GetCharacterPoint(character);
        }

        //애드온블랙(라이센스) 레벨 포인트
        totalPoint += statUpgradePoint * StatUpgradeManager.Instance.GetLicensePoint(licenseType);

        return totalPoint;
    }

    private int GetCharacterPoint(Character character)
    {
        RankingBalanceTable rankingBalanceTable = TableManager.Instance.GetTable<RankingBalanceTable>();
        RankingBalanceTableData tierPointData = rankingBalanceTable.GetDataByID((int)RankingBalanceDefine.RANKINGBALANCE_POINT_TIER);
        int levelPoint = (int)rankingBalanceTable.GetDataByID((int)RankingBalanceDefine.RANKINGBALANCE_POINT_LEVELUP).GetINDEX(0);
        int skillLevelPoint = (int)rankingBalanceTable.GetDataByID((int)RankingBalanceDefine.RANKINGBALANCE_POINT_SKILL_LEVELUP).GetINDEX(0);

        int totalPoint = 0;

        //1. 캐릭터 티어 포인트
        totalPoint += (int)tierPointData.GetINDEX(character.Tier - 1);

        //2. 캐릭터 레벨 포인트
        totalPoint += (character.Level - 1) * levelPoint;

        //3. 캐릭터 스킬레벨 포인트
        int upgradeCount = character.GetSkillUpgradeCount(SkillCategory.Passive1) +
            character.GetSkillUpgradeCount(SkillCategory.Passive2) +
            character.GetSkillUpgradeCount(SkillCategory.Active2) +
            character.GetSkillUpgradeCount(SkillCategory.SpecialTag);

        totalPoint += upgradeCount * skillLevelPoint;

        return totalPoint;
    }

    private async UniTask RequestGetRewards()
    {
        BaseProcess rewardGetProcess = NetworkManager.Web.GetProcess(WebProcess.RankingsMissionReceiveReward);
        rewardGetProcess.SetPacket(new RankingMissionReceiveRewardInDto(Model.RankingType));

        if (await rewardGetProcess.OnNetworkAsyncRequest())
        {
            rewardGetProcess.OnNetworkResponse();
            Model.SetRewardExists(false);
            View.ShowReward();
        }
    }
    #endregion Coding rule : Function
}