using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [TrackColor(0.8f, 0.2f, 0.8f)]
    [TrackClipType(typeof(CustomClip))]
    [TrackBindingType(typeof(GameObject))]
    public class CustomTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<CustomMixerBehaviour>.Create(graph, inputCount);
        }

        public TimelineClip CreateDefaultClip()
        {
            var clip = base.CreateDefaultClip();
            if (clip.asset == null)
            {
                clip.asset = ScriptableObject.CreateInstance<CustomClip>();
            }
            return clip;
        }
    }
}
