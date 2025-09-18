using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace GameCore.Seasonal
{
    /// <summary>
    /// 월별/시즌별 콘텐츠 관리
    /// </summary>
    public class SeasonalContentManager : MonoBehaviour
    {
        private static SeasonalContentManager _instance;
        public static SeasonalContentManager Instance => _instance;

        [System.Serializable]
        public class SeasonalContent
        {
            public string seasonId;              // "2026_03"
            public string displayName;           // "2026년 3월"
            public string catalogUrl;            // CDN URL
            public string groupName;             // "Seasonal_2026_03"
            public List<string> newCharacters;   // 신규 캐릭터 ID 리스트
            public List<string> newEventCuts;    // 신규 EventCut 리스트
            public string titleAddress;          // 월별 타이틀 주소
            public DateTime startDate;
            public DateTime endDate;
            public bool isDownloaded;
            public bool isActive;
        }

        [Header("Configuration")]
        [SerializeField] private string seasonalConfigUrl = "https://api.game.com/seasonal/config.json";
        [SerializeField] private bool autoCheckOnStart = true;
        [SerializeField] private bool autoDownloadNewSeason = false;

        [Header("Current Season")]
        [SerializeField] private SeasonalContent currentSeason;
        [SerializeField] private List<SeasonalContent> availableSeasons = new();

        private GameCore.Addressables.AddressableManager _addressableManager;

        public event Action<SeasonalContent> OnNewSeasonAvailable;
        public event Action<SeasonalContent> OnSeasonDownloaded;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            _addressableManager = GameCore.Addressables.AddressableManager.Instance;

            if (autoCheckOnStart)
            {
                await CheckForNewSeason();
            }
        }

        /// <summary>
        /// 새 시즌 체크
        /// </summary>
        public async Task<SeasonalContent> CheckForNewSeason()
        {
            try
            {
                // 서버에서 시즌 정보 가져오기
                var seasons = await FetchSeasonalConfig();

                // 현재 날짜에 맞는 시즌 찾기
                var now = DateTime.Now;
                foreach (var season in seasons)
                {
                    if (now >= season.startDate && now <= season.endDate)
                    {
                        if (currentSeason == null || currentSeason.seasonId != season.seasonId)
                        {
                            currentSeason = season;
                            OnNewSeasonAvailable?.Invoke(season);

                            // 자동 다운로드
                            if (autoDownloadNewSeason && !season.isDownloaded)
                            {
                                await DownloadSeasonalContent(season);
                            }

                            return season;
                        }
                    }
                }

                return currentSeason;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SeasonalContentManager] Failed to check new season: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 서버에서 시즌 설정 가져오기
        /// </summary>
        private async Task<List<SeasonalContent>> FetchSeasonalConfig()
        {
            using (var request = UnityWebRequest.Get(seasonalConfigUrl))
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[SeasonalContentManager] Failed to fetch config: {request.error}");
                    return availableSeasons;
                }

                var json = request.downloadHandler.text;
                var wrapper = JsonUtility.FromJson<SeasonalConfigWrapper>($"{{\"seasons\":{json}}}");

                availableSeasons = wrapper.seasons;

                // 다운로드 상태 확인
                foreach (var season in availableSeasons)
                {
                    season.isDownloaded = PlayerPrefs.GetInt($"Season_{season.seasonId}_Downloaded", 0) == 1;
                }

                return availableSeasons;
            }
        }

        /// <summary>
        /// 시즌 콘텐츠 다운로드
        /// </summary>
        public async Task<bool> DownloadSeasonalContent(SeasonalContent season)
        {
            if (season == null || season.isDownloaded)
            {
                return false;
            }

            try
            {
                Debug.Log($"[SeasonalContentManager] Downloading season: {season.seasonId}");

                // 카탈로그 로드 (있으면)
                if (!string.IsNullOrEmpty(season.catalogUrl))
                {
                    await UnityEngine.AddressableAssets.Addressables.LoadContentCatalogAsync(season.catalogUrl).Task;
                }

                // 그룹 다운로드
                bool success = await _addressableManager.DownloadDependenciesAsync(
                    season.groupName,
                    (progress) =>
                    {
                        Debug.Log($"[SeasonalContentManager] Download progress: {progress * 100:F1}%");
                    }
                );

                if (success)
                {
                    season.isDownloaded = true;
                    PlayerPrefs.SetInt($"Season_{season.seasonId}_Downloaded", 1);
                    PlayerPrefs.Save();

                    OnSeasonDownloaded?.Invoke(season);

                    // 타이틀 업데이트
                    if (!string.IsNullOrEmpty(season.titleAddress))
                    {
                        await UpdateSeasonalTitle(season);
                    }
                }

                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SeasonalContentManager] Download failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 시즌 타이틀 업데이트
        /// </summary>
        private async Task UpdateSeasonalTitle(SeasonalContent season)
        {
            try
            {
                var titlePrefab = await _addressableManager.LoadAssetAsync<GameObject>(season.titleAddress);

                if (titlePrefab != null)
                {
                    // TitleManager에 전달 (구현 필요)
                    /*var titleManager = FindObjectOfType<TitleManager>();
                    if (titleManager != null)
                    {
                        titleManager.UpdateTitle(titlePrefab);
                    }*/


                    Debug.Log($"[SeasonalContentManager] Title updated: {season.titleAddress}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SeasonalContentManager] Failed to update title: {e.Message}");
            }
        }

        /// <summary>
        /// 시즌 캐릭터 로드
        /// </summary>
        public async Task<List<GameObject>> LoadSeasonalCharacters(bool battleMode = true)
        {
            if (currentSeason == null || !currentSeason.isDownloaded)
            {
                return new List<GameObject>();
            }

            var characters = new List<GameObject>();

            foreach (var charId in currentSeason.newCharacters)
            {
                var character = await _addressableManager.LoadCharacterAsync(charId);
                if (character != null)
                {
                    characters.Add(character);
                }
            }

            return characters;
        }

        /// <summary>
        /// 시즌 콘텐츠 정리
        /// </summary>
        public void CleanupOldSeasons()
        {
            var cutoffDate = DateTime.Now.AddMonths(-3);

            foreach (var season in availableSeasons)
            {
                if (season.endDate < cutoffDate && season.isDownloaded)
                {
                    // 오래된 시즌 캐시 제거
                    Caching.ClearAllCachedVersions(season.groupName);

                    season.isDownloaded = false;
                    PlayerPrefs.DeleteKey($"Season_{season.seasonId}_Downloaded");

                    Debug.Log($"[SeasonalContentManager] Cleaned old season: {season.seasonId}");
                }
            }

            PlayerPrefs.Save();
        }

        [System.Serializable]
        private class SeasonalConfigWrapper
        {
            public List<SeasonalContent> seasons;
        }

        // 현재 시즌 정보 접근자
        public SeasonalContent CurrentSeason => currentSeason;
        public bool HasActiveSeason => currentSeason != null && currentSeason.isActive;
        public List<string> CurrentSeasonCharacters => currentSeason?.newCharacters ?? new List<string>();
    }
}