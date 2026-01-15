using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoplayTool : MonoBehaviour
{
    public TiffLayerManager tiffManager;
    public ProjectManager projectManager;
    string currentProjectName, oldProjectName;

    [Header("Autoplay Settings")]
    List<string> chosenLayersList;
    public TMP_InputField intervalInput;
    public Toggle loopToggle, reverseToggle;
    public Button startAutoplayButton;
    public GameObject autoplayPopUp, autoplaySeekBar;

    [Header("Token Input Field")]
    public TMP_InputField tokenInputField;
    public GameObject contentBox, tokenChipPrefab;
    public TMP_Dropdown inputDropdown;

    [Header("Seek Bar Resources")]
    public GameObject seekBar;
    public GameObject seekMarker, frameMarkerContainer, frameMarkerPref;

    float interval, seekBarSegment, seekBarLeftEdge;
    bool loopPlay, reversePlay, isPaused;
    IEnumerator r;
    int orderIndex;

    void Start()
    {
        tiffManager = tiffManager.GetComponent<TiffLayerManager>();
        projectManager = projectManager.GetComponent<ProjectManager>();
        chosenLayersList = new List<string>();
        startAutoplayButton.interactable = false;

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

        //set Token chip to remember AutoplayTool
        Autoplay_TokenChip chipData = newChip.GetComponent<Autoplay_TokenChip>();
        chipData.SetToolScript(this);

        //add Token name to the internal list
        chosenLayersList.Add(selectedLayerText);
        if (chosenLayersList.Count > 1)
            startAutoplayButton.interactable = true;

        tokenInputField.transform.SetAsLastSibling();
        tokenInputField.text = "";
        //  tokenInputField.ActivateInputField();
    }

    public void DeleteChip(string chipName)
    {
        chosenLayersList.Remove(chipName);
        if (chosenLayersList.Count < 2)
            startAutoplayButton.interactable = false;
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

        interval = Convert.ToInt32(intervalInput.text);
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
        foreach (string g in chosenLayersList)
        {
            tiffManager.OnPropertyToggleExternal(g, false);
        }
    }

    public void RecordSlideShow()
    {

    }

    IEnumerator SlideShow()
    {
        for (int i = 0; i < chosenLayersList.Count; i++)
        {
            orderIndex = reversePlay ? (chosenLayersList.Count - 1 - i) : i;

            SlideChooser(orderIndex);
            yield return new WaitForSeconds(interval);
            yield return new WaitUntil(() => !isPaused);
            if ((orderIndex == 0 && reversePlay) || (orderIndex == (chosenLayersList.Count - 1) && !reversePlay))
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

    public void Button_Pause()
    {
        isPaused = !isPaused;
    }

    public void Button_Previous()
    {
        ListLoop(-1);
        SlideChooser(orderIndex);
    }

    public void Button_Next()
    {
        ListLoop(1);
        SlideChooser(orderIndex);
    }

    int ListLoop(int number)
    {
        orderIndex += number;
        int realOrder = orderIndex > chosenLayersList.Count - 1 ? 0 : orderIndex;
        return realOrder;
    }
}