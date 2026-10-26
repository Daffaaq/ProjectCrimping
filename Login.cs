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
    ///   4. Klik LOGIN → data disimpan ke Session → buka Dashboard
    ///
    /// Validasi dilakukan dua lapis:
    ///   - Validasi FORMAT di sisi client (cepat, tanpa hit DB)
    ///   - Validasi DATA di sisi server (query DB untuk cek exist/valid)
    ///
    /// Validasi KOSONG:
    ///   - Tekan Enter di field kosong (Operator / Mesin) → pesan "wajib diisi" muncul.
    ///   - Klik LOGIN dengan field kosong → pesan "wajib diisi" muncul (BtnLogin_Click).
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

        private readonly DatabaseHelper _db = new DatabaseHelper();

        // ================================================================
        //  SINKRONISASI JAM DENGAN SQL SERVER
        // ================================================================
        private DateTime _dbBaseTime;
        private readonly System.Diagnostics.Stopwatch _dbClockWatch = new System.Diagnostics.Stopwatch();
        private bool _dbClockReady = false;

        private const int ResyncIntervalMs = 5 * 60 * 1000;
        private int _msSinceLastSync = 0;

        private readonly DatabaseHelper _dbSync = new DatabaseHelper(DbProvider.SqlServer);

        // ================================================================
        //  CACHE HASIL VALIDASI MESIN
        // ================================================================
        private string _lastMesinRaw = null;
        private string _lastMchName = null;
        private string _lastMchArea = null;
        private string _lastMchNo = null;

        // ================================================================
        //  CACHE HASIL VALIDASI OPERATOR
        //  Dipakai saat login supaya tidak query ulang, dan untuk
        //  mengisi Session.ext (extension foto profil) di Dashboard.
        // ================================================================
        private string _lastOperatorNoreg = null;
        private string _lastOperatorNama = null;
        private string _lastOperatorExt = null;

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

                // Reset cache operator — kalau text berubah, data lama invalid
                _lastOperatorNoreg = null;
                _lastOperatorNama = null;
                _lastOperatorExt = null;
            };

            txtNoRegOperator.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LookupOperatorAsync();

                    if (lblHintOperator.Visible)
                        cmbShift.Focus();
                }
            };

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

                _lastMesinRaw = null;
                _lastMchName = null;
                _lastMchArea = null;
                _lastMchNo = null;
            };

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
        //  PERBAIKAN BUG: Inputan di-capital-kan semua terlebih dahulu
        //  agar format yang di-return selalu konsisten (Contoh: CRP15-R2).
        // ================================================================
        private static (string Area, string No)? ExtractMesin(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return null;

            // Ubah ke kapital untuk mempermudah pengecekan
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

            // KEMBALIKAN DALAM FORMAT KAPITAL (SERAGAM)
            return (area, no);
        }

        // ================================================================
        //  LOOKUP OPERATOR
        //
        //  Ambil `nama` DAN `ext` dari DB_PEGAWAI.
        //  - `nama` dipakai untuk hint chip
        //  - `ext` dipakai untuk membentuk URL foto profil di Dashboard:
        //      http://122.144.4.194:31985/indoprima_gemilang/public/img/{noreg}.{ext}
        //
        //  Hasil disimpan di field _lastOperator* supaya tidak query ulang
        //  saat BtnLogin_Click().
        //
        //  Field kosong → tampilkan error "wajib diisi" (bukan diam saja).
        // ================================================================
        private async Task LookupOperatorAsync()
        {
            string raw = txtNoRegOperator.Text.Trim();

            // ===== VALIDASI KOSONG =====
            if (string.IsNullOrEmpty(raw))
            {
                AppThemeLogin.ClearHintChip(lblHintOperator);
                AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                    "No Reg Operator wajib diisi");

                _lastOperatorNoreg = null;
                _lastOperatorNama = null;
                _lastOperatorExt = null;
                return;
            }

            string noreg = ExtractNoreg(raw);

            if (noreg == null)
            {
                AppThemeLogin.ClearHintChip(lblHintOperator);
                AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                    "Noreg Tidak Sesuai Format");

                _lastOperatorNoreg = null;
                _lastOperatorNama = null;
                _lastOperatorExt = null;
                return;
            }

            try
            {
                string nama = null;
                string ext = null;
                bool found = false;        // ada row di DB_PEGAWAI (terlepas dari resign)
                bool sudahResign = false;  // tgl_resign terisi

                await Task.Run(() =>
                {
                    using (var conn = _db.Open())
                    using (var cmd = new SqlCommand(
                        @"SELECT TOP 1 nama, ext, tgl_resign 
                  FROM DB_PEGAWAI 
                  WHERE noreg = @noreg",
                        (SqlConnection)conn))
                    {
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.AddWithValue("@noreg", noreg);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                found = true;

                                object tglResign = reader["tgl_resign"];
                                sudahResign = tglResign != null && tglResign != DBNull.Value;

                                if (!sudahResign)
                                {
                                    nama = reader["nama"] as string;
                                    ext = reader["ext"] as string;
                                }
                            }
                        }
                    }
                });

                // ===== TIDAK ADA DI DB_PEGAWAI =====
                if (!found || (string.IsNullOrEmpty(nama) && !sudahResign))
                {
                    AppThemeLogin.ClearHintChip(lblHintOperator);
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "No Reg Operator tidak ditemukan");

                    _lastOperatorNoreg = null;
                    _lastOperatorNama = null;
                    _lastOperatorExt = null;
                    return;
                }

                // ===== SUDAH RESIGN =====
                if (sudahResign)
                {
                    AppThemeLogin.ClearHintChip(lblHintOperator);
                    AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                        "Operator sudah resign / tidak aktif");

                    _lastOperatorNoreg = null;
                    _lastOperatorNama = null;
                    _lastOperatorExt = null;
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
                AppThemeLogin.ClearHintChip(lblHintOperator);
                AppThemeLogin.SetFieldError(pnlFieldOperator, lblErrOperator,
                    "Gagal akses DB: " + ex.Message);

                _lastOperatorNoreg = null;
                _lastOperatorNama = null;
                _lastOperatorExt = null;
            }
        }
        // ================================================================
        //  LOOKUP MESIN
        //
        //  PERBAIKAN BUG: Menyimpan `_lastMesinRaw` dalam format standar
        //  (CRP<No>-<Area>) supaya konsisten meskipun operator mengetik
        //  dengan huruf kecil.
        //
        //  Field kosong → tampilkan error "wajib diisi" (bukan diam saja).
        // ================================================================
        private async Task LookupMesinAsync()
        {
            string raw = txtMesin.Text.Trim();

            // ===== VALIDASI KOSONG =====
            if (string.IsNullOrEmpty(raw))
            {
                AppThemeLogin.ClearHintChip(lblHintMesin);
                AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                    "Mesin wajib diisi");
                return;
            }

            var parsed = ExtractMesin(raw);

            if (parsed == null)
            {
                AppThemeLogin.ClearHintChip(lblHintMesin);
                AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                    "Format barcode mesin tidak valid (contoh: CRP15-R2)");
                return;
            }

            string mchArea = parsed.Value.Area;
            string mchNo = parsed.Value.No;

            try
            {
                string mchName = await Task.Run(() =>
                {
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
                    AppThemeLogin.ClearHintChip(lblHintMesin);
                    AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                        "Mesin tidak ditemukan");
                }
                else
                {
                    // ========================================================
                    // PERBAIKAN BUG: Simpan format standar (Kapital), 
                    // bukan inputan asli operator.
                    // ========================================================
                    _lastMesinRaw = $"CRP{mchNo}-{mchArea}";
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
        // ================================================================
        private void LoadBrandLogo(PictureBox picLogo, Panel brandBadge)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.StartupPath, "images", "logoipg.png");
                Image logo = System.IO.File.Exists(path) ? Image.FromFile(path) : null;
                if (logo == null) throw new Exception("Logo tidak ditemukan");

                picLogo.Image = logo;
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
        // ================================================================
        private async Task BtnLogin_Click()
        {
            if (_busy) return;

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
                else if (!lblHintOperator.Visible)
                {
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
                    AppThemeLogin.SetFieldError(pnlFieldMesin, lblErrMesin,
                        "Format barcode mesin tidak valid");
                    firstInvalid = firstInvalid ?? txtMesin;
                }
                else if (!lblHintMesin.Visible)
                {
                    await LookupMesinAsync();
                    if (!lblHintMesin.Visible)
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
                _busy = true;
                btnLogin.Enabled = false;
                btnLogin.Text = "Memproses...";
                lblFooterStatus.Text = "Memproses login...";
                lblFooterStatus.ForeColor = AppThemeLogin.ColorAccent;

                // ---- Isi Session ----
                Session.Noreg = noRegOperator;
                Session.Nama = _lastOperatorNama ?? lblHintOperator.Text;
                Session.ext = _lastOperatorExt;   // <- extension foto profil
                Session.Shift = int.TryParse(shiftValue, out int sh) ? sh : 0;
                Session.IsLoggedIn = true;

                Session.mesin = _lastMesinRaw;    // ← selalu format kapital: CRP15-R2
                Session.MesinName = _lastMchName;
                Session.MesinArea = _lastMchArea;
                Session.MesinNo = _lastMchNo;

                System.Diagnostics.Debug.WriteLine(
                    $"LOGIN => Operator={noRegOperator} ({Session.Nama}), " +
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

            _lastMesinRaw = null;
            _lastMchName = null;
            _lastMchArea = null;
            _lastMchNo = null;

            _lastOperatorNoreg = null;
            _lastOperatorNama = null;
            _lastOperatorExt = null;

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