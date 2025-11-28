using UnityEngine;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    public GameObject tooltipObject;
    public Text tooltipText;

    private void Awake()
    {
        Instance = this;
        tooltipObject.SetActive(false);
    }

    public void Show(string message, Vector3 position)
    {
        tooltipText.text = message;
        tooltipObject.transform.position = position;
        tooltipObject.SetActive(true);
    }

    public void Hide()
    {
        tooltipObject.SetActive(false);
    }
}
