using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManipulationTestScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
    //no rigidbodies so neither of these should work
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("something hit the object");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Something triggered the object");
    }
}