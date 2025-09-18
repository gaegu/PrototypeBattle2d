using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    public class TimelinePlayer : MonoBehaviour
    {
        [Header("타임라인 설정")]
        [SerializeField] private TimelineAsset timelineAsset;
        [SerializeField] private string timelinePath = "Assets/IronJade/ResourcesAddressable/2DRenewal/PortraitNew/TimelineAssets/";
        
        [Header("바인딩 오브젝트")]
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private Transform actionTarget;
        
        private PlayableDirector playableDirector;
        
        void Start()
        {
            // PlayableDirector 가져오기
            playableDirector = GetComponent<PlayableDirector>();
            if (playableDirector == null)
            {
                playableDirector = gameObject.AddComponent<PlayableDirector>();
            }
            
            // 기본 설정
            playableDirector.extrapolationMode = DirectorWrapMode.None;
            playableDirector.timeUpdateMode = DirectorUpdateMode.GameTime;
        }
        
        /// <summary>
        /// 타임라인 에셋으로 재생 (안전한 버전)
        /// </summary>
        public void PlayTimeline(TimelineAsset timeline)
        {
            if (timeline == null)
            {
                Debug.LogError("타임라인이 null입니다!");
                return;
            }
            
            try
            {
                // 기존 타임라인 정리
                if (playableDirector.playableAsset != null)
                {
                    playableDirector.Stop();
                    playableDirector.playableAsset = null;
                }
                
                SetupBindings(timeline);
                playableDirector.playableAsset = timeline;
                playableDirector.time = 0;
                playableDirector.Play();
                
                Debug.Log($"타임라인 재생 시작: {timeline.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"타임라인 재생 중 오류 발생: {e.Message}");
            }
        }
        
        /// <summary>
        /// 경로로 타임라인 로드 및 재생
        /// </summary>
        public void PlayTimelineByPath(string fileName)
        {
            string fullPath = timelinePath + fileName + ".playable";
            
            #if UNITY_EDITOR
            TimelineAsset timeline = UnityEditor.AssetDatabase.LoadAssetAtPath<TimelineAsset>(fullPath);
            #else
            TimelineAsset timeline = Resources.Load<TimelineAsset>(fullPath);
            #endif
            
            if (timeline != null)
            {
                PlayTimeline(timeline);
            }
            else
            {
                Debug.LogError($"타임라인을 찾을 수 없습니다: {fullPath}");
            }
        }
        
        /// <summary>
        /// 바인딩 설정
        /// </summary>
        private void SetupBindings(TimelineAsset timeline)
        {
            if (timeline == null) return;
            
            try
            {
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (track == null) continue;
                    
                    if (track is DialogueTrack || track is ImageToonTrack)
                    {
                        if (dialogueUI != null)
                        {
                            playableDirector.SetGenericBinding(track, dialogueUI);
                            Debug.Log($"{track.name} 바인딩 완료: DialogueUI");
                        }
                        else
                        {
                            Debug.LogWarning($"{track.name}: DialogueUI가 설정되지 않았습니다.");
                        }
                    }
                    else if (track is SoundTrack)
                    {
                        AudioSource audioSource = GetComponent<AudioSource>();
                        if (audioSource == null)
                        {
                            audioSource = gameObject.AddComponent<AudioSource>();
                        }
                        playableDirector.SetGenericBinding(track, audioSource);
                        Debug.Log($"{track.name} 바인딩 완료: AudioSource");
                    }
                    else if (track is ActionTrack)
                    {
                        Transform target = actionTarget != null ? actionTarget : transform;
                        playableDirector.SetGenericBinding(track, target);
                        Debug.Log($"{track.name} 바인딩 완료: {target.name}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"타임라인 바인딩 설정 중 오류 발생: {e.Message}");
            }
        }
        
        /// <summary>
        /// 재생 중지
        /// </summary>
        public void StopTimeline()
        {
            if (playableDirector != null)
            {
                playableDirector.Stop();
            }
        }
        
        /// <summary>
        /// 재생 상태 확인
        /// </summary>
        public bool IsPlaying()
        {
            return playableDirector != null && playableDirector.state == PlayState.Playing;
        }
        
        // 인스펙터에서 테스트용 버튼
        [ContextMenu("테스트 타임라인 재생")]
        private void TestPlayTimeline()
        {
            if (timelineAsset != null)
            {
                PlayTimeline(timelineAsset);
            }
        }
    }
}
