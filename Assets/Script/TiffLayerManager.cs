using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using BitMiracle.LibTiff.Classic;

// =========================================
// Manager untuk membaca GeoTIFF multi-band
// dan menampilkan layer sebagai overlay di peta
// dengan posisi geografis yang benar
// =========================================
public class TiffLayerManager : MonoBehaviour
{
    [Header("References")]
    public SimpleMapController_Baru mapController;
    public TMP_InputField namePrefixInput; // Input field untuk prefix nama
    public PropertyPanel propertyPanel;
    public ProjectManager projectManager; // Referensi ProjectManager
    public RectTransform overlayContainer;  // Tempat spawn overlay

    [Header("Settings")]
    public float overlayOpacity = 1f;
    public List<string> customBandNames; // Nama band custom (di-assign via Inspector)

    // List Layer yang sedang aktif
    public List<LayerData> layers = new List<LayerData>();
    
    // Cache untuk menyimpan layer TIFF yang pernah diload
    // Key: Path file TIFF, Value: List LayerData
    private Dictionary<string, List<LayerData>> layerCache = new Dictionary<string, List<LayerData>>();

    List<GameObject> overlays = new List<GameObject>();
    string currentTiffPath = "";


    // GeoTIFF bounds (koordinat geografis)
    double geoMinLat, geoMaxLat, geoMinLon, geoMaxLon;
    int imageWidth, imageHeight;
    bool hasGeoData = false;

    // Cache untuk update posisi
    double lastMapLat, lastMapLon;
    int lastMapZoom;

    // Struct untuk menyimpan data layer
    // Struct untuk menyimpan data layer
    [System.Serializable]
    public class LayerData
    {
        public string name;
        public Texture2D texture;
        public bool isVisible;
    }

    void Start()
    {
        // Subscribe ke event PropertyPanel
        if (propertyPanel != null)
        {
            propertyPanel.onPropertyChanged.AddListener(OnPropertyToggle);
        }

        // Subscribe ke event ProjectManager
        if (projectManager != null)
        {
            projectManager.onTiffProjectLoaded.AddListener(OnTiffProjectLoaded);
        }

        // Pastikan overlayContainer selalu ON
        if (overlayContainer != null)
        {
            overlayContainer.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        // Sync posisi overlay dengan peta jika peta bergerak/zoom
        if (hasGeoData && mapController != null)
        {
            bool mapChanged = (mapController.latitude != lastMapLat) ||
                              (mapController.longitude != lastMapLon) ||
                              (mapController.zoom != lastMapZoom);

            if (mapChanged)
            {
                UpdateAllOverlayPositions();
                lastMapLat = mapController.latitude;
                lastMapLon = mapController.longitude;
                lastMapZoom = mapController.zoom;
            }
        }
    }

    // =========================================
    // PUBLIC API
    // =========================================

    // Method Baru: Load PNG Overlay dengan Bounds Manual
    public void LoadPngOverlay(string pngPath, double north, double south, double west, double east, bool isPreview = false)
    {
        if (!File.Exists(pngPath))
        {
            Debug.LogError($"[TiffLayerManager] PNG tidak ditemukan: {pngPath}");
            return;
        }

        // Clear layer lama
        ClearLayers();
        currentTiffPath = pngPath; // Simpan path PNG sebagai referensi

        Debug.Log($"[TiffLayerManager] Loading PNG: {pngPath}");

        // Load PNG Texture dengan parameter aman
        byte[] fileData = File.ReadAllBytes(pngPath);
        
        // Buat tekstur baru tanpa MipMap (penting untuk UI/2D) dan Linear Color Space
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        
        if (tex.LoadImage(fileData)) // Auto-resize texture dimensions
        {
            // Set Texture Settings
            tex.wrapMode = TextureWrapMode.Clamp; // Cegah bleeding
            tex.filterMode = FilterMode.Bilinear; // Halus

            // DEBUG: Cek sampel pixel untuk memastikan data masuk
            if (tex.width > 0 && tex.height > 0)
            {
                // Cek pixel tengah
                Color centerPixel = tex.GetPixel(tex.width / 2, tex.height / 2);
                
                // Cek pixel di posisi lain (misal 1/4)
                Color qPixel = tex.GetPixel(tex.width / 4, tex.height / 4);

                Debug.Log($"[TiffLayerManager] Texture Size: {tex.width}x{tex.height}");
                Debug.Log($"[TiffLayerManager] Center Pixel: {centerPixel}");
                Debug.Log($"[TiffLayerManager] Quarter Pixel: {qPixel}");

                // JIKA SEMUA HITAM PEKAT (0,0,0,0) atau (0,0,0,1), coba paksa warna test
                // Ini untuk membuktikan apakah masalah di rendering atau data.
                if (centerPixel.a == 0 && qPixel.a == 0)
                {
                    Debug.LogWarning("[TiffLayerManager] WARNING: Texture appears fully transparent/empty!");
                }
            }

            tex.Apply(); // Apply perubahan setting

            // Set Geo Bounds dari parameter
            geoMaxLat = north;
            geoMinLat = south;
            geoMinLon = west;
            geoMaxLon = east;
            
            imageWidth = tex.width;
            imageHeight = tex.height;
            hasGeoData = true;

            Debug.Log($"[TiffLayerManager] Loaded PNG Overlay: {tex.width}x{tex.height}, Format: {tex.format}. Bounds: Lat [{south} - {north}], Lon [{west} - {east}]");

            // Buat Layer
            // Jika preview, beri nama khusus agar tidak tertukar dengan band asli
            string layerName = isPreview ? "PREVIEW_SATELIT" : Path.GetFileNameWithoutExtension(pngPath);
            var newLayer = new LayerData { name = layerName, texture = tex, isVisible = true };
            layers.Add(newLayer);

            // Tampilkan di Panel & Map
            if (isPreview)
            {
                // JIKA PREVIEW: Langsung tampilkan di peta tanpa sinkronisasi ke Project Properties
                // Ini mencegah munculnya toggle di UI panel secara permanen
                ShowLayerOnMap(newLayer);
            }
            else
            {
                // JIKA BUKAN PREVIEW: Jalankan flow standar (sinkron ke Project Manager)
                SyncWithProject();
            }
            
            if (mapController != null)
            {
                double centerLat = (geoMinLat + geoMaxLat) / 2.0;
                double centerLon = (geoMinLon + geoMaxLon) / 2.0;
                int suggestedZoom = CalculateFitZoom();
                mapController.GoToLocation(centerLat, centerLon, suggestedZoom);
            }
        }
    }

    // Load file GeoTIFF dan extract semua band
    public void LoadTiff(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[TiffLayerManager] File tidak ditemukan: {path}");
            return;
        }

        // Clear layer lama (sembunyikan visualnya)
        ClearLayers();
        currentTiffPath = path;

        // Cek Cache
        if (layerCache.ContainsKey(path))
        {
            Debug.Log($"[TiffLayerManager] Loading from CACHE: {path}");
            layers = layerCache[path];
            
            // Tampilkan kembali
            foreach (var layer in layers)
            {
                if (layer.isVisible) ShowLayerOnMap(layer);
            }
            
            hasGeoData = true; // Asumsi data geo tersimpan
            
            // Re-sync properties jika perlu
            SyncWithProject();
            return;
        }

        // Jika tidak ada di cache, load baru
        Debug.Log($"[TiffLayerManager] Loading from DISK: {path}");
        
        // Baca TIFF menggunakan LibTiff.Net
        using (Tiff tiff = Tiff.Open(path, "r"))
        {
            if (tiff == null)
            {
                Debug.LogError($"[TiffLayerManager] Gagal membuka TIFF: {path}");
                return;
            }

            // Dapatkan info dasar
            imageWidth = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            imageHeight = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int samplesPerPixel = 1;
            
            var sppField = tiff.GetField(TiffTag.SAMPLESPERPIXEL);
            if (sppField != null) samplesPerPixel = sppField[0].ToInt();

            int bitsPerSample = 8;
            var bpsField = tiff.GetField(TiffTag.BITSPERSAMPLE);
            if (bpsField != null) bitsPerSample = bpsField[0].ToInt();

            Debug.Log($"[TiffLayerManager] TIFF: {imageWidth}x{imageHeight}, {samplesPerPixel} bands, {bitsPerSample} bits");

            // Baca GeoTIFF tags untuk koordinat
            ReadGeoTiffTags(tiff);

            // ===========================================
            // MANUAL READ: Mengatasi masalah 16-bit dark & skew
            // & Support PlanarConfig (Interleaved vs Separate)
            // ===========================================
            
            // Cek Planar Config & Tiling
            FieldValue[] pConfigField = tiff.GetField(TiffTag.PLANARCONFIG);
            int planarConfig = 1; // Default Contiguous
            if (pConfigField != null && pConfigField.Length > 0) planarConfig = pConfigField[0].ToInt();
            
            bool isTiled = tiff.IsTiled();
            
            Debug.Log($"[TiffLayerManager] Mode: {(isTiled ? "TILED" : "STRIP")}, PlanarConfig: {planarConfig}, Bits: {bitsPerSample}, Samples: {samplesPerPixel}");

            int totalPixels = imageWidth * imageHeight;
            float[][] bandData = new float[samplesPerPixel][];
            for (int b = 0; b < samplesPerPixel; b++) bandData[b] = new float[totalPixels];

            float[] minVal = new float[samplesPerPixel];
            float[] maxVal = new float[samplesPerPixel];
            for (int k = 0; k < samplesPerPixel; k++) { minVal[k] = float.MaxValue; maxVal[k] = float.MinValue; }

            // ===================================
            // PATH A: TILED TIFF (Common for GeoTIFF)
            // ===================================
            if (isTiled)
            {
                int tileWidth = tiff.GetField(TiffTag.TILEWIDTH)[0].ToInt();
                int tileHeight = tiff.GetField(TiffTag.TILELENGTH)[0].ToInt();
                int tileSize = tiff.TileSize();
                byte[] tileBuffer = new byte[tileSize];

                for (int y = 0; y < imageHeight; y += tileHeight)
                {
                    for (int x = 0; x < imageWidth; x += tileWidth)
                    {
                        // Handle Contiguous vs Separate Tiles
                        if (planarConfig == 1) // RGBRGB...
                        {
                            tiff.ReadTile(tileBuffer, 0, x, y, 0, 0);
                            
                            // Iterate pixels in tile
                            for (int ty = 0; ty < tileHeight; ty++)
                            {
                                int globalY = y + ty;
                                if (globalY >= imageHeight) break;

                                for (int tx = 0; tx < tileWidth; tx++)
                                {
                                    int globalX = x + tx;
                                    if (globalX >= imageWidth) break;

                                    int pixelIdx = globalY * imageWidth + globalX;
                                    
                                    // Parse tile buffer (similar to scanline)
                                    for (int s = 0; s < samplesPerPixel; s++)
                                    {
                                        int byteIdx = 0;
                                        if (bitsPerSample == 8) byteIdx = (ty * tileWidth + tx) * samplesPerPixel + s;
                                        else if (bitsPerSample == 16) byteIdx = ((ty * tileWidth + tx) * samplesPerPixel + s) * 2;
                                        else if (bitsPerSample == 32) byteIdx = ((ty * tileWidth + tx) * samplesPerPixel + s) * 4;

                                        if (byteIdx >= tileSize) continue;

                                        float val = 0;
                                        if (bitsPerSample == 8) val = tileBuffer[byteIdx] / 255f;
                                        else if (bitsPerSample == 16) val = System.BitConverter.ToUInt16(tileBuffer, byteIdx) / 65535f;
                                        else if (bitsPerSample == 32) val = System.BitConverter.ToSingle(tileBuffer, byteIdx);

                                        bandData[s][pixelIdx] = val;
                                        if (val < minVal[s]) minVal[s] = val;
                                        if (val > maxVal[s]) maxVal[s] = val;
                                    }
                                }
                            }
                        }
                        else // Separate (Band by Band)
                        {
                            for (int s = 0; s < samplesPerPixel; s++)
                            {
                                // Sample param is checked differently for tiles? No, ReadTile takes sample.
                                tiff.ReadTile(tileBuffer, 0, x, y, 0, (short)s);
                                
                                for (int ty = 0; ty < tileHeight; ty++)
                                {
                                    int globalY = y + ty;
                                    if (globalY >= imageHeight) break;

                                    for (int tx = 0; tx < tileWidth; tx++)
                                    {
                                        int globalX = x + tx;
                                        if (globalX >= imageWidth) break;
                                        
                                        int pixelIdx = globalY * imageWidth + globalX;
                                        
                                        int byteIdx = 0;
                                        if (bitsPerSample == 8) byteIdx = (ty * tileWidth + tx);
                                        else if (bitsPerSample == 16) byteIdx = (ty * tileWidth + tx) * 2;
                                        
                                        if (byteIdx >= tileSize) continue;
                                        
                                        float val = 0;
                                        if (bitsPerSample == 8) val = tileBuffer[byteIdx] / 255f;
                                        else if (bitsPerSample == 16) val = System.BitConverter.ToUInt16(tileBuffer, byteIdx) / 65535f;

                                        bandData[s][pixelIdx] = val;
                                        if (val < minVal[s]) minVal[s] = val;
                                        if (val > maxVal[s]) maxVal[s] = val;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // ===================================
            // PATH B: STRIP TIFF (Standard)
            // ===================================
            else 
            {
                int scanlineSize = tiff.ScanlineSize();
                byte[] scanlineBuffer = new byte[scanlineSize];

                // Logic Pembacaan Berdasarkan Planar Config
                if (planarConfig == 1) // Contiguous (RGBRGB...)
                {
                    for (int y = 0; y < imageHeight; y++)
                    {
                        tiff.ReadScanline(scanlineBuffer, y);
                        
                        for (int x = 0; x < imageWidth; x++)
                        {
                            int pixelIdx = y * imageWidth + x;
                            for (int s = 0; s < samplesPerPixel; s++)
                            {
                                // Calculate byte offset
                                int byteIdx = 0;
                                if (bitsPerSample == 8) byteIdx = x * samplesPerPixel + s;
                                else if (bitsPerSample == 16) byteIdx = (x * samplesPerPixel + s) * 2;
                                else if (bitsPerSample == 32) byteIdx = (x * samplesPerPixel + s) * 4;

                                if (byteIdx >= scanlineSize) continue;

                                float val = 0;
                                if (bitsPerSample == 8) val = scanlineBuffer[byteIdx] / 255f;
                                else if (bitsPerSample == 16) val = System.BitConverter.ToUInt16(scanlineBuffer, byteIdx) / 65535f;
                                else if (bitsPerSample == 32) val = System.BitConverter.ToSingle(scanlineBuffer, byteIdx);

                                bandData[s][pixelIdx] = val;
                                if (val < minVal[s]) minVal[s] = val;
                                if (val > maxVal[s]) maxVal[s] = val;
                            }
                        }
                    }
                }
                else // Separate (RRR..., GGG..., BBB...)
                {
                    for (int s = 0; s < samplesPerPixel; s++)
                    {
                        for (int y = 0; y < imageHeight; y++)
                        {
                            // Baca specific sample/band untuk baris ini
                            tiff.ReadScanline(scanlineBuffer, y, (short)s);

                            for (int x = 0; x < imageWidth; x++)
                            {
                                int pixelIdx = y * imageWidth + x;
                                
                                // Calculate byte offset (non-interleaved)
                                int byteIdx = 0;
                                if (bitsPerSample == 8) byteIdx = x;
                                else if (bitsPerSample == 16) byteIdx = x * 2;
                                else if (bitsPerSample == 32) byteIdx = x * 4;

                                if (byteIdx >= scanlineSize) continue;

                                float val = 0;
                                if (bitsPerSample == 8) val = scanlineBuffer[byteIdx] / 255f;
                                else if (bitsPerSample == 16) val = System.BitConverter.ToUInt16(scanlineBuffer, byteIdx) / 65535f;
                                else if (bitsPerSample == 32) val = System.BitConverter.ToSingle(scanlineBuffer, byteIdx);

                                bandData[s][pixelIdx] = val;
                                if (val < minVal[s]) minVal[s] = val;
                                if (val > maxVal[s]) maxVal[s] = val;
                            }
                        }
                    }
                }
            }

            // =========================================
            // AUTO STRETCH (Normalization) & Create Texture
            // =========================================
            for (int s = 0; s < samplesPerPixel; s++)
            {
                // Safety check
                if (maxVal[s] - minVal[s] < 0.0001f) maxVal[s] = minVal[s] + 1f;
                Debug.Log($"[TiffLayerManager] Band {s} Range: {minVal[s]} - {maxVal[s]}");
            }

            // Helper to get normalized value
            float GetNorm(int band, int idx)
            {
                float val = bandData[band][idx];
                return Mathf.Clamp01((val - minVal[band]) / (maxVal[band] - minVal[band]));
            }

            // Cek prefix nama (Prioritas: Project Name -> InputField)
            string namingPrefix = "";
            bool usedProjectPrefix = false;
            
            if (projectManager != null)
            {
                var proj = projectManager.GetCurrentProject();
                if (proj != null && !string.IsNullOrEmpty(proj.tiffPath))
                {
                    // Gunakan Path.GetFullPath untuk normalisasi string path (slash, backslash, case)
                    string p1 = Path.GetFullPath(proj.tiffPath).ToLower();
                    string p2 = Path.GetFullPath(currentTiffPath).ToLower();
                    
                    if (p1 == p2)
                    {
                        namingPrefix = proj.name + " ";
                        usedProjectPrefix = true;
                    }
                }
            }

            if (!usedProjectPrefix && namePrefixInput != null && !string.IsNullOrEmpty(namePrefixInput.text))
            {
                namingPrefix = namePrefixInput.text + " ";
            }

            // Buat Layer untuk SETIAP Band yang ada
            for (int i = 0; i < samplesPerPixel; i++)
            {
                // Default: Kosong (sesuai request)
                string bandName = "";
                
                // Cek custom names dari Inspector
                if (customBandNames != null && i < customBandNames.Count && !string.IsNullOrEmpty(customBandNames[i]))
                {
                    bandName = customBandNames[i];
                }

                // Tambahkan prefix nama project (jika ada)
                if (!string.IsNullOrEmpty(namingPrefix))
                {
                    bandName = namingPrefix + bandName;
                }

                CreateLayerManual(bandData, imageWidth, imageHeight, bandName, i, minVal[i], maxVal[i]);
            }

            // Tambahkan Composite jika minimal 3 band
            if (samplesPerPixel >= 3)
            {
                string compName = "RGB Composite";
                if (!string.IsNullOrEmpty(namingPrefix)) compName = namingPrefix + compName;
                
                CreateCompositeManual(bandData, imageWidth, imageHeight, compName, minVal, maxVal);
            }

            // Clear bandData untuk membebaskan memori
            for (int b = 0; b < samplesPerPixel; b++)
            {
                bandData[b] = null;
            }

            // Simpan ke Cache
            if (!layerCache.ContainsKey(path))
            {
                layerCache.Add(path, new List<LayerData>(layers));
            }
        }

        // Tampilkan di Panel (via ProjectManager jika ada)
        SyncWithProject();

        if (hasGeoData && mapController != null)
        {
            double centerLat = (geoMinLat + geoMaxLat) / 2.0;
            double centerLon = (geoMinLon + geoMaxLon) / 2.0;
            
            // Hitung zoom level yang sesuai
            int suggestedZoom = CalculateFitZoom();
            
            mapController.GoToLocation(centerLat, centerLon, suggestedZoom);
            Debug.Log($"[TiffLayerManager] Navigasi ke: {centerLat}, {centerLon}, zoom {suggestedZoom}");
        }
    }
    
    // Callback dari ProjectManager
    void OnTiffProjectLoaded(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            ClearLayers();
            return;
        }

        // Jangan reload jika path sama (kecuali dipaksa)
        if (currentTiffPath != path || layers.Count == 0)
        {
            LoadTiff(path);
        }
        else
        {
            // Jika path sama, cuma sync property
            SyncWithProject();
        }
    }

    // Integrasi dengan ProjectManager
    void SyncWithProject()
    {
        // Fallback jika tidak ada project manager
        if (projectManager == null || projectManager.GetCurrentProject() == null)
        {
            ShowLayersInPanel();
            return;
        }

        // Pastikan setiap layer terdaftar dan sync kondisinya
        var proj = projectManager.GetCurrentProject();
        var props = proj.GetProps();

        foreach (var layer in layers)
        {
            // Jika properti belum ada di project, tambahkan default (false)
            // Jika sudah ada, ikuti nilai dari project
            if (!props.ContainsKey(layer.name))
            {
                // Default OFF saat load project biasa
                projectManager.AddProperty(layer.name, false); 
            }
            else
            {
                bool val = props[layer.name];
                layer.isVisible = val;
                
                if (val) ShowLayerOnMap(layer);
                else HideLayerFromMap(layer);
            }
        }
    }

    // Helper: Dapatkan Center Lat/Lon dari TIFF tanpa load full texture
    public bool GetTiffCenter(string path, out double centerLat, out double centerLon)
    {
        centerLat = 0;
        centerLon = 0;

        if (!File.Exists(path)) return false;

        using (Tiff tiff = Tiff.Open(path, "r"))
        {
            if (tiff == null) return false;
            
            // Baca dimensi
            int w = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int h = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

            // Set temporary variables untuk ReadGeoTiffTags
            imageWidth = w;
            imageHeight = h;

            ReadGeoTiffTags(tiff);

            if (hasGeoData)
            {
                centerLat = (geoMinLat + geoMaxLat) / 2.0;
                centerLon = (geoMinLon + geoMaxLon) / 2.0;
                return true;
            }
        }
        return false;
    }

    // Helper: Dapatkan Bounds (Min/Max Lat/Lon) dari TIFF
    public bool GetTiffBounds(string path, out double minLat, out double maxLat, out double minLon, out double maxLon)
    {
        minLat = maxLat = minLon = maxLon = 0;

        if (!File.Exists(path)) return false;

        using (Tiff tiff = Tiff.Open(path, "r"))
        {
            if (tiff == null) return false;
            
            // Baca dimensi untuk keperluan ReadGeoTiffTags
            int w = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int h = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

            // Set temporary variables
            imageWidth = w;
            imageHeight = h;

            ReadGeoTiffTags(tiff);

            if (hasGeoData)
            {
                minLat = geoMinLat;
                maxLat = geoMaxLat;
                minLon = geoMinLon;
                maxLon = geoMaxLon;
                return true;
            }
        }
        return false;
    }

    // Hapus spesifik path dari cache (dipanggil saat delete project)
    public void UnloadFromCache(string path)
    {
        if (layerCache.ContainsKey(path))
        {
            List<LayerData> cachedLayers = layerCache[path];
            // Opsional: Destroy texture jika ingin membebaskan memory
            foreach(var l in cachedLayers)
            {
                if (l.texture != null) Destroy(l.texture);
            }
            layerCache.Remove(path);
            Debug.Log($"[TiffLayerManager] Unloaded from Cache: {path}");
        }
    }

    // Hapus semua layer (hanya sembunyikan visual, data tetap di cache)
    public void ClearLayers()
    {
        // Sembunyikan semua overlay visual
        foreach (var overlay in overlays)
        {
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
        }
        // Kita tidak mendestroy overlay agar bisa direuse jika logic reuse diimplementasikan
        // Tapi saat ini kita destroy overlay object karena sulit manajemen pool-nya
        // Untuk "Hide", kita set active false. 
        
        // TAPI, layerCache menyimpan referensi ke Texture2D. 
        // Logic LoadTiff (CACHE) akan membuat Overlay object baru dari Texture2D yang ada.
        
        // Jadi di sini kita destroy GAME OBJECT overlay saja, texture aman di memory.
        foreach (var overlay in overlays)
        {
            if (overlay != null) Destroy(overlay);
        }
        overlays.Clear();
        
        // Kita juga clear list `layers` aktif agar tidak tertukar
        // Tapi ingat, list ini sudah disimpan di cache sebelum dicari user.
        layers = new List<LayerData>(); // Ganti referensi ke list kosong

        // Clear panel UI
        if (propertyPanel != null)
        {
            propertyPanel.ClearPanel();
        }
        
        hasGeoData = false;
        currentTiffPath = null; // Reset path agar reload berikutnya dipaksa jalan
    }


    // =========================================
    // GEOTIFF PARSING
    // =========================================

    void ReadGeoTiffTags(Tiff tiff)
    {
        hasGeoData = false;

        // Coba baca ModelTiepoint (tag 33922) dan ModelPixelScale (tag 33550)
        var tiepointField = tiff.GetField((TiffTag)33922);  // TIFFTAG_GEOTIFF_MODELTIEPOINT
        var scaleField = tiff.GetField((TiffTag)33550);     // TIFFTAG_GEOTIFF_MODELPIXELSCALE

        if (tiepointField != null && scaleField != null)
        {
            // Parse tiepoint: [I, J, K, X, Y, Z] dimana I,J=pixel, X,Y=koordinat
            int tiepointCount = tiepointField[0].ToInt();
            double[] tiepoints = tiepointField[1].ToDoubleArray();
            
            // Parse scale: [ScaleX, ScaleY, ScaleZ]
            int scaleCount = scaleField[0].ToInt();
            double[] scales = scaleField[1].ToDoubleArray();

            if (tiepoints != null && tiepoints.Length >= 6 && scales != null && scales.Length >= 2)
            {
                double originI = tiepoints[0];  // Pixel X (biasanya 0)
                double originJ = tiepoints[1];  // Pixel Y (biasanya 0)
                double originX = tiepoints[3];  // Longitude (atau X dalam CRS)
                double originY = tiepoints[4];  // Latitude (atau Y dalam CRS)

                double scaleX = scales[0];  // Derajat per pixel di X
                double scaleY = scales[1];  // Derajat per pixel di Y

                // Hitung bounding box
                // Origin biasanya adalah pojok kiri atas (minLon, maxLat)
                geoMinLon = originX - (originI * scaleX);
                geoMaxLat = originY + (originJ * scaleY);
                geoMaxLon = geoMinLon + (imageWidth * scaleX);
                geoMinLat = geoMaxLat - (imageHeight * scaleY);

                hasGeoData = true;
                Debug.Log($"[TiffLayerManager] GeoTIFF Bounds: Lat [{geoMinLat} - {geoMaxLat}], Lon [{geoMinLon} - {geoMaxLon}]");
            }
        }

        // Jika tidak ada tiepoint/scale, coba baca GeoTransform (tag 34264)
        if (!hasGeoData)
        {
            var transformField = tiff.GetField((TiffTag)34264);  // TIFFTAG_GEOTIFF_MODELTRANSFORMATION
            if (transformField != null)
            {
                double[] transform = transformField[1].ToDoubleArray();
                if (transform != null && transform.Length >= 16)
                {
                    // Affine transform matrix 4x4
                    // transform[0] = scaleX, transform[3] = originX
                    // transform[5] = -scaleY (negatif), transform[7] = originY
                    double scaleX = transform[0];
                    double scaleY = -transform[5];  // Biasanya negatif
                    double originX = transform[3];
                    double originY = transform[7];

                    geoMinLon = originX;
                    geoMaxLat = originY;
                    geoMaxLon = geoMinLon + (imageWidth * scaleX);
                    geoMinLat = geoMaxLat - (imageHeight * System.Math.Abs(scaleY));

                    hasGeoData = true;
                    Debug.Log($"[TiffLayerManager] GeoTIFF Transform Bounds: Lat [{geoMinLat} - {geoMaxLat}], Lon [{geoMinLon} - {geoMaxLon}]");
                }
            }
        }

        if (!hasGeoData)
        {
            Debug.LogWarning("[TiffLayerManager] Tidak ditemukan data koordinat GeoTIFF. Overlay akan menutupi seluruh peta.");
        }
    }

    // Hitung zoom level yang sesuai untuk menampilkan seluruh TIFF
    public int CalculateFitZoom()
    {
        if (!hasGeoData || overlayContainer == null) return 15;

        double latSpan = geoMaxLat - geoMinLat;
        double lonSpan = geoMaxLon - geoMinLon;

        // Ukuran container dalam pixel
        float containerWidth = overlayContainer.rect.width;
        float containerHeight = overlayContainer.rect.height;

        // Hitung zoom berdasarkan span terbesar
        // Rumus: 360 / (2^zoom) = derajat per tile
        for (int z = 18; z >= 3; z--)
        {
            double degreesPerPixel = 360.0 / (256.0 * System.Math.Pow(2, z));
            double pixelSpanX = lonSpan / degreesPerPixel;
            double pixelSpanY = latSpan / degreesPerPixel;

            if (pixelSpanX < containerWidth * 0.8 && pixelSpanY < containerHeight * 0.8)
            {
                return z;
            }
        }

        return 10;
    }

    // =========================================
    void CreateLayerManual(float[][] bandData, int width, int height, string name, int channel, float min, float max)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        float range = max - min;
        if (range < 0.0001f) range = 1f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Scanline read is Top-Down by default for most TIFFs.
                // Unity texture is Bottom-Up. So we flip Y here.
                int srcIdx = (height - 1 - y) * width + x;
                int dstIdx = y * width + x;
                
                float val = bandData[channel][srcIdx];
                
                // Normalize manually using calculated range
                float norm = Mathf.Clamp01((val - min) / range);

                pixels[dstIdx] = new Color(norm, norm, norm, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        layers.Add(new LayerData { name = name, texture = tex, isVisible = false });
    }

    void CreateCompositeManual(float[][] bandData, int width, int height, string name, float[] min, float[] max)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        
        float rRange = max[0] - min[0]; if (rRange < 0.0001f) rRange = 1f;
        float gRange = max[1] - min[1]; if (gRange < 0.0001f) gRange = 1f;
        float bRange = max[2] - min[2]; if (bRange < 0.0001f) bRange = 1f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = (height - 1 - y) * width + x;
                int dstIdx = y * width + x;

                float r = Mathf.Clamp01((bandData[0][srcIdx] - min[0]) / rRange);
                float g = Mathf.Clamp01((bandData[1][srcIdx] - min[1]) / gRange);
                float b = Mathf.Clamp01((bandData[2][srcIdx] - min[2]) / bRange);

                pixels[dstIdx] = new Color(r, g, b, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        layers.Add(new LayerData { name = name, texture = tex, isVisible = false });
    }

    // =========================================
    // OVERLAY DISPLAY
    // =========================================

    void ShowLayersInPanel()
    {
        if (propertyPanel == null) return;

        Dictionary<string, bool> props = new Dictionary<string, bool>();
        foreach (var layer in layers)
        {
            props[layer.name] = layer.isVisible;
        }

        propertyPanel.ShowProperties(props);
    }

    void OnPropertyToggle(string name, bool value)
    {
        // Update layer visibility
        LayerData layer = layers.Find(l => l.name == name);
        if (layer == null) return;

        layer.isVisible = value;

        if (value) 
        {
            if (overlayContainer != null) overlayContainer.gameObject.SetActive(true);
            ShowLayerOnMap(layer);
        }
        else HideLayerFromMap(layer);

        // Update ProjectManager juga (biar tersimpan)
        if (projectManager != null && projectManager.GetCurrentProject() != null)
        {
            projectManager.OnPropertyChanged(name, value);
            
            // LOGIKA BARU: Hide Polygon jika ada layer yang aktif
            bool anyLayerActive = layers.Exists(l => l.isVisible);
            projectManager.SetProjectPolygonVisibility(!anyLayerActive);
        }
    }

    // Dipanggil dari luar (OverlayToggleController) untuk mengubah visibility layer
    public void OnPropertyToggleExternal(string name, bool value)
    {
        LayerData layer = layers.Find(l => l.name == name);
        if (layer == null) return;

        layer.isVisible = value;

        if (value) 
        {
            if (overlayContainer != null) overlayContainer.gameObject.SetActive(true);
            ShowLayerOnMap(layer);
        }
        else HideLayerFromMap(layer);

        // Jangan update ProjectManager di sini karena sudah di-update oleh OverlayToggleController
        bool anyLayerActive = layers.Exists(l => l.isVisible);
        if (projectManager != null)
        {
            projectManager.SetProjectPolygonVisibility(!anyLayerActive);
        }
    }

    void ShowLayerOnMap(LayerData layer)
    {
        if (overlayContainer == null || layer.texture == null) return;
        
        // Pastikan container ON
        if (!overlayContainer.gameObject.activeSelf) 
            overlayContainer.gameObject.SetActive(true);

        // Cek apakah sudah ada overlay untuk layer ini
        GameObject existing = overlays.Find(o => o != null && o.name == layer.name);
        if (existing != null)
        {
            existing.SetActive(true);
            UpdateOverlayPosition(existing);
            return;
        }

        // Buat overlay baru
        GameObject overlay = new GameObject(layer.name);
        overlay.transform.SetParent(overlayContainer, false);

        RawImage img = overlay.AddComponent<RawImage>();
        img.texture = layer.texture;
        img.color = new Color(1f, 1f, 1f, overlayOpacity);

        // FIX: Pastikan tidak terpengaruh oleh default material yang mungkin gelap
        // Gunakan UI/Default shader jika memungkinkan
        Shader uiShader = Shader.Find("UI/Default");
        if (uiShader != null)
        {
            // Gunakan Sprites/Default karena lebih aman untuk menampilkan tekstur raw tanpa lighting
            // UI/Default kadang bermasalah dengan tint color jika tidak diset benar
            img.material = new Material(Shader.Find("Sprites/Default"));
        }
        else
        {
            // Fallback ke Sprites/Default jika UI/Default tidak ada (jarang terjadi)
            img.material = new Material(Shader.Find("Sprites/Default"));
        }

        // Posisikan berdasarkan koordinat geo
        UpdateOverlayPosition(overlay);

        overlays.Add(overlay);
    }

    void HideLayerFromMap(LayerData layer)
    {
        GameObject overlay = overlays.Find(o => o != null && o.name == layer.name);
        if (overlay != null)
        {
            overlay.SetActive(false);
        }
    }

    void UpdateOverlayPosition(GameObject overlay)
    {
        if (mapController == null || !hasGeoData) return;

        RectTransform rt = overlay.GetComponent<RectTransform>();

        // Konversi koordinat geo ke posisi UI
        // Pojok kiri bawah (minLon, minLat) dan pojok kanan atas (maxLon, maxLat)
        Vector2 posMin = mapController.LatLonToLocalPosition(geoMinLat, geoMinLon);
        Vector2 posMax = mapController.LatLonToLocalPosition(geoMaxLat, geoMaxLon);

        // Hitung center dan size
        Vector2 center = (posMin + posMax) / 2f;
        float width = Mathf.Abs(posMax.x - posMin.x);
        float height = Mathf.Abs(posMax.y - posMin.y);

        // Set posisi dan ukuran
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = new Vector2(width, height);
    }

    void UpdateAllOverlayPositions()
    {
        foreach (var overlay in overlays)
        {
            if (overlay != null && overlay.activeSelf)
            {
                UpdateOverlayPosition(overlay);
            }
        }
    }

    void OnDestroy()
    {
        ClearLayers();
    }
}
