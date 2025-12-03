using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DistanceButton : MonoBehaviour
{
    [SerializeField] private GameObject pentool;
    [Header("Button Type")]
    [SerializeField] private bool isDistanceButton = false; // Set to true for distance button, false for draw point button
    private Image _This;
    private bool isActive = false;
    public bool DistanceMode = false;
    public bool DrawPolygonMode = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pentool.SetActive(false);
        _This = this.GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        // Only change color if this is the distance button and it's active
        if(isDistanceButton)
            if(isActive)
            {
                _This.color = new Color32(54, 183, 104, 255);
            } else _This.color = new Color32(255, 255, 255, 255);
        else _This.color = new Color32(54, 183, 104, 255);
    }

    public void distanceButton()
    {
        DrawPolygonMode = false;
        Pentool pentoolScript = pentool.GetComponent<Pentool>();

        if (isActive)
        { 
            pentool.SetActive(false);
            isActive = false;
            DistanceMode = false; // Turn off distance text when deactivating
            // Clear all dots and lines when button becomes inactive
            if (pentoolScript != null)
            {
                pentoolScript.ClearAllDotsAndLines();
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(false);
            }
        }
        else 
        {
            DistanceMode = true; // Turn on distance text when activating distance button
            pentool.SetActive(true);
            isActive = true;
            if (pentoolScript != null)
            {
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(false);
            }
        }
    }
    
    public void drawLineButton()
    {
        DistanceMode = false; // Always turn off distance text for draw point button
        DrawPolygonMode = false;
        Pentool pentoolScript = pentool.GetComponent<Pentool>();

        if (isActive)
        {
            pentool.SetActive(false);
            isActive = false;
            // Clear all dots and lines when button becomes inactive
            if (pentoolScript != null)
            {
                pentoolScript.ClearAllDotsAndLines();
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(false);
            }
        }
        else 
        {
            pentool.SetActive(true);
            isActive = true;
            if (pentoolScript != null)
            {
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(false);
            }
        }
    }

    public void drawPointButton()
    {
        DistanceMode = false;
        DrawPolygonMode = false;
        Pentool pentoolScript = pentool.GetComponent<Pentool>();

        if (isActive)
        {
            pentool.SetActive(false);
            isActive = false;
            if (pentoolScript != null)
            {
                pentoolScript.ClearAllDotsAndLines();
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(false);
            }
        }
        else
        {
            pentool.SetActive(true);
            isActive = true;
            if (pentoolScript != null)
            {
                pentoolScript.SetDrawPointsOnly(true);
                pentoolScript.SetPolygonMode(false);
            }
        }
    }

    public void drawPolygonButton()
    {
        DistanceMode = false;
        DrawPolygonMode = true;
        Pentool pentoolScript = pentool.GetComponent<Pentool>();

        if (isActive)
        {
            pentool.SetActive(false);
            isActive = false;
            DrawPolygonMode = false;
            if (pentoolScript != null)
            {
                pentoolScript.ClearAllDotsAndLines();
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(false);
            }
        }
        else
        {
            pentool.SetActive(true);
            isActive = true;
            if (pentoolScript != null)
            {
                pentoolScript.SetDrawPointsOnly(false);
                pentoolScript.SetPolygonMode(true);
            }
        }
    }
}
