using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;



#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
#endif
namespace GameCore.Addressables
{
    /// <summary>
    /// Addressable 시스템 중앙 관리자
    /// 모든 리소스 로드/해제를 관리
    /// </summary>
    public class AddressableManager : MonoBehaviour
    {
        #region Singleton
        
        private static AddressableManager _instance;
        public static AddressableManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[AddressableManager]");
                    _instance = go.AddComponent<AddressableManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        #endregion

        #region Fields & Properties
        
        // 핸들 관리
        private readonly Dictionary<string, AsyncOperationHandle> _loadedHandles = new();
        private readonly Dictionary<string, int> _referenceCount = new();
        private readonly Dictionary<string, List<string>> _groupDependencies = new();
        
        // 캐릭터 캐시
        private readonly Dictionary<string, GameObject> _characterCache = new();
        private readonly Queue<string> _characterCacheOrder = new();
        private const int MAX_CACHED_CHARACTERS = 15;
        
        // 다운로드 관리
        private readonly Queue<DownloadTask> _downloadQueue = new();
        private readonly List<DownloadTask> _activeDownloads = new();
        private const int MAX_CONCURRENT_DOWNLOADS = 3;
        
        // 상태
        public bool IsInitialized { get; private set; }

        public bool IsDownloading => _activeDownloads.Count > 0;
        
        // 이벤트
        public event Action<string, float> OnDownloadProgress;
        public event Action<string> OnDownloadComplete;
        public event Action<string, Exception> OnLoadError;
        public event Action<MemoryStatus> OnMemoryWarning;
        
        #endregion

        #region Initialization
        
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
        
        private async Task Initialize()
        {
            try
            {
                Debug.Log("[AddressableManager] Initializing...");
                
                // Addressable 초기화
                var handle = UnityEngine.AddressableAssets.Addressables.InitializeAsync();
                await handle.Task;
                
                
                IsInitialized = true;

                // 카탈로그 업데이트는 백그라운드에서
                _ = CheckForCatalogUpdatesBackground();

                Debug.Log("[AddressableManager] Initialization complete");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Initialization failed: {e.Message}");
                IsInitialized = false;
            }
        }

        #endregion

        #region Catalog Management

        private async Task CheckForCatalogUpdatesBackground()
        {
            // 백그라운드에서 카탈로그 체크
            await Task.Delay(1000); // 1초 후 체크
            await CheckForCatalogUpdates();
        }

        /// <summary>
        /// 카탈로그 업데이트 체크 및 적용
        /// </summary>
        public async Task<bool> CheckForCatalogUpdates()
        {
            try
            {
                var checkHandle = UnityEngine.AddressableAssets.Addressables.CheckForCatalogUpdates();
                var catalogs = await checkHandle.Task;
                
                if (catalogs != null && catalogs.Count > 0)
                {
                    Debug.Log($"[AddressableManager] Found {catalogs.Count} catalog updates");
                    
                    var updateHandle = UnityEngine.AddressableAssets.Addressables.UpdateCatalogs(catalogs);
                    var updatedCatalogs = await updateHandle.Task;
                    
                    foreach (var catalog in updatedCatalogs)
                    {
                        Debug.Log($"[AddressableManager] Updated catalog: {catalog.LocatorId}");
                    }

                    UnityEngine.AddressableAssets.Addressables.Release(updateHandle);
                    UnityEngine.AddressableAssets.Addressables.Release(checkHandle);
                    return true;
                }

                UnityEngine.AddressableAssets.Addressables.Release(checkHandle);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Catalog update failed: {e.Message}");
                return false;
            }
        }
        
        #endregion

        #region Basic Loading
        
        /// <summary>
        /// 단일 에셋 로드 (참조 카운팅)
        /// </summary>
        public async Task<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            if (!IsInitialized)
            {
                Debug.LogError("[AddressableManager] Not initialized");
                return null;
            }
            
            try
            {
                // 이미 로드된 경우
                if (_loadedHandles.ContainsKey(address))
                {
                    _referenceCount[address]++;
                    Debug.Log($"[AddressableManager] Asset already loaded: {address} (RefCount: {_referenceCount[address]})");
                    
                    var existingHandle = _loadedHandles[address];
                    if (existingHandle.Result is T typedResult)
                    {
                        return typedResult;
                    }
                    
                    Debug.LogError($"[AddressableManager] Type mismatch for {address}");
                    return null;
                }

                // 새로 로드
                Debug.Log($"[AddressableManager] Loading asset: {address}");
                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(address);
                
                _loadedHandles[address] = handle;
                _referenceCount[address] = 1;
                
                var result = await handle.Task;
                
                if (result == null)
                {
                    throw new Exception($"Asset is null: {address}");
                }
                
                // 메모리 체크
              //  CheckMemoryStatus();
                
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Load failed - {address}: {e.Message}");
                OnLoadError?.Invoke(address, e);
                
                // 실패시 정리
                if (_loadedHandles.ContainsKey(address))
                {
                    _loadedHandles.Remove(address);
                    _referenceCount.Remove(address);
                }
                
                return null;
            }
        }
        
        /// <summary>
        /// Label로 여러 에셋 로드
        /// </summary>
        public async Task<IList<T>> LoadAssetsAsync<T>(string label, Action<T> callback = null) where T : UnityEngine.Object
        {
            if (!IsInitialized)
            {
                Debug.LogError("[AddressableManager] Not initialized");
                return new List<T>();
            }
            
            try
            {
                Debug.Log($"[AddressableManager] Loading assets with label: {label}");
                
                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetsAsync<T>(
                    label,
                    callback,
                    UnityEngine.AddressableAssets.Addressables.MergeMode.Union
                );
                
                var results = await handle.Task;
                
                // 개별 핸들로 관리
                string handleKey = $"Label_{label}_{typeof(T).Name}";
                _loadedHandles[handleKey] = handle;
                _referenceCount[handleKey] = 1;
                
                Debug.Log($"[AddressableManager] Loaded {results.Count} assets with label: {label}");
                
               // CheckMemoryStatus();
                
                return results;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Load failed - Label {label}: {e.Message}");
                OnLoadError?.Invoke(label, e);
                return new List<T>();
            }
        }


        public async Task<T[]> LoadAssetsAsyncArray<T>(string label, Action<T> callback = null) where T : UnityEngine.Object
        {
            if (!IsInitialized)
            {
                Debug.LogError("[AddressableManager] Not initialized");
                return new T[0];
            }

            try
            {
                Debug.Log($"[AddressableManager] Loading assets with label: {label}");

                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetsAsync<T>(
                    label,
                    callback,
                    UnityEngine.AddressableAssets.Addressables.MergeMode.Union
                );

                var results = await handle.Task;

                // 개별 핸들로 관리
                string handleKey = $"Label_{label}_{typeof(T).Name}";
                _loadedHandles[handleKey] = handle;
                _referenceCount[handleKey] = 1;

                Debug.Log($"[AddressableManager] Loaded {results.Count} assets with label: {label}");

               // CheckMemoryStatus();

                // IList<T>를 배열로 변환
                return results.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Load failed - Label {label}: {e.Message}");
                OnLoadError?.Invoke(label, e);
                return new T[0];
            }
        }

        /// <summary>
        /// 에셋 해제
        /// </summary>
        public void ReleaseAsset(string address)
        {
            if (!_loadedHandles.ContainsKey(address))
            {
                Debug.LogWarning($"[AddressableManager] Asset not loaded: {address}");
                return;
            }

            _referenceCount[address]--;
            Debug.Log($"[AddressableManager] Release asset: {address} (RefCount: {_referenceCount[address]})");

            if (_referenceCount[address] <= 0)
            {
                var handle = _loadedHandles[address];
                if (handle.IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(handle);
                }
                
                _loadedHandles.Remove(address);
                _referenceCount.Remove(address);
                
                // 캐릭터 캐시에서도 제거
                _characterCache.Remove(address);
                
                Debug.Log($"[AddressableManager] Asset fully released: {address}");
            }
        }
        
        #endregion

        #region Character Management
        
        /// <summary>
        /// 캐릭터 로드 (Battle/Town 구분)
        /// </summary>
        public async Task<GameObject> LoadCharacterAsync(string addressKey)
        {
            // 캐시 확인
            if (_characterCache.ContainsKey(addressKey))
            {
                Debug.Log($"[AddressableManager] Character from cache: {addressKey}"); 
                return _characterCache[addressKey];
            }

            // 캐시 크기 관리
            ManageCharacterCache();

            // 로드
            var character = await LoadAssetAsync<GameObject>(addressKey);
            if (character != null)
            {
                _characterCache[addressKey] = character;
                _characterCacheOrder.Enqueue(addressKey);
            }
            
            return character;
        }
        
        /// <summary>
        /// 팀 전체 프리로드
        /// </summary>
        public async Task<List<GameObject>> PreloadTeamCharacters(List<string> characterAddressKeys)
        {
            var results = new List<GameObject>();
            var tasks = new List<Task<GameObject>>();
            
            foreach (var key in characterAddressKeys)
            {
                tasks.Add(LoadCharacterAsync(key));
            }
            
            var characters = await Task.WhenAll(tasks);
            results.AddRange(characters.Where(c => c != null));
            
            Debug.Log($"[AddressableManager] Team preload complete: {results.Count}/{characterAddressKeys.Count} characters");
            
            return results;
        }
        
        /// <summary>
        /// 캐릭터 인스턴스 생성
        /// </summary>
        public GameObject InstantiateCharacter(string addressKey, Vector3 position, Transform parent = null, bool isBattle = true)
        {
            if (!_characterCache.ContainsKey(addressKey))
            {
                Debug.LogError($"[AddressableManager] Character not loaded: {addressKey}");
                return null;
            }
            
            var prefab = _characterCache[addressKey];
            return Instantiate(prefab, position, Quaternion.identity, parent);
        }
        
        private void ManageCharacterCache()
        {
            while (_characterCache.Count >= MAX_CACHED_CHARACTERS && _characterCacheOrder.Count > 0)
            {
                var oldestKey = _characterCacheOrder.Dequeue();
                
                // 참조 카운트 확인
                if (_referenceCount.ContainsKey(oldestKey) && _referenceCount[oldestKey] <= 1)
                {
                    ReleaseAsset(oldestKey);
                    Debug.Log($"[AddressableManager] Removed oldest character from cache: {oldestKey}");
                }
            }
        }
        
        #endregion

        #region Download Management
        
        /// <summary>
        /// 다운로드 크기 확인
        /// </summary>
        public async Task<long> GetDownloadSizeAsync(object key)
        {
            try
            {
                var handle = UnityEngine.AddressableAssets.Addressables.GetDownloadSizeAsync(key);
                var size = await handle.Task;
                UnityEngine.AddressableAssets.Addressables.Release(handle);
                
                Debug.Log($"[AddressableManager] Download size for {key}: {size / 1024f / 1024f:F2} MB");
                return size;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Failed to get download size: {e.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 다운로드 태스크 추가
        /// </summary>
        public void QueueDownload(string key, int priority = 0, Action<bool> onComplete = null)
        {
            var task = new DownloadTask
            {
                Key = key,
                Priority = priority,
                OnComplete = onComplete
            };
            
            _downloadQueue.Enqueue(task);
            
            // 우선순위 정렬
            var sorted = _downloadQueue.OrderByDescending(t => t.Priority).ToList();
            _downloadQueue.Clear();
            foreach (var t in sorted)
            {
                _downloadQueue.Enqueue(t);
            }
            
            // 다운로드 시작
            _ = ProcessDownloadQueue();
        }
        
        /// <summary>
        /// 즉시 다운로드
        /// </summary>
        public async Task<bool> DownloadDependenciesAsync(string key, Action<float> onProgress = null)
        {
            try
            {
                Debug.Log($"[AddressableManager] Downloading dependencies: {key}");
                
                var handle = UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync(key);
                
                while (!handle.IsDone)
                {
                    float progress = handle.PercentComplete;
                    onProgress?.Invoke(progress);
                    OnDownloadProgress?.Invoke(key, progress);
                    await Task.Yield();
                }
                
                bool success = handle.Status == AsyncOperationStatus.Succeeded;
                
                if (success)
                {
                    Debug.Log($"[AddressableManager] Download complete: {key}");
                    OnDownloadComplete?.Invoke(key);
                }
                else
                {
                    Debug.LogError($"[AddressableManager] Download failed: {key}");
                }

                UnityEngine.AddressableAssets.Addressables.Release(handle);
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Download error: {e.Message}");
                return false;
            }
        }
        
        private async Task ProcessDownloadQueue()
        {
            while (_downloadQueue.Count > 0 || _activeDownloads.Count > 0)
            {
                // 새 다운로드 시작
                while (_downloadQueue.Count > 0 && _activeDownloads.Count < MAX_CONCURRENT_DOWNLOADS)
                {
                    var task = _downloadQueue.Dequeue();
                    _activeDownloads.Add(task);
                    
                    _ = ProcessDownloadTask(task);
                }
                
                await Task.Delay(100);
            }
        }
        
        private async Task ProcessDownloadTask(DownloadTask task)
        {
            bool success = await DownloadDependenciesAsync(task.Key);
            
            task.OnComplete?.Invoke(success);
            _activeDownloads.Remove(task);
        }
        
        #endregion

        #region Scene Management
        
        /// <summary>
        /// 씬 로드 (Addressable)
        /// </summary>
        public async Task<bool> LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            try
            {
                Debug.Log($"[AddressableManager] Loading scene: {sceneName}");
                
                var handle = UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(sceneName, mode);
                var scene = await handle.Task;
                
                string handleKey = $"Scene_{sceneName}";
                _loadedHandles[handleKey] = handle;
                _referenceCount[handleKey] = 1;
                
                Debug.Log($"[AddressableManager] Scene loaded: {scene.Scene.name}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableManager] Scene load failed: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// EventCut 씬 로드 (EP별 자동 다운로드)
        /// </summary>
        public async Task<bool> LoadEventCutScene(string sceneName, int episode)
        {
            string epGroup = $"EventCut_EP{episode}_Group";
            
            // EP 다운로드 체크
            var size = await GetDownloadSizeAsync(epGroup);
            if (size > 0)
            {
                Debug.Log($"[AddressableManager] EP{episode} needs download: {size / 1024f / 1024f:F2} MB");
                
                bool success = await DownloadDependenciesAsync(epGroup);
                if (!success) return false;
            }
            
            // 씬 로드
            return await LoadSceneAsync(sceneName);
        }
        
        #endregion

        #region Memory Management
        
        /// <summary>
        /// 메모리 상태 확인
        /// </summary>
        public MemoryStatus GetMemoryStatus()
        {
            var status = new MemoryStatus
            {
                LoadedAssets = _loadedHandles.Count,
                CachedCharacters = _characterCache.Count,
                TotalReferences = _referenceCount.Values.Sum(),
                EstimatedMemoryMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024,
                ReservedMemoryMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024
            };
            
            return status;
        }
        

        //이놈 시간 엄청 잡아먹는다. gaegu.
        private void CheckMemoryStatus()
        {
            var status = GetMemoryStatus();
            
            // 메모리 경고 임계치 (MB)
            const long WARNING_THRESHOLD = 500;
            const long CRITICAL_THRESHOLD = 700;
            
            if (status.EstimatedMemoryMB > CRITICAL_THRESHOLD)
            {
                Debug.LogWarning($"[AddressableManager] Critical memory usage: {status.EstimatedMemoryMB} MB");
                OnMemoryWarning?.Invoke(status);
                ForceCleanup();
            }
            else if (status.EstimatedMemoryMB > WARNING_THRESHOLD)
            {
                Debug.LogWarning($"[AddressableManager] High memory usage: {status.EstimatedMemoryMB} MB");
                OnMemoryWarning?.Invoke(status);
            }
        }
        
        /// <summary>
        /// 씬 전환시 정리
        /// </summary>
        public void CleanupForSceneChange(SceneType fromScene, SceneType toScene)
        {
            Debug.Log($"[AddressableManager] Scene cleanup: {fromScene} -> {toScene}");
            
            switch (fromScene)
            {
                case SceneType.Battle:
                    CleanupBattleResources();
                    break;
                case SceneType.Town:
                    CleanupTownResources();
                    break;
                case SceneType.EventCut:
                    CleanupEventCutResources();
                    break;
            }
            
            // 메모리 정리
            if (_loadedHandles.Count > 30)
            {
                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
        }
        
        private void CleanupBattleResources()
        {
            var keysToRelease = _loadedHandles.Keys
                .Where(k => k.Contains("Battle") && !k.Contains("Common"))
                .ToList();
            
            foreach (var key in keysToRelease)
            {
                ReleaseAsset(key);
            }
            
            Debug.Log($"[AddressableManager] Released {keysToRelease.Count} battle resources");
        }
        
        private void CleanupTownResources()
        {
            var keysToRelease = _loadedHandles.Keys
                .Where(k => k.Contains("Town"))
                .ToList();
            
            foreach (var key in keysToRelease)
            {
                ReleaseAsset(key);
            }
            
            Debug.Log($"[AddressableManager] Released {keysToRelease.Count} town resources");
        }
        
        private void CleanupEventCutResources()
        {
            var keysToRelease = _loadedHandles.Keys
                .Where(k => k.Contains("EventCut") || k.Contains("EP"))
                .ToList();
            
            foreach (var key in keysToRelease)
            {
                ReleaseAsset(key);
            }
            
            Debug.Log($"[AddressableManager] Released {keysToRelease.Count} eventcut resources");
        }
        
        /// <summary>
        /// 강제 메모리 정리
        /// </summary>
        public void ForceCleanup()
        {
            Debug.Log("[AddressableManager] Force cleanup started");
            
            // 참조 카운트 1인 리소스 해제
            var keysToRelease = _referenceCount
                .Where(kvp => kvp.Value <= 1)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in keysToRelease)
            {
                ReleaseAsset(key);
            }
            
            // 캐릭터 캐시 절반 정리
            int toRemove = _characterCache.Count / 2;
            for (int i = 0; i < toRemove && _characterCacheOrder.Count > 0; i++)
            {
                var key = _characterCacheOrder.Dequeue();
                _characterCache.Remove(key);
                ReleaseAsset(key);
            }
            
            // GC 강제 실행
            Resources.UnloadUnusedAssets();
            GC.Collect();
            
            Debug.Log($"[AddressableManager] Force cleanup complete. Released {keysToRelease.Count} assets");
        }
        
        /// <summary>
        /// 전체 리소스 해제
        /// </summary>
        public void ReleaseAll()
        {
            Debug.Log("[AddressableManager] Releasing all resources");
            
            foreach (var handle in _loadedHandles.Values)
            {
                if (handle.IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(handle);
                }
            }
            
            _loadedHandles.Clear();
            _referenceCount.Clear();
            _characterCache.Clear();
            _characterCacheOrder.Clear();
            
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
        
        #endregion

        #region Helper Classes
        
        public class MemoryStatus
        {
            public int LoadedAssets { get; set; }
            public int CachedCharacters { get; set; }
            public int TotalReferences { get; set; }
            public long EstimatedMemoryMB { get; set; }
            public long ReservedMemoryMB { get; set; }
            
            public override string ToString()
            {
                return $"Memory Status:\n" +
                       $"  Loaded Assets: {LoadedAssets}\n" +
                       $"  Cached Characters: {CachedCharacters}\n" +
                       $"  Total References: {TotalReferences}\n" +
                       $"  Used Memory: {EstimatedMemoryMB} MB\n" +
                       $"  Reserved Memory: {ReservedMemoryMB} MB";
            }
        }
        
        public enum SceneType
        {
            Core,
            Prologue,
            Town,
            Battle,
            EventCut
        }
        
        private class DownloadTask
        {
            public string Key { get; set; }
            public int Priority { get; set; }
            public Action<bool> OnComplete { get; set; }
        }
        
        #endregion

        #region Unity Callbacks
        
        private void OnDestroy()
        {
            ReleaseAll();
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // 일시정지시 메모리 정리
               // CheckMemoryStatus();
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                // 포커스 잃을 때 메모리 체크
                var status = GetMemoryStatus();
                if (status.EstimatedMemoryMB > 600)
                {
                    ForceCleanup();
                }
            }
        }
        
        #endregion
    }
}