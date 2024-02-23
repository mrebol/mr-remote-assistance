using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Telepresence;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

[System.Serializable]
public class CameraBalls
{
    [SerializeField]
    public Vector3 RedPosition;
    [SerializeField]
    public Vector3 GreenPosition;
    [SerializeField]
    public Vector3 BluePosition;
    [SerializeField]
    public Vector3 YellowPosition;

}
[Serializable]
public class AlignmentBalls
{
    public List<CameraBalls> cameraBalls = new List<CameraBalls>();
}

[Serializable]
public class CameraTransformations
{
    public List<MyTransformation> cameraTransformation = new List<MyTransformation>(CommonParameters.numberOfCameras);
}
[Serializable]
public class MyTransformation
{
    public Vector3 translation;
    public Quaternion quaternion;
}

public class BallManager : MonoBehaviour
{
    public KinectReader KinectReader;
    public List<Pointer> pointer;

    public int currentBall;
    
    private bool runScript = false;
    private bool readScript = false;
    private float startTime;
    private Process process;
    public CameraTransformations cameraTransformation = new CameraTransformations();

    
    // Start is called before the first frame update
    void Start()
    {
        currentBall = 0;
        for (int j = 0; j < CommonParameters.numberOfCameras-1; j++)
        {
            var trans = new MyTransformation();
            trans.translation = Vector3.zero;
            trans.quaternion = Quaternion.identity;
            cameraTransformation.cameraTransformation.Add(trans);
        }
        
        startTime = Time.time;
        runScript = true;
        readScript = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("c"))
        {
            pointer[currentBall].pointerActive = false;
            currentBall = (currentBall + 1) % pointer.Count;
            pointer[currentBall].pointerActive = true;
        }
        
        if (runScript && !readScript && Time.time - startTime > 2) // 2 sec
        {
            var tempCameraTransformation = new CameraTransformations();
            string fileContents = "";
            try
            { 
                fileContents = File.ReadAllText(CommonParameters.directory + @"system-data\Transformation-Camera.json");
            }
            catch (Exception e)
            {
                print("Error: Not possible to read file: " + CommonParameters.directory + @"system-data\Transformation-Camera.json");
                Console.WriteLine(e);
                readScript = true;
                runScript = false;
                return;
                //throw;
            }
            
            print("JSON received: " + fileContents);
            JsonUtility.FromJsonOverwrite(fileContents, tempCameraTransformation); 
            if (tempCameraTransformation.cameraTransformation.Count == CommonParameters.numberOfCameras - 1)
            {
                for (int i = 0; i < tempCameraTransformation.cameraTransformation.Count; i++)
                {
                    cameraTransformation.cameraTransformation[i] = tempCameraTransformation.cameraTransformation[i];
                }

            }

            readScript = true;
            runScript = false;
        }
    }

    public void computeAlignment()
    {
        
        if (!runScript)
        {
            string json = JsonUtility.ToJson(KinectReader.alignmentBalls);
            print("JSON points: " + json);
            System.IO.File.WriteAllText(CommonParameters.directory + @"system-data\Ball-positions.json", json);
            

            process = new System.Diagnostics.Process();
            process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = "/c python " + CommonParameters.directory + @"system-data\rigid-transform.py";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            print("Python script: " + process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
            
            startTime = Time.time;
            runScript = true;
            readScript = false;

        }
    }
}
