#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BattleCharacterSystem.Timeline;
using BattleCharacterSystem;

namespace Cosmos.Timeline.Playback.Editor
{
    /// <summary>
    /// BattlePreviewWindow와 CosmosPlaybackSystem을 연결하는 어댑터
    /// </summary>
    public class PreviewPlaybackAdapter : MonoBehaviour
    {
        #region Fields

        // Playback System
        private CosmosPlaybackSystem playbackSystem;
        private PreviewEventHandler previewEventHandler;
        private PreviewResourceProvider previewResourceProvider;

        // Preview 참조
        private GameObject previewTarget;
        private Animator previewAnimator;
        private BattleCharacterDataSO currentCharacter;

        // UI 상태
        private TimelineDataSO currentTimeline;
        private bool isPlaying = false;
        private float playbackTime = 0f;

        // 설정
        [SerializeField] public bool autoLoadConfig = true;
        [SerializeField] private bool showDebugInfo = false;

        #endregion

        #region Initialization

        public void Initialize(GameObject target, BattleCharacterDataSO character = null)
        {
            previewTarget = target;
            currentCharacter = character;

            SetupPlaybackSystem();
            LoadTimelineConfig();

            if (target != null)
            {
                previewAnimator = target.GetComponent<Animator>() ??
                                 target.GetComponentInChildren<Animator>();
            }
        }

        private void SetupPlaybackSystem()
        {
            // PlaybackSystem 컴포넌트 추가
            if (playbackSystem == null)
            {
                playbackSystem = gameObject.AddComponent<CosmosPlaybackSystem>();
            }
            playbackSystem.InitializeSystem();

            // Preview용 EventHandler
            if (previewEventHandler == null)
            {
                previewEventHandler = gameObject.AddComponent<PreviewEventHandler>();
                previewEventHandler.Initialize();
                previewEventHandler.SetTarget(previewTarget);

                // ResourceProvider 설정 추가
                if (previewResourceProvider == null)
                {
                    previewResourceProvider = gameObject.AddComponent<PreviewResourceProvider>();
                }
                previewEventHandler.SetResourceProvider(previewResourceProvider);
            }
            else
            {
                previewEventHandler.Initialize();
            }

            // Preview용 ResourceProvider
            if (previewResourceProvider == null)
            {
                previewResourceProvider = gameObject.AddComponent<PreviewResourceProvider>();
            }

            // 시스템 연결
            playbackSystem.SetEventHandler(previewEventHandler);
            playbackSystem.SetResourceProvider(previewResourceProvider);

            // 이벤트 연결
            playbackSystem.OnStateChanged += OnPlaybackStateChanged;
            playbackSystem.OnEventTriggered += OnEventTriggered;
            playbackSystem.OnPlaybackFinished += OnPlaybackFinished;
            playbackSystem.OnLoopCompleted += OnLoopCompleted;

            // Context 설정
            var context = new EventContext
            {
                TargetObject = previewTarget,
                TargetTransform = previewTarget?.transform,
                TargetAnimator = previewAnimator
            };
            playbackSystem.SetEventContext(context);
        }

        private void LoadTimelineConfig()
        {
            /* if (!autoLoadConfig || currentCharacter == null) return;

             // 새로운 경로로 수정
             string configPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{currentCharacter.CharacterName}/Animations/{currentCharacter.CharacterName}_TimelineConfig.asset";

             // TimelineConfig가 없어도 개별 TimelineDataSO 파일들을 직접 로드 가능
             if (!System.IO.File.Exists(configPath))
             {
                 // 개별 Timeline 파일들 직접 검색
                 LoadIndividualTimelines(currentCharacter.CharacterName);
             }
             else
             {
                 timelineConfig = AssetDatabase.LoadAssetAtPath<CharacterTimelineConfig>(configPath);
             }*/

            if (currentCharacter != null)
            {
                timelineSettings = currentCharacter.TimelineSettings;
            }

        }

        private void LoadIndividualTimelines(string characterName)
        {
            string animationsPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{characterName}/Animations";

            // Attack1 Timeline 찾기
            string attack1Path = $"{animationsPath}/{characterName}_Attack1_Timeline_Data.asset";
            if (System.IO.File.Exists(attack1Path))
            {
                var attack1Timeline = AssetDatabase.LoadAssetAtPath<TimelineDataSO>(attack1Path);
                // 직접 사용 가능
            }

            // ActiveSkill1 Timeline 찾기
         /*   ActiveSkill1Path = $"{animationsPath}/{characterName}_ActiveSkill1_Timeline_Data.asset";
            if (System.IO.File.Exists(ActiveSkill1Path))
            {
                var attack1Timeline = AssetDatabase.LoadAssetAtPath<TimelineDataSO>(ActiveSkill1Path);
                // 직접 사용 가능
            }*/


        }


        #endregion

        #region Playback Control

        public void PlayTimeline(TimelineDataSO timeline)
        {
            if (timeline == null)
            {
                Debug.LogWarning("[PreviewAdapter] Timeline is null");
                return;
            }

            currentTimeline = timeline;
            playbackSystem?.Play(timeline);
            isPlaying = true;
        }

        public void PlayTimelineByName(string timelineName)
        {
            TimelineDataSO timeline = GetTimelineByState(timelineName);
            if (timeline != null)
            {
                PlayTimeline(timeline);
            }
            else
            {
                Debug.LogWarning($"[PreviewAdapter] Timeline not found: {timelineName}");
            }
        }

        public TimelineDataSO GetTimelineByState(string stateName)
        {
            return timelineSettings?.GetTimelineByState(stateName);
        }


        public void Pause()
        {
            playbackSystem?.Pause();
            isPlaying = false;
        }

        public void Resume()
        {
            playbackSystem?.Play();
            isPlaying = true;
        }

        public void Stop()
        {
            playbackSystem?.Stop();
            isPlaying = false;
            playbackTime = 0f;
        }

        public void Reset()
        {
               
            playbackSystem?.Reset();
            playbackTime = 0f;
        }

        public void Seek(float time)
        {
            playbackSystem?.Seek(time);
            playbackTime = time;
        }

        public void SetPlaybackSpeed(float speed)
        {
            playbackSystem.PlaybackSpeed = speed;
        }

        public void SetLoopMode(PlaybackMode mode)
        {
            playbackSystem.Mode = mode;
        }

        #endregion

        #region Event Callbacks

        private void OnPlaybackStateChanged(PlaybackState state)
        {
            if (showDebugInfo)
                Debug.Log($"[PreviewAdapter] State changed: {state}");

            switch (state)
            {
                case PlaybackState.Playing:
                    isPlaying = true;
                    break;
                case PlaybackState.Paused:
                case PlaybackState.Stopped:
                case PlaybackState.Finished:
                    isPlaying = false;
                    break;
            }
        }

        private void OnEventTriggered(TimelineDataSO.ITimelineEvent evt)
        {
            if (showDebugInfo)
            {
                string eventInfo = GetEventDescription(evt);
                Debug.Log($"[PreviewAdapter] Event: [{evt.TriggerTime:F2}s] {eventInfo}");
            }
        }

        private void OnPlaybackFinished()
        {
            if (showDebugInfo)
                Debug.Log("[PreviewAdapter] Playback finished");
        }

        private void OnLoopCompleted()
        {
            if (showDebugInfo)
                Debug.Log("[PreviewAdapter] Loop completed");
        }

        #endregion

        #region Public Properties

        public bool IsPlaying => isPlaying;
        public float CurrentTime => playbackSystem?.CurrentTime ?? 0f;
        public float Duration => currentTimeline?.duration ?? 0f;
        public float NormalizedTime => playbackSystem?.NormalizedTime ?? 0f;
        public TimelineDataSO CurrentTimeline => currentTimeline;


        private BattleCharacterDataSO.TimelineSetting timelineSettings;

        #endregion

        #region UI Support

        /// <summary>
        /// Preview Window에서 표시할 Timeline 정보
        /// </summary>
        public TimelineInfo GetTimelineInfo()
        {
            return new TimelineInfo
            {
                Name = currentTimeline?.timelineName ?? "None",
                Duration = Duration,
                CurrentTime = CurrentTime,
                EventCount = currentTimeline?.GetAllEventsSorted().Count ?? 0,
                IsPlaying = isPlaying,
                PlaybackSpeed = playbackSystem?.PlaybackSpeed ?? 1f,
                LoopMode = playbackSystem?.Mode ?? PlaybackMode.Once
            };
        }

        [Serializable]
        public class TimelineInfo
        {
            public string Name;
            public float Duration;
            public float CurrentTime;
            public int EventCount;
            public bool IsPlaying;
            public float PlaybackSpeed;
            public PlaybackMode LoopMode;
        }

        /// <summary>
        /// Timeline의 이벤트 리스트 가져오기
        /// </summary>
        public List<EventInfo> GetEventList()
        {
            var eventInfos = new List<EventInfo>();

            if (currentTimeline == null) return eventInfos;

            foreach (var evt in currentTimeline.GetAllEventsSorted())
            {
                eventInfos.Add(new EventInfo
                {
                    Time = evt.TriggerTime,
                    Type = GetEventType(evt),
                    Description = GetEventDescription(evt),
                    HasExecuted = evt.TriggerTime <= CurrentTime
                });
            }

            return eventInfos;
        }

        [Serializable]
        public class EventInfo
        {
            public float Time;
            public string Type;
            public string Description;
            public bool HasExecuted;
        }

        private string GetEventType(TimelineDataSO.ITimelineEvent evt)
        {
            return evt switch
            {
                TimelineDataSO.AnimationEvent _ => "Animation",
                TimelineDataSO.EffectEvent _ => "Effect",
                TimelineDataSO.SoundEvent _ => "Sound",
                TimelineDataSO.CameraEvent _ => "Camera",
                TimelineDataSO.CustomEvent _ => "Custom",
                _ => "Unknown"
            };
        }

        private string GetEventDescription(TimelineDataSO.ITimelineEvent evt)
        {
            return evt switch
            {
                TimelineDataSO.AnimationEvent anim => anim.animationStateName,
                TimelineDataSO.EffectEvent effect => System.IO.Path.GetFileNameWithoutExtension(effect.effectAddressableKey),
                TimelineDataSO.SoundEvent sound => System.IO.Path.GetFileNameWithoutExtension(sound.soundEventPath),
                TimelineDataSO.CameraEvent camera => camera.actionType.ToString(),
                TimelineDataSO.CustomEvent custom => custom.eventName,
                _ => "Unknown"
            };
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (playbackSystem != null)
            {
                playbackSystem.OnStateChanged -= OnPlaybackStateChanged;
                playbackSystem.OnEventTriggered -= OnEventTriggered;
                playbackSystem.OnPlaybackFinished -= OnPlaybackFinished;
                playbackSystem.OnLoopCompleted -= OnLoopCompleted;

                playbackSystem.Stop();
            }
        }

        #endregion
    }

    /// <summary>
    /// Preview 전용 EventHandler
    /// </summary>
    public class PreviewEventHandler : TimelineEventHandler
    {
        // Editor에서 실시간 미리보기를 위한 추가 기능

        public new void SetTarget(GameObject target)
        {
            base.SetTarget(target);
        }
    }

    /// <summary>
    /// Preview 전용 ResourceProvider
    /// </summary>
    public class PreviewResourceProvider : AddressableResourceProvider
    {
        // Editor 전용 동기 로드 지원
        public new void LoadResourceAsync<T>(string key, Action<T> onLoaded) where T : UnityEngine.Object
        {
            // Editor에서는 동기 로드 시도
            T resource = LoadResourceSync<T>(key);

            if (resource != null)
            {
                onLoaded?.Invoke(resource);
            }
            else
            {
                // 실패 시 비동기 로드
                base.LoadResourceAsync(key, onLoaded);
            }
        }
    }
}
#endif