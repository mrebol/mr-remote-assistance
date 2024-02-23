using System.Collections;
using System.Collections.Generic;
using Telepresence;
using UnityEngine;

public class YuvVideoRenderer : MonoBehaviour
{
    private Mesh mesh = null;
    private Material meshShaderMat = null;

    //transform of the screen's control cube
    //starting position of control cube
    public Vector3 stStart;
    //offset of cube from screen
    private Vector3 stOffset;

    public Transform setup;

    public int cameraFlip = -1;

    public IncomingFrameHandler IncomingFrameHandler;
    // mesh parameters
    private bool bMeshInited = false;
    
    public Texture2D _textureY = null;
    public Texture2D _textureU = null;
    public Texture2D _textureV = null;
    public byte[] array;
    public byte[] arrayBlack;

    private float mLastSample = 0;
    public int _Fps = 20;

    // Start is called before the first frame update
    void Start()
    {
        _textureY = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.R8, false);
        _textureU = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.R8, false); 
        _textureV = new Texture2D(CommonParameters.ColorWidth, CommonParameters.ColorHeight, TextureFormat.R8, false);
        
        setup = new GameObject().transform;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (mesh == null)
        {
            // init mesh and its related data
            InitMesh();
        }
        UpdateMesh();
        
    }
    
    private void InitMesh()
    {
        // create mesh
        mesh = new Mesh
        {
            name = "Screen",
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

        // create mesh vertices & indices
        CreateMeshVertInd();
        bMeshInited = true;
    }

    private void UpdateMesh()
    {
        mLastSample += Time.deltaTime;
        if (mLastSample >= 1.0f / _Fps && bMeshInited && meshShaderMat != null)
        {
            meshShaderMat.SetTexture("_YPlane", _textureY);
            meshShaderMat.SetTexture("_UPlane", _textureU);
            meshShaderMat.SetTexture("_VPlane", _textureV);
            
        }
    }
    
     private void CreateMeshVertInd()
    {
        int xVerts = 2;
        int yVerts = 2; 

        // mesh vertices
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Vector3[] meshVertices = new Vector3[xVerts * yVerts];
        Vector3[] meshNormals = new Vector3[xVerts * yVerts]; 
        Vector2[] meshUv = new Vector2[xVerts * yVerts];

        float vsx = 1.0f / (xVerts-1);
        float vsy = 1.0f / (yVerts-1);

        for (int y = 0, vi = 0; y < yVerts; y++)
        {
            for (int x = 0; x < xVerts; x++, vi++)
            {
                meshVertices[vi] = new Vector3(x * vsx, y * vsy, 0f);
                meshNormals[vi] = new Vector3(0f, 1f, 0f);  // 0f, 0f, -1f
                meshUv[vi] = new Vector2(x * vsx, y * vsy);
            }
        }
        
        // mesh indices
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
        
        mesh.vertices = meshVertices;
        mesh.normals = meshNormals;
        mesh.uv = meshUv;

        mesh.SetIndices(meshIndices, MeshTopology.Triangles, 0);
    }
     
     public void positionDisplay(Transform mainCamera)
     {
         setup.rotation = Quaternion.Euler(0, mainCamera.rotation.eulerAngles.y - 20, 0);
         setup.position = mainCamera.position + setup.rotation * new Vector3(-2.0f, -0.7f, 3); // right, up, back' x=-1.4f close to Holo

     }
}
