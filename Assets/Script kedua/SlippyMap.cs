using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

[RequireComponent(typeof(MeshRenderer))]
public class SlippyMap : MonoBehaviour
{
    public int zoom = 17;
    public int tileX = 108595;
    public int tileY = 63248;
    public string tileUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    MeshRenderer mr;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
    }

    void Start()
    {
        StartCoroutine(LoadTile());
    }

    IEnumerator LoadTile()
    {
        string url = tileUrl.Replace("{z}", zoom.ToString())
                            .Replace("{x}", tileX.ToString())
                            .Replace("{y}", tileY.ToString());

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                mr.material.mainTexture = tex;
                Debug.Log("Tile loaded: " + url);
            }
            else
            {
                Debug.LogError("Tile load error: " + req.error + " | url: " + url);
            }
        }
    }
}
