using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using GameCore.Addressables;
using System.Threading.Tasks;

namespace GameCore.Pooling
{
    /// <summary>
    /// AddressableManager와 연동된 오브젝트 풀링 시스템
    /// 메모리 효율성과 성능 최적화를 위한 필수 컴포넌트
    /// </summary>
    public class AddressablePoolingSystem : MonoBehaviour
    {
        #region Singleton

        private static AddressablePoolingSystem _instance;
        public static AddressablePoolingSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[AddressablePoolingSystem]");
                    _instance = go.AddComponent<AddressablePoolingSystem>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        #endregion

        #region Nested Classes

        [System.Serializable]
        public class PoolConfig
        {
            public string poolName;
            public string addressableKey;
            public int initialSize = 5;
            public int maxSize = 50;
            public bool autoExpand = true;
            public float cullDelay = 30f;
            public bool prewarm = false;
            public Transform containerParent;
            public PoolCategory category = PoolCategory.Effect;
            public int priority = 0; // 메모리 부족 시 정리 우선순위
        }

        public enum PoolCategory
        {
            Character,
            Effect,
            UI,
            Projectile,
            Environment,
            Item
        }

        private class Pool
        {
            public string Name { get; set; }
            public string AddressableKey { get; set; }
            public GameObject Prefab { get; set; }
            public Transform Container { get; set; }
            public Queue<GameObject> Available { get; set; } = new Queue<GameObject>();
            public HashSet<GameObject> InUse { get; set; } = new HashSet<GameObject>();
            public PoolConfig Config { get; set; }
            public float LastUsedTime { get; set; }
            public int TotalCreated { get; set; }
            public int PeakUsage { get; set; }
            public bool IsLoading { get; set; }

            // 통계
            public int TotalSpawned { get; set; }
            public int TotalDespawned { get; set; }
            public float AverageActiveTime { get; set; }
            public Dictionary<GameObject, float> SpawnTimes { get; set; } = new Dictionary<GameObject, float>();

            // 메모리 추정
            public long EstimatedMemorySize { get; set; }
        }

        [System.Serializable]
        public class PooledObject : MonoBehaviour
        {
            public string PoolKey { get; set; }
            public float SpawnTime { get; set; }
            public Action<GameObject> OnDespawnCallback { get; set; }

            private List<IPooledBehaviour> _pooledBehaviours = new List<IPooledBehaviour>();

            private void Awake()
            {
                _pooledBehaviours = GetComponentsInChildren<IPooledBehaviour>().ToList();
            }

            public void OnSpawn()
            {
                SpawnTime = Time.time;
                foreach (var behaviour in _pooledBehaviours)
                {
                    behaviour.OnSpawn();
                }
            }

            public void OnDespawn()
            {
                OnDespawnCallback?.Invoke(gameObject);
                OnDespawnCallback = null;

                foreach (var behaviour in _pooledBehaviours)
                {
                    behaviour.OnDespawn();
                }
            }
        }

        public interface IPooledBehaviour
        {
            void OnSpawn();
            void OnDespawn();
        }

        public class PoolStatistics
        {
            public string PoolName { get; set; }
            public string AddressableKey { get; set; }
            public PoolCategory Category { get; set; }
            public int AvailableCount { get; set; }
            public int InUseCount { get; set; }
            public int TotalCreated { get; set; }
            public int PeakUsage { get; set; }
            public float UtilizationRate { get; set; }
            public int TotalSpawned { get; set; }
            public int TotalDespawned { get; set; }
            public float AverageActiveTime { get; set; }
            public long EstimatedMemoryMB { get; set; }
        }

        #endregion

        #region Fields

        // AddressableManager 참조
        private GameCore.Addressables.AddressableManager _addressableManager;

        // 풀 관리
        private Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();
        private Dictionary<GameObject, string> _instanceToPoolMap = new Dictionary<GameObject, string>();
        private Dictionary<string, PoolConfig> _poolConfigs = new Dictionary<string, PoolConfig>();
        private Dictionary<PoolCategory, List<string>> _poolsByCategory = new Dictionary<PoolCategory, List<string>>();

        // 워밍업
        private Queue<string> _warmupQueue = new Queue<string>();
        private bool _isWarmingUp = false;

        // 컬링
        private float _lastCullTime;
        private const float CULL_INTERVAL = 10f;

        // 메모리 관리
        private long _totalPoolMemory = 0;
        private const long MEMORY_WARNING_THRESHOLD_MB = 100;
        private const long MEMORY_CRITICAL_THRESHOLD_MB = 200;

        // 설정
        [Header("Configuration")]
        [SerializeField] private bool _enableStatistics = true;
        [SerializeField] private bool _enableAutoCleanup = true;
        [SerializeField] private int _globalMaxInstances = 1000;
        [SerializeField] private float _defaultCullDelay = 30f;
        [SerializeField] private bool _integrateWithAddressableManager = true;

        // 씬별 풀 관리
        private Dictionary<GameCore.Addressables.AddressableManager.SceneType, List<string>> _sceneSpecificPools = new Dictionary<GameCore.Addressables.AddressableManager.SceneType, List<string>>();
        private GameCore.Addressables.AddressableManager.SceneType _currentSceneType = GameCore.Addressables.AddressableManager.SceneType.Core;

        // 이벤트
        public event Action<string, GameObject> OnObjectSpawned;
        public event Action<string, GameObject> OnObjectDespawned;
        public event Action<string> OnPoolCreated;
        public event Action<string> OnPoolDestroyed;
        public event Action<PoolStatistics> OnPoolStatisticsUpdated;
        public event Action<long> OnMemoryWarning;

        // 통계
        private int _currentActiveInstances = 0;
        private Dictionary<string, int> _poolUsageHistory = new Dictionary<string, int>();

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

            await InitializeSystem();
        }

        private void Update()
        {
            if (_enableAutoCleanup && Time.time - _lastCullTime > CULL_INTERVAL)
            {
                CullInactivePools();
                _lastCullTime = Time.time;
            }

            // 통계 업데이트
            if (_enableStatistics && Time.frameCount % 60 == 0)
            {
                UpdateStatistics();
            }
        }

        private void OnDestroy()
        {
            // AddressableManager 이벤트 구독 해제
            if (_addressableManager != null)
            {
                _addressableManager.OnMemoryWarning -= HandleMemoryWarning;
                _addressableManager.OnLoadError -= HandleLoadError;
            }

            ClearAllPools();
        }

        #endregion

        #region Initialization

        private async Task InitializeSystem()
        {
            Debug.Log("[PoolingSystem] Initializing with AddressableManager integration...");

            // AddressableManager 참조 획득
            _addressableManager = GameCore.Addressables.AddressableManager.Instance;

            // AddressableManager 초기화 대기
            int retryCount = 0;
            while (!_addressableManager.IsInitialized && retryCount < 20)
            {
                await Task.Delay(500);
                retryCount++;
            }

            if (!_addressableManager.IsInitialized)
            {
                Debug.LogError("[PoolingSystem] AddressableManager initialization timeout");
                _integrateWithAddressableManager = false;
                return;
            }

            // 이벤트 구독
            if (_integrateWithAddressableManager)
            {
                _addressableManager.OnMemoryWarning += HandleMemoryWarning;
                _addressableManager.OnLoadError += HandleLoadError;
            }

            // 카테고리별 리스트 초기화
            foreach (PoolCategory category in Enum.GetValues(typeof(PoolCategory)))
            {
                _poolsByCategory[category] = new List<string>();
            }

            // 씬별 풀 리스트 초기화
            foreach (GameCore.Addressables.AddressableManager.SceneType sceneType in Enum.GetValues(typeof(GameCore.Addressables.AddressableManager.SceneType)))
            {
                _sceneSpecificPools[sceneType] = new List<string>();
            }

            Debug.Log("[PoolingSystem] Initialization complete");
        }

        /// <summary>
        /// 풀 설정 등록
        /// </summary>
        public void RegisterPoolConfig(PoolConfig config, GameCore.Addressables.AddressableManager.SceneType? sceneType = null)
        {
            if (string.IsNullOrEmpty(config.addressableKey))
            {
                Debug.LogError("[PoolingSystem] Invalid pool config: missing addressable key");
                return;
            }

            _poolConfigs[config.addressableKey] = config;

            // 카테고리별 분류
            if (!_poolsByCategory[config.category].Contains(config.addressableKey))
            {
                _poolsByCategory[config.category].Add(config.addressableKey);
            }

            // 씬별 분류
            if (sceneType.HasValue)
            {
                if (!_sceneSpecificPools[sceneType.Value].Contains(config.addressableKey))
                {
                    _sceneSpecificPools[sceneType.Value].Add(config.addressableKey);
                }
            }

            // 프리웜 큐에 추가
            if (config.prewarm)
            {
                _warmupQueue.Enqueue(config.addressableKey);
                if (!_isWarmingUp)
                {
                    _ = ProcessWarmupQueue();
                }
            }

            Debug.Log($"[PoolingSystem] Registered pool config: {config.poolName ?? config.addressableKey}");
        }

        #endregion

        #region Pool Management with AddressableManager

        /// <summary>
        /// 풀 생성 또는 가져오기 (AddressableManager 연동)
        /// </summary>
        private async UniTask<Pool> GetOrCreatePool(string addressableKey)
        {
            if (_pools.TryGetValue(addressableKey, out var existingPool))
            {
                existingPool.LastUsedTime = Time.time;
                return existingPool;
            }

            // 이미 로딩 중인지 확인
            if (_pools.Values.Any(p => p.AddressableKey == addressableKey && p.IsLoading))
            {
                await UniTask.WaitUntil(() => _pools.ContainsKey(addressableKey));
                return _pools[addressableKey];
            }

            return await CreatePool(addressableKey);
        }

        /// <summary>
        /// 새 풀 생성 (AddressableManager 사용)
        /// </summary>
        private async UniTask<Pool> CreatePool(string addressableKey)
        {
            try
            {
                Debug.Log($"[PoolingSystem] Creating pool via AddressableManager: {addressableKey}");

                // 임시 풀 생성 (로딩 중 표시)
                var tempPool = new Pool
                {
                    AddressableKey = addressableKey,
                    IsLoading = true
                };
                _pools[addressableKey] = tempPool;

                // AddressableManager를 통해 Prefab 로드
                GameObject prefab = null;
                if (_integrateWithAddressableManager)
                {
                    prefab = await _addressableManager.LoadAssetAsync<GameObject>(addressableKey);
                }
                else
                {
                    // Fallback: 직접 로드
                    var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>(addressableKey);
                    prefab = await handle.Task;
                }

                if (prefab == null)
                {
                    Debug.LogError($"[PoolingSystem] Failed to load prefab: {addressableKey}");
                    _pools.Remove(addressableKey);
                    return null;
                }

                // Pool 컨테이너 생성
                GameObject containerGO = new GameObject($"Pool_{addressableKey}");
                containerGO.transform.SetParent(transform);

                // Pool 설정
                var config = _poolConfigs.ContainsKey(addressableKey)
                    ? _poolConfigs[addressableKey]
                    : CreateDefaultConfig(addressableKey);

                var pool = new Pool
                {
                    Name = config.poolName ?? addressableKey,
                    AddressableKey = addressableKey,
                    Prefab = prefab,
                    Container = containerGO.transform,
                    Config = config,
                    LastUsedTime = Time.time,
                    IsLoading = false,
                    EstimatedMemorySize = EstimateMemorySize(prefab)
                };

                // 기존 임시 풀 교체
                _pools[addressableKey] = pool;

                // 초기 인스턴스 생성
                await CreateInitialInstances(pool, config.initialSize);

                // 전체 메모리 업데이트
                UpdateTotalMemory();

                OnPoolCreated?.Invoke(addressableKey);

                return pool;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PoolingSystem] Pool creation failed: {e.Message}");
                _pools.Remove(addressableKey);
                return null;
            }
        }

        /// <summary>
        /// 메모리 크기 추정
        /// </summary>
        private long EstimateMemorySize(GameObject prefab)
        {
            long size = 0;

            // MeshRenderer 메모리 추정
            var meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in meshRenderers)
            {
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
                {
                    var texture = renderer.sharedMaterial.mainTexture;
                    size += texture.width * texture.height * 4; // RGBA
                }
            }

            // ParticleSystem 메모리 추정
            var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>();
            size += particleSystems.Length * 1024 * 100; // 약 100KB per particle system

            // 기본 GameObject 오버헤드
            size += 1024 * 10; // 10KB

            return size;
        }

        #endregion

        #region Spawn & Despawn

        /// <summary>
        /// 오브젝트 스폰
        /// </summary>
        public async UniTask<GameObject> Spawn(string addressableKey, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            // 메모리 체크
            if (_integrateWithAddressableManager)
            {
                var memStatus = _addressableManager.GetMemoryStatus();
                if (memStatus.EstimatedMemoryMB > 600)
                {
                    Debug.LogWarning("[PoolingSystem] High memory usage, triggering cleanup before spawn");
                    CleanupLowPriorityPools();
                }
            }

            var pool = await GetOrCreatePool(addressableKey);
            if (pool == null)
            {
                Debug.LogError($"[PoolingSystem] Failed to get pool: {addressableKey}");
                return null;
            }

            GameObject instance = null;

            // 사용 가능한 인스턴스 찾기
            if (pool.Available.Count > 0)
            {
                instance = pool.Available.Dequeue();
            }
            else if (pool.TotalCreated < pool.Config.maxSize || pool.Config.autoExpand)
            {
                // 새 인스턴스 생성
                instance = await CreateNewInstance(pool);
            }
            else
            {
                // 풀이 가득 참 - 가장 오래된 active 인스턴스 강제 회수
                if (pool.Config.autoExpand)
                {
                    instance = await CreateNewInstance(pool);
                }
                else
                {
                    Debug.LogWarning($"[PoolingSystem] Pool {pool.Name} is full. Max size: {pool.Config.maxSize}");
                    return null;
                }
            }

            if (instance == null) return null;

            // 위치 설정
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            if (parent != null)
            {
                instance.transform.SetParent(parent);
            }

            // 활성화
            instance.SetActive(true);
            pool.InUse.Add(instance);

            // PooledObject 컴포넌트 처리
            var pooledObject = instance.GetComponent<PooledObject>();
            if (pooledObject != null)
            {
                pooledObject.OnSpawn();
            }

            // 통계 업데이트
            pool.TotalSpawned++;
            pool.LastUsedTime = Time.time;
            if (pool.InUse.Count > pool.PeakUsage)
            {
                pool.PeakUsage = pool.InUse.Count;
            }

            _currentActiveInstances++;
            OnObjectSpawned?.Invoke(addressableKey, instance);

            return instance;
        }

        /// <summary>
        /// 오브젝트 디스폰
        /// </summary>
        public void Despawn(GameObject instance, float delay = 0f)
        {
            if (instance == null) return;

            if (delay > 0)
            {
                _ = DespawnDelayed(instance, delay);
                return;
            }

            DespawnImmediate(instance);
        }

        private void DespawnImmediate(GameObject instance)
        {
            if (!_instanceToPoolMap.TryGetValue(instance, out string poolKey))
            {
                Debug.LogWarning("[PoolingSystem] Trying to despawn non-pooled object");
                Destroy(instance);
                return;
            }

            if (!_pools.TryGetValue(poolKey, out Pool pool))
            {
                Debug.LogError($"[PoolingSystem] Pool not found: {poolKey}");
                Destroy(instance);
                return;
            }

            // PooledObject 컴포넌트 처리
            var pooledObject = instance.GetComponent<PooledObject>();
            if (pooledObject != null)
            {
                float activeTime = Time.time - pooledObject.SpawnTime;
                pool.AverageActiveTime = (pool.AverageActiveTime * pool.TotalDespawned + activeTime) / (pool.TotalDespawned + 1);
                pooledObject.OnDespawn();
            }

            // 비활성화 및 풀로 반환
            instance.SetActive(false);
            instance.transform.SetParent(pool.Container);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            pool.InUse.Remove(instance);
            pool.Available.Enqueue(instance);

            // 통계 업데이트
            pool.TotalDespawned++;
            _currentActiveInstances--;

            OnObjectDespawned?.Invoke(poolKey, instance);
        }

        private async UniTask DespawnDelayed(GameObject instance, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));

            if (instance != null)
            {
                DespawnImmediate(instance);
            }
        }

        /// <summary>
        /// 새 인스턴스 생성
        /// </summary>
        private async UniTask<GameObject> CreateNewInstance(Pool pool)
        {
            if (_currentActiveInstances >= _globalMaxInstances)
            {
                Debug.LogWarning($"[PoolingSystem] Global max instances reached: {_globalMaxInstances}");
                return null;
            }

            await UniTask.Yield(); // 프레임 분산

            GameObject instance = Instantiate(pool.Prefab, pool.Container);
            instance.name = $"{pool.Prefab.name}_{pool.TotalCreated:000}";
            instance.SetActive(false);

            // PooledObject 컴포넌트 추가
            var pooledObject = instance.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                pooledObject = instance.AddComponent<PooledObject>();
            }
            pooledObject.PoolKey = pool.AddressableKey;

            // 인스턴스 매핑
            _instanceToPoolMap[instance] = pool.AddressableKey;

            pool.TotalCreated++;
            pool.EstimatedMemorySize += EstimateMemorySize(instance);

            return instance;
        }

        #endregion

        #region Memory Management Integration

        /// <summary>
        /// AddressableManager 메모리 경고 처리
        /// </summary>
        private void HandleMemoryWarning(GameCore.Addressables.AddressableManager.MemoryStatus status)
        {
            Debug.LogWarning($"[PoolingSystem] Memory warning from AddressableManager. Used: {status.EstimatedMemoryMB}MB");

            if (status.EstimatedMemoryMB > 700)
            {
                // Critical: 적극적인 정리
                ForceCleanupAllPools(0.5f); // 50% 정리
            }
            else if (status.EstimatedMemoryMB > 500)
            {
                // Warning: 부분 정리
                CleanupLowPriorityPools();
                CullInactivePools();
            }

            OnMemoryWarning?.Invoke(_totalPoolMemory);
        }

        /// <summary>
        /// 낮은 우선순위 풀 정리
        /// </summary>
        private void CleanupLowPriorityPools()
        {
            var poolsToClean = _pools.Values
                .Where(p => p.Config.priority < 5)
                .OrderBy(p => p.Config.priority)
                .ThenBy(p => p.LastUsedTime)
                .Take(5);

            foreach (var pool in poolsToClean)
            {
                CleanupPool(pool, 0.7f); // 70% 정리
            }
        }

        /// <summary>
        /// 풀 정리
        /// </summary>
        private void CleanupPool(Pool pool, float cleanupRatio)
        {
            int toRemove = Mathf.CeilToInt(pool.Available.Count * cleanupRatio);

            for (int i = 0; i < toRemove && pool.Available.Count > 0; i++)
            {
                var instance = pool.Available.Dequeue();
                _instanceToPoolMap.Remove(instance);
                Destroy(instance);
                pool.TotalCreated--;
            }

            Debug.Log($"[PoolingSystem] Cleaned up {toRemove} instances from pool {pool.Name}");
        }

        /// <summary>
        /// 씬 전환 시 정리 (AddressableManager 연동)
        /// </summary>
        public void OnSceneChange(GameCore.Addressables.AddressableManager.SceneType fromScene, GameCore.Addressables.AddressableManager.SceneType toScene)
        {
            Debug.Log($"[PoolingSystem] Scene change: {fromScene} -> {toScene}");

            _currentSceneType = toScene;

            // 이전 씬의 풀 정리
            if (_sceneSpecificPools.ContainsKey(fromScene))
            {
                foreach (var poolKey in _sceneSpecificPools[fromScene])
                {
                    if (_pools.TryGetValue(poolKey, out var pool))
                    {
                        DestroyPool(poolKey);
                    }
                }
            }

            // 새 씬의 풀 프리웜
            if (_sceneSpecificPools.ContainsKey(toScene))
            {
                foreach (var poolKey in _sceneSpecificPools[toScene])
                {
                    if (_poolConfigs.TryGetValue(poolKey, out var config) && config.prewarm)
                    {
                        _warmupQueue.Enqueue(poolKey);
                    }
                }

                if (!_isWarmingUp)
                {
                    _ = ProcessWarmupQueue();
                }
            }
        }

        #endregion

        #region Cleanup & Culling

        /// <summary>
        /// 비활성 풀 컬링
        /// </summary>
        private void CullInactivePools()
        {
            var currentTime = Time.time;
            var poolsToCull = _pools.Values
                .Where(p => currentTime - p.LastUsedTime > p.Config.cullDelay && p.InUse.Count == 0)
                .ToList();

            foreach (var pool in poolsToCull)
            {
                int cullCount = Mathf.Min(pool.Available.Count / 2, 10);
                for (int i = 0; i < cullCount && pool.Available.Count > pool.Config.initialSize; i++)
                {
                    var instance = pool.Available.Dequeue();
                    _instanceToPoolMap.Remove(instance);
                    Destroy(instance);
                    pool.TotalCreated--;
                }

                if (cullCount > 0)
                {
                    Debug.Log($"[PoolingSystem] Culled {cullCount} instances from pool {pool.Name}");
                }
            }

            UpdateTotalMemory();
        }

        /// <summary>
        /// 강제 전체 풀 정리
        /// </summary>
        public void ForceCleanupAllPools(float cleanupRatio = 0.5f)
        {
            Debug.Log($"[PoolingSystem] Force cleanup all pools (ratio: {cleanupRatio:P})");

            foreach (var pool in _pools.Values)
            {
                CleanupPool(pool, cleanupRatio);
            }

            // AddressableManager에도 정리 요청
            if (_integrateWithAddressableManager)
            {
                _addressableManager.ForceCleanup();
            }

            Resources.UnloadUnusedAssets();
            GC.Collect();

            UpdateTotalMemory();
        }

        /// <summary>
        /// 특정 풀 제거
        /// </summary>
        public void DestroyPool(string addressableKey)
        {
            if (!_pools.TryGetValue(addressableKey, out var pool))
            {
                return;
            }

            // 모든 사용 중인 인스턴스 강제 회수
            foreach (var instance in pool.InUse.ToList())
            {
                DespawnImmediate(instance);
            }

            // 모든 인스턴스 제거
            while (pool.Available.Count > 0)
            {
                var instance = pool.Available.Dequeue();
                _instanceToPoolMap.Remove(instance);
                Destroy(instance);
            }

            // 컨테이너 제거
            if (pool.Container != null)
            {
                Destroy(pool.Container.gameObject);
            }

            // AddressableManager에서 리소스 해제
            if (_integrateWithAddressableManager)
            {
                _addressableManager.ReleaseAsset(addressableKey);
            }

            // 풀 제거
            _pools.Remove(addressableKey);

            // 카테고리별 리스트에서 제거
            foreach (var list in _poolsByCategory.Values)
            {
                list.Remove(addressableKey);
            }

            OnPoolDestroyed?.Invoke(addressableKey);
            UpdateTotalMemory();

            Debug.Log($"[PoolingSystem] Destroyed pool: {addressableKey}");
        }

        /// <summary>
        /// 모든 풀 제거
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var poolKey in _pools.Keys.ToList())
            {
                DestroyPool(poolKey);
            }

            _pools.Clear();
            _instanceToPoolMap.Clear();
            _currentActiveInstances = 0;
            _totalPoolMemory = 0;

            Debug.Log("[PoolingSystem] All pools cleared");
        }

        #endregion

        #region Warmup

        /// <summary>
        /// 워밍업 큐 처리
        /// </summary>
        private async UniTask ProcessWarmupQueue()
        {
            if (_isWarmingUp) return;

            _isWarmingUp = true;

            while (_warmupQueue.Count > 0)
            {
                var addressableKey = _warmupQueue.Dequeue();

                if (!_pools.ContainsKey(addressableKey))
                {
                    await GetOrCreatePool(addressableKey);
                }

                // 프레임 분산
                if (_warmupQueue.Count > 0)
                {
                    await UniTask.Delay(100);
                }
            }

            _isWarmingUp = false;

            Debug.Log("[PoolingSystem] Warmup complete");
        }

        /// <summary>
        /// 수동 프리웜
        /// </summary>
        public async UniTask PrewarmPool(string addressableKey, int count = -1)
        {
            var pool = await GetOrCreatePool(addressableKey);
            if (pool == null) return;

            int targetCount = count > 0 ? count : pool.Config.initialSize;
            int toCreate = targetCount - pool.Available.Count - pool.InUse.Count;

            if (toCreate <= 0) return;

            await CreateInitialInstances(pool, toCreate);
        }

        /// <summary>
        /// 카테고리별 프리웜
        /// </summary>
        public async UniTask PrewarmCategory(PoolCategory category)
        {
            if (!_poolsByCategory.ContainsKey(category))
            {
                return;
            }

            var tasks = new List<UniTask>();

            foreach (var poolKey in _poolsByCategory[category])
            {
                if (_poolConfigs.TryGetValue(poolKey, out var config))
                {
                    tasks.Add(PrewarmPool(poolKey, config.initialSize));
                }
            }

            await UniTask.WhenAll(tasks);

            Debug.Log($"[PoolingSystem] Category {category} prewarmed");
        }

        #endregion

        #region Statistics & Monitoring

        /// <summary>
        /// 전체 메모리 업데이트
        /// </summary>
        private void UpdateTotalMemory()
        {
            _totalPoolMemory = 0;

            foreach (var pool in _pools.Values)
            {
                _totalPoolMemory += pool.EstimatedMemorySize;
            }

            // MB 단위로 변환
            _totalPoolMemory = _totalPoolMemory / (1024 * 1024);

            if (_totalPoolMemory > MEMORY_CRITICAL_THRESHOLD_MB)
            {
                Debug.LogError($"[PoolingSystem] Critical memory usage: {_totalPoolMemory}MB");
                OnMemoryWarning?.Invoke(_totalPoolMemory);
            }
            else if (_totalPoolMemory > MEMORY_WARNING_THRESHOLD_MB)
            {
                Debug.LogWarning($"[PoolingSystem] High memory usage: {_totalPoolMemory}MB");
            }
        }

        /// <summary>
        /// 통계 업데이트
        /// </summary>
        private void UpdateStatistics()
        {
            foreach (var pool in _pools.Values)
            {
                var stats = new PoolStatistics
                {
                    PoolName = pool.Name,
                    AddressableKey = pool.AddressableKey,
                    Category = pool.Config.category,
                    AvailableCount = pool.Available.Count,
                    InUseCount = pool.InUse.Count,
                    TotalCreated = pool.TotalCreated,
                    PeakUsage = pool.PeakUsage,
                    UtilizationRate = pool.TotalCreated > 0 ? (float)pool.InUse.Count / pool.TotalCreated : 0,
                    TotalSpawned = pool.TotalSpawned,
                    TotalDespawned = pool.TotalDespawned,
                    AverageActiveTime = pool.AverageActiveTime,
                    EstimatedMemoryMB = pool.EstimatedMemorySize / (1024 * 1024)
                };

                OnPoolStatisticsUpdated?.Invoke(stats);
            }
        }

        /// <summary>
        /// 풀 통계 가져오기
        /// </summary>
        public PoolStatistics GetPoolStatistics(string addressableKey)
        {
            if (!_pools.TryGetValue(addressableKey, out var pool))
            {
                return null;
            }

            return new PoolStatistics
            {
                PoolName = pool.Name,
                AddressableKey = pool.AddressableKey,
                Category = pool.Config.category,
                AvailableCount = pool.Available.Count,
                InUseCount = pool.InUse.Count,
                TotalCreated = pool.TotalCreated,
                PeakUsage = pool.PeakUsage,
                UtilizationRate = pool.TotalCreated > 0 ? (float)pool.InUse.Count / pool.TotalCreated : 0,
                TotalSpawned = pool.TotalSpawned,
                TotalDespawned = pool.TotalDespawned,
                AverageActiveTime = pool.AverageActiveTime,
                EstimatedMemoryMB = pool.EstimatedMemorySize / (1024 * 1024)
            };
        }

        /// <summary>
        /// 전체 통계 가져오기
        /// </summary>
        public List<PoolStatistics> GetAllStatistics()
        {
            var stats = new List<PoolStatistics>();

            foreach (var pool in _pools.Values)
            {
                stats.Add(GetPoolStatistics(pool.AddressableKey));
            }

            return stats;
        }

        /// <summary>
        /// 시스템 상태 정보
        /// </summary>
        public string GetSystemStatus()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=== Pooling System Status ===");
            sb.AppendLine($"Total Pools: {_pools.Count}");
            sb.AppendLine($"Active Instances: {_currentActiveInstances}");
            sb.AppendLine($"Total Memory: {_totalPoolMemory}MB");
            sb.AppendLine($"AddressableManager Integration: {_integrateWithAddressableManager}");

            if (_integrateWithAddressableManager && _addressableManager != null)
            {
                var memStatus = _addressableManager.GetMemoryStatus();
                sb.AppendLine($"AddressableManager Memory: {memStatus.EstimatedMemoryMB}MB");
            }

            sb.AppendLine("\n=== Pool Details ===");
            foreach (var category in _poolsByCategory.Keys)
            {
                var categoryPools = _poolsByCategory[category];
                if (categoryPools.Count > 0)
                {
                    sb.AppendLine($"\n[{category}]");
                    foreach (var poolKey in categoryPools)
                    {
                        if (_pools.TryGetValue(poolKey, out var pool))
                        {
                            sb.AppendLine($"  {pool.Name}: {pool.InUse.Count}/{pool.TotalCreated} (Peak: {pool.PeakUsage})");
                        }
                    }
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 기본 풀 설정 생성
        /// </summary>
        private PoolConfig CreateDefaultConfig(string addressableKey)
        {
            return new PoolConfig
            {
                poolName = addressableKey,
                addressableKey = addressableKey,
                initialSize = 5,
                maxSize = 50,
                autoExpand = true,
                cullDelay = _defaultCullDelay,
                prewarm = false,
                category = DetermineCategory(addressableKey),
                priority = 5
            };
        }

        /// <summary>
        /// 카테고리 자동 판별
        /// </summary>
        private PoolCategory DetermineCategory(string addressableKey)
        {
            if (addressableKey.Contains("Char_") || addressableKey.Contains("Character"))
                return PoolCategory.Character;
            if (addressableKey.Contains("Effect_") || addressableKey.Contains("VFX"))
                return PoolCategory.Effect;
            if (addressableKey.Contains("UI_"))
                return PoolCategory.UI;
            if (addressableKey.Contains("Projectile") || addressableKey.Contains("Bullet"))
                return PoolCategory.Projectile;
            if (addressableKey.Contains("Item"))
                return PoolCategory.Item;

            return PoolCategory.Environment;
        }

        /// <summary>
        /// 초기 인스턴스 생성
        /// </summary>
        private async UniTask CreateInitialInstances(Pool pool, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = await CreateNewInstance(pool);
                if (instance != null)
                {
                    pool.Available.Enqueue(instance);
                }

                // 프레임 분산
                if (i % 3 == 0)
                {
                    await UniTask.Yield();
                }
            }

            Debug.Log($"[PoolingSystem] Created {count} initial instances for {pool.Name}");
        }

        /// <summary>
        /// 로드 에러 처리
        /// </summary>
        private void HandleLoadError(string address, Exception error)
        {
            Debug.LogError($"[PoolingSystem] Load error for {address}: {error.Message}");

            // 해당 풀이 로딩 중이었다면 제거
            if (_pools.ContainsKey(address) && _pools[address].IsLoading)
            {
                _pools.Remove(address);
            }
        }

        #endregion
    }
}