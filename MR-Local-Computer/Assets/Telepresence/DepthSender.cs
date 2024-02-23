using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.MixedReality.WebRTC;
//using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine;
using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

namespace Telepresence
{
    public class DepthSender : MonoBehaviour
    {
        public WebRTCManager remote;
        public WebRTCManager local;
        private float lastTimeSend;
        public FrameSender FrameSender;
        // Start is called before the first frame update
        void Start()
        {
        }
    
        // Update is called once per frame
        void Update()
        {
            if (CommonParameters.useWebRtcDataChannel)
            {
                print("not supported");
            }
            else
            {// checks per second. checks if a new depth frame is in the queue.
                if (Time.time > (lastTimeSend + (1.0 / (FrameSender.sendingFPS * 2)))) 
                {
                    if (local.peerConnection.Peer != null && local.peerConnection.Peer.IsConnected &&
                        remote.peerConnection.Peer != null && remote.peerConnection.Peer.IsConnected)
                    {
                        WsClient.SendMessage("depth", CommonParameters.LocalRemoteMulticastId);
                        lastTimeSend = Time.time;
                    } else if (remote.peerConnection.Peer != null && remote.peerConnection.Peer.IsConnected)
                    {
                        WsClient.SendMessage("depth", CommonParameters.RemoteId);
                        lastTimeSend = Time.time;
                    } else if (local.peerConnection.Peer != null && local.peerConnection.Peer.IsConnected)
                    {
                        WsClient.SendMessage("depth", CommonParameters.LocalId);
                        lastTimeSend = Time.time;
                    }
                }
                
            }
        }
    }
}

