using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProjectCrimping
{
    /// <summary>
    /// Dashboard utama operator crimping ("Shopfloor Crimping").
    ///
    /// ================================================================
    ///  RINGKASAN ALUR KERJA (urutan operator)
    /// ================================================================
    ///  1. Scan LOTNO (bar di atas)        → cari tiproductname di inventory,
    ///                                       lalu cari patokan (standar ukuran)
    ///                                       Terminal A/B di TrRPHMesin.
    ///                                       Kalau terminal ada di sisi A dan B,
    ///                                       operator memilih sisi lewat FormPilihSisi.
    ///  2. Scan APLIKATOR                  → divalidasi ke tabel MSAplikator,
    ///                                       lalu tabel riwayat hari ini dimuat.
    ///  3. Scan WOS (format XXXX.001)      → validasi format; tombol N1 aktif.
    ///  4. Tekan N1 → N2 → N3 (1 tombol)   → tiap tombol membuka form input ukuran.
    ///                                       Setelah N3 + konfirmasi NG, data di-INSERT
    ///                                       ke Tr_LhhpWH_copy dan tombol kembali ke N1.
    ///  5. Fokus kembali ke WOS            → operator bisa scan WOS berikutnya tanpa
    ///                                       scan ulang Lotno/Aplikator.
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
    ///  - "Patokan" = nilai standar CH/IH/CW/IW/Tension dari TrRPHMesin.
    ///    Patokan hanya dihapus saat Lotno/Terminal di-reset, TIDAK saat
    ///    erase Aplikator/WOS atau setelah simpan data (lihat ResetNState
    ///    vs ResetNStateOnly).
    ///  - "Hari produksi" berjalan jam 06:00 s/d 06:00 hari berikutnya
    ///    (lihat query di LoadDataCrimping).
    ///  - Tabel yang dipakai masih Tr_LhhpWH_copy (tabel salinan). Kalau
    ///    sudah go-live ke tabel asli, ganti nama tabel di LoadDataCrimping
    ///    dan InsertLhhpWH.
    ///  - Warna & font bersumber dari AppThemeLogin supaya konsisten
    ///    dengan halaman Login.
    /// </summary>
    public partial class Dashboard : Form
    {
        // ================================================================
        //  PALET WARNA
        //  Sebagian besar diambil dari AppThemeLogin agar seragam dengan Login.
        //  Ubah warna global di AppThemeLogin, bukan di sini.
        // ================================================================
        private static readonly Color Navy = AppThemeLogin.ColorNavy;
        private static readonly Color Accent = AppThemeLogin.ColorAccent;
        private static readonly Color AccentSoft = Color.FromArgb(253, 243, 235);   // oranye sangat muda (latar tombol erase / baris terpilih)
        private static readonly Color AccentLine = AppThemeLogin.ColorAccentDisabled;
        private static readonly Color TextDark = AppThemeLogin.ColorTextDark;
        private static readonly Color TextSub = AppThemeLogin.ColorTextSub;
        private static readonly Color FieldBg = AppThemeLogin.ColorFieldBg;
        private static readonly Color FieldBorder = AppThemeLogin.ColorFieldBorder;
        private static readonly Color FieldIcon = AppThemeLogin.ColorFieldIcon;
        private static readonly Color ErrorColor = AppThemeLogin.ColorError;
        private static readonly Color SuccessColor = Color.FromArgb(46, 204, 113);  // hijau: kedip border saat input valid
        private static readonly Color PageBg = Color.White;

        // Warna field yang sedang TERKUNCI (ReadOnly)
        private static readonly Color FieldLockedBg = Color.FromArgb(238, 240, 243);

        // Warna tombol N saat TERKUNCI (belum boleh ditekan)
        private static readonly Color NLockedBg = Color.FromArgb(237, 239, 242);
        private static readonly Color NLockedBorder = Color.FromArgb(221, 225, 230);
        private static readonly Color NLockedText = Color.FromArgb(168, 176, 186);

        // ================================================================
        //  GLYPH IKON (font Segoe MDL2 Assets)
        //  Kode unicode ikon. Kalau ikon tidak muncul, cek font tersedia
        //  atau gunakan fallback di AppThemeLogin.ApplyIcon.
        // ================================================================
        private const string IcAdd = "\uE710";
        private const string IcClock = "\uE121";
        private const string IcExport = "\uE8A5";
        private const string IcPower = "\uE7E8";
        private const string IcErase = "\uE75C";      // ikon penghapus (erase field)
        private const string IcClose = "\uE711";      // ikon X (reset semua, di bar Lotno)
        private const string IcTerminal = "\uE7F4";
        private const string IcTag = "\uE8EC";
        private const string IcSearch = "\uE721";

        // ================================================================
        //  KONTROL UI
        // ================================================================
        private TextBox txtLotno, txtTerminal, txtAplikator, txtWos;

        /// <summary>
        /// Satu tombol dinamis untuk N1 → N2 → N3.
        /// Label & aksinya ditentukan oleh <see cref="_nStep"/>.
        /// Status aktif/terkunci disimpan di properti Tag ("ready" / "locked").
        /// </summary>
        private RoundedButton btnN;

        /// <summary>Langkah pengukuran saat ini: 1 = N1, 2 = N2, 3 = N3.</summary>
        private int _nStep = 1;

        private DataGridView dgv;   // tabel riwayat

        // Panel pembungkus tiap field (untuk border/warna) & tombol erase-nya.
        // Disimpan sebagai field class karena dipakai lintas event handler.
        private RoundedPanel fieldLotno, fieldTerminal, fieldAplikator, fieldWos;
        private RoundedButton eraseLotno, eraseTerminal, eraseAplikator, eraseWos;

        // ================================================================
        //  STATE & DEPENDENCY
        // ================================================================

        /// <summary>Menampung hasil input N1/N2/N3 dan patokan selama satu siklus WOS.</summary>
        private CrimpingState _state = new CrimpingState();

        /// <summary>Helper koneksi database (SQL Server).</summary>
        private readonly DatabaseHelper _db = new DatabaseHelper(DbProvider.SqlServer);

        /// <summary>Referensi ke form Login, dipakai untuk kembali ke Login saat logout.</summary>
        private readonly Login _login;

        /// <summary>
        /// True kalau form ditutup karena LOGOUT.
        /// Kalau false (user menutup jendela), seluruh aplikasi ikut keluar (lihat OnFormClosed).
        /// </summary>
        private bool _isLoggingOut;

        public Dashboard(Login login)
        {
            _login = login;
            InitializeComponent();

            // Cegah jendela dikecilkan sampai layout rusak.
            MinimumSize = new Size(1100, 700);

            DoubleBuffered = true;
            BuildUI();
        }

        /// <summary>
        /// Flag WS_EX_COMPOSITED (0x02000000): semua child control digambar
        /// double-buffered untuk mengurangi flicker saat resize.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        /// <summary>
        /// Tutup Dashboard = keluar aplikasi, KECUALI kalau karena logout
        /// (dalam kasus itu Login ditampilkan kembali lewat login.ShowAgain()).
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (!_isLoggingOut)
                Application.Exit();
        }

        /// <summary>
        /// Merakit seluruh UI. Urutan Controls.Add penting karena Dock:
        /// konten (Fill) ditambahkan dulu, baru sidebar (Left).
        /// </summary>
        private void BuildUI()
        {
            SuspendLayout();
            Controls.Add(BuildContent());
            Controls.Add(BuildSidebar());
            ActiveControl = txtLotno;   // fokus awal ke Lotno (siap scan)
            ResumeLayout(true);
        }

        // =====================================================
        //  INNER CLASS: CrimpingState
        //  Penampung data sementara satu siklus input.
        //  Semua nullable: null = belum diisi (dikirim sebagai DBNull saat INSERT).
        // =====================================================
        private class CrimpingState
        {
            // ---- Hasil ukur N1 (+ Tension, hanya diukur di N1) ----
            public decimal? N1_CH { get; set; }
            public decimal? N1_IH { get; set; }
            public decimal? N1_CW { get; set; }
            public decimal? N1_IW { get; set; }
            public decimal? N1_Tension { get; set; }

            // ---- Hasil ukur N2 ----
            public decimal? N2_CH { get; set; }
            public decimal? N2_IH { get; set; }
            public decimal? N2_CW { get; set; }
            public decimal? N2_IW { get; set; }

            // ---- Hasil ukur N3 (+ jumlah NG) ----
            public decimal? N3_CH { get; set; }
            public decimal? N3_IH { get; set; }
            public decimal? N3_CW { get; set; }
            public decimal? N3_IW { get; set; }
            public int? N3_NG { get; set; }

            // ---- PATOKAN (standar) dari TrRPHMesin, dipakai form N1/N2/N3
            //      sebagai pembanding batas ukuran ----
            public string Tiproductname { get; set; }
            public string SisiTerminal { get; set; }      // "A" atau "B"
            public decimal? PatokanCH { get; set; }
            public decimal? PatokanIH { get; set; }
            public decimal? PatokanCW { get; set; }
            public decimal? PatokanIW { get; set; }
            public decimal? PatokanTension { get; set; }

            public void ClearN1()
            {
                N1_CH = null; N1_IH = null; N1_CW = null; N1_IW = null; N1_Tension = null;
            }
            public void ClearN2()
            {
                N2_CH = null; N2_IH = null; N2_CW = null; N2_IW = null;
            }
            public void ClearN3()
            {
                N3_CH = null; N3_IH = null; N3_CW = null; N3_IW = null; N3_NG = null;
            }
            public void ClearPatokan()
            {
                Tiproductname = null;
                SisiTerminal = null;
                PatokanCH = null;
                PatokanIH = null;
                PatokanCW = null;
                PatokanIW = null;
                PatokanTension = null;
            }

            /// <summary>Hapus semua: hasil N1-N3 DAN patokan.</summary>
            public void ClearAll()
            {
                ClearN1(); ClearN2(); ClearN3();
                ClearPatokan();
            }
        }

        // =====================================================
        //  SIDEBAR (kiri)
        //  Isi: profil operator, accordion Shift & Mesin,
        //  menu (Request Terminal, Downtime, Export), tombol Logout.
        // =====================================================
        private Control BuildSidebar()
        {
            var side = new SidebarPanel { Dock = DockStyle.Left, Width = 270 };

            // ---- Profil: avatar, nama, noreg ----
            var profile = new TransparentPanel { Dock = DockStyle.Top, Height = 215 };

            var avatar = new AvatarCircle
            {
                Initials = "DA",   // TODO: bisa diganti inisial dari Session.Nama
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                Size = new Size(92, 92),
                Location = new Point((270 - 92) / 2, 30),   // dipusatkan di lebar sidebar (270)
                FillColor = Navy,
                RingColor = Accent,
                InitialsColor = Accent
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

            // "Pill" berisi nomor registrasi operator
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
            profile.Controls.Add(lblName);
            profile.Controls.Add(pill);

            // ---- Tombol LOGOUT (menempel di bawah) ----
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
                // Minta konfirmasi dulu
                using (var f = new FormConfirmLogout())
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                }

                Session.Clear();
                _isLoggingOut = true;   // supaya OnFormClosed TIDAK menutup aplikasi

                var login = _login;
                Close();                // tutup Dashboard
                login.ShowAgain();      // tampilkan Login lagi (reset form di dalamnya)
            };
            bottom.Controls.Add(btnLogout);

            // ---- Menu tengah ----
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

            // Loop MUNDUR karena DockStyle.Top: control yang ditambah terakhir
            // tampil paling atas. Mundur menjaga urutan visual = urutan list.
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

            // ---- Accordion info Shift & Mesin (default tertutup) ----
            var acc = new InfoAccordion { Title = "SHIFT & MESIN", Dock = DockStyle.Top };
            acc.AddItem(IcClock, "SHIFT", Session.ShiftDisplay ?? "-");
            acc.AddItem(AppThemeLogin.IconGear, "MESIN", Session.MesinDisplay ?? "-");
            acc.SetExpanded(false);

            // Pembungkus: tingginya mengikuti accordion saat dibuka/ditutup
            var accWrap = new TransparentPanel { Dock = DockStyle.Top, Padding = new Padding(16, 6, 16, 12) };
            accWrap.Height = acc.Height + accWrap.Padding.Vertical;
            acc.SizeChanged += (s, e) => accWrap.Height = acc.Height + accWrap.Padding.Vertical;
            accWrap.Controls.Add(acc);

            // Urutan Add menentukan susunan Dock (Fill dulu, Top terakhir = paling atas)
            side.Controls.Add(menu);
            side.Controls.Add(bottom);
            side.Controls.Add(accWrap);
            side.Controls.Add(profile);
            return side;
        }

        /// <summary>Pabrik tombol menu sidebar (gaya transparan di atas latar navy).</summary>
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
        //  KONTEN UTAMA
        //  TableLayoutPanel 5 baris. Ubah tinggi baris di sini kalau
        //  ingin mengatur ruang tabel (makin kecil baris atas → tabel makin besar).
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
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // 0: header
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));      // 1: bar lotno
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));     // 2: kotak input (3 baris)
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));      // 3: tombol N
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));      // 4: tabel (sisa ruang)

            main.Controls.Add(BuildHeader(), 0, 0);
            main.Controls.Add(BuildLotnoBar(), 0, 1);
            main.Controls.Add(BuildInputCard(), 0, 2);
            main.Controls.Add(BuildNButtons(), 0, 3);
            main.Controls.Add(BuildTableCard(), 0, 4);
            return main;
        }

        /// <summary>Judul halaman + subjudul + garis aksen oranye.</summary>
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
        //  BAR LOTNO (di luar kotak input)
        //
        //  Kolom: [Label 90px] [Field 340px] [Tombol X 52px] [sisa kosong]
        //
        //  Alur Enter di Lotno:
        //    1. Cari tiproductname dari inventory (GetTiproductnameByLotno)
        //    2. Cari patokan Terminal A/B (CheckTerminal)
        //    3. Kalau hasil > 1 (terminal ada di sisi A & B) → operator pilih sisi
        //    4. Simpan patokan ke _state, kunci Lotno & Terminal,
        //       buka kunci Aplikator, fokus ke Aplikator.
        //
        //  Tombol X = RESET SEMUA (Lotno, Terminal, Aplikator, WOS, tabel, state N).
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
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));    // label
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));   // field (lebar tetap)
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));    // tombol X
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));    // ruang kosong
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

            // Panel berborder membungkus TextBox (TextBox native tidak punya sudut rounded)
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

            // Border oranye saat field fokus, kembali netral saat blur
            // (hanya kalau field tidak sedang terkunci/ReadOnly)
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

            // ===== Tombol X: reset semua =====
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

            // ===== ENTER di Lotno (scanner barcode biasanya mengirim Enter di akhir) =====
            txtLotno.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;   // cegah bunyi "ding" Windows

                string lotno = txtLotno.Text.Trim();
                if (string.IsNullOrEmpty(lotno))
                {
                    NotifPopup.Warning(this, "Lotno Kosong", "Silakan scan Lotno dulu.");
                    return;
                }

                // Langkah 1: Lotno → nama produk terminal (tiproductname)
                string tiproductname = GetTiproductnameByLotno(lotno);
                if (string.IsNullOrEmpty(tiproductname))
                {
                    NotifPopup.Warning(this, "Lotno Tidak Ditemukan",
                        $"Lotno '{lotno}' tidak terdaftar di inventory.");
                    txtLotno.Focus();
                    return;
                }

                // Langkah 2: nama produk → patokan di Terminal A / B
                var results = CheckTerminal(tiproductname);
                if (results.Count == 0)
                {
                    NotifPopup.Warning(this, "Terminal Tidak Ditemukan",
                        $"tiproductname '{tiproductname}' tidak terdaftar di Terminal A/B.\n" +
                        "Tidak bisa lanjut.");
                    txtLotno.Focus();
                    return;
                }

                // Lotno valid: tampilkan di field Terminal dan kunci keduanya
                _state.Tiproductname = tiproductname;
                txtTerminal.Text = tiproductname;
                LockField(fieldLotno, txtLotno, eraseLotno, true);
                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);

                // Langkah 3: tentukan sisi (A/B)
                TerminalResult selected = null;
                if (results.Count > 1)
                {
                    // Terminal ada di lebih dari satu sisi/baris → operator memilih
                    using (var f = new FormPilihSisi(results))
                    {
                        if (f.ShowDialog(this) != DialogResult.OK)
                        {
                            // Dibatalkan: kembalikan ke kondisi awal Lotno
                            txtTerminal.Clear();
                            txtLotno.Clear();
                            _state.ClearPatokan();
                            LockField(fieldLotno, txtLotno, eraseLotno, false);
                            LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
                            txtLotno.Focus();
                            return;
                        }
                        selected = f.SelectedResult;
                    }
                }
                else
                {
                    selected = results[0];
                }

                // Langkah 4: simpan patokan untuk dipakai form N1/N2/N3
                _state.SisiTerminal = selected.Sisi;
                _state.PatokanCH = selected.CH;
                _state.PatokanIH = selected.IH;
                _state.PatokanCW = selected.CW;
                _state.PatokanIW = selected.IW;
                _state.PatokanTension = selected.Tension;

                FlashSuccess(fieldLotno);
                // Lanjut ke Aplikator
                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                txtAplikator.Focus();
            };

            // ===== X: reset Lotno + Terminal + Aplikator + WOS + tabel + state N =====
            eraseLotno.Click += (s, e) =>
            {
                txtLotno.Clear();
                txtTerminal.Clear();
                txtAplikator.Clear();
                txtWos.Clear();

                // Lotno terbuka; sisanya terkunci sampai Lotno discan lagi
                LockField(fieldLotno, txtLotno, eraseLotno, false);
                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, true);

                ResetBorder(fieldLotno, fieldTerminal, fieldAplikator, fieldWos);
                ResetNState();   // TOTAL: patokan ikut terhapus
                ClearTable();

                txtLotno.Focus();
            };

            return bar;
        }

        // =====================================================
        //  KOTAK INPUT: TERMINAL, APLIKATOR, WOS
        //
        //  Status kunci tiap field (ReadOnly) mengatur urutan scan:
        //    Terminal  : selalu terkunci (diisi otomatis dari Lotno)
        //    Aplikator : terbuka setelah Lotno valid
        //    WOS       : terbuka setelah Aplikator valid
        //  Tombol N1 aktif setelah WOS valid.
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

            // Grid: [label 120] [field fleksibel] [tombol erase 58] x 3 baris sama tinggi
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

            // Buat 3 field. out-parameter mengisi field class (txtX, fieldX, eraseX).
            AddField(grid, 0, "TERMINAL", IcTerminal, "🖥",
                     out txtTerminal, out fieldTerminal, out eraseTerminal);
            AddField(grid, 1, "APLIKATOR", AppThemeLogin.IconGear, AppThemeLogin.FallbackGear,
                     out txtAplikator, out fieldAplikator, out eraseAplikator);
            AddField(grid, 2, "WOS", IcTag, "🏷",
                     out txtWos, out fieldWos, out eraseWos);

            // Kondisi awal: semua terkunci sampai Lotno discan
            LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
            LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
            LockField(fieldWos, txtWos, eraseWos, true);

            // ===== ENTER di Aplikator → cek ke MSAplikator =====
            txtAplikator.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtAplikator, fieldAplikator)) return;

                string kodeAplikator = txtAplikator.Text.Trim();

                if (!CheckAplikator(kodeAplikator))
                {
                    // Tidak terdaftar: border merah, tetap di field ini
                    fieldAplikator.BorderColor = ErrorColor;
                    fieldAplikator.Invalidate();

                    NotifPopup.Warning(this, "Aplikator Tidak Ditemukan",
                        $"Kode Aplikator '{kodeAplikator}' tidak terdaftar di MSAplikator.");

                    txtAplikator.Focus();
                    txtAplikator.SelectAll();
                    return;
                }

                // Valid: kunci Aplikator, buka WOS
                FlashSuccess(fieldAplikator);

                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, false);
                txtWos.Focus();

                // Muat riwayat hari ini untuk kombinasi terminal + aplikator + operator ini
                LoadDataCrimping(
                    txtTerminal.Text.Trim(),
                    kodeAplikator,
                    Session.Noreg);
            };

            // ===== ENTER di WOS → validasi format & skema =====
            // Format WOS: <lotno>.<skema 3 digit>, contoh BEJ81212112.001
            txtWos.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtWos, fieldWos)) return;

                string wosInput = txtWos.Text.Trim();

                // Validasi 1: harus ada titik pemisah
                if (!wosInput.Contains("."))
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();

                    NotifPopup.Warning(this, "Format WOS Salah",
                        "WOS harus mengandung titik (contoh: BEJ81212112.001).");

                    txtWos.Focus();
                    return;
                }

                // Validasi 2: skema setelah titik = tepat 3 digit angka
                if (!TryParseSkema(wosInput, out _))
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();

                    NotifPopup.Warning(this, "Format Skema Salah",
                        "Skema setelah titik harus 3 digit angka.\n" +
                        "Contoh: BEJ81212112.001");

                    txtWos.Focus();
                    txtWos.SelectAll();
                    return;
                }

                // Validasi 3: patokan harus sudah ada (Lotno sudah di-scan)
                if (string.IsNullOrEmpty(_state.SisiTerminal))
                {
                    NotifPopup.Warning(this, "Sisi Terminal Belum Ditentukan",
                        "Scan Lotno dulu untuk menentukan Terminal A/B.");
                    txtWos.Focus();
                    return;
                }

                // Semua valid: kunci WOS dan aktifkan tombol N1
                FlashSuccess(fieldWos);
                LockField(fieldWos, txtWos, eraseWos, true);
                _nStep = 1;
                SetNButtonState(NState.Ready);
            };

            // ===== ERASE TERMINAL → reset Terminal, Aplikator, WOS (patokan ikut terhapus) =====
            eraseTerminal.Click += (s, e) =>
            {
                txtTerminal.Clear();
                txtAplikator.Clear();
                txtWos.Clear();

                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, true);

                ResetBorder(fieldTerminal, fieldAplikator, fieldWos);
                ResetNState();   // TOTAL: patokan ikut hilang → harus scan Lotno lagi
                ClearTable();

                txtLotno.Focus();
            };

            // ===== ERASE APLIKATOR → reset Aplikator & WOS (patokan TETAP) =====
            eraseAplikator.Click += (s, e) =>
            {
                txtAplikator.Clear();
                txtWos.Clear();
                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                LockField(fieldWos, txtWos, eraseWos, true);
                ResetBorder(fieldAplikator, fieldWos);

                ResetNStateOnly();   // patokan jangan ikut terhapus
                ClearTable();

                txtAplikator.Focus();
            };

            // ===== ERASE WOS → reset WOS saja (patokan TETAP) =====
            eraseWos.Click += (s, e) =>
            {
                txtWos.Clear();
                LockField(fieldWos, txtWos, eraseWos, false);
                ResetBorder(fieldWos);

                ResetNStateOnly();   // patokan jangan ikut terhapus

                txtWos.Focus();
            };

            card.Controls.Add(grid);
            return card;
        }

        /// <summary>
        /// Membuat satu baris field (label + input berborder + tombol erase)
        /// dan menaruhnya di <paramref name="grid"/> pada baris <paramref name="row"/>.
        /// Referensi kontrol dikembalikan lewat out-parameter.
        /// </summary>
        private void AddField(TableLayoutPanel grid, int row, string label,
                              string glyph, string fallback,
                              out TextBox tb, out RoundedPanel field, out RoundedButton erase)
        {
            // Kolom 0: label
            grid.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI Semibold", 9.5f),
                BackColor = Color.Transparent
            }, 0, row);

            // Kolom 1: panel border + TextBox + ikon
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

            // Highlight border saat fokus (kecuali ReadOnly)
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

            // Kolom 2: tombol erase
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
        //  TOMBOL DINAMIS N1 → N2 → N3 → (kembali) N1
        //
        //  State machine sederhana berdasar _nStep:
        //
        //    _nStep=1 : buka FormN1  → simpan ke _state → _nStep=2
        //    _nStep=2 : buka FormN2  → simpan ke _state → _nStep=3
        //    _nStep=3 : SubmitN3()   → form N3 + konfirmasi NG + INSERT DB
        //                              → sukses: reset ke N1 (terkunci)
        //                              → gagal : tetap di N3 (bisa coba lagi)
        //
        //  Tombol hanya bereaksi jika Tag == "ready" (lihat SetNButtonState).
        //  Kalau user menutup form dengan Cancel, _nStep TIDAK maju.
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
            SetNButtonState(NState.Locked);   // awal: terkunci sampai WOS valid

            btnN.Click += (s, e) =>
            {
                // Abaikan klik kalau tombol sedang terkunci
                if ((btnN.Tag as string) != "ready") return;

                switch (_nStep)
                {
                    case 1:
                        // Form N1 menerima patokan (lengkap dengan Tension) untuk validasi ukuran
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
                        SetNButtonState(NState.Ready);   // label berubah jadi "N2"
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
                        SetNButtonState(NState.Ready);   // label berubah jadi "N3"
                        break;

                    case 3:
                        SubmitN3();   // langkah terakhir + simpan ke DB
                        break;
                }
            };

            return btnN;
        }

        /// <summary>
        /// Langkah terakhir (N3): input ukuran N3 → konfirmasi NG → INSERT ke DB.
        ///
        /// Titik keluar:
        ///  - User Cancel di FormN3        : return, tetap di N3.
        ///  - User Cancel di konfirmasi NG : data N3 dibersihkan, tetap di N3.
        ///  - INSERT gagal                 : tetap di N3 supaya bisa dicoba lagi
        ///                                   (data N1/N2/N3 masih tersimpan di _state).
        ///  - INSERT sukses                : WOS dikosongkan & dibuka untuk scan
        ///                                   berikutnya, tombol kembali ke N1,
        ///                                   tabel dimuat ulang.
        /// </summary>
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

            // Tanya jumlah NG (opsional). Kalau user batal, buang data N3.
            using (var fng = new FormConfirmNG())
            {
                if (fng.ShowDialog(this) != DialogResult.OK)
                {
                    _state.ClearN3();
                    return;
                }

                // HasNG=false → simpan NULL (bukan 0)
                _state.N3_NG = fng.HasNG ? (int?)fng.NGValue : null;
            }

            if (!InsertLhhpWH())
            {
                SetNButtonState(NState.Ready);   // tetap di N3, bisa coba lagi
                return;
            }

            // ---- Sukses: siapkan siklus berikutnya ----
            txtWos.Clear();
            LockField(fieldWos, txtWos, eraseWos, false);   // buka WOS lagi
            ResetBorder(fieldWos);

            ResetNStateOnly();   // _nStep kembali ke 1, tombol terkunci; patokan TETAP

            // BeginInvoke: tunggu dialog benar-benar tertutup sebelum set fokus,
            // kalau tidak fokus bisa "hilang".
            BeginInvoke(new Action(() =>
            {
                txtWos.Focus();
                txtWos.SelectAll();
            }));

            RefreshTable();   // tampilkan data yang baru disimpan
        }

        // =====================================================
        //  TABEL RIWAYAT
        //
        //  Menampilkan data hari produksi ini untuk kombinasi
        //  terminal + aplikator + operator. Urutan terbaru di atas.
        //
        //  Layout kolom bersifat responsif:
        //   - AutoSizeColumnsMode.Fill + FillWeight = lebar awal → kolom ikut
        //     melebar di layar lebar.
        //   - MinimumWidth menjaga kolom tidak terlalu sempit → di layar kecil
        //     muncul scroll horizontal.
        //
        //  Menambah kolom baru: (1) tambah dgv.Columns.Add, (2) atur lebar,
        //  (3) tambahkan nilainya di dgv.Rows.Add pada LoadDataCrimping
        //  DENGAN URUTAN YANG SAMA.
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
                EnableHeadersVisualStyles = false,   // wajib false agar warna header kustom dipakai
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 46,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowTemplate = { Height = 34 },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,   // diganti Fill di bagian bawah method
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Both
            };

            // ---- Gaya header ----
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Navy;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;   // agar "\n" di judul kolom jadi 2 baris
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Navy;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;

            // ---- Gaya sel ----
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.ForeColor = TextDark;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            dgv.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            dgv.DefaultCellStyle.SelectionBackColor = AccentSoft;
            dgv.DefaultCellStyle.SelectionForeColor = TextDark;

            // ---- Definisi kolom (URUTAN = urutan argumen di dgv.Rows.Add) ----
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

            // Lebar awal kolom teks
            dgv.Columns["colNo"].Width = 45;
            dgv.Columns["colLotNo"].Width = 130;
            dgv.Columns["colSkema"].Width = 80;
            dgv.Columns["colTerminal"].Width = 100;
            dgv.Columns["colAplikator"].Width = 110;

            // Kolom teks: rata kiri, lebar minimum = lebar awal
            foreach (var name in new[] { "colLotNo", "colSkema", "colTerminal", "colAplikator" })
            {
                var col = dgv.Columns[name];
                col.MinimumWidth = col.Width;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }

            // Kolom No: rata tengah, font Consolas (angka rata)
            var colNo = dgv.Columns["colNo"];
            colNo.MinimumWidth = 45;
            colNo.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colNo.DefaultCellStyle.Font = new Font("Consolas", 9f);
            colNo.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Kolom angka (hasil ukur): rata tengah, font monospace Consolas
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

            // Tension sedikit lebih lebar dari kolom angka lain
            dgv.Columns["colTension"].Width = 80;
            dgv.Columns["colTension"].MinimumWidth = 80;

            // ===== Responsif: kolom ikut melebar di layar lebar,
            //       tetap scroll horizontal kalau layar sempit (karena MinimumWidth) =====
            // PENTING: FillWeight harus diset SETELAH semua Width final di atas.
            foreach (DataGridViewColumn c in dgv.Columns)
                c.FillWeight = c.Width;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            dgv.Rows.Clear();

            card.Controls.Add(dgv);
            return card;
        }

        // =====================================================
        //  EXPORT PDF
        //
        //  Tidak membuat PDF di aplikasi ini; hanya membuka URL laporan
        //  di browser default. PDF digenerate oleh aplikasi web PHP
        //  (laporan-w-h/controllers/export.php).
        //
        //  Syarat: web server lokal (localhost) berjalan. Kalau server
        //  pindah/ganti, ubah konstanta baseUrl di bawah.
        // =====================================================
        private void ExportPdf()
        {
            string noreg = Session.Noreg;
            string aplikator = txtAplikator?.Text?.Trim();
            string terminal = txtTerminal?.Text?.Trim();
            string mesin = Session.mesin;

            // Semua parameter wajib terisi
            if (string.IsNullOrWhiteSpace(noreg) ||
                string.IsNullOrWhiteSpace(aplikator) ||
                string.IsNullOrWhiteSpace(terminal) ||
                string.IsNullOrWhiteSpace(mesin))
            {
                NotifPopup.Warning(this, "Data Belum Lengkap",
                    "Pastikan Terminal dan Aplikator sudah diisi, dan sesi login (Noreg & Mesin) valid.");
                return;
            }

            string tanggal = DateTime.Now.ToString("yyyy-MM-dd");
            const string baseUrl = "http://localhost/laporan-w-h/controllers/export.php";

            // EscapeDataString: aman untuk karakter khusus di URL
            string url =
                $"{baseUrl}?action=export_pdf" +
                $"&noreg={Uri.EscapeDataString(noreg)}" +
                $"&tanggal={Uri.EscapeDataString(tanggal)}" +
                $"&aplikator={Uri.EscapeDataString(aplikator)}" +
                $"&terminal={Uri.EscapeDataString(terminal)}" +
                $"&mesin={Uri.EscapeDataString(mesin)}";

            try
            {
                // UseShellExecute = true → buka URL dengan browser default
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

        /// <summary>
        /// Mengunci / membuka sebuah field.
        /// Terkunci  = TextBox ReadOnly + warna abu (belum boleh diisi).
        /// Terbuka   = TextBox bisa diketik + warna normal.
        /// Tombol erase selalu aktif supaya operator selalu bisa mereset.
        /// </summary>
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

        /// <summary>
        /// Cek field tidak kosong. Kalau kosong: border merah selama 1,2 detik
        /// lalu otomatis kembali normal. Return false kalau kosong.
        /// </summary>
        private bool ValidateNotEmpty(TextBox tb, RoundedPanel field)
        {
            if (!string.IsNullOrWhiteSpace(tb.Text)) return true;

            field.BorderColor = ErrorColor;
            field.Invalidate();

            // Timer WinForms (System.Windows.Forms.Timer) berjalan di UI thread,
            // aman menyentuh kontrol. Wajib di-Dispose sendiri setelah selesai.
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

        /// <summary>Kedipkan border hijau 0,8 detik sebagai tanda input valid.</summary>
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

        /// <summary>Kembalikan border field ke warna normal (menghapus merah/hijau sisa).</summary>
        private void ResetBorder(params RoundedPanel[] fields)
        {
            foreach (var f in fields)
            {
                f.BorderColor = f.Focused ? Accent : FieldBorder;
                f.Invalidate();
            }
        }

        // =====================================================
        //  HELPER: State tombol N (1 tombol dinamis)
        // =====================================================

        /// <summary>Locked = abu-abu, tidak merespon klik. Ready = oranye, bisa diklik.</summary>
        private enum NState { Locked, Ready }

        /// <summary>
        /// Mengatur tampilan + status tombol N.
        /// Label otomatis "N" + _nStep. Status disimpan di btnN.Tag
        /// dan dicek di handler Click (bukan lewat Enabled, supaya
        /// tampilan terkunci tetap terlihat jelas dan tidak "greyed out" bawaan Windows).
        /// </summary>
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

        // ===== Reset TOTAL: hasil N1-N3 DAN patokan terhapus =====
        // Dipakai saat Lotno/Terminal di-reset (harus scan Lotno ulang).
        private void ResetNState()
        {
            _state.ClearAll();
            _nStep = 1;
            SetNButtonState(NState.Locked);
        }

        // ===== Reset N SAJA: patokan TETAP =====
        // Dipakai saat erase Aplikator/WOS dan setelah simpan sukses,
        // supaya operator tidak perlu scan Lotno ulang.
        private void ResetNStateOnly()
        {
            _state.ClearN1();
            _state.ClearN2();
            _state.ClearN3();
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
        //  Memuat riwayat input hari produksi ini ke tabel.
        //
        //  Definisi "hari produksi": 06:00 s/d 06:00 esok hari.
        //   - Jam sekarang < 06:00 → masih dihitung hari produksi KEMARIN.
        //   - Rentang query: tanggalProduksi + 6 jam  s/d  tanggalProduksi + 30 jam.
        //
        //  Filter: terminal + aplikator + noreg operator yang login
        //  (operator hanya melihat data miliknya sendiri).
        // =====================================================
        private void LoadDataCrimping(string terminal, string aplikator, string noreg)
        {
            // Parameter belum lengkap → jangan query
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
FROM Tr_LhhpWH_copy
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

                        int no = 0;   // nomor urut tampilan (bukan dari DB)
                        while (reader.Read())
                        {
                            no++;

                            string wos = reader["Wos"] as string ?? "";
                            string term = reader["terminal"] as string ?? "";
                            string apl = reader["aplikator"] as string ?? "";

                            // Pecah WOS "BEJ81212112.001" → lotNo "BEJ81212112" + skema "1"
                            string lotNo = "";
                            string skema = "-";

                            if (!string.IsNullOrEmpty(wos))
                            {
                                int idx = wos.LastIndexOf('.');
                                if (idx > 0 && idx < wos.Length - 1)
                                {
                                    lotNo = wos.Substring(0, idx);
                                    string skemaStr = wos.Substring(idx + 1);

                                    // ===== Konversi skema ke int (001 → 1) =====
                                    if (int.TryParse(skemaStr, out int skemaInt))
                                        skema = skemaInt.ToString();
                                    else
                                        skema = skemaStr;
                                }
                                else
                                {
                                    // Tidak ada titik valid: tampilkan WOS apa adanya
                                    lotNo = wos;
                                }
                            }

                            // FormatDecimal: NULL / tidak valid → "-", selain itu 2 desimal
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

                            // URUTAN HARUS SAMA dengan definisi kolom di BuildTableCard
                            dgv.Rows.Add(
                                no,
                                lotNo, skema, term, apl,
                                chN1, ihN1, cwN1, iwN1,
                                chN2, ihN2, cwN2, iwN2,
                                chN3, ihN3, cwN3, iwN3,
                                ng, tension);
                        }

                        dgv.ClearSelection();   // jangan ada baris ter-highlight otomatis
                    }
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(this, "Error Query",
                    "Gagal memuat data dari database:\n\n" + ex.Message);
            }
        }

        /// <summary>Muat ulang tabel dengan filter yang sedang aktif di form.</summary>
        private void RefreshTable()
        {
            LoadDataCrimping(
                txtTerminal.Text.Trim(),
                txtAplikator.Text.Trim(),
                Session.Noreg);
        }

        // =====================================================
        //  INSERT: simpan satu siklus N1+N2+N3 ke Tr_LhhpWH_copy
        //
        //  Return true  = 1+ baris tersimpan.
        //  Return false = gagal (popup error sudah ditampilkan di sini).
        //
        //  Nilai yang null di _state dikirim sebagai DBNull.
        //  Kolom tension hanya diisi dari N1 (N2/N3 tidak mengukur tension).
        //  Kolom created_at diisi server (getDate()), bukan jam komputer client.
        // =====================================================
        private bool InsertLhhpWH()
        {
            string terminal = txtTerminal.Text.Trim();
            string aplikator = txtAplikator.Text.Trim();
            string wos = txtWos.Text.Trim();
            string lotno = wos.Split('.')[0];   // bagian sebelum titik

            const string sql = @"
INSERT INTO Tr_LhhpWH_copy
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
                    // ---- Identitas ----
                    cmd.Parameters.AddWithValue("@noreg", Session.Noreg ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@terminal", terminal);
                    cmd.Parameters.AddWithValue("@lotno", lotno);
                    cmd.Parameters.AddWithValue("@aplikator", aplikator);
                    cmd.Parameters.AddWithValue("@wos", wos);

                    // ---- N1 (termasuk tension) ----
                    cmd.Parameters.AddWithValue("@ch_n1", _state.N1_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n1", _state.N1_IH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw_n1", _state.N1_CW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@iw_n1", _state.N1_IW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@tension", _state.N1_Tension ?? (object)DBNull.Value);

                    // ---- N2 ----
                    cmd.Parameters.AddWithValue("@ch_n2", _state.N2_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n2", _state.N2_IH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw_n2", _state.N2_CW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@iw_n2", _state.N2_IW ?? (object)DBNull.Value);

                    // ---- N3 (termasuk NG) ----
                    cmd.Parameters.AddWithValue("@ch_n3", _state.N3_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n3", _state.N3_IH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@cw_n3", _state.N3_CW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@iw_n3", _state.N3_IW ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ng_n3", _state.N3_NG ?? (object)DBNull.Value);

                    // ---- Konteks sesi login ----
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

        /// <summary>Nilai DB → teks 2 desimal. NULL / tidak valid → "-".</summary>
        private static string FormatDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
                return "-";

            decimal d;
            if (!decimal.TryParse(value.ToString(), out d))
                return "-";

            return d.ToString("0.00");
        }

        /// <summary>Nilai DB → teks bilangan bulat. NULL / tidak valid → "-".</summary>
        private static string FormatInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return "-";

            int i;
            if (!int.TryParse(value.ToString(), out i))
                return "-";

            return i.ToString();
        }

        // =====================================================
        //  QUERY: Lotno → tiproductname
        //
        //  tlotid (lotno hasil scan) dicari di dbInventory2..tblFifo,
        //  lalu di-JOIN ke tblItem untuk mendapat nama produk terminal.
        //  Return null kalau tidak ditemukan / terjadi error.
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
        //  QUERY: tiproductname → patokan Terminal A/B
        //
        //  Tabel TrRPHMesin menyimpan standar ukuran per mesin:
        //    sisi A: cha, iha, cwa, iwa, T1 (tension)
        //    sisi B: chb, ihb, cwb, iwb, T2 (tension)
        //  Satu baris bisa cocok di sisi A, sisi B, atau keduanya,
        //  jadi satu baris DB bisa menghasilkan 0-2 TerminalResult.
        //
        //  Hasil diurutkan dari RPH terbaru (KodeRPHMeisn DESC).
        //  Catatan: nama kolom "KodeRPHMeisn" memang begitu di database
        //  (typo asli), jangan "diperbaiki" tanpa mengubah skema DB.
        // =====================================================
        private List<TerminalResult> CheckTerminal(string tiproductname)
        {
            var list = new List<TerminalResult>();
            if (string.IsNullOrWhiteSpace(tiproductname)) return list;

            const string sql = @"
SELECT KodeMesin, TERMINALA, TERMINALB,
       cha, iha, cwa, iwa, T1,
       chb, ihb, cwb, iwb, T2
FROM [assy].[dbo].[TrRPHMesin]
WHERE TERMINALA = @tiproductname
   OR TERMINALB = @tiproductname
ORDER BY KodeRPHMeisn DESC;";

            try
            {
                using (var conn = _db.Open())
                using (var cmd = new SqlCommand(sql, (SqlConnection)conn))
                {
                    cmd.Parameters.AddWithValue("@tiproductname", tiproductname);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string termA = reader["TERMINALA"] as string;
                            string termB = reader["TERMINALB"] as string;

                            // Cocok di sisi A → ambil kolom *a
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

                            // Cocok di sisi B → ambil kolom *b
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
        //
        //  Memakai LIKE '%kode%' (mengandung), bukan sama persis,
        //  karena hasil scan bisa berisi prefix/suffix tambahan.
        //  Konsekuensi: kode pendek bisa cocok dengan banyak aplikator.
        //  Kalau ingin lebih ketat, ganti ke "KodeAplikator = @kode".
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
        //  VALIDASI: Skema pada WOS harus tepat 3 digit angka
        //
        //  Contoh valid  : "BEJ81212112.001"  → skemaInt = 1
        //  Contoh invalid: "BEJ81212112.01", "BEJ81212112.0A1", "BEJ81212112."
        //
        //  Skema diambil dari bagian setelah titik TERAKHIR.
        // =====================================================
        private bool TryParseSkema(string wos, out int skemaInt)
        {
            skemaInt = -1;
            if (string.IsNullOrWhiteSpace(wos)) return false;

            int idx = wos.LastIndexOf('.');
            if (idx < 0 || idx >= wos.Length - 1) return false;   // tidak ada titik / kosong setelah titik

            string skema = wos.Substring(idx + 1).Trim();

            // Harus tepat 3 digit angka
            if (skema.Length != 3) return false;
            if (!skema.All(char.IsDigit)) return false;

            return int.TryParse(skema, out skemaInt);
        }

        /// <summary>Nilai DB → decimal?. NULL / tidak valid → null.</summary>
        private static decimal? ReadDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            decimal d;
            return decimal.TryParse(value.ToString(), out d) ? d : (decimal?)null;
        }

    }
}