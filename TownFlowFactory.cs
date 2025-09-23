// TownFlowFactory.cs (새 파일)
using UnityEngine;
using IronJade.Flow.Core;

public class TownFlowFactory : IFlowFactory
{
    private static bool useNewTownFlow = false;

    /// <summary>
    /// TownFlow 인스턴스 생성
    /// </summary>
    public static BaseFlow CreateTownFlow()
    {
#if UNITY_EDITOR
        // 에디터에서 F9 키로 전환 가능
        if (Input.GetKeyDown(KeyCode.F9))
        {
            useNewTownFlow = !useNewTownFlow;
            Debug.Log($"[TownFlowFactory] Switched to {(useNewTownFlow ? "NEW" : "LEGACY")} TownFlow");
        }
#endif

        if (useNewTownFlow)
        {
            Debug.Log("[TownFlowFactory] Creating NewTownFlow");
            return new NewTownFlow();
        }
        else
        {
            Debug.Log("[TownFlowFactory] Creating Legacy TownFlow");
            return new TownFlow();
        }
    }

    /// <summary>
    /// 강제로 특정 버전 사용
    /// </summary>
    public static void ForceUseNewTownFlow(bool useNew)
    {
        useNewTownFlow = useNew;
        Debug.Log($"[TownFlowFactory] Force use {(useNew ? "NEW" : "LEGACY")} TownFlow");
    }

    /// <summary>
    /// 현재 사용 중인 버전 확인
    /// </summary>
    public static bool IsUsingNewTownFlow()
    {
        return useNewTownFlow;
    }

    public bool CanHandle(FlowType flowType)
    {
        return flowType == FlowType.TownFlow;
    }

    public BaseFlow CreateFlow(FlowType flowType)
    {
        if (flowType != FlowType.TownFlow)
            return null;

        return useNewTownFlow ? new NewTownFlow() : new TownFlow();
    }
}