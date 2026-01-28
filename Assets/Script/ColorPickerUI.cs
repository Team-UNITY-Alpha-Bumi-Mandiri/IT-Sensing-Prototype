using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ColorPickerUI : MonoBehaviour
{
    public GameObject panel;
    public Transform content;
    public GameObject buttonPrefab;
    public System.Action<string> onGradientSelected;
    
    // Callback sementara untuk session show kali ini
    private System.Action<Gradient> currentCallback;

    public void Show()
    {
        currentCallback = null;
        if (panel != null) panel.SetActive(true);
        RefreshList();
    }

    // Overload untuk support direct callback
    public void Show(Gradient current, System.Action<Gradient> onSelect)
    {
        currentCallback = onSelect;
        if (panel != null) panel.SetActive(true);
        RefreshList();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    void RefreshList()
    {
        if (content == null || buttonPrefab == null || GradientManager.Instance == null) return;

        foreach (Transform child in content) Destroy(child.gameObject);

        foreach (var preset in GradientManager.Instance.presets)
        {
            var obj = Instantiate(buttonPrefab, content);
            var btn = obj.GetComponent<Button>();
            var txt = obj.GetComponentInChildren<TMPro.TMP_Text>();
            var img = obj.GetComponentInChildren<RawImage>(); // Optional preview

            if (txt != null) txt.text = preset.name;
            
            // Preview Gradient
            if (img != null)
            {
                img.texture = GradientManager.GradientToTexture(preset.gradient);
            }

            if (btn != null)
            {
                string pName = preset.name;
                Gradient pGrad = preset.gradient;

                btn.onClick.AddListener(() => 
                {
                    if (currentCallback != null)
                    {
                        currentCallback.Invoke(pGrad);
                    }
                    else
                    {
                        onGradientSelected?.Invoke(pName);
                    }
                    Hide();
                });
            }
        }
    }
}
