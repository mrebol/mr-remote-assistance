using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;
using UnityEngine.XR;
using Debug = UnityEngine.Debug;
using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

[Serializable]
public class CameraPoints
{
    public List<Vector3> points = new List<Vector3>(CommonParameters.numberOfBalls);
}

[Serializable]
public class SystemPoints
{ 
    public List<Vector3> holoPoints = new List<Vector3>();
    public List<CameraPoints> cameraPoints = new List<CameraPoints>(CommonParameters.numberOfCameras);
}

[Serializable]
public class CameraTransformations
{
    public List<MyTransformation> cameraTransformation = new List<MyTransformation>(Math.Max((byte)1, CommonParameters.numberOfCameras));
}

[Serializable]
public class MyTransformation
{
    public Vector3 translation;
    public Quaternion quaternion;
}

public class RelativeAlignment : MonoBehaviour
{
    public CameraTransformations cameraTransformation = new CameraTransformations();
    public Camera mainCamera;
    public Vector3 camera0Vector = Vector3.forward;
    public byte selectCamera = 0;
    public byte currentCamera = 0;
    public Text debugText3;
    public List<List<Vector3>> cameraPoints = new List<List<Vector3>>();
    public PeerConnection peerConnection;
    public Text rotationText;
    public Objects objects;
    
    public RectTransform canvas;
    public RectTransform canvasControls;
    
    private bool runScript = false;
    private bool readScript = false;
    private float startTime;
    private Process process;
    private float lastUpdate;
    public bool autoSwitchCamera;
    public PinchSlider cameraSlider;
    public IncomingFrameHandler incomingFrameHandler;
    
    // Start is called before the first frame update
    void Start()
    {
        for (int j = 0; j < CommonParameters.numberOfCameras; j++)
        {
            cameraPoints.Add(new List<Vector3>());
            for (int i = 0; i < CommonParameters.numberOfBalls; i++)
            {
                cameraPoints[j].Add(new Vector3(0, 0, 0));
            }

            var trans = new MyTransformation();
            trans.translation = Vector3.zero;
            trans.quaternion = Quaternion.identity;
            cameraTransformation.cameraTransformation.Add(trans); 
        }

        if (CommonParameters.numberOfCameras == 0)
        {
            var trans = new MyTransformation();
            trans.translation = Vector3.zero;
            trans.quaternion = Quaternion.identity;
            cameraTransformation.cameraTransformation.Add(trans);
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (CommonParameters.numberOfCameras == 2)
        {
            lastUpdate = Time.time;
            if (peerConnection.Peer != null && peerConnection.Peer.IsConnected && 
                cameraTransformation.cameraTransformation.Count == CommonParameters.numberOfCameras)
            {
                var camera0Forward =
                    Vector3.Normalize(IncomingFrameHandler.hologramTransform.rotation * camera0Vector);
                var camera1Forward =
                    Vector3.Normalize(IncomingFrameHandler.hologramTransform.rotation *
                                      Quaternion.Inverse(cameraTransformation.cameraTransformation[1].quaternion) *
                                      camera0Vector);
                if (autoSwitchCamera)
                {
                    if (Vector3.Dot(camera0Forward, mainCamera.transform.forward) >
                        Vector3.Dot(camera1Forward, mainCamera.transform.forward))
                    {
                        selectCamera = 0;
                    }
                    else
                    {
                        selectCamera = 1;
                    }
                }

                if (currentCamera != selectCamera)
                {
                    debugText3.text = "Select Camera: " + selectCamera;
                    if (CommonParameters.doSendSelectCamera)
                    {
                        byte[] cameraPacket = new byte[2];
                        cameraPacket[0] = CommonParameters.codeSelectCamera;
                        cameraPacket[1] = selectCamera;
                        if (CommonParameters.useWebRtcDataChannel)
                        {
                            peerConnection.selectCameraPacket.Enqueue(cameraPacket);
                            peerConnection.SendMessage("selectCamera");
                        }
                        else
                        {
                            WsClient.selectCameraPacket.Enqueue(cameraPacket);
                            WsClient.SendMessage("selectCamera", CommonParameters.LocalComputerId);
                        }
                    }

                    currentCamera = selectCamera;
                }
            }
            
            rotationText.text = "Hololens: " + mainCamera.transform.forward + "\n" +
                                "Camera0: " + (IncomingFrameHandler.hologramTransform.rotation * 
                                               Quaternion.Inverse(cameraTransformation.cameraTransformation[0].quaternion)*camera0Vector)+ "\n"+
                                "Camera1: " + (IncomingFrameHandler.hologramTransform.rotation * 
                                               Quaternion.Inverse(cameraTransformation.cameraTransformation[1].quaternion)*camera0Vector) ;

        } 
    }

    public void toggleCamera()
    {
        if (selectCamera == 0)
            selectCamera = 1;
        else if (selectCamera == 1)
            selectCamera = 0;

    }
        public void updateTime(SliderEventData data)
         {
             lastUpdate = data.NewValue * 10;
         }

        [ContextMenu("Auto switch camera toggle")]
        public void toggleAutoSwitchCamera()
        {
            autoSwitchCamera = !autoSwitchCamera;
            if (autoSwitchCamera)
            {
                cameraSlider.gameObject.SetActive(false);
            }
            else
            {
                cameraSlider.gameObject.SetActive(true);
            }

            print("Auto switch camera: " + autoSwitchCamera);
            
        }
        
        public void selectCameraSlider(SliderEventData data)
        {
            selectCamera = Convert.ToByte(Mathf.RoundToInt(data.NewValue));
        }
        
        [ContextMenu("Recenter UI")]
        public void recenterUI()
        {
            print("Recenter UI.");
            //InputTracking.Recenter();
            canvas.transform.position = mainCamera.transform.position + 0.5f * mainCamera.transform.forward;
            canvas.transform.forward = mainCamera.transform.forward;
        }
        

        [ContextMenu("Toggle Controls")]
        public void toggleControls()
        {
            canvasControls.gameObject.SetActive(!canvasControls.gameObject.activeSelf);
            print("Show Controls: " + canvasControls.gameObject.activeSelf);
        }
    
        public void updateCanvasPosition(SliderEventData data)
        {
            canvas.position = new Vector3(canvas.position.x, canvas.position.y, data.NewValue * 3 - 1);
        }
   

        [ContextMenu("Recenter Hologram")]
        public void recenterHologram()
        {
            print("Recenter Hologram.");
            IncomingFrameHandler.hologramTransform.position = mainCamera.transform.position + 0.0f * mainCamera.transform.forward;
            IncomingFrameHandler.hologramTransform.rotation = Quaternion.Euler(0, mainCamera.transform.rotation.eulerAngles.y, 0);
            // x .. +20 for horizontal camera rotation
            incomingFrameHandler.positionHolograms();
            
            objects.positionObjects(mainCamera.transform);

        }
        
        public void recenterHologramDelayed()
        {
            Invoke("recenterHologram",4);
        }
        
        public void recenterUIDelayed()
        {
            Invoke("recenterUI",4);
        }
        
        [ContextMenu("Recenter CS")]
        public void recenterCS()
        {
            print("Recenter CS.");
            UnityEngine.XR.InputTracking.Recenter();
        }
}
