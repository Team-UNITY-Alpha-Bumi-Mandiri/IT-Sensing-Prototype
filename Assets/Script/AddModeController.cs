using UnityEngine;
using UnityEngine.UI;

public class AddModeController : MonoBehaviour
{
    public Button[] targetButtons;   // 4 button yang dikontrol

    private bool isAddModeActive = false;
    private Button thisButton;

    private Color activeColor = new Color(0.2f, 0.5f, 1f); // biru
    private Color defaultColor;

    void Start()
    {
        thisButton = GetComponent<Button>();
        defaultColor = thisButton.image.color;

        SetButtonsInteractable(false);
    }

    public void ToggleAddMode()
    {
        isAddModeActive = !isAddModeActive;

        SetButtonsInteractable(isAddModeActive);

        thisButton.image.color = isAddModeActive ? activeColor : defaultColor;
    }

    void SetButtonsInteractable(bool state)
    {
        foreach (Button btn in targetButtons)
        {
            btn.interactable = state;
        }
    }
}
