
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


// 이벤트 매니저 (싱글톤)
public class BattleEventManager : MonoBehaviour
{
    private static BattleEventManager instance;
    public static BattleEventManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("BattleEventManager");
                instance = go.AddComponent<BattleEventManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // 이벤트 딕셔너리
    private Dictionary<BattleEventType, List<Action<BattleEventData>>> eventListeners;

    // 이벤트 히스토리 (디버깅용)
    private List<BattleEventData> eventHistory;
    private const int MAX_HISTORY_SIZE = 100;

    // 이벤트 필터 (특정 조건의 이벤트만 처리)
    private Dictionary<BattleEventType, Func<BattleEventData, bool>> eventFilters;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        eventListeners = new Dictionary<BattleEventType, List<Action<BattleEventData>>>();
        eventHistory = new List<BattleEventData>();
        eventFilters = new Dictionary<BattleEventType, Func<BattleEventData, bool>>();

        // 모든 이벤트 타입 초기화
        foreach (BattleEventType eventType in Enum.GetValues(typeof(BattleEventType)))
        {
            eventListeners[eventType] = new List<Action<BattleEventData>>();
        }
    }

    // 이벤트 구독
    public void Subscribe(BattleEventType eventType, Action<BattleEventData> listener)
    {
        if (eventListeners.ContainsKey(eventType))
        {
            eventListeners[eventType].Add(listener);
        }
        else
        {
            Debug.LogWarning($"Event type {eventType} not found in listeners dictionary");
        }
    }

    // 이벤트 구독 해제
    public void Unsubscribe(BattleEventType eventType, Action<BattleEventData> listener)
    {
        if (eventListeners.ContainsKey(eventType))
        {
            eventListeners[eventType].Remove(listener);
        }
    }

    // 모든 이벤트 구독 해제
    public void UnsubscribeAll(BattleEventType eventType)
    {
        if (eventListeners.ContainsKey(eventType))
        {
            eventListeners[eventType].Clear();
        }
    }

    // 이벤트 발생
    public void TriggerEvent(BattleEventType eventType, BattleEventData data)
    {
        // 히스토리에 추가
        AddToHistory(data);

        // 필터 확인
        if (eventFilters.ContainsKey(eventType))
        {
            if (!eventFilters[eventType](data))
            {
                Debug.Log($"Event {eventType} filtered out");
                return;
            }
        }

        // 리스너들에게 알림
        if (eventListeners.ContainsKey(eventType))
        {
            // 리스너 복사본 생성 (순회 중 수정 방지)
            var listeners = new List<Action<BattleEventData>>(eventListeners[eventType]);

            foreach (var listener in listeners)
            {
                try
                {
                    listener?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in event listener for {eventType}: {e.Message}");
                }
            }
        }

        // 디버그 로그
        if (Debug.isDebugBuild)
        {
            Debug.Log($"[Event] {eventType} triggered at {data.Timestamp}");
        }
    }

    // 필터 설정
    public void SetEventFilter(BattleEventType eventType, Func<BattleEventData, bool> filter)
    {
        eventFilters[eventType] = filter;
    }

    // 필터 제거
    public void RemoveEventFilter(BattleEventType eventType)
    {
        eventFilters.Remove(eventType);
    }

    // 히스토리에 추가
    private void AddToHistory(BattleEventData data)
    {
        eventHistory.Add(data);

        // 최대 크기 초과시 오래된 것 제거
        if (eventHistory.Count > MAX_HISTORY_SIZE)
        {
            eventHistory.RemoveAt(0);
        }
    }

    // 히스토리 조회
    public List<BattleEventData> GetHistory(BattleEventType? filterType = null)
    {
        if (filterType.HasValue)
        {
            return eventHistory.FindAll(e => e.EventType == filterType.Value);
        }
        return new List<BattleEventData>(eventHistory);
    }

    // 히스토리 클리어
    public void ClearHistory()
    {
        if( eventHistory != null )
            eventHistory.Clear();
    }

    // 모든 리스너 제거
    public void ClearAllListeners()
    {
        if (eventListeners != null)
        {
            foreach (var kvp in eventListeners)
            {
                kvp.Value.Clear();
            }
        }
    }

    // 특정 액터 관련 이벤트만 구독하는 헬퍼 메서드
    public void SubscribeToActor(BattleActor actor, BattleEventType eventType, Action<BattleEventData> listener)
    {
        Action<BattleEventData> filteredListener = (data) =>
        {
            bool shouldProcess = false;

            // 이벤트 타입에 따라 액터 확인
            if (data is TurnEventData turnData)
            {
                shouldProcess = turnData.Actor == actor;
            }
            else if (data is ActionEventData actionData)
            {
                shouldProcess = actionData.Actor == actor || actionData.Target == actor;
            }
            else if (data is DamageEventData damageData)
            {
                shouldProcess = damageData.Source == actor || damageData.Target == actor;
            }
            else if (data is StatusEventData statusData)
            {
                shouldProcess = statusData.Actor == actor;
            }

            if (shouldProcess)
            {
                listener(data);
            }
        };

        Subscribe(eventType, filteredListener);
    }

    private void OnDestroy()
    {
        ClearAllListeners();
        ClearHistory();
    }
}