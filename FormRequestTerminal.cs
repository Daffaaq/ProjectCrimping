using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public partial class FormRequestTerminal : Form
    {
        // ==================== CONTROL ====================
        private TextBox txtWos;
        private Label lblStatus, lblDataA, lblDataB;
        private RoundedButton btnTermA, btnTermB;
        private RoundedButton _btnSave;
        private bool useA, useB;

        // ==================== STATE (hasil scan) ====================
        private string scannedWOS = "";
        private string lotNo = "";
        private string seqNo = "";
        private string dataTerminalA = "";
        private string dataTerminalB = "";
        private string selectedTerminal = "";

        private const string EMPTY_DATA = "----------";

        // Guard supaya scan / simpan tidak bisa jalan dobel
        private bool _busy;

        // Koneksi dibuat sekali, dipakai ulang (konsisten dengan Login)
        private readonly DatabaseHelper _db = new DatabaseHelper(DbProvider.SqlServer);

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

            _btnSave = btnSave;

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

            AppThemePopup.SetCheck(btnTermA, "TERMINAL A", false);
            AppThemePopup.SetCheck(btnTermB, "TERMINAL B", false);

            // Toggle terminal A / B
            // Hanya boleh memilih salah satu
            btnTermA.Click += (s, e) => ToggleTerminal(true);
            btnTermB.Click += (s, e) => ToggleTerminal(false);

            // Teks WOS diubah setelah scan → hasil scan lama tidak berlaku lagi
            txtWos.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(scannedWOS) && scannedWOS != Wos)
                {
                    ClearScanResult();
                    SetStatus("Menunggu scan WOS...", true);
                }
            };

            // Enter dari scanner → cek WOS ke database
            txtWos.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;

                e.Handled = e.SuppressKeyPress = true;

                await ScanWOSAsync();
            };

            btnSave.Click += async (s, e) => await ButtonOk_ClickAsync();

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

        // ==================== WOS PARSING ====================

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
                lot = wos.Substring(0, lastDotIndex).Trim();
                seq = wos.Substring(lastDotIndex + 1).Trim();

                return
                    !string.IsNullOrEmpty(lot) &&
                    !string.IsNullOrEmpty(seq);
            }

            return false;
        }

        // ==================== DATABASE: QUERY TERMINAL ====================

        // Dijalankan di background thread (Task.Run).
        // Tidak menyentuh UI sama sekali.
        //
        // CATATAN: TOP 1 tanpa ORDER BY. Kalau satu lotno + seqnoint bisa
        // punya lebih dari satu baris dan hasilnya harus konsisten,
        // tambahkan ORDER BY sesuai kolom yang kamu mau.
        private (bool Found, string A, string B) QueryTerminal(string lot, int seq)
        {
            using (var conn = (SqlConnection)_db.Open())
            using (var cmd = new SqlCommand(
                @"SELECT TOP 1
                      terminala,
                      terminalb
                  FROM trrphmesin
                  WHERE lotno = @lotNo
                    AND seqnoint = @seqNo",
                conn))
            {
                cmd.CommandTimeout = 10;

                cmd.Parameters.Add("@lotNo", SqlDbType.VarChar, 50).Value = lot;
                cmd.Parameters.Add("@seqNo", SqlDbType.Int).Value = seq;

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return (false, "", "");

                    string a = (Convert.ToString(reader["terminala"]) ?? "").Trim();
                    string b = (Convert.ToString(reader["terminalb"]) ?? "").Trim();

                    return (true, a, b);
                }
            }
        }

        // ==================== SCAN WOS ====================

        private async Task ScanWOSAsync()
        {
            if (_busy) return;
            _busy = true;

            try
            {
                string wos = txtWos.Text.Trim();

                // Setiap scan baru: reset hasil sebelumnya
                ClearScanResult();

                // Validasi: WOS tidak boleh kosong
                if (string.IsNullOrEmpty(wos))
                {
                    SetStatus("Scan WOS dulu!", false);
                    return;
                }

                // Parse WOS menjadi LOTNO dan SEQNO
                string lot, seq;
                if (!ParseWOS(wos, out lot, out seq))
                {
                    SetStatus("Format: LOTNO.SEQNO", false);

                    NotifPopup.Warning(
                        this,
                        "Format WOS Salah",
                        "Input: '" + wos + "'\n" +
                        "Format yang benar: LOTNO.SEQNO (contoh: LOT001.1)\n\n" +
                        "Pastikan ada tanda titik (.) sebagai pemisah."
                    );

                    return;
                }

                // SEQNO harus angka (kolom seqnoint bertipe integer)
                int seqInt;
                if (!int.TryParse(seq, NumberStyles.None, CultureInfo.InvariantCulture, out seqInt))
                {
                    SetStatus("SEQNO harus berupa angka", false);

                    NotifPopup.Warning(
                        this,
                        "Format WOS Salah",
                        "Input: '" + wos + "'\n" +
                        "Bagian setelah titik (SEQNO) harus berupa angka.\n" +
                        "Contoh: LOT001.1"
                    );

                    return;
                }

                // Query di background thread supaya UI tidak freeze
                (bool Found, string A, string B) result;

                try
                {
                    result = await Task.Run(() => QueryTerminal(lot, seqInt));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[SCAN WOS] " + ex);

                    if (IsDisposed || IsWosStale(wos))
                        return;

                    SetStatus("Gagal terhubung ke database, coba lagi", false);

                    NotifPopup.Error(
                        this,
                        "Database Error",
                        "Gagal mengambil data terminal.\n" +
                        "Cek koneksi database lalu scan ulang."
                    );

                    return;
                }

                // Form sudah ditutup / WOS sudah diganti selama query → buang hasil lama
                if (IsDisposed || IsWosStale(wos))
                    return;

                // Tidak ketemu
                if (!result.Found)
                {
                    SetStatus(
                        "Data terminal tidak ditemukan, silakan cek ulang WOS",
                        false
                    );

                    return;
                }

                lotNo = lot;
                seqNo = seq;
                dataTerminalA = result.A;
                dataTerminalB = result.B;

                // Tampilkan data terminal
                SetData(dataTerminalA, dataTerminalB);

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
                    "LOT " + lotNo + "." + seqNo + " ketemu, pilih terminal",
                    true
                );
            }
            finally
            {
                _busy = false;
            }
        }

        private bool IsWosStale(string wos)
        {
            return !string.Equals(txtWos.Text.Trim(), wos, StringComparison.Ordinal);
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

            AppThemePopup.SetCheck(btnTermA, "TERMINAL A", false);
            AppThemePopup.SetCheck(btnTermB, "TERMINAL B", false);

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

                AppThemePopup.SetCheck(btnTermA, "TERMINAL A", true);
                AppThemePopup.SetCheck(btnTermB, "TERMINAL B", false);
            }
            else
            {
                // Pilih Terminal B
                useA = false;
                useB = true;

                // Simpan VALUE asli terminal B
                selectedTerminal = dataTerminalB;

                AppThemePopup.SetCheck(btnTermA, "TERMINAL A", false);
                AppThemePopup.SetCheck(btnTermB, "TERMINAL B", true);
            }

            UpdateSelectionStatus();
        }

        // Update status sesuai terminal yang dipilih
        private void UpdateSelectionStatus()
        {
            if (useA)
            {
                SetStatus("Terminal A dipilih, klik OK", true);
            }
            else if (useB)
            {
                SetStatus("Terminal B dipilih, klik OK", true);
            }
            else if (!string.IsNullOrEmpty(scannedWOS))
            {
                SetStatus(
                    "LOT " + lotNo + "." + seqNo + " ketemu, pilih terminal",
                    true
                );
            }
            else
            {
                SetStatus("Menunggu scan WOS...", true);
            }
        }

        // ==================== SAVE ====================

        private async Task ButtonOk_ClickAsync()
        {
            // Sedang scan / sedang menyimpan → abaikan
            if (_busy) return;
            _busy = true;
            _btnSave.Enabled = false;

            try
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

                // Session harus valid, jangan simpan data "Error" ke database
                if (string.IsNullOrEmpty(Session.mesin) ||
                    string.IsNullOrEmpty(Session.Noreg))
                {
                    NotifPopup.Error(
                        this,
                        "Error",
                        "Session login tidak valid, silakan login ulang."
                    );

                    return;
                }

                // Ambil nilai ke variabel lokal sebelum masuk background thread
                string mesinSession = Session.mesin;
                string noregSession = Session.Noreg;
                string terminal = selectedTerminal;
                string wos = scannedWOS;

                if (!NotifPopup.Confirm(
                    this,
                    "Simpan Request?",
                    "WOS: " + wos +
                    "\nTerminal: " + terminal,
                    "SIMPAN",
                    "BATAL"))
                {
                    return;
                }

                try
                {
                    int rowsAffected = await Task.Run(() =>
                        InsertRequest(mesinSession, terminal, noregSession, wos));

                    // Form sudah ditutup (mis. klik Batal) selama proses simpan
                    if (IsDisposed)
                        return;

                    if (rowsAffected > 0)
                    {
                        NotifPopup.Success(
                            this,
                            "Berhasil",
                            "Request " + terminal + " berhasil disimpan!"
                        );

                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        // 0 baris = sudah ada request pending untuk WOS + terminal ini
                        SetStatus(
                            "Request " + terminal + " untuk WOS ini sudah ada",
                            false
                        );

                        NotifPopup.Warning(
                            this,
                            "Request Sudah Ada",
                            "Request " + terminal + " untuk WOS " + wos +
                            " sudah dikirim dan masih menunggu diproses.\n\n" +
                            "Tidak perlu mengirim ulang."
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[SAVE REQUEST] " + ex);

                    if (IsDisposed)
                        return;

                    NotifPopup.Error(
                        this,
                        "Error",
                        "Gagal menyimpan request.\n" +
                        "Cek koneksi database lalu coba lagi."
                    );
                }
            }
            finally
            {
                _busy = false;

                if (!IsDisposed)
                    _btnSave.Enabled = true;
            }
        }

        // ==================== SAVE TO DATABASE ====================

        // Simpan ke tabel ReqTerminal
        // Kolom: mesin, terminal, noreg, wos, status, created_date
        //
        // Anti duplikat: INSERT hanya jalan kalau BELUM ada request dengan
        // wos + terminal yang sama dan status = 0 (masih menunggu).
        // Pengecekan dan insert dilakukan dalam SATU statement dengan
        // UPDLOCK + HOLDLOCK, jadi aman walau diklik dua kali, atau dikirim
        // bersamaan dari dua komputer.
        //
        // Return: 1 = tersimpan, 0 = ditolak karena sudah ada yang pending.
        //
        // Dijalankan di background thread. Tidak menyentuh UI.
        private int InsertRequest(
            string mesinValue,
            string terminalValue,
            string noregValue,
            string wosValue)
        {
            using (var conn = (SqlConnection)_db.Open())
            using (var cmd = new SqlCommand(
                @"INSERT INTO ReqTerminal
                      (
                          mesin,
                          terminal,
                          noreg,
                          wos,
                          status,
                          created_date
                      )
                  SELECT
                          @mesin,
                          @terminal,
                          @noreg,
                          @wos,
                          @status,
                          GETDATE()
                  WHERE NOT EXISTS
                      (
                          SELECT 1
                          FROM   ReqTerminal WITH (UPDLOCK, HOLDLOCK)
                          WHERE  wos      = @wos
                            AND  terminal = @terminal
                            AND  status   = 0
                      )",
                conn))
            {
                cmd.CommandTimeout = 10;

                cmd.Parameters.Add("@mesin", SqlDbType.VarChar, 100).Value = mesinValue;
                cmd.Parameters.Add("@terminal", SqlDbType.VarChar, 100).Value = terminalValue;
                cmd.Parameters.Add("@noreg", SqlDbType.VarChar, 100).Value = noregValue;
                cmd.Parameters.Add("@wos", SqlDbType.VarChar, 100).Value = wosValue;
                cmd.Parameters.Add("@status", SqlDbType.Int).Value = 0;

                return cmd.ExecuteNonQuery();
            }
        }
    }
}