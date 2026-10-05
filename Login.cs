using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MySql.Data.MySqlClient;

namespace ProjectCrimping
{
    /// <summary>
    /// Form Login utama aplikasi.
    /// 
    /// Alur kerja:
    ///   1. Operator scan barcode (format: ExcitedCRP<noreg>)
    ///   2. Pilih shift dari dropdown
    ///   3. Operator scan barcode mesin (format: CRP<no>-<area>, contoh CRP15-R2)
    ///   4. Klik LOGIN → data disimpan ke Session → (nanti) buka Dashboard
    /// 
    /// Validasi dilakukan dua lapis:
    ///   - Validasi FORMAT di sisi client (cepat, tanpa hit DB)
    ///   - Validasi DATA di sisi server (query DB untuk cek exist/valid)
    /// </summary>
    public partial class Login : Form
    {
        // Cache background supaya tidak di-render ulang tiap repaint (hemat CPU)
        private readonly AppThemeLogin.BackgroundCache _bgCache = new AppThemeLogin.BackgroundCache();

        // Timer untuk update jam & tanggal realtime di UI
        private readonly System.Windows.Forms.Timer _clockTimer = new System.Windows.Forms.Timer();

        // Culture Indonesia → format tanggal "Senin, 12 Mei 2025"
        private static readonly CultureInfo IdCulture = new CultureInfo("id-ID");

        // Teks status default di footer
        private const string StatusNormal = "Online   ·   Ver 2.0   ·   Protected";

        // ================================================================
        //  KONSTANTA PREFIX BARCODE
        // ================================================================
        // Operator: "ExcitedCRP" + noreg → contoh "ExcitedCRP12345"
        private const string OperatorPrefix = "ExcitedCRP";

        // Mesin: "CRP" + no + "-" + area → contoh "CRP15-R2"
        private const string MesinPrefix = "CRP";

        // Flag untuk mencegah double-click / re-entry saat proses login
        private bool _busy;

        // Helper akses database (SQL Server + MySQL via provider)
        private readonly DatabaseHelper _db = new DatabaseHelper();

        // ================================================================
        //  SINKRONISASI JAM DENGAN SQL SERVER (SQL Server 2008)
        //  - Ambil GETDATE() SEKALI saat form dibuka
        //  - Detik dihitung manual pakai Stopwatch (tanpa query ulang)
        //  - Resync tiap 5 menit untuk mencegah drift
        // ================================================================
        private DateTime _dbBaseTime;              // waktu server saat terakhir sync
        private readonly System.Diagnostics.Stopwatch _dbClockWatch = new System.Diagnostics.Stopwatch();
        private bool _dbClockReady = false;        // true kalau sudah pernah sync sukses

        // Interval resync (ms). 5 menit = 300_000
        private const int ResyncIntervalMs = 5 * 60 * 1000;
        private int _msSinceLastSync = 0;


        // Helper DB khusus sync jam (SQL Server)
        private readonly DatabaseHelper _dbSync = new DatabaseHelper(DbProvider.SqlServer);

        // ================================================================
        //  FIELD BANTU UNTUK MENYIMPAN HASIL VALIDASI MESIN
        //  Di-cache supaya tidak perlu query ulang saat proses login.
        // ================================================================
        private string _lastMesinRaw = null;   // raw scan, contoh: "CRP15-R2"
        private string _lastMchName = null;   // nama mesin, contoh: "Crimping"
        private string _lastMchArea = null;   // area mesin, contoh: "R2"
        private string _lastMchNo = null;   // nomor mesin, contoh: "15"

        public Login()
        {
            // Load font lokal (custom font dari resource/folder aplikasi)
            AppThemeLogin.LoadLocalFonts();
            InitializeComponent();

            // ================================================================
            // ISI DROPDOWN SHIFT
            // Menggunakan ShiftItem (custom class) supaya bisa simpan
            // Text ("Shift 1") dan Value ("1") secara terpisah.
            // ================================================================
            cmbShift.Items.Clear();
            cmbShift.Items.Add(new ShiftItem("Shift 1", "1"));
            cmbShift.Items.Add(new ShiftItem("Shift 2", "2"));
            cmbShift.SelectedIndex = 0; // default: Shift 1

            // ================================================================
            // APPLY THEME
            // Semua styling dipusatkan di AppThemeLogin supaya konsisten
            // dan mudah diubah tanpa sentuh Designer.
            // ================================================================
            AppThemeLogin.StyleBackground(bgPanel, _bgCache);

            AppThemeLogin.StyleBadgePill(badgePill, lblBadgeIcon, lblBadgeText);
            AppThemeLogin.StyleLeftTitle(lblLeftTitle, pnlAccentBar, lblLeftSubtitle);
            AppThemeLogin.StyleCrimpingGraphic(networkPanel);
            AppThemeLogin.StyleClock(lblClock, lblDate);

            AppThemeLogin.StyleBrandBadge(brandBadge, lblBrandInitial);
            AppThemeLogin.StyleCardBrand(lblBrandTitle, lblBrandSubtitle);
            AppThemeLogin.StyleCardHeading(lblTitle, lblSubtitle);

            AppThemeLogin.StyleFieldLabel(lblFieldOperator);
            AppThemeLogin.StyleFieldLabel(lblFieldShift);
            AppThemeLogin.StyleFieldLabel(lblFieldMesin);

            AppThemeLogin.StyleErrorLabel(lblErrOperator);
            AppThemeLogin.StyleErrorLabel(lblErrShift);
            AppThemeLogin.StyleErrorLabel(lblErrMesin);

            // Chip hint (badge verified) → muncul saat operator/mesin valid
            AppThemeLogin.StyleHintChip(lblHintOperator);
            AppThemeLogin.StyleHintChip(lblHintMesin);

            // Container field (kotak input + icon di dalamnya)
            AppThemeLogin.StyleFieldContainer(pnlFieldOperator, lblIconOperator, txtNoRegOperator,
                AppThemeLogin.IconPerson, AppThemeLogin.FallbackPerson);
            AppThemeLogin.StyleFieldContainer(pnlFieldShift, lblIconShift, cmbShift,
                AppThemeLogin.IconShift, AppThemeLogin.FallbackShift);
            AppThemeLogin.StyleFieldContainer(pnlFieldMesin, lblIconMesin, txtMesin,
                AppThemeLogin.IconGear, AppThemeLogin.FallbackGear);

            // TextBox & ComboBox flat style + placeholder
            AppThemeLogin.StyleTextBoxFlat(txtNoRegOperator, "Scan barcode operator");
            AppThemeLogin.StyleComboBoxFlat(cmbShift);
            AppThemeLogin.StyleTextBoxFlat(txtMesin, "Scan barcode mesin");

            AppThemeLogin.StylePrimaryButton(btnLogin, "LOGIN");
            AppThemeLogin.StyleFooter(pnlStatusDot, lblFooterStatus, lblFooterCompany);
            lblFooterStatus.Text = StatusNormal;

            // ================================================================
            // PASTIKAN CHIP & ERROR DI LAYER PALING ATAS
            // Supaya tidak ketutup oleh container field.
            // ================================================================
            lblHintOperator.BringToFront();
            lblHintMesin.BringToFront();
            lblErrOperator.BringToFront();
            lblErrMesin.BringToFront();

            // ================================================================
            // EVENT HANDLERS
            // ================================================================

            // ---- Operator ----
            // Setiap kali user mengetik → bersihkan error & hint lama
            txtNoRegOperator.TextChanged += (s, e) =>
            {
                AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
                AppThemeLogin.ClearHintChip(lblHintOperator);
            };

            // Enter di field operator → lookup ke DB
            // Kalau valid → auto-focus ke combo shift
            txtNoRegOperator.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // cegah bunyi "ding"
                    await LookupOperatorAsync();

                    if (lblHintOperator.Visible)
                        cmbShift.Focus();
                }
            };

            // Saat pindah field (blur) → auto lookup kalau belum divalidasi
            txtNoRegOperator.Leave += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtNoRegOperator.Text) &&
                    !lblHintOperator.Visible)
                {
                    await LookupOperatorAsync();
                }
            };

            // ---- Shift ----
            cmbShift.SelectedIndexChanged += (s, e) =>
                AppThemeLogin.ClearFieldError(pnlFieldShift, lblErrShift);

            // Enter di combo shift → pindah fokus ke field mesin
            cmbShift.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !cmbShift.DroppedDown)
                {
                    e.SuppressKeyPress = true;
                    txtMesin.Focus();
                }
            };

            // ---- Mesin ----
            txtMesin.TextChanged += (s, e) =>
            {
                AppThemeLogin.ClearFieldError(pnlFieldMesin, lblErrMesin);
                AppThemeLogin.ClearHintChip(lblHintMesin);

                // Reset cache mesin saat text berubah
                // → supaya tidak ada data "basi" dari scan sebelumnya
                _lastMesinRaw = null;
                _lastMchName = null;
                _lastMchArea = null;
                _lastMchNo = null;
            };

            // Enter di field mesin → lookup, kalau valid langsung login
            txtMesin.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LookupMesinAsync();

                    if (lblHintMesin.Visible)
                        await BtnLogin_Click();
                }
            };

            txtMesin.Leave += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtMesin.Text) &&
                    !lblHintMesin.Visible)
                {
                    await LookupMesinAsync();
                }
            };

            // Tombol LOGIN
            btnLogin.Click += async (s, e) => await BtnLogin_Click();

            // ---- Jam realtime (sync dengan SQL Server) ----
            _dbBaseTime = DateTime.Now;    // nilai awal (fallback), langsung ditimpa setelah sync
            _dbClockWatch.Start();

            UpdateClock();                 // tampilkan dulu pakai jam lokal

            _clockTimer.Interval = 1000;
            _clockTimer.Tick += (s, e) =>
            {
                UpdateClock();

                // ---- Resync periodik tiap 5 menit ----
                _msSinceLastSync += _clockTimer.Interval;
                if (_msSinceLastSync >= ResyncIntervalMs)
                {
                    _msSinceLastSync = 0;
                    _ = SyncClockFromServerAsync();   // fire & forget
                }
            };
            _clockTimer.Start();

            // Dispose timer & stopwatch saat form ditutup (cegah memory leak)
            Disposed += (s, e) =>
            {
                _clockTimer.Dispose();
                _dbClockWatch.Stop();
            };

            // Auto-focus ke field operator saat form muncul
            Shown += async (s, e) =>
            {
                txtNoRegOperator.Focus();

                // Sync pertama kali (async, non-blocking)
                await SyncClockFromServerAsync();
            };

            // ---- Resize ----
            // Posisi konten dihitung manual supaya layout tetap rapi
            // di berbagai ukuran layar.
            PositionContent();
            bgPanel.Resize += (s, e) =>
            {
                PositionContent();
                bgPanel.Invalidate(); // repaint background
            };

            LoadBrandLogo(picLogo, brandBadge);

            // Pastikan form panel ada di atas background
            formPanel.BringToFront();
        }

        // ================================================================
        //  HELPER: strip prefix "ExcitedCRP" dari barcode operator
        //  Return null kalau format gak valid / kosong.
        //
        //  Contoh:
        //    "ExcitedCRP12345" → "12345"
        //    "12345"           → null (tidak ada prefix)
        //    "ExcitedCRP"      → null (noreg kosong)
        // ================================================================
        private static string ExtractNoreg(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return null;

            string trimmed = rawInput.Trim();

            // Harus diawali "ExcitedCRP" (case-insensitive)
            if (!trimmed.StartsWith(OperatorPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            // Ambil sisa setelah prefix
            string noreg = trimmed.Substring(OperatorPrefix.Length).Trim();

            return string.IsNullOrEmpty(noreg) ? null : noreg;
        }

        // ================================================================
        //  HELPER: parse barcode mesin (FORMAT BARU)
        //
        //  Format: CRP<no>-<area>
        //    CRP15-R2  → area "R2", no "15"
        //    CRP01-R2  → area "R2", no "01"
        //    CRP07-R4  → area "R4", no "07"
        //
        //  Return (Area, No) atau null kalau format gak valid.
        // ================================================================
        private static (string Area, string No)? ExtractMesin(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return null;

            // Normalisasi ke uppercase supaya konsisten
            string trimmed = rawInput.Trim().ToUpperInvariant();

            // Harus diawali "CRP"
            if (!trimmed.StartsWith(MesinPrefix, StringComparison.Ordinal))
                return null;

            // Cari pemisah "-" setelah prefix
            int dash = trimmed.IndexOf('-', MesinPrefix.Length);
            if (dash == -1)
                return null;

            // Bagian no: antara "CRP" dan "-"
            string no = trimmed.Substring(MesinPrefix.Length, dash - MesinPrefix.Length);

            // Bagian area: setelah "-"
            string area = trimmed.Substring(dash + 1);

            // Validasi no: digit semua, wajib 2 digit (mis. "01", "15")
            if (no.Length != 2 || !no.All(char.IsDigit))
                return null;

            // Validasi area: harus "R" + 1 digit (mis. "R2", "R4")
            if (area.Length != 2 || area[0] != 'R' || !char.IsDigit(area[1]))
                return null;

            return (area, no);
        }

        // ================================================================
        //  LOOKUP OPERATOR
        //  Validasi prefix barcode → query SQL Server (DB_PEGAWAI).
        //  Hasil:
        //    - valid   → tampilkan nama di hint chip
        //    - invalid → tampilkan error di bawah field
        // ================================================================
        private async Task LookupOperatorAsync()
        {
            string raw = txtNoRegOperator.Text.Trim();
            if (string.IsNullOrEmpty(raw)) return;

            // Step 1: validasi format
            string noreg = ExtractNoreg(raw);

            if (noreg == null)
            {
                AppThemeLogin.ClearHintChip(lblHintOperator);
                AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                    "Noreg Tidak Sesuai Format");
                return;
            }

            try
            {
                // Step 2: query DB di background thread (biar UI tidak freeze)
                string nama = await Task.Run(() =>
                {
                    using (var conn = _db.Open())
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 nama FROM DB_PEGAWAI WHERE noreg = @noreg", (SqlConnection)conn))
                    {
                        cmd.CommandTimeout = 10; // timeout 10 detik
                        cmd.Parameters.AddWithValue("@noreg", noreg);

                        var result = cmd.ExecuteScalar();
                        return (result == null || result == DBNull.Value)
                            ? null
                            : result.ToString();
                    }
                });

                if (string.IsNullOrEmpty(nama))
                {
                    // Noreg gak ketemu di DB
                    AppThemeLogin.ClearHintChip(lblHintOperator);
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "No Reg Operator tidak ditemukan");
                }
                else
                {
                    // Valid → tampilkan nama operator
                    AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
                    AppThemeLogin.SetHintChip(lblHintOperator, nama);
                }
            }
            catch (Exception ex)
            {
                AppThemeLogin.ClearHintChip(lblHintOperator);
                AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                    "Gagal akses DB: " + ex.Message);
            }
        }

        // ================================================================
        //  LOOKUP MESIN
        //  Validasi format → query MySQL (tabel tabmch).
        //  Kalau valid, simpan hasilnya ke field bantu (_lastMesinRaw, dll)
        //  supaya tidak perlu query ulang saat login.
        // ================================================================
        private async Task LookupMesinAsync()
        {
            string raw = txtMesin.Text.Trim();
            if (string.IsNullOrEmpty(raw)) return;

            // Step 1: validasi format barcode
            var parsed = ExtractMesin(raw);

            if (parsed == null)
            {
                AppThemeLogin.ClearHintChip(lblHintMesin);
                AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                    "Format barcode mesin tidak valid (contoh: CRP15-R2)");
                return;
            }

            string mchArea = parsed.Value.Area;   // "R2" / "R4"
            string mchNo = parsed.Value.No;     // "01", "15", dst.

            try
            {
                // Step 2: query MySQL di background thread
                string mchName = await Task.Run(() =>
                {
                    // Pakai provider MySQL (beda dari SQL Server default)
                    var dbMy = new DatabaseHelper(DbProvider.MySql);

                    using (var conn = dbMy.Open())
                    using (var cmd = new MySqlCommand(
                        @"SELECT MchName 
                          FROM   tabmch 
                          WHERE  MchArea = @area 
                            AND  MchNo   = @no
                          LIMIT 1", (MySqlConnection)conn))
                    {
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.AddWithValue("@area", mchArea);
                        cmd.Parameters.AddWithValue("@no", mchNo);

                        var result = cmd.ExecuteScalar();
                        return (result == null || result == DBNull.Value)
                            ? null
                            : result.ToString();
                    }
                });

                if (string.IsNullOrEmpty(mchName))
                {
                    // Mesin gak ketemu di DB
                    AppThemeLogin.ClearHintChip(lblHintMesin);
                    AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                        "Mesin tidak ditemukan");
                }
                else
                {
                    // Valid → simpan hasil validasi ke field bantu
                    // (dipakai nanti di BtnLogin_Click)
                    _lastMesinRaw = raw;
                    _lastMchName = mchName;
                    _lastMchArea = mchArea;
                    _lastMchNo = mchNo;

                    AppThemeLogin.ClearFieldError(pnlFieldMesin, lblErrMesin);
                    AppThemeLogin.SetHintChip(lblHintMesin, $"{mchName} · {mchArea} No {mchNo}");
                }
            }
            catch (Exception ex)
            {
                AppThemeLogin.ClearHintChip(lblHintMesin);
                AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                    "Gagal akses DB: " + ex.Message);
            }
        }

        // ================================================================
        //  SINKRONISASI JAM DENGAN SQL SERVER 2008
        //  - Ambil GETDATE() SEKALI saat form dibuka
        //  - Detik selanjutnya dihitung manual pakai Stopwatch
        //  - Resync tiap 5 menit
        //
        //  Catatan: query ini mengasumsikan server DB sudah timezone WIB.
        //  Kalau server di UTC, ganti jadi:
        //    SELECT DATEADD(HOUR, 7, GETDATE());
        // ================================================================
        private async Task SyncClockFromServerAsync()
        {
            try
            {
                DateTime serverTime = await Task.Run(() =>
                {
                    using (var conn = _dbSync.Open())
                    using (var cmd = new SqlCommand("SELECT GETDATE();", (SqlConnection)conn))
                    {
                        cmd.CommandTimeout = 5;
                        object result = cmd.ExecuteScalar();

                        if (result == null || result == DBNull.Value)
                            throw new Exception("GETDATE() returned null");

                        return Convert.ToDateTime(result);
                    }
                });

                _dbBaseTime = serverTime;
                _dbClockWatch.Restart();   // reset stopwatch dari 0
                _dbClockReady = true;
                _msSinceLastSync = 0;

                System.Diagnostics.Debug.WriteLine(
                    $"[CLOCK] Synced with SQL Server: {_dbBaseTime:yyyy-MM-dd HH:mm:ss}");

                // Update UI langsung biar gak nunggu tick berikutnya
                UpdateClock();
            }
            catch (Exception ex)
            {
                // Kalau gagal sync → tetap pakai jam lokal (fallback di UpdateClock)
                System.Diagnostics.Debug.WriteLine("[CLOCK] Sync gagal: " + ex.Message);
            }
        }

        // ================================================================
        //  Update jam & tanggal realtime di UI
        //  - Kalau sudah sync dengan server → pakai base time + elapsed
        //  - Kalau belum → fallback ke jam lokal
        // ================================================================
        private void UpdateClock()
        {
            DateTime now;

            if (_dbClockReady)
            {
                // Waktu server = base + elapsed stopwatch
                now = _dbBaseTime.Add(_dbClockWatch.Elapsed);
            }
            else
            {
                // Fallback: kalau belum sempat sync (mis. DB down), pakai jam lokal
                now = DateTime.Now;
            }

            lblClock.Text = now.ToString("HH:mm:ss", IdCulture);
            lblDate.Text = now.ToString("dddd, dd MMMM yyyy", IdCulture);
        }

        // ================================================================
        //  Hitung posisi konten (left panel & form panel) saat resize.
        //  Layout split: kiri = branding, kanan = form login.
        // ================================================================
        private void PositionContent()
        {
            int w = bgPanel.ClientSize.Width;
            int h = bgPanel.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            // Lebar area kiri = SplitRatio × lebar total
            int leftW = (int)(w * AppThemeLogin.SplitRatio);

            const int leftMargin = 56;
            // Batasi lebar konten kiri: min 300px, max 480px
            leftContent.Width = Math.Max(300, Math.Min(480, leftW - leftMargin * 2));
            leftContent.Left = leftMargin;
            leftContent.Top = Math.Max(40, (h - leftContent.Height) / 2); // center vertikal

            // Form panel di sisi kanan → center di area kanan
            int rightW = w - leftW;
            formPanel.Left = leftW + (rightW - formPanel.Width) / 2;
            formPanel.Top = Math.Max(24, (h - formPanel.Height) / 2);
        }

        // ================================================================
        //  Load logo brand.
        //  Saat ini hanya toggle visibility (badge teks dipakai sebagai
        //  fallback kalau gambar logo tidak tersedia).
        // ================================================================
        private void LoadBrandLogo(PictureBox picLogo, Panel brandBadge)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.StartupPath, "images", "logoipg.png");
                Image logo = System.IO.File.Exists(path) ? Image.FromFile(path) : null;
                if (logo == null) throw new Exception("Logo tidak ditemukan");

                picLogo.Image = logo;
                picLogo.SizeMode = PictureBoxSizeMode.Zoom;   // proporsi gambar tetap
                picLogo.BackColor = Color.Transparent;

                // Samakan posisi & ukuran dengan badge lama
                picLogo.Bounds = brandBadge.Bounds;

                picLogo.Visible = true;
                picLogo.BringToFront();
                brandBadge.Visible = false;
            }
            catch
            {
                // Fallback: kalau logo gagal dimuat, tampilkan badge "CS" lagi
                picLogo.Visible = false;
                brandBadge.Visible = true;
            }
        }
        // ================================================================
        //  PROSES LOGIN
        //
        //  Alur:
        //    1. Ambil & validasi semua input
        //    2. Validasi format (client-side)
        //    3. Validasi data (server-side, kalau belum divalidasi)
        //    4. Simpan ke Session
        //    5. (Opsional) buka Dashboard
        // ================================================================
        private async Task BtnLogin_Click()
        {
            // Guard: cegah double-click / re-entry
            if (_busy) return;

            try
            {
                // ---- Ambil nilai input ----
                string rawOperator = txtNoRegOperator.Text.Trim();
                string noRegOperator = ExtractNoreg(rawOperator);

                string shiftValue = (cmbShift.SelectedItem as ShiftItem)?.Value ?? "";
                string mesin = txtMesin.Text.Trim();

                // Reset semua error dulu sebelum validasi ulang
                AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
                AppThemeLogin.ClearFieldError(pnlFieldShift, lblErrShift);
                AppThemeLogin.ClearFieldError(pnlFieldMesin, lblErrMesin);

                // Control pertama yang invalid → untuk auto-focus
                Control firstInvalid = null;

                // ---- Validasi Operator ----
                if (string.IsNullOrEmpty(rawOperator))
                {
                    // Kosong
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "No Reg Operator wajib diisi");
                    firstInvalid = firstInvalid ?? txtNoRegOperator;
                }
                else if (noRegOperator == null)
                {
                    // Format prefix salah
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "Format barcode tidak valid");
                    firstInvalid = firstInvalid ?? txtNoRegOperator;
                }
                else if (!lblHintOperator.Visible)
                {
                    // Belum divalidasi → coba lookup sekarang
                    await LookupOperatorAsync();
                    if (!lblHintOperator.Visible)
                        firstInvalid = firstInvalid ?? txtNoRegOperator;
                }

                // ---- Validasi Shift ----
                if (string.IsNullOrEmpty(shiftValue))
                {
                    AppThemeLogin.SetFieldError(pnlFieldShift, lblErrShift, "Silakan pilih shift");
                    firstInvalid = firstInvalid ?? cmbShift;
                }

                // ---- Validasi Mesin ----
                if (string.IsNullOrEmpty(mesin))
                {
                    AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin, "Mesin wajib diisi");
                    firstInvalid = firstInvalid ?? txtMesin;
                }
                else if (ExtractMesin(mesin) == null)
                {
                    // Format barcode mesin salah
                    AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                        "Format barcode mesin tidak valid");
                    firstInvalid = firstInvalid ?? txtMesin;
                }
                else if (!lblHintMesin.Visible)
                {
                    // Belum divalidasi → coba lookup sekarang
                    await LookupMesinAsync();
                    if (!lblHintMesin.Visible)
                        firstInvalid = firstInvalid ?? txtMesin;
                }

                // Kalau ada yang invalid → fokus ke field pertama yang error & stop
                if (firstInvalid != null)
                {
                    firstInvalid.Focus();
                    return;
                }

                // ============================================================
                //  MULAI PROSES LOGIN
                // ============================================================
                _busy = true;
                btnLogin.Enabled = false;
                btnLogin.Text = "Memproses...";
                lblFooterStatus.Text = "Memproses login...";
                lblFooterStatus.ForeColor = AppThemeLogin.ColorAccent;

                // ============================================================
                //  ISI SESSION
                //  Session dipakai sebagai "global state" untuk form lain.
                // ============================================================
                Session.Noreg = noRegOperator;
                Session.Nama = lblHintOperator.Text;
                Session.Shift = int.TryParse(shiftValue, out int sh) ? sh : 0;
                Session.IsLoggedIn = true;

                // ============================================================
                //  SIMPAN MESIN KE SESSION
                //  Simpan RAW + komponen terpisah, supaya:
                //    - Dashboard bisa tampil nama/area/no tanpa parse ulang
                //    - Insert DB bisa langsung pakai kolom terpisah
                // ============================================================
                Session.mesin = _lastMesinRaw;   // "CRP15-R2" (raw)
                Session.MesinName = _lastMchName;    // "Crimping"
                Session.MesinArea = _lastMchArea;    // "R2"
                Session.MesinNo = _lastMchNo;      // "15"

                // Log debug → muncul di Output window Visual Studio
                System.Diagnostics.Debug.WriteLine(
                    $"LOGIN => Operator={noRegOperator} ({Session.Nama}), " +
                    $"Shift={Session.Shift}, " +
                    $"MesinRaw={_lastMesinRaw}, " +
                    $"MesinName={_lastMchName}, " +
                    $"Area={_lastMchArea}, No={_lastMchNo}");

                // Delay kecil biar user sempat lihat status "Memproses..."
                await Task.Delay(300);

                // ============================================================
                //  BUKA DASHBOARD (uncomment kalau sudah siap)
                // ============================================================
                var dash = new Dashboard(this);
                dash.Show();
                Hide();
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error", ex.Message);
            }
            finally
            {
                // Selalu reset state tombol, apapun hasilnya
                _busy = false;
                btnLogin.Enabled = true;
                btnLogin.Text = "LOGIN";
                lblFooterStatus.Text = StatusNormal;
                lblFooterStatus.ForeColor = AppThemeLogin.ColorTextSub;
            }
        }
    }

    // ================================================================
    //  Helper: item dropdown shift
    //  Kenapa pakai class, bukan string biasa?
    //  → Supaya bisa simpan Text ("Shift 1") dan Value ("1") terpisah.
    //    Text untuk display, Value untuk dikirim ke DB/Session.
    // ================================================================
    internal sealed class ShiftItem
    {
        public string Text { get; }
        public string Value { get; }

        public ShiftItem(string text, string value)
        {
            Text = text;
            Value = value;
        }

        // ToString dipakai ComboBox untuk render item di dropdown
        public override string ToString() => Text;
    }
}