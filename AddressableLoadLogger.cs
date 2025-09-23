using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;

public class AddressableLoadLogger : MonoBehaviour
{
    private HashSet<string> loggedPaths = new HashSet<string>();
    private string logFilePath = StringDefine.PATH_ADDRESSABLE_LOAD_LIST;
    private string dependenciesIssuePath = StringDefine.PATH_ADDRESSABLE_ISSUE_LIST;

    private HashSet<string> checkCharacters = new HashSet<string>()
    {
        "soll",
        "vulkan",
        "rem",
        "noah",
        "akane",
        "dwight",
    };

    private void Start()
    {
        if (File.Exists(logFilePath))
            File.Delete(logFilePath);

        //// 어드레서블 로드 콜백
        //Addressables.ResourceManager.RegisterDiagnosticCallback(OnDiagnosticEvent);
    }

    //private void OnDiagnosticEvent(ResourceManager.DiagnosticEventContext diagnosticEvent)
    //{
    //    var location = diagnosticEvent.Location;
    //    if (location != null)
    //    {
    //        string assetPath = location.InternalId;

    //        if (!loggedPaths.Contains(assetPath))
    //        {
    //            loggedPaths.Add(assetPath);

    //            string filteredPath = null;

    //            if (assetPath.StartsWith("https://"))
    //            {
    //                filteredPath = FilterRemotePath(assetPath);
    //            }
    //            else if (assetPath.StartsWith("Assets/"))
    //            {
    //                filteredPath = FilterLocalPath(assetPath);
    //            }
    //            else
    //            {
    //                // 여기 걸리면 카탈로그 등 다른 에셋임
    //                return;
    //            }

    //            if (string.IsNullOrEmpty(filteredPath))
    //                return;

    //            if (CheckDependenciesIssue(filteredPath))
    //                LogFilteredPath(filteredPath, dependenciesIssuePath);

    //            if (location.Dependencies != null)
    //            {
    //                foreach (var dep in location.Dependencies)
    //                {
    //                    if (CheckDependenciesIssue(dep.InternalId))
    //                        LogFilteredPath($"[{filteredPath}]{dep.InternalId}", dependenciesIssuePath);
    //                }
    //            }

    //            LogFilteredPath(filteredPath, logFilePath);
    //        }
    //    }
    //}

    private bool CheckDependenciesIssue(string path)
    {
        string lowerPath = path.ToLower();

        if (path.Contains("ui") || path.Contains("voice") || path.Contains("sound"))
            return false;

        return checkCharacters.Any(x =>
               {
                   if (path.Contains(x))
                   {
                       IronJade.Debug.LogError($"[Load MoveSet] {path}");
                       return true;
                   }

                   return false;
               });
    }

    private string FilterRemotePath(string assetPath)
    {
        try
        {
            // 원격 경로 필터링
            if (assetPath.Contains("assets_all"))
                return null;

            var startIndex = assetPath.IndexOf("assets_") + "assets_".Length;
            if (startIndex != -1)
            {
                var endIndex = assetPath.LastIndexOf("_");

                if (endIndex != -1 && endIndex > startIndex)
                    return assetPath.Substring(startIndex, endIndex - startIndex);
            }
        }
        catch (Exception e)
        {
            IronJade.Debug.LogError($"Failed to filter remote path: {e}");
        }

        return null;
    }

    private string FilterLocalPath(string assetPath)
    {
        try
        {
            var startIndex = assetPath.IndexOf("ResourcesAddressable/") + "ResourcesAddressable/".Length;

            //ResourcesAddressable 경로 + 확장자 제거

            if (startIndex != -1)
            {
                var endIndex = assetPath.LastIndexOf(".");

                if (endIndex != -1 && endIndex > startIndex)
                    return assetPath.Substring(startIndex, endIndex - startIndex);
            }
        }
        catch (Exception e)
        {
            IronJade.Debug.LogError($"Failed to filter local path: {e}");
        }

        return null;
    }

    private void LogFilteredPath(string filteredPath, string logFilePath)
    {
        try
        {
            filteredPath = filteredPath.ToLower();
            File.AppendAllText(logFilePath, filteredPath + "," + Environment.NewLine);

            //IronJade.Debug.Log($"[Addressable Loaded]: {filteredPath}");
        }
        catch (Exception e)
        {
            IronJade.Debug.LogError($"Fail to write path : {e}");
        }
    }

    private void OnDestroy()
    {
        //Addressables.ResourceManager.UnregisterDiagnosticCallback(OnDiagnosticEvent);
    }
}
