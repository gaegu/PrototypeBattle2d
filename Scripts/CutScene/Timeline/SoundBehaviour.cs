using UnityEngine;
using UnityEngine.Playables;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class SoundBehaviour : PlayableBehaviour
    {
        public SoundClipData soundData;
        private bool isFirstFrame = true;
        private AudioSource audioSource;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            isFirstFrame = true;
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
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 클립이 끝나면 사운드 정지
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
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
