using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NewProjectModal : MonoBehaviour
{
    [Header("Dependencies")]
    public DropdownFilter dropdownFilter;
    
    [Header("UI Fields")]
    // Anda perlu InputField untuk Nama Proyek (asumsi)
    public TMP_InputField projectNameInputField; 
    public TMP_Dropdown typeDropdown;
    public TMP_InputField outputInputField; 

    [Header("Buttons")]
    public Button submitButton;
    public Button closeButton; // Tombol X di header

    private GameObject modalPanel;

    void Awake()
    {
        modalPanel = gameObject;
    }

    void Start()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);
        
        if (closeButton != null)
            closeButton.onClick.AddListener(HideModal);
        
        HideModal();
    }
    
    // ======================================
    // FUNGSI PUBLIK
    // ======================================
    
    public void ShowModal(bool isRename = false, string currentName = "")
    {
        // Logika tampilan untuk Create vs Rename
        if (isRename)
        {
            // Jika ini Rename, isi kolom nama dengan nama proyek saat ini
            if (projectNameInputField != null) projectNameInputField.text = currentName;
            
            // Dalam kasus Rename, Type dan Output biasanya dinonaktifkan/disembunyikan
            if (typeDropdown != null) typeDropdown.gameObject.SetActive(false);
            if (outputInputField != null) outputInputField.gameObject.SetActive(false);
            
            // TODO: Ganti teks judul modal menjadi "Rename Project"
        }
        else
        {
            // Jika ini Create
            if (projectNameInputField != null) projectNameInputField.text = "New Project"; 
            if (outputInputField != null) outputInputField.text = "";
            if (typeDropdown != null) typeDropdown.value = 0;
            
            // Pastikan Type dan Output terlihat
            if (typeDropdown != null) typeDropdown.gameObject.SetActive(true);
            if (outputInputField != null) outputInputField.gameObject.SetActive(true);
            
            // TODO: Ganti teks judul modal menjadi "New Project"
        }
        
        modalPanel.SetActive(true);
    }

    public void HideModal()
    {
        modalPanel.SetActive(false);
    }
    
    // ======================================
    // LISTENER
    // ======================================
    
    void OnSubmitClicked()
    {
        string projectName = (projectNameInputField != null) ? projectNameInputField.text.Trim() : "";
        string projectType = (typeDropdown != null) ? typeDropdown.options[typeDropdown.value].text : "Default";
        string projectOutput = (outputInputField != null) ? outputInputField.text.Trim() : "";
        
        if (string.IsNullOrWhiteSpace(projectName))
        {
            Debug.LogWarning("Nama Proyek tidak boleh kosong!");
            return;
        }

        if (dropdownFilter != null)
        {
            // Ini adalah logika untuk Create/Rename, perlu diperiksa apakah ini mode Rename
            if (typeDropdown.gameObject.activeSelf) // Jika Type terlihat, ini mode Create
            {
                dropdownFilter.CreateNewProject(projectName, projectType, projectOutput);
            }
            else // Jika Type tidak terlihat, ini mode Rename
            {
                dropdownFilter.RenameCurrentProject(projectName); // Gunakan projectName sebagai newName
            }
        }

        HideModal();
    }
}