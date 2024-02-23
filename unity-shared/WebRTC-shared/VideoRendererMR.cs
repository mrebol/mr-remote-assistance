// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using Unity.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.WebRTC.Unity.Editor;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Utility component used to play video frames obtained from a WebRTC video track. This can indiscriminately
    /// play video frames from a video track source on the local peer as well as video frames from a remote video
    /// receiver obtaining its frame from a remote WebRTC peer.
    /// </summary>
    /// <remarks>
    /// This component writes to the attached <a href="https://docs.unity3d.com/ScriptReference/Material.html">Material</a>,
    /// via the attached <a href="https://docs.unity3d.com/ScriptReference/Renderer.html">Renderer</a>.
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("MixedReality-WebRTC/Video Renderer MR")]
    public class VideoRendererMR : MonoBehaviour
    {
        [Tooltip("Max playback framerate, in frames per second")]
        [Range(0.001f, 120f)]
        public float MaxFramerate = 30f;

        [Header("Statistics")]
        [ToggleLeft]
        public bool EnableStatistics = true;

        /// <summary>
        /// A textmesh onto which frame load stat data will be written
        /// </summary>
        /// <remarks>
        /// This is how fast the frames are given from the underlying implementation
        /// </remarks>
        [Tooltip("A textmesh onto which frame load stat data will be written")]
        public TextMesh FrameLoadStatHolder;

        /// <summary>
        /// A textmesh onto which frame present stat data will be written
        /// </summary>
        /// <remarks>
        /// This is how fast we render frames to the display
        /// </remarks>
        [Tooltip("A textmesh onto which frame present stat data will be written")]
        public TextMesh FramePresentStatHolder;

        /// <summary>
        /// A textmesh into which frame skip stat dta will be written
        /// </summary>
        /// <remarks>
        /// This is how often we skip presenting an underlying frame
        /// </remarks>
        [Tooltip("A textmesh onto which frame skip stat data will be written")]
        public TextMesh FrameSkipStatHolder;

        // Source that this renderer is currently subscribed to.
        private IVideoSource _source;

        /// <summary>
        /// Internal reference to the attached texture
        /// </summary>
        public static Texture2D _textureY = null; // also used for ARGB32
        public static Texture2D _textureU = null;
        public static Texture2D _textureV = null;

        /// <summary>
        /// Internal timing counter
        /// </summary>
        private float lastUpdateTime = 0.0f;

        private Material videoMaterial;
        private float _minUpdateDelay;

        private VideoFrameQueue<I420AVideoFrameStorage> _i420aFrameQueue = null;
        private VideoFrameQueue<Argb32VideoFrameStorage> _argb32FrameQueue = null;

        private ProfilerMarker displayStatsMarker = new ProfilerMarker("DisplayStats");
        private ProfilerMarker loadTextureDataMarker = new ProfilerMarker("LoadTextureData");
        private ProfilerMarker uploadTextureToGpuMarker = new ProfilerMarker("UploadTextureToGPU");

        public I420AVideoFrameStorage currentColorFrame;

        private void Start()
        {
            CreateEmptyVideoTextures();

            // Leave 3ms of margin, otherwise it misses 1 frame and drops to ~20 FPS
            // when Unity is running at 60 FPS.
            _minUpdateDelay = Mathf.Max(0f, 1f / Mathf.Max(0.001f, MaxFramerate) - 0.003f);
        }

        /// <summary>
        /// Start rendering the passed source.
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="VideoTrackSource.VideoStreamStarted"/> or <see cref="VideoReceiver.VideoStreamStarted"/>.
        /// </remarks>
        public void StartRendering(IVideoSource source)
        {
            bool isRemote = (source is RemoteVideoTrack);
            int frameQueueSize = (isRemote ? 5 : 3);

            switch (source.FrameEncoding)
            {
                case VideoEncoding.I420A:
                    _i420aFrameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(frameQueueSize);
                    source.I420AVideoFrameReady += I420AVideoFrameReady;
                    break;

                case VideoEncoding.Argb32:
                    _argb32FrameQueue = new VideoFrameQueue<Argb32VideoFrameStorage>(frameQueueSize);
                    source.Argb32VideoFrameReady += Argb32VideoFrameReady;
                    break;
            }
        }

        /// <summary>
        /// Stop rendering the passed source. Must be called with the same source passed to <see cref="StartRendering(IVideoSource)"/>
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="VideoTrackSource.VideoStreamStopped"/> or <see cref="VideoReceiver.VideoStreamStopped"/>.
        /// </remarks>
        public void StopRendering(IVideoSource _)
        {
            // Clear the video display to not confuse the user who could otherwise
            // think that the video is still playing but is lagging/frozen.
            CreateEmptyVideoTextures();
        }

        protected void OnDisable()
        {
            // Clear the video display to not confuse the user who could otherwise
            // think that the video is still playing but is lagging/frozen.
            CreateEmptyVideoTextures();
        }

        protected void I420AVideoFrameReady(I420AVideoFrame frame)
        {
            // This callback is generally from a non-UI thread, but Unity object access is only allowed
            // on the main UI thread, so defer to that point.
            _i420aFrameQueue.Enqueue(frame);
        }

        protected void Argb32VideoFrameReady(Argb32VideoFrame frame)
        {
            // This callback is generally from a non-UI thread, but Unity object access is only allowed
            // on the main UI thread, so defer to that point.
            
            _argb32FrameQueue.Enqueue(frame);
        }

        private void CreateEmptyVideoTextures()
        {
            // Create a default checkboard texture which visually indicates
            // that no data is available. This is useful for debugging and
            // for the user to know about the state of the video.
            _textureY = new Texture2D(2, 2);
            _textureY.SetPixel(0, 0, Color.blue);
            _textureY.SetPixel(1, 1, Color.blue);
            _textureY.Apply();
            _textureU = new Texture2D(2, 2);
            _textureU.SetPixel(0, 0, Color.blue);
            _textureU.SetPixel(1, 1, Color.blue);
            _textureU.Apply();
            _textureV = new Texture2D(2, 2);
            _textureV.SetPixel(0, 0, Color.blue);
            _textureV.SetPixel(1, 1, Color.blue);
            _textureV.Apply();

            // Assign that texture to the video player's Renderer component
            videoMaterial = GetComponent<Renderer>().material;
            if (_i420aFrameQueue != null)
            {
                videoMaterial.SetTexture("_YPlane", _textureY);
                videoMaterial.SetTexture("_UPlane", _textureU);
                videoMaterial.SetTexture("_VPlane", _textureV);
            }
            else if (_argb32FrameQueue != null)
            {
                videoMaterial.SetTexture("_MainTex", _textureY);
            }
        }

        //// <summary>
        /// Unity Engine Start() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        /// </remarks>
        private void Update()
        {
            if ((_i420aFrameQueue != null) || (_argb32FrameQueue != null))
            {
#if UNITY_EDITOR
                // Inside the Editor, constantly update _minUpdateDelay to
                // react to user changes to MaxFramerate.

                // Leave 3ms of margin, otherwise it misses 1 frame and drops to ~20 FPS
                // when Unity is running at 60 FPS.
                _minUpdateDelay = Mathf.Max(0f, 1f / Mathf.Max(0.001f, MaxFramerate) - 0.003f);
#endif
                // FIXME - This will overflow/underflow the queue if not set at the same rate
                // as the one at which frames are enqueued!
                var curTime = Time.time;
                if (curTime - lastUpdateTime >= _minUpdateDelay)
                {
                    if (_i420aFrameQueue != null)
                    {
                        TryProcessI420AFrame();
                    }
                    else if (_argb32FrameQueue != null)
                    {
                        TryProcessArgb32Frame();
                    }
                    lastUpdateTime = curTime;
                }

                if (EnableStatistics)
                {
                    // Share our stats values, if possible.
                    using (var profileScope = displayStatsMarker.Auto())
                    {
                        IVideoFrameQueue stats = (_i420aFrameQueue != null ? (IVideoFrameQueue)_i420aFrameQueue : _argb32FrameQueue);
                        if (FrameLoadStatHolder != null)
                        {
                            FrameLoadStatHolder.text = stats.QueuedFramesPerSecond.ToString("F2");
                        }
                        if (FramePresentStatHolder != null)
                        {
                            FramePresentStatHolder.text = stats.DequeuedFramesPerSecond.ToString("F2");
                        }
                        if (FrameSkipStatHolder != null)
                        {
                            FrameSkipStatHolder.text = stats.DroppedFramesPerSecond.ToString("F2");
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
        public Dictionary<int, byte[]> colorData = new Dictionary<int, byte[]>();
        public int maxColorPacketNumber;
        public int bufferSize;
        
        /// <summary>
        /// Internal helper that attempts to process frame data in the frame queue
        /// </summary>
        private void TryProcessI420AFrame()
        {
            if (_i420aFrameQueue.TryDequeue(out I420AVideoFrameStorage frame))
            {
                uint colorPacketNumber = 0;

                // Read data into Dictionary
                if (frame.Width * frame.Height <= 27)
                {
                    _i420aFrameQueue.RecycleStorage(frame);
                    return;
                }
                
                var controlPattern = (readBit(frame.Buffer[24]), readBit(frame.Buffer[25]), readBit(frame.Buffer[26]), readBit(frame.Buffer[27]));
                if (controlPattern != (true, false, true, false))
                {
                    print("Error: wrong control pattern.");
                    _i420aFrameQueue.RecycleStorage(frame);
                    return;
                } 
                
                colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[0])) << 0;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[1])) << 1;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[2])) << 2;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[3])) << 3;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[4])) << 4;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[5])) << 5;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[6])) << 6;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[7])) << 7;

            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[8])) << 8;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[9])) << 9;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[10])) << 10;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[11])) << 11;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[12])) << 12;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[13])) << 13;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[14])) << 14;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[15])) << 15;

            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[16])) << 16;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[17])) << 17;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[18])) << 18;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[19])) << 19;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[20])) << 20;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[21])) << 21;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[22])) << 22;
            colorPacketNumber += Convert.ToUInt32(readBit(frame.Buffer[23])) << 23;
            
            int colorPacketNumberI = Convert.ToInt32(colorPacketNumber);
            if (colorData.ContainsKey(colorPacketNumberI)) // check if old packet
            {
                _i420aFrameQueue.RecycleStorage(frame);
                return;
            }    
            
            if (colorPacketNumberI > maxColorPacketNumber - bufferSize) // add to dict if not too old // bufferSize= 150 (10sec)
            {
                if (colorPacketNumberI > maxColorPacketNumber)
                    maxColorPacketNumber = colorPacketNumberI;
                
                colorData.Add(colorPacketNumberI, frame.Buffer.ToArray());  // .ToArray for deepCopy
                
            }

                // Recycle the video frame packet for a later frame
                _i420aFrameQueue.RecycleStorage(frame);
            }
            
            //Thread 2: delete if dict too large
            if (colorData.Count > bufferSize * 2)
            {
                var itemsToRemove = colorData.Where(f => f.Key < maxColorPacketNumber - bufferSize).ToArray();
                foreach (var item in itemsToRemove)
                {
                    colorData.Remove(item.Key);
                }
            }
        }

        /// <summary>
        /// Internal helper that attempts to process frame data in the frame queue
        /// </summary>
        private void TryProcessArgb32Frame()
        {
            if (_argb32FrameQueue.TryDequeue(out Argb32VideoFrameStorage frame))
            {
                int width = (int)frame.Width;
                int height = (int)frame.Height;
                if (_textureY == null || (_textureY.width != width || _textureY.height != height))
                {
                    _textureY = new Texture2D(width, height, TextureFormat.BGRA32, mipChain: false);
                    videoMaterial.SetTexture("_MainTex", _textureY);
                }

                // Copy data from C# buffer into system memory managed by Unity.
                // Note: Texture2D.LoadRawTextureData() expects some bottom-up texture data but
                // the WebRTC video frame is top-down, so the image is uploaded vertically flipped,
                // and needs to be flipped by in the shader used to sample it. See #388.
                using (var profileScope = loadTextureDataMarker.Auto())
                {
                    unsafe
                    {
                        fixed (void* buffer = frame.Buffer)
                        {
                            var src = new IntPtr(buffer);
                            int size = width * height * 4;
                            _textureY.LoadRawTextureData(src, size);
                        }
                    }
                }

                // Upload from system memory to GPU
                using (var profileScope = uploadTextureToGpuMarker.Auto())
                {
                    _textureY.Apply();
                }

                // Recycle the video frame packet for a later frame
                _argb32FrameQueue.RecycleStorage(frame);
            }
        }
    }
}
