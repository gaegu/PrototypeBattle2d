using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
/// <summary>
/// 진형 데이터를 쉽게 생성하고 편집할 수 있는 에디터 도구
/// </summary>
public class FormationDataEditor : EditorWindow
{
    private FormationData currentFormation;
    private FormationType selectedType = FormationType.OffensiveBalance;
    private bool isAllyFormation = true;
    private Vector2 scrollPos;

    // 프리셋 값들
    private readonly Dictionary<FormationType, string> formationNames = new Dictionary<FormationType, string>
    {
        { FormationType.Offensive, "공격 진형" },
        { FormationType.Defensive, "방어 진형" },
        { FormationType.OffensiveBalance, "공격 밸런스 진형" },
        { FormationType.DefensiveBalance, "방어 밸런스 진형" }
    };

    [MenuItem("Tools/Battle/Formation Editor")]
    public static void ShowWindow()
    {
        GetWindow<FormationDataEditor>("Formation Editor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("진형 데이터 에디터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 진형 타입 선택
        selectedType = (FormationType)EditorGUILayout.EnumPopup("진형 타입", selectedType);
        isAllyFormation = EditorGUILayout.Toggle("아군 진형", isAllyFormation);

        EditorGUILayout.Space();

        // 현재 진형 데이터
        currentFormation = EditorGUILayout.ObjectField("편집할 진형 데이터",
            currentFormation, typeof(FormationData), false) as FormationData;

        EditorGUILayout.Space();

        // 버튼들
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("새 진형 데이터 생성", GUILayout.Height(30)))
        {
            CreateNewFormation();
        }

        if (currentFormation != null && GUILayout.Button("자동 위치 설정", GUILayout.Height(30)))
        {
            AutoSetupPositions();
        }

        EditorGUILayout.EndHorizontal();

        if (currentFormation != null)
        {
            EditorGUILayout.Space();
            DrawFormationEditor();
        }

        EditorGUILayout.EndScrollView();
    }

    private void CreateNewFormation()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Formation Data",
            $"{selectedType}_{(isAllyFormation ? "Ally" : "Enemy")}",
            "asset",
            "Enter a file name to save the formation data."
        );

        if (!string.IsNullOrEmpty(path))
        {
            FormationData newFormation = ScriptableObject.CreateInstance<FormationData>();
            newFormation.formationType = selectedType;
            newFormation.formationName = formationNames[selectedType];
            newFormation.description = $"{formationNames[selectedType]} - {(isAllyFormation ? "아군" : "적군")}";
            newFormation.level = 1;
            newFormation.baseBuffPercentage = 10f;

            // 위치 자동 초기화
            newFormation.InitializePositions(isAllyFormation);

            AssetDatabase.CreateAsset(newFormation, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            currentFormation = newFormation;
            Selection.activeObject = newFormation;

            Debug.Log($"Created new formation: {path}");
        }
    }

    private void AutoSetupPositions()
    {
        if (currentFormation == null) return;

        currentFormation.formationType = selectedType;
        currentFormation.InitializePositions(isAllyFormation);
        EditorUtility.SetDirty(currentFormation);

        Debug.Log($"Auto-setup positions for {selectedType}");
    }

    private void DrawFormationEditor()
    {
        EditorGUILayout.LabelField("진형 설정", EditorStyles.boldLabel);

        // 기본 정보
        currentFormation.formationName = EditorGUILayout.TextField("진형 이름", currentFormation.formationName);
        currentFormation.description = EditorGUILayout.TextField("설명", currentFormation.description);
        currentFormation.level = EditorGUILayout.IntSlider("레벨", currentFormation.level, 1, 10);
        currentFormation.baseBuffPercentage = EditorGUILayout.Slider("기본 버프 %", currentFormation.baseBuffPercentage, 0, 50);

        EditorGUILayout.Space();

        // 간격 조정
        EditorGUILayout.LabelField("간격 설정", EditorStyles.boldLabel);
        currentFormation.frontBackDistance = EditorGUILayout.Slider("앞/뒷열 간격", currentFormation.frontBackDistance, 1f, 10f);
        currentFormation.characterSpacing = EditorGUILayout.Slider("캐릭터 간격", currentFormation.characterSpacing, 0.5f, 10f);

        EditorGUILayout.Space();

        // 각 슬롯별 위치 편집
        EditorGUILayout.LabelField("슬롯별 위치 설정", EditorStyles.boldLabel);

        if (currentFormation.positions == null || currentFormation.positions.Length != 5)
        {
            currentFormation.positions = new FormationPosition[5];
        }

        for (int i = 0; i < 5; i++)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"슬롯 {i}", EditorStyles.boldLabel);

            if (currentFormation.positions[i] == null)
            {
                currentFormation.positions[i] = new FormationPosition(i, false, Vector3.zero, Vector3.zero);
            }

            var pos = currentFormation.positions[i];

            pos.isFrontRow = EditorGUILayout.Toggle("앞열", pos.isFrontRow);
            pos.standPosition = EditorGUILayout.Vector3Field("대기 위치", pos.standPosition);
            pos.attackPosition = EditorGUILayout.Vector3Field("공격 위치", pos.attackPosition);

            EditorGUILayout.EndVertical();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(currentFormation);
        }
    }
}

/// <summary>
/// FormationData의 커스텀 인스펙터
/// </summary>
[CustomEditor(typeof(FormationData))]
public class FormationDataInspector : Editor
{
    private FormationData formation;
    private bool showPositions = true;
    private bool previewInScene = true;

    private void OnEnable()
    {
        formation = (FormationData)target;
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("진형 데이터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 기본 정보
        formation.formationType = (FormationType)EditorGUILayout.EnumPopup("진형 타입", formation.formationType);
        formation.formationName = EditorGUILayout.TextField("진형 이름", formation.formationName);
        formation.description = EditorGUILayout.TextArea(formation.description, GUILayout.Height(50));

        EditorGUILayout.Space();

        // 진형 스탯
        EditorGUILayout.LabelField("진형 효과", EditorStyles.boldLabel);
        formation.level = EditorGUILayout.IntSlider("레벨", formation.level, 1, 10);
        formation.baseBuffPercentage = EditorGUILayout.Slider("기본 버프 %", formation.baseBuffPercentage, 0, 50);

        float actualBuff = formation.GetActualBuffPercentage();
        EditorGUILayout.HelpBox($"실제 버프: {actualBuff:F1}% (기본 {formation.baseBuffPercentage}% + 레벨 보너스 {(formation.level - 1) * 2}%)",
            MessageType.Info);

        EditorGUILayout.Space();

        // 간격 설정
        EditorGUILayout.LabelField("위치 설정", EditorStyles.boldLabel);
        formation.frontBackDistance = EditorGUILayout.Slider("앞/뒷열 간격", formation.frontBackDistance, 1f, 10f);
        formation.characterSpacing = EditorGUILayout.Slider("캐릭터 간격", formation.characterSpacing, 0.5f, 10f);

        EditorGUILayout.Space();

        // 자동 설정 버튼
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("아군 위치 자동 설정"))
        {
            formation.InitializePositions(true);
            EditorUtility.SetDirty(formation);
        }
        if (GUILayout.Button("적군 위치 자동 설정"))
        {
            formation.InitializePositions(false);
            EditorUtility.SetDirty(formation);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 슬롯별 위치 (접기/펼치기)
        showPositions = EditorGUILayout.Foldout(showPositions, "슬롯별 상세 위치");
        if (showPositions)
        {
            if (formation.positions == null || formation.positions.Length != 5)
            {
                formation.positions = new FormationPosition[5];
            }

            for (int i = 0; i < 5; i++)
            {
                EditorGUILayout.BeginVertical("box");

                string rowText = formation.positions[i]?.isFrontRow == true ? "[앞열]" : "[뒷열]";
                EditorGUILayout.LabelField($"슬롯 {i} {rowText}", EditorStyles.boldLabel);

                if (formation.positions[i] == null)
                {
                    formation.positions[i] = new FormationPosition(i, false, Vector3.zero, Vector3.zero);
                }

                var pos = formation.positions[i];

                EditorGUI.indentLevel++;
                pos.isFrontRow = EditorGUILayout.Toggle("앞열 배치", pos.isFrontRow);
                pos.standPosition = EditorGUILayout.Vector3Field("대기 위치", pos.standPosition);
                pos.attackPosition = EditorGUILayout.Vector3Field("공격 위치", pos.attackPosition);
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space();

        // Scene 뷰 미리보기
        previewInScene = EditorGUILayout.Toggle("Scene에서 미리보기", previewInScene);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(formation);
        }
    }

    private void OnSceneGUI()
    {
        if (!previewInScene || formation == null || formation.positions == null) return;

        // Scene 뷰에서 위치 시각화
        for (int i = 0; i < formation.positions.Length; i++)
        {
            var pos = formation.positions[i];
            if (pos == null) continue;

            // 대기 위치
            Handles.color = pos.isFrontRow ? Color.red : Color.blue;
            Handles.DrawWireCube(pos.standPosition, Vector3.one * 0.5f);
            Handles.Label(pos.standPosition + Vector3.up * 0.5f, $"S{i}");

            // 공격 위치
            Handles.color = Color.yellow;
            Handles.DrawWireCube(pos.attackPosition, Vector3.one * 0.3f);

            // 연결선
            Handles.color = Color.green;
            Handles.DrawLine(pos.standPosition, pos.attackPosition);
        }
    }
}
#endif