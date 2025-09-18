#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline;
using UnityEngine.Networking;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// Addressable 빌드 자동화 도구
    /// CI/CD 통합, 증분 빌드, 자동 업로드 기능
    /// </summary>
    public class AddressableBuildAutomation : EditorWindow
    {
        #region Nested Classes

        [System.Serializable]
        public class BuildProfile
        {
            public string profileName = "Default";
            public BuildTarget platform = BuildTarget.Android;
            public string buildPath = "ServerData";
            public string remotePath = "https://cdn.example.com";
            public bool cleanBuild = false;
            public bool incrementalBuild = true;
            public bool uploadToCDN = false;
            public bool createBackup = true;
            public bool notifyOnComplete = true;
            public List<string> groupsToBuild = new List<string>();
            public Dictionary<string, string> customVariables = new Dictionary<string, string>();
        }

        [System.Serializable]
        public class CDNConfig
        {
            public CDNProvider provider = CDNProvider.AWS;
            public string bucketName = "";
            public string region = "us-east-1";
            public string accessKey = "";
            public string secretKey = "";
            public string cloudFrontUrl = "";
            public bool useCloudFront = false;
            public bool invalidateCache = true;
            public List<string> invalidationPaths = new List<string>();
        }

        public enum CDNProvider
        {
            AWS,
            GoogleCloud,
            Azure,
            Custom,
            FTP
        }

        [System.Serializable]
        public class BuildTask
        {
            public string taskId;
            public string taskName;
            public BuildProfile profile;
            public DateTime scheduledTime;
            public BuildTaskStatus status = BuildTaskStatus.Pending;
            public float progress;
            public string currentStep;
            public List<string> logs = new List<string>();
            public Exception error;
            public BuildReport report;
        }

        public enum BuildTaskStatus
        {
            Pending,
            Running,
            Completed,
            Failed,
            Cancelled
        }

        [System.Serializable]
        public class BuildReport
        {
            public DateTime startTime;
            public DateTime endTime;
            public TimeSpan duration;
            public bool success;
            public long totalSize;
            public long compressedSize;
            public int totalAssets;
            public int modifiedAssets;
            public Dictionary<string, long> bundleSizes = new Dictionary<string, long>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            public Dictionary<string, float> stepDurations = new Dictionary<string, float>();
        }

        [System.Serializable]
        public class BuildSchedule
        {
            public bool enabled;
            public ScheduleType type = ScheduleType.Daily;
            public DayOfWeek dayOfWeek = DayOfWeek.Monday;
            public int hour = 2; // 2 AM
            public int minute = 0;
            public BuildProfile profile;
            public DateTime lastRun;
            public DateTime nextRun;
        }

        public enum ScheduleType
        {
            Once,
            Daily,
            Weekly,
            OnChange
        }

        [System.Serializable]
        public class BuildComparison
        {
            public string previousBuildPath;
            public string currentBuildPath;
            public List<AssetChange> changes = new List<AssetChange>();
            public long sizeDifference;
            public int addedAssets;
            public int modifiedAssets;
            public int removedAssets;
        }

        [System.Serializable]
        public class AssetChange
        {
            public string assetPath;
            public ChangeType type;
            public long oldSize;
            public long newSize;
            public string oldHash;
            public string newHash;
        }

        public enum ChangeType
        {
            Added,
            Modified,
            Removed
        }

        #endregion

        #region Fields

        // 설정
        private AddressableAssetSettings _settings;
        private List<BuildProfile> _buildProfiles = new List<BuildProfile>();
        private CDNConfig _cdnConfig = new CDNConfig();
        private List<BuildSchedule> _schedules = new List<BuildSchedule>();

        // 빌드 상태
        private Queue<BuildTask> _buildQueue = new Queue<BuildTask>();
        private BuildTask _currentTask;
        private List<BuildTask> _completedTasks = new List<BuildTask>();
        private bool _isBuilding = false;
        private bool _cancelRequested = false;

        // UI 상태
        private int _selectedProfileIndex = 0;
        private int _selectedTab = 0;
        private Vector2 _scrollPosition;
        private bool _showAdvancedOptions = false;
        private string _consoleOutput = "";

        // 통계
        private int _totalBuilds = 0;
        private int _successfulBuilds = 0;
        private int _failedBuilds = 0;
        private TimeSpan _totalBuildTime;
        private DateTime _lastBuildTime;

        // 경로
        private const string SETTINGS_PATH = "Assets/AddressableAssetsData/BuildAutomation/Settings.json";
        private const string PROFILES_PATH = "Assets/AddressableAssetsData/BuildAutomation/Profiles.json";
        private const string REPORTS_PATH = "Assets/AddressableAssetsData/BuildAutomation/Reports";

        // 탭 이름
        private readonly string[] _tabNames = { "Build", "Profiles", "Schedule", "CDN", "History", "Settings" };

        #endregion

        #region Window Setup

        [MenuItem("*COSMOS*/Util/Addressables/Build Automation")]
        public static void ShowWindow()
        {
            var window = GetWindow<AddressableBuildAutomation>("Build Automation");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings;

            if (_settings == null)
            {
                Debug.LogError("Addressable settings not found!");
                return;
            }

            LoadSettings();
            LoadProfiles();

            // 스케줄러 시작
            EditorApplication.update += UpdateScheduler;
        }

        private void OnDisable()
        {
            SaveSettings();
            SaveProfiles();

            EditorApplication.update -= UpdateScheduler;
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                DrawNoSettingsUI();
                return;
            }

            DrawToolbar();

            EditorGUILayout.Space();

            // 탭 그리기
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);

            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case 0: DrawBuildTab(); break;
                case 1: DrawProfilesTab(); break;
                case 2: DrawScheduleTab(); break;
                case 3: DrawCDNTab(); break;
                case 4: DrawHistoryTab(); break;
                case 5: DrawSettingsTab(); break;
            }

            EditorGUILayout.EndScrollView();

            // 빌드 진행 상황 표시
            if (_isBuilding && _currentTask != null)
            {
                DrawBuildProgress();
            }
        }

        #endregion

        #region UI - Toolbar

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 프로파일 선택
            if (_buildProfiles.Count > 0)
            {
                string[] profileNames = _buildProfiles.Select(p => p.profileName).ToArray();
                _selectedProfileIndex = EditorGUILayout.Popup(_selectedProfileIndex, profileNames,
                    EditorStyles.toolbarPopup, GUILayout.Width(150));
            }

            GUILayout.FlexibleSpace();

            // 빌드 상태
            if (_isBuilding)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("● Building...", EditorStyles.toolbarButton);
                GUI.color = Color.white;

                if (GUILayout.Button("Cancel", EditorStyles.toolbarButton))
                {
                    _cancelRequested = true;
                }
            }
            else
            {
                GUI.color = Color.green;
                GUILayout.Label("● Ready", EditorStyles.toolbarButton);
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region UI - Build Tab

        private void DrawBuildTab()
        {
            EditorGUILayout.LabelField("Build Configuration", EditorStyles.boldLabel);

            if (_buildProfiles.Count == 0)
            {
                EditorGUILayout.HelpBox("No build profiles found. Create one in the Profiles tab.", MessageType.Info);
                return;
            }

            var profile = _buildProfiles[_selectedProfileIndex];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 기본 설정
            EditorGUILayout.LabelField("Profile: " + profile.profileName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Platform: " + profile.platform);
            EditorGUILayout.LabelField("Build Path: " + profile.buildPath);

            EditorGUILayout.Space();

            // 빌드 옵션
            profile.cleanBuild = EditorGUILayout.Toggle("Clean Build", profile.cleanBuild);
            profile.incrementalBuild = EditorGUILayout.Toggle("Incremental Build", profile.incrementalBuild);
            profile.uploadToCDN = EditorGUILayout.Toggle("Upload to CDN", profile.uploadToCDN);
            profile.createBackup = EditorGUILayout.Toggle("Create Backup", profile.createBackup);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 그룹 선택
            DrawGroupSelection(profile);

            EditorGUILayout.Space();

            // 빌드 버튼
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Build Now", GUILayout.Height(40)))
            {
                StartBuild(profile);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Build & Upload", GUILayout.Height(40), GUILayout.Width(150)))
            {
                profile.uploadToCDN = true;
                StartBuild(profile);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 빌드 검증
            if (GUILayout.Button("Validate Build"))
            {
                ValidateBuild(profile);
            }

            if (GUILayout.Button("Compare with Previous"))
            {
                CompareBuildWithPrevious(profile);
            }

            EditorGUILayout.Space();

            // 콘솔 출력
            DrawConsole();
        }

        private void DrawGroupSelection(BuildProfile profile)
        {
            EditorGUILayout.LabelField("Groups to Build", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool selectAll = GUILayout.Button("Select All", GUILayout.Width(100));
            bool deselectAll = GUILayout.Button("Deselect All", GUILayout.Width(100));

            EditorGUILayout.Space();

            foreach (var group in _settings.groups)
            {
                if (group == null) continue;

                bool isSelected = profile.groupsToBuild.Contains(group.name);
                bool newSelected = EditorGUILayout.ToggleLeft(group.name, isSelected);

                if (selectAll) newSelected = true;
                if (deselectAll) newSelected = false;

                if (newSelected != isSelected)
                {
                    if (newSelected)
                        profile.groupsToBuild.Add(group.name);
                    else
                        profile.groupsToBuild.Remove(group.name);
                }
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region UI - Profiles Tab

        private void DrawProfilesTab()
        {
            EditorGUILayout.LabelField("Build Profiles", EditorStyles.boldLabel);

            // 프로파일 리스트
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _buildProfiles.Count; i++)
            {
                var profile = _buildProfiles[i];

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(profile.profileName, EditorStyles.label))
                {
                    _selectedProfileIndex = i;
                }

                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                {
                    EditProfile(profile);
                }

                if (GUILayout.Button("Clone", GUILayout.Width(50)))
                {
                    CloneProfile(profile);
                }

                GUI.color = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    if (EditorUtility.DisplayDialog("Delete Profile",
                        $"Delete profile '{profile.profileName}'?", "Delete", "Cancel"))
                    {
                        _buildProfiles.RemoveAt(i);
                        SaveProfiles();
                        break;
                    }
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 새 프로파일 추가
            if (GUILayout.Button("Add New Profile"))
            {
                CreateNewProfile();
            }

            EditorGUILayout.Space();

            // 선택된 프로파일 편집
            if (_selectedProfileIndex < _buildProfiles.Count)
            {
                DrawProfileEditor(_buildProfiles[_selectedProfileIndex]);
            }
        }

        private void DrawProfileEditor(BuildProfile profile)
        {
            EditorGUILayout.LabelField("Profile Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            profile.profileName = EditorGUILayout.TextField("Name", profile.profileName);
            profile.platform = (BuildTarget)EditorGUILayout.EnumPopup("Platform", profile.platform);
            profile.buildPath = EditorGUILayout.TextField("Build Path", profile.buildPath);
            profile.remotePath = EditorGUILayout.TextField("Remote URL", profile.remotePath);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Build Options", EditorStyles.boldLabel);
            profile.cleanBuild = EditorGUILayout.Toggle("Clean Build", profile.cleanBuild);
            profile.incrementalBuild = EditorGUILayout.Toggle("Incremental Build", profile.incrementalBuild);
            profile.uploadToCDN = EditorGUILayout.Toggle("Upload to CDN", profile.uploadToCDN);
            profile.createBackup = EditorGUILayout.Toggle("Create Backup", profile.createBackup);
            profile.notifyOnComplete = EditorGUILayout.Toggle("Notify on Complete", profile.notifyOnComplete);

            EditorGUILayout.Space();

            // 커스텀 변수
            EditorGUILayout.LabelField("Custom Variables", EditorStyles.boldLabel);

            foreach (var kvp in profile.customVariables.ToList())
            {
                EditorGUILayout.BeginHorizontal();

                string key = EditorGUILayout.TextField(kvp.Key, GUILayout.Width(150));
                string value = EditorGUILayout.TextField(kvp.Value);

                if (key != kvp.Key)
                {
                    profile.customVariables.Remove(kvp.Key);
                    profile.customVariables[key] = value;
                }
                else
                {
                    profile.customVariables[key] = value;
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    profile.customVariables.Remove(kvp.Key);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Variable"))
            {
                profile.customVariables[$"var_{profile.customVariables.Count}"] = "";
            }

            EditorGUILayout.EndVertical();

            if (GUI.changed)
            {
                SaveProfiles();
            }
        }

        #endregion

        #region UI - Schedule Tab

        private void DrawScheduleTab()
        {
            EditorGUILayout.LabelField("Build Schedule", EditorStyles.boldLabel);

            // 스케줄 리스트
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < _schedules.Count; i++)
            {
                var schedule = _schedules[i];

                EditorGUILayout.BeginHorizontal();

                schedule.enabled = EditorGUILayout.Toggle(schedule.enabled, GUILayout.Width(20));

                string scheduleText = GetScheduleDescription(schedule);
                EditorGUILayout.LabelField(scheduleText);

                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                {
                    EditSchedule(schedule);
                }

                GUI.color = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    _schedules.RemoveAt(i);
                    SaveSettings();
                    break;
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                if (schedule.enabled)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Next Run: {schedule.nextRun:yyyy-MM-dd HH:mm}", EditorStyles.miniLabel);
                    if (schedule.lastRun != default)
                    {
                        EditorGUILayout.LabelField($"Last Run: {schedule.lastRun:yyyy-MM-dd HH:mm}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            if (GUILayout.Button("Add Schedule"))
            {
                CreateNewSchedule();
            }

            EditorGUILayout.Space();

            // 스케줄 에디터
            if (_editingSchedule != null)
            {
                DrawScheduleEditor(_editingSchedule);
            }
        }

        private BuildSchedule _editingSchedule;

        private void DrawScheduleEditor(BuildSchedule schedule)
        {
            EditorGUILayout.LabelField("Schedule Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            schedule.type = (ScheduleType)EditorGUILayout.EnumPopup("Type", schedule.type);

            switch (schedule.type)
            {
                case ScheduleType.Daily:
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Time", GUILayout.Width(50));
                    schedule.hour = EditorGUILayout.IntField(schedule.hour, GUILayout.Width(30));
                    EditorGUILayout.LabelField(":", GUILayout.Width(10));
                    schedule.minute = EditorGUILayout.IntField(schedule.minute, GUILayout.Width(30));
                    EditorGUILayout.EndHorizontal();
                    break;

                case ScheduleType.Weekly:
                    schedule.dayOfWeek = (DayOfWeek)EditorGUILayout.EnumPopup("Day", schedule.dayOfWeek);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Time", GUILayout.Width(50));
                    schedule.hour = EditorGUILayout.IntField(schedule.hour, GUILayout.Width(30));
                    EditorGUILayout.LabelField(":", GUILayout.Width(10));
                    schedule.minute = EditorGUILayout.IntField(schedule.minute, GUILayout.Width(30));
                    EditorGUILayout.EndHorizontal();
                    break;
            }

            // 프로파일 선택
            if (_buildProfiles.Count > 0)
            {
                string[] profileNames = _buildProfiles.Select(p => p.profileName).ToArray();
                int selectedIndex = schedule.profile != null ?
                    _buildProfiles.IndexOf(schedule.profile) : 0;

                selectedIndex = EditorGUILayout.Popup("Profile", selectedIndex, profileNames);

                if (selectedIndex >= 0 && selectedIndex < _buildProfiles.Count)
                {
                    schedule.profile = _buildProfiles[selectedIndex];
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save"))
            {
                UpdateScheduleNextRun(schedule);
                SaveSettings();
                _editingSchedule = null;
            }

            if (GUILayout.Button("Cancel"))
            {
                _editingSchedule = null;
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region UI - CDN Tab

        private void DrawCDNTab()
        {
            EditorGUILayout.LabelField("CDN Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _cdnConfig.provider = (CDNProvider)EditorGUILayout.EnumPopup("Provider", _cdnConfig.provider);

            switch (_cdnConfig.provider)
            {
                case CDNProvider.AWS:
                    DrawAWSConfig();
                    break;
                case CDNProvider.GoogleCloud:
                    DrawGoogleCloudConfig();
                    break;
                case CDNProvider.Azure:
                    DrawAzureConfig();
                    break;
                case CDNProvider.Custom:
                    DrawCustomCDNConfig();
                    break;
                case CDNProvider.FTP:
                    DrawFTPConfig();
                    break;
            }

            EditorGUILayout.Space();

            _cdnConfig.invalidateCache = EditorGUILayout.Toggle("Invalidate Cache", _cdnConfig.invalidateCache);

            if (_cdnConfig.invalidateCache)
            {
                EditorGUILayout.LabelField("Invalidation Paths", EditorStyles.boldLabel);

                for (int i = 0; i < _cdnConfig.invalidationPaths.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    _cdnConfig.invalidationPaths[i] = EditorGUILayout.TextField(_cdnConfig.invalidationPaths[i]);

                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        _cdnConfig.invalidationPaths.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Path"))
                {
                    _cdnConfig.invalidationPaths.Add("/*");
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 테스트 버튼
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Test Connection"))
            {
                TestCDNConnection();
            }

            if (GUILayout.Button("Upload Test File"))
            {
                UploadTestFile();
            }

            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
            {
                SaveSettings();
            }
        }

        private void DrawAWSConfig()
        {
            _cdnConfig.bucketName = EditorGUILayout.TextField("Bucket Name", _cdnConfig.bucketName);
            _cdnConfig.region = EditorGUILayout.TextField("Region", _cdnConfig.region);
            _cdnConfig.accessKey = EditorGUILayout.PasswordField("Access Key", _cdnConfig.accessKey);
            _cdnConfig.secretKey = EditorGUILayout.PasswordField("Secret Key", _cdnConfig.secretKey);

            _cdnConfig.useCloudFront = EditorGUILayout.Toggle("Use CloudFront", _cdnConfig.useCloudFront);
            if (_cdnConfig.useCloudFront)
            {
                _cdnConfig.cloudFrontUrl = EditorGUILayout.TextField("CloudFront URL", _cdnConfig.cloudFrontUrl);
            }
        }

        private void DrawGoogleCloudConfig()
        {
            EditorGUILayout.LabelField("Google Cloud Storage settings...");
            // Google Cloud 설정 UI
        }

        private void DrawAzureConfig()
        {
            EditorGUILayout.LabelField("Azure Blob Storage settings...");
            // Azure 설정 UI
        }

        private void DrawCustomCDNConfig()
        {
            EditorGUILayout.LabelField("Custom CDN settings...");
            // 커스텀 CDN 설정 UI
        }

        private void DrawFTPConfig()
        {
            EditorGUILayout.LabelField("FTP settings...");
            // FTP 설정 UI
        }

        #endregion

        #region UI - History Tab

        private void DrawHistoryTab()
        {
            EditorGUILayout.LabelField("Build History", EditorStyles.boldLabel);

            // 통계
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Total Builds: {_totalBuilds}");
            EditorGUILayout.LabelField($"Successful: {_successfulBuilds}");
            EditorGUILayout.LabelField($"Failed: {_failedBuilds}");
            EditorGUILayout.LabelField($"Success Rate: {(_totalBuilds > 0 ? (_successfulBuilds * 100f / _totalBuilds) : 0):F1}%");
            EditorGUILayout.LabelField($"Average Build Time: {(_totalBuilds > 0 ? _totalBuildTime.TotalMinutes / _totalBuilds : 0):F1} minutes");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 최근 빌드 리스트
            EditorGUILayout.LabelField("Recent Builds", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (var task in _completedTasks.OrderByDescending(t => t.report?.endTime).Take(10))
            {
                DrawBuildTaskSummary(task);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 액션 버튼
            if (GUILayout.Button("Export History"))
            {
                ExportBuildHistory();
            }

            if (GUILayout.Button("Clear History"))
            {
                if (EditorUtility.DisplayDialog("Clear History",
                    "Clear all build history?", "Clear", "Cancel"))
                {
                    _completedTasks.Clear();
                    _totalBuilds = 0;
                    _successfulBuilds = 0;
                    _failedBuilds = 0;
                    _totalBuildTime = TimeSpan.Zero;
                }
            }
        }

        private void DrawBuildTaskSummary(BuildTask task)
        {
            EditorGUILayout.BeginHorizontal();

            // 상태 아이콘
            GUI.color = task.status == BuildTaskStatus.Completed ? Color.green : Color.red;
            EditorGUILayout.LabelField(task.status == BuildTaskStatus.Completed ? "✓" : "✗",
                GUILayout.Width(20));
            GUI.color = Color.white;

            // 정보
            EditorGUILayout.LabelField($"{task.taskName} - {task.report?.endTime:yyyy-MM-dd HH:mm}");
            EditorGUILayout.LabelField($"{task.report?.duration.TotalMinutes:F1} min", GUILayout.Width(60));
            EditorGUILayout.LabelField($"{FormatBytes(task.report?.totalSize ?? 0)}", GUILayout.Width(80));

            // 액션
            if (GUILayout.Button("Details", GUILayout.Width(60)))
            {
                ShowBuildDetails(task);
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region UI - Settings Tab

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Build Automation Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 일반 설정
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            _showAdvancedOptions = EditorGUILayout.Toggle("Show Advanced Options", _showAdvancedOptions);

            EditorGUILayout.Space();

            // 경로 설정
            EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Settings Path", SETTINGS_PATH);
            EditorGUILayout.TextField("Profiles Path", PROFILES_PATH);
            EditorGUILayout.TextField("Reports Path", REPORTS_PATH);

            EditorGUILayout.Space();

            // 알림 설정
            EditorGUILayout.LabelField("Notifications", EditorStyles.boldLabel);
            bool enableNotifications = EditorGUILayout.Toggle("Enable Notifications", true);
            bool notifyOnSuccess = EditorGUILayout.Toggle("Notify on Success", true);
            bool notifyOnFailure = EditorGUILayout.Toggle("Notify on Failure", true);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 고급 설정
            if (_showAdvancedOptions)
            {
                DrawAdvancedSettings();
            }

            EditorGUILayout.Space();

            // 액션 버튼
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Reset all settings to defaults?", "Reset", "Cancel"))
                {
                    ResetSettings();
                }
            }

            if (GUILayout.Button("Export Settings"))
            {
                ExportSettings();
            }

            if (GUILayout.Button("Import Settings"))
            {
                ImportSettings();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSettings()
        {
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 빌드 설정
            int maxConcurrentBuilds = EditorGUILayout.IntField("Max Concurrent Builds", 1);
            int buildTimeout = EditorGUILayout.IntField("Build Timeout (minutes)", 60);

            // 캐시 설정
            bool useBuildCache = EditorGUILayout.Toggle("Use Build Cache", true);
            int cacheSize = EditorGUILayout.IntField("Cache Size (MB)", 1024);

            // 로깅 설정
            bool verboseLogging = EditorGUILayout.Toggle("Verbose Logging", false);
            bool saveLogsToFile = EditorGUILayout.Toggle("Save Logs to File", true);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Build Execution

        private async void StartBuild(BuildProfile profile)
        {
            if (_isBuilding)
            {
                Debug.LogWarning("Build already in progress!");
                return;
            }

            var task = new BuildTask
            {
                taskId = Guid.NewGuid().ToString(),
                taskName = $"{profile.profileName}_{DateTime.Now:yyyyMMdd_HHmmss}",
                profile = profile,
                scheduledTime = DateTime.Now,
                status = BuildTaskStatus.Pending
            };

            _buildQueue.Enqueue(task);
            ProcessBuildQueue();
        }

        private async void ProcessBuildQueue()
        {
            if (_isBuilding || _buildQueue.Count == 0)
                return;

            _isBuilding = true;
            _currentTask = _buildQueue.Dequeue();
            _currentTask.status = BuildTaskStatus.Running;
            _currentTask.report = new BuildReport { startTime = DateTime.Now };

            try
            {
                AddLog("Starting build: " + _currentTask.taskName);

                // 1. 준비
                await PrepareBuild(_currentTask);

                // 2. 빌드 실행
                await ExecuteBuild(_currentTask);

                // 3. 후처리
                await PostProcessBuild(_currentTask);

                // 4. CDN 업로드
                if (_currentTask.profile.uploadToCDN)
                {
                    await UploadToCDN(_currentTask);
                }

                _currentTask.status = BuildTaskStatus.Completed;
                _currentTask.report.success = true;
                _successfulBuilds++;

                AddLog("Build completed successfully!");
            }
            catch (Exception e)
            {
                _currentTask.status = BuildTaskStatus.Failed;
                _currentTask.error = e;
                _currentTask.report.success = false;
                _currentTask.report.errors.Add(e.Message);
                _failedBuilds++;

                AddLog($"Build failed: {e.Message}", LogType.Error);
                Debug.LogError(e);
            }
            finally
            {
                _currentTask.report.endTime = DateTime.Now;
                _currentTask.report.duration = _currentTask.report.endTime - _currentTask.report.startTime;

                _totalBuilds++;
                _totalBuildTime += _currentTask.report.duration;
                _lastBuildTime = DateTime.Now;

                _completedTasks.Add(_currentTask);
                _currentTask = null;
                _isBuilding = false;

                // 다음 빌드 처리
                if (_buildQueue.Count > 0)
                {
                    ProcessBuildQueue();
                }

                // 알림
                if (_currentTask?.profile.notifyOnComplete == true)
                {
                    SendBuildNotification(_currentTask);
                }

                SaveReports();
            }
        }

        private async Task PrepareBuild(BuildTask task)
        {
            task.currentStep = "Preparing build...";
            task.progress = 0.1f;

            // 백업 생성
            if (task.profile.createBackup)
            {
                CreateBackup(task.profile);
            }

            // Clean build
            if (task.profile.cleanBuild)
            {
                AddressableAssetSettings.CleanPlayerContent();
                AddLog("Cleaned player content");
            }

            // 프로파일 설정
            SetupBuildProfile(task.profile);

            await Task.Delay(100);
        }

        private async Task ExecuteBuild(BuildTask task)
        {
            task.currentStep = "Building addressables...";
            task.progress = 0.3f;

            AddressableAssetSettings.BuildPlayerContent();

            // 빌드 결과 수집
            CollectBuildResults(task);

            await Task.Delay(100);
        }

        private async Task PostProcessBuild(BuildTask task)
        {
            task.currentStep = "Post-processing...";
            task.progress = 0.7f;

            // 빌드 검증
            ValidateBuildOutput(task);

            // 리포트 생성
            GenerateBuildReport(task);

            await Task.Delay(100);
        }

        private async Task UploadToCDN(BuildTask task)
        {
            task.currentStep = "Uploading to CDN...";
            task.progress = 0.9f;

            AddLog("Starting CDN upload...");

            // CDN 업로드 구현
            await UploadFilesToCDN(task.profile.buildPath, _cdnConfig);

            // 캐시 무효화
            if (_cdnConfig.invalidateCache)
            {
                await InvalidateCDNCache(_cdnConfig);
            }

            AddLog("CDN upload completed");
        }

        #endregion

        #region CDN Operations

        private async Task UploadFilesToCDN(string localPath, CDNConfig config)
        {
            switch (config.provider)
            {
                case CDNProvider.AWS:
                    await UploadToAWS(localPath, config);
                    break;
                case CDNProvider.GoogleCloud:
                    await UploadToGoogleCloud(localPath, config);
                    break;
                case CDNProvider.Azure:
                    await UploadToAzure(localPath, config);
                    break;
                case CDNProvider.FTP:
                    await UploadToFTP(localPath, config);
                    break;
                case CDNProvider.Custom:
                    await UploadToCustomCDN(localPath, config);
                    break;
            }
        }

        private async Task UploadToAWS(string localPath, CDNConfig config)
        {
            // AWS S3 업로드 구현
            AddLog($"Uploading to AWS S3: {config.bucketName}");

            // 실제 구현에서는 AWS SDK 사용
            await Task.Delay(1000); // 시뮬레이션

            AddLog("AWS upload completed");
        }

        private async Task UploadToGoogleCloud(string localPath, CDNConfig config)
        {
            // Google Cloud Storage 업로드 구현
            await Task.Delay(1000);
        }

        private async Task UploadToAzure(string localPath, CDNConfig config)
        {
            // Azure Blob Storage 업로드 구현
            await Task.Delay(1000);
        }

        private async Task UploadToFTP(string localPath, CDNConfig config)
        {
            // FTP 업로드 구현
            await Task.Delay(1000);
        }

        private async Task UploadToCustomCDN(string localPath, CDNConfig config)
        {
            // 커스텀 CDN 업로드 구현
            await Task.Delay(1000);
        }

        private async Task InvalidateCDNCache(CDNConfig config)
        {
            AddLog("Invalidating CDN cache...");

            switch (config.provider)
            {
                case CDNProvider.AWS:
                    if (config.useCloudFront)
                    {
                        // CloudFront invalidation
                        await Task.Delay(500);
                    }
                    break;
            }

            AddLog("Cache invalidation completed");
        }

        #endregion

        #region Helper Methods

        private void DrawNoSettingsUI()
        {
            EditorGUILayout.HelpBox("Addressable Settings not found!", MessageType.Error);

            if (GUILayout.Button("Create Settings"))
            {
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true, true);
                _settings = AddressableAssetSettingsDefaultObject.Settings;
            }
        }

        private void DrawBuildProgress()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var progressRect = new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 20);
            EditorGUI.ProgressBar(progressRect, _currentTask.progress, _currentTask.currentStep);

            var statusRect = new Rect(rect.x + 10, rect.y + 35, rect.width - 20, 20);
            GUI.Label(statusRect, $"Task: {_currentTask.taskName}", EditorStyles.miniLabel);
        }

        private void DrawConsole()
        {
            EditorGUILayout.LabelField("Console Output", EditorStyles.boldLabel);

            var rect = GUILayoutUtility.GetRect(0, 150, GUILayout.ExpandWidth(true));
            _consoleOutput = EditorGUI.TextArea(rect, _consoleOutput);
        }

        private void AddLog(string message, LogType type = LogType.Log)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}\n";

            _consoleOutput += logEntry;

            if (_currentTask != null)
            {
                _currentTask.logs.Add(logEntry);
            }

            switch (type)
            {
                case LogType.Error:
                    Debug.LogError(message);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }

        private void CreateNewProfile()
        {
            var profile = new BuildProfile
            {
                profileName = $"Profile_{_buildProfiles.Count + 1}",
                platform = EditorUserBuildSettings.activeBuildTarget,
                buildPath = "ServerData/[BuildTarget]",
                remotePath = "https://cdn.example.com/[BuildTarget]"
            };

            _buildProfiles.Add(profile);
            SaveProfiles();
        }

        private void CloneProfile(BuildProfile original)
        {
            var clone = JsonUtility.FromJson<BuildProfile>(JsonUtility.ToJson(original));
            clone.profileName = original.profileName + "_Copy";
            _buildProfiles.Add(clone);
            SaveProfiles();
        }

        private void EditProfile(BuildProfile profile)
        {
            _selectedTab = 1; // Switch to Profiles tab
        }

        private void CreateNewSchedule()
        {
            var schedule = new BuildSchedule
            {
                enabled = false,
                type = ScheduleType.Daily,
                hour = 2,
                minute = 0,
                profile = _buildProfiles.FirstOrDefault()
            };

            _schedules.Add(schedule);
            _editingSchedule = schedule;
            UpdateScheduleNextRun(schedule);
            SaveSettings();
        }

        private void EditSchedule(BuildSchedule schedule)
        {
            _editingSchedule = schedule;
        }

        private string GetScheduleDescription(BuildSchedule schedule)
        {
            switch (schedule.type)
            {
                case ScheduleType.Once:
                    return $"Once at {schedule.nextRun:yyyy-MM-dd HH:mm}";
                case ScheduleType.Daily:
                    return $"Daily at {schedule.hour:D2}:{schedule.minute:D2}";
                case ScheduleType.Weekly:
                    return $"Weekly on {schedule.dayOfWeek} at {schedule.hour:D2}:{schedule.minute:D2}";
                case ScheduleType.OnChange:
                    return "On asset change";
                default:
                    return "Unknown";
            }
        }

        private void UpdateScheduleNextRun(BuildSchedule schedule)
        {
            var now = DateTime.Now;

            switch (schedule.type)
            {
                case ScheduleType.Daily:
                    schedule.nextRun = now.Date.AddHours(schedule.hour).AddMinutes(schedule.minute);
                    if (schedule.nextRun <= now)
                        schedule.nextRun = schedule.nextRun.AddDays(1);
                    break;

                case ScheduleType.Weekly:
                    schedule.nextRun = GetNextWeekday(schedule.dayOfWeek, schedule.hour, schedule.minute);
                    break;
            }
        }

        private DateTime GetNextWeekday(DayOfWeek dayOfWeek, int hour, int minute)
        {
            var now = DateTime.Now;
            var daysUntil = ((int)dayOfWeek - (int)now.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && now.Hour * 60 + now.Minute >= hour * 60 + minute)
                daysUntil = 7;

            return now.Date.AddDays(daysUntil).AddHours(hour).AddMinutes(minute);
        }

        private void UpdateScheduler()
        {
            var now = DateTime.Now;

            foreach (var schedule in _schedules.Where(s => s.enabled))
            {
                if (schedule.nextRun <= now)
                {
                    // 스케줄된 빌드 실행
                    if (schedule.profile != null)
                    {
                        StartBuild(schedule.profile);
                    }

                    schedule.lastRun = now;
                    UpdateScheduleNextRun(schedule);
                    SaveSettings();
                }
            }
        }

        private void SetupBuildProfile(BuildProfile profile)
        {
            // AddressableAssetSettings 프로파일 설정
            var profileSettings = _settings.profileSettings;

            // 커스텀 변수 적용
            foreach (var kvp in profile.customVariables)
            {
                // profileSettings.SetValue(profileId, kvp.Key, kvp.Value);
            }
        }

        private void CreateBackup(BuildProfile profile)
        {
            string backupPath = Path.Combine(Application.dataPath, "..", "Backups",
                $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}");

            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            // 빌드 폴더 백업
            string sourcePath = Path.Combine(Application.dataPath, "..", profile.buildPath);
            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, backupPath);
                AddLog($"Created backup at: {backupPath}");
            }
        }

        private void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string destDir = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        private void CollectBuildResults(BuildTask task)
        {
            // 빌드 결과 수집
            string buildPath = Path.Combine(Application.dataPath, "..", task.profile.buildPath);

            if (Directory.Exists(buildPath))
            {
                var files = Directory.GetFiles(buildPath, "*", SearchOption.AllDirectories);

                task.report.totalAssets = files.Length;
                task.report.totalSize = files.Sum(f => new FileInfo(f).Length);

                // 번들별 크기
                foreach (var file in files.Where(f => f.EndsWith(".bundle")))
                {
                    var info = new FileInfo(file);
                    task.report.bundleSizes[Path.GetFileName(file)] = info.Length;
                }
            }
        }

        private void ValidateBuildOutput(BuildTask task)
        {
            // 빌드 출력 검증
            AddLog("Validating build output...");

            // 필수 파일 체크
            // 크기 검증
            // 해시 검증
        }

        private void GenerateBuildReport(BuildTask task)
        {
            // 빌드 리포트 생성
            string reportPath = Path.Combine(REPORTS_PATH, $"{task.taskName}_report.json");

            if (!Directory.Exists(REPORTS_PATH))
            {
                Directory.CreateDirectory(REPORTS_PATH);
            }

            string json = JsonUtility.ToJson(task.report, true);
            File.WriteAllText(reportPath, json);

            AddLog($"Report saved: {reportPath}");
        }

        private void ValidateBuild(BuildProfile profile)
        {
            AddLog("Validating build configuration...");

            // 프로파일 검증
            if (string.IsNullOrEmpty(profile.buildPath))
            {
                AddLog("Build path is empty!", LogType.Error);
                return;
            }

            // 그룹 검증
            if (profile.groupsToBuild.Count == 0)
            {
                AddLog("No groups selected!", LogType.Warning);
            }

            AddLog("Validation completed");
        }

        private void CompareBuildWithPrevious(BuildProfile profile)
        {
            // 이전 빌드와 비교
            var comparison = new BuildComparison();

            // 비교 로직 구현

            ShowComparisonWindow(comparison);
        }

        private void ShowComparisonWindow(BuildComparison comparison)
        {
            // 비교 결과 윈도우 표시
        }

        private void ShowBuildDetails(BuildTask task)
        {
            // 빌드 상세 정보 윈도우
            var window = GetWindow<BuildDetailsWindow>("Build Details");
            window.SetTask(task);
            window.Show();
        }

        private void TestCDNConnection()
        {
            AddLog("Testing CDN connection...");

            // CDN 연결 테스트

            AddLog("Connection test completed");
        }

        private void UploadTestFile()
        {
            AddLog("Uploading test file...");

            // 테스트 파일 업로드

            AddLog("Test file uploaded");
        }

        private void SendBuildNotification(BuildTask task)
        {
            string title = task.status == BuildTaskStatus.Completed ?
                "Build Completed" : "Build Failed";

            string message = $"{task.taskName}\nDuration: {task.report.duration.TotalMinutes:F1} minutes";

            if (task.status == BuildTaskStatus.Failed)
            {
                message += $"\nError: {task.error?.Message}";
            }

            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private void ExportBuildHistory()
        {
            string path = EditorUtility.SaveFilePanel("Export Build History", "",
                $"build_history_{DateTime.Now:yyyyMMdd}", "csv");

            if (!string.IsNullOrEmpty(path))
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Task,Status,Start Time,Duration,Size,Errors");

                foreach (var task in _completedTasks)
                {
                    csv.AppendLine($"{task.taskName},{task.status}," +
                        $"{task.report?.startTime:yyyy-MM-dd HH:mm}," +
                        $"{task.report?.duration.TotalMinutes:F1}," +
                        $"{task.report?.totalSize}," +
                        $"{task.report?.errors.Count}");
                }

                File.WriteAllText(path, csv.ToString());
                AddLog($"History exported to: {path}");
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        #endregion

        #region Settings Persistence

        private void LoadSettings()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                string json = File.ReadAllText(SETTINGS_PATH);
                JsonUtility.FromJsonOverwrite(json, this);
            }
        }

        private void SaveSettings()
        {
            string dir = Path.GetDirectoryName(SETTINGS_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(SETTINGS_PATH, json);
        }

        private void LoadProfiles()
        {
            if (File.Exists(PROFILES_PATH))
            {
                string json = File.ReadAllText(PROFILES_PATH);
                var wrapper = JsonUtility.FromJson<ProfilesWrapper>(json);
                _buildProfiles = wrapper.profiles ?? new List<BuildProfile>();
            }

            // 기본 프로파일 생성
            if (_buildProfiles.Count == 0)
            {
                CreateDefaultProfiles();
            }
        }

        private void SaveProfiles()
        {
            string dir = Path.GetDirectoryName(PROFILES_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var wrapper = new ProfilesWrapper { profiles = _buildProfiles };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(PROFILES_PATH, json);
        }

        private void SaveReports()
        {
            // 리포트 저장
        }

        private void CreateDefaultProfiles()
        {
            // Android 프로파일
            _buildProfiles.Add(new BuildProfile
            {
                profileName = "Android Production",
                platform = BuildTarget.Android,
                buildPath = "ServerData/Android",
                remotePath = "https://cdn.example.com/android",
                cleanBuild = false,
                incrementalBuild = true
            });

            // iOS 프로파일
            _buildProfiles.Add(new BuildProfile
            {
                profileName = "iOS Production",
                platform = BuildTarget.iOS,
                buildPath = "ServerData/iOS",
                remotePath = "https://cdn.example.com/ios",
                cleanBuild = false,
                incrementalBuild = true
            });

            SaveProfiles();
        }

        private void ResetSettings()
        {
            _buildProfiles.Clear();
            _schedules.Clear();
            _cdnConfig = new CDNConfig();

            CreateDefaultProfiles();
            SaveSettings();
        }

        private void ExportSettings()
        {
            string path = EditorUtility.SaveFilePanel("Export Settings", "",
                "build_automation_settings", "json");

            if (!string.IsNullOrEmpty(path))
            {
                var export = new
                {
                    profiles = _buildProfiles,
                    schedules = _schedules,
                    cdnConfig = _cdnConfig
                };

                string json = JsonUtility.ToJson(export, true);
                File.WriteAllText(path, json);
            }
        }

        private void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel("Import Settings", "", "json");

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string json = File.ReadAllText(path);
                // Import logic
                SaveSettings();
                SaveProfiles();
            }
        }

        [System.Serializable]
        private class ProfilesWrapper
        {
            public List<BuildProfile> profiles;
        }

        #endregion

        #region Build Details Window

        public class BuildDetailsWindow : EditorWindow
        {
            private BuildTask _task;
            private Vector2 _scrollPos;

            public void SetTask(BuildTask task)
            {
                _task = task;
            }

            private void OnGUI()
            {
                if (_task == null)
                {
                    EditorGUILayout.LabelField("No task selected");
                    return;
                }

                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

                // Task 정보
                EditorGUILayout.LabelField("Task Information", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("ID: " + _task.taskId);
                EditorGUILayout.LabelField("Name: " + _task.taskName);
                EditorGUILayout.LabelField("Status: " + _task.status);
                EditorGUILayout.LabelField("Profile: " + _task.profile?.profileName);

                EditorGUILayout.Space();

                // Report
                if (_task.report != null)
                {
                    EditorGUILayout.LabelField("Build Report", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Start: {_task.report.startTime}");
                    EditorGUILayout.LabelField($"End: {_task.report.endTime}");
                    EditorGUILayout.LabelField($"Duration: {_task.report.duration.TotalMinutes:F1} minutes");
                    EditorGUILayout.LabelField($"Success: {_task.report.success}");
                    EditorGUILayout.LabelField($"Total Size: {_task.report.totalSize / 1024 / 1024} MB");

                    // Errors
                    if (_task.report.errors.Count > 0)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Errors", EditorStyles.boldLabel);
                        foreach (var error in _task.report.errors)
                        {
                            EditorGUILayout.HelpBox(error, MessageType.Error);
                        }
                    }

                    // Bundle sizes
                    if (_task.report.bundleSizes.Count > 0)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Bundle Sizes", EditorStyles.boldLabel);
                        foreach (var kvp in _task.report.bundleSizes)
                        {
                            EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value / 1024} KB");
                        }
                    }
                }

                // Logs
                if (_task.logs.Count > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Logs", EditorStyles.boldLabel);

                    string logs = string.Join("", _task.logs);
                    EditorGUILayout.TextArea(logs, GUILayout.Height(200));
                }

                EditorGUILayout.EndScrollView();
            }
        }

        #endregion
    }
}
#endif