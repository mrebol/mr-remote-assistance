using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEditor;
//using UnityEditor.Scripting.Python;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using NumSharp;
using Accord.Math.Decompositions;
using Accord.Math;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Diagnostics;
using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using NumSharp.Generic;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

public class Alignment : MonoBehaviour
{

   
    public CameraTransformations cameraTransformation = new CameraTransformations();
    public Camera mainCamera;
    public Vector3 camera0Vector = Vector3.forward;
    public byte selectCamera = 0;
    public byte currentCamera = 0;
    public Text debugText3;
    public List<List<Vector3>> cameraPoints = new List<List<Vector3>>();
    public PeerConnection peerConnection;
    public PlaceBall local;
    public Transform videoScreen;
    public Transform videoScreenTransform;
    
    public Transform usScreen;
    public Transform usScreenTransform;
    
    private bool runScript = false;
    private bool readScript = false;
    private float startTime;
    private Process process;
    
    public RectTransform canvas;
    public RectTransform canvasControls;

    public GameObject balls;

    private Vector3 localBallCenter;

    private Vector3 blue2yellowRay = Vector3.forward;
    private Vector3 green2redRay = Vector3.forward;
    
    private float lastTimeRead;
    void Start()
    {
        videoScreenTransform.rotation = Quaternion.identity;
        usScreenTransform.rotation = Quaternion.identity;
        videoScreenTransform.localScale = videoScreen.localScale;
        
        for (int j = 0; j < Math.Max(CommonParameters.numberOfCameras, (byte)1); j++)
        {
            cameraPoints.Add(new List<Vector3>());
            for (int i = 0; i < CommonParameters.numberOfBalls; i++)
            {
                cameraPoints[j].Add(new Vector3(0, 0, 0));
            }
            cameraTransformation.cameraTransformation.Add(new MyTransformation());
            cameraTransformation.cameraTransformation[j].quaternion = Quaternion.identity;
            cameraTransformation.cameraTransformation[j].translation = Vector3.zero;
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (CommonParameters.numberOfCameras > 1)
        {
            var camera0Forward =
                Vector3.Normalize(Quaternion.Inverse(cameraTransformation.cameraTransformation[0].quaternion) *
                                  camera0Vector);
            var camera1Forward =
                Vector3.Normalize(Quaternion.Inverse(cameraTransformation.cameraTransformation[1].quaternion) *
                                  camera0Vector);
            if (Vector3.Dot(camera0Forward, mainCamera.transform.forward) >
                Vector3.Dot(camera1Forward, mainCamera.transform.forward))
            {
                selectCamera = 0;
            }
            else
            {
                selectCamera = 1;
            }

            if (currentCamera != selectCamera)
            {
                debugText3.text = "Select Camera: " + selectCamera;
                currentCamera = selectCamera;
            }
        }
        
        
        if (Time.time > (lastTimeRead + (1.0 * 2))) // FPS
        {
            lastTimeRead = Time.time;
            videoScreen.Rotate(0,0,-videoScreen.rotation.eulerAngles.z);
            usScreen.Rotate(0,0,-usScreen.rotation.eulerAngles.z);
        }
    }

    public void resetScreenPosition()
    {
        videoScreen.forward = blue2yellowRay;
        videoScreen.position = localBallCenter + new Vector3(0.0f, 0.3f, 0.0f)  +
             blue2yellowRay*0.4f + green2redRay *0.0f;
        


        usScreen.forward = blue2yellowRay;
        usScreen.position = localBallCenter + new Vector3(0.0f, 0.1f, 0.0f)  +
                             blue2yellowRay*0.2f + green2redRay *0.0f;


}

public void AlignHologram()
{
SystemPoints systemPoints = new SystemPoints();
for (int i = 0; i < CommonParameters.numberOfBalls; i++)
{
    systemPoints.holoPoints.Add(local.balls[i].position);
}

for (int j = 0; j < CommonParameters.numberOfCameras; j++)
{
    systemPoints.cameraPoints.Add(new CameraPoints());
    for (int i = 0; i < CommonParameters.numberOfBalls; i++)
    {
        systemPoints.cameraPoints[j].points.Add(this.cameraPoints[j][i]);
    }
}
rigid_transform_3D();



// Position and Rotation Video Screen:
var A = np.zeros(3, CommonParameters.numberOfBalls);
for (int i = 0; i < CommonParameters.numberOfBalls; i++)
{
    A[0, i] = local.balls[i].position.x;
    A[1, i] = local.balls[i].position.y;
    A[2, i] = local.balls[i].position.z;
}
var centroid_A = np.mean(A, 1);
localBallCenter = new Vector3((float) centroid_A[0].GetDouble(),
    (float) centroid_A[1].GetDouble(),
    (float) centroid_A[2].GetDouble());

blue2yellowRay = Vector3.Normalize(local.balls[3].position - local.balls[2].position);
green2redRay = Vector3.Normalize(local.balls[0].position - local.balls[1].position);

}


public void rigid_transform_3D()
{

var A = np.zeros(3, CommonParameters.numberOfBalls);
var B = np.zeros(CommonParameters.numberOfCameras, 3, CommonParameters.numberOfBalls);
for (int i = 0; i < CommonParameters.numberOfBalls; i++)
{
    A[0, i] = local.balls[i].position.x;
    A[1, i] = local.balls[i].position.y;
    A[2, i] = local.balls[i].position.z;
}

for (int j = 0; j < CommonParameters.numberOfCameras; j++)
{
    for (int i = 0; i < CommonParameters.numberOfBalls; i++)
    {
        B[j, 0, i] = cameraPoints[j][i].x;
        B[j, 1, i] = cameraPoints[j][i].y;
        B[j, 2, i] = cameraPoints[j][i].z;
   }
}

for (int i = 0; i < CommonParameters.numberOfCameras; i++)
{
    var B_cam = B[i];

    //# find mean column wise
    var centroid_A = np.mean(A, 1);
    var centroid_B = np.mean(B_cam, 1);

    //# ensure centroids are 3x1
    var centroid_A_ = np.reshape(centroid_A, new Shape(-1, 1));
    var centroid_B_ = centroid_B.reshape(-1, 1);

    //# subtract mean
    var Am = A - centroid_A_;
    var Bm = B_cam - centroid_B_;

    var H = np.matmul(Am, np.transpose(Bm));

    var matrix = new double[3, 3]
    {
        {(double) H[0, 0], H[0, 1], H[0, 2]},
        {H[1, 0], H[1, 1], H[1, 2]},
        {H[2, 0], H[2, 1], H[2, 2]},

    };
    var USVt = H.svd(); // NOT WORKING
    var svd = new SingularValueDecomposition(matrix);

    var m = DenseMatrix.OfArray(new double[3, 3]
    {
        {(double) H[0, 0], H[0, 1], H[0, 2]},
        {H[1, 0], H[1, 1], H[1, 2]},
        {H[2, 0], H[2, 1], H[2, 2]},

    });
    
    var svd2 = m.Svd(true);


    var u = svd2.U;

    var s = svd.DiagonalMatrix;

    var vt = svd2.VT;
    var U = np.array(u.ToArray());
    var Vt = np.array(vt.ToArray());

    var R = np.matmul(Vt.T, U.T);

    var r = vt.Transpose().Multiply(u.Transpose());
    if (r.Determinant() < 0)
    {
        print("det(R) < R, reflection detected!, correcting for it ...");
        Vt["2, :"] *= -1;
        R = np.matmul(Vt.T, U.T);
    }
    
    var t = np.matmul((-1) * R, centroid_A_) + centroid_B_;


    var translation = new Vector3();
    translation.x = (float) t[0, 0].GetDouble();
    translation.y = (float) t[0, 1].GetDouble();
    translation.z = (float) t[0, 2].GetDouble();
    cameraTransformation.cameraTransformation[i].translation = translation;
    cameraTransformation.cameraTransformation[i].quaternion = mToQ(R); 
}
}

public Quaternion mToQ(NDArray m)
{
double tr = m[0,0].GetDouble() + m[1,1].GetDouble() + m[2,2].GetDouble();
double qw, qx, qy, qz;
if (tr > 0) { 
    double S = Math.Sqrt(tr+1.0) * 2; // S=4*qw 
    qw = 0.25 * S;
    qx = (m[2,1].GetDouble() - m[1,2].GetDouble()) / S;
    qy = (m[0,2].GetDouble() - m[2,0].GetDouble()) / S; 
    qz = (m[1,0].GetDouble() - m[0,1].GetDouble()) / S; 
} else if ((m[0,0].GetDouble() > m[1,1].GetDouble()) && (m[0,0].GetDouble() > m[2,2].GetDouble())) { 
    double S = Math.Sqrt(1.0 + m[0,0].GetDouble() - m[1,1].GetDouble() - m[2,2].GetDouble()) * 2; // S=4*qx 
    qw = (m[2,1].GetDouble() - m[1,2].GetDouble()) / S;
    qx = 0.25 * S;
    qy = (m[0,1].GetDouble() + m[1,0].GetDouble()) / S; 
    qz = (m[0,2].GetDouble() + m[2,0].GetDouble()) / S; 
} else if (m[1,1].GetDouble() > m[2,2].GetDouble()) { 
    double S = Math.Sqrt(1.0f + m[1,1].GetDouble() - m[0,0].GetDouble() - m[2,2].GetDouble()) * 2; // S=4*qy
    qw = (m[0,2].GetDouble() - m[2,0].GetDouble()) / S;
    qx = (m[0,1].GetDouble() + m[1,0].GetDouble()) / S; 
    qy = 0.25 * S;
    qz = (m[1,2].GetDouble() + m[2,1].GetDouble()) / S; 
} else { 
    double S = Math.Sqrt(1.0 + m[2,2].GetDouble() - m[0,0].GetDouble() - m[1,1].GetDouble()) * 2; // S=4*qz
    qw = (m[1,0].GetDouble() - m[0,1].GetDouble()) / S;
    qx = (m[0,2].GetDouble() + m[2,0].GetDouble()) / S;
    qy = (m[1,2].GetDouble() + m[2,1].GetDouble()) / S;
    qz = 0.25 * S;
}

return new Quaternion((float)qx, (float)qy, (float)qz, (float)qw);
}

[ContextMenu("Recenter UI")]
public void recenterUI()
{
print("Recenter UI.");
canvas.transform.position = mainCamera.transform.position + 0.5f * mainCamera.transform.forward;
canvas.transform.forward = mainCamera.transform.forward;
}

public void recenterUIDelayed()
{
Invoke("recenterUI",3);
}

[ContextMenu("Toggle Controls")]
public void toggleControls()
{
canvasControls.gameObject.SetActive(!canvasControls.gameObject.activeSelf);
print("Controls. Enabled: " + canvasControls.gameObject.activeSelf);
balls.gameObject.SetActive(!balls.gameObject.activeSelf);
}

[ContextMenu("Toggle Profiler")]
public void ToggleProfiler()
{
print("Toggle Profiler");
CoreServices.DiagnosticsSystem.ShowProfiler = !CoreServices.DiagnosticsSystem.ShowProfiler;
print("Toggle Profiler. Enabled: " + CoreServices.DiagnosticsSystem.ShowProfiler);
}

[ContextMenu("Recenter CS")]
public void recenterCS()
{
print("Recenter CS.");
UnityEngine.XR.InputTracking.Recenter();
}

}
