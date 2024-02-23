using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Telepresence;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class DepthData
    {
        public static uint Width { get; private set; }
        public static uint Height { get; private set; }
        public static uint sendWidth { get; private set; }
        public static uint sendHeight { get; private set; }

        public static Vector3 trackerPosition;
        public static Quaternion trackerRotation;
        
        public Queue<byte[]> DepthPackets {get; private set; }

        public static UnitySerializer srlzr;
        
        public void InitDepthFrame(uint swidth, uint sheight)
        {
            sendWidth = swidth;
            sendHeight = sheight;
            DepthPackets = new Queue<byte[]>();
        }
        
        public void addDepthData(byte[] depthData, uint packageNumber, Vector3 arucoPosition, Quaternion arucoRotation, 
            List<List<Vector3>> cameraPointers, byte cameraNumber, List<Tuple<Vector3, Quaternion>> cameraTransformations)
        {
            byte[] depthPacket = new byte[CommonParameters.depthPacketSize];
            int offset = 0;
            depthPacket[0] = CommonParameters.codeDepthPacket;
            offset += 1;
            Buffer.BlockCopy(BitConverter.GetBytes(packageNumber), 0, depthPacket, offset,  4);
            offset += 4;
            srlzr= new UnitySerializer();
            srlzr.Serialize(arucoPosition);
            srlzr.Serialize(arucoRotation * Quaternion.Euler(90,0,0) ); // 90deg first, rotation second
            //Pointers 
            foreach (var cameraPointer in cameraPointers)
            {
                foreach (var pointer in cameraPointer)
                {
                    srlzr.Serialize(pointer);
                }
                
            }
            //Camera Number
            srlzr.Serialize((int) cameraNumber);
            //Camera Transformation
            var counter = 0;
            foreach (var cameraTransformation in cameraTransformations)
            {
                srlzr.Serialize(cameraTransformation.Item1);
                srlzr.Serialize(cameraTransformation.Item2);
                counter++;
            }
            Assert.IsTrue((3+4)*4 + CommonParameters.ballDataSize + CommonParameters.cameraTranformationSize == srlzr.ByteArray.Length);
            Buffer.BlockCopy(srlzr.ByteArray, 0, depthPacket, offset,  srlzr.ByteArray.Length);
            offset += srlzr.ByteArray.Length;

            Buffer.BlockCopy(depthData, 0, depthPacket, offset,  (int) (sendWidth * sendHeight *2));
            DepthPackets.Enqueue(depthPacket);
        }

    }
}