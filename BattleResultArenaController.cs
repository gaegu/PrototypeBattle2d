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
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class BattleResultArenaController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.BattleResultArenaPopup; } }
    public override void SetModel() { SetModel(new BattleResultArenaPopupModel()); }
    private BattleResultArenaPopup View { get { return base.BaseView as BattleResultArenaPopup; } }
    private BattleResultArenaPopupModel Model { get { return GetModel<BattleResultArenaPopupModel>(); } }
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
        Model.SetEventExit(OnEventExit);
        Model.SetEventStatistics(OnEventStatistics);

        SetBasicInfo();
    }

    public override async UniTask LoadingProcess()
    {
        if (Model.IsWin)
        {
            //BattleVictoryAnimationUnitModel model = new BattleVictoryAnimationUnitModel();
            //model.AddOnEndTimelineAnimations(BattleVictoryAnimationUnitModel.AnimationState.Phone_Down, () =>
            //{

            //});

            //await AdditivePrefabManager.Instance.LoadAsync(model);
            //await AdditivePrefabManager.Instance.ShowAsync();
        }

        await TaskSetResultInfo();
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await Exit(async (state) =>
        {
            if (state == UISubState.Finished)
                Model.OnEventGoHome();
        });
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Battle/BattleResultArenaPopup";
    }

    private void SetBasicInfo()
    {
        User user = PlayerManager.Instance.MyPlayer.User;
        string nickName = user.NickName;

        CharacterTable characterTable = TableManager.Instance.GetTable<CharacterTable>();
        CharacterTableData characterData = characterTable.GetDataByID(user.CharacterModel.LeaderDataCharacterId);
        string thumbnailIllust = ThumbnailGeneratorModel.GetCharacterThumbnailByData(characterData, CharacterThumbnailType.Illust);

        TeamGroup teamGroup = user.TeamModel.GetTeamGroupByType(DeckType.ArenaAttack);
        int power = teamGroup.GetCurrentTeam().Power;

        Model.SetBasicInfo(nickName, thumbnailIllust, power);
    }

    private async UniTask TaskSetResultInfo()
    {
        IDto dto = await Model.ResultModel.FuncGetArenaResultDto(Model.FinishBattleInDto);
        SetResult((ArenaBattleFinishOutDto)dto);
    }

    private void SetResult(ArenaBattleFinishOutDto dto)
    {
        ArenaModel myModel = new ArenaModel();
        //myModel.UpdateArenaModel(dto.arenaPointBattleScore, dto.rank);

        ArenaModel enemyModel = new ArenaModel();
        //enemyModel.UpdateArenaModelByHistory(dto.arenaHistory, isMyInfo: false);

        GoodsGeneratorModel goodsGenerator = new GoodsGeneratorModel(PlayerManager.Instance.MyPlayer.User);
        ThumbnailGeneratorModel thumbnailGenerator = new ThumbnailGeneratorModel();
        List<ThumbnailRewardUnitModel> rewardList = new List<ThumbnailRewardUnitModel>();
        IReadOnlyList<Goods> goodsList = goodsGenerator.GetObtainGoodsByGoodsDto(dto.goods);

        foreach (Goods goods in goodsList)
        {
            ThumbnailRewardUnitModel model = new ThumbnailRewardUnitModel();
            model.SetThumbnailUnitModel(thumbnailGenerator.GetThumbnailModelByGoods(goods));
            rewardList.Add(model);
        }

        Model.SetResultInfo(myModel, enemyModel, rewardList);
    }

    private void OnEventExit(System.Action onEventAfterAction)
    {
        if (CheckPlayingAnimation())
            return;

        Exit(async (state) =>
        {
            if (state == UISubState.Finished)
                onEventAfterAction?.Invoke();
        }).Forget();
    }

    private void OnEventStatistics()
    {
        MessageBoxManager.ShowYesBox(LocalizationDefine.LOCALIZATION_POPUP_CONSTRUCTION).Forget();
    }
    #endregion Coding rule : Function
}