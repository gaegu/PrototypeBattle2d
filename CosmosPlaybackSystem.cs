using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BattleCharacterSystem.Timeline;

namespace Cosmos.Timeline.Playback
{
    /// <summary>
    /// Timeline 재생을 담당하는 핵심 시스템
    /// </summary>
    public class CosmosPlaybackSystem : MonoBehaviour, ITimelinePlayer
    {
        #region Fields & Properties

        // 현재 재생 중인 Timeline
        [SerializeField] private TimelineDataSO currentTimeline;

        // 재생 상태
        private PlaybackState state = PlaybackState.Stopped;
        private PlaybackMode mode = PlaybackMode.Once;
        private float playbackSpeed = 1f;
        private float currentTime = 0f;
        private bool isReversed = false; // PingPong용

        // 이벤트 관리
        private List<TimelineDataSO.ITimelineEvent> allEvents = new List<TimelineDataSO.ITimelineEvent>();
        private List<TimelineDataSO.ITimelineEvent> pendingEvents = new List<TimelineDataSO.ITimelineEvent>();
        private HashSet<TimelineDataSO.ITimelineEvent> executedEvents = new HashSet<TimelineDataSO.ITimelineEvent>();

        // 컴포넌트
        private ITimelineEventHandler eventHandler;
        private IResourceProvider resourceProvider;

        private EventContext eventContext;

        // ===== Battle Actor 참조 추가 =====
        private BattleActor battleActor = null;

        // 설정
        [SerializeField] private PlaybackSettings settings = new PlaybackSettings();
        [SerializeField] private EventFilter eventFilter = new EventFilter();


        // 새 메서드 추가
        private HashSet<TimelineDataSO.TrackAnimation> playingTracks = new HashSet<TimelineDataSO.TrackAnimation>();




        // 프로퍼티
        public PlaybackState State => state;
        public PlaybackMode Mode
        {
            get => mode;
            set => mode = value;
        }
        public float PlaybackSpeed
        {
            get => playbackSpeed;
            set => playbackSpeed = Mathf.Clamp(value, 0f, 10f);
        }
        public float CurrentTime => currentTime;
        public float Duration => currentTimeline?.duration ?? 0f;
        public float NormalizedTime => Duration > 0 ? currentTime / Duration : 0f;

        // 이벤트
        public event Action<PlaybackState> OnStateChanged;
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackFinished;
        public event Action OnLoopCompleted;
        public event Action<TimelineDataSO.ITimelineEvent> OnEventTriggered;

        #endregion

        #region Unity Lifecycle


        private void OnDestroy()
        {
            Stop();
            CleanupResources();
        }

        private void Update()
        {
            if (state == PlaybackState.Playing)
            {
                UpdatePlayback(Time.deltaTime);
            }
        }

        #endregion

        #region Initialization

        public void InitializeSystem( BattleActor actor = null )
        {
            // BattleActor 설정
            battleActor = actor;

            // Event Handler 설정
            eventHandler = GetComponent<ITimelineEventHandler>() ?? gameObject.AddComponent<TimelineEventHandler>();

            // Resource Provider 설정
            resourceProvider = GetComponent<IResourceProvider>() ?? gameObject.AddComponent<AddressableResourceProvider>();

            // Event Context 초기화
            eventContext = new EventContext
            {
                TargetObject = gameObject,
                TargetTransform = transform,
                TargetAnimator = GetComponent<Animator>()
            };
        }

        /// <summary>
        /// 외부에서 컴포넌트 주입
        /// </summary>
        public void SetEventHandler(ITimelineEventHandler handler)
        {
            eventHandler = handler ?? eventHandler;
        }

        public void SetResourceProvider(IResourceProvider provider)
        {
            resourceProvider = provider ?? resourceProvider;
        }

        public void SetEventContext(EventContext context)
        {
            eventContext = context ?? eventContext;
        }

        #endregion

        #region Playback Control

        public void Play()
        {
            if (currentTimeline == null)
            {
                Debug.LogWarning("[CosmosPlayback] No timeline loaded");
                return;
            }

            if (state == PlaybackState.Paused)
            {
                Resume();
            }
            else
            {
                StartPlayback();
            }
        }

        public void Play(TimelineDataSO timeline)
        {
            if (timeline == null)
            {
                Debug.LogError("[CosmosPlayback] Timeline is null");
                return;
            }

            // 이전 재생 정지
            if (state == PlaybackState.Playing)
            {
                Stop();
            }

            // 새 Timeline 로드
            LoadTimeline(timeline);

            // 재생 시작
            StartPlayback();
        }

        public void Pause()
        {
            if (state != PlaybackState.Playing) return;

            SetState(PlaybackState.Paused);

            if (settings.logEvents)
                Debug.Log($"[CosmosPlayback] Paused at {currentTime:F2}s");
        }

        public void Stop()
        {
            if (state == PlaybackState.Stopped) return;

            SetState(PlaybackState.Stopped);
            currentTime = 0f;
            isReversed = false;

            // 이벤트 정리
            pendingEvents.Clear();
            executedEvents.Clear();

            // 리소스 정리
            CleanupActiveEffects();

            if (settings.logEvents)
                Debug.Log("[CosmosPlayback] Stopped");
        }

        public void Reset()
        {
            Stop();

            if (currentTimeline != null)
            {
                LoadTimeline(currentTimeline);
            }
        }

        private void Resume()
        {
            if (state != PlaybackState.Paused) return;

            SetState(PlaybackState.Playing);

            if (settings.logEvents)
                Debug.Log($"[CosmosPlayback] Resumed at {currentTime:F2}s");
        }

        #endregion

        #region Timeline Loading

        private void LoadTimeline(TimelineDataSO timeline)
        {
            currentTimeline = timeline;
            currentTime = 0f;

            // 이벤트 로드 및 정렬
            allEvents.Clear();
            allEvents = timeline.GetAllEventsSorted();

            // 대기 이벤트 설정
            pendingEvents.Clear();
            pendingEvents.AddRange(allEvents);

            // 실행된 이벤트 초기화
            executedEvents.Clear();


            // TrackAnimation의 AnimationClip 로드 추가
            PreloadTrackAnimations(timeline);

            // 리소스 프리로드
            if (settings.autoPreloadResources)
            {
                PreloadTimelineResources();
            }

            if (settings.logEvents)
                Debug.Log($"[CosmosPlayback] Loaded timeline: {timeline.timelineName} ({allEvents.Count} events)");
        }

        // 새 메서드 추가
        private void PreloadTrackAnimations(TimelineDataSO timeline)
        {
            if (timeline.trackAnimations == null || resourceProvider == null) return;

            foreach (var trackAnim in timeline.trackAnimations)
            {
                if (trackAnim == null) continue;

                // animationClip이 없고 addressableKey가 있는 경우
                if (trackAnim.animationClip == null && !string.IsNullOrEmpty(trackAnim.animationClipAddressableKey))
                {
                    resourceProvider.LoadResourceAsync<AnimationClip>(
                        trackAnim.animationClipAddressableKey,
                        clip => {
                            if (clip != null)
                            {
                                trackAnim.animationClip = clip;
                                if (settings.logEvents)
                                    Debug.Log($"[CosmosPlayback] Loaded track animation: {trackAnim.trackName}");
                            }
                        }
                    );
                }
            }
        }


        private void PreloadTimelineResources()
        {
            if (resourceProvider == null) return;

            var resourceKeys = new HashSet<string>();

            foreach (var evt in allEvents)
            {
                switch (evt)
                {
                    case TimelineDataSO.AnimationEvent anim:
                        if (!string.IsNullOrEmpty(anim.animationClipAddressableKey))
                            resourceKeys.Add(anim.animationClipAddressableKey);
                        break;

                    case TimelineDataSO.EffectEvent effect:
                        if (!string.IsNullOrEmpty(effect.effectAddressableKey))
                            resourceKeys.Add(effect.effectAddressableKey);
                        break;

                    case TimelineDataSO.SoundEvent sound:
                        if (!string.IsNullOrEmpty(sound.soundEventPath))
                            resourceKeys.Add(sound.soundEventPath);
                        break;
                }
            }

            if (resourceKeys.Count > 0)
            {
                resourceProvider.PreloadResources(resourceKeys);

                if (settings.logEvents)
                    Debug.Log($"[CosmosPlayback] Preloading {resourceKeys.Count} resources");
            }
        }

        #endregion

        #region Playback Update

        private void StartPlayback()
        {
            SetState(PlaybackState.Playing);
            currentTime = 0f;
            isReversed = false;

            // 이벤트 리셋
            pendingEvents.Clear();
            pendingEvents.AddRange(allEvents);
            executedEvents.Clear();

            OnPlaybackStarted?.Invoke();


            // Track 애니메이션 시작
            if (currentTimeline.trackAnimations.Count > 0)
            {
                foreach (var trackAnim in currentTimeline.trackAnimations)
                {
                    if (trackAnim != null && trackAnim.startTime == 0)
                    {
                        // AnimationClip이 로드되었는지 확인
                        if (trackAnim.animationClip != null)
                        {
                            (eventHandler as TimelineEventHandler)?.HandleTrackAnimation(trackAnim);
                        }
                        else if (settings.logEvents)
                        {
                            Debug.LogError($"[CosmosPlayback] Track animation not loaded: {trackAnim.trackName}");
                        }
                    }
                }
            }

            if (settings.logEvents)
                Debug.Log($"[CosmosPlayback] Started: {currentTimeline.timelineName}");
        }

        private void UpdatePlayback(float deltaTime)
        {
            if (currentTimeline == null || state != PlaybackState.Playing)
                return;

            // 시간 업데이트
            float adjustedDelta = deltaTime * playbackSpeed;

            if (isReversed)
            {
                currentTime -= adjustedDelta;
            }
            else
            {
                currentTime += adjustedDelta;
            }

            // TrackAnimation 처리 추가
            ProcessTrackAnimations();

            // 이벤트 처리
            ProcessEvents();

            // 종료 체크
            CheckPlaybackEnd();
        }


        private void ProcessTrackAnimations()
        {
            if (currentTimeline.trackAnimations == null) return;

            foreach (var trackAnim in currentTimeline.trackAnimations)
            {
                if (trackAnim == null || trackAnim.animationClip == null) continue;

                trackAnim.animationClip.SampleAnimation(this.gameObject, CurrentTime);

            }

        }


        private void ProcessEvents()
        {
            if (isReversed)
            {
                // 역재생 시 이미 실행된 이벤트 롤백
                ProcessReverseEvents();
            }
            else
            {
                // 정상 재생
                ProcessForwardEvents();
            }
        }

        private void ProcessForwardEvents()
        {
            var eventsToExecute = pendingEvents
                .Where(e => e.TriggerTime <= currentTime)
                .OrderBy(e => e.TriggerTime)
                .ToList();

            foreach (var evt in eventsToExecute)
            {
                if (eventFilter.ShouldProcessEvent(evt))
                {
                    ExecuteEvent(evt);
                }

                pendingEvents.Remove(evt);
                executedEvents.Add(evt);
            }

            // 프리로드 체크
            if (settings.autoPreloadResources)
            {
                PreloadUpcomingEvents();
            }
        }

        private void ProcessReverseEvents()
        {
            var eventsToRollback = executedEvents
                .Where(e => e.TriggerTime > currentTime)
                .OrderByDescending(e => e.TriggerTime)
                .ToList();

            foreach (var evt in eventsToRollback)
            {
                // 이벤트 롤백 (주로 이펙트 제거)
                RollbackEvent(evt);

                executedEvents.Remove(evt);
                pendingEvents.Add(evt);
            }
        }

        private void PreloadUpcomingEvents()
        {
            float preloadTime = currentTime + settings.preloadTimeOffset;

            var upcomingEvents = pendingEvents
                .Where(e => e.TriggerTime <= preloadTime)
                .ToList();

            foreach (var evt in upcomingEvents)
            {
                PreloadEventResources(evt);
            }
        }

        #endregion

        #region Event Execution

        private void ExecuteEvent(TimelineDataSO.ITimelineEvent evt)
        {
            try
            {
                switch (evt)
                {
                    case TimelineDataSO.AnimationEvent animEvent:
                        eventHandler?.HandleAnimationEvent(animEvent);
                        break;

                    case TimelineDataSO.EffectEvent effectEvent:
                        eventHandler?.HandleEffectEvent(effectEvent);
                        break;

                    case TimelineDataSO.SoundEvent soundEvent:
                        eventHandler?.HandleSoundEvent(soundEvent);
                        break;

                    case TimelineDataSO.CameraEvent cameraEvent:
                        eventHandler?.HandleCameraEvent(cameraEvent);
                        break;

                    case TimelineDataSO.CustomEvent customEvent:
                        eventHandler?.HandleCustomEvent(customEvent);
                        break;
                }

                OnEventTriggered?.Invoke(evt);

                if (settings.logEvents)
                {
                    LogEventExecution(evt);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CosmosPlayback] Failed to execute event: {e.Message}");
            }
        }

        private void RollbackEvent(TimelineDataSO.ITimelineEvent evt)
        {
            // 이펙트 이벤트의 경우 생성된 이펙트 제거
            if (evt is TimelineDataSO.EffectEvent effectEvent)
            {
                // EventHandler에서 관리하는 이펙트 정리 요청
                (eventHandler as TimelineEventHandler)?.CleanupEffect(effectEvent.effectAddressableKey);
            }
        }

        private void PreloadEventResources(TimelineDataSO.ITimelineEvent evt)
        {
            if (resourceProvider == null) return;

            switch (evt)
            {
                case TimelineDataSO.EffectEvent effect:
                    if (!string.IsNullOrEmpty(effect.effectAddressableKey))
                    {
                        if (!resourceProvider.IsResourceLoaded(effect.effectAddressableKey))
                        {
                            resourceProvider.LoadResourceAsync<GameObject>(
                                effect.effectAddressableKey,
                                prefab => { /* 프리로드만 */ }
                            );
                        }
                    }
                    break;
            }



        }

        private void LogEventExecution(TimelineDataSO.ITimelineEvent evt)
        {
            string eventInfo = evt switch
            {
                TimelineDataSO.AnimationEvent anim => $"Animation: {anim.animationStateName}",
                TimelineDataSO.EffectEvent effect => $"Effect: {effect.effectAddressableKey}",
                TimelineDataSO.SoundEvent sound => $"Sound: {sound.soundEventPath}",
                TimelineDataSO.CameraEvent camera => $"Camera: {camera.actionType}",
                TimelineDataSO.CustomEvent custom => $"Custom: {custom.eventName}",
                _ => "Unknown"
            };

            Debug.Log($"[CosmosPlayback] Execute [{evt.TriggerTime:F2}s] {eventInfo}");
        }

        #endregion

        #region Playback End & Loop

        private void CheckPlaybackEnd()
        {
            bool shouldEnd = false;

            if (!isReversed && currentTime >= Duration)
            {
                currentTime = Duration;
                shouldEnd = true;
            }
            else if (isReversed && currentTime <= 0f)
            {
                currentTime = 0f;
                shouldEnd = true;
            }

            if (!shouldEnd) return;

            // Track 애니메이션 재생 상태 초기화
            playingTracks.Clear();

            switch (mode)
            {
                case PlaybackMode.Once:
                    HandlePlaybackComplete();
                    break;

                case PlaybackMode.Loop:
                    HandleLoop();
                    break;

                case PlaybackMode.PingPong:
                    HandlePingPong();
                    break;
            }
        }

        private void HandlePlaybackComplete()
        {
            SetState(PlaybackState.Finished);
            OnPlaybackFinished?.Invoke();

            if (settings.logEvents)
                Debug.Log("[CosmosPlayback] Playback finished");
        }

        private void HandleLoop()
        {
            currentTime = 0f;

            // 이벤트 리셋
            pendingEvents.Clear();
            pendingEvents.AddRange(allEvents);
            executedEvents.Clear();

            OnLoopCompleted?.Invoke();

            if (settings.logEvents)
                Debug.Log("[CosmosPlayback] Loop completed, restarting");
        }

        private void HandlePingPong()
        {
            isReversed = !isReversed;

            if (!isReversed)
            {
                OnLoopCompleted?.Invoke();
            }

            if (settings.logEvents)
                Debug.Log($"[CosmosPlayback] PingPong - Direction: {(isReversed ? "Reverse" : "Forward")}");
        }

        #endregion

        #region Time Control

        public void PlayFromTime(float time)
        {
            Seek(time);
            Play();
        }

        public void Seek(float time)
        {
            if (currentTimeline == null) return;

            time = Mathf.Clamp(time, 0f, Duration);
            currentTime = time;

            // 이벤트 상태 재구성
            RebuildEventStates();

            if (settings.logEvents)
                Debug.Log($"[CosmosPlayback] Seeked to {time:F2}s");
        }

        public void SetNormalizedTime(float normalizedTime)
        {
            normalizedTime = Mathf.Clamp01(normalizedTime);
            Seek(normalizedTime * Duration);
        }

        private void RebuildEventStates()
        {
            pendingEvents.Clear();
            executedEvents.Clear();
            playingTracks.Clear();  // 추가

            foreach (var evt in allEvents)
            {
                if (evt.TriggerTime <= currentTime)
                {
                    executedEvents.Add(evt);

                    // Seek 시 즉시 실행이 필요한 이벤트 처리
                    if (ShouldExecuteOnSeek(evt))
                    {
                        ExecuteEvent(evt);
                    }
                }
                else
                {
                    pendingEvents.Add(evt);
                }
            }


            // Track 애니메이션 상태도 재구성
            if (currentTimeline.trackAnimations != null)
            {
                foreach (var trackAnim in currentTimeline.trackAnimations)
                {
                    if (trackAnim != null && trackAnim.animationClip != null)
                    {
                        bool inRange = currentTime >= trackAnim.startTime &&
                                     currentTime <= trackAnim.startTime + trackAnim.duration;
                        if (inRange)
                        {
                            (eventHandler as TimelineEventHandler)?.HandleTrackAnimation(trackAnim);
                            playingTracks.Add(trackAnim);
                        }
                    }
                }
            }

        }

        private bool ShouldExecuteOnSeek(TimelineDataSO.ITimelineEvent evt)
        {
            // Animation 이벤트는 즉시 적용
            if (evt is TimelineDataSO.AnimationEvent)
                return true;

            // 지속형 이펙트는 즉시 생성
            if (evt is TimelineDataSO.EffectEvent effect &&
                effect.playMode == TimelineDataSO.EffectPlayMode.Duration)
            {
                float endTime = effect.triggerTime + effect.duration;
                return currentTime >= effect.triggerTime && currentTime < endTime;
            }

            return false;
        }

        #endregion

        #region State Management

        private void SetState(PlaybackState newState)
        {
            if (state == newState) return;

            var oldState = state;
            state = newState;

            OnStateChanged?.Invoke(newState);
        }

        #endregion

        #region Cleanup

        private void CleanupResources()
        {
            CleanupActiveEffects();

            if (resourceProvider != null)
            {
                resourceProvider.ClearPreloadedResources();
            }

            playingTracks.Clear();  // 추가
        }

        private void CleanupActiveEffects()
        {
            if (eventHandler is TimelineEventHandler handler)
            {
                handler.CleanupAllEffects();
            }
        }

        #endregion

        #region Public Utilities

        /// <summary>
        /// 현재 재생 정보 가져오기
        /// </summary>
        public PlaybackResult GetPlaybackResult()
        {
            return new PlaybackResult
            {
                Success = state == PlaybackState.Finished || state == PlaybackState.Playing,
                TotalDuration = Duration,
                EventsTriggered = executedEvents.Count,
                EventsFailed = 0 // TODO: 실패 카운트 구현
            };
        }

        /// <summary>
        /// 이벤트 필터 설정
        /// </summary>
        public void SetEventFilter(EventFilter filter)
        {
            eventFilter = filter ?? new EventFilter();
        }

        /// <summary>
        /// 재생 설정 변경
        /// </summary>
        public void SetPlaybackSettings(PlaybackSettings newSettings)
        {
            settings = newSettings ?? new PlaybackSettings();
        }

        #endregion
    }
}