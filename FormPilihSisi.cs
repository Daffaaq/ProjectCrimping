using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ProjectCrimping
{
    public class FormPilihSisi : Form
    {
        public TerminalResult SelectedResult { get; private set; }

        private ComboBox cbo;
        private RoundedButton btnOK, btnCancel;
        private List<TerminalResult> _items;

        public FormPilihSisi(List<TerminalResult> items)
        {
            _items = items;
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "Pilih Sisi Terminal";
            Size = new Size(420, 220);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);

            var lbl = new Label
            {
                Text = "tiproductname terdaftar di lebih dari satu Terminal.\nSilakan pilih sisi:",
                Location = new Point(20, 20),
                Size = new Size(370, 45),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            cbo = new ComboBox
            {
                Location = new Point(20, 75),
                Width = 365,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };
            foreach (var item in _items)
                cbo.Items.Add(item.Display);
            cbo.SelectedIndex = 0;

            btnOK = new RoundedButton
            {
                Text = "PILIH",
                Location = new Point(20, 125),
                Size = new Size(175, 40),
                Radius = 8,
                FillColor = Color.FromArgb(255, 140, 60),
                BorderColor = Color.FromArgb(255, 140, 60),
                TextColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f)
            };
            btnOK.Click += (s, e) =>
            {
                SelectedResult = _items[cbo.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel = new RoundedButton
            {
                Text = "BATAL",
                Location = new Point(210, 125),
                Size = new Size(175, 40),
                Radius = 8,
                FillColor = Color.FromArgb(240, 240, 240),
                BorderColor = Color.FromArgb(220, 220, 220),
                TextColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Segoe UI Semibold", 10f)
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(lbl);
            Controls.Add(cbo);
            Controls.Add(btnOK);
            Controls.Add(btnCancel);
        }
    }
}