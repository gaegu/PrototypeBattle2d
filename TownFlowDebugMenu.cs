using UnityEngine;

public class TownFlowDebugMenu : MonoBehaviour
{
    private bool showDebugMenu = false;
    private Rect windowRect = new Rect(20, 20, 250, 150);

    void Update()
    {
        // F12로 디버그 메뉴 토글
        if (Input.GetKeyDown(KeyCode.F12))
        {
            showDebugMenu = !showDebugMenu;
        }
    }

    void OnGUI()
    {
        if (!showDebugMenu) return;

        windowRect = GUI.Window(0, windowRect, DrawDebugWindow, "TownFlow Debug");
    }

    void DrawDebugWindow(int windowID)
    {
        GUILayout.BeginVertical();

        // 현재 아키텍처 표시
        bool isNewArch = TownFlowFactory.IsUsingNewArchitecture();
        GUILayout.Label($"Current: {(isNewArch ? "New Architecture" : "Legacy")}");

        // 아키텍처 전환 버튼
        if (GUILayout.Button(isNewArch ? "Switch to Legacy" : "Switch to New"))
        {
            TownFlowFactory.SetUseNewArchitecture(!isNewArch);

            // Flow 재시작 필요 알림
            Debug.LogWarning("[TownFlowDebugMenu] Architecture changed. Restart flow to apply.");
        }

        // State Machine 정보 (New Architecture만)
        if (isNewArch)
        {
            var currentFlow = FlowManager.Instance?.CurrentFlow as NewTownFlow;
            if (currentFlow != null)
            {
                GUILayout.Label($"State: {currentFlow.Model.CurrentUI}");
                GUILayout.Label($"Loading: {currentFlow.Model.IsLoading}");
            }
        }

        // Flow 재시작 버튼
        if (GUILayout.Button("Restart TownFlow"))
        {
            RestartTownFlow();
        }

        GUILayout.EndVertical();

        GUI.DragWindow();
    }

    private async void RestartTownFlow()
    {
        Debug.Log("[TownFlowDebugMenu] Restarting TownFlow...");

        // 현재 Flow 종료
        if (FlowManager.Instance?.CurrentFlow != null)
        {
            await FlowManager.Instance.CurrentFlow.Exit();
        }

        // 새 Flow 생성 및 시작
        ITownFlow newFlow = TownFlowFactory.CreateTownFlow();
        newFlow.Enter();

        Debug.Log("[TownFlowDebugMenu] TownFlow restarted");
    }
}