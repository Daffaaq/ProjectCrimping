using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormConfirmLogout : Form
    {
        public FormConfirmLogout()
        {
            InitializeComponent();

            RoundedButton btnLogout, btnCancel;
            var body = AppThemePopup.BuildShell(this, "Konfirmasi Logout", "Akhiri sesi kamu saat ini",
                                                0, out btnLogout, out btnCancel);
            ClientSize = new Size(440, 300);

            // Tombol aksi jadi merah
            btnLogout.Text = "YA, LOGOUT";
            btnLogout.FillColor = AppThemeLogin.ColorError;
            btnLogout.BorderColor = AppThemeLogin.ColorError;
            btnLogout.TextColor = Color.White;

            // Isi pesan
            var content = new MessagePanel { Dock = DockStyle.Fill };
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowCount = 1;
            body.Controls.Add(content, 0, 0);

            btnLogout.Click += (s, e) => { DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };

            ActiveControl = btnCancel;   // default aman: Enter = batal
        }

        // =====================================================
        //  Ikon bulat merah lembut + teks pertanyaan
        // =====================================================
        private class MessagePanel : Control
        {
            public MessagePanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(BackColor);

                Color err = AppThemeLogin.ColorError;

                // kartu lembut
                var card = new Rectangle(0, 8, Width - 1, Height - 20);
                using (var path = Round(card, 14))
                {
                    using (var br = new SolidBrush(Color.FromArgb(253, 240, 240))) g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(70, err), 1f)) g.DrawPath(pen, path);
                }

                // ikon bulat
                var badge = new Rectangle(20, card.Y + (card.Height - 60) / 2, 60, 60);
                using (var br = new SolidBrush(Color.FromArgb(32, err))) g.FillEllipse(br, badge);
                using (var pen = new Pen(err, 2f)) g.DrawEllipse(pen, badge);
                using (var f = new Font("Segoe MDL2 Assets", 20f))
                using (var br = new SolidBrush(err))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("\uE7E8", f, br, badge, sf);

                // teks
                float tx = badge.Right + 18;
                using (var f = new Font("Segoe UI Semibold", 13f))
                using (var br = new SolidBrush(AppThemePopup.TextDark))
                    g.DrawString("Yakin ingin logout?", f, br, tx, card.Y + 22);
                using (var f = new Font("Segoe UI", 10f))
                using (var br = new SolidBrush(AppThemePopup.TextSub))
                    g.DrawString("Sesi kamu akan diakhiri dan kamu kembali ke halaman login.",
                                 f, br, new RectangleF(tx, card.Y + 52, card.Right - tx - 14, 50));
            }

            private static GraphicsPath Round(Rectangle r, int radius)
            {
                int d = radius * 2;
                var p = new GraphicsPath();
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }
        }
    }
}