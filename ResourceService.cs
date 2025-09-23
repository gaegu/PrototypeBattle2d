using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ResourceManager 싱글톤 래핑 서비스
/// </summary>
public class ResourceService : IResourceService
{
    private readonly Dictionary<string, Scene> loadedScenes = new Dictionary<string, Scene>();

    #region Scene Management

    public async UniTask<Scene> LoadSceneAsync(string sceneName, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[ResourceService] Scene name is null or empty");
            return default;
        }

        Debug.Log($"[ResourceService] Loading scene: {sceneName}, Mode: {mode}");

        try
        {
            // Unity Scene 로드
            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (asyncOperation == null)
            {
                Debug.LogError($"[ResourceService] Failed to start loading scene: {sceneName}");
                return default;
            }

            await asyncOperation;

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid())
            {
                loadedScenes[sceneName] = loadedScene;
                Debug.Log($"[ResourceService] Scene loaded successfully: {sceneName}");
            }

            return loadedScene;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ResourceService] Failed to load scene: {sceneName}, Error: {e.Message}");
            return default;
        }
    }

    public async UniTask UnLoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ResourceService] Scene name is null or empty");
            return;
        }

        if (!CheckLoadedScene(sceneName))
        {
            Debug.LogWarning($"[ResourceService] Scene not loaded: {sceneName}");
            return;
        }

        Debug.Log($"[ResourceService] Unloading scene: {sceneName}");

        try
        {
            var asyncOperation = SceneManager.UnloadSceneAsync(sceneName);
            if (asyncOperation != null)
            {
                await asyncOperation;
                loadedScenes.Remove(sceneName);
                Debug.Log($"[ResourceService] Scene unloaded successfully: {sceneName}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ResourceService] Failed to unload scene: {sceneName}, Error: {e.Message}");
        }
    }

    public bool CheckLoadedScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    public Scene GetScene(string sceneName)
    {
        if (loadedScenes.TryGetValue(sceneName, out Scene scene))
        {
            return scene;
        }

        return SceneManager.GetSceneByName(sceneName);
    }

    #endregion

    #region Memory Management

    public async UniTask UnloadUnusedAssets(bool isGC = false)
    {
        Debug.Log($"[ResourceService] Unloading unused assets, GC: {isGC}");

        var asyncOperation = Resources.UnloadUnusedAssets();
        await asyncOperation;

        if (isGC)
        {
            System.GC.Collect();
            Debug.Log("[ResourceService] GC.Collect executed");
        }

        Debug.Log("[ResourceService] Unused assets unloaded");
    }

    #endregion

    #region Batch Operations

    public async UniTask LoadScenesAsync(string[] sceneNames, LoadSceneMode mode)
    {
        if (sceneNames == null || sceneNames.Length == 0)
        {
            Debug.LogWarning("[ResourceService] No scenes to load");
            return;
        }

        var tasks = new List<UniTask<Scene>>();
        foreach (var sceneName in sceneNames)
        {
            tasks.Add(LoadSceneAsync(sceneName, mode));
        }

        await UniTask.WhenAll(tasks);
    }

    public async UniTask UnloadScenesAsync(string[] sceneNames)
    {
        if (sceneNames == null || sceneNames.Length == 0)
        {
            Debug.LogWarning("[ResourceService] No scenes to unload");
            return;
        }

        var tasks = new List<UniTask>();
        foreach (var sceneName in sceneNames)
        {
            tasks.Add(UnLoadSceneAsync(sceneName));
        }

        await UniTask.WhenAll(tasks);
    }

    #endregion

    #region Scene Transition

    public async UniTask<Scene> TransitionSceneAsync(string fromScene, string toScene)
    {
        Debug.Log($"[ResourceService] Transitioning from {fromScene} to {toScene}");

        // 새 씬 로드
        var loadTask = LoadSceneAsync(toScene, LoadSceneMode.Additive);

        // 이전 씬 언로드
        var unloadTask = UnLoadSceneAsync(fromScene);

        await UniTask.WhenAll(loadTask, unloadTask);

        return await loadTask;
    }

    #endregion
}