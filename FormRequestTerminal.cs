using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormRequestTerminal : Form
    {
        // ==================== CONTROL ====================
        private TextBox txtWos;
        private Label lblStatus, lblDataA, lblDataB;
        private RoundedButton btnTermA, btnTermB;
        private bool useA, useB;

        // ==================== STATE (hasil scan) ====================
        private string scannedWOS = "";
        private string lotNo = "";
        private string seqNo = "";
        private string dataTerminalA = "";
        private string dataTerminalB = "";
        private string selectedTerminal = "";

        private const string EMPTY_DATA = "----------";

        // ==================== SESSION (diteruskan ke Session) ====================
        public static string mesin
        {
            get { return Session.mesin; }
            set { Session.mesin = value; }
        }

        public static string Noreg
        {
            get { return Session.Noreg; }
            set { Session.Noreg = value; }
        }

        // ==================== PROPERTIES UNTUK FORM INDUK ====================
        public string Wos
        {
            get { return txtWos.Text.Trim(); }
        }

        public bool UseTerminalA
        {
            get { return useA; }
        }

        public bool UseTerminalB
        {
            get { return useB; }
        }

        // ==================== CONSTRUCTOR ====================
        public FormRequestTerminal()
        {
            InitializeComponent();

            RoundedButton btnSave, btnCancel;

            // 4 baris: WOS, status, checkbox A/B, data A/B
            var body = AppThemePopup.BuildShell(
                this,
                "Request Terminal",
                "Scan WOS lalu pilih terminal",
                4,
                out btnSave,
                out btnCancel
            );

            txtWos = AppThemePopup.AddText(body, "SCAN WOS");

            lblStatus = AppThemePopup.AddStatus(
                body,
                "STATUS",
                "Menunggu scan WOS..."
            );

            AppThemePopup.AddCheckPair(
                body,
                "PILIH TERMINAL",
                "TERMINAL A",
                "TERMINAL B",
                out btnTermA,
                out btnTermB
            );

            AppThemePopup.AddDataPair(
                body,
                "DATA TERMINAL",
                out lblDataA,
                out lblDataB
            );

            AppThemePopup.SetCheck(
                btnTermA,
                "TERMINAL A",
                false
            );

            AppThemePopup.SetCheck(
                btnTermB,
                "TERMINAL B",
                false
            );

            // Toggle terminal A / B
            // Hanya boleh memilih salah satu
            btnTermA.Click += (s, e) => ToggleTerminal(true);
            btnTermB.Click += (s, e) => ToggleTerminal(false);

            // Enter dari scanner → cek WOS ke database
            txtWos.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;

                e.Handled = e.SuppressKeyPress = true;

                ScanWOS();
            };

            btnSave.Click += ButtonOk_Click;

            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
            };

            ActiveControl = txtWos;
        }

        // ==================== PUBLIC HELPER (FE) ====================

        /// <summary>
        /// Ubah teks status: ok = hijau, tidak ok = merah.
        /// </summary>
        public void SetStatus(string text, bool ok)
        {
            lblStatus.Text = text;

            lblStatus.ForeColor = ok
                ? AppThemeLogin.ColorOnline
                : AppThemeLogin.ColorError;
        }

        /// <summary>
        /// Isi kotak data di bawah checkbox.
        /// </summary>
        public void SetData(string a, string b)
        {
            lblDataA.Text = string.IsNullOrEmpty(a)
                ? EMPTY_DATA
                : a;

            lblDataB.Text = string.IsNullOrEmpty(b)
                ? EMPTY_DATA
                : b;
        }

        // ==================== DATABASE ====================

        private string GetConnectionStringSqlServer()
        {
            return new DatabaseHelper(
                DbProvider.SqlServer
            ).ConnectionString;
        }

        // ==================== WOS PARSING & SCANNING ====================

        // Format: LOTNO.SEQNO
        // contoh: LOT001.1
        // titik terakhir sebagai pemisah
        private bool ParseWOS(
            string wos,
            out string lot,
            out string seq)
        {
            lot = "";
            seq = "";

            if (string.IsNullOrEmpty(wos))
                return false;

            wos = wos.Trim();

            int lastDotIndex = wos.LastIndexOf('.');

            if (lastDotIndex > 0 &&
                lastDotIndex < wos.Length - 1)
            {
                lot = wos.Substring(
                    0,
                    lastDotIndex
                ).Trim();

                seq = wos.Substring(
                    lastDotIndex + 1
                ).Trim();

                return
                    !string.IsNullOrEmpty(lot) &&
                    !string.IsNullOrEmpty(seq);
            }

            return false;
        }

        private void ScanWOS()
        {
            string wos = txtWos.Text.Trim();

            // Setiap scan baru: reset hasil sebelumnya
            ClearScanResult();

            // Validasi: WOS tidak boleh kosong
            if (string.IsNullOrEmpty(wos))
            {
                SetStatus(
                    "Scan WOS dulu!",
                    false
                );

                return;
            }

            // Parse WOS menjadi LOTNO dan SEQNO
            if (!ParseWOS(
                    wos,
                    out lotNo,
                    out seqNo))
            {
                SetStatus(
                    "Format: LOTNO.SEQNO",
                    false
                );

                NotifPopup.Warning(
                    this,
                    "Format WOS Salah",
                    "Input: '" + wos + "'\n" +
                    "Format yang benar: LOTNO.SEQNO (contoh: LOT001.1)\n\n" +
                    "Pastikan ada tanda titik (.) sebagai pemisah."
                );

                return;
            }

            // Query data dari tabel trrphmesin
            // tanggalhasil IS NULL = belum selesai
            try
            {
                using (SqlConnection conn =
                    new SqlConnection(
                        GetConnectionStringSqlServer()))
                {
                    conn.Open();

                    string query = @"
                        SELECT TOP 1
                            terminala,
                            terminalb
                        FROM trrphmesin
                        WHERE lotno = @lotNo
                          AND seqnoint = @seqNo
                          AND tanggalhasil IS NULL";

                    using (SqlCommand cmd =
                        new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@lotNo",
                            lotNo
                        );

                        cmd.Parameters.AddWithValue(
                            "@seqNo",
                            seqNo
                        );

                        using (SqlDataReader reader =
                            cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                // Tidak ketemu atau sudah selesai
                                SetStatus(
                                    "WOS ga ketemu / udah selesai",
                                    false
                                );

                                return;
                            }

                            dataTerminalA =
                                Convert.ToString(
                                    reader["terminala"]
                                ) ?? "";

                            dataTerminalB =
                                Convert.ToString(
                                    reader["terminalb"]
                                ) ?? "";
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                NotifPopup.Error(
                    this,
                    "Database Error",
                    sqlEx.Message +
                    "\n(Error No: " +
                    sqlEx.Number +
                    ")\n\n" +
                    "Cek koneksi database dan tabel trrphmesin."
                );

                SetStatus(
                    "Error: " + sqlEx.Message,
                    false
                );

                return;
            }
            catch (Exception ex)
            {
                NotifPopup.Error(
                    this,
                    "Error",
                    "Error saat scan WOS:\n\n" +
                    ex.Message
                );

                SetStatus(
                    "Error: " + ex.Message,
                    false
                );

                return;
            }

            // Tampilkan data terminal
            SetData(
                dataTerminalA,
                dataTerminalB
            );

            if (IsEmptyData(dataTerminalA) &&
                IsEmptyData(dataTerminalB))
            {
                SetStatus(
                    "Data tidak ditemukan untuk WOS: " + wos,
                    false
                );

                return;
            }

            // WOS berhasil ditemukan
            scannedWOS = wos;

            SetStatus(
                "LOT " + lotNo + "." + seqNo +
                " ketemu, pilih terminal",
                true
            );
        }

        private static bool IsEmptyData(string data)
        {
            return string.IsNullOrWhiteSpace(data) ||
                   data == EMPTY_DATA;
        }

        // Reset hasil scan + pilihan terminal
        private void ClearScanResult()
        {
            scannedWOS = "";

            lotNo = "";
            seqNo = "";

            dataTerminalA = "";
            dataTerminalB = "";

            selectedTerminal = "";

            useA = false;
            useB = false;

            AppThemePopup.SetCheck(
                btnTermA,
                "TERMINAL A",
                false
            );

            AppThemePopup.SetCheck(
                btnTermB,
                "TERMINAL B",
                false
            );

            SetData("", "");
        }
        // ==================== TERMINAL SELECTION ====================

        private void ToggleTerminal(bool isA)
        {
            // Data terminal yang mau dipilih
            string data = isA
                ? dataTerminalA
                : dataTerminalB;

            // Kalau data terminal kosong
            if (IsEmptyData(data))
            {
                SetStatus(
                    "Data " +
                    (isA ? "Terminal A" : "Terminal B") +
                    " tidak tersedia!",
                    false
                );

                return;
            }

            if (isA)
            {
                // Pilih Terminal A
                useA = true;
                useB = false;

                // Simpan VALUE asli terminal A
                selectedTerminal = dataTerminalA;

                AppThemePopup.SetCheck(
                    btnTermA,
                    "TERMINAL A",
                    true
                );

                AppThemePopup.SetCheck(
                    btnTermB,
                    "TERMINAL B",
                    false
                );
            }
            else
            {
                // Pilih Terminal B
                useA = false;
                useB = true;

                // Simpan VALUE asli terminal B
                selectedTerminal = dataTerminalB;

                AppThemePopup.SetCheck(
                    btnTermA,
                    "TERMINAL A",
                    false
                );

                AppThemePopup.SetCheck(
                    btnTermB,
                    "TERMINAL B",
                    true
                );
            }

            UpdateSelectionStatus();
        }

        // Update status sesuai terminal yang dipilih
        private void UpdateSelectionStatus()
        {
            if (useA)
            {
                SetStatus(
                    "Terminal A dipilih, klik OK",
                    true
                );
            }
            else if (useB)
            {
                SetStatus(
                    "Terminal B dipilih, klik OK",
                    true
                );
            }
            else if (!string.IsNullOrEmpty(scannedWOS))
            {
                SetStatus(
                    "LOT " +
                    lotNo +
                    "." +
                    seqNo +
                    " ketemu, pilih terminal",
                    true
                );
            }
            else
            {
                SetStatus(
                    "Menunggu scan WOS...",
                    true
                );
            }
        }

        // ==================== SAVE ====================

        private void ButtonOk_Click(
            object sender,
            EventArgs e)
        {
            // WOS harus sudah di-scan
            // dan teks belum diubah sesudah scan
            if (string.IsNullOrEmpty(scannedWOS) ||
                scannedWOS != Wos)
            {
                NotifPopup.Warning(
                    this,
                    "Peringatan",
                    "Silakan scan WOS terlebih dahulu!"
                );

                txtWos.Focus();

                return;
            }

            // Harus memilih salah satu terminal
            if (!useA && !useB)
            {
                NotifPopup.Warning(
                    this,
                    "Peringatan",
                    "Silakan pilih Terminal A atau Terminal B!"
                );

                return;
            }

            // Hanya satu yang bisa aktif
            string terminal = selectedTerminal;

            if (NotifPopup.Confirm(
                this,
                "Simpan Request?",
                "WOS: " +
                scannedWOS +
                "\nTerminal: " +
                terminal,
                "SIMPAN",
                "BATAL"))
            {
                SaveToDatabase();
            }
        }

        // ==================== SAVE TO DATABASE ====================

        // Simpan ke tabel ReqTerminal
        // Kolom: mesin, terminal, noreg, wos, status, created_date
        private void SaveToDatabase()
        {
            try
            {
                string mesinSession =
                    Session.mesin ?? "Error";

                string noregSession =
                    Session.Noreg ?? "Error";

                // Terminal yang dipilih user
                string terminal = selectedTerminal;

                // WOS yang sudah di-scan & tervalidasi
                string wos = scannedWOS;

                using (SqlConnection conn =
                    new SqlConnection(
                        GetConnectionStringSqlServer()))
                {
                    conn.Open();

                    string query = @"
                        INSERT INTO ReqTerminal
                            (
                                mesin,
                                terminal,
                                noreg,
                                wos,
                                status,
                                created_date
                            )
                        VALUES
                            (
                                @mesin,
                                @terminal,
                                @noreg,
                                @wos,
                                @status,
                                GETDATE()
                            )";

                    using (SqlCommand cmd =
                        new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@mesin",
                            mesinSession
                        );

                        cmd.Parameters.AddWithValue(
                            "@terminal",
                            terminal
                        );

                        cmd.Parameters.AddWithValue(
                            "@noreg",
                            noregSession
                        );

                        cmd.Parameters.AddWithValue(
                            "@wos",
                            wos
                        );

                        cmd.Parameters.AddWithValue(
                            "@status",
                            0
                        );

                        int rowsAffected =
                            cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            NotifPopup.Success(
                                this,
                                "Berhasil",
                                "Request " +
                                terminal +
                                " berhasil disimpan!"
                            );

                            DialogResult =
                                DialogResult.OK;

                            Close();
                        }
                        else
                        {
                            NotifPopup.Error(
                                this,
                                "Gagal",
                                "Gagal menyimpan data!"
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NotifPopup.Error(
                    this,
                    "Error",
                    ex.Message
                );
            }
        }
    }
}