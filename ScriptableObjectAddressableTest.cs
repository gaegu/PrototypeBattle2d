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
    public class ScriptableObjectAddressableTest : EditorWindow
    {
        private Vector2 scrollPos;
        private AddressableAssetSettings settings;
        private Dictionary<string, List<string>> detectedSOs = new Dictionary<string, List<string>>();
        private Dictionary<string, string> currentGroupAssignments = new Dictionary<string, string>();
        private bool showOnlyProblems = false;
        private string testLog = "";

        [MenuItem("*COSMOS*/Test/SO Addressable Test")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScriptableObjectAddressableTest>("SO Test");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            settings = AddressableAssetSettingsDefaultObject.Settings;
            RefreshData();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawControlPanel();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawSOStatus();
            DrawTestLog();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ScriptableObject Addressable 테스트", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (settings == null)
            {
                EditorGUILayout.HelpBox("Addressable Settings not found!", MessageType.Error);
                if (GUILayout.Button("Create Settings"))
                {
                    settings = AddressableAssetSettings.Create(
                        AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                        AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                        true, true);
                }
                return;
            }
        }

        private void DrawControlPanel()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🔄 Refresh", GUILayout.Height(30)))
            {
                RefreshData();
            }

            if (GUILayout.Button("🧹 Clean SO Groups", GUILayout.Height(30)))
            {
                CleanSOGroups();
            }

            if (GUILayout.Button("🔨 Create SO Groups", GUILayout.Height(30)))
            {
                CreateSOGroups();
            }

            if (GUILayout.Button("⚡ Apply SO Rules", GUILayout.Height(30)))
            {
                ApplySOOnlyRules();
            }

            if (GUILayout.Button("🧪 Run Full Test", GUILayout.Height(30)))
            {
                RunFullTest();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            showOnlyProblems = EditorGUILayout.Toggle("Show Only Problems", showOnlyProblems);

            EditorGUILayout.Space();
        }

        private void DrawSOStatus()
        {
            EditorGUILayout.LabelField("📊 ScriptableObject 현황", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 카테고리별 상태
            foreach (var category in detectedSOs)
            {
                DrawCategoryStatus(category.Key, category.Value);
            }

            // 문제 있는 항목 강조
            if (currentGroupAssignments.Any(x => x.Value == "SO_Common" && !x.Key.Contains("/Common/")))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"⚠️ {currentGroupAssignments.Count(x => x.Value == "SO_Common" && !x.Key.Contains("/Common/"))} 개의 SO가 잘못된 그룹(SO_Common)에 있습니다!", MessageType.Warning);
            }
        }

        private void DrawCategoryStatus(string category, List<string> files)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 카테고리 헤더
            string expectedGroup = GetExpectedGroup(category);
            bool hasGroup = settings.groups.Any(g => g.name == expectedGroup);
            int correctCount = 0;
            int wrongCount = 0;

            foreach (var file in files)
            {
                if (currentGroupAssignments.ContainsKey(file))
                {
                    if (currentGroupAssignments[file] == expectedGroup)
                        correctCount++;
                    else
                        wrongCount++;
                }
            }

            // 상태 색상
            Color statusColor = wrongCount > 0 ? Color.red : (correctCount == files.Count ? Color.green : Color.yellow);

            EditorGUILayout.BeginHorizontal();
            GUI.color = statusColor;
            EditorGUILayout.LabelField($"📁 {category}: {files.Count} files", EditorStyles.boldLabel);
            GUI.color = Color.white;

            EditorGUILayout.LabelField($"Group: {expectedGroup} {(hasGroup ? "✅" : "❌")}", GUILayout.Width(200));
            EditorGUILayout.LabelField($"✓{correctCount} ✗{wrongCount}", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // 파일 상세 (문제 있는 것만 또는 전체)
            if (!showOnlyProblems || wrongCount > 0)
            {
                EditorGUI.indentLevel++;
                int displayCount = Mathf.Min(5, files.Count);

                for (int i = 0; i < displayCount; i++)
                {
                    DrawFileStatus(files[i], expectedGroup);
                }

                if (files.Count > displayCount)
                {
                    EditorGUILayout.LabelField($"... and {files.Count - displayCount} more");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFileStatus(string filePath, string expectedGroup)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string currentGroup = currentGroupAssignments.ContainsKey(filePath) ? currentGroupAssignments[filePath] : "None";
            bool isCorrect = currentGroup == expectedGroup;

            if (showOnlyProblems && isCorrect) return;

            EditorGUILayout.BeginHorizontal();

            GUI.color = isCorrect ? Color.green : (currentGroup == "None" ? Color.gray : Color.red);
            EditorGUILayout.LabelField($"  • {fileName}", EditorStyles.miniLabel, GUILayout.Width(200));

            EditorGUILayout.LabelField($"→ {currentGroup}", EditorStyles.miniLabel, GUILayout.Width(150));

            if (!isCorrect && currentGroup != "None")
            {
                EditorGUILayout.LabelField($"(Should be: {expectedGroup})", EditorStyles.miniLabel);
            }

            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestLog()
        {
            if (string.IsNullOrEmpty(testLog)) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("📝 Test Log", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(testLog, GUILayout.Height(100));
        }

        private void RefreshData()
        {
            testLog = "=== Refreshing Data ===\n";

            // SO 감지
            detectedSOs.Clear();
            detectedSOs["Characters"] = new List<string>();
            detectedSOs["Monsters"] = new List<string>();
            detectedSOs["MonsterGroups"] = new List<string>();
            detectedSOs["Common"] = new List<string>();

            string soPath = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects";

            if (Directory.Exists(soPath))
            {
                var allSOs = Directory.GetFiles(soPath, "*.asset", SearchOption.AllDirectories);

                foreach (var so in allSOs)
                {
                    // ⭐ Unity의 경로 정규화 메서드 사용
                    string unityPath = so.Replace('\\', '/');

                    // ⭐ 또는 Path.GetDirectoryName 사용
                    string directory = Path.GetDirectoryName(so).Replace('\\', '/');

                    if (directory.Contains("/Characters"))
                        detectedSOs["Characters"].Add(unityPath);
                    else if (directory.Contains("/MonsterGroups"))
                        detectedSOs["MonsterGroups"].Add(unityPath);
                    else if (directory.Contains("/Monsters"))
                        detectedSOs["Monsters"].Add(unityPath);
                    else if (directory.Contains("/Common"))
                        detectedSOs["Common"].Add(unityPath);
                }

                testLog += $"Found: Characters({detectedSOs["Characters"].Count}), ";
                testLog += $"Monsters({detectedSOs["Monsters"].Count}), ";
                testLog += $"MonsterGroups({detectedSOs["MonsterGroups"].Count}), ";
                testLog += $"Common({detectedSOs["Common"].Count})\n";
            }

            // 현재 그룹 할당 확인
            currentGroupAssignments.Clear();

            foreach (var group in settings.groups)
            {
                foreach (var entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (path.Contains("ScriptableObjects/"))
                    {
                        currentGroupAssignments[path] = group.name;
                    }
                }
            }

            testLog += $"Currently assigned: {currentGroupAssignments.Count} SOs\n";

            // 문제 체크
            int wrongAssignments = 0;
            foreach (var kvp in currentGroupAssignments)
            {
                string expected = GetExpectedGroupForPath(kvp.Key);
                if (kvp.Value != expected)
                {
                    wrongAssignments++;
                    testLog += $"❌ Wrong: {Path.GetFileNameWithoutExtension(kvp.Key)} is in {kvp.Value}, should be {expected}\n";
                }
            }

            if (wrongAssignments > 0)
            {
                testLog += $"\n⚠️ Total wrong assignments: {wrongAssignments}\n";
            }
            else
            {
                testLog += "\n✅ All assignments correct!\n";
            }

            Repaint();
        }

        private void CleanSOGroups()
        {
            testLog = "=== Cleaning SO Groups ===\n";

            // SO 관련 그룹 제거
            var soGroups = settings.groups.Where(g =>
                g.name == "SO_Characters" ||
                g.name == "SO_Monsters" ||
                g.name == "SO_MonsterGroups" ||
                g.name == "SO_Common").ToList();

            foreach (var group in soGroups)
            {
                testLog += $"Removing group: {group.name}\n";
                settings.RemoveGroup(group);
            }

            AssetDatabase.SaveAssets();
            RefreshData();
        }

        private void CreateSOGroups()
        {
            testLog = "=== Creating SO Groups ===\n";

            string[] groupNames = { "SO_Characters", "SO_Monsters", "SO_MonsterGroups", "SO_Common" };

            foreach (var groupName in groupNames)
            {
                if (!settings.groups.Any(g => g.name == groupName))
                {
                    var group = settings.CreateGroup(groupName, false, false, false, null);
                    testLog += $"Created group: {groupName}\n";
                }
                else
                {
                    testLog += $"Group already exists: {groupName}\n";
                }
            }

            AssetDatabase.SaveAssets();
            RefreshData();
        }

        private void ApplySOOnlyRules()
        {
            testLog = "=== Applying SO Rules ===\n";

            // 순서 중요: 구체적인 것부터 처리
            ProcessCategory("Characters", "SO_Characters");
            ProcessCategory("Monsters", "SO_Monsters");
            ProcessCategory("MonsterGroups", "SO_MonsterGroups");
            ProcessCategory("Common", "SO_Common");

            AssetDatabase.SaveAssets();
            RefreshData();
        }

        private void ProcessCategory(string category, string groupName)
        {
            var group = settings.groups.FirstOrDefault(g => g.name == groupName);
            if (group == null)
            {
                testLog += $"❌ Group not found: {groupName}\n";
                return;
            }

            int processed = 0;
            foreach (var filePath in detectedSOs[category])
            {
                var guid = AssetDatabase.AssetPathToGUID(filePath);
                if (string.IsNullOrEmpty(guid)) continue;

                // 이미 올바른 그룹에 있는지 확인
                var existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null && existingEntry.parentGroup.name == groupName)
                {
                    continue; // 이미 올바른 그룹에 있음
                }

                // 그룹에 추가/이동
                var entry = settings.CreateOrMoveEntry(guid, group);
                if (entry != null)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    entry.address = $"SO_{category}_{fileName}";
                    processed++;
                }
            }

            testLog += $"Processed {category}: {processed} files → {groupName}\n";
        }

        private void RunFullTest()
        {
            testLog = "=== Running Full Test ===\n";
            testLog += DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n";

            // 1. Clean
            testLog += "Step 1: Cleaning...\n";
            CleanSOGroups();

            // 2. Create Groups
            testLog += "\nStep 2: Creating groups...\n";
            CreateSOGroups();

            // 3. Apply Rules
            testLog += "\nStep 3: Applying rules...\n";
            ApplySOOnlyRules();

            // 4. Verify
            testLog += "\nStep 4: Verifying...\n";
            RefreshData();

            // 5. Result
            bool allCorrect = !currentGroupAssignments.Any(x =>
                x.Value != GetExpectedGroupForPath(x.Key));

            if (allCorrect)
            {
                testLog += "\n✅ TEST PASSED: All ScriptableObjects are in correct groups!\n";
                EditorUtility.DisplayDialog("Test Passed", "All ScriptableObjects are correctly assigned!", "OK");
            }
            else
            {
                testLog += "\n❌ TEST FAILED: Some ScriptableObjects are in wrong groups!\n";
                EditorUtility.DisplayDialog("Test Failed", "Some ScriptableObjects are incorrectly assigned. Check the log.", "OK");
            }
        }

        private string GetExpectedGroup(string category)
        {
            return category switch
            {
                "Characters" => "SO_Characters",
                "Monsters" => "SO_Monsters",
                "MonsterGroups" => "SO_MonsterGroups",
                "Common" => "SO_Common",
                _ => "SO_Common"
            };
        }

        private string GetExpectedGroupForPath(string path)
        {
            if (path.Contains("/Characters/")) return "SO_Characters";
            if (path.Contains("/MonsterGroups/")) return "SO_MonsterGroups";
            if (path.Contains("/Monsters/")) return "SO_Monsters";
            if (path.Contains("/Common/")) return "SO_Common";
            return "SO_Common";
        }
    }
}
#endif