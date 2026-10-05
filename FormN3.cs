using System;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormN3 : Form
    {
        private NumericUpDown nudCH, nudIH, nudNG;

        public decimal CH { get { return nudCH.Value; } }
        public decimal IH { get { return nudIH.Value; } }
        public int NG { get { return (int)nudNG.Value; } }

        public FormN3()
        {
            InitializeComponent();
            RoundedButton btnSave, btnCancel;
            var body = AppThemePopup.BuildShell(this, "Input N3", "Isi data pengukuran N3", 3, out btnSave, out btnCancel);

            nudCH = AppThemePopup.AddDecimal(body, "CH", 2);
            nudIH = AppThemePopup.AddDecimal(body, "IH", 2);
            nudNG = AppThemePopup.AddInteger(body, "NG");

            btnSave.Click += (s, e) => { DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
        }
    }
}