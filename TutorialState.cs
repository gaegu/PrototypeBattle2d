using Cysharp.Threading.Tasks;
using UnityEngine;

public class TutorialState : ITownState
{
    public string StateName => "Tutorial";

    public async UniTask Enter(TownStateContext context)
    {
        Debug.Log($"[{StateName}] Enter");

        // TutorialExplain 파라미터 가져오기
        var tutorialExplain = context.GetParameter<TutorialExplain>("TutorialExplain");

        if (tutorialExplain != null)
        {
            Debug.Log($"[{StateName}] Starting tutorial: {tutorialExplain}");
            await TownSceneManager.Instance.TutorialStepAsync(tutorialExplain);
        }
        else
        {
            Debug.LogWarning($"[{StateName}] No tutorial type specified");
        }
    }

    public async UniTask Execute(TownStateContext context)
    {
        await UniTask.Yield();
    }

    public async UniTask Exit()
    {
        Debug.Log($"[{StateName}] Exit");
    }

    public bool CanTransitionTo(FlowState nextState)
    {
        // 튜토리얼 중에는 제한적 전환
        return nextState == FlowState.None || nextState == FlowState.Home;
    }
}