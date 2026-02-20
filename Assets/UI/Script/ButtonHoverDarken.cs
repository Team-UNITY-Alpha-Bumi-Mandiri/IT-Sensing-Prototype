using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoverDarken : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image targetImage;
    [Range(0f, 1f)] public float hoverMultiplier = 0.9f;
    [Range(0f, 1f)] public float pressedMultiplier = 0.8f;

    Color baseColor;

    void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
            baseColor = targetImage.color;
    }

    void OnEnable()
    {
        if (targetImage != null)
            targetImage.color = baseColor;
    }

    Color Multiply(Color c, float m)
    {
        Color result = c * m;
        result.a = c.a;
        return result;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = Multiply(baseColor, hoverMultiplier);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = baseColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = Multiply(baseColor, pressedMultiplier);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetImage != null)
            targetImage.color = Multiply(baseColor, hoverMultiplier);
    }
}

