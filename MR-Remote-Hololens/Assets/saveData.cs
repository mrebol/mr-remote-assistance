using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

//class for saving the position data of the control cubes to the json file
//initializing them with default values so that they are ready to receive new values on save
//we can change these starting values if we want to have a specific setup that can be loaded on start by a user
[System.Serializable]
public class saveData
{
    public Vector3 usgPosition = new Vector3 (0,0,0);
    public Quaternion usgRotation = new Quaternion (0,0,0,0);
    public Vector3 usgScale = new Vector3 (1,1,1);

    public Vector3 yuvPosition = new Vector3(0, 0, 0);
    public Quaternion yuvRotation = new Quaternion(0, 0, 0, 0);
    public Vector3 yuvScale = new Vector3(1, 1, 1);

    public Vector3 holoPosition = new Vector3(0, 0, 0);
    public Quaternion holoRotation = new Quaternion(0, 0, 0, 0);
    public Vector3 holoScale = new Vector3(1, 1, 1);

    public Vector3 userZero = new Vector3(0, 0, 0);
    public Quaternion userRot = new Quaternion(0, 0, 0, 0);
    public Vector3 userCurrent = new Vector3(0, 0, 0);

    public Vector3 cylinderAPos = new Vector3(0, 0, 0);
    public Quaternion caRot = new Quaternion(0, 0, 0, 0);
    public Vector3 caScale = new Vector3(1, 1, 1);

    public Vector3 cylinderBPos = new Vector3(0, 0, 0);
    public Quaternion cbRot = new Quaternion(0, 0, 0, 0);
    public Vector3 cbScale = new Vector3(1, 1, 1);

    public Vector3 cylinderCPos = new Vector3(0, 0, 0);
    public Quaternion ccRot = new Quaternion(0, 0, 0, 0);
    public Vector3 ccScale = new Vector3(1, 1, 1);

    public Vector3 cubeAPos = new Vector3(0, 0, 0);
    public Quaternion cuARot = new Quaternion(0, 0, 0, 0);
    public Vector3 cuAScale = new Vector3(1, 1, 1);

    public Vector3 cubeBPos = new Vector3(0, 0, 0);
    public Quaternion cuBRot = new Quaternion(0, 0, 0, 0);
    public Vector3 cuBScale = new Vector3(1, 1, 1);

    public Vector3 syringePos = new Vector3(0, 0, 0);
    public Quaternion syringeRot = new Quaternion(0, 0, 0, 0);
    public Vector3 syringeScale = new Vector3(1, 1, 1);

    public Vector3 usProbePos = new Vector3(0, 0, 0);
    public Quaternion usProbeRot = new Quaternion(0, 0, 0, 0);
    public Vector3 usProbeScale = new Vector3(1, 1, 1);
}
