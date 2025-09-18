using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using GameCore.Addressables;
using GameCore.Pooling;

namespace GameCore.Preloading
{
    /// <summary>
    /// 사용자 패턴 기반 예측 프리로딩 시스템
    /// AddressableManager 및 PoolingSystem과 완전 연동
    /// </summary>
    public class AddressablePreloader : MonoBehaviour
    {
        #region Singleton

        private static AddressablePreloader _instance;
        public static AddressablePreloader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[AddressablePreloader]");
                    _instance = go.AddComponent<AddressablePreloader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        #endregion

        #region Nested Classes

        [System.Serializable]
        public class PreloadProfile
        {
            public string profileName;
            public List<PreloadItem> items = new List<PreloadItem>();
            public PreloadTrigger trigger;
            public int priority = 5;
            public bool cacheResult = true;
            public float timeoutSeconds = 30f;
        }

        [System.Serializable]
        public class PreloadItem
        {
            public string addressableKey;
            public PreloadType type;
            public int poolCount; // For pooling
            public bool keepLoaded;
            public int priority = 5;
        }

        public enum PreloadType
        {
            Asset,
            Pool,
            Scene,
            Dependencies
        }

        public enum PreloadTrigger
        {
            Manual,
            SceneLoad,
            Distance,
            Time,
            Custom
        }

        private class PreloadTask
        {
            public string Id { get; set; }
            public PreloadProfile Profile { get; set; }
            public List<string> LoadedAssets { get; set; } = new List<string>();
            public PreloadStatus Status { get; set; }
            public float Progress { get; set; }
            public float StartTime { get; set; }
            public float CompletionTime { get; set; }
            public Exception Error { get; set; }
        }

        public enum PreloadStatus
        {
            Pending,
            Loading,
            Completed,
            Failed,
            Cancelled
        }

        private class UserPattern
        {
            public Dictionary<string, int> AssetUsageCount = new Dictionary<string, int>();
            public Dictionary<string, float> AssetLastUsedTime = new Dictionary<string, float>();
            public Dictionary<string, List<string>> AssetSequences = new Dictionary<string, List<string>>();
            public Dictionary<string, float> SceneTransitionProbability = new Dictionary<string, float>();
            public List<string> RecentAssets = new List<string>();
            public int SessionCount;
            public float TotalPlayTime;
        }

        [System.Serializable]
        public class PreloadStatistics
        {
            public int TotalPreloads;
            public int SuccessfulPreloads;
            public int FailedPreloads;
            public float HitRate;
            public float AverageLoadTime;
            public long TotalBytesLoaded;
            public Dictionary<string, int> MostPreloadedAssets = new Dictionary<string, int>();
            public Dictionary<string, float> PredictionAccuracy = new Dictionary<string, float>();
        }

        #endregion

        #region Fields

        // 시스템 참조
        private GameCore.Addressables.AddressableManager _addressableManager;
        private AddressablePoolingSystem _poolingSystem;

        // 프리로드 관리
        private Dictionary<string, PreloadProfile> _profiles = new Dictionary<string, PreloadProfile>();
        private Dictionary<string, PreloadTask> _activeTasks = new Dictionary<string, PreloadTask>();
        private Queue<PreloadTask> _preloadQueue = new Queue<PreloadTask>();
        private HashSet<string> _preloadedAssets = new HashSet<string>();
        private Dictionary<string, List<string>> _scenePreloads = new Dictionary<string, List<string>>();

        // 사용자 패턴 분석
        private UserPattern _userPattern = new UserPattern();
        private const int PATTERN_HISTORY_SIZE = 100;
        private const float PATTERN_DECAY_RATE = 0.95f;

        // 예측 엔진
        private Dictionary<string, float> _assetPredictionScores = new Dictionary<string, float>();
        private float _lastPredictionUpdate;
        private const float PREDICTION_UPDATE_INTERVAL = 5f;

        // 백그라운드 로딩
        private bool _isBackgroundLoading;
        private int _maxConcurrentLoads = 3;
        private int _currentLoadCount;

        // 설정
        [Header("Configuration")]
        [SerializeField] private bool _enablePredictiveLoading = true;
        [SerializeField] private bool _enablePatternLearning = true;
        [SerializeField] private float _preloadDistance = 50f;
        [SerializeField] private float _preloadTimeAhead = 10f;
        [SerializeField] private int _maxPreloadedAssets = 50;
        [SerializeField] private bool _autoCleanupUnused = true;
        [SerializeField] private float _unusedAssetTimeout = 60f;

        // 통계
        private PreloadStatistics _statistics = new PreloadStatistics();
        private Dictionary<string, float> _assetLoadTimes = new Dictionary<string, float>();

        // 이벤트
        public event Action<string, float> OnPreloadProgress;
        public event Action<string> OnPreloadComplete;
        public event Action<string, Exception> OnPreloadFailed;
        public event Action<PreloadStatistics> OnStatisticsUpdated;
        public event Action<string, float> OnPredictionScoreUpdated;

        #endregion

        #region Unity Lifecycle

        private async void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            await Initialize();
        }

        private void Update()
        {
            // 예측 업데이트
            if (_enablePredictiveLoading && Time.time - _lastPredictionUpdate > PREDICTION_UPDATE_INTERVAL)
            {
                UpdatePredictions();
                _lastPredictionUpdate = Time.time;
            }

            // 백그라운드 로딩 처리
            if (!_isBackgroundLoading && _preloadQueue.Count > 0)
            {
                _ = ProcessPreloadQueue();
            }

            // 미사용 에셋 정리
            if (_autoCleanupUnused && Time.frameCount % 300 == 0)
            {
                CleanupUnusedPreloads();
            }
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (_addressableManager != null)
            {
                _addressableManager.OnLoadError -= HandleLoadError;
                _addressableManager.OnMemoryWarning -= HandleMemoryWarning;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            // 모든 프리로드 정리
            ClearAllPreloads();
        }

        #endregion

        #region Initialization

        private async Task Initialize()
        {
            Debug.Log("[Preloader] Initializing...");

            // 시스템 참조 획득
            _addressableManager = GameCore.Addressables.AddressableManager.Instance;
            _poolingSystem = AddressablePoolingSystem.Instance;

            // AddressableManager 초기화 대기
            int retryCount = 0;
            while (!_addressableManager.IsInitialized && retryCount < 20)
            {
                await Task.Delay(500);
                retryCount++;
            }

            if (!_addressableManager.IsInitialized)
            {
                Debug.LogError("[Preloader] AddressableManager initialization timeout");
                return;
            }

            // 이벤트 구독
            _addressableManager.OnLoadError += HandleLoadError;
            _addressableManager.OnMemoryWarning += HandleMemoryWarning;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // 사용자 패턴 로드
            LoadUserPattern();

            // 기본 프로파일 로드
            LoadDefaultProfiles();

            Debug.Log("[Preloader] Initialization complete");
        }

        #endregion

        #region Profile Management

        /// <summary>
        /// 프리로드 프로파일 등록
        /// </summary>
        public void RegisterProfile(PreloadProfile profile)
        {
            if (string.IsNullOrEmpty(profile.profileName))
            {
                Debug.LogError("[Preloader] Invalid profile: missing name");
                return;
            }

            _profiles[profile.profileName] = profile;

            // 씬 트리거인 경우 매핑
            if (profile.trigger == PreloadTrigger.SceneLoad)
            {
                // 프로파일 이름에서 씬 이름 추출 (예: "Battle_Preload" -> "Battle")
                string sceneName = profile.profileName.Replace("_Preload", "");
                if (!_scenePreloads.ContainsKey(sceneName))
                {
                    _scenePreloads[sceneName] = new List<string>();
                }
                _scenePreloads[sceneName].Add(profile.profileName);
            }

            Debug.Log($"[Preloader] Registered profile: {profile.profileName}");
        }

        /// <summary>
        /// 프로파일 실행
        /// </summary>
        public async UniTask<bool> ExecuteProfile(string profileName)
        {
            if (!_profiles.TryGetValue(profileName, out var profile))
            {
                Debug.LogError($"[Preloader] Profile not found: {profileName}");
                return false;
            }

            var task = new PreloadTask
            {
                Id = Guid.NewGuid().ToString(),
                Profile = profile,
                Status = PreloadStatus.Pending,
                StartTime = Time.time
            };

            _activeTasks[task.Id] = task;

            // 우선순위에 따라 큐에 추가
            if (profile.priority >= 8)
            {
                // 높은 우선순위는 즉시 실행
                await ExecutePreloadTask(task);
            }
            else
            {
                // 낮은 우선순위는 큐에 추가
                _preloadQueue.Enqueue(task);
            }

            return task.Status == PreloadStatus.Completed;
        }

        #endregion

        #region Preload Execution

        /// <summary>
        /// 프리로드 작업 실행
        /// </summary>
        private async UniTask ExecutePreloadTask(PreloadTask task)
        {
            task.Status = PreloadStatus.Loading;
            _currentLoadCount++;

            try
            {
                Debug.Log($"[Preloader] Executing profile: {task.Profile.profileName}");

                // 타임아웃 설정
                var timeoutToken = new System.Threading.CancellationTokenSource();
                timeoutToken.CancelAfter(TimeSpan.FromSeconds(task.Profile.timeoutSeconds));

                // 아이템별 로드
                var loadTasks = new List<UniTask>();
                int totalItems = task.Profile.items.Count;
                int loadedItems = 0;

                foreach (var item in task.Profile.items.OrderByDescending(i => i.priority))
                {
                    var loadTask = LoadPreloadItem(item, task);
                    loadTasks.Add(loadTask);

                    // 진행률 업데이트
                    loadedItems++;
                    task.Progress = (float)loadedItems / totalItems;
                    OnPreloadProgress?.Invoke(task.Profile.profileName, task.Progress);

                    // 동시 로드 제한
                    if (loadTasks.Count >= _maxConcurrentLoads)
                    {
                        await UniTask.WhenAny(loadTasks);
                        loadTasks.RemoveAll(t => t.Status == UniTaskStatus.Succeeded);
                    }
                }

                // 남은 작업 완료 대기
                await UniTask.WhenAll(loadTasks);

                task.Status = PreloadStatus.Completed;
                task.CompletionTime = Time.time;

                // 통계 업데이트
                _statistics.TotalPreloads++;
                _statistics.SuccessfulPreloads++;
                _statistics.AverageLoadTime = (_statistics.AverageLoadTime * (_statistics.SuccessfulPreloads - 1) +
                                                (task.CompletionTime - task.StartTime)) / _statistics.SuccessfulPreloads;

                OnPreloadComplete?.Invoke(task.Profile.profileName);

                Debug.Log($"[Preloader] Profile completed: {task.Profile.profileName} ({task.CompletionTime - task.StartTime:F2}s)");
            }
            catch (Exception e)
            {
                task.Status = PreloadStatus.Failed;
                task.Error = e;

                _statistics.TotalPreloads++;
                _statistics.FailedPreloads++;

                OnPreloadFailed?.Invoke(task.Profile.profileName, e);

                Debug.LogError($"[Preloader] Profile failed: {task.Profile.profileName} - {e.Message}");
            }
            finally
            {
                _currentLoadCount--;
                _activeTasks.Remove(task.Id);
            }
        }

        /// <summary>
        /// 개별 아이템 로드
        /// </summary>
        private async UniTask LoadPreloadItem(PreloadItem item, PreloadTask task)
        {
            try
            {
                float startTime = Time.time;

                switch (item.type)
                {
                    case PreloadType.Asset:
                        await PreloadAsset(item.addressableKey, item.keepLoaded);
                        break;

                    case PreloadType.Pool:
                        await PreloadPool(item.addressableKey, item.poolCount);
                        break;

                    case PreloadType.Scene:
                        await PreloadScene(item.addressableKey);
                        break;

                    case PreloadType.Dependencies:
                        await PreloadDependencies(item.addressableKey);
                        break;
                }

                // 로드 시간 기록
                float loadTime = Time.time - startTime;
                _assetLoadTimes[item.addressableKey] = loadTime;

                // 로드된 에셋 추가
                task.LoadedAssets.Add(item.addressableKey);
                _preloadedAssets.Add(item.addressableKey);

                // 사용자 패턴 업데이트
                if (_enablePatternLearning)
                {
                    UpdateUserPattern(item.addressableKey);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Preloader] Failed to load item: {item.addressableKey} - {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// 에셋 프리로드
        /// </summary>
        private async UniTask PreloadAsset(string addressableKey, bool keepLoaded)
        {
            var asset = await _addressableManager.LoadAssetAsync<UnityEngine.Object>(addressableKey);

            if (asset != null && !keepLoaded)
            {
                // keepLoaded가 false면 참조만 유지하고 나중에 해제
                _preloadedAssets.Add(addressableKey);
            }
        }

        /// <summary>
        /// 풀 프리로드
        /// </summary>
        private async UniTask PreloadPool(string addressableKey, int count)
        {
            await _poolingSystem.PrewarmPool(addressableKey, count);
        }

        /// <summary>
        /// 씬 프리로드
        /// </summary>
        private async UniTask PreloadScene(string sceneName)
        {
            // AddressableManager를 통한 씬 로드 (비활성 상태)
            await _addressableManager.LoadSceneAsync(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);

            // 씬을 비활성화
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid())
            {
                var rootObjects = scene.GetRootGameObjects();
                foreach (var obj in rootObjects)
                {
                    obj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 종속성 프리로드
        /// </summary>
        private async UniTask PreloadDependencies(string addressableKey)
        {
            await _addressableManager.DownloadDependenciesAsync(addressableKey);
        }

        #endregion

        #region Predictive Loading

        /// <summary>
        /// 예측 업데이트
        /// </summary>
        private void UpdatePredictions()
        {
            if (!_enablePredictiveLoading) return;

            _assetPredictionScores.Clear();

            // 최근 사용 패턴 기반 예측
            foreach (var kvp in _userPattern.AssetUsageCount)
            {
                string asset = kvp.Key;
                int usageCount = kvp.Value;

                float score = 0f;

                // 사용 빈도
                score += usageCount * 0.3f;

                // 최근 사용 시간
                if (_userPattern.AssetLastUsedTime.ContainsKey(asset))
                {
                    float timeSinceLastUse = Time.time - _userPattern.AssetLastUsedTime[asset];
                    score += Mathf.Max(0, 10f - timeSinceLastUse) * 0.2f;
                }

                // 시퀀스 패턴
                if (_userPattern.RecentAssets.Count > 0)
                {
                    string lastAsset = _userPattern.RecentAssets.Last();
                    if (_userPattern.AssetSequences.ContainsKey(lastAsset))
                    {
                        var sequence = _userPattern.AssetSequences[lastAsset];
                        if (sequence.Contains(asset))
                        {
                            score += 5f * 0.5f;
                        }
                    }
                }

                _assetPredictionScores[asset] = score;
                OnPredictionScoreUpdated?.Invoke(asset, score);
            }

            // 상위 예측 에셋 프리로드
            var topPredictions = _assetPredictionScores
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Where(kvp => kvp.Value > 3f && !_preloadedAssets.Contains(kvp.Key));

            foreach (var prediction in topPredictions)
            {
                var item = new PreloadItem
                {
                    addressableKey = prediction.Key,
                    type = PreloadType.Asset,
                    keepLoaded = false,
                    priority = Mathf.CeilToInt(prediction.Value)
                };

                var profile = new PreloadProfile
                {
                    profileName = $"Predictive_{prediction.Key}",
                    items = new List<PreloadItem> { item },
                    trigger = PreloadTrigger.Custom,
                    priority = Mathf.CeilToInt(prediction.Value)
                };

                _ = ExecuteProfile(profile.profileName);
            }
        }

        /// <summary>
        /// 사용자 패턴 업데이트
        /// </summary>
        private void UpdateUserPattern(string assetKey)
        {
            // 사용 횟수 증가
            if (!_userPattern.AssetUsageCount.ContainsKey(assetKey))
            {
                _userPattern.AssetUsageCount[assetKey] = 0;
            }
            _userPattern.AssetUsageCount[assetKey]++;

            // 최근 사용 시간 업데이트
            _userPattern.AssetLastUsedTime[assetKey] = Time.time;

            // 최근 에셋 리스트 업데이트
            _userPattern.RecentAssets.Add(assetKey);
            if (_userPattern.RecentAssets.Count > PATTERN_HISTORY_SIZE)
            {
                _userPattern.RecentAssets.RemoveAt(0);
            }

            // 시퀀스 패턴 업데이트
            if (_userPattern.RecentAssets.Count >= 2)
            {
                string prevAsset = _userPattern.RecentAssets[_userPattern.RecentAssets.Count - 2];
                if (!_userPattern.AssetSequences.ContainsKey(prevAsset))
                {
                    _userPattern.AssetSequences[prevAsset] = new List<string>();
                }

                if (!_userPattern.AssetSequences[prevAsset].Contains(assetKey))
                {
                    _userPattern.AssetSequences[prevAsset].Add(assetKey);

                    // 시퀀스 크기 제한
                    if (_userPattern.AssetSequences[prevAsset].Count > 10)
                    {
                        _userPattern.AssetSequences[prevAsset].RemoveAt(0);
                    }
                }
            }

            // 히트율 계산
            if (_preloadedAssets.Contains(assetKey))
            {
                _statistics.HitRate = (_statistics.HitRate * _statistics.TotalPreloads + 1) / (_statistics.TotalPreloads + 1);
            }
        }

        #endregion

        #region Scene-based Preloading

        /// <summary>
        /// 씬 로드 시 처리
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[Preloader] Scene loaded: {scene.name}");

            // 씬별 프리로드 실행
            if (_scenePreloads.ContainsKey(scene.name))
            {
                foreach (var profileName in _scenePreloads[scene.name])
                {
                    _ = ExecuteProfile(profileName);
                }
            }

            // 예측 기반 프리로드
            PredictSceneAssets(scene.name);

            // 씬 전환 패턴 업데이트
            UpdateSceneTransitionPattern(scene.name);
        }

        /// <summary>
        /// 씬 언로드 시 처리
        /// </summary>
        private void OnSceneUnloaded(Scene scene)
        {
            Debug.Log($"[Preloader] Scene unloaded: {scene.name}");

            // 씬 관련 프리로드 정리
            CleanupScenePreloads(scene.name);
        }

        /// <summary>
        /// 씬별 에셋 예측
        /// </summary>
        private void PredictSceneAssets(string sceneName)
        {
            // 씬별 자주 사용되는 에셋 프리로드
            var sceneAssets = GetCommonSceneAssets(sceneName);

            foreach (var asset in sceneAssets)
            {
                if (!_preloadedAssets.Contains(asset))
                {
                    var item = new PreloadItem
                    {
                        addressableKey = asset,
                        type = PreloadType.Asset,
                        keepLoaded = false,
                        priority = 3
                    };

                    var profile = new PreloadProfile
                    {
                        profileName = $"Scene_{sceneName}_{asset}",
                        items = new List<PreloadItem> { item },
                        trigger = PreloadTrigger.SceneLoad,
                        priority = 3
                    };

                    _ = ExecuteProfile(profile.profileName);
                }
            }
        }

        /// <summary>
        /// 씬 전환 패턴 업데이트
        /// </summary>
        private void UpdateSceneTransitionPattern(string sceneName)
        {
            // 이전 씬에서 현재 씬으로의 전환 확률 업데이트
            if (_userPattern.RecentAssets.Count > 0)
            {
                string key = $"{_userPattern.RecentAssets.Last()}_{sceneName}";
                if (!_userPattern.SceneTransitionProbability.ContainsKey(key))
                {
                    _userPattern.SceneTransitionProbability[key] = 0f;
                }
                _userPattern.SceneTransitionProbability[key] += 0.1f;
                _userPattern.SceneTransitionProbability[key] = Mathf.Min(1f, _userPattern.SceneTransitionProbability[key]);
            }
        }

        #endregion

        #region Background Loading

        /// <summary>
        /// 백그라운드 큐 처리
        /// </summary>
        private async UniTask ProcessPreloadQueue()
        {
            if (_isBackgroundLoading) return;

            _isBackgroundLoading = true;

            while (_preloadQueue.Count > 0 && _currentLoadCount < _maxConcurrentLoads)
            {
                var task = _preloadQueue.Dequeue();

                // 메모리 체크
                if (_addressableManager != null)
                {
                    var memStatus = _addressableManager.GetMemoryStatus();
                    if (memStatus.EstimatedMemoryMB > 500)
                    {
                        Debug.LogWarning("[Preloader] Delaying preload due to high memory usage");
                        await UniTask.Delay(5000);
                        continue;
                    }
                }

                _ = ExecutePreloadTask(task);

                // 프레임 분산
                await UniTask.Yield();
            }

            _isBackgroundLoading = false;
        }

        /// <summary>
        /// 거리 기반 프리로드
        /// </summary>
        public void PreloadByDistance(Vector3 position, float radius = -1)
        {
            if (radius < 0) radius = _preloadDistance;

            // 거리 기반 프리로드 로직
            var nearbyAssets = GetAssetsNearPosition(position, radius);

            foreach (var asset in nearbyAssets)
            {
                if (!_preloadedAssets.Contains(asset))
                {
                    var item = new PreloadItem
                    {
                        addressableKey = asset,
                        type = PreloadType.Asset,
                        keepLoaded = false,
                        priority = 2
                    };

                    var profile = new PreloadProfile
                    {
                        profileName = $"Distance_{asset}",
                        items = new List<PreloadItem> { item },
                        trigger = PreloadTrigger.Distance,
                        priority = 2
                    };

                    RegisterProfile(profile);
                    _ = ExecuteProfile(profile.profileName);
                }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 미사용 프리로드 정리
        /// </summary>
        private void CleanupUnusedPreloads()
        {
            var currentTime = Time.time;
            var toRemove = new List<string>();

            foreach (var asset in _preloadedAssets)
            {
                if (_userPattern.AssetLastUsedTime.ContainsKey(asset))
                {
                    float timeSinceLastUse = currentTime - _userPattern.AssetLastUsedTime[asset];
                    if (timeSinceLastUse > _unusedAssetTimeout)
                    {
                        toRemove.Add(asset);
                    }
                }
            }

            foreach (var asset in toRemove)
            {
                ReleasePreloadedAsset(asset);
            }

            if (toRemove.Count > 0)
            {
                Debug.Log($"[Preloader] Cleaned up {toRemove.Count} unused preloads");
            }
        }

        /// <summary>
        /// 씬 프리로드 정리
        /// </summary>
        private void CleanupScenePreloads(string sceneName)
        {
            var toRemove = _preloadedAssets
                .Where(a => a.Contains(sceneName))
                .ToList();

            foreach (var asset in toRemove)
            {
                ReleasePreloadedAsset(asset);
            }
        }

        /// <summary>
        /// 프리로드된 에셋 해제
        /// </summary>
        private void ReleasePreloadedAsset(string addressableKey)
        {
            if (_preloadedAssets.Remove(addressableKey))
            {
                _addressableManager.ReleaseAsset(addressableKey);
                Debug.Log($"[Preloader] Released: {addressableKey}");
            }
        }

        /// <summary>
        /// 모든 프리로드 정리
        /// </summary>
        public void ClearAllPreloads()
        {
            foreach (var asset in _preloadedAssets.ToList())
            {
                ReleasePreloadedAsset(asset);
            }

            _preloadedAssets.Clear();
            _activeTasks.Clear();
            _preloadQueue.Clear();

            Debug.Log("[Preloader] All preloads cleared");
        }

        #endregion

        #region Memory Management

        /// <summary>
        /// 메모리 경고 처리
        /// </summary>
        private void HandleMemoryWarning(GameCore.Addressables.AddressableManager.MemoryStatus status)
        {
            Debug.LogWarning($"[Preloader] Memory warning: {status.EstimatedMemoryMB}MB");

            // 낮은 우선순위 프리로드 취소
            CancelLowPriorityPreloads();

            // 미사용 프리로드 즉시 정리
            CleanupUnusedPreloads();

            // 큐 정리
            if (status.EstimatedMemoryMB > 600)
            {
                _preloadQueue.Clear();
                Debug.Log("[Preloader] Cleared preload queue due to memory pressure");
            }
        }

        /// <summary>
        /// 낮은 우선순위 프리로드 취소
        /// </summary>
        private void CancelLowPriorityPreloads()
        {
            var toCancel = _activeTasks.Values
                .Where(t => t.Profile.priority < 5 && t.Status == PreloadStatus.Loading)
                .ToList();

            foreach (var task in toCancel)
            {
                task.Status = PreloadStatus.Cancelled;
                _activeTasks.Remove(task.Id);
            }

            if (toCancel.Count > 0)
            {
                Debug.Log($"[Preloader] Cancelled {toCancel.Count} low priority preloads");
            }
        }

        #endregion

        #region Statistics & Monitoring

        /// <summary>
        /// 통계 가져오기
        /// </summary>
        public PreloadStatistics GetStatistics()
        {
            return _statistics;
        }

        /// <summary>
        /// 프리로드 상태 가져오기
        /// </summary>
        public string GetStatus()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=== Preloader Status ===");
            sb.AppendLine($"Preloaded Assets: {_preloadedAssets.Count}/{_maxPreloadedAssets}");
            sb.AppendLine($"Active Tasks: {_activeTasks.Count}");
            sb.AppendLine($"Queue Size: {_preloadQueue.Count}");
            sb.AppendLine($"Current Loads: {_currentLoadCount}/{_maxConcurrentLoads}");

            sb.AppendLine("\n=== Statistics ===");
            sb.AppendLine($"Total Preloads: {_statistics.TotalPreloads}");
            sb.AppendLine($"Success Rate: {(_statistics.SuccessfulPreloads / (float)_statistics.TotalPreloads * 100):F1}%");
            sb.AppendLine($"Hit Rate: {_statistics.HitRate * 100:F1}%");
            sb.AppendLine($"Average Load Time: {_statistics.AverageLoadTime:F2}s");

            if (_enablePredictiveLoading)
            {
                sb.AppendLine("\n=== Top Predictions ===");
                var topPredictions = _assetPredictionScores
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(5);

                foreach (var pred in topPredictions)
                {
                    sb.AppendLine($"  {pred.Key}: {pred.Value:F2}");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 로드 에러 처리
        /// </summary>
        private void HandleLoadError(string address, Exception error)
        {
            Debug.LogError($"[Preloader] Load error for {address}: {error.Message}");

            // 에러 통계 업데이트
            _statistics.FailedPreloads++;

            // 해당 에셋을 프리로드 목록에서 제거
            _preloadedAssets.Remove(address);
        }

        /// <summary>
        /// 사용자 패턴 저장
        /// </summary>
        private void SaveUserPattern()
        {
            // PlayerPrefs 또는 파일로 저장
            string json = JsonUtility.ToJson(_userPattern);
            PlayerPrefs.SetString("UserPattern", json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 사용자 패턴 로드
        /// </summary>
        private void LoadUserPattern()
        {
            string json = PlayerPrefs.GetString("UserPattern", "");
            if (!string.IsNullOrEmpty(json))
            {
                _userPattern = JsonUtility.FromJson<UserPattern>(json);
            }
        }

        /// <summary>
        /// 기본 프로파일 로드
        /// </summary>
        private void LoadDefaultProfiles()
        {
            // 기본 전투 프로파일
            var battleProfile = new PreloadProfile
            {
                profileName = "Battle_Preload",
                items = new List<PreloadItem>
                {
                    new PreloadItem { addressableKey = "UI_Battle_HUD", type = PreloadType.Asset, priority = 10 },
                    new PreloadItem { addressableKey = "Effect_Common_Hit", type = PreloadType.Pool, poolCount = 10, priority = 8 },
                    new PreloadItem { addressableKey = "Effect_Common_Critical", type = PreloadType.Pool, poolCount = 5, priority = 7 }
                },
                trigger = PreloadTrigger.SceneLoad,
                priority = 8
            };
            RegisterProfile(battleProfile);

            // 기본 타운 프로파일
            var townProfile = new PreloadProfile
            {
                profileName = "Town_Preload",
                items = new List<PreloadItem>
                {
                    new PreloadItem { addressableKey = "UI_Shop_Window", type = PreloadType.Asset, priority = 5 },
                    new PreloadItem { addressableKey = "UI_Inventory_Window", type = PreloadType.Asset, priority = 5 }
                },
                trigger = PreloadTrigger.SceneLoad,
                priority = 5
            };
            RegisterProfile(townProfile);
        }

        /// <summary>
        /// 씬별 공통 에셋 가져오기
        /// </summary>
        private List<string> GetCommonSceneAssets(string sceneName)
        {
            // 실제로는 설정 파일이나 데이터베이스에서 가져옴
            var commonAssets = new List<string>();

            switch (sceneName)
            {
                case "Battle":
                    commonAssets.Add("Effect_Common_Hit");
                    commonAssets.Add("Effect_Common_Critical");
                    commonAssets.Add("UI_Battle_HUD");
                    break;

                case "Town":
                    commonAssets.Add("UI_Shop_Window");
                    commonAssets.Add("UI_Inventory_Window");
                    break;
            }

            return commonAssets;
        }

        /// <summary>
        /// 위치 근처 에셋 가져오기
        /// </summary>
        private List<string> GetAssetsNearPosition(Vector3 position, float radius)
        {
            // 실제로는 공간 분할 트리나 그리드 시스템 사용
            var nearbyAssets = new List<string>();

            // 예시: 위치에 따른 에셋 매핑
            // 실제 구현에서는 더 정교한 공간 인덱싱 사용

            return nearbyAssets;
        }

        #endregion
    }
}