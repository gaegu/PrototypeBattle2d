using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameCore.Initialize
{
    /// <summary>
    /// 게임 초기화 및 리소스 다운로드 관리
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("UI References")]
      //  [SerializeField] private InitializeUI initializeUI;
      //  [SerializeField] private DownloadConfirmUI downloadConfirmUI;

        [Header("Settings")]
        [SerializeField] private bool skipPrologue = false;
        [SerializeField] private float minimumLoadingTime = 2f;
        [SerializeField] private bool autoDownloadEssentials = true;

        [Header("Essential Resources")]
        [SerializeField]
        private List<string> essentialGroups = new()
        {
            "Shared_Common",
            "UI_System",
            "Audio_Common"
        };

        private GameCore.Addressables.AddressableManager _addressableManager;

        private async void Start()
        {
            try
            {
                await InitializeGame();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameInitializer] Initialization failed: {e.Message}");
              //  initializeUI.ShowError("게임 초기화에 실패했습니다.\n인터넷 연결을 확인하고 다시 시도해주세요.");
            }
        }

        private async Task InitializeGame()
        {
            float startTime = Time.time;

            // AddressableManager 초기화 대기
            _addressableManager = GameCore.Addressables.AddressableManager.Instance;

           // initializeUI.SetStatus("게임 초기화중...");
          //  initializeUI.UpdateProgress(0.1f);

            // AddressableManager 초기화 대기
            int retryCount = 0;
            while (!_addressableManager.IsInitialized && retryCount < 10)
            {
                await Task.Delay(500);
                retryCount++;
            }

            if (!_addressableManager.IsInitialized)
            {
                throw new Exception("AddressableManager initialization timeout");
            }

            // 1. 카탈로그 업데이트
          //  initializeUI.SetStatus("업데이트 확인중...");
          //  initializeUI.UpdateProgress(0.2f);

            bool catalogUpdated = await _addressableManager.CheckForCatalogUpdates();
            if (catalogUpdated)
            {
               // initializeUI.SetStatus("카탈로그 업데이트 완료");
            }

            // 2. 필수 리소스 체크 및 다운로드
          //  initializeUI.SetStatus("필수 리소스 확인중...");
          //  initializeUI.UpdateProgress(0.3f);

            await CheckAndDownloadEssentials();

            // 3. 유저 데이터 확인
            bool isNewUser = !PlayerPrefs.HasKey("UserCreated");
          //  initializeUI.UpdateProgress(0.5f);

            if (isNewUser && !skipPrologue)
            {
                // 신규 유저: 프롤로그
                await PrepareForPrologue();
            }
            else
            {
                // 기존 유저: 메인 게임
                await PrepareForMainGame();
            }

            // 최소 로딩 시간 보장
            float elapsed = Time.time - startTime;
            if (elapsed < minimumLoadingTime)
            {
                await Task.Delay((int)((minimumLoadingTime - elapsed) * 1000));
            }

         //   initializeUI.UpdateProgress(1f);
            await Task.Delay(500);

            // 씬 전환
            if (isNewUser && !skipPrologue)
            {
                SceneManager.LoadScene("Prologue");
            }
            else
            {
                SceneManager.LoadScene("Town");
            }
        }

        private async Task CheckAndDownloadEssentials()
        {
            long totalSize = 0;
            var toDownload = new List<string>();

            // 필수 리소스 크기 확인
            foreach (var group in essentialGroups)
            {
                long size = await _addressableManager.GetDownloadSizeAsync(group);
                if (size > 0)
                {
                    totalSize += size;
                    toDownload.Add(group);
                }
            }

            if (toDownload.Count == 0)
            {
                Debug.Log("[GameInitializer] All essential resources already downloaded");
                return;
            }

            // 다운로드 확인 UI
            if (!autoDownloadEssentials)
            {
             /*   bool shouldDownload = await downloadConfirmUI.ShowDownloadConfirm(
                    "필수 리소스 다운로드",
                    $"게임 실행에 필요한 리소스를 다운로드합니다.\n크기: {totalSize / 1024f / 1024f:F1} MB",
                    totalSize
                );

                if (!shouldDownload)
                {
                    Application.Quit();
                    return;
                }*/
            }

            // 다운로드 실행
         //   initializeUI.SetStatus("필수 리소스 다운로드중...");
         //   initializeUI.ShowDownloadProgress(true);

            float progressPerGroup = 1f / toDownload.Count;
            float currentProgress = 0;

            foreach (var group in toDownload)
            {
                await _addressableManager.DownloadDependenciesAsync(
                    group,
                    (progress) =>
                    {
                        float totalProgress = currentProgress + (progress * progressPerGroup);
                       // initializeUI.UpdateProgress(0.3f + totalProgress * 0.4f);
                    }
                );
                currentProgress += progressPerGroup;
            }

          //  initializeUI.ShowDownloadProgress(false);
            Debug.Log("[GameInitializer] Essential resources download complete");
        }

        private async Task PrepareForPrologue()
        {
           // initializeUI.SetStatus("프롤로그 준비중...");

            // 프롤로그 리소스 다운로드
            long prologueSize = await _addressableManager.GetDownloadSizeAsync("Prologue_Group");

            if (prologueSize > 0)
            {
            /*    bool shouldDownload = await downloadConfirmUI.ShowDownloadConfirm(
                    "프롤로그 다운로드",
                    $"튜토리얼을 시작하기 위한 리소스를 다운로드합니다.\n크기: {prologueSize / 1024f / 1024f:F1} MB",
                    prologueSize
                );

                if (!shouldDownload)
                {
                    // 프롤로그 스킵
                    PlayerPrefs.SetInt("UserCreated", 1);
                    await PrepareForMainGame();
                    return;
                }

                initializeUI.SetStatus("프롤로그 다운로드중...");
                initializeUI.ShowDownloadProgress(true);

                await _addressableManager.DownloadDependenciesAsync(
                    "Prologue_Group",
                    (progress) => initializeUI.UpdateProgress(0.7f + progress * 0.3f)
                );

                initializeUI.ShowDownloadProgress(false);*/

            }

            // 프롤로그 캐릭터 프리로드
            var prologueCharacters = new List<string> { "Lain", "Tutorial_Enemy" };
            await _addressableManager.PreloadTeamCharacters(prologueCharacters);

           // initializeUI.UpdateProgress(0.95f);
        }

        private async Task PrepareForMainGame()
        {
            //initializeUI.SetStatus("게임 데이터 로딩중...");

            // 유저 팀 정보 로드
            var userTeam = LoadUserTeam();

            if (userTeam.Count > 0)
            {
            //    initializeUI.SetStatus($"캐릭터 로딩중... (0/{userTeam.Count})");

                // 팀 캐릭터 프리로드
                for (int i = 0; i < userTeam.Count; i++)
                {
                    await _addressableManager.LoadCharacterAsync(userTeam[i]);
                 //   initializeUI.SetStatus($"캐릭터 로딩중... ({i + 1}/{userTeam.Count})");
               //     initializeUI.UpdateProgress(0.7f + (0.25f * ((i + 1) / (float)userTeam.Count)));
                }
            }

            // 자주 사용하는 UI 프리로드
            await PreloadCommonUI();

          //  initializeUI.UpdateProgress(0.98f);
        }

        private List<string> LoadUserTeam()
        {
            var team = new List<string>();

            // 저장된 팀 정보 로드
            for (int i = 0; i < 5; i++)
            {
                string charId = PlayerPrefs.GetString($"Team_Slot_{i}", "");
                if (!string.IsNullOrEmpty(charId))
                {
                    team.Add(charId);
                }
            }

            // 팀이 없으면 기본 캐릭터
            if (team.Count == 0)
            {
                team.Add("Lain");
            }

            return team;
        }

        private async Task PreloadCommonUI()
        {
            var commonUIs = new List<string>
            {
                "UI_Shop_Window",
                "UI_Inventory_Window",
                "UI_CharacterInfo_Window"
            };

            var tasks = new List<Task>();
            foreach (var ui in commonUIs)
            {
                tasks.Add(_addressableManager.LoadAssetAsync<GameObject>(ui));
            }

            await Task.WhenAll(tasks);
        }
    }
}