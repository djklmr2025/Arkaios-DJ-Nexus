using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;

namespace ArkaiosDJAssistant
{
    public class SettingsForm : Form
    {
        private TabControl tabControl;
        private CheckBox chkTransparency;
        private CheckBox chkAdvancedTabs;
        private ComboBox cboAudioDevice;
        private TextBox txtVdjExe;
        private ListBox lstFolders;

        public SettingsForm()
        {
            this.Text = "Settings";
            this.Size = new Size(600, 450);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(120, 30), SizeMode = TabSizeMode.Fixed, Padding = new Point(15, 5) };
            this.Controls.Add(tabControl);

            // General Tab
            TabPage tabGeneral = new TabPage("General") { BackColor = Color.FromArgb(30, 30, 30) };
            tabControl.TabPages.Add(tabGeneral);

            Label lblGenTitle = new Label { Text = "Extra Features", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            tabGeneral.Controls.Add(lblGenTitle);

            chkTransparency = new CheckBox 
            { 
                Text = "Enable window transparency\nLet the compact and minimal modes go semi-transparent (becomes opaque when you hover)",
                Location = new Point(20, 60),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Checked = AppSettings.EnableTransparency
            };
            tabGeneral.Controls.Add(chkTransparency);

            Label lblVdjExe = new Label { Text = "VirtualDJ Executable Path:", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 110), AutoSize = true };
            tabGeneral.Controls.Add(lblVdjExe);

            txtVdjExe = new TextBox { Text = AppSettings.VdjExecutableFile, Location = new Point(20, 135), Width = 400, Font = new Font("Segoe UI", 10), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            tabGeneral.Controls.Add(txtVdjExe);

            Button btnBrowseExe = new Button { Text = "Browse", Location = new Point(430, 134), Width = 80, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50) };
            btnBrowseExe.Click += (s, ev) => 
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*", Title = "Select VirtualDJ Executable" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        txtVdjExe.Text = ofd.FileName;
                    }
                }
            };
            tabGeneral.Controls.Add(btnBrowseExe);

            // Options Tab
            TabPage tabOptions = new TabPage("Opciones") { BackColor = Color.FromArgb(30, 30, 30) };
            tabControl.TabPages.Add(tabOptions);

            Label lblOptionsTitle = new Label { Text = "Interfaz", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            tabOptions.Controls.Add(lblOptionsTitle);

            chkAdvancedTabs = new CheckBox
            {
                Text = "Modo avanzado\nMuestra Buscar y descargar, Hits / Plataformas, Descargas / Hub local y Organizador IA.",
                Location = new Point(20, 60),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.LightGray,
                Checked = AppSettings.ShowAdvancedTabs
            };
            tabOptions.Controls.Add(chkAdvancedTabs);

            Label lblAudioTitle = new Label { Text = "Salida de audio para preview local", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(20, 135), AutoSize = true };
            tabOptions.Controls.Add(lblAudioTitle);

            cboAudioDevice = new ComboBox
            {
                Location = new Point(20, 165),
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            foreach (string device in AudioDeviceCatalog.GetOutputDevices())
                cboAudioDevice.Items.Add(device);
            string selectedAudio = string.IsNullOrWhiteSpace(AppSettings.PreviewAudioDevice) ? AudioDeviceCatalog.DefaultDevice : AppSettings.PreviewAudioDevice;
            int selectedIndex = cboAudioDevice.FindStringExact(selectedAudio);
            if (selectedIndex < 0)
            {
                cboAudioDevice.Items.Add(selectedAudio);
                selectedIndex = cboAudioDevice.Items.Count - 1;
            }
            cboAudioDevice.SelectedIndex = Math.Max(0, selectedIndex);
            tabOptions.Controls.Add(cboAudioDevice);

            string vdjDevice = AudioDeviceCatalog.GetVirtualDjHeadphoneDevice();
            Label lblAudioNote = new Label
            {
                Text = "Detectado VDJ audifonos: " + (string.IsNullOrWhiteSpace(vdjDevice) ? "no detectado" : vdjDevice) + "\nNota: el motor liviano usa Windows default si el reproductor interno no permite ruteo directo.",
                Location = new Point(20, 200),
                Width = 520,
                Height = 60,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8)
            };
            tabOptions.Controls.Add(lblAudioNote);

            // Library Tab
            TabPage tabLibrary = new TabPage("Library") { BackColor = Color.FromArgb(30, 30, 30) };
            tabControl.TabPages.Add(tabLibrary);

            Label lblLibTitle = new Label { Text = "Music Folders", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            tabLibrary.Controls.Add(lblLibTitle);

            Label lblLibDesc = new Label { Text = "We scan these folders for songs and turn them into drag-and-drop recommendations.\n(Leave empty to scan entire VirtualDJ database).", Font = new Font("Segoe UI", 9), ForeColor = Color.LightGray, Location = new Point(20, 45), AutoSize = true };
            tabLibrary.Controls.Add(lblLibDesc);

            Button btnAddFolder = new Button { Text = "+ Add Folder", Location = new Point(450, 20), Width = 100, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50) };
            btnAddFolder.Click += BtnAddFolder_Click;
            tabLibrary.Controls.Add(btnAddFolder);

            lstFolders = new ListBox 
            { 
                Location = new Point(20, 90), 
                Width = 530, 
                Height = 220, 
                BackColor = Color.FromArgb(40, 40, 40), 
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None
            };
            foreach (var f in AppSettings.AllowedFolders) lstFolders.Items.Add(f);
            tabLibrary.Controls.Add(lstFolders);

            Button btnRemoveFolder = new Button { Text = "Remove Selected", Location = new Point(420, 320), Width = 130, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50) };
            btnRemoveFolder.Click += BtnRemoveFolder_Click;
            tabLibrary.Controls.Add(btnRemoveFolder);

            // License Tab
            TabPage tabLicense = new TabPage("Licencia") { BackColor = Color.FromArgb(30, 30, 30) };
            tabControl.TabPages.Add(tabLicense);

            Label lblLicTitle = new Label { Text = "Cómo obtener tu licencia", Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true };
            tabLicense.Controls.Add(lblLicTitle);

            Label lblLicDesc = new Label
            {
                Text = "Arkaios DJ Nexus requiere una licencia activa por dispositivo (HWID) o una\n" +
                       "licencia Vitalicia Universal. Para obtener tu serial debes registrarte en el\n" +
                       "portal oficial de Arkaios World y luego pegar la clave recibida en la\n" +
                       "ventana de activación.",
                Location = new Point(20, 55),
                Width = 520,
                Height = 70,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };
            tabLicense.Controls.Add(lblLicDesc);

            Label lblStep1 = new Label { Text = "1. Regístrate para obtener tu serial:", Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(20, 135), AutoSize = true };
            tabLicense.Controls.Add(lblStep1);

            LinkLabel lnkArkaiosWorld = new LinkLabel
            {
                Text = "Arkaios-Expo (arkaios-world.web.app)",
                Location = new Point(20, 158),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Underline),
                LinkColor = Color.DeepSkyBlue,
                VisitedLinkColor = Color.DeepSkyBlue
            };
            lnkArkaiosWorld.Links.Add(0, lnkArkaiosWorld.Text.Length, "https://arkaios-world.web.app/");
            lnkArkaiosWorld.LinkClicked += (s, ev) => Process.Start(ev.Link.LinkData.ToString());
            tabLicense.Controls.Add(lnkArkaiosWorld);

            Label lblStep2 = new Label { Text = "2. Portal / estado del servicio de licencias:", Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(20, 190), AutoSize = true };
            tabLicense.Controls.Add(lblStep2);

            LinkLabel lnkPortal = new LinkLabel
            {
                Text = "servidor-arkaios-api.vercel.app",
                Location = new Point(20, 213),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Underline),
                LinkColor = Color.DeepSkyBlue,
                VisitedLinkColor = Color.DeepSkyBlue
            };
            lnkPortal.Links.Add(0, lnkPortal.Text.Length, "https://servidor-arkaios-api.vercel.app/");
            lnkPortal.LinkClicked += (s, ev) => Process.Start(ev.Link.LinkData.ToString());
            tabLicense.Controls.Add(lnkPortal);

            Label lblHwidTitle = new Label { Text = "Tu Hardware ID (lo necesitas para el registro):", Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(20, 250), AutoSize = true };
            tabLicense.Controls.Add(lblHwidTitle);

            TextBox txtHwid = new TextBox
            {
                Text = LicenseManager.GetHardwareId(),
                Location = new Point(20, 273),
                Width = 400,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.Cyan
            };
            tabLicense.Controls.Add(txtHwid);

            Button btnActivateNow = new Button { Text = "Ingresar clave de licencia", Location = new Point(20, 310), Width = 200, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215) };
            btnActivateNow.Click += (s, ev) =>
            {
                using (var activation = new ActivationForm())
                {
                    activation.ShowDialog(this);
                }
            };
            tabLicense.Controls.Add(btnActivateNow);

            // Bottom Panel
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(25, 25, 25) };
            this.Controls.Add(pnlBottom);
            
            Button btnSave = new Button { Text = "Save", Location = new Point(this.Width - 140, 15), Width = 100, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215) };
            btnSave.Click += BtnSave_Click;
            pnlBottom.Controls.Add(btnSave);
        }

        private void BtnAddFolder_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    if (!lstFolders.Items.Contains(fbd.SelectedPath))
                    {
                        lstFolders.Items.Add(fbd.SelectedPath);
                    }
                }
            }
        }

        private void BtnRemoveFolder_Click(object sender, EventArgs e)
        {
            if (lstFolders.SelectedIndex >= 0)
            {
                lstFolders.Items.RemoveAt(lstFolders.SelectedIndex);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            AppSettings.EnableTransparency = chkTransparency.Checked;
            AppSettings.ShowAdvancedTabs = chkAdvancedTabs.Checked;
            AppSettings.PreviewAudioDevice = cboAudioDevice.SelectedItem == null ? AudioDeviceCatalog.DefaultDevice : cboAudioDevice.SelectedItem.ToString();
            AppSettings.VdjExecutableFile = txtVdjExe.Text;
            AppSettings.AllowedFolders.Clear();
            foreach (var item in lstFolders.Items)
            {
                AppSettings.AllowedFolders.Add(item.ToString());
            }
            AppSettings.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
