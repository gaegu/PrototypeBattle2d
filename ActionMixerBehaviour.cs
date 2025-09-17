using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class ActionMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            Transform targetTransform = playerData as Transform;
            if (targetTransform == null) return;

            int inputCount = playable.GetInputCount();
            
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight > 0)
                {
                    ScriptPlayable<ActionBehaviour> inputPlayable = (ScriptPlayable<ActionBehaviour>)playable.GetInput(i);
                    ActionBehaviour input = inputPlayable.GetBehaviour();
                    
                    // 입력이 활성화되어 있으면 처리
                    if (inputWeight > 0.5f)
                    {
                        // 액션 실행 처리
                        if (input.actionData != null)
                        {
                            // 액션 실행 로직
                        }
                    }
                }
            }
        }
    }
}
