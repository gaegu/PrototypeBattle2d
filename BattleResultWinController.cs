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
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class BattleResultWinController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.BattleResultWinPopup; } }
    private BattleResultWinPopup View { get { return base.BaseView as BattleResultWinPopup; } }
    protected BattleResultWinPopupModel Model { get; private set; }
    public BattleResultWinController() { Model = GetModel<BattleResultWinPopupModel>(); }
    public BattleResultWinController(BaseModel baseModel) : base(baseModel) { }
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
        if(Model.ThumbnailRewardUnitModels.Count == 0)
            Model.SetThumbnailRewardUnitModel(Model.DungeonTableData);
        Model.SetDungeonInfo();
        Model.SetEventExit(OnEventExit);
    }

    public override async UniTask<bool> Back(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        Model.OnEventGoHome();
        return true;
        //return await Exit(onEventFinished: async () => { Model.OnEventGoHome(); });
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        return await base.Exit(onEventExtra);
    }

    public override async UniTask LoadingProcess()
    {
        TutorialManager.Instance.SetPlayableDungeonTutorial(UIType.BattleResultWinPopup, Model.DungeonTableData.GetID());
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();

        WaitTutorial().Forget();
    }

    public async UniTask WaitTutorial()
    {
        if (Config.IsTutorial)
        {
            IronJade.Debug.LogError("[BattleResultWinPopup]전투결과 튜토리얼 - 시작");
            await UniTask.WaitUntil(() => { return !View.IsPlayingAnimation; });
            await UniTask.WaitUntil(() => { return View.IsButtonEnable; });
            await UniTask.WaitUntil(() => { return UIManager.Instance.CheckOpenCurrentUI(UIType.BattleResultWinPopup); });
            IronJade.Debug.LogError("[BattleResultWinPopup]전투결과 튜토리얼 - 조건만족");

            TutorialManager.Instance.PlayNextTutorial();
        }
    }

    public override void Refresh()
    {
        RefreshInfo();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Battle/BattleResultWinPopup";
    }

    public override void InitializeTestModel(params object[] parameters)
    {
        BattleResultStageDungeonInfoModel model = new BattleResultStageDungeonInfoModel();
        model.SetStageName("2-5");
        model.SetPrevExp(1532);
        model.SetFuncGetResultExp(() => { return 2345; });
        model.SetPrevStageClearCount(9);
        model.SetFuncGetResultStageClearCount(() => { return 10; });
        Model.SetBattleResultInfoModel(model);
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

    private void RefreshInfo()
    {
        switch (Model.DungeonType)
        {
            case DungeonType.InfinityCircuit:
                {
                    View.ShowInfinityDungeonInfo().Forget();
                    break;
                }
            case DungeonType.StageDungeon:
                {
                    View.ShowStageDungeonInfo().Forget();
                    break;
                }
            default:
                break;
        }
    }

    public override async UniTask TutorialStepAsync(Enum type)
    {
        switch ((TutorialExplain)type)
        {
            case TutorialExplain.BattleResultHome:
                {
                    if (!View.IsButtonCooldown)
                    {
                        View.OnClickButton();
                        Model.OnEventGoHome();
                    }
                    break;
                }

            case TutorialExplain.BattleResultNext:
                {
                    if (!View.IsButtonCooldown)
                    {
                        View.OnClickButton();
                        Model.OnEventGoNext();
                    }
                    break;
                }
        }
    }

    public override async UniTask PlayHideAsync()
    {
        base.PlayHideAsync().Forget();

        // 임의로 맞춤.. 피디님 요청
        await UniTask.Delay(IntDefine.PHONE_DOWN_DELAY);
    }

    #region Code Damage
    // 코드 컨텐츠 결과화면 관련 데이터 세팅 함수들 모아놨습니다.
    // 차후 Code 관련으로 모델로 옮길 예정

    private int CodeGetRewardProgressLevel(int totalDamage)
    {
        CodeDamageRewardTable codeDamageRewardTable = TableManager.Instance.GetTable<CodeDamageRewardTable>();
        CodeTableData codeData = Model.BattleInfo.CodeData;

        long bossTotalHp = (long)Math.Ceiling(GetBossTotalHp());

        for (int i = 0; i < codeData.GetCODE_DAMAGE_REWARDCount(); i++)
        {
            CodeDamageRewardTableData codeRewardRangeTableData = codeDamageRewardTable.GetDataByID(codeData.GetCODE_DAMAGE_REWARD(i));

            if (codeRewardRangeTableData.IsNull())
                return 0;

            float rewardRatio = codeRewardRangeTableData.GetDAMAGE_PERCENTAGE();
            float damageRatio = (float)totalDamage / bossTotalHp;

            if (rewardRatio >= damageRatio)
                return i;
        }

        return codeData.GetCODE_DAMAGE_REWARDCount() - 1;
    }

    private decimal GetBossTotalHp()
    {
        decimal bossTotalHp = 0;

        GoodsGeneratorModel goodsGenerator = new GoodsGeneratorModel();
        MonsterTable monsterTable = TableManager.Instance.GetTable<MonsterTable>();
        MonsterGroupTable monsterGroupTable = TableManager.Instance.GetTable<MonsterGroupTable>();

        DungeonTableData dungeonData = Model.BattleInfo.DungeonData;

        for (int i = 0; i < dungeonData.GetBATTLE_MONSTER_INFOCount(); i++)
        {
            MonsterGroupTableData monsterGroup = monsterGroupTable.GetDataByID(dungeonData.GetBATTLE_MONSTER_INFO(i));

            int bossID = monsterGroup.GetBOSS_ID();
            if (bossID == 0)
                continue;

            for (int j = 0; j < monsterGroup.GetMONSTER_IDCount(); j++)
            {
                MonsterTableData characterData = monsterTable.GetDataByID(monsterGroup.GetMONSTER_ID(j));
                Character character = goodsGenerator.CreateMonsterCharacterByGoodsValue(characterData.GetID(), bossID != 0 && bossID == characterData.GetID(), false, monsterGroup);

                decimal characterHp = character.GetStat(CharacterStatType.Hp).StatValue;
                bossTotalHp += characterHp;
            }
        }

        return bossTotalHp;
    }

    private (int, int) GetRewardDataIdByProgressLevel(CodeTableData codeData, int progressLevel)
    {
        CodeDamageRewardTable codeDamageRewardTable = TableManager.Instance.GetTable<CodeDamageRewardTable>();
        int codeDamageDataId = codeData.GetCODE_DAMAGE_REWARD(progressLevel);
        CodeDamageRewardTableData codeDamageRewardData = codeDamageRewardTable.GetDataByID(codeDamageDataId);

        int rewardDataId = codeDamageRewardData.GetREWARD_VALUE();
        int bonusRewardDataId = codeDamageRewardData.GetBONUS_ITEM_REWARD();
        return (rewardDataId, bonusRewardDataId);
    }
    #endregion Code Damage
    #endregion Coding rule : Function
}