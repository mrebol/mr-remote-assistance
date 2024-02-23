using System;
using System.Collections.Generic;
using System.Linq;
using Telepresence;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class IncomingDepthHandler : MonoBehaviour
    {
        private float lastTimeRead;
        public int bufferSize;
        public Dictionary<int, Tuple<byte[], byte>> depthData = new Dictionary<int, Tuple<byte[], byte>>();
        public int maxDepthPacketNumber;
        private byte[] depthDataBytes;
        private byte[] incomingPacket;
        private UnitySerializer serializer;
        public RelativeAlignment relativeAlignment;
        public Alignment alignment;
        public bool updateAlignment = true;
        private byte[] incomingDepthPacket;
        private void Start()
        {
            maxDepthPacketNumber = -1;
            incomingPacket = new byte[CommonParameters.depthPacketSize];
            
            

        }

        private void FixedUpdate()
        {
            if (CommonParameters.useWebRtcDataChannel)
                incomingDepthPacket = PeerConnection.incomingDepthPacket;
            else
            {
                incomingDepthPacket = WsClient.incomingDepthPacket;
            }
            if (incomingDepthPacket != null)
            {
                if (Time.time > (lastTimeRead + (1.0 / 30))) // FPS
                {
                    lastTimeRead = Time.time;

                    //Thread 1: incoming data into dictionary
                     
                    byte[] packetNumberBytes = new byte[4];   
                    //TODO: pointers instead of block copy
                    
                    Buffer.BlockCopy(incomingDepthPacket, 0, incomingPacket, 
                        0, incomingPacket.Length);
                    int offset = 1; // because first byte is identifier
                    Buffer.BlockCopy(incomingPacket, offset, packetNumberBytes, 0,  4);
                    offset += 4;

                    
                    int packetNumber = (int) BitConverter.ToUInt32(packetNumberBytes, 0);
                    if (!depthData.ContainsKey(packetNumber))  // check if new packet
                    {
                        if (packetNumber > maxDepthPacketNumber - bufferSize) // add to dict if not too old
                        {
                            byte[] serializedData = new byte[CommonParameters.serialzedSize];
                            Buffer.BlockCopy(incomingPacket, offset, serializedData, 0, CommonParameters.serialzedSize);
                            serializer = new UnitySerializer(serializedData);
                            var ArucoPosition = serializer.DeserializeVector3();
                            var ArucoRotation = serializer.DeserializeQuaternion();


                            for (int j = 0; j < CommonParameters.numberOfCameras; j++)
                            {
                                for (int i = 0; i < CommonParameters.numberOfBalls; i++)
                                {
                                    if(relativeAlignment != null)
                                    relativeAlignment.cameraPoints[j][i] = serializer.DeserializeVector3();
                                    else
                                    {
                                        alignment.cameraPoints[j][i] = serializer.DeserializeVector3();
                                    }
                                }
                            }
                            
                            int cameraNumber = serializer.DeserializeInt();
                            for (int i = 0; i < CommonParameters.numberOfCameras-1; i++)
                            {
                                var translation = serializer.DeserializeVector3();
                                var rotation = serializer.DeserializeQuaternion();
                                if (updateAlignment)
                                {
                                    if (relativeAlignment != null)
                                    {
                                        relativeAlignment.cameraTransformation.cameraTransformation[i + 1].translation =
                                            translation;
                                        relativeAlignment.cameraTransformation.cameraTransformation[i + 1].quaternion =
                                            rotation;
                                    }
                                }
                            }
                            offset += CommonParameters.serialzedSize;
                            
                            depthDataBytes = new byte[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth * 2];
                            Buffer.BlockCopy(incomingPacket, offset, depthDataBytes, 0,  depthDataBytes.Length);
                            if (packetNumber > maxDepthPacketNumber)
                                maxDepthPacketNumber = packetNumber;
                            depthData.Add(packetNumber, new Tuple<byte[],byte>(depthDataBytes,(byte) cameraNumber));
                        }
                        
                    }
                    
                    
                    //Thread 2: Delete if dict too large // TODO: do this on connection reset
                    if (depthData.Count > bufferSize * 2)
                    {
                        var itemsToRemove = depthData.Where(f => f.Key < maxDepthPacketNumber - bufferSize).ToArray();
                        foreach (var item in itemsToRemove)
                        {
                            depthData.Remove(item.Key);
                        }
                    }
    
                }
            }

        }
    }
}