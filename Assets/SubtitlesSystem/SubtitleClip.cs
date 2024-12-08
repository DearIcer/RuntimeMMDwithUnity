using UnityEngine;
using UnityEngine.Playables;

namespace SubtitlesSystem
{
    public class SubtitleClip : PlayableAsset
    {
        public string subtitleText;
    
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SubtitleBehavior>.Create(graph);
            SubtitleBehavior subtitleBehavior = playable.GetBehaviour();
            subtitleBehavior.SubtitleText = subtitleText;
            return playable;
        }
    }
}
