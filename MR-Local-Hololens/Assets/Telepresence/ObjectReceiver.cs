using System;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
//using Microsoft.MixedReality.Toolkit.Experimental.RiggedHandVisualizer;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEngine;
using UnityEngine.Profiling;

//namespace Microsoft.MixedReality.WebRTC.Unity
//{
    public class ObjectReceiver : MonoBehaviour
    {
        
        public Alignment Alignment;
        public Transform finalCorrection;
        public List<Transform> objects;
        private byte[] objectsArray;
        private bool showObjects;
        

        void Update()
        {
            objectsArray = WsClient.incomingObjects;
            

            if (objectsArray != null)
            {

                UnitySerializer dsrlzr = new UnitySerializer(objectsArray);
                showObjects = dsrlzr.DeserializeBool();
                if (showObjects)
                {
                    foreach (Transform obj in objects)
                    {
                        obj.gameObject.SetActive(true);
                        var remotePosition = dsrlzr.DeserializeVector3();
                        Quaternion remoteRotation = dsrlzr.DeserializeQuaternion();
                        var remoteScale = dsrlzr.DeserializeVector3();

                        obj.position = positionCamera0ToLocalCS(remotePosition) +
                                       finalCorrection.rotation * finalCorrection.position;
                        obj.rotation = rotateCamera0ToLocalCS(remoteRotation) * finalCorrection.rotation;
                        obj.localScale = remoteScale;
                    }
                }
                else
                {
                    foreach (Transform obj in objects)
                    {
                        obj.gameObject.SetActive(false);
                    }
                }
            } 

        }

        Quaternion rotateCamera0ToLocalCS(Quaternion input)
        {
            return Quaternion.Inverse(Alignment.cameraTransformation.cameraTransformation[0].quaternion) * input;
        }
        
        Vector3 positionCamera0ToLocalCS(Vector3 input)
        {

            return Quaternion.Inverse(Alignment.cameraTransformation.cameraTransformation[0].quaternion) *  (input - Alignment.cameraTransformation.cameraTransformation[0].translation);
        }
    }
//}