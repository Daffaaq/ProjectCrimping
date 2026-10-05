# ProjectCrimping

Aplikasi desktop **WinForms (C#)** untuk operator mesin crimping. Dipakai untuk mencatat hasil pengukuran crimping (N1/N2/N3), request terminal, dan memanggil bantuan (Setter / Maintenance / Penyedia WIP) lengkap dengan pencatatan downtime.

Semua input dirancang untuk **barcode scanner** (operator, mesin, terminal, aplikator, WOS), jadi operator cukup scan lalu tekan Enter.

---

## Fitur Utama

| Modul | Form | Fungsi |
|-------|------|--------|
| Login | `Login` | Scan operator, pilih shift, scan mesin, lalu masuk ke Dashboard |
| Dashboard Crimping | `Dashboard` | Input Terminal → Aplikator → WOS, lalu isi N1 → N2 → N3 dan simpan |
| Input N1 / N2 / N3 | `FormN1`, `FormN2`, `FormN3` | Popup input data pengukuran (CH, IH, dan lainnya) |
| Request Terminal | `FormRequestTerminal` | Scan WOS, pilih Terminal A/B, simpan request ke database |
| Maintenance Call | `FormMaintenanceCall` | Panggil Setter / Maintenance / Penyedia WIP, isi detail problem, simpan downtime |

---

## Tech Stack

- **Bahasa / Framework:** C# + Windows Forms (.NET Framework)
- **UI:** Guna.UI2.WinForms, plus custom control (`RoundedButton`, `RoundedPanel`, `InfoAccordion`, `AvatarCircle`, `SidebarPanel`, `NotifPopup`, dan lainnya)
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

- **Format (client-side):** prefix dan pola barcode.
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
| `Shift` | 1 / 2 |
| `mesin` | Raw barcode, contoh `CRP15-R2` |
| `MesinName`, `MesinArea`, `MesinNo` | Hasil parse dan lookup mesin |
| `IsLoggedIn` | Status login |

### 3. Dashboard Crimping

Urutan input (field dikunci/dibuka bertahap):

1. **TERMINAL**: scan lalu Enter → field terkunci, lanjut ke Aplikator.
2. **APLIKATOR**: scan lalu Enter → terkunci, lanjut ke WOS, tabel riwayat langsung dimuat.
3. **WOS**: scan lalu Enter. WOS **harus mengandung titik** (contoh `6B26031961.0001`) karena `LotNo` diambil dari bagian sebelum titik pertama. Setelah valid, tombol **N1** menjadi *Ready*.

Tombol **N1 → N2 → N3** bekerja berurutan (*Locked* → *Ready*):

| Tahap | Data yang diisi |
|-------|-----------------|
| **N1** | CH, IH, N1, pilih sisi terminal (A/B), scan terminal |
| **N2** | CH, IH |
| **N3** | CH, IH, NG |

Data N1 dan N2 ditahan sementara di `CrimpingState`. **Insert ke database baru terjadi setelah N3 disimpan.**

Setelah insert sukses:

- WOS dikosongkan dan fokus kembali ke WOS (siap scan berikutnya).
- Terminal dan Aplikator **tetap**.
- Status N1/N2/N3 di-reset dan tabel di-refresh.

Kalau insert gagal, N3 kembali *Ready* dan state tetap tersimpan, jadi operator bisa coba lagi.

**Tombol erase** (ikon penghapus di tiap field):

| Erase | Efek |
|-------|------|
| Terminal | Reset total: semua field, status N, dan tabel |
| Aplikator | Reset Aplikator, WOS, status N, dan tabel |
| WOS | Reset WOS dan status N saja (tabel tetap) |

**Tabel riwayat** menampilkan data `Tr_LhhpWH` berdasarkan `terminal + aplikator + noreg` pada **hari produksi** berjalan. Hari produksi dihitung **06:00 sampai 05:59 hari berikutnya**, jadi sebelum jam 06:00 dianggap masih hari kemarin. Kolom N1/N2/N3 diberi centang (`✓`) kalau datanya sudah lengkap.

**Sidebar:** profil operator (nama, noreg), menu *Request Terminal*, *Downtime*, *Export*, accordion info Shift & Mesin, dan tombol *Logout* (dengan konfirmasi).

### 4. Request Terminal

1. Scan **WOS** dengan format `LOTNO.SEQNO` (titik terakhir sebagai pemisah, contoh `LOT001.1`).
2. Sistem mencari di `trrphmesin` dengan `lotno`, `seqnoint`, dan `tanggalhasil IS NULL` (WOS belum selesai).
3. Data `terminala` dan `terminalb` ditampilkan, lalu operator memilih **Terminal A**, **B**, atau keduanya. Terminal yang datanya kosong tidak bisa dipilih.
4. Klik OK lalu konfirmasi, dan data disimpan ke `ReqTerminal` (`mesin`, `wos`, `noreg`, `status = 0`, `created_date = GETDATE()`).

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

Footer form hanya punya satu tombol, **SELESAI** (tombol BATAL sudah dihapus). Tombol ini baru aktif setelah validasi noreg/password berhasil (atau langsung aktif untuk Penyedia WIP). Form masih bisa ditutup dengan tombol **Esc** atau tombol X (`DialogResult.Cancel`), tapi tanpa menyimpan apa pun.

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
| `Tr_LhhpWH` | Hasil N1/N2/N3 (insert dan riwayat) |
| `trrphmesin` | Cek WOS dan data terminal A/B (Request Terminal) |
| `ReqTerminal` | Simpan request terminal |
| `user_MTCSetup` | Validasi Setter / Maintenance (`noreg`, `password`, `MTC`) |
| `TrRPHMesinSetup` | Simpan downtime (jam tunggu/mulai/selesai, lama, keterangan, MTC) |

**Kolom `Tr_LhhpWH` yang di-insert:**
`noreg, terminal, lotno, aplikator, Wos, ch_n1, ih_n1, n1, sisi_terminal_a_n1, sisi_terminal_b_n1, ch_n2, ih_n2, ch_n3, ih_n3, ng_n3, shift, kode_mesin`

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
├── Dashboard.cs              # Dashboard crimping, state N1/N2/N3, tabel riwayat
├── FormN1.cs                 # Popup input N1
├── FormN2.cs                 # Popup input N2
├── FormN3.cs                 # Popup input N3
├── FormRequestTerminal.cs    # Request terminal berdasarkan WOS
├── FormMaintenanceCall.cs    # Panggilan Andon + input downtime
│
├── (diacu oleh kode di atas, tidak ada di berkas ini)
│   ├── DatabaseHelper / DbProvider
│   ├── Session
│   ├── AppThemeLogin, AppThemePopup
│   ├── RoundedButton, RoundedPanel, InfoAccordion, AvatarCircle, SidebarPanel, TransparentPanel
│   └── NotifPopup, FormConfirmLogout
```

---

## Cara Menjalankan

1. Buka solution di **Visual Studio** (project .NET Framework WinForms).
2. Pastikan referensi/NuGet terpasang: `Guna.UI2.WinForms`, `MySql.Data`, dan `System.Data.SqlClient`.
3. Atur connection string SQL Server dan MySQL di `DatabaseHelper` (mode Dev/Prod).
4. Pastikan font/ikon **Segoe MDL2 Assets** tersedia di PC operator (kalau tidak, dipakai fallback emoji).
5. Build lalu jalankan. Form awal adalah `Login`.

> Gunakan barcode scanner dalam mode *keyboard wedge* dengan suffix **Enter**, supaya hasil scan langsung memicu validasi.

---

## Catatan untuk Developer

- Tombol **EXPORT** di sidebar Dashboard sudah ada tapi **belum punya handler**.
- Tombol **BATAL** di Maintenance Call sudah dihapus, tapi menutup form lewat **Esc** / X tetap tidak meng-update `ActivityLog` yang masih berstatus 1/2, jadi record bisa menggantung (masih ada TODO di kode).
- Di footer `FormMaintenanceCall`, `btnFinish` ditambahkan dua kali ke `actions` (`Add(btnFinish, 0, 0)` lalu `Add(btnFinish, 1, 0)`), padahal `actions` sekarang hanya 1 kolom. Sebaiknya hapus baris kedua beserta sisa baris komentar `btnCancel` supaya layout footer tidak bergeser.
- Query jam di `Login` mengasumsikan server SQL sudah di zona waktu WIB. Kalau server di UTC, ganti ke `SELECT DATEADD(HOUR, 7, GETDATE());`.
- `Dashboard` dan `FormMaintenanceCall` memanggil method `downtime_save` pada form induk lewat reflection kalau ada.
- Di `Dashboard`, `Session.ShiftDisplay` dan `Session.MesinDisplay` dipakai di accordion Shift & Mesin.

---

## Versi

Ver **2.1**