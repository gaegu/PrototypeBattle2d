#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// AddressableAutoSetup 검증 시스템
    /// </summary>
    public static class AddressableAutoSetupValidation
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        #region Full Validation

        public static ValidationResult RunFullValidation()
        {
            var result = new ValidationResult { isValid = true };

            // Core validations
            ValidateCoreFolder(result);
            ValidateSharedFolder(result);

            // Character validations
            ValidateAllCharacters(result);

            // Episode validations
            ValidateAllEpisodes(result);

            // Group validations
            ValidateGroupSizes(result);
            ValidateGroupSettings(result);

            // Asset validations
            ValidateDuplicates(result);
            ValidateAddresses(result);
            ValidateMissingAssets(result);

            // Generate optimization suggestions
            GenerateOptimizationSuggestions(result);

            return result;
        }

        #endregion

        #region Core Validations

        private static void ValidateCoreFolder(ValidationResult result)
        {
            if (!Directory.Exists(AddressableSetupConstants.CORE_PATH))
            {
                result.errors.Add("❌ Core folder not found! Create '_Core' folder for essential assets.");
                result.isValid = false;
                return;
            }

            // Check Core group
            var coreGroup = Settings.groups.Find(g => g.name == "Core_Local");
            if (coreGroup == null)
            {
                result.errors.Add("❌ Core_Local group not found! This group must be included in build.");
                result.isValid = false;
            }
            else
            {
                // Verify it's set to local
                var schema = coreGroup.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                {
                    bool isLocal = schema.LoadPath.GetValue(Settings).Contains("Local");
                    if (!isLocal)
                    {
                        result.warnings.Add("⚠️ Core_Local group is not set to local build path. This will increase app download size.");
                    }

                    if (!schema.IncludeInBuild)
                    {
                        result.errors.Add("❌ Core_Local group must be included in build!");
                        result.isValid = false;
                    }
                }
            }

            // Check size
            long coreSize = AddressableAutoSetupCore.CalculateFolderSize(AddressableSetupConstants.CORE_PATH);
            result.groupSizes["Core_Local"] = coreSize;

            if (coreSize > AddressableSetupConstants.CORE_SIZE_LIMIT)
            {
                result.warnings.Add($"⚠️ Core folder exceeds 30MB limit ({AddressableAutoSetupCore.FormatBytes(coreSize)}). Consider moving non-essential assets to _Shared.");
            }
            else if (coreSize < 1024 * 1024) // Less than 1MB
            {
                result.warnings.Add("⚠️ Core folder is very small. Make sure essential assets are included.");
            }
        }

        private static void ValidateSharedFolder(ValidationResult result)
        {
            if (!Directory.Exists(AddressableSetupConstants.SHARED_PATH))
            {
                result.suggestions.Add("💡 Create '_Shared' folder for common assets used across multiple features.");
                return;
            }

            // Check for shared group
            var sharedGroup = Settings.groups.Find(g => g.name == "Shared_Common");
            if (sharedGroup == null)
            {
                result.warnings.Add("⚠️ Shared_Common group not found. Common assets may not be optimally grouped.");
            }

            // Check for potential shared assets
            CheckForPotentialSharedAssets(result);

            // Check size
            long sharedSize = AddressableAutoSetupCore.CalculateFolderSize(AddressableSetupConstants.SHARED_PATH);
            if (sharedGroup != null)
            {
                result.groupSizes["Shared_Common"] = sharedSize;
            }

            if (sharedSize > AddressableSetupConstants.SHARED_SIZE_LIMIT)
            {
                result.warnings.Add($"⚠️ Shared folder exceeds 40MB ({AddressableAutoSetupCore.FormatBytes(sharedSize)}). Consider splitting into categories.");
            }
        }

        #endregion

        #region Character Validations

        private static void ValidateAllCharacters(ValidationResult result)
        {
            var characters = AddressableAutoSetupCore.DetectCharacters();

            foreach (var character in characters)
            {
                ValidateCharacter(character, result);
            }

            if (characters.Count == 0)
            {
                result.suggestions.Add("💡 No characters found. Add character folders under 'Characters/' directory.");
            }
        }

        public static void ValidateCharacter(string characterName, ValidationResult result)
        {
            string charPath = Path.Combine(AddressableSetupConstants.CHARACTERS_PATH, characterName);

            // Check folder structure
            if (!Directory.Exists(charPath))
            {
                result.errors.Add($"❌ Character folder not found: {characterName}");
                return;
            }

            // Check required folders
            string[] requiredFolders = { "Prefabs", "Atlas", "Effects", "Animations" };
            foreach (var folder in requiredFolders)
            {
                string folderPath = Path.Combine(charPath, folder);
                if (!Directory.Exists(folderPath))
                {
                    result.warnings.Add($"⚠️ {characterName}: Missing '{folder}' folder");
                }
            }

            // Check required files
            string battlePrefab = Path.Combine(charPath, "Prefabs", $"{characterName}_Battle.prefab");
            string townPrefab = Path.Combine(charPath, "Prefabs", $"{characterName}_Town.prefab");
            string atlas = Path.Combine(charPath, "Atlas", $"{characterName}_Atlas.spriteatlas");

            if (!File.Exists(battlePrefab))
            {
                result.errors.Add($"❌ {characterName}: Missing battle prefab ({characterName}_Battle.prefab)");
            }

            if (!File.Exists(townPrefab))
            {
                result.warnings.Add($"⚠️ {characterName}: Missing town prefab ({characterName}_Town.prefab)");
            }

            if (!File.Exists(atlas))
            {
                result.warnings.Add($"⚠️ {characterName}: Missing sprite atlas ({characterName}_Atlas.spriteatlas)");
            }

            // Check group
            string groupName = $"Character_{characterName}";
            var group = Settings.groups.Find(g => g.name == groupName);

            if (group == null)
            {
                result.warnings.Add($"⚠️ {characterName}: No group found. Expected '{groupName}'");
            }
            else
            {
                // Calculate character size
                long charSize = AddressableAutoSetupCore.CalculateFolderSize(charPath);
                result.groupSizes[groupName] = charSize;

                if (charSize > AddressableSetupConstants.CHARACTER_SIZE_LIMIT)
                {
                    result.warnings.Add($"⚠️ {characterName}: Size exceeds 20MB ({AddressableAutoSetupCore.FormatBytes(charSize)}). Consider optimization.");
                }

                // Check if group is remote
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null && schema.LoadPath.GetValue(Settings).Contains("Local"))
                {
                    result.warnings.Add($"⚠️ {characterName}: Group is set to Local. Should be Remote for dynamic loading.");
                }
            }
        }

        #endregion

        #region Episode Validations

        private static void ValidateAllEpisodes(ValidationResult result)
        {
            var episodes = AddressableAutoSetupCore.DetectEpisodes();

            foreach (var episode in episodes)
            {
                ValidateEpisode(episode, result);
            }
        }

        private static void ValidateEpisode(string episode, ValidationResult result)
        {
            string epPath = Path.Combine(AddressableSetupConstants.SCENES_PATH, "EventCut", episode);

            if (!Directory.Exists(epPath))
            {
                result.errors.Add($"❌ Episode folder not found: {episode}");
                return;
            }

            // Check episode size
            long epSize = AddressableAutoSetupCore.CalculateFolderSize(epPath);
            string groupName = $"EventCut_{episode}";

            result.groupSizes[groupName] = epSize;

            if (epSize > AddressableSetupConstants.EP_SIZE_WARNING)
            {
                result.warnings.Add($"⚠️ {episode}: Size exceeds 250MB ({AddressableAutoSetupCore.FormatBytes(epSize)}). Consider splitting or optimization.");
            }
            else if (epSize > AddressableSetupConstants.EP_SIZE_LIMIT)
            {
                result.suggestions.Add($"💡 {episode}: Size is {AddressableAutoSetupCore.FormatBytes(epSize)}. Target is 200MB for optimal loading.");
            }

            // Check group
            var group = Settings.groups.Find(g => g.name == groupName);
            if (group == null)
            {
                result.warnings.Add($"⚠️ {episode}: No group found. Expected '{groupName}'");
            }

            // Check for scenes
            var scenes = Directory.GetFiles(epPath, "*.unity", SearchOption.AllDirectories);
            if (scenes.Length == 0)
            {
                result.warnings.Add($"⚠️ {episode}: No scene files found");
            }
            else
            {
                result.suggestions.Add($"💡 {episode}: Contains {scenes.Length} scenes");
            }
        }

        #endregion

        #region Group Validations

        private static void ValidateGroupSizes(ValidationResult result)
        {
            foreach (var group in Settings.groups)
            {
                if (group.name == "Built In Data") continue; // Skip default group

                long groupSize = 0;
                int entryCount = 0;

                foreach (var entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        groupSize += new FileInfo(path).Length;
                        entryCount++;
                    }
                }

                if (!result.groupSizes.ContainsKey(group.name))
                {
                    result.groupSizes[group.name] = groupSize;
                }

                // Check for empty groups
                if (entryCount == 0)
                {
                    result.warnings.Add($"⚠️ Group '{group.name}' is empty");
                }

                // Check for oversized groups (except EventCut)
                if (!group.name.Contains("EventCut") && groupSize > AddressableSetupConstants.GROUP_SIZE_WARNING)
                {
                    result.warnings.Add($"⚠️ Group '{group.name}' is large ({AddressableAutoSetupCore.FormatBytes(groupSize)}). Consider splitting.");
                }

                // Check for tiny groups
                if (entryCount > 0 && groupSize < 1024 * 100) // Less than 100KB
                {
                    result.suggestions.Add($"💡 Group '{group.name}' is very small ({AddressableAutoSetupCore.FormatBytes(groupSize)}). Consider merging with similar groups.");
                }
            }
        }

        private static void ValidateGroupSettings(ValidationResult result)
        {
            foreach (var group in Settings.groups)
            {
                if (group.name == "Built In Data") continue;

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                {
                    result.errors.Add($"❌ Group '{group.name}' missing BundledAssetGroupSchema");
                    continue;
                }

                // Check compression settings
                if (group.name.Contains("Core") && schema.Compression != BundledAssetGroupSchema.BundleCompressionMode.Uncompressed)
                {
                    result.suggestions.Add($"💡 Core group should use Uncompressed for faster initial loading");
                }

                // Check build/load paths
                bool isLocal = schema.LoadPath.GetValue(Settings).Contains("Local");

                if (group.name.Contains("Core") && !isLocal)
                {
                    result.errors.Add($"❌ Core group must use Local build/load paths");
                    result.isValid = false;
                }

                if ((group.name.Contains("Character") || group.name.Contains("EventCut")) && isLocal)
                {
                    result.warnings.Add($"⚠️ '{group.name}' should use Remote paths for dynamic content");
                }

                // Check CRC
                if (!schema.UseAssetBundleCrc)
                {
                    result.suggestions.Add($"💡 Enable CRC for '{group.name}' to ensure data integrity");
                }
            }
        }

        #endregion

        #region Asset Validations

        private static void ValidateDuplicates(ValidationResult result)
        {
            var duplicates = AddressableAutoSetupCore.DetectDuplicateAssets();

            if (duplicates.Count > 0)
            {
                long totalWasted = 0;

                foreach (var kvp in duplicates)
                {
                    var duplicate = kvp.Value.First();
                    totalWasted += duplicate.size * (duplicate.paths.Count - 1);
                }

                result.warnings.Add($"⚠️ Found {duplicates.Count} duplicate asset groups wasting {AddressableAutoSetupCore.FormatBytes(totalWasted)}");

                // Add top duplicates to suggestions
                var topDuplicates = duplicates.Values
                    .SelectMany(list => list)
                    .OrderByDescending(d => d.GetWastedSpace())
                    .Take(3);

                foreach (var dup in topDuplicates)
                {
                    string fileName = Path.GetFileName(dup.paths.First());
                    result.suggestions.Add($"💡 '{fileName}' duplicated {dup.paths.Count} times ({AddressableAutoSetupCore.FormatBytes(dup.size)} each)");
                }
            }
        }

        private static void ValidateAddresses(ValidationResult result)
        {
            var addressCounts = new Dictionary<string, List<string>>();

            foreach (var group in Settings.groups)
            {
                foreach (var entry in group.entries)
                {
                    if (!addressCounts.ContainsKey(entry.address))
                    {
                        addressCounts[entry.address] = new List<string>();
                    }
                    addressCounts[entry.address].Add(group.name);
                }
            }

            var duplicateAddresses = addressCounts.Where(kvp => kvp.Value.Count > 1).ToList();

            foreach (var dup in duplicateAddresses)
            {
                result.errors.Add($"❌ Duplicate address '{dup.Key}' found in groups: {string.Join(", ", dup.Value)}");
                result.isValid = false;
            }

            // Check address naming convention
            foreach (var group in Settings.groups)
            {
                foreach (var entry in group.entries)
                {
                    if (!ValidateAddressNaming(entry.address, group.name))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                        string fileName = Path.GetFileName(path);
                        result.suggestions.Add($"💡 '{fileName}' address doesn't follow naming convention: {entry.address}");
                    }
                }
            }
        }

        private static void ValidateMissingAssets(ValidationResult result)
        {
            int missingCount = 0;

            foreach (var group in Settings.groups)
            {
                var toCheck = new List<AddressableAssetEntry>();

                foreach (var entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        toCheck.Add(entry);
                        missingCount++;
                    }
                }

                if (toCheck.Count > 0)
                {
                    result.errors.Add($"❌ Group '{group.name}' has {toCheck.Count} missing assets");
                }
            }

            if (missingCount > 0)
            {
                result.suggestions.Add($"💡 Run 'Clean Invalid Entries' to remove {missingCount} missing references");
            }
        }

        #endregion

        #region Optimization Suggestions

        public static List<OptimizationSuggestion> GenerateOptimizationSuggestions(ValidationResult result)
        {
            var suggestions = new List<OptimizationSuggestion>();

            // Check for assets that should be in _Shared
            CheckForSharedAssets(suggestions);

            // Check for texture compression
            CheckTextureCompression(suggestions);

            // Check for atlas optimization
            //CheckAtlasOptimization(suggestions);

            // Check for audio compression
            CheckAudioCompression(suggestions);

            // Check for unused assets
            CheckUnusedAssets(suggestions);

            return suggestions;
        }

        private static void CheckForSharedAssets(List<OptimizationSuggestion> suggestions)
        {
            // Find common effects in character folders
            var effectFiles = Directory.GetFiles(
                AddressableSetupConstants.CHARACTERS_PATH,
                "*Hit*.prefab",
                SearchOption.AllDirectories
            );

            if (effectFiles.Length > 5)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    title = "Move common effects to Shared",
                    description = $"Found {effectFiles.Length} hit effects in character folders that could be shared",
                    type = SuggestionType.MoveToShared,
                    estimatedSavingMB = effectFiles.Length * 0.5f
                });
            }

            // Find common UI elements
            var uiElements = Directory.GetFiles(
                AddressableSetupConstants.CHARACTERS_PATH,
                "*Button*.prefab",
                SearchOption.AllDirectories
            );

            if (uiElements.Length > 3)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    title = "Move common UI to Shared",
                    description = $"Found {uiElements.Length} UI elements that could be shared",
                    type = SuggestionType.MoveToShared,
                    estimatedSavingMB = uiElements.Length * 0.1f
                });
            }
        }

        private static void CheckTextureCompression(List<OptimizationSuggestion> suggestions)
        {
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { AddressableSetupConstants.BASE_PATH });
            int uncompressedCount = 0;
            float potentialSaving = 0;

            foreach (var guid in textures.Take(100)) // Check first 100 for performance
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    uncompressedCount++;
                    var fileInfo = new FileInfo(path);
                    potentialSaving += fileInfo.Length * 0.7f / (1024f * 1024f); // Assume 70% compression
                }
            }

            if (uncompressedCount > 0)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    title = "Compress textures",
                    description = $"Found {uncompressedCount} uncompressed textures",
                    type = SuggestionType.CompressTexture,
                    estimatedSavingMB = potentialSaving
                });
            }
        }

       /* private static void CheckAtlasOptimization(ValidationResult result)
        {
            var atlases = AssetDatabase.FindAssets("t:SpriteAtlas");

            foreach (var guid in atlases)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path);

                if (atlas == null) continue;

                // 파일 크기로 간접 체크
                var fileInfo = new System.IO.FileInfo(path);
                if (fileInfo.Exists)
                {
                    long fileSize = fileInfo.Length;
                    if (fileSize > 5 * 1024 * 1024) // 5MB
                    {
                        result.warnings.Add($"Large atlas file ({AddressableAutoSetupCore.FormatBytes(fileSize)}): {path}");
                    }
                }

                // 아틀라스 설정만 체크
                if (!settings.enableTightPacking)
                {
                    result.suggestions.Add($"Enable tight packing for better efficiency: {path}");
                }

                if (!settings.enableRotation)
                {
                    result.suggestions.Add($"Consider enabling rotation for better packing: {path}");
                }
            }
        }
       */

        private static void CheckAudioCompression(List<OptimizationSuggestion> suggestions)
        {
            var audioClips = AssetDatabase.FindAssets("t:AudioClip", new[] { AddressableSetupConstants.BASE_PATH });
            int uncompressedCount = 0;

            foreach (var guid in audioClips.Take(50)) // Check first 50 for performance
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;

                if (importer != null)
                {
                    var settings = importer.defaultSampleSettings;
                    if (settings.loadType == AudioClipLoadType.DecompressOnLoad)
                    {
                        uncompressedCount++;
                    }
                }
            }

            if (uncompressedCount > 0)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    title = "Optimize audio settings",
                    description = $"Found {uncompressedCount} audio clips with DecompressOnLoad",
                    type = SuggestionType.CompressTexture, // Reuse type
                    estimatedSavingMB = uncompressedCount * 0.5f
                });
            }
        }

        private static void CheckUnusedAssets(List<OptimizationSuggestion> suggestions)
        {
            // This is a simplified check - in production, you'd want more sophisticated dependency analysis
            var allAssets = AssetDatabase.FindAssets("", new[] { AddressableSetupConstants.BASE_PATH });
            var usedAssets = new HashSet<string>();

            // Collect all referenced assets
            foreach (var group in Settings.groups)
            {
                foreach (var entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        usedAssets.Add(path);

                        // Add dependencies
                        var deps = AssetDatabase.GetDependencies(path, true);
                        foreach (var dep in deps)
                        {
                            usedAssets.Add(dep);
                        }
                    }
                }
            }

            // Find potentially unused
            int unusedCount = 0;
            long unusedSize = 0;

            foreach (var guid in allAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path) && !usedAssets.Contains(path))
                {
                    if (File.Exists(path))
                    {
                        unusedCount++;
                        unusedSize += new FileInfo(path).Length;
                    }
                }
            }

            if (unusedCount > 10)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    title = "Remove unused assets",
                    description = $"Found {unusedCount} potentially unused assets",
                    type = SuggestionType.RemoveUnused,
                    estimatedSavingMB = unusedSize / (1024f * 1024f)
                });
            }
        }

        private static void CheckForPotentialSharedAssets(ValidationResult result)
        {
            // Find duplicate names across character folders
            var assetNameCounts = new Dictionary<string, int>();

            var characterAssets = Directory.GetFiles(
                AddressableSetupConstants.CHARACTERS_PATH,
                "*.*",
                SearchOption.AllDirectories
            ).Where(f => !f.EndsWith(".meta"));

            foreach (var asset in characterAssets)
            {
                string fileName = Path.GetFileName(asset);
                if (!assetNameCounts.ContainsKey(fileName))
                {
                    assetNameCounts[fileName] = 0;
                }
                assetNameCounts[fileName]++;
            }

            var commonAssets = assetNameCounts.Where(kvp => kvp.Value > 3).ToList();

            if (commonAssets.Count > 0)
            {
                result.suggestions.Add($"💡 Found {commonAssets.Count} asset names appearing in multiple characters. Consider moving to _Shared:");
                foreach (var asset in commonAssets.Take(5))
                {
                    result.suggestions.Add($"   • {asset.Key} ({asset.Value} occurrences)");
                }
            }
        }

        #endregion

        #region Helper Methods

        private static bool ValidateAddressNaming(string address, string groupName)
        {
            // Check if address follows naming convention
            if (groupName.StartsWith("Character_"))
            {
                return address.StartsWith("Char_");
            }
            if (groupName.StartsWith("UI_"))
            {
                return address.StartsWith("UI_");
            }
            if (groupName.StartsWith("Effect_"))
            {
                return address.StartsWith("Effect_");
            }
            if (groupName.StartsWith("EventCut_"))
            {
                return address.StartsWith("Scene_EventCut_");
            }
            if (groupName.StartsWith("Audio_"))
            {
                return address.StartsWith("BGM_") || address.StartsWith("SFX_") || address.StartsWith("Voice_");
            }

            return true; // Default to valid
        }

        #endregion
    }
}
#endif