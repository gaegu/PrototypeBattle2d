using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Cosmos.Timeline.Playback
{
    /// <summary>
    /// Addressable 기반 리소스 관리 구현
    /// </summary>
    public class AddressableResourceProvider : MonoBehaviour, IResourceProvider
    {
        #region Fields

        // 로드된 리소스 캐시
        private Dictionary<string, UnityEngine.Object> loadedResources = new Dictionary<string, UnityEngine.Object>();
        private Dictionary<string, AsyncOperationHandle> loadingHandles = new Dictionary<string, AsyncOperationHandle>();

        // 인스턴스 풀
        private Dictionary<string, Queue<GameObject>> objectPool = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, int> poolSizes = new Dictionary<string, int>();

        // 활성 인스턴스 추적
        private Dictionary<GameObject, string> activeInstances = new Dictionary<GameObject, string>();

        // 설정
        [SerializeField] private bool usePooling = true;
        [SerializeField] private int defaultPoolSize = 5;
        [SerializeField] private int maxPoolSize = 20;
        [SerializeField] private bool autoCleanup = true;
        [SerializeField] private float cleanupInterval = 60f; // 60초마다 정리

        [SerializeField] private bool debugMode = false;

        // 정리 타이머
        private float lastCleanupTime;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (autoCleanup && Time.time - lastCleanupTime > cleanupInterval)
            {
                CleanupUnusedResources();
                lastCleanupTime = Time.time;
            }
        }

        private void OnDestroy()
        {
            ReleaseAllResources();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            lastCleanupTime = Time.time;
        }

        #endregion

        #region Resource Loading

        public void LoadResourceAsync<T>(string key, Action<T> onLoaded) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[ResourceProvider] Empty key provided");
                onLoaded?.Invoke(null);
                return;
            }

            // 이미 로드된 경우
            if (loadedResources.TryGetValue(key, out var cached))
            {
                if (cached is T typedResource)
                {
                    onLoaded?.Invoke(typedResource);
                    return;
                }
            }

            // 로딩 중인 경우
            if (loadingHandles.ContainsKey(key))
            {
                StartCoroutine(WaitForLoadingAndInvoke(key, onLoaded));
                return;
            }

            // 새로 로드
            LoadFromAddressable<T>(key, onLoaded);
        }

        private void LoadFromAddressable<T>(string key, Action<T> onLoaded) where T : UnityEngine.Object
        {
            if (debugMode)
                Debug.Log($"[ResourceProvider] Loading: {key}");

            var handle = Addressables.LoadAssetAsync<T>(key);
            loadingHandles[key] = handle;

            handle.Completed += (AsyncOperationHandle<T> obj) =>
            {
                loadingHandles.Remove(key);

                if (obj.Status == AsyncOperationStatus.Succeeded)
                {
                    T resource = obj.Result;
                    loadedResources[key] = resource;

                    // GameObject인 경우 풀 초기화
                    if (resource is GameObject prefab && usePooling)
                    {
                        InitializePool(key, prefab);
                    }

                    onLoaded?.Invoke(resource);

                    if (debugMode)
                        Debug.Log($"[ResourceProvider] Loaded successfully: {key}");
                }
                else
                {
                    Debug.LogError($"[ResourceProvider] Failed to load: {key}");
                    onLoaded?.Invoke(null);
                }
            };
        }

        private System.Collections.IEnumerator WaitForLoadingAndInvoke<T>(string key, Action<T> onLoaded)
            where T : UnityEngine.Object
        {
            while (loadingHandles.ContainsKey(key))
            {
                yield return null;
            }

            if (loadedResources.TryGetValue(key, out var resource))
            {
                onLoaded?.Invoke(resource as T);
            }
            else
            {
                onLoaded?.Invoke(null);
            }
        }

        public void ReleaseResource(string key)
        {
            if (!loadedResources.ContainsKey(key)) return;

            // 풀 정리
            if (objectPool.ContainsKey(key))
            {
                var pool = objectPool[key];
                while (pool.Count > 0)
                {
                    var obj = pool.Dequeue();
                    if (obj != null)
                        Destroy(obj);
                }
                objectPool.Remove(key);
            }

            // 리소스 해제
            if (loadedResources[key] != null)
            {
                Addressables.Release(loadedResources[key]);
            }

            loadedResources.Remove(key);

            if (debugMode)
                Debug.Log($"[ResourceProvider] Released: {key}");
        }

        public bool IsResourceLoaded(string key)
        {
            return loadedResources.ContainsKey(key);
        }

        #endregion

        #region Preloading

        public void PreloadResources(IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                if (!IsResourceLoaded(key))
                {
                    LoadResourceAsync<UnityEngine.Object>(key, _ => { });
                }
            }

            if (debugMode)
                Debug.Log($"[ResourceProvider] Preloading {keys.Count()} resources");
        }

        public void ClearPreloadedResources()
        {
            var keysToRelease = new List<string>(loadedResources.Keys);

            foreach (var key in keysToRelease)
            {
                ReleaseResource(key);
            }

            if (debugMode)
                Debug.Log($"[ResourceProvider] Cleared all preloaded resources");
        }

        #endregion

        #region Object Pooling

        private void InitializePool(string key, GameObject prefab)
        {
            if (objectPool.ContainsKey(key)) return;

            objectPool[key] = new Queue<GameObject>();
            poolSizes[key] = 0;

            // 초기 풀 생성
            for (int i = 0; i < defaultPoolSize; i++)
            {
                CreatePooledObject(key, prefab);
            }

            if (debugMode)
                Debug.Log($"[ResourceProvider] Pool initialized for {key} with {defaultPoolSize} objects");
        }

        private GameObject CreatePooledObject(string key, GameObject prefab)
        {
            var obj = Instantiate(prefab);
            obj.name = $"{prefab.name}_Pooled_{poolSizes[key]}";
            obj.SetActive(false);

            // 풀 오브젝트는 이 컴포넌트의 자식으로
            obj.transform.SetParent(transform);

            objectPool[key].Enqueue(obj);
            poolSizes[key]++;

            return obj;
        }

        #endregion

        #region Instance Management

        public GameObject InstantiateEffect(string key, Vector3 position, Quaternion rotation)
        {
            GameObject instance = null;

            // 풀에서 가져오기 시도
            if (usePooling && objectPool.ContainsKey(key) && objectPool[key].Count > 0)
            {
                instance = GetFromPool(key);
            }
            else if (loadedResources.TryGetValue(key, out var resource))
            {
                // 풀이 비어있으면 새로 생성
                if (resource is GameObject prefab)
                {
                    instance = Instantiate(prefab);
                    instance.name = $"{prefab.name}_Instance";
                }
            }

            if (instance != null)
            {
                // 위치 및 회전 설정
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.SetActive(true);

                // 활성 인스턴스 추적
                activeInstances[instance] = key;

                // ParticleSystem 재시작
                var particleSystems = instance.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    ps.Clear();
                    ps.Play();
                }

                if (debugMode)
                    Debug.Log($"[ResourceProvider] Instantiated effect: {key}");
            }
            else
            {
                Debug.LogWarning($"[ResourceProvider] Failed to instantiate: {key} (not loaded)");
            }

            return instance;
        }

        private GameObject GetFromPool(string key)
        {
            if (!objectPool.ContainsKey(key) || objectPool[key].Count == 0)
                return null;

            GameObject obj = null;

            // 유효한 오브젝트 찾기
            while (objectPool[key].Count > 0)
            {
                obj = objectPool[key].Dequeue();
                if (obj != null)
                {
                    break;
                }
            }

            // 풀이 비어있고 최대 크기에 도달하지 않았으면 새로 생성
            if (obj == null && poolSizes[key] < maxPoolSize)
            {
                if (loadedResources.TryGetValue(key, out var resource) && resource is GameObject prefab)
                {
                    obj = CreatePooledObject(key, prefab);
                    objectPool[key].Dequeue(); // 방금 추가한 것 제거
                }
            }

            return obj;
        }

        public void DestroyEffect(GameObject effect)
        {
            if (effect == null) return;

            // 풀링 사용 중이고 추적 중인 인스턴스인 경우
            if (usePooling && activeInstances.TryGetValue(effect, out string key))
            {
                ReturnToPool(effect, key);
            }
            else
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(effect);
                else
#endif
                    Destroy(effect);
            }
        }

        private void ReturnToPool(GameObject obj, string key)
        {
            if (!objectPool.ContainsKey(key))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(obj);
                else
#endif
                    Destroy(obj);
                return;
            }

            // 오브젝트 초기화
            obj.SetActive(false);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            // ParticleSystem 정지
            var particleSystems = obj.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // 풀에 반환
            if (objectPool[key].Count < maxPoolSize)
            {
                objectPool[key].Enqueue(obj);
            }
            else
            {
                Destroy(obj);
                poolSizes[key]--;
            }

            // 추적 제거
            activeInstances.Remove(obj);

            if (debugMode)
                Debug.Log($"[ResourceProvider] Returned to pool: {key}");
        }

        #endregion

        #region Cleanup

        private void CleanupUnusedResources()
        {
            var keysToRemove = new List<string>();

            foreach (var kvp in objectPool)
            {
                // 활성 인스턴스가 없고 풀에만 있는 경우
                bool hasActiveInstances = activeInstances.Values.Contains(kvp.Key);

                if (!hasActiveInstances && kvp.Value.Count >= defaultPoolSize)
                {
                    // 초과분 제거
                    while (kvp.Value.Count > defaultPoolSize)
                    {
                        var obj = kvp.Value.Dequeue();
                        if (obj != null)
                        {
                            Destroy(obj);
                            poolSizes[kvp.Key]--;
                        }
                    }
                }

                // 완전히 사용되지 않는 리소스 표시
                if (!hasActiveInstances && kvp.Value.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // 표시된 리소스 해제
            foreach (var key in keysToRemove)
            {
                ReleaseResource(key);
            }

            if (debugMode && keysToRemove.Count > 0)
                Debug.Log($"[ResourceProvider] Cleaned up {keysToRemove.Count} unused resources");
        }

        private void ReleaseAllResources()
        {
            // 모든 활성 인스턴스 제거
            foreach (var instance in activeInstances.Keys.ToList())
            {
                if (instance != null)
                    Destroy(instance);
            }
            activeInstances.Clear();

            // 모든 풀 오브젝트 제거
            foreach (var pool in objectPool.Values)
            {
                while (pool.Count > 0)
                {
                    var obj = pool.Dequeue();
                    if (obj != null)
                        Destroy(obj);
                }
            }
            objectPool.Clear();
            poolSizes.Clear();

            // 로딩 핸들 취소
            foreach (var handle in loadingHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            loadingHandles.Clear();

            // 로드된 리소스 해제
            foreach (var resource in loadedResources.Values)
            {
                if (resource != null)
                {
                    Addressables.Release(resource);
                }
            }
            loadedResources.Clear();

            if (debugMode)
                Debug.Log("[ResourceProvider] All resources released");
        }

        #endregion

        #region Editor Support

#if UNITY_EDITOR
        /// <summary>
        /// Editor용 동기 로드 (Preview에서 사용)
        /// </summary>
        public T LoadResourceSync<T>(string key) where T : UnityEngine.Object
        {
            if (loadedResources.TryGetValue(key, out var cached))
            {
                return cached as T;
            }

            // Editor에서는 AssetDatabase 사용 가능
            string[] searchPaths = {
                $"Assets/Cosmos/ResourcesAddressable/{key}.prefab",
                $"Assets/Cosmos/ResourcesAddressable/{key}.asset",
                $"Assets/Resources/{key}.prefab",
                $"Assets/{key}.prefab"
            };

            foreach (var path in searchPaths)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    loadedResources[key] = asset;
                    return asset;
                }
            }

            // GUID로 검색
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{key} t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    loadedResources[key] = asset;
                    return asset;
                }
            }

            Debug.LogWarning($"[ResourceProvider] Failed to load resource in editor: {key}");
            return null;
        }
#endif

        #endregion

        #region Statistics

        /// <summary>
        /// 현재 리소스 사용 통계
        /// </summary>
        public ResourceStatistics GetStatistics()
        {
            return new ResourceStatistics
            {
                LoadedResourceCount = loadedResources.Count,
                ActiveInstanceCount = activeInstances.Count,
                PooledObjectCount = objectPool.Sum(kvp => kvp.Value.Count),
                TotalPoolSize = poolSizes.Sum(kvp => kvp.Value),
                PoolKeys = objectPool.Keys.ToList()
            };
        }

        [Serializable]
        public class ResourceStatistics
        {
            public int LoadedResourceCount;
            public int ActiveInstanceCount;
            public int PooledObjectCount;
            public int TotalPoolSize;
            public List<string> PoolKeys;
        }

        #endregion
    }
}