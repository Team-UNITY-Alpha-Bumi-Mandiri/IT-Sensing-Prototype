using UnityEngine;
using TMPro;

public class SplitViewTool : MonoBehaviour
{
   public TiffLayerManager tiffManager;
    public GameObject layerContainer;
    public TMP_Dropdown dropdownLeft, dropdownRight;
    GameObject layerLeft, layerRight;
    public GameObject divider;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
      //  tiffManager = tiffManager.GetComponent<TiffLayerManager>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SplitviewApply()
    {

    }

    public void SplitviewCancel()
    {

    }
}
