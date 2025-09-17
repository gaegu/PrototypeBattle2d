using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CustomTextMeshProUGUI : TextMeshProUGUI
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
        canvasRenderer.SetMesh(mesh);

        // 강제적으로 메시를 업데이트
        UpdateGeometry();

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
//         loadFontPath = $"Font/Eng/{fontPath}".Trim();
//         loadMaterialPath = $"Font/Eng/{materialPath}".Trim();
// #else
        loadFontPath = $"Font/{language}/{fontPath}".Trim();
        loadMaterialPath = $"Font/{language}/{materialPath}".Trim();
//#endif
        return true;
    }


    /// <summary>
    /// 폴더 경로를 Addressable Address로 변환
    /// 예: "Font/Kor/Gamer/Gamer-SDF_Asset" → "Fonts_Kor_Gamer-SDF_Asset"
    /// </summary>
    public static string ConvertPathToAddress(string path)
    {
        // 경로 정규화 (백슬래시를 슬래시로)
        path = path.Replace('\\', '/');

        // 확장자 제거 (필요한 경우)
        string pathWithoutExt = System.IO.Path.GetFileNameWithoutExtension(path);
        string fileName = System.IO.Path.GetFileName(pathWithoutExt);

        // 경로 분석
        string[] parts = path.Split('/');

        // Font 폴더 특별 처리
        if (parts.Length >= 2 && parts[0].ToLower() == "font")
        {
            // Font → Fonts 변경
            string category = "Fonts";

            // 언어 타입 (Kor, Eng 등)
            string lang = parts.Length > 1 ? parts[1] : "";

            // 마지막 파일명
            string file = fileName;

            // 조합: Fonts_Kor_FileName
            return $"{category}_{lang}_{file}";
        }

        // 다른 경로의 경우 기본 처리
        // Characters/Player/Player_Battle.prefab → Character_Player_Battle
        if (parts.Length >= 2 && parts[0] == "Characters")
        {
            return $"Character_{parts[1]}_{fileName}";
        }

        // UI/Battle/HUD.prefab → UI_Battle_HUD
        if (parts.Length >= 2 && parts[0] == "UI")
        {
            return $"UI_{parts[1]}_{fileName}";
        }

        // 기본: 폴더명_파일명
        if (parts.Length >= 2)
        {
            return $"{parts[0]}_{fileName}";
        }

        return fileName;
    }

    private async UniTask LoadFont()
    {
        // Builtin폰트 적용(영어가 아니고 어드레서블 패치가 완료되지 않으면 빌트인폰트로 변경)
        if (Application.isPlaying && !UtilModel.Resources.IsAddressablesUpdateComplete && language != "Eng")
        {
            string builtinFontPath = fontPath;
            int lastSlash = builtinFontPath.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash + 1 < builtinFontPath.Length)
            {
                builtinFontPath = builtinFontPath.Insert(lastSlash + 1, "(Builtin)");
            }
            builtinFontPath = $"Font/{language}/{builtinFontPath}";
            
            font = Resources.Load<TMP_FontAsset>(builtinFontPath);

            if (font == null)
            {
                Debug.LogError($"Fallback폰트 로드에 실패했습니다. builtinFontPath:{builtinFontPath}");
            }
           
            return;
        }
        
        TMP_FontAsset loadFont = null;
        if (UtilModel.Resources.Exists(loadFontPath))
        {

            loadFont = await GameCore.Addressables.AddressableManager.Instance.LoadAssetAsync<TMP_FontAsset>(ConvertPathToAddress(loadFontPath));
            if (loadFont == null)
            {
                Debug.LogError("##  GameCore.Addressables.AddressableManager font not exist " + ConvertPathToAddress(loadFontPath));
            }

            if ( loadFont == null )
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