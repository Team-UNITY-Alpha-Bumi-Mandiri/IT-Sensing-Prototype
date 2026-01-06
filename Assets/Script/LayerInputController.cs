using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class LayerInputController : MonoBehaviour
{
    [Header("External References")]
    public ProjectManager projectManager;
    public TMP_Dropdown modeDropdown;
    public TMP_InputField layerNameInput;
    public TextMeshProUGUI targetLayerLabel; // Text to be updated "Layer : ..."

    [Header("Settings")]
    public string newOptionName = "New"; // Option text that enables this button

    private Button myButton;

    void Start()
    {
        myButton = GetComponent<Button>();
        myButton.onClick.AddListener(OnSubmit);

        // Auto-find ProjectManager if not assigned
        if (projectManager == null)
        {
            projectManager = FindObjectOfType<ProjectManager>();
        }
    }

    void Update()
    {
        if (myButton == null) return;

        bool isValid = CheckConditions();
        myButton.interactable = isValid;
    }

    bool CheckConditions()
    {
        // 1. Check Project Selected
        if (projectManager == null || projectManager.GetCurrentProject() == null)
            return false;

        // 2. Check Dropdown is selected to "New"
        if (modeDropdown == null)
            return false;
        
        // Safety check index
        if (modeDropdown.value < 0 || modeDropdown.value >= modeDropdown.options.Count)
            return false;

        string selectedOption = modeDropdown.options[modeDropdown.value].text;
        if (selectedOption != newOptionName)
            return false;

        // 3. Check Input Field not empty
        if (layerNameInput == null || string.IsNullOrEmpty(layerNameInput.text))
            return false;

        return true;
    }

    public void OnSubmit()
    {
        if (layerNameInput != null && targetLayerLabel != null)
        {
            // Format requested: "Layer : @inputfield"
            targetLayerLabel.text = "Layer : " + layerNameInput.text;
            Debug.Log($"[LayerInputController] Updated Label: {targetLayerLabel.text}");

            // Add new property to Current Project and turn it ON
            if (projectManager != null)
            {
                projectManager.AddProperty(layerNameInput.text, true);
            }
        }
    }
}
