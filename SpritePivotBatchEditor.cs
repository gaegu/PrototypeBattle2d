using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class SpritePivotBatchEditor : EditorWindow
{
    private Vector2 customPivot = new Vector2(0.5f, 0.5f);
    private SpriteAlignment alignment = SpriteAlignment.Center;
    private bool useCustomPivot = false;
    private bool showSpriteDetails = false;

    [MenuItem("*COSMOS*/Util/Sprite Pivot Batch Editor")]
    public static void ShowWindow()
    {
        GetWindow<SpritePivotBatchEditor>("Sprite Pivot Editor");
    }

    void OnGUI()
    {
        GUILayout.Label("Sprite Pivot Batch Editor", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 피봇 설정 옵션
        GUILayout.Label("Pivot Settings", EditorStyles.boldLabel);
        useCustomPivot = EditorGUILayout.Toggle("Use Custom Pivot", useCustomPivot);

        if (useCustomPivot)
        {
            customPivot = EditorGUILayout.Vector2Field("Custom Pivot", customPivot);
            EditorGUILayout.HelpBox("Custom Pivot: X and Y values should be between 0 and 1", MessageType.Info);
        }
        else
        {
            alignment = (SpriteAlignment)EditorGUILayout.EnumPopup("Alignment", alignment);
        }

        GUILayout.Space(20);

        // 선택된 스프라이트 표시
        GUILayout.Label("Selected Sprites:", EditorStyles.boldLabel);

        Object[] selectedObjects = Selection.objects;
        int spriteCount = 0;
        int multiSpriteCount = 0;

        foreach (Object obj in selectedObjects)
        {
            if (obj is Texture2D)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && importer.textureType == TextureImporterType.Sprite)
                {
                    spriteCount++;

                    if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        multiSpriteCount++;
                    }
                }
            }
        }

        if (spriteCount == 0)
        {
            EditorGUILayout.HelpBox("No sprites selected. Please select sprites in the Project window.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField($"Total: {spriteCount} sprites selected");
            if (multiSpriteCount > 0)
            {
                EditorGUILayout.LabelField($"• {multiSpriteCount} Multiple sprite sheets");
                EditorGUILayout.LabelField($"• {spriteCount - multiSpriteCount} Single sprites");
            }

            // 상세 정보 표시 토글
            showSpriteDetails = EditorGUILayout.Foldout(showSpriteDetails, "Show Details");
            if (showSpriteDetails)
            {
                EditorGUI.indentLevel++;
                foreach (Object obj in selectedObjects)
                {
                    if (obj is Texture2D)
                    {
                        string path = AssetDatabase.GetAssetPath(obj);
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                        if (importer != null && importer.textureType == TextureImporterType.Sprite)
                        {
                            string mode = importer.spriteImportMode == SpriteImportMode.Multiple ?
                                         "[Multiple]" : "[Single]";
                            EditorGUILayout.LabelField($"• {obj.name} {mode}");
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        GUILayout.Space(20);

        // 적용 버튼
        GUI.enabled = spriteCount > 0;
        if (GUILayout.Button("Apply Pivot to Selected Sprites", GUILayout.Height(30)))
        {
            ApplyPivotToSelectedSprites();
        }
        GUI.enabled = true;

        GUILayout.Space(10);

        // 프리셋 버튼들
        GUILayout.Label("Quick Presets:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Center"))
        {
            SetPresetAndApply(SpriteAlignment.Center);
        }
        if (GUILayout.Button("Bottom"))
        {
            SetPresetAndApply(SpriteAlignment.BottomCenter);
        }
        if (GUILayout.Button("Top"))
        {
            SetPresetAndApply(SpriteAlignment.TopCenter);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Left"))
        {
            SetPresetAndApply(SpriteAlignment.LeftCenter);
        }
        if (GUILayout.Button("Right"))
        {
            SetPresetAndApply(SpriteAlignment.RightCenter);
        }
        GUILayout.EndHorizontal();

        // Custom pivot 프리셋
        GUILayout.Space(10);
        GUILayout.Label("Custom Pivot Presets:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0.25, 0.25"))
        {
            SetCustomPresetAndApply(new Vector2(0.25f, 0.25f));
        }
        if (GUILayout.Button("0.75, 0.75"))
        {
            SetCustomPresetAndApply(new Vector2(0.75f, 0.75f));
        }
        if (GUILayout.Button("0.5, 0.1"))
        {
            SetCustomPresetAndApply(new Vector2(0.5f, 0.1f));
        }
        GUILayout.EndHorizontal();

        // 정보
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("This tool works with both Single and Multiple sprite modes.", MessageType.Info);
    }

    void SetPresetAndApply(SpriteAlignment preset)
    {
        useCustomPivot = false;
        alignment = preset;
        ApplyPivotToSelectedSprites();
    }

    void SetCustomPresetAndApply(Vector2 pivot)
    {
        useCustomPivot = true;
        customPivot = pivot;
        ApplyPivotToSelectedSprites();
    }

    void ApplyPivotToSelectedSprites()
    {
        Object[] selectedObjects = Selection.objects;
        int modifiedCount = 0;
        int totalSpritesModified = 0;

        foreach (Object obj in selectedObjects)
        {
            if (obj is Texture2D)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && importer.textureType == TextureImporterType.Sprite)
                {
                    bool modified = false;

                    // Single sprite mode 처리
                    if (importer.spriteImportMode == SpriteImportMode.Single)
                    {
                        modified = ApplyPivotToSingleSprite(importer);
                        if (modified) totalSpritesModified++;
                    }
                    // Multiple sprite mode 처리
                    else if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        int spritesModified = ApplyPivotToMultipleSprites(importer);
                        if (spritesModified > 0)
                        {
                            modified = true;
                            totalSpritesModified += spritesModified;
                        }
                    }

                    if (modified)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                        modifiedCount++;
                    }
                }
            }
        }

        if (modifiedCount > 0)
        {
            Debug.Log($"Successfully updated pivot for {modifiedCount} texture(s) ({totalSpritesModified} sprite(s) total)!");
            AssetDatabase.Refresh();
        }
    }

    bool ApplyPivotToSingleSprite(TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        if (useCustomPivot)
        {
            // Custom pivot 설정
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = customPivot;

            importer.SetTextureSettings(settings);

            // SerializedObject를 사용하여 더 확실하게 설정
            SerializedObject serializedImporter = new SerializedObject(importer);
            SerializedProperty spritePivotProperty = serializedImporter.FindProperty("m_Alignment");
            SerializedProperty customPivotProperty = serializedImporter.FindProperty("m_SpritePivot");

            spritePivotProperty.intValue = (int)SpriteAlignment.Custom;
            customPivotProperty.vector2Value = customPivot;

            serializedImporter.ApplyModifiedProperties();
        }
        else
        {
            settings.spriteAlignment = (int)alignment;
            settings.spritePivot = GetPivotValue(alignment);
            importer.SetTextureSettings(settings);
        }

        return true;
    }

    int ApplyPivotToMultipleSprites(TextureImporter importer)
    {
        // SerializedObject를 사용하여 spriteSheet 데이터에 접근
        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty spriteSheetProperty = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");

        if (spriteSheetProperty == null || !spriteSheetProperty.isArray)
        {
            Debug.LogWarning($"Could not find sprite data in {importer.assetPath}");
            return 0;
        }

        int spriteCount = spriteSheetProperty.arraySize;

        // 각 스프라이트의 pivot 수정
        for (int i = 0; i < spriteCount; i++)
        {
            SerializedProperty sprite = spriteSheetProperty.GetArrayElementAtIndex(i);
            SerializedProperty alignmentProperty = sprite.FindPropertyRelative("m_Alignment");
            SerializedProperty pivotProperty = sprite.FindPropertyRelative("m_Pivot");

            if (alignmentProperty != null && pivotProperty != null)
            {
                if (useCustomPivot)
                {
                    alignmentProperty.intValue = (int)SpriteAlignment.Custom;
                    pivotProperty.vector2Value = customPivot;
                }
                else
                {
                    alignmentProperty.intValue = (int)alignment;
                    pivotProperty.vector2Value = GetPivotValue(alignment);
                }
            }
        }

        serializedImporter.ApplyModifiedProperties();

        Debug.Log($"Updated {spriteCount} sprites in {System.IO.Path.GetFileName(importer.assetPath)}");
        return spriteCount;
    }

    Vector2 GetPivotValue(SpriteAlignment alignment)
    {
        switch (alignment)
        {
            case SpriteAlignment.Center:
                return new Vector2(0.5f, 0.5f);
            case SpriteAlignment.TopLeft:
                return new Vector2(0f, 1f);
            case SpriteAlignment.TopCenter:
                return new Vector2(0.5f, 1f);
            case SpriteAlignment.TopRight:
                return new Vector2(1f, 1f);
            case SpriteAlignment.LeftCenter:
                return new Vector2(0f, 0.5f);
            case SpriteAlignment.RightCenter:
                return new Vector2(1f, 0.5f);
            case SpriteAlignment.BottomLeft:
                return new Vector2(0f, 0f);
            case SpriteAlignment.BottomCenter:
                return new Vector2(0.5f, 0f);
            case SpriteAlignment.BottomRight:
                return new Vector2(1f, 0f);
            default:
                return new Vector2(0.5f, 0.5f);
        }
    }

    // 스프라이트 메타데이터를 가져오는 대체 방법 (Unity 버전 호환성)
    List<SpriteRect> GetSpriteRects(TextureImporter importer)
    {
        List<SpriteRect> spriteRects = new List<SpriteRect>();

        // 먼저 기본 프로퍼티로 시도
        var spriteSheetProperty = typeof(TextureImporter).GetProperty("spritesheet",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (spriteSheetProperty != null)
        {
            var spriteSheet = spriteSheetProperty.GetValue(importer) as SpriteMetaData[];
            if (spriteSheet != null)
            {
                foreach (var sprite in spriteSheet)
                {
                    spriteRects.Add(new SpriteRect
                    {
                        name = sprite.name,
                        alignment = sprite.alignment,
                        pivot = sprite.pivot,
                        rect = sprite.rect
                    });
                }
            }
        }

        return spriteRects;
    }

    // 스프라이트 정보를 저장하기 위한 구조체
    [System.Serializable]
    public struct SpriteRect
    {
        public string name;
        public int alignment;
        public Vector2 pivot;
        public Rect rect;
    }
}

// 추가 기능: 컨텍스트 메뉴에서 바로 실행
public static class SpritePivotContextMenu
{
    [MenuItem("Assets/Set Sprite Pivot/Center", false, 100)]
    static void SetPivotCenter()
    {
        SetPivotForSelected(SpriteAlignment.Center, new Vector2(0.5f, 0.5f));
    }

    [MenuItem("Assets/Set Sprite Pivot/Bottom", false, 101)]
    static void SetPivotBottom()
    {
        SetPivotForSelected(SpriteAlignment.BottomCenter, new Vector2(0.5f, 0f));
    }

    [MenuItem("Assets/Set Sprite Pivot/Top", false, 102)]
    static void SetPivotTop()
    {
        SetPivotForSelected(SpriteAlignment.TopCenter, new Vector2(0.5f, 1f));
    }

    [MenuItem("Assets/Set Sprite Pivot/Custom (0.5, 0.3)", false, 103)]
    static void SetPivotCustom()
    {
        SetCustomPivotForSelected(new Vector2(0.5f, 0.3f));
    }

    // 스프라이트가 선택되었을 때만 메뉴 활성화
    [MenuItem("Assets/Set Sprite Pivot/Center", true)]
    [MenuItem("Assets/Set Sprite Pivot/Bottom", true)]
    [MenuItem("Assets/Set Sprite Pivot/Top", true)]
    [MenuItem("Assets/Set Sprite Pivot/Custom (0.5, 0.3)", true)]
    static bool ValidateSetPivot()
    {
        foreach (Object obj in Selection.objects)
        {
            if (obj is Texture2D)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType == TextureImporterType.Sprite)
                {
                    return true;
                }
            }
        }
        return false;
    }

    static void SetPivotForSelected(SpriteAlignment alignment, Vector2 pivotValue)
    {
        int textureCount = 0;
        int totalSpritesModified = 0;

        foreach (Object obj in Selection.objects)
        {
            if (obj is Texture2D)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && importer.textureType == TextureImporterType.Sprite)
                {
                    if (importer.spriteImportMode == SpriteImportMode.Single)
                    {
                        TextureImporterSettings settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        settings.spriteAlignment = (int)alignment;
                        settings.spritePivot = pivotValue;
                        importer.SetTextureSettings(settings);
                        totalSpritesModified++;
                    }
                    else if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        // SerializedObject를 사용하여 Multiple sprites 처리
                        SerializedObject serializedImporter = new SerializedObject(importer);
                        SerializedProperty spriteSheetProperty = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");

                        if (spriteSheetProperty != null && spriteSheetProperty.isArray)
                        {
                            for (int i = 0; i < spriteSheetProperty.arraySize; i++)
                            {
                                SerializedProperty sprite = spriteSheetProperty.GetArrayElementAtIndex(i);
                                SerializedProperty alignmentProperty = sprite.FindPropertyRelative("m_Alignment");
                                SerializedProperty pivotProperty = sprite.FindPropertyRelative("m_Pivot");

                                if (alignmentProperty != null && pivotProperty != null)
                                {
                                    alignmentProperty.intValue = (int)alignment;
                                    pivotProperty.vector2Value = pivotValue;
                                }
                            }
                            serializedImporter.ApplyModifiedProperties();
                            totalSpritesModified += spriteSheetProperty.arraySize;
                        }
                    }

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    textureCount++;
                }
            }
        }

        if (textureCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"Pivot set to {alignment} for {textureCount} texture(s) ({totalSpritesModified} sprite(s) total)!");
        }
    }

    static void SetCustomPivotForSelected(Vector2 customPivot)
    {
        int textureCount = 0;
        int totalSpritesModified = 0;

        foreach (Object obj in Selection.objects)
        {
            if (obj is Texture2D)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null && importer.textureType == TextureImporterType.Sprite)
                {
                    if (importer.spriteImportMode == SpriteImportMode.Single)
                    {
                        // Single sprite 처리
                        SerializedObject serializedImporter = new SerializedObject(importer);
                        SerializedProperty spritePivotProperty = serializedImporter.FindProperty("m_Alignment");
                        SerializedProperty customPivotProperty = serializedImporter.FindProperty("m_SpritePivot");

                        spritePivotProperty.intValue = (int)SpriteAlignment.Custom;
                        customPivotProperty.vector2Value = customPivot;

                        serializedImporter.ApplyModifiedProperties();
                        totalSpritesModified++;
                    }
                    else if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        // Multiple sprites 처리
                        SerializedObject serializedImporter = new SerializedObject(importer);
                        SerializedProperty spriteSheetProperty = serializedImporter.FindProperty("m_SpriteSheet.m_Sprites");

                        if (spriteSheetProperty != null && spriteSheetProperty.isArray)
                        {
                            for (int i = 0; i < spriteSheetProperty.arraySize; i++)
                            {
                                SerializedProperty sprite = spriteSheetProperty.GetArrayElementAtIndex(i);
                                SerializedProperty alignmentProperty = sprite.FindPropertyRelative("m_Alignment");
                                SerializedProperty pivotProperty = sprite.FindPropertyRelative("m_Pivot");

                                if (alignmentProperty != null && pivotProperty != null)
                                {
                                    alignmentProperty.intValue = (int)SpriteAlignment.Custom;
                                    pivotProperty.vector2Value = customPivot;
                                }
                            }
                            serializedImporter.ApplyModifiedProperties();
                            totalSpritesModified += spriteSheetProperty.arraySize;
                        }
                    }

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    textureCount++;
                }
            }
        }

        if (textureCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"Custom pivot ({customPivot.x}, {customPivot.y}) set for {textureCount} texture(s) ({totalSpritesModified} sprite(s) total)!");
        }
    }
}