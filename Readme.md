# ProjectCrimping

Aplikasi desktop **WinForms (C#)** untuk operator mesin crimping. Dipakai untuk mencatat hasil pengukuran crimping (N1/N2/N3), request terminal, export laporan, dan memanggil bantuan (Setter / Maintenance / Penyedia WIP) lengkap dengan pencatatan downtime.

Semua input dirancang untuk **barcode scanner** (operator, mesin, lot material, aplikator, WOS), jadi operator cukup scan lalu tekan Enter.

---

## Fitur Utama

| Modul | Form | Fungsi |
|-------|------|--------|
| Login | `Login` | Scan operator, pilih shift, scan mesin, lalu masuk ke Dashboard |
| Dashboard Crimping | `Dashboard` | Scan LOT MATERIAL → Aplikator → WOS, lalu isi N1 → N2 → N3 dan simpan |
| Input N1 / N2 / N3 | `FormN1`, `FormN2`, `FormN3`, `FormConfirmNG` | Popup input ukuran (CH, IH, CW, IW, Tension) dan konfirmasi NG |
| Request Terminal | `FormRequestTerminal` | Scan WOS, pilih Terminal A **atau** B, simpan request ke database |
| Maintenance Call | `FormMaintenanceCall` | Panggil Setter / Maintenance / Penyedia WIP, isi detail problem, simpan downtime |
| Export | (di `Dashboard`) | Membuka laporan PDF crimping di browser |

---

## Tech Stack

- **Bahasa / Framework:** C# + Windows Forms (.NET Framework)
- **UI:** Guna.UI2.WinForms, plus custom control (`RoundedButton`, `RoundedPanel`, `InfoAccordion`, `AvatarCircle`, `AvatarPicture`, `SidebarPanel`, `NotifPopup`, dan lainnya)
- **Ikon:** Segoe MDL2 Assets (dengan fallback emoji)
- **Database:**
  - **SQL Server 2008** via `System.Data.SqlClient` (data utama)
  - **MySQL** via `MySql.Data` (master mesin dan Andon `ActivityLog`)
- **Akses DB:** `DatabaseHelper` dengan `DbProvider.SqlServer` / `DbProvider.MySql` (connection string otomatis ikut mode Dev/Prod)

---

## Alur Aplikasi

### 1. Login

1. Scan **barcode operator**, formatnya `ExcitedCRP<noreg>`, contoh `ExcitedCRP12345`.
2. Pilih **Shift** (Shift 1 / Shift 2).
3. Scan **barcode mesin**, formatnya `CRP<no>-<area>`, contoh `CRP15-R2`.
4. Klik **LOGIN**, atau tekan Enter di field mesin kalau data sudah valid.

Validasi dilakukan dua lapis:

- **Format (client-side):**
  - Operator wajib berawalan `ExcitedCRP` (case-insensitive).
  - Mesin wajib `CRP` + **2 digit** + `-` + `R` + **1 digit** (contoh `CRP01-R2`, `CRP07-R4`).
- **Data (server-side):**
  - Operator dicek ke SQL Server, tabel `DB_PEGAWAI` (kolom `noreg`, `nama`).
  - Mesin dicek ke MySQL, tabel `tabmch` (kolom `MchArea`, `MchNo`, `MchName`).

Kalau valid, muncul *hint chip* (nama operator / nama mesin). Hasil validasi mesin di-cache supaya tidak query ulang saat login.

Jam di halaman login disinkronkan dengan `GETDATE()` SQL Server sekali saat form dibuka, detiknya dihitung pakai `Stopwatch`, dan resync tiap 5 menit. Kalau sync gagal, dipakai jam lokal PC.

### 2. Session

Setelah login, data disimpan di static class `Session`:

| Field | Isi |
|-------|-----|
| `Noreg`, `Nama` | Operator |
| `ext` | Ekstensi file foto profil operator |
| `Shift` | 1 / 2 |
| `mesin` | Raw barcode, contoh `CRP15-R2` |
| `MesinName`, `MesinArea`, `MesinNo` | Hasil parse dan lookup mesin |
| `ShiftDisplay`, `MesinDisplay` | Teks untuk accordion Shift & Mesin |
| `IsLoggedIn` | Status login |

### 3. Dashboard Crimping ("Shopfloor Crimping")

Layout: sidebar kiri (270 px) + konten (Card LOT MATERIAL & APLIKATOR, Card WOS, tombol N, tabel riwayat).

Urutan input (field dikunci/dibuka bertahap):

1. **LOT MATERIAL** (scan lalu Enter)
   - Dicari di `dbInventory2..tblFifo` (`tlotid`) yang di-join ke `dbInventory2..tblItem` untuk mendapat `tiid` dan `tipartnumber`.
   - Kalau tidak ditemukan, muncul peringatan dan field tetap terbuka.
   - Tiga nilai disimpan di state:
     - `LotMaterial`: barcode asli yang discan. **Inilah yang disimpan** ke kolom `terminal` di database.
     - `Tipartnumber`: dipakai untuk cek terminal (prioritas) dan **ditampilkan menggantikan teks di field**.
     - `Tiid`: kunci cadangan untuk cek terminal.
2. **APLIKATOR** (scan lalu Enter)
   - Divalidasi ke tabel `MSAplikator` (`KodeAplikator LIKE '%kode%'`).
   - Kalau valid, field terkunci dan tabel riwayat langsung dimuat.
3. **WOS** (scan lalu Enter)
   - Format harus mengandung titik dan bagian setelah titik terakhir (skema) berupa **1–5 digit angka**, contoh `BEJ81212112.001`.
   - Dicari **patokan** di `KUJNAGN.dbo.TrRPHMesin` (`TOP 1`) dengan syarat `barcode = WOS` dan salah satu kunci cocok:
     - **Prioritas:** `tipartnumber` cocok dengan `TERMINALA` / `TERMINALB`.
     - **Cadangan:** `tiid` cocok dengan `RmiTermA` / `RmiTermB`.
   - Kalau tidak ada yang cocok: peringatan "WOS Tidak Cocok".
   - Dari baris yang cocok, diambil sisi terminal (A/B) dan patokan CH, IH, CW, IW, Tension (`cha/iha/cwa/iwa/T1` untuk sisi A, `chb/ihb/cwb/iwb/T2` untuk sisi B).
   - Dicek duplikat (WOS + noreg + LOT MATERIAL + aplikator pada hari produksi berjalan). Kalau sudah ada, ditolak.
   - Setelah lolos, tombol **N1** menjadi *Ready*.

Urutan scan wajib berurutan; kalau salah urutan, muncul peringatan "Urutan Salah".

**Tombol N (satu tombol dinamis N1 → N2 → N3):**

| Tahap | Data yang diisi |
|-------|-----------------|
| **N1** | CH, IH, CW, IW, Tension (dengan acuan patokan) |
| **N2** | CH, IH, CW, IW |
| **N3** | CH, IH, CW, IW, lalu konfirmasi NG (`FormConfirmNG`, jumlah NG opsional) |

Data N1 dan N2 ditahan sementara di `CrimpingState`. **Insert ke database baru terjadi setelah N3 dan konfirmasi NG selesai.** Sebelum insert, dicek duplikat sekali lagi.

Setelah insert sukses (`ResetAfterSubmit`):

- WOS dikosongkan dan fokus kembali ke WOS (siap scan berikutnya).
- **LOT MATERIAL (beserta Tipartnumber dan Tiid) dan Aplikator tetap**, jadi tidak perlu scan ulang.
- Hasil N1/N2/N3, patokan, dan sisi terminal di-reset; tombol kembali ke N1 *Locked*; tabel di-refresh.

Kalau insert gagal, tombol N3 kembali *Ready* dan state tetap tersimpan, jadi operator bisa coba lagi. Kalau konfirmasi NG dibatalkan, hasil N3 dihapus dan N3 bisa diisi ulang.

**Tombol erase** (ikon penghapus di tiap field):

| Erase | Efek |
|-------|------|
| LOT MATERIAL | Reset total (`ResetNState`): semua field, state (termasuk LotMaterial), status N, dan tabel |
| Aplikator | Reset Aplikator, WOS, hasil N, dan patokan (LOT MATERIAL tetap), tabel dikosongkan |
| WOS | Reset WOS, hasil N, dan patokan (tabel tetap) |

**Tabel riwayat** menampilkan data `TrCrimpingResult` berdasarkan `terminal (LOT MATERIAL) + aplikator + noreg` pada **hari produksi** berjalan. Hari produksi dihitung **06:00 sampai 05:59 hari berikutnya**, jadi sebelum jam 06:00 dianggap masih hari kemarin. Kolom: NO, LOT NUMBER, SKEMA, TERMINAL, APLIKATOR, CH/IH/CW/IW untuk N1–N3, NG, TENSION. LOT NUMBER dan SKEMA diambil dari WOS (dipisah di titik terakhir).

**Sidebar:** foto/inisial operator, nama dan noreg, menu *Request Terminal*, *Downtime*, *Export*, accordion info Shift & Mesin, dan tombol *Logout* (dengan konfirmasi).

Foto profil dimuat async dari `AvatarBaseUrl + noreg + "." + ext` (timeout 5 detik). Kalau gagal, dipakai inisial.

**Export:** membuka laporan PDF di browser (`laporan-crimping-manual/controllers/export.php?action=export_pdf`) dengan parameter `noreg`, `tanggal` (tanggal produksi dari server), `aplikator`, `terminal` (LOT MATERIAL asli), dan `mesin`. LOT MATERIAL dan Aplikator harus sudah terisi. Lookup tipartnumber dilakukan di sisi laporan (PHP).

### 4. Request Terminal

1. Scan **WOS** dengan format `LOTNO.SEQNO` (titik terakhir sebagai pemisah, contoh `LOT001.1`).
2. Sistem mencari di `trrphmesin` dengan `lotno` dan `seqnoint` (`TOP 1`), lalu mengambil `terminala` dan `terminalb`.
3. Data terminal A dan B ditampilkan, lalu operator memilih **Terminal A atau Terminal B (hanya salah satu)**. Terminal yang datanya kosong tidak bisa dipilih. Kalau keduanya kosong, ditolak.
4. Klik OK lalu konfirmasi (SIMPAN / BATAL), dan data disimpan ke `ReqTerminal` (`mesin`, `terminal`, `noreg`, `wos`, `status = 0`, `created_date = GETDATE()`).

Catatan: nilai yang disimpan di kolom `terminal` adalah **isi** terminal yang dipilih (`terminala` / `terminalb`), bukan label "A"/"B".

Setiap scan baru mereset hasil scan dan pilihan sebelumnya. Teks WOS yang diubah setelah scan harus discan ulang sebelum bisa disimpan.

### 5. Maintenance Call (Downtime)

Ada tiga kartu panggilan. Hanya satu panggilan yang bisa aktif dalam satu waktu.

| Kartu | `CallID` | Validasi |
|-------|----------|----------|
| Setter | `4` | Input **noreg** (`user_MTCSetup`, `MTC = 0`) |
| Maintenance | `1` | Input **password** (`user_MTCSetup`, `MTC = 1`) |
| Penyedia WIP | `3` | Tanpa validasi; otomatis DANDORI / "Tunggu WIP" |

Alur:

1. Klik kartu → `INSERT` ke `ActivityLog` (MySQL) dengan `StatusID = 1` (dipanggil). Untuk Penyedia WIP langsung diubah ke `StatusID = 2` (waiting).
2. Setter / Maintenance memasukkan noreg / password → OK → `StatusID` jadi `2` dan `jam mulai` tercatat.
3. Operator memilih **Problem** (kategori) dan **Jenis Problem** (detail), lalu dropdown dinamis menyesuaikan:
   - **PROBLEM MESIN / WIRE SEAL:** detail kerusakan (`cmbExtra1`) lalu aktivitas perbaikan (`cmbExtra2`).
   - **PROBLEM APLIKATOR:** `cmbExtra1` dan `cmbExtra2` tampil.
   - **Kategori lain** (DANDORI, PLAN DOWNTIME, MINORSTOP): cukup textbox keterangan.
4. Klik **SELESAI** → `ActivityLog` di-update (`StatusID = 3`, `TimeFinish`), lalu downtime disimpan ke `TrRPHMesinSetup`.

Footer form hanya punya satu tombol, **SELESAI** (aktif setelah validasi noreg/password berhasil, atau langsung untuk Penyedia WIP). Form bisa ditutup dengan **Esc** atau tombol X (`DialogResult.Cancel`), tanpa menyimpan apa pun.

Kategori problem: `PROBLEM MESIN`, `PROBLEM APLIKATOR`, `PROBLEM WIRE SEAL`, `DANDORI`, `PLAN DOWNTIME`, `MINORSTOP`.

Waktu server diambil sekali saat form dibuka, lalu dihitung lewat offset terhadap jam PC (tidak query tiap detik).

**Konversi kode mesin → `MchID` Andon** (`GetMachineID`):

| Barcode mesin | MchID |
|---------------|-------|
| `CSMxx` | `2` + 2 digit terakhir |
| `KMXxx` | `3` + 2 digit terakhir |
| `CRPnn-R2` | `nn` |
| `CRPnn-R4` | `100 + nn` |
| Kosong | `999` |

---

## Struktur Database

### SQL Server

| Tabel | Dipakai untuk |
|-------|---------------|
| `DB_PEGAWAI` | Lookup nama operator dari `noreg` (Login) |
| `dbInventory2..tblFifo` | Lookup `tiid` dari LOT MATERIAL (`tlotid`) |
| `dbInventory2..tblItem` | Lookup `tipartnumber` dari `tiid` |
| `MSAplikator` | Validasi kode aplikator |
| `KUJNAGN.dbo.TrRPHMesin` | Patokan CH/IH/CW/IW/Tension per WOS (`barcode`) dan kecocokan terminal |
| `TrCrimpingResult` | Hasil N1/N2/N3 (insert, riwayat, cek duplikat) |
| `trrphmesin` | Cek WOS dan data terminal A/B (Request Terminal) |
| `ReqTerminal` | Simpan request terminal |
| `user_MTCSetup` | Validasi Setter / Maintenance (`noreg`, `password`, `MTC`) |
| `TrRPHMesinSetup` | Simpan downtime (jam tunggu/mulai/selesai, lama, keterangan, MTC) |

**Kolom `TrCrimpingResult` yang di-insert:**
`noreg, terminal (LOT MATERIAL asli), lotno, aplikator, Wos, ch_n1, ih_n1, cw_n1, iw_n1, tension, ch_n2, ih_n2, cw_n2, iw_n2, ch_n3, ih_n3, cw_n3, iw_n3, ng_n3, shift, kode_mesin, created_at (GETDATE())`

`lotno` diambil dari bagian WOS sebelum titik pertama.

### MySQL

| Tabel | Dipakai untuk |
|-------|---------------|
| `tabmch` | Master mesin (`MchName`, `MchArea`, `MchNo`) |
| `ActivityLog` | Log panggilan Andon (`MchID`, `CallID`, `StatusID`, `TimeCall`, `TimeWaiting`, `TimeFinish`) |

**`StatusID` ActivityLog:** `1` = dipanggil, `2` = menunggu / sedang ditangani, `3` = selesai.

---

## Struktur Kode

```
ProjectCrimping/
├── Login.cs                  # Login (operator, shift, mesin) + sinkron jam server
├── Dashboard.cs              # Dashboard crimping, state N1/N2/N3, tabel riwayat, export
├── FormN1.cs                 # Popup input N1 (+ patokan)
├── FormN2.cs                 # Popup input N2 (+ patokan)
├── FormN3.cs                 # Popup input N3 (+ patokan)
├── FormRequestTerminal.cs    # Request terminal berdasarkan WOS (pilih A atau B)
├── FormMaintenanceCall.cs    # Panggilan Andon + input downtime
│
├── (diacu oleh kode di atas, tidak ada di berkas ini)
│   ├── DatabaseHelper / DbProvider
│   ├── Session
│   ├── AppThemeLogin, AppThemePopup
│   ├── RoundedButton, RoundedPanel, InfoAccordion, AvatarCircle, AvatarPicture, SidebarPanel, TransparentPanel
│   └── NotifPopup, FormConfirmLogout, FormConfirmNG
```

---

## Cara Menjalankan

1. Buka solution di **Visual Studio** (project .NET Framework WinForms).
2. Pastikan referensi/NuGet terpasang: `Guna.UI2.WinForms`, `MySql.Data`, dan `System.Data.SqlClient`.
3. Atur connection string SQL Server dan MySQL di `DatabaseHelper` (mode Dev/Prod).
4. Pastikan font/ikon **Segoe MDL2 Assets** tersedia di PC operator (kalau tidak, dipakai fallback emoji).
5. Pastikan PC operator bisa mengakses server foto profil dan server laporan (lihat konfigurasi di bawah).
6. Build lalu jalankan. Form awal adalah `Login`.

> Gunakan barcode scanner dalam mode *keyboard wedge* dengan suffix **Enter**, supaya hasil scan langsung memicu validasi.

**Konfigurasi URL (hardcoded di `Dashboard.cs`):**

| Konstanta | Fungsi |
|-----------|--------|
| `AvatarBaseUrl` | Lokasi foto profil operator |
| `baseUrl` (di `ExportPdf`) | Endpoint laporan PDF crimping |

---

## Catatan untuk Developer

- Ada **tiga nilai** dari LOT MATERIAL, jangan tertukar: `LotMaterial` (disimpan ke DB), `Tipartnumber` (cek terminal + tampilan), `Tiid` (cadangan).
- Ada **dua jenis reset**: `ResetNState()` menghapus semua termasuk LotMaterial; `ResetAfterSubmit()` mempertahankan LotMaterial + Tipartnumber + Tiid.
- `ClearPatokanOnly()` sengaja tidak menghapus LotMaterial/Tipartnumber/Tiid.
- `IsWosDuplicate` mengembalikan `true` saat query error (fail-safe), jadi gangguan koneksi akan menolak WOS, bukan meloloskannya.
- `CheckAplikator` memakai `LIKE '%kode%'`, jadi kecocokan bersifat parsial.
- Di `FormRequestTerminal`, komentar kode menyebut `tanggalhasil IS NULL`, tetapi query saat ini **tidak** memfilter kolom itu. Sesuaikan komentar atau tambahkan filter jika memang dibutuhkan.
- Tombol **BATAL** di Maintenance Call sudah dihapus, tapi menutup form lewat **Esc** / X tetap tidak meng-update `ActivityLog` yang masih berstatus 1/2, jadi record bisa menggantung (masih ada TODO di kode).
- Di footer `FormMaintenanceCall`, `btnFinish` ditambahkan dua kali ke `actions` (`Add(btnFinish, 0, 0)` lalu `Add(btnFinish, 1, 0)`), padahal `actions` sekarang hanya 1 kolom. Sebaiknya hapus baris kedua beserta sisa komentar `btnCancel`.
- Query jam di `Login` mengasumsikan server SQL sudah di zona waktu WIB. Kalau server di UTC, ganti ke `SELECT DATEADD(HOUR, 7, GETDATE());`.
- `Dashboard` dan `FormMaintenanceCall` memanggil method `downtime_save` pada form induk lewat reflection kalau ada.
- URL avatar dan laporan masih hardcoded; pertimbangkan memindahkannya ke konfigurasi.

---

## Versi

Ver **2.2**