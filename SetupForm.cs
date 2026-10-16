using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace ArkaiosDJAssistant
{
    public class SetupForm : Form
    {
        private TextBox txtHistory;
        private TextBox txtDatabase;
        private ComboBox cboCandidates;
        private Label lblCandidateInfo;
        private List<AppSettings.VirtualDjCandidateInfo> detectedCandidates;

        public SetupForm()
        {
            this.Text = "Configurar Rutas de VirtualDJ - Arkaios DJ Nexus";
            this.Size = new Size(640, 520);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblTitle = new Label
            {
                Text = "Detección y configuración de Base de Datos e Historial de VirtualDJ",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Height = 35,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.LightSkyBlue
            };
            this.Controls.Add(lblTitle);

            Panel pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(25) };
            this.Controls.Add(pnlMain);
            this.Controls.SetChildIndex(pnlMain, 0);

            int y = 10;

            // Selector de candidatos detectados
            Label lblCbo = new Label
            {
                Text = "Instalaciones o Bases de Datos de VirtualDJ detectadas:",
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.LightGray
            };
            pnlMain.Controls.Add(lblCbo);
            y += 22;

            cboCandidates = new ComboBox
            {
                Location = new Point(0, y),
                Width = 490,
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            cboCandidates.SelectedIndexChanged += CboCandidates_SelectedIndexChanged;
            pnlMain.Controls.Add(cboCandidates);

            Button btnRefresh = CreateButton("↻", 500, y - 2, 40);
            btnRefresh.Click += (s, ev) => AutoDetectPaths();
            pnlMain.Controls.Add(btnRefresh);

            y += 30;

            lblCandidateInfo = new Label
            {
                Text = "",
                Location = new Point(0, y),
                Width = 550,
                Height = 22,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.LightGreen
            };
            pnlMain.Controls.Add(lblCandidateInfo);

            y += 30;

            // Database Section
            Label lblDb = new Label
            {
                Text = "Archivo database.xml (Base de datos de canciones de VirtualDJ):",
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            pnlMain.Controls.Add(lblDb);
            y += 22;

            txtDatabase = new TextBox
            {
                Location = new Point(0, y),
                Width = 450,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            pnlMain.Controls.Add(txtDatabase);

            Button btnDbBrowse = CreateButton("Examinar...", 460, y - 2, 90);
            btnDbBrowse.Click += BtnDbBrowse_Click;
            pnlMain.Controls.Add(btnDbBrowse);

            y += 45;

            // History Section
            Label lblHist = new Label
            {
                Text = "Carpeta History (Historial de sesiones de VirtualDJ):",
                Location = new Point(0, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            pnlMain.Controls.Add(lblHist);
            y += 22;

            txtHistory = new TextBox
            {
                Location = new Point(0, y),
                Width = 450,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            pnlMain.Controls.Add(txtHistory);

            Button btnHistBrowse = CreateButton("Examinar...", 460, y - 2, 90);
            btnHistBrowse.Click += BtnHistBrowse_Click;
            pnlMain.Controls.Add(btnHistBrowse);
            
            y += 45;

            // Info Box
            Label lblInfo = new Label
            {
                Text = "Nota: Arkaios DJ Nexus lee tus pistas y compatibilidad armónica Camelot directamente desde tu database.xml. Si VirtualDJ guarda la música en un disco externo o ruta personalizada, selecciónala manualmente aquí.",
                Location = new Point(0, y),
                Width = 550,
                Height = 55,
                ForeColor = Color.LightSkyBlue,
                Font = new Font("Segoe UI", 8)
            };
            pnlMain.Controls.Add(lblInfo);

            // Bottom Panel
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(25, 25, 25) };
            this.Controls.Add(pnlBottom);

            Button btnDiag = CreateButton("Ver Diagnóstico (.log)", 25, 15, 160);
            btnDiag.Click += (s, e) => DiagLog.OpenLog();
            pnlBottom.Controls.Add(btnDiag);

            Button btnNext = CreateButton("Guardar e Iniciar", this.Width - 170, 15, 130);
            btnNext.BackColor = Color.FromArgb(0, 120, 215);
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
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
        }

        private void AutoDetectPaths()
        {
            AppSettings.AutoDetectVirtualDjPaths(force: true);

            detectedCandidates = AppSettings.GetDetailedCandidates();
            cboCandidates.Items.Clear();

            int selectedIdx = -1;
            for (int i = 0; i < detectedCandidates.Count; i++)
            {
                var c = detectedCandidates[i];
                string label = $"{c.BaseFolder} [{c.SongCount} canciones, DB: {(c.ExistsDatabase ? "Sí" : "No")}]";
                cboCandidates.Items.Add(label);

                if (string.Equals(c.DatabaseFile, AppSettings.VdjDatabaseFile, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIdx = i;
                }
            }

            if (selectedIdx >= 0)
            {
                cboCandidates.SelectedIndex = selectedIdx;
            }
            else if (cboCandidates.Items.Count > 0)
            {
                cboCandidates.SelectedIndex = 0;
            }

            txtHistory.Text = AppSettings.VdjHistoryFolder ?? "";
            txtDatabase.Text = AppSettings.VdjDatabaseFile ?? "";
        }

        private void CboCandidates_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cboCandidates.SelectedIndex;
            if (idx >= 0 && idx < detectedCandidates.Count)
            {
                var c = detectedCandidates[idx];
                txtDatabase.Text = c.DatabaseFile;
                txtHistory.Text = c.ExistsHistory ? c.HistoryFolder : Path.Combine(c.BaseFolder, "History");
                lblCandidateInfo.Text = $"Canciones: {c.SongCount} | Tamaño: {c.DatabaseSizeBytes / 1024} KB | Modificado: {c.DatabaseLastWrite} | Historial M3U: {c.M3uCount}";
            }
        }

        private void BtnHistBrowse_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Selecciona la carpeta History de VirtualDJ";
                if (Directory.Exists(txtHistory.Text)) fbd.SelectedPath = txtHistory.Text;
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
                ofd.Filter = "Base de datos VirtualDJ (*.xml)|*database*.xml;*.xml|Todos los archivos (*.*)|*.*";
                ofd.Title = "Selecciona el archivo database.xml de VirtualDJ";
                if (File.Exists(txtDatabase.Text))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(txtDatabase.Text);
                }
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtDatabase.Text = ofd.FileName;
                    int count = AppSettings.QuickCountSongs(ofd.FileName);
                    lblCandidateInfo.Text = $"Archivo seleccionado manualmente: {count} canciones detectadas.";
                }
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(txtHistory.Text))
            {
                MessageBox.Show("La carpeta History especificada no existe.\n\nPor favor verifica la ruta o selecciona una válida.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!File.Exists(txtDatabase.Text))
            {
                MessageBox.Show("El archivo database.xml no existe en la ruta indicada.\n\nPor favor verifica la ruta o selecciona tu archivo database.xml.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Guardar configuración persistente
            AppSettings.VdjHistoryFolder = txtHistory.Text.Trim();
            AppSettings.VdjDatabaseFile = txtDatabase.Text.Trim();
            AppSettings.Save();

            DiagLog.Write($"[SetupForm] Configuración guardada manualmente: DB='{AppSettings.VdjDatabaseFile}', Historial='{AppSettings.VdjHistoryFolder}'");

            // Abrir MainForm
            this.Hide();
            var mainForm = new MainForm();
            mainForm.FormClosed += (s, args) => this.Close();
            mainForm.Show();
        }
    }
}
