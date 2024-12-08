using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace SubtitlesSystem
{
    public class SubtitleTrackMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            TextMeshProUGUI textMeshPro = playerData as TextMeshProUGUI;
            string currentText = "";
            float currentAlpha = 0f;

            if (!textMeshPro) return;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                if (inputWeight > 0f)
                {
                    ScriptPlayable<SubtitleBehavior> inputPlayable = (ScriptPlayable<SubtitleBehavior>)playable.GetInput(i);
                    SubtitleBehavior input = inputPlayable.GetBehaviour();
                    currentText = input.SubtitleText;
                    currentAlpha = inputWeight;
                }
            }

            textMeshPro.text = currentText;
            // textMeshPro.color = new Color(1, 1, 1, currentAlpha);
            
        }
        
        
    }
}