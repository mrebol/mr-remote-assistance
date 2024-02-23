using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public Vector3 usPosition = new Vector3(0, 0, 0);
    public Quaternion usRotation = new Quaternion(0, 0, 0, 0);
    public Vector3 usScale = new Vector3(1, 1, 1);

    public Vector3 remotePosition = new Vector3(0, 0, 0);
    public Quaternion remoteRotation = new Quaternion(0, 0, 0, 0);
    public Vector3 remoteScale = new Vector3(1, 1, 1);

    public Vector3 userZero = new Vector3(0, 0, 0);
    public Quaternion userRot = new Quaternion(0, 0, 0, 0);
    public Vector3 userCurrent = new Vector3(0, 0, 0);
}