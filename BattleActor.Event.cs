using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using SkillSystem;
using System;
using BattleCharacterSystem.Timeline;
using Cosmos.Timeline.Playback;
using FMOD;


/// <summary>
/// BattleActor의 부분 클래스 - 스킬 시스템 통합
/// </summary>
public partial class BattleActor : MonoBehaviour
{
    #region Timeline Components

    private CosmosPlaybackSystem playbackSystem;
    private TimelineEventHandler eventHandler;
    private TimelineDataSO currentTimeline;

    private bool debugMode = false;

    #endregion

    #region Timeline Initialization

    /// <summary>
    /// Timeline 시스템 초기화
    /// </summary>
    private void InitializeTimelineSystem()
    {
        // CosmosPlaybackSystem 가져오기/생성
        playbackSystem = GetComponent<CosmosPlaybackSystem>();
        if (playbackSystem == null)
        {
            playbackSystem = gameObject.AddComponent<CosmosPlaybackSystem>();
        }
        playbackSystem.InitializeSystem(this);


        // TimelineEventHandler 가져오기/생성
        eventHandler = GetComponent<TimelineEventHandler>();
        if (eventHandler == null)
        {
            eventHandler = gameObject.AddComponent<TimelineEventHandler>();
        }

        eventHandler.InitializeForBattle(this);

        // 이벤트 구독
        SubscribeTimelineEvents();



        UnityEngine.Debug.Log($"[BattleActor.Timeline] Timeline system initialized for {name}");
    }

    #endregion

    #region Event Subscription

    private void SubscribeTimelineEvents()
    {
        if (eventHandler == null) return;

        // Timeline 이벤트 구독
        eventHandler.OnEventTriggered += HandleTimelineEventTriggered;
        eventHandler.OnEventCompleted += HandleTimelineEventCompleted;
    }

    private void UnsubscribeTimelineEvents()
    {
        if (eventHandler == null) return;

        eventHandler.OnEventTriggered -= HandleTimelineEventTriggered;
        eventHandler.OnEventCompleted -= HandleTimelineEventCompleted;
    }

    #endregion
    #region Event Handlers

    /// <summary>
    /// Timeline 이벤트 시작 처리
    /// </summary>
    private void HandleTimelineEventTriggered(TimelineDataSO.ITimelineEvent timelineEvent)
    {
        switch (timelineEvent)
        {
            case TimelineDataSO.AnimationEvent animEvent:
                //OnTimelineAnimation(animEvent);
                break;

            case TimelineDataSO.EffectEvent effectEvent:
                //OnTimelineEffect(effectEvent);
                break;

            case TimelineDataSO.SoundEvent soundEvent:
                //OnTimelineSound(soundEvent);
                break;

            case TimelineDataSO.CustomEvent customEvent:
                OnTimelineCustomEvent(customEvent);
                break;
        }
    }

    /// <summary>
    /// Timeline 이벤트 완료 처리
    /// </summary>
    private void HandleTimelineEventCompleted(TimelineDataSO.ITimelineEvent timelineEvent)
    {
        // 필요시 완료 처리
        if (debugMode)
        {
            UnityEngine.Debug.Log($"[BattleActor.Timeline] Event completed: {timelineEvent.GetType().Name} at {timelineEvent.TriggerTime}");
        }
    }

    #endregion

    #region Event Processing




    /// <summary>
    /// 커스텀 이벤트 처리 (Battle 특화 이벤트)
    /// </summary>
    private void OnTimelineCustomEvent(TimelineDataSO.CustomEvent customEvent)
    {
        switch (customEvent.eventName.ToLower())
        {
            case "damage":
            case "hit":
                break;

            case "heal":
                break;

            case "buff":
                break;

            case "projectile":
                break;

            case "camera_shake":
                CamShake.shake(1);
                break;

            default:
                // BattleEventManager로 전달
                if (BattleEventManager.Instance != null)
                {
                    var eventData = new TimelineCustomEventData
                    {
                        EventName = customEvent.eventName,
                        Actor = this,
                        Parameters = customEvent.parameters
                    };
                    BattleEventManager.Instance.TriggerEvent(BattleEventType.TimelineCustom, eventData);
                }
                break;
        }

    }

   

    #region Cleanup

    private void CleanupTimeline()
    {
        UnsubscribeTimelineEvents();
        StopTimeline();
        currentTimeline = null;
    }

    #endregion


    #endregion

}


