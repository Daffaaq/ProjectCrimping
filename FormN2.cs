using System;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormN2 : Form
    {
        private NumericUpDown nudCH, nudIH;

        public decimal CH { get { return nudCH.Value; } }
        public decimal IH { get { return nudIH.Value; } }

        public FormN2()
        {
            InitializeComponent();
            RoundedButton btnSave, btnCancel;
            var body = AppThemePopup.BuildShell(this, "Input N2", "Isi data pengukuran N2", 2, out btnSave, out btnCancel);

            nudCH = AppThemePopup.AddDecimal(body, "CH", 2);
            nudIH = AppThemePopup.AddDecimal(body, "IH", 2);

            btnSave.Click += (s, e) => { DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
        }
    }
}