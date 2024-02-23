using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class AnnotationData
    {
        public static uint sendWidth { get; private set; }
        public static uint sendHeight { get; private set; }
        
        public Queue<byte[]> AnnotationPackets {get; private set; }

        public static UnitySerializer srlzr;


        public void InitAnnotationFrame(uint swidth, uint sheight)
        {

            sendWidth = swidth;
            sendHeight = sheight;
            AnnotationPackets = new Queue<byte[]>();
        }

        public static void increasePackageNumber()
        {
        }
        

        public void addAnnotationData(byte[] annotationData, uint packageNumber, Transform arucoMarker)
        {
            byte[] annotationPacket = new byte[1+ 4 + 4*7 +  sendWidth * sendHeight];
            int offset = 0;
            annotationPacket[0] = 0x67;
            offset += 1;
            Buffer.BlockCopy(BitConverter.GetBytes(packageNumber), 0, annotationPacket, offset,  4);
            offset += 4;
            srlzr= new UnitySerializer();
            srlzr.Serialize(arucoMarker.position);
            srlzr.Serialize(arucoMarker.rotation * Quaternion.Euler(90,0,0) ); // 90deg first, rotation second
            Buffer.BlockCopy(srlzr.ByteArray, 0, annotationPacket, offset,  srlzr.ByteArray.Length);
            offset += srlzr.ByteArray.Length;
            Buffer.BlockCopy(annotationData, 0, annotationPacket, offset,  (int) (sendWidth * sendHeight));
            offset += (int)(sendWidth * sendHeight);
            
            
            AnnotationPackets.Enqueue(annotationPacket);
            
        }

    }
}