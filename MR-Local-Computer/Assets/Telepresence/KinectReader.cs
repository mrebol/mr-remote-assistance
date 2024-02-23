using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Azure.Kinect.Sensor;
using UnityEngine;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Unity;
using Telepresence;
using UnityEngine.Assertions;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

public class KinectReader : MonoBehaviour
{
    Device kinect0;
    Device kinect1;
    Device kinect2;
    Transformation transformation;
    private Task t0;
    private Task t1;
    private Task t2;
    public bool runKinect = true;
    public bool prevKinectStopped = false;
    private byte currentKinect = 0;
    [UnityEngine.Range(0, 1)]
    public byte selectKinect = 0;

    public bool swapCameraIds = false;
    public byte kinectCount = 0;
    public PeerConnection remotePeerConnection;
    public List<MeshRender> meshRender;
    [HideInInspector]
    public byte[] colorImage{ get; set; }
    [HideInInspector] public byte[] depthImage { get; set; }
    public byte capturedKinect = 0;

    
    private static Calibration calibration;
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
    public AlignmentBalls alignmentBalls = new AlignmentBalls();

    public byte remoteSelectCamera;

    public bool allowRemoteSelectCamera = true;

    public BallManager ballManager;
    // Start is called before the first frame update
    void Start()
    {
        var loadSuccess = Load();
        
        if (loadSuccess)
        {
            print("Load success.");
            if (alignmentBalls.cameraBalls.Count == CommonParameters.numberOfCameras && CommonParameters.numberOfCameras > 0)
            {
                print("loading saved pointers for camera: " + selectKinect);
                // Load new pointers

                    ballManager.pointer[1].transform.position = new Vector3(
                        alignmentBalls.cameraBalls[selectKinect].RedPosition.x,
                        alignmentBalls.cameraBalls[selectKinect].RedPosition.y,
                        alignmentBalls.cameraBalls[selectKinect].RedPosition.z);
                    ballManager.pointer[2].transform.position = new Vector3(
                        alignmentBalls.cameraBalls[selectKinect].GreenPosition.x,
                        alignmentBalls.cameraBalls[selectKinect].GreenPosition.y,
                        alignmentBalls.cameraBalls[selectKinect].GreenPosition.z);
                    ballManager.pointer[3].transform.position = new Vector3(
                        alignmentBalls.cameraBalls[selectKinect].BluePosition.x,
                        alignmentBalls.cameraBalls[selectKinect].BluePosition.y,
                        alignmentBalls.cameraBalls[selectKinect].BluePosition.z);
                    ballManager.pointer[4].transform.position = new Vector3(
                        alignmentBalls.cameraBalls[selectKinect].YellowPosition.x,
                        alignmentBalls.cameraBalls[selectKinect].YellowPosition.y,
                        alignmentBalls.cameraBalls[selectKinect].YellowPosition.z);
            }
            else
            {
                alignmentBalls.cameraBalls.Clear();
                for (int i=0;i< CommonParameters.numberOfCameras; i++){ 
                    alignmentBalls.cameraBalls.Add(new CameraBalls());
                }
            }
        }
        else
        {
            print("Load error.");
            alignmentBalls.cameraBalls.Clear();
            for (int i=0;i< CommonParameters.numberOfCameras; i++){ 
                alignmentBalls.cameraBalls.Add(new CameraBalls());
            }
        }

        try
        {
            if (CommonParameters.numberOfCameras == 1)
            {
                kinect0 = Device.Open(0);
                kinectCount = 1;
            } else if (CommonParameters.numberOfCameras == 2)
            {
                //Connect with the 0th Kinect
                if (!swapCameraIds)
                {
                    kinect0 = Device.Open(0);
                    kinectCount = 1;
                    kinect1 = Device.Open(1);
                    kinectCount = 2;
                }
                else
                {
                    kinect0 = Device.Open(1);
                    kinectCount = 1;
                    kinect1 = Device.Open(0);
                    kinectCount = 2;
                }
            }

            kinect2 = Device.Open(2);
            kinectCount = 3;
        }
        catch (Exception ex)
        {
        }
        print("Kinect Count: " + kinectCount + " vs " + CommonParameters.numberOfCameras);
        Assert.IsTrue(kinectCount == CommonParameters.numberOfCameras);
        
        DeviceConfiguration deviceConfiguration = new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = (Microsoft.Azure.Kinect.Sensor.ColorResolution) CommonParameters.ColorResolution,
            DepthMode = (Microsoft.Azure.Kinect.Sensor.DepthMode) CommonParameters.depthMode,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        };
        
        //Setting the Kinect operation mode and starting it
        if (CommonParameters.numberOfCameras == 0)
            return;
        
        kinect0.StartCameras(deviceConfiguration);
        if (kinect1 != null)
        {
            kinect1.StartCameras(deviceConfiguration);
        }

        if (kinect2 != null) {
            kinect2.StartCameras(deviceConfiguration);
        }


        //Access to coordinate transformation information
        transformation = kinect0.GetCalibration().CreateTransformation();

        if (CommonParameters.COLOR_SDK_REGISTERED)
        {
            colorImage = new byte[CommonParameters.COLOR_REGISTERED_WIDTH * CommonParameters.COLOR_REGISTERED_HEIGHT * 4];
        }
        else
        {
            colorImage = new byte[CommonParameters.ColorHeight * CommonParameters.ColorWidth * 4];
        }

        depthImage = new byte[CommonParameters.depthSendingWidth * CommonParameters.depthSendingHeight * 2];
        
        
        calibration = kinect0.GetCalibration(deviceConfiguration.DepthMode, deviceConfiguration.ColorResolution);
        CameraCalibration colorCameraCalibration = calibration.ColorCameraCalibration;

        
        colorExtrinsicsR = new float[] { // row by row
                calibration.ColorCameraCalibration.Extrinsics.Rotation[0], 
                calibration.ColorCameraCalibration.Extrinsics.Rotation[1], 
                calibration.ColorCameraCalibration.Extrinsics.Rotation[2]
                ,
                calibration.ColorCameraCalibration.Extrinsics.Rotation[3], 
                calibration.ColorCameraCalibration.Extrinsics.Rotation[4], 
                calibration.ColorCameraCalibration.Extrinsics.Rotation[5]
                ,
                calibration.ColorCameraCalibration.Extrinsics.Rotation[6], 
                calibration.ColorCameraCalibration.Extrinsics.Rotation[7], 
                calibration.ColorCameraCalibration.Extrinsics.Rotation[8] 
                ,
        };
       
        colorExtrinsicsT = new Vector3(calibration.ColorCameraCalibration.Extrinsics.Translation[0], calibration.ColorCameraCalibration.Extrinsics.Translation[1],
          calibration.ColorCameraCalibration.Extrinsics.Translation[2] );
        
        
      colorIntrinsicsCxy = new Vector2(calibration.ColorCameraCalibration.Intrinsics.Parameters[0],
          calibration.ColorCameraCalibration.Intrinsics.Parameters[1]);
      colorIntrinsicsFxy = new Vector2(calibration.ColorCameraCalibration.Intrinsics.Parameters[2],
          calibration.ColorCameraCalibration.Intrinsics.Parameters[3]); 
      colorDistortionRadial = new[]
        {
            calibration.ColorCameraCalibration.Intrinsics.Parameters[4],
            calibration.ColorCameraCalibration.Intrinsics.Parameters[5],
            calibration.ColorCameraCalibration.Intrinsics.Parameters[6],
            calibration.ColorCameraCalibration.Intrinsics.Parameters[7],
            calibration.ColorCameraCalibration.Intrinsics.Parameters[8],
            calibration.ColorCameraCalibration.Intrinsics.Parameters[9],
            
        };
        colorDistortionTangential = new Vector2(
            calibration.ColorCameraCalibration.Intrinsics.Parameters[13],
            calibration.ColorCameraCalibration.Intrinsics.Parameters[12]
        );
        
        depthExtrinsicsR = new float[] 
        {
            calibration.DepthCameraCalibration.Extrinsics.Rotation[0], 
                calibration.DepthCameraCalibration.Extrinsics.Rotation[1], 
                calibration.DepthCameraCalibration.Extrinsics.Rotation[2],
            calibration.DepthCameraCalibration.Extrinsics.Rotation[3], 
                calibration.DepthCameraCalibration.Extrinsics.Rotation[4], 
                calibration.DepthCameraCalibration.Extrinsics.Rotation[5],
            calibration.DepthCameraCalibration.Extrinsics.Rotation[6], 
                calibration.DepthCameraCalibration.Extrinsics.Rotation[7], 
                calibration.DepthCameraCalibration.Extrinsics.Rotation[8]
        };
        depthExtrinsicsT = new Vector3(calibration.DepthCameraCalibration.Extrinsics.Translation[0], calibration.DepthCameraCalibration.Extrinsics.Translation[1],
            calibration.DepthCameraCalibration.Extrinsics.Translation[2]);
        depthIntrinsicsCxy = new Vector2(calibration.DepthCameraCalibration.Intrinsics.Parameters[0], 
        calibration.DepthCameraCalibration.Intrinsics.Parameters[1]);
        depthIntrinsicsFxy = new Vector2(calibration.DepthCameraCalibration.Intrinsics.Parameters[2],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[3]);
        depthDistortionRadial = new[]
        {
            calibration.DepthCameraCalibration.Intrinsics.Parameters[4],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[5],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[6],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[7],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[8],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[9],
            
        };
        depthDistortionTangential = new Vector2(
            calibration.DepthCameraCalibration.Intrinsics.Parameters[13],
            calibration.DepthCameraCalibration.Intrinsics.Parameters[12]
        );
        Matrix4x4 colorExtrinsicsTest = new Matrix4x4(
            new Vector4( 0.999975f, 0.00604121f, -0.003686489f,-31.99785f), 
            new Vector4(-0.005590246f, 0.9936896f, 0.1120258f,  -1.716765f),
            new Vector4(0.004339997f, -0.1120024f, 0.9936985f,3.964732f),
            new Vector4(0f,0f,0f,1f)).transpose;
        
        
        Vector4 vPos = new Vector4(0.5125f, 0.5138889f, 0.872f, 1f);
        
        
        //Loop to get data from Kinect and rendering
        switch (selectKinect)
        {
            case 0 : t0 = KinectLoop(kinect0, selectKinect);
                break;
            case 1:
                if (kinect1 != null)
                {
                    t1 = KinectLoop(kinect1, selectKinect);
                } break;
            case 2:
                if (kinect2 != null)
                {
                    t2 = KinectLoop(kinect2, selectKinect);
                } break;
        }



        currentKinect = selectKinect;
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!prevKinectStopped)
        {
            if (CommonParameters.useWebRtcDataChannel)
            {
                remoteSelectCamera = remotePeerConnection.selectCamera;
            }
            else
            {
                remoteSelectCamera = WsClient.selectCamera;
            }

            if ((allowRemoteSelectCamera && currentKinect != remoteSelectCamera && remotePeerConnection.Peer != null && 
                 remotePeerConnection.Peer.IsConnected) || currentKinect != selectKinect )
            {
                if (currentKinect != selectKinect) // 
                {
                }
                else if(allowRemoteSelectCamera)
                {
                    selectKinect = remoteSelectCamera;
                }

                if (selectKinect == 2 && kinect2 == null)
                    return;
                if (selectKinect == 1 && kinect1 == null)
                    return;
                runKinect = false;

                // Save current pointers
                for (int i = 0; i < 3; i++)
                {
                    alignmentBalls.cameraBalls[currentKinect].RedPosition[i] = ballManager.pointer[1].transform.position[i];
                    alignmentBalls.cameraBalls[currentKinect].GreenPosition[i] = ballManager.pointer[2].transform.position[i];
                    alignmentBalls.cameraBalls[currentKinect].BluePosition[i] = ballManager.pointer[3].transform.position[i];
                    alignmentBalls.cameraBalls[currentKinect].YellowPosition[i] = ballManager.pointer[4].transform.position[i];
                }

                // Load new pointers
                
                    ballManager.pointer[1].transform.position = new Vector3(alignmentBalls.cameraBalls[selectKinect].RedPosition.x,
                        alignmentBalls.cameraBalls[selectKinect].RedPosition.y,
                        alignmentBalls.cameraBalls[selectKinect].RedPosition.z);
                    ballManager.pointer[2].transform.position = new Vector3(alignmentBalls.cameraBalls[selectKinect].GreenPosition.x,
                        alignmentBalls.cameraBalls[selectKinect].GreenPosition.y,
                        alignmentBalls.cameraBalls[selectKinect].GreenPosition.z);
                    ballManager.pointer[3].transform.position = new Vector3(alignmentBalls.cameraBalls[selectKinect].BluePosition.x,
                        alignmentBalls.cameraBalls[selectKinect].BluePosition.y,
                        alignmentBalls.cameraBalls[selectKinect].BluePosition.z);
                    ballManager.pointer[4].transform.position = new Vector3(alignmentBalls.cameraBalls[selectKinect].YellowPosition.x,
                        alignmentBalls.cameraBalls[selectKinect].YellowPosition.y,
                        alignmentBalls.cameraBalls[selectKinect].YellowPosition.z);
                    
                currentKinect = selectKinect;
            }

        }
        else
        {
            runKinect = true;
            switch (selectKinect)
            {
                case 0: t0 = KinectLoop(kinect0, selectKinect); break;
                case 1: t1 = KinectLoop(kinect1, selectKinect); break;
                case 2: t2 = KinectLoop(kinect2, selectKinect); break;
            }
            
            prevKinectStopped = false;

        }
    }
    
     private async Task KinectLoop(Device device, byte selectKinect)
     {
        while (runKinect)
        {
            using (Capture capture = await Task.Run(() => device.GetCapture()).ConfigureAwait(true))
            {
                //Getting color information
                Image color;
                if (CommonParameters.COLOR_SDK_REGISTERED)
                {
                    color = transformation.ColorImageToDepthCamera(capture);
                }
                else
                {
                    color = capture.Color;
                    
                }
                color.Memory.CopyTo(colorImage);
                
                //Getting vertices of point cloud
                Image depth = capture.Depth;
                depth.Memory.CopyTo(depthImage);
                capturedKinect = currentKinect;
            }
        }
        prevKinectStopped = true;
    }

     private void OnDisable()
     {
         Save();
     }

     //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        runKinect = false;
        
        kinect0.StopCameras();
        kinect0.Dispose();

        if (kinect1 != null)
        {
            kinect1.StopCameras();
            kinect1.Dispose();
        }

        if (kinect2 != null)
        {
            kinect2.StopCameras();
            kinect2.Dispose();
        }
    }
    
 
        
             
        //it's static so we can call it from anywhere
        public void Save() {

            string jsonString = JsonUtility.ToJson(alignmentBalls);
            print("Saving JSON balls: " + jsonString);
            System.IO.File.WriteAllText(Application.persistentDataPath + "/savedGames.gd", jsonString);
        }   
     
        public bool Load()
        {
            if (File.Exists(Application.persistentDataPath + "/savedGames.gd"))
            {
               
                string fileContents = File.ReadAllText(Application.persistentDataPath + "/savedGames.gd");
                alignmentBalls = JsonUtility.FromJson<AlignmentBalls>(fileContents);

                print("Loading JSON balls:" + fileContents);
                return true;
            }
            else
            {
                return false;
            }

        }
}
