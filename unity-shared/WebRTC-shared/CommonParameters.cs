using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Telepresence
{
    public enum ColorResolution
    {
        Off,
        R720p,
        R1080p,
        R1440p,
        R1536p,
        R2160p,
        R3072p,
    }
    
    public enum DepthMode
    {
        Off,
        NFOV_2x2Binned,
        NFOV_Unbinned,
        WFOV_2x2Binned,
        WFOV_Unbinned,
        PassiveIR,
    }

    public static class CommonParameters
    {
        public const bool productionMode = true;
        public static byte numberOfCameras = 2; 
        public static bool useWebRtcDataChannel = false;
        public static bool doSendSelectCamera = true;
        public static string directory;
        public static string network = "ng"; //"ng";"inet";
        public static string webSocketAddress = "";////"ws://161.253.126.173:8080";
        
        public const bool COLOR_SDK_REGISTERED = false; 
        public const int COLOR_REGISTERED_WIDTH = 320;
        public const int COLOR_REGISTERED_HEIGHT = 288;
        public static int ColorWidth;
        public static int ColorHeight;
        public static ColorResolution ColorResolution = ColorResolution.R1080p;

        public static DepthMode depthMode = DepthMode.NFOV_2x2Binned;
        public const int useDepthCoordinateSystem = 1;
        public const int flipScene = 1;
        public const int numberOfBalls = 4;
        public static int ballDataSize;
        public static int cameraTranformationSize;
        public static int depthPacketSize;
        public static int incomingAnnotationPacketSize;
        public static int incomingPointerPacketSize = 1 + 2*4;
        public static int serialzedSize;

        public static int nrOfObjects = 7;
        public static int objectDataSize = 1/*codeID*/ + 1/*showObjects*/ + (4 * 4 + 3 * 4 + 3 * 4) * nrOfObjects;
        public static int nrOfScreens = 2;
        public static int screenTransformDataSize = 1/*codeID*/ + (4 * 4 + 3 * 4 + 3 * 4) * nrOfScreens;
        public static int maxAliveTimeoutSec = Int32.MaxValue; // 10;
        public static float sendAliveIntervalSec = float.MaxValue; //0.1f;
        public static bool dataChannelIsReliable = true;
        public static Vector3 handModelRightOffset = new Vector3(0, 0, 0);
        public static Vector3 handModelLeftOffset = new Vector3(0, 0, 0);//-0.25f);
        public static bool useMultipleDataChannels = false;

        
        public const bool DEPTH_DOWNSCALE = false;
        public static int depthSendingWidth;
        public static int depthSendingHeight;

        public static byte codeDepthPacket = 0x00;
        public static byte codeTrackerPacket = 0x01;
        public static byte codeShutdownPacket = 0x02;
        public static byte codeSelectCamera = 0x03;
        public static byte codeHandData = 0x04;
        public static byte alivePacket = 0x05;
        public static byte codeIdPacket = 0x06;
        public static byte codeObjectDataPacket = 0x07;
        public static byte codeScreenTransformPacket = 0x08;

        public static byte ServerId = 0x00;
        public static byte LocalComputerId = 0x01;
        public static byte RemoteComputerId = 0x02;
        public static byte LocalId = 0x03;
        public static byte RemoteId = 0x04;
        public static byte LocalRemoteMulticastId = 0x05;

        public static uint currentFrameSendingNumber = 0;

        
        public static int handDataSize = 1 + 2/*tracked?*/ + 26 * 2 * (3 * 4 + 4 * 4);

        public static float[] colorExtrinsicsR;
        public static Vector3 colorExtrinsicsT;
        public static Vector2 colorIntrinsicsCxy;
        public static Vector2 colorIntrinsicsFxy;
        public static float[] colorDistortionRadial;
        public static Vector2 colorDistortionTangential;
    
        public static float[] depthExtrinsicsR;
        public static Vector3 depthExtrinsicsT;
        public static Vector2 depthIntrinsicsCxy;
        public static Vector2 depthIntrinsicsFxy;
        public static float[] depthDistortionRadial;
        public static Vector2 depthDistortionTangential;
        
        public static int lumaWidth;
        public static int lumaHeight;
        public static int chromaWidth;
        public static int chromaHeight;


        public static string DSSserverAddress;
        
        public static Dictionary<string, Connection> connections = new Dictionary<string, Connection>();
        public class Connection
        {
            public ushort id;
            public string label;

        }
        
        static CommonParameters()
        {
            if (productionMode == true)
            {
                directory = @"C:\data\repos\mr-remote-assistance\"; 
                numberOfCameras = 2;
            }
            else // development mode
            {
                network = "inet";
                directory = @"C:\data\repos\mr-remote-assistance\";
            }
            Connection c;
            if (!useMultipleDataChannels)
            {
                c = new Connection();
                c.id = 13;
                c.label = "LocalComputerRemote";
                connections.Add(c.label, c);
                //c = new Connection();
                //c.id = 19;
                //c.label = "Remote2LocalComputer";
                //connections.Add(c.label, c);
                c = new Connection();
                c.id = 14;
                c.label = "LocalComputerLocal";
                connections.Add(c.label, c);
                c = new Connection();
                c.id = 15;
                c.label = "LocalComputerRemoteComputer";
                connections.Add(c.label, c);
                c = new Connection();
                c.id = 16;
                c.label = "RemoteComputerRemote";
                connections.Add(c.label, c);
                c = new Connection();
                c.id = 17;
                c.label = "RemoteComputerLocal";
                connections.Add(c.label, c);
                c = new Connection();
                c.id = 18;
                c.label = "LocalRemote";
                connections.Add(c.label, c);

            }
            else
            {
                c = new Connection();
                c.id = 13;
                c.label = "LocalComputerRemoteIn";
                connections.Add("LocalComputer2RemoteIn", c);
                connections.Add("Remote2LocalComputerOut", c);

                c = new Connection();
                c.id = 14;
                c.label = "LocalComputerRemoteOut";
                //c.id = 13;
                //c.label = "LocalComputerRemoteIn";
                connections.Add("LocalComputer2RemoteOut", c);
                connections.Add("Remote2LocalComputerIn", c);

                c = new Connection();
                c.id = 15;
                c.label = "LocalComputerLocalIn";
                connections.Add("LocalComputer2LocalIn", c);
                connections.Add("Local2LocalComputerOut", c);

                c = new Connection();
                c.id = 16;
                c.label = "LocalComputerLocalOut";
                connections.Add("LocalComputer2LocalOut", c);
                connections.Add("Local2LocalComputerIn", c);

                c = new Connection();
                c.id = 17;
                c.label = "LocalComputerRemoteComputerIn";
                connections.Add("LocalComputer2RemoteComputerIn", c);
                connections.Add("RemoteComputer2LocalComputerOut", c);

                c = new Connection();
                c.id = 18;
                c.label = "LocalComputerRemoteComputerOut";
                connections.Add("LocalComputer2RemoteComputerOut", c);
                connections.Add("RemoteComputer2LocalComputerIn", c);
                c = new Connection();
                c.id = 19;
                c.label = "RemoteComputerRemoteIn";
                connections.Add("RemoteComputer2RemoteIn", c);
                connections.Add("Remote2RemoteComputerOut", c);
                c = new Connection();
                c.id = 20;
                c.label = "RemoteComputerRemoteOut";
                connections.Add("RemoteComputer2RemoteOut", c);
                connections.Add("Remote2RemoteComputerIn", c);

                c = new Connection();
                c.id = 21;
                c.label = "RemoteComputerLocalIn";
                connections.Add("RemoteComputer2LocalIn", c);
                connections.Add("Local2RemoteComputerOut", c);
                c = new Connection();
                c.id = 22;
                c.label = "RemoteComputerLocalOut";
                connections.Add("RemoteComputer2LocalOut", c);
                connections.Add("Local2RemoteComputerIn", c);

                c = new Connection();
                c.id = 23;
                c.label = "LocalRemoteIn";
                connections.Add("Local2RemoteIn", c);
                connections.Add("Remote2LocalOut", c);

                c = new Connection();
                c.id = 24;
                c.label = "LocalRemoteOut";
                connections.Add("Local2RemoteOut", c);
                connections.Add("Remote2LocalIn", c);
            }

            if (network == "ng")
            {
                DSSserverAddress = "http://192.168.1.250:3000/";
                if (webSocketAddress == "")
                {
                    webSocketAddress ="ws://192.168.1.250:8080";
                }
            } else if (network == "inet")
            {
                if(webSocketAddress == "")
                    webSocketAddress = "ws://localhost:8080";
            }
            else if (false)
            {
                /*TODO: add a branch for local testing. this, for now,
                 is the address used in the 'ng' block but it should have its own so that
                 we dont have to worry about changing things that often when we go to different places.
                 */
            }
 
            depthExtrinsicsR = new float[] {1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f};
            depthExtrinsicsT = new Vector3(0.0f, 0.0f, 0.0f);

            if (COLOR_SDK_REGISTERED)
            {
                ColorHeight = COLOR_REGISTERED_HEIGHT;
                ColorWidth = COLOR_REGISTERED_WIDTH;
            }
            else
            {
                switch (ColorResolution)
                {
                    case ColorResolution.R2160p:
                        ColorHeight = 2160;
                        ColorWidth = 3840;
                        break;
                    case ColorResolution.R1080p:
                        ColorHeight = 1080;
                        ColorWidth = 1920;
                        
                        colorExtrinsicsR = new []{
                            0.999975f, 0.00604121f, -0.003686489f, -0.005590246f, 0.9936896f, 0.1120258f, 0.004339997f, -0.1120024f, 0.9936985f
                        };
                        colorExtrinsicsT = new Vector3(-31.99785f, -1.716765f, 3.964732f);
                        colorIntrinsicsCxy = new Vector2(961.2f, 548.3f);
                        colorIntrinsicsFxy = new Vector2(901.8f, 901.4f);
                        colorDistortionRadial = new[]
                        {
                            0.515914f, -2.867712f, 1.767789f, 0.396866f, -2.686653f, 1.686018f
            
                        };
                        colorDistortionTangential = new Vector2(0.001789134f, -0.0002903489f);  // already swapped to p1,p2 
                        break;
                    case ColorResolution.R720p:
                        ColorHeight = 720;
                        ColorWidth = 1280;
                        
                        colorExtrinsicsR = new []{
                            0.999975f, 0.00604121f, -0.003686489f, -0.005590246f, 0.9936896f, 0.1120258f, 0.004339997f, -0.1120024f, 0.9936985f
                        };
                        colorExtrinsicsT = new Vector3(-31.99785f, -1.716765f, 3.964732f);
                        colorIntrinsicsCxy = new Vector2(640.6013f, 365.3592f);
                        colorIntrinsicsFxy = new Vector2(601.2313f, 600.9281f);
                        colorDistortionRadial = new[]
                        {
                            0.515914f, -2.867712f, 1.767789f, 0.396866f, -2.686653f, 1.686018f
            
                        };
                        colorDistortionTangential = new Vector2(0.001789134f, -0.0002903489f);  // already swapped to p1,p2 
                        break;
                }

                switch (depthMode)
                {
                   case DepthMode.NFOV_Unbinned:
                       depthSendingHeight = 576;
                       depthSendingWidth = 640;
                       depthIntrinsicsCxy = new Vector2(330.3263f, 350.572f);
                       depthIntrinsicsFxy = new Vector2( 504.6487f, 504.6038f);
                       depthDistortionRadial = new[] {4.166512f, 2.984315f, 0.1661907f, 4.495253f, 4.36248f, 0.8454164f};
                       depthDistortionTangential = new Vector2(0.0f, 0.0f);
                       break;
                   case DepthMode.NFOV_2x2Binned:
                       depthSendingHeight = 288;
                       depthSendingWidth = 320;
                       depthIntrinsicsCxy = new Vector2(165.7f, 169.5f);
                       depthIntrinsicsFxy = new Vector2(252.2f, 252.2f);
                       depthDistortionRadial = new[] {5.25003f, 3.448708f, 0.178202f, 5.577984f, 5.19477f, 0.9382225f};
                       depthDistortionTangential = new Vector2(0.0f, 0.0f);
                       break;
                }

            }
            ballDataSize =  numberOfCameras*numberOfBalls*3*4 + 4;
            cameraTranformationSize = (4 + 3) * 4* (numberOfCameras-1);
            depthPacketSize = 1 + 4 + 4*7 + ballDataSize + depthSendingHeight * depthSendingWidth * 2 + cameraTranformationSize;
            incomingAnnotationPacketSize= 1+ 4 + 4*7 + ColorHeight * ColorWidth;
            serialzedSize = 7 * 4 + ballDataSize + cameraTranformationSize;
                
            lumaWidth = CommonParameters.ColorWidth;
            lumaHeight = CommonParameters.ColorHeight;
            chromaWidth = lumaWidth / 2;
            chromaHeight = lumaHeight / 2;
        }

    }
}