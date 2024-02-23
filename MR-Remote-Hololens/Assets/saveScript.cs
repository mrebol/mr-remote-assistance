using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine.Events;

//this script goes on the SaveHandler empty object

//TO DO: decrease coupling with objects and buttons
//TO DO: find a way to keep the saves persistent between uses and detect the number of saves already in the save file at program start
//TO DO: potentially give saves clearer more intuitive names than just numbers
//tO DO: figure out math to save the rotation as relative to the user position
public class saveScript : MonoBehaviour
{
    //where the save file is stored
    private string jsonSavePath;
    //keeps track of how many save files have been made
    private int saveCounter = 1;
    //creates name of saves
    private string saveName;
    //creates name of loads
    private string loadName;
    //where the file to be loaded is stored
    private string jsonLoadPath;

    //total number of saves that can be made and stored
    //6 is what fits comfortably on the menu button backplate as it is right now
    public static int totNumSaves = 6;

    //creating an instance of saveData to hold the values so we can save them to the JSON
    public saveData sav;

    //the three cubes I use to position the video screens in the level
    //I would like to find a more elegant solution to collecting their positions that involves less direct coupling
    //but first I have to think of that solution (happy for suggestions and ideas)
    //usg controls the Ultrasound screen
    public GameObject usgControl;
    //yuv controls the 2d video of the student and workspace
    public GameObject yuvControl;
    //holo controls the holographic display of the student and workspace
    public GameObject holoControl;
    //userCam is the in-level camera which will always occupy the same position as the user
    public Camera userCam;
    public GameObject syringe;
    public GameObject probe;
    public GameObject cubeA;
    public GameObject cubeB;
    public GameObject tubeA;
    public GameObject tubeB;
    public GameObject tubeC;

    //the menu object that shows the saves
    public GameObject saveListSubmenu;
    public GameObject settingMenu;
    public GameObject settingMenuContent;

    public GameObject hologramTransform;
    //list of buttons to display the different saves to be loaded
    public List<GameObject> buttonList = new List<GameObject>(new GameObject[totNumSaves]);

    //need to strategize further on decoupling because the nature of the appearing and disappearing hand menu means having it subscrinbe to events
    //on awake or on start won't work, since the subscription will start after the event is already in progress

    //need this for the recentering to be correct. [testing]
    public GameObject alignment;
    private Vector3 alignTransform;

    
    public string stringPath;

    //when the program starts
    private void Awake()
    {
        //initialize sav as saveData
        sav = new saveData();

        //turn off the save selection menu
        saveListSubmenu.SetActive(false);
        settingMenu.SetActive(false);
        settingMenuContent.SetActive(false);

        //turn off the buttons one at a time specifically so that the right number can be turned on later
        //for some reason the button list wasn't responding to regular for loops so this is a temporary solution until I can figure out that problem
        int b = 1;
        foreach (GameObject butt in buttonList) 
        {
            butt.SetActive(false);
            
            //assigns each button its corresponding number
            //TO DO: decouple this from the ButtonData class
            butt.GetComponent<buttonData>().buttonNumber = b;
            b++;
        }

        checkExisting();
    }

    private void Start()
    {
        alignTransform = alignment.transform.position;

        stringPath = Application.persistentDataPath + "_log.txt";
    }

    //function to save the positions
    //activated when the user presses the "Save Layout" button I added to the sys-eval hand menu
    public void saveData(int savecounter = -1)
    {
        if (savecounter == -1)
        {
            savecounter = saveCounter;
        }
        //the save gets labelled with its number
        saveName = "/saveload_remoteholo_" + savecounter.ToString() + ".json";
        jsonSavePath = Application.persistentDataPath + saveName;

        //saving the position of the camera to use as the relative center of the scene
        sav.userZero = alignTransform;
        sav.userRot = userCam.transform.rotation;
        sav.userCurrent = userCam.transform.position;

        //saving the locations of the control cubes to sav
        //relative position found by subtracting cube position from user position
        sav.usgPosition = sav.userZero - usgControl.transform.position;
        sav.usgRotation = usgControl.transform.rotation;
        sav.usgScale = usgControl.transform.localScale;

        sav.yuvPosition = sav.userZero - yuvControl.transform.position;
        sav.yuvRotation = yuvControl.transform.rotation;
        sav.yuvScale = yuvControl.transform.localScale;

        sav.holoPosition = sav.userZero - holoControl.transform.position;
        sav.holoRotation = holoControl.transform.rotation;
        sav.holoScale = holoControl.transform.localScale;

        sav.cylinderAPos = sav.userZero - tubeA.transform.position;
        sav.caRot = tubeA.transform.rotation;
        sav.caScale = tubeA.transform.localScale;

        sav.cylinderBPos = sav.userZero - tubeB.transform.position;
        sav.cbRot = tubeB.transform.rotation;
        sav.cbScale = tubeB.transform.localScale;

        sav.cylinderCPos = sav.userZero - tubeC.transform.position;
        sav.ccRot = tubeC.transform.rotation;
        sav.ccScale = tubeC.transform.localScale;

        sav.cubeAPos = sav.userZero - cubeA.transform.position;
        sav.cuARot = cubeA.transform.rotation;
        sav.cuAScale = cubeA.transform.localScale;

        sav.cubeBPos = sav.userZero - cubeB.transform.position;
        sav.cuBRot = cubeB.transform.rotation;
        sav.cuBScale = cubeB.transform.localScale;

        sav.syringePos = sav.userZero - syringe.transform.position;
        sav.syringeRot = syringe.transform.rotation;
        sav.syringeScale = syringe.transform.localScale;

        sav.usProbePos = sav.userZero - probe.transform.position;
        sav.usProbeRot = probe.transform.rotation;
        sav.usProbeScale = probe.transform.localScale;

        //converting sav to a JSON using Unity's builtin JSON utility (lots of references online for Unity's JSON stuff)
        string json_data = JsonUtility.ToJson(sav);

        //writing the string of JSON data into a file saved at the end of the JSON path set when the function is called
        File.WriteAllText(jsonSavePath, json_data);

        writeToTxt(json_data);

        Debug.Log("data saved at" + jsonSavePath);

        //reset the save counter and begin overwriting if we go over the number of saves that fits on the menu
        if (saveCounter < totNumSaves)
        {
            saveCounter++;
        }
        else
        {
            saveCounter = 1;
        }
        
        saveListSubmenu.SetActive(false);
    }

    //function to load saved positions
    //activated when the user chooses one of the saved layout buttons on the save list submenu after selecting the Load Saved Layouts button on the handmenu
    //it takes an int from the button it is attached to in order to know which save to load
    public void loadData(int saveNum)
    {
        //turn off the save selection menu now that the user has selected a save
        saveListSubmenu.SetActive(false);

        //creates the name of the save file selected and the path to it
        loadName = "/saveload_remoteholo_" + saveNum.ToString() + ".json";
        jsonLoadPath = Application.persistentDataPath + loadName;

        //checking if a save file exists at the end of the path before attempting to load it
        if (File.Exists(jsonLoadPath))
        {
            //converting the information from the save file back into a saveData object called sav
            //because jsonSavePath has not been updated with the incremented saveCounter yet, it finds the most recent save
            sav = JsonUtility.FromJson<saveData>(File.ReadAllText(jsonLoadPath));

            //sending the saved values from sav into the position etc. values for the various control cubes
            //working backwards from the save function, subtracting the saved position from the current user position to return the original position as related to world zero
            usgControl.transform.position = alignTransform - sav.usgPosition;
            usgControl.transform.rotation = sav.usgRotation;
            usgControl.transform.localScale = sav.usgScale;

            yuvControl.transform.position = alignTransform - sav.yuvPosition;
            yuvControl.transform.rotation = sav.yuvRotation;
            yuvControl.transform.localScale = sav.yuvScale;

            holoControl.transform.position = alignTransform - sav.holoPosition;
            holoControl.transform.rotation = sav.holoRotation;
            holoControl.transform.localScale = sav.holoScale;

            tubeA.transform.position = alignTransform - sav.cylinderAPos;
            tubeA.transform.rotation = sav.caRot;
            tubeA.transform.localScale = sav.caScale;

            tubeB.transform.position = alignTransform - sav.cylinderBPos;
            tubeB.transform.rotation = sav.cbRot;
            tubeB.transform.localScale = sav.cbScale;

            tubeC.transform.position = alignTransform -  sav.cylinderCPos;
            tubeC.transform.rotation = sav.ccRot;
            tubeC.transform.localScale = sav.ccScale;

            cubeA.transform.position = alignTransform - sav.cubeAPos;
            cubeA.transform.rotation = sav.cuARot;
            cubeA.transform.localScale = sav.cuAScale;

            cubeB.transform.position = alignTransform - sav.cubeBPos;
            cubeB.transform.rotation = sav.cuBRot;
            cubeB.transform.localScale = sav.cuBScale;

            syringe.transform.position = alignTransform - sav.syringePos;
            syringe.transform.rotation = sav.syringeRot;
            syringe.transform.localScale = sav.syringeScale;

            probe.transform.position = alignTransform - sav.usProbePos;
            probe.transform.rotation = sav.usProbeRot;
            probe.transform.localScale = sav.usProbeScale;

            Debug.Log("data loaded from" + jsonLoadPath);
            
            
        }
        //if no file is found send a message to the console and do nothing
        else 
        {
            Debug.Log("save file does not exist");
        }
        

        GameObject.Find("Alignment").GetComponent<recenterObjects>().recenterObject();
        
    }

    //turns on the save selection submenu when it is selected
    public void enableSubMenu() 
    {

        saveListSubmenu.SetActive(!saveListSubmenu.activeSelf);


        //this loop turns on the correct number of buttons and assigns them the number of the corresponding save
        //TO DO: decouple this and make it more efficient
        //took out turning individual buttons on and off because it was introducing new bugs combined with checking for existing saves
        string label;
        foreach (GameObject but in buttonList) 
        {

            but.SetActive(true);
            
            if (lastButtonPressed)
            {
                label = "Load " + but.GetComponent<buttonData>().buttonNumber;
            }
            else
            {
                label = "Save " + but.GetComponent<buttonData>().buttonNumber;

            }
            but.GetComponentInChildren<TextMeshPro>().text = label;

        }
    }
    
    public void enableSettingsMenu() 
    {
            settingMenu.SetActive(!settingMenu.activeSelf);
            settingMenuContent.SetActive(!settingMenuContent.activeSelf);

    }

    public void checkExisting() 
    {
        if (Directory.Exists(Application.persistentDataPath))
        {
            string savesFolder = Application.persistentDataPath;
            DirectoryInfo d = new DirectoryInfo(savesFolder);

            foreach (var file in d.GetFiles("*.json"))
            {
                if (saveCounter < totNumSaves)
                {
                    saveCounter++;
                }
            }
        }
        else 
        {
            Debug.Log("No existing files found");
        }
    }

    public void writeToTxt(string text) 
    {
        if (!File.Exists(stringPath))
        {
            Debug.Log("this");
            File.WriteAllText(stringPath, "\n" + text + "\n");
        }
        else 
        {
            Debug.Log("or this");
            File.AppendAllText(stringPath, "\n" + text + "\n");
        }
    }

    public void toggleHologramTransform()
    {
        hologramTransform.SetActive(!hologramTransform.activeSelf);
    }

    private bool lastButtonPressed = false;
    public void setSave()
    {
        lastButtonPressed = false;
    }

    public void setLoad()
    {
        lastButtonPressed = true;
    }

    public bool getLoadSave()
    {
        return lastButtonPressed;
    }
    
}
