using System;
using System.Collections.Generic;
using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine;

namespace Telepresence
{
    public class ScreenTransformSender : MonoBehaviour
    {
        private float lastTimeSend;
        public PeerConnection peerConnection;
        public Transform finalCorrection;

        public List<Transform> screens;
        public Alignment alignment;
        
        void Start()
        {
            ScreenTransformData.initialize();
        }

        private void Update()
        {
            if (peerConnection.Peer != null && peerConnection.Peer.IsConnected)
            {
                if (Time.time > (lastTimeSend + (1.0 * 2))) // FPS, every 2sec
                {
                    UnitySerializer srlzr = new UnitySerializer();
                    srlzr.Serialize((byte) CommonParameters.codeScreenTransformPacket);

                    foreach (var screen in screens)
                    {
                        Quaternion rotation = alignment.cameraTransformation.cameraTransformation[0].quaternion* Quaternion.Inverse(finalCorrection.rotation) * screen.rotation; 
                        Vector3 position = alignment.cameraTransformation.cameraTransformation[0].quaternion* 
                                            (screen.position - Quaternion.Inverse(finalCorrection.rotation) * finalCorrection.position +
                                             Quaternion.Inverse(alignment.cameraTransformation.cameraTransformation[0].quaternion) * alignment.cameraTransformation.cameraTransformation[0].translation);
                        srlzr.Serialize(position);
                        srlzr.Serialize(rotation);
                        srlzr.Serialize(screen.localScale);
                    }
                    ScreenTransformData.ScreenTransformArray = srlzr.ByteArray;  
                    
                    WsClient.SendMessage("screens", CommonParameters.RemoteId);
                    lastTimeSend = Time.time;
                }
            }
            
        }
        
    }  
}  