using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BattleCharacterSystem.Timeline;

namespace Cosmos.Timeline.Playback
{
    /// <summary>
    /// Timeline 큐 관리 시스템
    /// 여러 Timeline을 순차적으로 재생
    /// </summary>
    public class TimelineQueueSystem : MonoBehaviour, ITimelineQueue
    {
        #region Fields

        // 큐
        private Queue<TimelineDataSO> timelineQueue = new Queue<TimelineDataSO>();
        private TimelineDataSO currentTimeline;

        // Playback System
        private CosmosPlaybackSystem playbackSystem;

        // 상태
        private bool isPlaying = false;
        private int totalQueued = 0;
        private int completed = 0;

        // 설정
        [SerializeField] private float delayBetweenTimelines = 0f;
        [SerializeField] private bool autoPlayNext = true;
        [SerializeField] private bool loopQueue = false;

        // 큐 히스토리 (Loop용)
        private List<TimelineDataSO> queueHistory = new List<TimelineDataSO>();

        // 이벤트
        public event Action<TimelineDataSO> OnTimelineChanged;
        public event Action OnQueueCompleted;
        public event Action<TimelineDataSO> OnTimelineStarted;
        public event Action<TimelineDataSO> OnTimelineCompleted;

        // Properties
        public int Count => timelineQueue.Count;
        public bool IsPlaying => isPlaying;
        public TimelineDataSO CurrentTimeline => currentTimeline;
        public float Progress => totalQueued > 0 ? (float)completed / totalQueued : 0f;

        #endregion

        #region Initialization

        private void Awake()
        {
            InitializeSystem();
        }

        private void InitializeSystem()
        {
            // PlaybackSystem 찾기 또는 생성
            playbackSystem = GetComponent<CosmosPlaybackSystem>();
            if (playbackSystem == null)
            {
                playbackSystem = gameObject.AddComponent<CosmosPlaybackSystem>();
            }

            // 이벤트 연결
            playbackSystem.OnPlaybackFinished += OnCurrentTimelineFinished;
        }

        #endregion

        #region Queue Management

        public void Enqueue(TimelineDataSO timeline)
        {
            if (timeline == null)
            {
                Debug.LogWarning("[QueueSystem] Cannot enqueue null timeline");
                return;
            }

            timelineQueue.Enqueue(timeline);
            queueHistory.Add(timeline);
            totalQueued++;

            Debug.Log($"[QueueSystem] Enqueued: {timeline.timelineName} (Total: {Count})");
        }

        public void EnqueueRange(IEnumerable<TimelineDataSO> timelines)
        {
            foreach (var timeline in timelines)
            {
                Enqueue(timeline);
            }
        }

        public void Clear()
        {
            timelineQueue.Clear();
            queueHistory.Clear();
            currentTimeline = null;
            totalQueued = 0;
            completed = 0;
            isPlaying = false;

            playbackSystem.Stop();

            Debug.Log("[QueueSystem] Queue cleared");
        }

        public TimelineDataSO Dequeue()
        {
            if (timelineQueue.Count == 0) return null;
            return timelineQueue.Dequeue();
        }

        public TimelineDataSO Peek()
        {
            if (timelineQueue.Count == 0) return null;
            return timelineQueue.Peek();
        }

        #endregion

        #region Playback Control

        public void PlayQueue()
        {
            if (timelineQueue.Count == 0)
            {
                Debug.LogWarning("[QueueSystem] Queue is empty");
                return;
            }

            isPlaying = true;
            completed = 0;
            PlayNext();
        }

        public void PlayNext()
        {
            if (timelineQueue.Count == 0)
            {
                HandleQueueCompletion();
                return;
            }

            currentTimeline = timelineQueue.Dequeue();

            if (currentTimeline != null)
            {
                Debug.Log($"[QueueSystem] Playing: {currentTimeline.timelineName}");

                OnTimelineChanged?.Invoke(currentTimeline);
                OnTimelineStarted?.Invoke(currentTimeline);

                if (delayBetweenTimelines > 0)
                {
                    StartCoroutine(PlayWithDelay(currentTimeline, delayBetweenTimelines));
                }
                else
                {
                    playbackSystem.Play(currentTimeline);
                }
            }
        }

        private System.Collections.IEnumerator PlayWithDelay(TimelineDataSO timeline, float delay)
        {
            yield return new WaitForSeconds(delay);
            playbackSystem.Play(timeline);
        }

        public void SkipCurrent()
        {
            if (currentTimeline == null) return;

            Debug.Log($"[QueueSystem] Skipping: {currentTimeline.timelineName}");

            playbackSystem.Stop();

            if (autoPlayNext && isPlaying)
            {
                PlayNext();
            }
        }

        public void Pause()
        {
            playbackSystem.Pause();
        }

        public void Resume()
        {
            playbackSystem.Play();
        }

        public void Stop()
        {
            isPlaying = false;
            playbackSystem.Stop();
            Debug.Log("[QueueSystem] Stopped");
        }

        #endregion

        #region Event Handlers

        private void OnCurrentTimelineFinished()
        {
            if (currentTimeline != null)
            {
                completed++;
                OnTimelineCompleted?.Invoke(currentTimeline);

                Debug.Log($"[QueueSystem] Completed: {currentTimeline.timelineName} ({completed}/{totalQueued})");
            }

            if (autoPlayNext && isPlaying)
            {
                PlayNext();
            }
        }

        private void HandleQueueCompletion()
        {
            Debug.Log("[QueueSystem] Queue completed");

            if (loopQueue && queueHistory.Count > 0)
            {
                // 큐 다시 채우기
                foreach (var timeline in queueHistory)
                {
                    timelineQueue.Enqueue(timeline);
                }

                completed = 0;
                Debug.Log("[QueueSystem] Looping queue");

                if (isPlaying)
                {
                    PlayNext();
                }
            }
            else
            {
                isPlaying = false;
                OnQueueCompleted?.Invoke();
            }
        }

        #endregion

        #region Queue Operations

        /// <summary>
        /// 특정 Timeline을 우선순위로 삽입
        /// </summary>
        public void InsertPriority(TimelineDataSO timeline)
        {
            if (timeline == null) return;

            var tempQueue = new Queue<TimelineDataSO>();
            tempQueue.Enqueue(timeline);

            while (timelineQueue.Count > 0)
            {
                tempQueue.Enqueue(timelineQueue.Dequeue());
            }

            timelineQueue = tempQueue;
            totalQueued++;

            Debug.Log($"[QueueSystem] Inserted priority: {timeline.timelineName}");
        }

        /// <summary>
        /// 큐 순서 섞기
        /// </summary>
        public void Shuffle()
        {
            var list = timelineQueue.ToList();

            // Fisher-Yates shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }

            timelineQueue.Clear();
            foreach (var timeline in list)
            {
                timelineQueue.Enqueue(timeline);
            }

            Debug.Log("[QueueSystem] Queue shuffled");
        }

        /// <summary>
        /// 큐 내용 확인
        /// </summary>
        public List<TimelineDataSO> GetQueueContent()
        {
            return timelineQueue.ToList();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (playbackSystem != null)
            {
                playbackSystem.OnPlaybackFinished -= OnCurrentTimelineFinished;
            }

            Clear();
        }

        #endregion
    }

    /// <summary>
    /// Timeline 체인 빌더 - 편리한 큐 구성
    /// </summary>
    public class TimelineChainBuilder
    {
        private List<TimelineDataSO> chain = new List<TimelineDataSO>();
        private Dictionary<string, object> metadata = new Dictionary<string, object>();

        public TimelineChainBuilder Add(TimelineDataSO timeline)
        {
            if (timeline != null)
                chain.Add(timeline);
            return this;
        }

        public TimelineChainBuilder AddRange(params TimelineDataSO[] timelines)
        {
            chain.AddRange(timelines.Where(t => t != null));
            return this;
        }

        public TimelineChainBuilder AddWithRepeat(TimelineDataSO timeline, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Add(timeline);
            }
            return this;
        }

        public TimelineChainBuilder SetMetadata(string key, object value)
        {
            metadata[key] = value;
            return this;
        }

        public List<TimelineDataSO> Build()
        {
            return new List<TimelineDataSO>(chain);
        }

        public void ApplyTo(TimelineQueueSystem queueSystem)
        {
            queueSystem.Clear();
            queueSystem.EnqueueRange(chain);
        }
    }
}