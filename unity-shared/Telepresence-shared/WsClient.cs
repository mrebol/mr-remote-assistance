using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEngine;

using WebSocketSharp;

public class WsClient : MonoBehaviour
{
    public byte my_id;
    private static WebSocket ws;
    
    // Outgoing:
    public static DepthData DepthData = new DepthData();
    public static AnnotationData AnnotationData = new AnnotationData();
    public static Queue<byte[]> selectCameraPacket = new Queue<byte[]>();
    
    // Incoming:
    public static byte[] incomingDepthPacket;
    public static byte selectCamera;
    public static byte[] incomingHands;
    public static byte[] incomingObjects;
    public static byte[] incomingScreenTransforms;

    private void Start()
    {
        ws = new WebSocket(CommonParameters.webSocketAddress);
        ws.Connect();
        ws.OnMessage += (sender, e) =>
        {
            ChatMessageReceived(e.RawData);
        };
        
        ws.Send(new byte[]{CommonParameters.ServerId, my_id});

    }
    private void Update()
    {
        if(ws == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            try
            {
                if (ws.IsAlive)
                {
                    ws.Send(new byte[] {CommonParameters.ServerId, my_id});
                }
                else
                {
                    ws.Connect();
                    ws.Send(new byte[] {CommonParameters.ServerId, my_id});
                }
            }
            catch (Exception ex)
            {
                print("ws connection error.");
                
            }
        }
    }
    
    
    [ContextMenu("Reconnect Websocket")]
    public void reconnectWebSocket()
    {
        ws.Close();
        ws.Connect();
        ws.Send(new byte[]{CommonParameters.ServerId, my_id});

        
    }
    
     public static void SendMessage(string message, byte receiver)
    {
        byte[] sendBytes = null;
        if(message == "shutdown")
        {
            sendBytes = new byte[] {CommonParameters.codeShutdownPacket};
        } else if (message == "depth")
        {
            if (DepthData.DepthPackets.Count > 0)
            {
                sendBytes = DepthData.DepthPackets.Dequeue();
            }
            else
            {
                return;
            }
        } else if (message == "annotation")
        {
            if (AnnotationData.AnnotationPackets.Count > 0)
            {
                sendBytes = AnnotationData.AnnotationPackets.Dequeue();
            }
            else
            {
                return;
            }
        }else if (message == "selectCamera")
        {
            if (selectCameraPacket.Count > 0)
            {
                sendBytes = selectCameraPacket.Dequeue();
            }
            else
            {
                return;
            }
        }
        else if(message == "hand")
        {
            sendBytes = HandData.HandArray;
        } 
        else if (message == "object")
        {
            sendBytes = ObjectData.ObjectArray;
        }
        else if(message == "screens")
        {
            sendBytes = ScreenTransformData.ScreenTransformArray;
        } else if(message == "alive")
        {
            sendBytes = new byte[] {CommonParameters.alivePacket, 0x00, 0x00, 0x00};
        } 
        else
        {
            return;
        }
        
        var compressed = PeerConnection.Compress(sendBytes);
        try
        {
            var sendPacket = new byte[compressed.Length + 1];
            sendPacket[0] = receiver;
            Buffer.BlockCopy(compressed, 0, sendPacket, 1, compressed.Length);
            ws.Send(sendPacket);
        }
        catch
        {
        }
    }
     
     private void ChatMessageReceived(byte[] message)
        {
            var uncompressed = PeerConnection.Decompress(message);
            byte messageType = uncompressed[0];
            if (messageType == CommonParameters.codeDepthPacket)
            {
                incomingDepthPacket = uncompressed;
            }else if (messageType == CommonParameters.codeSelectCamera)
            {
                selectCamera = uncompressed[1];
            }  else if (messageType == (byte) CommonParameters.codeHandData)
            {
                UnitySerializer dsrlzr = new UnitySerializer(uncompressed);
                byte temp = dsrlzr.DeserializeByte();
                byte[] b = new byte[uncompressed.Length - 1];
                Buffer.BlockCopy(dsrlzr.ByteArray, 1, b, 0,uncompressed.Length - 1);
                incomingHands = b;
            }else if (messageType == (byte) CommonParameters.codeObjectDataPacket)
            {
                UnitySerializer dsrlzr = new UnitySerializer(uncompressed);
                byte temp = dsrlzr.DeserializeByte();
                byte[] b = new byte[uncompressed.Length - 1];
                Buffer.BlockCopy(dsrlzr.ByteArray, 1, b, 0,uncompressed.Length - 1);
                incomingObjects = b;
            }else if (messageType == (byte) CommonParameters.codeScreenTransformPacket)
            {
                UnitySerializer dsrlzr = new UnitySerializer(uncompressed);
                byte temp = dsrlzr.DeserializeByte();
                byte[] b = new byte[uncompressed.Length - 1];
                Buffer.BlockCopy(dsrlzr.ByteArray, 1, b, 0,uncompressed.Length - 1);
                incomingScreenTransforms = b;
            }
            
        }

     private void OnDestroy()
     {
         print("Closing web socket");
         ws.Close();
     }
}

