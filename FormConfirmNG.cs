using System;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormConfirmNG : Form
    {
        private NumericUpDown nudNG;
        private RoundedButton btnYes, btnNo;

        // true setelah user menekan YA atau TIDAK (jawaban eksplisit)
        private bool _answered;

        // Nilai NG di-snapshot saat OK, supaya tetap aman dibaca
        // pemanggil walaupun form sudah di-dispose.
        private int _ngValue;

        public bool HasNG { get; private set; }

        /// <summary>Jumlah NG. Selalu 0 kalau HasNG = false.</summary>
        public int NGValue { get { return _ngValue; } }

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

            // Tanpa batas atas (default NumericUpDown hanya 100).
            // int.MaxValue karena NGValue bertipe int.
            nudNG.DecimalPlaces = 0;
            nudNG.Minimum = 0;
            nudNG.Maximum = int.MaxValue;

            nudNG.Enabled = false;
            HasNG = false;
            _answered = false;

            // Tombol minus tidak boleh diketik (jumlah NG tidak pernah negatif)
            nudNG.KeyPress += (s, e) =>
            {
                if (e.KeyChar == '-')
                    e.Handled = true;
            };

            btnYes.Click += (s, e) =>
            {
                _answered = true;
                HasNG = true;
                AppThemePopup.SetToggleActive(btnYes, btnNo);

                // Ada NG → minimal 1. Value otomatis naik ke 1 kalau masih 0,
                // dan angka 0 / minus yang diketik akan dikoreksi ke 1.
                nudNG.Minimum = 1;
                if (nudNG.Value < 1)
                    nudNG.Value = 1;

                nudNG.Enabled = true;
                nudNG.Focus();
                nudNG.Select(0, nudNG.Text.Length);   // blok angka, tinggal ketik
            };

            btnNo.Click += (s, e) =>
            {
                _answered = true;
                HasNG = false;
                AppThemePopup.SetToggleActive(btnNo, btnYes);

                // Tidak ada NG → nilai 0 harus valid lagi
                nudNG.Minimum = 0;
                nudNG.Value = 0;
                nudNG.Enabled = false;
            };

            // Tombol Save dari BuildShell → OK
            btnSave.Click += (s, e) =>
            {
                // User belum menjawab YA / TIDAK → jangan dianggap "tidak ada NG"
                if (!_answered)
                {
                    NotifPopup.Warning(
                        this,
                        "Belum Dipilih",
                        "Pilih dulu: YA, ADA NG atau TIDAK ADA NG."
                    );

                    return;
                }

                // Paksa NumericUpDown membaca angka yang baru diketik.
                // Tanpa ini, Value bisa masih nilai lama kalau user belum
                // meninggalkan field.
                ValidateChildren();

                // Jaring pengaman: kalau ada NG, jumlah wajib minimal 1
                if (HasNG && nudNG.Value < 1)
                {
                    NotifPopup.Warning(
                        this,
                        "Jumlah NG Tidak Valid",
                        "Kamu pilih ADA NG, jumlah NG minimal 1. " +
                        "Isi jumlah NG dulu ya."
                    );

                    nudNG.Focus();
                    return;
                }

                _ngValue = HasNG ? (int)nudNG.Value : 0;

                DialogResult = DialogResult.OK;
            };

            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
            };
        }
    }
}