using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
/// <summary>
/// 진형 데이터를 쉽게 생성하고 편집할 수 있는 에디터 도구
/// </summary>
public class FormationDataEditor : EditorWindow
{
    // 기본 설정
    private const string SAVE_PATH = "Assets/Cosmos/ResourcesAddressable/ScriptableObjects/Formation";
    private const string VLACKY_PREFAB_PATH = "Assets/Cosmos/ResourcesAddressable/Characters/Vlacky/Prefabs/Vlacky_Battle_Type01.prefab";

    private FormationData currentFormation;
    private FormationType selectedType = FormationType.OffensiveBalance;
    private Vector2 scrollPos;

    // 탭 선택
    private int selectedTab = 0;
    private string[] tabNames = { "아군 설정", "적군 설정", "공통 설정", "프리뷰" };

    // 프리뷰 관련
    private bool showPreview = true;
    private bool animatePreview = false;
    private float previewScale = 1f;
    private GameObject vlackyPrefab;
    private List<GameObject> previewObjects = new List<GameObject>();

    // 편집 모드
    private bool isEditingAlly = true;
    private int selectedSlotIndex = -1;

    // 2D 뷰어
    private Rect formationViewRect;
    private Vector2 viewerCenter = new Vector2(400, 300);
    private float gridSize = 30f;

    // 드래그 관련
    private bool isDragging = false;
    private int draggingSlotIndex = -1;
    private bool draggingIsAlly = false;
    private Vector2 dragOffset = Vector2.zero;

    // 스냅 설정
    private bool enableSnap = true;
    private float snapValue = 0.01f;

    // 간격 설정 캐시 (실시간 비교용)
    private float lastBaseDistance = -1f;  // 추가
    private float lastFrontBackDistance = -1f;
    private float lastCharacterSpacing = -1f;
    private FormationType lastFormationType = FormationType.Offensive;

    // 좌표 표시 설정
    private bool showAllPositions = true;
    private bool showOnlySelectedPosition = false;

    // 프리셋 값들
    private readonly Dictionary<FormationType, string> formationNames = new Dictionary<FormationType, string>
    {
        { FormationType.Offensive, "공격 진형" },
        { FormationType.Defensive, "방어 진형" },
        { FormationType.OffensiveBalance, "공격 밸런스 진형" },
        { FormationType.DefensiveBalance, "방어 밸런스 진형" }
    };

    [MenuItem("*COSMOS*/Battle/전투 포메이션 Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<FormationDataEditor>("Formation Editor");
        window.minSize = new Vector2(800, 600);
        window.LoadVlackyPrefab();
    }

    private void OnEnable()
    {
        LoadVlackyPrefab();
    }

    private void OnDisable()
    {
        ClearPreviewObjects();
    }

    private void LoadVlackyPrefab()
    {
        vlackyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VLACKY_PREFAB_PATH);
        if (vlackyPrefab == null)
        {
            Debug.LogWarning($"Vlacky prefab not found at {VLACKY_PREFAB_PATH}");
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        // 왼쪽 패널 - 설정
        EditorGUILayout.BeginVertical(GUILayout.Width(350));
        DrawLeftPanel();
        EditorGUILayout.EndVertical();

        // 구분선
        GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

        // 오른쪽 패널 - 2D 뷰어
        EditorGUILayout.BeginVertical();
        DrawRightPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.LabelField("진형 데이터 에디터", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 진형 선택
        DrawFormationSelection();
        EditorGUILayout.Space();

        // 탭 선택
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
        EditorGUILayout.Space();

        // 탭별 컨텐츠
        if (currentFormation != null)
        {
            switch (selectedTab)
            {
                case 0: DrawAllySettings(); break;
                case 1: DrawEnemySettings(); break;
                case 2: DrawCommonSettings(); break;
                case 3: DrawPreviewSettings(); break;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.LabelField("2D 진형 뷰어", EditorStyles.boldLabel);

        // 뷰어 설정
        EditorGUILayout.BeginHorizontal();

        // 스냅 설정
        enableSnap = EditorGUILayout.Toggle("스냅", enableSnap, GUILayout.Width(50));
        if (enableSnap)
        {
            snapValue = EditorGUILayout.Slider("", snapValue, 0.01f, 1f, GUILayout.Width(100));
            EditorGUILayout.LabelField("단위", GUILayout.Width(30));
        }

        GUILayout.FlexibleSpace();

        // 좌표 표시 설정
        showAllPositions = EditorGUILayout.Toggle("전체 좌표", showAllPositions, GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();

        // 2D 뷰어 영역
        formationViewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        GUI.Box(formationViewRect, "");

        if (currentFormation != null)
        {
            Draw2DFormationViewer();

            // 간격 변경 감지 및 자동 적용
            CheckAndApplySpacingChanges();
        }
        else
        {
            GUI.Label(formationViewRect, "진형을 선택하세요", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void CheckAndApplySpacingChanges()
    {
        if (currentFormation == null) return;

        bool needsUpdate = false;

        // 기본 거리 변경 체크 (추가)
        if (!Mathf.Approximately(lastBaseDistance, currentFormation.baseDistance))
        {
            lastBaseDistance = currentFormation.baseDistance;
            needsUpdate = true;
        }

        // 진형 타입 변경 체크
        if (lastFormationType != currentFormation.formationType)
        {
            lastFormationType = currentFormation.formationType;
            needsUpdate = true;
        }

        // 앞뒤 간격 변경 체크
        if (!Mathf.Approximately(lastFrontBackDistance, currentFormation.frontBackDistance))
        {
            lastFrontBackDistance = currentFormation.frontBackDistance;
            needsUpdate = true;
        }

        // 캐릭터 간격 변경 체크
        if (!Mathf.Approximately(lastCharacterSpacing, currentFormation.characterSpacing))
        {
            lastCharacterSpacing = currentFormation.characterSpacing;
            needsUpdate = true;
        }

        // 변경사항이 있으면 자동 업데이트
        if (needsUpdate)
        {
            // 현재 진형 타입으로 재초기화
            currentFormation.formationType = lastFormationType;
            currentFormation.InitializePositions(true);  // 아군
            currentFormation.InitializePositions(false); // 적군

            EditorUtility.SetDirty(currentFormation);
            Repaint();

            Debug.Log($"Formation positions updated - Base: {lastBaseDistance:F2}, FrontBack: {lastFrontBackDistance:F2}, Spacing: {lastCharacterSpacing:F2}");
        }
    }

    private void DrawFormationSelection()
    {
        EditorGUILayout.LabelField("진형 선택", EditorStyles.boldLabel);

        // 진형 타입 선택
        selectedType = (FormationType)EditorGUILayout.EnumPopup("진형 타입", selectedType);

        // 현재 진형 데이터
        currentFormation = EditorGUILayout.ObjectField("편집할 진형",
            currentFormation, typeof(FormationData), false) as FormationData;

        EditorGUILayout.Space();

        // 버튼들
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("새로 만들기", GUILayout.Height(25)))
        {
            CreateNewFormation();
        }

        if (GUILayout.Button("기존 불러오기", GUILayout.Height(25)))
        {
            LoadExistingFormation();
        }

        EditorGUILayout.EndHorizontal();

        if (currentFormation != null)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("자동 설정", GUILayout.Height(25)))
            {
                AutoSetupAllPositions();
            }

            if (GUILayout.Button("저장", GUILayout.Height(25)))
            {
                SaveFormation();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawAllySettings()
    {
        EditorGUILayout.LabelField("아군 위치 설정", EditorStyles.boldLabel);
        isEditingAlly = true;
        DrawPositionSettings(currentFormation.allyPositions, true);
    }

    private void DrawEnemySettings()
    {
        EditorGUILayout.LabelField("적군 위치 설정", EditorStyles.boldLabel);
        isEditingAlly = false;
        DrawPositionSettings(currentFormation.enemyPositions, false);
    }

    private void DrawPositionSettings(FormationPosition[] positions, bool isAlly)
    {
        if (positions == null || positions.Length != 5)
        {
            if (GUILayout.Button("위치 배열 초기화"))
            {
                if (isAlly)
                    currentFormation.allyPositions = new FormationPosition[5];
                else
                    currentFormation.enemyPositions = new FormationPosition[5];
                currentFormation.InitializePositions(isAlly);
                EditorUtility.SetDirty(currentFormation);
            }
            return;
        }

        // 슬롯 순서 변경을 위한 임시 리스트
        List<FormationPosition> reorderedPositions = null;
        int moveFromIndex = -1;
        int moveToIndex = -1;

        for (int i = 0; i < 5; i++)
        {
            EditorGUILayout.BeginVertical("box");

            // 슬롯 헤더
            EditorGUILayout.BeginHorizontal();

            // 슬롯 순서 변경 버튼 추가
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(20)))
            {
                moveFromIndex = i;
                moveToIndex = i - 1;
            }
            GUI.enabled = i < 4;
            if (GUILayout.Button("▼", GUILayout.Width(20)))
            {
                moveFromIndex = i;
                moveToIndex = i + 1;
            }
            GUI.enabled = true;

            // 앞/뒷열 표시
            string rowIndicator = positions[i]?.isFrontRow == true ? "[앞]" : "[뒤]";
            EditorGUILayout.LabelField($"슬롯 {i} {rowIndicator}", EditorStyles.boldLabel, GUILayout.Width(80));

            if (selectedSlotIndex == i && isEditingAlly == isAlly)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("[선택됨]", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("선택", GUILayout.Width(50)))
            {
                selectedSlotIndex = i;
                isEditingAlly = isAlly;
            }
            EditorGUILayout.EndHorizontal();

            if (positions[i] == null)
            {
                positions[i] = new FormationPosition(i, false, Vector3.zero, Vector3.zero);
            }

            var pos = positions[i];

            // 슬롯 인덱스 수정 기능 추가
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("슬롯 인덱스:", GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            int newSlotIndex = EditorGUILayout.IntField(pos.slotIndex, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck())
            {
                // 인덱스 범위 체크
                newSlotIndex = Mathf.Clamp(newSlotIndex, 0, 4);

                // 중복 체크
                bool isDuplicate = false;
                for (int j = 0; j < positions.Length; j++)
                {
                    if (j != i && positions[j] != null && positions[j].slotIndex == newSlotIndex)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    pos.slotIndex = newSlotIndex;
                    EditorUtility.SetDirty(currentFormation);
                }
                else
                {
                    EditorUtility.DisplayDialog("중복 경고",
                        $"슬롯 인덱스 {newSlotIndex}는 이미 사용중입니다.", "확인");
                }
            }

            // 자동 정렬 버튼
            if (GUILayout.Button("자동정렬", GUILayout.Width(60)))
            {
                for (int j = 0; j < positions.Length; j++)
                {
                    if (positions[j] != null)
                    {
                        positions[j].slotIndex = j;
                    }
                }
                EditorUtility.SetDirty(currentFormation);
            }

            EditorGUILayout.EndHorizontal();

            // 앞열/뒷열 토글
            EditorGUI.BeginChangeCheck();
            pos.isFrontRow = EditorGUILayout.Toggle("앞열", pos.isFrontRow);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(currentFormation);
            }

            // 대기 위치 필드를 더 정밀하게 표시
            EditorGUI.BeginChangeCheck();
            Vector3 newStandPos = pos.standPosition;

            // 각 축별로 소수점 2자리 필드 제공 (옵션)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("대기 위치", GUILayout.Width(70));
            newStandPos.x = EditorGUILayout.FloatField(Mathf.Round(newStandPos.x * 100f) / 100f, GUILayout.Width(60));
            newStandPos.y = EditorGUILayout.FloatField(Mathf.Round(newStandPos.y * 100f) / 100f, GUILayout.Width(60));
            newStandPos.z = EditorGUILayout.FloatField(Mathf.Round(newStandPos.z * 100f) / 100f, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                pos.standPosition = enableSnap ? SnapVector3(newStandPos) : newStandPos;
                EditorUtility.SetDirty(currentFormation);
            }

            // 커스텀 공격 위치
            if (currentFormation.useCustomAttackPosition)
            {
                EditorGUILayout.LabelField("커스텀 공격 위치:");
                EditorGUI.BeginChangeCheck();
                Vector3 newAttackPos = EditorGUILayout.Vector3Field("", currentFormation.customAttackPositions[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    currentFormation.customAttackPositions[i] = enableSnap ? SnapVector3(newAttackPos) : newAttackPos;
                    EditorUtility.SetDirty(currentFormation);
                }
            }

            EditorGUILayout.EndVertical();
        }

        // 슬롯 순서 변경 처리
        if (moveFromIndex >= 0 && moveToIndex >= 0)
        {
            FormationPosition temp = positions[moveFromIndex];
            positions[moveFromIndex] = positions[moveToIndex];
            positions[moveToIndex] = temp;

            // 선택된 슬롯도 함께 이동
            if (selectedSlotIndex == moveFromIndex && isEditingAlly == isAlly)
            {
                selectedSlotIndex = moveToIndex;
            }
            else if (selectedSlotIndex == moveToIndex && isEditingAlly == isAlly)
            {
                selectedSlotIndex = moveFromIndex;
            }

            EditorUtility.SetDirty(currentFormation);
        }

        // 전체 슬롯 인덱스 검증 버튼 추가
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("슬롯 인덱스 검증", GUILayout.Height(25)))
        {
            ValidateSlotIndices(positions, isAlly);
        }

        if (GUILayout.Button("인덱스 리셋 (0-4)", GUILayout.Height(25)))
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (positions[i] != null)
                {
                    positions[i].slotIndex = i;
                }
            }
            EditorUtility.SetDirty(currentFormation);
            Debug.Log($"{(isAlly ? "아군" : "적군")} 슬롯 인덱스가 0-4로 리셋되었습니다.");
        }

        EditorGUILayout.EndHorizontal();
    }


    // 슬롯 인덱스 검증 메서드 추가
    private void ValidateSlotIndices(FormationPosition[] positions, bool isAlly)
    {
        HashSet<int> usedIndices = new HashSet<int>();
        List<int> duplicates = new List<int>();
        List<int> outOfRange = new List<int>();

        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i] == null) continue;

            int index = positions[i].slotIndex;

            // 범위 체크
            if (index < 0 || index > 4)
            {
                outOfRange.Add(i);
            }

            // 중복 체크
            if (usedIndices.Contains(index))
            {
                duplicates.Add(index);
            }
            else
            {
                usedIndices.Add(index);
            }
        }

        string message = $"{(isAlly ? "아군" : "적군")} 슬롯 검증 결과:\n";
        bool hasError = false;

        if (duplicates.Count > 0)
        {
            message += $"중복된 인덱스: {string.Join(", ", duplicates)}\n";
            hasError = true;
        }

        if (outOfRange.Count > 0)
        {
            message += $"범위 벗어남 (위치 {string.Join(", ", outOfRange)})\n";
            hasError = true;
        }

        if (!hasError)
        {
            message += "문제 없음!";
        }

        EditorUtility.DisplayDialog("슬롯 인덱스 검증", message, "확인");
    }


    private void DrawCommonSettings()
    {
        EditorGUILayout.LabelField("공통 설정", EditorStyles.boldLabel);

        // 기본 정보
        currentFormation.formationName = EditorGUILayout.TextField("진형 이름", currentFormation.formationName);
        currentFormation.description = EditorGUILayout.TextArea(currentFormation.description, GUILayout.Height(50));
        currentFormation.level = EditorGUILayout.IntSlider("레벨", currentFormation.level, 1, 10);
        currentFormation.baseBuffPercentage = EditorGUILayout.Slider("기본 버프 %", currentFormation.baseBuffPercentage, 0, 50);

        float actualBuff = currentFormation.GetActualBuffPercentage();
        EditorGUILayout.HelpBox($"실제 버프: {actualBuff:F1}%", MessageType.Info);

        EditorGUILayout.Space();

        // 간격 설정 (실시간 적용)
        EditorGUILayout.LabelField("간격 설정", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        // 진형 타입 변경
        currentFormation.formationType = (FormationType)EditorGUILayout.EnumPopup("진형 타입", currentFormation.formationType);

        // 중앙으로부터의 거리 (추가)
        currentFormation.baseDistance = EditorGUILayout.Slider("중앙 기본 거리", currentFormation.baseDistance, 1f, 10f);

        // 간격 슬라이더
        currentFormation.frontBackDistance = EditorGUILayout.Slider("앞/뒷열 간격", currentFormation.frontBackDistance, 0.5f, 5f);
        currentFormation.characterSpacing = EditorGUILayout.Slider("캐릭터 간격", currentFormation.characterSpacing, 0.5f, 5f);

        if (EditorGUI.EndChangeCheck())
        {
            // 변경 즉시 적용 (CheckAndApplySpacingChanges에서 처리됨)
            EditorUtility.SetDirty(currentFormation);
        }

        // 리셋 버튼
        if (GUILayout.Button("기본값으로 리셋", GUILayout.Height(25)))
        {
            currentFormation.baseDistance = 2.5f;  // 추가
            currentFormation.frontBackDistance = 3f;
            currentFormation.characterSpacing = 1.5f;
            currentFormation.InitializePositions(true);
            currentFormation.InitializePositions(false);
            EditorUtility.SetDirty(currentFormation);
        }

        EditorGUILayout.Space();

        // 공격 위치 설정
        EditorGUILayout.LabelField("공격 위치 설정", EditorStyles.boldLabel);
        currentFormation.useCustomAttackPosition = EditorGUILayout.Toggle("커스텀 공격 위치 사용", currentFormation.useCustomAttackPosition);

        if (!currentFormation.useCustomAttackPosition)
        {
            EditorGUILayout.HelpBox("모든 유닛이 (0,0,0)으로 중앙 집결합니다", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("각 슬롯별로 개별 공격 위치를 설정할 수 있습니다", MessageType.Info);
        }
    }

    private void DrawPreviewSettings()
    {
        EditorGUILayout.LabelField("프리뷰 설정", EditorStyles.boldLabel);

        showPreview = EditorGUILayout.Toggle("Scene 프리뷰 표시", showPreview);
        animatePreview = EditorGUILayout.Toggle("애니메이션 프리뷰", animatePreview);
        previewScale = EditorGUILayout.Slider("프리뷰 크기", previewScale, 0.5f, 2f);

        EditorGUILayout.Space();

        if (GUILayout.Button("Vlacky 프리뷰 생성"))
        {
            CreateVlackyPreview();
        }

        if (GUILayout.Button("프리뷰 제거"))
        {
            ClearPreviewObjects();
        }
    }

    private void Draw2DFormationViewer()
    {
        var e = Event.current;
        var center = new Vector2(formationViewRect.center.x, formationViewRect.center.y);

        // 그리드 그리기
        DrawGrid(center);

        // 중앙점 표시
        DrawCenterPoint(center);

        // 진형 영역 구분선
        DrawFormationAreas(center);

        // 아군 진형 그리기 (오른쪽)
        DrawFormation2D(currentFormation.allyPositions, true, center);

        // 적군 진형 그리기 (왼쪽)
        DrawFormation2D(currentFormation.enemyPositions, false, center);

        // 마우스 이벤트 처리
        HandleMouseEvents(e, center);
    }

    private void DrawFormationAreas(Vector2 center)
    {
        // 아군 영역 (오른쪽)
        Handles.color = new Color(0, 0, 1, 0.1f);
        Rect allyArea = new Rect(center.x, formationViewRect.yMin, formationViewRect.width / 2, formationViewRect.height);
        Handles.DrawSolidRectangleWithOutline(allyArea, new Color(0, 0, 1, 0.05f), Color.clear);

        // 적군 영역 (왼쪽)
        Handles.color = new Color(1, 0, 0, 0.1f);
        Rect enemyArea = new Rect(formationViewRect.xMin, formationViewRect.yMin, formationViewRect.width / 2, formationViewRect.height);
        Handles.DrawSolidRectangleWithOutline(enemyArea, new Color(1, 0, 0, 0.05f), Color.clear);

        // 중앙선
        Handles.color = new Color(1, 1, 1, 0.3f);
        Handles.DrawLine(new Vector3(center.x, formationViewRect.yMin), new Vector3(center.x, formationViewRect.yMax));

        // 영역 라벨
        GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(center.x + 50, formationViewRect.yMin + 10, 100, 20), "아군", labelStyle);
        GUI.Label(new Rect(center.x - 150, formationViewRect.yMin + 10, 100, 20), "적군", labelStyle);
    }

    private void HandleMouseEvents(Event e, Vector2 center)
    {
        if (!formationViewRect.Contains(e.mousePosition))
            return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    HandleMouseDown(e.mousePosition, center);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 0 && isDragging)
                {
                    HandleMouseDrag(e.mousePosition, center);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0 && isDragging)
                {
                    HandleMouseUp();
                    e.Use();
                }
                break;
        }
    }

    private void HandleMouseDown(Vector2 mousePos, Vector2 center)
    {
        float minDistance = float.MaxValue;
        int closestSlot = -1;
        bool closestIsAlly = false;

        CheckSlotsForClick(currentFormation.allyPositions, true, mousePos, center,
            ref minDistance, ref closestSlot, ref closestIsAlly);

        CheckSlotsForClick(currentFormation.enemyPositions, false, mousePos, center,
            ref minDistance, ref closestSlot, ref closestIsAlly);

        if (closestSlot >= 0 && minDistance < 30f)
        {
            isDragging = true;
            draggingSlotIndex = closestSlot;
            draggingIsAlly = closestIsAlly;
            selectedSlotIndex = closestSlot;
            isEditingAlly = closestIsAlly;

            var positions = draggingIsAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;
            Vector2 slotViewPos = WorldToViewerPosition(positions[closestSlot].standPosition, center);
            dragOffset = slotViewPos - mousePos;

            Repaint();
        }
    }

    private void CheckSlotsForClick(FormationPosition[] positions, bool isAlly, Vector2 mousePos, Vector2 center,
        ref float minDistance, ref int closestSlot, ref bool closestIsAlly)
    {
        if (positions == null) return;

        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i] == null) continue;

            Vector2 slotViewPos = WorldToViewerPosition(positions[i].standPosition, center);
            float distance = Vector2.Distance(mousePos, slotViewPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestSlot = i;
                closestIsAlly = isAlly;
            }
        }
    }

    private void HandleMouseDrag(Vector2 mousePos, Vector2 center)
    {
        if (!isDragging || draggingSlotIndex < 0) return;

        Vector2 newViewPos = mousePos + dragOffset;
        Vector3 newWorldPos = ViewerToWorldPosition(newViewPos, center);

        if (enableSnap)
        {
            newWorldPos = SnapVector3(newWorldPos);
        }

        var positions = draggingIsAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;
        if (positions != null && draggingSlotIndex < positions.Length && positions[draggingSlotIndex] != null)
        {
            positions[draggingSlotIndex].standPosition = newWorldPos;
            EditorUtility.SetDirty(currentFormation);
        }

        Repaint();
    }

    private void HandleMouseUp()
    {
        isDragging = false;
        draggingSlotIndex = -1;
        Repaint();
    }

    private Vector3 SnapVector3(Vector3 value)
    {
        float snap = snapValue;
        return new Vector3(
            Mathf.Round(value.x / snap) * snap,
            Mathf.Round(value.y / snap) * snap,
            Mathf.Round(value.z / snap) * snap
        );
    }

    private void DrawGrid(Vector2 center)
    {
        Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);

        for (float x = formationViewRect.xMin; x < formationViewRect.xMax; x += gridSize)
        {
            Handles.DrawLine(new Vector3(x, formationViewRect.yMin), new Vector3(x, formationViewRect.yMax));
        }

        for (float y = formationViewRect.yMin; y < formationViewRect.yMax; y += gridSize)
        {
            Handles.DrawLine(new Vector3(formationViewRect.xMin, y), new Vector3(formationViewRect.xMax, y));
        }

        if (enableSnap)
        {
            // 0.1 단위 그리드 (연한 색)
            if (snapValue <= 0.1f)
            {
                Handles.color = new Color(0.25f, 0.25f, 0.25f, 0.15f);
                float grid01 = gridSize * 0.1f;

                for (float x = formationViewRect.xMin; x < formationViewRect.xMax; x += grid01)
                {
                    Handles.DrawLine(new Vector3(x, formationViewRect.yMin), new Vector3(x, formationViewRect.yMax));
                }

                for (float y = formationViewRect.yMin; y < formationViewRect.yMax; y += grid01)
                {
                    Handles.DrawLine(new Vector3(formationViewRect.xMin, y), new Vector3(formationViewRect.xMax, y));
                }
            }

            // 0.01 단위 그리드 (매우 연한 색)
            if (snapValue <= 0.01f)
            {
                Handles.color = new Color(0.2f, 0.2f, 0.2f, 0.05f);
                float grid001 = gridSize * 0.01f;

                // 너무 많은 선을 그리지 않도록 뷰포트 제한
                float startX = Mathf.Max(formationViewRect.xMin, center.x - gridSize * 5);
                float endX = Mathf.Min(formationViewRect.xMax, center.x + gridSize * 5);
                float startY = Mathf.Max(formationViewRect.yMin, center.y - gridSize * 5);
                float endY = Mathf.Min(formationViewRect.yMax, center.y + gridSize * 5);

                for (float x = startX; x < endX; x += grid001)
                {
                    Handles.DrawLine(new Vector3(x, startY), new Vector3(x, endY));
                }

                for (float y = startY; y < endY; y += grid001)
                {
                    Handles.DrawLine(new Vector3(startX, y), new Vector3(endX, y));
                }
            }
        }
    }

    private void DrawCenterPoint(Vector2 center)
    {
        Handles.color = Color.white;
        Handles.DrawSolidDisc(center, Vector3.forward, 5f);
        GUI.Label(new Rect(center.x - 20, center.y + 10, 40, 20), "(0,0)", EditorStyles.miniLabel);
    }

    private void DrawFormation2D(FormationPosition[] positions, bool isAlly, Vector2 center)
    {
        if (positions == null) return;

        Color baseColor = isAlly ? new Color(0, 0, 1, 0.8f) : new Color(1, 0, 0, 0.8f);

        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i] == null) continue;

            Vector2 viewPos = WorldToViewerPosition(positions[i].standPosition, center);

            bool isSelected = (selectedSlotIndex == i && isEditingAlly == isAlly);
            bool isBeingDragged = (isDragging && draggingSlotIndex == i && draggingIsAlly == isAlly);

            DrawSlot2D(viewPos, i, positions[i].isFrontRow, isAlly, isSelected, isBeingDragged);

            // 공격 위치 표시
            if (currentFormation.useCustomAttackPosition)
            {
                Vector2 attackPos = WorldToViewerPosition(currentFormation.customAttackPositions[i], center);
                Handles.color = Color.yellow * 0.5f;
                Handles.DrawDottedLine(viewPos, attackPos, 2f);
                Handles.DrawSolidDisc(attackPos, Vector3.forward, 3f);
            }

            // 좌표 표시 (전체 또는 선택된 것만)
            if (showAllPositions || isSelected || isBeingDragged)
            {
                Vector3 pos = positions[i].standPosition;
                string coordText = $"({pos.x:F2}, {pos.z:F2})";

                GUIStyle coordStyle = new GUIStyle(EditorStyles.whiteMiniLabel);
                coordStyle.normal.background = Texture2D.blackTexture;
                coordStyle.padding = new RectOffset(2, 2, 1, 1);

                GUI.Label(new Rect(viewPos.x + 15, viewPos.y - 25, 70, 16),
                    coordText, coordStyle);
            }
        }
    }

    private void DrawSlot2D(Vector2 pos, int index, bool isFrontRow, bool isAlly, bool isSelected, bool isDragging)
    {
        float size = isDragging ? 30f : (isSelected ? 25f : 20f);
        Color color = isAlly ? Color.blue : Color.red;

        if (isDragging)
            color = Color.green;
        else if (isSelected)
            color = Color.yellow;

        Handles.color = color;

        if (isFrontRow)
        {
            Handles.DrawSolidRectangleWithOutline(
                new Rect(pos.x - size / 2, pos.y - size / 2, size, size),
                color * 0.5f,
                color
            );
        }
        else
        {
            Handles.DrawSolidDisc(pos, Vector3.forward, size / 2);
        }

        // 슬롯 번호와 실제 인덱스 모두 표시
        var positions = isAlly ? currentFormation.allyPositions : currentFormation.enemyPositions;
        int actualIndex = positions[index].slotIndex;

        GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteMiniLabel);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontStyle = isDragging ? FontStyle.Bold : FontStyle.Normal;

        // 배열 위치와 슬롯 인덱스가 다른 경우 둘 다 표시
        string labelText = actualIndex != index ?
            $"{(isAlly ? "A" : "E")}{actualIndex}[{index}]" :
            $"{(isAlly ? "A" : "E")}{index}";

        GUI.Label(new Rect(pos.x - 20, pos.y - 10, 40, 20),
            labelText,
            labelStyle);
    }

    private Vector2 WorldToViewerPosition(Vector3 worldPos, Vector2 center)
    {
        return center + new Vector2(worldPos.x * gridSize, -worldPos.z * gridSize);
    }

    private Vector3 ViewerToWorldPosition(Vector2 viewerPos, Vector2 center)
    {
        Vector2 offset = viewerPos - center;
        return new Vector3(offset.x / gridSize, 0, -offset.y / gridSize);
    }

    private void CreateNewFormation()
    {
        if (!AssetDatabase.IsValidFolder(SAVE_PATH))
        {
            Directory.CreateDirectory(SAVE_PATH);
            AssetDatabase.Refresh();
        }

        string fileName = $"{selectedType}_Formation";
        string path = $"{SAVE_PATH}/{fileName}.asset";

        int counter = 1;
        while (AssetDatabase.LoadAssetAtPath<FormationData>(path) != null)
        {
            path = $"{SAVE_PATH}/{fileName}_{counter}.asset";
            counter++;
        }

        FormationData newFormation = ScriptableObject.CreateInstance<FormationData>();
        newFormation.formationType = selectedType;
        newFormation.formationName = formationNames[selectedType];
        newFormation.description = formationNames[selectedType];
        newFormation.level = 1;
        newFormation.baseBuffPercentage = 10f;
        newFormation.baseDistance = 2.5f;  // 추가
        newFormation.frontBackDistance = 3f;
        newFormation.characterSpacing = 1.5f;

        newFormation.InitializePositions(true);
        newFormation.InitializePositions(false);

        // 캐시 초기화
        lastBaseDistance = newFormation.baseDistance;  // 추가
        lastFrontBackDistance = newFormation.frontBackDistance;
        lastCharacterSpacing = newFormation.characterSpacing;
        lastFormationType = newFormation.formationType;

        AssetDatabase.CreateAsset(newFormation, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        currentFormation = newFormation;
        Selection.activeObject = newFormation;

        Debug.Log($"Created new formation: {path}");
    }

    private void LoadExistingFormation()
    {
        string path = EditorUtility.OpenFilePanel("Load Formation", SAVE_PATH, "asset");
        if (!string.IsNullOrEmpty(path))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);
            currentFormation = AssetDatabase.LoadAssetAtPath<FormationData>(path);

            if (currentFormation != null)
            {
                selectedType = currentFormation.formationType;

                // 캐시 초기화
                lastFrontBackDistance = currentFormation.frontBackDistance;
                lastCharacterSpacing = currentFormation.characterSpacing;
                lastFormationType = currentFormation.formationType;

                Repaint();
            }
        }
    }

    private void SaveFormation()
    {
        if (currentFormation == null) return;

        EditorUtility.SetDirty(currentFormation);
        AssetDatabase.SaveAssets();

        Debug.Log($"Formation saved: {currentFormation.name}");
    }

    private void AutoSetupAllPositions()
    {
        if (currentFormation == null) return;

        currentFormation.formationType = selectedType;
        currentFormation.InitializePositions(true);
        currentFormation.InitializePositions(false);

        // 캐시 업데이트
        lastBaseDistance = currentFormation.baseDistance;  // 추가
        lastFrontBackDistance = currentFormation.frontBackDistance;
        lastCharacterSpacing = currentFormation.characterSpacing;
        lastFormationType = currentFormation.formationType;

        EditorUtility.SetDirty(currentFormation);

        Debug.Log($"Auto-setup positions for {selectedType}");
    }

    private void CreateVlackyPreview()
    {
        ClearPreviewObjects();

        if (vlackyPrefab == null || currentFormation == null) return;

        CreateVlackyGroup(currentFormation.allyPositions, true);
        CreateVlackyGroup(currentFormation.enemyPositions, false);
    }

    private void CreateVlackyGroup(FormationPosition[] positions, bool isAlly)
    {
        if (positions == null) return;

        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i] == null) continue;

            GameObject vlacky = PrefabUtility.InstantiatePrefab(vlackyPrefab) as GameObject;
            if (vlacky != null)
            {
                vlacky.name = $"Preview_{(isAlly ? "Ally" : "Enemy")}_{i}";
                vlacky.transform.position = positions[i].standPosition;
                vlacky.transform.rotation = Quaternion.Euler(0, isAlly ? 0 : 180, 0);
                vlacky.transform.localScale = Vector3.one * previewScale;

                previewObjects.Add(vlacky);
            }
        }
    }

    private void ClearPreviewObjects()
    {
        foreach (var obj in previewObjects)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }
        previewObjects.Clear();
    }
}
#endif