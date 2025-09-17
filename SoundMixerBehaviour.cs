using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class SoundMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            AudioSource audioSource = playerData as AudioSource;
            if (audioSource == null) return;

            int inputCount = playable.GetInputCount();
            
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight > 0)
                {
                    ScriptPlayable<SoundBehaviour> inputPlayable = (ScriptPlayable<SoundBehaviour>)playable.GetInput(i);
                    SoundBehaviour input = inputPlayable.GetBehaviour();
                    
                    // 입력이 활성화되어 있으면 처리
                    if (inputWeight > 0.5f)
                    {
                        // 사운드 재생 처리
                        if (input.soundData != null)
                        {
                            // 사운드 재생 로직
                        }
                    }
                }
            }
        }
    }
}
