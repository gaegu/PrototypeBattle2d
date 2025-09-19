using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace BattleCharacterSystem.Timeline
{
    /// <summary>
    /// Timeline Asset을 파싱해서 저장하는 ScriptableObject
    /// 런타임에서는 이 데이터만 사용
    /// </summary>
    [CreateAssetMenu(fileName = "TimelineData", menuName = "Battle/Timeline/TimelineData")]
    public class TimelineDataSO : ScriptableObject
    {
        [Header("Basic Info")]
        public string timelineName;
        public float duration;
        public string sourceTimelineAssetPath; // 원본 Timeline Asset 경로 (에디터 전용)

        [Header("Timeline Events")]
        public List<AnimationEvent> animationEvents = new List<AnimationEvent>();
        public List<EffectEvent> effectEvents = new List<EffectEvent>();
        public List<SoundEvent> soundEvents = new List<SoundEvent>();
        public List<CameraEvent> cameraEvents = new List<CameraEvent>();
        public List<CustomEvent> customEvents = new List<CustomEvent>();

        [Header("Resource References")]
        public List<string> requiredAddressableKeys = new List<string>(); // 필요한 모든 리소스 키


        // TimelineDataSO 클래스에 추가
        [Header("Track Level Animations")]
        public List<TrackAnimation> trackAnimations = new List<TrackAnimation>();
        /// <summary>
        /// Track 레벨 애니메이션 (Animated Values)
        /// </summary>
        [Serializable]
        public class TrackAnimation
        {
            public string trackName;              // Track 이름
            public AnimationClip animationClip;   // infiniteClip
            public string animationClipAddressableKey;  // 추가
            public float startTime = 0f;          // 시작 시간 (보통 0)
            public float duration;                 // 전체 Timeline duration과 동일
            public string targetPath;              // 바인딩 경로 (예: "Character/Sprite")
            public bool isLooping = false;        // 반복 여부
        }


        #region Event Definitions

        /// <summary>
        /// 애니메이션 이벤트
        /// </summary>
        [Serializable]
        public class AnimationEvent : ITimelineEvent
        {
            public float triggerTime;
            public string animationStateName;
            public float crossFadeDuration = 0.1f;
            public AnimationPlayMode playMode = AnimationPlayMode.CrossFade;

            // Animation Clip 정보 (선택적)
            public string animationClipAddressableKey;
            public float clipSpeed = 1f;
            public bool loop = false;

            public float TriggerTime => triggerTime;

            // 추가 필드
            public AnimationClip extractedClip;     // Timeline에서 추출한 AnimationClip
            public bool hasAnimatedValues;          // Animated Values 존재 여부
            public string targetBindingPath;        // 바인딩 경로 (예: "Character/Sprite")
        }

        /// <summary>
        /// 이펙트 이벤트
        /// </summary>
        [Serializable]
        public class EffectEvent : ITimelineEvent
        {
            public float triggerTime;
            public string effectAddressableKey;
            public Vector3 positionOffset;
            public Vector3 rotationOffset;
            public float scale = 1f;
            public float duration = 2f;
            public bool attachToActor = false;
            public string attachBoneName = ""; // 특정 본에 부착
            public EffectPlayMode playMode = EffectPlayMode.OneShot;

            public float TriggerTime => triggerTime;
        }

        /// <summary>
        /// 사운드 이벤트 (FMOD 대비)
        /// </summary>
        [Serializable]
        public class SoundEvent : ITimelineEvent
        {
            public float triggerTime;
            public string soundEventPath; // FMOD 이벤트 경로 또는 AudioClip 키
            public float volume = 1f;
            public bool is3D = false;
            public Vector3 positionOffset;

            public float TriggerTime => triggerTime;
        }

        /// <summary>
        /// 카메라 이벤트 (확장용)
        /// </summary>
        [Serializable]
        public class CameraEvent : ITimelineEvent
        {
            public float triggerTime;
            public CameraActionType actionType;
            public float duration = 0.5f;
            public float intensity = 1f;
            public AnimationCurve curve;

            public float TriggerTime => triggerTime;
        }

        /// <summary>
        /// 커스텀 이벤트 (확장용)
        /// </summary>
        [Serializable]
        public class CustomEvent : ITimelineEvent
        {
            public float triggerTime;
            public string eventName;
            public Dictionary<string, object> parameters = new Dictionary<string, object>();

            public float TriggerTime => triggerTime;
        }

        #endregion

        #region Enums

        public enum AnimationPlayMode
        {
            Play,
            CrossFade,
            CrossFadeQueued,
            PlayQueued
        }

        public enum EffectPlayMode
        {
            OneShot,        // 한번 재생 후 자동 제거
            Loop,           // 반복 재생
            Duration        // 지정 시간동안 재생
        }

        public enum CameraActionType
        {
            None,
            Shake,
            Zoom,
            SlowMotion,
            Flash,
            Fade
        }

        #endregion

        #region Interfaces

        public interface ITimelineEvent
        {
            float TriggerTime { get; }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 모든 이벤트를 시간순으로 정렬해서 반환
        /// </summary>
        public List<ITimelineEvent> GetAllEventsSorted()
        {
            var allEvents = new List<ITimelineEvent>();
            allEvents.AddRange(animationEvents);
            allEvents.AddRange(effectEvents);
            allEvents.AddRange(soundEvents);
            allEvents.AddRange(cameraEvents);
            allEvents.AddRange(customEvents);

            allEvents.Sort((a, b) => a.TriggerTime.CompareTo(b.TriggerTime));
            return allEvents;
        }

        /// <summary>
        /// 특정 시간 범위의 이벤트만 반환
        /// </summary>
        public List<ITimelineEvent> GetEventsInTimeRange(float startTime, float endTime)
        {
            var events = GetAllEventsSorted();
            return events.FindAll(e => e.TriggerTime >= startTime && e.TriggerTime <= endTime);
        }

        /// <summary>
        /// 필요한 리소스 키 수집
        /// </summary>
        public void CollectRequiredResources()
        {
            requiredAddressableKeys.Clear();

            // 애니메이션 클립
            foreach (var anim in animationEvents)
            {
                if (!string.IsNullOrEmpty(anim.animationClipAddressableKey))
                    requiredAddressableKeys.Add(anim.animationClipAddressableKey);
            }

            // 이펙트
            foreach (var effect in effectEvents)
            {
                if (!string.IsNullOrEmpty(effect.effectAddressableKey))
                    requiredAddressableKeys.Add(effect.effectAddressableKey);
            }

            // Track 애니메이션 추가
            foreach (var trackAnim in trackAnimations)
            {
                if (trackAnim.animationClip != null)
                {
                    if (!string.IsNullOrEmpty(trackAnim.animationClipAddressableKey))
                    {
                        requiredAddressableKeys.Add(trackAnim.animationClipAddressableKey);
                    }
                }
            }

            // 중복 제거
            requiredAddressableKeys = new List<string>(new HashSet<string>(requiredAddressableKeys));
        }

        #endregion

        #region Editor Only

#if UNITY_EDITOR
        /// <summary>
        /// Timeline Asset에서 데이터 갱신
        /// </summary>
        public void RefreshFromTimelineAsset(TimelineAsset timelineAsset)
        {
            if (timelineAsset == null) return;

            duration = (float)timelineAsset.duration;

            // 이벤트 클리어
            animationEvents.Clear();
            effectEvents.Clear();
            soundEvents.Clear();
            cameraEvents.Clear();
            customEvents.Clear();

            // Timeline 파싱 로직은 TimelineConverter에서 처리
        }

        private void OnValidate()
        {
            // 리소스 목록 자동 수집
            CollectRequiredResources();
        }
#endif

        #endregion
    }

}