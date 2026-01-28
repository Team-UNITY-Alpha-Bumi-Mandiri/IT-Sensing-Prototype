using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems; // WAJIB untuk Raycast
using TMPro; 
using UnityEngine.InputSystem; 

public class MapSearchSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container; 

    [Header("UI Input")]
    public InputField searchInput; 
    public Button searchButton;

    [Header("Prefabs")]
    public GameObject markerPrefab; 
    public GameObject bubblePrefab; 

    // State
    private GameObject currentMarker;
    private GameObject currentBubble;
    private double targetLat;
    private double targetLon;
    private bool hasTarget = false;

    // Cache untuk Sync Peta
    private double lastLat;
    private double lastLon;
    private int lastZoom;

    void Start()
    {
        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchClicked);
        
        ClearSearchResults();
    }

    void Update()
    {
        // ====================================================================
        // LOGIKA INTERAKSI: Hapus Marker (Fixed)
        // ====================================================================
        if (hasTarget && Mouse.current != null)
        {
            // Deteksi Awal Klik Kiri atau Scroll Mouse
            bool isClicking = Mouse.current.leftButton.wasPressedThisFrame;
            bool isScrolling = Mouse.current.scroll.ReadValue().y != 0;

            if (isClicking || isScrolling)
            {
                // Cek apakah yang diklik BOLEH menghapus marker?
                if (ShouldClearMarker())
                {
                    ClearSearchResults();
                }
            }
        }

        // ====================================================================
        // LOGIKA SYNC MARKER
        // ====================================================================
        if (!hasTarget || currentMarker == null) return;

        bool mapChanged = (mapController.latitude != lastLat) || 
                          (mapController.longitude != lastLon) || 
                          (mapController.zoom != lastZoom);

        if (mapChanged)
        {
            UpdateMarkerPosition();
            lastLat = mapController.latitude;
            lastLon = mapController.longitude;
            lastZoom = mapController.zoom;
        }
    }

    // FUNGSI CERDAS: Menentukan apakah klik ini harus menghapus marker
    bool ShouldClearMarker()
    {
        // 1. Tembakkan Raycast ke posisi mouse
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Mouse.current.position.ReadValue();
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // 2. Jika mouse mengenai sesuatu (UI)
        if (results.Count > 0)
        {
            GameObject topObj = results[0].gameObject;

            // JANGAN HAPUS jika yang diklik adalah:
            // a. Kolom Input Search
            if (IsPartOf(topObj, searchInput.gameObject)) return false;
            
            // b. Tombol Search
            if (IsPartOf(topObj, searchButton.gameObject)) return false;
            
            // c. Bubble Info itu sendiri (biar bisa dibaca/dicopy)
            if (IsPartOf(topObj, currentBubble)) return false;

            // Jika yang diklik adalah Peta (InputArea), TileContainer, atau background kosong lain -> HAPUS!
            return true;
        }

        // 3. Jika mouse tidak mengenai UI sama sekali (klik ruang hampa) -> HAPUS!
        return true;
    }

    // Helper untuk mengecek parent (apakah obj adalah anak dari parent?)
    bool IsPartOf(GameObject obj, GameObject parent)
    {
        return parent != null && (obj == parent || obj.transform.IsChildOf(parent.transform));
    }

    void OnSearchClicked()
    {
        if (searchInput == null) return;
        string query = searchInput.text;
        if (string.IsNullOrEmpty(query)) return;

        StartCoroutine(SearchRoutine(query));
    }

    IEnumerator SearchRoutine(string query)
    {
        string url = $"https://photon.komoot.io/api/?limit=1&q={UnityWebRequest.EscapeURL(query)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                ParseAndGoToLocation(req.downloadHandler.text);
            }
        }
    }

    void ParseAndGoToLocation(string json)
    {
        PhotonResponse res = JsonUtility.FromJson<PhotonResponse>(json);

        if (res != null && res.features != null && res.features.Length > 0)
        {
            var feature = res.features[0];
            double lon = feature.geometry.coordinates[0];
            double lat = feature.geometry.coordinates[1];
            string address = BuildAddress(feature.properties);

            mapController.GoToLocation(lat, lon, 15); 
            ShowMarker(lat, lon, address);
        }
    }

    public void ShowMarker(double lat, double lon, string info) 
    {
        ClearSearchResults();

        hasTarget = true;
        targetLat = lat;
        targetLon = lon;

        currentMarker = Instantiate(markerPrefab, container);
        currentBubble = Instantiate(bubblePrefab, container);
        
        TMP_Text tmpText = currentBubble.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = info;
        }
        else
        {
            Text legacyText = currentBubble.GetComponentInChildren<Text>();
            if (legacyText != null) legacyText.text = info;
        }

        UpdateMarkerPosition();
    }

    void UpdateMarkerPosition()
    {
        if (!hasTarget || currentMarker == null) return;
        Vector2 pos = mapController.LatLonToLocalPosition(targetLat, targetLon);
        currentMarker.GetComponent<RectTransform>().anchoredPosition = pos;
        currentBubble.GetComponent<RectTransform>().anchoredPosition = pos + new Vector2(0, 60);
    }

    public void ClearSearchResults() 
    {
        if (currentMarker != null) Destroy(currentMarker);
        if (currentBubble != null) Destroy(currentBubble);
        hasTarget = false;
    }

    // JSON Helper
    [System.Serializable] class PhotonResponse { public PhotonFeature[] features; }
    [System.Serializable] class PhotonFeature { public PhotonGeometry geometry; public PhotonProperties properties; }
    [System.Serializable] class PhotonGeometry { public double[] coordinates; }
    [System.Serializable] class PhotonProperties
    {
        public string name; public string housenumber; public string street; 
        public string district; public string city; public string postcode; 
        public string state; public string country;
    }

    string BuildAddress(PhotonProperties p)
    {
        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(p.name)) parts.Add(p.name);
        string streetLine = "";
        if (!string.IsNullOrEmpty(p.street)) streetLine += p.street;
        if (!string.IsNullOrEmpty(p.housenumber)) streetLine += " " + p.housenumber;
        if (!string.IsNullOrEmpty(streetLine)) parts.Add(streetLine);
        if (!string.IsNullOrEmpty(p.district)) parts.Add(p.district);
        if (!string.IsNullOrEmpty(p.city)) parts.Add(p.city);
        if (!string.IsNullOrEmpty(p.state)) parts.Add(p.state);
        if (!string.IsNullOrEmpty(p.postcode)) parts.Add(p.postcode);
        if (!string.IsNullOrEmpty(p.country)) parts.Add(p.country);
        return string.Join(", ", parts);
    }
}