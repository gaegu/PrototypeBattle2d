using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using BattleCharacterSystem;

public class MonsterGroupEditorTab
{
    // 데이터 리스트
    private List<MonsterGroupDataSO> groupDataList = new List<MonsterGroupDataSO>();
    private MonsterGroupDataSO selectedGroup;

    // 몬스터 선택용
    private List<BattleMonsterDataSO> availableMonsters = new List<BattleMonsterDataSO>();

    // 검색/필터
    private string searchString = "";
    private string filterPurpose = "All";
    private bool showFilters = false;

    // UI 상태
    private Vector2 listScrollPos;
    private Vector2 detailScrollPos;
    private Vector2 monsterPickerScrollPos;
    private bool isDirty = false;
    private bool showMonsterPicker = false;
    private int monsterPickerSlot = -1;

    // 슬롯 시각화
    private bool showFormationPreview = true;

    // 스타일
    private GUIStyle headerStyle;
    private GUIStyle slotStyle;
    private GUIStyle emptySlotStyle;
    private GUIStyle filledSlotStyle;

    // UI 상태 아래에 추가
    private string monsterSearchString = "";  // 몬스터 검색
    private BattleCharacterSystem.MonsterType filterMonsterType = BattleCharacterSystem.MonsterType.Normal;  // 몬스터 타입 필터
    private bool showAllTypes = true;  // 모든 타입 표시 여부
                                       // ID 관리를 위한 추가 필드

    private static int lastAssignedId = 4999;  // 마지막 할당된 ID 저장

    public void Initialize()
    {
        LoadAllGroupData();

        if (groupDataList != null && groupDataList.Count > 0)
        {
            lastAssignedId = groupDataList.Max(g => g.GroupId);
        }

        LoadAvailableMonsters();
        InitializeStyles();
    }

    private void InitializeStyles()
    {
        headerStyle = new GUIStyle();
        headerStyle.fontSize = 14;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.white;
        headerStyle.padding = new RectOffset(5, 5, 5, 5);

        slotStyle = new GUIStyle();
        slotStyle.alignment = TextAnchor.MiddleCenter;
        slotStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        slotStyle.normal.textColor = Color.white;

        emptySlotStyle = new GUIStyle(slotStyle);
        emptySlotStyle.normal.background = MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.3f));

        filledSlotStyle = new GUIStyle(slotStyle);
        filledSlotStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.5f, 0.3f, 0.5f));
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

    public void DrawMonsterGroupTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            // 왼쪽 패널 (리스트)
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                DrawSearchBar();
                DrawGroupList();
            }

            // 구분선
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // 오른쪽 패널 (디테일)
            using (new EditorGUILayout.VerticalScope())
            {
                DrawGroupDetails();
            }
        }

        // 몬스터 선택 팝업
        if (showMonsterPicker)
        {
            DrawMonsterPicker();
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

        showFilters = EditorGUILayout.Foldout(showFilters, "Filters");
        if (showFilters)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string[] purposes = new string[] { "All", "Tutorial", "Normal", "Boss", "Special", "Event" };
                int selectedIndex = System.Array.IndexOf(purposes, filterPurpose);
                selectedIndex = EditorGUILayout.Popup("Purpose", selectedIndex, purposes);
                filterPurpose = purposes[selectedIndex];
            }
        }

        EditorGUILayout.Space(5);
    }

    private void DrawGroupList()
    {
        // 새로 만들기 버튼
        if (GUILayout.Button("+ Create New Group", GUILayout.Height(30)))
        {
            CreateNewGroup();
        }

        // 템플릿에서 생성
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tutorial", GUILayout.Height(25)))
                CreateFromTemplate("Tutorial");
            if (GUILayout.Button("Balanced", GUILayout.Height(25)))
                CreateFromTemplate("Balanced");
            if (GUILayout.Button("Boss", GUILayout.Height(25)))
                CreateFromTemplate("Boss");
        }

        EditorGUILayout.Space(5);

        // 그룹 리스트
        using (var scrollView = new EditorGUILayout.ScrollViewScope(listScrollPos))
        {
            listScrollPos = scrollView.scrollPosition;

            var filteredList = GetFilteredGroupList();

            // Purpose별로 그룹화
            var groupedByPurpose = filteredList.GroupBy(g => g.GroupPurpose).OrderBy(g => g.Key);

            foreach (var purposeGroup in groupedByPurpose)
            {
                EditorGUILayout.LabelField($"📁 {purposeGroup.Key}", EditorStyles.boldLabel);

                foreach (var group in purposeGroup.OrderBy(g => g.GroupId))
                {
                    if (group == null) continue;

                    bool isSelected = (group == selectedGroup);

                    using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
                    {
                        // 그룹 정보
                        using (new EditorGUILayout.VerticalScope())
                        {
                            string displayName = $"[{group.GroupId}] {group.GroupName}";
                            if (GUILayout.Button(displayName, EditorStyles.label))
                            {
                                SelectGroup(group);
                            }

                            // 추가 정보
                            string info = $"Lv.{group.RecommendedLevel} | 난이도:{group.Difficulty} | 몬스터:{group.GetActiveMonsterCount()}/5";
                            GUILayout.Label(info, EditorStyles.miniLabel);

                            // 전투력 표시
                            GUILayout.Label($"⚡ Power: {group.TotalPower}", EditorStyles.miniLabel);
                        }

                        GUILayout.FlexibleSpace();

                        // 복사 버튼
                        if (GUILayout.Button("📋", GUILayout.Width(25)))
                        {
                            DuplicateGroup(group);
                        }

                        // 삭제 버튼
                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            DeleteGroup(group);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                }

                EditorGUILayout.Space(3);
            }
        }
    }

    private void DrawGroupDetails()
    {
        if (selectedGroup == null)
        {
            EditorGUILayout.HelpBox("Select a group from the list or create a new one.", MessageType.Info);
            return;
        }

        using (var scrollView = new EditorGUILayout.ScrollViewScope(detailScrollPos))
        {
            detailScrollPos = scrollView.scrollPosition;

            EditorGUI.BeginChangeCheck();

            // 기본 정보
            DrawBasicInfo();
            EditorGUILayout.Space(10);

            // 몬스터 슬롯
            DrawMonsterSlots();
            EditorGUILayout.Space(10);

            // 진형 설정
            DrawFormationSettings();
            EditorGUILayout.Space(10);

            // 전투 설정
            DrawBattleSettings();
            EditorGUILayout.Space(10);

            // 보상 설정
            DrawRewardSettings();

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
            var serializedObject = new SerializedObject(selectedGroup);

            // ID는 읽기 전용으로 표시
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groupId"));
            GUI.enabled = true;

            // 이름 변경 감지
            var nameProp = serializedObject.FindProperty("groupName");
            string oldName = nameProp.stringValue;
            EditorGUILayout.PropertyField(nameProp);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("groupPurpose"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("recommendedLevel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("difficulty"));

            serializedObject.ApplyModifiedProperties();

            // 이름이 변경되었으면 파일명도 업데이트
            if (oldName != nameProp.stringValue)
            {
                isDirty = true;
            }
        }
    }

    private void DrawMonsterSlots()
    {
        EditorGUILayout.LabelField("Monster Slots", headerStyle);

        // 진형 미리보기
        if (showFormationPreview)
        {
            DrawFormationPreview();
        }

        EditorGUILayout.Space(5);

        // 슬롯별 상세 정보
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            for (int i = 0; i < 5; i++)
            {
                DrawMonsterSlot(i);
                if (i < 4) EditorGUILayout.Space(3);
            }
        }

        // 전체 클리어 버튼
        EditorGUILayout.Space(5);
        if (GUILayout.Button("Clear All Slots", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear All", "Remove all monsters from group?", "Clear", "Cancel"))
            {
                selectedGroup.ClearAllMonsters();
                EditorUtility.SetDirty(selectedGroup);
            }
        }
    }

    private void DrawFormationPreview()
    {
        EditorGUILayout.LabelField("Formation Preview:", EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // 진형에 따른 슬롯 배치 시각화
            switch (selectedGroup.FormationType)
            {
                case FormationType.Offensive: // 1-4
                    DrawSlotBox(0);
                    GUILayout.Space(20);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawSlotBox(1);
                        DrawSlotBox(2);
                        DrawSlotBox(3);
                        DrawSlotBox(4);
                    }
                    break;

                case FormationType.Defensive: // 4-1
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawSlotBox(0);
                        DrawSlotBox(1);
                        DrawSlotBox(2);
                        DrawSlotBox(3);
                    }
                    GUILayout.Space(20);
                    DrawSlotBox(4);
                    break;

                case FormationType.OffensiveBalance: // 2-3
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawSlotBox(0);
                        DrawSlotBox(1);
                    }
                    GUILayout.Space(20);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawSlotBox(2);
                        DrawSlotBox(3);
                        DrawSlotBox(4);
                    }
                    break;

                case FormationType.DefensiveBalance: // 3-2
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawSlotBox(0);
                        DrawSlotBox(1);
                        DrawSlotBox(2);
                    }
                    GUILayout.Space(20);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawSlotBox(3);
                        DrawSlotBox(4);
                    }
                    break;
            }

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawSlotBox(int index)
    {
        var slot = selectedGroup.MonsterSlots[index];
        string label = slot.isEmpty ? $"Slot {index}" : GetMonsterShortName(slot.monsterData);

        var style = slot.isEmpty ? emptySlotStyle : filledSlotStyle;

        if (GUILayout.Button(label, style, GUILayout.Width(80), GUILayout.Height(40)))
        {
            ShowMonsterPicker(index);
        }
    }

    private string GetMonsterShortName(BattleMonsterDataSO monster)
    {
        if (monster == null) return "Empty";

        string name = monster.MonsterName;
        if (name.Length > 8)
        {
            name = name.Substring(0, 8) + "..";
        }

        string icon = GetMonsterIcon(monster.MonsterType);
        return $"{icon}{name}";
    }

    private void DrawMonsterSlot(int index)
    {
        var slot = selectedGroup.MonsterSlots[index];

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Slot {index}:", GUILayout.Width(60));

            if (slot.isEmpty)
            {
                EditorGUILayout.LabelField("[Empty]", EditorStyles.miniLabel);
            }
            else if (slot.monsterData != null)
            {
                EditorGUILayout.LabelField($"{GetMonsterIcon(slot.monsterData.MonsterType)} {slot.monsterData.MonsterName}",
                    GUILayout.Width(150));

                // 레벨 설정
                EditorGUILayout.LabelField("Lv:", GUILayout.Width(25));
                int newLevel = EditorGUILayout.IntField(slot.level, GUILayout.Width(50));
                if (newLevel != slot.level)
                {
                    slot.level = Mathf.Clamp(newLevel, 1, 200);
                    selectedGroup.CalculateTotalPower();
                    EditorUtility.SetDirty(selectedGroup);
                    isDirty = true;
                }
            }

            GUILayout.FlexibleSpace();

            // 몬스터 선택 버튼
            GUI.backgroundColor = slot.isEmpty ? Color.green : Color.cyan;
            if (GUILayout.Button(slot.isEmpty ? "Select" : "Change", GUILayout.Width(60)))
            {
                ShowMonsterSelectionWindow(index);
            }
            GUI.backgroundColor = Color.white;

            // 제거 버튼
            if (!slot.isEmpty)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    selectedGroup.RemoveMonster(index);
                    EditorUtility.SetDirty(selectedGroup);
                    isDirty = true;
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }

    private void ShowMonsterSelectionWindow(int slotIndex)
    {
        MonsterSelectionWindow.ShowWindow(slotIndex, (monsterId, slotIdx) =>
        {
            var monster = availableMonsters.FirstOrDefault(m => m.MonsterId == monsterId);
            if (monster != null)
            {
                selectedGroup.AddMonster(slotIdx, monster, selectedGroup.RecommendedLevel);
                EditorUtility.SetDirty(selectedGroup);
                isDirty = true;
            }
        });
    }

    private void DrawFormationSettings()
    {
        EditorGUILayout.LabelField("Formation Settings", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedGroup);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("formationType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useCustomFormation"));

            serializedObject.ApplyModifiedProperties();

            // 진형 미리보기 토글
            showFormationPreview = EditorGUILayout.Toggle("Show Formation Preview", showFormationPreview);
        }
    }

    private void DrawBattleSettings()
    {
        EditorGUILayout.LabelField("Battle Settings", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedGroup);

            // 전투력
            EditorGUILayout.LabelField($"Total Power: {selectedGroup.TotalPower}", EditorStyles.boldLabel);

            var autoCalcProp = serializedObject.FindProperty("autoCalculatePower");
            EditorGUILayout.PropertyField(autoCalcProp);

            if (!autoCalcProp.boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("totalPower"));
            }
            else
            {
                if (GUILayout.Button("Recalculate Power"))
                {
                    selectedGroup.CalculateTotalPower();
                }
            }

            EditorGUILayout.Space(5);

            // 특수 조건
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hasTimeLimit"));
            if (serializedObject.FindProperty("hasTimeLimit").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("timeLimit"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("hasSpecialCondition"));
            if (serializedObject.FindProperty("hasSpecialCondition").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("specialConditionDescription"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawRewardSettings()
    {
        EditorGUILayout.LabelField("Reward Settings", headerStyle);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var serializedObject = new SerializedObject(selectedGroup);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("goldReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("expReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemRewardIds"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawSaveButton()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            // 검증 버튼
            if (GUILayout.Button("Validate", GUILayout.Width(100), GUILayout.Height(30)))
            {
                string error;
                if (selectedGroup.ValidateGroup(out error))
                {
                    EditorUtility.DisplayDialog("Validation", "Group is valid!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Failed", error, "OK");
                }
            }

            // 저장 버튼
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

    // 몬스터 선택 팝업
    private void ShowMonsterPicker(int slotIndex)
    {
        monsterPickerSlot = slotIndex;
        showMonsterPicker = true;
    }

    private void DrawMonsterPicker()
    {
        // 팝업 창 스타일
        Rect windowRect = new Rect(
            (Screen.width - 400) / 2,
            (Screen.height - 500) / 2,
            400, 500
        );

        GUI.Window(999, windowRect, DrawMonsterPickerWindow, "Select Monster");
    }

    private void DrawMonsterPickerWindow(int windowId)
    {
        // 닫기 버튼
        if (GUI.Button(new Rect(370, 5, 25, 20), "X"))
        {
            showMonsterPicker = false;
            monsterPickerSlot = -1;
        }

        GUILayout.Space(25);

        // 몬스터 리스트
        using (var scrollView = new EditorGUILayout.ScrollViewScope(monsterPickerScrollPos))
        {
            monsterPickerScrollPos = scrollView.scrollPosition;

            // None 옵션
            if (GUILayout.Button("[None - Clear Slot]"))
            {
                selectedGroup.RemoveMonster(monsterPickerSlot);
                showMonsterPicker = false;
                EditorUtility.SetDirty(selectedGroup);
            }

            EditorGUILayout.Space(5);

            // 타입별로 그룹화
            var groupedMonsters = availableMonsters.GroupBy(m => m.MonsterType).OrderBy(g => g.Key);

            foreach (var group in groupedMonsters)
            {
                EditorGUILayout.LabelField($"{GetMonsterIcon(group.Key)} {group.Key}", EditorStyles.boldLabel);

                foreach (var monster in group.OrderBy(m => m.MonsterId))
                {
                    if (GUILayout.Button($"[{monster.MonsterId}] {monster.MonsterName}"))
                    {
                        selectedGroup.AddMonster(monsterPickerSlot, monster, selectedGroup.RecommendedLevel);
                        showMonsterPicker = false;
                        EditorUtility.SetDirty(selectedGroup);
                    }
                }

                EditorGUILayout.Space(3);
            }
        }
    }

    // 데이터 관리 메서드
    private void LoadAllGroupData()
    {
        groupDataList.Clear();

        string[] guids = AssetDatabase.FindAssets("t:MonsterGroupDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonsterGroupDataSO data = AssetDatabase.LoadAssetAtPath<MonsterGroupDataSO>(path);
            if (data != null)
            {
                groupDataList.Add(data);
                Debug.Log($"Loaded: {data.name} (ID: {data.GroupId})"); // 디버그 추가
            }
        }

        groupDataList = groupDataList.OrderBy(g => g.GroupId).ToList();
    }

    private void LoadAvailableMonsters()
    {
        availableMonsters.Clear();

        string[] guids = AssetDatabase.FindAssets("t:BattleMonsterDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BattleMonsterDataSO data = AssetDatabase.LoadAssetAtPath<BattleMonsterDataSO>(path);
            if (data != null)
            {
                availableMonsters.Add(data);
            }
        }

        availableMonsters = availableMonsters.OrderBy(m => m.MonsterId).ToList();
    }

    private List<MonsterGroupDataSO> GetFilteredGroupList()
    {
        var filtered = groupDataList.AsEnumerable();

        if (!string.IsNullOrEmpty(searchString))
        {
            filtered = filtered.Where(g =>
                g.GroupName.ToLower().Contains(searchString.ToLower()) ||
                g.GroupId.ToString().Contains(searchString));
        }

        if (showFilters && filterPurpose != "All")
        {
            filtered = filtered.Where(g => g.GroupPurpose == filterPurpose);
        }

        return filtered.ToList();
    }

    private void SelectGroup(MonsterGroupDataSO group)
    {
        selectedGroup = group;
    }


    // ID 자동 생성 개선
    private int GetNextGroupId()
    {
        // 1. 기존 그룹들에서 최대 ID 찾기
        int maxId = 4999;

        // 현재 로드된 그룹에서 최대값 찾기
        if (groupDataList != null && groupDataList.Count > 0)
        {
            maxId = groupDataList.Max(g => g.GroupId);
        }

        // 2. 저장된 모든 MonsterGroup 파일 검사 (혹시 로드 안 된 것 체크)
        string[] guids = AssetDatabase.FindAssets("t:MonsterGroupDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonsterGroupDataSO data = AssetDatabase.LoadAssetAtPath<MonsterGroupDataSO>(path);
            if (data != null && data.GroupId > maxId)
            {
                maxId = data.GroupId;
            }
        }

        // 3. lastAssignedId와 비교하여 더 큰 값 사용
        if (lastAssignedId > maxId)
        {
            maxId = lastAssignedId;
        }

        // 4. 다음 ID 할당
        int nextId = maxId + 1;
        lastAssignedId = nextId;

        Debug.Log($"[MonsterGroupTab] Generated new ID: {nextId}");
        return nextId;
    }

    // 새 그룹 생성 수정
    private void CreateNewGroup()
    {
        var newGroup = ScriptableObject.CreateInstance<MonsterGroupDataSO>();

        // 자동 ID 할당
        int nextId = GetNextGroupId();

        // 기본 이름 설정
        string defaultName = $"MonsterGroup_{nextId}";

        // 리플렉션으로 private 필드 설정
        var type = newGroup.GetType();
        var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        type.GetField("groupId", bindingFlags)?.SetValue(newGroup, nextId);
        type.GetField("groupName", bindingFlags)?.SetValue(newGroup, defaultName);

        // 파일명: GroupName_ID 형식
        newGroup.name = $"{defaultName}_{nextId}";

        // 저장
        SaveGroupAsset(newGroup);

        LoadAllGroupData();
        SelectGroup(newGroup);
    }

    // 템플릿에서 생성 수정
    private void CreateFromTemplate(string templateType)
    {
        var newGroup = ScriptableObject.CreateInstance<MonsterGroupDataSO>();
        newGroup.InitializeFromTemplate(templateType);

        // 자동 ID 할당
        int nextId = GetNextGroupId();

        // 템플릿 기반 이름
        string groupName = $"{templateType}Group_{nextId}";

        // 리플렉션으로 설정
        var type = newGroup.GetType();
        var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        type.GetField("groupId", bindingFlags)?.SetValue(newGroup, nextId);
        type.GetField("groupName", bindingFlags)?.SetValue(newGroup, groupName);

        // 파일명: GroupName_ID 형식
        newGroup.name = $"{groupName}_{nextId}";

        // 저장
        SaveGroupAsset(newGroup);

        LoadAllGroupData();
        SelectGroup(newGroup);
    }

    private void DuplicateGroup(MonsterGroupDataSO original)
    {
        if (original == null) return;

        var copy = Object.Instantiate(original);

        // 새 ID 할당
        int nextId = GetNextGroupId();

        // 이름 설정 (원본 이름에 _Copy 추가)
        string baseName = original.GroupName;
        if (baseName.Contains("_Copy"))
        {
            // 이미 Copy가 있으면 숫자 증가
            baseName = baseName.Substring(0, baseName.IndexOf("_Copy"));
        }
        string copyName = $"{baseName}_Copy";

        // 리플렉션으로 설정
        var type = copy.GetType();
        var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        type.GetField("groupId", bindingFlags)?.SetValue(copy, nextId);
        type.GetField("groupName", bindingFlags)?.SetValue(copy, copyName);

        // 파일명: GroupName_ID 형식
        copy.name = $"{copyName}_{nextId}";

        // 저장
        SaveGroupAsset(copy);

        LoadAllGroupData();
        SelectGroup(copy);
    }
    // 저장 메서드 통합
    private void SaveGroupAsset(MonsterGroupDataSO group)
    {
        string directory = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/MonsterGroups";

        // 디렉토리 확인/생성
        if (!AssetDatabase.IsValidFolder(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        // 파일명 정리 (특수문자 제거)
        string safeName = SanitizeFileName(group.name);
        string path = $"{directory}/{safeName}.asset";

        // 중복 파일명 체크
        int counter = 1;
        string originalPath = path;
        while (AssetDatabase.LoadAssetAtPath<MonsterGroupDataSO>(path) != null)
        {
            path = originalPath.Replace(".asset", $"_{counter}.asset");
            counter++;
        }

        AssetDatabase.CreateAsset(group, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MonsterGroupTab] Saved: {path}");
    }

    // 파일명 정리 메서드
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


    // 그룹 이름 변경 시 파일명도 업데이트
    private void UpdateGroupFileName(MonsterGroupDataSO group)
    {
        if (group == null) return;

        string oldPath = AssetDatabase.GetAssetPath(group);
        if (string.IsNullOrEmpty(oldPath)) return;

        // 새 파일명: GroupName_ID 형식
        string newName = $"{group.GroupName}_{group.GroupId}";
        string safeName = SanitizeFileName(newName);

        // 경로 생성
        string directory = System.IO.Path.GetDirectoryName(oldPath);
        string newPath = $"{directory}/{safeName}.asset";

        // 파일명 변경
        if (oldPath != newPath)
        {
            string error = AssetDatabase.RenameAsset(oldPath, safeName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"Failed to rename asset: {error}");
            }
            else
            {
                Debug.Log($"[MonsterGroupTab] Renamed: {oldPath} -> {newPath}");
                AssetDatabase.SaveAssets();
            }
        }
    }


    private void DeleteGroup(MonsterGroupDataSO group)
    {
        if (group == null) return;

        if (EditorUtility.DisplayDialog("Delete Group",
            $"Are you sure you want to delete {group.GroupName}?",
            "Delete", "Cancel"))
        {
            string path = AssetDatabase.GetAssetPath(group);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            LoadAllGroupData();
            selectedGroup = null;
        }
    }

    private void SaveChanges()
    {
        if (selectedGroup != null)
        {
            // 파일명 업데이트 체크
            UpdateGroupFileName(selectedGroup);

            EditorUtility.SetDirty(selectedGroup);
            AssetDatabase.SaveAssets();
            isDirty = false;

            EditorUtility.DisplayDialog("Saved",
                $"Monster group saved.\nID: {selectedGroup.GroupId}\nName: {selectedGroup.GroupName}",
                "OK");
        }
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


// 몬스터 선택 전용 팝업 윈도우
public class MonsterSelectionWindow : EditorWindow
{
    private int targetSlotIndex;
    private System.Action<int, int> onMonsterSelected;

    // 몬스터 리스트
    private List<BattleMonsterDataSO> allMonsters = new List<BattleMonsterDataSO>();
    private Dictionary<BattleCharacterSystem.MonsterType, List<BattleMonsterDataSO>> monstersByType;

    // 필터/검색
    private string searchString = "";
    private BattleCharacterSystem.MonsterType selectedType = BattleCharacterSystem.MonsterType.Normal;
    private bool showAllTypes = true;

    // UI
    private Vector2 scrollPos;
    private GUIStyle headerStyle;
    private bool[] typeFoldouts = new bool[5];  // 각 타입별 펼침 상태

    public static void ShowWindow(int slotIndex, System.Action<int, int> callback)
    {
        var window = GetWindow<MonsterSelectionWindow>(true, "Select Monster");
        window.minSize = new Vector2(500, 600);
        window.maxSize = new Vector2(500, 800);
        window.targetSlotIndex = slotIndex;
        window.onMonsterSelected = callback;
        window.Initialize();
        window.Show();
    }

    private void Initialize()
    {
        LoadMonsters();
        InitializeStyles();

        // 모든 타입 펼치기
        for (int i = 0; i < typeFoldouts.Length; i++)
        {
            typeFoldouts[i] = true;
        }
    }

    private void InitializeStyles()
    {
        headerStyle = new GUIStyle();
        headerStyle.fontSize = 13;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.white;
        headerStyle.padding = new RectOffset(5, 5, 3, 3);
    }

    private void LoadMonsters()
    {
        allMonsters.Clear();

        string[] guids = AssetDatabase.FindAssets("t:BattleMonsterDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var monster = AssetDatabase.LoadAssetAtPath<BattleMonsterDataSO>(path);
            if (monster != null)
            {
                allMonsters.Add(monster);
            }
        }

        // 타입별로 그룹화
        monstersByType = allMonsters
            .GroupBy(m => m.MonsterType)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.MonsterId).ToList());
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);

        // 헤더
        EditorGUILayout.LabelField($"Select Monster for Slot {targetSlotIndex}", headerStyle);
        EditorGUILayout.Space(5);

        // 검색 바
        DrawSearchBar();

        EditorGUILayout.Space(5);

        // 필터 옵션
        DrawFilterOptions();

        EditorGUILayout.Space(10);

        // 몬스터 리스트
        DrawMonsterList();

        EditorGUILayout.Space(10);

        // 하단 버튼
        DrawBottomButtons();
    }

    private void DrawSearchBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchString = EditorGUILayout.TextField(searchString);

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                searchString = "";
                GUI.FocusControl(null);
            }
        }
    }

    private void DrawFilterOptions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            showAllTypes = EditorGUILayout.Toggle("Show All Types", showAllTypes, GUILayout.Width(150));

            if (!showAllTypes)
            {
                selectedType = (BattleCharacterSystem.MonsterType)EditorGUILayout.EnumPopup(selectedType);
            }
        }
    }

    private void DrawMonsterList()
    {
        using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPos, EditorStyles.helpBox))
        {
            scrollPos = scrollView.scrollPosition;

            // None 옵션 (슬롯 비우기)
            GUI.backgroundColor = Color.gray;
            if (GUILayout.Button("[Clear Slot]", GUILayout.Height(25)))
            {
                onMonsterSelected?.Invoke(0, targetSlotIndex);
                Close();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // 수정된 부분: 타입 통일
            IEnumerable<BattleCharacterSystem.MonsterType> typesToShow;
            if (showAllTypes)
            {
                typesToShow = monstersByType.Keys.OrderBy(t => t);
            }
            else
            {
                typesToShow = new[] { selectedType };
            }

            foreach (var type in typesToShow)
            {
                if (!monstersByType.ContainsKey(type)) continue;

                var monsters = monstersByType[type];
                var filteredMonsters = FilterMonsters(monsters);

                if (filteredMonsters.Count == 0) continue;

                // 타입 헤더 (Foldout)
                int typeIndex = (int)type;
                using (new EditorGUILayout.HorizontalScope())
                {
                    typeFoldouts[typeIndex] = EditorGUILayout.Foldout(
                        typeFoldouts[typeIndex],
                        $"{GetMonsterTypeIcon(type)} {type} ({filteredMonsters.Count})",
                        true
                    );
                }

                if (typeFoldouts[typeIndex])
                {
                    EditorGUI.indentLevel++;

                    foreach (var monster in filteredMonsters)
                    {
                        DrawMonsterEntry(monster);
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);
            }
        }
    }

    private void DrawMonsterEntry(BattleMonsterDataSO monster)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            // 몬스터 정보
            string label = $"[{monster.MonsterId}] {monster.MonsterName}";

            // 보스 표시
            if (monster.IsBoss)
            {
                GUI.color = Color.yellow;
                label += " [BOSS]";
            }

            // 원소 타입 표시
            label += $" ({monster.ElementType})";

            if (GUILayout.Button(label, EditorStyles.label))
            {
                SelectMonster(monster);
            }

            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            // 선택 버튼
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                SelectMonster(monster);
            }
        }
    }

    private List<BattleMonsterDataSO> FilterMonsters(List<BattleMonsterDataSO> monsters)
    {
        if (string.IsNullOrEmpty(searchString))
            return monsters;

        string search = searchString.ToLower();
        return monsters.Where(m =>
            m.MonsterName.ToLower().Contains(search) ||
            m.MonsterId.ToString().Contains(search) ||
            m.ElementType.ToString().ToLower().Contains(search)
        ).ToList();
    }

    private void SelectMonster(BattleMonsterDataSO monster)
    {
        onMonsterSelected?.Invoke(monster.MonsterId, targetSlotIndex);
        Close();
    }

    private void DrawBottomButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(100)))
            {
                Close();
            }
        }
    }

    private string GetMonsterTypeIcon(BattleCharacterSystem.MonsterType type)
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
