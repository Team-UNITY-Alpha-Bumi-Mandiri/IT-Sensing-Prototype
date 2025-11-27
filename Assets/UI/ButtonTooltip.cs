using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string tooltipMessage;
    public float hoverDelay = 0.5f; // detik sebelum tooltip muncul

    private bool isHovering = false;
    private Coroutine delayRoutine;

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        delayRoutine = StartCoroutine(ShowTooltipAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (delayRoutine != null)
            StopCoroutine(delayRoutine);

        TooltipManager.Instance.Hide();
    }

    IEnumerator ShowTooltipAfterDelay()
    {
        yield return new WaitForSeconds(hoverDelay);

        if (isHovering)
        {
            // posisi di bawah button
            Vector3 buttonPos = transform.position;
            Vector3 tooltipPos = buttonPos + new Vector3(0, -40, 0); 
            TooltipManager.Instance.Show(tooltipMessage, tooltipPos);
        }
    }
}
