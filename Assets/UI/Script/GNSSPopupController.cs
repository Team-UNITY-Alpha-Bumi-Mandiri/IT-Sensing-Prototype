using UnityEngine;
using UnityEngine.UI;

public class GNSSPopupController : MonoBehaviour
{
    public Button cancelButton;
    public Button executeButton;
    public string popupTitle;

    void Start()
    {
        RefreshListeners();
    }

    public void Setup(Button cancel, Button execute, string title)
    {
        cancelButton = cancel;
        executeButton = execute;
        popupTitle = title;
        
        if (gameObject.activeInHierarchy)
            RefreshListeners();
    }

    private void RefreshListeners()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => {
                Debug.Log($"Closing {popupTitle}");
                Destroy(gameObject);
            });
        }

        if (executeButton != null)
        {
            executeButton.onClick.RemoveAllListeners();
            executeButton.onClick.AddListener(() => {
                Debug.Log($"Executing {popupTitle}...");
            });
        }
    }
}
