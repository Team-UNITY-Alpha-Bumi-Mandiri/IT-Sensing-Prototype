using System;
using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoplayTool : MonoBehaviour
{
    [Header("Autoplay Settings")]
    public GameObject[] chosenLayers;
    public TMP_InputField intervalInput;
    public Toggle loopToggle, reverseToggle;

    [Header("Seek Bar Resources")]
    public GameObject seekBar;
    public GameObject seekMarker, frameMarkerContainer, frameMarkerPref;

    float interval, seekBarSegment, seekBarLeftEdge;
    bool loopPlay, reversePlay;
    IEnumerator r;

    void Start()
    {
        RectTransform rt = seekBar.GetComponent<RectTransform>();
        seekBarLeftEdge = -(rt.sizeDelta.x / 2);
        seekBarSegment = rt.sizeDelta.x / (chosenLayers.Length - 1);
        for (int i = 0; i < chosenLayers.Length; i++)
        {
            GameObject frameMarkerInst = Instantiate(frameMarkerPref, frameMarkerContainer.transform);
            frameMarkerInst.transform.localPosition = new Vector2(seekBarLeftEdge + i * seekBarSegment, 0);
        }
    }

    public void StartSlideShow()
    {

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
    }

    //start loop
    IEnumerator SlideShow()
    {
        for (int i = 0; i < chosenLayers.Length; i++)
        {
            int j = reversePlay ? (chosenLayers.Length - 1 - i) : i;
            SlideSolo(j);
            yield return new WaitForSeconds(interval);

            if ((j == 0 && reversePlay) || (j == (chosenLayers.Length - 1) && !reversePlay))
            {
                if (loopPlay)
                    i = -1;
                else
                    Debug.Log("done showing");
            }
        }
        yield return null;
    }

    void SlideSolo(int index)
    {
        foreach (GameObject g in chosenLayers)
        {
            if (g == chosenLayers[index])
                g.SetActive(true);
            else
                g.SetActive(false);
        }

        seekMarker.transform.localPosition = new Vector2(seekBarLeftEdge + index * seekBarSegment, 0);
    }
}