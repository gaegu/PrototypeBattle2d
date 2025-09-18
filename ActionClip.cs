using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [System.Serializable]
    public class ActionClip : PlayableAsset, ITimelineClipAsset
    {
        public ActionClipData actionData = new ActionClipData();
        
        // 씬 오브젝트 드래그 앤 드롭을 위한 ExposedReference
        public ExposedReference<GameObject> bodyObject;   // 움직일 본체(Body)
        public ExposedReference<GameObject> targetObject; // 도달할 목표(Target)

        [Header("Move Settings")]
        public bool useCustomEase = false;
        public DG.Tweening.Ease easeType = DG.Tweening.Ease.Linear;
        public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public float moveSpeed = 1f;
        public bool useSpeedInsteadOfDuration = false;
        public bool useCustomCurve = false;  // 커스텀 커브 사용 여부
        
        [Header("Offset Settings")]
        public bool useOffset = false;       // 오프셋 사용 여부
        public float offsetDistance = 1.5f;  // 오프셋 거리

        public ClipCaps clipCaps => ClipCaps.None;

        // Move 타입일 때만 표시되는 커브 설정
        public bool UseCustomEase => useCustomEase;
        public DG.Tweening.Ease EaseType => easeType;
        public AnimationCurve CustomCurve => customCurve;
        public float MoveSpeed => moveSpeed;
        public bool UseSpeedInsteadOfDuration => useSpeedInsteadOfDuration;
        public bool UseCustomCurve => useCustomCurve;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ActionBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.actionData = actionData;
            behaviour.bodyObject = bodyObject;     // 전달
            behaviour.targetObject = targetObject; // 전달
            
            // 커브 설정 전달
            behaviour.useCustomEase = useCustomEase;
            behaviour.easeType = easeType;
            behaviour.customCurve = customCurve;
            behaviour.moveSpeed = moveSpeed;
            behaviour.useSpeedInsteadOfDuration = useSpeedInsteadOfDuration;
            behaviour.useCustomCurve = useCustomCurve;
            
            // 오프셋 설정 전달
            behaviour.useOffset = useOffset;
            behaviour.offsetDistance = offsetDistance;
            
            return playable;
        }
    }
}
