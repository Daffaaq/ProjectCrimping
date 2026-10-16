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
    ///  1. Scan LOT MATERIAL (field di Card 1) → cari tipartnumber + tiid
    ///                                            di inventory.
    ///                                            Simpan LOT MATERIAL asli + tipartnumber
    ///                                            + tiid ke _state.
    ///                                            Field ditimpa tampilannya jadi tipartnumber.
    ///                                            (Patokan BELUM diambil di sini.)
    ///  2. Scan APLIKATOR (field di Card 1)    → divalidasi ke tabel MSAplikator,
    ///                                            lalu tabel riwayat hari ini dimuat.
    ///  3. Scan WOS (field di Card 2, format XXXX.001)
    ///                                         → validasi format; lalu cari patokan
    ///                                            di TrRPHMesin dengan DUA kunci:
    ///                                              - tipartnumber di TERMINALA/TERMINALB (PRIORITAS)
    ///                                              - tiid          di RmiTermA/RmiTermB  (CADANGAN)
    ///                                            Syarat kedua selalu: barcode = WOS.
    ///                                            Ambil TOP 1 (kalau data dobel, ambil 1 saja).
    ///                                            Tombol N1 aktif setelah WOS valid.
    ///  4. Tekan N1 → N2 → N3 (1 tombol)       → tiap tombol membuka form input ukuran.
    ///                                            Setelah N3 + konfirmasi NG, data di-INSERT
    ///                                            ke TrCrimpingResult dan tombol kembali ke N1.
    ///  5. Fokus kembali ke WOS                → operator bisa scan WOS berikutnya TANPA
    ///                                            scan ulang LOT MATERIAL/Aplikator, karena
    ///                                            ResetAfterSubmit() mempertahankan
    ///                                            LotMaterial + tipartnumber + tiid.
    ///
    /// ================================================================
    ///  STRUKTUR LAYOUT (BuildContent)
    /// ================================================================
    ///  Form
    ///   ├─ Sidebar (kiri, 270 px, Dock.Left)  : profil, menu, logout
    ///   └─ Konten (Dock.Fill, TableLayoutPanel 1 kolom x 5 baris)
    ///        ├─ Baris 0 (AutoSize)   : Header judul
    ///        ├─ Baris 1 (156 px)     : Card 1 — LOT MATERIAL + APLIKATOR
    ///        ├─ Baris 2 (120 px)     : Card 2 — WOS
    ///        ├─ Baris 3 (52 px)      : Tombol dinamis N1/N2/N3
    ///        └─ Baris 4 (sisa ruang) : Tabel riwayat (DataGridView)
    ///
    /// ================================================================
    ///  CATATAN PENTING UNTUK MAINTAINER
    /// ================================================================
    ///  - TIGA nilai dari LOT MATERIAL:
    ///        * LotMaterial   = LOT MATERIAL asli yang discan operator
    ///                          → DISIMPAN ke TrCrimpingResult.terminal
    ///        * Tipartnumber  = hasil lookup dari tblItem
    ///                          → DIPAKAI untuk cek TERMINALA/TERMINALB
    ///                          → DIPAKAI sebagai TAMPILAN di field
    ///        * Tiid          = hasil lookup dari tblFifo
    ///                          → DIPAKAI sebagai CADANGAN cek RmiTermA/RmiTermB
    ///  - "Patokan" = nilai standar CH/IH/CW/IW/Tension dari TrRPHMesin,
    ///    diambil SETELAH WOS di-scan (karena butuh kolom barcode).
    ///  - Ada DUA jenis reset, jangan ketukar:
    ///        * ResetNState()       → hapus SEMUA, termasuk LotMaterial.
    ///        * ResetAfterSubmit()  → hapus hasil N + patokan + sisi,
    ///                                 TAPI pertahankan LotMaterial + Tipartnumber + Tiid.
    ///  - "Hari produksi" berjalan jam 06:00 s/d 06:00 hari berikutnya.
    ///  - KUNCI WOS: CheckTerminal terima DUA kunci sekaligus:
    ///        - tipartnumber → dicek ke TERMINALA / TERMINALB   (PRIORITAS)
    ///        - tiid         → dicek ke RmiTermA  / RmiTermB    (CADANGAN)
    ///    Cuma SATU panggilan CheckTerminal di txtWos.KeyDown.
    ///    TOP 1 di SQL → ambil satu baris terbaik saja (antisipasi data dobel).
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

        private static readonly Font BigInputFont = new Font("Segoe UI Semibold", 18f);

        private static readonly Color FieldLockedBg = Color.FromArgb(238, 240, 243);

        private static readonly Color NLockedBg = Color.FromArgb(237, 239, 242);
        private static readonly Color NLockedBorder = Color.FromArgb(221, 225, 230);
        private static readonly Color NLockedText = Color.FromArgb(168, 176, 186);

        // ================================================================
        //  KONSTANTA: URL FOTO PROFIL
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
        private const string IcTerminal = "\uE7F4";
        private const string IcTag = "\uE8EC";

        // ================================================================
        //  KONTROL UI
        // ================================================================
        private TextBox txtTerminal, txtAplikator, txtWos;

        private RoundedButton btnN;
        private int _nStep = 1;
        private DataGridView dgv;

        private RoundedPanel fieldTerminal, fieldAplikator, fieldWos;
        private RoundedButton eraseTerminal, eraseAplikator, eraseWos;

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

            LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
            LockField(fieldWos, txtWos, eraseWos, true);

            ActiveControl = txtTerminal;
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

            // ===== LOT MATERIAL ASLI (yang discan operator) =====
            // Disimpan ke TrCrimpingResult.terminal.
            // SENGAJA dipertahankan setelah submit (tidak dihapus di ClearPatokanOnly()).
            public string LotMaterial { get; set; }

            // ===== hasil lookup dari tblItem =====
            // Dipakai untuk cek TERMINALA / TERMINALB di TrRPHMesin.
            // Dipakai juga sebagai tampilan di field LOT MATERIAL.
            public string Tipartnumber { get; set; }

            // ===== hasil lookup dari tblFifo =====
            // Dipakai sebagai kunci CADANGAN: cek ke RmiTermA / RmiTermB.
            public string Tiid { get; set; }

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
                LotMaterial = null;             // ===== ikut dihapus di reset total
                Tipartnumber = null;
                Tiid = null;
                SisiTerminal = null;
                PatokanCH = null; PatokanIH = null; PatokanCW = null; PatokanIW = null; PatokanTension = null;
            }

            /// <summary>
            /// Hapus patokan + sisi terminal saja.
            /// LotMaterial + Tipartnumber + Tiid SENGAJA tidak dihapus —
            /// dipakai ResetAfterSubmit() supaya operator bisa langsung scan
            /// WOS berikutnya tanpa scan LOT MATERIAL ulang.
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

        private async Task LoadAvatarAsync()
        {
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

            if (!string.IsNullOrWhiteSpace(ext))
                ext = ext.TrimStart('.').Trim();

            if (string.IsNullOrWhiteSpace(noreg))
            {
                _avatarPic.Visible = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(ext))
            {
                _avatarPic.Visible = false;
                return;
            }

            string url = AvatarBaseUrl + Uri.EscapeDataString(noreg) + "." + ext;

            try
            {
                byte[] data;

                using (var wc = new WebClient())
                {
                    var downloadTask = wc.DownloadDataTaskAsync(url);
                    var timeoutTask = Task.Delay(5000);

                    var completed = await Task.WhenAny(downloadTask, timeoutTask);

                    if (completed == timeoutTask)
                    {
                        _avatarPic.Visible = false;
                        return;
                    }

                    data = await downloadTask;
                }

                if (data == null || data.Length == 0)
                {
                    _avatarPic.Visible = false;
                    return;
                }

                Image img;
                using (var ms = new System.IO.MemoryStream(data))
                {
                    using (var raw = Image.FromStream(ms))
                    {
                        img = new Bitmap(raw);
                    }
                }

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        _avatarPic.Image = img;
                        _avatarPic.Visible = true;
                        _avatarPic.BringToFront();
                        _avatarPic.Invalidate();
                    }));
                }
                else
                {
                    _avatarPic.Image = img;
                    _avatarPic.Visible = true;
                    _avatarPic.BringToFront();
                    _avatarPic.Invalidate();
                }
            }
            catch (WebException)
            {
                if (_avatarPic != null) _avatarPic.Visible = false;
            }
            catch (Exception)
            {
                if (_avatarPic != null) _avatarPic.Visible = false;
            }
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
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            main.Controls.Add(BuildHeader(), 0, 0);
            main.Controls.Add(BuildLotMaterialCard(), 0, 1);
            main.Controls.Add(BuildWosCard(), 0, 2);
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
        //  CARD 1: LOT MATERIAL + APLIKATOR
        // =====================================================
        private Control BuildLotMaterialCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(20, 14, 20, 14),
                Radius = 16,
                FillColor = Color.White,
                BorderColor = FieldBorder
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ---------- Kolom kiri: LOT MATERIAL ----------
            var colLot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 0, 10, 0)
            };
            colLot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            colLot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            colLot.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            colLot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            colLot.Controls.Add(new Label
            {
                Text = "LOT MATERIAL",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            }, 0, 0);

            var fldLot = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(1),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                FocusColor = Accent
            };
            var innerLot = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
            txtTerminal = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = BigInputFont
            };
            innerLot.Controls.Add(txtTerminal);
            CenterInput(innerLot, txtTerminal);
            var iconLot = new Label { Dock = DockStyle.Left, Width = 44, AutoSize = false };
            AppThemeLogin.ApplyIcon(iconLot, IcTerminal, "🖥", 12F, FieldIcon);
            fldLot.Controls.Add(innerLot);
            fldLot.Controls.Add(iconLot);
            fieldTerminal = fldLot;
            colLot.Controls.Add(fldLot, 0, 1);

            eraseTerminal = new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                IconGlyph = IcErase,
                Radius = 10,
                FillColor = AccentSoft,
                BorderColor = AccentLine,
                IconColor = Accent
            };
            colLot.Controls.Add(eraseTerminal, 1, 1);
            grid.Controls.Add(colLot, 0, 0);

            // ---------- Kolom kanan: APLIKATOR ----------
            var colApl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(10, 0, 0, 0)
            };
            colApl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            colApl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            colApl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            colApl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            colApl.Controls.Add(new Label
            {
                Text = "APLIKATOR",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            }, 0, 0);

            var fldApl = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(1),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                FocusColor = Accent
            };
            var innerApl = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
            txtAplikator = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = BigInputFont
            };
            innerApl.Controls.Add(txtAplikator);
            CenterInput(innerApl, txtAplikator);
            var iconApl = new Label { Dock = DockStyle.Left, Width = 44, AutoSize = false };
            AppThemeLogin.ApplyIcon(iconApl, AppThemeLogin.IconGear, AppThemeLogin.FallbackGear, 12F, FieldIcon);
            fldApl.Controls.Add(innerApl);
            fldApl.Controls.Add(iconApl);
            fieldAplikator = fldApl;
            colApl.Controls.Add(fldApl, 0, 1);

            eraseAplikator = new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                IconGlyph = IcErase,
                Radius = 10,
                FillColor = AccentSoft,
                BorderColor = AccentLine,
                IconColor = Accent
            };
            colApl.Controls.Add(eraseAplikator, 1, 1);
            grid.Controls.Add(colApl, 1, 0);

            // ----- Focus border event -----
            txtTerminal.Enter += (s, e) => { fldLot.Focused = true; if (!txtTerminal.ReadOnly) fldLot.BorderColor = Accent; fldLot.Invalidate(); };
            txtTerminal.Leave += (s, e) => { fldLot.Focused = false; if (!txtTerminal.ReadOnly) fldLot.BorderColor = FieldBorder; fldLot.Invalidate(); };
            txtAplikator.Enter += (s, e) => { fldApl.Focused = true; if (!txtAplikator.ReadOnly) fldApl.BorderColor = Accent; fldApl.Invalidate(); };
            txtAplikator.Leave += (s, e) => { fldApl.Focused = false; if (!txtAplikator.ReadOnly) fldApl.BorderColor = FieldBorder; fldApl.Invalidate(); };

            txtAplikator.Enter += (s, e) =>
            {
                if (txtAplikator.ReadOnly) BeginInvoke(new Action(FocusCurrentStep));
            };

            // ===== ENTER di LOT MATERIAL =====
            txtTerminal.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtTerminal, fieldTerminal)) return;

                string lotMaterial = txtTerminal.Text.Trim();

                LotInfo lotInfo = GetLotInfoByLotno(lotMaterial);
                if (lotInfo == null || string.IsNullOrEmpty(lotInfo.Tipartnumber))
                {
                    fieldTerminal.BorderColor = ErrorColor;
                    fieldTerminal.Invalidate();

                    NotifPopup.Warning(this, "LOT MATERIAL Tidak Ditemukan",
                        $"LOT MATERIAL '{lotMaterial}' tidak ditemukan.\n\nPeriksa kembali barcode yang discan.");

                    txtTerminal.Focus();
                    txtTerminal.SelectAll();
                    return;
                }

                // ===== SIMPAN TIGA NILAI =====
                _state.LotMaterial = lotMaterial;              // ← yang DISIMPAN ke DB
                _state.Tipartnumber = lotInfo.Tipartnumber;    // ← untuk cek TERMINALA/TERMINALB + tampilan
                _state.Tiid = lotInfo.Tiid;                    // ← cadangan: cek RmiTermA/RmiTermB

                // Tampilan field diganti jadi tipartnumber (biar operator tahu materialnya).
                txtTerminal.Text = lotInfo.Tipartnumber;

                FlashSuccess(fieldTerminal);
                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);

                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                txtAplikator.Focus();
            };

            // ===== ENTER di APLIKATOR =====
            txtAplikator.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;

                if (txtAplikator.ReadOnly) return;
                if (string.IsNullOrEmpty(_state.LotMaterial))
                {
                    NotifPopup.Warning(this, "Urutan Salah",
                        "Scan LOT MATERIAL dulu sebelum APLIKATOR.");
                    FocusCurrentStep();
                    return;
                }

                if (!ValidateNotEmpty(txtAplikator, fieldAplikator)) return;

                string kodeAplikator = txtAplikator.Text.Trim();

                if (!CheckAplikator(kodeAplikator))
                {
                    fieldAplikator.BorderColor = ErrorColor;
                    fieldAplikator.Invalidate();

                    NotifPopup.Warning(this, "Aplikator Tidak Ditemukan",
                        $"Aplikator '{kodeAplikator}' tidak ditemukan.\n\nPeriksa kembali barcode aplikator yang discan.");

                    txtAplikator.Focus();
                    txtAplikator.SelectAll();
                    return;
                }

                FlashSuccess(fieldAplikator);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);

                LockField(fieldWos, txtWos, eraseWos, false);
                txtWos.Focus();

                // Pakai LOT MATERIAL asli (bukan txtTerminal.Text yang sudah tipartnumber)
                LoadDataCrimping(
                    _state.LotMaterial,
                    kodeAplikator,
                    Session.Noreg);
            };

            // ===== ERASE LOT MATERIAL → reset total =====
            eraseTerminal.Click += (s, e) =>
            {
                txtTerminal.Clear();
                txtAplikator.Clear();
                txtWos.Clear();

                LockField(fieldTerminal, txtTerminal, eraseTerminal, false);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, true);

                ResetBorder(fieldTerminal, fieldAplikator, fieldWos);
                ResetNState();
                ClearTable();

                txtTerminal.Focus();
            };

            // ===== ERASE APLIKATOR =====
            eraseAplikator.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(_state.LotMaterial))
                {
                    FocusCurrentStep();
                    return;
                }

                txtAplikator.Clear();
                txtWos.Clear();

                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                LockField(fieldWos, txtWos, eraseWos, true);
                ResetBorder(fieldAplikator, fieldWos);

                ResetAfterSubmit();
                ClearTable();

                txtAplikator.Focus();
            };

            card.Controls.Add(grid);
            return card;
        }

        // =====================================================
        //  CARD 2: WOS
        // =====================================================
        private Control BuildWosCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(20, 14, 20, 14),
                Radius = 16,
                FillColor = Color.White,
                BorderColor = FieldBorder
            };

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 3,
                RowCount = 1
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            row.Controls.Add(new Label
            {
                Text = "WOS",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            }, 0, 0);

            var fld = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(1),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                FocusColor = Accent
            };
            var inner = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };
            txtWos = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = BigInputFont
            };
            inner.Controls.Add(txtWos);
            CenterInput(inner, txtWos);
            var icon = new Label { Dock = DockStyle.Left, Width = 44, AutoSize = false };
            AppThemeLogin.ApplyIcon(icon, IcTag, "🏷", 12F, FieldIcon);
            fld.Controls.Add(inner);
            fld.Controls.Add(icon);
            fieldWos = fld;
            row.Controls.Add(fld, 1, 0);

            eraseWos = new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                IconGlyph = IcErase,
                Radius = 10,
                FillColor = AccentSoft,
                BorderColor = AccentLine,
                IconColor = Accent
            };
            row.Controls.Add(eraseWos, 2, 0);

            txtWos.Enter += (s, e) => { fld.Focused = true; if (!txtWos.ReadOnly) fld.BorderColor = Accent; fld.Invalidate(); };
            txtWos.Leave += (s, e) => { fld.Focused = false; if (!txtWos.ReadOnly) fld.BorderColor = FieldBorder; fld.Invalidate(); };

            txtWos.Enter += (s, e) =>
            {
                if (txtWos.ReadOnly) BeginInvoke(new Action(FocusCurrentStep));
            };

            // ===== ENTER di WOS =====
            txtWos.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;

                if (txtWos.ReadOnly) return;
                if (!txtAplikator.ReadOnly)
                {
                    NotifPopup.Warning(this, "Urutan Salah",
                        "Scan APLIKATOR dulu sebelum WOS.");
                    FocusCurrentStep();
                    return;
                }

                if (!ValidateNotEmpty(txtWos, fieldWos)) return;

                string wosInput = txtWos.Text.Trim();

                bool formatValid = wosInput.Contains(".") && TryParseSkema(wosInput, out _);
                if (!formatValid)
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();
                    NotifPopup.Warning(this, "Format WOS Tidak Sesuai",
                        "Format WOS tidak sesuai.\n\nContoh: BEJ81212112.001");
                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                if (string.IsNullOrEmpty(_state.LotMaterial))
                {
                    NotifPopup.Warning(this, "LOT MATERIAL Belum Discan",
                        "Scan LOT MATERIAL dulu sebelum WOS.");
                    txtWos.Focus();
                    return;
                }

                // ===== KUNCI WOS: satu panggilan, dua kunci =====
                // - tipartnumber → dicek ke TERMINALA / TERMINALB (PRIORITAS)
                // - tiid         → dicek ke RmiTermA  / RmiTermB  (CADANGAN)
                var results = CheckTerminal(_state.Tipartnumber, _state.Tiid, wosInput);

                if (results.Count == 0)
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();
                    NotifPopup.Warning(this, "WOS Tidak Cocok",
                        $"WOS '{wosInput}' tidak sesuai dengan material '{_state.Tipartnumber}'.\n\n" +
                        "Pastikan WOS dan LOT MATERIAL yang discan sudah benar.");
                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                // TOP 1 sudah dijamin di SQL, tapi ambil index 0 untuk aman.
                TerminalResult selected = results[0];

                _state.SisiTerminal = selected.Sisi;
                _state.PatokanCH = selected.CH;
                _state.PatokanIH = selected.IH;
                _state.PatokanCW = selected.CW;
                _state.PatokanIW = selected.IW;
                _state.PatokanTension = selected.Tension;

                // Cek duplikat pakai LOT MATERIAL asli (bukan tipartnumber)
                if (IsWosDuplicate(wosInput, _state.LotMaterial, txtAplikator.Text.Trim(), Session.Noreg))
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();
                    NotifPopup.Warning(this, "WOS Sudah Pernah Diinput",
                        $"WOS '{wosInput}' sudah pernah Anda input hari ini.\n\nSilakan scan WOS yang lain.");
                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                FlashSuccess(fieldWos);
                LockField(fieldWos, txtWos, eraseWos, true);
                _nStep = 1;
                SetNButtonState(NState.Ready);
            };

            // ===== ERASE WOS =====
            eraseWos.Click += (s, e) =>
            {
                if (!txtAplikator.ReadOnly)
                {
                    FocusCurrentStep();
                    return;
                }

                txtWos.Clear();
                LockField(fieldWos, txtWos, eraseWos, false);
                ResetBorder(fieldWos);
                ResetAfterSubmit();
                txtWos.Focus();
            };

            card.Controls.Add(row);
            return card;
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
                Font = new Font("Segoe UI Semibold", 13f),
                Radius = 12,
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

            // Cek duplikat pakai LOT MATERIAL asli
            if (IsWosDuplicate(
                    txtWos.Text.Trim(),
                    _state.LotMaterial,
                    txtAplikator.Text.Trim(),
                    Session.Noreg))
            {
                NotifPopup.Warning(this, "WOS Sudah Pernah Diinput",
                    "WOS ini ternyata baru saja diinput.\n" +
                    "Data tidak disimpan supaya tidak dobel.\n\n" +
                    "Silakan scan WOS yang lain.");

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
            dgv.Columns["colLotNo"].Width = 150;
            dgv.Columns["colSkema"].Width = 80;
            dgv.Columns["colTerminal"].Width = 120;
            dgv.Columns["colAplikator"].Width = 130;

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

            dgv.Rows.Clear();

            card.Controls.Add(dgv);
            return card;
        }

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
                ShowFriendlyError("Tanggal Tidak Bisa Diambil",
                    "Aplikasi belum bisa mengambil tanggal produksi.\nPeriksa koneksi jaringan lalu coba lagi.",
                    ex, "GetServerTanggalProduksi");
                return null;
            }
        }

        // =====================================================
        //  EXPORT PDF
        //
        //  Yang dikirim ke report: LOT MATERIAL asli (bukan tipartnumber).
        //  Report PHP nanti yang akan lookup tipartnumber via tblFifo/tblItem.
        // =====================================================
        private void ExportPdf()
        {
            string noreg = Session.Noreg;
            string aplikator = txtAplikator?.Text?.Trim();
            string terminal = _state.LotMaterial;         // ← LOT MATERIAL ASLI
            string mesin = Session.mesin;

            if (string.IsNullOrWhiteSpace(noreg) ||
                string.IsNullOrWhiteSpace(aplikator) ||
                string.IsNullOrWhiteSpace(terminal) ||
                string.IsNullOrWhiteSpace(mesin))
            {
                NotifPopup.Warning(this, "Data Belum Lengkap",
                    "Isi dulu LOT MATERIAL dan APLIKATOR sebelum menekan EXPORT.\n\n" +
                    "Kalau sudah terisi tapi pesan ini tetap muncul, silakan login ulang.");
                return;
            }

            DateTime? tglServer = GetServerTanggalProduksi();
            if (tglServer == null) return;

            string tanggal = tglServer.Value.ToString("yyyy-MM-dd");
            const string baseUrl = "http://192.168.5.4:8080/laporan-crimping-manual/controllers/export.php";

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
                ShowFriendlyError("Laporan Tidak Bisa Dibuka",
                    "Laporan belum bisa dibuka.\nPastikan komputer ini terhubung ke jaringan pabrik dan punya browser (Chrome/Edge).",
                    ex, "ExportPdf");
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

        private void FocusCurrentStep()
        {
            if (!txtTerminal.ReadOnly) { txtTerminal.Focus(); return; }
            if (!txtAplikator.ReadOnly) { txtAplikator.Focus(); return; }
            if (!txtWos.ReadOnly) { txtWos.Focus(); return; }
        }

        private static void CenterInput(Control host, TextBox tb)
        {
            tb.Dock = DockStyle.None;
            tb.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            void Relayout()
            {
                int w = Math.Max(10, host.ClientSize.Width - host.Padding.Right);
                int y = Math.Max(0, (host.ClientSize.Height - tb.Height) / 2);
                tb.SetBounds(0, y, w, tb.Height);
            }

            host.Resize += (s, e) => Relayout();
            Relayout();
        }

        private void ShowFriendlyError(string title, string message, Exception ex, string context)
        {
            string line = $"[ERROR {DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}";
            System.Diagnostics.Debug.WriteLine(line);
            System.Diagnostics.Trace.WriteLine(line);

            NotifPopup.Error(this, title,
                message + "\n\nJika masih terjadi, hubungi atasan atau teknisi.");
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
            btnN.Font = new Font("Segoe UI Semibold", 13f);
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
        private void ResetNState()
        {
            _state.ClearAll();
            _nStep = 1;
            SetNButtonState(NState.Locked);
        }

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
        //
        //  Parameter `terminal` = LOT MATERIAL asli (bukan tipartnumber).
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
                ShowFriendlyError("Riwayat Tidak Bisa Dimuat",
                    "Tabel riwayat belum bisa ditampilkan.\nPeriksa koneksi jaringan lalu coba lagi.",
                    ex, "LoadDataCrimping");
            }
        }

        private void RefreshTable()
        {
            LoadDataCrimping(
                _state.LotMaterial,              // ← LOT MATERIAL ASLI
                txtAplikator.Text.Trim(),
                Session.Noreg);
        }

        // =====================================================
        //  INSERT: simpan satu siklus N1+N2+N3
        //
        //  Yang disimpan ke kolom `terminal`: LOT MATERIAL asli.
        //  (bukan tipartnumber — tipartnumber cuma untuk cek & tampilan)
        // =====================================================
        private bool InsertLhhpWH()
        {
            string terminal = _state.LotMaterial ?? "";    // ← LOT MATERIAL ASLI
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
                        NotifPopup.Error(this, "Data Belum Tersimpan",
                            "Data belum berhasil disimpan.\nSilakan coba lagi.");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Data Gagal Disimpan",
                    "Data belum berhasil disimpan.\nPeriksa koneksi jaringan lalu coba lagi.",
                    ex, "InsertLhhpWH");
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
        //  INNER CLASS: LotInfo
        // =====================================================
        private class LotInfo
        {
            public string Tipartnumber { get; set; }
            public string Tiid { get; set; }
        }

        // =====================================================
        //  QUERY: Lotno → tipartnumber + tiid
        // =====================================================
        private LotInfo GetLotInfoByLotno(string tlotid)
        {
            if (string.IsNullOrWhiteSpace(tlotid)) return null;

            const string sql = @"
SELECT a.tiid, b.tipartnumber
FROM dbInventory2..tblFifo a
LEFT JOIN dbInventory2..tblItem b ON b.tiid = a.tiid
WHERE a.tlotid = @tlotid;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@tlotid", tlotid);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;

                        return new LotInfo
                        {
                            Tipartnumber = reader["tipartnumber"] == DBNull.Value
                                ? null
                                : reader["tipartnumber"].ToString().Trim(),
                            Tiid = reader["tiid"] == DBNull.Value
                                ? null
                                : reader["tiid"].ToString().Trim()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError("LOT MATERIAL Tidak Bisa Dicek",
                    "Aplikasi belum bisa memeriksa LOT MATERIAL.\nPeriksa koneksi jaringan lalu scan ulang.",
                    ex, "GetLotInfoByLotno");
                return null;
            }
        }

        // =====================================================
        //  QUERY: cari patokan di TrRPHMesin (TOP 1)
        //
        //  Menerima DUA kunci:
        //    - tipartnumber → dicek ke TERMINALA / TERMINALB  (PRIORITAS)
        //    - tiid         → dicek ke RmiTermA  / RmiTermB   (CADANGAN)
        // =====================================================
        private List<TerminalResult> CheckTerminal(string tipartnumber, string tiid, string wos)
        {
            var list = new List<TerminalResult>();

            if (string.IsNullOrWhiteSpace(wos)) return list;
            if (string.IsNullOrWhiteSpace(tipartnumber) && string.IsNullOrWhiteSpace(tiid)) return list;

            const string sql = @"
SELECT TOP 1 
       KodeMesin, TERMINALA, TERMINALB, RmiTermA, RmiTermB,
       cha, iha, cwa, iwa, T1,
       chb, ihb, cwb, iwb, T2
FROM [KUJNAGN].[dbo].[TrRPHMesin]
WHERE (
        (@tipartnumber IS NOT NULL AND (TERMINALA = @tipartnumber OR TERMINALB = @tipartnumber))
     OR (@tiid IS NOT NULL AND (RmiTermA = @tiid OR RmiTermB = @tiid))
      )
  AND barcode = @wos
ORDER BY 
    CASE 
        WHEN @tipartnumber IS NOT NULL 
             AND (TERMINALA = @tipartnumber OR TERMINALB = @tipartnumber) 
        THEN 0 
        ELSE 1 
    END,
    KodeRPHMeisn DESC;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@tipartnumber",
                        string.IsNullOrWhiteSpace(tipartnumber) ? (object)DBNull.Value : tipartnumber);
                    cmd.Parameters.AddWithValue("@tiid",
                        string.IsNullOrWhiteSpace(tiid) ? (object)DBNull.Value : tiid);
                    cmd.Parameters.AddWithValue("@wos", wos);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string termA = reader["TERMINALA"] as string;
                            string termB = reader["TERMINALB"] as string;
                            string rmiA = reader["RmiTermA"] as string;
                            string rmiB = reader["RmiTermB"] as string;

                            bool matchA = (!string.IsNullOrEmpty(tipartnumber) && termA == tipartnumber)
                                       || (!string.IsNullOrEmpty(tiid) && rmiA == tiid);

                            bool matchB = (!string.IsNullOrEmpty(tipartnumber) && termB == tipartnumber)
                                       || (!string.IsNullOrEmpty(tiid) && rmiB == tiid);

                            if (matchA)
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

                            if (matchB)
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
                ShowFriendlyError("WOS Tidak Bisa Dicek",
                    "Aplikasi belum bisa memeriksa WOS.\nPeriksa koneksi jaringan lalu scan ulang.",
                    ex, "CheckTerminal");
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
                ShowFriendlyError("Aplikator Tidak Bisa Dicek",
                    "Aplikasi belum bisa memeriksa aplikator.\nPeriksa koneksi jaringan lalu scan ulang.",
                    ex, "CheckAplikator");
                return false;
            }
        }

        // =====================================================
        //  QUERY: cek WOS duplikat
        //
        //  Parameter `terminal` = LOT MATERIAL asli.
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
                ShowFriendlyError("WOS Tidak Bisa Dicek",
                    "Aplikasi belum bisa memeriksa apakah WOS ini sudah pernah diinput.\nPeriksa koneksi jaringan lalu scan ulang.",
                    ex, "IsWosDuplicate");

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