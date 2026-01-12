# Dokumentasi Fitur Raster Transform & Integrasi Python

Dokumen ini menjelaskan integrasi fitur analisis citra satelit berbasis Python (`rasterTransform.exe`) ke dalam Unity GIS Prototype.

## 1. Arsitektur Sistem

Sistem menggunakan pendekatan *Loose Coupling* antara Unity (Frontend) dan Python (Backend):
*   **Frontend (Unity)**: Menangani UI, input file, visualisasi peta, dan manajemen layer.
*   **Backend (Python)**: Menangani komputasi berat (algoritma spektral) dan pemrosesan citra (GeoTIFF).
*   **Komunikasi**: Unity memanggil `.exe` backend dengan argumen CLI dan membaca output JSON dari `stdout`.

## 2. Fitur Backend (`rasterTransform.exe`)

Alat ini adalah script Python (`rasterTransform.py`) yang dikompilasi menjadi executable.

### Lokasi File
`Assets/StreamingAssets/Backend/rasterTransform.exe`

### Kemampuan
Menghitung indeks spektral dari citra satelit multispektral (Landsat 8/9, Sentinel-2).

### Algoritma yang Didukung
| Algoritma | Deskripsi | Rumus Ringkas |
| :--- | :--- | :--- |
| **NDVI** | Normalized Difference Vegetation Index | `(NIR - Red) / (NIR + Red)` |
| **NDTI** | Normalized Difference Turbidity Index | `(Red - Green) / (Red + Green)` |
| **NDBI** | Normalized Difference Built-up Index | `(SWIR - NIR) / (SWIR + NIR)` |
| **NDWI** | Normalized Difference Water Index | `(Green - NIR) / (Green + NIR)` |
| **NGRDI** | Normalized Green Red Difference Index | `(Green - Red) / (Green + Red)` |
| **SAVI** | Soil Adjusted Vegetation Index | Modifikasi NDVI untuk area tanah terbuka |
| **EVI** | Enhanced Vegetation Index | Sensitivitas vegetasi tinggi |
| **GNDVI** | Green NDVI | `(NIR - Green) / (NIR + Green)` |
| **ARVI** | Atmospherically Resistant Vegetation Index | Tahan gangguan atmosfer |
| **MSAVI** | Modified Soil Adjusted Vegetation Index | Minimalisir pengaruh tanah |
| **TCI** | True Color Image | Gabungan Red, Green, Blue (Visual Asli) |
| **CLGREEN** | Chlorophyll Green | `(NIR / Green) - 1` |

### Output
1.  **GeoTIFF (.tif)**: File data ilmiah hasil perhitungan (Float32).
2.  **Preview PNG (.png)**: File visualisasi berwarna (RGBA) untuk ditampilkan di Unity.
    *   Menggunakan *Color Map* (Jet/Heatmap) untuk indeks.
    *   Transparansi otomatis untuk area *No Data*.

## 3. Integrasi Unity (`RasterTransformController.cs`)

### Alur Kerja
1.  **User Input**: Memilih file `.tif` input dan algoritma via UI Unity.
2.  **Eksekusi**: Unity menjalankan `rasterTransform.exe` via `System.Diagnostics.Process`.
3.  **Parsing**: Membaca output stream untuk mencari JSON `{"status": "success", ...}`.
4.  **Visualisasi**:
    *   Mencari file **PNG Preview** yang dihasilkan backend.
    *   Menggunakan **TiffLayerManager** untuk menempatkan PNG di peta sesuai koordinat geografis (Bounds).

### Penanganan Masalah (Troubleshooting)
*   **Visual Hitam**:
    *   Pastikan `.exe` backend terbaru sudah digunakan (support Alpha Channel).
    *   Unity otomatis mencari PNG di subfolder `TRANSFORM` jika tidak ditemukan di root.
*   **File Tidak Ditemukan**:
    *   Sistem sekarang melakukan pencarian rekursif di folder `Backend` jika path relatif gagal.

## 4. Cara Penggunaan (User Guide)

1.  Buka Panel **Raster Transform**.
2.  Klik **Pilih Input Imagery** dan pilih file GeoTIFF (Landsat/Sentinel).
3.  Pilih **Algoritma** dari dropdown (misal: NDTI untuk kekeruhan air).
4.  Isi **Suffix Output** (opsional) untuk penamaan file.
5.  Klik **Submit**.
6.  Tunggu status berubah menjadi "Selesai".
7.  Hasil akan otomatis muncul sebagai overlay di peta, dan folder hasil akan terbuka di Explorer.
