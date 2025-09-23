using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using DG.DemiEditor;
#endif

[CreateAssetMenu(fileName = "FG9SceneSettings", menuName = "FG9/Scene Settings", order = 1)]
public class FG9SceneSettings : ScriptableObject
{
    [System.Serializable]
    public class EnvironmentSettings
    {
        public Color lightColor = Color.white;
        public bool fogEnabled = false;
        public Color fogColor = Color.gray;
        public FogMode fogMode = FogMode.Linear;
        public float fogDensity = 0.01f;
        public float fogStartDistance = 0f;
        public float fogEndDistance = 300f;
    }

    [System.Serializable]
    public class SceneSettings : EnvironmentSettings
    {
        public string sceneName;
    }

    [System.Serializable]
    public class PrefabSettings : EnvironmentSettings
    {
        public string prefabName;
    }

    public List<SceneSettings> sceneSettings = new List<SceneSettings>();
    public List<PrefabSettings> prefabSettings = new List<PrefabSettings>();

    public SceneSettings GetSettingsForScene(string sceneName)
    {
        return sceneSettings.Find(settings => settings.sceneName == sceneName);
    }

    public PrefabSettings GetSettingsForPrefab(string prefabName)
    {
        return prefabSettings.Find(settings => settings.prefabName == prefabName);
    }

    public void SetSettingsForScene(string sceneName, SceneSettings settings)
    {
        int index = sceneSettings.FindIndex(s => s.sceneName == sceneName);
        if (index != -1)
        {
            sceneSettings[index] = settings;
        }
        else
        {
            settings.sceneName = sceneName;
            sceneSettings.Add(settings);
        }
    }

    public void SetSettingsForPrefab(string prefabName, PrefabSettings settings)
    {
        int index = prefabSettings.FindIndex(p => p.prefabName == prefabName);
        if (index != -1)
        {
            prefabSettings[index] = settings;
        }
        else
        {
            settings.prefabName = prefabName;
            prefabSettings.Add(settings);
        }
    }
    public void RemoveSettingsForScene(string sceneName)
    {
        int index = sceneSettings.FindIndex(s => s.sceneName == sceneName);
        if (index != -1)
        {
            sceneSettings.RemoveAt(index);
        }
    }

    public void RemoveSettingsForPrefab(string prefabName)
    {
        int index = prefabSettings.FindIndex(p => p.prefabName == prefabName);
        if (index != -1)
        {
            prefabSettings.RemoveAt(index);
        }
    }

    public List<string> FindMissingScenes()
    {
#if UNITY_EDITOR
        var missingScenes = new List<string>();

        foreach (var sceneSetting in sceneSettings)
        {
            if (string.IsNullOrEmpty(sceneSetting.sceneName))
            {
                missingScenes.Add("(Empty scene name)");
                continue;
            }

            bool sceneExists = false;

            // 빌드 세팅에서 확인
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (scene.enabled && System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneSetting.sceneName)
                {
                    sceneExists = true;
                    break;
                }
            }

            // 프로젝트 내 모든 씬에서 확인
            if (!sceneExists)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneSetting.sceneName}");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneSetting.sceneName)
                    {
                        sceneExists = true;
                        break;
                    }
                }
            }

            if (!sceneExists)
            {
                missingScenes.Add(sceneSetting.sceneName);
            }
        }

        return missingScenes;
#else
        return new List<string>();
#endif
    }

    public void CleanupUnusedSettings()
    {
#if UNITY_EDITOR
        var missingScenes = FindMissingScenes();

        // 존재하지 않는 씬의 설정 정리
        sceneSettings.RemoveAll(s =>( missingScenes.Contains(s.sceneName) || s.sceneName.IsNullOrEmpty() == true ));

        // 존재하지 않는 프리팹의 설정 정리
        prefabSettings.RemoveAll(p => string.IsNullOrEmpty(p.prefabName) ||
            UnityEditor.AssetDatabase.FindAssets($"t:Prefab {p.prefabName}").Length == 0);
#endif
    }


}