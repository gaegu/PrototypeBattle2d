// TownFlowDebugMenu.cs (새 파일 - 선택사항)
#if UNITY_EDITOR
using UnityEngine;

public class TownFlowDebugMenu : MonoBehaviour
{
    private bool showDebugMenu = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            showDebugMenu = !showDebugMenu;
        }
    }

    void OnGUI()
    {
        if (!showDebugMenu) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Box("TownFlow Debug Menu");

        bool isUsingNew = TownFlowFactory.IsUsingNewTownFlow();
        GUILayout.Label($"Current: {(isUsingNew ? "NEW" : "LEGACY")} TownFlow");

        if (GUILayout.Button("Switch to " + (isUsingNew ? "LEGACY" : "NEW")))
        {
            TownFlowFactory.ForceUseNewTownFlow(!isUsingNew);
        }

        if (GUILayout.Button("Reload TownFlow"))
        {
            // 현재 Flow 재시작
            Debug.Log("Reloading TownFlow...");
        }

        GUILayout.EndArea();
    }
}
#endif