using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using BattleCharacterSystem;

public class MonsterEditorTab
{
    // 데이터 리스트
    private List<BattleMonsterDataSO> monsterDataList = new List<BattleMonsterDataSO>();
    private BattleMonsterDataSO selectedMonster;

    // 검색/필터
    private string searchString = "";
    private BattleCharacterSystem.MonsterType filterType = BattleCharacterSystem.MonsterType.Normal;
    private bool showFilters = false;
    private bool showBossOnly = false;

    // UI 상태
    private Vector2 listScrollPos;
    private Vector2 detailScrollPos;
    private bool isCreatingNew = false;
    private bool isDirty = false;

    // 보스 페이즈 편집
    private int selectedPhaseIndex = -1;
    private bool phasesFoldout = true;
    private bool guardianStoneFoldout = true;

    // 스타일
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;

    public void Initialize()
    {
        LoadAllMonsterData();
        InitializeStyles();
    }

    private void InitializeStyles()
    {
        headerStyle = new GUIStyle();
        headerStyle.fontSize = 14;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.white;
        headerStyle.padding = new RectOffset(5, 5, 5, 5);

        boxStyle = new GUIStyle();
        boxStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.3f));
        boxStyle.padding = new RectOffset(5, 5, 5, 5);
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

    public void DrawMonsterTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            // 왼쪽 패널 (리스트)
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                DrawSearchBar();
                DrawMonsterList();
            }

            // 구분선
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // 오른쪽 패널 (디테일)
            using (new EditorGUILayout.VerticalScope())
            {
                DrawMonsterDetails();
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

        // 필터
        showFilters = EditorGUILayout.Foldout(showFilters, "Filters");
        if (showFilters)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                filterType = (BattleCharacterSystem.MonsterType)EditorGUILayout.EnumPopup("Type", filterType);
                showBossOnly = EditorGUILayout.Toggle("Boss Only", showBossOnly);
            }
        }

        EditorGUILayout.Space(5);
    }

    private void DrawMonsterList()
    {
        // 새로 만들기 버튼
        if (GUILayout.Button("+ Create New Monster", GUILayout.Height(30)))
        {
            CreateNewMonster();
        }

        EditorGUILayout.Space(5);

        // 몬스터 리스트
        using (var scrollView = new EditorGUILayout.ScrollViewScope(listScrollPos))
        {
            listScrollPos = scrollView.scrollPosition;

            var filteredList = GetFilteredMonsterList();

            // 타입별로 그룹화
            var groupedMonsters = filteredList.GroupBy(m => m.MonsterType).OrderBy(g => g.Key);

            foreach (var group in groupedMonsters)
            {
                EditorGUILayout.LabelField($"{GetMonsterIcon(group.Key)} {group.Key}", EditorStyles.boldLabel);

                foreach (var monster in group.OrderBy(m => m.MonsterId))
                {
                    if (monster == null) continue;

                    bool isSelected = (monster == selectedMonster);

                    using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
                    {
                        // 몬스터 정보 표시
                        string displayName = $"[{monster.MonsterId}] {monster.MonsterName}";
                        string info = monster.IsBoss ? "BOSS" : monster.BehaviorPattern.ToString();

                        using (new EditorGUILayout.VerticalScope())
                        {
                            if (GUILayout.Button(displayName, EditorStyles.label))
                            {
                                SelectMonster(monster);
                            }

                            // 부가 정보
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label(info, EditorStyles.miniLabel);

                                // 수호석 표시
                                if (monster.HasGuardianStone)
                                {
                                    GUILayout.Label($"💎{monster.GuardianStoneElements.Length}",
                                        EditorStyles.miniLabel);
                                }
                            }
                        }

                        GUILayout.FlexibleSpace();

                        // 복사 버튼
                        if (GUILayout.Button("📋", GUILayout.Width(25)))
                        {
                            DuplicateMonster(monster);
                        }

                        // 삭제 버튼
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            DeleteMonster(monster);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                }

                EditorGUILayout.Space(3);
            }
        }
    }

    private void DrawMonsterDetails()
    {
        if (selectedMonster == null)
        {
            EditorGUILayout.HelpBox("Select a monster from the list or create a new one.", MessageType.Info);
            return;
        }

        using (var scrollView = new EditorGUILayout.ScrollViewScope(detailScrollPos))
        {
            detailScrollPos = scrollView.scrollPosition;

            EditorGUI.BeginChangeCheck();

            // 기본 정보
            DrawBasicInfo();
            EditorGUILayout.Space(10);

            // 분류
            DrawClassification();
            EditorGUILayout.Space(10);

            // 스탯
            DrawStats();
            EditorGUILayout.Space(10);

            // 면역
            DrawImmunity();
            EditorGUILayout.Space(10);

            // 수호석
            DrawGuardianStones();
            EditorGUILayout.Space(10);

            // 스킬
            DrawSkills();
            EditorGUILayout.Space(10);

            // 보스 페이즈 (보스만)
            if (selectedMonster.IsBoss)
            {
                DrawBossPhases();
                EditorGUILayout.Space(10);
            }

            // 리소스
            DrawResources();

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
            }

            // 저장 버튼
            EditorGUILayout.Space(20);
            DrawSaveButton();
        }
    }

    private void DrawBasicInfo()
    {
        EditorGUILayout.LabelField("Basic Info", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedMonster);

            // ID는 읽기 전용
            GUI.enabled = false;
            var idProp = serializedObject.FindProperty("monsterId");
            EditorGUILayout.PropertyField(idProp, new GUIContent("Monster ID"));
            GUI.enabled = true;

            // 이름 변경 감지
            var nameProp = serializedObject.FindProperty("monsterName");
            string oldName = nameProp.stringValue;
            EditorGUILayout.PropertyField(nameProp, new GUIContent("Monster Name"));

            var descProp = serializedObject.FindProperty("description");
            EditorGUILayout.PropertyField(descProp, new GUIContent("Description"));

            serializedObject.ApplyModifiedProperties();

            // 이름이 변경되었으면 isDirty 설정
            if (oldName != nameProp.stringValue)
            {
                isDirty = true;
            }
        }
    }

    private void DrawClassification()
    {
        EditorGUILayout.LabelField("Classification", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedMonster);

            var typeProp = serializedObject.FindProperty("monsterType");
            var patternProp = serializedObject.FindProperty("behaviorPattern");
            var elementProp = serializedObject.FindProperty("elementType");
            var attackTypeProp = serializedObject.FindProperty("attackType");

            EditorGUILayout.PropertyField(typeProp, new GUIContent("Monster Type"));
            EditorGUILayout.PropertyField(patternProp, new GUIContent("Behavior Pattern"));
            EditorGUILayout.PropertyField(elementProp, new GUIContent("Element"));
            EditorGUILayout.PropertyField(attackTypeProp, new GUIContent("Attack Type"));

            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawStats()
    {
        EditorGUILayout.LabelField("Stats & Multipliers", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedMonster);

            // 배율
            var multProp = serializedObject.FindProperty("statMultiplier");
            var isBossProp = serializedObject.FindProperty("isBoss");
            var bossMultProp = serializedObject.FindProperty("bossMultiplier");

            EditorGUILayout.PropertyField(multProp, new GUIContent("Stat Multiplier"));
            EditorGUILayout.PropertyField(isBossProp, new GUIContent("Is Boss"));

            if (isBossProp.boolValue)
            {
                EditorGUILayout.PropertyField(bossMultProp, new GUIContent("Boss Multiplier"));
            }

            EditorGUILayout.Space(5);

            // 스탯
            var baseStatsProp = serializedObject.FindProperty("baseStats");
            var fixedStatsProp = serializedObject.FindProperty("fixedStats");

            EditorGUILayout.PropertyField(baseStatsProp, new GUIContent("Base Stats"), true);
            EditorGUILayout.PropertyField(fixedStatsProp, new GUIContent("Fixed Stats"), true);

            serializedObject.ApplyModifiedProperties();

            // 최종 스탯 프리뷰
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Final Stats Preview (Level 1)", EditorStyles.miniBoldLabel);
            var finalStats = selectedMonster.GetFinalStats(1);
            EditorGUILayout.LabelField($"HP: {finalStats.hp}, ATK: {finalStats.attack}, DEF: {finalStats.defense}");
        }
    }

    private void DrawImmunity()
    {
        EditorGUILayout.LabelField("Immunity", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedMonster);
            var immunityProp = serializedObject.FindProperty("immunityType");

            EditorGUILayout.PropertyField(immunityProp, new GUIContent("Immunity Type"));

            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawGuardianStones()
    {
        guardianStoneFoldout = EditorGUILayout.Foldout(guardianStoneFoldout, "Guardian Stones", true);

        if (guardianStoneFoldout)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var serializedObject = new SerializedObject(selectedMonster);

                var hasStonesProp = serializedObject.FindProperty("hasGuardianStone");
                EditorGUILayout.PropertyField(hasStonesProp, new GUIContent("Has Guardian Stones"));

                if (hasStonesProp.boolValue)
                {
                    var elementsProp = serializedObject.FindProperty("guardianStoneElements");
                    EditorGUILayout.PropertyField(elementsProp, new GUIContent("Stone Elements"), true);
                }

                serializedObject.ApplyModifiedProperties();

                // 수호석 빠른 설정
                EditorGUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("No Stones"))
                    {
                        selectedMonster.SetGuardianStones(0);
                        EditorUtility.SetDirty(selectedMonster);
                    }
                    if (GUILayout.Button("2 Stones"))
                    {
                        selectedMonster.SetGuardianStones(2);
                        EditorUtility.SetDirty(selectedMonster);
                    }
                    if (GUILayout.Button("3 Stones"))
                    {
                        selectedMonster.SetGuardianStones(3);
                        EditorUtility.SetDirty(selectedMonster);
                    }
                    if (GUILayout.Button("4 Stones"))
                    {
                        selectedMonster.SetGuardianStones(4);
                        EditorUtility.SetDirty(selectedMonster);
                    }
                }
            }
        }
    }

    private void DrawSkills()
    {
        EditorGUILayout.LabelField("Skills", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedMonster);
            var skillsProp = serializedObject.FindProperty("skillIds");

            EditorGUILayout.PropertyField(skillsProp, new GUIContent("Skill IDs"), true);

            serializedObject.ApplyModifiedProperties();

            // 스킬 추가 버튼
            if (GUILayout.Button("+ Add Skill"))
            {
                selectedMonster.SkillIds.Add(0);
                EditorUtility.SetDirty(selectedMonster);
            }
        }
    }

    private void DrawBossPhases()
    {
        phasesFoldout = EditorGUILayout.Foldout(phasesFoldout, "Boss Phases", true);

        if (phasesFoldout)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var serializedObject = new SerializedObject(selectedMonster);
                var phasesProp = serializedObject.FindProperty("bossPhases");

                // 페이즈 리스트
                for (int i = 0; i < phasesProp.arraySize; i++)
                {
                    var phaseProp = phasesProp.GetArrayElementAtIndex(i);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var nameProp = phaseProp.FindPropertyRelative("phaseName");
                        var hpProp = phaseProp.FindPropertyRelative("hpTriggerPercent");

                        EditorGUILayout.LabelField($"{i + 1}. {nameProp.stringValue} (HP {hpProp.floatValue}%)",
                            GUILayout.Width(200));

                        if (GUILayout.Button("Edit", GUILayout.Width(50)))
                        {
                            selectedPhaseIndex = (selectedPhaseIndex == i) ? -1 : i;
                        }

                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            phasesProp.DeleteArrayElementAtIndex(i);
                        }
                        GUI.backgroundColor = Color.white;
                    }

                    // 선택된 페이즈 편집
                    if (selectedPhaseIndex == i)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(phaseProp, new GUIContent($"Phase {i + 1}"), true);
                        EditorGUI.indentLevel--;
                    }
                }

                // 페이즈 추가 버튼
                if (GUILayout.Button("+ Add Phase"))
                {
                    phasesProp.InsertArrayElementAtIndex(phasesProp.arraySize);
                }

                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    private void DrawResources()
    {
        EditorGUILayout.LabelField("Resources", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedMonster);

            // 리소스 모드 선택 추가
            var useExistingProp = serializedObject.FindProperty("useExistingCharacter");
            EditorGUILayout.PropertyField(useExistingProp, new GUIContent("Use Existing Character"));

            if (useExistingProp.boolValue)
            {
                // 기존 캐릭터 모드
                EditorGUILayout.Space(5);

                var charIdProp = serializedObject.FindProperty("baseCharacterId");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(charIdProp, new GUIContent("Character ID"));

                    // 캐릭터 선택 버튼
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        ShowCharacterSelectionWindow((int selectedId) =>
                        {
                            charIdProp.intValue = selectedId;
                            serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(selectedMonster);
                        });
                    }
                }

                // 선택된 캐릭터 정보 표시
                if (charIdProp.intValue > 0)
                {
                    EditorGUILayout.HelpBox($"Using: {BattleMonsterDataSO.GetCharacterNameById(charIdProp.intValue)} (ID: {charIdProp.intValue})", MessageType.Info);

                    // 프리뷰 정보
                    GUI.enabled = false;
                    EditorGUILayout.TextField("Prefab Path", selectedMonster.GetActualPrefabPath());
                    EditorGUILayout.TextField("Resource Name", selectedMonster.GetActualResourceName());
                    EditorGUILayout.TextField("Addressable Key", selectedMonster.GetActualAddressableKey());
                    GUI.enabled = true;
                }
            }
            else
            {
                // 신규 몬스터 모드 (기존 코드 유지)
                EditorGUILayout.Space(5);

                var prefabPathProp = serializedObject.FindProperty("prefabPath");
                var resourceNameProp = serializedObject.FindProperty("monsterResourceName");
                var addressableKeyProp = serializedObject.FindProperty("addressableKey");

                EditorGUILayout.PropertyField(prefabPathProp, new GUIContent("Prefab Path"));
                EditorGUILayout.PropertyField(resourceNameProp, new GUIContent("Resource Name"));
                EditorGUILayout.PropertyField(addressableKeyProp, new GUIContent("Addressable Key"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }


    // 캐릭터 선택 창
    private void ShowCharacterSelectionWindow(System.Action<int> onSelected)
    {
        // 간단한 선택 창
        GenericMenu menu = new GenericMenu();

        // 캐릭터 목록 로드
        string[] guids = AssetDatabase.FindAssets("t:BattleCharacterDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var charData = AssetDatabase.LoadAssetAtPath<BattleCharacterDataSO>(path);
            if (charData != null)
            {
                int id = charData.CharacterId;
                string name = charData.CharacterName;
                menu.AddItem(new GUIContent($"{name} (ID: {id})"), false, () => onSelected(id));
            }
        }

        menu.ShowAsContext();
    }


    private void DrawSaveButton()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (isDirty)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                if (GUILayout.Button("💾 Save Changes", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    SaveChanges();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("✓ Saved", GUILayout.Height(30), GUILayout.Width(150));
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();
        }
    }

    // 데이터 관리 메서드
    private void LoadAllMonsterData()
    {
        monsterDataList.Clear();

        string[] guids = AssetDatabase.FindAssets("t:BattleMonsterDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleMonsterDataSO data = AssetDatabase.LoadAssetAtPath<BattleMonsterDataSO>(path);
            if (data != null)
            {
                monsterDataList.Add(data);
            }
        }

        monsterDataList = monsterDataList.OrderBy(m => m.MonsterId).ToList();
    }

    private List<BattleMonsterDataSO> GetFilteredMonsterList()
    {
        var filtered = monsterDataList.AsEnumerable();

        if (!string.IsNullOrEmpty(searchString))
        {
            filtered = filtered.Where(m =>
                m.MonsterName.ToLower().Contains(searchString.ToLower()) ||
                m.MonsterId.ToString().Contains(searchString));
        }

        if (showFilters)
        {
            if (showBossOnly)
            {
                filtered = filtered.Where(m => m.IsBoss);
            }
            else
            {
                filtered = filtered.Where(m => m.MonsterType == filterType);
            }
        }

        return filtered.ToList();
    }

    private void SelectMonster(BattleMonsterDataSO monster)
    {
        selectedMonster = monster;
        isCreatingNew = false;
        selectedPhaseIndex = -1;
    }

    private void CreateNewMonster()
    {
        // 타입 선택 다이얼로그
        int choice = EditorUtility.DisplayDialogComplex("Create New Monster",
            "Select monster type:",
            "Normal/Elite",
            "Boss",
            "Cancel");

        if (choice == 2) return; // Cancel

        var newMonster = ScriptableObject.CreateInstance<BattleMonsterDataSO>();

        // 다음 ID 자동 할당
        int nextId = (choice == 1) ? 3000 : 2000; // Boss는 3000번대
        var sameTypeMonsters = monsterDataList.Where(m =>
            (choice == 1 && m.MonsterId >= 3000) ||
            (choice == 0 && m.MonsterId >= 2000 && m.MonsterId < 3000));

        if (sameTypeMonsters.Any())
        {
            nextId = sameTypeMonsters.Max(m => m.MonsterId) + 1;
        }

        // 초기화
        var type = (choice == 1) ? BattleCharacterSystem.MonsterType.Boss : BattleCharacterSystem.MonsterType.Normal;
        newMonster.InitializeFromType(type);

        // 기본 이름 설정
        string defaultName = $"Monster{type}_{nextId}";
        var nameField = newMonster.GetType().GetField("monsterName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nameField?.SetValue(newMonster, defaultName);

        // 파일명: MonsterName_ID 형식으로 수정
        newMonster.name = $"{defaultName}_{nextId}";




        // 리플렉션으로 ID 설정
        var idField = newMonster.GetType().GetField("monsterId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(newMonster, nextId);

        // 저장
        string directory = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Monsters";
        if (!AssetDatabase.IsValidFolder(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        string path = $"{directory}/{defaultName}_{nextId}.asset";
        AssetDatabase.CreateAsset(newMonster, path);
        AssetDatabase.SaveAssets();

        LoadAllMonsterData();
        SelectMonster(newMonster);
    }

    private void DuplicateMonster(BattleMonsterDataSO original)
    {
        if (original == null) return;

        var copy = Object.Instantiate(original);

        // 새 ID 할당
        int nextId = original.MonsterId + 1;
        while (monsterDataList.Any(m => m.MonsterId == nextId))
        {
            nextId++;
        }

        // ID 설정
        var idField = copy.GetType().GetField("monsterId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(copy, nextId);

        // 이름에 _Copy 추가
        string copyName = original.MonsterName + "_Copy";
        var nameField = copy.GetType().GetField("monsterName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nameField?.SetValue(copy, copyName);

        // 파일명: MonsterName_ID 형식
        copy.name = $"{copyName}_{nextId}";

        string path = AssetDatabase.GetAssetPath(original);
        string directory = System.IO.Path.GetDirectoryName(path);
        string newPath = $"{directory}/{copyName}_{nextId}.asset";

        AssetDatabase.CreateAsset(copy, newPath);
        AssetDatabase.SaveAssets();

        LoadAllMonsterData();
        SelectMonster(copy);
    }

    private void DeleteMonster(BattleMonsterDataSO monster)
    {
        if (monster == null) return;

        if (EditorUtility.DisplayDialog("Delete Monster",
            $"Are you sure you want to delete {monster.MonsterName}?",
            "Delete", "Cancel"))
        {
            string path = AssetDatabase.GetAssetPath(monster);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            LoadAllMonsterData();
            selectedMonster = null;
        }
    }

    private void SaveChanges()
    {
        if (selectedMonster != null)
        {
            // 현재 파일 경로
            string currentPath = AssetDatabase.GetAssetPath(selectedMonster);

            // 새 파일명 생성
            string newFileName = $"{selectedMonster.MonsterName}_{selectedMonster.MonsterId}";
            newFileName = SanitizeFileName(newFileName);

            // 파일명이 변경되었는지 확인
            string currentFileName = System.IO.Path.GetFileNameWithoutExtension(currentPath);
            if (currentFileName != newFileName)
            {
                // 파일명 변경
                string error = AssetDatabase.RenameAsset(currentPath, newFileName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Failed to rename monster asset: {error}");
                }
                else
                {
                    Debug.Log($"Renamed monster: {currentFileName} -> {newFileName}");
                    selectedMonster.name = newFileName;
                }
            }

            EditorUtility.SetDirty(selectedMonster);
            AssetDatabase.SaveAssets();
            isDirty = false;


            EditorUtility.DisplayDialog("Saved",
                $"Monster saved.\nID: {selectedMonster.MonsterId}\nName: {selectedMonster.MonsterName}\nResources RootName: {selectedMonster.GetActualResourceRootName()}",
                "OK");
        }
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


    private string GetMonsterIcon(BattleCharacterSystem.MonsterType type)
    {
        switch (type)
        {
            case BattleCharacterSystem.MonsterType.Normal: return "👾";
            case BattleCharacterSystem.MonsterType.Elite: return "👹";
            case BattleCharacterSystem.MonsterType.MiniBoss: return "🦾";
            case BattleCharacterSystem.MonsterType.Boss: return "👺";
            case BattleCharacterSystem.MonsterType.Special: return "✨";
            default: return "❓";
        }
    }
}