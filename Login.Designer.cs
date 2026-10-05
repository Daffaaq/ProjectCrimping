using Guna.UI2.WinForms;

namespace ProjectCrimping
{
    partial class Login
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel bgPanel;

        // ===== Konten sisi kiri =====
        private System.Windows.Forms.Panel leftContent;
        private System.Windows.Forms.Panel badgePill;
        private System.Windows.Forms.Label lblBadgeIcon;
        private System.Windows.Forms.Label lblBadgeText;
        private System.Windows.Forms.Label lblLeftTitle;
        private System.Windows.Forms.Panel pnlAccentBar;
        private System.Windows.Forms.Label lblLeftSubtitle;
        private System.Windows.Forms.Panel networkPanel;
        private System.Windows.Forms.Label lblClock;
        private System.Windows.Forms.Label lblDate;

        // ===== Form di kanan =====
        private System.Windows.Forms.Panel formPanel;

        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.Panel brandBadge;
        private System.Windows.Forms.Label lblBrandInitial;
        private System.Windows.Forms.Label lblBrandTitle;
        private System.Windows.Forms.Label lblBrandSubtitle;

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblSubtitle;

        private System.Windows.Forms.FlowLayoutPanel flowFields;

        // ---- grup OPERATOR ----
        private System.Windows.Forms.Panel grpOperator;
        private System.Windows.Forms.Label lblFieldOperator;
        private System.Windows.Forms.Panel pnlFieldOperator;
        private System.Windows.Forms.Label lblIconOperator;
        private Guna.UI2.WinForms.Guna2TextBox txtNoRegOperator;
        private System.Windows.Forms.Label lblErrOperator;
        private System.Windows.Forms.Label lblHintOperator;

        // ---- grup SHIFT ----
        private System.Windows.Forms.Panel grpShift;
        private System.Windows.Forms.Label lblFieldShift;
        private System.Windows.Forms.Panel pnlFieldShift;
        private System.Windows.Forms.Label lblIconShift;
        private Guna.UI2.WinForms.Guna2ComboBox cmbShift;
        private System.Windows.Forms.Label lblErrShift;

        // ---- grup MESIN ----
        private System.Windows.Forms.Panel grpMesin;
        private System.Windows.Forms.Label lblFieldMesin;
        private System.Windows.Forms.Panel pnlFieldMesin;
        private System.Windows.Forms.Label lblIconMesin;
        private Guna.UI2.WinForms.Guna2TextBox txtMesin;
        private System.Windows.Forms.Label lblErrMesin;
        private System.Windows.Forms.Label lblHintMesin;

        private Guna.UI2.WinForms.Guna2Button btnLogin;

        private System.Windows.Forms.Panel pnlStatusDot;
        private System.Windows.Forms.Label lblFooterStatus;
        private System.Windows.Forms.Label lblFooterCompany;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.bgPanel = new System.Windows.Forms.Panel();
            this.leftContent = new System.Windows.Forms.Panel();
            this.badgePill = new System.Windows.Forms.Panel();
            this.lblBadgeIcon = new System.Windows.Forms.Label();
            this.lblBadgeText = new System.Windows.Forms.Label();
            this.lblLeftTitle = new System.Windows.Forms.Label();
            this.pnlAccentBar = new System.Windows.Forms.Panel();
            this.lblLeftSubtitle = new System.Windows.Forms.Label();
            this.networkPanel = new System.Windows.Forms.Panel();
            this.lblClock = new System.Windows.Forms.Label();
            this.lblDate = new System.Windows.Forms.Label();

            this.formPanel = new System.Windows.Forms.Panel();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.brandBadge = new System.Windows.Forms.Panel();
            this.lblBrandInitial = new System.Windows.Forms.Label();
            this.lblBrandTitle = new System.Windows.Forms.Label();
            this.lblBrandSubtitle = new System.Windows.Forms.Label();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();

            this.flowFields = new System.Windows.Forms.FlowLayoutPanel();

            // OPERATOR
            this.grpOperator = new System.Windows.Forms.Panel();
            this.lblFieldOperator = new System.Windows.Forms.Label();
            this.pnlFieldOperator = new System.Windows.Forms.Panel();
            this.lblIconOperator = new System.Windows.Forms.Label();
            this.txtNoRegOperator = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblErrOperator = new System.Windows.Forms.Label();
            this.lblHintOperator = new System.Windows.Forms.Label();

            // SHIFT
            this.grpShift = new System.Windows.Forms.Panel();
            this.lblFieldShift = new System.Windows.Forms.Label();
            this.pnlFieldShift = new System.Windows.Forms.Panel();
            this.lblIconShift = new System.Windows.Forms.Label();
            this.cmbShift = new Guna.UI2.WinForms.Guna2ComboBox();
            this.lblErrShift = new System.Windows.Forms.Label();

            // MESIN
            this.grpMesin = new System.Windows.Forms.Panel();
            this.lblFieldMesin = new System.Windows.Forms.Label();
            this.pnlFieldMesin = new System.Windows.Forms.Panel();
            this.lblIconMesin = new System.Windows.Forms.Label();
            this.txtMesin = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblErrMesin = new System.Windows.Forms.Label();
            this.lblHintMesin = new System.Windows.Forms.Label();

            this.btnLogin = new Guna.UI2.WinForms.Guna2Button();

            this.pnlStatusDot = new System.Windows.Forms.Panel();
            this.lblFooterStatus = new System.Windows.Forms.Label();
            this.lblFooterCompany = new System.Windows.Forms.Label();

            this.bgPanel.SuspendLayout();
            this.leftContent.SuspendLayout();
            this.badgePill.SuspendLayout();
            this.formPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.brandBadge.SuspendLayout();
            this.flowFields.SuspendLayout();
            this.grpOperator.SuspendLayout();
            this.pnlFieldOperator.SuspendLayout();
            this.grpShift.SuspendLayout();
            this.pnlFieldShift.SuspendLayout();
            this.grpMesin.SuspendLayout();
            this.pnlFieldMesin.SuspendLayout();
            this.SuspendLayout();

            // ============================================================
            // bgPanel
            // ============================================================
            this.bgPanel.BackColor = System.Drawing.Color.White;
            this.bgPanel.Controls.Add(this.leftContent);
            this.bgPanel.Controls.Add(this.formPanel);
            this.bgPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bgPanel.Location = new System.Drawing.Point(0, 0);
            this.bgPanel.Name = "bgPanel";
            this.bgPanel.Size = new System.Drawing.Size(1280, 800);
            this.bgPanel.TabIndex = 0;

            // ============================================================
            //  SISI KIRI
            // ============================================================
            this.leftContent.BackColor = System.Drawing.Color.Transparent;
            this.leftContent.Controls.Add(this.badgePill);
            this.leftContent.Controls.Add(this.lblLeftTitle);
            this.leftContent.Controls.Add(this.pnlAccentBar);
            this.leftContent.Controls.Add(this.lblLeftSubtitle);
            this.leftContent.Controls.Add(this.networkPanel);
            this.leftContent.Controls.Add(this.lblClock);
            this.leftContent.Controls.Add(this.lblDate);
            this.leftContent.Location = new System.Drawing.Point(56, 100);
            this.leftContent.Name = "leftContent";
            this.leftContent.Size = new System.Drawing.Size(480, 600);
            this.leftContent.TabIndex = 1;

            this.badgePill.BackColor = System.Drawing.Color.Transparent;
            this.badgePill.Controls.Add(this.lblBadgeIcon);
            this.badgePill.Controls.Add(this.lblBadgeText);
            this.badgePill.Location = new System.Drawing.Point(0, 0);
            this.badgePill.Name = "badgePill";
            this.badgePill.Size = new System.Drawing.Size(230, 32);
            this.badgePill.TabIndex = 0;

            this.lblBadgeIcon.Location = new System.Drawing.Point(12, 0);
            this.lblBadgeIcon.Name = "lblBadgeIcon";
            this.lblBadgeIcon.Size = new System.Drawing.Size(22, 32);
            this.lblBadgeIcon.TabIndex = 0;

            this.lblBadgeText.Location = new System.Drawing.Point(36, 0);
            this.lblBadgeText.Name = "lblBadgeText";
            this.lblBadgeText.Size = new System.Drawing.Size(190, 32);
            this.lblBadgeText.Text = "INTERNAL ACCESS ONLY";
            this.lblBadgeText.TabIndex = 1;

            this.lblLeftTitle.AutoSize = true;
            this.lblLeftTitle.Location = new System.Drawing.Point(0, 64);
            this.lblLeftTitle.Name = "lblLeftTitle";
            this.lblLeftTitle.Text = "CRIMPING\r\nSYSTEM";
            this.lblLeftTitle.TabIndex = 1;

            this.pnlAccentBar.Location = new System.Drawing.Point(4, 204);
            this.pnlAccentBar.Name = "pnlAccentBar";
            this.pnlAccentBar.Size = new System.Drawing.Size(56, 4);
            this.pnlAccentBar.TabIndex = 2;

            this.lblLeftSubtitle.Location = new System.Drawing.Point(2, 226);
            this.lblLeftSubtitle.Name = "lblLeftSubtitle";
            this.lblLeftSubtitle.Size = new System.Drawing.Size(420, 44);
            this.lblLeftSubtitle.Text = "Dept Operational Plant 1 · PT Indoprima Gemilang";
            this.lblLeftSubtitle.TabIndex = 3;

            this.networkPanel.BackColor = System.Drawing.Color.Transparent;
            this.networkPanel.Location = new System.Drawing.Point(0, 310);
            this.networkPanel.Name = "networkPanel";
            this.networkPanel.Size = new System.Drawing.Size(360, 140);
            this.networkPanel.TabIndex = 4;

            this.lblClock.AutoSize = true;
            this.lblClock.Location = new System.Drawing.Point(0, 490);
            this.lblClock.Name = "lblClock";
            this.lblClock.Text = "00:00:00";
            this.lblClock.TabIndex = 5;

            this.lblDate.AutoSize = true;
            this.lblDate.Location = new System.Drawing.Point(4, 545);
            this.lblDate.Name = "lblDate";
            this.lblDate.Text = "-";
            this.lblDate.TabIndex = 6;

            // ============================================================
            //  SISI KANAN — FORM
            // ============================================================
            this.formPanel.BackColor = System.Drawing.Color.White;
            this.formPanel.Controls.Add(this.picLogo);
            this.formPanel.Controls.Add(this.brandBadge);
            this.formPanel.Controls.Add(this.lblBrandTitle);
            this.formPanel.Controls.Add(this.lblBrandSubtitle);
            this.formPanel.Controls.Add(this.lblTitle);
            this.formPanel.Controls.Add(this.lblSubtitle);
            this.formPanel.Controls.Add(this.flowFields);
            this.formPanel.Controls.Add(this.pnlStatusDot);
            this.formPanel.Controls.Add(this.lblFooterStatus);
            this.formPanel.Controls.Add(this.lblFooterCompany);
            this.formPanel.Location = new System.Drawing.Point(700, 40);
            this.formPanel.Name = "formPanel";
            this.formPanel.Size = new System.Drawing.Size(420, 660);
            this.formPanel.TabIndex = 2;

            this.picLogo.BackColor = System.Drawing.Color.Transparent;
            this.picLogo.Location = new System.Drawing.Point(0, 0);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(56, 56);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 0;
            this.picLogo.TabStop = false;
            this.picLogo.Visible = false;

            this.brandBadge.BackColor = System.Drawing.Color.Transparent;
            this.brandBadge.Controls.Add(this.lblBrandInitial);
            this.brandBadge.Location = new System.Drawing.Point(0, 0);
            this.brandBadge.Name = "brandBadge";
            this.brandBadge.Size = new System.Drawing.Size(56, 56);
            this.brandBadge.TabIndex = 1;

            this.lblBrandInitial.Name = "lblBrandInitial";
            this.lblBrandInitial.Text = "CS";
            this.lblBrandInitial.TabIndex = 0;

            this.lblBrandTitle.AutoSize = true;
            this.lblBrandTitle.Location = new System.Drawing.Point(68, 6);
            this.lblBrandTitle.Name = "lblBrandTitle";
            this.lblBrandTitle.Text = "Crimping System";
            this.lblBrandTitle.TabIndex = 2;

            this.lblBrandSubtitle.AutoSize = true;
            this.lblBrandSubtitle.Location = new System.Drawing.Point(70, 32);
            this.lblBrandSubtitle.Name = "lblBrandSubtitle";
            this.lblBrandSubtitle.Text = "Portal Login";
            this.lblBrandSubtitle.TabIndex = 3;

            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(-2, 92);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "Selamat Datang";
            this.lblTitle.TabIndex = 4;

            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.Location = new System.Drawing.Point(0, 140);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Text = "Scan data operator, shift, dan mesin untuk melanjutkan";
            this.lblSubtitle.TabIndex = 5;

            // flowFields
            this.flowFields.BackColor = System.Drawing.Color.White;
            this.flowFields.Controls.Add(this.grpOperator);
            this.flowFields.Controls.Add(this.grpShift);
            this.flowFields.Controls.Add(this.grpMesin);
            this.flowFields.Controls.Add(this.btnLogin);
            this.flowFields.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowFields.Location = new System.Drawing.Point(0, 182);
            this.flowFields.Margin = new System.Windows.Forms.Padding(0);
            this.flowFields.Name = "flowFields";
            this.flowFields.Size = new System.Drawing.Size(420, 420);
            this.flowFields.TabIndex = 6;
            this.flowFields.WrapContents = false;

            // ---------------- grup OPERATOR ----------------
            this.grpOperator.BackColor = System.Drawing.Color.White;
            this.grpOperator.Controls.Add(this.lblFieldOperator);
            this.grpOperator.Controls.Add(this.pnlFieldOperator);
            this.grpOperator.Controls.Add(this.lblErrOperator);
            this.grpOperator.Controls.Add(this.lblHintOperator);
            this.grpOperator.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.grpOperator.Name = "grpOperator";
            this.grpOperator.Size = new System.Drawing.Size(420, 112);        // ← 108 → 112
            this.grpOperator.TabIndex = 0;

            this.lblFieldOperator.AutoSize = true;
            this.lblFieldOperator.Location = new System.Drawing.Point(2, 0);
            this.lblFieldOperator.Name = "lblFieldOperator";
            this.lblFieldOperator.Text = "NO REG OPERATOR";
            this.lblFieldOperator.TabIndex = 0;

            this.pnlFieldOperator.Controls.Add(this.lblIconOperator);
            this.pnlFieldOperator.Controls.Add(this.txtNoRegOperator);
            this.pnlFieldOperator.Location = new System.Drawing.Point(0, 22);
            this.pnlFieldOperator.Name = "pnlFieldOperator";
            this.pnlFieldOperator.Size = new System.Drawing.Size(420, 48);
            this.pnlFieldOperator.TabIndex = 1;

            this.lblIconOperator.Location = new System.Drawing.Point(8, 0);
            this.lblIconOperator.Name = "lblIconOperator";
            this.lblIconOperator.Size = new System.Drawing.Size(36, 48);
            this.lblIconOperator.TabIndex = 0;

            this.txtNoRegOperator.DefaultText = "";
            this.txtNoRegOperator.Location = new System.Drawing.Point(44, 6);
            this.txtNoRegOperator.Name = "txtNoRegOperator";
            this.txtNoRegOperator.PasswordChar = '\0';
            this.txtNoRegOperator.PlaceholderText = "Scan barcode / ketik no reg";
            this.txtNoRegOperator.SelectedText = "";
            this.txtNoRegOperator.Size = new System.Drawing.Size(364, 36);
            this.txtNoRegOperator.TabIndex = 1;

            this.lblErrOperator.AutoSize = false;
            this.lblErrOperator.Location = new System.Drawing.Point(2, 78);   // ← 74 → 78
            this.lblErrOperator.Name = "lblErrOperator";
            this.lblErrOperator.Size = new System.Drawing.Size(410, 20);
            this.lblErrOperator.TabIndex = 2;

            this.lblHintOperator.AutoSize = false;
            this.lblHintOperator.Location = new System.Drawing.Point(2, 78);  // ← 74 → 78
            this.lblHintOperator.Name = "lblHintOperator";
            this.lblHintOperator.Size = new System.Drawing.Size(150, 26);
            this.lblHintOperator.TabIndex = 3;

            // ---------------- grup SHIFT ----------------
            this.grpShift.BackColor = System.Drawing.Color.White;
            this.grpShift.Controls.Add(this.lblFieldShift);
            this.grpShift.Controls.Add(this.pnlFieldShift);
            this.grpShift.Controls.Add(this.lblErrShift);
            this.grpShift.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.grpShift.Name = "grpShift";
            this.grpShift.Size = new System.Drawing.Size(420, 92);
            this.grpShift.TabIndex = 1;

            this.lblFieldShift.AutoSize = true;
            this.lblFieldShift.Location = new System.Drawing.Point(2, 0);
            this.lblFieldShift.Name = "lblFieldShift";
            this.lblFieldShift.Text = "SHIFT";
            this.lblFieldShift.TabIndex = 0;

            this.pnlFieldShift.Controls.Add(this.lblIconShift);
            this.pnlFieldShift.Controls.Add(this.cmbShift);
            this.pnlFieldShift.Location = new System.Drawing.Point(0, 22);
            this.pnlFieldShift.Name = "pnlFieldShift";
            this.pnlFieldShift.Size = new System.Drawing.Size(420, 48);
            this.pnlFieldShift.TabIndex = 1;

            this.lblIconShift.Location = new System.Drawing.Point(8, 0);
            this.lblIconShift.Name = "lblIconShift";
            this.lblIconShift.Size = new System.Drawing.Size(36, 48);
            this.lblIconShift.TabIndex = 0;

            this.cmbShift.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cmbShift.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbShift.ItemHeight = 30;
            this.cmbShift.Location = new System.Drawing.Point(44, 6);
            this.cmbShift.Name = "cmbShift";
            this.cmbShift.Size = new System.Drawing.Size(364, 36);
            this.cmbShift.TabIndex = 1;

            this.lblErrShift.AutoSize = true;
            this.lblErrShift.Location = new System.Drawing.Point(2, 74);
            this.lblErrShift.Name = "lblErrShift";
            this.lblErrShift.TabIndex = 2;

            // ---------------- grup MESIN ----------------
            this.grpMesin.BackColor = System.Drawing.Color.White;
            this.grpMesin.Controls.Add(this.lblFieldMesin);
            this.grpMesin.Controls.Add(this.pnlFieldMesin);
            this.grpMesin.Controls.Add(this.lblErrMesin);
            this.grpMesin.Controls.Add(this.lblHintMesin);
            this.grpMesin.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.grpMesin.Name = "grpMesin";
            this.grpMesin.Size = new System.Drawing.Size(420, 112);           // ← 108 → 112
            this.grpMesin.TabIndex = 2;

            this.lblFieldMesin.AutoSize = true;
            this.lblFieldMesin.Location = new System.Drawing.Point(2, 0);
            this.lblFieldMesin.Name = "lblFieldMesin";
            this.lblFieldMesin.Text = "MESIN";
            this.lblFieldMesin.TabIndex = 0;

            this.pnlFieldMesin.Controls.Add(this.lblIconMesin);
            this.pnlFieldMesin.Controls.Add(this.txtMesin);
            this.pnlFieldMesin.Location = new System.Drawing.Point(0, 22);
            this.pnlFieldMesin.Name = "pnlFieldMesin";
            this.pnlFieldMesin.Size = new System.Drawing.Size(420, 48);
            this.pnlFieldMesin.TabIndex = 1;

            this.lblIconMesin.Location = new System.Drawing.Point(8, 0);
            this.lblIconMesin.Name = "lblIconMesin";
            this.lblIconMesin.Size = new System.Drawing.Size(36, 48);
            this.lblIconMesin.TabIndex = 0;

            this.txtMesin.DefaultText = "";
            this.txtMesin.Location = new System.Drawing.Point(44, 6);
            this.txtMesin.Name = "txtMesin";
            this.txtMesin.PasswordChar = '\0';
            this.txtMesin.PlaceholderText = "Scan barcode / ketik kode mesin";
            this.txtMesin.SelectedText = "";
            this.txtMesin.Size = new System.Drawing.Size(364, 36);
            this.txtMesin.TabIndex = 1;

            this.lblErrMesin.AutoSize = false;
            this.lblErrMesin.Location = new System.Drawing.Point(2, 78);       // ← 74 → 78
            this.lblErrMesin.Name = "lblErrMesin";
            this.lblErrMesin.Size = new System.Drawing.Size(410, 20);
            this.lblErrMesin.TabIndex = 2;

            this.lblHintMesin.AutoSize = false;
            this.lblHintMesin.Location = new System.Drawing.Point(2, 78);      // ← 74 → 78
            this.lblHintMesin.Name = "lblHintMesin";
            this.lblHintMesin.Size = new System.Drawing.Size(150, 26);
            this.lblHintMesin.TabIndex = 3;

            // btnLogin
            this.btnLogin.Margin = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(420, 52);
            this.btnLogin.Text = "LOGIN";
            this.btnLogin.TabIndex = 3;

            // pnlStatusDot
            this.pnlStatusDot.Location = new System.Drawing.Point(0, 610);
            this.pnlStatusDot.Name = "pnlStatusDot";
            this.pnlStatusDot.Size = new System.Drawing.Size(9, 9);
            this.pnlStatusDot.TabIndex = 7;

            // lblFooterStatus
            this.lblFooterStatus.AutoSize = true;
            this.lblFooterStatus.Location = new System.Drawing.Point(15, 605);
            this.lblFooterStatus.Name = "lblFooterStatus";
            this.lblFooterStatus.Text = "Online   ·   Ver 3.1   ·   Protected";
            this.lblFooterStatus.TabIndex = 8;

            // lblFooterCompany
            this.lblFooterCompany.AutoSize = true;
            this.lblFooterCompany.Location = new System.Drawing.Point(0, 630);
            this.lblFooterCompany.Name = "lblFooterCompany";
            this.lblFooterCompany.Text = "PT Indoprima Gemilang Plant 1 · Internal Use · Nganjuk";
            this.lblFooterCompany.TabIndex = 9;

            // Login
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.Controls.Add(this.bgPanel);
            this.MinimumSize = new System.Drawing.Size(1000, 720);
            this.Name = "Login";
            this.BackColor = System.Drawing.Color.White;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Crimping System - Login";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;

            this.bgPanel.ResumeLayout(false);
            this.leftContent.ResumeLayout(false);
            this.leftContent.PerformLayout();
            this.badgePill.ResumeLayout(false);
            this.formPanel.ResumeLayout(false);
            this.formPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.brandBadge.ResumeLayout(false);
            this.flowFields.ResumeLayout(false);
            this.grpOperator.ResumeLayout(false);
            this.grpOperator.PerformLayout();
            this.pnlFieldOperator.ResumeLayout(false);
            this.grpShift.ResumeLayout(false);
            this.grpShift.PerformLayout();
            this.pnlFieldShift.ResumeLayout(false);
            this.grpMesin.ResumeLayout(false);
            this.grpMesin.PerformLayout();
            this.pnlFieldMesin.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion
    }
}