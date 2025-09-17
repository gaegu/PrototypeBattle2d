using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class CustomMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            GameObject targetObject = playerData as GameObject;
            if (targetObject == null) return;

            int inputCount = playable.GetInputCount();
            
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight > 0)
                {
                    ScriptPlayable<CustomBehaviour> inputPlayable = (ScriptPlayable<CustomBehaviour>)playable.GetInput(i);
                    CustomBehaviour input = inputPlayable.GetBehaviour();
                    
                    // 입력이 활성화되어 있으면 처리
                    if (inputWeight > 0.5f)
                    {
                        // 커스텀 액션 실행 처리
                        if (input.customData != null)
                        {
                            // CustomBehaviour에서 직접 처리됨
                        }
                    }
                }
            }
        }
    }
}
