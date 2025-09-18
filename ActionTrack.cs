using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [TrackColor(0.2f, 0.8f, 0.8f)]
    [TrackClipType(typeof(ActionClip))]
    [TrackBindingType(typeof(Transform))]
    public class ActionTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<ActionMixerBehaviour>.Create(graph, inputCount);
        }

        public TimelineClip CreateDefaultClip()
        {
            var clip = base.CreateDefaultClip();
            if (clip.asset == null)
            {
                clip.asset = ScriptableObject.CreateInstance<ActionClip>();
            }
            return clip;
        }
    }
}
