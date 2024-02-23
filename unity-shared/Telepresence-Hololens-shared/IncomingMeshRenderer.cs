using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.MixedReality.Toolkit.UI;
using Telepresence;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Telepresence
{

     /// <summary>
    /// SceneMeshRendererGpu renders virtual mesh of the environment in the scene, as detected by the given sensor. This component uses GPU for mesh processing rather than CPU. 
    /// </summary>
    public class IncomingMeshRenderer : MonoBehaviour
     {
         public byte meshNumber;

         [Tooltip("Mesh coarse factor.")]
        [Range(1, 12)]
        public int coarseFactor = 1;
        
        [Tooltip("Horizontal limit - minimum, in meters.")]
        [Range(-5f, 5f)]
        public float xMin = -2f;

        [Tooltip("Horizontal limit - maximum, in meters.")]
        [Range(-5f, 5f)]
        public float xMax = 2f;

        [Tooltip("Vertical limit - minimum, in meters.")]
        [Range(-5f, 5f)]
        public float yMin = 0f;

        [Tooltip("Vertical limit - maximum, in meters.")]
        [Range(-5f, 5f)]
        public float yMax = 2.5f;

        [Tooltip("Distance limit - minimum, in meters.")]
        [Range(0.0f, 10f)]
        public float zMin = 1f;

        [Tooltip("Distance limit - maximum, in meters.")]
        [Range(0.5f, 10f)]
        public float zMax = 3f;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int stableTriangles;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int colorAlpha;
        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int moveEdges;

        [Tooltip("")][UnityEngine.Range(0, 1)]
        public int colorEdges;
        public float depthFactor = 1;
        public IncomingFrameHandler IncomingFrameHandler;
        public MeshRenderer MeshRenderer;

        public Transform finalCorrection;
        public Transform camera2Correction;
        
        [Tooltip("Time interval between scene mesh updates, in seconds. 0 means no wait.")]
        private float updateMeshInterval = 0f;

        [Tooltip("Time interval between mesh-collider updates, in seconds. 0 means no mesh-collider updates.")]
        private float updateColliderInterval = 0f;
        

        // reference to object's mesh
        public Mesh mesh = null;
        private Transform trans = null;
        private Material meshShaderMat = null;

        //incoming Depth buffer
        private ComputeBuffer currentDepthComputeBuffer = null; 
        public byte[] currentDepthArray { get; set; }

        
        // 2D textures
        public static RenderTexture incomingColorTextureY; 
        public static RenderTexture incomingColorTextureU; 
        public static RenderTexture incomingColorTextureV;

        //Annotations
        public static Texture2D AnnotationTexture2D;
        public static byte[] AnnotationArray;
        public Vector2Int annotationPixel = new Vector2Int(-1,-1);
        public byte[] PointerArray;
        public static Texture2D PointerTexture;
        public Vector3 pointerIntersection = new Vector3(-1,-1, -1);
        public Vector3 PointerPosition;
        public GameObject mainCamera;
        
        ComputeBuffer computeBuffer;
        ComputeBuffer PointerIntersectionWorldCB;
        ComputeBuffer PointerIntersectionPxCB;
        public bool showColor = true;
        public bool renderHologram = true;
        
        // times
        private ulong lastDepthFrameTime = 0;
        private float lastMeshUpdateTime = 0f;
        private float lastColliderUpdateTime = 0f;
        private float lastTimeRender;
        private float lastTimeGpuRead;


        // mesh parameters
        private bool bMeshInited = false;
        private int meshParamsCache = 0;

        public static Matrix4x4 localToWorldTransform;
        public static Matrix4x4 worldToLocalTransform;

        
        public Texture2D _textureY = null;
        public Texture2D _textureU = null;
        public Texture2D _textureV = null;
        


        
        public RelativeAlignment relativeAlignment;
        public Alignment alignment;
        
        void Start()
        {
            currentDepthArray = new byte[CommonParameters.depthSendingWidth*CommonParameters.depthSendingHeight*2]; 
            PointerArray = new byte[CommonParameters.ColorWidth* CommonParameters.ColorHeight];
            if (GameObject.Find("GestureHandler") != null)
            {
                AnnotationTexture2D = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.RGBA32, false);
                AnnotationArray = new byte[CommonParameters.ColorWidth* CommonParameters.ColorHeight];
                
                PointerTexture = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.R8, false);
            }
            else
            {
                AnnotationTexture2D = Texture2D.blackTexture;
                PointerTexture = Texture2D.blackTexture;
            }

            PointerPosition = new Vector3();

            if (_textureY == null || (_textureY.width != CommonParameters.lumaWidth || _textureY.height != CommonParameters.lumaHeight))
            {
                _textureY = new Texture2D(CommonParameters.lumaWidth, CommonParameters.lumaHeight, TextureFormat.R8, mipChain: false);
            }

            if (_textureU == null || (_textureU.width != CommonParameters.chromaWidth || _textureU.height != CommonParameters.chromaHeight))
            {
                _textureU = new Texture2D(CommonParameters.chromaWidth, CommonParameters.chromaHeight, TextureFormat.R8, mipChain: false);
            }
            if (_textureV == null || (_textureV.width != CommonParameters.chromaWidth || _textureV.height != CommonParameters.chromaHeight))
            {
                _textureV = new Texture2D(CommonParameters.chromaWidth, CommonParameters.chromaHeight, TextureFormat.R8, mipChain: false);
            }

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
            if (renderHologram && meshNumber < CommonParameters.numberOfCameras)
            {
                if (mesh == null)
                {
                    // init mesh and its related data
                    InitMesh();
                }

                if (bMeshInited )
                {
                    if (relativeAlignment != null && relativeAlignment.cameraTransformation.cameraTransformation.Count == CommonParameters.numberOfCameras ||
                      alignment != null && alignment.cameraTransformation.cameraTransformation.Count == CommonParameters.numberOfCameras )
                    {
                        positionHologram();
                    }

                    localToWorldTransform = transform.localToWorldMatrix;
                    worldToLocalTransform = transform.worldToLocalMatrix;

                    UpdateMesh();

                }
            }
        }

        public void rePositionHologram()
        {

                disableHologram();
                positionHologram();
                enableHologram();
          
        }

        
        public void positionHologram()
        {            
            if (CommonParameters.numberOfCameras > 0)
            {
                if (relativeAlignment != null) // Remote
                {
                    
                    if (meshNumber == 1)
                    {
                        transform.rotation = IncomingFrameHandler.hologramTransform.rotation *
                                             Quaternion.Inverse(relativeAlignment.cameraTransformation
                                                 .cameraTransformation[meshNumber].quaternion) * camera2Correction.rotation;
                        transform.position = IncomingFrameHandler.hologramTransform.position +
                                             IncomingFrameHandler.hologramTransform.rotation *
                                             Quaternion.Inverse(relativeAlignment.cameraTransformation.cameraTransformation[meshNumber].quaternion) *
                                             (-relativeAlignment.cameraTransformation.cameraTransformation[meshNumber].translation + 
                                              camera2Correction.rotation * camera2Correction.position);
                    }
                    else
                    {
                        transform.rotation =  IncomingFrameHandler.hologramTransform.rotation * 
                                              Quaternion.Inverse(relativeAlignment.cameraTransformation.cameraTransformation[meshNumber].quaternion);
                        transform.position = IncomingFrameHandler.hologramTransform.position  +
                                                 IncomingFrameHandler.hologramTransform.rotation *
                                                 Quaternion.Inverse(relativeAlignment.cameraTransformation.cameraTransformation[meshNumber].quaternion) *
                                                 (-relativeAlignment.cameraTransformation.cameraTransformation[meshNumber].translation);
                        
                    }



 }
                else  // Local
                {
                    transform.rotation = Quaternion.Inverse(alignment.cameraTransformation
                        .cameraTransformation[IncomingFrameHandler.packetCameraNumber].quaternion) * finalCorrection.rotation;
                    transform.position =
                        Quaternion.Inverse(alignment.cameraTransformation
                            .cameraTransformation[IncomingFrameHandler.packetCameraNumber].quaternion) *
                        (-alignment.cameraTransformation.cameraTransformation[IncomingFrameHandler.packetCameraNumber]
                            .translation+ finalCorrection.rotation * finalCorrection.position) ;
                    
                }  
            }
        }
        
        // inits the mesh and related data
        private void InitMesh()
        {
            
            // create mesh
            mesh = new Mesh
            {
                name = "Hologram",
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
            
            //color and depth Texture2Ds
            incomingColorTextureY     = CreateRenderTexture(incomingColorTextureY, CommonParameters.ColorWidth, CommonParameters.ColorHeight, RenderTextureFormat.R8);  // TODO maybe use RenderTexture
            incomingColorTextureU     = CreateRenderTexture(incomingColorTextureU, CommonParameters.ColorWidth, CommonParameters.ColorHeight, RenderTextureFormat.R8);  // TODO maybe use RenderTexture
            incomingColorTextureV     = CreateRenderTexture(incomingColorTextureV, CommonParameters.ColorWidth, CommonParameters.ColorHeight, RenderTextureFormat.R8);  // TODO maybe use RenderTexture
            
            AnnotationTexture2D = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.R8, false);
            
            
            currentDepthComputeBuffer = CreateComputeBuffer(
                currentDepthComputeBuffer, 
                CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight/2, 
                sizeof(uint));
            PointerIntersectionWorldCB = new ComputeBuffer(3, sizeof(float), ComputeBufferType.Default);
            PointerIntersectionPxCB = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Default);  //.Default);
            
            
            // create mesh vertices & indices
            CreateMeshVertInd();
            bMeshInited = true;
        }
        
        // creates the mesh vertices and indices
        private void CreateMeshVertInd()
        {
            int xVerts = (CommonParameters.depthSendingWidth / coarseFactor);
            int yVerts = (CommonParameters.depthSendingHeight / coarseFactor);
            int vCount = xVerts * yVerts;

            // mesh vertices
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] meshVertices = new Vector3[xVerts * yVerts];
            Vector3[] meshNormals = new Vector3[xVerts * yVerts];
            Vector2[] meshUv = new Vector2[xVerts * yVerts];

            float vsx = (float)coarseFactor / (float)CommonParameters.depthSendingWidth; // + 1;
            float vsy = (float)coarseFactor / (float)CommonParameters.depthSendingHeight; // + 1;

            for (int y = 0, vi = 0; y < yVerts; y++)
            {
                for (int x = 0; x < xVerts; x++, vi++)
                {
                    meshVertices[vi] = new Vector3(x * vsx, y * vsy, 0f);
                    meshNormals[vi] = new Vector3(0f, 1f, 0f);  // 0f, 0f, -1f
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
                        meshIndices[ii++] = (y + 1) * xVerts + x;
                        meshIndices[ii++] = y * xVerts + x + 1;
                        meshIndices[ii++] = y * xVerts + x;

                        meshIndices[ii++] = (y + 1) * xVerts + x + 1;
                        meshIndices[ii++] = y * xVerts + x + 1;
                        meshIndices[ii++] = (y + 1) * xVerts + x;
                    }
                }

                mesh.SetIndices(meshIndices, MeshTopology.Triangles, 0);
            

            meshParamsCache = coarseFactor + (false ? 10 : 0);
        }
        
        // releases mesh-related resources
        private void FinishMesh()
        {
            if(currentDepthComputeBuffer != null)
            {
                currentDepthComputeBuffer.Release();
                currentDepthComputeBuffer.Dispose();
                currentDepthComputeBuffer = null;
            }
            
            if(PointerIntersectionPxCB != null)
            {
                PointerIntersectionPxCB.Release();
                PointerIntersectionPxCB.Dispose();
                PointerIntersectionPxCB = null;
            }

            if (PointerIntersectionWorldCB != null)
            {
                PointerIntersectionWorldCB.Release();
                PointerIntersectionWorldCB = null;
            }
            

            bMeshInited = false;
        }

        private float mLastSample = 0;
        private float _Fps = 20;

        // updates the mesh according to current depth frame
        private void UpdateMesh()
        {
            mLastSample += Time.deltaTime;
            
            if (mLastSample >= 1.0f / _Fps && bMeshInited && meshShaderMat != null && renderHologram)
            {
                int paramsCache = coarseFactor + (false ? 10 : 0);
                if (meshParamsCache != paramsCache)
                {
                    CreateMeshVertInd();
                }

                
                currentDepthComputeBuffer.SetData(currentDepthArray);
                //print("Render: " + meshNumber);
                meshShaderMat.SetBuffer("_DepthMap", currentDepthComputeBuffer);
                meshShaderMat.SetTexture("_ColorTexY", _textureY);
                meshShaderMat.SetTexture("_ColorTexU", _textureU);
                meshShaderMat.SetTexture("_ColorTexV", _textureV);
                
                meshShaderMat.SetInt("_showColor", Convert.ToInt32(showColor));
                meshShaderMat.SetFloat("_MaxEdgeLen", IncomingFrameHandler.maxEdgeLength);
                meshShaderMat.SetInt("_stableTriangles", stableTriangles);
                meshShaderMat.SetInt("_colorAlpha", colorAlpha);
                meshShaderMat.SetInt("_moveEdges", moveEdges);
                meshShaderMat.SetInt("_colorEdges", colorEdges);
                
                meshShaderMat.SetFloatArray("_ColorExtrinsicsR", CommonParameters.colorExtrinsicsR);
                meshShaderMat.SetVector("_ColorExtrinsicsT", CommonParameters.colorExtrinsicsT);
                meshShaderMat.SetVector("_colorIntrinsicsCxy", CommonParameters.colorIntrinsicsCxy);
                meshShaderMat.SetVector("_colorIntrinsicsFxy", CommonParameters.colorIntrinsicsFxy);
                meshShaderMat.SetFloatArray("_ColorDistortionRadial", CommonParameters.colorDistortionRadial);
                meshShaderMat.SetVector("_ColorDistortionTangential", CommonParameters.colorDistortionTangential);
    
                meshShaderMat.SetFloatArray("_DepthExtrinsicsR", CommonParameters.depthExtrinsicsR);
                meshShaderMat.SetVector("_DepthExtrinsicsT", CommonParameters.depthExtrinsicsT);
                meshShaderMat.SetVector("_depthIntrinsicsCxy", CommonParameters.depthIntrinsicsCxy);
                meshShaderMat.SetVector("_depthIntrinsicsFxy", CommonParameters.depthIntrinsicsFxy);
                meshShaderMat.SetFloatArray("_DepthDistortionRadial", CommonParameters.depthDistortionRadial);
                meshShaderMat.SetVector("_DepthDistortionTangential", CommonParameters.depthDistortionTangential);

                if (AnnotationArray != null)
                {
                    AnnotationTexture2D.LoadRawTextureData(AnnotationArray);
                    AnnotationTexture2D.Apply();
                }

                meshShaderMat.SetTexture("_AnnotationTex", AnnotationTexture2D);
                meshShaderMat.SetVector("_PointerPosition", PointerPosition);
                meshShaderMat.SetVector("_CameraPosition", mainCamera.transform.position);
                if (PointerArray != null)
                {
                    PointerTexture.LoadRawTextureData(PointerArray);
                    PointerTexture.Apply();
                }

                meshShaderMat.SetTexture("_PointerTex", PointerTexture);
                
                // other parameters update on GPU
                meshShaderMat.SetVector("_SpaceScale",
                    new Vector3(1, 1, 1)); 
                meshShaderMat.SetVector("_ColorTexRes",
                    new Vector2(CommonParameters.ColorWidth, CommonParameters.ColorHeight)); // currently not used
                meshShaderMat.SetVector("_DepthTexRes", new Vector2(CommonParameters.depthSendingWidth, CommonParameters.depthSendingHeight));
                meshShaderMat.SetInt("_CoarseFactor", coarseFactor);
                meshShaderMat.SetInt("_IsPointCloud", false ? 1 : 0);
                
                meshShaderMat.SetMatrix("_LocalToWorldTransform", localToWorldTransform); 
                meshShaderMat.SetMatrix("_WorldToLocalTransform", worldToLocalTransform); 
                meshShaderMat.SetVector("_PosMin", new Vector3(xMin, yMin, zMin));
                meshShaderMat.SetVector("_PosMax", new Vector3(xMax, yMax, zMax));
                meshShaderMat.SetFloat("_DepthFactor", depthFactor);
                
                // mesh bounds, clipping
                Vector3 boundsCenter = new Vector3(0, 0, (zMax - zMin) / 2f);
                Vector3 boundsSize = new Vector3((xMax - xMin), (yMax - yMin), (zMax - zMin)); 
                mesh.bounds = new Bounds(boundsCenter, boundsSize);
                
            }
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
        
        private int maxXYDistance = 6;
        public void updateXmin(SliderEventData data)
        {
            xMin = data.NewValue * (maxXYDistance/2) - (maxXYDistance/2);
        }
        
        public void updateXmax(SliderEventData data)
        {
            xMax = data.NewValue * (maxXYDistance/2);
        }
        
        public void updateYmin(SliderEventData data)
        {
            yMin =  data.NewValue * (maxXYDistance/2) - (maxXYDistance/2);
        }
        
        public void updateYmax(SliderEventData data)
        {
            yMax = data.NewValue * (maxXYDistance/2);
        }

        


        public void toggleHologram()
        {
            if (!renderHologram)
            {
                enableHologram();
            }
            else
            {
                disableHologram();
            }
        }

        public void enableHologram()
        {
            renderHologram = true;
            print("Hologram on");
        }

        public void disableHologram()
        {
            renderHologram = false;
            FinishMesh();
            mesh = null;
            print("Hologram off");
        }
        
        public void toggleShowColor()
        {
            if (!showColor)
            {
                showColor = true;
                print("Color on");
            }
            else
            {
                showColor = false;
                print("Color off");
            }
        }

       public void toggleStableTriangles()
        {
            if (stableTriangles == 0)
            {
                stableTriangles = 1;
                print("StabTri on");
            }
            else 
            {
                stableTriangles = 0;
                print("StabTri off");
            }
        }

        public void toggleColorAlpha() 
        {
            if (colorAlpha == 0)
            {
                colorAlpha = 1;
                print("colorAlpha on");
            }
            else
            {
                colorAlpha = 0;
                print("colorAlpha off");
            }
        }

        public void toggleMoveEdges() 
        {
            if (moveEdges == 0)
            {
                moveEdges = 1;
                print("moveEdges on");
            }
            else 
            {
                moveEdges = 0;
                print("moveEdges off");
            }
        }
    }

}

