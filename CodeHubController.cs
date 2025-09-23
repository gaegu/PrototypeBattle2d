//=========================================================================================================
#pragma warning disable CS1998
using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading;             // 쓰레드
//using UnityEngine;                  // UnityEngine 기본
//using UnityEngine.UI;               // UnityEngine의 UI 기본
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using IronJade.Observer.Core;
using IronJade.Server.Web.Core;
using IronJade.UI.Core;             // UI 관련 클래스들 모음 (UI관련 각 Base Class들)
//using IronJade.Table.Data;          // 데이터 테이블
//using IronJade.Item.Core;           // 아이템과 관련된 클래스들 모음 (Equipment, Item 등)
//=========================================================================================================

public class CodeHubController : BaseController, IObserver
{
    public override bool IsPopup { get { return false; } }
    public override UIType UIType { get { return UIType.CodeHubView; } }
    public override void SetModel() { SetModel(new CodeHubViewModel()); }
    private CodeHubView View { get { return base.BaseView as CodeHubView; } }
    private CodeHubViewModel Model { get { return GetModel<CodeHubViewModel>(); } }

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool isAfterBattle = false;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        ObserverManager.AddObserver(DungeonObserverID.DungeonExit, this);

        Model.SetApplication(true);
        Model.SetOnClickCodeHubUnit(OnClickCodeHubUnit);
    }

    public override async UniTask LoadingProcess()
    {
        await RequestStageDungeonGet();
        await RequestCodeGet();
        await LoadVierHaven();

        UpdateModel();
    }

    public override async UniTask Process()
    {
        SoundManager.BgmFmod.Play(StringDefine.PATH_FMOD_EVENT_CODE_BGM);

        TaskUpdateRemainTime().Forget();
        await View.ShowAsync();
    }

    public override UniTask<bool> Exit(System.Func<UISubState, UniTask> onEventExtra = null)
    {
        ObserverManager.RemoveObserver(DungeonObserverID.DungeonExit, this);

        return base.Exit(onEventExtra);
    }

    public override async UniTask BackProcess()
    {
        await RequestStageDungeonGet();
        await RequestCodeGet();

        UpdateModel();

        if (isAfterBattle)
        {
            isAfterBattle = false;

            await View.ShowAsync();
        }
    }

    public override async UniTask PlayHideAsync()
    {
        base.PlayHideAsync().Forget();
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Dungeon/Code/CodeHubView";
    }

    private async UniTask RequestCodeGet()
    {
        BaseProcess codeGetProcess = NetworkManager.Web.GetProcess(WebProcess.CodeGet);

        if (await codeGetProcess.OnNetworkAsyncRequest())
            codeGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestStageDungeonGet()
    {
        BaseProcess stageDungeonGetProcess = NetworkManager.Web.GetProcess(WebProcess.StageDungeonGet);

        if (await stageDungeonGetProcess.OnNetworkAsyncRequest())
            stageDungeonGetProcess.OnNetworkResponse();
    }

    private async UniTask RequestExStageGet()
    {
        BaseProcess exStageGetProcess = NetworkManager.Web.GetProcess(WebProcess.ExStageGet);

        if (await exStageGetProcess.OnNetworkAsyncRequest())
            exStageGetProcess.OnNetworkResponse();
    }

    private async UniTask LoadVierHaven()
    {
        await AdditivePrefabManager.Instance.LoadAsync(AdditiveType.Vierheaven);
    }

    private void UpdateModel()
    {
        var stageDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<StageDungeonGroupModel>();
        var codeDungeonGroupModel = DungeonManager.Instance.GetDungeonGroupModel<CodeDungeonGroupModel>();

        if (codeDungeonGroupModel == null)
            return;

        Model.CreateCodeHubUnitModel(PlayerManager.Instance.MyPlayer.User, stageDungeonGroupModel, codeDungeonGroupModel);
    }

    public async UniTask TaskUpdateRemainTime()
    {
        while (true)
        {
            CheckRemainTime();

            await UniTask.Delay(IntDefine.TIME_MILLISECONDS_ONE, cancellationToken: TokenPool.Get(GetHashCode()));
        }
    }

    private void CheckRemainTime()
    {
        if (View == null)
            return;

        SetRemainTimeText();

        View.UpdateRemainTimeText();
    }

    public void SetRemainTimeText()
    {
        DateTime nowTime = NetworkManager.Web.ServerTimeUTC;
        DateTime endTime = TimeManager.Instance.GetResetTime(CheckResetTimeType.Daily);

        TimeSpan remainTime = endTime - nowTime;

        Model.SetRemainTimeText($"{remainTime.Days}D {remainTime.Hours}H {remainTime.Minutes}M");
    }

    public void OnClickCodeHubUnit(CodeDungeonModel codeDungeonModel)
    {
        BaseController codeController = UIManager.Instance.GetController(UIType.CodeView);
        CodeViewModel codeViewModel = codeController.GetModel<CodeViewModel>();
        // LoadingProcess에서 현재 난이도의 던전모델 기준으로 갱신 
        codeViewModel.SetDifficulty(codeDungeonModel.DifficultType);

        UIManager.Instance.EnterAsync(codeController).Forget();
    }

    void IObserver.HandleMessage(System.Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case DungeonObserverID.DungeonExit:
                {
                    isAfterBattle = true;
                }
                break;
        }
    }
    #endregion Coding rule : Function
}