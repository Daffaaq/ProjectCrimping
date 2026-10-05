using System;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormN1 : Form
    {
        private NumericUpDown nudCH, nudIH, nudN1;
        private TextBox txtTerminal;
        private RoundedButton btnTermA, btnTermB;

        public decimal CH { get { return nudCH.Value; } }
        public decimal IH { get { return nudIH.Value; } }
        public decimal ValN1 { get { return nudN1.Value; } }
        public string Terminal { get; private set; }            // "A" atau "B"
        public string TerminalText { get { return txtTerminal.Text.Trim(); } }

        public FormN1()
        {
            InitializeComponent();
            RoundedButton btnSave, btnCancel;
            // 5 baris: CH, IH, N1, pilihan terminal, teks terminal
            var body = AppThemePopup.BuildShell(this, "Input N1", "Isi data pengukuran N1", 5, out btnSave, out btnCancel);

            nudCH = AppThemePopup.AddDecimal(body, "CH", 2);
            nudIH = AppThemePopup.AddDecimal(body, "IH", 2);
            nudN1 = AppThemePopup.AddDecimal(body, "N1", 2);
            AppThemePopup.AddToggle(body, "PILIH TERMINAL", "TERMINAL A", "TERMINAL B", out btnTermA, out btnTermB);
            txtTerminal = AppThemePopup.AddText(body, "SCAN / INPUT TERMINAL");

            // Belum ada pilihan di awal, user harus memilih A/B dulu
            Terminal = null;
            txtTerminal.Enabled = false;

            btnTermA.Click += (s, e) => SetTerminal("A");
            btnTermB.Click += (s, e) => SetTerminal("B");

            btnSave.Click += (s, e) => { DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
        }

        private void SetTerminal(string t)
        {
            Terminal = t;
            if (t == "A") AppThemePopup.SetToggleActive(btnTermA, btnTermB);
            else AppThemePopup.SetToggleActive(btnTermB, btnTermA);

            txtTerminal.Enabled = true;   // aktif setelah memilih A/B
            txtTerminal.Focus();          // langsung siap scan
        }
    }
}