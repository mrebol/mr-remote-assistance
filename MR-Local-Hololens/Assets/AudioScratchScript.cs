using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioScratchScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Invoke(nameof(MicDebugging), 2f);
       

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void MicDebugging()
    {
        Debug.Log("Audio Scratch debug start----------------------------------");
        foreach (var mic in Microphone.devices)
        {
            Debug.Log(mic);
        }
        
        
        Debug.Log("Audio Scratch debug end----------------------------------");
    }
}
