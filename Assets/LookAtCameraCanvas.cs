using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtCameraCanvas : MonoBehaviour
{

    Camera camera;
    private void Start()
    {
        camera=Camera.main;
    }


    // Update is called once per frame
    void Update()
    {
        transform.LookAt(-camera.transform.position,Vector2.up);
    }
}
