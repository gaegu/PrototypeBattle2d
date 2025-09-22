using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR
/// <summary>
/// 진형 데이터를 쉽게 생성하는 도구
/// </summary>
public class FormationCreatorTool : MonoBehaviour
{
    private const string SAVE_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Formation";

    [MenuItem("*COSMOS*/Battle/Create All Formation Data")]
    public static void CreateAllFormationData()
    {
        // 디렉토리 확인/생성
        if (!AssetDatabase.IsValidFolder(SAVE_PATH))
        {
            string[] folders = SAVE_PATH.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
            AssetDatabase.Refresh();
        }

        // 4가지 진형 타입 생성
        CreateSingleFormation(FormationType.Offensive, "Offensive_Formation");
        CreateSingleFormation(FormationType.Defensive, "Defensive_Formation");
        CreateSingleFormation(FormationType.OffensiveBalance, "OffensiveBalance_Formation");
        CreateSingleFormation(FormationType.DefensiveBalance, "DefensiveBalance_Formation");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Formation data created at: {SAVE_PATH}");
        EditorUtility.DisplayDialog("Success", "All formation data created successfully!", "OK");
    }

    private static void CreateSingleFormation(FormationType type, string fileName)
    {
        string fullPath = $"{SAVE_PATH}/{fileName}.asset";

        // 기존 파일 체크
        if (AssetDatabase.LoadAssetAtPath<FormationData>(fullPath) != null)
        {
            if (!EditorUtility.DisplayDialog("Overwrite?",
                $"{fileName} already exists. Overwrite?",
                "Yes", "No"))
            {
                return;
            }
            AssetDatabase.DeleteAsset(fullPath);
        }

        FormationData formation = ScriptableObject.CreateInstance<FormationData>();

        // 기본 설정
        formation.formationType = type;
        formation.formationName = GetFormationName(type);
        formation.description = GetFormationDescription(type);
        formation.level = 1;
        formation.baseBuffPercentage = 10f;
        formation.frontBackDistance = 3f;
        formation.characterSpacing = 1.5f;
        formation.useCustomAttackPosition = false;

        // 아군/적군 위치 초기화
        formation.InitializePositions(true);  // 아군
        formation.InitializePositions(false); // 적군

        // 에셋 생성
        AssetDatabase.CreateAsset(formation, fullPath);

        Debug.Log($"Created: {fullPath}");
    }

    private static string GetFormationName(FormationType type)
    {
        switch (type)
        {
            case FormationType.Offensive: return "공격 진형";
            case FormationType.Defensive: return "방어 진형";
            case FormationType.OffensiveBalance: return "공격 밸런스 진형";
            case FormationType.DefensiveBalance: return "방어 밸런스 진형";
            default: return "Unknown";
        }
    }

    private static string GetFormationDescription(FormationType type)
    {
        switch (type)
        {
            case FormationType.Offensive:
                return "앞열 1명, 뒷열 4명 - 탱커가 앞에서 버티고 딜러들이 후방에서 공격";
            case FormationType.Defensive:
                return "앞열 4명, 뒷열 1명 - 방어에 특화되어 있으며 힐러를 보호";
            case FormationType.OffensiveBalance:
                return "앞열 2명, 뒷열 3명 - 공격과 방어의 균형";
            case FormationType.DefensiveBalance:
                return "앞열 3명, 뒷열 2명 - 방어 중심의 균형 진형";
            default: return "";
        }
    }

    [MenuItem("*COSMOS*/Battle/Create Battle Position Manager")]
    public static void CreateBattlePositionManager()
    {
        // BattlePositionNew GameObject 생성
        GameObject battlePosObj = new GameObject("BattlePositionNew");

        // BattlePositionNew 컴포넌트 추가
        var battlePos = battlePosObj.AddComponent<BattlePositionNew>();

        // Formation 데이터 자동 로드 시도
        LoadFormationData(battlePos);

        // 선택 및 포커스
        Selection.activeGameObject = battlePosObj;
        EditorUtility.FocusProjectWindow();

        Debug.Log("BattlePositionNew created with formations loaded.");
    }

    private static void LoadFormationData(BattlePositionNew battlePos)
    {
        // Formation 데이터 배열 가져오기
        var formationArray = new FormationData[4];

        string[] formationFiles = new string[]
        {
            "Offensive_Formation",
            "Defensive_Formation",
            "OffensiveBalance_Formation",
            "DefensiveBalance_Formation"
        };

        for (int i = 0; i < formationFiles.Length; i++)
        {
            string path = $"{SAVE_PATH}/{formationFiles[i]}.asset";
            var formation = AssetDatabase.LoadAssetAtPath<FormationData>(path);

            if (formation != null)
            {
                formationArray[i] = formation;
                Debug.Log($"Loaded: {formationFiles[i]}");
            }
            else
            {
                Debug.LogWarning($"Formation not found: {path}");
            }
        }

        // BattlePositionNew에 설정 (Reflection 사용)
        var type = battlePos.GetType();
        var field = type.GetField("formationDataList",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(battlePos, formationArray);
        }
    }

    [MenuItem("*COSMOS*/Battle/Validate All Formations")]
    public static void ValidateAllFormations()
    {
        string[] guids = AssetDatabase.FindAssets("t:FormationData", new[] { SAVE_PATH });
        int validCount = 0;
        int invalidCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FormationData formation = AssetDatabase.LoadAssetAtPath<FormationData>(path);

            if (formation != null)
            {
                bool isValid = ValidateFormation(formation);
                if (isValid)
                {
                    validCount++;
                    Debug.Log($"✓ Valid: {formation.name}");
                }
                else
                {
                    invalidCount++;
                    Debug.LogWarning($"✗ Invalid: {formation.name}");
                }
            }
        }

        EditorUtility.DisplayDialog("Validation Complete",
            $"Valid: {validCount}\nInvalid: {invalidCount}",
            "OK");
    }

    private static bool ValidateFormation(FormationData formation)
    {
        if (formation == null) return false;

        // 아군 위치 체크
        if (formation.allyPositions == null || formation.allyPositions.Length != 5)
        {
            Debug.LogWarning($"{formation.name}: Invalid ally positions array");
            return false;
        }

        // 적군 위치 체크
        if (formation.enemyPositions == null || formation.enemyPositions.Length != 5)
        {
            Debug.LogWarning($"{formation.name}: Invalid enemy positions array");
            return false;
        }

        // 각 위치 검증
        for (int i = 0; i < 5; i++)
        {
            if (formation.allyPositions[i] == null)
            {
                Debug.LogWarning($"{formation.name}: Null ally position at slot {i}");
                return false;
            }

            if (formation.enemyPositions[i] == null)
            {
                Debug.LogWarning($"{formation.name}: Null enemy position at slot {i}");
                return false;
            }
        }

        // 커스텀 공격 위치 체크
        if (formation.useCustomAttackPosition)
        {
            if (formation.customAttackPositions == null || formation.customAttackPositions.Length != 5)
            {
                Debug.LogWarning($"{formation.name}: Invalid custom attack positions");
                return false;
            }
        }

        return true;
    }

    [MenuItem("*COSMOS*/Battle/Migrate Old Formation Data")]
    public static void MigrateOldFormationData()
    {
        if (!EditorUtility.DisplayDialog("Migrate Formation Data",
            "This will convert old separate ally/enemy formations to the new unified format. Continue?",
            "Yes", "No"))
        {
            return;
        }

        int migratedCount = 0;

        // 기존 진형 파일들 찾기
        string[] guids = AssetDatabase.FindAssets("t:FormationData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FormationData oldFormation = AssetDatabase.LoadAssetAtPath<FormationData>(path);

            if (oldFormation != null && oldFormation.allyPositions == null)
            {
                // 구 버전 감지 (positions 필드만 있는 경우)
                MigrateFormation(oldFormation);
                migratedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Migration Complete",
            $"Migrated {migratedCount} formation(s) to new format.",
            "OK");
    }

    private static void MigrateFormation(FormationData formation)
    {
        // positions 필드에서 데이터 복사 (Reflection 사용)
        var type = formation.GetType();
        var positionsField = type.GetField("positions");

        if (positionsField != null)
        {
            var positions = positionsField.GetValue(formation) as FormationPosition[];

            if (positions != null)
            {
                // 새 배열 생성
                formation.allyPositions = new FormationPosition[5];
                formation.enemyPositions = new FormationPosition[5];

                // 기존 데이터를 아군으로 복사
                for (int i = 0; i < positions.Length && i < 5; i++)
                {
                    if (positions[i] != null)
                    {
                        formation.allyPositions[i] = new FormationPosition(
                            positions[i].slotIndex,
                            positions[i].isFrontRow,
                            positions[i].standPosition,
                            positions[i].attackPosition
                        );
                    }
                }

                // 적군 위치 자동 생성 (미러링)
                formation.InitializePositions(false);

                // 커스텀 공격 위치 초기화
                formation.customAttackPositions = new Vector3[5];
                formation.useCustomAttackPosition = false;

                EditorUtility.SetDirty(formation);
                Debug.Log($"Migrated: {formation.name}");
            }
        }
    }
}

/// <summary>
/// FormationPosition을 위한 간단한 Property Drawer
/// </summary>
[CustomPropertyDrawer(typeof(FormationPosition))]
public class FormationPositionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 한 줄로 표시
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // 필드들 가로로 배치
        float fieldWidth = position.width / 4;

        var slotRect = new Rect(position.x, position.y, 30, position.height);
        var frontRect = new Rect(position.x + 35, position.y, 50, position.height);
        var standRect = new Rect(position.x + 90, position.y, fieldWidth * 1.5f, position.height);
        var attackRect = new Rect(position.x + 90 + fieldWidth * 1.5f + 5, position.y, fieldWidth * 1.5f, position.height);

        EditorGUI.PropertyField(slotRect, property.FindPropertyRelative("slotIndex"), GUIContent.none);
        EditorGUI.PropertyField(frontRect, property.FindPropertyRelative("isFrontRow"), GUIContent.none);

        EditorGUI.LabelField(new Rect(standRect.x - 20, standRect.y, 20, standRect.height), "S:");
        EditorGUI.PropertyField(standRect, property.FindPropertyRelative("standPosition"), GUIContent.none);

        EditorGUI.LabelField(new Rect(attackRect.x - 20, attackRect.y, 20, attackRect.height), "A:");
        EditorGUI.PropertyField(attackRect, property.FindPropertyRelative("attackPosition"), GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
#endif