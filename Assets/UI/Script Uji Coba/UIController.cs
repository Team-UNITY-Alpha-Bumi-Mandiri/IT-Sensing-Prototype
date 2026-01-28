
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Handles input (drag, zoom) and connects UI to MapCore
public class UIController : MonoBehaviour
{
    public MapCore mapCore;
    public InputField searchField;
    public Button searchButton;
    public RectTransform marker;
    public GameObject infoBubble;
    public Text infoText;

    private bool dragging = false;
    private Vector2 lastMouse;

    void Start()
    {
        if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // Drag handling
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            lastMouse = Mouse.current.position.ReadValue();
        }
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            dragging = false;
        }

        if (dragging)
        {
            Vector2 now = Mouse.current.position.ReadValue();
            Vector2 delta = now - lastMouse;
            lastMouse = now;

            // Convert pixel delta to lat/lon via mapCore helper
            if (mapCore != null)
                ProcessDrag(delta);
        }

        // Zoom (wheel)
        Vector2 scroll = Mouse.current.scroll.ReadValue();
        if (Mathf.Abs(scroll.y) > 0.1f)
        {
            if (mapCore != null)
            {
                if (scroll.y > 0) mapCore.SetZoom(mapCore.zoom + 1);
                else mapCore.SetZoom(mapCore.zoom - 1);
            }
        }
    }

    void ProcessDrag(Vector2 delta)
    {
        // non-inverted: dragging moves the world in same direction
        double n = Math.Pow(2, mapCore.zoom);
        double degPerTile = 360.0 / n;
        double tileFractionX = delta.x / mapCore.tileSize;
        double tileFractionY = delta.y / mapCore.tileSize;

        double newLon = mapCore.longitude + tileFractionX * degPerTile;
        double latRad = mapCore.latitude * Mathf.Deg2Rad;
        double scale = Math.Cos(latRad);
        if (scale <= 0) scale = 0.0001;
        double newLat = mapCore.latitude - tileFractionY * degPerTile * scale;

        mapCore.SetLatLon(newLat, newLon, true);
    }

    void OnSearchClicked()
    {
        if (searchField == null) return;
        string q = searchField.text.Trim();
        if (string.IsNullOrEmpty(q)) return;
        StartCoroutine(DoSearch(q));
    }

    IEnumerator DoSearch(string q)
    {
        string url = $"https://photon.komoot.io/api/?limit=1&q={UnityEngine.Networking.UnityWebRequest.EscapeURL(q)}";
        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
            var res = JsonUtility.FromJson<PhotonResponse>(req.downloadHandler.text);
            if (res == null || res.features == null || res.features.Length == 0) yield break;
            var f = res.features[0];
            double lon = f.geometry.coordinates[0];
            double lat = f.geometry.coordinates[1];
            mapCore.SetLatLon(lat, lon, true);
            if (marker != null) marker.gameObject.SetActive(true);
            if (infoBubble != null) infoBubble.SetActive(false);
            if (infoText != null) infoText.text = BuildFullAddress(f.properties);
            yield break;
        }
    }

    // Photon POI classes (simple)
    [Serializable] public class PhotonResponse { public PhotonFeature[] features; }
    [Serializable] public class PhotonFeature { public PhotonGeometry geometry; public PhotonProperties properties; }
    [Serializable] public class PhotonGeometry { public double[] coordinates; }
    [Serializable] public class PhotonProperties
    {
        public string name; public string street; public string housenumber; public string city;
        public string postcode; public string county; public string state; public string country;
    }
    string BuildFullAddress(PhotonProperties p)
    {
        if (p == null) return "";
        var list = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(p.name)) list.Add(p.name);
        string st = "";
        if (!string.IsNullOrEmpty(p.street)) st += p.street;
        if (!string.IsNullOrEmpty(p.housenumber)) st += " " + p.housenumber;
        if (st.Length > 0) list.Add(st);
        if (!string.IsNullOrEmpty(p.city)) list.Add(p.city);
        if (!string.IsNullOrEmpty(p.postcode)) list.Add(p.postcode);
        if (!string.IsNullOrEmpty(p.state)) list.Add(p.state);
        if (!string.IsNullOrEmpty(p.country)) list.Add(p.country);
        return string.Join(", ", list);
    }
}
