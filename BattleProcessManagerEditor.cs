using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using BattleCharacterSystem;

[CustomEditor(typeof(BattleProcessManagerNew))]
public class BattleProcessManagerEditor : Editor
{
    private BattleProcessManagerNew manager;

    // 캐릭터 선택 관련
    private bool showCharacterSelector = false;
    private List<BattleCharacterDataSO> availableCharacters = new List<BattleCharacterDataSO>();
    private Vector2 characterScrollPos;
    private string characterSearchFilter = "";

    // 몬스터 그룹 선택 관련
    private bool showMonsterGroupSelector = false;
    private List<MonsterGroupDataSO> availableMonsterGroups = new List<MonsterGroupDataSO>();
    private Vector2 monsterGroupScrollPos;
    private string monsterGroupSearchFilter = "";

    // 스타일
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private Color allyColor = new Color(0.3f, 0.5f, 1f, 0.3f);
    private Color enemyColor = new Color(1f, 0.3f, 0.3f, 0.3f);

    private void OnEnable()
    {
        manager = (BattleProcessManagerNew)target;
        LoadAvailableData();
        InitStyles();
    }

    private void InitStyles()
    {
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        boxStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10)
        };
    }

    private void LoadAvailableData()
    {
        // 모든 캐릭터 데이터 로드
        availableCharacters.Clear();
        string[] charGuids = AssetDatabase.FindAssets("t:BattleCharacterDataSO");
        foreach (string guid in charGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<BattleCharacterDataSO>(path);
            if (data != null)
            {
                availableCharacters.Add(data);
            }
        }
        availableCharacters = availableCharacters.OrderBy(c => c.CharacterId).ToList();

        // 모든 몬스터 그룹 로드
        availableMonsterGroups.Clear();
        string[] groupGuids = AssetDatabase.FindAssets("t:MonsterGroupDataSO");
        foreach (string guid in groupGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<MonsterGroupDataSO>(path);
            if (data != null)
            {
                availableMonsterGroups.Add(data);
            }
        }
        availableMonsterGroups = availableMonsterGroups.OrderBy(g => g.GroupId).ToList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 필드들 표시
        DrawDefaultFields();

        EditorGUILayout.Space(20);

        // 새 데이터 시스템 사용 여부
        EditorGUILayout.LabelField("===== 전투 데이터 시스템 =====", headerStyle);


        EditorGUILayout.Space(10);

        // 아군 캐릭터 섹션
        DrawAllyCharacterSection();

        EditorGUILayout.Space(10);

        // 적 몬스터 그룹 섹션
        DrawEnemyMonsterSection();

        EditorGUILayout.Space(10);

        // 퀵 버튼들
        DrawQuickButtons();


        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultFields()
    {
        // useNewDataSystem과 test 배열을 제외한 다른 필드들 표시
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            // 특정 필드들은 건너뛰기
            if (prop.name == "m_Script" ||
                prop.name == "useNewDataSystem" ||
                prop.name == "characterIds" ||
                prop.name == "characterKeys" ||
                prop.name == "monsterGroupId")
                continue;

            EditorGUILayout.PropertyField(prop, true);
        }
    }

    private void DrawAllyCharacterSection()
    {
        // 아군 섹션 헤더
        GUI.backgroundColor = allyColor;
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField("🛡️ 아군 캐릭터 (최대 5명)", EditorStyles.boldLabel);

            SerializedProperty charIdsProp = serializedObject.FindProperty("characterIds");
            SerializedProperty charKeysProp = serializedObject.FindProperty("characterKeys");

            // 현재 선택된 캐릭터들 표시
            for (int i = 0; i < charIdsProp.arraySize && i < 5; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"슬롯 {i + 1}:", GUILayout.Width(60));

                    int charId = charIdsProp.GetArrayElementAtIndex(i).intValue;
                    if (charId > 0)
                    {
                        var charData = availableCharacters.FirstOrDefault(c => c.CharacterId == charId);
                        if (charData != null)
                        {
                            string displayName = $"[{charData.CharacterId}] {charData.CharacterName} ({charData.Tier})";
                            EditorGUILayout.LabelField(displayName);

                            if (GUILayout.Button("X", GUILayout.Width(25)))
                            {
                                charIdsProp.GetArrayElementAtIndex(i).intValue = 0;
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"ID {charId} (Not Found)");
                            if (GUILayout.Button("X", GUILayout.Width(25)))
                            {
                                charIdsProp.GetArrayElementAtIndex(i).intValue = 0;
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(비어있음)");
                        if (GUILayout.Button("+", GUILayout.Width(25)))
                        {
                            showCharacterSelector = true;
                            characterScrollPos = Vector2.zero;
                        }
                    }
                }
            }

            // 캐릭터 선택 UI
            if (showCharacterSelector)
            {
                DrawCharacterSelector(charIdsProp, charKeysProp );
            }

            // 전체 관리 버튼
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("캐릭터 추가"))
                {
                    showCharacterSelector = !showCharacterSelector;
                    if (showCharacterSelector)
                    {
                        LoadAvailableData();
                    }
                }

                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("모두 제거"))
                {
                    for (int i = 0; i < charIdsProp.arraySize; i++)
                    {
                        charIdsProp.GetArrayElementAtIndex(i).intValue = 0;
                    }
                    showCharacterSelector = false;
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }

    private void DrawCharacterSelector(SerializedProperty charIdsProp, SerializedProperty charKeysProp)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("캐릭터 선택", EditorStyles.boldLabel);

        // 검색 필터
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("검색:", GUILayout.Width(40));
            characterSearchFilter = EditorGUILayout.TextField(characterSearchFilter);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                characterSearchFilter = "";
            }
        }

        // 캐릭터 리스트
        var filteredCharacters = availableCharacters.Where(c =>
            string.IsNullOrEmpty(characterSearchFilter) ||
            c.CharacterName.ToLower().Contains(characterSearchFilter.ToLower()) ||
            c.CharacterId.ToString().Contains(characterSearchFilter)
        ).ToList();

        characterScrollPos = EditorGUILayout.BeginScrollView(characterScrollPos, GUILayout.Height(200));

        foreach (var character in filteredCharacters)
        {
            // 이미 선택된 캐릭터는 비활성화
            bool isAlreadySelected = false;
            for (int i = 0; i < charIdsProp.arraySize; i++)
            {
                if (charIdsProp.GetArrayElementAtIndex(i).intValue == character.CharacterId)
                {
                    isAlreadySelected = true;
                    break;
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                // 티어별 색상
                Color tierColor = GetTierColor(character.Tier);
                GUI.backgroundColor = tierColor;
                EditorGUILayout.LabelField(character.Tier.ToString(), GUILayout.Width(30));
                GUI.backgroundColor = Color.white;

                // 클래스 아이콘
                string classIcon = GetClassIcon(character.CharacterClass);
                EditorGUILayout.LabelField(classIcon, GUILayout.Width(25));

                // 캐릭터 정보
                EditorGUILayout.LabelField($"[{character.CharacterId}] {character.CharacterName}");

                // 선택 버튼
                GUI.enabled = !isAlreadySelected;
                if (GUILayout.Button(isAlreadySelected ? "선택됨" : "선택", GUILayout.Width(60)))
                {
                    // 빈 슬롯 찾기
                    for (int i = 0; i < charIdsProp.arraySize && i < 5; i++)
                    {
                        if (charIdsProp.GetArrayElementAtIndex(i).intValue == 0)
                        {
                            charIdsProp.GetArrayElementAtIndex(i).intValue = character.CharacterId;
                            charKeysProp.GetArrayElementAtIndex(i).stringValue = "SO_" + character.name + "_asset";
                            showCharacterSelector = false;
                            break;
                        }
                    }
                }
                GUI.enabled = true;
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("닫기"))
        {
            showCharacterSelector = false;
        }
    }

    private void DrawEnemyMonsterSection()
    {
        // 적 섹션 헤더
        GUI.backgroundColor = enemyColor;
        using (new EditorGUILayout.VerticalScope(boxStyle))
        {
            GUI.backgroundColor = Color.white;

            EditorGUILayout.LabelField("⚔️ 적 몬스터 그룹", EditorStyles.boldLabel);

            SerializedProperty groupIdProp = serializedObject.FindProperty("monsterGroupId");

            // 현재 선택된 그룹 표시
            int groupId = groupIdProp.intValue;
            if (groupId > 0)
            {
                var groupData = availableMonsterGroups.FirstOrDefault(g => g.GroupId == groupId);
                if (groupData != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"[{groupData.GroupId}] {groupData.GroupName}");

                        // 난이도 표시
                        for (int i = 0; i < groupData.Difficulty; i++)
                        {
                            EditorGUILayout.LabelField("★", GUILayout.Width(15));
                        }

                        if (GUILayout.Button("변경", GUILayout.Width(50)))
                        {
                            showMonsterGroupSelector = !showMonsterGroupSelector;
                        }

                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            groupIdProp.intValue = 0;
                        }
                    }

                    // 그룹 내 몬스터 미리보기
                    EditorGUI.indentLevel++;
                    int monsterCount = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        if (!groupData.MonsterSlots[i].isEmpty && groupData.MonsterSlots[i].monsterData != null)
                        {
                            var monster = groupData.MonsterSlots[i].monsterData;
                            EditorGUILayout.LabelField($"  슬롯 {i + 1}: {monster.MonsterName} (Lv.{groupData.MonsterSlots[i].level})");
                            monsterCount++;
                        }
                    }
                    EditorGUILayout.LabelField($"총 {monsterCount}마리");
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField($"Group ID {groupId} (Not Found)");
                    if (GUILayout.Button("선택", GUILayout.Width(60)))
                    {
                        showMonsterGroupSelector = true;
                    }
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("(선택되지 않음)");
                    if (GUILayout.Button("그룹 선택", GUILayout.Width(100)))
                    {
                        showMonsterGroupSelector = true;
                        LoadAvailableData();
                    }
                }
            }

            // 몬스터 그룹 선택 UI
            if (showMonsterGroupSelector)
            {
                DrawMonsterGroupSelector(groupIdProp);
            }
        }
    }

    private void DrawMonsterGroupSelector(SerializedProperty groupIdProp)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("몬스터 그룹 선택", EditorStyles.boldLabel);

        // 검색 필터
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("검색:", GUILayout.Width(40));
            monsterGroupSearchFilter = EditorGUILayout.TextField(monsterGroupSearchFilter);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                monsterGroupSearchFilter = "";
            }
        }

        // 그룹 리스트
        var filteredGroups = availableMonsterGroups.Where(g =>
            string.IsNullOrEmpty(monsterGroupSearchFilter) ||
            g.GroupName.ToLower().Contains(monsterGroupSearchFilter.ToLower()) ||
            g.GroupId.ToString().Contains(monsterGroupSearchFilter) ||
            g.GroupPurpose.ToLower().Contains(monsterGroupSearchFilter.ToLower())
        ).ToList();

        monsterGroupScrollPos = EditorGUILayout.BeginScrollView(monsterGroupScrollPos, GUILayout.Height(200));

        foreach (var group in filteredGroups)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // 그룹 타입별 색상
                    Color purposeColor = GetPurposeColor(group.GroupPurpose);
                    GUI.backgroundColor = purposeColor;
                    EditorGUILayout.LabelField(group.GroupPurpose, GUILayout.Width(80));
                    GUI.backgroundColor = Color.white;

                    // 그룹 정보
                    EditorGUILayout.LabelField($"[{group.GroupId}] {group.GroupName}");

                    // 난이도
                    string stars = "";
                    for (int i = 0; i < group.Difficulty; i++) stars += "★";
                    EditorGUILayout.LabelField(stars, GUILayout.Width(50));

                    // 선택 버튼
                    if (GUILayout.Button("선택", GUILayout.Width(50)))
                    {
                        groupIdProp.intValue = group.GroupId;
                        showMonsterGroupSelector = false;
                    }
                }

                // 몬스터 구성 미리보기
                string monsters = "구성: ";
                int count = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (!group.MonsterSlots[i].isEmpty && group.MonsterSlots[i].monsterData != null)
                    {
                        if (count > 0) monsters += ", ";
                        monsters += group.MonsterSlots[i].monsterData.MonsterName;
                        count++;
                    }
                }
                EditorGUILayout.LabelField(monsters, EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("닫기"))
        {
            showMonsterGroupSelector = false;
        }
    }

    private void DrawQuickButtons()
    {
        EditorGUILayout.LabelField("빠른 설정", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("🎮 테스트 세팅 1"))
            {
                SetupTestConfiguration(1);
            }

            if (GUILayout.Button("🎮 테스트 세팅 2"))
            {
                SetupTestConfiguration(2);
            }

            if (GUILayout.Button("🎮 보스전 세팅"))
            {
                SetupBossConfiguration();
            }

            if (GUILayout.Button("🔄 데이터 새로고침"))
            {
                LoadAvailableData();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("📁 캐릭터 에디터 열기"))
            {
                EditorWindow.GetWindow<BattleCharacterEditorWindow>();
            }

            if (GUILayout.Button("▶️ 전투 시작"))
            {
                if (Application.isPlaying)
                {
                    manager.StartBattle().Forget();
                }
                else
                {
                    Debug.LogWarning("Play 모드에서만 전투를 시작할 수 있습니다.");
                }
            }
        }
    }

    private void SetupTestConfiguration(int config)
    {
        SerializedProperty charIdsProp = serializedObject.FindProperty("characterIds");
        SerializedProperty charKeysProp = serializedObject.FindProperty("characterKeys");
        SerializedProperty groupIdProp = serializedObject.FindProperty("monsterGroupId");

        switch (config)
        {
            case 1:
                // 기본 테스트 구성
                if (availableCharacters.Count >= 3)
                {
                    for (int i = 0; i < 3 && i < charIdsProp.arraySize; i++)
                    {
                        charIdsProp.GetArrayElementAtIndex(i).intValue = availableCharacters[i].CharacterId;
                        charKeysProp.GetArrayElementAtIndex(i).stringValue = availableCharacters[i].AddressableKey;
                    }
                }
                if (availableMonsterGroups.Count > 0)
                {
                    var normalGroup = availableMonsterGroups.FirstOrDefault(g => g.GroupPurpose == "Normal");
                    if (normalGroup != null)
                        groupIdProp.intValue = normalGroup.GroupId;
                }
                break;

            case 2:
                // 풀파티 구성
                if (availableCharacters.Count >= 5)
                {
                    for (int i = 0; i < 5 && i < charIdsProp.arraySize; i++)
                    {
                        charIdsProp.GetArrayElementAtIndex(i).intValue = availableCharacters[i].CharacterId;
                        charKeysProp.GetArrayElementAtIndex(i).stringValue = availableCharacters[i].AddressableKey;
                    }
                }
                if (availableMonsterGroups.Count > 0)
                {
                    var eliteGroup = availableMonsterGroups.FirstOrDefault(g => g.Difficulty >= 3);
                    if (eliteGroup != null)
                        groupIdProp.intValue = eliteGroup.GroupId;
                }
                break;
        }
    }

    private void SetupBossConfiguration()
    {
        SerializedProperty charIdsProp = serializedObject.FindProperty("characterIds");
        SerializedProperty charKeysProp = serializedObject.FindProperty("characterKeys");
        SerializedProperty groupIdProp = serializedObject.FindProperty("monsterGroupId");

        // 최고 티어 캐릭터들로 구성
        var topCharacters = availableCharacters
            .OrderBy(c => c.Tier)
            .ThenByDescending(c => c.BaseStats.hp + c.BaseStats.attack + c.BaseStats.defense)
            .Take(5)
            .ToList();

        for (int i = 0; i < topCharacters.Count && i < charIdsProp.arraySize; i++)
        {
            charIdsProp.GetArrayElementAtIndex(i).intValue = topCharacters[i].CharacterId;
        }

        // 보스 그룹 선택
        var bossGroup = availableMonsterGroups.FirstOrDefault(g => g.GroupPurpose == "Boss");
        if (bossGroup != null)
        {
            groupIdProp.intValue = bossGroup.GroupId;
        }
    }

    private Color GetTierColor(CharacterTier tier)
    {
        switch (tier)
        {
            case CharacterTier.XA: return new Color(1f, 0.8f, 0f, 0.3f);
            case CharacterTier.X: return new Color(0.8f, 0.4f, 1f, 0.3f);
            case CharacterTier.S: return new Color(1f, 0.4f, 0.4f, 0.3f);
            case CharacterTier.A: return new Color(0.4f, 0.8f, 1f, 0.3f);
            default: return new Color(0.7f, 0.7f, 0.7f, 0.3f);
        }
    }

    private string GetClassIcon(ClassType classType)
    {
        switch (classType)
        {
            case ClassType.Slaughter: return "⚔️";
            case ClassType.Vanguard: return "🛡️";
            case ClassType.Jacker: return "🎯";
            case ClassType.Rewinder: return "⏰";
            default: return "❓";
        }
    }

    private Color GetPurposeColor(string purpose)
    {
        switch (purpose)
        {
            case "Tutorial": return new Color(0.4f, 1f, 0.4f, 0.3f);
            case "Normal": return new Color(0.7f, 0.7f, 0.7f, 0.3f);
            case "Boss": return new Color(1f, 0.3f, 0.3f, 0.3f);
            case "Special": return new Color(1f, 0.8f, 0f, 0.3f);
            case "Event": return new Color(0.8f, 0.4f, 1f, 0.3f);
            default: return new Color(0.7f, 0.7f, 0.7f, 0.3f);
        }
    }
}