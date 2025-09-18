using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using BattleCharacterSystem;
using IronJade.Table.Data;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using BattleCharacterSystem.Editor;
using BattleCharacterSystem.Timeline;
using UnityEngine.Timeline;

public class BattleCharacterEditorWindow : EditorWindow
{
    // 탭 관련
    private enum EditorTab
    {
        Character,
        Monster,
        MonsterGroup,
        Templates,
        Settings
    }

    private EditorTab currentTab = EditorTab.Character;
    private Vector2 scrollPosition;

    // 데이터 리스트
    private List<BattleCharacterDataSO> characterDataList = new List<BattleCharacterDataSO>();
    private BattleCharacterDataSO selectedCharacter;
    private int selectedCharacterIndex = -1;

    // 검색/필터
    private string searchString = "";
    private CharacterTier filterTier = CharacterTier.A;
    private ClassType filterClass = ClassType.Slaughter;
    private bool showFilters = false;

    // 에디터 상태
    private bool isCreatingNew = false;
    private bool isDirty = false;

    // 템플릿 탭 관리자
    private TemplateEditorTab templateTab;
    // 몬스터 탭 관리자  
    private MonsterEditorTab monsterTab;

    private MonsterGroupEditorTab monsterGroupTab;

    // 스타일
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle selectedStyle;

    private HashSet<int> usedCharacterIds = new HashSet<int>(); // ID 중복 체크용
                                                                // 썸네일 관련
    private Dictionary<int, Texture2D> thumbnailCache = new Dictionary<int, Texture2D>();
    private bool showThumbnails = true;
    private float thumbnailSize = 32f;

    // BattleCharacterEditorWindow.cs에 추가할 필드
    //private CharacterTimelineConfig timelineConfig;

    private bool showTimelineSection = true;




    [MenuItem("*COSMOS*/Battle/🎮 캐릭터 제작 툴")]
    public static void ShowWindow()
    {
        var window = GetWindow<BattleCharacterEditorWindow>("Battle Character Editor");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadAllCharacterData();
        InitializeStyles();

        CollectUsedIds();

        // 썸네일 캐시 초기화
        thumbnailCache.Clear();

        // 템플릿 탭 초기화
        templateTab = new TemplateEditorTab();
        templateTab.Initialize();

        monsterTab = new MonsterEditorTab();
        monsterTab.Initialize();

        monsterGroupTab = new MonsterGroupEditorTab();
        monsterGroupTab.Initialize();

    }


    private void CollectUsedIds()
    {
        usedCharacterIds.Clear();
        foreach (var character in characterDataList)
        {
            usedCharacterIds.Add(character.CharacterId);
        }
    }

    private int GetNextAvailableId()
    {
        int nextId = 1000;
        while (characterDataList.Any(c => c.CharacterId == nextId))
        {
            nextId++;
        }
        return nextId;
    }

    private void InitializeStyles()
    {
        headerStyle = new GUIStyle();
        headerStyle.fontSize = 14;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.white;
        headerStyle.padding = new RectOffset(5, 5, 5, 5);
        headerStyle.richText = true;  // Rich Text 활성화


        boxStyle = new GUIStyle();
        boxStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.5f));
        boxStyle.padding = new RectOffset(5, 5, 5, 5);
        boxStyle.margin = new RectOffset(2, 2, 2, 2);
        boxStyle.richText = true;  // Rich Text 활성화


        selectedStyle = new GUIStyle();
        selectedStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.5f, 0.7f, 0.5f));
    }

    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    private void OnGUI()
    {
        DrawHeader();

        DrawTabs();

        EditorGUILayout.Space(5);

        // 템플릿 탭은 별도 처리
        if (currentTab == EditorTab.Templates)
        {
            templateTab.DrawTemplateTab();
            return;
        }

        // 몬스터 탭 별도 처리
        if (currentTab == EditorTab.Monster)
        {
            monsterTab.DrawMonsterTab();
            return;
        }

        // 몬스터 그룹 탭 별도 처리
        if (currentTab == EditorTab.MonsterGroup)
        {
           monsterGroupTab.DrawMonsterGroupTab();
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            // 왼쪽 패널 (리스트)
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                DrawSearchBar();
                DrawListPanel();
            }

            // 구분선
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // 오른쪽 패널 (디테일)
            using (new EditorGUILayout.VerticalScope())
            {
                DrawDetailPanel();
            }
        }

        // 하단 버튼
        DrawBottomButtons();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Battle Character System", headerStyle);
            GUILayout.FlexibleSpace();

            // 변경사항 표시
            if (isDirty)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⚠ Unsaved Changes", EditorStyles.toolbarButton);
                GUI.color = Color.white;
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (isDirty)
                {
                    if (EditorUtility.DisplayDialog("Unsaved Changes",
                        "You have unsaved changes. Do you want to save before refreshing?",
                        "Save", "Don't Save"))
                    {
                        SaveChanges();
                    }
                }
                LoadAllCharacterData();
            }

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                currentTab = EditorTab.Settings;
            }
        }
    }

    private void DrawTabs()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var tabs = System.Enum.GetValues(typeof(EditorTab)).Cast<EditorTab>();
            foreach (var tab in tabs)
            {
                var style = (tab == currentTab) ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (tab == currentTab)
                {
                    GUI.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
                }

                if (GUILayout.Button(tab.ToString(), style))
                {
                    currentTab = tab;
                    isCreatingNew = false;
                }

                GUI.backgroundColor = Color.white;
            }
        }
    }

    private void DrawSearchBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            searchString = EditorGUILayout.TextField(searchString, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchString = "";
                GUI.FocusControl(null);
            }
        }

        // 썸네일 크기 조절 슬라이더 추가
        if (showThumbnails)
        {
            GUILayout.Label("Size:", GUILayout.Width(35));
            thumbnailSize = EditorGUILayout.Slider(thumbnailSize, 24f, 64f, GUILayout.Width(100));
        }

        // 필터 토글
        showFilters = EditorGUILayout.Foldout(showFilters, "Filters");
        if (showFilters)
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                filterTier = (CharacterTier)EditorGUILayout.EnumPopup("Tier", filterTier);
                filterClass = (ClassType)EditorGUILayout.EnumPopup("Class", filterClass);
            }
        }

        EditorGUILayout.Space(5);
    }

    private void DrawListPanel()
    {
        // 리스트 상단에 썸네일 토글 추가
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            showThumbnails = EditorGUILayout.ToggleLeft("Show Thumbnails", showThumbnails, GUILayout.Width(120));
        }

        // 새로 만들기 버튼
        if (GUILayout.Button("+ Create New Character", GUILayout.Height(30)))
        {
            CreateNewCharacter();
        }

        EditorGUILayout.Space(5);

        // 캐릭터 리스트
        using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
        {
            scrollPosition = scrollView.scrollPosition;

            var filteredList = GetFilteredCharacterList();

            for (int i = 0; i < filteredList.Count; i++)
            {
                var character = filteredList[i];
                if (character == null) continue;

                bool isSelected = (character == selectedCharacter);

                using (new EditorGUILayout.HorizontalScope(isSelected ? selectedStyle : boxStyle))
                {

                    // 썸네일 표시
                    if (showThumbnails)
                    {
                        var thumbnail = GetCharacterThumbnail(character);
                        if (thumbnail != null)
                        {
                            GUILayout.Label(thumbnail, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));
                        }
                        else
                        {
                            GUILayout.Box("?", GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));
                        }
                    }


                    // 캐릭터 정보 표시
                    string displayName = $"[{character.CharacterId}] {character.CharacterName}";
                    string info = $"{character.Tier} - {character.CharacterClass}";

                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(displayName, EditorStyles.boldLabel);
                        GUILayout.Label(info, EditorStyles.miniLabel);
                    }

                    GUILayout.FlexibleSpace();

                    // 복사 버튼
                    if (GUILayout.Button("Copy", GUILayout.Width(40)))
                    {
                        DuplicateCharacter(character);
                    }

                    // 삭제 버튼
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        DeleteCharacter(character);
                        break;
                    }
                }

                // 클릭 처리
                if (Event.current.type == EventType.MouseDown &&
                    GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    SelectCharacter(character, i);
                    Event.current.Use();
                }
            }
        }
    }


    /// <summary>
    /// 캐릭터 썸네일 가져오기 (캐시 사용)
    /// </summary>
    private Texture2D GetCharacterThumbnail(BattleCharacterDataSO character)
    {
        if (character == null) return null;

        // 캐시 확인
        if (thumbnailCache.ContainsKey(character.CharacterId) == true && thumbnailCache[character.CharacterId] != null )
        {
            return thumbnailCache[character.CharacterId];
        }

        // 썸네일이 아직 로드되지 않았으면 비동기로 로드 시작
        LoadThumbnailAsync(character);
        return null;
    }

    // 새로운 비동기 로드 메서드 추가
    private async void LoadThumbnailAsync(BattleCharacterDataSO character)
    {
        if (character == null || thumbnailCache.ContainsKey(character.CharacterId)) return;

        string thumbnailKey = $"Char_{character.CharacterName}_Idle_png";
        Texture2D texture = await ResourceLoadHelper.LoadAssetAsync<Texture2D>(thumbnailKey);

        if (texture != null)
        {
            thumbnailCache[character.CharacterId] = texture;
           // Repaint(); // 로드 완료 후 다시 그리기
        }
    }



    private Vector2 detailScrollPosition;  // 디테일 패널용 별도 스크롤 위치


    private void DrawDetailPanel()
    {

        if (selectedCharacter == null && !isCreatingNew)
        {
            EditorGUILayout.HelpBox("Select a character from the list or create a new one.", MessageType.Info);
            return;
        }

        if (isCreatingNew)
        {
            DrawNewCharacterPanel();
            return;
        }

        // 스크롤뷰 시작 - 올바른 방식
        detailScrollPosition = EditorGUILayout.BeginScrollView(
            detailScrollPosition,
            GUILayout.ExpandHeight(true)  // 높이 확장
        );

        // 모든 캐릭터 상세 정보
        DrawCharacterDetails();

        // 저장 버튼
        EditorGUILayout.Space(20);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (isDirty)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                if (GUILayout.Button("💾 Save Changes", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    SaveChanges();
                    EditorUtility.DisplayDialog("Saved", "Character data has been saved.", "OK");
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("✔ Saved", GUILayout.Height(30), GUILayout.Width(150));
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space(10); // 하단 여백

        // 스크롤뷰 종료
        EditorGUILayout.EndScrollView();
    }

    private void DrawTimelineSection()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Timeline Settings", headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            if (selectedCharacter == null) return;

            var serializedObject = new SerializedObject(selectedCharacter);
            var timelineSettingsProp = serializedObject.FindProperty("timelineSettings");

            if (timelineSettingsProp == null)
            {
                EditorGUILayout.HelpBox("TimelineSettings not found in CharacterDataSO", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();

            // Attack1 Timeline
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Attack1", GUILayout.Width(100));

                var attack1Prop = timelineSettingsProp.FindPropertyRelative("attack1Timeline");
                EditorGUILayout.PropertyField(attack1Prop, GUIContent.none);

                // Timeline Asset 변환 버튼
                if (GUILayout.Button("Convert", GUILayout.Width(60)))
                {
                    ConvertTimelineFromPath("Attack1", attack1Prop);
                }
            }

            // ActiveSkill1 Timeline
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("ActiveSkill1", GUILayout.Width(100));

                var activeSkill1Prop = timelineSettingsProp.FindPropertyRelative("activeSkill1Timeline");
                EditorGUILayout.PropertyField(activeSkill1Prop, GUIContent.none);

                if (GUILayout.Button("Convert", GUILayout.Width(60)))
                {
                    ConvertTimelineFromPath("ActiveSkill1", activeSkill1Prop);
                }
            }

            // PassiveSkill1 Timeline
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("PassiveSkill1", GUILayout.Width(100));

                var passiveSkill1Prop = timelineSettingsProp.FindPropertyRelative("passiveSkill1Timeline");
                EditorGUILayout.PropertyField(passiveSkill1Prop, GUIContent.none);

                if (GUILayout.Button("Convert", GUILayout.Width(60)))
                {
                    ConvertTimelineFromPath("PassiveSkill1", passiveSkill1Prop);
                }
            }

            // Custom Timelines
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Custom Timelines", EditorStyles.boldLabel);

            var customTimelinesProp = timelineSettingsProp.FindPropertyRelative("customTimelines");
            EditorGUILayout.PropertyField(customTimelinesProp, true);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(selectedCharacter);
                isDirty = true;
            }

            // 프리뷰 버튼
            EditorGUILayout.Space(5);
            if (GUILayout.Button("Preview Cosmos Timeline Window"))
            {
                OpenTimelinePreview();
            }
        }
    }



    private void ConvertAndAssignTimeline(TimelineAsset timeline, string slotName, SerializedProperty targetProp)
    {
        if (timeline == null || selectedCharacter == null) return;

        string outputPath = $"Assets/Cosmos/ResourcesAddressable/Characters/{selectedCharacter.CharacterName}/Animations/{timeline.name}_Data.asset";

        // 폴더 생성
        string dir = System.IO.Path.GetDirectoryName(outputPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        // Timeline 변환
        var timelineData = TimelineConverter.ConvertTimelineToSO(timeline, outputPath);

        // SerializedProperty에 직접 할당
        if (targetProp != null)
        {
            targetProp.objectReferenceValue = timelineData;
        }

        EditorUtility.SetDirty(selectedCharacter);
        isDirty = true;
    }

    private void ConvertTimelineFromPath(string slotName, SerializedProperty targetProp)
    {
        // Timeline Asset 선택 다이얼로그
        string path = EditorUtility.OpenFilePanel(
            $"Select {slotName} Timeline",
            "Assets/Dev/Cosmos/Timeline/Characters",
            "playable");

        if (!string.IsNullOrEmpty(path))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline != null)
            {
                ConvertAndAssignTimeline(timeline, slotName, targetProp);
            }
        }
    }

    private void OpenTimelinePreview()
    {
        // BattlePreviewWindow 열고 Timeline 재생 모드로 설정
        //BattlePreviewWindow.ShowWindow(selectedCharacter, null, null);
        // TODO: Timeline 재생 모드 추가
        // 간단하게 호출
        CosmosTimelineController.ShowWindowWithCharacter(selectedCharacter);

    }



    private void DrawCharacterDetails()
    {
        if (selectedCharacter == null) return;

        EditorGUI.BeginChangeCheck();

        // 기본 정보
        EditorGUILayout.LabelField("Basic Info", headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            selectedCharacter.name = EditorGUILayout.TextField("Asset Name", selectedCharacter.name);
            EditorGUILayout.Space(5);

            var serializedObject = new SerializedObject(selectedCharacter);

            // 기본 정보 필드들
            var idProp = serializedObject.FindProperty("characterId");
            var nameProp = serializedObject.FindProperty("characterName");
            var descProp = serializedObject.FindProperty("description");

            EditorGUI.BeginChangeCheck();
            int previousId = idProp.intValue;
            EditorGUILayout.PropertyField(idProp, new GUIContent("Character ID"));


            if (EditorGUI.EndChangeCheck())
            {
                int newId = idProp.intValue;
                if (newId != previousId)
                {
                    // 다른 캐릭터가 이 ID를 사용중인지 체크
                    bool isDuplicate = characterDataList.Any(c => c != selectedCharacter && c.CharacterId == newId);

                    if (isDuplicate)
                    {
                        EditorUtility.DisplayDialog("ID 중복",
                            $"ID {newId}는 이미 사용중입니다. 다른 ID를 입력해주세요.",
                            "확인");
                        idProp.intValue = previousId;
                    }
                    else
                    {
                        // ID 변경 성공
                        usedCharacterIds.Remove(previousId);
                        usedCharacterIds.Add(newId);
                    }
                }
            }

            // ID 사용 가능 표시 - 수정된 로직
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);

                // 현재 선택된 캐릭터를 제외하고 중복 체크
                bool isCurrentlyUsedByOthers = characterDataList.Any(c => c != selectedCharacter && c.CharacterId == idProp.intValue);

                if (!isCurrentlyUsedByOthers)
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("✓ 사용 가능", EditorStyles.miniLabel, GUILayout.Width(80));
                }
                else
                {
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField("✗ 중복됨", EditorStyles.miniLabel, GUILayout.Width(80));
                }
                GUI.color = Color.white;
            }



            EditorGUILayout.PropertyField(nameProp, new GUIContent("Character Name"));
            EditorGUILayout.PropertyField(descProp, new GUIContent("Description"));

            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(10);

        // 분류 정보
        EditorGUILayout.LabelField("Classification", headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            var serializedObject = new SerializedObject(selectedCharacter);

            var tierProp = serializedObject.FindProperty("tier");
            var classProp = serializedObject.FindProperty("characterClass");
            var elementProp = serializedObject.FindProperty("elementType");

            var attackTypeProp = serializedObject.FindProperty("attackType");
            var attackRangeProp = serializedObject.FindProperty("attackRange");


            EditorGUILayout.PropertyField(tierProp);
            EditorGUILayout.PropertyField(classProp, new GUIContent("Class"));
            EditorGUILayout.PropertyField(elementProp, new GUIContent("Element"));

            EditorGUILayout.PropertyField(attackTypeProp, new GUIContent("Attack Type"));
            EditorGUILayout.PropertyField(attackRangeProp, new GUIContent("Attack Range"));

            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(10);


        bool useCustomStat = false;
        // =====================================================
        // 새로 추가: 기본 스탯 섹션
        // =====================================================
        EditorGUILayout.LabelField("Base Stats (Level 1)", headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            var serializedObject = new SerializedObject(selectedCharacter);

            // Custom Stats 토글
            var useCustomStatsProp = serializedObject.FindProperty("useCustomStats");
            EditorGUILayout.PropertyField(useCustomStatsProp, new GUIContent("Use Custom Stats"));

            EditorGUILayout.Space(5);

            // Base Stats
            var baseStatsProp = serializedObject.FindProperty("baseStats");

            GUI.enabled = useCustomStatsProp.boolValue; // Custom Stats가 켜져있을 때만 편집 가능

            useCustomStat = useCustomStatsProp.boolValue;
            if (useCustomStat == true)
            {
                EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("hp"), new GUIContent("HP"));
                    EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("attack"), new GUIContent("ATK"));
                    EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("defense"), new GUIContent("DEF"));
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Growth Rates (per level)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("hpGrowth"), new GUIContent("HP Growth"));
                    EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("attackGrowth"), new GUIContent("ATK Growth"));
                    EditorGUILayout.PropertyField(baseStatsProp.FindPropertyRelative("defenseGrowth"), new GUIContent("DEF Growth"));
                }
            }

            GUI.enabled = true;

            // 레벨별 스탯 미리보기
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Level Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lv 1", GUILayout.Width(50)))
                    ShowStatsAtLevel(1);
                if (GUILayout.Button("Lv 50", GUILayout.Width(50)))
                    ShowStatsAtLevel(50);
                if (GUILayout.Button("Lv 100", GUILayout.Width(50)))
                    ShowStatsAtLevel(100);
                if (GUILayout.Button("Lv 200", GUILayout.Width(50)))
                    ShowStatsAtLevel(200);
            }

            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(10);

        // =====================================================
        // 새로 추가: 고정 스탯 섹션
        // =====================================================

        if (useCustomStat == true)
        {
            EditorGUILayout.LabelField("Fixed Stats", headerStyle);
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                var serializedObject = new SerializedObject(selectedCharacter);
                var fixedStatsProp = serializedObject.FindProperty("fixedStats");
                var useCustomStatsProp = serializedObject.FindProperty("useCustomStats");

                GUI.enabled = useCustomStatsProp.boolValue; // Custom Stats가 켜져있을 때만 편집 가능

                // BP & Speed
                EditorGUILayout.LabelField("Core Stats", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("maxBP"), new GUIContent("Max BP"));
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("turnSpeed"), new GUIContent("Speed"));
                }

                EditorGUILayout.Space(5);

                // Critical Stats
                EditorGUILayout.LabelField("Critical Stats", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("critRate"), new GUIContent("Crit Rate (%)"));
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("critDamage"), new GUIContent("Crit DMG (%)"));
                }

                EditorGUILayout.Space(5);

                // Accuracy Stats
                EditorGUILayout.LabelField("Accuracy & Evasion", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("hitRate"), new GUIContent("Hit (%)"));
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("dodgeRate"), new GUIContent("Dodge (%)"));
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("blockRate"), new GUIContent("Block (%)"));
                }

                EditorGUILayout.Space(5);

                // Special Stats
                EditorGUILayout.LabelField("Special Stats", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("aggro"), new GUIContent("Aggro"));
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("skillHitRate"), new GUIContent("Skill Hit (%)"));
                    EditorGUILayout.PropertyField(fixedStatsProp.FindPropertyRelative("skillResist"), new GUIContent("Skill Resist (%)"));
                }

                GUI.enabled = true;

                // 자동 계산 버튼
                if (!useCustomStatsProp.boolValue)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("Stats are automatically calculated based on Tier and Class", MessageType.Info);

                    if (GUILayout.Button("Recalculate Stats"))
                    {
                        RecalculateStats();
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(10);
        }

        // =====================================================
        // 새로 추가: 스탯 요약 섹션
        // =====================================================
        if (showStatsPreview)
        {
            EditorGUILayout.LabelField($"Stats Preview (Level {previewLevel})", headerStyle);
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                var stats = selectedCharacter.GetStatsAtLevel(previewLevel);

                // 3열로 표시
                EditorGUILayout.BeginHorizontal();

                // 첫 번째 열
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("HP:", stats.hp.ToString("N0"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Attack:", stats.attack.ToString("N0"));
                EditorGUILayout.LabelField("Defense:", stats.defense.ToString("N0"));
                EditorGUILayout.EndVertical();

                // 두 번째 열
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Speed:", selectedCharacter.FixedStats.turnSpeed.ToString("F1"));
                EditorGUILayout.LabelField("Crit Rate:", selectedCharacter.FixedStats.critRate.ToString("F1") + "%");
                EditorGUILayout.LabelField("Crit DMG:", selectedCharacter.FixedStats.critDamage.ToString("F0") + "%");
                EditorGUILayout.EndVertical();

                // 세 번째 열
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Hit Rate:", selectedCharacter.FixedStats.hitRate.ToString("F1") + "%");
                EditorGUILayout.LabelField("Dodge:", selectedCharacter.FixedStats.dodgeRate.ToString("F1") + "%");
                EditorGUILayout.LabelField("Block:", selectedCharacter.FixedStats.blockRate.ToString("F1") + "%");
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // 전투력 계산 (간단한 공식)
                float combatPower = stats.hp * 0.5f + stats.attack * 2f + stats.defense * 1.5f;
                EditorGUILayout.LabelField("Combat Power:", combatPower.ToString("N0"), EditorStyles.boldLabel);
            }
        }
        EditorGUILayout.Space(10);




        // 스킬 정보
        EditorGUILayout.LabelField("Skills", headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            var serializedObject = new SerializedObject(selectedCharacter);

            // 액티브 스킬
            EditorGUILayout.LabelField("액티브 스킬", EditorStyles.boldLabel);
            var activeSkillProp = serializedObject.FindProperty("activeSkillId");

            using (new EditorGUILayout.HorizontalScope())
            {
                // 스킬 정보 표시
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(300)))
                {
                    if (activeSkillProp.intValue > 0)
                    {
                        var skillData = GetSkillData(activeSkillProp.intValue);
                        if (skillData != null)
                        {
                            EditorGUILayout.LabelField($"[{skillData.skillId}] {skillData.skillName}", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"카테고리: {skillData.category}");
                            if (skillData.cooldown > 0)
                                EditorGUILayout.LabelField($"쿨다운: {skillData.cooldown}턴");
                            EditorGUILayout.LabelField(skillData.description, EditorStyles.wordWrappedMiniLabel, GUILayout.MaxHeight(40));
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"ID: {activeSkillProp.intValue} (데이터 없음)", EditorStyles.miniLabel);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(선택되지 않음)", EditorStyles.miniLabel);
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("선택", GUILayout.Width(60)))
                {
                    SkillSelectionWindow.ShowWindow(SkillSelectionWindow.SkillSelectMode.Active, (skillId) =>
                    {
                        serializedObject.Update();
                        activeSkillProp.intValue = skillId;
                        serializedObject.ApplyModifiedProperties();
                        isDirty = true;
                    });
                }

                GUI.enabled = activeSkillProp.intValue > 0;
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    activeSkillProp.intValue = 0;
                    serializedObject.ApplyModifiedProperties();
                    isDirty = true;
                }
                GUI.enabled = true;
            }

            EditorGUILayout.Space(5);

            // 패시브 스킬
            EditorGUILayout.LabelField("패시브 스킬", EditorStyles.boldLabel);
            var passiveSkillProp = serializedObject.FindProperty("passiveSkillId");

            using (new EditorGUILayout.HorizontalScope())
            {
                // 스킬 정보 표시
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(300)))
                {
                    if (passiveSkillProp.intValue > 0)
                    {
                        var skillData = GetSkillData(passiveSkillProp.intValue);
                        if (skillData != null)
                        {
                            EditorGUILayout.LabelField($"[{skillData.skillId}] {skillData.skillName}", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"카테고리: {skillData.category}");
                            EditorGUILayout.LabelField(skillData.description, EditorStyles.wordWrappedMiniLabel, GUILayout.MaxHeight(40));
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"ID: {passiveSkillProp.intValue} (데이터 없음)", EditorStyles.miniLabel);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(선택되지 않음)", EditorStyles.miniLabel);
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("선택", GUILayout.Width(60)))
                {
                    SkillSelectionWindow.ShowWindow(SkillSelectionWindow.SkillSelectMode.Passive, (skillId) =>
                    {
                        serializedObject.Update();
                        passiveSkillProp.intValue = skillId;
                        serializedObject.ApplyModifiedProperties();
                        isDirty = true;
                    });
                }

                GUI.enabled = passiveSkillProp.intValue > 0;
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    passiveSkillProp.intValue = 0;
                    serializedObject.ApplyModifiedProperties();
                    isDirty = true;
                }
                GUI.enabled = true;
            }


            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(10);

        // 리소스 정보
        EditorGUILayout.LabelField("Resources", headerStyle);
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            var serializedObject = new SerializedObject(selectedCharacter);

            // 썸네일 미리보기
            var thumbnail = GetCharacterThumbnail(selectedCharacter);
            if (thumbnail != null)
            {
                EditorGUILayout.LabelField("Thumbnail Preview:");

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(thumbnail, GUILayout.Width(64), GUILayout.Height(64));
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.HelpBox($"Key: Char_{selectedCharacter.CharacterName}_Idle_png", MessageType.Info);
                EditorGUILayout.Space(5);
            }
            else
            {
                EditorGUILayout.HelpBox($"Thumbnail not found: Char_{selectedCharacter.CharacterName}_Idle_png", MessageType.Warning);
            }

            var prefabPathProp = serializedObject.FindProperty("prefabPath");
            var resourceNameProp = serializedObject.FindProperty("characterResourceName");
            var addressableKeyProp = serializedObject.FindProperty("addressableKey");


            EditorGUILayout.PropertyField(prefabPathProp, new GUIContent("Prefab Path"));
            EditorGUILayout.PropertyField(resourceNameProp, new GUIContent("Resource Name"));
            EditorGUILayout.PropertyField(addressableKeyProp, new GUIContent("Addressable Key"));


            if (GUILayout.Button("Browse Prefab"))
            {
                // TODO: 프리팹 브라우저 열기
                Debug.Log("Opening prefab browser...");
            }

            serializedObject.ApplyModifiedProperties();
        }

        DrawTimelineSection();

        if (EditorGUI.EndChangeCheck())
        {
            isDirty = true;
        }
    }

    private void ClearThumbnailCache()
    {
        thumbnailCache.Clear();
    }

    /// <summary>
    /// 특정 캐릭터의 썸네일 캐시 갱신
    /// </summary>
    private void RefreshThumbnail(BattleCharacterDataSO character)
    {
        if (character != null && thumbnailCache.ContainsKey(character.CharacterId))
        {
            thumbnailCache.Remove(character.CharacterId);
            GetCharacterThumbnail(character); // 재로드
        }
    }

    private bool showStatsPreview = false;
    private int previewLevel = 1;

    // 레벨별 스탯 미리보기
    private void ShowStatsAtLevel(int level)
    {
        showStatsPreview = true;
        previewLevel = level;
        Repaint();
    }

    // 스탯 재계산
    private void RecalculateStats()
    {
        if (selectedCharacter == null) return;

        // Reflection을 사용해 private 메서드 호출
        var method = selectedCharacter.GetType().GetMethod("OnValidate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(selectedCharacter, null);
            EditorUtility.SetDirty(selectedCharacter);
            isDirty = true;
            Repaint();
        }
    }


    private SkillSystem.AdvancedSkillData GetSkillData(int skillId)
    {
        // 스킬 데이터베이스에서 검색
        var database = SkillSystem.AdvancedSkillDatabase.Load();
        if (database != null)
        {
            return database.GetSkillById(skillId);
        }

        return null;
    }
    private void DrawNewCharacterPanel()
    {
        EditorGUILayout.LabelField("Create New Character", headerStyle);

        // 템플릿 선택 UI
        EditorGUILayout.HelpBox("Select a template to create a new character", MessageType.Info);

        // TODO: 템플릿 선택 구현
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // 썸네일 캐시 정리 버튼 추가
            if (GUILayout.Button("Refresh Thumbnails", GUILayout.Width(120)))
            {
                ClearThumbnailCache();
                Repaint();
            }


            if (GUILayout.Button("Import CSV", GUILayout.Width(100)))
            {
                ImportFromCSV();
            }

            if (GUILayout.Button("Export CSV", GUILayout.Width(100)))
            {
                ExportToCSV();
            }

            if (GUILayout.Button("Battle Simulation", GUILayout.Width(200)))
            {
                OpenBattleSimulationWindow();
            }

            if (isDirty)
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Save Changes", GUILayout.Width(100)))
                {
                    SaveChanges();
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }

    // 데이터 관리 메서드
    private void LoadAllCharacterData()
    {
        characterDataList.Clear();

        string[] guids = AssetDatabase.FindAssets("t:BattleCharacterDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleCharacterDataSO data = AssetDatabase.LoadAssetAtPath<BattleCharacterDataSO>(path);
            if (data != null)
            {
                characterDataList.Add(data);
            }
        }

        characterDataList = characterDataList.OrderBy(c => c.CharacterId).ToList();

        // 추가: ID 목록 업데이트
        CollectUsedIds();

    }

    private List<BattleCharacterDataSO> GetFilteredCharacterList()
    {
        var filtered = characterDataList.AsEnumerable();

        if (!string.IsNullOrEmpty(searchString))
        {
            filtered = filtered.Where(c =>
                c.CharacterName.ToLower().Contains(searchString.ToLower()) ||
                c.CharacterId.ToString().Contains(searchString));
        }

        if (showFilters)
        {
            filtered = filtered.Where(c => c.Tier == filterTier && c.CharacterClass == filterClass);
        }

        return filtered.ToList();
    }

    private void SelectCharacter(BattleCharacterDataSO character, int index)
    {
        selectedCharacter = character;
        selectedCharacterIndex = index;
        isCreatingNew = false;
    }

    private void CreateNewCharacter()
    {
        isCreatingNew = true;
        selectedCharacter = null;

        // 새 캐릭터 생성
        var newCharacter = ScriptableObject.CreateInstance<BattleCharacterDataSO>();

        // 다음 ID 자동 할당
        int nextId = GetNextAvailableId();

        // 기본값 설정
        newCharacter.InitializeFromTemplate(CharacterTier.A, ClassType.Slaughter);
        newCharacter.SetCharacterId(nextId);

        // 기본 이름 설정
        string defaultName = $"NewCharacter{nextId}";
        var nameField = newCharacter.GetType().GetField("characterName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nameField?.SetValue(newCharacter, defaultName);

        // 파일명: CharacterName_ID 형식으로 수정
        newCharacter.name = $"{defaultName}_{nextId}";

        // 저장 경로 수정
        string path = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Characters/NewCharacter_" + nextId + ".asset";

        // 디렉토리 생성 확인
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(newCharacter, path);
        AssetDatabase.SaveAssets();

        // 리스트에 추가
        characterDataList.Add(newCharacter);
        selectedCharacter = newCharacter;

        // 추가: 사용된 ID 목록 업데이트
        usedCharacterIds.Add(nextId);


        isCreatingNew = false;
    }

    private void DuplicateCharacter(BattleCharacterDataSO original)
    {
        if (original == null) return;

        var copy = Instantiate(original);

        // 새 ID 할당
        int newId = GetNextAvailableId();
        copy.SetCharacterId(newId);

        // 이름에 _Copy 추가
        string copyName = original.CharacterName + "_Copy";
        var nameField = copy.GetType().GetField("characterName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nameField?.SetValue(copy, copyName);

        // 파일명: CharacterName_ID 형식
        copy.name = $"{copyName}_{newId}";

        string path = AssetDatabase.GetAssetPath(original);
        string directory = System.IO.Path.GetDirectoryName(path);
        string newPath = $"{directory}/{copyName}_{newId}.asset";

        AssetDatabase.CreateAsset(copy, newPath);
        AssetDatabase.SaveAssets();

        LoadAllCharacterData();
        selectedCharacter = copy;
    }

    private void DeleteCharacter(BattleCharacterDataSO character)
    {
        if (character == null) return;

        if (EditorUtility.DisplayDialog("Delete Character",
            $"Are you sure you want to delete {character.CharacterName}?",
            "Delete", "Cancel"))
        {
            // 추가: 삭제되는 ID를 사용 가능 목록에서 제거
            usedCharacterIds.Remove(character.CharacterId);

            string path = AssetDatabase.GetAssetPath(character);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            LoadAllCharacterData();
            selectedCharacter = null;
        }
    }

    private void SaveChanges()
    {
        if (selectedCharacter != null)
        {
            // 현재 파일 경로
            string currentPath = AssetDatabase.GetAssetPath(selectedCharacter);

            // 새 파일명 생성
            string newFileName = $"{selectedCharacter.CharacterName}_{selectedCharacter.CharacterId}";
            newFileName = SanitizeFileName(newFileName);

            // 파일명이 변경되었는지 확인
            string currentFileName = System.IO.Path.GetFileNameWithoutExtension(currentPath);
            if (currentFileName != newFileName)
            {
                // 파일명 변경
                string directory = System.IO.Path.GetDirectoryName(currentPath);
                string error = AssetDatabase.RenameAsset(currentPath, newFileName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Failed to rename asset: {error}");
                }
                else
                {
                    Debug.Log($"Renamed: {currentFileName} -> {newFileName}");
                    selectedCharacter.name = newFileName;
                }
            }

            EditorUtility.SetDirty(selectedCharacter);
            AssetDatabase.SaveAssets();
        }
        isDirty = false;
    }

    // 파일명 정리 헬퍼 메서드 추가
    private string SanitizeFileName(string fileName)
    {
        // 파일명에 사용할 수 없는 문자 제거
        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        string safe = fileName;

        foreach (char c in invalidChars)
        {
            safe = safe.Replace(c.ToString(), "");
        }

        // 공백을 언더스코어로 변경
        safe = safe.Replace(" ", "_");

        return safe;
    }

    private void ImportFromCSV()
    {
        // TODO: CSV 임포트 구현
        Debug.Log("Import CSV - To be implemented");
    }

    private void ExportToCSV()
    {
        // TODO: CSV 익스포트 구현
        Debug.Log("Export CSV - To be implemented");
    }

    private void GenerateCSVTemplate()
    {
        string path = EditorUtility.SaveFilePanel(
            "Save CSV Template",
            Application.dataPath,
            "CharacterTemplate",
            "csv"
        );

        if (string.IsNullOrEmpty(path)) return;

        // 샘플 데이터 생성
        var sampleCharacters = new List<BattleCharacterDataSO>();

        // 각 티어와 클래스별로 샘플 생성
        var tiers = new[] { CharacterTier.A, CharacterTier.S };
        var classes = new[] { ClassType.Slaughter, ClassType.Vanguard };
        int id = 1000;

        foreach (var tier in tiers)
        {
            foreach (var charClass in classes)
            {
                var sample = ScriptableObject.CreateInstance<BattleCharacterDataSO>();
                sample.InitializeFromTemplate(tier, charClass);

                // 리플렉션으로 private 필드 설정
                SetPrivateField(sample, "characterId", id++);
                SetPrivateField(sample, "characterName", $"Sample_{tier}_{charClass}");
                SetPrivateField(sample, "description", $"Sample character for {tier} tier {charClass} class");

                sampleCharacters.Add(sample);
            }
        }

        // CSV로 내보내기
        CSVImportExportHandler.ExportCharactersToCSV(sampleCharacters);

        // 임시 객체 정리
        foreach (var sample in sampleCharacters)
        {
            DestroyImmediate(sample);
        }

        EditorUtility.DisplayDialog("Template Generated",
            "CSV template has been generated with sample data.", "OK");
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }

    private void OpenBattleSimulationWindow()
    {
        /*if (currentTab == EditorTab.Character && selectedCharacter != null)
        {
            BattlePreviewWindow.ShowWindow(selectedCharacter, null, null);
        }
        else if (currentTab == EditorTab.Monster && monsterTab != null)
        {
            // MonsterTab에서 선택된 몬스터 가져오기 (추후 구현)
            BattlePreviewWindow.ShowWindow(null, null, null);
        }
        else if (currentTab == EditorTab.MonsterGroup && monsterGroupTab != null)
        {
            // MonsterGroupTab에서 선택된 그룹 가져오기 (추후 구현)
            BattlePreviewWindow.ShowWindow(null, null, null);
        }
        else
        {
            BattlePreviewWindow.ShowWindow(null, null, null);
        }*/

        if (selectedCharacter == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a character first.", "OK");
            return;
        }

        BattlePreviewWindow.ShowWindow(selectedCharacter, null, null);
    }
}