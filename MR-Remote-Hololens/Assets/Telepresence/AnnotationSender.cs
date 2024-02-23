using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine;
using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

namespace Telepresence
{
    public class AnnotationSender : MonoBehaviour
    {
        public PeerConnection peerConnection;
        private float lastTimeSend;
        // Start is called before the first frame update
        void Start()
        {
        }
    
        // Update is called once per frame
        void Update()
        {
            if (peerConnection.myDataChannelOut != null)
            {
                if (peerConnection.myDataChannelOut.State == DataChannel.ChannelState.Open)
                {
                    if (Time.time > (lastTimeSend + (1.0 / 60))) // checks per second. checks if a new depth frame is in the queue.
                    {
                        peerConnection.SendMessage("annotation");
                        lastTimeSend = Time.time;
                    }
                }
            }
        }
    }
}

