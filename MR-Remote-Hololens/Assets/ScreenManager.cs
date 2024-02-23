using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    public GameObject remoteWebcam;
    public Transform videoScreenTransform;
    public Transform usScreenTransform;
    private float lastTimeRead;
    public IncomingScreenTransformHandler IncomingScreenTransformHandler;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time > (lastTimeRead + (1.0 * 2)) && !IncomingScreenTransformHandler.localScreenPosition) // FPS
        {
            lastTimeRead = Time.time;
            videoScreenTransform.Rotate(0,0,-videoScreenTransform.rotation.eulerAngles.z);
            usScreenTransform.Rotate(0,0,-usScreenTransform.rotation.eulerAngles.z);
            remoteWebcam.transform.Rotate(0,0,-remoteWebcam.transform.rotation.eulerAngles.z);
        }
    }

    public void toggleWebcamScreen()
    {
        remoteWebcam.SetActive(!remoteWebcam.activeSelf);
    }
}
