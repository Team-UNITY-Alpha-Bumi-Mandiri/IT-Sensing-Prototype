using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;

public class MeasureAreaTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container; 

    [Header("Prefabs")]
    public GameObject pointPrefab; 
    public GameObject linePrefab;  
    public GameObject tooltipPrefab; 
    public GameObject areaFillPrefab; 

    [Header("Settings")]
    public Color drawingColor = Color.white;  
    public Color finishedColor = new Color(1f, 0.92f, 0.016f, 0.5f); // Kuning Transparan
    public float closeSnapDistancePixels = 30f; 

    // State
    private bool isActive = false;
    private bool isComplete = false;
    private List<Vector2> waypoints = new List<Vector2>(); 
    private List<GameObject> spawnedVisuals = new List<GameObject>(); 

    // Visual Objects
    private GameObject ghostLineObj;
    private RectTransform ghostLineRect;
    private GameObject tooltipObj;
    private TMP_Text tooltipText;
    private RectTransform tooltipRect;
    
    // FILL OBJECT (Disimpan agar bisa diupdate tiap frame)
    private GameObject currentFillObj; 
    private PolygonRenderer currentPolyRenderer;

    // Cache Sync Peta
    private double lastLat, lastLon;
    private int lastZoom;

    void Start()
    {
        if (container != null)
        {
            container.gameObject.SetActive(false);
            CreateGhostLine();
            CreateTooltip();
        }
    }

    void Update()
    {
        if (!isActive) return;
        if (mapController == null || container == null) return;

        bool mapChanged = (mapController.latitude != lastLat) ||
                          (mapController.longitude != lastLon) ||
                          (mapController.zoom != lastZoom);

        if (mapChanged)
        {
            RebuildVisuals();
            lastLat = mapController.latitude;
            lastLon = mapController.longitude;
            lastZoom = mapController.zoom;
        }

        if (!isComplete)
        {
            HandleInput();
        }
        else
        {
            if (tooltipObj != null) tooltipObj.SetActive(false);
            if (ghostLineObj != null) ghostLineObj.SetActive(false);
        }
    }

    void HandleInput()
    {
        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (ghostLineObj == null) CreateGhostLine();
        if (tooltipObj == null) CreateTooltip();

        // 1. UPDATE VISUAL FILL & GHOST LINE (REAL-TIME)
        Vector2 mouseLatLon = mapController.ScreenToLatLon(mousePos);
        bool canClose = false;

        // --- PREVIEW FILL AREA (Fitur Baru) ---
        // Kita kirim posisi mouse saat ini sebagai "ujung" sementara poligon
        DrawAreaFill(mouseLatLon);

        if (tooltipObj != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localMouse);
            tooltipRect.anchoredPosition = localMouse + new Vector2(20, -20);
            tooltipObj.SetActive(true);

            string msg = "Click to start drawing shape.";
            
            if (waypoints.Count > 0)
            {
                msg = "Click to continue drawing shape.";
                
                // Cek Snap Close (Minimal 3 titik total termasuk mouse cursor jika waypoints count >= 2)
                if (waypoints.Count >= 3)
                {
                    Vector2 startPos = mapController.LatLonToLocalPosition(waypoints[0].x, waypoints[0].y);
                    if (Vector2.Distance(startPos, localMouse) < closeSnapDistancePixels)
                    {
                        msg = "Click first point to close this shape.";
                        canClose = true;
                    }
                }
            }
            tooltipText.text = msg;
            tooltipObj.transform.SetAsLastSibling(); 

            // Update Ghost Line
            if (waypoints.Count > 0 && ghostLineObj != null)
            {
                Vector2 lastPoint = waypoints[waypoints.Count - 1];
                Vector2 targetForGhost = mouseLatLon;
                
                if (canClose) targetForGhost = waypoints[0]; 

                DrawLineUI(ghostLineRect, lastPoint, targetForGhost, drawingColor);
                ghostLineObj.SetActive(true);
                ghostLineObj.transform.SetAsLastSibling();
            }
            else if (ghostLineObj != null)
            {
                ghostLineObj.SetActive(false);
            }
        }

        // --- KLIK KIRI ---
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Cek Close Shape
            if (waypoints.Count >= 3)
            {
                Vector2 startScreenPos = mapController.LatLonToLocalPosition(waypoints[0].x, waypoints[0].y);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localM);

                if (Vector2.Distance(startScreenPos, localM) < closeSnapDistancePixels)
                {
                    FinishShape();
                    return;
                }
            }

            AddPoint(mouseLatLon);
        }
    }

    void AddPoint(Vector2 latLon)
    {
        waypoints.Add(latLon);
        // RebuildVisuals akan menggambar ulang titik & garis statis
        RebuildVisuals(); 
    }

    void FinishShape()
    {
        isComplete = true;
        
        if (ghostLineObj != null) ghostLineObj.SetActive(false);
        if (tooltipObj != null) tooltipObj.SetActive(false);
        
        RebuildVisuals();
        
        Debug.Log("Area selesai dibuat!");
    }

    // ========================================================================
    // LOGIKA FILL (REAL-TIME UPDATE)
    // ========================================================================
    
    // Parameter 'dynamicTip' adalah posisi kursor mouse.
    // Jika null, berarti kita hanya menggambar waypoints yang sudah fix.
    void DrawAreaFill(Vector2? dynamicTip = null)
    {
        // Hitung total titik yang tersedia
        int totalPoints = waypoints.Count + (dynamicTip.HasValue ? 1 : 0);

        // Jika kurang dari 3 titik, sembunyikan fill dan return
        if (totalPoints < 3) 
        {
            if (currentFillObj != null) currentFillObj.SetActive(false);
            return;
        }

        // 1. Pastikan Objek Fill Ada
        if (currentFillObj == null)
        {
            currentFillObj = new GameObject("GeneratedAreaFill");
            currentFillObj.transform.SetParent(container, false);
            
            currentFillObj.AddComponent<CanvasRenderer>();
            currentPolyRenderer = currentFillObj.AddComponent<PolygonRenderer>();

            RectTransform rect = currentFillObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            currentPolyRenderer.raycastTarget = false;
        }

        currentFillObj.SetActive(true);

        // 2. Susun Daftar Titik (Fixed Waypoints + Mouse Tip)
        List<Vector2> localPoints = new List<Vector2>();
        
        // Masukkan titik yang sudah diklik
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector2 pos = mapController.LatLonToLocalPosition(waypoints[i].x, waypoints[i].y);
            localPoints.Add(pos);
        }

        // Masukkan titik mouse (jika ada dan belum selesai)
        if (dynamicTip.HasValue && !isComplete)
        {
            Vector2 mouseLocal = mapController.LatLonToLocalPosition(dynamicTip.Value.x, dynamicTip.Value.y);
            localPoints.Add(mouseLocal);
        }

        // 3. Update Polygon Renderer
        if (currentPolyRenderer != null)
        {
            currentPolyRenderer.SetPoints(localPoints, finishedColor);
        }

        // Taruh di belakang garis
        currentFillObj.transform.SetAsFirstSibling();
    }

    // ========================================================================
    // VISUALISASI TITIK & GARIS (STATIS)
    // ========================================================================
    public void RebuildVisuals()
    {
        // Hapus visual lama (KECUALI Fill, Fill dihandle terpisah agar performa bagus)
        foreach (var obj in spawnedVisuals) { if (obj != null) Destroy(obj); }
        spawnedVisuals.Clear();

        if (waypoints.Count == 0) return;

        Color useColor = isComplete ? finishedColor : drawingColor;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector2 currentPos = mapController.LatLonToLocalPosition(waypoints[i].x, waypoints[i].y);
            GameObject p = Instantiate(pointPrefab, container);
            p.GetComponent<RectTransform>().anchoredPosition = currentPos;
            
            Image pImg = p.GetComponent<Image>();
            if (pImg) 
            {
                pImg.color = useColor;
                pImg.raycastTarget = false; 
            }
            spawnedVisuals.Add(p);

            if (i > 0)
            {
                DrawSegment(waypoints[i - 1], waypoints[i], useColor);
            }
        }

        // Jika selesai, tutup loop & gambar fill final (tanpa mouse tip)
        if (isComplete)
        {
            if(waypoints.Count > 2)
                DrawSegment(waypoints[waypoints.Count - 1], waypoints[0], useColor);
            
            DrawAreaFill(null); // Gambar fill statis
        }
        else
        {
            // Jika belum selesai, RebuildVisuals dipanggil saat AddPoint
            // Kita biarkan HandleInput yang mengurus DrawAreaFill(mousePos)
        }
    }

    void DrawSegment(Vector2 start, Vector2 end, Color c)
    {
        GameObject line = Instantiate(linePrefab, container);
        DrawLineUI(line.GetComponent<RectTransform>(), start, end, c);
        spawnedVisuals.Add(line);
    }

    void DrawLineUI(RectTransform lineRect, Vector2 startLatLon, Vector2 endLatLon, Color c)
    {
        if (lineRect == null) return;

        Vector2 startPos = mapController.LatLonToLocalPosition(startLatLon.x, startLatLon.y);
        Vector2 endPos = mapController.LatLonToLocalPosition(endLatLon.x, endLatLon.y);

        Vector2 dir = endPos - startPos;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRect.anchoredPosition = startPos + (dir * 0.5f);
        lineRect.sizeDelta = new Vector2(dist, isComplete ? 5f : 3f);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
        
        Image img = lineRect.GetComponent<Image>();
        if (img) 
        {
            img.color = c;
            img.raycastTarget = false;
        }
    }

    public void ToggleAreaTool(bool status)
    {
        isActive = status;
        if (container == null) return;
        container.gameObject.SetActive(status);

        if (status)
        {
            if (isComplete)
            {
                isComplete = false;
                waypoints.Clear();
                RebuildVisuals();
                // Bersihkan fill saat reset
                if (currentFillObj != null) Destroy(currentFillObj);
                currentFillObj = null;
            }
            CreateGhostLine();
            CreateTooltip();
        }
        else
        {
            if (ghostLineObj != null) ghostLineObj.SetActive(false);
            if (tooltipObj != null) tooltipObj.SetActive(false);
        }
    }

    void CreateGhostLine()
    {
        if (container == null || ghostLineObj != null || linePrefab == null) return;
        ghostLineObj = Instantiate(linePrefab, container);
        ghostLineRect = ghostLineObj.GetComponent<RectTransform>();
        Image img = ghostLineObj.GetComponent<Image>();
        if (img)
        {
            img.color = new Color(drawingColor.r, drawingColor.g, drawingColor.b, 0.5f);
            img.raycastTarget = false; 
        }
        ghostLineObj.SetActive(false);
    }

    void CreateTooltip()
    {
        if (container == null || tooltipObj != null || tooltipPrefab == null) return;
        tooltipObj = Instantiate(tooltipPrefab, container);
        tooltipRect = tooltipObj.GetComponent<RectTransform>();
        tooltipText = tooltipObj.GetComponentInChildren<TMP_Text>();
        if(tooltipObj.GetComponent<Image>()) tooltipObj.GetComponent<Image>().raycastTarget = false;
        if(tooltipText != null) tooltipText.raycastTarget = false;
        tooltipObj.SetActive(false);
    }
}