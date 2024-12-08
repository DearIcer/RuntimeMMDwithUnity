using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

public class TrackManger : MonoBehaviour
{
    private PlayableDirector director;
    public Slider slider;
    public Canvas EndCanvas;

    public void Awake()
    {
        director = GetComponent<PlayableDirector>();
    }

    void Start()
    {
        director = GetComponent<PlayableDirector>();
        slider.maxValue = (float)director.duration;
        Play();
    }
    
    public void Update()
    {
        if ((int)slider.value == (int)slider.maxValue)
        {
            EndCanvas.gameObject.SetActive(true);
        }
    }

    public void Play()
    {
        if (director.state == PlayState.Paused)
        {
            director.Play();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        director.Pause();
    }

    public void Resume()
    {
        director.Resume();
    }

    public void Stop()
    {
        slider.value = slider.maxValue;
        director.time = director.duration;
        director.Stop();
        EndCanvas.gameObject.SetActive(true);
    }
}