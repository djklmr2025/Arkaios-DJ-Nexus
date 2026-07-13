using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class SetupForm : Form
    {
        private TextBox txtHistory;
        private TextBox txtDatabase;

        public SetupForm()
        {
            this.Text = "Configure Session File Path";
            this.Size = new Size(600, 450);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblTitle = new Label { Text = "Help us find where your DJ software saves its session history", Dock = DockStyle.Top, Font = new Font("Segoe UI", 10), Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGray };
            this.Controls.Add(lblTitle);

            Panel pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30) };
            this.Controls.Add(pnlMain);
            this.Controls.SetChildIndex(pnlMain, 0); // Bring to front

            int y = 20;

            // History Section
            Label lblHist = new Label { Text = "History File Location", Location = new Point(0, y), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            pnlMain.Controls.Add(lblHist);
            y += 25;

            txtHistory = new TextBox { Location = new Point(0, y), Width = 380, Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, ReadOnly = true };
            pnlMain.Controls.Add(txtHistory);

            Button btnHistBrowse = CreateButton("Browse", 390, y - 2, 80);
            btnHistBrowse.Click += BtnHistBrowse_Click;
            pnlMain.Controls.Add(btnHistBrowse);
            
            y += 50;

            // Database Section
            Label lblDb = new Label { Text = "Settings File Location (database.xml)", Location = new Point(0, y), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            pnlMain.Controls.Add(lblDb);
            y += 25;

            txtDatabase = new TextBox { Location = new Point(0, y), Width = 380, Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, ReadOnly = true };
            pnlMain.Controls.Add(txtDatabase);

            Button btnDbBrowse = CreateButton("Browse", 390, y - 2, 80);
            btnDbBrowse.Click += BtnDbBrowse_Click;
            pnlMain.Controls.Add(btnDbBrowse);

            y += 60;

            // Info Box
            Label lblInfo = new Label
            {
                Text = "Where to find it\nYour DJ software's history folder is usually in your Music folder or Documents. If you've installed it in a custom location, click Browse to select the correct path.",
                Location = new Point(0, y),
                Width = 500,
                Height = 80,
                ForeColor = Color.LightSkyBlue,
                Font = new Font("Segoe UI", 9)
            };
            pnlMain.Controls.Add(lblInfo);

            // Bottom Panel
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(25, 25, 25) };
            this.Controls.Add(pnlBottom);

            Button btnNext = CreateButton("Next", this.Width - 140, 15, 100);
            btnNext.BackColor = Color.FromArgb(0, 120, 215); // Accent color
            btnNext.Click += BtnNext_Click;
            pnlBottom.Controls.Add(btnNext);

            AutoDetectPaths();
        }

        private Button CreateButton(string text, int x, int y, int w)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = w,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
        }

        private void AutoDetectPaths()
        {
            string docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documentos");
            if (!Directory.Exists(docsPath)) docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string vdjFolder = Path.Combine(docsPath, "VirtualDJ");
            
            if (Directory.Exists(Path.Combine(vdjFolder, "History")))
                txtHistory.Text = Path.Combine(vdjFolder, "History");
            if (File.Exists(Path.Combine(vdjFolder, "database.xml")))
                txtDatabase.Text = Path.Combine(vdjFolder, "database.xml");
        }

        private void BtnHistBrowse_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select VirtualDJ History Folder";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtHistory.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnDbBrowse_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "VirtualDJ Database (database.xml)|database.xml|All files (*.*)|*.*";
                ofd.Title = "Select VirtualDJ Database XML";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtDatabase.Text = ofd.FileName;
                }
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(txtHistory.Text))
            {
                MessageBox.Show("History folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!File.Exists(txtDatabase.Text))
            {
                MessageBox.Show("Database file does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Save Settings
            AppSettings.VdjHistoryFolder = txtHistory.Text;
            AppSettings.VdjDatabaseFile = txtDatabase.Text;
            AppSettings.Save();

            // Check if VDJ is running to warn user about restart
            Process[] processes = Process.GetProcessesByName("VirtualDJ8");
            if (processes.Length > 0)
            {
                DialogResult dr = MessageBox.Show("Please CLOSE Virtual DJ to continue.\n\nVirtualDJ needs to be restarted so changes to historyDelay take effect.\nHave you closed it?", "CLOSE Virtual DJ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr == DialogResult.No) return;
            }

            // Launch Main Form
            this.Hide();
            var mainForm = new MainForm();
            mainForm.FormClosed += (s, args) => this.Close();
            mainForm.Show();
        }
    }
}
