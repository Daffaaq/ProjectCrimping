using System;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormConfirmNG : Form
    {
        private NumericUpDown nudNG;
        private RoundedButton btnYes, btnNo;

        public bool HasNG { get; private set; }
        public int NGValue { get { return (int)nudNG.Value; } }

        public FormConfirmNG()
        {
            InitializeComponent();
            RoundedButton btnSave, btnCancel;

            // 2 baris: pertanyaan konfirmasi + input NG
            var body = AppThemePopup.BuildShell(
                this,
                "Konfirmasi NG",
                "Apakah ada NG pada N3?",
                2,
                out btnSave,
                out btnCancel
            );

            // Baris 1: tombol konfirmasi YA / TIDAK
            AppThemePopup.AddToggle(
                body,
                "ADA NG?",
                "YA, ADA NG",
                "TIDAK ADA NG",
                out btnYes,
                out btnNo
            );

            // Baris 2: input NG (default disabled, aktif kalau pilih YA)
            nudNG = AppThemePopup.AddInteger(body, "JUMLAH NG");

            nudNG.Enabled = false;
            HasNG = false;

            btnYes.Click += (s, e) =>
            {
                HasNG = true;
                AppThemePopup.SetToggleActive(btnYes, btnNo);
                nudNG.Enabled = true;
                nudNG.Focus();
            };

            btnNo.Click += (s, e) =>
            {
                HasNG = false;
                AppThemePopup.SetToggleActive(btnNo, btnYes);
                nudNG.Enabled = false;
                nudNG.Value = 0;
            };

            // Tombol Save dari BuildShell → OK
            btnSave.Click += (s, e) =>
            {
                // Kalau user bilang ada NG tapi jumlah 0 → warning
                if (HasNG && nudNG.Value <= 0)
                {
                    NotifPopup.Warning(
                        this,
                        "Jumlah NG Kosong",
                        "Kamu pilih ADA NG, tapi jumlah NG masih 0. " +
                        "Isi jumlah NG dulu ya."
                    );

                    nudNG.Focus();
                    return;
                }

                DialogResult = DialogResult.OK;
            };

            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
            };
        }
    }
}