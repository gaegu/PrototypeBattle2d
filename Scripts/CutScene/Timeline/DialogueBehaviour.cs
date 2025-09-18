using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class DialogueBehaviour : PlayableBehaviour
    {
        public DialogueClipData dialogueData;
        private bool isFirstFrame = true;
        private DialogueUI dialogueUI; // playerData를 저장할 필드

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            isFirstFrame = true;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (isFirstFrame)
            {
                dialogueUI = playerData as DialogueUI; // playerData를 필드에 저장
                if (dialogueUI != null && dialogueData != null)
                {
                    // 대사창은 항상 표시
                    dialogueUI.SetDialogue(dialogueData.characterName, dialogueData.dialogueText);
                    dialogueUI.ShowDialogue();
                }
                isFirstFrame = false;
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (dialogueUI != null)
            {
                dialogueUI.HideDialogue();
                // 말풍선도 함께 제거
                RemoveSpeechBubble();
            }
        }
        
        private void RemoveSpeechBubble()
        {
            // DialoguePanelUI에서 말풍선 제거
            if (dialogueUI != null && dialogueUI.dialoguePanelUI != null)
            {
                // 말풍선 제거 로직은 DialoguePanelUI에서 처리
                // 여기서는 HideDialogue만 호출하면 됨
            }
        }
    }
}
