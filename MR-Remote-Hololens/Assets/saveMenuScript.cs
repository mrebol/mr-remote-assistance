using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class saveMenuScript : MonoBehaviour
{
    private void Awake()
    {
        //saveScript.LoadMenuOpen.AddListener(EnableMenu);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void EnableMenu()
    {
        Debug.Log("this happened");
    }
}
