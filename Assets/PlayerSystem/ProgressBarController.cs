using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ProgressBarController : MonoBehaviour
{
    private bool _isProgressBarVisible;
    public float hideDelay = 1.0f; // 鼠标停止移动后隐藏进度条的延迟时间
    private float _lastMouseMoveTime;
    private Canvas _canvas;
    private Animator _animator;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        _lastMouseMoveTime = Time.time;
        _isProgressBarVisible = false;
    }
    void Update()
    {
        if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
        {
            _lastMouseMoveTime = Time.time;
            if (!_isProgressBarVisible)
            {
                Debug.Log("Show");
                _animator.Play("Show");
                _isProgressBarVisible = true;
            }
        }
        else
        {
            // 如果鼠标停止移动超过hideDelay时间，则播放隐藏动画
            if (Time.time - _lastMouseMoveTime > hideDelay && _isProgressBarVisible)
            {
                Debug.Log("Hide");
                _animator.Play("Hide");
                _isProgressBarVisible = false;
            }
        }
        
    }
}
