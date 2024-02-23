using System;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
//using Microsoft.MixedReality.Toolkit.Experimental.RiggedHandVisualizer;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.WebRTC.Unity;
using NumSharp.Utilities;
using Telepresence;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Profiling;

//namespace Microsoft.MixedReality.WebRTC.Unity
//{
    public class GestureReceiver : MonoBehaviour
    {

        public RiggedHandVisualizerMR handVisualizerRight;
        public RiggedHandVisualizerMR handVisualizerLeft;
        
        public GameObject markerGo;
        public Alignment Alignment;
        public Transform testCube;
        public Transform correctionTransform;
        public Transform finalCorrection;
        public Dictionary<TrackedHandJoint, GameObject> cubesR = new Dictionary<TrackedHandJoint, GameObject>();
        public Dictionary<TrackedHandJoint, GameObject> cubesL = new Dictionary<TrackedHandJoint, GameObject>();
        public bool isHandModelAbstract = false;
        private GameObject handRGO;
        private GameObject handLGO;
        private byte[] hand;
        
        
        void Start()
        {
            // Turn off all hand rays
            PointerUtils.SetHandRayPointerBehavior(PointerBehavior.AlwaysOff);

        }

        void Update()
        {
            if (CommonParameters.useWebRtcDataChannel)
            {
                hand = PeerConnection.incomingGesture;
            }
            else
            {
                hand = WsClient.incomingHands;
            }


            if (hand != null)
            {


                
                Array handJoints = Enum.GetValues(typeof(TrackedHandJoint));

                UnitySerializer dsrlzr = new UnitySerializer(hand);
                //print("incomming size " + dsrlzr.ByteArray.Length);
                var remoteArucoPositionTemp = dsrlzr.DeserializeVector3();
                var remoteArucoRotationTemp = dsrlzr.DeserializeQuaternion();
                var showRightHand = dsrlzr.DeserializeByte();
                var showLeftHand = dsrlzr.DeserializeByte();

                if (showRightHand == 1)
                {
                    handVisualizerRight.gameObject.SetActive(true);
                    handVisualizerRight.HandRenderer.enabled = true;

                }
                else
                {
                    handVisualizerRight.gameObject.SetActive(false);
                    handVisualizerRight.HandRenderer.enabled = false;
                    
                }

                if (showLeftHand == 1)
                {
                    handVisualizerLeft.gameObject.SetActive(true);
                    handVisualizerLeft.HandRenderer.enabled = true;
                }
                else
                {
                    handVisualizerLeft.gameObject.SetActive(false);
                    handVisualizerLeft.HandRenderer.enabled = false;
                }
                
                for (int i=0;i<2;i++){
                    var handJointNumber = -1;
                    foreach (TrackedHandJoint handJoint in handJoints)
                    {

                        if (handJoint == TrackedHandJoint.None)
                        {
                            continue;
                        }
                        
                        var camera0Position = dsrlzr.DeserializeVector3();
                        Quaternion camera0Rotation = dsrlzr.DeserializeQuaternion();
                        handJointNumber++;
                        if (isHandModelAbstract)
                        {

                            if (i == 0)
                            {
                                cubesR[handJoint].transform.position = camera0ToLocalHololensTranslation(camera0Position) + finalCorrection.rotation * finalCorrection.position;
                                cubesR[handJoint].transform.rotation = camera0ToLocalHololensRotation(camera0Rotation) * finalCorrection.rotation;
                            }
                            else
                            {
                                cubesL[handJoint].transform.position = camera0ToLocalHololensTranslation(camera0Position) + finalCorrection.rotation * finalCorrection.position;
                                cubesL[handJoint].transform.rotation = camera0ToLocalHololensRotation(camera0Rotation) * finalCorrection.rotation;

                            }
                            
                        }
                        else // Use Hand Model
                        {
                            Quaternion correctionRotation = Quaternion.Euler(0,-90,90); // order: Z, X, Y
                            Quaternion correctionRotation2 = Quaternion.Euler(0,-90,70); // order: Z, X, Y
                            Quaternion correctionRotationRight = Quaternion.Euler(0,-90,-90 ); // order: Z, X, Y
                            Quaternion correctionRotationLeft = Quaternion.Euler(0,-90,90); // order: Z, X, Y


                            if (handVisualizerRight.joints.ContainsKey(handJoint))
                            {

                                Transform joint = null;
                                if (i == 0)
                                {
                                    joint = handVisualizerRight.joints[handJoint];
                                }
                                else
                                {
                                    joint = handVisualizerLeft.joints[handJoint];
                                    //continue;
                                }

                                if (joint == null)
                                {
                                    //print(handJoint.ToString() + " null");
                                    continue;
                                }


                                if (handJoint == TrackedHandJoint.Wrist) // incoming data is wrist
                                {

                                    if (i == 0)
                                    {

                                        handVisualizerRight.joints[TrackedHandJoint.Palm].rotation = camera0ToLocalHololensRotation(
                                            camera0Rotation * handVisualizerRight.userBoneRotation) * finalCorrection.rotation;
                                        
                                          handVisualizerRight.joints[TrackedHandJoint.Palm].position = camera0ToLocalHololensTranslation(camera0Position)
                                              + finalCorrection.rotation * finalCorrection.position;
                                    }
                                    else
                                    {

                                        handVisualizerLeft.joints[TrackedHandJoint.Palm].rotation = camera0ToLocalHololensRotation(
                                            camera0Rotation * handVisualizerLeft.userBoneRotation) * finalCorrection.rotation;
                                        
                                        handVisualizerLeft.joints[TrackedHandJoint.Palm].position = camera0ToLocalHololensTranslation(camera0Position)
                                            + finalCorrection.rotation * finalCorrection.position;
                                    }

                                    continue;
                                }

                                if (handJoint == TrackedHandJoint.Palm)
                                {
                                    continue;
                                }
                                
                                if(i==0)
                                    joint.rotation = camera0ToLocalHololensRotation(camera0Rotation * handVisualizerRight.Reorientation())
                                                     * finalCorrection.rotation;
                                else
                                    joint.rotation = camera0ToLocalHololensRotation(camera0Rotation * handVisualizerLeft.Reorientation())
                                                     * finalCorrection.rotation;
                            }


                        }
                    }
                }
            }
            else
            {
                handVisualizerRight.gameObject.SetActive(false);
                handVisualizerLeft.gameObject.SetActive(false);
                handVisualizerRight.HandRenderer.enabled = false;
                handVisualizerLeft.HandRenderer.enabled = false;
            }

        }

        Quaternion camera0ToLocalHololensRotation(Quaternion input)
        {
            return Quaternion.Inverse(Alignment.cameraTransformation.cameraTransformation[0].quaternion) * input;
        }
        
        Vector3 camera0ToLocalHololensTranslation(Vector3 input)
        {

            return Quaternion.Inverse(Alignment.cameraTransformation.cameraTransformation[0].quaternion) *  (input - Alignment.cameraTransformation.cameraTransformation[0].translation);
        }
    }
//}