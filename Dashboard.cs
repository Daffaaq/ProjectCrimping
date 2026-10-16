using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectCrimping
{
    /// <summary>
    /// Dashboard utama operator crimping ("Shopfloor Crimping").
    ///
    /// ================================================================
    ///  RINGKASAN ALUR KERJA (urutan operator)
    /// ================================================================
    ///  1. Scan LOTNO (bar di atas)        → cari tiproductname di inventory.
    ///                                       Simpan tiproductname ke _state.
    ///                                       (Patokan BELUM diambil di sini.)
    ///  2. Scan APLIKATOR                  → divalidasi ke tabel MSAplikator,
    ///                                       lalu tabel riwayat hari ini dimuat.
    ///  3. Scan WOS (format XXXX.001)      → validasi format; lalu cari patokan
    ///                                       di TrRPHMesin dengan DUA syarat:
    ///                                         - TERMINALA/TERMINALB = tiproductname
    ///                                         - barcode = WOS
    ///                                       Kalau ketemu di sisi A dan B,
    ///                                       operator memilih lewat FormPilihSisi.
    ///                                       Tombol N1 aktif setelah WOS valid.
    ///  4. Tekan N1 → N2 → N3 (1 tombol)   → tiap tombol membuka form input ukuran.
    ///                                       Setelah N3 + konfirmasi NG, data di-INSERT
    ///                                       ke TrCrimpingResult dan tombol kembali ke N1.
    ///  5. Fokus kembali ke WOS            → operator bisa scan WOS berikutnya TANPA
    ///                                       scan ulang Lotno/Aplikator, karena
    ///                                       ResetAfterSubmit() mempertahankan
    ///                                       tiproductname dan hanya menghapus
    ///                                       patokan + hasil N1/N2/N3.
    ///
    /// ================================================================
    ///  STRUKTUR LAYOUT (BuildContent)
    /// ================================================================
    ///  Form
    ///   ├─ Sidebar (kiri, 270 px, Dock.Left)  : profil, menu, logout
    ///   └─ Konten (Dock.Fill, TableLayoutPanel 1 kolom x 5 baris)
    ///        ├─ Baris 0 (AutoSize)   : Header judul
    ///        ├─ Baris 1 (60 px)      : Bar LOTNO + tombol X (reset semua)
    ///        ├─ Baris 2 (180 px)     : Kotak input TERMINAL / APLIKATOR / WOS
    ///        ├─ Baris 3 (64 px)      : Tombol dinamis N1/N2/N3
    ///        └─ Baris 4 (sisa ruang) : Tabel riwayat (DataGridView)
    ///
    ///  Seluruh UI dibangun lewat kode (BuildUI), BUKAN lewat Designer,
    ///  jadi mengubah tampilan = ubah method Build* di file ini.
    ///
    /// ================================================================
    ///  CATATAN PENTING UNTUK MAINTAINER
    /// ================================================================
    ///  - "Patokan" = nilai standar CH/IH/CW/IW/Tension dari TrRPHMesin,
    ///    diambil SETELAH WOS di-scan (karena butuh kolom barcode).
    ///  - Ada DUA jenis reset, jangan ketukar:
    ///        * ResetNState()       → hapus SEMUA, termasuk Tiproductname.
    ///                                 Dipakai saat Lotno/Terminal di-reset,
    ///                                 operator WAJIB scan Lotno ulang.
    ///        * ResetAfterSubmit()  → hapus hasil N + patokan + sisi,
    ///                                 TAPI pertahankan Tiproductname.
    ///                                 Dipakai setelah simpan N3 sukses,
    ///                                 erase Aplikator, dan erase WOS.
    ///  - "Hari produksi" berjalan jam 06:00 s/d 06:00 hari berikutnya
    ///    (lihat query di LoadDataCrimping).
    ///  - Foto profil diambil dari URL:
    ///        http://122.144.4.194:31985/indoprima_gemilang/public/img/{noreg}.{ext}
    ///    noreg & ext diambil dari Session (diisi di Login).
    ///    Kalau gagal load → fallback ke AvatarCircle inisial.
    ///  - Tabel yang dipakai masih TrCrimpingResult (tabel salinan).
    ///  - Warna & font bersumber dari AppThemeLogin supaya konsisten
    ///    dengan halaman Login.
    /// </summary>
    public partial class Dashboard : Form
    {
        // ================================================================
        //  PALET WARNA
        // ================================================================
        private static readonly Color Navy = AppThemeLogin.ColorNavy;
        private static readonly Color Accent = AppThemeLogin.ColorAccent;
        private static readonly Color AccentSoft = Color.FromArgb(253, 243, 235);
        private static readonly Color AccentLine = AppThemeLogin.ColorAccentDisabled;
        private static readonly Color TextDark = AppThemeLogin.ColorTextDark;
        private static readonly Color TextSub = AppThemeLogin.ColorTextSub;
        private static readonly Color FieldBg = AppThemeLogin.ColorFieldBg;
        private static readonly Color FieldBorder = AppThemeLogin.ColorFieldBorder;
        private static readonly Color FieldIcon = AppThemeLogin.ColorFieldIcon;
        private static readonly Color ErrorColor = AppThemeLogin.ColorError;
        private static readonly Color SuccessColor = Color.FromArgb(46, 204, 113);
        private static readonly Color PageBg = Color.White;

        private static readonly Color FieldLockedBg = Color.FromArgb(238, 240, 243);

        private static readonly Color NLockedBg = Color.FromArgb(237, 239, 242);
        private static readonly Color NLockedBorder = Color.FromArgb(221, 225, 230);
        private static readonly Color NLockedText = Color.FromArgb(168, 176, 186);

        // ================================================================
        //  KONSTANTA: URL FOTO PROFIL
        //  Format: <base>{noreg}.{ext}
        // ================================================================
        private const string AvatarBaseUrl =
            "http://122.144.4.194:31985/indoprima_gemilang/public/img/";

        // ================================================================
        //  GLYPH IKON
        // ================================================================
        private const string IcAdd = "\uE710";
        private const string IcClock = "\uE121";
        private const string IcExport = "\uE8A5";
        private const string IcPower = "\uE7E8";
        private const string IcErase = "\uE75C";
        private const string IcClose = "\uE711";
        private const string IcTerminal = "\uE7F4";
        private const string IcTag = "\uE8EC";
        private const string IcSearch = "\uE721";

        // ================================================================
        //  KONTROL UI
        // ================================================================
        private TextBox txtLotno, txtTerminal, txtAplikator, txtWos;
        private RoundedButton btnN;
        private int _nStep = 1;
        private DataGridView dgv;

        private RoundedPanel fieldLotno, fieldTerminal, fieldAplikator, fieldWos;
        private RoundedButton eraseLotno, eraseTerminal, eraseAplikator, eraseWos;

        /// <summary>Foto profil operator dari URL. Menutupi AvatarCircle kalau berhasil load.</summary>
        private AvatarPicture _avatarPic;

        // ================================================================
        //  STATE & DEPENDENCY
        // ================================================================
        private CrimpingState _state = new CrimpingState();
        private readonly DatabaseHelper _db = new DatabaseHelper(DbProvider.SqlServer);
        private readonly Login _login;
        private bool _isLoggingOut;

        public Dashboard(Login login)
        {
            _login = login;
            InitializeComponent();

            MinimumSize = new Size(1100, 700);
            DoubleBuffered = true;
            BuildUI();

            // Load foto profil secara async SETELAH form ditampilkan.
            // Kalau gagal, AvatarCircle (inisial) tetap tampil.
            //Shown += async (s, e) => await LoadAvatarAsync();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadAvatarAsync();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (!_isLoggingOut)
                Application.Exit();
        }

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Add(BuildContent());
            Controls.Add(BuildSidebar());
            ActiveControl = txtLotno;
            ResumeLayout(true);
        }

        // =====================================================
        //  INNER CLASS: CrimpingState
        // =====================================================
        private class CrimpingState
        {
            public decimal? N1_CH { get; set; }
            public decimal? N1_IH { get; set; }
            public decimal? N1_CW { get; set; }
            public decimal? N1_IW { get; set; }
            public decimal? N1_Tension { get; set; }

            public decimal? N2_CH { get; set; }
            public decimal? N2_IH { get; set; }
            public decimal? N2_CW { get; set; }
            public decimal? N2_IW { get; set; }

            public decimal? N3_CH { get; set; }
            public decimal? N3_IH { get; set; }
            public decimal? N3_CW { get; set; }
            public decimal? N3_IW { get; set; }
            public int? N3_NG { get; set; }

            public string Tiproductname { get; set; }
            public string SisiTerminal { get; set; }
            public decimal? PatokanCH { get; set; }
            public decimal? PatokanIH { get; set; }
            public decimal? PatokanCW { get; set; }
            public decimal? PatokanIW { get; set; }
            public decimal? PatokanTension { get; set; }

            public void ClearN1() { N1_CH = null; N1_IH = null; N1_CW = null; N1_IW = null; N1_Tension = null; }
            public void ClearN2() { N2_CH = null; N2_IH = null; N2_CW = null; N2_IW = null; }
            public void ClearN3() { N3_CH = null; N3_IH = null; N3_CW = null; N3_IW = null; N3_NG = null; }
            public void ClearPatokan()
            {
                Tiproductname = null;
                SisiTerminal = null;
                PatokanCH = null; PatokanIH = null; PatokanCW = null; PatokanIW = null; PatokanTension = null;
            }

            /// <summary>
            /// Hapus patokan + sisi terminal saja.
            /// Tiproductname SENGAJA tidak dihapus — dipakai ResetAfterSubmit()
            /// supaya operator bisa langsung scan WOS berikutnya.
            /// </summary>
            public void ClearPatokanOnly()
            {
                SisiTerminal = null;
                PatokanCH = null; PatokanIH = null; PatokanCW = null; PatokanIW = null; PatokanTension = null;
            }

            public void ClearAll() { ClearN1(); ClearN2(); ClearN3(); ClearPatokan(); }
        }

        // =====================================================
        //  SIDEBAR
        // =====================================================
        private Control BuildSidebar()
        {
            var side = new SidebarPanel { Dock = DockStyle.Left, Width = 270 };

            var profile = new TransparentPanel { Dock = DockStyle.Top, Height = 215 };

            // ---- AvatarCircle: fallback inisial (kalau foto gagal load) ----
            string initials = GetInitials(Session.Nama);

            var avatar = new AvatarCircle
            {
                Initials = initials,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                Size = new Size(92, 92),
                Location = new Point((270 - 92) / 2, 30),
                FillColor = Navy,
                RingColor = Accent,
                InitialsColor = Accent
            };

            // ---- PictureBox di atas AvatarCircle: foto dari URL ----
            //  Awalnya Visible=false. LoadAvatarAsync() akan menampilkan
            //  kalau download gambar berhasil.
            _avatarPic = new AvatarPicture
            {
                Size = new Size(92, 92),
                Location = new Point((270 - 92) / 2, 30),
                RingColor = Accent,
                RingThickness = 3,
                FillColor = Navy,
                BackColor = Color.Transparent,
                Visible = false
            };

            var lblName = new Label
            {
                Text = Session.Nama ?? "-",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(270, 30),
                Location = new Point(0, 134),
                BackColor = Color.Transparent
            };

            var pill = new RoundedPanel
            {
                Radius = 14,
                FillColor = Color.FromArgb(22, 255, 255, 255),
                BorderColor = Color.FromArgb(120, Accent),
                Size = new Size(130, 30),
                Location = new Point((270 - 130) / 2, 170)
            };
            pill.Controls.Add(new Label
            {
                Text = Session.Noreg ?? "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent
            });

            // Urutan Add: avatar dulu, baru _avatarPic di atasnya.
            // _avatarPic.Visible = false sampai foto berhasil didownload.
            profile.Controls.Add(avatar);
            profile.Controls.Add(_avatarPic);
            profile.Controls.Add(lblName);
            profile.Controls.Add(pill);

            var bottom = new TransparentPanel { Dock = DockStyle.Bottom, Height = 84, Padding = new Padding(16, 10, 16, 16) };
            var btnLogout = MenuButton(IcPower, "LOGOUT");
            btnLogout.FillColor = Color.FromArgb(40, ErrorColor);
            btnLogout.BorderColor = Color.FromArgb(120, ErrorColor);
            btnLogout.IconColor = Color.FromArgb(240, 120, 120);
            btnLogout.TextColor = Color.FromArgb(245, 160, 160);
            btnLogout.Dock = DockStyle.Fill;
            btnLogout.Margin = Padding.Empty;
            btnLogout.Click += (s, e) =>
            {
                using (var f = new FormConfirmLogout())
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                }

                Session.Clear();
                _isLoggingOut = true;

                var login = _login;
                Close();
                login.ShowAgain();
            };
            bottom.Controls.Add(btnLogout);

            var menu = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };

            var items = new List<RoundedButton>
            {
                MenuButton(IcAdd,    "REQUEST TERMINAL"),
                MenuButton(IcClock,  "DOWNTIME"),
                MenuButton(IcExport, "EXPORT")
            };
            items[0].Click += (s, e) => { using (var f = new FormRequestTerminal()) f.ShowDialog(this); };
            items[1].Click += (s, e) => { using (var f = new FormMaintenanceCall()) f.ShowDialog(this); };
            items[2].Click += (s, e) => ExportPdf();

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var b = items[i];
                b.Dock = DockStyle.Fill;
                b.Margin = Padding.Empty;
                var wrap = new TransparentPanel
                {
                    Dock = DockStyle.Top,
                    Height = 66,
                    Padding = new Padding(16, 0, 16, 12)
                };
                wrap.Controls.Add(b);
                menu.Controls.Add(wrap);
            }

            var acc = new InfoAccordion { Title = "SHIFT & MESIN", Dock = DockStyle.Top };
            acc.AddItem(IcClock, "SHIFT", Session.ShiftDisplay ?? "-");
            acc.AddItem(AppThemeLogin.IconGear, "MESIN", Session.MesinDisplay ?? "-");
            acc.SetExpanded(false);

            var accWrap = new TransparentPanel { Dock = DockStyle.Top, Padding = new Padding(16, 6, 16, 12) };
            accWrap.Height = acc.Height + accWrap.Padding.Vertical;
            acc.SizeChanged += (s, e) => accWrap.Height = acc.Height + accWrap.Padding.Vertical;
            accWrap.Controls.Add(acc);

            side.Controls.Add(menu);
            side.Controls.Add(bottom);
            side.Controls.Add(accWrap);
            side.Controls.Add(profile);
            return side;
        }

        private RoundedButton MenuButton(string icon, string text)
        {
            return new RoundedButton
            {
                Text = text,
                IconGlyph = icon,
                LeftAligned = true,
                Height = 54,
                Font = new Font("Segoe UI Semibold", 10f),
                Radius = 12,
                FillColor = Color.FromArgb(22, 255, 255, 255),
                BorderColor = Color.FromArgb(45, 255, 255, 255),
                TextColor = Color.White,
                IconColor = Accent
            };
        }

        // =====================================================
        //  HELPER: inisial dari nama
        // =====================================================
        /// <summary>
        /// Ambil inisial dari nama, max 2 karakter.
        /// Contoh: "Dedi Ahmad" → "DA", "Budi" → "BU", null → "?".
        /// </summary>
        private static string GetInitials(string nama)
        {
            if (string.IsNullOrWhiteSpace(nama)) return "?";

            var parts = nama.Trim()
                            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return "?";
            if (parts.Length == 1)
            {
                string p = parts[0];
                return p.Length >= 2
                    ? p.Substring(0, 2).ToUpperInvariant()
                    : p.ToUpperInvariant();
            }

            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString())
                .ToUpperInvariant();
        }

        // =====================================================
        //  LOAD FOTO PROFIL DARI URL
        //
        //  URL: http://122.144.4.194:31985/indoprima_gemilang/public/img/{noreg}.{ext}
        //  Kalau gagal (noreg/ext kosong, file tidak ada, server down),
        //  _avatarPic dibiarkan Visible=false → AvatarCircle inisial tampil.
        //
        //  Dipanggil sekali di event Shown (lihat constructor).
        // =====================================================
        private async Task LoadAvatarAsync()
        {
            // ============================================================
            //  LOGGING HELPER (lokal) — biar konsisten formatnya
            // ============================================================
            void Log(string msg)
            {
                string line = $"[AVATAR {DateTime.Now:HH:mm:ss.fff}] {msg}";
                System.Diagnostics.Debug.WriteLine(line);
                System.Diagnostics.Trace.WriteLine(line);
                Console.WriteLine(line);
            }

            Log("===== MULAI LoadAvatarAsync =====");

            if (_avatarPic == null)
            {
                Log("STOP: _avatarPic == null (BuildSidebar belum jalan?)");
                return;
            }

            string noreg = Session.Noreg;
            string ext = Session.ext;

            Log($"Session.Noreg = '{noreg ?? "(null)"}'");
            Log($"Session.ext   = '{ext ?? "(null)"}'");

            // ext di-normalisasi: buang titik di depan kalau ada.
            if (!string.IsNullOrWhiteSpace(ext))
                ext = ext.TrimStart('.').Trim();

            Log($"ext (setelah normalisasi) = '{ext ?? "(null)"}'");

            if (string.IsNullOrWhiteSpace(noreg))
            {
                Log("STOP: noreg kosong → fallback inisial");
                _avatarPic.Visible = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(ext))
            {
                Log("STOP: ext kosong → fallback inisial");
                _avatarPic.Visible = false;
                return;
            }

            string url = AvatarBaseUrl + Uri.EscapeDataString(noreg) + "." + ext;
            Log($"URL final = {url}");

            try
            {
                byte[] data;

                using (var wc = new WebClient())
                {
                    Log("Mulai download...");

                    var downloadTask = wc.DownloadDataTaskAsync(url);
                    var timeoutTask = Task.Delay(5000);

                    var completed = await Task.WhenAny(downloadTask, timeoutTask);

                    if (completed == timeoutTask)
                    {
                        Log("TIMEOUT: download > 5 detik → fallback inisial");
                        _avatarPic.Visible = false;
                        return;
                    }

                    // Kalau download selesai, tapi status-nya faulted/exception,
                    // kita tangkap di sini sebelum await.
                    if (downloadTask.IsFaulted)
                    {
                        Log($"DOWNLOAD FAULTED. Exception:");
                        if (downloadTask.Exception != null)
                        {
                            foreach (var ex in downloadTask.Exception.Flatten().InnerExceptions)
                            {
                                Log($"   → {ex.GetType().Name}: {ex.Message}");
                            }
                        }
                    }

                    data = await downloadTask;

                    Log($"Download SELESAI. Panjang byte = {(data?.Length ?? 0)}");
                }

                if (data == null || data.Length == 0)
                {
                    Log("STOP: data null atau kosong → fallback inisial");
                    _avatarPic.Visible = false;
                    return;
                }

                // Cek magic byte: apakah benar-benar JPEG/PNG?
                // JPEG: FF D8 FF ...
                // PNG : 89 50 4E 47 ...
                // HTML (kemungkinan error page): 3C 21 ... atau 3C 68 ...
                string magic = BitConverter.ToString(data, 0, Math.Min(8, data.Length));
                Log($"Magic bytes (8 pertama) = {magic}");

                bool isJpeg = data.Length >= 3 &&
                              data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
                bool isPng = data.Length >= 4 &&
                             data[0] == 0x89 && data[1] == 0x50 &&
                             data[2] == 0x4E && data[3] == 0x47;

                if (!isJpeg && !isPng)
                {
                    Log("WARNING: byte bukan JPEG/PNG. Kemungkinan server balikin HTML error.");

                    // Coba intip 200 karakter pertama (kalau HTML, bakal keliatan)
                    int previewLen = Math.Min(200, data.Length);
                    string preview = System.Text.Encoding.UTF8.GetString(data, 0, previewLen);
                    Log($"Preview 200 char pertama:\n{preview}");
                }

                Log("Mulai Image.FromStream...");

                Image img;
                using (var ms = new System.IO.MemoryStream(data))
                {
                    using (var raw = Image.FromStream(ms))
                    {
                        img = new Bitmap(raw);
                        Log($"Image.FromStream OK. Ukuran asli = {raw.Width} x {raw.Height}");
                    }
                }

                Log("Bitmap copy OK. Set ke _avatarPic...");

                if (InvokeRequired)
                {
                    Log("InvokeRequired = true → Invoke ke UI thread");
                    Invoke(new Action(() =>
                    {
                        _avatarPic.Image = img;
                        _avatarPic.Visible = true;
                        _avatarPic.BringToFront();
                        _avatarPic.Invalidate();
                        Log("Foto ter-set + Visible=true + BringToFront");
                    }));
                }
                else
                {
                    Log("InvokeRequired = false → langsung set dari thread ini");
                    _avatarPic.Image = img;
                    _avatarPic.Visible = true;
                    _avatarPic.BringToFront();
                    _avatarPic.Invalidate();
                    Log("Foto ter-set + Visible=true + BringToFront");
                }

                Log($"===== SUKSES: {url} =====");
            }
            catch (WebException wex)
            {
                Log("===== WebException =====");
                Log($"Status      = {wex.Status}");
                Log($"Message     = {wex.Message}");

                if (wex.Response is System.Net.HttpWebResponse resp)
                {
                    Log($"HTTP Status = {(int)resp.StatusCode} {resp.StatusCode}");
                }

                if (_avatarPic != null)
                    _avatarPic.Visible = false;
            }
            catch (Exception ex)
            {
                Log("===== Exception umum =====");
                Log($"Tipe    = {ex.GetType().FullName}");
                Log($"Message = {ex.Message}");
                Log($"Stack   =\n{ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Log($"Inner Exception = {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }

                if (_avatarPic != null)
                    _avatarPic.Visible = false;
            }

            Log("===== SELESAI LoadAvatarAsync =====");
        }

        // =====================================================
        //  KONTEN UTAMA
        // =====================================================
        private Control BuildContent()
        {
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(28, 16, 28, 16),
                ColumnCount = 1,
                RowCount = 5
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            main.Controls.Add(BuildHeader(), 0, 0);
            main.Controls.Add(BuildLotnoBar(), 0, 1);
            main.Controls.Add(BuildInputCard(), 0, 2);
            main.Controls.Add(BuildNButtons(), 0, 3);
            main.Controls.Add(BuildTableCard(), 0, 4);
            return main;
        }

        private Control BuildHeader()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = PageBg,
                Margin = new Padding(0, 0, 0, 10)
            };
            flow.Controls.Add(new Label
            {
                Text = "Shopfloor Crimping",
                AutoSize = true,
                Margin = Padding.Empty,
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                BackColor = PageBg
            });
            flow.Controls.Add(new Label
            {
                Text = "Input data crimping",
                AutoSize = true,
                Margin = new Padding(2, 2, 0, 0),
                ForeColor = TextSub,
                Font = new Font("Segoe UI", 10f),
                BackColor = PageBg
            });
            flow.Controls.Add(new Panel
            {
                Size = new Size(48, 4),
                BackColor = Accent,
                Margin = new Padding(3, 10, 0, 0)
            });
            return flow;
        }

        // =====================================================
        //  BAR LOTNO
        // =====================================================
        private Control BuildLotnoBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 12)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            bar.Controls.Add(new Label
            {
                Text = "LOTNO",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent
            }, 0, 0);

            fieldLotno = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                Padding = new Padding(1),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                FocusColor = Accent
            };

            var inner = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 9, 12, 0) };
            txtLotno = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = AppThemeLogin.FontInput
            };
            inner.Controls.Add(txtLotno);

            var icon = new Label { Dock = DockStyle.Left, Width = 44, AutoSize = false };
            AppThemeLogin.ApplyIcon(icon, IcSearch, "🔍", 12F, FieldIcon);

            fieldLotno.Controls.Add(inner);
            fieldLotno.Controls.Add(icon);

            txtLotno.Enter += (s, e) =>
            {
                fieldLotno.Focused = true;
                if (!txtLotno.ReadOnly) fieldLotno.BorderColor = Accent;
                fieldLotno.Invalidate();
            };
            txtLotno.Leave += (s, e) =>
            {
                fieldLotno.Focused = false;
                if (!txtLotno.ReadOnly) fieldLotno.BorderColor = FieldBorder;
                fieldLotno.Invalidate();
            };

            bar.Controls.Add(fieldLotno, 1, 0);

            eraseLotno = new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 4, 0, 4),
                IconGlyph = IcClose,
                Radius = 10,
                FillColor = AccentSoft,
                BorderColor = AccentLine,
                IconColor = Accent
            };
            bar.Controls.Add(eraseLotno, 2, 0);

            // ===== ENTER di Lotno =====
            txtLotno.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;

                string lotno = txtLotno.Text.Trim();
                if (string.IsNullOrEmpty(lotno))
                {
                    NotifPopup.Warning(this, "Lotno Kosong", "Silakan scan Lotno dulu.");
                    return;
                }

                string tiproductname = GetTiproductnameByLotno(lotno);
                if (string.IsNullOrEmpty(tiproductname))
                {
                    NotifPopup.Warning(this, "Lotno Tidak Ditemukan",
                        $"Lotno '{lotno}' tidak terdaftar di inventory.");
                    txtLotno.Focus();
                    return;
                }

                _state.Tiproductname = tiproductname;
                txtTerminal.Text = tiproductname;

                LockField(fieldLotno, txtLotno, eraseLotno, true);
                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);

                FlashSuccess(fieldLotno);

                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                txtAplikator.Focus();
            };

            // ===== X: reset SEMUA =====
            eraseLotno.Click += (s, e) =>
            {
                txtLotno.Clear();
                txtTerminal.Clear();
                txtAplikator.Clear();
                txtWos.Clear();

                LockField(fieldLotno, txtLotno, eraseLotno, false);
                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, true);

                ResetBorder(fieldLotno, fieldTerminal, fieldAplikator, fieldWos);
                ResetNState();
                ClearTable();

                txtLotno.Focus();
            };

            return bar;
        }

        // =====================================================
        //  KOTAK INPUT: TERMINAL, APLIKATOR, WOS
        // =====================================================
        private Control BuildInputCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(20, 10, 20, 10),
                Radius = 16,
                FillColor = Color.White,
                BorderColor = FieldBorder
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 3,
                RowCount = 3
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            AddField(grid, 0, "TERMINAL", IcTerminal, "🖥",
                     out txtTerminal, out fieldTerminal, out eraseTerminal);
            AddField(grid, 1, "APLIKATOR", AppThemeLogin.IconGear, AppThemeLogin.FallbackGear,
                     out txtAplikator, out fieldAplikator, out eraseAplikator);
            AddField(grid, 2, "WOS", IcTag, "🏷",
                     out txtWos, out fieldWos, out eraseWos);

            LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
            LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
            LockField(fieldWos, txtWos, eraseWos, true);

            // ===== ENTER di Aplikator =====
            txtAplikator.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtAplikator, fieldAplikator)) return;

                string kodeAplikator = txtAplikator.Text.Trim();

                if (!CheckAplikator(kodeAplikator))
                {
                    fieldAplikator.BorderColor = ErrorColor;
                    fieldAplikator.Invalidate();

                    NotifPopup.Warning(this, "Aplikator Tidak Ditemukan",
                        $"Kode Aplikator '{kodeAplikator}' tidak terdaftar di MSAplikator.");

                    txtAplikator.Focus();
                    txtAplikator.SelectAll();
                    return;
                }

                FlashSuccess(fieldAplikator);

                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, false);
                txtWos.Focus();

                LoadDataCrimping(
                    txtTerminal.Text.Trim(),
                    kodeAplikator,
                    Session.Noreg);
            };

            // ===== ENTER di WOS =====
            txtWos.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtWos, fieldWos)) return;

                string wosInput = txtWos.Text.Trim();

                // Validasi format WOS: harus ada titik + skema angka setelahnya
                bool formatValid = wosInput.Contains(".") && TryParseSkema(wosInput, out _);

                if (!formatValid)
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();

                    NotifPopup.Warning(this, "Format WOS Tidak Sesuai",
                        "Format WOS tidak sesuai.\n\n" +
                        "Contoh: BEJ81212112.001");

                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                if (string.IsNullOrEmpty(_state.Tiproductname))
                {
                    NotifPopup.Warning(this, "Lotno Belum Discan",
                        "Scan Lotno dulu sebelum WOS.");
                    txtWos.Focus();
                    return;
                }

                var results = CheckTerminal(_state.Tiproductname, wosInput);

                if (results.Count == 0)
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();

                    NotifPopup.Warning(this, "WOS Tidak Cocok",
                        $"WOS '{wosInput}' tidak ditemukan di TrRPHMesin (kolom barcode)\n" +
                        $"untuk terminal '{_state.Tiproductname}'.\n\n" +
                        "Tidak bisa lanjut.");

                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                TerminalResult selected = null;
                if (results.Count > 1)
                {
                    using (var f = new FormPilihSisi(results))
                    {
                        if (f.ShowDialog(this) != DialogResult.OK)
                        {
                            txtWos.Focus();
                            txtWos.SelectAll();
                            return;
                        }
                        selected = f.SelectedResult;
                    }
                }
                else
                {
                    selected = results[0];
                }

                _state.SisiTerminal = selected.Sisi;
                _state.PatokanCH = selected.CH;
                _state.PatokanIH = selected.IH;
                _state.PatokanCW = selected.CW;
                _state.PatokanIW = selected.IW;
                _state.PatokanTension = selected.Tension;

                if (IsWosDuplicate(
                        wosInput,
                        txtTerminal.Text.Trim(),
                        txtAplikator.Text.Trim(),
                        Session.Noreg))
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();

                    NotifPopup.Warning(this, "WOS Duplikat",
                        $"WOS '{wosInput}' sudah pernah diinput hari ini untuk\n" +
                        $"terminal + aplikator + operator yang sama.\n\n" +
                        "Silakan scan WOS lain.");

                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                FlashSuccess(fieldWos);
                LockField(fieldWos, txtWos, eraseWos, true);
                _nStep = 1;
                SetNButtonState(NState.Ready);
            };

            // ===== ERASE TERMINAL =====
            eraseTerminal.Click += (s, e) =>
            {
                txtTerminal.Clear();
                txtAplikator.Clear();
                txtWos.Clear();

                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, true);

                ResetBorder(fieldTerminal, fieldAplikator, fieldWos);
                ResetNState();
                ClearTable();

                txtLotno.Focus();
            };

            // ===== ERASE APLIKATOR =====
            eraseAplikator.Click += (s, e) =>
            {
                txtAplikator.Clear();
                txtWos.Clear();
                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                LockField(fieldWos, txtWos, eraseWos, true);
                ResetBorder(fieldAplikator, fieldWos);

                ResetAfterSubmit();
                ClearTable();

                txtAplikator.Focus();
            };

            // ===== ERASE WOS =====
            eraseWos.Click += (s, e) =>
            {
                txtWos.Clear();
                LockField(fieldWos, txtWos, eraseWos, false);
                ResetBorder(fieldWos);

                ResetAfterSubmit();

                txtWos.Focus();
            };

            card.Controls.Add(grid);
            return card;
        }

        private void AddField(TableLayoutPanel grid, int row, string label,
                              string glyph, string fallback,
                              out TextBox tb, out RoundedPanel field, out RoundedButton erase)
        {
            grid.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent
            }, 0, row);

            var fld = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 6),
                Padding = new Padding(1),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                FocusColor = Accent
            };

            var inner = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 9, 12, 0) };
            var input = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = AppThemeLogin.FontInput
            };
            inner.Controls.Add(input);

            var icon = new Label { Dock = DockStyle.Left, Width = 44, AutoSize = false };
            AppThemeLogin.ApplyIcon(icon, glyph, fallback, 12F, FieldIcon);

            fld.Controls.Add(inner);
            fld.Controls.Add(icon);

            input.Enter += (s, e) =>
            {
                fld.Focused = true;
                if (!input.ReadOnly) fld.BorderColor = Accent;
                fld.Invalidate();
            };
            input.Leave += (s, e) =>
            {
                fld.Focused = false;
                if (!input.ReadOnly) fld.BorderColor = FieldBorder;
                fld.Invalidate();
            };

            tb = input;
            field = fld;
            grid.Controls.Add(fld, 1, row);

            var er = new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
                IconGlyph = IcErase,
                Radius = 10,
                FillColor = AccentSoft,
                BorderColor = AccentLine,
                IconColor = Accent
            };
            erase = er;
            grid.Controls.Add(er, 2, row);
        }

        // =====================================================
        //  TOMBOL DINAMIS N1 → N2 → N3
        // =====================================================
        private Control BuildNButtons()
        {
            btnN = new RoundedButton
            {
                Text = "N1",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Font = new Font("Segoe UI Semibold", 16f),
                Radius = 14,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                TextColor = TextSub,
                IconGlyph = null
            };

            _nStep = 1;
            SetNButtonState(NState.Locked);

            btnN.Click += (s, e) =>
            {
                if ((btnN.Tag as string) != "ready") return;

                switch (_nStep)
                {
                    case 1:
                        using (var f = new FormN1(
                            _state.PatokanCH,
                            _state.PatokanIH,
                            _state.PatokanCW,
                            _state.PatokanIW,
                            _state.PatokanTension))
                        {
                            if (f.ShowDialog(this) != DialogResult.OK) return;

                            _state.N1_CH = f.CH;
                            _state.N1_IH = f.IH;
                            _state.N1_CW = f.CW;
                            _state.N1_IW = f.IW;
                            _state.N1_Tension = f.Tension;
                        }
                        _nStep = 2;
                        SetNButtonState(NState.Ready);
                        break;

                    case 2:
                        using (var f = new FormN2(
                            _state.PatokanCH,
                            _state.PatokanIH,
                            _state.PatokanCW,
                            _state.PatokanIW))
                        {
                            if (f.ShowDialog(this) != DialogResult.OK) return;

                            _state.N2_CH = f.CH;
                            _state.N2_IH = f.IH;
                            _state.N2_CW = f.CW;
                            _state.N2_IW = f.IW;
                        }
                        _nStep = 3;
                        SetNButtonState(NState.Ready);
                        break;

                    case 3:
                        SubmitN3();
                        break;
                }
            };

            return btnN;
        }

        private void SubmitN3()
        {
            using (var f = new FormN3(
                _state.PatokanCH,
                _state.PatokanIH,
                _state.PatokanCW,
                _state.PatokanIW))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;

                _state.N3_CH = f.CH;
                _state.N3_IH = f.IH;
                _state.N3_CW = f.CW;
                _state.N3_IW = f.IW;
            }

            using (var fng = new FormConfirmNG())
            {
                if (fng.ShowDialog(this) != DialogResult.OK)
                {
                    _state.ClearN3();
                    return;
                }

                _state.N3_NG = fng.HasNG ? (int?)fng.NGValue : null;
            }

            if (IsWosDuplicate(
                    txtWos.Text.Trim(),
                    txtTerminal.Text.Trim(),
                    txtAplikator.Text.Trim(),
                    Session.Noreg))
            {
                NotifPopup.Warning(this, "WOS Sudah Diinput",
                    "WOS ini baru saja diinput oleh proses lain.\n" +
                    "Data tidak disimpan untuk menghindari duplikat.\n\n" +
                    "Silakan scan WOS lain.");

                _state.ClearN3();
                SetNButtonState(NState.Ready);
                return;
            }

            if (!InsertLhhpWH())
            {
                SetNButtonState(NState.Ready);
                return;
            }

            txtWos.Clear();
            LockField(fieldWos, txtWos, eraseWos, false);
            ResetBorder(fieldWos);

            ResetAfterSubmit();

            BeginInvoke(new Action(() =>
            {
                txtWos.Focus();
                txtWos.SelectAll();
            }));

            RefreshTable();
        }

        // =====================================================
        //  TABEL RIWAYAT
        // =====================================================
        private Control BuildTableCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Radius = 16,
                FillColor = Color.White,
                BorderColor = FieldBorder
            };

            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(238, 241, 245),
                EnableHeadersVisualStyles = false,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 46,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowTemplate = { Height = 34 },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Both
            };

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Navy;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Navy;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;

            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.ForeColor = TextDark;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dgv.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            dgv.DefaultCellStyle.SelectionBackColor = AccentSoft;
            dgv.DefaultCellStyle.SelectionForeColor = TextDark;

            dgv.Columns.Add("colNo", "NO");
            dgv.Columns.Add("colLotNo", "LOT\nNUMBER");
            dgv.Columns.Add("colSkema", "SKEMA");
            dgv.Columns.Add("colTerminal", "TERMINAL");
            dgv.Columns.Add("colAplikator", "APLIKATOR");
            dgv.Columns.Add("colChN1", "CH\nN1");
            dgv.Columns.Add("colIhN1", "IH\nN1");
            dgv.Columns.Add("colCwN1", "CW\nN1");
            dgv.Columns.Add("colIwN1", "IW\nN1");
            dgv.Columns.Add("colChN2", "CH\nN2");
            dgv.Columns.Add("colIhN2", "IH\nN2");
            dgv.Columns.Add("colCwN2", "CW\nN2");
            dgv.Columns.Add("colIwN2", "IW\nN2");
            dgv.Columns.Add("colChN3", "CH\nN3");
            dgv.Columns.Add("colIhN3", "IH\nN3");
            dgv.Columns.Add("colCwN3", "CW\nN3");
            dgv.Columns.Add("colIwN3", "IW\nN3");
            dgv.Columns.Add("colNg", "NG");
            dgv.Columns.Add("colTension", "TENSION");

            dgv.Columns["colNo"].Width = 45;
            dgv.Columns["colLotNo"].Width = 130;
            dgv.Columns["colSkema"].Width = 80;
            dgv.Columns["colTerminal"].Width = 100;
            dgv.Columns["colAplikator"].Width = 110;

            foreach (var name in new[] { "colLotNo", "colSkema", "colTerminal", "colAplikator" })
            {
                var col = dgv.Columns[name];
                col.MinimumWidth = col.Width;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }

            var colNo = dgv.Columns["colNo"];
            colNo.MinimumWidth = 45;
            colNo.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colNo.DefaultCellStyle.Font = new Font("Consolas", 9f);
            colNo.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            foreach (var name in new[]
            {
                "colChN1", "colIhN1", "colCwN1", "colIwN1",
                "colChN2", "colIhN2", "colCwN2", "colIwN2",
                "colChN3", "colIhN3", "colCwN3", "colIwN3",
                "colNg", "colTension"
            })
            {
                var col = dgv.Columns[name];
                col.Width = 62;
                col.MinimumWidth = 62;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Font = new Font("Consolas", 9f);
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            dgv.Columns["colTension"].Width = 80;
            dgv.Columns["colTension"].MinimumWidth = 80;

            foreach (DataGridViewColumn c in dgv.Columns)
                c.FillWeight = c.Width;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.Rows.Clear();

            card.Controls.Add(dgv);
            return card;
        }

        // =====================================================
        //  QUERY: tanggal PRODUKSI dari jam server SQL
        //  Konsisten dengan LoadDataCrimping / IsWosDuplicate:
        //  sebelum jam 06:00 masih dihitung hari kemarin.
        // =====================================================
        private DateTime? GetServerTanggalProduksi()
        {
            const string sql = @"
SELECT CASE
           WHEN DATEPART(HOUR, GETDATE()) < 6
               THEN CAST(DATEADD(DAY, -1, GETDATE()) AS DATE)
           ELSE CAST(GETDATE() AS DATE)
       END;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.CommandTimeout = 5;
                    object result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value) return null;
                    return Convert.ToDateTime(result);
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Ambil Tanggal Server",
                    "Gagal mengambil tanggal dari server:\n\n" + ex.Message);
                return null;
            }
        }

        // =====================================================
        //  EXPORT PDF
        // =====================================================
        private void ExportPdf()
        {
            string noreg = Session.Noreg;
            string aplikator = txtAplikator?.Text?.Trim();
            string terminal = txtTerminal?.Text?.Trim();
            string mesin = Session.mesin;

            if (string.IsNullOrWhiteSpace(noreg) ||
                string.IsNullOrWhiteSpace(aplikator) ||
                string.IsNullOrWhiteSpace(terminal) ||
                string.IsNullOrWhiteSpace(mesin))
            {
                NotifPopup.Warning(this, "Data Belum Lengkap",
                    "Pastikan Terminal dan Aplikator sudah diisi, dan sesi login (Noreg & Mesin) valid.");
                return;
            }

            DateTime? tglServer = GetServerTanggalProduksi();
            if (tglServer == null) return;   // error sudah ditampilkan di method

            string tanggal = tglServer.Value.ToString("yyyy-MM-dd");
            const string baseUrl = "http://192.168.5.4/laporan-crimping-manual/controllers/export.php";

            string url =
                $"{baseUrl}?action=export_pdf" +
                $"&noreg={Uri.EscapeDataString(noreg)}" +
                $"&tanggal={Uri.EscapeDataString(tanggal)}" +
                $"&aplikator={Uri.EscapeDataString(aplikator)}" +
                $"&terminal={Uri.EscapeDataString(terminal)}" +
                $"&mesin={Uri.EscapeDataString(mesin)}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Gagal Membuka Browser",
                    "Tidak dapat membuka URL export:\n\n" + ex.Message);
            }
        }

        // =====================================================
        //  HELPER: Lock / Validasi / Flash
        // =====================================================
        private void LockField(RoundedPanel field, TextBox tb, RoundedButton erase, bool locked)
        {
            tb.ReadOnly = locked;

            if (locked)
            {
                tb.BackColor = FieldLockedBg;
                tb.ForeColor = TextSub;
                field.FillColor = FieldLockedBg;
                field.BorderColor = FieldBorder;
            }
            else
            {
                tb.BackColor = FieldBg;
                tb.ForeColor = TextDark;
                field.FillColor = FieldBg;
                field.BorderColor = field.Focused ? Accent : FieldBorder;
            }
            field.Invalidate();
            erase.Enabled = true;
        }

        private bool ValidateNotEmpty(TextBox tb, RoundedPanel field)
        {
            if (!string.IsNullOrWhiteSpace(tb.Text)) return true;

            field.BorderColor = ErrorColor;
            field.Invalidate();

            var t = new Timer { Interval = 1200 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                if (tb.ReadOnly) field.BorderColor = FieldBorder;
                else field.BorderColor = field.Focused ? Accent : FieldBorder;
                field.Invalidate();
            };
            t.Start();

            return false;
        }

        private void FlashSuccess(RoundedPanel field)
        {
            field.BorderColor = SuccessColor;
            field.Invalidate();

            var t = new Timer { Interval = 800 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                field.BorderColor = field.Focused ? Accent : FieldBorder;
                field.Invalidate();
            };
            t.Start();
        }

        private void ResetBorder(params RoundedPanel[] fields)
        {
            foreach (var f in fields)
            {
                f.BorderColor = f.Focused ? Accent : FieldBorder;
                f.Invalidate();
            }
        }

        // =====================================================
        //  STATE tombol N
        // =====================================================
        private enum NState { Locked, Ready }

        private void SetNButtonState(NState state)
        {
            if (btnN == null) return;

            btnN.Text = "N" + _nStep;
            btnN.Font = new Font("Segoe UI Semibold", 16f);
            btnN.IconGlyph = null;

            if (state == NState.Locked)
            {
                btnN.Tag = "locked";
                btnN.Enabled = true;
                btnN.FillColor = NLockedBg;
                btnN.BorderColor = NLockedBorder;
                btnN.TextColor = NLockedText;
                btnN.Cursor = Cursors.Default;
            }
            else
            {
                btnN.Tag = "ready";
                btnN.Enabled = true;
                btnN.FillColor = AccentSoft;
                btnN.BorderColor = Accent;
                btnN.TextColor = Accent;
                btnN.Cursor = Cursors.Hand;
            }
            btnN.Invalidate();
        }

        // =====================================================
        //  RESET STATE
        // =====================================================

        /// <summary>
        /// Reset TOTAL.
        /// Menghapus hasil N1/N2/N3 + patokan + sisi + Tiproductname.
        /// Dipakai saat Lotno/Terminal direset → operator WAJIB
        /// scan Lotno ulang sebelum bisa lanjut.
        /// </summary>
        private void ResetNState()
        {
            _state.ClearAll();
            _nStep = 1;
            SetNButtonState(NState.Locked);
        }

        /// <summary>
        /// Reset SETELAH SUBMIT N3 / ERASE APLIKATOR / ERASE WOS.
        /// Menghapus hasil N1/N2/N3 + patokan + sisi,
        /// TAPI Tiproductname DIPERTAHANKAN.
        /// Jadi operator bisa LANGSUNG scan WOS berikutnya tanpa
        /// scan Lotno & Aplikator lagi.
        /// </summary>
        private void ResetAfterSubmit()
        {
            _state.ClearN1();
            _state.ClearN2();
            _state.ClearN3();
            _state.ClearPatokanOnly();

            _nStep = 1;
            SetNButtonState(NState.Locked);
        }

        private void ClearTable()
        {
            if (dgv != null) dgv.Rows.Clear();
        }

        // =====================================================
        //  QUERY: LoadDataCrimping
        // =====================================================
        private void LoadDataCrimping(string terminal, string aplikator, string noreg)
        {
            if (string.IsNullOrEmpty(terminal) ||
                string.IsNullOrEmpty(aplikator) ||
                string.IsNullOrEmpty(noreg))
                return;

            const string sql = @"
DECLARE @tanggalProduksi DATE =
    CASE
        WHEN DATEPART(HOUR, GETDATE()) < 6
            THEN CAST(DATEADD(DAY, -1, GETDATE()) AS DATE)
        ELSE
            CAST(GETDATE() AS DATE)
    END;

SELECT 
    Wos,
    terminal,
    aplikator,
    ch_n1, ih_n1, cw_n1, iw_n1,
    ch_n2, ih_n2, cw_n2, iw_n2,
    ch_n3, ih_n3, cw_n3, iw_n3,
    ng_n3,
    tension,
    created_at
FROM TrCrimpingResult
WHERE terminal = @terminal
  AND aplikator = @aplikator
  AND noreg = @noreg
  AND created_at >= DATEADD(HOUR, 6, CAST(@tanggalProduksi AS DATETIME))
  AND created_at <  DATEADD(HOUR, 30, CAST(@tanggalProduksi AS DATETIME))
ORDER BY created_at DESC;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@terminal", terminal);
                    cmd.Parameters.AddWithValue("@aplikator", aplikator);
                    cmd.Parameters.AddWithValue("@noreg", noreg);

                    using (var reader = cmd.ExecuteReader())
                    {
                        dgv.Rows.Clear();

                        int no = 0;
                        while (reader.Read())
                        {
                            no++;

                            string wos = reader["Wos"] as string ?? "";
                            string term = reader["terminal"] as string ?? "";
                            string apl = reader["aplikator"] as string ?? "";

                            string lotNo = "";
                            string skema = "-";

                            if (!string.IsNullOrEmpty(wos))
                            {
                                int idx = wos.LastIndexOf('.');
                                if (idx > 0 && idx < wos.Length - 1)
                                {
                                    lotNo = wos.Substring(0, idx);
                                    string skemaStr = wos.Substring(idx + 1);

                                    if (int.TryParse(skemaStr, out int skemaInt))
                                        skema = skemaInt.ToString();
                                    else
                                        skema = skemaStr;
                                }
                                else
                                {
                                    lotNo = wos;
                                }
                            }

                            string chN1 = FormatDecimal(reader["ch_n1"]);
                            string ihN1 = FormatDecimal(reader["ih_n1"]);
                            string cwN1 = FormatDecimal(reader["cw_n1"]);
                            string iwN1 = FormatDecimal(reader["iw_n1"]);

                            string chN2 = FormatDecimal(reader["ch_n2"]);
                            string ihN2 = FormatDecimal(reader["ih_n2"]);
                            string cwN2 = FormatDecimal(reader["cw_n2"]);
                            string iwN2 = FormatDecimal(reader["iw_n2"]);

                            string chN3 = FormatDecimal(reader["ch_n3"]);
                            string ihN3 = FormatDecimal(reader["ih_n3"]);
                            string cwN3 = FormatDecimal(reader["cw_n3"]);
                            string iwN3 = FormatDecimal(reader["iw_n3"]);

                            string ng = FormatInt(reader["ng_n3"]);
                            string tension = FormatDecimal(reader["tension"]);

                            dgv.Rows.Add(
                                no,
                                lotNo, skema, term, apl,
                                chN1, ihN1, cwN1, iwN1,
                                chN2, ihN2, cwN2, iwN2,
                                chN3, ihN3, cwN3, iwN3,
                                ng, tension);
                        }

                        dgv.ClearSelection();
                    }
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Query",
                    "Gagal memuat data dari database:\n\n" + ex.Message);
            }
        }

        private void RefreshTable()
        {
            LoadDataCrimping(
                txtTerminal.Text.Trim(),
                txtAplikator.Text.Trim(),
                Session.Noreg);
        }

        // =====================================================
        //  INSERT: simpan satu siklus N1+N2+N3
        // =====================================================
        private bool InsertLhhpWH()
        {
            string terminal = txtTerminal.Text.Trim();
            string aplikator = txtAplikator.Text.Trim();
            string wos = txtWos.Text.Trim();
            string lotno = wos.Split('.')[0];

            const string sql = @"
INSERT INTO TrCrimpingResult
(
    noreg, terminal, lotno, aplikator, Wos,
    ch_n1, ih_n1, cw_n1, iw_n1, tension,
    ch_n2, ih_n2, cw_n2, iw_n2,
    ch_n3, ih_n3, cw_n3, iw_n3, ng_n3,
    shift, kode_mesin, created_at
)
VALUES
(
    @noreg, @terminal, @lotno, @aplikator, @wos,
    @ch_n1, @ih_n1, @cw_n1, @iw_n1, @tension,
    @ch_n2, @ih_n2, @cw_n2, @iw_n2,
    @ch_n3, @ih_n3, @cw_n3, @iw_n3, @ng_n3,
    @shift, @kode_mesin, getDate()
);";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@noreg", Session.Noreg ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@terminal", terminal);
                    cmd.Parameters.AddWithValue("@lotno", lotno);
                    cmd.Parameters.AddWithValue("@aplikator", aplikator);
                    cmd.Parameters.AddWithValue("@wos", wos);

                    cmd.Parameters.AddWithValue("@ch_n1", _state.N1_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n1", _state.N1_IH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw_n1", _state.N1_CW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@iw_n1", _state.N1_IW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@tension", _state.N1_Tension ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@ch_n2", _state.N2_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n2", _state.N2_IH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw_n2", _state.N2_CW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@iw_n2", _state.N2_IW ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@ch_n3", _state.N3_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n3", _state.N3_IH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw_n3", _state.N3_CW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@iw_n3", _state.N3_IW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ng_n3", _state.N3_NG ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@shift", Session.Shift);
                    cmd.Parameters.AddWithValue("@kode_mesin", Session.mesin ?? (object)DBNull.Value);

                    int rows = cmd.ExecuteNonQuery();
                    if (rows <= 0)
                    {
                        NotifPopup.Error(this, "Insert Gagal",
                            "Insert tidak menyimpan data apapun. Cek kembali.");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Insert",
                    "Gagal menyimpan data ke database:\n\n" + ex.Message);
                return false;
            }
        }

        // =====================================================
        //  HELPER FORMAT TAMPILAN
        // =====================================================
        private static string FormatDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return "-";
            decimal d;
            if (!decimal.TryParse(value.ToString(), out d)) return "-";
            return d.ToString("0.00");
        }

        private static string FormatInt(object value)
        {
            if (value == null || value == DBNull.Value) return "-";
            int i;
            if (!int.TryParse(value.ToString(), out i)) return "-";
            return i.ToString();
        }

        // =====================================================
        //  QUERY: Lotno → tiproductname
        // =====================================================
        private string GetTiproductnameByLotno(string tlotid)
        {
            if (string.IsNullOrWhiteSpace(tlotid)) return null;

            const string sql = @"
SELECT b.tiproductname
FROM dbInventory2..tblFifo a
LEFT JOIN dbInventory2..tblItem b ON b.tiid = a.tiid
WHERE a.tlotid = @tlotid;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@tlotid", tlotid);
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value
                        ? null
                        : result.ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Query Lotno",
                    "Gagal mengambil tiproductname:\n\n" + ex.Message);
                return null;
            }
        }

        // =====================================================
        //  QUERY: tiproductname + WOS → patokan di TrRPHMesin
        // =====================================================
        private List<TerminalResult> CheckTerminal(string tiproductname, string wos)
        {
            var list = new List<TerminalResult>();
            if (string.IsNullOrWhiteSpace(tiproductname)) return list;
            if (string.IsNullOrWhiteSpace(wos)) return list;

            const string sql = @"
SELECT KodeMesin, TERMINALA, TERMINALB,
       cha, iha, cwa, iwa, T1,
       chb, ihb, cwb, iwb, T2
FROM [KUJNAGN].[dbo].[TrRPHMesin]
WHERE (TERMINALA = @tiproductname OR TERMINALB = @tiproductname)
  AND barcode = @wos
ORDER BY KodeRPHMeisn DESC;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@tiproductname", tiproductname);
                    cmd.Parameters.AddWithValue("@wos", wos);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string termA = reader["TERMINALA"] as string;
                            string termB = reader["TERMINALB"] as string;

                            if (termA == tiproductname)
                            {
                                list.Add(new TerminalResult
                                {
                                    Sisi = "A",
                                    KodeMesin = reader["KodeMesin"] as string,
                                    CH = ReadDecimal(reader["cha"]),
                                    IH = ReadDecimal(reader["iha"]),
                                    CW = ReadDecimal(reader["cwa"]),
                                    IW = ReadDecimal(reader["iwa"]),
                                    Tension = ReadDecimal(reader["T1"])
                                });
                            }

                            if (termB == tiproductname)
                            {
                                list.Add(new TerminalResult
                                {
                                    Sisi = "B",
                                    KodeMesin = reader["KodeMesin"] as string,
                                    CH = ReadDecimal(reader["chb"]),
                                    IH = ReadDecimal(reader["ihb"]),
                                    CW = ReadDecimal(reader["cwb"]),
                                    IW = ReadDecimal(reader["iwb"]),
                                    Tension = ReadDecimal(reader["T2"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Query Terminal",
                    "Gagal cek Terminal A/B:\n\n" + ex.Message);
            }

            return list;
        }

        // =====================================================
        //  QUERY: validasi kode Aplikator ke MSAplikator
        // =====================================================
        private bool CheckAplikator(string kodeAplikator)
        {
            if (string.IsNullOrWhiteSpace(kodeAplikator)) return false;

            const string sql = @"
SELECT COUNT(*) 
FROM MSAplikator 
WHERE KodeAplikator LIKE '%' + @kode + '%';";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@kode", kodeAplikator.Trim());
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Query Aplikator",
                    "Gagal cek Aplikator ke database:\n\n" + ex.Message);
                return false;
            }
        }

        // =====================================================
        //  QUERY: cek WOS duplikat
        // =====================================================
        private bool IsWosDuplicate(string wos, string terminal, string aplikator, string noreg)
        {
            if (string.IsNullOrWhiteSpace(wos) ||
                string.IsNullOrWhiteSpace(terminal) ||
                string.IsNullOrWhiteSpace(aplikator) ||
                string.IsNullOrWhiteSpace(noreg))
                return false;

            const string sql = @"
DECLARE @tanggalProduksi DATE =
    CASE
        WHEN DATEPART(HOUR, GETDATE()) < 6
            THEN CAST(DATEADD(DAY, -1, GETDATE()) AS DATE)
        ELSE
            CAST(GETDATE() AS DATE)
    END;

SELECT COUNT(*)
FROM TrCrimpingResult
WHERE Wos = @wos
  AND noreg = @noreg
  AND terminal = @terminal
  AND aplikator = @aplikator
  AND created_at >= DATEADD(HOUR, 6, CAST(@tanggalProduksi AS DATETIME))
  AND created_at <  DATEADD(HOUR, 30, CAST(@tanggalProduksi AS DATETIME));";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@wos", wos);
                    cmd.Parameters.AddWithValue("@noreg", noreg);
                    cmd.Parameters.AddWithValue("@terminal", terminal);
                    cmd.Parameters.AddWithValue("@aplikator", aplikator);

                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Cek Duplikat WOS",
                    "Gagal cek WOS duplikat ke database:\n\n" + ex.Message);

                return true;
            }
        }

        // =====================================================
        //  VALIDASI: Skema pada WOS
        // =====================================================
        private bool TryParseSkema(string wos, out int skemaInt)
        {
            skemaInt = -1;
            if (string.IsNullOrWhiteSpace(wos)) return false;

            int idx = wos.LastIndexOf('.');
            if (idx < 0 || idx >= wos.Length - 1) return false;

            string skema = wos.Substring(idx + 1).Trim();

            // Fleksibel: 1 s/d 5 digit angka
            if (skema.Length < 1 || skema.Length > 5) return false;
            if (!skema.All(char.IsDigit)) return false;

            return int.TryParse(skema, out skemaInt);
        }

        private static decimal? ReadDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            decimal d;
            return decimal.TryParse(value.ToString(), out d) ? d : (decimal?)null;
        }
    }
}