using UnityEngine;

public class ToggleButton : MonoBehaviour
{
    public GameObject[] targets;   // B, C, D

    public void ToggleGroup()
    {
        // cek kondisi pertama dari target
        bool shouldActivate = !targets[0].activeSelf;

        // set semua berdasarkan kondisi itu
        foreach (GameObject obj in targets)
        {
            obj.SetActive(shouldActivate);
        }
    }
}
