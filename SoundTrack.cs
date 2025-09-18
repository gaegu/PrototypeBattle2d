using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [TrackColor(0.8f, 0.2f, 0.2f)]
    [TrackClipType(typeof(SoundClip))]
    [TrackBindingType(typeof(AudioSource))]
    public class SoundTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SoundMixerBehaviour>.Create(graph, inputCount);
        }

        public TimelineClip CreateDefaultClip()
        {
            var clip = base.CreateDefaultClip();
            if (clip.asset == null)
            {
                clip.asset = ScriptableObject.CreateInstance<SoundClip>();
            }
            return clip;
        }
    }
}
