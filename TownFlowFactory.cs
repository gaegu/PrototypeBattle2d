using IronJade.Flow.Core;
using UnityEngine;

public class TownFlowFactory : IFlowFactory
{
    private static bool useNewTownFlow = true; // 새 아키텍처 사용 플래그
    public FlowType GetFlowType()
    {
        return FlowType.TownFlow;
    }
    public BaseFlow CreateFlow()
    {
        return CreateTownFlow() as BaseFlow;
    }

    public static ITownFlow CreateTownFlow()
    {
        // 환경 변수나 설정으로 제어 가능
#if UNITY_EDITOR
        useNewTownFlow = UnityEditor.EditorPrefs.GetBool("UseNewTownFlow", true);
#endif

        if (useNewTownFlow)
        {
            Debug.Log("[TownFlowFactory] Creating NewTownFlow (Modular Architecture)");

            // 서비스 컨테이너 생성
            IServiceContainer serviceContainer = new TownServiceContainer();

            // 서비스 검증
            if (!ValidateServices(serviceContainer))
            {
                Debug.LogWarning("[TownFlowFactory] Service validation failed, falling back to legacy TownFlow");
                return new TownFlow();
            }

            return new NewTownFlow(serviceContainer);
        }
        else
        {
            Debug.Log("[TownFlowFactory] Creating Legacy TownFlow");
            return new TownFlow();
        }
    }

    private static bool ValidateServices(IServiceContainer container)
    {
        // 필수 서비스 체크
        bool hasRequiredServices =
            container.HasService<ITownSceneService>() &&
            container.HasService<IPlayerService>() &&
            container.HasService<ITownObjectService>() &&
            container.HasService<IResourceService>();

        if (!hasRequiredServices)
        {
            Debug.LogError("[TownFlowFactory] Required services missing");
        }

        return hasRequiredServices;
    }

    // 런타임 전환 지원
    public static void SetUseNewArchitecture(bool useNew)
    {
        useNewTownFlow = useNew;
        Debug.Log($"[TownFlowFactory] Architecture switched to: {(useNew ? "New" : "Legacy")}");

#if UNITY_EDITOR
        UnityEditor.EditorPrefs.SetBool("UseNewTownFlow", useNew);
#endif
    }

    public static bool IsUsingNewArchitecture()
    {
        return useNewTownFlow;
    }
}