using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

public class LrcPlayer : MonoBehaviour
{
    [SerializeField]
    public LrcDictionaryAsset lrcDictionaryAsset;
    [SerializeField]
    public float speed = 1.0f;
    [SerializeField]
    public AudioSource audioSource;
    [SerializeField]
    public TextMeshProUGUI subtitle;

    [SerializeField] public bool isTimeLine = false;
    
    [SerializeField]
    public PlayableDirector director;

    private int currentLineIndex = 0;
    private void Start()
    {
        if (isTimeLine == false)
        {
            audioSource.Play();
        }
    }

    void Update()
    {
        var lyrics = lrcDictionaryAsset.ToDictionary();
        if (currentLineIndex < lyrics.Count)
        {
            if (isTimeLine == false)
            {
                if (audioSource.time >= lyrics.ElementAt(currentLineIndex).Key)
                {
                    subtitle.text = lyrics.ElementAt(currentLineIndex).Value;
                    currentLineIndex++;
                }
            }
            else
            {
                if (director.time >= lyrics.ElementAt(currentLineIndex).Key)
                {
                    subtitle.text = lyrics.ElementAt(currentLineIndex).Value;
                    currentLineIndex++;
                }
            }
        }
    }
}
