using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    [Range(0.01f, 0.5f)]
    public float speed = 0.2f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.Rotate(0,speed* 100 * Time.deltaTime, 0);
        }
        if(Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Rotate(0,-speed * 100 * Time.deltaTime, 0);
            
        }
        if(Input.GetKey(KeyCode.UpArrow))
        {
            transform.Rotate(-speed * 100 * Time.deltaTime,0, 0);
        }
        if(Input.GetKey(KeyCode.DownArrow))
        {
            transform.Rotate(speed * 100 * Time.deltaTime,0, 0);
        }
        
        
        if(Input.GetKey("i"))
        {
            transform.position = transform.position + transform.forward * speed * Time.deltaTime;
        }
        if(Input.GetKey("k"))
        {
            transform.position = transform.position + transform.forward * -speed * Time.deltaTime;
        }
        
        if(Input.GetKey("j"))
        {
            
            transform.Translate(new Vector3(-speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey("l"))
        {
            
            transform.Translate(new Vector3(speed * Time.deltaTime,0,0));
        }
        
        if(Input.GetKey("o"))
        {
            transform.Translate(new Vector3(0,speed * Time.deltaTime,0));
        }
        if(Input.GetKey("u"))
        {
            transform.Translate(new Vector3(0,-speed * Time.deltaTime,0));
        }
        
    }
}
