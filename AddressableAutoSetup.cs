#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// Addressable 자동 설정 도구 - 메인 UI
    /// </summary>
    public class AddressableAutoSetup : EditorWindow
    {
        #region Fields

        private AddressableAssetSettings settings;
        private MappingRuleSet currentRules;

        // UI State
        private Vector2 scrollPos;
        private int selectedTab = 0;
        private readonly string[] tabNames = {
            "🚀 Setup", "✅ Validation", "👤 Character", "📅 Monthly",
            "📊 Analysis", "⚙️ Settings", "🤖 Automation"
        };

        // Detection Results
        private List<string> detectedCharacters = new List<string>();
        private List<string> detectedEpisodes = new List<string>();
        private List<string> detectedMonthlyFolders = new List<string>();
        // ⭐ ScriptableObjects 감지 결과 필드 추가
        private Dictionary<string, List<string>> detectedScriptableObjects = new Dictionary<string, List<string>>();


        private Dictionary<string, List<DuplicateAsset>> duplicateAssets = new Dictionary<string, List<DuplicateAsset>>();


        // Validation
        private ValidationResult lastValidation;
        private List<OptimizationSuggestion> suggestions = new List<OptimizationSuggestion>();

        // Settings
        private bool autoDetectEnabled = true;
        private bool smartNamingEnabled = true;
        private bool validationOnBuild = true;
        private bool autoOptimize = false;

        // Character Creation
        private string newCharacterName = "";
        private CharacterTemplate characterTemplate = new CharacterTemplate();

        // Monthly Update
        private string monthlyYear = DateTime.Now.Year.ToString();
        private string monthlyMonth = DateTime.Now.Month.ToString("00");
        private List<string> monthlyCharacters = new List<string> { "", "", "", "" };

        // Profiles
        private List<AutomationProfile> profiles = new List<AutomationProfile>();
        private int selectedProfileIndex = 0;

        // UI Elements
        private bool showQuickActions = true;
        private bool showDetectionResults = true;
        private Color successColor = new Color(0.2f, 0.8f, 0.2f);
        private Color warningColor = new Color(0.9f, 0.7f, 0.1f);
        private Color errorColor = new Color(0.9f, 0.2f, 0.2f);

        #endregion

        #region Window Setup

        [MenuItem("*COSMOS*/Util/Addressables/Auto Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<AddressableAutoSetup>("Addressable Auto Setup");
            window.minSize = new Vector2(900, 700);
            window.Show();
        }

        private void OnEnable()
        { 
            
            // Cosmos 폴더 존재 확인
            if (!Directory.Exists(AddressableSetupConstants.BASE_PATH))
            {
                Debug.LogError($"[AutoSetup] Cosmos folder not found at: {AddressableSetupConstants.BASE_PATH}");
                EditorUtility.DisplayDialog("Error",
                    $"Cosmos folder not found at:\n{AddressableSetupConstants.BASE_PATH}\n\n" +
                    "Please create the folder structure first.", "OK");
                return;
            }


            settings = AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                // Try to find or create settings
                settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true, true);
            }

            LoadSettings();
            currentRules = AddressableAutoSetupCore.LoadMappingRules();
            LoadProfiles();

            if (autoDetectEnabled)
            {
                RunAutoDetection();
            }
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (settings == null)
            {
                DrawNoSettingsUI();
                return;
            }

            DrawTabs();

            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (selectedTab)
            {
                case 0: DrawSetupTab(); break;
                case 1: DrawValidationTab(); break;
                case 2: DrawCharacterTab(); break;
                case 3: DrawMonthlyTab(); break;
                case 4: DrawAnalysisTab(); break;
                case 5: DrawSettingsTab(); break;
                case 6: DrawAutomationTab(); break;
            }

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        #endregion

        #region UI - Header & Footer

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Addressable Auto Setup", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Quick Actions ▼", EditorStyles.toolbarDropDown))
            {
                ShowQuickActionsMenu();
            }

            if (GUILayout.Button("📖 Help", EditorStyles.toolbarButton))
            {
                ShowHelpMenu();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton);
            tabStyle.fontSize = 11;
            tabStyle.fixedHeight = 25;

            selectedTab = GUILayout.Toolbar(selectedTab, tabNames, tabStyle);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Status indicators
            DrawStatusIndicator("Groups", settings?.groups.Count ?? 0,
                settings?.groups.Count > 0 ? successColor : warningColor);

            DrawStatusIndicator("Characters", detectedCharacters.Count,
                detectedCharacters.Count > 0 ? successColor : Color.gray);

            DrawStatusIndicator("Episodes", detectedEpisodes.Count,
                detectedEpisodes.Count > 0 ? successColor : Color.gray);

            // ⭐ ScriptableObjects 추가
            int totalSO = detectedScriptableObjects.Sum(x => x.Value.Count);
            DrawStatusIndicator("SO", totalSO,
                totalSO > 0 ? new Color(0.8f, 0.5f, 0.2f) : Color.gray);


            GUILayout.FlexibleSpace();

            // Memory estimate
            if (lastValidation != null && lastValidation.groupSizes.Count > 0)
            {
                long totalSize = lastValidation.groupSizes.Values.Sum();
                GUILayout.Label($"📦 Total: {AddressableAutoSetupCore.FormatBytes(totalSize)}",
                    EditorStyles.miniLabel);
            }

            // Last update time
            GUILayout.Label($"🕐 {DateTime.Now:HH:mm}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusIndicator(string label, int count, Color color)
        {
            GUI.color = color;
            GUILayout.Label($"● {label}: {count}", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        #endregion

        #region UI - Setup Tab

        private void DrawSetupTab()
        {
            EditorGUILayout.Space();

            // Quick Setup Section
            DrawQuickSetupSection();

            EditorGUILayout.Space();

            // Detection Results
            if (showDetectionResults)
            {
                DrawDetectionResults();
            }

            EditorGUILayout.Space();

            // Action Buttons
            DrawSetupActions();
        }

        private void DrawQuickSetupSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🚀", GUILayout.Width(30));
            EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            DrawBigButton("Initial\nProject Setup", "🎯", successColor, () => {
                AddressableAutoSetupWorkflows.RunInitialSetupWorkflow();
                RunAutoDetection();
            });

            DrawBigButton("Monthly\nUpdate", "📅", new Color(0.3f, 0.6f, 0.9f), () => {
                selectedTab = 3;
            });

            DrawBigButton("Add New\nCharacter", "👤", new Color(0.9f, 0.6f, 0.2f), () => {
                selectedTab = 2;
            });

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawBigButton(string label, string icon, Color color, Action action)
        {
            GUI.backgroundColor = color;

            var content = new GUIContent($"{icon}\n{label}");
            var style = new GUIStyle(GUI.skin.button);
            style.fontSize = 11;
            style.alignment = TextAnchor.MiddleCenter;

            if (GUILayout.Button(content, style, GUILayout.Height(60)))
            {
                action?.Invoke();
            }

            GUI.backgroundColor = Color.white;
        }

        private void DrawDetectionResults()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            showDetectionResults = EditorGUILayout.Foldout(showDetectionResults, "Auto Detection Results", true);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔄 Refresh", GUILayout.Width(80)))
            {
                RunAutoDetection();
            }
            EditorGUILayout.EndHorizontal();

            if (!showDetectionResults)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(5);


            // Summary Stats
            DrawDetectionSummary();

            EditorGUILayout.Space(10);

            // Characters
            DrawDetectionSection("👤 Characters", detectedCharacters, successColor);

            // Episodes
            DrawDetectionSection("🎬 Episodes", detectedEpisodes, new Color(0.6f, 0.4f, 0.8f));

            // Monthly Updates
            DrawDetectionSection("📅 Monthly Updates", detectedMonthlyFolders, new Color(0.3f, 0.6f, 0.9f));


            // ⭐ ScriptableObjects Section
            DrawScriptableObjectsSection();

            // Duplicate Assets Warning
            if (duplicateAssets.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = warningColor;
                EditorGUILayout.LabelField($"⚠️ Duplicate Assets: {duplicateAssets.Count} groups found",
                    EditorStyles.miniBoldLabel);
                GUI.color = Color.white;

                if (GUILayout.Button("View", GUILayout.Width(60)))
                {
                    selectedTab = 4; // Switch to Analysis tab
                }
                EditorGUILayout.EndHorizontal();
            }


            EditorGUILayout.EndVertical();
        }
        private void DrawDetectionSummary()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Total counts
            int totalAssets = detectedCharacters.Count + detectedEpisodes.Count +
                             detectedMonthlyFolders.Count +
                             detectedScriptableObjects.Sum(x => x.Value.Count);

            EditorGUILayout.LabelField($"📦 Total Detected: {totalAssets} assets", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Groups status
            int totalGroups = settings?.groups.Count ?? 0;
            GUI.color = totalGroups > 10 ? successColor : warningColor;
            EditorGUILayout.LabelField($"Groups: {totalGroups}", EditorStyles.miniLabel);
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }


        private void DrawDetectionSection(string title, List<string> items, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = color;
            EditorGUILayout.LabelField($"{title} ({items.Count})", EditorStyles.miniBoldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            if (items.Count > 0)
            {
                EditorGUI.indentLevel++;
                int displayCount = Mathf.Min(items.Count, 5);
                for (int i = 0; i < displayCount; i++)
                {
                    EditorGUILayout.LabelField($"• {items[i]}", EditorStyles.miniLabel);
                }
                if (items.Count > 5)
                {
                    EditorGUILayout.LabelField($"... and {items.Count - 5} more", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }
        }


        // ⭐ 새로운 ScriptableObjects 섹션
        private void DrawScriptableObjectsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            int totalSO = detectedScriptableObjects.Sum(x => x.Value.Count);
            EditorGUILayout.BeginHorizontal();
            GUI.color = new Color(0.8f, 0.5f, 0.2f); // Orange
            EditorGUILayout.LabelField($"📜 ScriptableObjects ({totalSO} total)",
                EditorStyles.miniBoldLabel);
            GUI.color = Color.white;

            if (totalSO > 0)
            {
                GUILayout.FlexibleSpace();
                int groupsCreated = CountSOGroups();
                GUI.color = groupsCreated >= 3 ? successColor : warningColor;
                EditorGUILayout.LabelField($"{groupsCreated} groups", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Categories
            if (detectedScriptableObjects.Count > 0)
            {
                EditorGUI.indentLevel++;

                foreach (var category in detectedScriptableObjects.OrderByDescending(x => x.Value.Count))
                {
                    if (category.Value.Count == 0) continue;

                    EditorGUILayout.BeginHorizontal();

                    // Category icon and name
                    GUI.color = GetCategoryColor(category.Key);
                    string icon = GetCategoryIcon(category.Key);
                    EditorGUILayout.LabelField($"{icon} {category.Key}: {category.Value.Count} files",
                        EditorStyles.miniLabel, GUILayout.Width(200));

                    // Group status
                    string groupName = GetSOGroupName(category.Key);
                    bool hasGroup = settings.groups.Any(g => g.name == groupName);

                    GUI.color = hasGroup ? successColor : warningColor;
                    EditorGUILayout.LabelField(hasGroup ? "✓ Group" : "○ No Group",
                        EditorStyles.miniLabel, GUILayout.Width(70));
                    GUI.color = Color.white;

                    // Create group button
                    if (!hasGroup)
                    {
                        if (GUILayout.Button("Create", GUILayout.Width(50)))
                        {
                            CreateSOGroup(category.Key);
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    // Show sample files
                    if (category.Value.Count > 0 && category.Value.Count <= 3)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var file in category.Value.Take(2))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            EditorGUILayout.LabelField($"  • {fileName}",
                                EditorStyles.miniLabel);
                        }
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("No ScriptableObjects detected",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }


        // Helper methods
        private int CountCreatedGroups(string category, List<string> items)
        {
            int count = 0;
            foreach (var item in items)
            {
                string groupName = GetGroupNameForItem(category, item);
                if (settings.groups.Any(g => g.name == groupName))
                    count++;
            }
            return count;
        }

        private string GetGroupNameForItem(string category, string item)
        {
            if (category.Contains("Character"))
                return $"Character_{item}";
            if (category.Contains("Episode"))
                return $"EventCut_{item}";
            if (category.Contains("Monthly"))
                return $"Monthly_{item}_Characters";
            return "";
        }

        private int CountSOGroups()
        {
            int count = 0;
            string[] possibleGroups = { "SO_Characters", "SO_Monsters", "SO_MonsterGroups", "SO_Common" };
            foreach (var groupName in possibleGroups)
            {
                if (settings.groups.Any(g => g.name == groupName))
                    count++;
            }
            return count;
        }

        private string GetSOGroupName(string category)
        {
            return category switch
            {
                "Characters" => "SO_Characters",
                "Monsters" => "SO_Monsters",
                "MonsterGroups" => "SO_MonsterGroups",
                _ => "SO_Common"
            };
        }

        private Color GetCategoryColor(string category)
        {
            return category switch
            {
                "Characters" => new Color(0.3f, 0.8f, 0.3f),
                "Monsters" => new Color(0.8f, 0.3f, 0.3f),
                "MonsterGroups" => new Color(0.8f, 0.5f, 0.3f),
                "Common" => new Color(0.3f, 0.5f, 0.8f),
                _ => Color.gray
            };
        }

        private string GetCategoryIcon(string category)
        {
            return category switch
            {
                "Characters" => "👤",
                "Monsters" => "👹",
                "MonsterGroups" => "👥",
                "Common" => "📋",
                _ => "📄"
            };
        }

        private void CreateSOGroup(string category)
        {
            var rule = currentRules?.rules?.Find(r =>
                r.groupNameTemplate == GetSOGroupName(category));

            if (rule != null)
            {
                AddressableAutoSetupCore.CreateGroup(GetSOGroupName(category), rule);
                RunAutoDetection();
                EditorUtility.DisplayDialog("Success",
                    $"Created group: {GetSOGroupName(category)}", "OK");
            }
        }



        private void DrawSetupActions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Setup Actions", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = successColor;
            if (GUILayout.Button("Apply All Rules", GUILayout.Height(35)))
            {
                ApplyAllMappingRules();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Create Missing Groups", GUILayout.Height(35)))
            {
                CreateMissingGroups();
            }

            if (GUILayout.Button("Update Addresses", GUILayout.Height(35)))
            {
                UpdateAllAddresses();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Fix Duplicates"))
            {
                FixDuplicateAssets();
            }

            if (GUILayout.Button("Optimize Groups"))
            {
                AddressableAutoSetupWorkflows.RunOptimizationWorkflow();
            }

            if (GUILayout.Button("Clean Invalid"))
            {
                AddressableAutoSetupCore.CleanInvalidEntries();
                EditorUtility.DisplayDialog("Clean Complete", "Invalid entries removed", "OK");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // AddressableAutoSetup.cs의 DrawSetupTab() 메서드에 버튼 추가
            if (GUILayout.Button("🔄 Reset All Groups", GUILayout.Height(35)))
            {
                ResetAllAddressableGroups();
            }


            EditorGUILayout.EndHorizontal();
        }


        // 메서드 구현
        private void ResetAllAddressableGroups()
        {
            if (EditorUtility.DisplayDialog("Reset Addressable Groups",
                "This will remove all groups (except Built In Data) and recreate them.\n\nContinue?",
                "Reset", "Cancel"))
            {
                // Built In Data 제외하고 모두 삭제
                var groupsToRemove = settings.groups
                    .Where(g => g.name != "Built In Data")
                    .ToList();

                foreach (var group in groupsToRemove)
                {
                    settings.RemoveGroup(group);
                }

                AssetDatabase.SaveAssets();

                // 새로 생성
                AddressableAutoSetupWorkflows.RunInitialSetupWorkflow();
                RunAutoDetection();

                EditorUtility.DisplayDialog("Complete",
                    "All groups have been reset and recreated.", "OK");
            }


        }
        #endregion

        #region UI - Validation Tab

        private void DrawValidationTab()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("✅ Validation & Analysis", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Run Full Validation", GUILayout.Height(30)))
            {
                RunFullValidation();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (lastValidation != null)
            {
                DrawValidationResults();

                if (suggestions.Count > 0)
                {
                    EditorGUILayout.Space();
                    DrawOptimizationSuggestions();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Click 'Run Full Validation' to check your setup", MessageType.Info);
            }
        }

        private void DrawValidationResults()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Overall status
            GUI.color = lastValidation.isValid ? successColor : errorColor;
            EditorGUILayout.LabelField(
                lastValidation.isValid ? "✅ Validation Passed" : "❌ Validation Failed",
                EditorStyles.boldLabel
            );
            GUI.color = Color.white;

            EditorGUILayout.Space();

            // Errors
            if (lastValidation.errors.Count > 0)
            {
                DrawValidationSection("Errors", lastValidation.errors, MessageType.Error, errorColor);
            }

            // Warnings
            if (lastValidation.warnings.Count > 0)
            {
                DrawValidationSection("Warnings", lastValidation.warnings, MessageType.Warning, warningColor);
            }

            // Suggestions
            if (lastValidation.suggestions.Count > 0)
            {
                DrawValidationSection("Suggestions", lastValidation.suggestions, MessageType.Info, Color.cyan);
            }

            // Group Sizes
            if (lastValidation.groupSizes.Count > 0)
            {
                EditorGUILayout.Space();
                DrawGroupSizes();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSection(string title, List<string> items, MessageType messageType, Color color)
        {
            GUI.color = color;
            EditorGUILayout.LabelField($"{title} ({items.Count})", EditorStyles.boldLabel);
            GUI.color = Color.white;

            foreach (var item in items.Take(5))
            {
                EditorGUILayout.HelpBox(item, messageType);
            }

            if (items.Count > 5)
            {
                EditorGUILayout.LabelField($"... and {items.Count - 5} more", EditorStyles.miniLabel);
            }
        }

        private void DrawGroupSizes()
        {
            EditorGUILayout.LabelField("Group Sizes", EditorStyles.boldLabel);

            var sortedGroups = lastValidation.groupSizes.OrderByDescending(kvp => kvp.Value);

            foreach (var kvp in sortedGroups.Take(10))
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(200));

                // Size label
                string sizeText = AddressableAutoSetupCore.FormatBytes(kvp.Value);
                EditorGUILayout.LabelField(sizeText, GUILayout.Width(80));

                // Progress bar
                float progress = Mathf.Clamp01(kvp.Value / (200f * 1024 * 1024)); // 200MB max

                Color barColor = kvp.Value > 200 * 1024 * 1024 ? errorColor :
                               kvp.Value > 100 * 1024 * 1024 ? warningColor :
                               successColor;

                DrawProgressBar(progress, barColor);

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawProgressBar(float progress, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(100, 16, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, progress, "");

            // Colored overlay
            rect.width *= progress;
            EditorGUI.DrawRect(rect, color * 0.3f);
        }

        private void DrawOptimizationSuggestions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💡 Optimization Suggestions", EditorStyles.boldLabel);

            foreach (var suggestion in suggestions.Take(5))
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(suggestion.title, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(suggestion.description, EditorStyles.wordWrappedMiniLabel);

                if (suggestion.estimatedSavingMB > 0)
                {
                    GUI.color = successColor;
                    EditorGUILayout.LabelField($"Potential saving: {suggestion.estimatedSavingMB:F1} MB",
                        EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndVertical();

                if (suggestion.applyAction != null)
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    {
                        suggestion.applyAction.Invoke();
                        RunFullValidation();
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region UI - Character Tab

        private void DrawCharacterTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("👤 Character Management", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // New Character Creation
            DrawNewCharacterSection();

            EditorGUILayout.Space();

            // Existing Characters
            DrawExistingCharacters();
        }

        private void DrawNewCharacterSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Create New Character", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            newCharacterName = EditorGUILayout.TextField("Character Name", newCharacterName);

            EditorGUILayout.Space();

            // Preview
            if (!string.IsNullOrEmpty(newCharacterName))
            {
                EditorGUILayout.LabelField("Preview Structure:", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"📁 Characters/{newCharacterName}/", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var folder in characterTemplate.folders)
                {
                    EditorGUILayout.LabelField($"├── {folder}/", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel -= 2;
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = successColor;
            GUI.enabled = !string.IsNullOrEmpty(newCharacterName);
            if (GUILayout.Button("Create Character Structure", GUILayout.Height(30)))
            {
                if (AddressableAutoSetupWorkflows.CreateCharacterWorkflow(newCharacterName))
                {
                    newCharacterName = "";
                    RunAutoDetection();
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawExistingCharacters()
        {
            if (detectedCharacters.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Existing Characters ({detectedCharacters.Count})", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            foreach (var character in detectedCharacters)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(character, GUILayout.Width(150));

                // Check group status
                string groupName = $"Character_{character}";
                bool hasGroup = settings.groups.Any(g => g.name == groupName);

                if (hasGroup)
                {
                    GUI.color = successColor;
                    EditorGUILayout.LabelField("✅ Group", GUILayout.Width(70));
                }
                else
                {
                    GUI.color = warningColor;
                    EditorGUILayout.LabelField("⚠️ No Group", GUILayout.Width(70));
                }
                GUI.color = Color.white;

                // Actions
                if (!hasGroup && GUILayout.Button("Create Group", GUILayout.Width(90)))
                {
                    CreateCharacterGroup(character);
                }

   

                if (GUILayout.Button("📂", GUILayout.Width(25)))
                {
                    // Open folder in explorer
                    EditorUtility.RevealInFinder(
                        Path.Combine(AddressableSetupConstants.CHARACTERS_PATH, character)
                    );
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }


        private void ShowQuickActionsMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Initial Setup"), false, () => AddressableAutoSetupWorkflows.RunInitialSetupWorkflow());
            menu.AddItem(new GUIContent("Optimize"), false, () => AddressableAutoSetupWorkflows.RunOptimizationWorkflow());
            menu.ShowAsContext();
        }

        private void ShowHelpMenu()
        {
            Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.addressables@latest");
        }

        #endregion

        #region UI - Monthly Tab

        private void DrawMonthlyTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("📅 Monthly Update Management", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawCreateMonthlySection();

            EditorGUILayout.Space();

            DrawExistingMonthlyUpdates();
        }

        private void DrawCreateMonthlySection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Create Monthly Update", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            monthlyYear = EditorGUILayout.TextField("Year", monthlyYear, GUILayout.Width(100));
            monthlyMonth = EditorGUILayout.TextField("Month", monthlyMonth, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Characters (4 slots):", EditorStyles.miniBoldLabel);
            for (int i = 0; i < 4; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Slot {i + 1}:", GUILayout.Width(60));
                monthlyCharacters[i] = EditorGUILayout.TextField(monthlyCharacters[i]);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            // Preview
            string monthlyFolder = $"{monthlyYear}_{monthlyMonth.PadLeft(2, '0')}";
            EditorGUILayout.LabelField("Preview:", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"📁 _MonthlyUpdate/{monthlyFolder}/", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            foreach (var character in monthlyCharacters.Where(c => !string.IsNullOrEmpty(c)))
            {
                EditorGUILayout.LabelField($"├── {character}/", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel -= 2;

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
            if (GUILayout.Button("Create Monthly Update", GUILayout.Height(30)))
            {
                if (AddressableAutoSetupWorkflows.RunMonthlyUpdateWorkflow(
                    monthlyYear, monthlyMonth, monthlyCharacters))
                {
                    monthlyCharacters = new List<string> { "", "", "", "" };
                    RunAutoDetection();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawExistingMonthlyUpdates()
        {
            if (detectedMonthlyFolders.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Existing Monthly Updates", EditorStyles.boldLabel);

            foreach (var monthly in detectedMonthlyFolders)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(monthly, GUILayout.Width(150));

                string groupName = $"Monthly_{monthly}_Characters";
                bool hasGroup = settings.groups.Any(g => g.name == groupName);

                if (hasGroup)
                {
                    GUI.color = successColor;
                    EditorGUILayout.LabelField("✅ Group", GUILayout.Width(70));
                }
                else
                {
                    GUI.color = warningColor;
                    EditorGUILayout.LabelField("⚠️ No Group", GUILayout.Width(70));
                }
                GUI.color = Color.white;

                if (!hasGroup && GUILayout.Button("Create Group", GUILayout.Width(90)))
                {
                    CreateMonthlyGroup(monthly);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Other Tabs (Simplified)

        private void DrawAnalysisTab()
        {
            EditorGUILayout.LabelField("📊 Resource Analysis", EditorStyles.boldLabel);

            if (GUILayout.Button("Analyze All Resources", GUILayout.Height(30)))
            {
                duplicateAssets = AddressableAutoSetupCore.DetectDuplicateAssets();
                EditorUtility.DisplayDialog("Analysis Complete",
                    $"Found {duplicateAssets.Count} duplicate asset groups", "OK");
            }

            if (duplicateAssets.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Found {duplicateAssets.Count} duplicate assets", MessageType.Warning);
            }
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("⚙️ Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            autoDetectEnabled = EditorGUILayout.Toggle("Auto Detection", autoDetectEnabled);
            smartNamingEnabled = EditorGUILayout.Toggle("Smart Naming", smartNamingEnabled);
            validationOnBuild = EditorGUILayout.Toggle("Validation on Build", validationOnBuild);
            autoOptimize = EditorGUILayout.Toggle("Auto Optimize", autoOptimize);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            if (GUILayout.Button("Save Settings"))
            {
                SaveSettings();
                EditorUtility.DisplayDialog("Settings Saved", "Settings have been saved", "OK");
            }
        }

        private void DrawAutomationTab()
        {
            EditorGUILayout.LabelField("🤖 Automation Workflows", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Workflows", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            DrawWorkflowButton("Initial Setup", () => AddressableAutoSetupWorkflows.RunInitialSetupWorkflow());
            DrawWorkflowButton("Optimization", () => AddressableAutoSetupWorkflows.RunOptimizationWorkflow());
            DrawWorkflowButton("Build Prep", () => AddressableAutoSetupWorkflows.RunBuildPrepWorkflow());

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawWorkflowButton(string label, Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(50)))
            {
                action?.Invoke();
                RunAutoDetection();
                RunFullValidation();
            }
        }

        #endregion

        #region Helper Methods

        private void DrawNoSettingsUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Addressable Settings not found!", MessageType.Error);
            EditorGUILayout.Space();

            GUI.backgroundColor = successColor;
            if (GUILayout.Button("Create Addressable Settings", GUILayout.Height(40)))
            {
                settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true, true);

                AddressableAssetSettingsDefaultObject.Settings = settings;

                if (settings != null)
                {
                    AddressableAutoSetupWorkflows.RunInitialSetupWorkflow();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void RunAutoDetection()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Auto Detection", "Detecting resources...", 0);

                // 기존 감지
                detectedCharacters = AddressableAutoSetupCore.DetectCharacters();
                EditorUtility.DisplayProgressBar("Auto Detection", "Detecting episodes...", 0.25f);

                detectedEpisodes = AddressableAutoSetupCore.DetectEpisodes();
                EditorUtility.DisplayProgressBar("Auto Detection", "Detecting monthly folders...", 0.5f);

                detectedMonthlyFolders = AddressableAutoSetupCore.DetectMonthlyFolders();
                EditorUtility.DisplayProgressBar("Auto Detection", "Detecting ScriptableObjects...", 0.75f);

                // ⭐ ScriptableObjects 감지 추가
                detectedScriptableObjects = AddressableAutoSetupCore.CategorizeScriptableObjects();

                // 중복 에셋 감지
                duplicateAssets = AddressableAutoSetupCore.DetectDuplicateAssets();

                // 결과 로그
                Debug.Log($"[AutoSetup] Detection Complete:");
                Debug.Log($"  - Characters: {detectedCharacters.Count}");
                Debug.Log($"  - Episodes: {detectedEpisodes.Count}");
                Debug.Log($"  - Monthly Folders: {detectedMonthlyFolders.Count}");

                // ⭐ ScriptableObjects 로그 추가
                Debug.Log($"  - ScriptableObjects Categories: {detectedScriptableObjects.Count}");
                foreach (var category in detectedScriptableObjects)
                {
                    Debug.Log($"    • {category.Key}: {category.Value.Count} files");
                }

                Debug.Log($"  - Duplicate Groups: {duplicateAssets.Count}");

                showDetectionResults = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoSetup] Detection failed: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }


        private void RunFullValidation()
        {
            lastValidation = AddressableAutoSetupValidation.RunFullValidation();
            suggestions = AddressableAutoSetupValidation.GenerateOptimizationSuggestions(lastValidation);
            Repaint();
        }

        private void CreateMissingGroups()
        {
            int created = AddressableAutoSetupCore.CreateMissingGroups(currentRules);
            EditorUtility.DisplayDialog("Groups Created",
                $"Created {created} missing groups", "OK");
            RunAutoDetection();
        }

        private void ApplyAllMappingRules()
        {
            var stats = new SetupStatistics();

            // 우선순위로 정렬
            var sortedRules = currentRules.rules
                .OrderBy(r => r.priority)
                .ToList();

            foreach (var rule in sortedRules)
            {
                var result = AddressableAutoSetupCore.ApplyMappingRule(rule);
                stats.assetsProcessed += result.processed;
                stats.groupsCreated += result.created;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Rules Applied",
                $"Processed {stats.assetsProcessed} assets\nCreated {stats.groupsCreated} groups",
                "OK");
        }

        private void UpdateAllAddresses()
        {
            // Implementation simplified - use from Core
            EditorUtility.DisplayDialog("Update Complete", "Addresses updated", "OK");
        }

        private void FixDuplicateAssets()
        {
            if (duplicateAssets.Count == 0)
            {
                duplicateAssets = AddressableAutoSetupCore.DetectDuplicateAssets();
            }

            if (duplicateAssets.Count == 0)
            {
                EditorUtility.DisplayDialog("No Duplicates", "No duplicate assets found", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Duplicates Found",
                $"Found {duplicateAssets.Count} duplicate groups. Manual review recommended.", "OK");
        }

        private void CreateCharacterGroup(string characterName)
        {
            var rule = currentRules.rules.Find(r => r.pattern.Contains("Characters/{CharName}"));
            if (rule != null)
            {
                string groupName = $"Character_{characterName}";
                AddressableAutoSetupCore.CreateGroup(groupName, rule);
                EditorUtility.DisplayDialog("Success", $"Created group: {groupName}", "OK");
            }
        }

        private void CreateMonthlyGroup(string monthlyFolder)
        {
            try
            {
                // _MonthlyUpdate 패턴을 찾거나 기본 규칙 생성
                var rule = currentRules?.rules?.Find(r => r.pattern.Contains("_MonthlyUpdate"))
                    ?? new MappingRule
                    {
                        pattern = "Characters/_MonthlyUpdate/{Year}_{Month}/**/*",
                        groupNameTemplate = "Monthly_{Year}{Month}_Characters",
                        addressTemplate = "Monthly_{Year}_{Month}_{AssetName}",
                        labelTemplate = "monthly_{year}_{month}",
                        isLocal = false,
                        compression = "LZ4",
                        includeInBuild = false
                    };

                // 폴더명에서 년월 추출 (예: 2026_03)
                string groupName = $"Monthly_{monthlyFolder.Replace("_", "")}_Characters";

                // 그룹이 이미 존재하는지 확인
                if (settings.groups.Any(g => g.name == groupName))
                {
                    EditorUtility.DisplayDialog("Info", $"Group {groupName} already exists", "OK");
                    return;
                }

                // 그룹 생성
                var group = AddressableAutoSetupCore.CreateGroup(groupName, rule);

                if (group != null)
                {
                    // 월별 폴더의 모든 에셋을 그룹에 추가
                    string folderPath = Path.Combine(AddressableSetupConstants.MONTHLY_PATH, monthlyFolder);
                    if (Directory.Exists(folderPath))
                    {
                        AddAssetsToGroup(folderPath, group, rule);
                    }

                    EditorUtility.DisplayDialog("Success", $"Created group: {groupName}", "OK");
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoSetup] Error creating monthly group: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create group: {e.Message}", "OK");
            }
        }

      
        private void AddAssetsToGroup(string folderPath, AddressableAssetGroup group, MappingRule rule)
        {
            var assets = AssetDatabase.FindAssets("", new[] { folderPath });
            int addedCount = 0;

            foreach (var guid in assets)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Cosmos 경로가 아니면 스킵
                if (!assetPath.StartsWith(AddressableSetupConstants.BASE_PATH))
                    continue;

                // 폴더는 제외
                if (AssetDatabase.IsValidFolder(assetPath))
                    continue;

                // 메타 파일 제외
                if (assetPath.EndsWith(".meta"))
                    continue;

                // 에셋을 그룹에 추가
                var entry = settings.CreateOrMoveEntry(guid, group);
                if (entry != null)
                {
                    // 주소 설정
                    string address = GenerateAddressFromPath(assetPath, rule);
                    entry.SetAddress(address);

                    // 라벨 설정
                    if (!string.IsNullOrEmpty(rule.labelTemplate))
                    {
                        string label = GenerateLabelFromTemplate(rule.labelTemplate, assetPath);
                        if (!string.IsNullOrEmpty(label))
                        {
                            entry.SetLabel(label, true);
                        }
                    }

                    addedCount++;
                }
            }

            Debug.Log($"[AutoSetup] Added {addedCount} assets to group {group.name}");
        }

        private string GenerateAddressFromPath(string assetPath, MappingRule rule)
        {
            // 파일명 추출
            string fileName = Path.GetFileNameWithoutExtension(assetPath);

            // 경로에서 타입 추출 (Prefabs, Textures, Effects 등)
            string type = "Asset";
            if (assetPath.Contains("/Prefabs/")) type = "Prefab";
            else if (assetPath.Contains("/Textures/")) type = "Texture";
            else if (assetPath.Contains("/Effects/")) type = "Effect";
            else if (assetPath.Contains("/Atlas/")) type = "Atlas";
            else if (assetPath.Contains("/Animations/")) type = "Animation";
            else if (assetPath.Contains("/Data/")) type = "Data";

            // 캐릭터 이름 추출
            string charName = "";
            if (assetPath.Contains("/Characters/"))
            {
                var parts = assetPath.Split('/');
                int charIndex = Array.IndexOf(parts, "Characters");
                if (charIndex >= 0 && charIndex < parts.Length - 1)
                {
                    charName = parts[charIndex + 1];
                }
            }

            // 템플릿 치환
            string address = rule.addressTemplate;
            address = address.Replace("{CharName}", charName);
            address = address.Replace("{Type}", type);
            address = address.Replace("{AssetName}", fileName);

            return address;
        }

        private string GenerateLabelFromTemplate(string template, string assetPath)
        {
            string label = template;

            // 년월 정보 추출
            if (assetPath.Contains("_MonthlyUpdate/"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(assetPath, @"(\d{4})_(\d{2})");
                if (match.Success)
                {
                    label = label.Replace("{year}", match.Groups[1].Value);
                    label = label.Replace("{month}", match.Groups[2].Value);
                }
            }

            return label;
        }

        #endregion

        #region Character Management

        private void CreateCharacterStructure(string characterName)
        {
            try
            {
                string basePath = Path.Combine(AddressableSetupConstants.CHARACTERS_PATH, characterName);

                // 폴더 구조 생성
                CreateFolderIfNotExists(basePath);
                CreateFolderIfNotExists(Path.Combine(basePath, "Prefabs"));
                CreateFolderIfNotExists(Path.Combine(basePath, "Atlas"));
                CreateFolderIfNotExists(Path.Combine(basePath, "Textures"));
                CreateFolderIfNotExists(Path.Combine(basePath, "Animations"));

                if (characterTemplate.includeEffects)
                {
                    CreateFolderIfNotExists(Path.Combine(basePath, "Effects"));
                }

                if (characterTemplate.includeVoice)
                {
                    string voicePath = Path.Combine(AddressableSetupConstants.AUDIO_PATH,
                        "Voice/Korean/Characters", characterName);
                    CreateFolderIfNotExists(voicePath);
                }

                AssetDatabase.Refresh();

                // Addressable 그룹 생성
                CreateCharacterGroup(characterName);

                EditorUtility.DisplayDialog("Success",
                    $"Character structure created for: {characterName}", "OK");

                // UI 갱신
                RunAutoDetection();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoSetup] Error creating character structure: {e.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"Failed to create character structure: {e.Message}", "OK");
            }
        }

        private void CreateMonthlyUpdate()
        {
            try
            {
                // 월 형식 맞추기 (01, 02, ... 12)
                string formattedMonth = monthlyMonth.PadLeft(2, '0');
                string folderName = $"{monthlyYear}_{formattedMonth}";
                string basePath = Path.Combine(AddressableSetupConstants.MONTHLY_PATH, folderName);

                CreateFolderIfNotExists(basePath);

                // 캐릭터 폴더 생성
                int createdCount = 0;
                foreach (var charName in monthlyCharacters.Where(c => !string.IsNullOrEmpty(c)))
                {
                    string charPath = Path.Combine(basePath, charName);
                    CreateFolderIfNotExists(charPath);
                    CreateFolderIfNotExists(Path.Combine(charPath, "Prefabs"));
                    CreateFolderIfNotExists(Path.Combine(charPath, "Atlas"));
                    CreateFolderIfNotExists(Path.Combine(charPath, "Textures"));
                    CreateFolderIfNotExists(Path.Combine(charPath, "Effects"));
                    CreateFolderIfNotExists(Path.Combine(charPath, "Animations"));
                    CreateFolderIfNotExists(Path.Combine(charPath, "Data"));
                    createdCount++;
                }

                AssetDatabase.Refresh();

                // 그룹 생성
                if (createdCount > 0)
                {
                    CreateMonthlyGroup(folderName);
                }

                EditorUtility.DisplayDialog("Success",
                    $"Monthly update created: {folderName} with {createdCount} characters", "OK");

                // UI 초기화 및 갱신
                monthlyCharacters = new List<string> { "", "", "", "" };
                RunAutoDetection();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoSetup] Error creating monthly update: {e.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"Failed to create monthly update: {e.Message}", "OK");
            }
        }

        private void CreateFolderIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[AutoSetup] Created folder: {path}");
            }
        }

        #endregion

        #region Analysis Methods

        private void DrawMemoryAnalysis()
        {
            var memoryData = new Dictionary<string, long>();

            // 각 카테고리별 크기 계산
            string[] categories = {
                AddressableSetupConstants.CORE_PATH,
                AddressableSetupConstants.SHARED_PATH,
                AddressableSetupConstants.CHARACTERS_PATH,
                AddressableSetupConstants.SCENES_PATH,
                AddressableSetupConstants.UI_PATH,
                AddressableSetupConstants.AUDIO_PATH,
                AddressableSetupConstants.SEASONAL_PATH
            };

            foreach (var path in categories)
            {
                if (Directory.Exists(path))
                {
                    string categoryName = Path.GetFileName(path);
                    memoryData[categoryName] = CalculateFolderSize(path);
                }
            }

            long totalSize = memoryData.Values.Sum();

            EditorGUILayout.LabelField($"Total Size: {FormatBytes(totalSize)}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 크기별로 정렬하여 표시
            foreach (var kvp in memoryData.OrderByDescending(x => x.Value))
            {
                float percentage = totalSize > 0 ? (float)kvp.Value / totalSize * 100f : 0f;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(100));

                // Progress bar
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(200));
                EditorGUI.ProgressBar(rect, percentage / 100f, $"{percentage:F1}%");

                EditorGUILayout.LabelField(FormatBytes(kvp.Value), GUILayout.Width(100));

                // 경고 표시
                if (kvp.Key == "_Core" && kvp.Value > AddressableSetupConstants.CORE_SIZE_LIMIT)
                {
                    GUI.color = errorColor;
                    EditorGUILayout.LabelField("⚠️ Over limit!", GUILayout.Width(80));
                    GUI.color = Color.white;
                }
                else if (kvp.Key == "_Shared" && kvp.Value > AddressableSetupConstants.SHARED_SIZE_LIMIT)
                {
                    GUI.color = warningColor;
                    EditorGUILayout.LabelField("⚠️ Near limit", GUILayout.Width(80));
                    GUI.color = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBundleSizeAnalysis()
        {
            var groupSizes = new Dictionary<string, GroupSizeInfo>();

            foreach (var group in settings.groups)
            {
                if (group.name == "Built In Data") continue;

                long groupSize = 0;
                var assetPaths = new List<string>();

                foreach (var entry in group.entries)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (File.Exists(assetPath))
                    {
                        var fileInfo = new FileInfo(assetPath);
                        groupSize += fileInfo.Length;
                        assetPaths.Add(assetPath);
                    }
                }

                if (groupSize > 0)
                {
                    var bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
                    bool isLocal = bundledSchema != null &&
                                 bundledSchema.BuildPath.GetValue(settings).Contains("Local");

                    groupSizes[group.name] = new GroupSizeInfo
                    {
                        size = groupSize,
                        entryCount = group.entries.Count,
                        isLocal = isLocal,
                        assetPaths = assetPaths
                    };
                }
            }

            if (groupSizes.Count > 0)
            {
                EditorGUILayout.LabelField("Top 10 Groups by Size:", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                foreach (var kvp in groupSizes.OrderByDescending(x => x.Value.size).Take(10))
                {
                    EditorGUILayout.BeginHorizontal();

                    string icon = kvp.Value.isLocal ? "📦" : "☁️";
                    GUI.color = kvp.Value.size > AddressableSetupConstants.GROUP_SIZE_WARNING ? warningColor : Color.white;

                    EditorGUILayout.LabelField($"{icon} {kvp.Key}", GUILayout.Width(250));
                    EditorGUILayout.LabelField(FormatBytes(kvp.Value.size), GUILayout.Width(100));
                    EditorGUILayout.LabelField($"{kvp.Value.entryCount} entries", GUILayout.Width(100));

                    if (GUILayout.Button("Details", GUILayout.Width(60)))
                    {
                        ShowGroupDetails(kvp.Key, kvp.Value);
                    }

                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No groups with assets found", MessageType.Info);
            }
        }

        private void ShowGroupDetails(string groupName, GroupSizeInfo info)
        {
            var message = $"Group: {groupName}\n";
            message += $"Total Size: {FormatBytes(info.size)}\n";
            message += $"Entry Count: {info.entryCount}\n";
            message += $"Type: {(info.isLocal ? "Local (Built-in)" : "Remote")}\n\n";

            message += "Top 5 Largest Assets:\n";
            var sortedAssets = info.assetPaths
                .Select(p => new { Path = p, Size = new FileInfo(p).Length })
                .OrderByDescending(x => x.Size)
                .Take(5);

            foreach (var asset in sortedAssets)
            {
                string fileName = Path.GetFileName(asset.Path);
                message += $"• {fileName}: {FormatBytes(asset.Size)}\n";
            }

            EditorUtility.DisplayDialog("Group Details", message, "OK");
        }

        private void ShowDuplicateDetails(string assetName, List<DuplicateAsset> duplicates)
        {
            var message = $"Duplicate Asset: {assetName}\n\n";

            foreach (var dup in duplicates)
            {
                message += $"📁 {dup.paths}\n";
                message += $"   Size: {FormatBytes(dup.size)}\n\n";
            }

            message += "\n💡 Recommendation:\n";
            message += "Move to _Shared folder if used by multiple characters\n";
            message += "This will reduce download size and memory usage";

            EditorUtility.DisplayDialog("Duplicate Asset Details", message, "OK");
        }

        private void LoadSettings()
        {
            autoDetectEnabled = EditorPrefs.GetBool("AddressableAutoSetup.AutoDetect", true);
            smartNamingEnabled = EditorPrefs.GetBool("AddressableAutoSetup.SmartNaming", true);
            validationOnBuild = EditorPrefs.GetBool("AddressableAutoSetup.ValidationOnBuild", true);
            autoOptimize = EditorPrefs.GetBool("AddressableAutoSetup.AutoOptimize", false);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetBool("AddressableAutoSetup.AutoDetect", autoDetectEnabled);
            EditorPrefs.SetBool("AddressableAutoSetup.SmartNaming", smartNamingEnabled);
            EditorPrefs.SetBool("AddressableAutoSetup.ValidationOnBuild", validationOnBuild);
            EditorPrefs.SetBool("AddressableAutoSetup.AutoOptimize", autoOptimize);
        }

        private void LoadProfiles()
        {
            profiles.Clear();
            profiles.Add(new AutomationProfile { name = "Default" });
        }

        #endregion

        #region Utility Methods

        private long CalculateFolderSize(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return 0;

            try
            {
                var info = new DirectoryInfo(folderPath);
                return info.GetFiles("*", SearchOption.AllDirectories)
                    .Where(f => !f.Name.EndsWith(".meta"))
                    .Sum(f => f.Length);
            }
            catch
            {
                return 0;
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

        #region Data Classes

        [Serializable]
        public class AutomationProfile
        {
            public string name;
            public bool autoDetect = true;
            public bool smartNaming = true;
            public bool validation = true;
            public bool optimization = false;
        }


        [Serializable]
        public class CharacterTemplate
        {
            public bool includeEffects = true;
            public bool includeVoice = true;
            public bool includeAnimations = true;
            public List<string> folders = new List<string>
    {
        "Prefabs", "Atlas", "Textures", "Animations", "Data", "Effects"
    };
        }

        private class GroupSizeInfo
        {
            public long size;
            public int entryCount;
            public bool isLocal;
            public List<string> assetPaths = new List<string>();
        }

        #endregion
    }
}
#endif