using System;
using System.Collections.Generic;
using UnityEngine;
using BattleCharacterSystem.Timeline;

namespace Cosmos.Timeline.Playback
{
    /// <summary>
    /// Timeline 재생 상태
    /// </summary>
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused,
        Finished
    }

    /// <summary>
    /// 재생 모드
    /// </summary>
    public enum PlaybackMode
    {
        Once,       // 한번만 재생
        Loop,       // 반복 재생
        PingPong    // 왕복 재생
    }

    /// <summary>
    /// Timeline 재생 제어 인터페이스
    /// </summary>
    public interface ITimelinePlayer
    {
        // 상태
        PlaybackState State { get; }
        PlaybackMode Mode { get; set; }
        float PlaybackSpeed { get; set; }
        float CurrentTime { get; }
        float Duration { get; }
        float NormalizedTime { get; }

        // 기본 제어
        void Play();
        void Play(TimelineDataSO timeline);
        void Pause();
        void Stop();
        void Reset();

        // 시간 제어
        void PlayFromTime(float time);
        void Seek(float time);
        void SetNormalizedTime(float normalizedTime);

        // 이벤트
        event Action<PlaybackState> OnStateChanged;
        event Action OnPlaybackStarted;
        event Action OnPlaybackFinished;
        event Action OnLoopCompleted;
    }

    /// <summary>
    /// Timeline 이벤트 처리 인터페이스
    /// </summary>
    public interface ITimelineEventHandler
    {
        // 이벤트 처리
        void HandleAnimationEvent(TimelineDataSO.AnimationEvent animEvent);
        void HandleEffectEvent(TimelineDataSO.EffectEvent effectEvent);
        void HandleSoundEvent(TimelineDataSO.SoundEvent soundEvent);
        void HandleCameraEvent(TimelineDataSO.CameraEvent cameraEvent);
        void HandleCustomEvent(TimelineDataSO.CustomEvent customEvent);

        // 콜백
        event Action<TimelineDataSO.ITimelineEvent> OnEventTriggered;
        event Action<TimelineDataSO.ITimelineEvent> OnEventCompleted;
    }

    /// <summary>
    /// 리소스 제공 인터페이스
    /// </summary>
    public interface IResourceProvider
    {
        // Addressable 로드
        void LoadResourceAsync<T>(string key, Action<T> onLoaded) where T : UnityEngine.Object;
        void ReleaseResource(string key);
        bool IsResourceLoaded(string key);

        // 프리로드
        void PreloadResources(IEnumerable<string> keys);
        void ClearPreloadedResources();

        // 인스턴스 관리
        GameObject InstantiateEffect(string key, Vector3 position, Quaternion rotation);
        void DestroyEffect(GameObject effect);
    }

    /// <summary>
    /// Timeline 큐 관리 인터페이스
    /// </summary>
    public interface ITimelineQueue
    {
        // 큐 관리
        void Enqueue(TimelineDataSO timeline);
        void EnqueueRange(IEnumerable<TimelineDataSO> timelines);
        void Clear();
        int Count { get; }

        // 재생
        void PlayQueue();
        void PlayNext();
        void SkipCurrent();

        // 이벤트
        event Action<TimelineDataSO> OnTimelineChanged;
        event Action OnQueueCompleted;
    }

    /// <summary>
    /// 동기화 인터페이스
    /// </summary>
    public interface ISyncProvider
    {
        // 동기화 상태
        bool IsSynced { get; }
        float SyncTime { get; }

        // 동기화 제어
        void StartSync();
        void StopSync();
        void SyncTo(float time);

        // 네트워크 동기화
        void SendSyncData(float time, string eventId);
        void ReceiveSyncData(float time, string eventId);

        // 이벤트
        event Action<float> OnSyncTimeUpdated;
    }

    /// <summary>
    /// Playback 시스템 설정
    /// </summary>
    [Serializable]
    public class PlaybackSettings
    {
        [Header("General")]
        public bool autoPreloadResources = true;
        public float preloadTimeOffset = 1f; // 이벤트 발생 1초 전 미리 로드

        [Header("Performance")]
        public int maxConcurrentEffects = 10;
        public bool enablePooling = true;
        public int poolInitialSize = 5;

        [Header("Sync")]
        public bool enableFrameIndependentPlayback = true;
        public float syncTolerance = 0.016f; // 약 60fps

        [Header("Debug")]
        public bool logEvents = false;
        public bool showDebugUI = false;
    }

    /// <summary>
    /// 이벤트 컨텍스트 - 이벤트 실행 시 필요한 정보
    /// </summary>
    public class EventContext
    {
        public GameObject TargetObject { get; set; }
        public Transform TargetTransform { get; set; }
        public Animator TargetAnimator { get; set; }
        public Vector3 WorldPosition { get; set; }
        public Dictionary<string, object> CustomData { get; set; }

        public EventContext()
        {
            CustomData = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Timeline 재생 결과
    /// </summary>
    public class PlaybackResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public float TotalDuration { get; set; }
        public int EventsTriggered { get; set; }
        public int EventsFailed { get; set; }
    }

    /// <summary>
    /// 이벤트 필터 - 특정 이벤트만 재생
    /// </summary>
    [Serializable]
    public class EventFilter
    {
        public bool enableAnimationEvents = true;
        public bool enableEffectEvents = true;
        public bool enableSoundEvents = true;
        public bool enableCameraEvents = true;
        public bool enableCustomEvents = true;

        public List<string> excludedEventNames = new List<string>();
        public List<string> includedEventNames = new List<string>();

        public bool ShouldProcessEvent(TimelineDataSO.ITimelineEvent evt)
        {
            // 타입별 필터
            switch (evt)
            {
                case TimelineDataSO.AnimationEvent _ when !enableAnimationEvents:
                case TimelineDataSO.EffectEvent _ when !enableEffectEvents:
                case TimelineDataSO.SoundEvent _ when !enableSoundEvents:
                case TimelineDataSO.CameraEvent _ when !enableCameraEvents:
                case TimelineDataSO.CustomEvent _ when !enableCustomEvents:
                    return false;
            }

            // 이름 기반 필터
            string eventName = GetEventName(evt);

            if (includedEventNames.Count > 0 && !includedEventNames.Contains(eventName))
                return false;

            if (excludedEventNames.Contains(eventName))
                return false;

            return true;
        }

        private string GetEventName(TimelineDataSO.ITimelineEvent evt)
        {
            return evt switch
            {
                TimelineDataSO.AnimationEvent anim => anim.animationStateName,
                TimelineDataSO.EffectEvent effect => effect.effectAddressableKey,
                TimelineDataSO.SoundEvent sound => sound.soundEventPath,
                TimelineDataSO.CustomEvent custom => custom.eventName,
                _ => ""
            };
        }
    }
}