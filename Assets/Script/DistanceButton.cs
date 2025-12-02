using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DistanceButton : MonoBehaviour
{
    [SerializeField] private GameObject pentool;
    private Image _This;
    public bool isActive = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pentool.SetActive(false);
        _This = this.GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        if(isActive)
        {
            _This.color = new Color32(54, 183, 104, 255);
        } else _This.color = new Color32(255, 255, 255, 255);
    }

    public void OnClick()
    {
        if (isActive)
        {
            pentool.SetActive(false);
            isActive = false;
            
            // Clear all dots and lines when button becomes inactive
            Pentool pentoolScript = pentool.GetComponent<Pentool>();
            if (pentoolScript != null)
            {
                pentoolScript.ClearAllDotsAndLines();
            }
        }
        else 
        {
            pentool.SetActive(true);
            isActive = true;
        }
    }
}
