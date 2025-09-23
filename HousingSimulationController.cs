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
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
using UnityEngine.SceneManagement;
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class HousingSimulationController : BaseController, IObserver
{
    // 아래 코드는 필수 코드 입니다.
    // Model은 Controller에서 Set을 해주세요
    // Model은 View (or Popup)와 바인딩이 되어 있으므로 별도로 덮어주지 않아도 됩니다.
    // View에 필요한 파라미터는 Model을 이용하여 사용해주세요
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.HousingSimulationView; } }
    public override void SetModel() { SetModel(new HousingSimulationViewModel()); }
    private HousingSimulationView View { get { return base.BaseView as HousingSimulationView; } }
    private HousingSimulationViewModel Model { get { return GetModel<HousingSimulationViewModel>(); } }
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
        ObserverManager.AddObserver(HousingObserverID.EventBind, this);
        ObserverManager.AddObserver(HousingObserverID.ProductUpdate, this);

        Model.SetEventClose(OnEventClose);
        Model.SetEventShowMenu(OnEventShowMenu);
    }

    public override async UniTask LoadingProcess()
    {
        await ShowHousingScene();
        await HousingSimulationManager.Instance.ShowTower();
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Housing/HousingSimulationView";
    }

    public override async UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(HousingObserverID.EventBind, this);
        ObserverManager.RemoveObserver(HousingObserverID.ProductUpdate, this);

        ObserverManager.NotifyObserver(HousingObserverID.BeforeExitHousing, null);

        return await base.Exit(onEventExtra: async (state) =>
        {
            if (state == UISubState.Finished)
                ObserverManager.NotifyObserver(HousingObserverID.ExitHousing, null);

            if (onEventExtra != null)
                await onEventExtra(state);
        });
    }

    private void OnEventClose()
    {
        Exit().Forget();
    }

    private void OnEventShowMenu()
    {
        UIManager.Instance.EnterAsync(UIType.HousingMenuSelectPopup).Forget();
    }

    private async UniTask ShowHousingScene()
    {
        if (SceneManager.GetSceneByName(StringDefine.PATH_SCENE_HOUSINGSIMULATION).isLoaded)
            return;

        await UtilModel.Resources.LoadSceneAsync(StringDefine.PATH_SCENE_HOUSINGSIMULATION, LoadSceneMode.Additive);
    }

    public void HandleMessage(Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case HousingObserverID.EventBind:
                {
                    if (Model != null)
                    {
                        HousingEventParam param = (HousingEventParam)observerParam;

                        Model.SetEventStartElevatorMove(param.OnEventStartElevatorMove);
                        Model.SetEventStopElevator(param.OnEventStopElevator);
                    }

                    break;
                }

            case HousingObserverID.ProductUpdate:
                {
                    if (Model != null)
                    {
                        var items = PlayerManager.Instance.MyPlayer.User.ItemModel.FindAll(item => item.UseMaterialType == UseItemType.Housing);

                        Model.SetProducts(items);

                        if (observerParam is HousingProductUpdateParam updateParam)
                        {
                            if (updateParam.IsUpdateMaxValues)
                                Model.SetProductMaxValues(updateParam.ProductMaxValues);
                        }

                        if (View)
                            View.ShowProducts();
                    }

                    break;
                }
        }
    }
    #endregion Coding rule : Function
}