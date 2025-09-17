using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace IronJade.ResourcesAddressable._2DRenewal.PortraitNew
{
    [System.Serializable]
    public class DialogueClip : PlayableAsset, ITimelineClipAsset
    {
        public DialogueClipData dialogueData = new DialogueClipData();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogueBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.dialogueData = dialogueData;
            return playable;
        }
    }
}
