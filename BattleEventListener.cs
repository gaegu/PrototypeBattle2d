using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


// 이벤트 리스너 컴포넌트 (MonoBehaviour용)
public class BattleEventListener : MonoBehaviour
{
    [System.Serializable]
    public class EventSubscription
    {
        public BattleEventType eventType;
        public UnityEvent<BattleEventData> response;
    }

    public List<EventSubscription> subscriptions = new List<EventSubscription>();

    private void OnEnable()
    {
        foreach (var subscription in subscriptions)
        {
            BattleEventManager.Instance.Subscribe(subscription.eventType, OnEventTriggered);
        }
    }

    private void OnDisable()
    {
        foreach (var subscription in subscriptions)
        {
            BattleEventManager.Instance.Unsubscribe(subscription.eventType, OnEventTriggered);
        }
    }

    private void OnEventTriggered(BattleEventData data)
    {
        var subscription = subscriptions.Find(s => s.eventType == data.EventType);
        subscription?.response?.Invoke(data);
    }
}