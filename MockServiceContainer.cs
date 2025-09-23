using System;
using System.Collections.Generic;
using UnityEngine;

public class MockServiceContainer : IServiceContainer
{
    private readonly Dictionary<Type, object> mockServices = new Dictionary<Type, object>();

    public T GetService<T>() where T : class
    {
        Type serviceType = typeof(T);

        if (mockServices.TryGetValue(serviceType, out object service))
        {
            return service as T;
        }

        // Mock 서비스가 없으면 자동 생성 (Moq 라이브러리 사용 시)
        return CreateMockService<T>();
    }

    public void RegisterService<T>(T service) where T : class
    {
        mockServices[typeof(T)] = service;
    }

    private T CreateMockService<T>() where T : class
    {
        // 간단한 Mock 구현 (실제로는 Moq 사용 권장)
        Debug.Log($"[MockServiceContainer] Creating mock for {typeof(T).Name}");
        return null;
    }
}