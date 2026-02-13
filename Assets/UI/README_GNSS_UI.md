# Dokumentasi Sistem Popup GNSS

Sistem ini dirancang untuk membuat dan mengelola popup UI GNSS (Geotagging, Static Processing, GNSS Data Viewer) dengan pendekatan **Prefab-First**, namun tetap memiliki **Code Fallback**.

## Komponen Utama

### 1. [GNSSPopupFactory.cs](file:///Assets/UI/Script/GNSSPopupFactory.cs)
Ini adalah "mesin" utama yang membangun struktur UI.
- **Fungsi**: Membangun hierarki GameObject (Panel, Header, Input, Button) secara prosedural menggunakan TextMeshPro (TMP).
- **Logika Prefab**: Setiap fungsi `Create...` (seperti `CreateGeotagging`) akan mengecek folder `Assets/Resources` terlebih dahulu. Jika prefab ditemukan, sistem akan memuat prefab tersebut. Jika tidak, sistem akan membangunnya dari kode.
- **Helper Methods**: Menyediakan fungsi modular seperti `CreateInputWithBrowse`, `CreateLabel`, dan `CreateSmallButton` untuk konsistensi layout.

### 2. [GNSSPopupController.cs](file:///Assets/UI/Script/GNSSPopupController.cs)
Script runtime yang menangani interaksi pengguna.
- **Tugas**: Mencari tombol dengan nama "CANCEL" atau "EXECUTE" di dalam popup.
- **Fitur**:
    - **Cancel**: Menghancurkan (Destroy) objek popup saat diklik.
    - **Execute**: Tempat untuk menghubungkan logika pemrosesan data GNSS.
- **Auto-Attach**: Script ini otomatis dipasangkan oleh Factory ke setiap popup yang dibuat.

### 3. [NavbarManager.cs](file:///Assets/UI/Script/NavbarManager.cs)
Pintu masuk (Entry Point) utama dari menu navigasi atas.
- Menghubungkan klik menu (Geotagging, dll) ke fungsi pembuat di `GNSSPopupFactory`.

## Alur Kerja (Workflow)

### Cara Membuat/Update Prefab
Agar Anda bisa mengedit tampilan secara visual di Unity Editor:
1. Pergi ke menu bar atas: **Tools -> GNSS**.
2. Pilih salah satu menu (contoh: **Create Geotagging Prefab**).
3. File prefab akan muncul di folder `Assets/Resources`.
4. **Buka Prefab tersebut** di Editor untuk mengubah warna, font, posisi, atau menambahkan komponen lain.

### Keuntungan Sistem Ini
- **Mudah Dikustomisasi**: Anda tidak perlu mengubah kode C# untuk sekadar mengganti warna background. Cukup edit Prefab-nya.
- **Interaktif**: Menggunakan komponen asli Unity (`TMP_Dropdown`, `TMP_InputField`, `Button`) sehingga mendukung input keyboard dan mouse secara penuh.
- **Scalable**: Mudah untuk menambah field input baru hanya dengan memanggil helper method di Factory.

## Struktur Folder Resources
Pastikan file prefab berikut tetap berada di `Assets/Resources` agar sistem loading otomatis berjalan:
- `GNSS_Geotagging_Popup.prefab`
- `GNSS_StaticProcessing_Popup.prefab`
- `GNSS_DataReader_Popup.prefab`
- `GNSS_PPKGeotagging_Popup.prefab`
- `IMU_BinToLog_Popup.prefab`

## Cuplikan Kode Penting

### 1. Sistem Pemuatan Prefab Otomatis
Kode ini memastikan editan visual Anda di Unity (Prefab) selalu diprioritaskan.
```csharp
public static GameObject CreateGeotagging(Canvas canvas = null, bool forceNew = false)
{
    if (!forceNew) {
        // Mencoba memuat dari folder Resources
        GameObject prefab = Resources.Load<GameObject>("GNSS_Geotagging_Popup");
        if (prefab != null) {
            GameObject instance = Object.Instantiate(prefab, canvas.transform);
            // Pasangkan logika tombol secara otomatis
            AttachPopupLogic(instance, "Geotagging");
            return instance;
        }
    }
    // ... fallback ke pembuatan via kode ...
}
```

### 2. Deteksi Tombol Otomatis (`AttachPopupLogic`)
Sistem ini secara cerdas mencari tombol Cancel/Execute di dalam hierarki tanpa perlu drag-and-drop manual.
```csharp
private static void AttachPopupLogic(GameObject root, string title)
{
    Button[] allButtons = root.GetComponentsInChildren<Button>(true);
    foreach (var btn in allButtons)
    {
        // Mencari tombol berdasarkan nama (case-insensitive)
        if (btn.name.ToUpper().Contains("CANCEL")) {
            btn.onClick.AddListener(() => Object.Destroy(root));
        }
        if (btn.name.ToUpper().Contains("EXECUTE")) {
            btn.onClick.AddListener(() => Debug.Log($"Executing {title}..."));
        }
    }
}
```

### 3. Pembuatan Editor Tool (Menu Unity)
Fungsi ini menambahkan menu di atas Unity Editor untuk mengekspor desain kode ke file Prefab.
```csharp
#if UNITY_EDITOR
[UnityEditor.MenuItem("Tools/GNSS/Create Geotagging Prefab")]
public static void CreateGeotaggingPrefab()
{
    GameObject popup = CreateGeotagging(null, true); 
    string path = "Assets/Resources/GNSS_Geotagging_Popup.prefab";
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
    Object.DestroyImmediate(popup);
}

[UnityEditor.MenuItem("Tools/GNSS/Create PPK Geotagging Prefab")]
public static void CreatePPKGeotaggingPrefab()
{
    GameObject popup = CreatePPKGeotagging(null, true); 
    string path = "Assets/Resources/GNSS_PPKGeotagging_Popup.prefab";
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(popup, path);
    Object.DestroyImmediate(popup);
}
#endif

### 4. Fitur Tambahan: Offline Mode
Fitur ini memungkinkan pengguna untuk mematikan pengambilan data peta dari internet dan menggantinya dengan basemap kotak-kotak (grid).

- **Lokasi**: Menu `Tools` -> `Offline Mode` / `Online Mode`.
- **Cara Kerja**:
    - Mengatur properti statis `SimpleMapController_Baru.IsOfflineMode`.
    - Saat aktif, `SimpleMapController_Baru` akan menggunakan tekstur grid prosedural 256x256 sebagai pengganti tile online.
    - Teks menu akan berubah secara dinamis antara "Offline Mode" dan "Online Mode".

```csharp
// Contoh Logika Toggle Offline Mode di NavbarManager.cs
if (sub.title == "Offline Mode" || sub.title == "Online Mode") {
    SimpleMapController_Baru.IsOfflineMode = !SimpleMapController_Baru.IsOfflineMode;
    sub.title = SimpleMapController_Baru.IsOfflineMode ? "Online Mode" : "Offline Mode";
    
    SimpleMapController_Baru map = SimpleMapController_Baru.Instance;
    if (map != null) map.RefreshMap(true);
}
```

---

## Daftar Popup yang Tersedia

1. **GNSS Data Viewer** (`CreateGNSSDataReader`)
   - Membaca file mentah .ubx.
2. **Static Processing** (`CreateStaticProcessing`)
   - Pemrosesan data statis GNSS.
3. **Geotagging** (`CreateGeotagging`)
   - Sinkronisasi foto dengan koordinat.
4. **PPK + Geotagging** (`CreatePPKGeotagging`)
   - Popup komprehensif untuk post-processing (TGS Post Processing).
   - Mendukung: Dual Frequency, Rinex Base, Antenna Rover Offset, Satellite Selection (GPS, GLO, dll), dan Flight Log.
5. **GNSS Converter** (External App)
   - Membuka aplikasi eksternal `GNSS-Converter.exe` yang terletak di folder `StreamingAssets/Backend`.
   - Menggunakan `System.Diagnostics.Process` untuk integrasi aplikasi pihak ketiga.
6. **IMU File Bin to Log** (`CreateIMUBinToLog`)
   - Konverter file binary IMU (.bin) ke format log.
   - Fitur: Input binary file dan output directory selection.

---

## Kesimpulan
Sistem ini memisahkan antara **Struktur UI (Factory)**, **Logika Interaksi (Controller)**, dan **Navigasi (Navbar)**. Hal ini memungkinkan pengembangan UI yang kompleks namun tetap mudah dikelola oleh programmer maupun desainer.
