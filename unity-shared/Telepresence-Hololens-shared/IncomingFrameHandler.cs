using System;
using System.Collections;
using System.Collections.Generic;
using Accord;
using Microsoft.MixedReality.Toolkit.Diagnostics;
using Microsoft.MixedReality.Toolkit.Experimental.Diagnostics;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class IncomingFrameHandler : MonoBehaviour
{
    [Range(1, 30)]
    public int senderFPS = 30;
    [Range(10,500)]
    public int maxWaitTimeMs = 20;
    [Range(10,500)]
    public int maxLatencyTimeMs = 50;
    private float lastTimeRender;
    public IncomingDepthHandler incomingDepthHandler;
    public VideoRendererMR videoRendererMR;
    public static int currentPacketNumber=0;
    public Text debugText;
    public Text vertexText;
    public Text zMax1Text;
    public Text zMax2Text;
    [UnityEngine.Range(0.01f, 0.08f)]
    public float maxEdgeLength = 0.08f;

    public bool cacheHologram = false;
    public int maxColorPacketNumber; // just for observing
    public YuvVideoRenderer YuvVideoRenderer;
    [FormerlySerializedAs("Usg")] public Us us;
    
    //[Tooltip("Change X Rotation of Hologram.")][UnityEngine.Range(-90, 90)]
    public static Transform hologramTransform;
    public GameObject hologramTransformGO;
    public byte packetCameraNumber;
    public static byte previousCameraNumber = 255;
    public List<IncomingMeshRenderer> incomingMeshRenderer;

    private ProfilerMarker loadTextureDataMarker = new ProfilerMarker("LoadTextureData");
    private ProfilerMarker uploadTextureToGpuMarker = new ProfilerMarker("UploadTextureToGPU");
    public WebRTCManager WebRtcManager;

    // Start is called before the first frame update
    void Start()
    {
        incomingDepthHandler.bufferSize = senderFPS * 5;
        videoRendererMR.bufferSize = senderFPS * 5; // buffer 5 sec
        debugText.text = "Not Connected.";
        if(hologramTransformGO != null)
            hologramTransform = hologramTransformGO.transform;
        lastTimeRender = Time.time;
        
        if(CommonParameters.numberOfCameras == 1)
            incomingMeshRenderer.RemoveAt(1);
    }

    // Update is called once per frame
    void Update()
    {

        
        byte[] incomingDepthPacket;
        if (CommonParameters.useWebRtcDataChannel)
            incomingDepthPacket = PeerConnection.incomingDepthPacket;
        else
        {
            incomingDepthPacket = WsClient.incomingDepthPacket;
        }
        // Check if connection reset
        if (WebRtcManager.doConnectionReset)
        {
            incomingDepthHandler.maxDepthPacketNumber = 0;
            incomingDepthHandler.depthData = new Dictionary<int, Tuple<byte[], byte>>();
            incomingDepthPacket  = new byte[CommonParameters.depthPacketSize]; 
            videoRendererMR.maxColorPacketNumber = 0;
            videoRendererMR.colorData = new Dictionary<int, byte[]>();
            currentPacketNumber = 0;
            WebRtcManager.doConnectionReset = false;
        }
        
        
        //Thread 3: Set data for render
        if (incomingDepthPacket != null && Time.time > (lastTimeRender + (1.0 / senderFPS)))
        {
           
            var maxPacketNumber =
                Math.Min(incomingDepthHandler.maxDepthPacketNumber, videoRendererMR.maxColorPacketNumber);
            if (currentPacketNumber <= maxPacketNumber)
            {
                if (currentPacketNumber < maxPacketNumber - senderFPS * (maxLatencyTimeMs / 1000.0f)) // check if more than 2 sec behind 
                {
                    print("Reset because latency became too high.");
                    currentPacketNumber = maxPacketNumber;
                }
                
                if (currentPacketNumber < maxPacketNumber - senderFPS * (maxWaitTimeMs / 1000.0f)) // check if more than 2 sec behind 
                {
                    print("Skip packet because already waited for " + maxWaitTimeMs + " ms.");
                    currentPacketNumber++;
                }

                if (currentPacketNumber % (senderFPS * 2) == 0) // update stats every two seconds
                {
                    maxColorPacketNumber = videoRendererMR.maxColorPacketNumber; // just for visualization
                    string text = "Net sync stats:\nRender packet: " + currentPacketNumber + "\nMax color packet: " +
                                  videoRendererMR.maxColorPacketNumber + "\nMax depth packet: " +
                                  incomingDepthHandler.maxDepthPacketNumber;
                    debugText.text = text;
                } 



                if (incomingDepthHandler.depthData.ContainsKey(currentPacketNumber) &&
                    videoRendererMR.colorData.ContainsKey(currentPacketNumber)) // else keep old depth
                {

                    packetCameraNumber = incomingDepthHandler.depthData[currentPacketNumber].Item2;
                    if (previousCameraNumber != packetCameraNumber && YuvVideoRenderer != null)
                    {
                        if (!cacheHologram)
                        {
                            if (previousCameraNumber != 255)
                            {
                                incomingMeshRenderer[previousCameraNumber].disableHologram();
                            }

                            incomingMeshRenderer[packetCameraNumber].enableHologram();
                        }
                        else
                        {
                            if (previousCameraNumber != 255)
                            {
                                incomingMeshRenderer[previousCameraNumber].enableHologram();
                            }

                            incomingMeshRenderer[packetCameraNumber].enableHologram();
                            

                        }
                    }
                    previousCameraNumber = packetCameraNumber;

                    if (incomingMeshRenderer.Count == 1)
                        packetCameraNumber = 0;
                    incomingMeshRenderer[packetCameraNumber].currentDepthArray =
                        incomingDepthHandler.depthData[currentPacketNumber].Item1;

                    // Copy data from C# buffer into system memory managed by Unity.
                    // Note: This only "looks right" in Unity because we apply the
                    // "YUVFeedShader(Unlit)" to the texture (converting YUV planar to RGB).
                    // Note: Texture2D.LoadRawTextureData() expects some bottom-up texture data but
                    // the WebRTC video frame is top-down, so the image is uploaded vertically flipped,
                    // and needs to be flipped by in the shader used to sample it. See #388.
                    using (var profileScope = loadTextureDataMarker.Auto())
                    {
                        unsafe
                        {
                            fixed (void* buffer = videoRendererMR.colorData[currentPacketNumber])
                            {
                                var src = new IntPtr(buffer);
                                int lumaSize = CommonParameters.lumaWidth * CommonParameters.lumaHeight;
                                incomingMeshRenderer[packetCameraNumber]._textureY.LoadRawTextureData(src, lumaSize);
                                src += lumaSize;
                                int chromaSize = CommonParameters.chromaWidth * CommonParameters.chromaHeight;
                                incomingMeshRenderer[packetCameraNumber]._textureU.LoadRawTextureData(src, chromaSize);
                                src += chromaSize;
                                incomingMeshRenderer[packetCameraNumber]._textureV.LoadRawTextureData(src, chromaSize);
                            }
                        }
                    }

                    // Upload from system memory to GPU
                    using (var profileScope = uploadTextureToGpuMarker.Auto())
                    {
                        incomingMeshRenderer[packetCameraNumber]._textureY.Apply();
                        incomingMeshRenderer[packetCameraNumber]._textureU.Apply();
                        incomingMeshRenderer[packetCameraNumber]._textureV.Apply();
                    }

                    if (YuvVideoRenderer != null)
                    {
                        YuvVideoRenderer._textureY = incomingMeshRenderer[packetCameraNumber]._textureY;
                        YuvVideoRenderer._textureU = incomingMeshRenderer[packetCameraNumber]._textureU;
                        YuvVideoRenderer._textureV = incomingMeshRenderer[packetCameraNumber]._textureV;
                    }

                    currentPacketNumber++;
                    lastTimeRender = Time.time;
                }
                else
                {

                }
            }

        }
    }

    public void positionHolograms()
    {
        foreach (var mesh in incomingMeshRenderer)
        {
            mesh.rePositionHologram();
        }
    }
    
    public void depthFactorSlider(SliderEventData data)
    {
        foreach (var mesh in incomingMeshRenderer)
        {
            mesh.depthFactor = data.NewValue;
        }
    }
    
    public void updateCoarseFactor(SliderEventData data)
    {
        foreach (var mesh in incomingMeshRenderer)
        {
            mesh.coarseFactor = Mathf.RoundToInt(data.NewValue * 3 + 1);
        }

        vertexText.text = "Vertex Reduction: " + incomingMeshRenderer[0].coarseFactor;
    }
    
    public void updateZmin(SliderEventData data)
    {
        foreach (var mesh in incomingMeshRenderer)
        {
            mesh.zMin = data.NewValue * 10;
        }
        
    }
        
    public void updateZmax(SliderEventData data)
    {
        if (data.Slider.name.Contains("Slider0"))
        {
            incomingMeshRenderer[0].zMax = data.NewValue * 10;
            zMax1Text.text = "Z Max Cam 1: " + incomingMeshRenderer[0].zMax.ToString("F1")+"m";
        }
        else if(data.Slider.name.Contains("Slider1")) 
        {
            incomingMeshRenderer[1].zMax = data.NewValue * 10;
            zMax2Text.text = "Z Max Cam 2: " + incomingMeshRenderer[1].zMax.ToString("F1")+"m";
        }
    }
    
    public void toggleComputeAlpha() 
    {
        foreach (var mesh in incomingMeshRenderer)
        {
            if (mesh.colorAlpha == 0)
            {
                mesh.colorAlpha = 1;
                print("Compute Alpha on");
            }
            else
            {
                mesh.colorAlpha = 0;
                print("Compute Alpha off");
            }
        }
    }
        
    public void toggleComputeEdges()
    {
        foreach (var mesh in incomingMeshRenderer)
        {
            if (mesh.moveEdges == 0)
            {
                mesh.moveEdges = 1;
                print("ComputeEdges on");
            }
            else
            {
                mesh.moveEdges = 0;
                print("ComputeEdges off");
            }
        }
    }  
    
}
