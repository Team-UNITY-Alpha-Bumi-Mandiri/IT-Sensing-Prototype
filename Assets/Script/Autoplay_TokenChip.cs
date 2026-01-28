using TMPro;
using UnityEngine;

public class Autoplay_TokenChip : MonoBehaviour
{
    AutoplayTool autoToolScript;
    TMP_Text thisChipName;

    void Awake()
    {
        thisChipName = GetComponentInChildren<TMP_Text>();
    }

    public void SetToolScript(AutoplayTool tooSCript)
    {
        autoToolScript = tooSCript;
    }

    public void DeleteSelfAndTellToolScriptToChangeTheWorld()
    {
        autoToolScript.DeleteChip(thisChipName.text);
        Destroy(gameObject);
    }
}