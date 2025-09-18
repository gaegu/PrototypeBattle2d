using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class ImageToonMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            DialogueUI dialogueUI = playerData as DialogueUI;
            if (dialogueUI == null) return;

            int inputCount = playable.GetInputCount();
            
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight > 0)
                {
                    ScriptPlayable<ImageToonBehaviour> inputPlayable = (ScriptPlayable<ImageToonBehaviour>)playable.GetInput(i);
                    ImageToonBehaviour input = inputPlayable.GetBehaviour();
                    
                    // 입력이 활성화되어 있으면 처리
                    if (inputWeight > 0.5f)
                    {
                        // 이미지툰 표시 처리
                        if (input.imageToonData != null)
                        {
                            // 이미지툰 표시 로직
                        }
                    }
                }
            }
        }
    }
}
