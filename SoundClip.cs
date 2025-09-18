using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [System.Serializable]
    public class SoundClip : PlayableAsset, ITimelineClipAsset
    {
        public SoundClipData soundData = new SoundClipData();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SoundBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.soundData = soundData;
            return playable;
        }
    }
}
