using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMODUnity;
using FMOD;       // FMOD 네임스페이스
using FMOD.Studio; // FMOD.Studio 네임스페이스
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Cosmos.Audio.FMOD
{
    /// <summary>
    /// FMOD Bank를 Addressable로 관리하는 매니저
    /// </summary>
    public class FMODAddressableManager : MonoBehaviour
    {
        #region Singleton

        private static FMODAddressableManager _instance;
        public static FMODAddressableManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[FMODAddressableManager]");
                    _instance = go.AddComponent<FMODAddressableManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        // Bank 관리
        private Dictionary<string, Bank> loadedBanks = new Dictionary<string, Bank>();
        private Dictionary<string, AsyncOperationHandle<TextAsset>> bankHandles = new Dictionary<string, AsyncOperationHandle<TextAsset>>();
        private Dictionary<string, int> bankReferenceCount = new Dictionary<string, int>();

        // Event Instance 풀링
        private Dictionary<string, Queue<EventInstance>> instancePool = new Dictionary<string, Queue<EventInstance>>();
        private Dictionary<string, EventDescription> eventDescriptions = new Dictionary<string, EventDescription>();
        private Dictionary<EventInstance, string> activeInstances = new Dictionary<EventInstance, string>();

        // 설정
        [SerializeField] private int maxPoolSizePerEvent = 10;
        [SerializeField] private int initialPoolSize = 3;
        [SerializeField] private bool debugMode = false;

        // Master Bank 경로
        private const string MASTER_BANK_PATH = "_Core/MasterBank/Master";
        private const string MASTER_STRINGS_BANK_PATH = "_Core/MasterBank/Master.strings";

        #endregion

        #region Initialization

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Master Bank 자동 로드
            InitializeMasterBanks().Forget();
        }

        private async UniTask InitializeMasterBanks()
        {
            try
            {
                // Master.strings.bank 먼저 로드
                await LoadBankAsync(MASTER_STRINGS_BANK_PATH);

                // Master.bank 로드
                await LoadBankAsync(MASTER_BANK_PATH);

                if (debugMode)
                    Debug.Log("[FMODAddressable] Master banks loaded successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FMODAddressable] Failed to load master banks: {e.Message}");
            }
        }

        #endregion

        #region Bank Loading

        public async UniTask<bool> LoadBankAsync(string bankKey)
        {
            // 이미 로드된 경우
            if (loadedBanks.ContainsKey(bankKey))
            {
                bankReferenceCount[bankKey]++;
                if (debugMode)
                    Debug.Log($"[FMODAddressable] Bank already loaded: {bankKey} (ref: {bankReferenceCount[bankKey]})");
                return true;
            }

            // 로딩 중인 경우 대기
            if (bankHandles.ContainsKey(bankKey))
            {
                await UniTask.WaitUntil(() => !bankHandles.ContainsKey(bankKey) || loadedBanks.ContainsKey(bankKey));
                return loadedBanks.ContainsKey(bankKey);
            }

            try
            {
                // Addressable 키 생성
                string addressableKey = GetAddressableKey(bankKey);

                // TextAsset으로 Bank 파일 로드
                var handle = Addressables.LoadAssetAsync<TextAsset>(addressableKey);
                bankHandles[bankKey] = handle;

                var bankData = await handle;

                if (bankData != null && bankData.bytes != null)
                {
                    // FMOD Bank 로드
                    Bank bank;
                    var result = RuntimeManager.StudioSystem.loadBankMemory( bankData.bytes, LOAD_BANK_FLAGS.NORMAL, out bank );

                    if (result == RESULT.OK)
                    {
                        loadedBanks[bankKey] = bank;
                        bankReferenceCount[bankKey] = 1;

                        // Event Description 캐싱
                        CacheEventDescriptions(bank);

                        if (debugMode)
                            Debug.Log($"[FMODAddressable] Bank loaded: {bankKey}");

                        return true;
                    }
                    else
                    {
                        Debug.LogError($"[FMODAddressable] FMOD failed to load bank: {bankKey} - {result}");
                        Addressables.Release(handle);
                    }
                }
                else
                {
                    Debug.LogError($"[FMODAddressable] Bank data is null: {bankKey}");
                    Addressables.Release(handle);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FMODAddressable] Failed to load bank {bankKey}: {e.Message}");
            }
            finally
            {
                bankHandles.Remove(bankKey);
            }

            return false;
        }

        private string GetAddressableKey(string bankKey)
        {
            // Master Bank 특별 처리
            if (bankKey.StartsWith("_Core/"))
            {
                return $"FMODBanks/{bankKey}";
            }

            // .bank 확장자 처리
            if (!bankKey.EndsWith(".bank"))
            {
                bankKey += ".bank";
            }

            return $"FMODBanks/{bankKey}";
        }

        private void CacheEventDescriptions(Bank bank)
        {
            bank.getEventList(out EventDescription[] events);

            if (events == null) return;

            foreach (var eventDesc in events)
            {
                eventDesc.getPath(out string path);
                if (!string.IsNullOrEmpty(path))
                {
                    eventDescriptions[path] = eventDesc;

                    // 초기 풀 생성
                    if (!instancePool.ContainsKey(path))
                    {
                        instancePool[path] = new Queue<EventInstance>();
                        PrewarmPool(path, eventDesc);
                    }
                }
            }
        }

        #endregion

        #region Bank Unloading

        public void UnloadBank(string bankKey)
        {
            if (!loadedBanks.ContainsKey(bankKey)) return;

            // 참조 카운트 감소
            bankReferenceCount[bankKey]--;

            if (bankReferenceCount[bankKey] <= 0)
            {
                // Bank 언로드
                var bank = loadedBanks[bankKey];
                bank.unload();

                // Addressable 해제
                if (bankHandles.ContainsKey(bankKey))
                {
                    Addressables.Release(bankHandles[bankKey]);
                    bankHandles.Remove(bankKey);
                }

                loadedBanks.Remove(bankKey);
                bankReferenceCount.Remove(bankKey);

                if (debugMode)
                    Debug.Log($"[FMODAddressable] Bank unloaded: {bankKey}");
            }
        }

        #endregion

        #region Event Instance Management

        public EventInstance CreateInstance(string eventPath)
        {
            // Pool에서 재사용
            if (instancePool.ContainsKey(eventPath) && instancePool[eventPath].Count > 0)
            {
                var pooledInstance = instancePool[eventPath].Dequeue();

                // 유효성 체크
                if (pooledInstance.isValid())
                {
                    activeInstances[pooledInstance] = eventPath;

                    if (debugMode)
                        Debug.Log($"[FMODAddressable] Reused instance from pool: {eventPath}");

                    return pooledInstance;
                }
            }

            // 새로 생성
            try
            {
                var instance = RuntimeManager.CreateInstance(eventPath);
                if (instance.isValid())
                {
                    activeInstances[instance] = eventPath;

                    if (debugMode)
                        Debug.Log($"[FMODAddressable] Created new instance: {eventPath}");

                    return instance;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FMODAddressable] Failed to create instance for {eventPath}: {e.Message}");
            }

            return new EventInstance();
        }

        public void ReturnToPool(EventInstance instance)
        {
            if (!instance.isValid()) return;

            if (activeInstances.TryGetValue(instance, out string eventPath))
            {
                // 상태 초기화
                instance.stop( STOP_MODE.IMMEDIATE  );
                instance.setTimelinePosition(0);

                // 모든 파라미터 리셋
                ResetInstanceParameters(instance);

                // 풀에 반환
                if (!instancePool.ContainsKey(eventPath))
                {
                    instancePool[eventPath] = new Queue<EventInstance>();
                }

                if (instancePool[eventPath].Count < maxPoolSizePerEvent)
                {
                    instancePool[eventPath].Enqueue(instance);

                    if (debugMode)
                        Debug.Log($"[FMODAddressable] Returned to pool: {eventPath}");
                }
                else
                {
                    instance.release();

                    if (debugMode)
                        Debug.Log($"[FMODAddressable] Released instance (pool full): {eventPath}");
                }

                activeInstances.Remove(instance);
            }
            else
            {
                instance.release();
            }
        }

        private void ResetInstanceParameters(EventInstance instance)
        {
            // 모든 파라미터를 기본값으로 리셋
            instance.getDescription(out EventDescription desc);
            if (!desc.isValid()) return;

            desc.getParameterDescriptionCount(out int count);
            for (int i = 0; i < count; i++)
            {
                desc.getParameterDescriptionByIndex(i, out PARAMETER_DESCRIPTION param);
                instance.setParameterByID(param.id, param.defaultvalue);
            }
        }

        private void PrewarmPool(string eventPath, EventDescription desc)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                desc.createInstance(out EventInstance instance);
                if (instance.isValid())
                {
                    instancePool[eventPath].Enqueue(instance);
                }
            }
        }

        #endregion

        #region Cleanup

        public void CleanupPools()
        {
            foreach (var kvp in instancePool)
            {
                while (kvp.Value.Count > initialPoolSize)
                {
                    var instance = kvp.Value.Dequeue();
                    instance.release();
                }
            }
        }

        public void ReleaseAll()
        {
            // 활성 인스턴스 정리
            foreach (var instance in activeInstances.Keys)
            {
                instance.stop(STOP_MODE.IMMEDIATE);
                instance.release();
            }
            activeInstances.Clear();

            // 풀 정리
            foreach (var pool in instancePool.Values)
            {
                while (pool.Count > 0)
                {
                    var instance = pool.Dequeue();
                    instance.release();
                }
            }
            instancePool.Clear();

            // Bank 언로드
            foreach (var bank in loadedBanks.Values)
            {
                bank.unload();
            }
            loadedBanks.Clear();

            // Addressable 핸들 해제
            foreach (var handle in bankHandles.Values)
            {
                Addressables.Release(handle);
            }
            bankHandles.Clear();
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        #endregion

        #region Helper Methods

        public bool IsBankLoaded(string bankKey)
        {
            return loadedBanks.ContainsKey(bankKey);
        }

        public List<string> GetLoadedBanks()
        {
            return loadedBanks.Keys.ToList();
        }

        public int GetPooledInstanceCount(string eventPath)
        {
            return instancePool.ContainsKey(eventPath) ? instancePool[eventPath].Count : 0;
        }

        #endregion
    }
}