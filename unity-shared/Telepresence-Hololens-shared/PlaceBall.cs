using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.UI;

public class PlaceBall : MonoBehaviour
{
    public List<Transform> balls;
    public Text ballSelectedLabel;
    private int selected = 0;
    private float placingBallStart = 0;
    

    // Start is called before the first frame update
    void Start()
    {
        placingBallStart = float.MaxValue; // To make sure no placing in the beginning
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time > placingBallStart && Time.time < placingBallStart + 4)
        {
            var handJointService = CoreServices.GetInputSystemDataProvider<IMixedRealityHandJointService>();
            if (handJointService != null)
            {
                var pointer = handJointService.RequestJointTransform(TrackedHandJoint.IndexTip, Handedness.Right).position;
                balls[selected].transform.position = pointer;
            }
        }
    }

    public void positionBall()
    {
        placingBallStart = Time.time;
    }
    

    public void changeBall()
    {
        selected = (selected + 1) % balls.Count;
        ballSelectedLabel.text = balls[selected].name;
    }
}
