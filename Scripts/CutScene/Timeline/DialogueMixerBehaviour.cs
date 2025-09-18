using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class DialogueMixerBehaviour : PlayableBehaviour
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
                    ScriptPlayable<DialogueBehaviour> inputPlayable = (ScriptPlayable<DialogueBehaviour>)playable.GetInput(i);
                    DialogueBehaviour input = inputPlayable.GetBehaviour();
                    
                    // 입력이 활성화되어 있으면 처리
                    if (inputWeight > 0.5f)
                    {
                        // 대화 UI 업데이트
                        if (input.dialogueData != null)
                        {
                            dialogueUI.SetDialogue(input.dialogueData.characterName, input.dialogueData.dialogueText);
                        }
                    }
                }
            }
        }
    }
}
