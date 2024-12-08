using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace SubtitlesSystem
{
    public class SubtitleBehavior : PlayableBehaviour
    {
        public string SubtitleText;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            TextMeshProUGUI textMeshPro = playerData as TextMeshProUGUI;
            if (textMeshPro != null)
            {
                textMeshPro.text = SubtitleText;
            }
        }
    }
}