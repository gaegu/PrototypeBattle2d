using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

[TrackColor(1f, 0.2f, 0.2f)]
[TrackClipType(typeof(CosmosBattleEventClip))]
[DisplayName("*COSMOS* Track/Battle Event Track")]
public class CosmosBattleEventTrack : TrackAsset
{
    [Header("Track Settings")]
    [SerializeField] private bool validateDamagePercent = true;
    [SerializeField] private bool allowOverlappingEvents = false;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        // 데미지 퍼센트 검증
        if (validateDamagePercent)
        {
            ValidateDamagePercentages();
        }

        return ScriptPlayable<CosmosBattleEventMixerBehaviour>.Create(graph, inputCount);
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        base.GatherProperties(director, driver);
    }

    private void ValidateDamagePercentages()
    {
        var clips = GetClips().ToList();
        float totalDamagePercent = 0f;

        // Hit 이벤트의 총 데미지 퍼센트 계산
        foreach (var clip in clips)
        {
            var eventClip = clip.asset as CosmosBattleEventClip;
            if (eventClip != null &&
                eventClip.eventType == CosmosBattleEventClip.EventType.Hit &&
                eventClip.hitType == CosmosBattleEventClip.HitType.RealHit)
            {
                totalDamagePercent += eventClip.damagePercent;
            }
        }

        // 100%를 초과하면 자동 조정
        if (totalDamagePercent > 100f)
        {
            float scale = 100f / totalDamagePercent;
            foreach (var clip in clips)
            {
                var eventClip = clip.asset as CosmosBattleEventClip;
                if (eventClip != null &&
                    eventClip.eventType == CosmosBattleEventClip.EventType.Hit &&
                    eventClip.hitType == CosmosBattleEventClip.HitType.RealHit)
                {
                    eventClip.damagePercent *= scale;
                }
            }

#if UNITY_EDITOR
            Debug.LogWarning($"[CosmosBattleEventTrack] Total damage percent exceeded 100%. Auto-adjusted to fit.");
#endif
        }
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);

        // 클립 기본 설정
        clip.displayName = "Battle Event";
        clip.duration = 0.5f;

        var eventClip = clip.asset as CosmosBattleEventClip;
        if (eventClip != null)
        {
            // 기본값 설정
            eventClip.duration = (float)clip.duration;
        }
    }

#if UNITY_EDITOR
    public void RefreshClips()
    {
        // 에디터에서 클립 새로고침
        ValidateDamagePercentages();
    }

    public float GetTotalDamagePercent()
    {
        float total = 0f;
        foreach (var clip in GetClips())
        {
            var eventClip = clip.asset as CosmosBattleEventClip;
            if (eventClip != null &&
                eventClip.eventType == CosmosBattleEventClip.EventType.Hit &&
                eventClip.hitType == CosmosBattleEventClip.HitType.RealHit)
            {
                total += eventClip.damagePercent;
            }
        }
        return total;
    }
#endif
}