using UnityEngine;
using UnityEngine.SceneManagement;

public class FG9SceneSettingsApplier : MonoBehaviour
{
    private static FG9SceneSettingsApplier instance;
    public static FG9SceneSettingsApplier Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<FG9SceneSettingsApplier>();

            return instance;
        }
    }


    public FG9SceneSettings sceneSettings;
    private string currentPrefabName;  // 현재 적용된 프리팹 이름을 저장

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySettings(scene.name);
    }

    private void ApplySettings(string sceneName)
    {
        if (sceneSettings == null)
            return;

        var settings = sceneSettings.GetSettingsForScene(sceneName);

        if (settings == null)
            return;

        IronJade.Debug.Log("## settings " + sceneName + " / " + settings.fogColor);

        ApplyEnvironmentSettings(settings);
    }

    private void ApplyEnvironmentSettings(FG9SceneSettings.EnvironmentSettings settings)
    {
        if (settings == null)
            return;

        // Apply light settings
        Light mainLight = FindObjectOfType<Light>();
        if (mainLight != null)
        {
            mainLight.color = settings.lightColor;
        }

        // Apply fog settings
        RenderSettings.fog = settings.fogEnabled;
        RenderSettings.fogColor = settings.fogColor;
        RenderSettings.fogMode = settings.fogMode;

        switch (settings.fogMode)
        {
            case FogMode.Linear:
                RenderSettings.fogStartDistance = settings.fogStartDistance;
                RenderSettings.fogEndDistance = settings.fogEndDistance;
                break;
            case FogMode.Exponential:
            case FogMode.ExponentialSquared:
                RenderSettings.fogDensity = settings.fogDensity;
                break;
        }
    }

    // 프리팹 설정을 적용하는 public 함수
    public void ApplyPrefabSettings(string prefabName)
    {
        if (sceneSettings == null) return;

        var settings = sceneSettings.GetSettingsForPrefab(prefabName);

        if (settings != null)
        {
            currentPrefabName = prefabName;
            ApplyEnvironmentSettings(settings);
        }
    }

    public bool HasPrefabFogSetting(string prefabName)
    {
        if (sceneSettings == null)
            return false;

        var settings = sceneSettings.GetSettingsForPrefab(prefabName);

        return settings != null;
    }

    // 프리팹 설정을 해제하고 현재 씬의 설정으로 되돌리는 함수
    public void ClearPrefabSettings()
    {
        currentPrefabName = null;
        //Scene Setting 없을경우 PrefabSetting이 남아있어 fog false처리
        RenderSettings.fog = false;
        ApplySettings(SceneManager.GetActiveScene().name);
    }

    public void SetEnableFog(bool enabled)
    {
        RenderSettings.fog = enabled;
    }

    // 현재 적용된 프리팹 이름 반환
    public string GetCurrentPrefabName()
    {
        return currentPrefabName;
    }
}