using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Data.SqlClient;

namespace ProjectCrimping
{
    public partial class FormMaintenanceCall : Form
    {
        // ==================== KARTU & CONTROL ====================
        private CallCard cardSetter, cardMaint, cardWip;
        private TextBox txtPassword, txtKet;
        private ComboBox cmbProblem, cmbJenis, cmbExtra1, cmbExtra2;
        private StatsCard stats;
        private RoundedButton btnOkPassword, btnFinish;

        // Layout body + referensi untuk hide/show (row ikut collapse)
        private TableLayoutPanel body;
        private Control passwordRow;
        private FieldCard fieldPassword, fieldKeterangan;
        private BareCard cardExtra1, cardExtra2;

        // Tinggi tiap baris body (index = nomor row)
        private const int ROW_CALLS = 130;
        private const int ROW_PASSWORD = 66;
        private const int ROW_PAIR = 66;
        private const int ROW_KET = 66;
        private const int ROW_EXTRA = 56;
        private const int ROW_STATS = 78;

        // ==================== TIMER ====================
        private System.Windows.Forms.Timer clock;     // update jam kanan
        private System.Windows.Forms.Timer timer1;    // hitung durasi

        // ==================== STATE ====================
        private Form parentForm;
        private string currentCallID = "";
        private string loggedInNoreg = "";
        private int countimer = 0;
        private bool isFinished = false;
        private bool isProblemActive = false;
        private bool _isUpdatingComboBox = false;
        private bool _isCalling = false;              // sudah ada panggilan aktif?
        private DateTime _waktuMulaiDowntime;
        private TimeSpan _serverOffset = TimeSpan.Zero; // selisih jam server vs jam PC

        // true  = Setter, Maintenance, dan Penyedia WIP semuanya INSERT ke ActivityLog dari form ini.
        // false = hanya Penyedia WIP (perilaku form lama yang aktif).
        private const bool INSERT_ACTIVITYLOG_ALL_CALLS = true;

        // ==================== STATIC SESSION ====================
        // Diteruskan ke Session supaya tidak perlu di-set manual dari form induk
        public static string mesin
        {
            get { return Session.mesin; }
            set { Session.mesin = value; }
        }

        public static int Shift
        {
            get { return Session.Shift; }
            set { Session.Shift = value; }
        }

        // ==================== PROPERTIES UNTUK BE ====================
        public string Password { get { return txtPassword.Text; } }
        public string Keterangan { get { return txtKet.Text.Trim(); } }

        // Diisi saat Finish (aman dibaca setelah form ditutup)
        public string JenisProblem { get; private set; } = "";        // kategori (cmbProblem)
        public string SubProblem { get; private set; } = "";          // detail   (cmbJenis)
        public string AktivitasPerbaikan { get; private set; } = "";
        public string KeteranganProblem { get; private set; } = "";
        public string DurasiProblem { get; private set; } = "0";
        public string StartProblem { get; private set; } = "";
        public string FinishProblem { get; private set; } = "";
        public string Mtc { get; private set; } = null;

        // ==================== CONSTRUCTOR ====================
        public FormMaintenanceCall(Form parent = null)
        {
            InitializeComponent();
            parentForm = parent;
            BuildUI();

            // Ambil waktu server sekali, selanjutnya pakai offset (UI tidak query tiap detik)
            _waktuMulaiDowntime = GetServerDateTime();
            _serverOffset = _waktuMulaiDowntime - DateTime.Now;
            stats.LeftTime = _waktuMulaiDowntime.ToString("HH:mm:ss");
            stats.RightTime = _waktuMulaiDowntime.ToString("HH:mm:ss");

            // Timer durasi — langsung jalan saat form dimuat (sama seperti form lama)
            timer1 = new System.Windows.Forms.Timer { Interval = 1000 };
            timer1.Tick += Timer1_Tick;
            StartProblemTimer();

            // Timer jam kanan
            clock = new System.Windows.Forms.Timer { Interval = 1000 };
            clock.Tick += (s, e) => stats.RightTime = ServerNow().ToString("HH:mm:ss");
            clock.Start();

            // Disable input di awal (nunggu user panggil dulu)
            DisableAllInputs();
        }

        private DateTime ServerNow()
        {
            return DateTime.Now + _serverOffset;
        }

        // SetCalling untuk kompatibilitas dengan BE lama (jika dipanggil dari luar)
        public void SetCalling(int index, bool calling)
        {
            var c = index == 0 ? cardSetter : index == 1 ? cardMaint : cardWip;
            if (c != null) c.Calling = calling;
        }

        public void SetCounter(int value) { stats.Counter = value.ToString(); }
        public void SetTimes(string left, string right) { stats.LeftTime = left; stats.RightTime = right; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var wa = Screen.FromControl(this).WorkingArea;
            if (Height > wa.Height - 20) Height = wa.Height - 20;
            Top = wa.Top + (wa.Height - Height) / 2;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (clock != null) { clock.Stop(); clock.Dispose(); }
            if (timer1 != null) { timer1.Stop(); timer1.Dispose(); }
            base.OnFormClosed(e);
        }

        // cmbExtra1, cmbExtra2, txtKet SENGAJA tidak dimatikan di sini
        // (di form lama memang tidak pernah di-disable, dan cmbProblem/cmbJenis
        //  yang jadi gerbang karena masih disabled sebelum ada panggilan)
        private void DisableAllInputs()
        {
            txtPassword.Enabled = false;
            btnOkPassword.Enabled = false;
            cmbProblem.Enabled = false;
            cmbJenis.Enabled = false;
            if (btnFinish != null) btnFinish.Enabled = false;
        }

        // ==================== LAYOUT HELPER ====================
        // Hide/show satu baris di body DAN collapse tingginya supaya tidak bolong
        private void SetRowVisible(Control c, bool visible, int height)
        {
            c.Visible = visible;
            int row = body.GetRow(c);
            if (row >= 0 && row < body.RowStyles.Count)
                body.RowStyles[row].Height = visible ? height : 0;
            RecalcBodyHeight();
        }

        private void RecalcBodyHeight()
        {
            int total = 0;
            foreach (RowStyle rs in body.RowStyles) total += (int)rs.Height;
            body.Height = total + 8;
        }

        // Atur kombinasi tampilan: cmbExtra1, cmbExtra2, textbox keterangan
        private void ShowDetailLayout(bool extra1, bool extra2, bool ket)
        {
            SetRowVisible(cardExtra1, extra1, ROW_EXTRA);
            SetRowVisible(cardExtra2, extra2, ROW_EXTRA);
            SetRowVisible(fieldKeterangan, ket, ROW_KET);
        }

        // ==================== DATABASE ====================
        private DateTime GetServerDateTime()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionStringSqlServer()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT GETDATE()", conn))
                        return Convert.ToDateTime(cmd.ExecuteScalar());
                }
            }
            catch
            {
                return DateTime.Now;
            }
        }

        // Connection string diambil dari DatabaseHelper (otomatis ikut mode Dev/Prod)
        private string GetConnectionStringMySql()
        {
            return new DatabaseHelper(DbProvider.MySql).ConnectionString;
        }

        private string GetConnectionStringSqlServer()
        {
            return new DatabaseHelper(DbProvider.SqlServer).ConnectionString;
        }

        private string GetMachineID()
        {
            string labelText = mesin;
            if (string.IsNullOrEmpty(labelText)) return "999";

            if (labelText.StartsWith("CSM")) return "2" + labelText.Substring(labelText.Length - 2);
            if (labelText.StartsWith("KMX")) return "3" + labelText.Substring(labelText.Length - 2);

            if (labelText.StartsWith("CRP"))
            {
                var match = Regex.Match(labelText, @"CRP(\d+)");
                if (match.Success)
                {
                    int crpNumber = int.Parse(match.Groups[1].Value);
                    if (labelText.EndsWith("R2")) return crpNumber.ToString();
                    if (labelText.EndsWith("R4")) return (100 + crpNumber).ToString();
                }
            }
            return labelText;
        }

        private void SaveDowntimeToDatabase()
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionStringSqlServer()))
            {
                try
                {
                    conn.Open();
                    string query = @"INSERT INTO TrRPHMesinSetup 
                        (KodeMesin, tanggal, SIFT, jamtunggu, jammulai, jamselesai, 
                         KETERANGAN, KETERANGAN1, LAMA, KETERANGAN2, TERMINAL, APLIKATOR, MTC, KETERANGAN3)
                        VALUES (@KodeMesin, @tanggal, @SIFT, @jamtunggu, @jammulai, @jamselesai, 
                                @KETERANGAN, @KETERANGAN1, @LAMA, @KETERANGAN2, @TERMINAL, @APLIKATOR, @MTC, @KETERANGAN3)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        DateTime serverDate = GetServerDateTime();
                        cmd.Parameters.AddWithValue("@KodeMesin", mesin);
                        cmd.Parameters.AddWithValue("@tanggal", serverDate.Date);
                        cmd.Parameters.AddWithValue("@SIFT", Shift);
                        cmd.Parameters.AddWithValue("@jamtunggu", _waktuMulaiDowntime.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@jammulai", StartProblem);
                        cmd.Parameters.AddWithValue("@jamselesai", FinishProblem);
                        cmd.Parameters.AddWithValue("@KETERANGAN1", JenisProblem);   // kategori
                        cmd.Parameters.AddWithValue("@KETERANGAN2", SubProblem);     // detail
                        cmd.Parameters.AddWithValue("@KETERANGAN", AktivitasPerbaikan);
                        cmd.Parameters.AddWithValue("@LAMA", countimer);
                        cmd.Parameters.AddWithValue("@KETERANGAN3", KeteranganProblem);
                        cmd.Parameters.AddWithValue("@TERMINAL", DBNull.Value);
                        cmd.Parameters.AddWithValue("@APLIKATOR", DBNull.Value);
                        cmd.Parameters.AddWithValue("@MTC", string.IsNullOrEmpty(loggedInNoreg) ? (object)DBNull.Value : loggedInNoreg);

                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    NotifPopup.Error(this, "Gagal Simpan Downtime",
                        "Error saving to TrRPHMesinSetup:\n\n" + ex.Message);
                }
            }
        }

        // INSERT baris baru ke ActivityLog (MySQL)
        private void InsertActivityLog(string callID, string message)
        {
            using (MySqlConnection myConn = new MySqlConnection(GetConnectionStringMySql()))
            {
                try
                {
                    myConn.Open();
                    string mcname = GetMachineID();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = myConn;
                        cmd.CommandText = "INSERT INTO ActivityLog(MchID, CallID, StatusID, TimeCall) VALUES(@mesin, @callid, '1', NOW())";
                        cmd.Parameters.AddWithValue("@mesin", mcname);
                        cmd.Parameters.AddWithValue("@callid", callID);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception er)
                {
                    NotifPopup.Error(this, "Error Database", er.Message);
                }
            }
        }

        private void UpdateActivityLogWaiting()
        {
            using (MySqlConnection myConn = new MySqlConnection(GetConnectionStringMySql()))
            {
                try
                {
                    myConn.Open();
                    string mcname = GetMachineID();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = myConn;
                        cmd.CommandText = "UPDATE ActivityLog SET TimeWaiting = NOW(), StatusID = '2' " +
                                          "WHERE MchID = @mesin AND StatusID = '1' AND TimeWaiting IS NULL";
                        cmd.Parameters.AddWithValue("@mesin", mcname);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception er)
                {
                    NotifPopup.Error(this, "Error Update MySQL", er.Message);
                }
            }
        }

        // ==================== BUILD UI ====================
        private void BuildUI()
        {
            SuspendLayout();

            Text = "Maintenance Call";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(560, 760);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };

            // ---------- Header ----------
            var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.White };
            header.Controls.Add(new Label
            {
                Text = "Maintenance Call",
                AutoSize = true,
                Location = new Point(24, 14),
                ForeColor = AppThemePopup.TextDark,
                Font = AppThemePopup.FontTitle,
                BackColor = Color.White
            });
            header.Controls.Add(new Label
            {
                Text = "Panggil bantuan lalu isi detail problem",
                AutoSize = true,
                Location = new Point(26, 54),
                ForeColor = AppThemePopup.TextSub,
                Font = AppThemePopup.FontSub,
                BackColor = Color.White
            });
            header.Controls.Add(new Panel
            {
                Size = new Size(48, 4),
                Location = new Point(27, 82),
                BackColor = AppThemePopup.Accent
            });

            // ---------- Footer ----------
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 84,
                BackColor = Color.White,
                Padding = new Padding(24, 10, 24, 20),
                ColumnCount = 1,
                RowCount = 1
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            btnFinish = new RoundedButton
            {
                Text = "SELESAI",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Font = AppThemePopup.FontBtn,
                Radius = 12,
                FillColor = AppThemePopup.Accent,
                BorderColor = AppThemePopup.Accent,
                TextColor = Color.White,
                Enabled = false
            };
            btnFinish.Click += Finish_Click;
            actions.Controls.Add(btnFinish, 0, 0);
            // TODO (opsional): saat BATAL, update ActivityLog yang masih StatusID 1/2
            // supaya tidak menggantung. Versi lama juga tidak melakukannya.
            //btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            //btnFinish.Click += Finish_Click;
            //actions.Controls.Add(btnCancel, 0, 0);
            actions.Controls.Add(btnFinish, 1, 0);

            // ---------- Body (7 baris) ----------
            var scroller = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };

            body = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(24, 0, 24, 0),
                ColumnCount = 1,
                RowCount = 7
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_CALLS));     // 0 kartu
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_PASSWORD));  // 1 password
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_PAIR));      // 2 problem + jenis
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_KET));       // 3 keterangan
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_EXTRA));     // 4 cmbExtra1
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_EXTRA));     // 5 cmbExtra2
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, ROW_STATS));     // 6 stats

            body.Controls.Add(BuildCalls(), 0, 0);

            passwordRow = BuildPassword();
            body.Controls.Add(passwordRow, 0, 1);

            cmbProblem = NewCombo();
            cmbJenis = NewCombo();
            body.Controls.Add(Pair(new FieldCard("PILIH PROBLEM", true, cmbProblem),
                                   new FieldCard("JENIS PROBLEM", false, cmbJenis)), 0, 2);

            txtKet = NewText();
            fieldKeterangan = new FieldCard("KETERANGAN", false, txtKet) { Dock = DockStyle.Fill };
            body.Controls.Add(fieldKeterangan, 0, 3);

            // cmbExtra1 & cmbExtra2 pakai BareCard (tanpa caption)
            cmbExtra1 = NewCombo();
            cmbExtra2 = NewCombo();
            cardExtra1 = new BareCard(cmbExtra1) { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            cardExtra2 = new BareCard(cmbExtra2) { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            body.Controls.Add(cardExtra1, 0, 4);
            body.Controls.Add(cardExtra2, 0, 5);

            stats = new StatsCard { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) };
            body.Controls.Add(stats, 0, 6);

            RecalcBodyHeight();
            scroller.Controls.Add(body);

            Controls.Add(scroller);
            Controls.Add(actions);
            Controls.Add(header);

            // Event handler dropdown dinamis
            cmbProblem.SelectedIndexChanged += ComboBoxProblem_SelectedIndexChanged;
            cmbJenis.SelectedIndexChanged += ComboBoxJenisProblem_SelectedIndexChanged;
            cmbExtra1.SelectedIndexChanged += ComboBoxKeterangan_SelectedIndexChanged;

            // Init dropdown utama
            InitializeDropdowns();

            // Awal: hanya textbox keterangan yang tampil (extra dropdown disembunyikan & collapse)
            ShowDetailLayout(false, false, true);

            ResumeLayout(true);
        }

        private Control BuildCalls()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 3,
                RowCount = 1
            };
            for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            cardSetter = new CallCard("\uE9E9", "SETTER") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            cardMaint = new CallCard("\uE90F", "MAINTANANCE") { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0) };
            cardWip = new CallCard("\uE7B8", "PENYEDIA WIP") { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };

            // Klik kartu = eksekusi panggilan (hanya kalau belum ada panggilan aktif)
            cardSetter.Click += (s, e) => { if (!_isCalling) ExecuteAndonCall("4", "Panggil Setter", "TUNGGU SETTER"); };
            cardMaint.Click += (s, e) => { if (!_isCalling) ExecuteAndonCall("1", "Panggil Maintenance", "TUNGGU MAINTENANCE"); };
            cardWip.Click += (s, e) => { if (!_isCalling) ExecuteAndonCall("3", "Panggil Penyedia WIP", "TUNGGU PENYEDIA WIP"); };

            grid.Controls.Add(cardSetter, 0, 0);
            grid.Controls.Add(cardMaint, 1, 0);
            grid.Controls.Add(cardWip, 2, 0);
            return grid;
        }

        private Control BuildPassword()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 1
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            txtPassword = NewText();
            txtPassword.UseSystemPasswordChar = true;
            fieldPassword = new FieldCard("*PASSWORD", true, txtPassword) { Dock = DockStyle.Fill, Margin = Padding.Empty };

            btnOkPassword = new RoundedButton
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 24, 0, 6),
                IconGlyph = "\uE73E",
                Radius = 12,
                FillColor = AppThemePopup.Accent,
                BorderColor = AppThemePopup.Accent,
                IconColor = Color.White,
                Enabled = false
            };
            btnOkPassword.Click += PasswordOK_Click;

            row.Controls.Add(fieldPassword, 0, 0);
            row.Controls.Add(btnOkPassword, 1, 0);
            return row;
        }

        private TableLayoutPanel Pair(FieldCard a, FieldCard b)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 1
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            a.Dock = DockStyle.Fill; a.Margin = new Padding(0, 0, 6, 0);
            b.Dock = DockStyle.Fill; b.Margin = new Padding(6, 0, 0, 0);
            grid.Controls.Add(a, 0, 0);
            grid.Controls.Add(b, 1, 0);
            return grid;
        }

        private static TextBox NewText()
        {
            return new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = AppThemePopup.FieldBg,
                ForeColor = AppThemePopup.TextDark,
                Font = AppThemeLogin.FontInput
            };
        }

        private static ComboBox NewCombo()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppThemePopup.FieldBg,
                ForeColor = AppThemePopup.TextDark,
                Font = AppThemeLogin.FontInput
            };
        }

        // ==================== EXECUTE ANDON CALL ====================
        // callID: 1 = Maintenance, 3 = Penyedia WIP, 4 = Setter
        private void ExecuteAndonCall(string callID, string message, string waitingText)
        {
            _isCalling = true;
            currentCallID = callID;

            // Reset UI dulu
            ResetProblemTimer();
            txtPassword.Text = "";
            txtKet.Text = "";
            txtKet.Enabled = true;
            cmbExtra1.Enabled = true;
            cmbExtra2.Enabled = true;
            cmbProblem.SelectedIndex = -1;
            cmbJenis.SelectedIndex = -1;
            cmbExtra1.Items.Clear();
            cmbExtra2.Items.Clear();

            // Semua kartu "tidak aktif" dulu
            cardSetter.Calling = false;
            cardMaint.Calling = false;
            cardWip.Calling = false;

            DisableOtherCards(callID);

            // Reset layout detail ke default
            ShowDetailLayout(false, false, true);

            // Setter & Maintenance: INSERT ActivityLog (Penyedia WIP insert sendiri di bawah)
            if (INSERT_ACTIVITYLOG_ALL_CALLS && callID != "3")
                InsertActivityLog(callID, message);

            if (callID == "1")
            {
                // MAINTENANCE → pakai password
                SetRowVisible(passwordRow, true, ROW_PASSWORD);

                fieldPassword.Visible = true;
                btnOkPassword.Visible = true;

                txtPassword.UseSystemPasswordChar = true;
                txtPassword.PasswordChar = '●';
                txtPassword.Enabled = true;
                btnOkPassword.Enabled = true;
                cardMaint.Calling = true;

                InitializeDropdowns();
                cmbProblem.Enabled = true;
                cmbJenis.Enabled = true;

                txtPassword.Focus();
            }
            else if (callID == "4")
            {
                // SETTER → pakai noreg
                SetRowVisible(passwordRow, true, ROW_PASSWORD);

                fieldPassword.Visible = true;
                btnOkPassword.Visible = true;

                txtPassword.UseSystemPasswordChar = false;
                txtPassword.PasswordChar = '\0';
                txtPassword.Enabled = true;
                btnOkPassword.Enabled = true;
                cardSetter.Calling = true;

                InitializeDropdowns();

                // Set ke DANDORI dulu
                _isUpdatingComboBox = true;
                int dandoriIndex = cmbProblem.Items.IndexOf("DANDORI");
                if (dandoriIndex >= 0) cmbProblem.SelectedIndex = dandoriIndex;
                _isUpdatingComboBox = false;

                // Trigger manual untuk isi cmbJenis
                ComboBoxProblem_SelectedIndexChanged(cmbProblem, EventArgs.Empty);

                // Set ke "Setting Aplikator"
                int idxSetter = cmbJenis.Items.IndexOf("Setting Aplikator");
                if (idxSetter >= 0) cmbJenis.SelectedIndex = idxSetter;

                cmbProblem.Enabled = false;
                cmbJenis.Enabled = true;

                txtPassword.Focus();
            }
            else if (callID == "3")
            {
                // PENYEDIA WIP → tanpa validasi (sembunyikan baris password sampai collapse)
                SetRowVisible(passwordRow, false, ROW_PASSWORD);

                txtPassword.Enabled = false;
                btnOkPassword.Enabled = false;
                cardWip.Calling = true;

                _isUpdatingComboBox = true;
                cmbProblem.Items.Clear();
                cmbProblem.Items.Add("DANDORI");
                cmbProblem.SelectedIndex = 0;
                cmbProblem.Enabled = false;

                cmbJenis.Items.Clear();
                cmbJenis.Items.Add("Tunggu WIP");
                cmbJenis.SelectedIndex = 0;
                cmbJenis.Enabled = false;
                _isUpdatingComboBox = false;

                // Tidak ada input keterangan sama sekali
                txtKet.Enabled = false;
                ShowDetailLayout(false, false, false);

                loggedInNoreg = null;
                Mtc = null;

                // Insert ke ActivityLog
                InsertActivityLog(callID, message);

                // Langsung aktifkan Finish
                btnFinish.Enabled = true;
                StartProblem = GetServerDateTime().ToString("HH:mm:ss");

                // Update ActivityLog jadi waiting
                UpdateActivityLogWaiting();
            }
        }

        private void DisableOtherCards(string callID)
        {
            cardSetter.Cursor = callID == "4" ? Cursors.Hand : Cursors.Default;
            cardMaint.Cursor = callID == "1" ? Cursors.Hand : Cursors.Default;
            cardWip.Cursor = callID == "3" ? Cursors.Hand : Cursors.Default;
        }

        // ==================== PASSWORD / NOREG VALIDATION ====================
        private void PasswordOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                NotifPopup.Warning(this, "Data Belum Lengkap", "Harap masukkan password / noreg!");
                txtPassword.Focus();
                return;
            }

            string noreg = "0";
            using (SqlConnection conn = new SqlConnection(GetConnectionStringSqlServer()))
            {
                try
                {
                    conn.Open();
                    using (SqlCommand sql = new SqlCommand())
                    {
                        sql.Connection = conn;
                        if (currentCallID == "1")
                            sql.CommandText = "SELECT noreg FROM user_MTCSetup WHERE password = @input AND MTC = 1";
                        else if (currentCallID == "4")
                            sql.CommandText = "SELECT noreg FROM user_MTCSetup WHERE noreg = @input AND MTC = 0";
                        else
                        {
                            NotifPopup.Error(this, "Error", "Invalid call ID");
                            return;
                        }

                        sql.Parameters.AddWithValue("@input", txtPassword.Text);
                        using (SqlDataReader dt = sql.ExecuteReader())
                        {
                            if (dt.Read()) noreg = dt["noreg"].ToString();
                        }
                    }
                }
                catch (Exception er)
                {
                    NotifPopup.Error(this, "Koneksi Error", "Koneksi SQL Server Error:\n\n" + er.Message);
                    return;
                }
            }

            if (noreg == "0" || string.IsNullOrEmpty(noreg))
            {
                NotifPopup.Error(this,
                    currentCallID == "1" ? "Password Salah" : "Noreg Tidak Valid",
                    currentCallID == "1"
                        ? "Password salah / user tidak ditemukan di MTC Setup."
                        : "Noreg tidak terdaftar / tidak sesuai kriteria.");
                txtPassword.Text = "";
                txtPassword.Focus();
                return;
            }

            loggedInNoreg = noreg;
            Mtc = noreg;

            txtPassword.Enabled = false;
            btnOkPassword.Enabled = false;
            btnFinish.Enabled = true;

            if (currentCallID == "1")
            {
                cmbProblem.Enabled = true;
                cmbJenis.Enabled = true;
                txtKet.Enabled = true;
            }
            else if (currentCallID == "4")
            {
                cmbProblem.Items.Clear();
                cmbProblem.Items.Add("DANDORI");
                cmbProblem.SelectedIndex = 0;
                cmbProblem.Enabled = false;

                cmbJenis.Items.Clear();
                cmbJenis.Items.Add("Pasang Aplikator");
                cmbJenis.Items.Add("Setting Aplikator");
                cmbJenis.SelectedIndex = 0;
                cmbJenis.Enabled = true;

                txtKet.Enabled = true;
            }

            ResetProblemTimer();
            StartProblem = GetServerDateTime().ToString("HH:mm:ss");
            UpdateActivityLogWaiting();
        }

        // ==================== FINISH ====================
        private void Finish_Click(object sender, EventArgs e)
        {
            if (cmbProblem.SelectedIndex == -1)
            {
                NotifPopup.Warning(this, "Data Belum Lengkap", "Harap pilih jenis problem (kategori) terlebih dahulu!");
                return;
            }
            if (cmbJenis.SelectedIndex == -1)
            {
                NotifPopup.Warning(this, "Data Belum Lengkap", "Harap pilih problem (detail) terlebih dahulu!");
                return;
            }

            string keterangan3 = "";
            string keterangan = "";

            if (cardExtra1.Visible && cmbExtra1.SelectedIndex != -1)
                keterangan3 = cmbExtra1.Text;

            if (cardExtra2.Visible)
            {
                if (cmbExtra2.SelectedIndex == -1)
                {
                    NotifPopup.Warning(this, "Data Belum Lengkap", "Harap pilih aktivitas perbaikan!");
                    return;
                }
                keterangan = cmbExtra2.Text;
            }
            else if (fieldKeterangan.Visible)
            {
                if (string.IsNullOrWhiteSpace(txtKet.Text))
                {
                    NotifPopup.Warning(this, "Data Belum Lengkap", "Harap isi keterangan!");
                    txtKet.Focus();
                    return;
                }
                keterangan = txtKet.Text;
            }

            isFinished = true;
            StopProblemTimer();

            // Simpan ke property (dibaca form induk & SaveDowntimeToDatabase)
            JenisProblem = cmbProblem.Text;
            SubProblem = cmbJenis.Text;
            AktivitasPerbaikan = keterangan;
            KeteranganProblem = keterangan3;
            DurasiProblem = countimer.ToString();
            FinishProblem = GetServerDateTime().ToString("HH:mm:ss");

            // Update ActivityLog
            using (MySqlConnection myConn = new MySqlConnection(GetConnectionStringMySql()))
            {
                try
                {
                    myConn.Open();
                    string mcname = GetMachineID();
                    string currentStatus = "0";

                    using (MySqlCommand cmdCheck = new MySqlCommand(
                        "SELECT StatusID FROM ActivityLog WHERE MchID = @mesin ORDER BY TimeCall DESC LIMIT 1", myConn))
                    {
                        cmdCheck.Parameters.AddWithValue("@mesin", mcname);
                        object resultObj = cmdCheck.ExecuteScalar();
                        currentStatus = resultObj != null ? resultObj.ToString() : "0";
                    }

                    if (currentStatus != "0")
                    {
                        using (MySqlCommand cmdUpdate = new MySqlCommand())
                        {
                            cmdUpdate.Connection = myConn;
                            cmdUpdate.Parameters.AddWithValue("@mesin", mcname);

                            if (currentStatus == "1")
                                cmdUpdate.CommandText = "UPDATE ActivityLog SET TimeWaiting = NOW(), TimeFinish = NOW(), StatusID = '3' WHERE MchID = @mesin AND StatusID = '1'";
                            else if (currentStatus == "2")
                                cmdUpdate.CommandText = "UPDATE ActivityLog SET TimeFinish = NOW(), StatusID = '3' WHERE MchID = @mesin AND StatusID = '2'";
                            else if (currentStatus == "3")
                                cmdUpdate.CommandText = "UPDATE ActivityLog SET TimeFinish = NOW() WHERE MchID = @mesin ORDER BY TimeCall DESC LIMIT 1";

                            if (!string.IsNullOrEmpty(cmdUpdate.CommandText))
                                cmdUpdate.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception er)
                {
                    NotifPopup.Error(this, "Error Database", "Error Database (Finish):\n\n" + er.Message);
                }
            }

            SaveDowntimeToDatabase();

            // Panggil parent downtime_save
            if (parentForm != null)
            {
                var method = parentForm.GetType().GetMethod("downtime_save");
                if (method != null) method.Invoke(parentForm, null);
            }

            DialogResult = DialogResult.OK;
            this.Close();
        }

        // ==================== TIMER ====================
        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (isFinished || !isProblemActive) return;
            countimer++;
            stats.Counter = countimer.ToString();
        }

        // Mulai timer dari awal (dipanggil saat form dimuat)
        private void StartProblemTimer()
        {
            isFinished = false;
            isProblemActive = true;
            countimer = 0;
            stats.Counter = "0";
            timer1.Stop();
            timer1.Start();
        }

        // Reset timer (panggilan baru / konfirmasi password)
        private void ResetProblemTimer()
        {
            timer1.Stop();
            isFinished = false;
            isProblemActive = true;
            countimer = 0;
            stats.Counter = "0";
            timer1.Start();
        }

        private void StopProblemTimer()
        {
            isFinished = true;
            isProblemActive = false;
            if (timer1 != null) timer1.Stop();
        }

        // ==================== DROPDOWN DINAMIS ====================
        private void InitializeDropdowns()
        {
            _isUpdatingComboBox = true;
            cmbProblem.Items.Clear();
            cmbProblem.Items.Add("PROBLEM MESIN");
            cmbProblem.Items.Add("PROBLEM APLIKATOR");
            cmbProblem.Items.Add("PROBLEM WIRE SEAL");
            cmbProblem.Items.Add("DANDORI");
            cmbProblem.Items.Add("PLAN DOWNTIME");
            cmbProblem.Items.Add("MINORSTOP");
            cmbProblem.SelectedIndex = -1;

            cmbJenis.Items.Clear();
            cmbJenis.SelectedIndex = -1;
            cmbExtra1.Items.Clear();
            cmbExtra1.SelectedIndex = -1;
            cmbExtra2.Items.Clear();
            cmbExtra2.SelectedIndex = -1;
            _isUpdatingComboBox = false;
        }

        private void ComboBoxProblem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingComboBox) return;
            if (cmbProblem.SelectedIndex == -1) return;

            string selectedCategory = cmbProblem.Text;
            cmbJenis.Items.Clear();
            cmbExtra1.Items.Clear();
            cmbExtra2.Items.Clear();
            txtKet.Text = "";

            switch (selectedCategory)
            {
                case "PROBLEM MESIN":
                    cmbJenis.Items.AddRange(new string[] {
                        "AMP Servo", "Prefeeder", "Clamp F", "Clamp R", "Conveyor", "Cutter",
                        "Crimping", "Discharge", "Sensor", "Measuring", "Panjang Tidak Stabil",
                        "PC Error", "Sensor Cover", "Sensor Crimping", "Sensor Stripping",
                        "Pneumatik (Angin)", "Stripping Tidak Stabil", "Double Clamp",
                        "Clamp Dipping", "Clamp Insert Konektor", "Tungku Flux & Timah",
                        "Kelistrikan Mesin"
                    });
                    break;
                case "PLAN DOWNTIME":
                    cmbJenis.Items.AddRange(new string[] { "5R", "Trial" });
                    break;
                case "DANDORI":
                    cmbJenis.Items.AddRange(new string[] {
                        "Setting Aplikator", "Connect Wire", "Problem Supply Wire (Gudang)",
                        "Problem Supply Terminal (Gudang)", "Problem Supply Wire (Substore)",
                        "Problem Supply Terminal (Substore)", "Ganti Aplikator", "Ganti Terminal",
                        "Kuras Wireseal", "Ganti Wire", "Tunggu Aplikator", "Problem Wire"
                    });
                    break;
                case "MINORSTOP":
                    cmbJenis.Items.AddRange(new string[] { "Repair", "Downtime QC" });
                    break;
                case "PROBLEM APLIKATOR":
                    cmbJenis.Items.AddRange(new string[] {
                        "Aplicator Unlock", "Barel Insulation Cacat", "Barel Insulation Terbuka",
                        "Barel Wire Cacat", "Bellmouth Besar Atas", "Bellmouth Besar Bawah",
                        "Crimp High Naik", "Crimp High Turun", "Crimper Pecah", "Cutter Pecah",
                        "Kepala Terminal Cacat", "Kepala Terminal Terpotong", "Kertas Masuk Aplikator",
                        "Lock Terminal Cacat", "Lock Terminal Naik", "Lock Terminal Naik/ Terbalik",
                        "Lock Terminal Turun", "Punggung Terminal Kasar", "Tabs Atas Terminal Kasar",
                        "Tabs Atas Terminal Panjang", "Tabs Bawah Terminal Kasar", "Tabs Bawah Terminal Panjang",
                        "Tembaga Crimping Kepanjangan", "Tembaga Keluar Dari Barel Wire", "Tembaga Tidak Kelihatan",
                        "Terminal Bend Down", "Terminal Bend Up", "Terminal Cacat", "Terminal Karat / Kusam",
                        "Terminal Kusut", "Terminal Lengket", "Terminal Meluntir", "Terminal Numpuk",
                        "Terminal Numpuk di Bobin", "Terminal Numpuk di Rell Aplikator", "Terminal Salah Type",
                        "Terminal Tidak Tercrimping", "Tidak Ada Bellmouth Bawah",
                        "Vinyl Mundur Dari Barel Insulation", "Vinyl Terjepit Barel Wire"
                    });
                    break;
                case "PROBLEM WIRE SEAL":
                    cmbJenis.Items.AddRange(new string[] {
                        "ESCAPE SEAL", "INSERT SEAL TERTEKUK", "SENSOR SEAL", "DRUMFEEDER",
                        "GUIDE SEAL", "HOLDER SEAL", "POSISI WIRE SEAL TIDAK STABIL",
                        "INSERT PIN SEAL", "SEAL SOBEK", "ESCAPE DRUMFEEDER",
                        "SELANG TRANSFER SEAL", "TIMING BELT", "CLAMP SEAL"
                    });
                    break;
            }

            cmbJenis.SelectedIndex = -1;

            // Reset ke default (textbox keterangan)
            ShowDetailLayout(false, false, true);
        }

        private void ComboBoxJenisProblem_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingComboBox) return;
            if (cmbJenis.SelectedIndex == -1) return;

            string selectedProblem = cmbJenis.Text;
            string mainCategory = cmbProblem.Text;

            cmbExtra1.Items.Clear();
            cmbExtra2.Items.Clear();
            txtKet.Text = "";

            if (mainCategory == "PROBLEM MESIN")
            {
                ShowDetailLayout(true, false, false);

                switch (selectedProblem)
                {
                    case "AMP Servo": cmbExtra1.Items.AddRange(new[] { "Servo motor error", "Amplifier servo error", "Servo motor & amplifier servo error", "Lainnya" }); break;
                    case "Prefeeder": cmbExtra1.Items.AddRange(new[] { "wire lecet", "Shaft patah", "Roller motor aus", "Sensor rusak", "Roller teflon aus", "Lainnya" }); break;
                    case "Clamp F": cmbExtra1.Items.AddRange(new[] { "Clamp aus", "Bearing aus", "per rusak", "air cylinder rusak", "solenoid rusak", "Lainnya" }); break;
                    case "Clamp R": cmbExtra1.Items.AddRange(new[] { "Clamp aus", "Bearing aus", "per rusak", "air cylinder rusak", "solenoid rusak", "Clamp patah", "Clamp bengkok", "Rel rusak" }); break;
                    case "Conveyor": cmbExtra1.Items.AddRange(new[] { "Belt rusak", "Tombol rusak", "Motor rusak", "Lainnya" }); break;
                    case "Cutter": cmbExtra1.Items.AddRange(new[] { "Cutter aus", "Sensor cutter rusak", "LM guide cutter rusak", "Lainnya" }); break;
                    case "Crimping": cmbExtra1.Items.AddRange(new[] { "Head crimping rusak", "Alas aplikator rusak", "Mekanik crimping rusak", "Lainnya" }); break;
                    case "Discharge": cmbExtra1.Items.AddRange(new[] { "Clamp discharge rusak", "Bearing discharge", "Air cylinder rusak", "LM guide rusak" }); break;
                    case "Sensor": cmbExtra1.Items.AddRange(new[] { "Sensor cover rusak", "Sensor overlimit rusak (kelebihan batas)", "Sensor Stripping rusak", "Sensor Crimping rusak", "Lainnya" }); break;
                    case "Measuring": cmbExtra1.Items.AddRange(new[] { "Panjang tidak stabil", "Roller rusak/aus", "Belt aus/putus", "Pulley aus", "Mekanik gerak aus", "Motor rusak", "Sensor rusak", "Lainnya" }); break;
                    case "PC Error": cmbExtra1.Items.AddRange(new[] { "Monitor rusak", "Keyboard atau Mouse rusak", "CPU rusak" }); break;
                    case "Pneumatik (Angin)": cmbExtra1.Items.AddRange(new[] { "Solenoid valve rusak", "Selang atau nepel rusak", "Air dryer rusak", "Lainnya" }); break;
                    case "Stripping Tidak Stabil": cmbExtra1.Items.AddRange(new[] { "Posisi wire tidak center dengan cutter", "Pengunci clamp dengan wire tidak stabil", "LM guide tidak stabil" }); break;
                    case "Double Clamp": cmbExtra1.Items.AddRange(new[] { "Posisi double clamp tidak center", "Wire terjepit di clamp", "Clamp aus" }); break;
                    case "Clamp Dipping": cmbExtra1.Items.AddRange(new[] { "Clamp aus", "Clamp patah", "Bearing aus", "Selang rusak", "Air cylinder rusak", "Lainnya" }); break;
                    case "Clamp Insert Konektor": cmbExtra1.Items.AddRange(new[] { "Clamp patah", "Air cylinder rusak" }); break;
                    case "Tungku Flux & Timah": cmbExtra1.Items.AddRange(new[] { "Timah kotor", "Sensor suhu rusak", "Thermocontroller rusak", "Penggaruk timah rusak", "Tungku Flux rusak", "Lainnya" }); break;
                    case "Kelistrikan Mesin": cmbExtra1.Items.AddRange(new[] { "Mesin short circuit", "Mainboard rusak", "Power supply rusak", "Kipas pendingin mati" }); break;
                    default: cmbExtra1.Items.Add("Lainnya"); break;
                }
            }
            else if (mainCategory == "PROBLEM WIRE SEAL")
            {
                // Sama seperti form lama: aktivitas perbaikan tidak ditampilkan untuk WIRE SEAL
                ShowDetailLayout(true, false, false);

                switch (selectedProblem)
                {
                    case "ESCAPE SEAL": cmbExtra1.Items.AddRange(new[] { "Seal tersangkut", "Escape seal aus" }); break;
                    case "INSERT SEAL TERTEKUK": cmbExtra1.Items.AddRange(new[] { "Clamp F longgar", "Posisi insert tidak sesuai", "Guide seal longgar/kotor" }); break;
                    case "SENSOR SEAL": cmbExtra1.Items.AddRange(new[] { "Sensor escape", "Sensor holder tidak sesuai" }); break;
                    case "DRUMFEEDER": cmbExtra1.Items.Add("Laju seal tidak lancar"); break;
                    case "GUIDE SEAL": cmbExtra1.Items.AddRange(new[] { "Guide seal longgar", "Guide seal kotor" }); break;
                    case "HOLDER SEAL": cmbExtra1.Items.Add("Seal menumpuk di holder"); break;
                    case "POSISI WIRE SEAL TIDAK STABIL": cmbExtra1.Items.AddRange(new[] { "Collar holder aus", "Insert pin kurang naik/turun", "Guide seal longgar/kotor" }); break;
                    case "INSERT PIN SEAL": cmbExtra1.Items.AddRange(new[] { "Insert seal tidak stabil", "Seal tidak insert" }); break;
                    case "SEAL SOBEK": cmbExtra1.Items.AddRange(new[] { "Escape seal macet", "Posisi seal tidak pas" }); break;
                    case "ESCAPE DRUMFEEDER": cmbExtra1.Items.AddRange(new[] { "Seal tersangkut", "Transfer seal tidak lancar" }); break;
                    case "SELANG TRANSFER SEAL": cmbExtra1.Items.Add("Seal tersangkut"); break;
                    case "TIMING BELT": cmbExtra1.Items.AddRange(new[] { "Timing belt putus/rantas", "Timing belt kendor", "Timing belt tersangkut avalan" }); break;
                    case "CLAMP SEAL": cmbExtra1.Items.Add("-"); break;
                    default: cmbExtra1.Items.Add("-"); break;
                }
            }
            else if (mainCategory == "PROBLEM APLIKATOR")
            {
                ShowDetailLayout(true, true, false);

                switch (selectedProblem)
                {
                    case "Aplicator Unlock": cmbExtra1.Items.Add("Ganti part"); break;
                    case "Barel Insulation Cacat": cmbExtra1.Items.Add("Setting rel terminal"); break;
                    case "Barel Insulation Terbuka": cmbExtra1.Items.Add("Setting dial"); break;
                    case "Barel Wire Cacat": cmbExtra1.Items.Add("Setting feed finger (langkah)"); break;
                    case "Bellmouth Besar Atas": cmbExtra1.Items.Add("setting posisi feed finger"); break;
                    case "Bellmouth Besar Bawah": cmbExtra1.Items.Add("Setting rel terminal"); break;
                    case "Crimp High Naik":
                    case "Crimp High Turun": cmbExtra1.Items.Add("Setting dial pin"); break;
                    case "Crimper Pecah": cmbExtra1.Items.Add("Ganti Crimper"); break;
                    case "Cutter Pecah": cmbExtra1.Items.Add("Ganti Cutter"); break;
                    case "Kepala Terminal Cacat": cmbExtra1.Items.Add("Setting langkah, Setting terminal guide"); break;
                    case "Kepala Terminal Terpotong": cmbExtra1.Items.Add("Setting langkah"); break;
                    case "Kertas Masuk Aplikator": cmbExtra1.Items.Add("Perbaikan paper roll"); break;
                    case "Lock Terminal Cacat": cmbExtra1.Items.Add("Setting langkah, Setting terminal guide"); break;
                    case "Lock Terminal Naik": cmbExtra1.Items.Add("setting cutter bawah, setting clamp terminal"); break;
                    case "Lock Terminal Naik/ Terbalik": cmbExtra1.Items.Add("setting cutter, setting clamp"); break;
                    case "Lock Terminal Turun": cmbExtra1.Items.Add("setting terminal guide"); break;
                    case "Punggung Terminal Kasar": cmbExtra1.Items.Add("ganti anvil"); break;
                    case "Tabs Atas Terminal Kasar": cmbExtra1.Items.Add("setting cutter"); break;
                    case "Tabs Atas Terminal Panjang": cmbExtra1.Items.Add("setting rel/langkah"); break;
                    case "Tabs Bawah Terminal Kasar": cmbExtra1.Items.Add("setting cutter"); break;
                    case "Tabs Bawah Terminal Panjang": cmbExtra1.Items.Add("setting rel/langkah"); break;
                    case "Tembaga Crimping Kepanjangan": cmbExtra1.Items.Add("setting posisi crimping"); break;
                    case "Tembaga Keluar Dari Barel Wire": cmbExtra1.Items.Add("setting posisi crimping, pasang pir stopper"); break;
                    case "Tembaga Tidak Kelihatan": cmbExtra1.Items.Add("setting posisi crimping"); break;
                    case "Terminal Bend Down":
                    case "Terminal Bend Up":
                    case "Terminal Cacat": cmbExtra1.Items.Add("setting anvil"); break;
                    case "Terminal Karat / Kusam": cmbExtra1.Items.Add("pembersihan rel"); break;
                    case "Terminal Lengket": cmbExtra1.Items.Add("pemasangan up stripper"); break;
                    case "Terminal Meluntir": cmbExtra1.Items.Add("setting langkah"); break;
                    case "Terminal Numpuk":
                    case "Terminal Numpuk di Rell Aplikator": cmbExtra1.Items.Add("setting rel"); break;
                    case "Terminal Tidak Tercrimping": cmbExtra1.Items.Add("setting posisi crimping"); break;
                    case "Tidak Ada Bellmouth Bawah": cmbExtra1.Items.Add("setting crimper, setting rel, setting langkah"); break;
                    case "Vinyl Mundur Dari Barel Insulation":
                    case "Vinyl Terjepit Barel Wire": cmbExtra1.Items.Add("pasang pir stopper"); break;
                    default: cmbExtra1.Items.Add("-"); break;
                }
            }
            else
            {
                ShowDetailLayout(false, false, true);
            }

            if (cmbExtra1.Items.Count > 0) cmbExtra1.SelectedIndex = -1;
        }

        private void ComboBoxKeterangan_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingComboBox) return;
            if (cmbExtra1.SelectedIndex == -1) return;

            string selectedProblem = cmbJenis.Text;
            string detail = cmbExtra1.Text;
            string mainCategory = cmbProblem.Text;

            cmbExtra2.Items.Clear();

            if (mainCategory == "PROBLEM MESIN")
            {
                switch (selectedProblem)
                {
                    case "AMP Servo":
                        if (detail == "Servo motor error") cmbExtra2.Items.AddRange(new[] { "Perbaikan motor servo", "Ganti motor servo" });
                        else if (detail == "Amplifier servo error") cmbExtra2.Items.AddRange(new[] { "Perbaikan amplifier servo", "Ganti amplifier servo" });
                        else if (detail == "Servo motor & amplifier servo error") cmbExtra2.Items.AddRange(new[] { "Perbaikan motor & amplifier servo", "Ganti motor & amplifier servo" });
                        else cmbExtra2.Items.Add("Lainnya");
                        break;
                    case "Prefeeder": cmbExtra2.Items.AddRange(new[] { "Setting roller prefeeder", "Ganti shaft", "Ganti roller motor", "Ganti sensor", "Ganti roller teflon", "Lainnya" }); break;
                    case "Clamp F": cmbExtra2.Items.AddRange(new[] { "Ganti clamp", "Ganti bearing", "Ganti per", "Ganti air cylinder", "Ganti solenoid", "Lainnya" }); break;
                    case "Clamp R": cmbExtra2.Items.AddRange(new[] { "Ganti clamp", "Ganti bearing", "Ganti per", "Ganti air cylinder", "Ganti solenoid", "Ganti rel" }); break;
                    case "Conveyor": cmbExtra2.Items.AddRange(new[] { "Ganti belt", "Ganti tombol", "Perbaikan motor", "Lainnya" }); break;
                    case "Cutter": cmbExtra2.Items.AddRange(new[] { "Ganti cutter", "Ganti sensor", "Perbaikan LM Guide", "Setting sensor", "Lainnya" }); break;
                    case "Crimping": cmbExtra2.Items.AddRange(new[] { "Ganti head crimping", "Perbaikan alas aplikator", "Perbaikan mekanik crimping", "Lainnya" }); break;
                    case "Discharge": cmbExtra2.Items.AddRange(new[] { "Perbaikan clamp discharge", "Ganti clamp discharge", "Ganti bearing clamp", "Ganti air cylinder", "Perbaikan/ganti LM Guide" }); break;
                    case "Sensor": cmbExtra2.Items.AddRange(new[] { "Ganti sensor cover", "Ganti sensor overlimit", "Ganti kabel sensor", "Ganti amplifier sensor", "Perbaikan sensor", "Ganti sensor crimping", "Lainnya" }); break;
                    case "Measuring": cmbExtra2.Items.AddRange(new[] { "Perbaikan measuring unit", "Ganti bearing", "Ganti roller", "Ganti belt", "Ganti pulley", "Ganti mekanik gerak", "Ganti motor", "Ganti sensor", "Lainnya" }); break;
                    case "PC Error": cmbExtra2.Items.AddRange(new[] { "Perbaikan hardware", "Ganti monitor", "Ganti Keyboard/mouse", "Ganti CPU" }); break;
                    case "Pneumatik (Angin)": cmbExtra2.Items.AddRange(new[] { "Perbaikan solenoid", "Ganti solenoid", "Ganti selang/nepel", "Ganti filter air dryer", "Lainnya" }); break;
                    case "Stripping Tidak Stabil": cmbExtra2.Items.AddRange(new[] { "Kalibrasi posisi wire dengan cutter", "Perbaikan kerapatan pengunci wire", "Ganti LLM guide" }); break;
                    case "Double Clamp": cmbExtra2.Items.AddRange(new[] { "Kalibrasi double clamp", "Ganti clamp" }); break;
                    case "Clamp Dipping": cmbExtra2.Items.AddRange(new[] { "Perbaikan clamp", "Ganti clamp", "Ganti bearing", "Ganti selang", "Ganti air cylinder", "Lainnya" }); break;
                    case "Clamp Insert Konektor": cmbExtra2.Items.AddRange(new[] { "Ganti clamp", "ganti air cylinder" }); break;
                    case "Tungku Flux & Timah": cmbExtra2.Items.AddRange(new[] { "Kuras timah", "Ganti sensor", "Ganti thermocontroller", "Perbaikan penggaruk timah", "Perbaikan tungku flux", "Lainnya" }); break;
                    case "Kelistrikan Mesin": cmbExtra2.Items.AddRange(new[] { "Perbaikan instalasi listrik mesin", "Perbaikan Mainboard", "Ganti power supply", "Ganti kipas" }); break;
                }
            }
            else if (mainCategory == "PROBLEM WIRE SEAL")
            {
                switch (selectedProblem)
                {
                    case "ESCAPE SEAL": cmbExtra2.Items.AddRange(new[] { "Setting escape seal", "Ganti escape seal" }); break;
                    case "INSERT SEAL TERTEKUK": cmbExtra2.Items.AddRange(new[] { "Setting clamp F", "Setting posisi insert", "Setting belt /cleaning" }); break;
                    case "SENSOR SEAL": cmbExtra2.Items.AddRange(new[] { "Cleaning sensor escape", "Setting posisi holder" }); break;
                    case "DRUMFEEDER": cmbExtra2.Items.Add("Setting drumfeeder"); break;
                    case "GUIDE SEAL": cmbExtra2.Items.AddRange(new[] { "Setting belt/cleaning", "Pembersihan avalan tersangkut" }); break;
                    case "HOLDER SEAL": cmbExtra2.Items.Add("Cleaning seal yang menumpuk"); break;
                    case "POSISI WIRE SEAL TIDAK STABIL": cmbExtra2.Items.AddRange(new[] { "Ganti Collar", "Setting insert pin", "Setting belt/cleaning" }); break;
                    case "INSERT PIN SEAL": cmbExtra2.Items.Add("Setting insert pin"); break;
                    case "SEAL SOBEK": cmbExtra2.Items.AddRange(new[] { "Setting escape seal", "Setting posisi" }); break;
                    case "ESCAPE DRUMFEEDER": cmbExtra2.Items.Add("Setting drumfeeder escape"); break;
                    case "SELANG TRANSFER SEAL": cmbExtra2.Items.Add("Setting selang transfer seal"); break;
                    case "TIMING BELT": cmbExtra2.Items.AddRange(new[] { "Penggantian timing belt", "Pengencangan timing belt", "Pembersihan timing belt" }); break;
                }
            }

            if (cmbExtra2.Items.Count == 0) cmbExtra2.Items.Add("-");
            if (cmbExtra2.Items.Count == 1) cmbExtra2.SelectedIndex = 0;
            else cmbExtra2.SelectedIndex = -1;
        }

        // =====================================================
        //  Helper gambar
        // =====================================================
        private static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static void Prep(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        }

        private static readonly StringFormat Center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        private class BufferedControl : Control
        {
            public BufferedControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }
        }

        // =====================================================
        //  FieldCard: label di atas + kotak abu
        // =====================================================
        private class FieldCard : BufferedControl
        {
            public readonly Control Input;
            private readonly string caption;
            private readonly bool required;
            private bool focused;

            public FieldCard(string caption, bool required, Control input)
            {
                this.caption = caption;
                this.required = required;
                Input = input;
                BackColor = Color.White;
                Controls.Add(input);
                input.Enter += (s, e) => { focused = true; Invalidate(); };
                input.Leave += (s, e) => { focused = false; Invalidate(); };
            }

            private Rectangle FieldRect
            {
                get { return new Rectangle(0, 24, Width - 1, Height - 24 - 6); }
            }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                var f = FieldRect;
                Input.Left = f.X + 14;
                Input.Width = Math.Max(20, f.Width - 28);
                Input.Top = f.Y + (f.Height - Input.Height) / 2;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Prep(g);
                g.Clear(BackColor);

                float x = 2;
                if (required)
                {
                    using (var br = new SolidBrush(AppThemeLogin.ColorError))
                        g.DrawString("*", AppThemePopup.FontLabel, br, x, 2);
                    x += 11;
                }
                using (var br = new SolidBrush(AppThemePopup.TextSub))
                    g.DrawString(caption, AppThemePopup.FontLabel, br, x, 2);

                var fr = FieldRect;
                using (var path = Round(fr, 10))
                {
                    using (var br = new SolidBrush(AppThemePopup.FieldBg)) g.FillPath(br, path);
                    using (var pen = new Pen(focused ? AppThemePopup.Accent : AppThemePopup.FieldBorder, focused ? 2f : 1f))
                        g.DrawPath(pen, path);
                }
            }
        }

        // =====================================================
        //  BareCard: TANPA label, hanya kotak abu
        // =====================================================
        private class BareCard : BufferedControl
        {
            public readonly Control Input;
            private bool focused;

            public BareCard(Control input)
            {
                Input = input;
                BackColor = Color.White;
                Controls.Add(input);
                input.Enter += (s, e) => { focused = true; Invalidate(); };
                input.Leave += (s, e) => { focused = false; Invalidate(); };
            }

            private Rectangle FieldRect
            {
                get { return new Rectangle(0, 0, Width - 1, Height - 1); }
            }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                var f = FieldRect;
                Input.Left = f.X + 14;
                Input.Width = Math.Max(20, f.Width - 28);
                Input.Top = f.Y + (f.Height - Input.Height) / 2;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Prep(g);
                g.Clear(BackColor);

                var fr = FieldRect;
                using (var path = Round(fr, 10))
                {
                    using (var br = new SolidBrush(AppThemePopup.FieldBg)) g.FillPath(br, path);
                    using (var pen = new Pen(focused ? AppThemePopup.Accent : AppThemePopup.FieldBorder, focused ? 2f : 1f))
                        g.DrawPath(pen, path);
                }
            }
        }

        // =====================================================
        //  CallCard
        // =====================================================
        private class CallCard : BufferedControl
        {
            private readonly string glyph, name;
            private bool calling, hover;

            public bool Calling
            {
                get { return calling; }
                set { calling = value; Invalidate(); }
            }

            public CallCard(string glyph, string name)
            {
                this.glyph = glyph;
                this.name = name;
                BackColor = Color.White;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Prep(g);
                g.Clear(BackColor);

                var r = new RectangleF(1, 1, Width - 3, Height - 6);
                using (var path = Round(r, 14))
                {
                    using (var br = new SolidBrush(calling ? AppThemePopup.AccentSoft : AppThemePopup.FieldBg)) g.FillPath(br, path);
                    Color border = calling ? AppThemePopup.Accent : (hover ? AppThemePopup.AccentLine : AppThemePopup.FieldBorder);
                    using (var pen = new Pen(border, calling ? 2f : 1f)) g.DrawPath(pen, path);
                }

                var badge = new Rectangle((Width - 46) / 2, (int)r.Y + 10, 46, 46);
                using (var br = new SolidBrush(calling ? AppThemePopup.Accent : AppThemePopup.Navy)) g.FillEllipse(br, badge);
                using (var f = AppThemeLogin.IconFont(17f))
                using (var br = new SolidBrush(calling ? Color.White : AppThemePopup.Accent))
                    g.DrawString(glyph, f, br, badge, Center);

                using (var f = new Font("Segoe UI Semibold", 8.5f))
                using (var br = new SolidBrush(calling ? AppThemePopup.Accent : AppThemePopup.TextDark))
                    g.DrawString("PANGGIL " + name, f, br, new RectangleF(r.X + 4, badge.Bottom + 4, r.Width - 8, 20), Center);

                var chip = new RectangleF(r.X + 8, r.Bottom - 30, r.Width - 16, 22);
                using (var path = Round(chip, 11))
                {
                    using (var br = new SolidBrush(calling ? AppThemePopup.Accent : Color.White)) g.FillPath(br, path);
                    if (!calling)
                        using (var pen = new Pen(AppThemePopup.FieldBorder, 1f)) g.DrawPath(pen, path);
                }
                using (var br = new SolidBrush(calling ? Color.White : AppThemePopup.TextSub))
                {
                    g.FillEllipse(br, chip.X + 10, chip.Y + 7, 8, 8);
                    using (var f = new Font("Segoe UI Semibold", 7.5f))
                        g.DrawString("TUNGGU", f, br,
                            new RectangleF(chip.X + 20, chip.Y, chip.Width - 24, chip.Height), Center);
                }
            }
        }

        // =====================================================
        //  StatsCard
        // =====================================================
        private class StatsCard : BufferedControl
        {
            private string counter = "0", left = "--:--:--", right = "--:--:--";

            public string Counter { get { return counter; } set { counter = value; Invalidate(); } }
            public string LeftTime { get { return left; } set { left = value; Invalidate(); } }
            public string RightTime { get { return right; } set { right = value; Invalidate(); } }

            public StatsCard() { BackColor = Color.White; }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Prep(g);
                g.Clear(BackColor);

                var r = new RectangleF(1, 1, Width - 3, Height - 4);
                using (var path = Round(r, 14))
                {
                    using (var br = new SolidBrush(AppThemePopup.FieldBg)) g.FillPath(br, path);
                    using (var pen = new Pen(AppThemePopup.FieldBorder, 1f)) g.DrawPath(pen, path);
                }

                float colW = r.Width / 3f;
                DrawCol(g, new RectangleF(r.X, r.Y, colW, r.Height), "MULAI", left, 13f);
                DrawCol(g, new RectangleF(r.X + colW, r.Y, colW, r.Height), "DURASI", counter, 20f);
                DrawCol(g, new RectangleF(r.X + colW * 2, r.Y, colW, r.Height), "SEKARANG", right, 13f);

                using (var pen = new Pen(AppThemePopup.FieldBorder, 1f))
                {
                    g.DrawLine(pen, r.X + colW, r.Y + 14, r.X + colW, r.Bottom - 14);
                    g.DrawLine(pen, r.X + colW * 2, r.Y + 14, r.X + colW * 2, r.Bottom - 14);
                }
            }

            private static void DrawCol(Graphics g, RectangleF a, string caption, string value, float size)
            {
                using (var f = new Font("Segoe UI", 8f))
                using (var br = new SolidBrush(AppThemePopup.TextSub))
                    g.DrawString(caption, f, br, new RectangleF(a.X, a.Y + 8, a.Width, 16), Center);
                using (var f = new Font("Segoe UI Semibold", size, FontStyle.Bold))
                using (var br = new SolidBrush(AppThemePopup.TextDark))
                    g.DrawString(value, f, br, new RectangleF(a.X, a.Y + 24, a.Width, a.Height - 28), Center);
            }
        }
    }
}