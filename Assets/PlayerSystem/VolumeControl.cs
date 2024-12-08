using UnityEngine;
using UnityEngine.UI;

public class VolumeControl : MonoBehaviour
{
    public Slider volumeSlider;

    void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener((value) => VolumeManager.Instance.SetMasterVolume(value));
        }
    }
}