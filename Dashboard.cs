using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class Dashboard : Form
    {
        // ====== Palet diambil dari AppThemeLogin supaya selalu sama dengan halaman login ======
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
        private static readonly Color PageBg = Color.White;

        private static readonly Color FieldLockedBg = Color.FromArgb(238, 240, 243);
        private static readonly Color NLockedBg = Color.FromArgb(237, 239, 242);
        private static readonly Color NLockedBorder = Color.FromArgb(221, 225, 230);
        private static readonly Color NLockedText = Color.FromArgb(168, 176, 186);

        // Glyph Segoe MDL2 Assets
        private const string IcAdd = "\uE710";
        private const string IcClock = "\uE121";
        private const string IcExport = "\uE8A5";
        private const string IcPower = "\uE7E8";
        private const string IcErase = "\uE75C";
        private const string IcTerminal = "\uE7F4";
        private const string IcTag = "\uE8EC";

        // Kontrol
        private TextBox txtTerminal, txtAplikator, txtWos;
        private RoundedButton btnN1, btnN2, btnN3;
        private DataGridView dgv;

        // Referensi field & tombol erase
        private RoundedPanel fieldTerminal, fieldAplikator, fieldWos;
        private RoundedButton eraseTerminal, eraseAplikator, eraseWos;


        // State sementara data N1/N2/N3 (belum di-insert sampai user isi N3)
        private CrimpingState _state = new CrimpingState();

        // Database helper
        private readonly DatabaseHelper _db = new DatabaseHelper(DbProvider.SqlServer);

        private readonly Login _login;

        public Dashboard(Login login)
        {
            _login = login;

            InitializeComponent();

            DoubleBuffered = true;
            BuildUI();
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

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Add(BuildContent());
            Controls.Add(BuildSidebar());
            ActiveControl = txtTerminal;
            ResumeLayout(true);
        }

        // =====================================================
        //  INNER CLASS: State N1 / N2 / N3
        // =====================================================
        private class CrimpingState
        {
            // N1
            public decimal? N1_CH { get; set; }
            public decimal? N1_IH { get; set; }
            public decimal? N1_Val { get; set; }
            public string N1_SisiTerminal { get; set; }   // "A" / "B"
            public string N1_TerminalText { get; set; }   // barcode terminal yang di-scan

            // N2
            public decimal? N2_CH { get; set; }
            public decimal? N2_IH { get; set; }

            // N3
            public decimal? N3_CH { get; set; }
            public decimal? N3_IH { get; set; }
            public int? N3_NG { get; set; }

            public void ClearN1()
            {
                N1_CH = null; N1_IH = null; N1_Val = null;
                N1_SisiTerminal = null; N1_TerminalText = null;
            }
            public void ClearN2() { N2_CH = null; N2_IH = null; }
            public void ClearN3() { N3_CH = null; N3_IH = null; N3_NG = null; }

            public void ClearAll()
            {
                ClearN1(); ClearN2(); ClearN3();
            }
        }

        // =====================================================
        //  SIDEBAR
        // =====================================================
        private Control BuildSidebar()
        {
            var side = new SidebarPanel { Dock = DockStyle.Left, Width = 270 };

            var profile = new TransparentPanel { Dock = DockStyle.Top, Height = 215 };

            var avatar = new AvatarCircle
            {
                Initials = "DA",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                Size = new Size(92, 92),
                Location = new Point((270 - 92) / 2, 30),
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
                    if (f.ShowDialog(this) == DialogResult.OK)
                    {
                        Session.Clear();

                        _login.Show();
                        Close();
                    }
                }
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
        //  KONTEN UTAMA
        // =====================================================
        private Control BuildContent()
        {
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(28, 22, 28, 22),
                ColumnCount = 1,
                RowCount = 4
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 212));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            main.Controls.Add(BuildHeader(), 0, 0);
            main.Controls.Add(BuildInputCard(), 0, 1);
            main.Controls.Add(BuildNButtons(), 0, 2);
            main.Controls.Add(BuildTableCard(), 0, 3);
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
                Margin = new Padding(0, 0, 0, 16)
            };
            flow.Controls.Add(new Label
            {
                Text = "Dashboard Crimping",
                AutoSize = true,
                Margin = Padding.Empty,
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
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

        private Control BuildInputCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(20, 16, 20, 16),
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

            LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
            LockField(fieldWos, txtWos, eraseWos, true);

            // ===== ENTER di Terminal =====
            txtTerminal.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtTerminal, fieldTerminal)) return;

                LockField(fieldTerminal, txtTerminal, eraseTerminal, true);
                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                txtAplikator.Focus();
            };

            // ===== ENTER di Aplikator =====
            txtAplikator.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtAplikator, fieldAplikator)) return;

                LockField(fieldAplikator, txtAplikator, eraseAplikator, true);
                LockField(fieldWos, txtWos, eraseWos, false);
                txtWos.Focus();

                // Pemicu query dgv #1
                LoadDataCrimping(
                    txtTerminal.Text.Trim(),
                    txtAplikator.Text.Trim(),
                    Session.Noreg);
            };

            // ===== ENTER di WOS =====
            txtWos.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                if (!ValidateNotEmpty(txtWos, fieldWos)) return;

                // Validasi WOS harus punya titik (buat LotNo)
                if (!txtWos.Text.Contains("."))
                {
                    fieldWos.BorderColor = ErrorColor;
                    fieldWos.Invalidate();

                    NotifPopup.Warning(this, "Format WOS Salah",
                        "WOS harus mengandung titik (contoh: 6B26031961.0001).");

                    txtWos.Focus();
                    return;
                }

                LockField(fieldWos, txtWos, eraseWos, true);

                // WOS selesai → N1 Ready
                SetNButtonState(btnN1, NState.Ready);
            };

            // ===== ERASE TERMINAL =====
            // Reset total: field, N-state, tabel.
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
            // Reset Aplikator + WOS + N-state + tabel.
            eraseAplikator.Click += (s, e) =>
            {
                txtAplikator.Clear();
                txtWos.Clear();
                LockField(fieldAplikator, txtAplikator, eraseAplikator, false);
                LockField(fieldWos, txtWos, eraseWos, true);
                ResetBorder(fieldAplikator, fieldWos);

                ResetNState();
                ClearTable();

                txtAplikator.Focus();
            };

            // ===== ERASE WOS =====
            // Reset WOS + N-state saja. Tabel tetap (pemicu tabel di Aplikator).
            eraseWos.Click += (s, e) =>
            {
                txtWos.Clear();
                LockField(fieldWos, txtWos, eraseWos, false);
                ResetBorder(fieldWos);

                ResetNState();

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
        //  TOMBOL N1 / N2 / N3
        // =====================================================
        private Control BuildNButtons()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 3,
                RowCount = 1
            };
            for (int i = 0; i < 3; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            btnN1 = NButton("N1", 0);
            btnN2 = NButton("N2", 1);
            btnN3 = NButton("N3", 2);

            SetNButtonState(btnN1, NState.Locked);
            SetNButtonState(btnN2, NState.Locked);
            SetNButtonState(btnN3, NState.Locked);

            // ===== N1 =====
            btnN1.Click += (s, e) =>
            {
                if ((btnN1.Tag as string) != "ready") return;

                using (var f = new FormN1())
                {
                    if (f.ShowDialog(this) != DialogResult.OK)
                    {
                        // User Cancel / X → tidak berubah, N1 tetap Ready
                        return;
                    }

                    // Validasi: user harus scan terminal
                    if (string.IsNullOrWhiteSpace(f.TerminalText))
                    {
                        NotifPopup.Warning(this, "Data Belum Lengkap",
                            "Terminal belum di-scan. Silakan pilih sisi (A/B) dan scan terminal.");
                        return;
                    }

                    // Simpan ke state sementara
                    _state.N1_CH = f.CH;
                    _state.N1_IH = f.IH;
                    _state.N1_Val = f.ValN1;
                    _state.N1_SisiTerminal = f.Terminal;          // "A" / "B"
                    _state.N1_TerminalText = f.TerminalText;      // barcode

                    // N1 locked, N2 Ready
                    SetNButtonState(btnN1, NState.Locked);
                    SetNButtonState(btnN2, NState.Ready);
                }
            };

            // ===== N2 =====
            btnN2.Click += (s, e) =>
            {
                if ((btnN2.Tag as string) != "ready") return;

                using (var f = new FormN2())
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;

                    _state.N2_CH = f.CH;
                    _state.N2_IH = f.IH;

                    SetNButtonState(btnN2, NState.Locked);
                    SetNButtonState(btnN3, NState.Ready);
                }
            };

            // ===== N3 =====
            btnN3.Click += (s, e) =>
            {
                if ((btnN3.Tag as string) != "ready") return;

                using (var f = new FormN3())
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;

                    _state.N3_CH = f.CH;
                    _state.N3_IH = f.IH;
                    _state.N3_NG = f.NG;

                    // Insert ke Tr_LhhpWH
                    bool ok = InsertLhhpWH();

                    if (!ok)
                    {
                        // Gagal insert → kembalikan N3 Ready (biar bisa coba lagi),
                        // state tetap tersimpan
                        SetNButtonState(btnN3, NState.Ready);
                        return;
                    }

                    // Sukses:
                    // 1. Reset WOS (otomatis N1/N2/N3 reset)
                    // 2. Terminal & Aplikator TETAP
                    // 3. Focus ke WOS DULU (biar operator bisa langsung scan berikutnya)
                    // 4. Refresh tabel di belakang (tidak blocking)
                    txtWos.Clear();
                    LockField(fieldWos, txtWos, eraseWos, false);
                    ResetBorder(fieldWos);

                    ResetNState();

                    // Paksa fokus ke WOS via BeginInvoke
                    // (biar jalan setelah semua event handler selesai)
                    BeginInvoke(new Action(() =>
                    {
                        txtWos.Focus();
                        txtWos.SelectAll();
                    }));

                    // Refresh tabel setelah fokus di-set
                    RefreshTable();
                }
            };

            row.Controls.Add(btnN1, 0, 0);
            row.Controls.Add(btnN2, 1, 0);
            row.Controls.Add(btnN3, 2, 0);
            return row;
        }

        private RoundedButton NButton(string text, int index)
        {
            return new RoundedButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(index == 0 ? 0 : 8, 0, index == 2 ? 0 : 8, 16),
                Font = new Font("Segoe UI Semibold", 16f),
                Radius = 16,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                TextColor = TextSub,
                IconGlyph = null
            };
        }

        // =====================================================
        //  TABEL
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
                ColumnHeadersHeight = 42,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowTemplate = { Height = 38 },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            dgv.ColumnHeadersDefaultCellStyle.BackColor = Navy;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Navy;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;

            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.ForeColor = TextDark;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
            dgv.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
            dgv.DefaultCellStyle.SelectionBackColor = AccentSoft;
            dgv.DefaultCellStyle.SelectionForeColor = TextDark;

            dgv.Columns.Add("colNo", "NO");
            dgv.Columns.Add("colTerminal", "TERMINAL");
            dgv.Columns.Add("colAplikator", "APLIKATOR");
            dgv.Columns.Add("colWos", "WOS");
            dgv.Columns.Add("colN1", "N1");
            dgv.Columns.Add("colN2", "N2");
            dgv.Columns.Add("colN3", "N3");
            dgv.Columns.Add("colCreatedAt", "CREATED AT");

            // NO — kecil aja
            dgv.Columns["colNo"].FillWeight = 25;
            dgv.Columns["colNo"].MinimumWidth = 45;

            // N1 / N2 / N3 — sedang
            foreach (var name in new[] { "colN1", "colN2", "colN3" })
            {
                var col = dgv.Columns[name];
                col.FillWeight = 40;
                col.MinimumWidth = 55;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Padding = Padding.Empty;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Padding = Padding.Empty;
                col.DefaultCellStyle.ForeColor = AppThemeLogin.ColorOnline;
                col.DefaultCellStyle.SelectionForeColor = AppThemeLogin.ColorOnline;
                col.DefaultCellStyle.Font = AppThemeLogin.IconFont(12f);
            }

            // CREATED AT — fixed width biar gak kepotong
            var colCreated = dgv.Columns["colCreatedAt"];
            colCreated.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colCreated.Width = 170;
            colCreated.MinimumWidth = 170;
            colCreated.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colCreated.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colCreated.DefaultCellStyle.Font = new Font("Consolas", 9.5f);  // monospace biar rapi
            colCreated.DefaultCellStyle.ForeColor = TextSub;
            colCreated.DefaultCellStyle.SelectionForeColor = TextSub;

            dgv.Rows.Clear();

            card.Controls.Add(dgv);
            return card;
        }
        // =====================================================
        //  HELPER: Lock / Validasi
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

        private void ResetBorder(params RoundedPanel[] fields)
        {
            foreach (var f in fields)
            {
                f.BorderColor = f.Focused ? Accent : FieldBorder;
                f.Invalidate();
            }
        }

        // =====================================================
        //  HELPER: State tombol N
        // =====================================================
        private enum NState { Locked, Ready }

        private void SetNButtonState(RoundedButton btn, NState state)
        {
            if (btn == null) return;

            string baseText = btn == btnN1 ? "N1" : btn == btnN2 ? "N2" : "N3";

            if (state == NState.Locked)
            {
                btn.Tag = "locked";
                btn.Enabled = true;
                btn.Text = baseText;
                btn.Font = new Font("Segoe UI Semibold", 16f);
                btn.FillColor = NLockedBg;
                btn.BorderColor = NLockedBorder;
                btn.TextColor = NLockedText;
                btn.IconGlyph = null;
                btn.Cursor = Cursors.Default;
            }
            else
            {
                btn.Tag = "ready";
                btn.Enabled = true;
                btn.Text = baseText;
                btn.Font = new Font("Segoe UI Semibold", 16f);
                btn.FillColor = AccentSoft;
                btn.BorderColor = Accent;
                btn.TextColor = Accent;
                btn.IconGlyph = null;
                btn.Cursor = Cursors.Hand;
            }
            btn.Invalidate();
        }

        /// <summary>
        /// Reset semua tombol N → Locked, dan bersihkan state N1/N2/N3.
        /// </summary>
        private void ResetNState()
        {
            _state.ClearAll();
            SetNButtonState(btnN1, NState.Locked);
            SetNButtonState(btnN2, NState.Locked);
            SetNButtonState(btnN3, NState.Locked);
        }

        private void ClearTable()
        {
            if (dgv != null) dgv.Rows.Clear();
        }

        // =====================================================
        //  QUERY: LoadDataCrimping
        // =====================================================
        /// <summary>
        /// Query Tr_LhhpWH dan tampilkan ke dgv. Filter: terminal + aplikator + noreg.
        /// Filter tanggal produksi (06:00 - 05:59) sudah ditanam di query.
        /// </summary>
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
    wos,
    CASE WHEN ch_n1 IS NOT NULL AND ih_n1 IS NOT NULL AND n1 IS NOT NULL 
         THEN 'Centang' ELSE 'Tidak' END AS N1,
    CASE WHEN ch_n2 IS NOT NULL AND ih_n2 IS NOT NULL 
         THEN 'Centang' ELSE 'Tidak' END AS N2,
    CASE WHEN ch_n3 IS NOT NULL AND ih_n3 IS NOT NULL 
         THEN 'Centang' ELSE 'Tidak' END AS N3,
    created_at
FROM Tr_LhhpWH
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
                        int no = 1;

                        while (reader.Read())
                        {
                            string wos = reader["wos"] as string ?? "";
                            string n1 = (reader["N1"] as string) == "Centang" ? "\uE73E" : "";
                            string n2 = (reader["N2"] as string) == "Centang" ? "\uE73E" : "";
                            string n3 = (reader["N3"] as string) == "Centang" ? "\uE73E" : "";

                            // Format created_at → YYYY-MM-DD HH:MM:SS
                            string createdAt = "";
                            if (reader["created_at"] != DBNull.Value)
                            {
                                DateTime dt = Convert.ToDateTime(reader["created_at"]);
                                createdAt = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            }

                            dgv.Rows.Add(
                                no.ToString(),
                                terminal,
                                aplikator,
                                wos,
                                n1, n2, n3,
                                createdAt);

                            no++;
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

        /// <summary>
        /// Refresh dgv pakai nilai yang ada di field sekarang.
        /// Dipakai setelah insert sukses N3.
        /// </summary>
        private void RefreshTable()
        {
            LoadDataCrimping(
                txtTerminal.Text.Trim(),
                txtAplikator.Text.Trim(),
                Session.Noreg);
        }

        // =====================================================
        //  INSERT: Tr_LhhpWH
        // =====================================================
        /// <summary>
        /// Insert hasil N1/N2/N3 ke Tr_LhhpWH.
        /// Terminal & Aplikator diambil dari field saat ini.
        /// LotNo = WOS sebelum titik pertama.
        /// Return true kalau sukses.
        /// </summary>
        private bool InsertLhhpWH()
        {
            string terminal = txtTerminal.Text.Trim();
            string aplikator = txtAplikator.Text.Trim();
            string wos = txtWos.Text.Trim();
            string lotno = wos.Split('.')[0];

            // Sisi A / B: hanya salah satu yang diisi (sesuai pilihan user di FormN1)
            string sisiA = null;
            string sisiB = null;
            if (_state.N1_SisiTerminal == "A") sisiA = _state.N1_TerminalText;
            else if (_state.N1_SisiTerminal == "B") sisiB = _state.N1_TerminalText;

            const string sql = @"
INSERT INTO Tr_LhhpWH
(
    noreg, terminal, lotno, aplikator, Wos,
    ch_n1, ih_n1, n1,
    sisi_terminal_a_n1, sisi_terminal_b_n1,
    ch_n2, ih_n2,
    ch_n3, ih_n3, ng_n3,
    shift, kode_mesin
)
VALUES
(
    @noreg, @terminal, @lotno, @aplikator, @wos,
    @ch_n1, @ih_n1, @n1,
    @sisi_a, @sisi_b,
    @ch_n2, @ih_n2,
    @ch_n3, @ih_n3, @ng_n3,
    @shift, @kode_mesin
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
                    cmd.Parameters.AddWithValue("@n1", _state.N1_Val ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@sisi_a", (object)sisiA ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@sisi_b", (object)sisiB ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@ch_n2", _state.N2_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n2", _state.N2_IH ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@ch_n3", _state.N3_CH ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ih_n3", _state.N3_IH ?? (object)DBNull.Value);
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
    }
}