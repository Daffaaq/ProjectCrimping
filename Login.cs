using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
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
    ///   4. Klik LOGIN → data disimpan ke Session → buka Dashboard
    ///
    /// Validasi dilakukan dua lapis:
    ///   - Validasi FORMAT di sisi client (cepat, tanpa hit DB)
    ///   - Validasi DATA di sisi server (query DB untuk cek exist/valid)
    ///
    /// Status "sudah tervalidasi" ditentukan dari CACHE (_lastOperatorNoreg /
    /// _lastMesinRaw), bukan dari visibilitas label di UI.
    /// </summary>
    public partial class Login : Form
    {
        private readonly AppThemeLogin.BackgroundCache _bgCache = new AppThemeLogin.BackgroundCache();

        private readonly System.Windows.Forms.Timer _clockTimer = new System.Windows.Forms.Timer();

        private static readonly CultureInfo IdCulture = new CultureInfo("id-ID");

        private const string StatusNormal = "Online   ·   Ver 2.0   ·   Protected";

        // ================================================================
        //  KONSTANTA PREFIX BARCODE
        // ================================================================
        private const string OperatorPrefix = "ExcitedCRP";
        private const string MesinPrefix = "CRP";

        private bool _busy;

        // Koneksi: dibuat sekali, dipakai ulang oleh semua lookup.
        private readonly DatabaseHelper _db = new DatabaseHelper();
        private readonly DatabaseHelper _dbMy = new DatabaseHelper(DbProvider.MySql);
        private readonly DatabaseHelper _dbSync = new DatabaseHelper(DbProvider.SqlServer);

        // ================================================================
        //  SINKRONISASI JAM DENGAN SQL SERVER
        // ================================================================
        private DateTime _dbBaseTime;
        private readonly System.Diagnostics.Stopwatch _dbClockWatch = new System.Diagnostics.Stopwatch();
        private bool _dbClockReady = false;

        private const int ResyncIntervalMs = 5 * 60 * 1000;
        private int _msSinceLastSync = 0;

        // ================================================================
        //  CACHE HASIL VALIDASI MESIN
        // ================================================================
        private string _lastMesinRaw = null;
        private string _lastMchName = null;
        private string _lastMchArea = null;
        private string _lastMchNo = null;

        // ================================================================
        //  CACHE HASIL VALIDASI OPERATOR
        // ================================================================
        private string _lastOperatorNoreg = null;
        private string _lastOperatorNama = null;
        private string _lastOperatorExt = null;

        // ================================================================
        //  PENANDA LOOKUP YANG SEDANG BERJALAN (anti query dobel)
        // ================================================================
        private Task _operatorLookupTask;
        private string _operatorLookupRaw;
        private Task _mesinLookupTask;
        private string _mesinLookupRaw;

        // Logo di-load ke memori (tidak mengunci file), di-dispose saat form ditutup.
        private Image _logoImage;

        public Login()
        {
            AppThemeLogin.LoadLocalFonts();
            InitializeComponent();

            // ---- Dropdown shift ----
            cmbShift.Items.Clear();
            cmbShift.Items.Add(new ShiftItem("Shift 1", "1"));
            cmbShift.Items.Add(new ShiftItem("Shift 2", "2"));
            cmbShift.SelectedIndex = 0;

            // ---- Theme ----
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

            AppThemeLogin.StyleHintChip(lblHintOperator);
            AppThemeLogin.StyleHintChip(lblHintMesin);

            AppThemeLogin.StyleFieldContainer(pnlFieldOperator, lblIconOperator, txtNoRegOperator,
                AppThemeLogin.IconPerson, AppThemeLogin.FallbackPerson);
            AppThemeLogin.StyleFieldContainer(pnlFieldShift, lblIconShift, cmbShift,
                AppThemeLogin.IconShift, AppThemeLogin.FallbackShift);
            AppThemeLogin.StyleFieldContainer(pnlFieldMesin, lblIconMesin, txtMesin,
                AppThemeLogin.IconGear, AppThemeLogin.FallbackGear);

            AppThemeLogin.StyleTextBoxFlat(txtNoRegOperator, "Scan barcode operator");
            AppThemeLogin.StyleComboBoxFlat(cmbShift);
            AppThemeLogin.StyleTextBoxFlat(txtMesin, "Scan barcode mesin");

            AppThemeLogin.StylePrimaryButton(btnLogin, "LOGIN");
            AppThemeLogin.StyleFooter(pnlStatusDot, lblFooterStatus, lblFooterCompany);
            lblFooterStatus.Text = StatusNormal;

            lblHintOperator.BringToFront();
            lblHintMesin.BringToFront();
            lblErrOperator.BringToFront();
            lblErrMesin.BringToFront();

            // ================================================================
            // EVENT HANDLERS
            // ================================================================

            // ---- Operator ----
            txtNoRegOperator.TextChanged += (s, e) =>
            {
                AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
                AppThemeLogin.ClearHintChip(lblHintOperator);
                ClearOperatorCache();
            };

            txtNoRegOperator.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LookupOperatorAsync();

                    if (_lastOperatorNoreg != null)
                        cmbShift.Focus();
                }
            };

            txtNoRegOperator.Leave += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtNoRegOperator.Text) &&
                    _lastOperatorNoreg == null)
                {
                    await LookupOperatorAsync();
                }
            };

            // ---- Shift ----
            cmbShift.SelectedIndexChanged += (s, e) =>
                AppThemeLogin.ClearFieldError(pnlFieldShift, lblErrShift);

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
                ClearMesinCache();
            };

            txtMesin.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LookupMesinAsync();

                    if (_lastMesinRaw != null)
                        await BtnLogin_Click();
                }
            };

            txtMesin.Leave += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtMesin.Text) &&
                    _lastMesinRaw == null)
                {
                    await LookupMesinAsync();
                }
            };

            btnLogin.Click += async (s, e) => await BtnLogin_Click();

            // ---- Jam realtime ----
            _dbBaseTime = DateTime.Now;
            _dbClockWatch.Start();

            UpdateClock();

            _clockTimer.Interval = 1000;
            _clockTimer.Tick += (s, e) =>
            {
                UpdateClock();

                _msSinceLastSync += _clockTimer.Interval;
                if (_msSinceLastSync >= ResyncIntervalMs)
                {
                    _msSinceLastSync = 0;
                    _ = SyncClockFromServerAsync();
                }
            };
            _clockTimer.Start();

            Disposed += (s, e) =>
            {
                _clockTimer.Dispose();
                _dbClockWatch.Stop();

                if (_logoImage != null)
                {
                    picLogo.Image = null;
                    _logoImage.Dispose();
                    _logoImage = null;
                }
            };

            Shown += async (s, e) =>
            {
                txtNoRegOperator.Focus();
                await SyncClockFromServerAsync();
            };

            // ---- Resize ----
            PositionContent();
            bgPanel.Resize += (s, e) =>
            {
                PositionContent();
                bgPanel.Invalidate();
            };

            LoadBrandLogo(picLogo, brandBadge);

            formPanel.BringToFront();
        }

        // ================================================================
        //  TAMPILKAN LOGIN LAGI SETELAH LOGOUT
        //
        //  Dipanggil dari Dashboard SETELAH Dashboard di-Close().
        //  Urutan penting:
        //    1. Reset isi form
        //    2. Normal dulu (saat masih hidden) → Show → Maximized,
        //       supaya bounds & title bar dihitung ulang dengan benar
        //       (bug WinForms: Hide/Show pada form Maximized)
        //    3. Activate supaya form menerima keyboard
        //    4. Fokus diset lewat BeginInvoke (setelah form benar-benar aktif)
        // ================================================================
        public void ShowAgain()
        {
            ResetLoginForm();

            // Pastikan tombol minimize/close selalu ada
            FormBorderStyle = FormBorderStyle.Sizable;
            ControlBox = true;
            MinimizeBox = true;
            MaximizeBox = true;

            WindowState = FormWindowState.Normal;
            Show();

            // Hapus baris di bawah kalau ingin Login tampil ukuran normal
            // (tidak maximize) setelah logout.
            WindowState = FormWindowState.Maximized;

            Activate();
            BringToFront();

            BeginInvoke(new Action(() =>
            {
                PositionContent();
                ActiveControl = txtNoRegOperator;
                txtNoRegOperator.Focus();
            }));
        }

        // ================================================================
        //  HELPER: reset cache
        // ================================================================
        private void ClearOperatorCache()
        {
            _lastOperatorNoreg = null;
            _lastOperatorNama = null;
            _lastOperatorExt = null;
        }

        private void ClearMesinCache()
        {
            _lastMesinRaw = null;
            _lastMchName = null;
            _lastMchArea = null;
            _lastMchNo = null;
        }

        // ================================================================
        //  HELPER: strip prefix "ExcitedCRP" dari barcode operator
        // ================================================================
        private static string ExtractNoreg(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return null;

            string trimmed = rawInput.Trim();

            if (!trimmed.StartsWith(OperatorPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string noreg = trimmed.Substring(OperatorPrefix.Length).Trim();

            return string.IsNullOrEmpty(noreg) ? null : noreg;
        }

        // ================================================================
        //  HELPER: parse barcode mesin. Format: CRP<no>-<area>
        //
        //  Input di-kapital-kan dulu supaya hasil selalu konsisten
        //  (contoh: CRP15-R2).
        //
        //  CATATAN: format saat ini kaku (tepat 2 digit no, "R" + 1 digit
        //  area). Kalau nanti ada mesin seperti CRP105-R10, longgarkan
        //  aturan panjang di bawah.
        // ================================================================
        private static (string Area, string No)? ExtractMesin(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return null;

            string trimmed = rawInput.Trim().ToUpperInvariant();

            if (!trimmed.StartsWith(MesinPrefix, StringComparison.Ordinal))
                return null;

            int dash = trimmed.IndexOf('-', MesinPrefix.Length);
            if (dash == -1)
                return null;

            string no = trimmed.Substring(MesinPrefix.Length, dash - MesinPrefix.Length);
            string area = trimmed.Substring(dash + 1);

            if (no.Length != 2 || !no.All(char.IsDigit))
                return null;

            if (area.Length != 2 || area[0] != 'R' || !char.IsDigit(area[1]))
                return null;

            return (area, no);
        }

        // ================================================================
        //  LOOKUP OPERATOR
        //
        //  - Kalau lookup untuk input yang SAMA sedang berjalan, task yang
        //    sama dipakai ulang (tidak query dobel).
        //  - Kalau input berubah selagi query berjalan, hasil lama dibuang.
        //
        //  Ambil `nama` DAN `ext` dari DB_PEGAWAI:
        //  - `nama` dipakai untuk hint chip
        //  - `ext` dipakai untuk URL foto profil di Dashboard
        // ================================================================
        private Task LookupOperatorAsync()
        {
            string raw = txtNoRegOperator.Text.Trim();

            if (_operatorLookupTask != null &&
                !_operatorLookupTask.IsCompleted &&
                _operatorLookupRaw == raw)
            {
                return _operatorLookupTask;
            }

            _operatorLookupRaw = raw;
            _operatorLookupTask = LookupOperatorCoreAsync(raw);
            return _operatorLookupTask;
        }

        private bool IsOperatorInputStale(string raw)
        {
            return !string.Equals(txtNoRegOperator.Text.Trim(), raw, StringComparison.Ordinal);
        }

        private void SetOperatorError(string message)
        {
            AppThemeLogin.ClearHintChip(lblHintOperator);
            AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator, message);
            ClearOperatorCache();
        }

        private async Task LookupOperatorCoreAsync(string raw)
        {
            // ===== VALIDASI KOSONG =====
            if (string.IsNullOrEmpty(raw))
            {
                SetOperatorError("No Reg Operator wajib diisi");
                return;
            }

            string noreg = ExtractNoreg(raw);

            if (noreg == null)
            {
                SetOperatorError("Noreg Tidak Sesuai Format");
                return;
            }

            try
            {
                string nama = null;
                string ext = null;
                bool found = false;        // ada row di DB_PEGAWAI
                bool sudahResign = false;  // sudah lewat tanggal resign

                await Task.Run(() =>
                {
                    using (var conn = _db.Open())
                    using (var cmd = new SqlCommand(
                        @"SELECT TOP 1
                                 nama,
                                 ext,
                                 CASE WHEN tgl_resign IS NOT NULL
                                      THEN 1 ELSE 0 END AS sudah_resign
                          FROM   DB_PEGAWAI
                          WHERE  noreg = @noreg",
                        (SqlConnection)conn))
                    {
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add("@noreg", SqlDbType.VarChar, 20).Value = noreg;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                found = true;
                                sudahResign = Convert.ToInt32(reader["sudah_resign"]) == 1;

                                if (!sudahResign)
                                {
                                    nama = reader["nama"] as string;
                                    ext = reader["ext"] as string;
                                }
                            }
                        }
                    }
                });

                // Input sudah berubah selama query → buang hasil lama
                if (IsOperatorInputStale(raw))
                    return;

                // ===== TIDAK ADA DI DB_PEGAWAI =====
                if (!found || (string.IsNullOrEmpty(nama) && !sudahResign))
                {
                    SetOperatorError("No Reg Operator tidak ditemukan");
                    return;
                }

                // ===== SUDAH RESIGN =====
                if (sudahResign)
                {
                    SetOperatorError("Operator sudah resign / tidak aktif");
                    return;
                }

                // ===== VALID =====
                AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
                AppThemeLogin.SetHintChip(lblHintOperator, nama);

                _lastOperatorNoreg = noreg;
                _lastOperatorNama = nama;
                _lastOperatorExt = ext;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LOOKUP OPERATOR] " + ex);

                if (IsOperatorInputStale(raw))
                    return;

                SetOperatorError("Gagal terhubung ke database, coba lagi");
            }
        }

        // ================================================================
        //  LOOKUP MESIN
        //
        //  Sama seperti operator: anti query dobel + buang hasil basi.
        //  Cache disimpan dalam format standar (CRP<No>-<Area>, kapital).
        // ================================================================
        private Task LookupMesinAsync()
        {
            string raw = txtMesin.Text.Trim();

            if (_mesinLookupTask != null &&
                !_mesinLookupTask.IsCompleted &&
                _mesinLookupRaw == raw)
            {
                return _mesinLookupTask;
            }

            _mesinLookupRaw = raw;
            _mesinLookupTask = LookupMesinCoreAsync(raw);
            return _mesinLookupTask;
        }

        private bool IsMesinInputStale(string raw)
        {
            return !string.Equals(txtMesin.Text.Trim(), raw, StringComparison.Ordinal);
        }

        private void SetMesinError(string message)
        {
            AppThemeLogin.ClearHintChip(lblHintMesin);
            AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin, message);
            ClearMesinCache();
        }

        private async Task LookupMesinCoreAsync(string raw)
        {
            // ===== VALIDASI KOSONG =====
            if (string.IsNullOrEmpty(raw))
            {
                SetMesinError("Mesin wajib diisi");
                return;
            }

            var parsed = ExtractMesin(raw);

            if (parsed == null)
            {
                SetMesinError("Format barcode mesin tidak valid (contoh: CRP15-R2)");
                return;
            }

            string mchArea = parsed.Value.Area;
            string mchNo = parsed.Value.No;

            try
            {
                string mchName = await Task.Run(() =>
                {
                    using (var conn = _dbMy.Open())
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

                // Input sudah berubah selama query → buang hasil lama
                if (IsMesinInputStale(raw))
                    return;

                if (string.IsNullOrEmpty(mchName))
                {
                    SetMesinError("Mesin tidak ditemukan");
                    return;
                }

                _lastMesinRaw = $"CRP{mchNo}-{mchArea}";
                _lastMchName = mchName;
                _lastMchArea = mchArea;
                _lastMchNo = mchNo;

                AppThemeLogin.ClearFieldError(pnlFieldMesin, lblErrMesin);
                AppThemeLogin.SetHintChip(lblHintMesin, $"{mchName} · {mchArea} No {mchNo}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LOOKUP MESIN] " + ex);

                if (IsMesinInputStale(raw))
                    return;

                SetMesinError("Gagal terhubung ke database, coba lagi");
            }
        }

        // ================================================================
        //  SINKRONISASI JAM DENGAN SQL SERVER
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
                _dbClockWatch.Restart();
                _dbClockReady = true;
                _msSinceLastSync = 0;

                System.Diagnostics.Debug.WriteLine(
                    $"[CLOCK] Synced with SQL Server: {_dbBaseTime:yyyy-MM-dd HH:mm:ss}");

                UpdateClock();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[CLOCK] Sync gagal: " + ex.Message);
            }
        }

        private void UpdateClock()
        {
            DateTime now;

            if (_dbClockReady)
                now = _dbBaseTime.Add(_dbClockWatch.Elapsed);
            else
                now = DateTime.Now;

            lblClock.Text = now.ToString("HH:mm:ss", IdCulture);
            lblDate.Text = now.ToString("dddd, dd MMMM yyyy", IdCulture);
        }

        // ================================================================
        //  Hitung posisi konten saat resize.
        // ================================================================
        private void PositionContent()
        {
            int w = bgPanel.ClientSize.Width;
            int h = bgPanel.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int leftW = (int)(w * AppThemeLogin.SplitRatio);

            const int leftMargin = 56;
            leftContent.Width = Math.Max(300, Math.Min(480, leftW - leftMargin * 2));
            leftContent.Left = leftMargin;
            leftContent.Top = Math.Max(40, (h - leftContent.Height) / 2);

            int rightW = w - leftW;
            formPanel.Left = leftW + (rightW - formPanel.Width) / 2;
            formPanel.Top = Math.Max(24, (h - formPanel.Height) / 2);
        }

        // ================================================================
        //  Load logo brand.
        //  Pakai Bitmap(path) → dicopy ke memori, file tidak terkunci.
        // ================================================================
        private void LoadBrandLogo(PictureBox picLogo, Panel brandBadge)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.StartupPath, "images", "logoipg.png");
                if (!System.IO.File.Exists(path))
                    throw new Exception("Logo tidak ditemukan");

                _logoImage = new Bitmap(path);

                picLogo.Image = _logoImage;
                picLogo.SizeMode = PictureBoxSizeMode.Zoom;
                picLogo.BackColor = Color.Transparent;

                picLogo.Bounds = brandBadge.Bounds;

                picLogo.Visible = true;
                picLogo.BringToFront();
                brandBadge.Visible = false;
            }
            catch
            {
                picLogo.Visible = false;
                brandBadge.Visible = true;
            }
        }

        // ================================================================
        //  PROSES LOGIN
        //
        //  _busy diset PALING AWAL supaya Enter dobel / Enter + klik
        //  tidak bisa lolos dua kali (validasi punya await di dalamnya).
        //  finally selalu mereset _busy, termasuk saat validasi gagal.
        // ================================================================
        private async Task BtnLogin_Click()
        {
            if (_busy) return;
            _busy = true;

            try
            {
                string rawOperator = txtNoRegOperator.Text.Trim();
                string noRegOperator = ExtractNoreg(rawOperator);

                string shiftValue = (cmbShift.SelectedItem as ShiftItem)?.Value ?? "";
                string mesin = txtMesin.Text.Trim();

                AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
                AppThemeLogin.ClearFieldError(pnlFieldShift, lblErrShift);
                AppThemeLogin.ClearFieldError(pnlFieldMesin, lblErrMesin);

                Control firstInvalid = null;

                // ---- Validasi Operator ----
                if (string.IsNullOrEmpty(rawOperator))
                {
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "No Reg Operator wajib diisi");
                    firstInvalid = firstInvalid ?? txtNoRegOperator;
                }
                else if (noRegOperator == null)
                {
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "Format barcode tidak valid");
                    firstInvalid = firstInvalid ?? txtNoRegOperator;
                }
                else if (_lastOperatorNoreg == null)
                {
                    await LookupOperatorAsync();
                    if (_lastOperatorNoreg == null)
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
                    AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                        "Format barcode mesin tidak valid");
                    firstInvalid = firstInvalid ?? txtMesin;
                }
                else if (_lastMesinRaw == null)
                {
                    await LookupMesinAsync();
                    if (_lastMesinRaw == null)
                        firstInvalid = firstInvalid ?? txtMesin;
                }

                if (firstInvalid != null)
                {
                    firstInvalid.Focus();
                    return;
                }

                // ============================================================
                //  MULAI PROSES LOGIN
                // ============================================================
                btnLogin.Enabled = false;
                btnLogin.Text = "Memproses...";
                lblFooterStatus.Text = "Memproses login...";
                lblFooterStatus.ForeColor = AppThemeLogin.ColorAccent;

                // ---- Isi Session (semua dari cache hasil validasi) ----
                Session.Noreg = _lastOperatorNoreg;
                Session.Nama = _lastOperatorNama;
                Session.ext = _lastOperatorExt;   // extension foto profil
                Session.Shift = int.TryParse(shiftValue, out int sh) ? sh : 0;

                Session.mesin = _lastMesinRaw;    // selalu format kapital: CRP15-R2
                Session.MesinName = _lastMchName;
                Session.MesinArea = _lastMchArea;
                Session.MesinNo = _lastMchNo;

                // Dashboard kemungkinan membaca Session di constructor-nya,
                // jadi IsLoggedIn diset sebelum dibuat dan di-rollback di catch.
                Session.IsLoggedIn = true;

                System.Diagnostics.Debug.WriteLine(
                    $"LOGIN => Operator={Session.Noreg} ({Session.Nama}), " +
                    $"Ext={Session.ext ?? "(null)"}, " +
                    $"Shift={Session.Shift}, " +
                    $"MesinRaw={_lastMesinRaw}, " +
                    $"MesinName={_lastMchName}, " +
                    $"Area={_lastMchArea}, No={_lastMchNo}");

                await Task.Delay(300);

                var dash = new Dashboard(this);
                dash.Show();
                Hide();
            }
            catch (Exception ex)
            {
                // Dashboard gagal dibuka → batalkan status login
                Session.IsLoggedIn = false;
                NotifPopup.Error(this, "Error", ex.Message);
            }
            finally
            {
                _busy = false;
                btnLogin.Enabled = true;
                btnLogin.Text = "LOGIN";
                lblFooterStatus.Text = StatusNormal;
                lblFooterStatus.ForeColor = AppThemeLogin.ColorTextSub;
            }
        }

        // ================================================================
        //  RESET FORM LOGIN ke kondisi awal
        //  Dipanggil dari ShowAgain() (setelah logout).
        //  Fokus TIDAK diset di sini; diset di ShowAgain() setelah form aktif.
        // ================================================================
        public void ResetLoginForm()
        {
            txtNoRegOperator.Clear();
            txtMesin.Clear();
            if (cmbShift.Items.Count > 0)
                cmbShift.SelectedIndex = 0;

            AppThemeLogin.ClearFieldError(pnlFieldOperator, lblErrOperator);
            AppThemeLogin.ClearFieldError(pnlFieldShift, lblErrShift);
            AppThemeLogin.ClearFieldError(pnlFieldMesin, lblErrMesin);

            AppThemeLogin.ClearHintChip(lblHintOperator);
            AppThemeLogin.ClearHintChip(lblHintMesin);

            ClearMesinCache();
            ClearOperatorCache();

            _busy = false;
            btnLogin.Enabled = true;
            btnLogin.Text = "LOGIN";

            lblFooterStatus.Text = StatusNormal;
            lblFooterStatus.ForeColor = AppThemeLogin.ColorTextSub;
        }
    }

    // ================================================================
    //  Helper: item dropdown shift
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

        public override string ToString() => Text;
    }
}