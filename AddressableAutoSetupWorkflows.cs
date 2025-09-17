#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// AddressableAutoSetup 워크플로우
    /// </summary>
    public static class AddressableAutoSetupWorkflows
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        #region Main Workflows

        /// <summary>
        /// 초기 프로젝트 설정 워크플로우
        /// </summary>
        public static SetupStatistics RunInitialSetupWorkflow()
        {
            var stats = new SetupStatistics();
            var startTime = Time.realtimeSinceStartup;

            try
            {
                EditorUtility.DisplayProgressBar("Initial Setup", "Starting...", 0);

                // 1. Create folder structure
                CreateProjectFolderStructure();
                EditorUtility.DisplayProgressBar("Initial Setup", "Folder structure created", 0.2f);

                // 2. Load or create rules
                var rules = AddressableAutoSetupCore.LoadMappingRules();
                EditorUtility.DisplayProgressBar("Initial Setup", "Rules loaded", 0.3f);

                // 3. Create essential groups
                CreateEssentialGroups(rules, stats);
                EditorUtility.DisplayProgressBar("Initial Setup", "Groups created", 0.4f);

                // 4. Run auto detection
                var characters = AddressableAutoSetupCore.DetectCharacters();
                var episodes = AddressableAutoSetupCore.DetectEpisodes();
                var monthlyFolders = AddressableAutoSetupCore.DetectMonthlyFolders();
                EditorUtility.DisplayProgressBar("Initial Setup", "Detection complete", 0.5f);

                // 5. Create missing groups
                stats.groupsCreated += AddressableAutoSetupCore.CreateMissingGroups(rules);
                EditorUtility.DisplayProgressBar("Initial Setup", "Missing groups created", 0.6f);

                // 6. Apply mapping rules
                ApplyAllRules(rules, stats);
                EditorUtility.DisplayProgressBar("Initial Setup", "Rules applied", 0.8f);

                // 7. Run validation
                var validation = AddressableAutoSetupValidation.RunFullValidation();
                EditorUtility.DisplayProgressBar("Initial Setup", "Validation complete", 0.9f);

                // 8. Save assets
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                stats.timeTaken = Time.realtimeSinceStartup - startTime;

                // Show results
                ShowSetupResults(stats, validation, characters.Count, episodes.Count);

                return stats;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 월별 업데이트 워크플로우
        /// </summary>
        public static bool RunMonthlyUpdateWorkflow(string year, string month, List<string> characterNames)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Monthly Update", "Creating structure...", 0);

                string monthlyFolder = $"{year}_{month.PadLeft(2, '0')}";
                string basePath = Path.Combine(AddressableSetupConstants.MONTHLY_PATH, monthlyFolder);

                // 1. Create folder structure
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }

                EditorUtility.DisplayProgressBar("Monthly Update", "Creating characters...", 0.3f);

                // 2. Create character folders
                foreach (var charName in characterNames.Where(c => !string.IsNullOrEmpty(c)))
                {
                    CreateCharacterStructure(Path.Combine(basePath, charName), charName);
                }

                EditorUtility.DisplayProgressBar("Monthly Update", "Creating group...", 0.6f);

                // 3. Create monthly group
                var rules = AddressableAutoSetupCore.LoadMappingRules();
                var rule = rules.rules.Find(r => r.pattern.Contains("_MonthlyUpdate"));
                if (rule != null)
                {
                    string groupName = $"Monthly_{monthlyFolder}_Characters";
                    if (!Settings.groups.Any(g => g.name == groupName))
                    {
                        AddressableAutoSetupCore.CreateGroup(groupName, rule);
                    }
                }

                EditorUtility.DisplayProgressBar("Monthly Update", "Applying rules...", 0.8f);

                // 4. Apply mapping rules
                var stats = new SetupStatistics();
                ApplyRulesForPath(basePath, rules, stats);

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Monthly Update Complete",
                    $"Created monthly update for {monthlyFolder}\n" +
                    $"Characters: {characterNames.Count(c => !string.IsNullOrEmpty(c))}",
                    "OK");

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 캐릭터 생성 워크플로우
        /// </summary>
        public static bool CreateCharacterWorkflow(string characterName, CharacterTemplate template = null)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                EditorUtility.DisplayDialog("Error", "Character name cannot be empty", "OK");
                return false;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Create Character", "Creating structure...", 0);

                string basePath = Path.Combine(AddressableSetupConstants.CHARACTERS_PATH, characterName);

                if (Directory.Exists(basePath))
                {
                    if (!EditorUtility.DisplayDialog("Character Exists",
                        $"Character '{characterName}' already exists. Overwrite?", "Yes", "No"))
                    {
                        return false;
                    }
                }

                // Use template or default
                if (template == null)
                {
                    template = new CharacterTemplate { name = characterName };
                }

                CreateCharacterStructure(basePath, characterName, template);

                EditorUtility.DisplayProgressBar("Create Character", "Creating group...", 0.5f);

                // Create character group
                var rules = AddressableAutoSetupCore.LoadMappingRules();
                var rule = rules.rules.Find(r => r.pattern.Contains("Characters/{CharName}"));
                if (rule != null)
                {
                    string groupName = $"Character_{characterName}";
                    if (!Settings.groups.Any(g => g.name == groupName))
                    {
                        AddressableAutoSetupCore.CreateGroup(groupName, rule);
                    }
                }

                EditorUtility.DisplayProgressBar("Create Character", "Applying rules...", 0.8f);

                // Apply mapping rules
                var stats = new SetupStatistics();
                ApplyRulesForPath(basePath, rules, stats);

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Character Created",
                    $"Successfully created character: {characterName}", "OK");

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 시즌 이벤트 워크플로우
        /// </summary>
        public static bool RunSeasonEventWorkflow(string seasonName)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Season Event", "Creating structure...", 0);

                string basePath = Path.Combine(AddressableSetupConstants.SEASONAL_PATH, seasonName);

                // 1. Create folder structure
                CreateSeasonStructure(basePath, seasonName);

                EditorUtility.DisplayProgressBar("Season Event", "Creating group...", 0.5f);

                // 2. Create seasonal group
                var rules = AddressableAutoSetupCore.LoadMappingRules();
                var rule = rules.rules.Find(r => r.pattern.Contains("_Seasonal"));
                if (rule != null)
                {
                    string groupName = $"Seasonal_{seasonName}";
                    if (!Settings.groups.Any(g => g.name == groupName))
                    {
                        AddressableAutoSetupCore.CreateGroup(groupName, rule);
                    }
                }

                EditorUtility.DisplayProgressBar("Season Event", "Applying rules...", 0.8f);

                // 3. Apply mapping rules
                var stats = new SetupStatistics();
                ApplyRulesForPath(basePath, rules, stats);

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Season Event Created",
                    $"Successfully created season: {seasonName}", "OK");

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 최적화 워크플로우
        /// </summary>
        public static void RunOptimizationWorkflow()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Optimization", "Analyzing...", 0);

                // 1. Run validation
                var validation = AddressableAutoSetupValidation.RunFullValidation();

                EditorUtility.DisplayProgressBar("Optimization", "Generating suggestions...", 0.3f);

                // 2. Generate optimization suggestions
                var suggestions = AddressableAutoSetupValidation.GenerateOptimizationSuggestions(validation);

                if (suggestions.Count == 0)
                {
                    EditorUtility.DisplayDialog("Optimization", "No optimization suggestions found", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Optimization", "Applying optimizations...", 0.5f);

                // 3. Show and apply suggestions
                int applied = 0;
                foreach (var suggestion in suggestions)
                {
                    if (suggestion.applyAction != null)
                    {
                        if (EditorUtility.DisplayDialog("Optimization",
                            $"{suggestion.title}\n\n{suggestion.description}\n\nEstimated saving: {suggestion.estimatedSavingMB:F1} MB\n\nApply?",
                            "Yes", "No"))
                        {
                            suggestion.applyAction.Invoke();
                            applied++;
                        }
                    }
                }

                if (applied > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    EditorUtility.DisplayDialog("Optimization Complete",
                        $"Applied {applied} optimizations", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 빌드 준비 워크플로우
        /// </summary>
        public static bool RunBuildPrepWorkflow()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Build Preparation", "Cleaning invalid entries...", 0);

                // 1. Clean invalid entries
                AddressableAutoSetupCore.CleanInvalidEntries();

                EditorUtility.DisplayProgressBar("Build Preparation", "Running validation...", 0.3f);

                // 2. Run full validation
                var validation = AddressableAutoSetupValidation.RunFullValidation();

                EditorUtility.DisplayProgressBar("Build Preparation", "Checking critical issues...", 0.6f);

                // 3. Check for critical issues
                if (!validation.isValid)
                {
                    string errors = string.Join("\n", validation.errors.Take(5));

                    EditorUtility.DisplayDialog("Build Not Ready",
                        $"Found {validation.errors.Count} critical errors:\n\n{errors}\n\nPlease fix these issues before building.",
                        "OK");

                    return false;
                }

                EditorUtility.DisplayProgressBar("Build Preparation", "Optimizing groups...", 0.8f);

                // 4. Final optimizations
                OptimizeForBuild();

                AssetDatabase.SaveAssets();

                // 5. Show summary
                ShowBuildReadySummary(validation);

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #endregion

        #region Helper Methods

        private static void CreateProjectFolderStructure()
        {
            // Core folders
            CreateFolderIfNotExists(AddressableSetupConstants.CORE_PATH);
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.CORE_PATH, "UI"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.CORE_PATH, "Systems"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.CORE_PATH, "Scenes"));

            // Shared folders
            CreateFolderIfNotExists(AddressableSetupConstants.SHARED_PATH);
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SHARED_PATH, "UI"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SHARED_PATH, "Effects"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SHARED_PATH, "Materials"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SHARED_PATH, "Audio"));

            // Character folders
            CreateFolderIfNotExists(AddressableSetupConstants.CHARACTERS_PATH);
            CreateFolderIfNotExists(AddressableSetupConstants.MONTHLY_PATH);

            // Scene folders
            CreateFolderIfNotExists(AddressableSetupConstants.SCENES_PATH);
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SCENES_PATH, "Town"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SCENES_PATH, "Battle"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.SCENES_PATH, "EventCut"));

            // UI folders
            CreateFolderIfNotExists(AddressableSetupConstants.UI_PATH);
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.UI_PATH, "Windows"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.UI_PATH, "Battle"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.UI_PATH, "Event"));

            // Audio folders
            CreateFolderIfNotExists(AddressableSetupConstants.AUDIO_PATH);
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.AUDIO_PATH, "BGM"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.AUDIO_PATH, "SFX"));
            CreateFolderIfNotExists(Path.Combine(AddressableSetupConstants.AUDIO_PATH, "Voice"));

            // Seasonal folders
            CreateFolderIfNotExists(AddressableSetupConstants.SEASONAL_PATH);
        }

        private static void CreateFolderIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"[AutoSetup] Created folder: {path}");
            }
        }

        private static void CreateEssentialGroups(MappingRuleSet rules, SetupStatistics stats)
        {
            // Core_Local (must exist)
            if (!Settings.groups.Any(g => g.name == "Core_Local"))
            {
                var coreRule = rules.rules.Find(r => r.groupNameTemplate == "Core_Local")
                    ?? new MappingRule
                    {
                        groupNameTemplate = "Core_Local",
                        isLocal = true,
                        compression = "Uncompressed",
                        includeInBuild = true
                    };
                AddressableAutoSetupCore.CreateGroup("Core_Local", coreRule);
                stats.groupsCreated++;
            }

            // Shared_Common
            if (!Settings.groups.Any(g => g.name == "Shared_Common"))
            {
                var sharedRule = rules.rules.Find(r => r.groupNameTemplate == "Shared_Common")
                    ?? new MappingRule
                    {
                        groupNameTemplate = "Shared_Common",
                        isLocal = false,
                        compression = "LZ4"
                    };
                AddressableAutoSetupCore.CreateGroup("Shared_Common", sharedRule);
                stats.groupsCreated++;
            }
        }

        private static void CreateCharacterStructure(string basePath, string characterName, CharacterTemplate template = null)
        {
            if (template == null)
            {
                template = new CharacterTemplate { name = characterName };
            }

            // Create folders
            foreach (var folder in template.folders)
            {
                string folderPath = Path.Combine(basePath, folder);
                CreateFolderIfNotExists(folderPath);
            }

            // Create placeholder files
            string prefabsPath = Path.Combine(basePath, "Prefabs");
            CreatePlaceholderPrefab(Path.Combine(prefabsPath, $"{characterName}_Battle.prefab"), "Battle");
            CreatePlaceholderPrefab(Path.Combine(prefabsPath, $"{characterName}_Town.prefab"), "Town");

            // Create atlas placeholder
            string atlasPath = Path.Combine(basePath, "Atlas");
            CreatePlaceholderAtlas(Path.Combine(atlasPath, $"{characterName}_Atlas.spriteatlas"));

            // Create animation controller placeholder
            string animPath = Path.Combine(basePath, "Animations");
            CreatePlaceholderAnimController(Path.Combine(animPath, $"{characterName}_Controller.controller"));

            // Create ScriptableObject data
            string dataPath = Path.Combine(basePath, "Data");
            CreateCharacterData(Path.Combine(dataPath, $"{characterName}_Stats.asset"), characterName);
        }

        private static void CreateSeasonStructure(string basePath, string seasonName)
        {
            CreateFolderIfNotExists(basePath);
            CreateFolderIfNotExists(Path.Combine(basePath, "Title"));
            CreateFolderIfNotExists(Path.Combine(basePath, "Events"));
            CreateFolderIfNotExists(Path.Combine(basePath, "Limited"));
            CreateFolderIfNotExists(Path.Combine(basePath, "UI"));
            CreateFolderIfNotExists(Path.Combine(basePath, "Effects"));

            // Create placeholder title prefab
            CreatePlaceholderPrefab(
                Path.Combine(basePath, "Title", $"Title_{seasonName}.prefab"),
                "SeasonTitle"
            );
        }

        private static void CreatePlaceholderPrefab(string path, string type)
        {
            if (File.Exists(path)) return;

            GameObject go = new GameObject($"Placeholder_{type}");

            // Add component based on type
            switch (type)
            {
                case "Battle":
                case "Town":
                    go.AddComponent<SpriteRenderer>();
                    break;
                case "UI":
                    go.AddComponent<RectTransform>();
                    go.AddComponent<CanvasRenderer>();
                    break;
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            GameObject.DestroyImmediate(go);
        }

        private static void CreatePlaceholderAtlas(string path)
        {
            if (File.Exists(path)) return;

            var atlas = new UnityEngine.U2D.SpriteAtlas();

            // Configure atlas settings
            var settings = atlas.GetPackingSettings();
            settings.enableRotation = false;
            settings.enableTightPacking = false;
            settings.padding = 2;
            atlas.SetPackingSettings(settings);

            var textureSettings = atlas.GetTextureSettings();
            textureSettings.readable = false;
            textureSettings.generateMipMaps = false;
            textureSettings.filterMode = FilterMode.Bilinear;
            atlas.SetTextureSettings(textureSettings);

            var platformSettings = atlas.GetPlatformSettings("Android");
            platformSettings.maxTextureSize = 2048;
            platformSettings.textureCompression = TextureImporterCompression.Compressed;
            platformSettings.format = TextureImporterFormat.ASTC_6x6;
            atlas.SetPlatformSettings(platformSettings);

            AssetDatabase.CreateAsset(atlas, path);
        }

        private static void CreatePlaceholderAnimController(string path)
        {
            if (File.Exists(path)) return;

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);

            // Add basic layers and parameters
            var rootStateMachine = controller.layers[0].stateMachine;
            var idleState = rootStateMachine.AddState("Idle");
            rootStateMachine.defaultState = idleState;
        }

        private static void CreateCharacterData(string path, string characterName)
        {
            if (File.Exists(path)) return;

            // Create a basic ScriptableObject placeholder
            // In a real project, you would create your actual CharacterStats ScriptableObject
            var data = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(data, path);
        }

        private static void ApplyAllRules(MappingRuleSet rules, SetupStatistics stats)
        {
            foreach (var rule in rules.rules)
            {
                var result = AddressableAutoSetupCore.ApplyMappingRule(rule);
                stats.assetsProcessed += result.processed;
                stats.groupsCreated += result.created;
            }
        }

        private static void ApplyRulesForPath(string path, MappingRuleSet rules, SetupStatistics stats)
        {
            foreach (var rule in rules.rules)
            {
                // Check if rule applies to this path
                var matchingPaths = AddressableAutoSetupCore.FindMatchingPaths(rule.pattern);
                var pathMatches = matchingPaths.Where(p => p.StartsWith(path)).ToList();

                if (pathMatches.Count > 0)
                {
                    var result = AddressableAutoSetupCore.ApplyMappingRule(rule);
                    stats.assetsProcessed += result.processed;
                    stats.groupsCreated += result.created;
                }
            }
        }

        private static void OptimizeForBuild()
        {
            // Remove empty groups
            var emptyGroups = Settings.groups.Where(g => g.entries.Count == 0 && g.name != "Built In Data").ToList();
            foreach (var group in emptyGroups)
            {
                Settings.RemoveGroup(group);
                Debug.Log($"[AutoSetup] Removed empty group: {group.name}");
            }

            // Ensure Core_Local is first in build
            var coreGroup = Settings.groups.Find(g => g.name == "Core_Local");
            if (coreGroup != null)
            {
                Settings.groups.Remove(coreGroup);
                Settings.groups.Insert(0, coreGroup);
            }
        }

        private static void ShowSetupResults(SetupStatistics stats, ValidationResult validation, int charCount, int epCount)
        {
            var message = "Initial Setup Complete!\n\n";

            message += "📊 Statistics:\n";
            message += $"• Groups Created: {stats.groupsCreated}\n";
            message += $"• Assets Processed: {stats.assetsProcessed}\n";
            message += $"• Characters Found: {charCount}\n";
            message += $"• Episodes Found: {epCount}\n";
            message += $"• Time Taken: {stats.timeTaken:F1} seconds\n\n";

            if (validation.errors.Count > 0)
            {
                message += $"⚠️ Found {validation.errors.Count} errors that need attention\n";
            }
            else if (validation.warnings.Count > 0)
            {
                message += $"✓ Setup successful with {validation.warnings.Count} warnings\n";
            }
            else
            {
                message += "✓ Perfect! No issues found\n";
            }

            EditorUtility.DisplayDialog("Setup Complete", message, "OK");
        }

        private static void ShowBuildReadySummary(ValidationResult validation)
        {
            var message = "Build Preparation Complete!\n\n";

            message += "📋 Summary:\n";

            long totalSize = validation.groupSizes.Values.Sum();
            message += $"• Total Size: {AddressableAutoSetupCore.FormatBytes(totalSize)}\n";
            message += $"• Groups: {Settings.groups.Count}\n";

            var localSize = validation.groupSizes.Where(kvp => kvp.Key.Contains("Local")).Sum(kvp => kvp.Value);
            var remoteSize = totalSize - localSize;

            message += $"• Local (App): {AddressableAutoSetupCore.FormatBytes(localSize)}\n";
            message += $"• Remote (CDN): {AddressableAutoSetupCore.FormatBytes(remoteSize)}\n\n";

            if (validation.warnings.Count > 0)
            {
                message += $"⚠️ {validation.warnings.Count} warnings (non-critical)\n";
            }
            else
            {
                message += "✓ No warnings - optimal configuration!\n";
            }

            message += "\n🚀 Project is ready for build!";

            EditorUtility.DisplayDialog("Build Ready", message, "Great!");
        }

        #endregion
    }
}
#endif