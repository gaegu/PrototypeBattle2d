//=========================================================================================================
#pragma warning disable CS1998
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using System;
using System.Linq;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.Table.Data;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class SkillMaterialDungeonController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.SkillMaterialDungeonView; } }
    public override void SetModel() { SetModel(new SkillMaterialDungeonViewModel()); }
    private SkillMaterialDungeonView View { get { return base.BaseView as SkillMaterialDungeonView; } }
    private SkillMaterialDungeonViewModel Model { get { return GetModel<SkillMaterialDungeonViewModel>(); } }
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

        Model.InitializeDatas(PlayerManager.Instance.MyPlayer.User);

        Model.SetEventClose(OnEventClose);
        Model.SetEventOpenBuffPopup(OnEventOpenBuffPopup);
        Model.SetEventOpenContentsShop(OnEventOpenContentsShop);
        Model.SetEventClickDungeonModel(OnEventOpenStageView);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestSkillMaterialDungeonGet();

        Model.SetDungeonUnitModel();
        Model.SetSkillMaterialDungeonGroupModel(GetSkillMaterialDungeonGroupModel());
    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
        View.ShowAsync().Forget();
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(CommonObserverID.DailyRefreshData, this);

        return await base.Exit(onEventExtra);
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/SkillMaterialDungeon/SkillMaterialDungeonView";
    }

    private async UniTask RefreshSkillMaterialDungeon()
    {
        await RequestSkillMaterialDungeonGet();

        Model.SetDungeonUnitModel();
        Model.SetSkillMaterialDungeonGroupModel(GetSkillMaterialDungeonGroupModel());

        await View.ShowAsync();
    }

    private void SetCurrencyModel()
    {
        if (ShopManager.Instance.ContentsShopInfoModel.GetContentsShopModels().ContainsKey(ContentsShopCategory.SkillItem))
        {
            Model.UpdateCurrencyModel(ShopManager.Instance.ContentsShopInfoModel.GetShopInfoByCategory(ContentsShopCategory.SkillItem).FirstOrDefault());
        }
        else
        {
            Model.SetCurrencyIcon(new CurrencyIconUnitModel(CurrencyType.Gold));
        }
    }

    private void OnEventOpenBuffPopup()
    {
        BaseController buffInfoController = UIManager.Instance.GetController(UIType.SkillMaterialDungeonBuffInfoPopup);
        SkillMaterialDungeonBuffInfoPopupModel buffInfoPopupModel = buffInfoController.GetModel<SkillMaterialDungeonBuffInfoPopupModel>();

        SkillMaterialDungeonTable dungeonTable = TableManager.Instance.GetTable<SkillMaterialDungeonTable>();

        buffInfoPopupModel.InitializeData(dungeonTable);

        UIManager.Instance.EnterAsync(buffInfoController).Forget();
    }

    private void OnEventOpenContentsShop()
    {
        BaseController controller = UIManager.Instance.GetController(UIType.ContentsShopView);
        ContentsShopViewModel model = controller.GetModel<ContentsShopViewModel>();
        //방화벽 상점 데이터가 없다.
        model.SetCurrentShop(ContentsShopCategory.Normal);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventOpenStageView(SkillMaterialDungeonTableData dungeonData)
    {
        BaseController controller = UIManager.Instance.GetController(UIType.SkillMaterialDungeonInfoView);
        SkillMaterialDungeonInfoViewModel model = controller.GetModel<SkillMaterialDungeonInfoViewModel>();

        model.InitializeDatas(Model.GetUser(), dungeonData);

        UIManager.Instance.EnterAsync(controller).Forget();
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private async UniTask RequestSkillMaterialDungeonGet()
    {
        SkillMaterialDungeonGetProcess skillMaterialDungeonGetProcess = NetworkManager.Web.GetProcess<SkillMaterialDungeonGetProcess>();
        if (await skillMaterialDungeonGetProcess.OnNetworkAsyncRequest())
            skillMaterialDungeonGetProcess.OnNetworkResponse();
    }

    public SkillMaterialDungeonGroupModel GetSkillMaterialDungeonGroupModel()
    {
        SkillMaterialDungeonGroupModel skillMaterialDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<SkillMaterialDungeonGroupModel>();
        skillMaterialDungeonGroupModel.UpdateDungeonOpen();

        return skillMaterialDungeonGroupModel;
    }

    void IObserver.HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        if (View == null)
            return;

        switch (observerMessage)
        {
            case CommonObserverID.DailyRefreshData:
                {
                    RefreshSkillMaterialDungeon().Forget();
                    break;
                }
        }
    }

    #endregion Coding rule : Function
}