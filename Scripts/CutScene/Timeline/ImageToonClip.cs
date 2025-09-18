using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [System.Serializable]
    public class ImageToonClip : PlayableAsset, ITimelineClipAsset
    {
        public ImageToonClipData imageToonData = new ImageToonClipData();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ImageToonBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.imageToonData = imageToonData;
            return playable;
        }
    }
}
