using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// 진형 데이터를 쉽게 생성하는 도구
/// </summary>
public class FormationCreatorTool : MonoBehaviour
{
    [MenuItem("Assets/Create/Battle/Create All Formation Data")]
    public static void CreateAllFormationData()
    {
        string path = EditorUtility.SaveFolderPanel("Save Formation Data", "Assets", "FormationData");

        if (string.IsNullOrEmpty(path))
            return;

        // Assets 상대 경로로 변환
        if (path.StartsWith(Application.dataPath))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);
        }

        // 아군 진형 생성
        CreateFormationSet(path, true);

        // 적군 진형 생성
        CreateFormationSet(path, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Formation data created at: {path}");
        EditorUtility.DisplayDialog("Success", "All formation data created successfully!", "OK");
    }

    private static void CreateFormationSet(string basePath, bool isAlly)
    {
        string prefix = isAlly ? "Ally" : "Enemy";

        // 4가지 진형 타입 생성
        CreateSingleFormation(basePath, FormationType.Offensive, $"{prefix}_Offensive", isAlly);
        CreateSingleFormation(basePath, FormationType.Defensive, $"{prefix}_Defensive", isAlly);
        CreateSingleFormation(basePath, FormationType.OffensiveBalance, $"{prefix}_OffensiveBalance", isAlly);
        CreateSingleFormation(basePath, FormationType.DefensiveBalance, $"{prefix}_DefensiveBalance", isAlly);
    }

    private static void CreateSingleFormation(string path, FormationType type, string fileName, bool isAlly)
    {
        FormationData formation = ScriptableObject.CreateInstance<FormationData>();

        // 기본 설정
        formation.formationType = type;
        formation.formationName = GetFormationName(type);
        formation.description = $"{GetFormationName(type)} - {(isAlly ? "아군" : "적군")}";
        formation.level = 1;
        formation.baseBuffPercentage = 10f;
        formation.frontBackDistance = 3f;
        formation.characterSpacing = 1.5f;

        // 위치 자동 초기화
        formation.InitializePositions(isAlly);

        // 에셋 생성
        string fullPath = $"{path}/{fileName}.asset";
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

    [MenuItem("GameObject/Battle/Create Battle Position with Formations")]
    public static void CreateBattlePositionWithFormations()
    {
        // BattlePositionNew GameObject 생성
        GameObject battlePosObj = new GameObject("BattlePositionNew");

        // BattlePositionNew 컴포넌트 추가
        var battlePos = battlePosObj.AddComponent<BattlePositionNew>();


        // 선택 및 포커스
        Selection.activeGameObject = battlePosObj;
        EditorUtility.FocusProjectWindow();

        Debug.Log("BattlePositionNew created. Please assign FormationData assets to the arrays.");
        EditorUtility.DisplayDialog("Setup Required",
            "BattlePositionNew GameObject created.\n\n" +
            "Please:\n" +
            "1. Create FormationData assets using Assets menu\n" +
            "2. Assign them to Ally/Enemy Formation Data Lists",
            "OK");
    }
}

/// <summary>
/// FormationData를 위한 간단한 Property Drawer
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
        var slotRect = new Rect(position.x, position.y, 30, position.height);
        var frontRect = new Rect(position.x + 35, position.y, 50, position.height);
        var standRect = new Rect(position.x + 90, position.y, (position.width - 180) / 2, position.height);
        var attackRect = new Rect(position.x + 90 + (position.width - 180) / 2 + 5, position.y, (position.width - 180) / 2, position.height);

        EditorGUI.PropertyField(slotRect, property.FindPropertyRelative("slotIndex"), GUIContent.none);
        EditorGUI.PropertyField(frontRect, property.FindPropertyRelative("isFrontRow"), GUIContent.none);
        EditorGUI.PropertyField(standRect, property.FindPropertyRelative("standPosition"), GUIContent.none);
        EditorGUI.PropertyField(attackRect, property.FindPropertyRelative("attackPosition"), GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
#endif