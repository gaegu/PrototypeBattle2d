using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class TutorialState : ServicedTownStateBase
{
    public override string StateName => "Tutorial";

    public TutorialState(IServiceContainer container = null) : base(container) { }

    protected override async UniTask OnEnter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Starting tutorial");

        var tutorialExplain = context.GetParameter<TutorialExplain>("TutorialExplain");

        if (tutorialExplain == null)
        {
            Debug.LogError($"[{StateName}] No tutorial data");
            return;
        }

        // 튜토리얼 UI 표시
        if (UIService != null)
        {
            var controller = UIService.GetController(UIType.TutorialPopup);
            if (controller != null)
            {
                await UIService.EnterAsync(controller);
            }
        }

        // 입력 제한
        TownSceneService?.TownInputSupport?.SetInput(false);
    }

    public override async UniTask Exit()
    {
        // 입력 복원
        TownSceneService?.TownInputSupport?.SetInput(true);
        await base.Exit();
    }

    public override bool CanTransitionTo(FlowState nextState)
    {
        // 튜토리얼 중에는 제한적 전환
        return nextState == FlowState.None ||
               nextState == FlowState.Home;
    }
}