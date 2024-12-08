using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeManager : MonoBehaviour
{
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    private static VolumeManager instance;

    public static VolumeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<VolumeManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("VolumeManager");
                    instance = obj.AddComponent<VolumeManager>();
                }
            }
            return instance;
        }
    }

    private void Start()
    {
        SetMasterVolume(masterVolume);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = volume;
        AudioListener.volume = masterVolume;

        // 更新所有音频源的音量
        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in audioSources)
        {
            source.volume = masterVolume;
        }
    }
}