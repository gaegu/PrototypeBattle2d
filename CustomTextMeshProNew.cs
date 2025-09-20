using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CustomTextMeshProNew : TextMeshPro
{
    public static string Language = string.Empty;

    public int presetIndex = 0;
    public string fontPath = string.Empty;
    public string materialPath = string.Empty;
    private string loadFontPath = string.Empty;
    private string loadMaterialPath = string.Empty;
    private string language = string.Empty;
    private string defaultFont = string.Empty;

#if UNITY_EDITOR
    private bool IsRefresh = false;
    private bool IsCreated = false;

    protected override void OnValidate()
    {
        base.OnValidate();

        // OnValidate는 빌드중에 에러로 처리되므로 빌드중이 아닐경우만 체크하게 변경(kosuchoi)
        if (UnityEditor.BuildPipeline.isBuildingPlayer)
            return;

        if (!Application.isPlaying)
        {
            if (IsRefresh)
            {
                loadMaterialPath = $"Font/Eng/{materialPath}".Trim();

                if (UtilModel.Resources.Exists(loadMaterialPath))
                {
                    m_sharedMaterial = UtilModel.Resources.Load<Material>(loadMaterialPath, this);
                    fontSharedMaterial = UtilModel.Resources.Load<Material>(loadMaterialPath, this);
                    fontMaterial = UtilModel.Resources.Load<Material>(loadMaterialPath, this);
                }

                IsRefresh = false;
            }
        }

        Rebuild(CanvasUpdate.PreRender);

        // CanvasRenderer 갱신 강제 호출
        if (canvasRenderer)
        {
            canvasRenderer.SetMesh(mesh);

            // 강제적으로 메시를 업데이트
            UpdateGeometry();
        }

        FindFontPath();
    }

    [ContextMenu("FIND")]
    public void FindFontPath()
    {
        if (!IsCreated)
            return;

        if (IsRefresh)
            return;

        if (font == null)
            return;

        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(font);
        string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        fontPath = $"{System.IO.Directory.GetParent(assetPath).Name}/{assetName}";

        string materialForRenderingPath = UnityEditor.AssetDatabase.GetAssetPath(materialForRendering);
        if (string.IsNullOrEmpty(materialForRenderingPath))
            materialForRenderingPath = UnityEditor.AssetDatabase.GetAssetPath(fontSharedMaterial);

        string materialForRenderingName = System.IO.Path.GetFileNameWithoutExtension(materialForRenderingPath);

        if (string.IsNullOrEmpty(materialForRenderingPath)) // 경로 예외처리 추가
            return;

        materialPath = $"{System.IO.Directory.GetParent(materialForRenderingPath).Name}/{materialForRenderingName}";

        // 텍스트 갱신 플래그 설정
        SetAllDirty();
    }

    protected override void Start()
    {
        if (!Application.isPlaying)
            return;

        base.Start();

        // 텍스트 갱신 플래그 설정
        SetAllDirty();
    }

    protected override void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        base.OnEnable();

        if (!ChangePath())
            return;

        LoadFont().Forget();
    }

    public void EditorRefresh()
    {
        if (!Application.isPlaying)
        {
            IsCreated = true;
            IsRefresh = true;
            OnValidate();
            return;
        }
    }
#else
    protected override void OnEnable()
    {
        base.OnEnable();

        if (!ChangePath())
            return;

        LoadFont().Forget();
    }
#endif

    private bool ChangePath()
    {
        if (language.Equals(Language))
            return false;

        language = Language;

        defaultFont = $"Font/{language}/Countach-Bold/Countach-BoldSDF_Asset";

// #if UNITY_EDITOR
//         // 어차피 Fallback으로 Kor에 없으면 Eng껄 찾아다 쓴다.
//         loadFontPath = $"Font/Kor/{fontPath}".Trim();
//         loadMaterialPath = $"Font/Kor/{materialPath}".Trim();
// #else
        loadFontPath = $"Font/{language}/{fontPath}".Trim();
        loadMaterialPath = $"Font/{language}/{materialPath}".Trim();
//#endif
        return true;
    }

    private async UniTask LoadFont()
    {
        TMP_FontAsset loadFont = null;
        if (UtilModel.Resources.Exists(loadFontPath))
        {
            loadFont = await UtilModel.Resources.LoadAsync<TMP_FontAsset>(loadFontPath, this);
        }

        if (loadFont == null)
        {
            // 찾으려는 폰트가 없으면 기본 Bold로 씌운다.
            if (UtilModel.Resources.Exists(defaultFont))
            {
                loadFont = await UtilModel.Resources.LoadAsync<TMP_FontAsset>(defaultFont, this);
            }
            
            if (loadFont == null)
                return;
        }

        font = loadFont;

        Material materialPreset = null;
        if (UtilModel.Resources.Exists(loadMaterialPath))
        {
            materialPreset = await UtilModel.Resources.LoadAsync<Material>(loadMaterialPath, this);
        }

        if (materialPreset == null)
            return;

        fontSharedMaterial = materialPreset;

        // 일단은 임시 작동 처리
        if (fontSharedMaterial.shader == null || fontSharedMaterial.shader.name.Contains("Hidden/InternalErrorShader"))
            fontSharedMaterial.shader = Shader.Find("TextMeshPro/Distance Field");
    }
}