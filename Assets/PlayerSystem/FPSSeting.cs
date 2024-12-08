using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSSeting : MonoBehaviour
{
    // Start is called before the first frame update
    void Awake()
    {

    }
    void Start()
    {
        QualitySettings.vSyncCount = 0; // 禁用 VSync
        Application.targetFrameRate = 60; // 锁定帧率为 60 FPS
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
