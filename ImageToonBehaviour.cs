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
        private bool isWaitingForClick = false;
        private bool hasReceivedClick = false;
        private PlayableDirector director; // Timeline 제어를 위한 참조
        private double pauseTime; // 클릭 대기 시작 시간

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            isFirstFrame = true;
            isWaitingForClick = false;
            hasReceivedClick = false;
            
            // PlayableDirector 참조 가져오기
            if (director == null)
            {
                director = Object.FindObjectOfType<PlayableDirector>();
            }
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
            
            // 클립이 거의 끝날 때 클릭 대기 시작
            double currentTime = playable.GetTime();
            double duration = playable.GetDuration();
            double progress = currentTime / duration;
            
            // 클립이 99% 진행되었을 때 클릭 대기 시작
            if (imageToonData != null && imageToonData.waitForClick && !isWaitingForClick && progress >= 0.99)
            {
                isWaitingForClick = true;
                pauseTime = currentTime; // 클릭 대기 시작 시간 저장
                if (dialogueUI != null)
                {
                    dialogueUI.SetWaitingForClick(true);
                }
                
                // Timeline 일시정지
                if (director != null)
                {
                    director.Pause();
                }
                
                // ClickDetector를 통해 클릭 감지 시작
                var clickDetector = Object.FindObjectOfType<ClickDetector>();
                if (clickDetector == null)
                {
                    GameObject detectorObj = new GameObject("ClickDetector");
                    clickDetector = detectorObj.AddComponent<ClickDetector>();
                }
                
                clickDetector.StartWaitingForClick(() => {
                    hasReceivedClick = true;
                    isWaitingForClick = false;
                    if (dialogueUI != null)
                    {
                        dialogueUI.SetWaitingForClick(false);
                    }
                });
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 클릭 대기 중이 아닐 때만 UI를 숨김
            if (!isWaitingForClick)
            {
                if (dialogueUI != null)
                {
                    dialogueUI.HideImageToon();
                    dialogueUI.SetWaitingForClick(false);
                }
            }
        }
    }
}
