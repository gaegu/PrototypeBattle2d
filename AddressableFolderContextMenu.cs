#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace GameCore.Editor.Addressables
{
    /// <summary>
    /// 폴더 우클릭 메뉴로 해당 폴더만 어드레서블 설정
    /// AddressableAutoSetup과 동일한 Core 로직 사용
    /// </summary>
    public static class AddressableFolderContextMenu
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        #region Context Menu Items

        /// <summary>
        /// 폴더 우클릭 → Setup Addressable
        /// </summary>
        [MenuItem("Assets/*COSMOS*/Addressable/Setup This Folder", false, 1000)]
        public static void SetupSelectedFolder()
        {
            var selectedFolders = GetSelectedFolders();
            if (selectedFolders.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Please select a folder first", "OK");
                return;
            }

            // AddressableAutoSetup과 동일한 방식으로 처리
            var stats = new SetupStatistics();
            var rules = AddressableAutoSetupCore.LoadMappingRules();

            // 우선순위로 정렬 (AddressableAutoSetup의 ApplyAllMappingRules와 동일)
            var sortedRules = rules.rules
                .OrderBy(r => r.priority)
                .ToList();

            foreach (var folder in selectedFolders)
            {
                // 각 폴더에 대해 매칭되는 규칙만 적용
                foreach (var rule in sortedRules)
                {
                    // 해당 폴더 내의 에셋만 필터링
                    var matchingPaths = AddressableAutoSetupCore.FindMatchingPaths(rule.pattern)
                        .Where(path => path.StartsWith(folder))
                        .ToList();

                    if (matchingPaths.Count > 0)
                    {
                        // ✨ 로그 14: 폴더 내 매칭
                        Debug.LogError($"[FolderSetup] Rule {rule.pattern} matches {matchingPaths.Count} files in {folder}");


                        // 폴더별로 필터링된 경로로 규칙 적용
                        var result = ApplyRuleToFilteredPaths(rule, matchingPaths);
                        stats.assetsProcessed += result.processed;
                        stats.groupsCreated += result.created;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = $"Setup Complete!\n\n" +
                           $"Processed: {stats.assetsProcessed} assets\n" +
                           $"Groups Created: {stats.groupsCreated}";

            EditorUtility.DisplayDialog("Setup Complete", message, "OK");
        }

        /// <summary>
        /// 폴더 우클릭 → Quick Setup (미리보기 없이 즉시 적용)
        /// </summary>
        [MenuItem("Assets/*COSMOS*/Addressable/Quick Setup (No Preview)", false, 1001)]
        public static void QuickSetupSelectedFolder()
        {
            var selectedFolders = GetSelectedFolders();
            if (selectedFolders.Count == 0) return;

            var stats = new SetupStatistics();
            var rules = AddressableAutoSetupCore.LoadMappingRules();
            var sortedRules = rules.rules.OrderBy(r => r.priority).ToList();

            foreach (var folder in selectedFolders)
            {
                foreach (var rule in sortedRules)
                {
                    var matchingPaths = AddressableAutoSetupCore.FindMatchingPaths(rule.pattern)
                        .Where(path => path.StartsWith(folder))
                        .ToList();

                    if (matchingPaths.Count > 0)
                    {
                        var result = ApplyRuleToFilteredPaths(rule, matchingPaths);
                        stats.assetsProcessed += result.processed;
                        stats.groupsCreated += result.created;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FolderSetup] Quick setup complete: {stats.assetsProcessed} assets processed, {stats.groupsCreated} groups created");
        }

        /// <summary>
        /// 폴더 우클릭 → Preview Setup (실행하지 않고 미리보기)
        /// </summary>
        [MenuItem("Assets/*COSMOS*/Addressable/Preview Setup", false, 1002)]
        public static void PreviewSetup()
        {
            var selectedFolders = GetSelectedFolders();
            if (selectedFolders.Count == 0) return;

            var window = EditorWindow.GetWindow<FolderSetupPreviewWindow>("Setup Preview");
            window.Initialize(selectedFolders);
            window.Show();
        }

        /// <summary>
        /// 폴더 우클릭 → Remove from Addressable
        /// </summary>
        [MenuItem("Assets/*COSMOS*/Addressable/Remove from Addressable", false, 1003)]
        public static void RemoveFromAddressable()
        {
            var selectedFolders = GetSelectedFolders();
            if (selectedFolders.Count == 0) return;

            if (!EditorUtility.DisplayDialog("Confirm",
                $"Remove {selectedFolders.Count} folder(s) from Addressable?",
                "Yes", "No"))
                return;

            int removed = 0;
            foreach (var folder in selectedFolders)
            {
                removed += RemoveFolderFromAddressable(folder);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Complete", $"Removed {removed} assets from Addressable", "OK");
        }

        #endregion

        #region Core Processing

        /// <summary>
        /// 필터링된 경로에 대해 규칙 적용 (AddressableAutoSetupCore.ApplyMappingRule과 동일한 로직)
        /// </summary>
        private static (int processed, int created) ApplyRuleToFilteredPaths(MappingRule rule, List<string> filteredPaths)
        {
            int processedCount = 0;
            int createdGroups = 0;
            var assetsByGroup = new Dictionary<string, List<string>>();

            foreach (var path in filteredPaths)
            {
                // 경로 정규화
                string normalizedPath = path.Replace('\\', '/');
                string groupName = AddressableAutoSetupCore.GenerateGroupName(rule.groupNameTemplate, normalizedPath);

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
                    group = AddressableAutoSetupCore.CreateGroup(groupName, rule);
                    createdGroups++;
                }

                foreach (var assetPath in assets)
                {
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrEmpty(guid)) continue;

                    var entry = Settings.CreateOrMoveEntry(guid, group);
                    if (entry != null)
                    {
                        entry.address = AddressableAutoSetupCore.GenerateSmartAddress(assetPath, rule.addressTemplate, rule);

                        // 레이블 설정
                        entry.labels.Clear();
                        foreach (var labelTemplate in rule.autoLabels)
                        {
                            string label = AddressableAutoSetupCore.GenerateLabel(labelTemplate, assetPath);
                            if (!string.IsNullOrEmpty(label) && !entry.labels.Contains(label))
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

        #region Validation

        [MenuItem("Assets/*COSMOS*/Setup This Folder", true)]
        [MenuItem("Assets/*COSMOS*/Quick Setup (No Preview)", true)]
        [MenuItem("Assets/*COSMOS*/Preview Setup", true)]
        [MenuItem("Assets/*COSMOS*/Remove from Addressable", true)]
        private static bool ValidateSelectedFolder()
        {
            return GetSelectedFolders().Count > 0;
        }

        #endregion

        #region Preview Window

        /// <summary>
        /// 미리보기 윈도우 - Core 로직 사용
        /// </summary>
        private class FolderSetupPreviewWindow : EditorWindow
        {
            private List<string> _folders;
            private MappingRuleSet _rules;
            private Vector2 _scrollPos;
            private Dictionary<string, PreviewResult> _previewResults;

            public void Initialize(List<string> folders)
            {
                _folders = folders;
                _rules = AddressableAutoSetupCore.LoadMappingRules();
                AnalyzeFolders();
            }

            private void AnalyzeFolders()
            {
                _previewResults = new Dictionary<string, PreviewResult>();

                foreach (var folder in _folders)
                {
                    var result = new PreviewResult { folderPath = folder };

                    // 적용될 규칙 찾기
                    foreach (var rule in _rules.rules.OrderBy(r => r.priority))
                    {
                        var matchingPaths = AddressableAutoSetupCore.FindMatchingPaths(rule.pattern)
                            .Where(path => path.StartsWith(folder))
                            .ToList();

                        if (matchingPaths.Count > 0)
                        {
                            result.applicableRules.Add(rule);
                            result.affectedAssets.AddRange(matchingPaths);

                            // 그룹명 예측
                            var predictedGroups = new HashSet<string>();
                            foreach (var path in matchingPaths.Take(5)) // 샘플로 5개만
                            {
                                string groupName = AddressableAutoSetupCore.GenerateGroupName(
                                    rule.groupNameTemplate,
                                    path.Replace('\\', '/'));
                                predictedGroups.Add(groupName);
                            }
                            result.predictedGroups.AddRange(predictedGroups);
                        }
                    }

                    _previewResults[folder] = result;
                }
            }

            private void OnGUI()
            {
                if (_previewResults == null)
                {
                    EditorGUILayout.LabelField("Analyzing...");
                    return;
                }

                EditorGUILayout.LabelField("Setup Preview", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

                foreach (var kvp in _previewResults)
                {
                    var result = kvp.Value;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"📁 {result.folderPath}", EditorStyles.boldLabel);

                    if (result.applicableRules.Count == 0)
                    {
                        EditorGUILayout.LabelField("⚠️ No matching rules found", EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Rules: {result.applicableRules.Count}, Assets: {result.affectedAssets.Count}");

                        if (result.showDetails = EditorGUILayout.Foldout(result.showDetails, "Details"))
                        {
                            EditorGUI.indentLevel++;

                            // 적용될 규칙들
                            EditorGUILayout.LabelField("Rules to Apply:", EditorStyles.miniBoldLabel);
                            foreach (var rule in result.applicableRules)
                            {
                                EditorGUILayout.LabelField($"• Pattern: {rule.pattern}", EditorStyles.miniLabel);
                                EditorGUILayout.LabelField($"  Template: {rule.groupNameTemplate}", EditorStyles.miniLabel);
                            }

                            // 생성될 그룹 예측
                            if (result.predictedGroups.Count > 0)
                            {
                                EditorGUILayout.Space();
                                EditorGUILayout.LabelField("Groups to Create/Use:", EditorStyles.miniBoldLabel);
                                foreach (var group in result.predictedGroups)
                                {
                                    EditorGUILayout.LabelField($"• {group}", EditorStyles.miniLabel);
                                }
                            }

                            // 영향받을 에셋 샘플
                            if (result.affectedAssets.Count > 0)
                            {
                                EditorGUILayout.Space();
                                EditorGUILayout.LabelField($"Sample Assets ({Math.Min(5, result.affectedAssets.Count)} of {result.affectedAssets.Count}):",
                                    EditorStyles.miniBoldLabel);
                                foreach (var asset in result.affectedAssets.Take(5))
                                {
                                    EditorGUILayout.LabelField($"  - {Path.GetFileName(asset)}", EditorStyles.miniLabel);
                                }
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Apply Changes", GUILayout.Height(30)))
                {
                    ApplyChanges();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(30)))
                {
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }

            private void ApplyChanges()
            {
                var stats = new SetupStatistics();
                var sortedRules = _rules.rules.OrderBy(r => r.priority).ToList();

                foreach (var folder in _folders)
                {
                    foreach (var rule in sortedRules)
                    {
                        var matchingPaths = AddressableAutoSetupCore.FindMatchingPaths(rule.pattern)
                            .Where(path => path.StartsWith(folder))
                            .ToList();

                        if (matchingPaths.Count > 0)
                        {
                            var result = ApplyRuleToFilteredPaths(rule, matchingPaths);
                            stats.assetsProcessed += result.processed;
                            stats.groupsCreated += result.created;
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Complete",
                    $"Processed: {stats.assetsProcessed} assets\n" +
                    $"Groups Created: {stats.groupsCreated}",
                    "OK");

                Close();
            }

            private class PreviewResult
            {
                public string folderPath;
                public List<MappingRule> applicableRules = new List<MappingRule>();
                public List<string> affectedAssets = new List<string>();
                public List<string> predictedGroups = new List<string>();
                public bool showDetails;
            }
        }

        #endregion

        #region Helper Methods

        private static List<string> GetSelectedFolders()
        {
            var folders = new List<string>();
            var selectedObjects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                {
                    // Cosmos 폴더 체크
                    if (!path.StartsWith(AddressableSetupConstants.BASE_PATH))
                    {
                        Debug.LogWarning($"[FolderSetup] Skipping non-Cosmos folder: {path}");
                        continue;
                    }
                    folders.Add(path);
                }
            }

            return folders;
        }

        private static int RemoveFolderFromAddressable(string folderPath)
        {
            int removed = 0;
            var guids = AssetDatabase.FindAssets("", new[] { folderPath });

            foreach (var guid in guids)
            {
                var entry = Settings.FindAssetEntry(guid);
                if (entry != null)
                {
                    Settings.RemoveAssetEntry(guid);
                    removed++;
                }
            }

            Debug.Log($"[FolderSetup] Removed {removed} assets from Addressable");
            return removed;
        }

        #endregion
    }
}
#endif