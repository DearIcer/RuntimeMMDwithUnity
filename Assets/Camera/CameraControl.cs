using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [SerializeField]
    public Camera TargetCamera;

    [SerializeField]
    public GameObject TargetBone;
    
    [SerializeField] public Vector3 OffseTransform;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        TargetCamera.transform.position = TargetBone.transform.position + OffseTransform;
    }
}
