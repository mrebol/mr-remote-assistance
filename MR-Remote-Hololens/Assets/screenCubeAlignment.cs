using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class screenCubeAlignment : MonoBehaviour
{
    public GameObject screen;
    public GameObject toAlignWith;
    public GameObject alignmentCube;

    public Collider transformCollider;
    // Start is called before the first frame update
    void Start()
    {

        Invoke(nameof(AlignYuv), 1);
        Invoke(nameof(AlignScreen), 1);
        
    }

    // Update is called once per frame
    void Update()
    {

        //screen scales with the transform
        
        screen.transform.localScale = transformCollider.transform.localScale;
    }

    public void AlignScreen()
    {
        toAlignWith.transform.parent = this.gameObject.transform;


        //make parent

        toAlignWith.transform.position = transform.position + new Vector3(0, screen.transform.localScale.y * 0.51f, 0);//new Vector3(0, 0, 0);
        
        toAlignWith.transform.rotation = Quaternion.identity;

     
    }


    public void AlignYuv()
    {

        //sets alignment cube to the middle of the screen
        alignmentCube.transform.position = new Vector3(screen.transform.localScale.x * 0.5f, screen.transform.localScale.y * 0.5f, 0);


        toAlignWith.transform.parent = this.gameObject.transform;
        
        toAlignWith.transform.rotation = Quaternion.identity;
        transformCollider.transform.position = alignmentCube.transform.position;
        transformCollider.transform.localScale = new Vector3(16, 9, 0.25f)/10;



    }
}
