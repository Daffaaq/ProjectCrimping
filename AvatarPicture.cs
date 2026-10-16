using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProjectCrimping
{
    /// <summary>
    /// PictureBox yang menampilkan gambar dengan CLIP BULAT + ring tepi.
    /// Dipakai untuk foto profil operator di Dashboard.
    ///
    /// Kalau Image == null → hanya tampil lingkaran background + ring
    /// (bisa dianggap sebagai placeholder).
    ///
    /// Cara pakai:
    ///     var pic = new AvatarPicture
    ///     {
    ///         Size = new Size(92, 92),
    ///         Image = foto,
    ///         RingColor = AppThemeLogin.ColorAccent,
    ///         RingThickness = 3,
    ///         BackColor = AppThemeLogin.ColorNavy
    ///     };
    /// </summary>
    public class AvatarPicture : PictureBox
    {
        private Color _ringColor = Color.Orange;
        private int _ringThickness = 3;
        private Color _fillColor = Color.FromArgb(20, 32, 60);   // navy fallback

        public AvatarPicture()
        {
            // Supaya gambar kita digambar sendiri di OnPaint, bukan oleh base Paint.
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            BackColor = Color.Transparent;
        }

        /// <summary>Warna ring (border) di tepi lingkaran.</summary>
        public Color RingColor
        {
            get => _ringColor;
            set { _ringColor = value; Invalidate(); }
        }

        /// <summary>Tebal ring dalam pixel.</summary>
        public int RingThickness
        {
            get => _ringThickness;
            set { _ringThickness = Math.Max(0, value); Invalidate(); }
        }

        /// <summary>Warna isian lingkaran kalau Image kosong.</summary>
        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Kotak lingkaran: sisakan ruang untuk ring
            int ring = _ringThickness;
            var rect = new Rectangle(
                ring,
                ring,
                Width - ring * 2 - 1,
                Height - ring * 2 - 1);

            if (rect.Width <= 0 || rect.Height <= 0) return;

            // 1) Isian background lingkaran (kalau gambar belum/tidak ada)
            using (var brushBg = new SolidBrush(_fillColor))
            {
                g.FillEllipse(brushBg, rect);
            }

            // 2) Gambar (kalau ada), di-clip bulat
            if (Image != null)
            {
                var oldClip = g.Clip;
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(rect);
                    g.SetClip(path);

                    // Hitung ukuran gambar supaya cover penuh lingkaran
                    // (mirip PictureBoxSizeMode.Zoom, tapi cover)
                    Rectangle imgRect = FitImageCover(Image, rect);

                    g.DrawImage(Image, imgRect);

                    g.Clip = oldClip;
                }
            }

            // 3) Ring di tepi
            if (_ringThickness > 0)
            {
                using (var pen = new Pen(_ringColor, _ringThickness))
                {
                    // Perlu inset setengah tebal pen supaya ring tidak kepotong
                    float inset = _ringThickness / 2f;
                    var ringRect = new RectangleF(
                        inset,
                        inset,
                        Width - _ringThickness,
                        Height - _ringThickness);

                    g.DrawEllipse(pen, ringRect);
                }
            }
        }

        /// <summary>
        /// Hitung rectangle gambar supaya "cover" area target
        /// (bukan fit), jadi tidak ada celah kosong.
        /// </summary>
        private static Rectangle FitImageCover(Image img, Rectangle target)
        {
            float targetRatio = (float)target.Width / target.Height;
            float imgRatio = (float)img.Width / img.Height;

            int w, h;
            if (imgRatio > targetRatio)
            {
                // Gambar lebih lebar → tinggi pas, lebar dipotong
                h = target.Height;
                w = (int)(h * imgRatio);
            }
            else
            {
                // Gambar lebih tinggi → lebar pas, tinggi dipotong
                w = target.Width;
                h = (int)(w / imgRatio);
            }

            int x = target.X + (target.Width - w) / 2;
            int y = target.Y + (target.Height - h) / 2;

            return new Rectangle(x, y, w, h);
        }
    }
}