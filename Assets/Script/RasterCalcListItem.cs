using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;

public class RasterCalcListItem : MonoBehaviour
{
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textDate;
    public Button btnLoad;
    public Button btnDelete;

    public void Setup(string name, string date, UnityAction onLoad, UnityAction onDelete)
    {
        if (textName) textName.text = name;
        if (textDate) textDate.text = date;

        if (btnLoad)
        {
            btnLoad.onClick.RemoveAllListeners();
            btnLoad.onClick.AddListener(onLoad);
        }

        if (btnDelete)
        {
            btnDelete.onClick.RemoveAllListeners();
            btnDelete.onClick.AddListener(onDelete);
        }
    }
}
