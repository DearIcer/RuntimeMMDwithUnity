using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

public class ProgressSlider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public PlayableDirector director;
    public Slider slider;
    private bool _isDragging = false;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }


    // Update is called once per frame
    void Update()
    {
        if (!_isDragging && director.state == PlayState.Playing)
        {
            slider.value = (float)director.time;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragging = true;
        director.time = slider.value;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;
        director.time = slider.value;
    }
}