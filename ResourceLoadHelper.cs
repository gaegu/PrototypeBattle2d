using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class ResourceLoadHelper
{
    private static int timeoutMs = 3000; //3초후 에러 

    public static async UniTask<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
    {
        try
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeoutMs);

            var handle = Addressables.LoadAssetAsync<T>(address);
            var result = await handle.Task.ConfigureAwait(false);

            if (result == null)
            {
                throw new Exception($"Asset is null: {address}");
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            Debug.LogError($"[ResourceHelper] Load timeout - {address}");
            return null;
        }
    }


    //같은 형식 여러개 가져올때. ex> Sprite
    public static async UniTask<T[]> LoadAssetsAsync<T>(string address) where T : UnityEngine.Object
    {
        try
        {
            // 새로 로드
            Debug.Log($"[ResourceHelper] Loading asset: {address}");
            var handle = Addressables.LoadAssetAsync<T[]>(address);
            var result = await handle.Task;
            if (result == null)
            {
                throw new Exception($"Asset is null: {address}");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ResourceHelper] Load failed - {address}: {e.Message}");
            return null;
        }

    }

}