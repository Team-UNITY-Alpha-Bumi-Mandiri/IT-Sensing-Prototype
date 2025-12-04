using UnityEngine;
using UnityEngine.EventSystems;

// Skrip ini mencegah klik pada InputField diteruskan ke elemen parent (Dropdown)
// yang akan memicu penutupan otomatis.

public class StopClickPropagation : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // Mengonsumsi event klik sehingga tidak memicu penutupan Dropdown
        eventData.Use(); 
    }
}