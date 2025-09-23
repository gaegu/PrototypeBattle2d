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

public class StatUpgradeController : BaseController
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.StatUpgradeView; } }
    private StatUpgradeView View { get { return base.BaseView as StatUpgradeView; } }
    protected StatUpgradeViewModel Model { get; private set; }
    public StatUpgradeController() { Model = GetModel<StatUpgradeViewModel>(); }
    public StatUpgradeController(BaseModel baseModel) : base(baseModel) { }
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
    public override async void Enter()
    {
        Model.SetUser(PlayerManager.Instance.MyPlayer.User);

        Model.SetOnClickTab(ChangeTab);
        Model.SetOnClickCharacterList(OnEventCharacterList);
        Model.SetOnClickStatInfo(OnEventTotalStat);

        Model.SetOnClickSelectLicenseSlot(OnEventSelectLicenseSlot);
        Model.SetOnClickSelectClassSlot(OnEventSelectClassSlot);
        Model.SetOnClickSelectElementSlot(OnEventSelectElementSlot);

        SetStatUpgrade();
        Model.ChangeTab(Model.CurrentStatUpgradeType);      // Init Tab
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "StatUpgrade/StatUpgradeView";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        StatUpgradeManager.Instance.AcceptStatUpgradeStat();

        UIManager.Instance.RemoveToTarget(UIType.StatUpgradeLobbyView);

        return await base.Exit(onEventExtra);
    }

    private void ChangeTab(StatUpgradeType type)
    {
        if (Model.CurrentStatUpgradeType == type)
            return;

        if (!Model.TabUnitModels.Find(x => x.StatUpgradeType == type).IsOpen)
        {

            return;
        }

        Model.ChangeTab(type);
        View.ShowAsync().Forget();
    }

    private void SetStatUpgrade()
    {
        Model.Init();

        SetLicenseInfo();
        SetClassInfo();
        SetElementInfo();
    }

    private void SetLicenseInfo()
    {
        IReadOnlyDictionary<LicenseType, StatUpgradeLicenseInfoModel> licenseInfo = StatUpgradeManager.Instance.GetStatUpgradeLicenseInfos();
        bool isOpen = ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_TYPESTATUPGRADE);
        bool isRedDot = StatUpgradeManager.Instance.CheckUpgradeableLicenseSlotExists(PlayerManager.Instance.MyPlayer.User);

        Model.SetTabState(StatUpgradeType.License, isOpen, isRedDot);
        Model.SetLicenseUnitModel(licenseInfo);
    }

    private void SetClassInfo()
    {
        IReadOnlyDictionary<ClassType, StatUpgradeClassInfoModel> classInfo = StatUpgradeManager.Instance.GetStatUpgradeClassInfos();
        bool isOpen = true;//ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_OPEN_TYPESTATUPGRADE_CLASS);
        bool isRedDot = StatUpgradeManager.Instance.CheckUpgradeableClassSlotExists(PlayerManager.Instance.MyPlayer.User);

        Model.SetTabState(StatUpgradeType.Class, isOpen, isRedDot);
        Model.SetClassUnitModel(classInfo);
    }

    private void SetElementInfo()
    {
        IReadOnlyDictionary<ElementType, StatUpgradeElementInfoModel> elementInfo = StatUpgradeManager.Instance.GetStatUpgradeElementInfos();
        bool isOpen = true;// ContentsOpenManager.Instance.CheckContentsOpen(ContentsOpenDefine.CONTENTS_OPEN_TYPESTATUPGRADE_ELEMENT);
        bool isRedDot = StatUpgradeManager.Instance.CheckUpgradeElementSlotExists(PlayerManager.Instance.MyPlayer.User);

        Model.SetTabState(StatUpgradeType.Element, isOpen, isRedDot);
        Model.SetElementUnitModel(elementInfo);
    }

    private void OnEventCharacterList()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.StatUpgradeCharacterListPopup);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventTotalStat()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.StatUpgradeStatInfoPopup);
        StatUpgradeStatInfoPopupModel model = controller.GetModel<StatUpgradeStatInfoPopupModel>();
        model.SetStatUpgradeType(Model.CurrentStatUpgradeType);
        UIManager.Instance.EnterAsync(controller).Forget();
    }

    #region 업그레이드 관련 Event & NetworkCall
    private void OnEventSelectLicenseSlot(int licenseStatUpgradeDataId)
    {
        LicenseStatUpgradeTable statUpgradeTable = TableManager.Instance.GetTable<LicenseStatUpgradeTable>();
        LicenseStatUpgradeTableData statUpgradeData = statUpgradeTable.GetDataByID(licenseStatUpgradeDataId);
        int limitedLevel = StatUpgradeManager.Instance.GetLicenseUpgradeMaxLevel(statUpgradeData);

        StatUpgradeLicenseInfoModel infoModel = StatUpgradeManager.Instance.GetLicenseInfoByDataId(licenseStatUpgradeDataId);
        if (!infoModel.CheckPrevUpgradeComplete(statUpgradeData))
            limitedLevel = 0;

        BaseController controller = UIManager.Instance.GetController(UIType.StatUpgradeLevelUpPopup);
        StatUpgradeLevelUpPopupModel model = controller.GetModel<StatUpgradeLevelUpPopupModel>();
        model.SetLicenseUpgrade(licenseStatUpgradeDataId, infoModel.UpgradeDatas[statUpgradeData], limitedLevel);
        model.SetEventUpgrade((x, y) => { RequestLicenseUpgrade(x, y).Forget(); });

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private async UniTask RequestLicenseUpgrade(int dataId, int upgradeCount)
    {
        BaseProcess licenseLevelupProcess = NetworkManager.Web.GetProcess(WebProcess.StatUpgradeLicenseLevelup);
        licenseLevelupProcess.SetPacket(new LevelUpLicenseStatUpgradeInDto(dataId, upgradeCount));

        if (await licenseLevelupProcess.OnNetworkAsyncRequest())
        {
            licenseLevelupProcess.OnNetworkResponse();
            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetLicenseUnitModel(StatUpgradeManager.Instance.GetStatUpgradeLicenseInfos());
            View.ShowAsync().Forget();
        }
    }

    private void OnEventSelectClassSlot(ClassType classType)
    {
        int limitedLevel = StatUpgradeManager.Instance.GetClassUpgradeableMaxLevel(classType);
        if (classType != ClassType.None)
            limitedLevel = Math.Min(limitedLevel, Model.ClassUnitModel.PublicLevel);
        else
            limitedLevel = Math.Min(limitedLevel, StatUpgradeManager.Instance.GetPublicClassMaxLevel());

        BaseController controller = UIManager.Instance.GetController(UIType.StatUpgradeLevelUpPopup);
        StatUpgradeLevelUpPopupModel model = controller.GetModel<StatUpgradeLevelUpPopupModel>();
        model.SetClassUpgrade(Model.ClassUnitModel.ClassStatUpgradeInfos[classType], limitedLevel);
        model.SetEventUpgrade((x, y) => { RequestClassUpgrade(x, y).Forget(); });

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private async UniTask RequestClassUpgrade(int type, int upgradeCount)
    {
        BaseProcess classLevelupProcess = NetworkManager.Web.GetProcess(WebProcess.StatUpgradeClassLevelup);
        classLevelupProcess.SetPacket(new LevelUpClassStatUpgradeInDto((ClassType)type, upgradeCount));

        if (await classLevelupProcess.OnNetworkAsyncRequest())
        {
            classLevelupProcess.OnNetworkResponse();
            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetClassUnitModel(StatUpgradeManager.Instance.GetStatUpgradeClassInfos());
            View.ShowAsync().Forget();
        }
    }

    private void OnEventSelectElementSlot(ElementType elementType)
    {
        int limitedLevel = StatUpgradeManager.Instance.GetElementUpgradeMaxLevel(elementType);

        BaseController controller = UIManager.Instance.GetController(UIType.StatUpgradeLevelUpPopup);
        StatUpgradeLevelUpPopupModel model = controller.GetModel<StatUpgradeLevelUpPopupModel>();
        model.SetElementUpgrade(Model.ElementUnitModel.ElementStatUpgradeInfos[elementType], limitedLevel);
        model.SetEventUpgrade((x, y) => { RequestElementUpgrade(x, y).Forget(); });

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private async UniTask RequestElementUpgrade(int dataId, int upgradeCount)
    {
        BaseProcess elementLevelUpProcess = NetworkManager.Web.GetProcess(WebProcess.StatUpgradeElementLevelup);
        elementLevelUpProcess.SetPacket(new LevelUpElementStatUpgradeInDto(dataId, upgradeCount));

        if (await elementLevelUpProcess.OnNetworkAsyncRequest())
        {
            elementLevelUpProcess.OnNetworkResponse();
            Model.SetUser(PlayerManager.Instance.MyPlayer.User);
            Model.SetElementUnitModel(StatUpgradeManager.Instance.GetStatUpgradeElementInfos());
            View.ShowAsync().Forget();
        }
    }
    #endregion 업그레이드 관련 Event & NetworkCall

    #endregion Coding rule : Function
}