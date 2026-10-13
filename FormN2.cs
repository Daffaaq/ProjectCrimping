using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormN2 : Form
    {
        private NumericUpDown nudCH, nudIH, nudCW, nudIW;

        public decimal CH { get { return nudCH.Value; } }
        public decimal IH { get { return nudIH.Value; } }
        public decimal CW { get { return nudCW.Value; } }
        public decimal IW { get { return nudIW.Value; } }

        public FormN2(decimal? initCH = null, decimal? initIH = null,
                      decimal? initCW = null, decimal? initIW = null)
        {
            InitializeComponent();
            RoundedButton btnSave, btnCancel;

            var body = AppThemePopup.BuildShell(
                this,
                "Input N2",
                "Isi data pengukuran N2",
                2,
                out btnSave,
                out btnCancel
            );

            AddDecimalRow(body, "C/H", out nudCH, "I/H", out nudIH);
            AddDecimalRow(body, "C/W", out nudCW, "I/W", out nudIW);

            // ===== Set nilai awal dari patokan (default 0 kalau NULL) =====
            nudCH.Value = Clamp(initCH ?? 0m, nudCH);
            nudIH.Value = Clamp(initIH ?? 0m, nudIH);
            nudCW.Value = Clamp(initCW ?? 0m, nudCW);
            nudIW.Value = Clamp(initIW ?? 0m, nudIW);

            btnSave.Click += (s, e) => { DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
        }

        private static decimal Clamp(decimal v, NumericUpDown nud)
        {
            if (v < nud.Minimum) return nud.Minimum;
            if (v > nud.Maximum) return nud.Maximum;
            return v;
        }

        private void AddDecimalRow(
            TableLayoutPanel body,
            string labelLeft, out NumericUpDown left,
            string labelRight, out NumericUpDown right,
            int decimals = 2)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var cellL = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 0,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 6, 0),
                Padding = Padding.Empty
            };
            cellL.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var cellR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 0,
                BackColor = Color.Transparent,
                Margin = new Padding(6, 0, 0, 0),
                Padding = Padding.Empty
            };
            cellR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            left = AppThemePopup.AddDecimal(cellL, labelLeft, decimals);
            right = AppThemePopup.AddDecimal(cellR, labelRight, decimals);

            row.Controls.Add(cellL, 0, 0);
            row.Controls.Add(cellR, 1, 0);

            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            body.RowCount = body.RowStyles.Count;
            body.Controls.Add(row, 0, body.RowCount - 1);
        }
    }
}