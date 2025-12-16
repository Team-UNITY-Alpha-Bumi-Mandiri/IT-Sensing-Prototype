using UnityEngine;
using UnityEngine.UI;

// Script sederhana untuk toggle (on/off) GameObject UI
public class ButtonCustom : MonoBehaviour
{
    private bool isActive;
    [SerializeField] private GameObject button;

    // Toggle status aktif/non-aktif object target
    public void CreateButtonActive()
    {
        isActive = !isActive;
        button.SetActive(isActive);
    }
}