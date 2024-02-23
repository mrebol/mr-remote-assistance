using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.OpenXR;
using Microsoft.MixedReality.Toolkit;
//using Microsoft.MixedReality.Toolkit.Experimental.SurfacePulse;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEngine;
using Handedness = Microsoft.MixedReality.Toolkit.Utilities.Handedness;
using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

public class HandRotation : MonoBehaviour
{
    public RiggedHandVisualizerMR handVisualizerRight;
    public RiggedHandVisualizerMR handVisualizerLeft;
    
    private float lastTimeSend;
    private float lastTimeConnect;
    public PeerConnection peerConnection;
    public static Vector3 pointer;

    public Dictionary<TrackedHandJoint, GameObject> cubesR = new Dictionary<TrackedHandJoint, GameObject>();
    public Dictionary<TrackedHandJoint, GameObject> cubesL = new Dictionary<TrackedHandJoint, GameObject>();

    public Vector3 cameraForward;
    public float handDistance = 0.5f;
    public bool showLongArms = false;
    private GameObject handRGO;
    private GameObject handLGO;

    public bool showGhostHands = true;
    Quaternion correctionHandModelRotationRight = Quaternion.Euler(0,0,0 ); // order: Z, X, Y
    Quaternion correctionHandModelRotationLeft = Quaternion.Euler(0,0,0); // order: Z, X, Y
    
    public Transform alignmentCube;

    private Vector3 initialAlignmentCubePosition;
    public Transform debugCube;
    
    public Camera Camera;

    private int rightHandTrackedHistory ;
    private int LeftHandTrackedHistory;
    private float lastTimeTrackingChecked;
    
    // Start is called before the first frame update
    void Start()
    {
        HandData.initHand();
        
        // Turn off all hand rays
        PointerUtils.SetHandRayPointerBehavior(PointerBehavior.AlwaysOff);
        initialAlignmentCubePosition = alignmentCube.position;
    }


    public static void initAbstractHandModel(out GameObject handRGO, out GameObject handLGO, Dictionary<TrackedHandJoint, GameObject> cubesR, Dictionary<TrackedHandJoint, GameObject> cubesL)
    
    {
        
        Array handJoints = Enum.GetValues(typeof(TrackedHandJoint));
        if (true)
        {
            handRGO = new GameObject("AbstractHandR");
            handLGO = new GameObject("AbstractHandL");
            foreach (TrackedHandJoint handJoint in handJoints)
            {
                if(handJoint == TrackedHandJoint.None)
                    continue;
                if (handJoint.ToString().Contains("Tip"))
                {
                    var sphereR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphereR.name = handJoint.ToString();
                    sphereR.transform.SetParent(handRGO.transform);
                    sphereR.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    sphereR.GetComponent<Renderer>().material.color = Color.cyan;
                    cubesR.Add(handJoint, sphereR);
                    


                    var sphereL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphereL.name = handJoint.ToString();
                    sphereL.transform.SetParent(handLGO.transform);
                    sphereL.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    sphereL.GetComponent<Renderer>().material.color = Color.cyan;
                    cubesL.Add(handJoint, sphereL);
                }
                else
                {
                    var cubeR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubeR.name = handJoint.ToString();
                    cubeR.transform.SetParent(handRGO.transform);
                    cubeR.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    cubeR.GetComponent<Renderer>().material.color = Color.gray;
                    cubesR.Add(handJoint, cubeR);


                    var cubeL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cubeL.name = handJoint.ToString();
                    cubeL.transform.SetParent(handLGO.transform);
                    cubeL.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    cubeL.GetComponent<Renderer>().material.color = Color.gray;
                    cubesL.Add(handJoint, cubeL);
                }
            }
        }
        
    }
    
    // Update is called once per frame
    void Update()
    {

        
        
        alignmentCube.rotation = Quaternion.Inverse(IncomingFrameHandler.hologramTransform.rotation);
        alignmentCube.position = Quaternion.Inverse(alignmentCube.rotation) * 
                                 (initialAlignmentCubePosition) + IncomingFrameHandler.hologramTransform.position;

       
        var handJointService = CoreServices.GetInputSystemDataProvider<IMixedRealityHandJointService>();
        
        if (handJointService != null)
        {
            
            if (Time.time > (lastTimeTrackingChecked + 0.1)) // 10 checks per second
            {
                lastTimeTrackingChecked = Time.time;
                if (handJointService.IsHandTracked(Handedness.Right))
                {
                    if (rightHandTrackedHistory < 10)
                        rightHandTrackedHistory++;
                }
                else
                {
                    if (rightHandTrackedHistory > 0)
                        rightHandTrackedHistory--;
                }
            
                if(handJointService.IsHandTracked(Handedness.Left))
                {
                    if (LeftHandTrackedHistory < 10)
                        LeftHandTrackedHistory++;
                }
                else
                {
                    if (LeftHandTrackedHistory > 0)
                        LeftHandTrackedHistory--;
                }
            
            }
            
            
            if (peerConnection.Peer != null && !peerConnection.Peer.IsConnected)
            {
                if (Time.time > (lastTimeConnect + (1.0 / 1))) // FPS
                {

                    lastTimeConnect = Time.time;
                }

            }

            Array handJoints = Enum.GetValues(typeof(TrackedHandJoint));
            UnitySerializer srlzr = new UnitySerializer();
            srlzr.Serialize((byte)CommonParameters.codeHandData);
            srlzr.Serialize(Vector3.zero);
            srlzr.Serialize(Quaternion.identity);
            
            if(rightHandTrackedHistory > 5)
                srlzr.Serialize((byte)1);
            else
            {
                srlzr.Serialize((byte)0);
            }
            
            if(LeftHandTrackedHistory > 5)
                srlzr.Serialize((byte)1);
            else
            {
                srlzr.Serialize((byte)0);
            }

            for (int i = 0; i < 2; i++)
            {
                var handJointNumber = 0;
                foreach (TrackedHandJoint handJoint in handJoints)
                {
                    if (handJoint == TrackedHandJoint.None)
                    {
                        continue;
                    }
                    
                    Transform jointTransform;
                    if (i == 0)
                    {
                        jointTransform = handJointService.RequestJointTransform(handJoint, Handedness.Right);
                    }
                    else
                    {
                        jointTransform = handJointService.RequestJointTransform(handJoint, Handedness.Left);

                    }
                    srlzr.Serialize(Quaternion.Inverse(IncomingFrameHandler.hologramTransform.rotation) *
                                    (jointTransform.position-IncomingFrameHandler.hologramTransform.position));
                    srlzr.Serialize(Quaternion.Inverse(IncomingFrameHandler.hologramTransform.rotation) * jointTransform.rotation);
                    handJointNumber++;

                    if (handJoint == TrackedHandJoint.IndexTip)
                    {
                        if (i == 0)
                        {
                            pointer = jointTransform.position;
                        }
                    }
                    
                }
            }

            
            HandData.HandArray = srlzr.ByteArray;  // TODO: potential concurrency issue

        }

        // TODO: sending is a second job, should be done in own GameObject
        if (CommonParameters.useWebRtcDataChannel)
        {
            if (peerConnection.myDataChannelOut != null)
            {
                if (peerConnection.myDataChannelOut.State == DataChannel.ChannelState.Open)
                {
                    if (Time.time > (lastTimeSend + (1.0 / 20))) // FPS
                    {
                        peerConnection.SendMessage("hand");
                        lastTimeSend = Time.time;
                    }
                }
            }
        }
        else 
        {
            if (peerConnection.Peer != null && peerConnection.Peer.IsConnected)
            {
                if (Time.time > (lastTimeSend + (1.0 / 20))) // FPS
                {
                    WsClient.SendMessage("hand", CommonParameters.LocalId);
                    lastTimeSend = Time.time;
                }
            }
        }
        
    }
    
    public void toggleShowLongArms()
    {
        if (!showLongArms)
        {
            showLongArms = true;
            print("Long arms on");
            cameraForward = Camera.transform.forward;
            print("Reinit long hand position.");
            cameraForward = Camera.transform.forward;
        }
        else
        {
            showLongArms = false;
            print("Long arms off");
        }
    }
    
}
