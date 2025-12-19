using UnityEngine;

// =========================================
// Script untuk toggle show/hide objek UI
// =========================================
public class ButtonCustom : MonoBehaviour
{
    // Objek yang akan di-toggle (drag dari Inspector)
    [SerializeField] 
    GameObject target;

    // Panggil fungsi ini untuk toggle show/hide
    public void Toggle()
    {
        if (target != null)
        {
            target.SetActive(!target.activeSelf);
        }
    }
}