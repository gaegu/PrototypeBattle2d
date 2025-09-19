using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class SoundBehaviour : PlayableBehaviour
    {
        public SoundClipData soundData;
        private bool isFirstFrame = true;
        private AudioSource audioSource;
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
                audioSource = playerData as AudioSource;
                if (audioSource != null && soundData != null && !string.IsNullOrEmpty(soundData.soundPath))
                {
                    // Addressable로 오디오 클립 로드 및 재생
                    PlaySound(soundData.soundPath);
                }
                isFirstFrame = false;
            }
            
            // 클립이 거의 끝날 때 클릭 대기 시작
            double currentTime = playable.GetTime();
            double duration = playable.GetDuration();
            double progress = currentTime / duration;
            
            // 클립이 99% 진행되었을 때 클릭 대기 시작
            if (soundData != null && soundData.waitForClick && !isWaitingForClick && progress >= 0.99)
            {
                isWaitingForClick = true;
                pauseTime = currentTime; // 클릭 대기 시작 시간 저장
                // SoundBehaviour는 DialogueUI가 없으므로 별도 처리 필요
                // 필요시 DialogueUI를 찾아서 클릭 대기 상태 설정
                var dialogueUI = Object.FindObjectOfType<DialogueUI>();
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
                    var dialogueUI = Object.FindObjectOfType<DialogueUI>();
                    if (dialogueUI != null)
                    {
                        dialogueUI.SetWaitingForClick(false);
                    }
                });
            }
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 클립이 끝나면 사운드 정지
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            // 클릭 대기 중이 아닐 때만 UI 상태를 해제
            if (!isWaitingForClick)
            {
                var dialogueUI = Object.FindObjectOfType<DialogueUI>();
                if (dialogueUI != null)
                {
                    dialogueUI.SetWaitingForClick(false);
                }
            }
        }

        private async void PlaySound(string soundPath)
        {
            // Addressable 오디오 로드 (나중에 구현)
            Debug.Log($"사운드 재생: {soundPath}");
            // AudioClip clip = await LoadAudioClipAsync(soundPath);
            // if (clip != null && audioSource != null)
            // {
            //     audioSource.clip = clip;
            //     audioSource.Play();
            // }
        }
    }
}
