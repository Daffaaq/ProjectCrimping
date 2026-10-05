using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectCrimping
{
    /// <summary>Tema + helper untuk semua popup (FormN1/N2/N3). Ubah tampilan cukup di sini.</summary>
    internal static class AppThemePopup
    {
        // ===== Palet (ikut login) =====
        public static readonly Color Navy = AppThemeLogin.ColorNavy;
        public static readonly Color Accent = AppThemeLogin.ColorAccent;
        public static readonly Color AccentSoft = Color.FromArgb(253, 243, 235);
        public static readonly Color AccentLine = AppThemeLogin.ColorAccentDisabled;
        public static readonly Color TextDark = AppThemeLogin.ColorTextDark;
        public static readonly Color TextSub = AppThemeLogin.ColorTextSub;
        public static readonly Color FieldBg = AppThemeLogin.ColorFieldBg;
        public static readonly Color FieldBorder = AppThemeLogin.ColorFieldBorder;

        // ===== Font =====
        public static readonly Font FontTitle = new Font("Segoe UI", 18f, FontStyle.Bold);
        public static readonly Font FontSub = new Font("Segoe UI", 10f);
        public static readonly Font FontLabel = new Font("Segoe UI Semibold", 9.5f);
        public static readonly Font FontBtn = new Font("Segoe UI Semibold", 10.5f);

        // ===== Ukuran =====
        private const int FormWidth = 440;
        private const int HeaderHeight = 96;
        private const int RowHeight = 76;
        private const int ActionHeight = 84;

        /// <summary>
        /// Bangun kerangka popup: header + body (baris field) + tombol Simpan/Batal.
        /// Return: body, tempat field ditambahkan lewat AddDecimal / AddInteger / AddToggle.
        /// </summary>
        public static TableLayoutPanel BuildShell(Form f, string title, string subtitle, int rowCount,
                                                  out RoundedButton btnSave, out RoundedButton btnCancel)
        {
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.BackColor = Color.White;
            f.AutoScaleMode = AutoScaleMode.None;
            f.ClientSize = new Size(FormWidth, HeaderHeight + rowCount * RowHeight + ActionHeight + 12);
            f.KeyPreview = true;
            f.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { f.DialogResult = DialogResult.Cancel; }
            };

            // --- Header ---
            var header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight, BackColor = Color.White };
            header.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Location = new Point(24, 14),
                ForeColor = TextDark,
                Font = FontTitle,
                BackColor = Color.White
            });
            header.Controls.Add(new Label
            {
                Text = subtitle,
                AutoSize = true,
                Location = new Point(26, 54),
                ForeColor = TextSub,
                Font = FontSub,
                BackColor = Color.White
            });
            header.Controls.Add(new Panel
            {
                Size = new Size(48, 4),
                Location = new Point(27, 82),
                BackColor = Accent
            });

            // --- Body ---
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24, 0, 24, 0),
                ColumnCount = 1
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // --- Tombol aksi ---
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = ActionHeight,
                BackColor = Color.White,
                Padding = new Padding(24, 10, 24, 20),
                ColumnCount = 2,
                RowCount = 1
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            btnCancel = new RoundedButton
            {
                Text = "BATAL",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                Font = FontBtn,
                Radius = 12,
                FillColor = AccentSoft,
                BorderColor = AccentLine,
                TextColor = Accent
            };
            btnSave = new RoundedButton
            {
                Text = "SIMPAN",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0),
                Font = FontBtn,
                Radius = 12,
                FillColor = Accent,
                BorderColor = Accent,
                TextColor = Color.White
            };
            actions.Controls.Add(btnCancel, 0, 0);
            actions.Controls.Add(btnSave, 1, 0);

            // Urutan dock: Fill dulu, lalu Bottom, lalu Top
            f.Controls.Add(body);
            f.Controls.Add(actions);
            f.Controls.Add(header);
            return body;
        }

        // ===== Field desimal (number up-down) =====
        public static NumericUpDown AddDecimal(TableLayoutPanel body, string label, int decimals)
        {
            decimal step = 1m;
            for (int i = 0; i < decimals; i++) step /= 10m;

            var nud = NewNumeric();
            nud.DecimalPlaces = decimals;
            nud.Increment = step;
            nud.Minimum = 0;
            nud.Maximum = 999999;
            AddRow(body, label, WrapField(nud));
            return nud;
        }

        // ===== Field angka bulat =====
        public static NumericUpDown AddInteger(TableLayoutPanel body, string label)
        {
            var nud = NewNumeric();
            nud.DecimalPlaces = 0;
            nud.Increment = 1;
            nud.Minimum = 0;
            nud.Maximum = 999999;
            AddRow(body, label, WrapField(nud));
            return nud;
        }

        // ===== Toggle 2 pilihan (Terminal A / B) =====
        public static void AddToggle(TableLayoutPanel body, string label, string textA, string textB,
                                     out RoundedButton btnA, out RoundedButton btnB)
        {
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

            btnA = NewToggleButton(textA, new Padding(0, 0, 5, 0));
            btnB = NewToggleButton(textB, new Padding(5, 0, 0, 0));
            grid.Controls.Add(btnA, 0, 0);
            grid.Controls.Add(btnB, 1, 0);
            AddRow(body, label, grid);
        }

        /// <summary>Tandai tombol mana yang aktif.</summary>
        public static void SetToggleActive(RoundedButton active, RoundedButton inactive)
        {
            Style(active, true);
            Style(inactive, false);
        }

        // ===== Internal =====
        private static void Style(RoundedButton b, bool on)
        {
            b.FillColor = on ? Accent : FieldBg;
            b.BorderColor = on ? Accent : FieldBorder;
            b.TextColor = on ? Color.White : TextSub;
            b.Invalidate();
        }

        private static RoundedButton NewToggleButton(string text, Padding margin)
        {
            var b = new RoundedButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = margin,
                Font = FontBtn,
                Radius = 10
            };
            Style(b, false);
            return b;
        }

        private static NumericUpDown NewNumeric()
        {
            return new NumericUpDown
            {
                Dock = DockStyle.Top,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = AppThemeLogin.FontInput,
                TextAlign = HorizontalAlignment.Left,
                ThousandsSeparator = false
            };
        }

        // ===== Field teks (untuk scan/ketik) =====
        public static TextBox AddText(TableLayoutPanel body, string label)
        {
            var tb = new TextBox
            {
                Dock = DockStyle.Top,
                BorderStyle = BorderStyle.None,
                BackColor = FieldBg,
                ForeColor = TextDark,
                Font = AppThemeLogin.FontInput
            };
            AddRow(body, label, WrapField(tb));
            return tb;
        }

        // ===== Kotak status (teks satu baris di dalam field) =====
        public static Label AddStatus(TableLayoutPanel body, string label, string text)
        {
            var box = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 0, 8, 0),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder
            };
            var lbl = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = new Font("Segoe UI", 10f, FontStyle.Italic),
                BackColor = Color.Transparent
            };
            box.Controls.Add(lbl);
            AddRow(body, label, box);
            return lbl;
        }

        // ===== Dua checkbox berbentuk tombol (boleh dua-duanya aktif) =====
        public static void AddCheckPair(TableLayoutPanel body, string label, string textA, string textB,
                                        out RoundedButton btnA, out RoundedButton btnB)
        {
            AddToggle(body, label, textA, textB, out btnA, out btnB);
        }

        public static void SetCheck(RoundedButton b, string text, bool on)
        {
            b.Text = text;
            b.Checked = on;
            Style(b, on);
        }

        // ===== Dua kotak data (kiri A, kanan B) =====
        public static void AddDataPair(TableLayoutPanel body, string label, out Label lblA, out Label lblB)
        {
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

            lblA = NewDataCard(grid, 0, new Padding(0, 0, 5, 0));
            lblB = NewDataCard(grid, 1, new Padding(5, 0, 0, 0));
            AddRow(body, label, grid);
        }

        private static Label NewDataCard(TableLayoutPanel grid, int col, Padding margin)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = margin,
                Padding = new Padding(8),
                Radius = 10,
                FillColor = Color.White,
                BorderColor = FieldBorder
            };
            var lbl = new Label
            {
                Text = "----------",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TextDark,
                Font = new Font("Segoe UI Semibold", 11f),
                BackColor = Color.Transparent
            };
            card.Controls.Add(lbl);
            grid.Controls.Add(card, col, 0);
            return lbl;
        }

        // Kotak field gaya login (abu muda, border oranye saat fokus)
        private static Control WrapField(Control input)
        {
            var field = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                Radius = 10,
                FillColor = FieldBg,
                BorderColor = FieldBorder,
                FocusColor = Accent
            };
            var inner = new TransparentPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 9, 8, 0) };
            inner.Controls.Add(input);
            field.Controls.Add(inner);

            input.Enter += (s, e) => { field.Focused = true; field.Invalidate(); };
            input.Leave += (s, e) => { field.Focused = false; field.Invalidate(); };
            return field;
        }

        private static void AddRow(TableLayoutPanel body, string label, Control content)
        {
            var wrap = new TransparentPanel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 10)
            };
            var lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextSub,
                Font = FontLabel,
                BackColor = Color.Transparent
            };
            content.Dock = DockStyle.Fill;
            wrap.Controls.Add(content);   // Fill dulu
            wrap.Controls.Add(lbl);       // lalu Top

            body.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
            body.RowCount = body.RowStyles.Count;
            body.Controls.Add(wrap, 0, body.RowCount - 1);
        }
    }
}