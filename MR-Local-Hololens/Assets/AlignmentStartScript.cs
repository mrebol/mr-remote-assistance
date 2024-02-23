using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AlignmentStartScript : MonoBehaviour
{
    
    /// <summary>
    /// this script aims to make the ultrasound screen (although should be generalizable)
    /// able to be moved intuitively by the user.
    ///
    /// another script should be made which allows for the user to save and load their configurations
    /// 
    /// </summary>
    // Start is called before the first frame update
    public GameObject videoScreen;
    public GameObject toAlignWith;
    public GameObject alignmentCube;
    private GameObject[] balls = new GameObject[4];
    private Vector3[] posToBalls = new Vector3[4];
    private Vector3 posToCam;
    private Quaternion saveRotation;
    private const float ScreenCubeRatio = 9.5f;
    public Camera userCamera;

    //[helene] working on saving to and loading from an external file so we can have multiple saves
    public delegate void saveInfo();
    public static event saveInfo saveThis;
    /// <summary>
    /// Set up the Ultrasound screen to be grabbable (works) (should be generalizeable)
    /// and allow the user to save their configurations so that they are easy to load back in.
    /// </summary>
    void Start()
    {
        userCamera = Camera.main;

        //align the screen first
        Invoke(nameof(AlignScreen), 0.1f);
        
        //set up the balls so that we can save the relative position of the ultrasound screen
        balls[0] = GameObject.Find("BallRed");
        balls[1] = GameObject.Find("BallGreen");
        balls[2] = GameObject.Find("BallBlue");
        balls[3] = GameObject.Find("BallYellow");

        foreach (var ball in balls)
        {
            Debug.Log("Balls " + ball.name);
        }
        
        
    }

    // Update is called once per frame
    void Update()
    {
        //scale the us screen with the scale of the cube we use to move the object
        //need the cube ration otherwise the US screen will be the same size as the cube (no bueno)
        videoScreen.transform.localScale = transform.localScale; // * ScreenCubeRatio;
    }

    public void AlignScreen()
    {
        //make parent
        toAlignWith.transform.parent = this.gameObject.transform;
        
        //set up in the right position
        //this also lock the screen and the cube together perfectly
        toAlignWith.transform.position = transform.position + new Vector3(0,videoScreen.transform.localScale.y * 0.51f, 0);//new Vector3(0, 0, 0);
        toAlignWith.transform.rotation = Quaternion.identity;
        
        //more information in the Alignment.cs script
        

    }

    public void SaveAlignment()
    {
        Debug.Log("Saving Alignment...");
        
        //first we need to caluculate the distance and angle from the aligment cube (this) to each ball.
        //from that, we should be able to save the position of the screen in space

        //TODO: also need to add in reference to how far the screen is from the user

        for (int ball = 0; ball < balls.Length; ball++)
        {
            posToBalls[ball] = transform.position - balls[ball].transform.position;
        }

        saveRotation = transform.rotation;
        
        
        //TODO: save how far it is from the camera when they hit the save button
        
        //saving the position relative to the headset
        posToCam = transform.position - userCamera.transform.position;
        
        Debug.Log(posToCam);




    }

    public void LoadAlignment()
    {
        //this is currently just in reference to the red ball. It works, although probably not the most robust. 

        if (saveRotation == Quaternion.identity)
        {
            Debug.Log("No user alignment set");
            return;
        }
        
        Debug.Log("Loading Alignment...");

        transform.position = balls[0].transform.position + posToBalls[0];
        transform.rotation = saveRotation;
    }

    //I commented this out because the console error reports were getting in the way of tracking other console alerts
    //private void OnDrawGizmos()
    //{
      //  Gizmos.DrawRay(balls[0].transform.position, posToBalls[0]);
        //Debug.Log("this function was called");
    //}
}
