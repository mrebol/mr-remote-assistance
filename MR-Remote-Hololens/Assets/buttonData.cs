using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//class that holds a save/load button's corresponding number
public class buttonData : MonoBehaviour
{
    //the save number the button displays, assigned by the saveScript
    //TO DO: decouple
    public int buttonNumber = 0;
    private bool loadsave;

    //the saveHandler with the loadData function
    //TO DO: decouple
    public GameObject SaveHandler;

    public void OnLoadClick() 
    {
        //calls the loadData function and passes it the number of the button that corresponds to the save being loaded
        
        loadsave = SaveHandler.GetComponent<saveScript>().getLoadSave();
        if (loadsave)
        {
            //calls the loadData function and passes it the number of the button that corresponds to the save being loaded
            SaveHandler.GetComponent<saveScript>().loadData(buttonNumber);
            Debug.Log(buttonNumber);
            
        }
        else
        {
            SaveHandler.GetComponent<saveScript>().saveData(buttonNumber);
            Debug.Log((buttonNumber));
        }
    }
}
