using System;
using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;


public class WebRTCManager : MonoBehaviour
{
    public PeerConnection peerConnection;
    public NodeDssSignaler signaler;
    private bool doStartWebRTC = false;
    public bool doConnectionReset;

    public Text connectionText;
    public Text dataChannelText;
    private String connectionTextBase;
    private String datachannelTextBase;
    public String connectionStatus;  // for Unity Inspector
    public String dataChannelStatus; // for Unity Inspector
    private float lastTimeAliveSent;
    private float lastAliveReceived;
    private bool isConnecting = false;
    private float connectionStartTime;
    
    // Start is called before the first frame update
    void Start()
    {
        connectionTextBase = connectionText.text;
        if(dataChannelText != null)
            datachannelTextBase = dataChannelText.text;
        
        Assert.IsTrue(CommonParameters.DSSserverAddress != "");
        signaler.HttpServerAddress = CommonParameters.DSSserverAddress;

    }

    // Update is called once per frame
    void Update()
    {
        
        if (CommonParameters.useWebRtcDataChannel && peerConnection.myDataChannelOut == null)
        {
            print(peerConnection.PeerName + ": Data Channel Out is null");
        }
        if (CommonParameters.useWebRtcDataChannel && CommonParameters.useMultipleDataChannels && peerConnection.myDataChannelIn == null)
        {
            print(peerConnection.PeerName + ": Data Channel In is null");
        }
        if (isConnecting && peerConnection.myDataChannelOut != null)
        {
            if (CommonParameters.useWebRtcDataChannel)
            {
                if (peerConnection.myDataChannelOut.State == DataChannel.ChannelState.Connecting &&
                    Time.time - connectionStartTime > 2) // try every 2 sec
                {
                    print(peerConnection.PeerName + ": Still connecting... Send additional connection offer.");
                    StartConnection();
                }
                else if (peerConnection.myDataChannelOut.State == DataChannel.ChannelState.Open)
                {
                    isConnecting = false;
                }
            }
            else
            {
                if (peerConnection.Peer.IsConnected)
                {
                    isConnecting = false;
                }
                else if(Time.time - connectionStartTime > 5)
                {
                    print(peerConnection.PeerName + ": Still connecting... Send additional connection offer.");
                    StartConnection();
                }
                
            }
        }
            
        if (doStartWebRTC)
        {
            StartWebRTC();
            doStartWebRTC = false;
        }
        

        if (peerConnection.myDataChannelOut != null && peerConnection.myDataChannelOut.State == DataChannel.ChannelState.Closed)
        {
            print(signaler.LocalPeerId + ": Shutdown detected! Reset Connection...");
            resetConnection();
        }

        if(peerConnection.Peer == null)
            connectionText.text = connectionTextBase + " null" ;
        else
        {
            connectionText.text = connectionTextBase + " " + peerConnection.Peer.IsConnected;
            connectionStatus = peerConnection.Peer.IsConnected.ToString();
        }

        if (dataChannelText != null)
        {
            if (peerConnection.myDataChannelOut == null)
                dataChannelText.text = datachannelTextBase + " null";
            else
            {
                dataChannelText.text = datachannelTextBase + " " + peerConnection.myDataChannelOut.State;
            }
        }

        if (peerConnection.myDataChannelOut != null)
        {
            dataChannelStatus = peerConnection.myDataChannelOut.State.ToString();
            if (peerConnection.Peer.IsConnected && peerConnection.myDataChannelOut.State == DataChannel.ChannelState.Open && 
                Time.time - lastTimeAliveSent > CommonParameters.sendAliveIntervalSec)
            {
                //1. Send peer alive
                peerConnection.SendMessage("alive");
                lastTimeAliveSent = Time.time;
                
                //2. Check if peer alive recieved
                if (peerConnection.alivePacketCount > 0)
                {
                    peerConnection.alivePacketCount = 0;
                    lastAliveReceived = Time.time;
                    print("Alive received at: " + lastAliveReceived);
                }
                
                //3. Check last peer alive
                if (lastAliveReceived > 0 && Time.time - lastAliveReceived > CommonParameters.maxAliveTimeoutSec)
                {
                    print(signaler.LocalPeerId +": No alive received for " + CommonParameters.maxAliveTimeoutSec + " seconds. Resetting connection...");
                    resetConnection();
                }
            }
        }
    }
    
    public void StartWebRTC()
    {
        signaler.enabled = true;
        peerConnection.enabled = true;
    }
    
    [ContextMenu("Connect")]
    public void StartConnection()
    {
        if (peerConnection.myDataChannelOut == null)
        {
            if (CommonParameters.useWebRtcDataChannel)
            {
                peerConnection.AddDataChannels();
            }
        }
        peerConnection.StartConnection();
        isConnecting = true;
        connectionStartTime = Time.time;
    }

    [ContextMenu("Reset Connection")]
    public void resetConnection()
    {
        isConnecting = false;
        lastAliveReceived = 0;
        peerConnection.enabled = false; 
        signaler.enabled = false;
        doConnectionReset = true;
        if(peerConnection.DepthData.DepthPackets != null)
            peerConnection.DepthData.DepthPackets.Clear();
        
        
        peerConnection.myDataChannelOut = null;
        doStartWebRTC = true;
}
}
