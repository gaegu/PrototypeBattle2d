//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class StatUpgradeLobbyController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.StatUpgradeLobbyView; } }
    public override void SetModel() { SetModel(new StatUpgradeLobbyViewModel()); }
    private StatUpgradeLobbyView View { get { return base.BaseView as StatUpgradeLobbyView; } }
    private StatUpgradeLobbyViewModel Model { get { return GetModel<StatUpgradeLobbyViewModel>(); } }
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
        Model.SetEvetnEnterContents(OnEventEnterContents);
        SetContentsModel();
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
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "StatUpgrade/StatUpgradeLobbyView";
    }

    private void SetContentsModel()
    {
        StatUpgradeBalanceTable statUpgradeBalanceTable = TableManager.Instance.GetTable<StatUpgradeBalanceTable>();
        StatUpgradeBalanceTableData statUpgradeOpenData = statUpgradeBalanceTable.GetDataByID((int)StatUpgradeBalanceDefine.BALANCE_STAT_UPGRADE_OPEN_CONDITION);

        Model.Clear();

        for (int i = 0; i < statUpgradeOpenData.GetINDEXCount(); ++i)
        {
            StatUpgradeType type = (StatUpgradeType)i;
            int contentsOpenConditionDataId = (int)statUpgradeOpenData.GetINDEX(i);
            bool isOpen = ContentsOpenManager.Instance.CheckStageDungeonCondition(contentsOpenConditionDataId);
            bool isRedDot = StatUpgradeManager.Instance.CheckUpgradeableSlotExists(type, PlayerManager.Instance.MyPlayer.User);
            (int, int) stageDungeonNumber = ContentsOpenManager.Instance.GetChapterStageNumber(contentsOpenConditionDataId);

            Model.AddContentsUnitModel(type, isOpen, stageDungeonNumber.Item1, stageDungeonNumber.Item2, isRedDot);
        }
    }

    private void OnEventEnterContents(StatUpgradeType type)
    {
        if (View.IsPlayingAnimation)
            return;

        if (!Model.CheckOpen(type))
        {
            MessageBoxManager.ShowToastMessage(LocalizationDefine.LOCALIZATION_UI_LABEL_TYPESTATUPGRADE_NOTICE_UNOPENED_CONTENTS);
            return;
        }

        BaseController controller = UIManager.Instance.GetController(UIType.StatUpgradeView);
        StatUpgradeViewModel model = controller.GetModel<StatUpgradeViewModel>();
        model.SetCurrentStatUpgradeType(type);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    public override async UniTask TutorialStepAsync(System.Enum type)
    {
        TutorialExplain stepType = (TutorialExplain)type;

        switch (stepType)
        {
            case TutorialExplain.StatUpgradeLobbyLicense:
                {
                    OnEventEnterContents(StatUpgradeType.License);
                    await TutorialManager.WaitUntilEnterUI(UIType.StatUpgradeView);
                    break;

                }
        }
    }
    #endregion Coding rule : Function
}