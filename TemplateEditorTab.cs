using UnityEngine;
using UnityEditor;
using System.Linq;
using BattleCharacterSystem;

public class TemplateEditorTab
{
    private TemplateDatabase templateDatabase;
    private CharacterTemplate selectedCharTemplate;
    private MonsterTemplate selectedMonsterTemplate;
    private MonsterGroupTemplate selectedGroupTemplate;

    private enum TemplateType
    {
        Character,
        Monster,
        MonsterGroup
    }

    private TemplateType currentTemplateType = TemplateType.Character;
    private Vector2 scrollPos;

    public void Initialize()
    {
        LoadTemplateDatabase();
    }

    private void LoadTemplateDatabase()
    {
        // 템플릿 데이터베이스 로드
        string[] guids = AssetDatabase.FindAssets("t:TemplateDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            templateDatabase = AssetDatabase.LoadAssetAtPath<TemplateDatabase>(path);
        }

        // 없으면 생성
        if (templateDatabase == null)
        {
            templateDatabase = ScriptableObject.CreateInstance<TemplateDatabase>();
            AssetDatabase.CreateAsset(templateDatabase, "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Common/TemplateDatabase.asset");
            templateDatabase.CreateDefaultTemplates();
            AssetDatabase.SaveAssets();
        }
    }

    public void DrawTemplateTab()
    {
        if (templateDatabase == null)
        {
            LoadTemplateDatabase();
        }

        EditorGUILayout.Space(5);

        // 템플릿 타입 선택
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Toggle(currentTemplateType == TemplateType.Character, "Character Templates", EditorStyles.toolbarButton))
                currentTemplateType = TemplateType.Character;
            if (GUILayout.Toggle(currentTemplateType == TemplateType.Monster, "Monster Templates", EditorStyles.toolbarButton))
                currentTemplateType = TemplateType.Monster;
            if (GUILayout.Toggle(currentTemplateType == TemplateType.MonsterGroup, "Monster Group Templates", EditorStyles.toolbarButton))
                currentTemplateType = TemplateType.MonsterGroup;
        }

        EditorGUILayout.Space(10);

        // 메인 컨텐츠
        using (new EditorGUILayout.HorizontalScope())
        {
            // 왼쪽: 템플릿 리스트
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                DrawTemplateList();
            }

            // 구분선
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // 오른쪽: 템플릿 상세/액션
            using (new EditorGUILayout.VerticalScope())
            {
                DrawTemplateDetails();
            }
        }
    }

    private void DrawTemplateList()
    {
        EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);

        // 템플릿 재생성 버튼
        if (GUILayout.Button("Reset to Default Templates", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Reset Templates",
                "This will reset all templates to default. Are you sure?",
                "Reset", "Cancel"))
            {
                templateDatabase.CreateDefaultTemplates();
            }
        }

        EditorGUILayout.Space(5);

        using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos))
        {
            scrollPos = scroll.scrollPosition;

            switch (currentTemplateType)
            {
                case TemplateType.Character:
                    DrawCharacterTemplateList();
                    break;
                case TemplateType.Monster:
                    DrawMonsterTemplateList();
                    break;
                case TemplateType.MonsterGroup:
                    DrawGroupTemplateList();
                    break;
            }
        }
    }

    private void DrawCharacterTemplateList()
    {
        if (templateDatabase.characterTemplates == null) return;

        // 티어별로 그룹화
        var groupedTemplates = templateDatabase.characterTemplates
            .GroupBy(t => t.tier)
            .OrderByDescending(g => g.Key);

        foreach (var tierGroup in groupedTemplates)
        {
            EditorGUILayout.LabelField($"Tier {tierGroup.Key}", EditorStyles.miniBoldLabel);

            foreach (var template in tierGroup.OrderBy(t => t.characterClass))
            {
                bool isSelected = (selectedCharTemplate == template);

                using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
                {
                    // 아이콘 (직업별)
                    string icon = GetClassIcon(template.characterClass);
                    GUILayout.Label(icon, GUILayout.Width(25));

                    // 템플릿 이름
                    if (GUILayout.Button(template.templateName, EditorStyles.label))
                    {
                        selectedCharTemplate = template;
                        selectedMonsterTemplate = null;
                        selectedGroupTemplate = null;
                    }
                }
            }

            EditorGUILayout.Space(3);
        }
    }

    private void DrawMonsterTemplateList()
    {
        if (templateDatabase.monsterTemplates == null) return;

        foreach (var template in templateDatabase.monsterTemplates.OrderBy(t => t.monsterType))
        {
            bool isSelected = (selectedMonsterTemplate == template);

            using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
            {
                // 몬스터 타입 아이콘
                string icon = GetMonsterIcon(template.monsterType);
                GUILayout.Label(icon, GUILayout.Width(25));

                if (GUILayout.Button(template.templateName, EditorStyles.label))
                {
                    selectedMonsterTemplate = template;
                    selectedCharTemplate = null;
                    selectedGroupTemplate = null;
                }
            }
        }
    }

    private void DrawGroupTemplateList()
    {
        if (templateDatabase.groupTemplates == null) return;

        foreach (var template in templateDatabase.groupTemplates)
        {
            bool isSelected = (selectedGroupTemplate == template);

            using (new EditorGUILayout.HorizontalScope(isSelected ? EditorStyles.helpBox : GUIStyle.none))
            {
                // 그룹 아이콘
                GUILayout.Label("👥", GUILayout.Width(25));

                if (GUILayout.Button(template.templateName, EditorStyles.label))
                {
                    selectedGroupTemplate = template;
                    selectedCharTemplate = null;
                    selectedMonsterTemplate = null;
                }
            }
        }
    }

    private void DrawTemplateDetails()
    {
        EditorGUILayout.LabelField("Template Details", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (selectedCharTemplate != null)
        {
            DrawCharacterTemplateDetails();
        }
        else if (selectedMonsterTemplate != null)
        {
            DrawMonsterTemplateDetails();
        }
        else if (selectedGroupTemplate != null)
        {
            DrawGroupTemplateDetails();
        }
        else
        {
            EditorGUILayout.HelpBox("Select a template from the list", MessageType.Info);
        }
    }

    private void DrawCharacterTemplateDetails()
    {
        var template = selectedCharTemplate;

        // 기본 정보
        EditorGUILayout.LabelField("Template Info", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Name:", template.templateName);
            EditorGUILayout.LabelField("Tier:", template.tier.ToString());
            EditorGUILayout.LabelField("Class:", template.characterClass.ToString());
            EditorGUILayout.LabelField("Element:", template.preferredElement.ToString());

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Description:");
            EditorGUILayout.LabelField(template.description, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(10);

        // 스탯 프리뷰
        EditorGUILayout.LabelField("Base Stats (Level 1)", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"HP: {template.baseStatsPreset.hp}");
            EditorGUILayout.LabelField($"Attack: {template.baseStatsPreset.attack}");
            EditorGUILayout.LabelField($"Defense: {template.baseStatsPreset.defense}");
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Max BP: {template.fixedStatsPreset.maxBP}");
            EditorGUILayout.LabelField($"Speed: {template.fixedStatsPreset.turnSpeed:F1}");
            EditorGUILayout.LabelField($"Crit Rate: {template.fixedStatsPreset.critRate:F1}%");
            EditorGUILayout.LabelField($"Crit Damage: {template.fixedStatsPreset.critDamage:F0}%");
        }

        EditorGUILayout.Space(10);

        // 생성 버튼
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("Create Character from Template", GUILayout.Height(35)))
        {
            CreateCharacterFromTemplate(template);
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawMonsterTemplateDetails()
    {
        var template = selectedMonsterTemplate;

        EditorGUILayout.LabelField("Template Info", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Name:", template.templateName);
            EditorGUILayout.LabelField("Type:", template.monsterType.ToString());
            EditorGUILayout.LabelField("Pattern:", template.pattern.ToString());
            EditorGUILayout.LabelField("Guardian Stones:", template.guardianStoneCount.ToString());

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Description:");
            EditorGUILayout.LabelField(template.description, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(10);

        // 스탯 범위
        EditorGUILayout.LabelField("Stat Ranges", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"HP: {template.minHp} ~ {template.maxHp}");
            EditorGUILayout.LabelField($"Attack: {template.minAttack} ~ {template.maxAttack}");
            EditorGUILayout.LabelField($"Defense: {template.minDefense} ~ {template.maxDefense}");
        }

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
        if (GUILayout.Button("Create Monster from Template", GUILayout.Height(35)))
        {
            // TODO: 몬스터 생성
            Debug.Log("Creating monster from template: " + template.templateName);
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawGroupTemplateDetails()
    {
        var template = selectedGroupTemplate;

        EditorGUILayout.LabelField("Group Template Info", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Name:", template.templateName);
            EditorGUILayout.LabelField("Purpose:", template.purpose);
            EditorGUILayout.LabelField("Formation:", template.formationType.ToString());

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Description:");
            EditorGUILayout.LabelField(template.description, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(10);

        // 슬롯 구성
        EditorGUILayout.LabelField("Slot Configuration", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            for (int i = 0; i < 5; i++)
            {
                var slot = template.slots[i];
                if (slot.isEmpty)
                {
                    EditorGUILayout.LabelField($"Slot {i}: [Empty]");
                }
                else
                {
                    EditorGUILayout.LabelField($"Slot {i}: {slot.preferredType} - {slot.preferredPattern}");
                }
            }
        }

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.3f, 0.3f, 0.8f);
        if (GUILayout.Button("Create Monster Group from Template", GUILayout.Height(35)))
        {
            // TODO: 몬스터 그룹 생성
            Debug.Log("Creating monster group from template: " + template.templateName);
        }
        GUI.backgroundColor = Color.white;
    }

    private void CreateCharacterFromTemplate(CharacterTemplate template)
    {
        // 이름 입력 다이얼로그
        string characterName = EditorInputDialog.Show("Create Character", "Enter character name:", "NewCharacter");

        if (string.IsNullOrEmpty(characterName)) return;

        // 캐릭터 생성
        var newCharacter = templateDatabase.CreateCharacterFromTemplate(template, characterName);
        newCharacter.name = characterName;

        // 다음 ID 할당
        string[] guids = AssetDatabase.FindAssets("t:BattleCharacterDataSO");
        int maxId = 999;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<BattleCharacterDataSO>(path);
            if (data != null && data.CharacterId > maxId && data.CharacterId < 2000)
            {
                maxId = data.CharacterId;
            }
        }

        // ID 설정 (리플렉션 사용)
        var idField = newCharacter.GetType().GetField("characterId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(newCharacter, maxId + 1);

        var nameField = newCharacter.GetType().GetField("characterName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nameField?.SetValue(newCharacter, characterName);

        // 저장 경로 수정
        string savePath = $"Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Characters/{characterName}_{maxId + 1}.asset";

        // 디렉토리 확인 및 생성
        string directory = System.IO.Path.GetDirectoryName(savePath);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(newCharacter, savePath);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success",
            $"Character '{characterName}' created successfully!\nID: {maxId + 1}", "OK");

        // 에디터 새로고침
        AssetDatabase.Refresh();
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

    private string GetMonsterIcon(BattleCharacterSystem.MonsterType type)
    {
        switch (type)
        {
            case BattleCharacterSystem.MonsterType.Normal: return "👾";
            case BattleCharacterSystem.MonsterType.Elite: return "👹";
            case BattleCharacterSystem.MonsterType.MiniBoss: return "🦾";
            case BattleCharacterSystem.MonsterType.Boss: return "👺";
            default: return "❓";
        }
    }
}

// 간단한 입력 다이얼로그
public class EditorInputDialog : EditorWindow
{
    private string inputText = "";
    private System.Action<string> callback;
    private string message;

    public static string Show(string title, string message, string defaultValue)
    {
        string result = defaultValue;
        var window = CreateInstance<EditorInputDialog>();
        window.titleContent = new GUIContent(title);
        window.message = message;
        window.inputText = defaultValue;
        window.minSize = new Vector2(300, 100);
        window.maxSize = new Vector2(300, 100);
        window.ShowModal();
        return window.inputText;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField(message);
        inputText = EditorGUILayout.TextField(inputText);

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("OK"))
            {
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                inputText = "";
                Close();
            }
        }
    }
}