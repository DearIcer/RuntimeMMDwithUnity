using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SubtitlesSystem
{
    [TrackBindingType(typeof(TextMeshProUGUI))]
    [TrackClipType(typeof(SubtitleClip))]
    public class SubtitleTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SubtitleTrackMixer>.Create(graph, inputCount);
        }
        
    }
}
