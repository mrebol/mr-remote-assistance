using System;
using UnityEngine;

namespace Telepresence
{

    public class Us : MonoBehaviour//: Component
    {
        //public Transform setup;

        //this is the transform of the cube used to control the scale and position of the video
        public Transform usTransform;

        //the offset of the control cube from the video
        private Vector3 offset;


        private void Update()
        {
            //[Jesse] commented out some of this stuff because it was messing with what I was working on today
            //(moving around the screens and saving configurations)
            //[helene] brought this back in on to see if it interfered wit the save/load system I'm working on currently
            
            //rotation of the cube controls the rotation of the video
            transform.rotation = usTransform.rotation; 
            
            transform.Rotate(0, 0, -usTransform.rotation.eulerAngles.z);

            //the position of the video is always offset from the position of the cube
            transform.position = usTransform.position;

            //the video is always five times bigger than the cube, scaling with the cube's growth or shrinkage (need to refine for aspect ratio, user limits)
            //right now if the cube is scaled too large it will merge into the video, and if it is scaled too small it will get very far away from the video
            transform.localScale = usTransform.localScale;

            //this set of code stops us from being able to move the ultrasound screen around freely.
            //so we want to ignore this if we are trying to manipulate.
            //not sure what else this is attached to so best to keep it running otherwise
        }
    }
}