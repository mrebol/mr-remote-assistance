using System;
using System.Collections.Generic;
using System.Linq;
using Telepresence;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class IncomingColorHandler : MonoBehaviour
    {
        public Dictionary<int, ColorData> colorData = new Dictionary<int, ColorData>();
        private float lastTimeRead;
        public int maxColorPacketNumber;
        public int bufferSize;

        private void FixedUpdate()
        {
            byte[] incomingDepthPacket;
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
                    
                    // Thread 1: read color data
                    readColorData();

                    //Thread 2: delete if dict too large
                    if (colorData.Count > bufferSize * 2)
                    {
                        var itemsToRemove = colorData.Where(f => f.Key < maxColorPacketNumber - bufferSize).ToArray();
                        foreach (var item in itemsToRemove)
                        {
                            Destroy(item.Value.textureY);  // free RAM
                            Destroy(item.Value.textureU);
                            Destroy(item.Value.textureV);
                            colorData.Remove(item.Key);
                        }
                    }

                }
            }
        }
        bool readBit(byte y)
        {
            if (y < 128)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        
        bool readColorBit(Color c)
        {
            if (c.r < 0.5)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void readColorData()
        {
            uint colorPacketNumber = 0;
            
            // Read data into Dictionary
            if (VideoRendererMR._textureY.width * VideoRendererMR._textureY.height <= 27)
            {
                return;
            }
            Color[] controlPixels = VideoRendererMR._textureY.GetPixels(0,0,28,1);
            
            var controlPattern = (readColorBit(controlPixels[24]), readColorBit(controlPixels[25]), readColorBit(controlPixels[26]), readColorBit(controlPixels[27]));
            if (controlPattern != (true, false, true, false))
            {
                print("Error: wrong control pattern.");
                return;
            }

            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[0])) << 0;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[1])) << 1;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[2])) << 2;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[3])) << 3;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[4])) << 4;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[5])) << 5;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[6])) << 6;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[7])) << 7;

            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[8])) << 8;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[9])) << 9;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[10])) << 10;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[11])) << 11;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[12])) << 12;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[13])) << 13;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[14])) << 14;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[15])) << 15;

            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[16])) << 16;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[17])) << 17;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[18])) << 18;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[19])) << 19;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[20])) << 20;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[21])) << 21;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[22])) << 22;
            colorPacketNumber += Convert.ToUInt32(readColorBit(controlPixels[23])) << 23;
            
            int colorPacketNumberI = Convert.ToInt32(colorPacketNumber);
            if (colorData.ContainsKey(colorPacketNumberI)) // check if old packet
            {
                return;
            }    
            
            if (colorPacketNumberI > maxColorPacketNumber - bufferSize) // add to dict if not too old
            {
                if (colorPacketNumberI > maxColorPacketNumber)
                    maxColorPacketNumber = colorPacketNumberI;

                if (true)  // performance overhead, but needed
                {
                    Texture2D textureY = new Texture2D(VideoRendererMR._textureY.width,
                        VideoRendererMR._textureY.height, VideoRendererMR._textureY.format, false);
                    Texture2D textureU = new Texture2D(VideoRendererMR._textureU.width,
                        VideoRendererMR._textureU.height, VideoRendererMR._textureU.format, false);
                    Texture2D textureV = new Texture2D(VideoRendererMR._textureV.width,
                        VideoRendererMR._textureV.height, VideoRendererMR._textureV.format, false);
                    Graphics.CopyTexture(VideoRendererMR._textureY, textureY);
                    Graphics.CopyTexture(VideoRendererMR._textureU, textureU);
                    Graphics.CopyTexture(VideoRendererMR._textureV, textureV);
                    colorData.Add(colorPacketNumberI, new ColorData(textureY, textureU, textureV));
                }
                else
                {
                    colorData.Add(colorPacketNumberI, new ColorData(VideoRendererMR._textureY, VideoRendererMR._textureU, VideoRendererMR._textureV));
                    
                }
                
            }
            
        }
    }

    public class ColorData
        {
            public Texture2D textureY;
            public Texture2D textureU;
            public Texture2D textureV;

            public ColorData(Texture2D textureY, Texture2D textureU, Texture2D textureV)
            {
                this.textureY = textureY;
                this.textureU = textureU;
                this.textureV = textureV;
            }

        }

}