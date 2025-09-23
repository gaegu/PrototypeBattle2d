#pragma warning disable CS1998
using Cysharp.Threading.Tasks;
using IronJade.Flow.Core;
using IronJade.Observer.Core;
using IronJade.UI.Core;
using UnityEngine;

public class LogoFlow : BaseFlow
{
    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    // 아래 코드는 필수 코드 입니다.
    public LogoFlowModel Model { get { return GetModel<LogoFlowModel>(); } }
    //=========================================================================================================

    #region Coding rule : Property
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
        NetworkManager.SessionChecker(false);
        UIManager.Instance.SetEventUIProcess(OnEventHome, OnEventChangeState);

        GameManager.Instance.ActiveRendererFeatures(false);
    }

    public override async UniTask<bool> Back()
    {
        bool isBack = await UIManager.Instance.BackAsync();

        return isBack;
    }

    public override async UniTask Exit()
    {
        IronJade.Debug.Log("LogoFlow : Exit");
        UIManager.Instance.DeleteAllUI();
    }

    public override async UniTask LoadingProcess(System.Func<UniTask> onEventExitPrevFlow)
    {
        // =============================================
        // ================== 씬 로딩 ==================
        bool isReturn = FlowManager.Instance.CheckPrevFlow(FlowType.IntroFlow) ||
                        FlowManager.Instance.CheckPrevFlow(FlowType.TownFlow) ||
                        FlowManager.Instance.CheckPrevFlow(FlowType.BattleFlow);

        System.Func<UniTask> waitCommonProcess = async () =>
        {
            if (!UtilModel.Resources.CheckLoadedScene(StringDefine.SCENE_LOGO))
                await UtilModel.Resources.LoadSceneAsync(StringDefine.SCENE_LOGO, UnityEngine.SceneManagement.LoadSceneMode.Additive);

            // 이전 Flow 닫기
            await onEventExitPrevFlow();
            await FlowManager.Instance.UnloadFlowScene(FlowType.IntroFlow);
            await FlowManager.Instance.UnloadFlowScene(FlowType.TownFlow);
            await FlowManager.Instance.UnloadFlowScene(FlowType.BattleFlow);

            ObserverManager.Clear();
            CameraManager.Instance.SortingCameraStack();
            UIManager.Instance.RemovePrevStackNavigator();

            // 진행중인 도입부 시퀀스 캔슬
            if (PrologueManager.Instance.IsProgressing)
                PrologueManager.Instance.ResetPrologue();

            await UIManager.Instance.LoadAsync(UIManager.Instance.GetController(UIType.LogoView));
        };

        if (isReturn)
        {
            UIManager.Instance.EnableApplicationFrame(UIType.None);
            UIManager.Instance.HideTouchScreen();
            UIManager.Instance.Home();

            await TransitionManager.ReturnToLogoIn();
            TransitionManager.ShowGlitch(true);
            await waitCommonProcess();
            await TransitionManager.ReturnToLogoOut();
        }
        else
        {
            await waitCommonProcess();
        }
    }

    public override async UniTask ChangeStateProcess(System.Enum state)
    {
        FlowState flowState = (FlowState)state;

        switch (flowState)
        {
            case FlowState.None:
                {
                    await ShowLogo();
                    break;
                }
        }
    }

    private async UniTask ShowLogo()
    {
        BaseController baseLogoController = UIManager.Instance.GetController(UIType.LogoView);
        await UIManager.Instance.EnterAsync(baseLogoController);
        await baseLogoController.PlayShowAsync();

        FlowManager.Instance.ChangeFlow(FlowType.IntroFlow, isStack: false).Forget();
    }

    private async UniTask OnEventHome() { }
    private async UniTask OnEventChangeState(UIState state, UISubState subState, UIType prevUIType, UIType uiType) { }
    #endregion Coding rule : Function
}