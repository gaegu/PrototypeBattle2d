using AC;
using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class ImageToonBehaviour : PlayableBehaviour
    {
        public ImageToonClipData imageToonData;
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
                if (dialogueUI != null && imageToonData != null)
                {
                    // 이미지툰 표시 전에 기존 말풍선만 제거 (대사창은 유지)
                    if (dialogueUI.dialoguePanelUI != null)
                    {
                        dialogueUI.dialoguePanelUI.HideSpeechBubble();
                    }
                    
                    // 스프라이트가 직접 할당되어 있으면 우선 사용
                    if (imageToonData.imageToonSprite != null)
                    {
                        dialogueUI.imageToonUI.SetSprite(imageToonData.imageToonSprite);
                    }
                    // 없으면 imagePath 사용 (기존 방식)
                    else if (!string.IsNullOrEmpty(imageToonData.imagePath))
                    {
                        //로드하고 셋팅
                        //dialogueUI.imageToonUI.SetSprite(imageToonData.imagePath);
                    }
                    
                    dialogueUI.ShowImageToon();
                }
                isFirstFrame = false;
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (dialogueUI != null)
            {
                dialogueUI.HideImageToon();
            }
        }
    }
}
