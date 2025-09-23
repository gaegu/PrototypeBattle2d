using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;

public interface IServiceContainer
{
    T GetService<T>() where T : class;
    void RegisterService<T>(T service) where T : class;
    bool HasService<T>() where T : class;
    void UnregisterService<T>() where T : class;
}

public class TownServiceContainer : IServiceContainer
{
    private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

    public TownServiceContainer()
    {
        // 기본 서비스 등록
        RegisterDefaultServices();
    }

    private void RegisterDefaultServices()
    {
        // 서비스 인스턴스 생성 및 등록
        RegisterService<ITownSceneService>(new TownSceneService());
        RegisterService<IPlayerService>(new PlayerService());
        RegisterService<ITownObjectService>(new TownObjectService());
        RegisterService<IMissionService>(new MissionService());
        RegisterService<IResourceService>(new ResourceService());
        RegisterService<IUIService>(new UIServiceWrapper()); // 추가
        RegisterService<INetworkService>(new NetworkServiceWrapper()); // 추가
        RegisterService<ICameraService>(new CameraServiceWrapper()); // 추가


        Debug.Log($"[TownServiceContainer] Registered {services.Count} services");
    }

    public T GetService<T>() where T : class
    {
        Type serviceType = typeof(T);

        if (services.TryGetValue(serviceType, out object service))
        {
            return service as T;
        }

        Debug.LogWarning($"[TownServiceContainer] Service {serviceType.Name} not found");
        return null;
    }

    public void RegisterService<T>(T service) where T : class
    {
        Type serviceType = typeof(T);

        if (services.ContainsKey(serviceType))
        {
            Debug.LogWarning($"[TownServiceContainer] Service {serviceType.Name} already registered");
            return;
        }

        services[serviceType] = service;
        Debug.Log($"[TownServiceContainer] Registered service: {serviceType.Name}");
    }

    public bool HasService<T>() where T : class
    {
        Type serviceType = typeof(T);
        return services.ContainsKey(serviceType);
    }

    public void UnregisterService<T>() where T : class
    {
        Type serviceType = typeof(T);

        if (services.Remove(serviceType))
        {
            Debug.Log($"[TownServiceContainer] Unregistered service: {serviceType.Name}");
        }
        else
        {
            Debug.LogWarning($"[TownServiceContainer] Service {serviceType.Name} not found for unregistration");
        }
    }
}