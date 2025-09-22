using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

[TrackColor(0.8f, 0.2f, 0.8f)]  // 보라색 계열
[TrackClipType(typeof(CosmosCustomClip))]
[TrackBindingType(typeof(GameObject))]  // BattleActor GameObject 바인딩
[DisplayName("*COSMOS* Track/Add Custom Action Track")]
public class CosmosCustomTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        // 모든 클립의 이름 업데이트
        foreach (var clip in GetClips())
        {
            var customClip = clip.asset as CosmosCustomClip;
            if (customClip != null && customClip.actionType != CustomActionType.None)
            {
                clip.displayName = customClip.actionType.ToString();
            }
        }

        return ScriptPlayable<CosmosCustomMixerBehaviour>.Create(graph, inputCount);
    }


}

public class CosmosCustomMixerBehaviour : PlayableBehaviour
{
    // 필요시 믹서 로직 추가
}