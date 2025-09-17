// =====================================================
// AdvancedSkillEditorWindow.cs
// 메인 에디터 윈도우 - Editor 폴더에 저장
// =====================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR

namespace SkillSystem.Editor
{
    public partial class AdvancedSkillEditorWindow : EditorWindow
    {
        // 탭 관리
        private enum EditorTab
        {
            Dashboard,      // 대시보드
            SkillList,      // 스킬 목록
            SkillCreator,   // 스킬 제작
            EffectLibrary,  // 효과 라이브러리
            Templates,      // 템플릿 관리
            BatchEdit,      // 일괄 편집
            BPUpgrade,
            Export,         // 내보내기/가져오기
            Settings        // 설정
        }

        private EditorTab currentTab = EditorTab.Dashboard;

        // 스킬 데이터
        private AdvancedSkillDatabase database;
        private Vector2 scrollPosition;
        private Vector2 effectScrollPosition;

        // 필터링
        private CharacterClass filterClass = CharacterClass.All;
        private SkillCategory filterCategory = SkillCategory.All;
        private int filterTier = -1;
        private string searchText = "";


        // 검색 관련 필드 추가
        private enum SearchMode
        {
            All,        // 모든 필드 검색
            Name,       // 이름만
            ID,         // ID만
            Description,// 설명
            Effect,     // 효과 내용
            Tags        // 태그
        }

        private SearchMode searchMode = SearchMode.All;
        private bool useAdvancedSearch = false;
        private int searchMinId = 0;
        private int searchMaxId = 99999;
        private bool searchIncludeBPSkills = true;



        // 현재 편집 중인 스킬
        private AdvancedSkillData currentSkill;
        private AdvancedSkillData copiedSkill;
        private AdvancedSkillData compareSkill;

        // UI 상태
        private bool showAdvancedOptions = false;
        private bool autoSave = true;
        private float lastAutoSaveTime;

        // 일괄 편집
        private List<AdvancedSkillData> selectedSkills = new List<AdvancedSkillData>();
        private BatchEditOperation batchOperation = BatchEditOperation.None;
        private int batchTierChange = 0;
        private int batchManaChange = 0;
        private AdvancedSkillEffect batchEffectToAdd;
        private EffectType batchEffectTypeToRemove;
        private float batchScaleFactor = 1.0f;

        // 효과 라이브러리
        private string selectedEffectCategory = "데미지";
        private AdvancedSkillEffect copiedEffect;

        // BP 관련 필드 추가
        private BPUpgradeDatabase bpDatabase;
        private SkillUpgradePath currentUpgradePath;
        private bool showBPPreview = false;


        [MenuItem("*COSMOS*/Battle/🎮 스킬 제작 툴")]
        public static void ShowWindow()
        {
            var window = GetWindow<AdvancedSkillEditorWindow>("🎮 스킬 제작 툴");
            window.minSize = new Vector2(1200, 700);
            window.Show();
        }

        private void OnEnable()
        {
            LoadDatabase();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (autoSave) SaveDatabase();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && autoSave)
            {
                SaveDatabase();
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();

            EditorGUILayout.Space();

            // 탭별 컨텐츠
            switch (currentTab)
            {
                case EditorTab.Dashboard:
                    DrawDashboardTab();
                    break;
                case EditorTab.SkillList:
                    DrawSkillListTab();
                    break;
                case EditorTab.SkillCreator:
                    DrawSkillCreatorTab();
                    break;
                case EditorTab.EffectLibrary:
                    DrawEffectLibraryTab();
                    break;
                case EditorTab.Templates:
                    DrawTemplatesTab();
                    break;
                case EditorTab.BatchEdit:
                    DrawBatchEditTab();
                    break;

                case EditorTab.BPUpgrade:
                    DrawBPUpgradeTab();
                    break;

                case EditorTab.Export:
                    DrawExportTab();
                    break;
                case EditorTab.Settings:
                    DrawSettingsTab();
                    break;
            }

            // 자동 저장
            if (autoSave && Time.realtimeSinceStartup - lastAutoSaveTime > 60f)
            {
                SaveDatabase();
                lastAutoSaveTime = Time.realtimeSinceStartup;
            }
        }

        // =====================================================
        // 헤더와 탭
        // =====================================================

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("🎮 스킬 제작 통합 툴 V2", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("➕ 새 스킬", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                CreateNewSkill(SkillCategory.Active);
            }

            if (GUILayout.Button("💾 저장", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SaveDatabase();
            }

            if (GUILayout.Button("📁 불러오기", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                LoadDatabase();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            var tabs = Enum.GetValues(typeof(EditorTab)).Cast<EditorTab>();
            foreach (var tab in tabs)
            {
                var style = tab == currentTab ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Button(GetTabName(tab), style))
                {
                    currentTab = tab;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private string GetTabName(EditorTab tab)
        {
            return tab switch
            {
                EditorTab.Dashboard => "📊 대시보드",
                EditorTab.SkillList => "📋 스킬 목록",
                EditorTab.SkillCreator => "🔨 스킬 제작",
                EditorTab.EffectLibrary => "✨ 효과 라이브러리",
                EditorTab.Templates => "📁 템플릿",
                EditorTab.BatchEdit => "🔧 일괄 편집",
                EditorTab.Export => "💾 내보내기/가져오기",
                EditorTab.Settings => "⚙️ 설정",
                _ => tab.ToString()
            };
        }

        // =====================================================
        // 대시보드 탭
        // =====================================================

        private void DrawDashboardTab()
        {
            EditorGUILayout.LabelField("📊 스킬 대시보드", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // 좌측: 통계
            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            DrawStatistics();
            EditorGUILayout.EndVertical();

            // 중앙: 최근 작업
            EditorGUILayout.BeginVertical();
            DrawRecentWork();
            EditorGUILayout.EndVertical();

            // 우측: 빠른 액션
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawQuickActions();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // BP 업그레이드 통계 추가
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ BP 업그레이드", EditorStyles.boldLabel);

            int baseSkills = database.GetAllSkills().Count(s => s.bpLevel == 0);
            int bpSkills = database.GetAllSkills().Count(s => s.bpLevel > 0);
            int pathCount = bpDatabase?.GetAllUpgradePaths().Count ?? 0;

            EditorGUILayout.LabelField($"기본 스킬: {baseSkills}개");
            EditorGUILayout.LabelField($"BP 업그레이드: {bpSkills}개");
            EditorGUILayout.LabelField($"업그레이드 경로: {pathCount}개");

            if (GUILayout.Button("BP 업그레이드 관리"))
            {
                currentTab = EditorTab.BPUpgrade;
            }

            EditorGUILayout.EndVertical();

        }

        private void DrawStatistics()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📈 통계", EditorStyles.boldLabel);

            var skills = database.GetAllSkills();

            EditorGUILayout.LabelField($"총 스킬 수: {skills.Count}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("카테고리별:", EditorStyles.boldLabel);
            foreach (SkillCategory category in Enum.GetValues(typeof(SkillCategory)))
            {
                if (category == SkillCategory.All) continue;
                var count = skills.Count(s => s.category == category);
                if (count > 0)
                {
                    EditorGUILayout.LabelField($"  {GetCategoryIcon(category)} {category}: {count}");
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("클래스별:", EditorStyles.boldLabel);
            foreach (CharacterClass charClass in Enum.GetValues(typeof(CharacterClass)))
            {
                if (charClass == CharacterClass.All) continue;
                var count = skills.Count(s => s.characterClass == charClass);
                if (count > 0)
                {
                    EditorGUILayout.LabelField($"  {GetClassIcon(charClass)} {charClass}: {count}");
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("티어별:", EditorStyles.boldLabel);
            for (int tier = 0; tier <= 5; tier++)
            {
                var count = skills.Count(s => s.tier == tier);
                if (count > 0)
                {
                    EditorGUILayout.LabelField($"  티어 {tier}: {count}");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRecentWork()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🕐 최근 작업", EditorStyles.boldLabel);

            var recentSkills = database.GetRecentSkills(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));

            foreach (var skill in recentSkills)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"{GetCategoryIcon(skill.category)} {skill.skillName}");

                if (GUILayout.Button("편집", GUILayout.Width(50)))
                {
                    currentSkill = skill;
                    currentTab = EditorTab.SkillCreator;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ 빠른 액션", EditorStyles.boldLabel);

            if (GUILayout.Button("➕ 새 액티브 스킬", GUILayout.Height(30)))
            {
                CreateNewSkill(SkillCategory.Active);
            }

            if (GUILayout.Button("➕ 새 패시브 스킬", GUILayout.Height(30)))
            {
                CreateNewSkill(SkillCategory.Passive);
            }

            if (GUILayout.Button("➕ 새 스페셜 액티브", GUILayout.Height(30)))
            {
                CreateNewSkill(SkillCategory.SpecialActive);
            }

            if (GUILayout.Button("➕ 새 스페셜 패시브", GUILayout.Height(30)))
            {
                CreateNewSkill(SkillCategory.SpecialPassive);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("📋 템플릿에서 생성", GUILayout.Height(25)))
            {
                currentTab = EditorTab.Templates;
            }

            if (GUILayout.Button("📁 CSV 가져오기", GUILayout.Height(25)))
            {
                currentTab = EditorTab.Export;
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("🔍 스킬 검색", GUILayout.Height(25)))
            {
                currentTab = EditorTab.SkillList;
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // 스킬 목록 탭
        // =====================================================

        // 페이지네이션 변수
        private int currentPage = 0;
        private int itemsPerPage = 20;
        private enum SortOrder { Newest, Oldest, Name, Tier, ID }
        private SortOrder currentSortOrder = SortOrder.Newest;

        private void DrawSkillListTab()
        {
            EditorGUILayout.BeginHorizontal();

            // 왼쪽: 필터 패널
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawFilterPanel();
            EditorGUILayout.EndVertical();

            // 중앙: 스킬 리스트
            EditorGUILayout.BeginVertical();
            //  DrawSkillList();

            DrawEnhancedSkillList();  // 수정된 메서드 호출


            EditorGUILayout.EndVertical();

            // 오른쪽: 스킬 미리보기
            if (currentSkill != null)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(300));
                DrawSkillPreview();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterPanel()
        {
            EditorGUILayout.LabelField("🔍 검색", EditorStyles.boldLabel);

            // 검색 모드 선택
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("검색 모드:", GUILayout.Width(70));
            searchMode = (SearchMode)EditorGUILayout.EnumPopup(searchMode, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // 검색어 입력
            EditorGUILayout.BeginHorizontal();
            searchText = EditorGUILayout.TextField(searchText);

            // 검색 버튼
            if (GUILayout.Button("🔍", GUILayout.Width(30)))
            {
                // 검색 실행 (엔터키 효과)
                GUI.FocusControl(null);
            }

            // 클리어 버튼
            if (GUILayout.Button("✕", GUILayout.Width(30)))
            {
                searchText = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            // 검색 힌트
            EditorGUILayout.LabelField(GetSearchHint(), EditorStyles.miniLabel);

            // 고급 검색 토글
            EditorGUILayout.Space();
            useAdvancedSearch = EditorGUILayout.Foldout(useAdvancedSearch, "고급 검색 옵션");

            if (useAdvancedSearch)
            {
                EditorGUI.indentLevel++;
                DrawAdvancedSearchOptions();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 기존 필터들
            EditorGUILayout.LabelField("📁 필터", EditorStyles.boldLabel);

            filterCategory = (SkillCategory)EditorGUILayout.EnumPopup("카테고리", filterCategory);
            filterClass = (CharacterClass)EditorGUILayout.EnumPopup("클래스", filterClass);
            filterTier = EditorGUILayout.IntSlider("티어", filterTier, -1, 5);

            if (filterTier == -1)
            {
                EditorGUILayout.LabelField("티어: 전체", EditorStyles.miniLabel);
            }

            // BP 스킬 포함 여부
            searchIncludeBPSkills = EditorGUILayout.Toggle("BP 스킬 포함", searchIncludeBPSkills);

            EditorGUILayout.Space();

            // 필터 리셋
            if (GUILayout.Button("필터 초기화"))
            {
                ResetFilters();
            }

            EditorGUILayout.Space();

            // 검색 결과 통계
            var filteredSkills = GetFilteredSkillsEnhanced();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"검색 결과: {filteredSkills.Count}개", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(searchText))
            {
                // 검색어별 매칭 통계
                var exactMatches = filteredSkills.Count(s =>
                    s.skillId.ToString() == searchText ||
                    s.skillName.Equals(searchText, StringComparison.OrdinalIgnoreCase));

                if (exactMatches > 0)
                {
                    EditorGUILayout.LabelField($"정확한 일치: {exactMatches}개", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndVertical();
        }
        // 필터 초기화
        private void ResetFilters()
        {
            searchText = "";
            searchMode = SearchMode.All;
            filterCategory = SkillCategory.All;
            filterClass = CharacterClass.All;
            filterTier = -1;
            searchIncludeBPSkills = true;
            useAdvancedSearch = false;
            searchMinId = 0;
            searchMaxId = 99999;
            GUI.FocusControl(null);
        }

        // 빠른 ID 검색 (단축키 지원)
        private void OnGUI_SearchShortcuts()
        {
            Event e = Event.current;

            // Ctrl+F: 검색 포커스
            if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.F)
            {
                GUI.FocusControl("SearchField");
                e.Use();
            }

            // Ctrl+Shift+F: ID 검색 모드로 전환
            if (e.type == EventType.KeyDown && e.control && e.shift && e.keyCode == KeyCode.F)
            {
                searchMode = SearchMode.ID;
                GUI.FocusControl("SearchField");
                e.Use();
            }

            // ESC: 검색 초기화
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (!string.IsNullOrEmpty(searchText))
                {
                    searchText = "";
                    GUI.FocusControl(null);
                    e.Use();
                }
            }
        }


        // 고급 검색 옵션
        private void DrawAdvancedSearchOptions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ID 범위 검색
            EditorGUILayout.LabelField("ID 범위", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            searchMinId = EditorGUILayout.IntField("최소", searchMinId, GUILayout.Width(100));
            searchMaxId = EditorGUILayout.IntField("최대", searchMaxId, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // 정규식 검색
            EditorGUILayout.Space();
            bool useRegex = EditorGUILayout.Toggle("정규식 사용", false);
            if (useRegex)
            {
                EditorGUILayout.HelpBox("정규식 패턴을 사용하여 검색합니다.\n예: ^Fire.* (Fire로 시작하는 모든 스킬)", MessageType.Info);
            }

            // 복수 조건 검색
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("추가 검색 조건", EditorStyles.miniLabel);

            // 효과 타입으로 검색
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("효과 타입", GUILayout.Width(70));
            var effectType = (SkillSystem.EffectType)EditorGUILayout.EnumPopup(SkillSystem.EffectType.None);
            EditorGUILayout.EndHorizontal();

            // 쿨다운으로 검색
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("쿨다운", GUILayout.Width(70));
            int cooldown = EditorGUILayout.IntSlider(0, 0, 10);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // 향상된 스킬 목록 표시
        private void DrawEnhancedSkillList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📋 스킬 목록", EditorStyles.boldLabel);

            // 정렬 옵션
            EditorGUILayout.LabelField("정렬:", GUILayout.Width(40));
            var sortOptions = new[] { "ID", "이름", "티어", "최근 수정" };
            var sortIndex = EditorGUILayout.Popup(0, sortOptions, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var skills = GetFilteredSkillsEnhanced();

            // 정렬 적용
            switch (sortIndex)
            {
                case 0: skills = skills.OrderBy(s => s.skillId).ToList(); break;
                case 1: skills = skills.OrderBy(s => s.skillName).ToList(); break;
                case 2: skills = skills.OrderBy(s => s.tier).ThenBy(s => s.skillId).ToList(); break;
                case 3: skills = skills.OrderByDescending(s => s.lastModified).ToList(); break;
            }

            // 검색 결과 하이라이트
            bool hasSearchTerm = !string.IsNullOrEmpty(searchText);

            foreach (var skill in skills)
            {
                DrawSkillListItem(skill, hasSearchTerm);
            }

            EditorGUILayout.EndScrollView();
        }



      /*  private void DrawSkillList()
        {
            var filteredSkills = GetFilteredAndSortedSkills();
            int totalSkills = filteredSkills.Count;
            int totalPages = Mathf.CeilToInt((float)totalSkills / itemsPerPage);

            // 페이지 범위 조정
            if (currentPage >= totalPages && totalPages > 0)
                currentPage = totalPages - 1;
            if (currentPage < 0)
                currentPage = 0;

            // 헤더
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📋 스킬 목록 (총 {totalSkills}개)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"페이지 {currentPage + 1} / {Mathf.Max(1, totalPages)}");
            EditorGUILayout.EndHorizontal();

            // 페이지네이션 컨트롤 (상단)
            DrawPaginationControls(totalPages);

            // 스킬 리스트
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, totalSkills);

            for (int i = startIndex; i < endIndex; i++)
            {
                var skill = filteredSkills[i];
                DrawSkillListItem(skill, i - startIndex + 1);
            }

            EditorGUILayout.EndScrollView();

            // 페이지네이션 컨트롤 (하단)
            DrawPaginationControls(totalPages);
        }*/



        private void DrawSkillListItem(AdvancedSkillData skill, bool highlightSearch)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 선택 체크박스 (배치 편집 모드일 때만)
            if (batchOperation != BatchEditOperation.None)
            {
                bool isSelected = selectedSkills.Contains(skill);
                if (EditorGUILayout.Toggle(isSelected, GUILayout.Width(20)))
                {
                    if (!isSelected) selectedSkills.Add(skill);
                }
                else
                {
                    if (isSelected) selectedSkills.Remove(skill);
                }
            }

            // 번호 표시 제거 (skills 리스트가 없으므로)
            // 대신 체크마크나 BP 레벨 표시
            if (skill.bpLevel > 0)
            {
                EditorGUILayout.LabelField($"BP{skill.bpLevel}",
                    EditorStyles.miniLabel, GUILayout.Width(30));
            }
            else if (skill.parentSkillId > 0)
            {
                EditorGUILayout.LabelField("→",
                    EditorStyles.miniLabel, GUILayout.Width(30));
            }
            else
            {
                EditorGUILayout.LabelField("", GUILayout.Width(30));
            }

            // 아이콘
            if (skill.icon != null)
            {
                GUILayout.Label(skill.icon.texture, GUILayout.Width(32), GUILayout.Height(32));
            }
            else
            {
                GUILayout.Space(36);
            }

            // 스킬 정보
            EditorGUILayout.BeginVertical();

            // 이름과 ID - 검색어 하이라이트
            EditorGUILayout.BeginHorizontal();

            string displayName = GetDisplaySkillName(skill);

            // 검색어 매칭시 하이라이트
            if (highlightSearch && IsSearchMatch(skill, searchText))
            {
                GUI.color = Color.yellow;
            }

            // BP 스킬 표시
            if (skill.bpLevel > 0 || skill.parentSkillId > 0)
            {
                GUI.color = Color.cyan;
                displayName = $"[BP{skill.bpLevel}] {displayName}";
            }

            EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);

            // ID는 항상 표시
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"ID: {skill.skillId}",
                EditorStyles.miniLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();

            // 추가 정보
            EditorGUILayout.LabelField(
                $"{GetClassIcon(skill.characterClass)} {skill.characterClass} | " +
                $"T{skill.tier} | {skill.category} | CD:{skill.cooldown}");

            // 효과 요약
            if (skill.effects.Count > 0)
            {
                string effectSummary = skill.effects[0].GetShortDescription();
                if (skill.effects.Count > 1)
                    effectSummary += $" 외 {skill.effects.Count - 1}개";
                EditorGUILayout.LabelField(effectSummary, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            // 액션 버튼들
            DrawSkillActionButtons(skill);

            EditorGUILayout.EndHorizontal();

        }

        private void DrawSkillActionButtons(AdvancedSkillData skill)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(120));

            // 첫 번째 줄 버튼들
            EditorGUILayout.BeginHorizontal();

            // 편집 버튼
            if (GUILayout.Button("편집", GUILayout.Width(55)))
            {
                currentSkill = skill;
                currentTab = EditorTab.SkillCreator;

                // 이름이 없으면 자동 생성된 이름 제안
                if (string.IsNullOrEmpty(skill.skillName) || skill.skillName.Length < 2)
                {
                    skill.skillName = GenerateSkillName(skill);
                }
            }

            // 복제 버튼
            if (GUILayout.Button("복제", GUILayout.Width(55)))
            {
                DuplicateSkill(skill);
            }

            EditorGUILayout.EndHorizontal();

            // 두 번째 줄 버튼들
            EditorGUILayout.BeginHorizontal();

            // 비교 버튼
            GUI.backgroundColor = (compareSkill == skill) ? Color.cyan : Color.white;
            if (GUILayout.Button("비교", GUILayout.Width(55)))
            {
                compareSkill = skill;
            }
            GUI.backgroundColor = Color.white;

            // 삭제 버튼
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("삭제", GUILayout.Width(55)))
            {
                if (EditorUtility.DisplayDialog("삭제 확인",
                    $"{GetDisplaySkillName(skill)} (ID: {skill.skillId})을(를) 삭제하시겠습니까?",
                    "삭제", "취소"))
                {
                    database.RemoveSkill(skill);
                    SaveDatabase();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }


        // 향상된 필터링 메서드
        private List<AdvancedSkillData> GetFilteredSkillsEnhanced()
        {
            var skills = database.GetAllSkills();

            // ID 범위 필터
            if (useAdvancedSearch)
            {
                skills = skills.Where(s => s.skillId >= searchMinId && s.skillId <= searchMaxId).ToList();
            }

            // BP 스킬 필터
            if (!searchIncludeBPSkills)
            {
                skills = skills.Where(s => s.bpLevel == 0 && s.parentSkillId == 0).ToList();
            }

            // 검색어 필터 (향상된 버전)
            if (!string.IsNullOrEmpty(searchText))
            {
                switch (searchMode)
                {
                    case SearchMode.All:
                        skills = skills.Where(s =>
                            s.skillId.ToString().Contains(searchText) ||
                            s.skillName.ToLower().Contains(searchText.ToLower()) ||
                            s.description.ToLower().Contains(searchText.ToLower()) ||
                            s.tags.ToLower().Contains(searchText.ToLower()) ||
                            s.effects.Any(e => e.name?.ToLower().Contains(searchText.ToLower()) ?? false)
                        ).ToList();
                        break;

                    case SearchMode.ID:
                        // ID 검색 - 정확한 매칭 또는 부분 매칭
                        if (int.TryParse(searchText, out int searchId))
                        {
                            skills = skills.Where(s => s.skillId == searchId).ToList();
                        }
                        else
                        {
                            skills = skills.Where(s => s.skillId.ToString().Contains(searchText)).ToList();
                        }
                        break;

                    case SearchMode.Name:
                        skills = skills.Where(s => s.skillName.ToLower().Contains(searchText.ToLower())).ToList();
                        break;

                    case SearchMode.Description:
                        skills = skills.Where(s => s.description.ToLower().Contains(searchText.ToLower())).ToList();
                        break;

                    case SearchMode.Effect:
                        skills = skills.Where(s =>
                            s.effects.Any(e =>
                                e.name?.ToLower().Contains(searchText.ToLower()) ?? false ||
                                e.type.ToString().ToLower().Contains(searchText.ToLower())
                            )
                        ).ToList();
                        break;

                    case SearchMode.Tags:
                        skills = skills.Where(s => s.tags.ToLower().Contains(searchText.ToLower())).ToList();
                        break;
                }
            }

            // 기존 필터들 적용
            if (filterCategory != SkillCategory.All)
            {
                skills = skills.Where(s => s.category == filterCategory).ToList();
            }

            if (filterClass != CharacterClass.All)
            {
                skills = skills.Where(s => s.characterClass == filterClass).ToList();
            }

            if (filterTier >= 0)
            {
                skills = skills.Where(s => s.tier == filterTier).ToList();
            }

            return skills;
        }

        // 검색 매칭 확인
        private bool IsSearchMatch(AdvancedSkillData skill, string search)
        {
            if (string.IsNullOrEmpty(search)) return false;

            switch (searchMode)
            {
                case SearchMode.ID:
                    return skill.skillId.ToString().Contains(search);
                case SearchMode.Name:
                    return skill.skillName.ToLower().Contains(search.ToLower());
                default:
                    return skill.skillId.ToString().Contains(search) ||
                           skill.skillName.ToLower().Contains(search.ToLower());
            }
        }

        // 검색 힌트 텍스트
        private string GetSearchHint()
        {
            switch (searchMode)
            {
                case SearchMode.All:
                    return "ID, 이름, 설명, 효과, 태그 검색";
                case SearchMode.ID:
                    return "스킬 ID로 검색 (예: 1001)";
                case SearchMode.Name:
                    return "스킬 이름으로 검색";
                case SearchMode.Description:
                    return "스킬 설명으로 검색";
                case SearchMode.Effect:
                    return "효과 이름이나 타입으로 검색";
                case SearchMode.Tags:
                    return "태그로 검색";
                default:
                    return "";
            }
        }



        private void DrawPaginationControls(int totalPages)
        {
            if (totalPages <= 1) return;

            EditorGUILayout.BeginHorizontal();

            // 첫 페이지
            GUI.enabled = currentPage > 0;
            if (GUILayout.Button("|◀", GUILayout.Width(40)))
            {
                currentPage = 0;
            }

            // 이전 페이지
            if (GUILayout.Button("◀", GUILayout.Width(40)))
            {
                currentPage = Mathf.Max(0, currentPage - 1);
            }
            GUI.enabled = true;

            // 페이지 번호 직접 입력
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("페이지", GUILayout.Width(40));
            int newPage = EditorGUILayout.IntField(currentPage + 1, GUILayout.Width(50)) - 1;
            if (newPage != currentPage && newPage >= 0 && newPage < totalPages)
            {
                currentPage = newPage;
            }
            EditorGUILayout.LabelField($"/ {totalPages}", GUILayout.Width(40));

            GUILayout.FlexibleSpace();

            // 다음 페이지
            GUI.enabled = currentPage < totalPages - 1;
            if (GUILayout.Button("▶", GUILayout.Width(40)))
            {
                currentPage = Mathf.Min(totalPages - 1, currentPage + 1);
            }

            // 마지막 페이지
            if (GUILayout.Button("▶|", GUILayout.Width(40)))
            {
                currentPage = totalPages - 1;
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // 빠른 페이지 이동
            if (totalPages > 10)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("빠른 이동:", GUILayout.Width(60));

                int[] quickPages = { 1, 5, 10, 20, 30, 50 };
                foreach (int page in quickPages)
                {
                    if (page <= totalPages)
                    {
                        if (GUILayout.Button(page.ToString(), GUILayout.Width(30)))
                        {
                            currentPage = page - 1;
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private List<AdvancedSkillData> GetFilteredAndSortedSkills()
        {
            var skills = GetFilteredSkills();

            // 정렬 적용
            switch (currentSortOrder)
            {
                case SortOrder.Newest:
                    skills = skills.OrderByDescending(s => s.lastModified)
                                  .ThenByDescending(s => s.skillId).ToList();
                    break;
                case SortOrder.Oldest:
                    skills = skills.OrderBy(s => s.lastModified)
                                  .ThenBy(s => s.skillId).ToList();
                    break;
                case SortOrder.Name:
                    skills = skills.OrderBy(s => s.skillName).ToList();
                    break;
                case SortOrder.Tier:
                    skills = skills.OrderByDescending(s => s.tier)
                                  .ThenBy(s => s.skillName).ToList();
                    break;
                case SortOrder.ID:
                    skills = skills.OrderBy(s => s.skillId).ToList();
                    break;
            }

            return skills;
        }

        private void DrawSkillPreview()
        {
            if (currentSkill == null) return;

            EditorGUILayout.LabelField("👁️ 미리보기", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ID 표시 추가
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(currentSkill.skillName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {currentSkill.skillId}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"클래스: {currentSkill.characterClass}");
            EditorGUILayout.LabelField($"카테고리: {currentSkill.category}");
            EditorGUILayout.LabelField($"티어: {currentSkill.tier}");
            
            EditorGUILayout.LabelField($"쿨다운: {currentSkill.cooldown} 턴");

            // 효율 점수 표시
            if (currentSkill.effects.Count > 0)
            {
                var efficiency = SkillEfficiencyCalculator.CalculateEfficiency(currentSkill);
                EditorGUILayout.Space();

                var oldColor = GUI.color;
                GUI.color = efficiency.GetGradeColor();
                EditorGUILayout.LabelField($"효율 등급: {efficiency.GetGradeText()} ({efficiency.TotalScore:F1}점)");
                GUI.color = oldColor;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("효과:", EditorStyles.boldLabel);

            foreach (var effect in currentSkill.effects)
            {
                EditorGUILayout.LabelField($"• {effect.GetFullDescription()}");
            }

            // 최근 수정일 표시
            if (currentSkill.lastModified != DateTime.MinValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"수정일: {currentSkill.lastModified:yyyy-MM-dd HH:mm}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            // 액션 버튼
            EditorGUILayout.Space();
            if (GUILayout.Button("이 스킬 편집", GUILayout.Height(25)))
            {
                currentTab = EditorTab.SkillCreator;
            }
        }

        // =====================================================
        // 유틸리티 메서드
        // =====================================================

        private void CreateNewSkill(SkillCategory category)
        {
            currentSkill = new AdvancedSkillData
            {
                skillId = database.GetNextId(category),  // 카테고리별 ID 자동 할당
                skillName = $"새 {category} 스킬",
                category = category,
                characterClass = CharacterClass.Vanguard,
                tier = 1,
            };

            currentTab = EditorTab.SkillCreator;
        }

        private void DuplicateSkill(AdvancedSkillData original)
        {
            var duplicate = original.Clone();
            duplicate.skillId = database.GetNextId(duplicate.category);  // 카테고리 기반 ID 할당
            duplicate.skillName += " (복사본)";
            database.AddOrUpdateSkill(duplicate);
            SaveDatabase();  // 즉시 저장
        }

        private List<AdvancedSkillData> GetFilteredSkills()
        {
            var skills = database.GetAllSkills();

            if (!string.IsNullOrEmpty(searchText))
            {
                skills = skills.Where(s => s.skillName.ToLower().Contains(searchText.ToLower())).ToList();
            }

            if (filterCategory != SkillCategory.All)
            {
                skills = skills.Where(s => s.category == filterCategory).ToList();
            }

            if (filterClass != CharacterClass.All)
            {
                skills = skills.Where(s => s.characterClass == filterClass).ToList();
            }

            if (filterTier >= 0)
            {
                skills = skills.Where(s => s.tier == filterTier).ToList();
            }

            return skills;
        }

        private void SaveDatabase()
        {
            database.Save();
            AssetDatabase.Refresh();
            Debug.Log("스킬 데이터베이스 저장 완료");
        }

        private void LoadDatabase()
        {
            database = AdvancedSkillDatabase.Load();
            Debug.Log($"스킬 데이터베이스 로드 완료: {database.GetAllSkills().Count}개 스킬");
        }

        // 아이콘 헬퍼 메서드들
        private string GetCategoryIcon(SkillCategory category) => category switch
        {
            SkillCategory.Active => "⚔️",
            SkillCategory.Passive => "🛡️",
            SkillCategory.SpecialActive => "⭐",
            SkillCategory.SpecialPassive => "💫",
            _ => "📋"
        };

        private string GetClassIcon(CharacterClass charClass) => charClass switch
        {
            CharacterClass.Vanguard => "🛡️",
            CharacterClass.Slaughter => "⚔️",
            CharacterClass.Jacker => "🗡️",
            CharacterClass.Rewinder => "💉",
            _ => "👤"
        };

        private string GetEffectIcon(EffectType type) => type switch
        {
            EffectType.Damage => "💥",
            EffectType.Heal => "💚",
            EffectType.Buff => "⬆️",
            EffectType.Debuff => "⬇️",
            EffectType.StatusEffect => "😵",
            EffectType.Shield => "🛡️",
            EffectType.Reflect => "🔄",
            EffectType.Dispel => "✨",
            _ => "•"
        };

        private string GetTierStars(int tier)
        {
            return new string('⭐', tier);
        }

        #region 스킬 이름 자동 생성 헬퍼 메서드들 (클래스 내부에 추가)

        /// <summary>
        /// 스킬 표시용 이름 가져오기 (비어있으면 자동 생성)
        /// </summary>
        private string GetDisplaySkillName(AdvancedSkillData skill)
        {
            // 이름이 있으면 그대로 반환
            if (!string.IsNullOrEmpty(skill.skillName) && skill.skillName.Length > 2)
                return skill.skillName;

            // 이름이 없으면 자동 생성
            return GenerateSkillName(skill);
        }

        /// <summary>
        /// 스킬 특성 기반 이름 자동 생성
        /// </summary>
        private string GenerateSkillName(AdvancedSkillData skill)
        {
            string generatedName = "";

            // 1. 주요 효과 기반 이름 생성
            if (skill.effects != null && skill.effects.Count > 0)
            {
                var mainEffect = skill.effects[0];

                // 효과 타입별 기본 이름
                string effectName = mainEffect.type switch
                {
                    EffectType.Damage => "공격",
                    EffectType.TrueDamage => "관통공격",
                    EffectType.Heal => "치유",
                    EffectType.HealOverTime => "지속치유",
                    EffectType.LifeSteal => "흡혈",
                    EffectType.Buff => GetBuffName(mainEffect),
                    EffectType.Debuff => GetDebuffName(mainEffect),
                    EffectType.StatusEffect => GetStatusEffectName(mainEffect),
                    EffectType.Shield => "보호막",
                    EffectType.Reflect => "반사",
                    EffectType.DamageReduction => "피해감소",
                    EffectType.Immunity => "면역",
                    EffectType.Invincible => "무적",
                    EffectType.Dispel => "해제",
                    EffectType.Summon => "소환",
                    EffectType.Transform => "변신",
                    EffectType.Special => "특수",
                    _ => "효과"
                };

                // 타겟 타입 추가
                string targetPrefix = mainEffect.targetType switch
                {
                    TargetType.Self => "자가",
                    TargetType.EnemySingle => "단일",
                    TargetType.EnemyAll => "전체",
                    TargetType.EnemyRow => "열",
                    TargetType.AllySingle => "아군",
                    TargetType.AllyAll => "광역",
                    TargetType.AllyRow => "아군열",
                    TargetType.Random => "무작위",
                    TargetType.Multiple => $"{mainEffect.targetCount}연속",
                    TargetType.Lowest => "최소",
                    TargetType.Highest => "최대",
                    _ => ""
                };

                // 강도 표시
                string intensity = "";
                if (mainEffect.value > 0)
                {
                    intensity = mainEffect.value switch
                    {
                        < 50 => "약한",
                        < 100 => "",
                        < 200 => "강력한",
                        _ => "극강"
                    };
                }

                // 조합
                generatedName = $"{intensity} {targetPrefix} {effectName}".Trim();
            }
            else
            {
                // 효과가 없으면 카테고리 기반
                generatedName = skill.category switch
                {
                    SkillCategory.Active => "액티브",
                    SkillCategory.Passive => "패시브",
                    SkillCategory.SpecialActive => "특수액티브",
                    SkillCategory.SpecialPassive => "특수패시브",
                    _ => "스킬"
                };
            }

            // 2. 클래스 특성 추가
            if (skill.characterClass != CharacterClass.All)
            {
                string className = GetClassNameKorean(skill.characterClass);
                if (!string.IsNullOrEmpty(className))
                    generatedName = $"[{className}] {generatedName}";
            }

            // 3. 티어 표시 (높은 티어만)
            if (skill.tier >= 3)
            {
                generatedName = $"{generatedName} T{skill.tier}";
            }

            // 4. ID 추가 (중복 방지)
            generatedName = $"{generatedName} #{skill.skillId}";

            return generatedName;
        }

        /// <summary>
        /// 버프 이름 생성
        /// </summary>
        private string GetBuffName(AdvancedSkillEffect effect)
        {
            return effect.statType switch
            {
                StatType.Attack => "공격강화",
                StatType.Defense => "방어강화",
                StatType.Speed => "가속",
                StatType.CritRate => "치명강화",
                StatType.CritDamage => "치명피해강화",
                StatType.Accuracy => "정확강화",
                StatType.Evasion => "회피강화",
                StatType.EffectHit => "효과적중강화",
                StatType.EffectResist => "저항강화",
                StatType.MaxHP => "체력강화",
                _ => "강화"
            };
        }

        /// <summary>
        /// 디버프 이름 생성
        /// </summary>
        private string GetDebuffName(AdvancedSkillEffect effect)
        {
            return effect.statType switch
            {
                StatType.Attack => "공격약화",
                StatType.Defense => "방어약화",
                StatType.Speed => "둔화",
                StatType.CritRate => "치명약화",
                StatType.CritDamage => "치명피해약화",
                StatType.Accuracy => "정확약화",
                StatType.Evasion => "회피약화",
                StatType.EffectHit => "효과적중약화",
                StatType.EffectResist => "저항약화",
                StatType.HealBlock => "회복차단",
                StatType.HealReduction => "회복감소",
                _ => "약화"
            };
        }

        /// <summary>
        /// 상태이상 이름 생성
        /// </summary>
        private string GetStatusEffectName(AdvancedSkillEffect effect)
        {
            return effect.statusType switch
            {
                StatusType.Stun => "기절",
                StatusType.Silence => "침묵",
                StatusType.Taunt => "도발",
                StatusType.Freeze => "빙결",
                StatusType.Burn => "화상",
                StatusType.Poison => "중독",
                StatusType.Bleed => "출혈",
                StatusType.Sleep => "수면",
                StatusType.Confuse => "혼란",
                StatusType.Petrify => "석화",
                StatusType.Blind => "실명",
                StatusType.Slow => "둔화",
                StatusType.Weaken => "약화",
                StatusType.Curse => "저주",
                _ => "상태이상"
            };
        }

        /// <summary>
        /// 클래스 한글 이름
        /// </summary>
        private string GetClassNameKorean(CharacterClass charClass)
        {
            return charClass switch
            {
                CharacterClass.Vanguard => "뱅가드",
                CharacterClass.Slaughter => "슬로터",
                CharacterClass.Jacker => "재커",
                CharacterClass.Rewinder => "리와인더",
                _ => ""
            };
        }

        /// <summary>
        /// 일괄 이름 생성 유틸리티
        /// </summary>
        private void GenerateNamesForUnnamedSkills()
        {
            int count = 0;
            foreach (var skill in database.GetAllSkills())
            {
                if (string.IsNullOrEmpty(skill.skillName) || skill.skillName.Length < 2 )
                {
                    skill.skillName = GenerateSkillName(skill);
                    count++;
                }
            }

            if (count > 0)
            {
                SaveDatabase();
                Debug.Log($"[Skill Editor] {count}개의 스킬에 이름을 자동 생성했습니다.");
            }
        }

        #endregion




    }
}

#endif