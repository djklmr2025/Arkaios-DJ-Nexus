using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class DownloadHubControl : UserControl
    {
        public event Action RefreshLibraryRequested;
        private readonly TextBox searchBox;
        private readonly ComboBox typeBox;
        private readonly ListView fileList;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly FlowLayoutPanel toolbar;
        private readonly Button refreshButton;
        private readonly List<HubFile> files = new List<HubFile>();

        public DownloadHubControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);

            toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8), WrapContents = false };
            searchBox = new TextBox { Width = 320 };
            searchBox.TextChanged += (s, e) => ApplyFilter();
            typeBox = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Todos", "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            var scanButton = new Button { Text = "Reescanear Hub", AutoSize = true };
            scanButton.Click += (s, e) => Scan();
            refreshButton = new Button { Text = "Actualizar biblioteca y Camelot", AutoSize = true };
            refreshButton.Click += (s, e) => { var handler = RefreshLibraryRequested; if (handler != null) handler(); };
            var folderButton = new Button { Text = "Abrir carpeta", AutoSize = true };
            folderButton.Click += (s, e) => OpenSelectedFolder();
            toolbar.Controls.AddRange(new Control[] { searchBox, typeBox, scanButton, refreshButton, folderButton });

            fileList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White };
            fileList.Columns.Add("Nombre real", 390);
            fileList.Columns.Add("Tipo", 90);
            fileList.Columns.Add("Formato", 80);
            fileList.Columns.Add("Tamaño", 90);
            fileList.Columns.Add("Carpeta", 260);
            fileList.ItemDrag += FileList_ItemDrag;
            fileList.MouseDoubleClick += (s, e) => PreviewSelected();
            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(8), ForeColor = Color.LightGray };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };

            Controls.Add(fileList);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            Load += (s, e) => Scan();
        }

        public async void Scan()
        {
            SetBusy(true, "Escaneando archivos reales de Music, Video y Karaoke...");
            try
            {
                List<HubFile> found = await Task.Run(() =>
                {
                    var scanned = new List<HubFile>();
                    scanned.AddRange(ScanFolder("Music", AppSettings.GetDownloadFolder("music")));
                    scanned.AddRange(ScanFolder("Video", AppSettings.GetDownloadFolder("video")));
                    scanned.AddRange(ScanFolder("Karaoke", AppSettings.GetDownloadFolder("karaoke")));
                    return scanned;
                });
                files.Clear();
                files.AddRange(found);
                ApplyFilter();
            }
            catch (Exception ex) { statusLabel.Text = "No se pudo completar el escaneo: " + ex.Message; }
            finally { SetBusy(false, null); }
        }

        public void AddDownloadedFile(string path)
        {
            if (!File.Exists(path)) return;
            Scan();
        }

        private static List<HubFile> ScanFolder(string type, string folder)
        {
            var found = new List<HubFile>();
            if (!Directory.Exists(folder)) return found;
            string[] allowed = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".mp4", ".mkv", ".webm", ".avi", ".mov" };
            try
            {
                foreach (string path in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                    if (allowed.Contains(Path.GetExtension(path).ToLowerInvariant())) found.Add(new HubFile { Path = path, Type = type });
            }
            catch { }
            return found;
        }

        private void SetBusy(bool busy, string message)
        {
            progressBar.Visible = busy;
            toolbar.Enabled = !busy;
            if (!string.IsNullOrEmpty(message)) statusLabel.Text = message;
        }

        public void SetLibraryRefreshBusy(bool busy, string message)
        {
            SetBusy(busy, message);
            refreshButton.Text = busy ? "Actualizando..." : "Actualizar biblioteca y Camelot";
        }

        private void ApplyFilter()
        {
            if (fileList == null) return;
            string query = searchBox.Text.Trim();
            string type = typeBox.SelectedItem == null ? "Todos" : typeBox.SelectedItem.ToString();
            fileList.BeginUpdate();
            fileList.Items.Clear();
            foreach (HubFile file in files.Where(f => (type == "Todos" || f.Type == type) && (query.Length == 0 || Path.GetFileNameWithoutExtension(f.Path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(f => Path.GetFileName(f.Path)))
            {
                var info = new FileInfo(file.Path);
                var item = new ListViewItem(Path.GetFileNameWithoutExtension(file.Path));
                item.SubItems.Add(file.Type);
                item.SubItems.Add(info.Extension.ToLowerInvariant());
                item.SubItems.Add(string.Format("{0:0.0} MB", info.Length / 1048576.0));
                item.SubItems.Add(info.DirectoryName);
                item.Tag = file.Path;
                fileList.Items.Add(item);
            }
            fileList.EndUpdate();
            statusLabel.Text = string.Format("{0} archivos visibles de {1} encontrados. Doble clic previsualiza; arrastra al plato.", fileList.Items.Count, files.Count);
        }

        private void PreviewSelected()
        {
            if (fileList.SelectedItems.Count == 0) return;
            Process.Start((string)fileList.SelectedItems[0].Tag);
        }

        private void OpenSelectedFolder()
        {
            string folder = fileList.SelectedItems.Count > 0 ? Path.GetDirectoryName((string)fileList.SelectedItems[0].Tag) : AppSettings.MediaLibraryRoot;
            if (Directory.Exists(folder)) Process.Start("explorer.exe", folder);
        }

        private void FileList_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var paths = fileList.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag).Where(File.Exists).ToArray();
            if (paths.Length > 0) fileList.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
        }

        private class HubFile { public string Path; public string Type; }
    }
}
