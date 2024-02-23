using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telepresence;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class IncomingAnnotationHandler : MonoBehaviour
    {
        private float lastTimeRead;
        public int bufferSize;
        public Dictionary<int, byte[]> annotationData = new Dictionary<int, byte[]>();
        public int maxDepthPacketNumber;
        private byte[] annotationDataBytes;
        private byte[] incomingPacket;
        private UnitySerializer serializer;
        public IncomingMeshRenderer mesh;
        public byte[] currentAnnotationData{ set; get; }
        private byte[] blackArray;
        public bool pointerOnHologram = false;

        
        private void Start()
        {
            currentAnnotationData  = new byte[CommonParameters.ColorHeight * CommonParameters.ColorWidth];
            incomingPacket = new byte[CommonParameters.incomingAnnotationPacketSize-1];
            blackArray = new byte[CommonParameters.ColorWidth * CommonParameters.ColorHeight];
        }

        private void FixedUpdate()
        {
            if (PeerConnection.incomingAnnotationPacket != null)
            {
                if (Time.time > (lastTimeRead + (1.0 / 10))) // FPS
                {
                    lastTimeRead = Time.time;
                    
                    handlePointer();
                    
                    //Thread 1: incoming data into dictionary
                     
                    byte[] packetNumberBytes = new byte[4];   
                    //TODO: pointers instead of block copy
                    Buffer.BlockCopy(PeerConnection.incomingAnnotationPacket, 1, incomingPacket, 0,  incomingPacket.Length-1);
                    int offset = 0;
                    Buffer.BlockCopy(incomingPacket, offset, packetNumberBytes, 0,  4);
                    offset += 4;
                    //var depthTexture = new Texture2D(depthWidth, depthHeight, TextureFormat.R16, false);
                    //depthTexture.LoadRawTextureData(depthDataBytes);
                    //ComputeBuffer depthBuffer = new ComputeBuffer(depthWidth * depthHeight/2, sizeof(uint));
                    //depthBuffer.SetData(depthDataBytes);
                    
                    int packetNumber = (int) BitConverter.ToUInt32(packetNumberBytes, 0);
                    //if (!annotationData.ContainsKey(packetNumber))  // check if new packet
                    {
                        //if (packetNumber > maxDepthPacketNumber - bufferSize) // add to dict if not too old
                        {
                            byte[] arucoData = new byte[7 * 4];
                            Buffer.BlockCopy(incomingPacket, offset, arucoData, 0, 7 * 4);
                            serializer = new UnitySerializer(arucoData);
                            serializer.DeserializeVector3(); // Aruco Position (not needed)
                            serializer.DeserializeQuaternion(); // Aruco Rotation (not needed)
                            //print("out " + mesh.masterArucoRotation);
                            
                            offset += 7 * 4;

                                annotationDataBytes = new byte[CommonParameters.ColorHeight* CommonParameters.ColorWidth];
                            Buffer.BlockCopy(incomingPacket, offset, annotationDataBytes, 0,  annotationDataBytes.Length);
                            //print("incoming depth : " + packetNumber);
                            //if (packetNumber > maxDepthPacketNumber)
                            //    maxDepthPacketNumber = packetNumber;
                            //annotationData.Add(packetNumber, annotationDataBytes);
                            currentAnnotationData = annotationDataBytes;
                        }
                        
                    }

                    //Thread 2: delete if dict too large
                    //if (annotationData.Count > bufferSize * 2)
                    //{
                    //    var itemsToRemove = annotationData.Where(f => f.Key < maxDepthPacketNumber - bufferSize).ToArray();
                    //    foreach (var item in itemsToRemove)
                    //    {
                    //        annotationData.Remove(item.Key);
                    //    }
                    //}
    
                }
            }
        }

        public void handlePointer()
        {
            if (PeerConnection.incomingPointerPacket != null)
            {
                serializer = new UnitySerializer(PeerConnection.incomingPointerPacket);
                var junk = serializer.DeserializeByte();
                var x = serializer.DeserializeInt();
                var y = serializer.DeserializeInt();

                if (x == -1 && y == -1)
                {

                    mesh.PointerArray = blackArray;
                }
                else
                {
                    drawPointer(Convert.ToUInt16(x), Convert.ToUInt16(y));

                }


            }

        }
        
        public void drawPointer(ushort x, ushort y)
        {
            Task.Run(() =>  // Tasks runs in main thread, but async/non-blocking
            {
                byte[] b = new byte[CommonParameters.ColorHeight * CommonParameters.ColorWidth];
                int radius = 4;
                float rSquared = radius * radius;

                for (int u = x - radius; u < x + radius + 1; u++)
                {
                    for (int v = y - radius; v < y + radius + 1; v++)
                    {
                        if ((x - u) * (x - u) + (y - v) * (y - v) < rSquared)
                        {
                            b[u + v * CommonParameters.ColorWidth] = 255;
                            //tex.SetPixel(u, v, color);
                        }
                    }
                }

                mesh.PointerArray = b;

            });
        }
    }
}