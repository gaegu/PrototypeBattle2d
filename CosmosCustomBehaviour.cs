using UnityEngine;
using UnityEngine.Playables;

public class CosmosCustomBehaviour : PlayableBehaviour
{
    public CustomActionType actionType;
    public CustomActionData actionData;

    private bool hasTriggered = false;
    private GameObject targetObject;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        hasTriggered = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        targetObject = playerData as GameObject;

        if (!hasTriggered && targetObject != null)
        {
            ExecuteAction();
            hasTriggered = true;
        }
    }

    private void ExecuteAction()
    {
        // 실제 실행은 TimelineEventHandler에서 처리
        // 여기서는 이벤트 데이터만 준비
        if (targetObject == null) return;

        var battleActor = targetObject.GetComponent<BattleActor>();
        if (battleActor == null) return;

        // TimelineEventHandler로 이벤트 전달 (다음 단계에서 구현)
        Debug.Log($"[CosmosCustom] Execute Action: {actionType} on {battleActor.name}");
    }
}