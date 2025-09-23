using UnityEngine;

public abstract class ServicedTownStateBase : TownStateBase
{
    protected readonly IServiceContainer serviceContainer;

    protected ServicedTownStateBase(IServiceContainer container = null)
    {
        serviceContainer = container;
    }

    // 자주 사용하는 서비스들 속성으로 제공
    protected IPlayerService PlayerService =>
        serviceContainer?.GetService<IPlayerService>();

    protected ITownSceneService TownSceneService =>
        serviceContainer?.GetService<ITownSceneService>();

    protected ITownObjectService TownObjectService =>
        serviceContainer?.GetService<ITownObjectService>();
}