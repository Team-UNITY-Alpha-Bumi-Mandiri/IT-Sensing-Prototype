# IT-Sensing-Prototype
Pembuatan aplikasi Desktop berdasarkan website IT Sensing 

✅ CHECKLIST FITUR YANG SUDAH DIBUAT :
- Layout UI (Sudah ada UI) ✔
- Toolbar kanan Peta (Sudah ditambah UI) ✔
- Peta menggunakan Tiles download ✔
- Scroll dan Drag di Peta ✔
- Map Style Switching ✔
- Script Proxy Cloudflare dan No Proxy (Jika misal peta tidak load/block connection) ✔
- Fitur search Lokasi mengikuti nominatim dan ada informasi lokasi ✔
- Map loading fade in supaya tidak menumpuk dan stuttering ✔


●	✅ CHECKLIST LAPORAN DEBUGGING DAN ALASAN
-	UI Peta (drag and scroll) hanya bisa di area Peta supaya tidak mengganggu fungsi UI lain dan panel toolbar
-	Penggunaan Tile agar bisa terlihat yaitu direfresh saat zoom atau drag
-	Pergerakan Zoom dan Drag lebih smooth
-	Zoom range dibatasi (2–19)
-	Untuk switching style Peta Semua tile lama dihancurkan kemudian di keluarkan lagi
-	Style "Terrarin" sudah bisa diakses menggunakan akses google satelite
-	Untuk Mengatasi jika gambar tidak bisa load kemungkinan karena di block dari google maka menggunakan proxy Cloudflare sehingga diberikan 2 script yaitu untuk proxy cloudflare dan tanpa proxy atau normal⚠️
-	Pointer Lokasi masih terlalu random karena mengikuti nominatim dimana tidak menggunakan pointer namun scaling lokasi sehingga pointer berada ditengah gambaran⚠️
-	Gambar peta masih sering stuttering⚠️
-	Ada bug yang membuat pergerakan mouse drag dan zoom inverted (sedang diperbaiki) ⚠️
-	Solusi pencegahan stuttering sebagian tiles sudah pre-fetch dan lazyloading alias loading yang terlambat supaya tidak menumpuk tiles tetapi masih tidak berjalan lancar sepenuhnya ⚠️
