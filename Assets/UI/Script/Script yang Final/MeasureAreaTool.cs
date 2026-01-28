using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

public class MeasureAreaTool : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleMapController_Baru mapController;
    public RectTransform container;

    [Header("UI References")]
    public GameObject infoBoxPanel;      // Panel Kotak Putih di kiri bawah
    public TextMeshProUGUI infoBoxText;  // Teks di dalam kotak tersebut

    [Header("Prefabs")]
    public GameObject pointPrefab;
    public GameObject linePrefab;
    public GameObject tooltipPrefab;
    public GameObject measurementLabelPrefab; // Label kecil di atas garis
    public GameObject centerLabelPrefab;      // (BARU) Label Area di tengah Polygon

    [Header("Settings")]
    public Color drawingColor = Color.white;
    public Color finishedColor = new Color(1f, 0.92f, 0.016f, 0.5f); // Kuning Transparan
    public float closeSnapDistancePixels = 30f;

    // STATE
    private bool isActive = false;
    private bool isComplete = false;
    private List<Vector2> waypoints = new List<Vector2>();
    private List<GameObject> spawnedVisuals = new List<GameObject>();

    // HELPER
    private GameObject ghostLineObj;
    private GameObject tooltipObj;
    private TMP_Text tooltipText;
    private GameObject currentFillObj;
    private PolygonRenderer currentPolyRenderer;

    // CACHE
    private double lastLat, lastLon;
    private int lastZoom;

    void Start()
    {
        if (container != null)
        {
            container.gameObject.SetActive(true); // Pastikan container siap
            container.gameObject.SetActive(false); // Lalu sembunyikan
            CreateGhostLine();
            CreateTooltip();
        }
        UpdateInfoBoxUI();
    }

    void Update()
    {
        if (!isActive) return;

        // SYNC PETA
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

        // INPUT
        if (!isComplete) HandleInput();
        else
        {
            if (tooltipObj) tooltipObj.SetActive(false);
            if (ghostLineObj) ghostLineObj.SetActive(false);
        }
    }

    void HandleInput()
    {
        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (!ghostLineObj) CreateGhostLine();
        if (!tooltipObj) CreateTooltip();

        Vector2 mouseLatLon = mapController.ScreenToLatLon(mousePos);
        DrawAreaFill(mouseLatLon);

        // Update Tooltip
        if (tooltipObj != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localMouse);
            tooltipObj.GetComponent<RectTransform>().anchoredPosition = localMouse + new Vector2(20, -20);
            tooltipObj.SetActive(true);
            tooltipText.text = "Click to add point";

            if (waypoints.Count >= 3)
            {
                Vector2 startScreen = mapController.LatLonToLocalPosition(waypoints[0].x, waypoints[0].y);
                if (Vector2.Distance(startScreen, localMouse) < closeSnapDistancePixels)
                {
                    tooltipText.text = "Click Start to Finish";
                }
            }
            tooltipObj.transform.SetAsLastSibling();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (waypoints.Count >= 3)
            {
                Vector2 startScreen = mapController.LatLonToLocalPosition(waypoints[0].x, waypoints[0].y);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(container, mousePos, null, out Vector2 localM);

                if (Vector2.Distance(startScreen, localM) < closeSnapDistancePixels)
                {
                    FinishShape();
                    return;
                }
            }
            AddPoint(mouseLatLon);
        }
    }

    void AddPoint(Vector2 latLon) { waypoints.Add(latLon); RebuildVisuals(); }

    void FinishShape()
    {
        isComplete = true;
        RebuildVisuals();
        UpdateInfoBoxUI();
    }

    // =================================================================
    // LOGIKA UI UTAMA (FORMAT TEKS PERSIS REFERENSI)
    // =================================================================
    void UpdateInfoBoxUI()
    {
        if (infoBoxPanel == null || infoBoxText == null) return;

        infoBoxPanel.SetActive(isActive);

        if (!isComplete || waypoints.Count < 3)
        {
            // Tampilan Awal (Kosong)
            infoBoxText.text = "<b><u>Measurement</u></b>\n\nArea & Perimeter\nwill appear here.";
        }
        else
        {
            float perimeter = CalculatePerimeter();
            float area = CalculateArea();

            // Format Angka: "N2" artinya angka dengan koma pemisah ribuan dan 2 digit desimal
            // Contoh: 155,489,393.23

            //D: pemotongan ke kilometer
            string sArea, sPerim;
            float trArea, trPerim;

            if (area > 999999)
            {
                trArea = area / 1_000_000f;
                sArea = $"{trArea:N2} km²";
            }
            else
                sArea = $"{area:N2} m²";

            if (perimeter > 999999)
            {
                trPerim = perimeter / 1_000_000f;
                sPerim = $"{trPerim:N2} km²";
            }
            else
                sPerim = $"{perimeter:N2} m²";

            //  string sArea = $"{area:N2} m²";
            //  string sPerim = $"{perimeter:N2} m";

            // FORMAT HTML TEXT MESH PRO
            // <u> = Underline, <b> = Bold
            infoBoxText.text = $"<b><u>Measurement</u></b>\n\n" +
                               $"<b>Area:</b>\n{sArea}\n\n" +
                               $"<b>Perimeter :</b>\n{sPerim}";
        }
    }

    // =================================================================
    // LOGIKA MATEMATIKA
    // =================================================================
    float CalculatePerimeter()
    {
        float total = 0;
        for (int i = 0; i < waypoints.Count; i++)
            total += CalculateDistanceMeters(waypoints[i], waypoints[(i + 1) % waypoints.Count]);
        return total;
    }

    float CalculateArea()
    {
        float area = 0;
        Vector2 origin = waypoints[0];
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector2 p1 = waypoints[i];
            Vector2 p2 = waypoints[(i + 1) % waypoints.Count];
            Vector2 m1 = LatLonToMetersRelative(p1, origin);
            Vector2 m2 = LatLonToMetersRelative(p2, origin);
            area += (m1.x * m2.y) - (m1.y * m2.x);
        }
        return Mathf.Abs(area / 2.0f);
    }

    Vector2 LatLonToMetersRelative(Vector2 p, Vector2 origin)
    {
        float x = CalculateDistanceMeters(new Vector2(origin.x, origin.y), new Vector2(origin.x, p.y));
        float y = CalculateDistanceMeters(new Vector2(origin.x, origin.y), new Vector2(p.x, origin.y));
        if (p.y < origin.y) x = -x;
        if (p.x < origin.x) y = -y;
        return new Vector2(x, y);
    }

    float CalculateDistanceMeters(Vector2 p1, Vector2 p2)
    {
        float R = 6371000f;
        float dLat = (p2.x - p1.x) * Mathf.Deg2Rad;
        float dLon = (p2.y - p1.y) * Mathf.Deg2Rad;
        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) + Mathf.Cos(p1.x * Mathf.Deg2Rad) * Mathf.Cos(p2.x * Mathf.Deg2Rad) * Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);
        return R * 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
    }

    // =================================================================
    // VISUALISASI (LABEL MIRING & TENGAH)
    // =================================================================
    public void RebuildVisuals()
    {
        foreach (var obj in spawnedVisuals) { if (obj != null) Destroy(obj); }
        spawnedVisuals.Clear();

        if (waypoints.Count == 0) return;
        Color useColor = isComplete ? finishedColor : drawingColor;

        Vector2 centerSum = Vector2.zero; // Untuk mencari titik tengah polygon

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector2 currentPos = mapController.LatLonToLocalPosition(waypoints[i].x, waypoints[i].y);
            GameObject p = Instantiate(pointPrefab, container);
            p.GetComponent<RectTransform>().anchoredPosition = currentPos;
            spawnedVisuals.Add(p);

            centerSum += currentPos;

            if (i > 0) DrawSegment(waypoints[i - 1], waypoints[i], useColor);
        }

        if (isComplete)
        {
            if (waypoints.Count > 2) DrawSegment(waypoints[waypoints.Count - 1], waypoints[0], useColor);
            DrawAreaFill(null);

            // --- BARU: LABEL AREA DI TENGAH POLYGON ---
            if (centerLabelPrefab != null)
            {
                Vector2 centroid = centerSum / waypoints.Count; // Rata-rata posisi (sederhana)
                GameObject centerLbl = Instantiate(centerLabelPrefab, container);
                centerLbl.GetComponent<RectTransform>().anchoredPosition = centroid;

                float areaKm = CalculateArea() / 1000000f;
                // Format: "155 km2"
                centerLbl.GetComponentInChildren<TextMeshProUGUI>().text = $"{areaKm:N0} km\u00B2";
                spawnedVisuals.Add(centerLbl);
            }
        }
        else DrawAreaFill(null);
    }

    void DrawSegment(Vector2 start, Vector2 end, Color c)
    {
        Vector2 startPos = mapController.LatLonToLocalPosition(start.x, start.y);
        Vector2 endPos = mapController.LatLonToLocalPosition(end.x, end.y);
        Vector2 dir = endPos - startPos;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float distPixel = dir.magnitude;

        // Gambar Garis
        GameObject line = Instantiate(linePrefab, container);
        RectTransform rt = line.GetComponent<RectTransform>();
        rt.anchoredPosition = startPos + (dir * 0.5f);
        rt.sizeDelta = new Vector2(distPixel, 3f);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        line.GetComponent<Image>().color = c;
        spawnedVisuals.Add(line);

        // Gambar Label Jarak (Hanya jika Selesai)
        if (isComplete && measurementLabelPrefab != null)
        {
            GameObject label = Instantiate(measurementLabelPrefab, container);
            RectTransform labelRT = label.GetComponent<RectTransform>();
            labelRT.anchoredPosition = startPos + (dir * 0.5f);

            // Rotasi label mengikuti garis (Anti terbalik)
            float textAngle = angle;
            if (textAngle > 90 || textAngle < -90) textAngle += 180;
            labelRT.localRotation = Quaternion.Euler(0, 0, textAngle);

            float distKm = CalculateDistanceMeters(start, end) / 1000f;
            label.GetComponentInChildren<TextMeshProUGUI>().text = $"{distKm:F1} km"; // Contoh: "21.2 km"
            spawnedVisuals.Add(label);
        }
    }

    void DrawAreaFill(Vector2? dynamicTip = null)
    {
        int totalPoints = waypoints.Count + (dynamicTip.HasValue ? 1 : 0);
        if (totalPoints < 3) { if (currentFillObj) currentFillObj.SetActive(false); return; }
        if (!currentFillObj)
        {
            currentFillObj = new GameObject("GeneratedAreaFill");
            currentFillObj.transform.SetParent(container, false);
            currentFillObj.AddComponent<CanvasRenderer>();
            currentPolyRenderer = currentFillObj.AddComponent<PolygonRenderer>();
            currentPolyRenderer.material = new Material(Shader.Find("UI/Default"));
            currentPolyRenderer.raycastTarget = false;
        }
        currentFillObj.SetActive(true);
        List<Vector2> pts = new List<Vector2>();
        foreach (var p in waypoints) pts.Add(mapController.LatLonToLocalPosition(p.x, p.y));
        if (dynamicTip.HasValue && !isComplete) pts.Add(mapController.LatLonToLocalPosition(dynamicTip.Value.x, dynamicTip.Value.y));
        if (currentPolyRenderer) currentPolyRenderer.SetPoints(pts, finishedColor);
        currentFillObj.transform.SetAsFirstSibling();
    }

    void CreateGhostLine() { if (!ghostLineObj) { ghostLineObj = Instantiate(linePrefab, container); ghostLineObj.GetComponent<Image>().raycastTarget = false; ghostLineObj.SetActive(false); } }
    void CreateTooltip() { if (!tooltipObj) { tooltipObj = Instantiate(tooltipPrefab, container); tooltipText = tooltipObj.GetComponentInChildren<TMP_Text>(); tooltipObj.SetActive(false); } }
    public void ToggleAreaTool(bool status) { isActive = status; if (container) container.gameObject.SetActive(status); if (infoBoxPanel) infoBoxPanel.SetActive(status); if (status) { isComplete = false; waypoints.Clear(); UpdateInfoBoxUI(); RebuildVisuals(); CreateGhostLine(); CreateTooltip(); } }
}