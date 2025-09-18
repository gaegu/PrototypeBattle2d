#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Newtonsoft.Json;
using DG.Tweening.Plugins.Core.PathCore;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// AddressableAutoSetup 핵심 로직
    /// </summary>
    public static class AddressableAutoSetupCore
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        #region Auto Detection

        public static List<string> DetectCharacters()
        {
            var characters = new List<string>();

            if (!Directory.Exists(AddressableSetupConstants.CHARACTERS_PATH))
                return characters;

            var dirs = Directory.GetDirectories(AddressableSetupConstants.CHARACTERS_PATH);
            foreach (var dir in dirs)
            {
                string dirName = System.IO.Path.GetFileName(dir);

                // _MonthlyUpdate 폴더 제외
                if (!dirName.StartsWith("_") && !dirName.StartsWith("."))
                {
                    characters.Add(dirName);
                }
            }

            Debug.Log($"[AutoSetup] Detected {characters.Count} characters");
            return characters;
        }

        public static List<string> DetectEpisodes()
        {
            var episodes = new List<string>();
            string eventCutPath = System.IO.Path.Combine(AddressableSetupConstants.SCENES_PATH, "EventCut");

            if (!Directory.Exists(eventCutPath))
                return episodes;

            var dirs = Directory.GetDirectories(eventCutPath);
            foreach (var dir in dirs)
            {
                string dirName = System.IO.Path.GetFileName(dir);
                if (Regex.IsMatch(dirName, @"EP\d+"))
                {
                    episodes.Add(dirName);
                }
            }

            Debug.Log($"[AutoSetup] Detected {episodes.Count} episodes");
            return episodes;
        }

        public static List<string> DetectMonthlyFolders()
        {
            var monthlyFolders = new List<string>();

            if (!Directory.Exists(AddressableSetupConstants.MONTHLY_PATH))
                return monthlyFolders;

            var dirs = Directory.GetDirectories(AddressableSetupConstants.MONTHLY_PATH);
            foreach (var dir in dirs)
            {
                string dirName = System.IO.Path.GetFileName(dir);
                if (Regex.IsMatch(dirName, @"\d{4}_\d{2}"))
                {
                    monthlyFolders.Add(dirName);
                }
            }

            Debug.Log($"[AutoSetup] Detected {monthlyFolders.Count} monthly folders");
            return monthlyFolders;
        }


        // <summary>
        /// ScriptableObject 폴더 감지
        /// </summary>
        public static List<string> DetectScriptableObjects()
        {
            var scriptableObjects = new List<string>();

            // Characters ScriptableObjects
            if (Directory.Exists(AddressableSetupConstants.SO_CHARACTERS_PATH))
            {
                var charFiles = Directory.GetFiles(AddressableSetupConstants.SO_CHARACTERS_PATH, "*.asset", SearchOption.AllDirectories);
                scriptableObjects.AddRange(charFiles);
                Debug.Log($"[AutoSetup] Found {charFiles.Length} character ScriptableObjects");
            }

            // Monsters ScriptableObjects
            if (Directory.Exists(AddressableSetupConstants.SO_MONSTERS_PATH))
            {
                var monsterFiles = Directory.GetFiles(AddressableSetupConstants.SO_MONSTERS_PATH, "*.asset", SearchOption.AllDirectories);
                scriptableObjects.AddRange(monsterFiles);
                Debug.Log($"[AutoSetup] Found {monsterFiles.Length} monster ScriptableObjects");
            }

            // MonsterGroups ScriptableObjects
            if (Directory.Exists(AddressableSetupConstants.SO_MONSTER_GROUPS_PATH))
            {
                var groupFiles = Directory.GetFiles(AddressableSetupConstants.SO_MONSTER_GROUPS_PATH, "*.asset", SearchOption.AllDirectories);
                scriptableObjects.AddRange(groupFiles);
                Debug.Log($"[AutoSetup] Found {groupFiles.Length} monster group ScriptableObjects");
            }

            // 기타 ScriptableObjects
            if (Directory.Exists(AddressableSetupConstants.SCRIPTABLE_OBJECTS_PATH))
            {
                var otherFiles = Directory.GetFiles(AddressableSetupConstants.SCRIPTABLE_OBJECTS_PATH, "*.asset", SearchOption.TopDirectoryOnly);
                scriptableObjects.AddRange(otherFiles);
                Debug.Log($"[AutoSetup] Found {otherFiles.Length} other ScriptableObjects");
            }

            if (Directory.Exists(AddressableSetupConstants.SO_COMMON_PATH))
            {
                var commonFiles = Directory.GetFiles(AddressableSetupConstants.SO_COMMON_PATH, "*.asset", SearchOption.AllDirectories);
                scriptableObjects.AddRange(commonFiles);
                Debug.Log($"[AutoSetup] Found {commonFiles.Length} common ScriptableObjects");
            }

            return scriptableObjects;
        }

        /// <summary>
        /// ScriptableObject 카테고리별 분류
        /// </summary>
        public static Dictionary<string, List<string>> CategorizeScriptableObjects()
        {
            var categories = new Dictionary<string, List<string>>();

            categories["Characters"] = new List<string>();
            categories["Monsters"] = new List<string>();
            categories["MonsterGroups"] = new List<string>();
            categories["Others"] = new List<string>();
            categories["Common"] = new List<string>();


            var allSOs = DetectScriptableObjects();

            foreach (var so in allSOs)
            {
                // ⭐ 경로 정규화
                string normalizedPath = so.Replace('\\', '/');

                if (normalizedPath.Contains("/Characters"))
                    categories["Characters"].Add(normalizedPath);
                else if (normalizedPath.Contains("/MonsterGroups"))
                    categories["MonsterGroups"].Add(normalizedPath);
                else if (normalizedPath.Contains("/Monsters"))
                    categories["Monsters"].Add(normalizedPath);
                else if (normalizedPath.Contains("/Common"))
                    categories["Common"].Add(normalizedPath);
            }

            return categories;
        }

        public static Dictionary<string, List<DuplicateAsset>> DetectDuplicateAssets()
        {
            var duplicates = new Dictionary<string, List<DuplicateAsset>>();

            var assetPaths = AssetDatabase.FindAssets("", new[] { AddressableSetupConstants.BASE_PATH })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .ToList();

            var hashGroups = new Dictionary<string, List<string>>();

            foreach (var path in assetPaths)
            {
                string hash = GetFileHash(path);
                if (!hashGroups.ContainsKey(hash))
                {
                    hashGroups[hash] = new List<string>();
                }
                hashGroups[hash].Add(path);
            }

            foreach (var kvp in hashGroups.Where(g => g.Value.Count > 1))
            {
                var duplicate = new DuplicateAsset
                {
                    hash = kvp.Key,
                    paths = kvp.Value,
                    size = new FileInfo(kvp.Value[0]).Length,
                    canMoveToShared = CanMoveToShared(kvp.Value)
                };

                duplicates[kvp.Key] = new List<DuplicateAsset> { duplicate };
            }

            Debug.Log($"[AutoSetup] Found {duplicates.Count} duplicate asset groups");
            return duplicates;
        }

        #endregion

        #region Mapping Rules

        public static MappingRuleSet LoadMappingRules()
        {
            if (File.Exists(AddressableSetupConstants.RULES_FILE))
            {
                string json = File.ReadAllText(AddressableSetupConstants.RULES_FILE);
                return JsonConvert.DeserializeObject<MappingRuleSet>(json);
            }

            return MappingRuleSet.GetDefault();
        }

        public static void SaveMappingRules(MappingRuleSet rules)
        {
            string json = JsonConvert.SerializeObject(rules, Formatting.Indented);

            string dir = System.IO.Path.GetDirectoryName(AddressableSetupConstants.RULES_FILE);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(AddressableSetupConstants.RULES_FILE, json);
            AssetDatabase.Refresh();

            Debug.Log($"[AutoSetup] Mapping rules saved to {AddressableSetupConstants.RULES_FILE}");
        }



        private static bool IsValidCosmosPath(string path)
        {
            return path.StartsWith(AddressableSetupConstants.BASE_PATH);
        }


        public static (int processed, int created) ApplyMappingRule(MappingRule rule)
        {
            int processedCount = 0;
            int createdGroups = 0;

            var matchingPaths = FindMatchingPaths(rule.pattern);
            var assetsByGroup = new Dictionary<string, List<string>>();

            foreach (var path in matchingPaths)
            {
                // ⭐ 경로 정규화
                string normalizedPath = path.Replace('\\', '/');

                string groupName = GenerateGroupName(rule.groupNameTemplate, normalizedPath);

                if (!assetsByGroup.ContainsKey(groupName))
                {
                    assetsByGroup[groupName] = new List<string>();
                }

                assetsByGroup[groupName].Add(path);

            }

            foreach (var kvp in assetsByGroup)
            {
                string groupName = kvp.Key;
                var assets = kvp.Value;

                var group = Settings.groups.Find(g => g.name == groupName);
                if (group == null)
                {
                    group = CreateGroup(groupName, rule);
                    createdGroups++;
                }

                foreach (var assetPath in assets)
                {
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrEmpty(guid)) continue;


                    var entry = Settings.CreateOrMoveEntry(guid, group);
                    if (entry != null)
                    {
                        entry.address = GenerateSmartAddress(assetPath, rule.addressTemplate, rule);

                        foreach (var labelTemplate in rule.autoLabels)
                        {
                            string label = GenerateLabel(labelTemplate, assetPath);
                            if (!entry.labels.Contains(label))
                            {
                                entry.labels.Add(label);
                            }
                        }

                        processedCount++;
                    }

                }
            }

            return (processedCount, createdGroups);
        }

        #endregion

        #region Group Management

        public static AddressableAssetGroup CreateGroup(string groupName, MappingRule rule)
        {
            var group = Settings.CreateGroup(groupName, false, false, false, null);

            var bundledSchema = group.AddSchema<BundledAssetGroupSchema>();
            var contentSchema = group.AddSchema<ContentUpdateGroupSchema>();

            if (rule.isLocal)
            {
                bundledSchema.BuildPath.SetVariableByName(Settings, "Local.BuildPath");
                bundledSchema.LoadPath.SetVariableByName(Settings, "Local.LoadPath");
            }
            else
            {
                bundledSchema.BuildPath.SetVariableByName(Settings, "Remote.BuildPath");
                bundledSchema.LoadPath.SetVariableByName(Settings, "Remote.LoadPath");
            }

            bundledSchema.Compression = rule.compression switch
            {
                "Uncompressed" => BundledAssetGroupSchema.BundleCompressionMode.Uncompressed,
                "LZ4" => BundledAssetGroupSchema.BundleCompressionMode.LZ4,
                "LZMA" => BundledAssetGroupSchema.BundleCompressionMode.LZMA,
                _ => BundledAssetGroupSchema.BundleCompressionMode.LZ4
            };

            bundledSchema.IncludeInBuild = rule.includeInBuild;
            bundledSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundledSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            bundledSchema.UseAssetBundleCrc = true;

            contentSchema.StaticContent = rule.isLocal;

            Debug.Log($"[AutoSetup] Created group: {groupName}");
            return group;
        }

        public static int CreateMissingGroups(MappingRuleSet rules)
        {
            int created = 0;
            var characters = DetectCharacters();
            var episodes = DetectEpisodes();
            var monthlyFolders = DetectMonthlyFolders();
            var scriptableObjectCategories = CategorizeScriptableObjects();

            Debug.Log($"[AutoSetup] Detecting missing groups...");
            Debug.Log($"[AutoSetup] Found: {characters.Count} characters, {episodes.Count} episodes, {monthlyFolders.Count} monthly, {scriptableObjectCategories.Count} SO folders");


            // Character groups
            foreach (var character in characters)
            {
                string groupName = $"Character_{character}";
                if (!Settings.groups.Any(g => g.name == groupName))
                {
                    var rule = rules.rules.Find(r => r.pattern.Contains("Characters/{CharName}"));
                    if (rule != null)
                    {
                        CreateGroup(groupName, rule);
                        created++;
                    }
                }
            }

            // Episode groups
            foreach (var episode in episodes)
            {
                string groupName = $"EventCut_{episode}";
                if (!Settings.groups.Any(g => g.name == groupName))
                {
                    var rule = rules.rules.Find(r => r.pattern.Contains("EventCut"));
                    if (rule != null)
                    {
                        CreateGroup(groupName, rule);
                        created++;
                    }
                }
            }

            // Monthly groups
            foreach (var monthly in monthlyFolders)
            {
                string groupName = $"Monthly_{monthly}_Characters";
                if (!Settings.groups.Any(g => g.name == groupName))
                {
                    var rule = rules.rules.Find(r => r.pattern.Contains("_MonthlyUpdate"));
                    if (rule != null)
                    {
                        CreateGroup(groupName, rule);
                        created++;
                    }
                }
            }

            // Dictionary의 Key를 올바르게 사용
            foreach (var category in scriptableObjectCategories.Keys)
            {
                // Common과 Others는 SO_Common으로 통합
                string groupName = (category == "Others" || category == "Common")
                    ? "SO_Common"
                    : $"SO_{category}";


                if (!Settings.groups.Any(g => g.name == groupName))
                {
                    // 카테고리명으로 올바른 룰 찾기
                    var rule = rules.rules.Find(r =>
                        r.groupNameTemplate == groupName ||
                        r.pattern.Contains($"ScriptableObjects/{category}/"));

                    if (rule != null)
                    {
                        CreateGroup(groupName, rule);
                        created++;
                        Debug.Log($"[AutoSetup] Created SO group: {groupName}");
                    }
                }
            }



            return created;
        }



        #endregion

        #region Address & Label Generation

        public static string GenerateSmartAddress(string assetPath, string template, MappingRule rule = null)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            string result = template;


            // ✨ 조건부 템플릿 확인 (새로 추가)
            if (rule != null && rule.conditionalTemplates != null && rule.conditionalTemplates.Count > 0)
            {
                foreach (var condition in rule.conditionalTemplates)
                {
                    // 간단한 패턴 매칭 (폴더명과 확장자 체크)
                    if (IsPathMatchCondition(assetPath, condition.Key))
                    {
                        result = condition.Value;
                        break;
                    }
                }
            }

            // Determine type
            string type = DetermineAssetType(assetPath);

            // Extract character name
            var charMatch = Regex.Match(assetPath, @"Characters/([^/]+)/");
            string charName = charMatch.Success ? charMatch.Groups[1].Value : "";

            // Extract episode
            var epMatch = Regex.Match(assetPath, @"(EP\d+[^/]*)");
            string episode = epMatch.Success ? epMatch.Groups[1].Value : "";

            string fileNameWithExt = System.IO.Path.GetFileName(assetPath);  // ✨ 확장자 포함 파일명
            string extension = System.IO.Path.GetExtension(assetPath);        // ✨ 확장자만 (.prefab)
            string extensionOnly = extension.TrimStart('.');        // ✨ 점 없는 확장자 (prefab)


            // 폴더명 추출 (새로 추가)
            string folderName = "";
            if (assetPath.Contains("/"))
            {
                var parts = assetPath.Split('/');
                if (parts.Length > 1)
                {
                    folderName = parts[parts.Length - 2]; // 파일의 부모 폴더
                }
            }

            result = result
                .Replace("{filename}", fileName)
                .Replace("{filenameExt}", fileNameWithExt)     // ✨ 새 변수
                .Replace("{ext}", extensionOnly)                // ✨ 새 변수
                .Replace("{.ext}", extension)                   // ✨ 새 변수
                .Replace("{type}", type)
                .Replace("{CharName}", charName)
                .Replace("{Episode}", episode)
                .Replace("{folder}", folderName );

            return result;
        }

        // ✨ 새로운 헬퍼 함수 추가
        private static bool IsPathMatchCondition(string assetPath, string condition)
        {
            // 간단한 와일드카드 패턴 매칭
            // 예: "*/Animations/*.anim" => Animations 폴더의 .anim 파일
            if (condition.Contains("*"))
            {
                string pattern = condition
                    .Replace("**/", ".*")
                    .Replace("*/", "[^/]+/")
                    .Replace("*.", @"\*\.")
                    .Replace("*", "[^/]*");

                return Regex.IsMatch(assetPath, pattern);
            }

            return assetPath.Contains(condition);
        }

        public static string GenerateLabel(string template, string assetPath)
        {
            string result = template;

            var charMatch = Regex.Match(assetPath, @"Characters/([^/]+)/");
            if (charMatch.Success)
            {
                result = result.Replace("{CharName}", charMatch.Groups[1].Value.ToLower());
            }

            var monthMatch = Regex.Match(assetPath, @"(\d{4}_\d{2})");
            if (monthMatch.Success)
            {
                result = result.Replace("{YYYY_MM}", monthMatch.Groups[1].Value);
            }

            var epMatch = Regex.Match(assetPath, @"EP(\d+)");
            if (epMatch.Success)
            {
                result = result.Replace("{Episode}", $"ep_{epMatch.Groups[1].Value}");
            }

            var seasonMatch = Regex.Match(assetPath, @"_Seasonal/([^/]+)/");
            if (seasonMatch.Success)
            {
                result = result.Replace("{SeasonName}", seasonMatch.Groups[1].Value.ToLower());
            }

            return result;
        }

        #endregion

        #region Helper Methods

        public static List<string> FindMatchingPaths(string pattern)
        {
            var results = new List<string>();

            // 패턴에서 기본 경로 추출
            string searchPath = AddressableSetupConstants.BASE_PATH;


            string regexPattern = pattern
                .Replace("**", ".*")
                .Replace("*", "[^/]*")
                .Replace("{CharName}", "([^/]+)")
                .Replace("{YYYY_MM}", @"(\d{4}_\d{2})")
                .Replace("{Episode}", @"(EP\d+[^/]*)")
                .Replace("{StageName}", "([^/]+)")
                .Replace("{SeasonName}", "([^/]+)");

            var allAssets = AssetDatabase.FindAssets("", new[] { searchPath })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !AssetDatabase.IsValidFolder(path));

            var regex = new Regex($"{searchPath}/{regexPattern}");

            foreach (var asset in allAssets)
            {
                if (regex.IsMatch(asset))
                {
                    results.Add(asset);
                }
            }
            Debug.Log($"[AutoSetup] AllCount : {allAssets.Count()} Pattern '{pattern}' found {results.Count} matching assets");

            return results;
        }

        public static string GenerateGroupName(string template, string assetPath)
        {
            string result = template;

            var charMatch = Regex.Match(assetPath, @"Characters/([^/]+)/");
            if (charMatch.Success)
            {
                result = result.Replace("{CharName}", charMatch.Groups[1].Value);
            }

            var monthMatch = Regex.Match(assetPath, @"(\d{4}_\d{2})");
            if (monthMatch.Success)
            {
                result = result.Replace("{YYYY_MM}", monthMatch.Groups[1].Value);
            }

            var epMatch = Regex.Match(assetPath, @"(EP\d+[^/]*)");
            if (epMatch.Success)
            {
                result = result.Replace("{Episode}", epMatch.Groups[1].Value);
            }

            var stageMatch = Regex.Match(assetPath, @"Stages/([^/]+)/");
            if (stageMatch.Success)
            {
                result = result.Replace("{StageName}", stageMatch.Groups[1].Value);
            }

            var seasonMatch = Regex.Match(assetPath, @"_Seasonal/([^/]+)/");
            if (seasonMatch.Success)
            {
                result = result.Replace("{SeasonName}", seasonMatch.Groups[1].Value);
            }

            return result;
        }

        private static string DetermineAssetType(string assetPath)
        {
            if (assetPath.Contains("Battle")) return "Battle";
            if (assetPath.Contains("Town")) return "Town";
            if (assetPath.Contains("Skill")) return "Skill";
            if (assetPath.Contains("Ultimate")) return "Ultimate";
            if (assetPath.Contains("Shop")) return "Shop";
            if (assetPath.Contains("Inventory")) return "Inventory";
            if (assetPath.Contains("UI")) return "UI";
            if (assetPath.Contains("Effect")) return "Effect";

            return System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(assetPath));
        }

        private static string GetFileHash(string path)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var stream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }

        private static bool CanMoveToShared(List<string> paths)
        {
            return paths.All(p => p.Contains("/Characters/") &&
                            (p.Contains("/Effects/") || p.Contains("/UI/")));
        }

        public static long CalculateFolderSize(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return 0;

            var info = new DirectoryInfo(folderPath);
            return info.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }

        public static string FormatBytes(long bytes)
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

        public static void CleanInvalidEntries()
        {
            int removed = 0;

            foreach (var group in Settings.groups)
            {
                var toRemove = new List<AddressableAssetEntry>();

                foreach (var entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        toRemove.Add(entry);
                    }
                }

                foreach (var entry in toRemove)
                {
                    group.RemoveAssetEntry(entry);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(Settings);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AutoSetup] Removed {removed} invalid entries");
            }
        }

        #endregion
    }
}
#endif