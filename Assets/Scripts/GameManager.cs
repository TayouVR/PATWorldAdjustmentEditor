﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using SFB;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Debug = UnityEngine.Debug;
using Slider = UnityEngine.UI.Slider;

public class GameManager : MonoBehaviour {
    
    public Button saveWorldFile;

    public int worldSize = 500; // overwritten by file
    private int diameter;

    public GameObject planet;
    public Material selectedMaterial;
    public Material sphereMaterial;

    public Slider radiusSlider;
    public InputField radiusInputText;
    public Slider heightSlider;
    public InputField heightInputText;

    public Text jsonDataForCurrentObject;

    public GameObject openDialog;
    public GameObject radiusPrefab;
    
    public int defaultRadius;
    public int defaultHeight = 0;
    
#pragma warning disable 108,114
    private GameObject camera;
#pragma warning restore 108,114
    private Vector3 lastMousePosition;
    private List<HeightAdjustment> heightAdjustments;
    private int tool = 0;
    private HeightAdjustment activeObject;

    private string loadedWorldFile;
    
    // Start is called before the first frame update
    void Start() {
        camera = Camera.main.gameObject;
        diameter = worldSize * 2;
        defaultRadius = (int)Math.Floor(diameter * 0.1f);
        setWorldSize();
        heightAdjustments = new List<HeightAdjustment>();
    }

    public void SelectNoTool() {
        tool = 0;
    }

    public void SelectPlaceTool() {
        tool = 1;
        planet.layer = LayerMask.NameToLayer("Default");
        foreach (var adjustment in heightAdjustments) {
            adjustment.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }
    }

    public void SelectSelectTool() {
        tool = 2;
        planet.layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (var adjustment in heightAdjustments) {
            adjustment.gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!EventSystem.current.IsPointerOverGameObject()) {
            if (Input.GetAxis("Fire2") >= 0.1f || Input.GetAxis("Fire3") >= 0.1f || (tool == 0 && Input.GetAxis("Fire1") >= 0.1f)) {
                camera.transform.parent.Rotate(new Vector3(
                                                           lastMousePosition.y - Input.mousePosition.y, 
                                                           Input.mousePosition.x - lastMousePosition.x, 
                                                           Input.mousePosition.z - lastMousePosition.z), Space.Self);
            }
            lastMousePosition = Input.mousePosition;
        
            if (Input.GetMouseButtonDown(0)) {
                if (tool == 1) {
                    Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, Mathf.Infinity);
                    if (hit.collider.gameObject != null) {
                        GameObject newSphere = Instantiate(radiusPrefab, hit.point, new Quaternion());
                        var adjustment = newSphere.GetComponent<HeightAdjustment>();
                        adjustment.Pos = new[]{ (int)hit.point.x, (int)hit.point.y, (int)hit.point.z};
                        heightAdjustments.Add(adjustment);
                        adjustment.adjustment = defaultHeight;
                        adjustment.radius = defaultRadius;
                        SetActiveObject(adjustment);
                    }
                } else if (tool == 2) {
                    Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, Mathf.Infinity);
                    try {
                        SetActiveObject(hit.collider.gameObject.GetComponent<HeightAdjustment>());
                    } catch (Exception e) {
                        Debug.Log("" + e);
                    }
                }
            }

            if (activeObject != null) {
                if (Input.GetButton("Delete")) {
                    Destroy(activeObject);
                }
            }
        
            camera.transform.Translate(Vector3.forward * (Input.mouseScrollDelta.y * 10f), Space.Self);
        }
    }

    private void SetActiveObject(HeightAdjustment active) {
        if (activeObject != null) {
            activeObject.GetComponent<MeshRenderer>().material = sphereMaterial;
        }
        activeObject = active;
        activeObject.GetComponent<MeshRenderer>().material = selectedMaterial;
        SetRadiusForActiveObject(activeObject.radius);
        SetHeightForActiveObject(activeObject.adjustment);
    }

    private void SetRadiusForActiveObject(int value) {
        activeObject.Radius = value;
        jsonDataForCurrentObject.text = WriteObjectToJson(activeObject);
        radiusSlider.SetValueWithoutNotify(value);
        radiusInputText.text = value.ToString();
        defaultRadius = value;
    }

    private void SetHeightForActiveObject(int value) {
        activeObject.adjustment = value;
        jsonDataForCurrentObject.text = WriteObjectToJson(activeObject);
        heightSlider.SetValueWithoutNotify(value);
        heightInputText.text = value.ToString();
        defaultHeight = value;
    }

    public void SetHeightInputFromSlider(float value) {
        SetHeightForActiveObject((int)value);
    }

    public void SetHeightSliderFromText(String value) {
        try {
            SetHeightForActiveObject(int.Parse(value));
        } catch (Exception e) {
            Debug.Log("The String was not valid:\n" + e);
        }
    }

    public void SetRadiusInputFromSlider(float value) {
        SetRadiusForActiveObject((int)value);
    }

    public void SetRadiusSliderFromText(String value) {
        try {
            SetRadiusForActiveObject(int.Parse(value));
        } catch (Exception e) {
            Debug.Log("The String was not valid:\n" + e);
        }
    }

    public void OpenWorldFile() {
        StandaloneFileBrowser.OpenFilePanelAsync("Open World File", 
                                                 "", 
                                                 "", 
                                                 false,
                                                 paths => {
                                                     if (!string.IsNullOrEmpty(paths[0])) {
                                                         loadedWorldFile = paths[0];
                                                     }
                                                 });
        var seperatedHeightAdjustments = loadedWorldFile.Substring(loadedWorldFile.IndexOf("\"heightAdjustments\": [", StringComparison.CurrentCulture), 
                                                                   loadedWorldFile.IndexOf("			}\n        }\n    ]\n}", StringComparison.CurrentCulture) - 
                                                                   loadedWorldFile.IndexOf("\"heightAdjustments\": [", StringComparison.CurrentCulture));
        var heightAdjustmentArray = JsonUtility.FromJson<HeightAdjustment[]>(seperatedHeightAdjustments);
        openDialog.SetActive(true);
        var dialogResponse = openDialog.GetComponent<DialogBox>().CreateDialog();
        openDialog.SetActive(false);
        if (dialogResponse == 1) {
            foreach (var heightAdjustment in heightAdjustments) {
                heightAdjustments.Remove(heightAdjustment);
                Destroy(heightAdjustment.gameObject);
            }
            heightAdjustments.AddRange(heightAdjustmentArray);
        } else if (dialogResponse == 2) {
            heightAdjustments.AddRange(heightAdjustmentArray);
        }
    }

    public void SaveInfoToFile() {
        var outString = "				\"heightAdjustments\": [\n				    ";
        for (var index = 0; index < heightAdjustments.Count; index++) {
            var adjustment = heightAdjustments[index];
            outString += Regex.Replace(WriteObjectToJson(adjustment) + (heightAdjustments.Count-index == 1 ? "" : ",\n"), "\n", "\n					");
        }

        outString += "\n				]";
        //var outString = JsonUtility.ToJson(heightAdjustments.ToArray());

        StandaloneFileBrowser.SaveFilePanelAsync("Save File", 
                                                 "", 
                                                 "outFile.json", 
                                                 "", 
                                                 (string path) => { 
                                                     if (!string.IsNullOrEmpty(path)) {
                                                         File.WriteAllText(path, outString);
                                                     }  
                                                 });
    }

    public void setWorldSize() {
        planet.transform.localScale = new Vector3(diameter, diameter, diameter);
        camera.transform.position = new Vector3(0, 0, (float)(-diameter*1.2));
        camera.transform.rotation = new Quaternion();
    }

    private String WriteObjectToJson(HeightAdjustment heightAdjustment) {
        /*String outString = "{\n" +
                           "    \"adjustment\": " + heightAdjustment.adjustment + ",\n" +
                           "    \"radius\": " + heightAdjustment.radius + ",\n" +
                           "    \"pos\": [\n" +
                           "        " + heightAdjustment.pos[0] + ",\n" +
                           "        " + heightAdjustment.pos[1] + ",\n" +
                           "        " + heightAdjustment.pos[2] + "\n" +
                           "    ]\n" +
                           "}";*/
        String outString = JsonUtility.ToJson(heightAdjustment, true);
        return outString;
    }
}
