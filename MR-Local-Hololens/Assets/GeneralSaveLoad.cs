using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UIElements;

public class GeneralSaveLoad : MonoBehaviour
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

    public GameObject USControl;
    public GameObject remoteControl;
    public Camera userCam;

    //object of the saveData class
    public SaveData saveFile;

    public GameObject buttonGroup;
    public List<GameObject> buttonList = new List<GameObject>(new GameObject[totNumSaves]);
    public GameObject saveButton;
    public GameObject loadButton;

    public GameObject alignment;
    private Vector3 alignTransform;

    public string stringPath;
    private bool lastButtonPressed = false;

    private void Awake()
    {
        saveFile = new SaveData();

        buttonGroup.SetActive(false);

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

    public void saveJSON(int savecounter = -1)
    {
        if (savecounter == -1)
        {
            savecounter = saveCounter;
        }
        //the save gets labelled with its number
        saveName = "/saveload_localholo_" + savecounter.ToString() + ".json";
        //and the save path is created, sending it to the /AppData/LocalLow/DefaultCompany/MR-Remote-2020 folder
        jsonSavePath = Application.persistentDataPath + saveName;

        //Debug.Log(jsonSavePath);

        //saving the position of the camera to use as the relative center of the scene
        saveFile.userZero = alignTransform;
        saveFile.userRot = userCam.transform.rotation;
        saveFile.userCurrent = userCam.transform.position;
        //Debug.Log(saveFile.userZero);

        saveFile.usPosition = saveFile.userZero - USControl.transform.position;
        saveFile.usRotation = USControl.transform.rotation;
        saveFile.usScale = USControl.transform.localScale;
        distanceCamCast(USControl);

        saveFile.remotePosition = saveFile.userZero - remoteControl.transform.position;
        saveFile.remoteRotation = remoteControl.transform.rotation;
        saveFile.remoteScale = remoteControl.transform.localScale;

        //converting sav to a JSON using Unity's builtin JSON utility (lots of references online for Unity's JSON stuff)
        string json_data = JsonUtility.ToJson(saveFile);
        //Debug.Log(json_data);
        //writing the string of JSON data into a file saved at the end of the JSON path set when the function is called
        File.WriteAllText(jsonSavePath, json_data);

        Debug.Log("data saved at" + jsonSavePath);
        //Debug.Log(sav.usgPosition + " " + sav.userZero);

        //reset the save counter and begin overwriting if we go over the number of saves that fits on the menu
        if (saveCounter < totNumSaves)
        {
            saveCounter++;
        }
        else
        {
            saveCounter = 1;
        }
        
        buttonGroup.SetActive(false);
    }

    public void loadJSON(int saveNumber) 
    {
        buttonGroup.SetActive(false);

        Debug.Log(saveNumber);
        //creates the name of the save file selected and the path to it
        loadName = "/saveload_localholo_" + saveNumber.ToString() + ".json";
        jsonLoadPath = Application.persistentDataPath + loadName;
        Debug.Log(jsonLoadPath);

        //checking if a save file exists at the end of the path before attempting to load it
        if (File.Exists(jsonLoadPath))
        {
            //converting the information from the save file back into a saveData object called sav
            //because jsonSavePath has not been updated with the incremented saveCounter yet, it finds the most recent save
            saveFile = JsonUtility.FromJson<SaveData>(File.ReadAllText(jsonLoadPath));

            USControl.transform.position = alignTransform - saveFile.usPosition;
            USControl.transform.rotation = saveFile.usRotation;
            USControl.transform.localScale = saveFile.usScale;

            remoteControl.transform.position = alignTransform - saveFile.remotePosition;
            remoteControl.transform.rotation = saveFile.remoteRotation;
            remoteControl.transform.localScale = saveFile.remoteScale;

            Debug.Log("data loaded from" + jsonLoadPath);
            //Debug.Log(usgControl.transform.position + " " + userCam.transform.position);
        }
        //if no file is found send a message to the console and do nothing
        else
        {
            Debug.Log("save file does not exist");
        }

        GameObject.Find("Alignment").GetComponent<recenterObjects>().recenterObject();
    }

    public void enableSubMenu()
    {
        buttonGroup.SetActive(true);

        //this loop turns on the correct number of buttons and assigns them the number of the corresponding save
        //took out turning on and off individual buttons because it was interacting badly with checking for pre-existing saves
        //TO DO: decouple this and make it more efficient
        string label;
       // int i = 1;
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
    public void distanceCamCast(GameObject otherObj) 
    {
        var distance = Vector3.Distance(userCam.transform.position, otherObj.transform.position);
        Debug.Log(distance);
        Debug.DrawLine(userCam.transform.position, otherObj.transform.position);

    }

    public int getSaveNumber()
    {
        return saveCounter;
    }

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
