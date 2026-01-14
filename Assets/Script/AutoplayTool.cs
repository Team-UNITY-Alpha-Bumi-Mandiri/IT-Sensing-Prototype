using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoplayTool : MonoBehaviour
{
    public TiffLayerManager tiffManager;
    public ProjectManager projectManager;
    string currentProjectName, oldProjectName;

    [Header("Autoplay Settings")]
    public GameObject[] chosenLayers; // placeholder testing
    List<string> chosenLayersList;
    public GameObject autoplayPopUp, autoplaySeekBar;

    public TMP_InputField intervalInput;
    public Toggle loopToggle, reverseToggle;

    [Header("Token Input Field")]
    public TMP_InputField tokenInputField;
    public GameObject contentBox, tokenChipPrefab;
    public TMP_Dropdown inputDropdown;

    [Header("Seek Bar Resources")]
    public GameObject seekBar;
    public GameObject seekMarker, frameMarkerContainer, frameMarkerPref;

    float interval, seekBarSegment, seekBarLeftEdge;
    bool loopPlay, reversePlay;
    IEnumerator r;

    void Start()
    {
        tiffManager = tiffManager.GetComponent<TiffLayerManager>();
        projectManager = projectManager.GetComponent<ProjectManager>();
        chosenLayersList = new List<string>();

        //  PrepareSlideShow(); //tempt test
    }

    private void Update()
    {
        // Death loop contingency plan
        if (contentBox.transform.childCount > 10)
        {
            Time.timeScale = 0;
            Debug.Log("HIGHWAY TO HELL");
        }
    }

    public void UpdateDropdownOptions()
    {
        var currentProject = projectManager.GetCurrentProject();
        // currentProjectName = currentProject.name;
        if (currentProject != null)// && currentProjectName != oldProjectName)
        {
            //   return;

            List<string> layerNameOptions = new List<string>();
            layerNameOptions.Add("None");
            foreach (var prop in currentProject.properties)
            {
                layerNameOptions.Add(prop.key);
            }

            inputDropdown.ClearOptions();
            inputDropdown.AddOptions(layerNameOptions);
            //     oldProjectName = currentProjectName;
        }
    }

    public void AddChip()
    {
        int selectedIndex = inputDropdown.value;
        string selectedLayerText = inputDropdown.options[selectedIndex].text;

        GameObject newChip = Instantiate(tokenChipPrefab, contentBox.transform);
        TMP_Text chipText = newChip.GetComponentInChildren<TMP_Text>();
        chipText.text = selectedLayerText;

        chosenLayersList.Add(selectedLayerText);

        tokenInputField.transform.SetAsLastSibling();
        tokenInputField.text = "";
        //  tokenInputField.ActivateInputField();
    }

    void PrepareSlideShow()
    {

    }

    public void StartSlideShow()
    {
        autoplayPopUp.SetActive(false);
        autoplaySeekBar.SetActive(true);

        // After layers are selected
        RectTransform rt = seekBar.GetComponent<RectTransform>();
        seekBarLeftEdge = -(rt.sizeDelta.x / 2);
        seekBarSegment = rt.sizeDelta.x / (chosenLayersList.Count - 1);

        //making frames on Seek Bar
        for (int i = 0; i < chosenLayersList.Count; i++)
        {
            GameObject frameMarkerInst = Instantiate(frameMarkerPref, frameMarkerContainer.transform);
            frameMarkerInst.transform.localPosition = new Vector2(seekBarLeftEdge + i * seekBarSegment, 0);
        }

        interval = Convert.ToInt32(intervalInput);
        loopPlay = loopToggle.isOn;
        reversePlay = reverseToggle.isOn;
        if (r != null)
            StopCoroutine(r);

        r = SlideShow();
        StartCoroutine(r);
    }

    public void StopSlideShow()
    {
        StopCoroutine(r);
        autoplaySeekBar.SetActive(false);
    }

    /*
    public list string selectedlayers = new list string

      {

        int index = 0
        while true
        {
            string currentlyer = selectedlayer(index)
        
            tiffmanager.onpropertytoggleexternal(currentlayer,true)
           interval
             tiffmanager.onpropertytoggleexternal(currentlayer,true)
             index=(index+1)%selectedlayers.count
        }
    }
    */

    //start loop
    IEnumerator SlideShow()
    {
        for (int i = 0; i < chosenLayersList.Count; i++)
        {
            int j = reversePlay ? (chosenLayersList.Count - 1 - i) : i;
            SlideChooser(j);
            yield return new WaitForSeconds(interval);

            if ((j == 0 && reversePlay) || (j == (chosenLayersList.Count - 1) && !reversePlay))
            {
                if (loopPlay)
                    i = -1;
                else
                    Debug.Log("done showing");
            }
        }
        yield return null;
    }

    void SlideChooser(int index)
    {
        foreach (string g in chosenLayersList)
        {
            if (g == chosenLayersList[index])
            {
                tiffManager.OnPropertyToggleExternal(g, true);
            }
            else
            {
                tiffManager.OnPropertyToggleExternal(g, false);
            }
        }
        seekMarker.transform.localPosition = new Vector2(seekBarLeftEdge + index * seekBarSegment, 0);
    }

    void SlideSolo(int index)
    {
        foreach (GameObject g in chosenLayers)
        {
            if (g == chosenLayers[index])
            {
                g.SetActive(true);
            }
            else
            {
                g.SetActive(false);
            }
        }
        seekMarker.transform.localPosition = new Vector2(seekBarLeftEdge + index * seekBarSegment, 0);
    }
}