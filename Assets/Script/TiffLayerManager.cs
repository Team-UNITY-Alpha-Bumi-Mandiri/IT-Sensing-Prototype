using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using BitMiracle.LibTiff.Classic;

// ============================================================
// TiffLayerManager - Manager untuk GeoTIFF dan PNG Overlay
// ============================================================
// Fungsi utama:
// - Load file GeoTIFF multi-band dan konversi ke texture
// - Load PNG overlay dengan koordinat geografis
// - Sinkronisasi layer visibility dengan PropertyPanel dan ProjectManager
// - Update posisi overlay saat peta bergerak/zoom
// ============================================================
public class TiffLayerManager : MonoBehaviour
{
    [Header("References")]
    public SimpleMapController_Baru mapController;  // Kontrol peta untuk konversi koordinat
    public TMP_InputField namePrefixInput;          // Prefix nama layer (opsional)
    public PropertyPanel propertyPanel;             // Panel toggle property
    public ProjectManager projectManager;           // Manager project untuk sinkronisasi
    public RectTransform overlayContainer;          // Container untuk spawn overlay

    [Header("Settings")]
    public float overlayOpacity = 1f;               // Opacity default overlay
    public List<string> customBandNames;            // Nama band custom dari Inspector

    [Header("Enhancement")]
    public EnhancementTool enhanceTool;             // Tool enhancement (brightness, contrast, dll)
    public Material enhanceMat;                     // Material untuk enhancement shader
    public Shader colorRampShader;                  // Shader baru untuk Color Ramp
    public ColorPickerUI colorPicker;               // UI untuk memilih warna
    public LegendController legendController;       // Controller legend

    // Data internal
    public List<LayerData> layers = new();                      // Layer aktif saat ini
    Dictionary<string, List<LayerData>> layerCache = new();     // Cache layer per TIFF path
    List<GameObject> overlays = new();                          // GameObject overlay di scene
    string currentTiffPath = "";                                // Path TIFF yang sedang aktif

    // Bounds geografis dari GeoTIFF
    double geoMinLat, geoMaxLat, geoMinLon, geoMaxLon;
    int imageWidth, imageHeight;
    bool hasGeoData = false;

    // Cache posisi peta untuk deteksi perubahan
    double lastMapLat, lastMapLon;
    int lastMapZoom;

    // Struktur data untuk menyimpan info layer
    [System.Serializable]
    public class LayerData
    {
        public string name;         // Nama layer (ditampilkan di PropertyPanel)
        public string path;         // Path file fisik (untuk rename/delete)
        public Texture2D texture;   // Texture hasil load
        public bool isVisible;      // Status visibility
        public float minVal, maxVal; // Range nilai data
        public Gradient currentGradient; // Gradient aktif (null = grayscale)
        public bool isSingleBand;
        public float[] rawData;      // Data normalized 0-1 untuk coloring
        public int width, height;
    }

    // ============================================================
    // LIFECYCLE
    // ============================================================

    void Start()
    {
        // Subscribe ke event PropertyPanel dan ProjectManager
        propertyPanel?.onPropertyChanged.AddListener(OnPropertyToggle);
        propertyPanel?.onListRefreshed.AddListener(OnPropertyPanelRefreshed); // [NEW] Sync UI saat panel refresh
        projectManager?.onTiffProjectLoaded.AddListener(OnTiffProjectLoaded);
        
        // Inisialisasi Shader Gradient
        if (colorRampShader == null) colorRampShader = Shader.Find("Unlit/Texture"); // Fallback
        
        // Auto-find LegendController jika belum diassign
        if (legendController == null) legendController = FindObjectOfType<LegendController>();
        
        // Auto-find ColorPickerUI jika belum diassign
        if (colorPicker == null) colorPicker = FindObjectOfType<ColorPickerUI>();

        // Subscribe color picker request
        if (legendController != null)
        {
            legendController.onColorPickerRequest = OnLegendColorPickerRequest;
        }
        
        // Pastikan container selalu aktif
        if (overlayContainer != null) overlayContainer.gameObject.SetActive(true);
    }

    void Update()
    {
        // Skip jika tidak ada data geo atau mapController
        if (!hasGeoData || mapController == null) return;

        // Cek apakah peta bergerak/zoom
        bool mapChanged = mapController.latitude != lastMapLat ||
                         mapController.longitude != lastMapLon ||
                         mapController.zoom != lastMapZoom;

        if (mapChanged)
        {
            // Update posisi semua overlay
            UpdateAllOverlayPositions();
            lastMapLat = mapController.latitude;
            lastMapLon = mapController.longitude;
            lastMapZoom = mapController.zoom;
        }
    }

    void OnDestroy() => ClearLayers();

    // ============================================================
    // PNG OVERLAY - Load PNG dengan bounds geografis manual
    // ============================================================

    [System.Serializable]
    public class LayerMeta { public float min_val; public float max_val; public bool is_single_band; }

    // Load PNG sebagai overlay dengan koordinat geografis
    // Params:
    //   pngPath     - Path ke file PNG
    //   north/south - Latitude bounds (derajat)
    //   west/east   - Longitude bounds (derajat)
    //   isPreview   - True jika ini preview sementara (tidak disimpan ke project)
    //   clearExisting - True untuk hapus layer sebelumnya
    //   isSingleBand - True jika ini adalah layer single band (NDVI, etc)
    //   realMin/realMax - Range nilai asli data (jika ada)
    public void LoadPngOverlay(string pngPath, double north, double south, double west, double east, bool isPreview = false, bool clearExisting = true, string customLayerName = "", bool isSingleBand = false, float realMin = 0, float realMax = 0)
    {
        // Try load metadata from sidecar if defaults
        if (Mathf.Abs(realMin) < 0.0001f && Mathf.Abs(realMax) < 0.0001f)
        {
             string jsonPath = pngPath + ".json";
             if (!File.Exists(jsonPath)) jsonPath = Path.ChangeExtension(pngPath, ".json");
             if (File.Exists(jsonPath))
             {
                 try {
                     var meta = JsonUtility.FromJson<LayerMeta>(File.ReadAllText(jsonPath));
                     realMin = meta.min_val;
                     realMax = meta.max_val;
                     isSingleBand = meta.is_single_band;
                     Debug.Log($"[TiffLayerManager] Loaded metadata from {jsonPath}: Min={realMin}, Max={realMax}, SingleBand={isSingleBand}");
                 } catch {}
             }
        }

        if (!File.Exists(pngPath)) { Debug.LogError($"[TiffLayerManager] PNG tidak ditemukan: {pngPath}"); return; }

        // Tentukan nama layer
        string layerName = isPreview ? "PREVIEW_SATELIT" : 
                          (!string.IsNullOrEmpty(customLayerName) ? customLayerName : Path.GetFileNameWithoutExtension(pngPath));
        
        // Hapus preview lama jika ada
        if (layerName == "PREVIEW_SATELIT") RemoveLayer("PREVIEW_SATELIT");
        else if (layers.Exists(l => l.name == layerName)) return; // Skip jika sudah ada

        if (clearExisting) ClearLayers();
        currentTiffPath = pngPath;

        // Load texture dari file
        byte[] fileData = File.ReadAllBytes(pngPath);
        // [IMPORTANT] Use Linear texture for scientific data to avoid sRGB->Linear conversion in GetPixels()
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        
        if (!tex.LoadImage(fileData)) return;

        // Set texture settings
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();

        // Extract raw data (normalized 0-1 from grayscale)
            var pixels = tex.GetPixels();
            var rawData = new float[pixels.Length];
            
            // Gunakan range asli dari Python jika tersedia
            bool useRealRange = Mathf.Abs(realMax - realMin) > 0.0001f;
            float calcMin = float.MaxValue;
            float calcMax = float.MinValue;

            for(int i=0; i<pixels.Length; i++) 
            {
                float val = pixels[i].r;
                rawData[i] = val;
                
                if (!useRealRange)
                {
                    if (val < calcMin) calcMin = val;
                    if (val > calcMax) calcMax = val;
                }
            }

            float finalMin = useRealRange ? realMin : calcMin;
            float finalMax = useRealRange ? realMax : calcMax;

            // Safety check
            if (finalMin > finalMax) { finalMin = 0; finalMax = 1; }
            if (Mathf.Approximately(finalMin, finalMax)) { finalMax = finalMin + 0.01f; }

            // Simpan bounds geografis
            geoMaxLat = north; geoMinLat = south;
            geoMinLon = west; geoMaxLon = east;
            imageWidth = tex.width; imageHeight = tex.height;
            hasGeoData = true;

            // Buat layer data
            var newLayer = new LayerData { 
                name = layerName, 
                texture = tex, 
                isVisible = true, 
                path = pngPath, 
                isSingleBand = isSingleBand,
                rawData = rawData,
                minVal = finalMin,
                maxVal = finalMax,
                width = tex.width,
                height = tex.height
            };

            // Assign default gradient for single band layers
            if (isSingleBand)
            {
                if (GradientManager.Instance == null) GradientManager.Instance = FindObjectOfType<GradientManager>();
                
                if (GradientManager.Instance != null)
                {
                    if (layerName.ToUpper().Contains("NDVI")) 
                        newLayer.currentGradient = GradientManager.Instance.GetGradient("NDVI");
                    else if (layerName.ToUpper().Contains("THERMAL"))
                        newLayer.currentGradient = GradientManager.Instance.GetGradient("Thermal");
                    else
                        newLayer.currentGradient = GradientManager.Instance.GetGradient("Grayscale");
                }
                else
                {
                    Debug.LogWarning("[TiffLayerManager] GradientManager Instance not found!");
                }
            }

            Debug.Log($"[TiffLayerManager] Loaded Layer: {layerName}, isSingleBand: {isSingleBand}, Range: {finalMin}-{finalMax}");
            layers.Add(newLayer);

            // Tampilkan di peta
            if (isPreview) ShowLayerOnMap(newLayer);
            else 
            { 
                ForceCreateOverlayObject(newLayer); 
                SyncWithProject(); 
                SetupLayerUI(newLayer);

                // Apply gradient awal jika ada
                if (newLayer.currentGradient != null)
                {
                    ApplyGradientToLayer(newLayer, newLayer.currentGradient);
                }
            }

        // Navigasi ke lokasi overlay
        if (mapController != null)
        {
            double cLat = (geoMinLat + geoMaxLat) / 2;
            double cLon = (geoMinLon + geoMaxLon) / 2;
            mapController.GoToLocation(cLat, cLon, CalculateFitZoom());
        }
    }

    // ============================================================
    // TIFF LOADING - Load GeoTIFF multi-band
    // ============================================================

    // Load file GeoTIFF dan extract semua band sebagai layer terpisah
    public void LoadTiff(string path, bool clearExisting = true)
    {
        if (!File.Exists(path)) { Debug.LogError($"[TiffLayerManager] File tidak ditemukan: {path}"); return; }
        if (clearExisting) ClearLayers();
        currentTiffPath = path;

        // Cek cache - jika sudah pernah load, gunakan dari cache
        if (layerCache.ContainsKey(path))
        {
            layers = layerCache[path];
            foreach (var layer in layers) if (layer.isVisible) ShowLayerOnMap(layer);
            hasGeoData = true;
            SyncWithProject();
            return;
        }

        // Buka dan proses file TIFF
        using (Tiff tiff = Tiff.Open(path, "r"))
        {
            if (tiff == null) { Debug.LogError($"[TiffLayerManager] Gagal membuka TIFF: {path}"); return; }

            // Baca metadata TIFF
            imageWidth = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            imageHeight = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;
            int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 8;
            int planarConfig = tiff.GetField(TiffTag.PLANARCONFIG)?[0].ToInt() ?? 1;
            bool isTiled = tiff.IsTiled();

            // Baca koordinat geografis dari tag GeoTIFF
            ReadGeoTiffTags(tiff);

            // Alokasi array untuk menyimpan data band
            int totalPixels = imageWidth * imageHeight;
            float[][] bandData = new float[samplesPerPixel][];
            float[] minVal = new float[samplesPerPixel];
            float[] maxVal = new float[samplesPerPixel];
            
            for (int b = 0; b < samplesPerPixel; b++)
            {
                bandData[b] = new float[totalPixels];
                minVal[b] = float.MaxValue;
                maxVal[b] = float.MinValue;
            }

            // Baca pixel data berdasarkan format TIFF (tiled atau strip)
            if (isTiled) ReadTiledTiff(tiff, bandData, minVal, maxVal, samplesPerPixel, bitsPerSample, planarConfig);
            else ReadStripTiff(tiff, bandData, minVal, maxVal, samplesPerPixel, bitsPerSample, planarConfig);

            // Buat layer untuk setiap band
            string prefix = GetNamingPrefix();
            for (int i = 0; i < samplesPerPixel; i++)
            {
                string bandName = (customBandNames != null && i < customBandNames.Count) ? customBandNames[i] : "";
                CreateLayerManual(bandData, imageWidth, imageHeight, prefix + bandName, i, minVal[i], maxVal[i]);
            }

            // Buat RGB composite jika ada minimal 3 band
            if (samplesPerPixel >= 3)
                CreateCompositeManual(bandData, imageWidth, imageHeight, prefix + "RGB Composite", minVal, maxVal);

            // Simpan ke cache
            if (!layerCache.ContainsKey(path))
                layerCache.Add(path, new List<LayerData>(layers));
        }

        // Sync dengan project dan navigasi ke lokasi
        SyncWithProject();
        if (hasGeoData && mapController != null)
        {
            double cLat = (geoMinLat + geoMaxLat) / 2;
            double cLon = (geoMinLon + geoMaxLon) / 2;
            mapController.GoToLocation(cLat, cLon, CalculateFitZoom());
        }
    }

    // Baca data dari TIFF format tiled (umum untuk file besar)
    void ReadTiledTiff(Tiff tiff, float[][] bandData, float[] minVal, float[] maxVal, int samples, int bits, int planar)
    {
        int tileW = tiff.GetField(TiffTag.TILEWIDTH)[0].ToInt();
        int tileH = tiff.GetField(TiffTag.TILELENGTH)[0].ToInt();
        int tileSize = tiff.TileSize();
        byte[] buf = new byte[tileSize];

        // Iterasi setiap tile
        for (int y = 0; y < imageHeight; y += tileH)
        {
            for (int x = 0; x < imageWidth; x += tileW)
            {
                if (planar == 1) // Contiguous (RGBRGB...)
                {
                    tiff.ReadTile(buf, 0, x, y, 0, 0);
                    for (int ty = 0; ty < tileH && y + ty < imageHeight; ty++)
                    {
                        for (int tx = 0; tx < tileW && x + tx < imageWidth; tx++)
                        {
                            int pIdx = (y + ty) * imageWidth + (x + tx);
                            for (int s = 0; s < samples; s++)
                            {
                                float val = ReadPixelValue(buf, (ty * tileW + tx) * samples + s, bits, tileSize);
                                bandData[s][pIdx] = val;
                                if (val < minVal[s]) minVal[s] = val;
                                if (val > maxVal[s]) maxVal[s] = val;
                            }
                        }
                    }
                }
                else // Separate (RRR..., GGG..., BBB...)
                {
                    for (int s = 0; s < samples; s++)
                    {
                        tiff.ReadTile(buf, 0, x, y, 0, (short)s);
                        for (int ty = 0; ty < tileH && y + ty < imageHeight; ty++)
                        {
                            for (int tx = 0; tx < tileW && x + tx < imageWidth; tx++)
                            {
                                int pIdx = (y + ty) * imageWidth + (x + tx);
                                float val = ReadPixelValue(buf, ty * tileW + tx, bits, tileSize);
                                bandData[s][pIdx] = val;
                                if (val < minVal[s]) minVal[s] = val;
                                if (val > maxVal[s]) maxVal[s] = val;
                            }
                        }
                    }
                }
            }
        }
    }

    // Baca data dari TIFF format strip (format standar)
    void ReadStripTiff(Tiff tiff, float[][] bandData, float[] minVal, float[] maxVal, int samples, int bits, int planar)
    {
        int scanSize = tiff.ScanlineSize();
        byte[] buf = new byte[scanSize];

        if (planar == 1) // Contiguous
        {
            for (int y = 0; y < imageHeight; y++)
            {
                tiff.ReadScanline(buf, y);
                for (int x = 0; x < imageWidth; x++)
                {
                    int pIdx = y * imageWidth + x;
                    for (int s = 0; s < samples; s++)
                    {
                        float val = ReadPixelValue(buf, x * samples + s, bits, scanSize);
                        bandData[s][pIdx] = val;
                        if (val < minVal[s]) minVal[s] = val;
                        if (val > maxVal[s]) maxVal[s] = val;
                    }
                }
            }
        }
        else // Separate
        {
            for (int s = 0; s < samples; s++)
            {
                for (int y = 0; y < imageHeight; y++)
                {
                    tiff.ReadScanline(buf, y, (short)s);
                    for (int x = 0; x < imageWidth; x++)
                    {
                        int pIdx = y * imageWidth + x;
                        float val = ReadPixelValue(buf, x, bits, scanSize);
                        bandData[s][pIdx] = val;
                        if (val < minVal[s]) minVal[s] = val;
                        if (val > maxVal[s]) maxVal[s] = val;
                    }
                }
            }
        }
    }

    // Baca nilai pixel dari buffer berdasarkan bit depth
    float ReadPixelValue(byte[] buf, int idx, int bits, int maxSize)
    {
        int byteIdx = bits == 8 ? idx : bits == 16 ? idx * 2 : idx * 4;
        if (byteIdx >= maxSize) return 0;

        // Konversi ke float [0-1]
        if (bits == 8) return buf[byteIdx] / 255f;
        if (bits == 16) return System.BitConverter.ToUInt16(buf, byteIdx) / 65535f;
        return System.BitConverter.ToSingle(buf, byteIdx);
    }

    // Dapatkan prefix nama dari project atau input field
    string GetNamingPrefix()
    {
        if (projectManager != null)
        {
            var proj = projectManager.GetCurrentProject();
            if (proj != null && !string.IsNullOrEmpty(proj.tiffPath))
            {
                if (Path.GetFullPath(proj.tiffPath).ToLower() == Path.GetFullPath(currentTiffPath).ToLower())
                    return proj.name + " ";
            }
        }
        if (namePrefixInput != null && !string.IsNullOrEmpty(namePrefixInput.text))
            return namePrefixInput.text + " ";
        return "";
    }

    // Helper to get min/max from a single band TIFF without creating layers
    public bool GetSingleBandMinMax(string path, out float min, out float max)
    {
        min = 0; max = 0;
        if (!File.Exists(path)) return false;

        using (Tiff tiff = Tiff.Open(path, "r"))
        {
            if (tiff == null) return false;

            int w = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int h = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int bits = tiff.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 8;
            bool isTiled = tiff.IsTiled();
            
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            bool anyVal = false;

            if (isTiled)
            {
                 int tileW = tiff.GetField(TiffTag.TILEWIDTH)[0].ToInt();
                 int tileH = tiff.GetField(TiffTag.TILELENGTH)[0].ToInt();
                 int tileSize = tiff.TileSize();
                 byte[] buf = new byte[tileSize];
                 
                 for (int y = 0; y < h; y += tileH)
                 {
                     for (int x = 0; x < w; x += tileW)
                     {
                         tiff.ReadTile(buf, 0, x, y, 0, 0);
                         int actualTileW = System.Math.Min(tileW, w - x);
                         int actualTileH = System.Math.Min(tileH, h - y);
                         
                         for (int ty = 0; ty < actualTileH; ty++)
                         {
                             for (int tx = 0; tx < actualTileW; tx++)
                             {
                                 float val = ReadPixelValue(buf, ty * tileW + tx, bits, tileSize);
                                 // Ignore extreme values if needed (like NaN or -9999)
                                 // But ReadPixelValue returns 0 for NaN usually? No, ToSingle can return NaN.
                                 if (!float.IsNaN(val) && !float.IsInfinity(val))
                                 {
                                     if (val < minVal) minVal = val;
                                     if (val > maxVal) maxVal = val;
                                     anyVal = true;
                                 }
                             }
                         }
                     }
                 }
            }
            else
            {
                int scanSize = tiff.ScanlineSize();
                byte[] buf = new byte[scanSize];
                for (int y = 0; y < h; y++)
                {
                    tiff.ReadScanline(buf, y);
                    for (int x = 0; x < w; x++)
                    {
                        float val = ReadPixelValue(buf, x, bits, scanSize);
                        if (!float.IsNaN(val) && !float.IsInfinity(val))
                        {
                            if (val < minVal) minVal = val;
                            if (val > maxVal) maxVal = val;
                            anyVal = true;
                        }
                    }
                }
            }
            
            if (anyVal)
            {
                min = minVal;
                max = maxVal;
                return true;
            }
        }
        return false;
    }

    // ============================================================
    // GEOTIFF PARSING - Baca koordinat geografis dari tag TIFF
    // ============================================================

    // Baca tag ModelTiepoint dan ModelPixelScale untuk menentukan bounds geografis
    void ReadGeoTiffTags(Tiff tiff)
    {
        hasGeoData = false;

        // Coba baca ModelTiepoint (tag 33922) dan ModelPixelScale (tag 33550)
        var tiepointField = tiff.GetField((TiffTag)33922);
        var scaleField = tiff.GetField((TiffTag)33550);

        if (tiepointField != null && scaleField != null)
        {
            double[] tiepoints = tiepointField[1].ToDoubleArray();
            double[] scales = scaleField[1].ToDoubleArray();

            if (tiepoints?.Length >= 6 && scales?.Length >= 2)
            {
                // Hitung bounds dari tiepoint dan scale
                geoMinLon = tiepoints[3] - tiepoints[0] * scales[0];
                geoMaxLat = tiepoints[4] + tiepoints[1] * scales[1];
                geoMaxLon = geoMinLon + imageWidth * scales[0];
                geoMinLat = geoMaxLat - imageHeight * scales[1];
                hasGeoData = true;
            }
        }

        // Fallback ke ModelTransformation (tag 34264)
        if (!hasGeoData)
        {
            var transformField = tiff.GetField((TiffTag)34264);
            if (transformField != null)
            {
                double[] t = transformField[1].ToDoubleArray();
                if (t?.Length >= 16)
                {
                    geoMinLon = t[3];
                    geoMaxLat = t[7];
                    geoMaxLon = geoMinLon + imageWidth * t[0];
                    geoMinLat = geoMaxLat - imageHeight * System.Math.Abs(t[5]);
                    hasGeoData = true;
                }
            }
        }
    }

    // Hitung zoom level optimal untuk menampilkan seluruh overlay
    public int CalculateFitZoom()
    {
        if (!hasGeoData || overlayContainer == null) return 15;

        double latSpan = geoMaxLat - geoMinLat;
        double lonSpan = geoMaxLon - geoMinLon;
        float cW = overlayContainer.rect.width;
        float cH = overlayContainer.rect.height;

        // Cari zoom level dimana overlay muat di container
        for (int z = 18; z >= 3; z--)
        {
            double dpp = 360.0 / (256.0 * System.Math.Pow(2, z));
            if (lonSpan / dpp < cW * 0.8 && latSpan / dpp < cH * 0.8) return z;
        }
        return 10;
    }

    // Dapatkan center koordinat dari file TIFF tanpa load texture
    public bool GetTiffCenter(string path, out double lat, out double lon)
    {
        lat = lon = 0;
        if (!File.Exists(path)) return false;

        using (Tiff t = Tiff.Open(path, "r"))
        {
            if (t == null) return false;
            imageWidth = t.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            imageHeight = t.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            ReadGeoTiffTags(t);
            if (hasGeoData)
            {
                lat = (geoMinLat + geoMaxLat) / 2;
                lon = (geoMinLon + geoMaxLon) / 2;
                return true;
            }
        }
        return false;
    }

    // Dapatkan bounds dari file TIFF
    public bool GetTiffBounds(string path, out double minLat, out double maxLat, out double minLon, out double maxLon)
    {
        minLat = maxLat = minLon = maxLon = 0;
        if (!File.Exists(path)) return false;

        using (Tiff t = Tiff.Open(path, "r"))
        {
            if (t == null) return false;
            imageWidth = t.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            imageHeight = t.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            ReadGeoTiffTags(t);
            if (hasGeoData)
            {
                minLat = geoMinLat; maxLat = geoMaxLat;
                minLon = geoMinLon; maxLon = geoMaxLon;
                return true;
            }
        }
        return false;
    }

    // ============================================================
    // LAYER CREATION - Buat texture dari data band
    // ============================================================

    // Buat layer grayscale dari satu band
    void CreateLayerManual(float[][] bandData, int w, int h, string name, int ch, float min, float max)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        var rawData = new float[w * h];
        float range = max - min;
        if (range < 0.0001f) range = 1f;

        // Konversi band data ke grayscale dengan normalisasi
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Flip Y karena TIFF origin di top-left, texture di bottom-left
                float val = Mathf.Clamp01((bandData[ch][(h - 1 - y) * w + x] - min) / range);
                pixels[y * w + x] = new Color(val, val, val, 1f);
                rawData[y * w + x] = val;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        var newLayer = new LayerData { 
            name = name, 
            texture = tex, 
            isVisible = false, 
            minVal = min, 
            maxVal = max, 
            isSingleBand = true,
            rawData = rawData,
            width = w,
            height = h
        };
        layers.Add(newLayer);
        SetupLayerUI(newLayer);
    }

    // Buat layer RGB composite dari 3 band pertama
    void CreateCompositeManual(float[][] bandData, int w, int h, string name, float[] min, float[] max)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];

        // Hitung range untuk setiap channel
        float rR = max[0] - min[0], gR = max[1] - min[1], bR = max[2] - min[2];
        if (rR < 0.0001f) rR = 1f;
        if (gR < 0.0001f) gR = 1f;
        if (bR < 0.0001f) bR = 1f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int src = (h - 1 - y) * w + x;
                int dst = y * w + x;
                pixels[dst] = new Color(
                    Mathf.Clamp01((bandData[0][src] - min[0]) / rR),
                    Mathf.Clamp01((bandData[1][src] - min[1]) / gR),
                    Mathf.Clamp01((bandData[2][src] - min[2]) / bR), 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        var newLayer = new LayerData { name = name, texture = tex, isVisible = false, isSingleBand = false };
        layers.Add(newLayer);
        SetupLayerUI(newLayer);
    }

    // ============================================================
    // LAYER MANAGEMENT
    // ============================================================

    // Callback dari ProjectManager saat project dengan TIFF diload
    void OnTiffProjectLoaded(string path)
    {
        var proj = projectManager?.GetCurrentProject();

        if (string.IsNullOrEmpty(path))
        {
            ClearLayers();
            if (proj != null) ScanAndLoadExtraLayers(proj.name, proj.polygonCoords);
            return;
        }

        // Load TIFF jika belum ada atau path berubah
        if (currentTiffPath != path || layers.Count == 0 || overlays.Count == 0)
        {
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png")
            {
                // Handle PNG Overlay (Composite/Raster Result)
                if (proj != null && proj.polygonCoords != null && proj.polygonCoords.Count > 0)
                {
                    double n = double.MinValue, s = double.MaxValue, w = double.MaxValue, e = double.MinValue;
                    foreach (var c in proj.polygonCoords)
                    {
                        if (c.x > n) n = c.x;
                        if (c.x < s) s = c.x;
                        if (c.y > e) e = c.y; // Lon is y? ProjectManager uses x=lat, y=lon
                        if (c.y < w) w = c.y;
                    }
                    
                    // Determine single band status from filename (safe assumption for this project)
                    bool isTCI = Path.GetFileName(path).Contains("_TCI_") || Path.GetFileName(path).Contains("TrueColor");
                    bool isSingleBand = !isTCI;
                    
                    LoadPngOverlay(path, n, s, w, e, false, true, "", isSingleBand);
                }
                else
                {
                    Debug.LogWarning("[TiffLayerManager] PNG loaded without bounds in project. Cannot display overlay.");
                }
            }
            else
            {
                // Handle GeoTIFF
                LoadTiff(path);
            }
        }
        else
            SyncWithProject();

        // Scan dan load layer PNG tambahan (hasil GEE download)
        if (proj != null) ScanAndLoadExtraLayers(proj.name, proj.polygonCoords);
    }

    // Scan folder project untuk PNG tambahan (hasil download GEE)
    // Scan folder project untuk PNG tambahan (hasil download GEE)
    public void ScanAndLoadExtraLayers(string projectName, List<Vector2> coords)
    {
        if (string.IsNullOrEmpty(projectName) || coords == null || coords.Count == 0) return;

        // Hitung bounds dari polygon project
        double n = double.MinValue, s = double.MaxValue, w = double.MaxValue, e = double.MinValue;
        foreach (var c in coords)
        {
            if (c.x > n) n = c.x;
            if (c.x < s) s = c.x;
            if (c.y < w) w = c.y;
            if (c.y > e) e = c.y;
        }

        // Ambil list properties yang valid dari ProjectManager
        var currentProps = projectManager?.GetCurrentProject()?.GetProps();
        
        // Scan folder downloaded_bands/{projectName}
        string targetDir = Path.Combine(Application.streamingAssetsPath, "Backend", "downloaded_bands", projectName);
        if (!Directory.Exists(targetDir)) return;

        foreach (string pngPath in Directory.GetFiles(targetDir, "*.png", SearchOption.AllDirectories))
        {
            if (pngPath.Contains("temp")) continue;
            
            // Tentukan layer name (konsisten dengan GeeDownloadController)
            string layerName = Path.GetFileNameWithoutExtension(pngPath);
            string parentDir = Path.GetFileName(Path.GetDirectoryName(pngPath));
            
            // Cek subfolder (tanggal)
            if (!string.Equals(Path.GetFullPath(Path.GetDirectoryName(pngPath)).TrimEnd('\\', '/'), 
                               Path.GetFullPath(targetDir).TrimEnd('\\', '/'), 
                               System.StringComparison.OrdinalIgnoreCase))
            {
                layerName = parentDir;
            }
            
            string originalName = layerName; // Gunakan ini sebagai identifier layer
            
            // LOGIKA PERSISTENCE:
            // 1. Jika layer ini sudah ada di PropertyPanel (key exists) -> Load (mungkin renamed atau tidak)
            // 2. Jika layer ini TIDAK ada di PropertyPanel -> 
            //    - Cek apakah ini file baru? (belum pernah diload sebelumnya)
            //    - ATAU apakah ini file lama yang sudah didelete user?
            
            bool shouldLoad = false;

            if (currentProps == null || currentProps.Count == 0)
            {
                // Kasus A: Project baru / belum ada property sama sekali -> Load semua
                shouldLoad = true;
            }
            else
            {
                // Kasus B: Cek eksistensi di properties
                // Kita gunakan originalName (subfolder name) sebagai key awal
                if (currentProps.ContainsKey(originalName))
                {
                    shouldLoad = true;
                }
            }

            if (shouldLoad)
            {
                // Cek apakah sudah diload di memory
                if (!layers.Exists(l => l.name == originalName))
                {
                    // Determine single band status from filename
                    bool isTCI = Path.GetFileName(pngPath).Contains("_TCI_") || Path.GetFileName(pngPath).Contains("TrueColor");
                    bool isSingleBand = !isTCI;
                    
                    LoadPngOverlay(pngPath, n, s, w, e, false, false, originalName, isSingleBand);
                }
            }
        }
        
        // Terakhir, sync lagi untuk memastikan visibility benar
        SyncWithProject();
    }

    // Hapus layer dari memory dan peta
    public void RemoveLayer(string name, bool deleteFile = false)
    {
        var layer = layers.Find(l => l.name == name);
        if (layer != null)
        {
            // Hapus FOLDER fisik jika diminta
            if (deleteFile && !string.IsNullOrEmpty(layer.path) && File.Exists(layer.path))
            {
                try
                {
                    string dir = Path.GetDirectoryName(layer.path);
                    // Cek apakah nama folder sama dengan nama layer (validasi extra safety)
                    if (Path.GetFileName(dir) == name)
                    {
                        // Hapus folder beserta isinya
                        Directory.Delete(dir, true);

                        // Hapus meta file folder juga
                        string metaPath = dir + ".meta";
                        if (File.Exists(metaPath)) File.Delete(metaPath);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TiffLayerManager] Delete folder failed: {ex.Message}");
                }
            }

            if (layer.texture != null) Destroy(layer.texture);
            layers.Remove(layer);
        }

        // Hapus overlay GameObject
        var overlay = overlays.Find(o => o != null && o.name == name);
        if (overlay != null)
        {
            overlays.Remove(overlay);
            Destroy(overlay);
        }
    }

    // Rename layer (termasuk folder fisik)
    public void RenameLayer(string oldName, string newName)
    {
        var layer = layers.Find(l => l.name == oldName);
        if (layer == null) return;

        // Rename FOLDER fisik
        if (!string.IsNullOrEmpty(layer.path) && File.Exists(layer.path))
        {
            try
            {
                string oldDir = Path.GetDirectoryName(layer.path);
                // Cek apakah nama folder sama dengan nama layer (validasi extra)
                if (Path.GetFileName(oldDir) == oldName)
                {
                    string parentDir = Path.GetDirectoryName(oldDir); // Folder di atasnya (downloaded_bands/ProjectName)
                    string newDir = Path.Combine(parentDir, newName);

                    if (!Directory.Exists(newDir))
                    {
                        Directory.Move(oldDir, newDir);
                        
                        // Rename .meta file folder jika ada
                        string oldMeta = oldDir + ".meta";
                        string newMeta = newDir + ".meta";
                        if (File.Exists(oldMeta) && !File.Exists(newMeta))
                        {
                            File.Move(oldMeta, newMeta);
                        }
                        
                        // Update path layer ke lokasi baru
                        string fileName = Path.GetFileName(layer.path);
                        layer.path = Path.Combine(newDir, fileName);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TiffLayerManager] Rename folder failed: {ex.Message}");
            }
        }

        // Update nama di memory
        layer.name = newName;

        // Update nama GameObject
        var overlay = overlays.Find(o => o != null && o.name == oldName);
        if (overlay != null) overlay.name = newName;
    }

    // Sinkronisasi layer dengan PropertyPanel dan ProjectManager
    void SyncWithProject()
    {
        if (projectManager == null || projectManager.GetCurrentProject() == null)
        {
            ShowLayersInPanel();
            return;
        }

        var props = projectManager.GetCurrentProject().GetProps();
        foreach (var layer in layers)
        {
            // Skip preview layer
            if (layer.name == "PREVIEW_SATELIT") continue;

            // Tambah property jika belum ada
            if (!props.ContainsKey(layer.name))
                projectManager.AddProperty(layer.name, false);
            else
            {
                // Sync visibility dari project properties
                layer.isVisible = props[layer.name];
                if (layer.isVisible) ShowLayerOnMap(layer);
                else HideLayerFromMap(layer);
            }
        }

        // Re-setup UI (legend button) karena PropertyPanel di-refresh setiap kali AddProperty dipanggil
        foreach (var layer in layers)
        {
            SetupLayerUI(layer);
        }
    }

    // Hapus path dari cache (dipanggil saat delete project)
    public void UnloadFromCache(string path)
    {
        if (!layerCache.ContainsKey(path)) return;
        foreach (var l in layerCache[path])
            if (l.texture != null) Destroy(l.texture);
        layerCache.Remove(path);
    }

    // Hapus semua layer (visual saja, texture di cache tetap aman)
    public void ClearLayers()
    {
        foreach (var o in overlays)
            if (o != null) Destroy(o);
        overlays.Clear();
        layers = new List<LayerData>();
        propertyPanel?.ClearPanel();
        hasGeoData = false;
        currentTiffPath = null;
    }

    // ============================================================
    // OVERLAY DISPLAY - Tampilkan layer di peta
    // ============================================================

    // Tampilkan layer di PropertyPanel (tanpa ProjectManager)
    void ShowLayersInPanel()
    {
        if (propertyPanel == null) return;
        var props = new Dictionary<string, bool>();
        foreach (var l in layers) props[l.name] = l.isVisible;
        propertyPanel.ShowProperties(props);

        // Re-setup UI
        foreach (var layer in layers)
        {
            SetupLayerUI(layer);
        }
    }

    // Toggle visibility layer
    // updateProjectManager: true jika dipanggil dari UI langsung, false jika dari external (Autoplay)
    void SetLayerVisibility(string name, bool value, bool updateProjectManager)
    {
        var layer = layers.Find(l => l.name == name);
        if (layer == null) return;
        layer.isVisible = value;

        if (value)
        {
            if (overlayContainer != null) overlayContainer.gameObject.SetActive(true);
            ShowLayerOnMap(layer);
        }
        else HideLayerFromMap(layer);

        if (projectManager != null)
        {
            if (updateProjectManager) projectManager.OnPropertyChanged(name, value);
            projectManager.SetProjectPolygonVisibility(!layers.Exists(l => l.isVisible));
        }
    }

    // Wrapper dari PropertyPanel (event listener)
    void OnPropertyToggle(string name, bool value) => SetLayerVisibility(name, value, true);

    // Wrapper dari OverlayToggleController / AutoplayTool (sudah update ProjectManager di sana)
    public void OnPropertyToggleExternal(string name, bool value) => SetLayerVisibility(name, value, false);

    // [NEW] Callback saat PropertyPanel selesai refresh list
    void OnPropertyPanelRefreshed()
    {
        Debug.Log("[TiffLayerManager] PropertyPanel refreshed. Re-applying UI setup for all layers.");
        foreach (var layer in layers)
        {
            SetupLayerUI(layer);
        }
    }

    // Tampilkan layer di peta sebagai RawImage overlay
    void ShowLayerOnMap(LayerData layer)
    {
        if (overlayContainer == null || layer.texture == null) return;
        if (!overlayContainer.gameObject.activeSelf) overlayContainer.gameObject.SetActive(true);

        // Cek apakah sudah ada overlay
        var existing = overlays.Find(o => o != null && o.name == layer.name);
        if (existing != null)
        {
            existing.SetActive(true);
            UpdateOverlayPosition(existing);

            // Apply enhancement material jika ada
            RawImage existingRaw = existing.GetComponent<RawImage>();
            if (existingRaw.material != enhanceMat)
            {
                existingRaw.material = Instantiate(enhanceMat);
            }
                enhanceTool.AssignValues(existing, layer.name);

            return;
        }

        // Buat overlay baru
        var overlay = new GameObject(layer.name);
        overlay.transform.SetParent(overlayContainer, false);

        var img = overlay.AddComponent<RawImage>();
        img.texture = layer.texture;
        img.color = new Color(1f, 1f, 1f, overlayOpacity);
        img.material = new Material(Shader.Find("Sprites/Default"));

        UpdateOverlayPosition(overlay);
        overlays.Add(overlay);
    }

    // Buat overlay object (tapi inactive - untuk preload saat load project)
    void ForceCreateOverlayObject(LayerData layer)
    {
        if (overlayContainer == null || layer.texture == null) return;
        if (overlays.Exists(o => o != null && o.name == layer.name)) return;
        CreateOverlayObject(layer);
    }

    // Buat overlay object (internal helper)
    GameObject CreateOverlayObject(LayerData layer)
    {
        if (overlayContainer == null || layer.texture == null) return null;

        var existing = overlays.Find(o => o != null && o.name == layer.name);
        if (existing != null) return existing;

        var overlay = new GameObject(layer.name);
        overlay.transform.SetParent(overlayContainer, false);

        var img = overlay.AddComponent<RawImage>();
        img.texture = layer.texture;
        img.color = new Color(1f, 1f, 1f, overlayOpacity);
        img.material = new Material(Shader.Find("Sprites/Default"));

        UpdateOverlayPosition(overlay);
        overlays.Add(overlay);

        // Default inactive, SyncWithProject yang akan nyalakan jika perlu
        overlay.SetActive(false);
        return overlay;
    }

    // Sembunyikan overlay
    void HideLayerFromMap(LayerData layer)
    {
        var overlay = overlays.Find(o => o != null && o.name == layer.name);
        if (overlay != null) overlay.SetActive(false);
    }

    // Update posisi dan ukuran overlay berdasarkan koordinat geo
    void UpdateOverlayPosition(GameObject overlay)
    {
        if (mapController == null || !hasGeoData) return;

        var rt = overlay.GetComponent<RectTransform>();

        // Konversi koordinat geo ke local position di container
        Vector2 posMin = mapController.LatLonToLocalPosition(geoMinLat, geoMinLon);
        Vector2 posMax = mapController.LatLonToLocalPosition(geoMaxLat, geoMaxLon);

        // Set anchor ke center
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Set posisi dan ukuran
        rt.anchoredPosition = (posMin + posMax) / 2f;
        rt.sizeDelta = new Vector2(Mathf.Abs(posMax.x - posMin.x), Mathf.Abs(posMax.y - posMin.y));
    }

    // Update posisi semua overlay aktif
    void UpdateAllOverlayPositions()
    {
        foreach (var o in overlays)
            if (o != null && o.activeSelf)
                UpdateOverlayPosition(o);
    }

    // Helper untuk mencari nama gradient (agak lambat tapi aman)
    string GetGradientName(Gradient g)
    {
        foreach(var p in GradientManager.Instance.presets)
        {
            if (p.gradient == g) return p.name;
        }
        return "Grayscale";
    }

    // Setup UI (Tombol Plus) untuk layer
    void SetupLayerUI(LayerData layer)
    {
        if (propertyPanel == null) return;
        
        // Ensure property exists
        if (!propertyPanel.HasProperty(layer.name))
        {
            propertyPanel.AddProperty(layer.name, layer.isVisible);
        }

        var item = propertyPanel.GetItem(layer.name);
        
        if (item == null)
        {
            Debug.LogWarning($"[TiffLayerManager] Could not find PropertyToggleItem for {layer.name}");
            return;
        }
        
        Debug.Log($"[TiffLayerManager] SetupLayerUI for {layer.name}. isSingleBand: {layer.isSingleBand}");

        item.SetupLegend(layer.isSingleBand, () => {
                Debug.Log($"[TiffLayerManager] Legend Callback Executed for {layer.name}");
                
                // Robustness check: Try to find LegendController if null
                if (legendController == null) 
                {
                    legendController = FindObjectOfType<LegendController>();
                    if (legendController != null) Debug.Log("[TiffLayerManager] LegendController found via FindObjectOfType inside callback.");
                }

                if (legendController != null)
                {
                    legendController.Setup(layer.name, layer.minVal, layer.maxVal, layer.currentGradient);
                    
                    // Assign callback for color picker
                    legendController.onColorPickerRequest = OnLegendColorPickerRequest;

                    // Force expand if closed
                    if (!legendController.isExpanded) legendController.ToggleExpand();
                }
                else
                {
                    Debug.LogError("[TiffLayerManager] LegendController is NULL! Make sure LegendController exists in the scene.");
                }
            });
    }

    // Callback saat user klik color picker di Legend
    void OnLegendColorPickerRequest(string layerName)
    {
        Debug.Log($"[TiffLayerManager] Color Picker Requested for {layerName}");
        var layer = layers.Find(l => l.name == layerName);
        
        if (colorPicker == null) colorPicker = FindObjectOfType<ColorPickerUI>(); // Try find again

        if (layer != null && colorPicker != null)
        {
            colorPicker.Show(layer.currentGradient, (newGrad) => {
                layer.currentGradient = newGrad;
                if (legendController != null) legendController.UpdateGradientVisual(newGrad);
                ApplyGradientToLayer(layer, newGrad);
            });
        }
        else
        {
            Debug.LogError($"[TiffLayerManager] Cannot show ColorPicker. LayerFound: {layer!=null}, ColorPickerFound: {colorPicker!=null}");
        }
    }

    // Apply gradient ke layer texture
    void ApplyGradientToLayer(LayerData layer, Gradient grad)
    {
        if (layer.texture == null || layer.rawData == null) return;

        var pixels = layer.texture.GetPixels();
        
        // Determine coloring mode
        bool useAbsoluteRange = false;
        float absMin = -1f;
        float absMax = 1f;

        string uName = layer.name.ToUpper();
        if (uName.Contains("NDVI") || uName.Contains("NDWI") || uName.Contains("NDTI") || uName.Contains("GNDVI"))
        {
            useAbsoluteRange = true;
            absMin = -1f;
            absMax = 1f;
        }

        // Check for zero range fallback
        if (Mathf.Abs(layer.maxVal - layer.minVal) < 0.0001f)
        {
             if (useAbsoluteRange) 
             { 
                 layer.minVal = -1f; 
                 layer.maxVal = 1f; 
                 Debug.LogWarning($"[TiffLayerManager] Layer {layer.name} has 0 min/max. Fallback to -1/1."); 
             }
             else 
             { 
                 layer.maxVal = layer.minVal + 1f; 
                 Debug.LogWarning($"[TiffLayerManager] Layer {layer.name} has 0 min/max. Fallback Max to Min+1."); 
             }
        }

        Debug.Log($"[TiffLayerManager] Applying Gradient to {layer.name}. Mode: {(useAbsoluteRange ? "Absolute" : "Relative")}. Min: {layer.minVal}, Max: {layer.maxVal}");

        // Debug sample
        int centerIdx = pixels.Length / 2;
        if (centerIdx < layer.rawData.Length) {
             float val = layer.rawData[centerIdx];
             float originalVal = val * (layer.maxVal - layer.minVal) + layer.minVal;
             float t_debug = useAbsoluteRange ? Mathf.Clamp01((originalVal - absMin) / (absMax - absMin)) : val;
             Debug.Log($"[Gradient Debug] Center Pixel: Raw={val:F4}, Orig={originalVal:F4}, t={t_debug:F4}");
        }

        // Gunakan rawData agar coloring akurat dan bisa diganti-ganti
        for (int i = 0; i < layer.rawData.Length && i < pixels.Length; i++)
        {
            float val = layer.rawData[i]; // Ini adalah nilai 0-1 relatif terhadap Min/Max layer
            float t = val;

            if (useAbsoluteRange)
            {
                // Recover nilai asli
                float originalVal = val * (layer.maxVal - layer.minVal) + layer.minVal;
                
                // Map ke range absolute (-1 sampai 1)
                t = Mathf.Clamp01((originalVal - absMin) / (absMax - absMin));
            }

            if (grad != null)
            {
                Color c = grad.Evaluate(t);
                pixels[i] = new Color(c.r, c.g, c.b, 1f);
            }
            else
            {
                pixels[i] = new Color(val, val, val, 1f);
            }
        }
        layer.texture.SetPixels(pixels);
        layer.texture.Apply();
    }
}
