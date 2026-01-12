using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MeasureTool2 : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container; 

    [Header("Prefabs")]
    public GameObject pointPrefab;      
    public GameObject linePrefab;       
    public GameObject intervalPrefab;   
    public GameObject labelPrefab;      

    [Header("Settings")]
    public Color activeColor = new Color(1f, 0f, 0.5f); 
    public float doubleClickThreshold = 0.3f; // Waktu toleransi double click

    // State
    private bool isActive = false;
    private bool isLocked = false; // BARU: Status apakah pengukuran sudah selesai/terkunci
    private float lastClickTime = 0f; // BARU: Untuk deteksi double click

    private List<Vector2> waypoints = new List<Vector2>(); 
    private List<GameObject> spawnedObjects = new List<GameObject>(); 
    
    // Ghost Objects
    private GameObject ghostLineObj;
    private RectTransform ghostLineRect;
    private GameObject ghostLabelObj;
    private Text ghostLabelText;

    // Cache Sync
    private double lastLat;
    private double lastLon;
    private int lastZoom;

    void Start()
    {
        if (container != null) 
        {
            CreateGhostObjects();
            container.gameObject.SetActive(false); // Sembunyikan saat awal
        }
    }

    void Update()
    {
        if (!isActive) return;
        if (mapController == null || container == null) return;

        // 1. SYNC POSISI: Update posisi visual saat peta bergerak
        bool mapChanged = (mapController.latitude != lastLat) || 
                          (mapController.longitude != lastLon) || 
                          (mapController.zoom != lastZoom);

        if (mapChanged)
        {
            RebuildAllVisuals();
            lastLat = mapController.latitude;
            lastLon = mapController.longitude;
            lastZoom = mapController.zoom;
        }

        // 2. INPUT MOUSE (Hanya jika belum locked/selesai)
        HandleInput();
    }

    void HandleInput()
    {
        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (ghostLineObj == null || ghostLabelObj == null) CreateGhostObjects();

        // --- LOGIKA GHOST LINE (Hanya muncul jika BELUM selesai/Locked) ---
        if (!isLocked && waypoints.Count > 0 && ghostLineObj != null)
        {
            Vector2 lastPointLatLon = waypoints[waypoints.Count - 1];
            Vector2 mouseLatLon = mapController.ScreenToLatLon(mousePos);
            
            float dist = CalculateDistance(lastPointLatLon, mouseLatLon);
            float totalDist = GetTotalDistance() + dist;

            DrawLineUI(ghostLineRect, lastPointLatLon, mouseLatLon);
            ghostLineObj.SetActive(true);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localMouse))
            {
                ghostLabelObj.GetComponent<RectTransform>().anchoredPosition = localMouse + new Vector2(20, 20);
                ghostLabelText.text = $"{totalDist:F2} km";
                ghostLabelObj.SetActive(true);
            }
        }
        else
        {
            // Sembunyikan ghost jika locked atau belum ada titik
            if (ghostLineObj != null) ghostLineObj.SetActive(false);
            if (ghostLabelObj != null) ghostLabelObj.SetActive(false);
        }

        // --- KLIK KIRI ---
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Jika sudah locked, klik tidak melakukan apa-apa (atau bisa buat logika reset)
            if (isLocked) return;

            float timeSinceLastClick = Time.time - lastClickTime;
            lastClickTime = Time.time;

            // DETEKSI DOUBLE CLICK
            if (timeSinceLastClick <= doubleClickThreshold)
            {
                // Double Click = Finish / Lepas dari kursor
                FinishMeasurement();
            }
            else
            {
                // Single Click = Tambah Titik
                Vector2 latLon = mapController.ScreenToLatLon(mousePos);
                AddWaypoint(latLon);
            }
        }
    }

    void FinishMeasurement()
    {
        isLocked = true; // Kunci alat ukur
        
        // Sembunyikan Ghost segera
        if (ghostLineObj != null) ghostLineObj.SetActive(false);
        if (ghostLabelObj != null) ghostLabelObj.SetActive(false);

        Debug.Log("Measurement Finished (Locked). Toggle off/on to reset.");
    }

    void AddWaypoint(Vector2 latLon)
    {
        waypoints.Add(latLon);
        RebuildAllVisuals(); 
    }

    public void RebuildAllVisuals()
    {
        foreach (var obj in spawnedObjects) { if (obj != null) Destroy(obj); }
        spawnedObjects.Clear();

        if (waypoints.Count == 0) return;

        float totalDistance = 0;
        int nextIntervalKm = 1;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector2 currentPos = mapController.LatLonToLocalPosition(waypoints[i].x, waypoints[i].y);
            GameObject p = Instantiate(pointPrefab, container);
            p.GetComponent<RectTransform>().anchoredPosition = currentPos;
            spawnedObjects.Add(p);

            if (i > 0)
            {
                Vector2 prevLatLon = waypoints[i - 1];
                Vector2 currLatLon = waypoints[i];
                
                float segmentDist = CalculateDistance(prevLatLon, currLatLon);
                
                GameObject line = Instantiate(linePrefab, container);
                DrawLineUI(line.GetComponent<RectTransform>(), prevLatLon, currLatLon);
                spawnedObjects.Add(line);

                float distSoFar = totalDistance;
                float distAfter = totalDistance + segmentDist;

                while (nextIntervalKm < distAfter)
                {
                    float remaining = nextIntervalKm - distSoFar;
                    float t = remaining / segmentDist; 
                    
                    Vector2 intervalLatLon = Vector2.Lerp(prevLatLon, currLatLon, t);
                    Vector2 intervalPos = mapController.LatLonToLocalPosition(intervalLatLon.x, intervalLatLon.y);

                    GameObject marker = Instantiate(intervalPrefab, container);
                    marker.GetComponent<RectTransform>().anchoredPosition = intervalPos;
                    Text tLabel = marker.GetComponentInChildren<Text>();
                    if(tLabel) tLabel.text = $"{nextIntervalKm} km";
                    
                    spawnedObjects.Add(marker);
                    nextIntervalKm++;
                }

                totalDistance += segmentDist;

                GameObject label = Instantiate(labelPrefab, container);
                label.GetComponent<RectTransform>().anchoredPosition = currentPos + new Vector2(0, -25);
                Text lText = label.GetComponentInChildren<Text>();
                if(lText) lText.text = $"{totalDistance:F2} km";
                spawnedObjects.Add(label);
            }
        }
        
        // Pastikan Ghost di atas
        if (ghostLineObj != null) ghostLineObj.transform.SetAsLastSibling();
        if (ghostLabelObj != null) ghostLabelObj.transform.SetAsLastSibling();
    }

    void DrawLineUI(RectTransform lineRect, Vector2 startLatLon, Vector2 endLatLon)
    {
        if (lineRect == null) return;
        Vector2 startPos = mapController.LatLonToLocalPosition(startLatLon.x, startLatLon.y);
        Vector2 endPos = mapController.LatLonToLocalPosition(endLatLon.x, endLatLon.y);
        Vector2 dir = endPos - startPos;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        lineRect.anchoredPosition = startPos + (dir * 0.5f);
        lineRect.sizeDelta = new Vector2(dist, lineRect.sizeDelta.y);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void ToggleMeasure(bool status)
    {
        isActive = status;

        if (container == null)
        {
            Debug.LogError("MeasureContainer hilang!");
            return;
        }

        container.gameObject.SetActive(status);
        
        if (status)
        {
            // Reset state saat dinyalakan kembali
            isLocked = false; 
            waypoints.Clear(); // Bersihkan titik lama agar mulai baru (atau hapus ini jika ingin lanjut)
            RebuildAllVisuals(); // Bersihkan visual lama

            CreateGhostObjects();
            lastLat = -999; 
        }
        else
        {
            // Saat dimatikan
            if (ghostLineObj != null) ghostLineObj.SetActive(false);
            if (ghostLabelObj != null) ghostLabelObj.SetActive(false);
        }
    }

    void CreateGhostObjects()
    {
        if (container == null) return;
        if (ghostLineObj == null && linePrefab != null)
        {
            ghostLineObj = Instantiate(linePrefab, container);
            ghostLineRect = ghostLineObj.GetComponent<RectTransform>();
            Image img = ghostLineObj.GetComponent<Image>();
            if(img) 
            {
                img.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.5f);
                img.raycastTarget = false;
            }
            ghostLineObj.SetActive(false);
        }
        if (ghostLabelObj == null && labelPrefab != null)
        {
            ghostLabelObj = Instantiate(labelPrefab, container);
            ghostLabelText = ghostLabelObj.GetComponentInChildren<Text>();
            Image img = ghostLabelObj.GetComponent<Image>();
            if(img) img.raycastTarget = false;
            ghostLabelObj.SetActive(false);
        }
    }

    float CalculateDistance(Vector2 p1, Vector2 p2)
    {
        float R = 6371f; 
        float dLat = (p2.x - p1.x) * Mathf.Deg2Rad;
        float dLon = (p2.y - p1.y) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat/2) * Mathf.Sin(dLat/2) +
                  Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon/2) * Mathf.Sin(dLon/2);
        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1-a));
        return R * c;
    }
    
    float GetTotalDistance()
    {
        float total = 0;
        for(int i=1; i<waypoints.Count; i++) total += CalculateDistance(waypoints[i-1], waypoints[i]);
        return total;
    }
}