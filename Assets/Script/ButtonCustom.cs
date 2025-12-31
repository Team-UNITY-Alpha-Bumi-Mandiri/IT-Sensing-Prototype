using UnityEngine;
using UnityEngine.UI;

// =========================================
// Script untuk toggle show/hide objek UI
// =========================================
public class ButtonCustom : MonoBehaviour
{
    // Objek yang akan di-toggle (drag dari Inspector)
    [SerializeField]
    GameObject target;

    // Referensi ke Toggle UI (drag dari Inspector)
    [SerializeField]
    Toggle toggleComponent;

    // Panggil fungsi ini untuk toggle show/hide
    public void ToggleVisibility()
    {
        if (target != null)
        {
            target.SetActive(!target.activeSelf);
        }
    }

    public void ToggleColor()
    {
        Image img = target.GetComponent<Image>();

        if (toggleComponent.isOn)
            img.color = new Color32(0x36, 0xB7, 0x68, 255); // #36B768
        else
            img.color = new Color32(0xFF, 0xFF, 0xFF, 255); // #FFFFFF
    }
}