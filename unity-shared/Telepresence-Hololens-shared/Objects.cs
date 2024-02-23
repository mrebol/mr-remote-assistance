using System;
using System.Collections.Generic;
using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine;

namespace Telepresence
{
    public class Objects : MonoBehaviour
    {
        private float lastTimeSend;
        public PeerConnection peerConnection;
        public List<Transform> objects;
        public bool showObjects = true;
        void Start()
        {
            ObjectData.initObject();
        }

        private void Update()
        {
            if (peerConnection.Peer != null && peerConnection.Peer.IsConnected)
            {
                if (Time.time > (lastTimeSend + (1.0 / 10))) // FPS
                {
                    UnitySerializer srlzr = new UnitySerializer();
                    srlzr.Serialize((byte) CommonParameters.codeObjectDataPacket);
                    srlzr.Serialize(showObjects);
                    foreach (var obj in objects)
                    {
                        Quaternion rotation = Quaternion.Inverse(IncomingFrameHandler.hologramTransform.rotation) * obj.rotation;
                        Vector3 position = Quaternion.Inverse(IncomingFrameHandler.hologramTransform.rotation) *
                                           (obj.position-IncomingFrameHandler.hologramTransform.position);
                        srlzr.Serialize(position);
                        srlzr.Serialize(rotation);
                        srlzr.Serialize(obj.localScale);
                    }
                    ObjectData.ObjectArray = srlzr.ByteArray;  
                    
                    WsClient.SendMessage("object", CommonParameters.LocalId);
                    lastTimeSend = Time.time;
                }
            }
            
        }
        
        public void positionObjects(Transform mainCamera)
        {
            var cameraRotation = Quaternion.Euler(0, mainCamera.rotation.eulerAngles.y, 0);
            for (int i=0; i < objects.Count;i++)
            {
                objects[i].position = mainCamera.position + cameraRotation * new Vector3(0.7f + 0.1f*i, -0.4f, 1.2f); // x.. right, y..up
            }
        }
   
        [ContextMenu("Show/Hide Objects")]
        public void toggleShowObjects()
        {
            if (showObjects)
            {
                foreach (var obj in objects)
                {
                    obj.gameObject.SetActive(false);
                }
                showObjects = false;
            }
            else
            {
                foreach (var obj in objects)
                {
                    obj.gameObject.SetActive(true);
                }
                showObjects = true;
            }
            print("Show objects: " + showObjects);
        }
    }  
}  