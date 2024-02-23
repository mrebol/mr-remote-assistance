using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEngine;
using UnityEngine.UI;
using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;




    public class FrameSender : MonoBehaviour
    {
        private float lastTimeRead;
        [UnityEngine.Range(0, 200)] public uint sendingFPS = 15;

        public Slider fpsSlider;
        public Text fpsText;
        public BallManager ballManager;

        public KinectReader kinectReader;

        public DepthData DepthData;

        public List<SceneVideoSource> sceneVideoSources;

        public static byte[] colorFrame { set; get; }

        public static byte[] depthFrame { set; get; }
        public static byte cameraNumber;
        public List<WebRTCManager> WebRtcManager;
        
        // Start is called before the first frame update
        void Start()
        {
            if (CommonParameters.useWebRtcDataChannel)
            {
            } else
            {
                DepthData = WsClient.DepthData;
            }

            foreach (var sceneVideoSource in sceneVideoSources)
            {
                sceneVideoSource.colorData.InitColorFrame((uint) CommonParameters.ColorWidth, (uint) CommonParameters.ColorHeight);;
            }
            
            depthFrame = new byte[CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight * 2];

            DepthData.InitDepthFrame((uint) CommonParameters.depthSendingWidth,
                (uint) CommonParameters.depthSendingHeight);

            fpsSlider.value = sendingFPS;
            fpsText.text = "Sending FPS: " + fpsSlider.value.ToString();



        }

        // Update is called once per frame
        void Update()
        {

            if (Time.time > (lastTimeRead + (1.0 / sendingFPS))) // FPS
            {
                lastTimeRead = Time.time;

                var numberOfClientsOnline = 0;
                for (int i =0;i<WebRtcManager.Count;i++)
                {
                    
                    if (WebRtcManager[i].doConnectionReset)
                    {
                        DepthData.DepthPackets.Clear();
                        WebRtcManager[i].doConnectionReset = false;

                    }

                    if (WebRtcManager[i].peerConnection.Peer != null && WebRtcManager[i].peerConnection.Peer.IsConnected)
                    {
                        numberOfClientsOnline++;

                        Debug.Assert(CommonParameters.currentFrameSendingNumber <
                                     Math.Pow(2, 24)); // max connection length is 2^24 frames

                        if (sceneVideoSources[i].colorData.ColorPackets.Count < 50) // TODO remove old color+depth frames from the queues
                        {
                            sceneVideoSources[i].colorData.addColorPacket(colorFrame, CommonParameters.currentFrameSendingNumber);
                        }
                        else
                        {
                            print("Clearing ALL color packets!!");
                            sceneVideoSources[i].colorData.ColorPackets.Clear();  // This should not be necessary
                        }
                    }
                }

                if (numberOfClientsOnline > 0 && DepthData.DepthPackets.Count < 50)
                {

                    List<List<Vector3>> cameraPointers = new List<List<Vector3>>();
                    for (int i = 0; i < kinectReader.kinectCount; i++)
                    {
                        List<Vector3> pointers = new List<Vector3>();
                        for (int j = 0; j < CommonParameters.numberOfBalls; j++)
                        {
                            switch (j)
                            {
                                case 0:
                                    pointers.Add(kinectReader.alignmentBalls.cameraBalls[i].RedPosition);
                                    break;
                                case 1:
                                    pointers.Add(kinectReader.alignmentBalls.cameraBalls[i].GreenPosition);
                                    break;
                                case 2:
                                    pointers.Add(kinectReader.alignmentBalls.cameraBalls[i].BluePosition);
                                    break;
                                case 3:
                                    pointers.Add(kinectReader.alignmentBalls.cameraBalls[i].YellowPosition);
                                    break;
                            }

                        }

                        cameraPointers.Add(pointers);
                    }

                    List<Tuple<Vector3, Quaternion>> cameraTransformation = new List<Tuple<Vector3, Quaternion>>();
                    for (int i = 0; i < ballManager.cameraTransformation.cameraTransformation.Count; i++)
                    {
                        cameraTransformation.Add(new Tuple<Vector3, Quaternion>(
                            ballManager.cameraTransformation.cameraTransformation[i].translation,
                            ballManager.cameraTransformation.cameraTransformation[i].quaternion));
                    }

                    DepthData.addDepthData(depthFrame, CommonParameters.currentFrameSendingNumber, new Vector3(), new Quaternion(),
                        cameraPointers, cameraNumber, cameraTransformation);
                

                    CommonParameters.currentFrameSendingNumber++;
                }
            }
        }

        Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
        {
            RenderTexture rt = new RenderTexture(targetX, targetY, 16);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt); // src, dst
            Texture2D result = new Texture2D(targetX, targetY, TextureFormat.R16, false);
            result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
            result.Apply();
            return result;
        }

        byte[] downScaleDepthImage(ushort[] inputArray, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            byte[] depthByteArray = new byte[2 * oldWidth * oldHeight];
            Buffer.BlockCopy(inputArray, 0, depthByteArray, 0, oldWidth * oldHeight * 2);

            return downScaleDepthImage(depthByteArray, oldWidth, oldHeight, newWidth, newHeight);
        }

        byte[] downScaleDepthImage(byte[] inputArray, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            byte[] depthByteArray = new byte[2 * oldWidth * oldHeight];
            byte[] depthScaled = new byte[2 * newWidth * newHeight];
            Buffer.BlockCopy(inputArray, 0, depthByteArray, 0, oldWidth * oldHeight * 2);
            Texture2D tex = new Texture2D(oldWidth, oldHeight, TextureFormat.R16, false);
            tex.LoadRawTextureData(depthByteArray);
            tex.Apply(); // slow: copy data from CPU to GPU
            Texture2D outTex = Resize(tex, newWidth, newHeight);
            outTex.Apply(); // slow: copy data from CPU to GPU

            Buffer.BlockCopy(outTex.GetRawTextureData(), 0, depthScaled, 0, (newHeight * newWidth * 2));


            return depthScaled;
        }

        public void sliderFPSChanged()
        {
            fpsText.text = "Sending FPS: " + fpsSlider.value.ToString();
            sendingFPS = (uint) fpsSlider.value;
        }

    }
