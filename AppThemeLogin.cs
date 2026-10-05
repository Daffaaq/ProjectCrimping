using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace ProjectCrimping
{
    internal static class AppThemeLogin
    {
        // ====================================================================
        //  FONT ICON
        // ====================================================================
        private static readonly PrivateFontCollection _privateFonts = new PrivateFontCollection();
        private static readonly List<IntPtr> _fontPointers = new List<IntPtr>();
        private static FontFamily _iconFamily;
        private static bool _iconResolved;

        private static bool _fontsLoaded;

        public static void LoadLocalFonts()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string resourceName = Array.Find(asm.GetManifestResourceNames(),
                    n => n.EndsWith("segmdl2.ttf", StringComparison.OrdinalIgnoreCase));
                if (resourceName == null) return;

                string tmp = Path.Combine(Path.GetTempPath(), "crimping_segmdl2.ttf");
                if (!File.Exists(tmp))
                {
                    using (var s = asm.GetManifestResourceStream(resourceName))
                    using (var f = File.Create(tmp))
                        s.CopyTo(f);
                }
                _privateFonts.AddFontFile(tmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Font gagal di-load: " + ex.Message);
            }
        }
        private static FontFamily ResolveIconFamily()
        {
            LoadLocalFonts();
            if (_iconResolved) return _iconFamily;
            _iconResolved = true;
            try
            {
                var installed = new InstalledFontCollection();
                foreach (string name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
                    foreach (var fam in installed.Families)
                        if (string.Equals(fam.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            _iconFamily = fam;
                            return _iconFamily;
                        }

                if (_privateFonts.Families.Length > 0)
                    _iconFamily = _privateFonts.Families[0];
            }
            catch { }
            return _iconFamily;
        }

        public const string IconPerson = "\uE77B";
        public const string IconPeople = "\uE716";
        public const string IconGear = "\uE713";
        public const string IconLock = "\uE72E";
        public const string IconShift = "\uE7E8";

        public const string FallbackPerson = "👤";
        public const string FallbackPeople = "👥";
        public const string FallbackGear = "⚙";
        public const string FallbackLock = "🔒";
        public const string FallbackShift = "🕒";

        public static void ApplyIcon(Label lbl, string mdlGlyph, string fallback, float size, Color color)
        {
            var fam = ResolveIconFamily();
            if (fam != null)
            {
                lbl.Font = new Font(fam, size, FontStyle.Regular);
                lbl.Text = mdlGlyph;
            }
            else
            {
                lbl.Font = new Font("Segoe UI Emoji", size, FontStyle.Regular);
                lbl.Text = fallback;
            }
            lbl.ForeColor = color;
            lbl.BackColor = Color.Transparent;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
        }

        public static Font IconFont(float size)
        {
            var fam = ResolveIconFamily();
            return fam != null
                ? new Font(fam, size, FontStyle.Regular, GraphicsUnit.Point)
                : new Font("Segoe UI Emoji", size);
        }

        // ====================================================================
        //  PALETTE
        // ====================================================================
        public static readonly Color ColorNavy = Color.FromArgb(23, 35, 46);
        public static readonly Color ColorNavyLight = Color.FromArgb(32, 47, 61);

        public static readonly Color ColorAccent = Color.FromArgb(232, 131, 58);
        public static readonly Color ColorAccentHover = Color.FromArgb(210, 112, 42);
        public static readonly Color ColorAccentPressed = Color.FromArgb(190, 98, 34);
        public static readonly Color ColorAccentDisabled = Color.FromArgb(240, 190, 150);

        public static readonly Color ColorTextDark = Color.FromArgb(28, 36, 48);
        public static readonly Color ColorTextSub = Color.FromArgb(107, 118, 134);
        public static readonly Color ColorWhiteSub = Color.FromArgb(176, 190, 204);

        public static readonly Color ColorFieldBg = Color.FromArgb(245, 246, 248);
        public static readonly Color ColorFieldBorder = Color.FromArgb(226, 230, 236);
        public static readonly Color ColorFieldIcon = Color.FromArgb(140, 150, 164);

        public static readonly Color ColorError = Color.FromArgb(214, 69, 69);
        public static readonly Color ColorOnline = Color.FromArgb(46, 184, 114);

        // Warna chip verified
        public static readonly Color ColorChipBg = Color.FromArgb(232, 246, 238);
        public static readonly Color ColorChipBorder = Color.FromArgb(181, 220, 196);
        public static readonly Color ColorChipText = Color.FromArgb(30, 130, 80);

        public const float SplitRatio = 0.46f;

        // ====================================================================
        //  FONT
        // ====================================================================
        public static Font FontBadge = new Font("Segoe UI Semibold", 8.5F, FontStyle.Regular);
        public static Font FontBigTitle = new Font("Segoe UI", 34F, FontStyle.Bold);
        public static Font FontLeftSubtitle = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        public static Font FontClock = new Font("Segoe UI Light", 30F, FontStyle.Regular);
        public static Font FontDate = new Font("Segoe UI", 10F, FontStyle.Regular);

        public static Font FontCardBrandTitle = new Font("Segoe UI", 13F, FontStyle.Bold);
        public static Font FontCardBrandSub = new Font("Segoe UI", 9F, FontStyle.Regular);
        public static Font FontCardTitle = new Font("Segoe UI", 24F, FontStyle.Bold);
        public static Font FontCardSubtitle = new Font("Segoe UI", 10F, FontStyle.Regular);

        public static Font FontLabel = new Font("Segoe UI Semibold", 8.5F, FontStyle.Regular);
        public static Font FontInput = new Font("Segoe UI", 11F, FontStyle.Regular);
        public static Font FontError = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        public static Font FontButton = new Font("Segoe UI Semibold", 12F, FontStyle.Regular);
        public static Font FontFooter = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        public static Font FontFooterSmall = new Font("Segoe UI", 7.5F, FontStyle.Regular);

        // Font chip
        public static Font FontChip = new Font("Segoe UI Semibold", 8.5F, FontStyle.Regular);

        // ====================================================================
        //  HELPER UMUM
        // ====================================================================
        public static void ApplyFormTheme(Form form)
        {
            form.Font = new Font("Segoe UI", 9F);
        }

        public static void EnableSmoothPaint(Control c)
        {
            typeof(Control).GetMethod("SetStyle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(c, new object[]
                {
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw,
                    true
                });
        }

        private static GraphicsPath RoundedRect(Rectangle b, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(b.Width, b.Height)));
            var path = new GraphicsPath();
            path.AddArc(b.X, b.Y, d, d, 180, 90);
            path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
            path.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            path.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ====================================================================
        //  BACKGROUND
        // ====================================================================
        public class BackgroundCache
        {
            public Bitmap Bitmap;

            public void Rebuild(int width, int height)
            {
                if (width <= 0 || height <= 0) return;

                Bitmap?.Dispose();
                Bitmap = new Bitmap(width, height);

                int leftW = Math.Max(1, (int)(width * SplitRatio));

                using (var g = Graphics.FromImage(Bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.White);

                    var leftRect = new Rectangle(0, 0, leftW, height);
                    using (var br = new LinearGradientBrush(leftRect, ColorNavyLight, ColorNavy, LinearGradientMode.Vertical))
                        g.FillRectangle(br, leftRect);

                    g.SetClip(leftRect);
                    using (var pen = new Pen(Color.FromArgb(14, 255, 255, 255), 1.5f))
                    {
                        for (int r = 160; r <= 520; r += 90)
                            g.DrawEllipse(pen, leftW - r, height - r, r * 2, r * 2);
                    }
                    g.ResetClip();

                    using (var accent = new SolidBrush(ColorAccent))
                        g.FillRectangle(accent, leftW - 4, 0, 4, height);
                }
            }
        }

        public static void StyleBackground(Panel bgPanel, BackgroundCache cache)
        {
            EnableSmoothPaint(bgPanel);
            bgPanel.Paint += (s, e) =>
            {
                var rect = bgPanel.ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0) return;

                if (cache.Bitmap == null || cache.Bitmap.Width != rect.Width || cache.Bitmap.Height != rect.Height)
                    cache.Rebuild(rect.Width, rect.Height);

                e.Graphics.DrawImageUnscaled(cache.Bitmap, 0, 0);
            };
        }

        // ====================================================================
        //  SISI KIRI
        // ====================================================================
        public static void StyleBadgePill(Panel pill, Label lblIcon, Label lblText)
        {
            pill.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, pill.Width - 1, pill.Height - 1);
                if (rect.Width <= 0 || rect.Height <= 0) return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(rect, rect.Height / 2))
                {
                    using (var bg = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
                        e.Graphics.FillPath(bg, path);
                    using (var border = new Pen(Color.FromArgb(120, ColorAccent)))
                        e.Graphics.DrawPath(border, path);
                }
            };

            ApplyIcon(lblIcon, IconLock, FallbackLock, 9.5F, ColorAccent);

            lblText.Font = FontBadge;
            lblText.ForeColor = Color.White;
            lblText.BackColor = Color.Transparent;
            lblText.TextAlign = ContentAlignment.MiddleLeft;
        }

        public static void StyleLeftTitle(Label lblTitle, Panel accentBar, Label lblSubtitle)
        {
            lblTitle.Font = FontBigTitle;
            lblTitle.ForeColor = Color.White;
            lblTitle.BackColor = Color.Transparent;

            accentBar.BackColor = ColorAccent;

            lblSubtitle.Font = FontLeftSubtitle;
            lblSubtitle.ForeColor = ColorWhiteSub;
            lblSubtitle.BackColor = Color.Transparent;
        }

        public static void StyleCrimpingGraphic(Panel panel)
        {
            panel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var pWhite = new Pen(Color.FromArgb(150, 255, 255, 255), 1.8f))
                using (var pCopper = new Pen(ColorAccent, 2f))
                using (var pStrand = new Pen(Color.FromArgb(200, ColorAccent), 1.2f))
                using (var fillIns = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                using (var fillBarr = new SolidBrush(Color.FromArgb(30, ColorAccent)))
                {
                    using (var path = RoundedRect(new Rectangle(2, 46, 110, 48), 10))
                    {
                        g.FillPath(fillIns, path);
                        g.DrawPath(pWhite, path);
                    }

                    for (int i = 0; i < 5; i++)
                    {
                        float y = 56 + i * 8f;
                        g.DrawLine(pStrand, 112, y, 176, y);
                    }

                    using (var path = RoundedRect(new Rectangle(176, 38, 96, 64), 8))
                    {
                        g.FillPath(fillBarr, path);
                        g.DrawPath(pCopper, path);
                    }
                    foreach (int x in new[] { 200, 224, 248 })
                        g.DrawLine(pCopper, x, 46, x, 94);

                    g.DrawRectangle(pCopper, 272, 60, 44, 20);
                    g.DrawEllipse(pCopper, 316, 50, 40, 40);
                    g.DrawEllipse(pCopper, 328, 62, 16, 16);
                }
            };
        }

        public static void StyleClock(Label lblClock, Label lblDate)
        {
            lblClock.Font = FontClock;
            lblClock.ForeColor = Color.White;
            lblClock.BackColor = Color.Transparent;

            lblDate.Font = FontDate;
            lblDate.ForeColor = ColorWhiteSub;
            lblDate.BackColor = Color.Transparent;
        }

        // ====================================================================
        //  SISI KANAN
        // ====================================================================
        public static void StyleBrandBadge(Panel badge, Label lblInitial)
        {
            badge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, badge.Width - 1, badge.Height - 1);
                using (var path = RoundedRect(rect, 14))
                using (var brush = new SolidBrush(ColorNavy))
                    e.Graphics.FillPath(brush, path);
            };
            lblInitial.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblInitial.ForeColor = ColorAccent;
            lblInitial.BackColor = Color.Transparent;
            lblInitial.TextAlign = ContentAlignment.MiddleCenter;
            lblInitial.Dock = DockStyle.Fill;
            badge.Invalidate();
        }

        public static void StyleCardBrand(Label lblTitle, Label lblSub)
        {
            lblTitle.Font = FontCardBrandTitle;
            lblTitle.ForeColor = ColorTextDark;
            lblTitle.BackColor = Color.Transparent;

            lblSub.Font = FontCardBrandSub;
            lblSub.ForeColor = ColorTextSub;
            lblSub.BackColor = Color.Transparent;
        }

        public static void StyleCardHeading(Label lblTitle, Label lblSubtitle)
        {
            lblTitle.Font = FontCardTitle;
            lblTitle.ForeColor = ColorTextDark;
            lblTitle.BackColor = Color.Transparent;

            lblSubtitle.Font = FontCardSubtitle;
            lblSubtitle.ForeColor = ColorTextSub;
            lblSubtitle.BackColor = Color.Transparent;
        }

        public static void StyleFieldLabel(Label lbl)
        {
            lbl.Font = FontLabel;
            lbl.ForeColor = ColorTextSub;
            lbl.BackColor = Color.Transparent;
        }

        public static void StyleErrorLabel(Label lbl)
        {
            lbl.Font = FontError;
            lbl.ForeColor = ColorError;
            lbl.BackColor = Color.Transparent;
            lbl.Text = "";
        }

        // ====================================================================
        //  HINT CHIP — badge verified
        // ====================================================================
        public static void StyleHintChip(Label lbl)
        {
            lbl.Font = FontChip;
            lbl.ForeColor = ColorChipText;
            lbl.BackColor = Color.White;   // solid white biar gak nembus apa pun di bawahnya
            lbl.AutoSize = false;
            lbl.Height = 26;
            lbl.Text = "";
            lbl.Visible = false;
            lbl.Padding = new Padding(0);
            lbl.TextAlign = ContentAlignment.MiddleLeft;

            lbl.Paint += (s, e) =>
            {
                if (string.IsNullOrEmpty(lbl.Text)) return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // clear background dulu (biar gak ada artefak dari label lain)
                using (var bgClear = new SolidBrush(lbl.BackColor))
                    g.FillRectangle(bgClear, lbl.ClientRectangle);

                // Background pill
                var rect = new Rectangle(0, 0, lbl.Width - 1, lbl.Height - 1);
                using (var path = RoundedRect(rect, rect.Height / 2))
                {
                    using (var bg = new SolidBrush(ColorChipBg))
                        g.FillPath(bg, path);
                    using (var border = new Pen(ColorChipBorder, 1f))
                        g.DrawPath(border, path);
                }

                // Lingkaran + centang
                int cx = 13;
                int cy = lbl.Height / 2;
                int cr = 7;

                using (var circleFill = new SolidBrush(ColorChipText))
                    g.FillEllipse(circleFill, cx - cr, cy - cr, cr * 2, cr * 2);

                using (var checkPen = new Pen(Color.White, 1.6f))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    g.DrawLines(checkPen, new[]
                    {
                        new PointF(cx - 3.2f, cy + 0.2f),
                        new PointF(cx - 1.0f, cy + 2.4f),
                        new PointF(cx + 3.2f, cy - 2.2f)
                    });
                }

                // Teks manual
                var textRect = new Rectangle(26, 0, lbl.Width - 26 - 12, lbl.Height);
                TextRenderer.DrawText(
                    g,
                    lbl.Text,
                    lbl.Font,
                    textRect,
                    ColorChipText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
        }

        public static void SetHintChip(Label lbl, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                ClearHintChip(lbl);
                return;
            }

            lbl.Text = text;

            int textW = TextRenderer.MeasureText(text, lbl.Font).Width;
            lbl.Width = textW + 26 + 12 + 6;

            lbl.Visible = true;
            lbl.BringToFront();
            lbl.Invalidate();
        }

        public static void ClearHintChip(Label lbl)
        {
            lbl.Text = "";
            lbl.Visible = false;
        }

        // ---------- Container field ----------
        private sealed class FieldState
        {
            public bool Focused;
            public bool HasError;
        }

        public const int FieldIconColumnWidth = 44;

        public static void StyleFieldContainer(Panel pnl, Label lblIcon, Control input, string mdlGlyph, string fallback)
        {
            var state = new FieldState();
            pnl.Tag = state;
            pnl.BackColor = Color.White;

            pnl.Paint += (s, e) =>
            {
                var rect = new Rectangle(1, 1, pnl.Width - 3, pnl.Height - 3);
                if (rect.Width <= 0 || rect.Height <= 0) return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Color borderColor = state.HasError ? ColorError
                                  : state.Focused ? ColorAccent
                                  : ColorFieldBorder;
                float borderWidth = (state.HasError || state.Focused) ? 1.8f : 1f;

                using (var path = RoundedRect(rect, 10))
                {
                    using (var fill = new SolidBrush(ColorFieldBg))
                        e.Graphics.FillPath(fill, path);
                    using (var pen = new Pen(borderColor, borderWidth))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            lblIcon.AutoSize = false;
            lblIcon.Location = new Point(8, 0);
            lblIcon.Size = new Size(FieldIconColumnWidth - 8, pnl.Height);
            ApplyIcon(lblIcon, mdlGlyph, fallback, 12F, ColorFieldIcon);

            input.Enter += (s, e) => { state.Focused = true; pnl.Invalidate(); };
            input.Leave += (s, e) => { state.Focused = false; pnl.Invalidate(); };
        }

        public static void SetFieldError(Panel pnl, Label lblError, string message)
        {
            var state = pnl.Tag as FieldState;
            if (state != null) state.HasError = true;
            lblError.Text = message;
            lblError.BringToFront();
            pnl.Invalidate();
        }

        public static void ClearFieldError(Panel pnl, Label lblError)
        {
            var state = pnl.Tag as FieldState;
            if (state != null) state.HasError = false;
            lblError.Text = "";
            pnl.Invalidate();
        }

        public static void StyleTextBoxFlat(Guna2TextBox tb, string placeholder = "")
        {
            var parent = tb.Parent;
            int h = parent != null ? parent.Height : 48;

            tb.BorderThickness = 0;
            tb.BorderRadius = 0;
            tb.FillColor = ColorFieldBg;
            tb.Font = FontInput;
            tb.ForeColor = ColorTextDark;
            tb.PlaceholderText = placeholder;
            tb.PlaceholderForeColor = Color.FromArgb(160, 166, 176);
            tb.PasswordChar = '\0';

            tb.Height = h - 12;
            tb.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tb.Location = new Point(FieldIconColumnWidth, (h - tb.Height) / 2);
            if (parent != null)
                tb.Width = parent.Width - FieldIconColumnWidth - 12;
        }

        public static void StyleComboBoxFlat(Guna2ComboBox cmb)
        {
            var parent = cmb.Parent;
            int h = parent != null ? parent.Height : 48;

            cmb.BorderThickness = 0;
            cmb.BorderRadius = 0;
            cmb.FillColor = ColorFieldBg;
            cmb.Font = FontInput;
            cmb.ForeColor = ColorTextDark;
            cmb.ItemHeight = 32;
            cmb.DropDownStyle = ComboBoxStyle.DropDownList;

            cmb.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmb.Location = new Point(FieldIconColumnWidth, (h - cmb.Height) / 2);
            if (parent != null)
                cmb.Width = parent.Width - FieldIconColumnWidth - 12;
        }

        public static void StylePrimaryButton(Guna2Button btn, string text = "LOGIN")
        {
            btn.Text = text;
            btn.FillColor = ColorAccent;
            btn.ForeColor = Color.White;
            btn.Font = FontButton;
            btn.BorderRadius = 12;
            btn.Animated = true;
            btn.HoverState.FillColor = ColorAccentHover;
            btn.PressedColor = ColorAccentPressed;
            btn.DisabledState.FillColor = ColorAccentDisabled;
            btn.DisabledState.ForeColor = Color.White;
            btn.Cursor = Cursors.Hand;
            btn.Enabled = true;
            btn.Visible = true;
        }

        public static void StyleFooter(Panel dot, Label lblStatus, Label lblCompany)
        {
            dot.BackColor = Color.Transparent;
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(ColorOnline))
                    e.Graphics.FillEllipse(br, 0, 0, dot.Width - 1, dot.Height - 1);
            };

            lblStatus.Font = FontFooter;
            lblStatus.ForeColor = ColorTextSub;
            lblStatus.BackColor = Color.Transparent;

            lblCompany.Font = FontFooterSmall;
            lblCompany.ForeColor = Color.FromArgb(170, 176, 188);
            lblCompany.BackColor = Color.Transparent;
        }
    }
}