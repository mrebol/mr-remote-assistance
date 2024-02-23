using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

public class buttonData : MonoBehaviour
{
    //the save number the button displays, assigned by the saveScript
    //TO DO: decouple
    public int buttonNumber = 0;
    private bool loadsave;
    private ButtonConfigHelper helper;

    //the saveHandler with the loadData function
    //TO DO: decouple
    public GameObject SaveHandler;

    private void Start()
    {
        helper = GetComponent<ButtonConfigHelper>();
    }

    public void OnLoadClick()
    {
        loadsave = SaveHandler.GetComponent<GeneralSaveLoad>().getLoadSave();
        if (loadsave)
        {
            //calls the loadData function and passes it the number of the button that corresponds to the save being loaded
            SaveHandler.GetComponent<GeneralSaveLoad>().loadJSON(buttonNumber);
            Debug.Log(buttonNumber);
            helper.MainLabelText = "Load " + buttonNumber;
            
        }
        else
        {
            SaveHandler.GetComponent<GeneralSaveLoad>().saveJSON(buttonNumber);
            Debug.Log((buttonNumber));
            helper.MainLabelText = "Save " + buttonNumber;
        }
        Debug.Log(helper.MainLabelText);

    }
}
