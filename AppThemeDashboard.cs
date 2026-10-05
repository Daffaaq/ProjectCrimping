using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Drawing.Text;

namespace ProjectCrimping
{
    internal static class Gfx
    {
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawGlyph(Graphics g, string glyph, Font font, Rectangle r, Color color)
        {
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(color))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.DrawString(glyph, font, br, r, sf);
            }
        }
    }

    /// <summary>Panel transparan (supaya background gradient parent tetap kelihatan).</summary>
    internal class TransparentPanel : Panel
    {
        public TransparentPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }
    }

    /// <summary>Background sidebar: gradient navy + lingkaran dekoratif + garis aksen oranye (sama seperti login).</summary>
    internal class SidebarPanel : Panel
    {
        public SidebarPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new LinearGradientBrush(rect, AppThemeLogin.ColorNavyLight, AppThemeLogin.ColorNavy, LinearGradientMode.Vertical))
                g.FillRectangle(br, rect);

            using (var pen = new Pen(Color.FromArgb(14, 255, 255, 255), 1.5f))
            {
                for (int r = 160; r <= 520; r += 90)
                    g.DrawEllipse(pen, Width - r, Height - r, r * 2, r * 2);
            }

            using (var accent = new SolidBrush(AppThemeLogin.ColorAccent))
                g.FillRectangle(accent, Width - 4, 0, 4, Height);
        }
    }

    /// <summary>Panel dengan sudut membulat + dukungan border fokus.</summary>
    internal class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 14;
        public Color FillColor { get; set; } = Color.White;
        public Color BorderColor { get; set; } = Color.FromArgb(226, 230, 236);
        public Color FocusColor { get; set; } = Color.FromArgb(232, 131, 58);
        public bool Focused { get; set; } = false;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var path = Gfx.RoundRect(rect, Radius))
            using (var fill = new SolidBrush(FillColor))
            using (var pen = new Pen(Focused ? FocusColor : BorderColor, Focused ? 1.8f : 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Button flat dengan sudut membulat + ikon (Segoe MDL2 Assets).</summary>
    internal class RoundedButton : Button
    {
        public int Radius { get; set; } = 12;
        public Color FillColor { get; set; } = Color.White;
        public Color BorderColor { get; set; } = Color.FromArgb(226, 230, 236);
        public Color TextColor { get; set; } = Color.FromArgb(28, 36, 48);
        public Color IconColor { get; set; } = Color.FromArgb(232, 131, 58);
        public string IconGlyph { get; set; } = "";
        public bool LeftAligned { get; set; } = false;

        public bool Checked { get; set; } = false;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.RoundRect(rect, Radius))
            using (var fill = new SolidBrush(FillColor))
            using (var pen = new Pen(BorderColor))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            if (!string.IsNullOrEmpty(IconGlyph))
            {
                using (var iconFont = AppThemeLogin.IconFont(15f))
                {
                    var iconRect = LeftAligned ? new Rectangle(18, 0, 32, Height) : ClientRectangle;
                    Gfx.DrawGlyph(g, IconGlyph, iconFont, iconRect, IconColor);
                }
            }

            if (!string.IsNullOrEmpty(Text))
            {
                if (LeftAligned)
                {
                    var textRect = new Rectangle(62, 0, Width - 70, Height);
                    TextRenderer.DrawText(g, Text, Font, textRect, TextColor, flags | TextFormatFlags.Left);
                }
                else
                {
                    TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor,
                        flags | TextFormatFlags.HorizontalCenter);
                }
            }

            if (Checked && !string.IsNullOrEmpty(Text) && !LeftAligned)
            {
                int tw = TextRenderer.MeasureText(g, Text, Font).Width;
                float cx = (Width - tw) / 2f - 16;
                float cy = Height / 2f;
                using (var pen = new Pen(TextColor, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new[]
                    {
                        new PointF(cx - 5, cy),
                        new PointF(cx - 1, cy + 4),
                        new PointF(cx + 6, cy - 4)
                    });
                }
            }
        }
    }

    /// <summary>Avatar bulat dengan inisial (gaya badge "CS" di login).</summary>
    internal class AvatarCircle : Control
    {
        public string Initials { get; set; } = "";
        public Color RingColor { get; set; } = Color.FromArgb(232, 131, 58);
        public Color FillColor { get; set; } = Color.FromArgb(23, 35, 46);
        public Color InitialsColor { get; set; } = Color.FromArgb(232, 131, 58);

        public AvatarCircle()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(3, 3, Width - 7, Height - 7);
            using (var fill = new SolidBrush(FillColor))
            using (var pen = new Pen(RingColor, 3f))
            {
                g.FillEllipse(fill, rect);
                g.DrawEllipse(pen, rect);
            }
            TextRenderer.DrawText(g, Initials, Font, ClientRectangle, InitialsColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }

    internal class AccordionItem
    {
        public string Glyph;
        public string Label;
        public string Value;
    }

    /// <summary>Panel info yang bisa dibuka/ditutup (accordion), gaya kaca transparan di sidebar.</summary>
    internal class InfoAccordion : Control
    {
        private const int HeaderH = 46;
        private const int RowH = 46;
        private const int PadBottom = 8;

        private readonly List<AccordionItem> _items = new List<AccordionItem>();
        private readonly Timer _timer = new Timer { Interval = 15 };
        private readonly Font _fTitle = new Font("Segoe UI Semibold", 9.5f);
        private readonly Font _fLabel = new Font("Segoe UI", 7.5f);
        private readonly Font _fValue = new Font("Segoe UI Semibold", 10f);
        private readonly Font _fIcon = AppThemeLogin.IconFont(12f);
        private int _targetH;

        public string Title { get; set; } = "";
        public string TitleGlyph { get; set; } = "\uE946";
        public bool Expanded { get; private set; } = true;

        public InfoAccordion()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = HeaderH + PadBottom;
            _targetH = Height;
            _timer.Tick += (s, e) => Animate();
        }

        private int FullHeight { get { return HeaderH + _items.Count * RowH + PadBottom; } }

        public void AddItem(string glyph, string label, string value)
        {
            _items.Add(new AccordionItem { Glyph = glyph, Label = label, Value = value });
            if (Expanded) { _targetH = FullHeight; Height = FullHeight; }
            Invalidate();
        }

        /// <summary>Atur buka/tutup langsung tanpa animasi (dipakai saat awal dimuat).</summary>
        public void SetExpanded(bool expanded)
        {
            _timer.Stop();
            Expanded = expanded;
            _targetH = expanded ? FullHeight : HeaderH;
            Height = _targetH;
            Invalidate();
        }

        /// <summary>
        /// false (default) = buka/tutup langsung dalam satu kali gambar ulang (tanpa kedip).
        /// true = animasi geser; di WinForms ini memaksa layout ulang tiap ~15 ms di atas
        /// background gradient transparan sehingga cenderung berkedip.
        /// </summary>
        public bool Animated { get; set; } = false;

        public void Toggle()
        {
            if (!Animated)
            {
                SetExpanded(!Expanded);
                return;
            }

            Expanded = !Expanded;
            _targetH = Expanded ? FullHeight : HeaderH;
            _timer.Start();
            Invalidate();
        }

        private void Animate()
        {
            int diff = _targetH - Height;
            if (Math.Abs(diff) <= 3)
            {
                Height = _targetH;
                _timer.Stop();
                return;
            }
            int step = (int)(diff * 0.3);
            if (step == 0) step = diff > 0 ? 1 : -1;
            Height += step;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = e.Y <= HeaderH ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Y <= HeaderH) Toggle();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var path = Gfx.RoundRect(rect, 12))
            using (var fill = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            using (var pen = new Pen(Color.FromArgb(45, 255, 255, 255)))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            var center = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;
            var left = TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
                       TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            // --- header ---
            Gfx.DrawGlyph(g, TitleGlyph, _fIcon, new Rectangle(12, 0, 32, HeaderH), AppThemeLogin.ColorAccent);
            TextRenderer.DrawText(g, Title, _fTitle, new Rectangle(50, 0, Width - 50 - 36, HeaderH), Color.White, left);

            // chevron
            float cx = Width - 22, cy = HeaderH / 2f;
            using (var cp = new Pen(Color.FromArgb(200, 255, 255, 255), 1.8f))
            {
                cp.StartCap = LineCap.Round;
                cp.EndCap = LineCap.Round;
                cp.LineJoin = LineJoin.Round;
                if (Expanded)
                    g.DrawLines(cp, new[] { new PointF(cx - 5, cy + 2), new PointF(cx, cy - 3), new PointF(cx + 5, cy + 2) });
                else
                    g.DrawLines(cp, new[] { new PointF(cx - 5, cy - 2), new PointF(cx, cy + 3), new PointF(cx + 5, cy - 2) });
            }

            if (Height <= HeaderH + 4) return;

            // garis pemisah
            using (var sep = new Pen(Color.FromArgb(30, 255, 255, 255)))
                g.DrawLine(sep, 14, HeaderH, Width - 14, HeaderH);

            // --- isi ---
            for (int i = 0; i < _items.Count; i++)
            {
                int y = HeaderH + i * RowH;
                var it = _items[i];
                Gfx.DrawGlyph(g, it.Glyph, _fIcon, new Rectangle(12, y, 32, RowH), AppThemeLogin.ColorWhiteSub);
                TextRenderer.DrawText(g, it.Label, _fLabel, new Rectangle(50, y + 6, Width - 60, 14), AppThemeLogin.ColorWhiteSub, left);
                TextRenderer.DrawText(g, it.Value, _fValue, new Rectangle(50, y + 19, Width - 60, 22), Color.White, left);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _fTitle.Dispose(); _fLabel.Dispose(); _fValue.Dispose(); _fIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}