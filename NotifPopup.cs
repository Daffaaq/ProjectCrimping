using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProjectCrimping
{
    /// <summary>Popup notifikasi bertema sama dengan AppThemePopup. Panggil: NotifPopup.Success(this, "Judul", "Pesan");</summary>
    internal static class NotifPopup
    {
        public enum Kind { Info, Success, Warning, Error }

        private static readonly Color ColSuccess = Color.FromArgb(34, 160, 90);
        private static readonly Color ColError = Color.FromArgb(214, 64, 69);

        private const int FormWidth = 440;
        private const int ActionHeight = 84;
        private const int PadX = 24;

        // ===== Shortcut =====
        public static void Info(IWin32Window owner, string title, string message)
        { Show(owner, Kind.Info, title, message); }

        public static void Success(IWin32Window owner, string title, string message)
        { Show(owner, Kind.Success, title, message); }

        public static void Warning(IWin32Window owner, string title, string message)
        { Show(owner, Kind.Warning, title, message); }

        public static void Error(IWin32Window owner, string title, string message)
        { Show(owner, Kind.Error, title, message); }

        /// <summary>Dialog konfirmasi. True kalau user pilih Ya.</summary>
        public static bool Confirm(IWin32Window owner, string title, string message,
                                   string yesText = "YA", string noText = "BATAL")
        {
            return Show(owner, Kind.Warning, title, message, yesText, noText) == DialogResult.OK;
        }

        // ===== Inti =====
        public static DialogResult Show(IWin32Window owner, Kind kind, string title, string message,
                                        string okText = "OK", string cancelText = null)
        {
            Color tone = ToneOf(kind);
            int textWidth = FormWidth - PadX * 2;
            int msgHeight = TextRenderer.MeasureText(message ?? "", AppThemePopup.FontSub,
                                new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 6;

            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.BackColor = Color.White;
                f.AutoScaleMode = AutoScaleMode.None;
                f.KeyPreview = true;
                f.ClientSize = new Size(FormWidth, 96 + msgHeight + 16 + ActionHeight);

                f.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                        f.DialogResult = cancelText == null ? DialogResult.OK : DialogResult.Cancel;
                    else if (e.KeyCode == Keys.Enter)
                        f.DialogResult = DialogResult.OK;
                };

                // --- Header (ikon + judul) ---
                var header = new Panel { Dock = DockStyle.Top, Height = 96 + msgHeight, BackColor = Color.White };

                var icon = new Panel { Size = new Size(52, 52), Location = new Point(PadX, 22), BackColor = Color.White };
                icon.Paint += (s, e) => DrawIcon(e.Graphics, icon.ClientRectangle, kind, tone);

                var lblTitle = new Label
                {
                    Text = title,
                    Location = new Point(PadX + 52 + 14, 22),
                    Size = new Size(FormWidth - (PadX + 52 + 14) - PadX, 52),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    ForeColor = AppThemePopup.TextDark,
                    Font = AppThemePopup.FontTitle,
                    BackColor = Color.White
                };

                var bar = new Panel { Size = new Size(48, 4), Location = new Point(PadX + 3, 84), BackColor = tone };

                var lblMsg = new Label
                {
                    Text = message,
                    Location = new Point(PadX + 2, 96),
                    Size = new Size(textWidth, msgHeight),
                    ForeColor = AppThemePopup.TextSub,
                    Font = AppThemePopup.FontSub,
                    BackColor = Color.White
                };

                header.Controls.Add(icon);
                header.Controls.Add(lblTitle);
                header.Controls.Add(bar);
                header.Controls.Add(lblMsg);

                // --- Tombol ---
                bool two = cancelText != null;
                var actions = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = ActionHeight,
                    BackColor = Color.White,
                    Padding = new Padding(PadX, 10, PadX, 20),
                    ColumnCount = two ? 2 : 1,
                    RowCount = 1
                };
                actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                var btnOk = new RoundedButton
                {
                    Text = okText,
                    Dock = DockStyle.Fill,
                    Font = AppThemePopup.FontBtn,
                    Radius = 12,
                    FillColor = tone,
                    BorderColor = tone,
                    TextColor = Color.White
                };
                btnOk.Click += (s, e) => { f.DialogResult = DialogResult.OK; };

                if (two)
                {
                    actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
                    actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));

                    var btnNo = new RoundedButton
                    {
                        Text = cancelText,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(0, 0, 8, 0),
                        Font = AppThemePopup.FontBtn,
                        Radius = 12,
                        FillColor = AppThemePopup.AccentSoft,
                        BorderColor = AppThemePopup.AccentLine,
                        TextColor = AppThemePopup.Accent
                    };
                    btnNo.Click += (s, e) => { f.DialogResult = DialogResult.Cancel; };
                    btnOk.Margin = new Padding(8, 0, 0, 0);
                    actions.Controls.Add(btnNo, 0, 0);
                    actions.Controls.Add(btnOk, 1, 0);
                }
                else
                {
                    actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    actions.Controls.Add(btnOk, 0, 0);
                }

                f.Controls.Add(actions);
                f.Controls.Add(header);

                return owner != null ? f.ShowDialog(owner) : f.ShowDialog();
            }
        }

        // ===== Internal =====
        private static Color ToneOf(Kind k)
        {
            switch (k)
            {
                case Kind.Success: return ColSuccess;
                case Kind.Error: return ColError;
                case Kind.Warning: return AppThemePopup.Accent;
                default: return AppThemePopup.Navy;
            }
        }

        private static void DrawIcon(Graphics g, Rectangle r, Kind kind, Color tone)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            r.Inflate(-2, -2);

            using (var soft = new SolidBrush(Color.FromArgb(32, tone)))
                g.FillEllipse(soft, r);
            var inner = r; inner.Inflate(-7, -7);
            using (var solid = new SolidBrush(tone))
                g.FillEllipse(solid, inner);

            string glyph;
            switch (kind)
            {
                case Kind.Success: glyph = "\u2713"; break;
                case Kind.Warning: glyph = "!"; break;
                case Kind.Error: glyph = "\u2715"; break;
                default: glyph = "i"; break;
            }
            using (var font = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, font, Brushes.White, inner, sf);
        }
    }
}