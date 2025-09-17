using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmos.Timeline.Playback
{
    /// <summary>
    /// Timeline 동기화 제공자
    /// 프레임 독립적 재생 및 네트워크 동기화 지원
    /// </summary>
    public class TimelineSyncProvider : MonoBehaviour, ISyncProvider
    {
        #region Fields

        // 동기화 상태
        private bool isSynced = false;
        private float syncTime = 0f;
        private float lastSyncTime = 0f;

        // 마스터/슬레이브 모드
        private bool isMaster = true;
        private string syncId = "";

        // 동기화 버퍼
        private Queue<SyncData> syncBuffer = new Queue<SyncData>();
        private Dictionary<string, float> eventTimestamps = new Dictionary<string, float>();

        // 설정
        [SerializeField] private float syncInterval = 0.1f; // 100ms마다 동기화
        [SerializeField] private float syncTolerance = 0.016f; // 약 60fps
        [SerializeField] private int maxBufferSize = 100;
        [SerializeField] private bool useFrameIndependentUpdate = true;

        // 타이머
        private float timeSinceLastSync = 0f;
        private float deltaTimeAccumulator = 0f;

        // 이벤트
        public event Action<float> OnSyncTimeUpdated;
        public event Action<SyncData> OnSyncDataReceived;
        public event Action OnSyncStarted;
        public event Action OnSyncStopped;

        // Properties
        public bool IsSynced => isSynced;
        public float SyncTime => syncTime;
        public bool IsMaster => isMaster;
        public string SyncId => syncId;

        #endregion

        #region Data Structures

        [Serializable]
        public class SyncData
        {
            public string id;
            public float time;
            public string eventId;
            public SyncEventType eventType;
            public Dictionary<string, object> parameters;
            public float timestamp; // 실제 시간

            public SyncData()
            {
                parameters = new Dictionary<string, object>();
                timestamp = Time.realtimeSinceStartup;
            }
        }

        public enum SyncEventType
        {
            TimeUpdate,
            EventTrigger,
            StateChange,
            PlaybackControl
        }

        #endregion

        #region Initialization

        private void Awake()
        {
            GenerateSyncId();
        }

        private void GenerateSyncId()
        {
            syncId = $"Sync_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        public void Initialize(bool asMaster = true)
        {
            isMaster = asMaster;

            if (isMaster)
            {
                Debug.Log($"[SyncProvider] Initialized as MASTER: {syncId}");
            }
            else
            {
                Debug.Log($"[SyncProvider] Initialized as SLAVE: {syncId}");
            }
        }

        #endregion

        #region Sync Control

        public void StartSync()
        {
            if (isSynced) return;

            isSynced = true;
            syncTime = 0f;
            lastSyncTime = 0f;
            timeSinceLastSync = 0f;

            OnSyncStarted?.Invoke();

            Debug.Log($"[SyncProvider] Sync started: {syncId}");
        }

        public void StopSync()
        {
            if (!isSynced) return;

            isSynced = false;

            // 버퍼 정리
            syncBuffer.Clear();
            eventTimestamps.Clear();

            OnSyncStopped?.Invoke();

            Debug.Log($"[SyncProvider] Sync stopped: {syncId}");
        }

        public void SyncTo(float time)
        {
            float oldTime = syncTime;
            syncTime = time;

            // 허용 오차 내에서만 이벤트 발생
            if (Mathf.Abs(syncTime - oldTime) > syncTolerance)
            {
                OnSyncTimeUpdated?.Invoke(syncTime);

                if (isMaster)
                {
                    BroadcastTimeSync(syncTime);
                }
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!isSynced) return;

            if (useFrameIndependentUpdate)
            {
                UpdateFrameIndependent();
            }
            else
            {
                UpdateFrameDependent();
            }

            ProcessSyncBuffer();
        }

        private void UpdateFrameIndependent()
        {
            // 프레임 독립적 시간 업데이트
            float realDeltaTime = Time.unscaledDeltaTime;
            deltaTimeAccumulator += realDeltaTime;

            // 고정 간격으로 동기화
            while (deltaTimeAccumulator >= syncTolerance)
            {
                syncTime += syncTolerance;
                deltaTimeAccumulator -= syncTolerance;
                OnSyncTimeUpdated?.Invoke(syncTime);
            }

            // 주기적 동기화 브로드캐스트
            if (isMaster)
            {
                timeSinceLastSync += realDeltaTime;
                if (timeSinceLastSync >= syncInterval)
                {
                    BroadcastTimeSync(syncTime);
                    timeSinceLastSync = 0f;
                }
            }
        }

        private void UpdateFrameDependent()
        {
            // 일반 프레임 기반 업데이트
            float deltaTime = Time.deltaTime;
            syncTime += deltaTime;

            OnSyncTimeUpdated?.Invoke(syncTime);

            // 주기적 동기화
            if (isMaster)
            {
                timeSinceLastSync += deltaTime;
                if (timeSinceLastSync >= syncInterval)
                {
                    BroadcastTimeSync(syncTime);
                    timeSinceLastSync = 0f;
                }
            }
        }

        #endregion

        #region Network Sync

        public void SendSyncData(float time, string eventId)
        {
            if (!isSynced || !isMaster) return;

            var syncData = new SyncData
            {
                id = syncId,
                time = time,
                eventId = eventId,
                eventType = SyncEventType.EventTrigger
            };

            // 실제 네트워크 전송 구현
            TransmitSyncData(syncData);

            // 로컬 타임스탬프 기록
            eventTimestamps[eventId] = time;
        }

        public void ReceiveSyncData(float time, string eventId)
        {
            if (!isSynced || isMaster) return;

            var syncData = new SyncData
            {
                id = syncId,
                time = time,
                eventId = eventId,
                eventType = SyncEventType.EventTrigger
            };

            // 버퍼에 추가
            AddToSyncBuffer(syncData);
        }

        private void BroadcastTimeSync(float currentTime)
        {
            var syncData = new SyncData
            {
                id = syncId,
                time = currentTime,
                eventType = SyncEventType.TimeUpdate
            };

            TransmitSyncData(syncData);
        }

        private void TransmitSyncData(SyncData data)
        {
            // 실제 네트워크 전송 구현 위치
            // 예: Photon, Mirror, Netcode 등

            // 로컬 테스트용 직접 수신
            if (Application.isEditor)
            {
                SimulateNetworkReceive(data);
            }
        }

        private void SimulateNetworkReceive(SyncData data)
        {
            // Editor 테스트용 시뮬레이션
            // 약간의 지연 추가
            StartCoroutine(DelayedReceive(data, UnityEngine.Random.Range(0.01f, 0.05f)));
        }

        private System.Collections.IEnumerator DelayedReceive(SyncData data, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!isMaster)
            {
                AddToSyncBuffer(data);
            }
        }

        #endregion

        #region Sync Buffer

        private void AddToSyncBuffer(SyncData data)
        {
            if (syncBuffer.Count >= maxBufferSize)
            {
                syncBuffer.Dequeue(); // 오래된 데이터 제거
            }

            syncBuffer.Enqueue(data);
        }

        private void ProcessSyncBuffer()
        {
            while (syncBuffer.Count > 0)
            {
                var data = syncBuffer.Peek();

                // 현재 시간에 도달한 이벤트만 처리
                if (data.time <= syncTime + syncTolerance)
                {
                    syncBuffer.Dequeue();
                    ProcessSyncData(data);
                }
                else
                {
                    break; // 아직 시간이 안됨
                }
            }
        }

        private void ProcessSyncData(SyncData data)
        {
            OnSyncDataReceived?.Invoke(data);

            switch (data.eventType)
            {
                case SyncEventType.TimeUpdate:
                    if (!isMaster)
                    {
                        // 슬레이브는 마스터 시간에 맞춤
                        AdjustToMasterTime(data.time);
                    }
                    break;

                case SyncEventType.EventTrigger:
                    // 이벤트 트리거 처리
                    Debug.Log($"[SyncProvider] Event synced: {data.eventId} at {data.time:F3}s");
                    break;

                case SyncEventType.StateChange:
                    // 상태 변경 처리
                    break;

                case SyncEventType.PlaybackControl:
                    // 재생 제어 처리
                    break;
            }
        }

        private void AdjustToMasterTime(float masterTime)
        {
            float timeDiff = masterTime - syncTime;

            // 큰 차이가 있을 때만 조정
            if (Mathf.Abs(timeDiff) > syncTolerance * 2)
            {
                if (Mathf.Abs(timeDiff) > 1f)
                {
                    // 1초 이상 차이나면 즉시 동기화
                    syncTime = masterTime;
                    Debug.Log($"[SyncProvider] Hard sync to master: {masterTime:F3}s");
                }
                else
                {
                    // 점진적 동기화
                    syncTime = Mathf.Lerp(syncTime, masterTime, Time.deltaTime * 5f);
                }

                OnSyncTimeUpdated?.Invoke(syncTime);
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// 동기화 지연 시간 계산
        /// </summary>
        public float GetSyncLatency()
        {
            if (syncBuffer.Count == 0) return 0f;

            var oldest = syncBuffer.Peek();
            return Time.realtimeSinceStartup - oldest.timestamp;
        }

        /// <summary>
        /// 동기화 정확도 확인
        /// </summary>
        public float GetSyncAccuracy()
        {
            if (!isSynced || lastSyncTime == 0) return 1f;

            float expectedTime = lastSyncTime + (Time.realtimeSinceStartup - lastSyncTime);
            float diff = Mathf.Abs(syncTime - expectedTime);

            return Mathf.Clamp01(1f - (diff / syncTolerance));
        }

        /// <summary>
        /// 동기화 상태 정보
        /// </summary>
        public SyncStatus GetSyncStatus()
        {
            return new SyncStatus
            {
                IsSynced = isSynced,
                IsMaster = isMaster,
                SyncTime = syncTime,
                BufferSize = syncBuffer.Count,
                Latency = GetSyncLatency(),
                Accuracy = GetSyncAccuracy()
            };
        }

        [Serializable]
        public class SyncStatus
        {
            public bool IsSynced;
            public bool IsMaster;
            public float SyncTime;
            public int BufferSize;
            public float Latency;
            public float Accuracy;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!Application.isEditor || !isSynced) return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 150));
            GUILayout.Box($"Sync Provider: {(isMaster ? "MASTER" : "SLAVE")}");
            GUILayout.Label($"ID: {syncId.Substring(0, 8)}");
            GUILayout.Label($"Time: {syncTime:F3}s");
            GUILayout.Label($"Buffer: {syncBuffer.Count}/{maxBufferSize}");
            GUILayout.Label($"Latency: {GetSyncLatency() * 1000:F1}ms");
            GUILayout.Label($"Accuracy: {GetSyncAccuracy() * 100:F1}%");
            GUILayout.EndArea();
        }

        #endregion
    }

    /// <summary>
    /// 간단한 로컬 동기화 관리자
    /// </summary>
    public class LocalSyncManager : MonoBehaviour
    {
        private static LocalSyncManager instance;
        private Dictionary<string, TimelineSyncProvider> providers = new Dictionary<string, TimelineSyncProvider>();

        public static LocalSyncManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("LocalSyncManager");
                    instance = go.AddComponent<LocalSyncManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public void RegisterProvider(TimelineSyncProvider provider)
        {
            if (provider != null && !string.IsNullOrEmpty(provider.SyncId))
            {
                providers[provider.SyncId] = provider;
            }
        }

        public void UnregisterProvider(string syncId)
        {
            providers.Remove(syncId);
        }

        public void BroadcastSync(TimelineSyncProvider.SyncData data)
        {
            foreach (var provider in providers.Values)
            {
                if (provider.SyncId != data.id && !provider.IsMaster)
                {
                    provider.ReceiveSyncData(data.time, data.eventId);
                }
            }
        }
    }
}