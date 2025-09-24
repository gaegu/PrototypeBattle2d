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
        return await UtilModel.Resources.LoadSceneAsync(sceneName, mode);
    }

    public async UniTask UnLoadSceneAsync(string sceneName)
    {
         await UtilModel.Resources.UnLoadSceneAsync(sceneName);
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
            tasks.Add(UtilModel.Resources.LoadSceneAsync(sceneName, mode));
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
            tasks.Add(UtilModel.Resources.UnLoadSceneAsync(sceneName));
        }

        await UniTask.WhenAll(tasks);
    }

    #endregion

    #region Scene Transition

    public async UniTask<Scene> TransitionSceneAsync(string fromScene, string toScene)
    {
        Debug.Log($"[ResourceService] Transitioning from {fromScene} to {toScene}");

        // 새 씬 로드
        var loadTask = UtilModel.Resources.LoadSceneAsync(toScene, LoadSceneMode.Additive);

        // 이전 씬 언로드
        var unloadTask = UtilModel.Resources.UnLoadSceneAsync(fromScene);

        await UniTask.WhenAll(loadTask, unloadTask);

        return await loadTask;
    }

    #endregion
}