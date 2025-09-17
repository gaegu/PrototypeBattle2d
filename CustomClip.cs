using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [System.Serializable]
    public class CustomClip : PlayableAsset, ITimelineClipAsset
    {
        public CustomClipData customData = new CustomClipData();
        
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CustomBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.customData = customData;
            return playable;
        }
    }
}
