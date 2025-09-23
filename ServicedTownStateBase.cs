using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class ServicedTownStateBase : TownStateBase
{
    protected readonly IServiceContainer Container;

    // 서비스 캐싱
    protected ITownSceneService TownSceneService { get; private set; }
    protected IPlayerService PlayerService { get; private set; }
    protected ITownObjectService TownObjectService { get; private set; }
    protected IMissionService MissionService { get; private set; }
    protected IResourceService ResourceService { get; private set; }

    // UI 매니저 래핑 (싱글톤 격리)
    protected IUIService UIService { get; private set; }

    protected ServicedTownStateBase(IServiceContainer container = null)
    {
        Container = container;

        if (container != null)
        {
            // 서비스 초기화
            TownSceneService = container.GetService<ITownSceneService>();
            PlayerService = container.GetService<IPlayerService>();
            TownObjectService = container.GetService<ITownObjectService>();
            MissionService = container.GetService<IMissionService>();
            ResourceService = container.GetService<IResourceService>();
            UIService = container.GetService<IUIService>();
        }
    }

    // 싱글톤 폴백 헬퍼 (점진적 마이그레이션용)
    protected T GetServiceOrSingleton<T>(T service, System.Func<T> singletonGetter) where T : class
    {
        return service ?? singletonGetter?.Invoke();
    }
}