

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;


/* CAMERA APP 1 */
namespace Telepresence 
{

    /// <summary>
    /// SceneMeshRendererGpu renders virtual mesh of the environment in the scene, as detected by the given sensor. This component uses GPU for mesh processing rather than CPU. 
    /// </summary>
    public class MeshRender : MonoBehaviour
    {
        public byte meshNumber;
        
        [Tooltip("Depth sensor index - 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0; 

        

        [Tooltip("Mesh coarse factor.")] [UnityEngine.Range(0.25f, 4)]
        public float coarseFactor = 1;

        [Tooltip("Horizontal limit - minimum, in meters.")] [UnityEngine.Range(-5f, 5f)]
        public float xMin = -2f;

        [Tooltip("Horizontal limit - maximum, in meters.")] [UnityEngine.Range(-5f, 5f)]
        public float xMax = 2f;

        [Tooltip("Vertical limit - minimum, in meters.")] [UnityEngine.Range(-5f, 5f)]
        public float yMin = 0f;

        [Tooltip("Vertical limit - maximum, in meters.")] [UnityEngine.Range(-5f, 5f)]
        public float yMax = 2.5f;

        [Tooltip("Distance limit - minimum, in meters.")] [UnityEngine.Range(0.0f, 10f)]
        public float zMin = 1f;

        [Tooltip("Distance limit - maximum, in meters.")] [UnityEngine.Range(0.5f, 10f)]
        public float zMax = 3f;

        public float updateMeshFPS = 20;

        [Tooltip("Time interval between mesh-collider updates, in seconds. 0 means no mesh-collider updates.")]
        private float updateColliderInterval = 0f;

        private bool saveFiles = false;

        //[Tooltip("Mesh coarse factor.")]
        [UnityEngine.Range(0, 100)]
        public int historyFrames = 4;
        private int historyFramesPrev = 0;
        [UnityEngine.Range(0, 8)][HideInInspector]
        public int innerBandThreshold = 2;// default 2;  // TODO adjust
        [UnityEngine.Range(0, 16)][HideInInspector]
        public int outerBandThreshold = 5;// default 5;
        private DepthSmoothing dm;
        public List<byte[]> depthFilteredList;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int interpolateCurrentDepth;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int interpolatePreviousDepth;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int interpolateHistoryDepth;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int replaceZeroHistoric;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int convergeFlat;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int convergeNoisy;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int stableTriangles;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int colorAlpha;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int moveEdges;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int colorEdges;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public float depthFactor = 1;


        private int frameCounter = 0;
        
        // reference to object's mesh
        private Mesh mesh = null;

        private Material meshShaderMat = null;

        // space table
        private Vector3[] spaceTable = null;
        private ComputeBuffer spaceTableBuffer = null;

        private Texture2D depthTexture = null;
        private ComputeBuffer depthImageBuffer = null;
        private ComputeBuffer depthHistory = null;
        private ComputeBuffer previousDepthImage = null;
        private byte[] depthHistoryArray { set; get; }

        private byte[] previousDepthFrame;

        public byte[] previousDepthImageArray { set; get; }

        // hologram
        byte[] hologramMeshXY;

        // render textures 
        public Texture2D colorTexture2D;

        public static RenderTexture colorRenderTexture;
        private bool colorTextureCreated = false;

        //Annotations
        public static byte[] AnnotationArray { set; get; }
        public static Texture2D AnnotationTexture2D;
        public byte[] PointerArray { set; get; }
        public static Texture2D PointerTexture;
        public Vector2Int annotationPixel = new Vector2Int(-1,-1);
        public Vector3 pointerIntersection = new Vector3(-1,-1, -1);
        public Vector3 PointerPosition;
        public GameObject mainCamera;
        ComputeBuffer debugBuffer;
        ComputeBuffer PointerIntersectionWorldCB;
        ComputeBuffer PointerIntersectionPxCB;
        ComputeBuffer filteredDepthCB;
        ComputeBuffer measuredDepthCB;
        public bool showHologram;


        // times
        private float lastMeshUpdateTime = 0f;
        private float lastColliderUpdateTime = 0f;


        // mesh parameters
        private bool bMeshInited = false;
        private float meshParamsCache = 0;

        public static Matrix4x4 localToWorldTransform;
        public static Matrix4x4 worldToLocalTransform;

        
        private UnityEngine.UI.RawImage colorImageDisplay;
        private UnityEngine.UI.RawImage depthImageDisplay;
        public FrameSender frameSender; 
        

        public KinectReader kinectReader;
        private byte[] inputColorFrame;
        private byte[] inputDepthFrame;
        private Texture2D depthDisplayTexture;

        void Start()
        {

            localToWorldTransform = this.transform.localToWorldMatrix;
            worldToLocalTransform = this.transform.worldToLocalMatrix;

            if (GameObject.Find("RawImage") != null)
                colorImageDisplay = GameObject.Find("RawImage").GetComponent<UnityEngine.UI.RawImage>();
            if (GameObject.Find("DepthImage") != null)
                depthImageDisplay = GameObject.Find("DepthImage").GetComponent<UnityEngine.UI.RawImage>();
            colorTexture2D = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.BGRA32, false); 

            if (GameObject.Find("Pointer") != null)
            {
                AnnotationTexture2D = new Texture2D(colorTexture2D.width, colorTexture2D.height, TextureFormat.R8, false);
                AnnotationArray = new byte[colorTexture2D.width * colorTexture2D.height];
                PointerArray = new byte[colorTexture2D.width * colorTexture2D.height];
                PointerTexture = new Texture2D(colorTexture2D.width, colorTexture2D.height, TextureFormat.R8, false);
            }
            else
            {
                AnnotationTexture2D = Texture2D.blackTexture;
                PointerTexture = Texture2D.blackTexture;
            }
            
            depthDisplayTexture = new Texture2D(CommonParameters.depthSendingWidth, CommonParameters.depthSendingHeight,
                TextureFormat.RGB565, false);


            PointerPosition = new Vector3();
            mainCamera = GameObject.Find("Main Camera");
            debugBuffer = new ComputeBuffer(9, sizeof(float), ComputeBufferType.Default); //.Default);
            PointerIntersectionWorldCB = new ComputeBuffer(3, sizeof(float), ComputeBufferType.Default);
            PointerIntersectionPxCB = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Default);  //.Default);
            filteredDepthCB = new ComputeBuffer(CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth / 2, sizeof(uint), ComputeBufferType.Default);  //.Default);
            measuredDepthCB = new ComputeBuffer(CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth / 2, sizeof(uint), ComputeBufferType.Default);  //.Default);
            
            depthFilteredList = new List<byte[]>();
            
            dm = new DepthSmoothing();
            
            inputColorFrame = new byte[CommonParameters.ColorWidth * CommonParameters.ColorHeight * 4];
            inputDepthFrame= new byte[CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight * 2];
            previousDepthImageArray= new byte[CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight * 2];
            
        }


        void OnDestroy()
        {
            if (bMeshInited)
            {
                // release the mesh-related resources
                FinishMesh();
            }
            
        }


        void FixedUpdate()
        {
            if (mesh == null)
            {
                // init mesh and its related data
                InitMesh();
            }

            if (bMeshInited)
            {
                
                if (colorImageDisplay != null)
                {

                   
                }
                
                if(CommonParameters.numberOfCameras > 0)
                    // update the mesh
                    UpdateMesh();
            }
        }


        // inits the mesh and related data
        private void InitMesh()
        {
            // create mesh
            mesh = new Mesh
            {
                name = "SceneMesh-Sensor" + sensorIndex,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.mesh = mesh;
            }
            else
            {
                Debug.LogWarning("MeshFilter not found! You may not see the mesh on screen");
            }

            // get the mesh material
            Renderer meshRenderer = GetComponent<Renderer>();
            if (meshRenderer && meshRenderer.material)
            {
                meshShaderMat = meshRenderer.material;
            }
            
            // create point cloud color texture
            if (meshShaderMat != null)
            {
                depthImageBuffer = CreateComputeBuffer(depthImageBuffer, 
                CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight /2, sizeof(uint));
                meshShaderMat.SetBuffer("_DepthMap", depthImageBuffer);
                    
                previousDepthImage = CreateComputeBuffer(previousDepthImage, 
                    CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight /2, sizeof(uint));


                // create mesh vertices & indices
                CreateMeshVertInd();
                bMeshInited = true;
            }
        }


        // creates the mesh vertices and indices
        private void CreateMeshVertInd()
        {
            int xVerts = (int)(CommonParameters.depthSendingWidth / coarseFactor); // + 1;
            int yVerts = (int)(CommonParameters.depthSendingHeight / coarseFactor); // + 1;
            int vCount = xVerts * yVerts;

            // mesh vertices
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] meshVertices = new Vector3[xVerts * yVerts];
            Vector3[] meshNormals = new Vector3[xVerts * yVerts];
            Vector2[] meshUv = new Vector2[xVerts * yVerts];

            float vsx = (float) coarseFactor / (float) CommonParameters.depthSendingWidth;
            float vsy = (float) coarseFactor / (float) CommonParameters.depthSendingHeight;

            for (int y = 0, vi = 0; y < yVerts; y++)
            {
                for (int x = 0; x < xVerts; x++, vi++)
                {
                    meshVertices[vi] = new Vector3(x * vsx, y * vsy, 0f);
                    meshNormals[vi] = new Vector3(0f, 1f, 0f); // 0f, 0f, -1f
                    meshUv[vi] = new Vector2(x * vsx, y * vsy);
                }
            }

            mesh.vertices = meshVertices;
            mesh.normals = meshNormals;
            mesh.uv = meshUv;


                int[] meshIndices = new int[(xVerts - 1) * (yVerts - 1) * 6];
                for (int y = 0, ii = 0; y < (yVerts - 1); y++)
                {
                    for (int x = 0; x < (xVerts - 1); x++)
                    {
                        meshIndices[ii++] = (y + 1) * xVerts + x; // below
                        meshIndices[ii++] = y * xVerts + x + 1; // right
                        meshIndices[ii++] = y * xVerts + x; // current

                        meshIndices[ii++] = (y + 1) * xVerts + x + 1; // below-right
                        meshIndices[ii++] = y * xVerts + x + 1;  // right
                        meshIndices[ii++] = (y + 1) * xVerts + x;  // below
                    }
                }

                mesh.SetIndices(meshIndices, MeshTopology.Triangles, 0);
            

            meshParamsCache = coarseFactor + (false ? 10 : 0);
        }


        // releases mesh-related resources
        private void FinishMesh()
        {

            if (depthImageBuffer != null)
            {
                depthImageBuffer.Release();
                depthImageBuffer.Dispose();
                depthImageBuffer = null;
            }
            
            if (depthHistory != null)
            {
                depthHistory.Release();
                depthHistory.Dispose();
                depthHistory = null;
            }            
            
            if (previousDepthImage != null)
            {
                previousDepthImage.Release();
                previousDepthImage.Dispose();
                previousDepthImage = null;
            }

            if (colorRenderTexture && colorTextureCreated)
            {
                colorRenderTexture.Release();
                colorRenderTexture = null;
            }

            if (spaceTableBuffer != null)
            {
                spaceTableBuffer.Dispose();
                spaceTableBuffer = null;
            }
            
            PointerIntersectionPxCB.Release();
            measuredDepthCB.Release();
            filteredDepthCB.Release();
            PointerIntersectionWorldCB.Release();
            PointerIntersectionWorldCB = null;
            
            bMeshInited = false;
        }


        // updates the mesh according to current depth frame
        private void UpdateMesh()
        {
            if (bMeshInited && meshShaderMat != null && ((Time.time - lastMeshUpdateTime) >= 1.0 / updateMeshFPS))
            {
                inputColorFrame = kinectReader.colorImage;
                    inputDepthFrame = kinectReader.depthImage;
                    frameCounter++;

                    lastMeshUpdateTime = Time.time;

                    float paramsCache = coarseFactor + (false ? 10 : 0);
                    if (meshParamsCache != paramsCache)
                    {
                        CreateMeshVertInd();
                    }

                    if (depthImageBuffer != null)
                    {

                        depthImageBuffer.SetData(inputDepthFrame);

                        depthDisplayTexture.LoadRawTextureData(inputDepthFrame);
                        depthDisplayTexture.Apply();
                        depthImageDisplay.texture = depthDisplayTexture;

                        if (saveFiles && CommonParameters.currentFrameSendingNumber == 100)
                            //if (saveFiles)
                        {
                            // Save as png
                            // Encode texture into PNG
                            byte[] depthBytes = depthDisplayTexture.EncodeToPNG();
                            // For testing purposes, also write to a file in the project folder
                            File.WriteAllBytes(Application.dataPath + "/SavedDepth.png", depthBytes);
                        }
                    }

                    if (historyFrames != historyFramesPrev)
                    {
                        historyFramesPrev = historyFrames;
                        depthHistoryArray = new byte[CommonParameters.depthSendingHeight *
                                                     CommonParameters.depthSendingWidth * historyFrames * 2];
                        if (depthHistory != null)
                        {
                            depthHistory.Dispose();
                            depthHistory = null;
                        }

                        if (historyFrames > 0)
                        {
                            depthHistory = CreateComputeBuffer(depthHistory,
                                (CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight / 2) *
                                historyFrames,
                                sizeof(uint));
                        }

                        while (depthFilteredList.Count > historyFrames)
                        {
                            depthFilteredList.RemoveAt(0);
                        }
                    }

                    if (historyFrames > 0)
                    {

                        for (int i = 0; i < depthFilteredList.Count; i++)
                        {
                            depthFilteredList[i].CopyTo(depthHistoryArray,
                                i * CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth * 2);
                        }

                        depthHistory.SetData(depthHistoryArray);
                    }

                    if (frameCounter > 1)
                    {
                        previousDepthFrame.CopyTo(previousDepthImageArray, 0); // todo remove copy
                        previousDepthImage.SetData(previousDepthImageArray);
                    }

                    meshShaderMat.SetBuffer("_DepthMap", depthImageBuffer);
                    meshShaderMat.SetBuffer("_depthHistory", depthHistory);
                    meshShaderMat.SetBuffer("_previousDepthImage", previousDepthImage);
                    meshShaderMat.SetInt("_historyFrames", historyFrames);
                    meshShaderMat.SetInt("_innerBandThreshold", historyFrames);
                    meshShaderMat.SetInt("_outerBandThreshold", historyFrames);
                    AnnotationTexture2D.LoadRawTextureData(AnnotationArray);
                    AnnotationTexture2D.Apply();
                    meshShaderMat.SetTexture("_AnnotationTex", AnnotationTexture2D);

                    PointerTexture.LoadRawTextureData(PointerArray);
                    PointerTexture.Apply();
                    meshShaderMat.SetTexture("_PointerTex", PointerTexture);
                    meshShaderMat.SetVector("_PointerPosition", PointerPosition);
                    meshShaderMat.SetVector("_CameraPosition", mainCamera.transform.position);


                    colorTexture2D.LoadRawTextureData(inputColorFrame);
                    colorTexture2D.Apply();
                    colorImageDisplay.texture = colorTexture2D;
                    if (saveFiles && CommonParameters.currentFrameSendingNumber == 100)
                    {
                        // Save as png
                        // Encode texture into PNG
                        byte[] colorBytes = colorTexture2D.EncodeToPNG();
                        // For testing purposes, also write to a file in the project folder
                        File.WriteAllBytes(Application.dataPath + "/SavedColor.png", colorBytes);
                        saveFiles = false;
                    }

                    meshShaderMat.SetTexture("_ColorTex", colorTexture2D); // RenderTexture -> KinectInterop()
                    meshShaderMat.SetVector("_SpaceScale", new Vector3(1, 1, 1));

                    meshShaderMat.SetVector("_DepthTexRes", new Vector2(DepthData.sendWidth, DepthData.sendHeight));
                    meshShaderMat.SetVector("_ColorTexRes",
                        new Vector2(CommonParameters.ColorWidth, CommonParameters.ColorHeight));
                    meshShaderMat.SetInt("_isColorRegistered", Convert.ToInt32(CommonParameters.COLOR_SDK_REGISTERED));
                    meshShaderMat.SetInt("_useDepthCoordinateSystem", CommonParameters.useDepthCoordinateSystem);
                    meshShaderMat.SetInt("_flipScene", CommonParameters.flipScene);
                    meshShaderMat.SetInt("_showHologram", Convert.ToInt32(showHologram));

                    meshShaderMat.SetInt("_interpolateCurrentDepth", interpolateCurrentDepth);
                    meshShaderMat.SetInt("_interpolatePreviousDepth", interpolatePreviousDepth);
                    meshShaderMat.SetInt("_interpolateHistoryDepth", interpolateHistoryDepth);
                    meshShaderMat.SetInt("_replaceZeroHistoric", replaceZeroHistoric);
                    meshShaderMat.SetInt("_convergeFlat", convergeFlat);
                    meshShaderMat.SetInt("_convergeNoisy", convergeNoisy);
                    meshShaderMat.SetInt("_stableTriangles", stableTriangles);
                    meshShaderMat.SetInt("_colorAlpha", colorAlpha);
                    meshShaderMat.SetInt("_moveEdges", moveEdges);
                    meshShaderMat.SetInt("_colorEdges", colorEdges);

                    meshShaderMat.SetFloat("_vsx", (float) coarseFactor / (float) CommonParameters.depthSendingWidth);
                    meshShaderMat.SetFloat("_vsy", coarseFactor / (float) CommonParameters.depthSendingHeight);

                    if (frameCounter % updateMeshFPS == 0)
                    {
                        meshShaderMat.SetInt("_resetHistory", 1);
                    }

                    {
                        meshShaderMat.SetInt("_resetHistory", 0);
                    }

                    meshShaderMat.SetFloatArray("_ColorExtrinsicsR", KinectReader.colorExtrinsicsR);
                    meshShaderMat.SetVector("_ColorExtrinsicsT", KinectReader.colorExtrinsicsT);
                    meshShaderMat.SetVector("_colorIntrinsicsCxy", KinectReader.colorIntrinsicsCxy);
                    meshShaderMat.SetVector("_colorIntrinsicsFxy", KinectReader.colorIntrinsicsFxy);
                    meshShaderMat.SetFloatArray("_ColorDistortionRadial", KinectReader.colorDistortionRadial);
                    meshShaderMat.SetVector("_ColorDistortionTangential", KinectReader.colorDistortionTangential);

                    meshShaderMat.SetFloatArray("_DepthExtrinsicsR", KinectReader.depthExtrinsicsR);
                    meshShaderMat.SetVector("_DepthExtrinsicsT", KinectReader.depthExtrinsicsT);
                    meshShaderMat.SetVector("_depthIntrinsicsCxy", KinectReader.depthIntrinsicsCxy);
                    meshShaderMat.SetVector("_depthIntrinsicsFxy", KinectReader.depthIntrinsicsFxy);
                    meshShaderMat.SetFloatArray("_DepthDistortionRadial", KinectReader.depthDistortionRadial);
                    meshShaderMat.SetVector("_DepthDistortionTangential", KinectReader.depthDistortionTangential);

                    meshShaderMat.SetInt("_CoarseFactor", (int) coarseFactor); // 1
                    meshShaderMat.SetInt("_IsPointCloud", false ? 1 : 0); // false

                    meshShaderMat.SetMatrix("_LocalToWorldTransform",
                        localToWorldTransform); // 1.00000	0.00000	0.00000	0.00000, 0.00000 0.99452 -0.10453	1.00000, 0.00000	0.10453	0.99452	0.00000, 0.00000	0.00000	0.00000	1.00000
                    meshShaderMat.SetMatrix("_WorldToLocalTransform",
                        worldToLocalTransform); // 1.00000	0.00000	0.00000	0.00000, 0.00000 0.99452 -0.10453	1.00000, 0.00000	0.10453	0.99452	0.00000, 0.00000	0.00000	0.00000	1.00000
                    meshShaderMat.SetVector("_PosMin", new Vector3(xMin, yMin, zMin)); // (-5, -5, 0.5)
                    meshShaderMat.SetVector("_PosMax", new Vector3(xMax, yMax, zMax)); // (5, 5, 10)
                    meshShaderMat.SetFloat("_DepthFactor", depthFactor);

                    // mesh bounds
                    Vector3 boundsCenter = new Vector3((xMax - xMin) / 2f, (yMax - yMin) / 2f, (zMax - zMin) / 2f);
                    Vector3 boundsSize = new Vector3((xMax - xMin), (yMax - yMin), (zMax - zMin));
                    mesh.bounds = new Bounds(boundsCenter, boundsSize);


                    if (updateColliderInterval > 0 && (Time.time - lastColliderUpdateTime) >= updateColliderInterval)
                    {
                        lastColliderUpdateTime = Time.time;
                        MeshCollider meshCollider = GetComponent<MeshCollider>();

                        if (meshCollider)
                        {
                            meshCollider.sharedMesh = null;
                            meshCollider.sharedMesh = mesh;
                        }
                    }
                    
                    Graphics.ClearRandomWriteTargets();
                    meshShaderMat.SetPass(0);

                    meshShaderMat.SetBuffer("_DebugBuffer", debugBuffer);
                    meshShaderMat.SetBuffer("_PointerIntersectionWorld", PointerIntersectionWorldCB);
                    meshShaderMat.SetBuffer("_PointerIntersectionPx", PointerIntersectionPxCB);
                    meshShaderMat.SetBuffer("_FilteredDepth", filteredDepthCB);
                    meshShaderMat.SetBuffer("_MeasuredDepth", measuredDepthCB);

                    // https://forum.unity.com/threads/how-to-store-and-read-data-strictly-in-gpu.922769/
                    Graphics.SetRandomWriteTarget(1, PointerIntersectionWorldCB, false);
                    Graphics.SetRandomWriteTarget(2, PointerIntersectionPxCB, false);
                    Graphics.SetRandomWriteTarget(3, debugBuffer, false); // index 3 refers to u3
                    Graphics.SetRandomWriteTarget(4, filteredDepthCB, false);
                    Graphics.SetRandomWriteTarget(5, measuredDepthCB, false);
                    
                    float[] values = {2.6f, 4f, 5f, 3f, 3f, 3f, 6f, 6f, 6f};
                    debugBuffer.GetData(values);

                    float[] pointerInt = {1.1f, 2.2f, 3.3f};
                    PointerIntersectionWorldCB.GetData(pointerInt);
                    pointerIntersection.Set(pointerInt[0], pointerInt[1], pointerInt[2]);

                    uint[] pointerIntersectionPxIn = {0, 0};
                    uint[] pointerIntersectionPx = {0, 0};
                    PointerIntersectionPxCB.GetData(pointerIntersectionPx);

                    if (pointerIntersectionPx[0] > 0 && pointerIntersectionPx[1] > 0)
                    {
                        annotationPixel.Set((int) pointerIntersectionPx[0], (int) pointerIntersectionPx[1]);
                    }

                    byte[] filteredDepthOutput =
                        new byte[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth * 2];
                    filteredDepthCB.GetData(filteredDepthOutput);


                    byte[] measuredDepthOutput =
                        new byte[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth * 2];
                    measuredDepthCB.GetData(measuredDepthOutput);

                    depthFilteredList.Add(
                        measuredDepthOutput); // TODO no need to let measured depth to through the shader
                    while (depthFilteredList.Count > historyFrames)
                    {
                        depthFilteredList.RemoveAt(0);
                    }

                    Buffer.BlockCopy(filteredDepthOutput, 0, FrameSender.depthFrame, 0,filteredDepthOutput.Length);
                    previousDepthFrame = filteredDepthOutput;
                    FrameSender.colorFrame = inputColorFrame;
                    FrameSender.cameraNumber = kinectReader.capturedKinect;
            }
        }

        private void OnDisable()
        {
            debugBuffer.Dispose();
            PointerIntersectionWorldCB.Dispose();
        }
        
        
        public static RenderTexture CreateRenderTexture(RenderTexture currentTex, int width, int height, RenderTextureFormat texFormat = RenderTextureFormat.Default)
        {
            if(currentTex != null)
            {
                currentTex.Release();
            }

            RenderTexture renderTex = new RenderTexture(width, height, 0, texFormat);
            renderTex.wrapMode = TextureWrapMode.Clamp;
            renderTex.filterMode = FilterMode.Point;
            renderTex.enableRandomWrite = true;

            return renderTex;
        }
        
        public static ComputeBuffer CreateComputeBuffer(ComputeBuffer currentBuf, int bufLen, int bufStride)
        {
            if(currentBuf != null)
            {
                currentBuf.Release();
                currentBuf.Dispose();
            }

            ComputeBuffer computeBuf = new ComputeBuffer(bufLen, bufStride);
            return computeBuf;
        }
    }
}
