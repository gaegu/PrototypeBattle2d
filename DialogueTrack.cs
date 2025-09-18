using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [TrackColor(0.855f, 0.8623f, 0.87f)]
    [TrackClipType(typeof(DialogueClip))]
    [TrackBindingType(typeof(DialogueUI))]
    public class DialogueTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<DialogueMixerBehaviour>.Create(graph, inputCount);
        }

        public TimelineClip CreateDefaultClip()
        {
            var clip = base.CreateDefaultClip();
            if (clip.asset == null)
            {
                clip.asset = ScriptableObject.CreateInstance<DialogueClip>();
            }
            return clip;
        }
    }
}
