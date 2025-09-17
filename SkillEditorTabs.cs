// =====================================================
// SkillEditorTabs.cs
// 에디터의 나머지 탭들 - Editor 폴더에 저장
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR

namespace SkillSystem.Editor
{
    public partial class AdvancedSkillEditorWindow
    {
        // =====================================================
        // 효과 라이브러리 탭
        // =====================================================

        private void DrawEffectLibraryTab()
        {
            EditorGUILayout.LabelField("📚 효과 라이브러리", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // 좌측: 카테고리
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawEffectCategories();
            EditorGUILayout.EndVertical();

            // 우측: 효과 목록
            EditorGUILayout.BeginVertical();
            DrawEffectPresets();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEffectCategories()
        {
            EditorGUILayout.LabelField("카테고리", EditorStyles.boldLabel);

            var categories = AdvancedEffectPresets.GetCategories();
            foreach (var category in categories)
            {
                var style = category == selectedEffectCategory ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Button(category, style))
                {
                    selectedEffectCategory = category;
                }
            }
        }

        private void DrawEffectPresets()
        {
            var presets = AdvancedEffectPresets.GetPresetsForCategory(selectedEffectCategory);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var preset in presets)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"{GetEffectIcon(preset.type)} {preset.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(preset.GetFullDescription());

                if (!string.IsNullOrEmpty(preset.tooltip))
                {
                    EditorGUILayout.LabelField(preset.tooltip, EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical(GUILayout.Width(150));

                if (currentSkill != null && GUILayout.Button("현재 스킬에 추가"))
                {
                    currentSkill.effects.Add(preset.Clone());
                    currentTab = EditorTab.SkillCreator;
                }

                if (GUILayout.Button("복사"))
                {
                    copiedEffect = preset.Clone();
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // =====================================================
        // 템플릿 탭
        // =====================================================

        private void DrawTemplatesTab()
        {
            EditorGUILayout.LabelField("📁 스킬 템플릿", EditorStyles.boldLabel);

            // 클래스별 추천 템플릿
            var templates = GetSkillTemplates();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var classTemplates in templates)
            {
                EditorGUILayout.LabelField(classTemplates.Key.ToString(), EditorStyles.boldLabel);

                foreach (var template in classTemplates.Value)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(template.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"티어 {template.tier} ");
                    EditorGUILayout.LabelField(template.description);
                    EditorGUILayout.EndVertical();

                    if (GUILayout.Button("사용", GUILayout.Width(60)))
                    {
                        currentSkill = template.CreateSkill();
                        currentTab = EditorTab.SkillCreator;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        // =====================================================
        // 일괄 편집 탭
        // =====================================================

        private void DrawBatchEditTab()
        {
            EditorGUILayout.LabelField("🔧 일괄 편집", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // 좌측: 스킬 선택
            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            DrawBatchSkillSelection();
            EditorGUILayout.EndVertical();

            // 우측: 편집 작업
            EditorGUILayout.BeginVertical();
            DrawBatchOperations();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBatchSkillSelection()
        {
            EditorGUILayout.LabelField("스킬 선택", EditorStyles.boldLabel);

            // 빠른 선택
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("모두 선택"))
            {
                selectedSkills = database.GetAllSkills().ToList();
            }
            if (GUILayout.Button("선택 해제"))
            {
                selectedSkills.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // 필터 선택
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("클래스별 선택"))
            {
                var menu = new GenericMenu();
                foreach (CharacterClass charClass in Enum.GetValues(typeof(CharacterClass)))
                {
                    if (charClass != CharacterClass.All)
                    {
                        var localClass = charClass;
                        menu.AddItem(new GUIContent(charClass.ToString()), false,
                            () => selectedSkills = database.GetSkillsByClass(localClass));
                    }
                }
                menu.ShowAsContext();
            }
            if (GUILayout.Button("티어별 선택"))
            {
                var menu = new GenericMenu();
                for (int tier = 0; tier <= 5; tier++)
                {
                    var localTier = tier;
                    menu.AddItem(new GUIContent($"티어 {tier}"), false,
                        () => selectedSkills = database.GetSkillsByTier(localTier));
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"선택된 스킬: {selectedSkills.Count}개");

            // 선택된 스킬 목록
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

            for (int i = 0; i < selectedSkills.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(selectedSkills[i].skillName);
                if (GUILayout.Button("제거", GUILayout.Width(50)))
                {
                    selectedSkills.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBatchOperations()
        {
            EditorGUILayout.LabelField("편집 작업", EditorStyles.boldLabel);

            batchOperation = (BatchEditOperation)EditorGUILayout.EnumPopup("작업 선택", batchOperation);

            EditorGUILayout.Space();

            switch (batchOperation)
            {
                case BatchEditOperation.ChangeTier:
                    DrawBatchChangeTier();
                    break;
                case BatchEditOperation.AddEffect:
                    DrawBatchAddEffect();
                    break;
                case BatchEditOperation.RemoveEffect:
                    DrawBatchRemoveEffect();
                    break;
                case BatchEditOperation.ScaleValues:
                    DrawBatchScaleValues();
                    break;
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("일괄 적용", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("확인",
                    $"{selectedSkills.Count}개 스킬을 수정하시겠습니까?", "예", "아니오"))
                {
                    ApplyBatchOperation();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawBatchChangeTier()
        {
            batchTierChange = EditorGUILayout.IntSlider("티어 변경", batchTierChange, -2, 2);
            EditorGUILayout.HelpBox($"선택된 스킬의 티어를 {batchTierChange:+#;-#;0} 만큼 변경합니다.", MessageType.Info);
        }


        private void DrawBatchAddEffect()
        {
            if (batchEffectToAdd == null)
            {
                batchEffectToAdd = new AdvancedSkillEffect();
            }

            EditorGUILayout.LabelField("추가할 효과:");
            batchEffectToAdd.type = (EffectType)EditorGUILayout.EnumPopup("타입", batchEffectToAdd.type);
            batchEffectToAdd.value = EditorGUILayout.FloatField("값", batchEffectToAdd.value);
            batchEffectToAdd.duration = EditorGUILayout.IntField("지속 시간", batchEffectToAdd.duration);

            EditorGUILayout.HelpBox("선택된 모든 스킬에 이 효과를 추가합니다.", MessageType.Info);
        }

        private void DrawBatchRemoveEffect()
        {
            batchEffectTypeToRemove = (EffectType)EditorGUILayout.EnumPopup("제거할 효과 타입", batchEffectTypeToRemove);
            EditorGUILayout.HelpBox($"선택된 스킬에서 {batchEffectTypeToRemove} 타입의 효과를 모두 제거합니다.", MessageType.Info);
        }

        private void DrawBatchScaleValues()
        {
            batchScaleFactor = EditorGUILayout.Slider("배율", batchScaleFactor, 0.5f, 2.0f);
            EditorGUILayout.HelpBox($"모든 수치를 {batchScaleFactor:F2}배로 조정합니다.", MessageType.Info);
        }

        private void ApplyBatchOperation()
        {
            foreach (var skill in selectedSkills)
            {
                switch (batchOperation)
                {
                    case BatchEditOperation.ChangeTier:
                        skill.tier = Mathf.Clamp(skill.tier + batchTierChange, 0, 5);
                        break;

                    case BatchEditOperation.AddEffect:
                        if (batchEffectToAdd != null)
                            skill.effects.Add(batchEffectToAdd.Clone());
                        break;

                    case BatchEditOperation.RemoveEffect:
                        skill.effects.RemoveAll(e => e.type == batchEffectTypeToRemove);
                        break;

                    case BatchEditOperation.ScaleValues:
                        foreach (var effect in skill.effects)
                        {
                            effect.value *= batchScaleFactor;
                        }
                        break;
                }

                skill.lastModified = DateTime.Now;
            }

            SaveDatabase();
            EditorUtility.DisplayDialog("완료", $"{selectedSkills.Count}개 스킬이 수정되었습니다.", "확인");
        }


        // 마이그레이션 관련 필드
        private bool showMigrationTool = false;
        private Dictionary<int, List<AdvancedSkillData>> bpSkillGroups = new Dictionary<int, List<AdvancedSkillData>>();
        private List<int> selectedForMigration = new List<int>();

        // 필드 추가 (명확한 구분을 위해)
        private AdvancedSkillData selectedBPBaseSkill;  // BP 탭에서 선택된 기본 스킬
        private SkillUpgradePath selectedUpgradePath;   // BP 탭에서 선택된 경로


        private void DrawBPUpgradeTab()
        {
            EditorGUILayout.LabelField("⚡ BP 업그레이드 설정", EditorStyles.boldLabel);


            // 마이그레이션 툴 토글
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(showMigrationTool ? "▼ 마이그레이션 툴 숨기기" : "▶ 마이그레이션 툴 표시",
                GUILayout.Height(25)))
            {
                showMigrationTool = !showMigrationTool;
                if (showMigrationTool)
                {
                    AnalyzeBPSkills();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (showMigrationTool)
            {
                DrawMigrationTool();
                EditorGUILayout.Space(20);
            }


            EditorGUILayout.BeginHorizontal();

            // 왼쪽: 스킬 선택
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawBPSkillSelector();
            EditorGUILayout.EndVertical();

            // 중앙: 업그레이드 경로 편집
            EditorGUILayout.BeginVertical();
            DrawBPUpgradePathEditor();
            EditorGUILayout.EndVertical();

            // 오른쪽: 미리보기
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            DrawBPUpgradePreview();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // === 마이그레이션 툴 UI ===
        private void DrawMigrationTool()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔄 기존 BP 스킬 마이그레이션", EditorStyles.boldLabel);

            // BP 데이터베이스 체크
            if (bpDatabase == null)
            {
                EditorGUILayout.HelpBox("먼저 BP Database를 선택하거나 생성하세요.", MessageType.Warning);
                if (GUILayout.Button("새 BP Database 생성"))
                {
                    CreateNewBPDatabase();
                }
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // 왼쪽: BP 스킬 그룹 목록
            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            DrawBPSkillGroups();
            EditorGUILayout.EndVertical();

            // 오른쪽: 선택된 그룹 상세
            EditorGUILayout.BeginVertical();
            DrawSelectedGroupDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // 하단: 액션 버튼
            DrawMigrationActions();

            EditorGUILayout.EndVertical();
        }

        // === BP 스킬 분석 ===
        private void AnalyzeBPSkills()
        {
            bpSkillGroups.Clear();
            var allSkills = database.GetAllSkills();

            foreach (var skill in allSkills)
            {
                // 패턴 1: parentSkillId가 설정된 경우
                if (skill.parentSkillId > 0)
                {
                    if (!bpSkillGroups.ContainsKey(skill.parentSkillId))
                    {
                        bpSkillGroups[skill.parentSkillId] = new List<AdvancedSkillData>();

                        // 부모 스킬도 추가
                        var parentSkill = allSkills.FirstOrDefault(s => s.skillId == skill.parentSkillId);
                        if (parentSkill != null)
                        {
                            bpSkillGroups[skill.parentSkillId].Add(parentSkill);
                        }
                    }
                    bpSkillGroups[skill.parentSkillId].Add(skill);
                }
                // 패턴 2: ID 규칙 (+1000, +2000, +3000)
                else if (skill.skillId % 1000 == 0)
                {
                    int baseId = skill.skillId;
                    if (!bpSkillGroups.ContainsKey(baseId))
                    {
                        bpSkillGroups[baseId] = new List<AdvancedSkillData>();
                        bpSkillGroups[baseId].Add(skill);

                        // 관련 BP 스킬 찾기
                        var bp1 = allSkills.FirstOrDefault(s => s.skillId == baseId + 1000);
                        var bp3 = allSkills.FirstOrDefault(s => s.skillId == baseId + 2000);
                        var bp5 = allSkills.FirstOrDefault(s => s.skillId == baseId + 3000);

                        if (bp1 != null) bpSkillGroups[baseId].Add(bp1);
                        if (bp3 != null) bpSkillGroups[baseId].Add(bp3);
                        if (bp5 != null) bpSkillGroups[baseId].Add(bp5);
                    }
                }
                // 패턴 3: 이름 패턴 (스킬명+, 스킬명++, 스킬명 MAX)
                else if (skill.skillName.EndsWith(" MAX") ||
                         skill.skillName.EndsWith("++") ||
                         skill.skillName.EndsWith("+"))
                {
                    string baseName = skill.skillName
                        .Replace(" MAX", "")
                        .Replace("++", "")
                        .Replace("+", "")
                        .Trim();

                    var baseSkill = allSkills.FirstOrDefault(s => s.skillName == baseName);
                    if (baseSkill != null)
                    {
                        int baseId = baseSkill.skillId;
                        if (!bpSkillGroups.ContainsKey(baseId))
                        {
                            bpSkillGroups[baseId] = new List<AdvancedSkillData>();
                            bpSkillGroups[baseId].Add(baseSkill);
                        }
                        if (!bpSkillGroups[baseId].Contains(skill))
                        {
                            bpSkillGroups[baseId].Add(skill);
                        }
                    }
                }
            }

            // 단일 스킬만 있는 그룹 제거
            var keysToRemove = bpSkillGroups.Where(kvp => kvp.Value.Count <= 1).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                bpSkillGroups.Remove(key);
            }

            Debug.Log($"[Migration] Found {bpSkillGroups.Count} BP skill groups");
        }

        private void DrawBPSkillSelector()
        {
            EditorGUILayout.LabelField("기본 스킬 선택", EditorStyles.boldLabel);

            // BP 데이터베이스 선택
            bpDatabase = EditorGUILayout.ObjectField("BP Database", bpDatabase,
                typeof(BPUpgradeDatabase), false) as BPUpgradeDatabase;

            if (bpDatabase == null)
            {
                if (GUILayout.Button("새 BP Database 생성"))
                {
                    CreateNewBPDatabase();
                }
                return;
            }

            EditorGUILayout.Space();

            // 부모 스킬만 표시 (bpLevel == 0)
            var baseSkills = database.GetAllSkills()
                .Where(s => s.bpLevel == 0 || s.parentSkillId == 0)
                .OrderBy(s => s.skillId);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var skill in baseSkills)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // 스킬 정보
                EditorGUILayout.LabelField($"[{skill.skillId}] {skill.skillName}",
                    GUILayout.Width(200));

                // BP 경로 존재 여부
                bool hasPath = HasBPUpgradePath(skill.skillId);
                GUI.color = hasPath ? Color.green : Color.white;
                EditorGUILayout.LabelField(hasPath ? "✓" : "-", GUILayout.Width(20));
                GUI.color = Color.white;

                // 선택 버튼
                if (GUILayout.Button("선택", GUILayout.Width(50)))
                {
                    SelectSkillForBPUpgrade(skill);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBPUpgradePathEditor()
        {
            EditorGUILayout.LabelField("업그레이드 경로 편집", EditorStyles.boldLabel);

            // selectedBPBaseSkill 사용 (currentSkill 대신)
            if (selectedBPBaseSkill == null)
            {
                EditorGUILayout.HelpBox("왼쪽 마이그레이션 툴에서 '편집' 버튼을 클릭하거나\n아래에서 기본 스킬을 선택하세요", MessageType.Info);

                // 수동 선택 옵션
                EditorGUILayout.Space();
                if (GUILayout.Button("기본 스킬 선택하기"))
                {
                    // 스킬 선택 팝업 또는 드롭다운
                    ShowSkillSelectionPopup();
                }
                return;
            }

            // selectedUpgradePath 사용 (currentUpgradePath 대신)
            if (selectedUpgradePath == null)
            {
                selectedUpgradePath = new SkillUpgradePath
                {
                    baseSkillId = selectedBPBaseSkill.skillId,
                    pathName = $"{selectedBPBaseSkill.skillName} 업그레이드",
                    pathDescription = $"{selectedBPBaseSkill.skillName}의 BP 업그레이드 경로"
                };
            }

            // 현재 편집중인 스킬 표시
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = Color.cyan;
            EditorGUILayout.LabelField($"편집중: [{selectedBPBaseSkill.skillId}] {selectedBPBaseSkill.skillName}", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 경로 기본 정보
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            selectedUpgradePath.pathName = EditorGUILayout.TextField("경로 이름", selectedUpgradePath.pathName);
            EditorGUILayout.LabelField("경로 설명");
            selectedUpgradePath.pathDescription = EditorGUILayout.TextArea(
                selectedUpgradePath.pathDescription ?? "", GUILayout.Height(40));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // BP 레벨별 업그레이드 - selectedUpgradePath 사용
            DrawBPLevelUpgrade("BP 1-2 업그레이드", ref selectedUpgradePath.upgrade1,
                BPUpgradeLevel.Upgrade1, 1);
            DrawBPLevelUpgrade("BP 3-4 업그레이드", ref selectedUpgradePath.upgrade2,
                BPUpgradeLevel.Upgrade2, 3);
            DrawBPLevelUpgrade("BP 5 (MAX) 업그레이드", ref selectedUpgradePath.upgradeMax,
                BPUpgradeLevel.MAX, 5);

            EditorGUILayout.Space();

            // 저장 버튼
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("💾 업그레이드 경로 저장", GUILayout.Height(30)))
            {
                SaveSelectedBPUpgradePath();
            }
            GUI.backgroundColor = Color.white;

            // 새로 선택 버튼
            if (GUILayout.Button("다른 스킬 선택", GUILayout.Height(25)))
            {
                selectedBPBaseSkill = null;
                selectedUpgradePath = null;
            }
        }


        // SaveBPUpgradePath 수정 - selectedBPBaseSkill과 selectedUpgradePath 사용
        private void SaveSelectedBPUpgradePath()
        {
            if (bpDatabase == null)
            {
                EditorUtility.DisplayDialog("오류", "BP Database가 선택되지 않았습니다.", "확인");
                return;
            }

            if (selectedUpgradePath == null || selectedBPBaseSkill == null)
            {
                EditorUtility.DisplayDialog("오류", "저장할 업그레이드 경로가 없습니다.", "확인");
                return;
            }

            // 기존 SaveBPUpgradePath 로직 사용하되, currentSkill과 currentUpgradePath 대신
            // selectedBPBaseSkill과 selectedUpgradePath 사용

            var field = typeof(BPUpgradeDatabase).GetField("upgradePaths",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                var list = field.GetValue(bpDatabase) as List<BPUpgradeDatabase.SkillUpgradePathData>;
                if (list != null)
                {
                    // 기존 경로 제거
                    list.RemoveAll(p => p.baseSkillId == selectedUpgradePath.baseSkillId);

                    // 새 경로 추가
                    var pathData = new BPUpgradeDatabase.SkillUpgradePathData
                    {
                        baseSkillId = selectedUpgradePath.baseSkillId,
                        pathName = selectedUpgradePath.pathName
                    };

                    if (selectedUpgradePath.upgrade1 != null)
                    {
                        pathData.upgrade1 = ConvertToBPUpgradeModifierData(selectedUpgradePath.upgrade1);
                    }
                    if (selectedUpgradePath.upgrade2 != null)
                    {
                        pathData.upgrade2 = ConvertToBPUpgradeModifierData(selectedUpgradePath.upgrade2);
                    }
                    if (selectedUpgradePath.upgradeMax != null)
                    {
                        pathData.upgradeMax = ConvertToBPUpgradeModifierData(selectedUpgradePath.upgradeMax);
                    }

                    list.Add(pathData);
                }
            }

            EditorUtility.SetDirty(bpDatabase);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("저장 완료",
                $"{selectedBPBaseSkill.skillName}의 BP 업그레이드 경로가 저장되었습니다.", "확인");
        }

        private void DrawBPLevelUpgrade(string label, ref BPUpgradeModifier modifier,
            BPUpgradeLevel level, int requiredBP)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool isExpanded = EditorGUILayout.Foldout(modifier != null, label);

            if (isExpanded)
            {
                if (modifier == null)
                {
                    modifier = new BPUpgradeModifier
                    {
                        level = level,
                        requiredBP = requiredBP
                    };
                }

                EditorGUI.indentLevel++;

                // 기본 정보
                modifier.upgradeName = EditorGUILayout.TextField("이름", modifier.upgradeName);
                modifier.description = EditorGUILayout.TextArea(modifier.description,
                    GUILayout.Height(30));
                modifier.type = (UpgradeType)EditorGUILayout.EnumPopup("타입", modifier.type);

                // 타입별 세부 설정
                switch (modifier.type)
                {
                    case UpgradeType.Numerical:
                        DrawNumericalUpgrade(modifier);
                        break;
                    case UpgradeType.Mechanical:
                        DrawMechanicalUpgrade(modifier);
                        break;
                    case UpgradeType.Additional:
                        DrawAdditionalUpgrade(modifier);
                        break;
                    case UpgradeType.Transform:
                        DrawTransformUpgrade(modifier);
                        break;
                }

                // 시각 효과
                EditorGUILayout.LabelField("시각 효과", EditorStyles.miniLabel);
                modifier.upgradedEffectPrefab = EditorGUILayout.ObjectField("이펙트",
                    modifier.upgradedEffectPrefab, typeof(GameObject), false) as GameObject;
                modifier.effectColorTint = EditorGUILayout.ColorField("색상",
                    modifier.effectColorTint);
                modifier.effectScaleMultiplier = EditorGUILayout.Slider("크기",
                    modifier.effectScaleMultiplier, 0.5f, 3f);

                EditorGUI.indentLevel--;

                // 삭제 버튼
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("삭제", GUILayout.Width(50)))
                {
                    modifier = null;
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }


         // === BP 스킬 그룹 목록 표시 ===
        private void DrawBPSkillGroups()
        {
            EditorGUILayout.LabelField($"발견된 BP 그룹: {bpSkillGroups.Count}개", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

            foreach (var group in bpSkillGroups)
            {
                var baseSkill = group.Value.FirstOrDefault(s => s.bpLevel == 0 || s.parentSkillId == 0);
                if (baseSkill == null) baseSkill = group.Value[0];

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // 체크박스
                bool isSelected = selectedForMigration.Contains(group.Key);
                bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                if (newSelected != isSelected)
                {
                    if (newSelected)
                        selectedForMigration.Add(group.Key);
                    else
                        selectedForMigration.Remove(group.Key);
                }

                // 스킬 정보
                EditorGUILayout.LabelField($"[{baseSkill.skillId}] {baseSkill.skillName}", GUILayout.Width(200));
                EditorGUILayout.LabelField($"({group.Value.Count}개 버전)", GUILayout.Width(80));

                // 상세 버튼 - 수정된 부분
                GUI.backgroundColor = (selectedBPBaseSkill == baseSkill) ? Color.yellow : Color.white;
                if (GUILayout.Button("편집", GUILayout.Width(50)))
                {
                    // BP 탭 전용 변수에 저장
                    selectedBPBaseSkill = baseSkill;
                    selectedUpgradePath = AnalyzeGroupForPath(group.Value);

                    Debug.Log($"[BP Editor] Selected skill: {selectedBPBaseSkill.skillName}, Path: {selectedUpgradePath?.pathName}");
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
        
        // === 선택된 그룹 상세 표시 ===
        private void DrawSelectedGroupDetails()
        {
            EditorGUILayout.LabelField("선택된 그룹 상세", EditorStyles.boldLabel);
            
            if (selectedForMigration.Count == 0)
            {
                EditorGUILayout.HelpBox("마이그레이션할 그룹을 선택하세요.", MessageType.Info);
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            foreach (int baseId in selectedForMigration)
            {
                if (!bpSkillGroups.ContainsKey(baseId)) continue;
                
                var group = bpSkillGroups[baseId];
                var baseSkill = group.FirstOrDefault(s => s.bpLevel == 0 || s.parentSkillId == 0) ?? group[0];
                
                EditorGUILayout.LabelField($"• {baseSkill.skillName}", EditorStyles.boldLabel);
                
                foreach (var skill in group.OrderBy(s => s.bpLevel > 0 ? s.bpLevel : (s.skillId % 1000 == 0 ? 0 : s.skillId)))
                {
                    string levelInfo = GetBPLevelInfo(skill, baseSkill);
                    EditorGUILayout.LabelField($"  - [{levelInfo}] {skill.skillName} (ID: {skill.skillId})", 
                        EditorStyles.miniLabel);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // === 마이그레이션 액션 버튼 ===
        private void DrawMigrationActions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            
            // 전체 선택/해제
            if (GUILayout.Button("전체 선택", GUILayout.Width(100)))
            {
                selectedForMigration.Clear();
                selectedForMigration.AddRange(bpSkillGroups.Keys);
            }
            
            if (GUILayout.Button("전체 해제", GUILayout.Width(100)))
            {
                selectedForMigration.Clear();
            }
            
            GUILayout.FlexibleSpace();
            
            // 마이그레이션 실행
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button($"선택한 {selectedForMigration.Count}개 그룹 마이그레이션", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("마이그레이션 확인", 
                    $"{selectedForMigration.Count}개 스킬 그룹을 BP 업그레이드 시스템으로 변환하시겠습니까?\n\n" +
                    "• BP 업그레이드 경로가 생성됩니다\n" +
                    "• 기존 개별 BP 스킬은 삭제할 수 있습니다", 
                    "실행", "취소"))
                {
                    MigrateSelectedGroups();
                }
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }
        
        // === 그룹을 업그레이드 경로로 분석 ===
        private SkillUpgradePath AnalyzeGroupForPath(List<AdvancedSkillData> group)
        {
            var baseSkill = group.FirstOrDefault(s => s.bpLevel == 0 || s.parentSkillId == 0) ?? group[0];
            
            var path = new SkillUpgradePath
            {
                baseSkillId = baseSkill.skillId,
                pathName = $"{baseSkill.skillName} 업그레이드",
                pathDescription = $"마이그레이션된 {baseSkill.skillName} BP 경로"
            };
            
            // 각 BP 레벨 찾기
            foreach (var skill in group)
            {
                if (skill == baseSkill) continue;
                
                var modifier = CreateModifierFromSkillDifference(baseSkill, skill);
                
                // BP 레벨 판단
                if (skill.bpLevel > 0)
                {
                    // bpLevel 필드 사용
                    if (skill.bpLevel <= 2)
                        path.upgrade1 = modifier;
                    else if (skill.bpLevel <= 4)
                        path.upgrade2 = modifier;
                    else
                        path.upgradeMax = modifier;
                }
                else
                {
                    // ID 패턴 또는 이름 패턴으로 판단
                    if (skill.skillId == baseSkill.skillId + 1000 || skill.skillName.EndsWith("+"))
                        path.upgrade1 = modifier;
                    else if (skill.skillId == baseSkill.skillId + 2000 || skill.skillName.EndsWith("++"))
                        path.upgrade2 = modifier;
                    else if (skill.skillId == baseSkill.skillId + 3000 || skill.skillName.Contains("MAX"))
                        path.upgradeMax = modifier;
                }
            }
            
            return path;
        }
        
        // === 스킬 차이로부터 Modifier 생성 ===
        private BPUpgradeModifier CreateModifierFromSkillDifference(AdvancedSkillData baseSkill, AdvancedSkillData upgradedSkill)
        {
            var modifier = new BPUpgradeModifier
            {
                upgradeName = upgradedSkill.bpUpgradeName ?? upgradedSkill.skillName.Replace(baseSkill.skillName, "").Trim(),
                description = upgradedSkill.description,
                type = DetermineUpgradeType(baseSkill, upgradedSkill)
            };
            
            // 수치 변경 분석
            if (baseSkill.effects.Count == upgradedSkill.effects.Count)
            {
                modifier.valueModifiers = new List<ValueModifier>();
                
                for (int i = 0; i < baseSkill.effects.Count; i++)
                {
                    var baseEffect = baseSkill.effects[i];
                    var upgEffect = upgradedSkill.effects[i];
                    
                    if (Math.Abs(baseEffect.value - upgEffect.value) > 0.01f)
                    {
                        var valueMod = new ValueModifier
                        {
                            targetField = "value",
                            operation = ValueModifier.ModifierOperation.Override,
                            value = upgEffect.value,
                            isPercentage = false
                        };
                        
                        // 비율 계산
                        if (baseEffect.value > 0)
                        {
                            float ratio = upgEffect.value / baseEffect.value;
                            if (Math.Abs(ratio - 1.25f) < 0.01f || 
                                Math.Abs(ratio - 1.5f) < 0.01f || 
                                Math.Abs(ratio - 2f) < 0.01f)
                            {
                                valueMod.operation = ValueModifier.ModifierOperation.Multiply;
                                valueMod.value = ratio;
                            }
                        }
                        
                        modifier.valueModifiers.Add(valueMod);
                    }
                }
            }
            
            // 추가 효과 분석
            if (upgradedSkill.effects.Count > baseSkill.effects.Count)
            {
                modifier.additionalEffects = new List<AdvancedSkillEffect>();
                for (int i = baseSkill.effects.Count; i < upgradedSkill.effects.Count; i++)
                {
                    modifier.additionalEffects.Add(upgradedSkill.effects[i].Clone());
                }
            }
            
            return modifier;
        }
        
        // === 업그레이드 타입 판단 ===
        private UpgradeType DetermineUpgradeType(AdvancedSkillData baseSkill, AdvancedSkillData upgradedSkill)
        {
            // 완전히 다른 스킬인 경우
            if (baseSkill.effects.Count != upgradedSkill.effects.Count ||
                baseSkill.effects[0].type != upgradedSkill.effects[0].type)
            {
                return UpgradeType.Transform;
            }
            
            // 타겟 타입이 변경된 경우
            if (baseSkill.effects.Any(e => upgradedSkill.effects.Any(u => e.targetType != u.targetType)))
            {
                return UpgradeType.Mechanical;
            }
            
            // 추가 효과가 있는 경우
            if (upgradedSkill.effects.Count > baseSkill.effects.Count)
            {
                return UpgradeType.Additional;
            }
            
            // 수치만 변경된 경우
            return UpgradeType.Numerical;
        }
        
        // === BP 레벨 정보 가져오기 ===
        private string GetBPLevelInfo(AdvancedSkillData skill, AdvancedSkillData baseSkill)
        {
            if (skill.bpLevel > 0)
                return $"BP {skill.bpLevel}";
            
            if (skill.skillId == baseSkill.skillId)
                return "Base";
            
            int diff = skill.skillId - baseSkill.skillId;
            if (diff == 1000) return "BP 1-2";
            if (diff == 2000) return "BP 3-4";
            if (diff == 3000) return "BP 5";
            
            if (skill.skillName.EndsWith(" MAX")) return "BP 5";
            if (skill.skillName.EndsWith("++")) return "BP 3-4";
            if (skill.skillName.EndsWith("+")) return "BP 1-2";
            
            return "?";
        }
        
        // === 선택된 그룹 마이그레이션 실행 ===
        private void MigrateSelectedGroups()
        {
            int successCount = 0;
            List<AdvancedSkillData> skillsToDelete = new List<AdvancedSkillData>();
            
            foreach (int baseId in selectedForMigration)
            {
                if (!bpSkillGroups.ContainsKey(baseId)) continue;
                
                var group = bpSkillGroups[baseId];
                var path = AnalyzeGroupForPath(group);
                
                // 경로 저장
                currentUpgradePath = path;
                currentSkill = group[0];
                SaveBPUpgradePath();
                
                // BP 스킬들을 삭제 대상에 추가 (기본 스킬은 유지)
                foreach (var skill in group)
                {
                    if (skill.bpLevel > 0 || skill.skillId != baseId)
                    {
                        skillsToDelete.Add(skill);
                    }
                }
                
                successCount++;
            }
            
            // 삭제 확인
            if (skillsToDelete.Count > 0)
            {
                if (EditorUtility.DisplayDialog("BP 스킬 정리", 
                    $"마이그레이션이 완료되었습니다.\n\n" +
                    $"• {successCount}개 경로 생성됨\n" +
                    $"• {skillsToDelete.Count}개 BP 스킬 삭제 가능\n\n" +
                    "기존 BP 스킬들을 삭제하시겠습니까?", 
                    "삭제", "유지"))
                {
                    foreach (var skill in skillsToDelete)
                    {
                        database.RemoveSkill(skill);
                    }
                    SaveDatabase();
                    
                    EditorUtility.DisplayDialog("완료", 
                        $"{skillsToDelete.Count}개 BP 스킬이 삭제되었습니다.\n" +
                        "이제 BP 업그레이드 시스템을 사용할 수 있습니다.", "확인");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("완료", 
                    $"{successCount}개 BP 업그레이드 경로가 생성되었습니다.", "확인");
            }
            
            // 리프레시
            selectedForMigration.Clear();
            AnalyzeBPSkills();
        }
    

        private void DrawNumericalUpgrade(BPUpgradeModifier modifier)
        {
            EditorGUILayout.LabelField("수치 변경", EditorStyles.miniLabel);

            // Value Modifiers 리스트
            for (int i = 0; i < modifier.valueModifiers.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var vm = modifier.valueModifiers[i];
                vm.targetField = EditorGUILayout.TextField(vm.targetField, GUILayout.Width(80));
                vm.operation = (ValueModifier.ModifierOperation)EditorGUILayout.EnumPopup(
                    vm.operation, GUILayout.Width(70));
                vm.value = EditorGUILayout.FloatField(vm.value, GUILayout.Width(50));
                vm.isPercentage = EditorGUILayout.Toggle(vm.isPercentage, GUILayout.Width(20));
                EditorGUILayout.LabelField("%", GUILayout.Width(20));

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    modifier.valueModifiers.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ 수치 추가"))
            {
                modifier.valueModifiers.Add(new ValueModifier());
            }
        }

        private void DrawBPUpgradePreview()
        {
            EditorGUILayout.LabelField("📊 업그레이드 미리보기", EditorStyles.boldLabel);

            // selectedBPBaseSkill과 selectedUpgradePath 사용
            if (selectedBPBaseSkill == null || selectedUpgradePath == null)
            {
                EditorGUILayout.HelpBox("스킬과 업그레이드 경로를 선택하세요", MessageType.Info);
                return;
            }

            // BP 레벨 슬라이더
            int previewBP = EditorGUILayout.IntSlider("BP 레벨", 0, 0, 5);

            // 업그레이드 적용 시뮬레이션
            var previewData = SimulateBPUpgrade(selectedBPBaseSkill, selectedUpgradePath, previewBP);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 스킬 이름
            EditorGUILayout.LabelField(previewData.skillName, EditorStyles.boldLabel);

            // 변경사항 표시
            if (previewBP > 0)
            {
                GUI.color = Color.yellow;
                var modifier = selectedUpgradePath.GetUpgradeForBP(previewBP);
                if (modifier != null)
                {
                    EditorGUILayout.LabelField($"[{modifier.upgradeName}]");
                    EditorGUILayout.LabelField(modifier.description, EditorStyles.wordWrappedLabel);
                }
                GUI.color = Color.white;
            }

            // 효과 목록
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("효과:", EditorStyles.boldLabel);
            foreach (var effect in previewData.effects)
            {
                EditorGUILayout.LabelField($"• {effect.GetFullDescription()}");
            }

            // 수치 비교
            if (previewBP > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("변경 사항:", EditorStyles.boldLabel);
                CompareSkillData(selectedBPBaseSkill, previewData);
            }

            EditorGUILayout.EndVertical();
        }


        // 스킬 선택 팝업 (추가)
        private void ShowSkillSelectionPopup()
        {
            GenericMenu menu = new GenericMenu();

            var baseSkills = database.GetAllSkills()
                .Where(s => s.bpLevel == 0 || s.parentSkillId == 0)
                .OrderBy(s => s.skillId);

            foreach (var skill in baseSkills)
            {
                var skillCopy = skill; // 클로저 문제 방지
                menu.AddItem(new GUIContent($"[{skill.skillId}] {skill.skillName}"),
                    selectedBPBaseSkill == skill,
                    () => {
                        selectedBPBaseSkill = skillCopy;
                        selectedUpgradePath = null; // 새 스킬 선택시 경로 리셋
                    });
            }

            menu.ShowAsContext();
        }

        // === 1. CreateNewBPDatabase ===
        private void CreateNewBPDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create BP Upgrade Database",
                "BPUpgradeDatabase",
                "asset",
                "Please enter a file name for the BP Upgrade Database");

            if (!string.IsNullOrEmpty(path))
            {
                bpDatabase = ScriptableObject.CreateInstance<BPUpgradeDatabase>();
                AssetDatabase.CreateAsset(bpDatabase, path);
                AssetDatabase.SaveAssets();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = bpDatabase;
            }
        }

        // === 2. HasBPUpgradePath ===
        private bool HasBPUpgradePath(int skillId)
        {
            if (bpDatabase == null) return false;
            var path = bpDatabase.GetUpgradePath(skillId);
            return path != null;
        }

        // === 3. SelectSkillForBPUpgrade ===
        private void SelectSkillForBPUpgrade(AdvancedSkillData skill)
        {
            currentSkill = skill;

            // 기존 경로 로드 또는 새로 생성
            if (bpDatabase != null)
            {
                currentUpgradePath = bpDatabase.GetUpgradePath(skill.skillId);
            }

            if (currentUpgradePath == null)
            {
                currentUpgradePath = new SkillUpgradePath
                {
                    baseSkillId = skill.skillId,
                    pathName = $"{skill.skillName} 업그레이드",
                    pathDescription = $"{skill.skillName}의 BP 업그레이드 경로"
                };
            }
        }

        // === 4. SaveBPUpgradePath ===
        private void SaveBPUpgradePath()
        {
            if (bpDatabase == null)
            {
                EditorUtility.DisplayDialog("오류", "BP Database가 선택되지 않았습니다.", "확인");
                return;
            }

            if (currentUpgradePath == null || currentSkill == null)
            {
                EditorUtility.DisplayDialog("오류", "저장할 업그레이드 경로가 없습니다.", "확인");
                return;
            }

            // BPUpgradeDatabase의 private 필드에 접근 (리플렉션)
            var field = typeof(BPUpgradeDatabase).GetField("upgradePaths",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                var list = field.GetValue(bpDatabase) as List<BPUpgradeDatabase.SkillUpgradePathData>;
                if (list != null)
                {
                    // 기존 경로 제거
                    list.RemoveAll(p => p.baseSkillId == currentUpgradePath.baseSkillId);

                    // 새 경로 추가 (SkillUpgradePath를 SkillUpgradePathData로 변환)
                    var pathData = new BPUpgradeDatabase.SkillUpgradePathData
                    {
                        baseSkillId = currentUpgradePath.baseSkillId,
                        pathName = currentUpgradePath.pathName
                    };

                    // 각 레벨 변환
                    if (currentUpgradePath.upgrade1 != null)
                    {
                        pathData.upgrade1 = ConvertToBPUpgradeModifierData(currentUpgradePath.upgrade1);
                    }
                    if (currentUpgradePath.upgrade2 != null)
                    {
                        pathData.upgrade2 = ConvertToBPUpgradeModifierData(currentUpgradePath.upgrade2);
                    }
                    if (currentUpgradePath.upgradeMax != null)
                    {
                        pathData.upgradeMax = ConvertToBPUpgradeModifierData(currentUpgradePath.upgradeMax);
                    }

                    list.Add(pathData);
                }
            }

            EditorUtility.SetDirty(bpDatabase);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("저장 완료",
                $"{currentSkill.skillName}의 BP 업그레이드 경로가 저장되었습니다.", "확인");
        }

        // 헬퍼 메서드: BPUpgradeModifier를 BPUpgradeModifierData로 변환
        private BPUpgradeDatabase.BPUpgradeModifierData ConvertToBPUpgradeModifierData(BPUpgradeModifier modifier)
        {
            return new BPUpgradeDatabase.BPUpgradeModifierData
            {
                upgradeName = modifier.upgradeName,
                description = modifier.description,
                type = modifier.type,
                valueModifiers = modifier.valueModifiers,
                changeTargetType = modifier.targetChange?.enabled ?? false,
                newTargetType = modifier.targetChange?.toType ?? SkillSystem.TargetType.Self,
                additionalDuration = modifier.durationChange?.additionalTurns ?? 0,
                additionalEffects = modifier.additionalEffects,
                upgradedEffectPrefab = modifier.upgradedEffectPrefab,
                effectColorTint = modifier.effectColorTint
            };
        }

        // === 5. DrawMechanicalUpgrade ===
        private void DrawMechanicalUpgrade(BPUpgradeModifier modifier)
        {
            EditorGUILayout.LabelField("메커니즘 변경", EditorStyles.miniLabel);

            // 타겟 타입 변경
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("타겟 변경", GUILayout.Width(80));
            if (modifier.targetChange == null)
                modifier.targetChange = new TargetTypeChange();

            modifier.targetChange.enabled = EditorGUILayout.Toggle(modifier.targetChange.enabled, GUILayout.Width(20));
            if (modifier.targetChange.enabled)
            {
                modifier.targetChange.toType = (SkillSystem.TargetType)EditorGUILayout.EnumPopup(
                    modifier.targetChange.toType);
                modifier.targetChange.additionalTargets = EditorGUILayout.IntField("추가 타겟",
                    modifier.targetChange.additionalTargets);
            }
            EditorGUILayout.EndHorizontal();

            // 지속시간 변경
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("지속시간", GUILayout.Width(80));
            if (modifier.durationChange == null)
                modifier.durationChange = new DurationChange();

            modifier.durationChange.enabled = EditorGUILayout.Toggle(modifier.durationChange.enabled, GUILayout.Width(20));
            if (modifier.durationChange.enabled)
            {
                modifier.durationChange.additionalTurns = EditorGUILayout.IntField("추가 턴",
                    modifier.durationChange.additionalTurns);
                modifier.durationChange.makeItPermanent = EditorGUILayout.Toggle("영구 지속",
                    modifier.durationChange.makeItPermanent);
            }
            EditorGUILayout.EndHorizontal();

            // DoT 변환
            modifier.convertToDamageOverTime = EditorGUILayout.Toggle("DoT로 변환",
                modifier.convertToDamageOverTime);
            modifier.addAreaOfEffect = EditorGUILayout.Toggle("범위 공격 추가",
                modifier.addAreaOfEffect);
        }

        // === 6. DrawAdditionalUpgrade ===
        private void DrawAdditionalUpgrade(BPUpgradeModifier modifier)
        {
            EditorGUILayout.LabelField("추가 효과", EditorStyles.miniLabel);

            if (modifier.additionalEffects == null)
                modifier.additionalEffects = new List<AdvancedSkillEffect>();

            for (int i = 0; i < modifier.additionalEffects.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var effect = modifier.additionalEffects[i];
                effect.name = EditorGUILayout.TextField("이름", effect.name);
                effect.type = (SkillSystem.EffectType)EditorGUILayout.EnumPopup("타입", effect.type);
                effect.value = EditorGUILayout.FloatField("값", effect.value);
                effect.duration = EditorGUILayout.IntField("지속시간", effect.duration);

                if (GUILayout.Button("제거", GUILayout.Width(50)))
                {
                    modifier.additionalEffects.RemoveAt(i);
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ 효과 추가"))
            {
                modifier.additionalEffects.Add(new AdvancedSkillEffect());
            }
        }

        // === 7. DrawTransformUpgrade ===
        private void DrawTransformUpgrade(BPUpgradeModifier modifier)
        {
            EditorGUILayout.LabelField("완전 변형", EditorStyles.miniLabel);

            modifier.isCompleteOverride = EditorGUILayout.Toggle("완전 교체", modifier.isCompleteOverride);

            if (modifier.isCompleteOverride)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (modifier.transformedSkill == null)
                {
                    if (GUILayout.Button("변형 스킬 생성"))
                    {
                        modifier.transformedSkill = currentSkill.Clone();
                        modifier.transformedSkill.skillName = $"{currentSkill.skillName} (변형)";
                    }
                }
                else
                {
                    EditorGUILayout.LabelField($"변형 스킬: {modifier.transformedSkill.skillName}");

                    // 간단한 편집
                    modifier.transformedSkill.skillName = EditorGUILayout.TextField("이름",
                        modifier.transformedSkill.skillName);
                    modifier.transformedSkill.description = EditorGUILayout.TextArea(
                        modifier.transformedSkill.description, GUILayout.Height(40));

                    if (GUILayout.Button("상세 편집"))
                    {
                        // 현재 스킬을 변형 스킬로 전환하여 편집
                        var temp = currentSkill;
                        currentSkill = modifier.transformedSkill;
                        currentTab = EditorTab.SkillCreator;
                        // 나중에 돌아올 수 있도록 임시 저장
                        copiedSkill = temp;
                    }

                    if (GUILayout.Button("제거"))
                    {
                        modifier.transformedSkill = null;
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        // === 8. SimulateBPUpgrade ===
        private AdvancedSkillData SimulateBPUpgrade(AdvancedSkillData baseSkill,
            SkillUpgradePath path, int bpLevel)
        {
            if (baseSkill == null || path == null)
                return baseSkill;

            // UpgradedSkillData 생성 및 시뮬레이션
            var upgradeData = new UpgradedSkillData
            {
                baseSkill = baseSkill,
                currentSkill = baseSkill.Clone(),
                currentLevel = path.GetUpgradeLevel(bpLevel),
                totalBPUsed = bpLevel
            };

            // BP 레벨에 맞는 업그레이드 적용
            var modifier = path.GetUpgradeForBP(bpLevel);
            if (modifier != null)
            {
                upgradeData.ApplyUpgrade(modifier);
            }

            return upgradeData.currentSkill ?? baseSkill;
        }

        // === 9. CompareSkillData ===
        private void CompareSkillData(AdvancedSkillData original, AdvancedSkillData upgraded)
        {
            // 데미지/효과 값 비교
            for (int i = 0; i < original.effects.Count && i < upgraded.effects.Count; i++)
            {
                var origEffect = original.effects[i];
                var upgEffect = upgraded.effects[i];

                if (Math.Abs(origEffect.value - upgEffect.value) > 0.01f)
                {
                    float change = upgEffect.value - origEffect.value;
                    float percent = (change / origEffect.value) * 100;

                    GUI.color = change > 0 ? Color.green : Color.red;
                    EditorGUILayout.LabelField(
                        $"• {origEffect.name ?? "효과"}: {origEffect.value:F1} → {upgEffect.value:F1} ({change:+0.#}, {percent:+0}%)");
                    GUI.color = Color.white;
                }

                // 지속시간 변경
                if (origEffect.duration != upgEffect.duration)
                {
                    GUI.color = Color.cyan;
                    EditorGUILayout.LabelField(
                        $"• 지속시간: {origEffect.duration}턴 → {upgEffect.duration}턴");
                    GUI.color = Color.white;
                }
            }

            // 추가된 효과
            if (upgraded.effects.Count > original.effects.Count)
            {
                GUI.color = Color.yellow;
                for (int i = original.effects.Count; i < upgraded.effects.Count; i++)
                {
                    EditorGUILayout.LabelField($"• [추가] {upgraded.effects[i].GetShortDescription()}");
                }
                GUI.color = Color.white;
            }

            // 쿨다운 변경
            if (original.cooldown != upgraded.cooldown)
            {
                GUI.color = Color.magenta;
                EditorGUILayout.LabelField($"• 쿨다운: {original.cooldown}턴 → {upgraded.cooldown}턴");
                GUI.color = Color.white;
            }
        }







        // =====================================================
        // Export/Import 탭
        // =====================================================

        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("💾 내보내기/가져오기", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 내보내기 섹션
            EditorGUILayout.LabelField("CSV 내보내기", EditorStyles.boldLabel);

            var exportClass = (CharacterClass)EditorGUILayout.EnumPopup("클래스", CharacterClass.All);
            var exportCategory = (SkillCategory)EditorGUILayout.EnumPopup("카테고리", SkillCategory.All);

            List<AdvancedSkillData> skillsToExport;
            if (exportClass == CharacterClass.All && exportCategory == SkillCategory.All)
            {
                skillsToExport = database.GetAllSkills();
            }
            else if (exportClass != CharacterClass.All)
            {
                skillsToExport = database.GetSkillsByClass(exportClass);
                if (exportCategory != SkillCategory.All)
                {
                    skillsToExport = skillsToExport.Where(s => s.category == exportCategory).ToList();
                }
            }
            else
            {
                skillsToExport = database.GetSkillsByCategory(exportCategory);
            }

            EditorGUILayout.LabelField($"내보낼 스킬 수: {skillsToExport.Count}");

            if (GUILayout.Button("CSV로 내보내기", GUILayout.Height(30)))
            {
                ExportToCSV(skillsToExport);
            }

            EditorGUILayout.EndVertical();

            // 가져오기 섹션
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("CSV 가져오기", EditorStyles.boldLabel);

            if (GUILayout.Button("CSV 파일 선택", GUILayout.Height(30)))
            {
                ImportFromCSV();
            }

            EditorGUILayout.EndVertical();

            // JSON 내보내기/가져오기
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("JSON 백업", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON 내보내기"))
            {
                ExportToJSON();
            }
            if (GUILayout.Button("JSON 가져오기"))
            {
                ImportFromJSON();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ExportToCSV(List<AdvancedSkillData> skills)
        {
            var path = EditorUtility.SaveFilePanel("CSV 저장", "", "skills.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var csv = database.ExportToCSV(skills);
            File.WriteAllText(path, csv);

            EditorUtility.DisplayDialog("성공", $"{skills.Count}개 스킬을 CSV로 내보냈습니다.", "확인");
        }

        private void ImportFromCSV()
        {
            var path = EditorUtility.OpenFilePanel("CSV 선택", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var csvContent = File.ReadAllText(path);
            database.ImportFromCSV(csvContent);

            SaveDatabase();
            EditorUtility.DisplayDialog("성공", "CSV 가져오기가 완료되었습니다.", "확인");
        }

        private void ExportToJSON()
        {
            var path = EditorUtility.SaveFilePanel("JSON 저장", "", "skill_database.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            var json = JsonUtility.ToJson(database, true);
            File.WriteAllText(path, json);

            EditorUtility.DisplayDialog("성공", "JSON 백업이 완료되었습니다.", "확인");
        }

        private void ImportFromJSON()
        {
            var path = EditorUtility.OpenFilePanel("JSON 선택", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            var json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, database);

            SaveDatabase();
            EditorUtility.DisplayDialog("성공", "JSON 복원이 완료되었습니다.", "확인");
        }

        // =====================================================
        // 설정 탭
        // =====================================================

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("⚙️ 설정", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("자동 저장", EditorStyles.boldLabel);
            autoSave = EditorGUILayout.Toggle("자동 저장 활성화", autoSave);
            EditorGUILayout.HelpBox("1분마다 자동으로 데이터베이스를 저장합니다.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("데이터베이스 관리", EditorStyles.boldLabel);

            if (GUILayout.Button("데이터베이스 백업"))
            {
                BackupDatabase();
            }

            if (GUILayout.Button("데이터베이스 복원"))
            {
                RestoreDatabase();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("데이터베이스 초기화"))
            {
                if (EditorUtility.DisplayDialog("경고",
                    "모든 스킬 데이터가 삭제됩니다. 계속하시겠습니까?", "초기화", "취소"))
                {
                    database = new AdvancedSkillDatabase();
                    SaveDatabase();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("통계", EditorStyles.boldLabel);

            var stats = database.GetStatistics();
            foreach (var stat in stats)
            {
                EditorGUILayout.LabelField($"{stat.Key}: {stat.Value}");
            }

            EditorGUILayout.EndVertical();
        }

        private void BackupDatabase()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = EditorUtility.SaveFilePanel("백업 저장", "", $"skill_backup_{timestamp}.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            var json = JsonUtility.ToJson(database, true);
            File.WriteAllText(path, json);

            EditorUtility.DisplayDialog("성공", "백업이 완료되었습니다.", "확인");
        }

        private void RestoreDatabase()
        {
            var path = EditorUtility.OpenFilePanel("백업 파일 선택", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            if (EditorUtility.DisplayDialog("확인",
                "현재 데이터를 백업 파일로 덮어씁니다. 계속하시겠습니까?", "복원", "취소"))
            {
                var json = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(json, database);
                SaveDatabase();

                EditorUtility.DisplayDialog("성공", "복원이 완료되었습니다.", "확인");
            }
        }

        // =====================================================
        // 스킬 템플릿 데이터
        // =====================================================

        private Dictionary<CharacterClass, List<SkillTemplate>> GetSkillTemplates()
        {
            return new Dictionary<CharacterClass, List<SkillTemplate>>
            {
                [CharacterClass.Vanguard] = new List<SkillTemplate>
                {
                    new SkillTemplate
                    {
                        name = "철벽 방어",
                        description = "강력한 데미지와 기절, 그리고 반사를 조합한 방어형 스킬",
                        tier = 5,
                        effects = new List<AdvancedSkillEffect>
                        {
                            new AdvancedSkillEffect { type = EffectType.Damage, value = 187, targetType = TargetType.EnemySingle },
                            new AdvancedSkillEffect { type = EffectType.StatusEffect, statusType = StatusType.Stun, duration = 4, chance = 0.6f },
                            new AdvancedSkillEffect { type = EffectType.Reflect, value = 30, duration = 8, targetType = TargetType.Self }
                        }
                    },
                    new SkillTemplate
                    {
                        name = "도발의 함성",
                        description = "적을 도발하고 방어력을 높이는 탱커 스킬",
                        tier = 4,
                        effects = new List<AdvancedSkillEffect>
                        {
                            new AdvancedSkillEffect { type = EffectType.StatusEffect, statusType = StatusType.Taunt, duration = 4, chance = 0.6f, targetType = TargetType.EnemyAll },
                            new AdvancedSkillEffect { type = EffectType.Buff, statType = StatType.Defense, value = 33, duration = 8, targetType = TargetType.Self }
                        }
                    }
                },

                [CharacterClass.Slaughter] = new List<SkillTemplate>
                {
                    new SkillTemplate
                    {
                        name = "피의 축제",
                        description = "강력한 데미지와 출혈을 동시에 입히는 공격 스킬",
                        tier = 5,
                        effects = new List<AdvancedSkillEffect>
                        {
                            new AdvancedSkillEffect { type = EffectType.Damage, value = 250, canCrit = true },
                            new AdvancedSkillEffect { type = EffectType.StatusEffect, statusType = StatusType.Bleed, value = 82, duration = 3, chance = 0.6f }
                        }
                    }
                },

                [CharacterClass.Jacker] = new List<SkillTemplate>
                {
                    new SkillTemplate
                    {
                        name = "암살",
                        description = "치명타 확률을 높이고 강력한 일격을 가하는 스킬",
                        tier = 4,
                        effects = new List<AdvancedSkillEffect>
                        {
                            new AdvancedSkillEffect { type = EffectType.Buff, statType = StatType.CritRate, value = 50, duration = 8, targetType = TargetType.Self },
                            new AdvancedSkillEffect { type = EffectType.Damage, value = 187, canCrit = true }
                        }
                    }
                },

                [CharacterClass.Rewinder] = new List<SkillTemplate>
                {
                    new SkillTemplate
                    {
                        name = "대규모 치유",
                        description = "아군 전체를 회복시키고 방어력을 높이는 지원 스킬",
                        tier = 4,
                        effects = new List<AdvancedSkillEffect>
                        {
                            new AdvancedSkillEffect { type = EffectType.Heal, value = 36, targetType = TargetType.AllyAll },
                            new AdvancedSkillEffect { type = EffectType.Buff, statType = StatType.Defense, value = 33, duration = 8, targetType = TargetType.AllyAll }
                        }
                    }
                }
            };
        }

        private class SkillTemplate
        {
            public string name;
            public string description;
            public int tier;
            public List<AdvancedSkillEffect> effects;

            public AdvancedSkillData CreateSkill()
            {
                return new AdvancedSkillData
                {
                    skillName = name,
                    description = description,
                    tier = tier,
                    effects = effects.Select(e => e.Clone()).ToList()
                };
            }
        }
    }
}

#endif