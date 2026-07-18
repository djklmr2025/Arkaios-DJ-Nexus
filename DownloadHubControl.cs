using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private readonly List<HubFile> files = new List<HubFile>();

        public DownloadHubControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8), WrapContents = false };
            searchBox = new TextBox { Width = 320 };
            searchBox.TextChanged += (s, e) => ApplyFilter();
            typeBox = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Todos", "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            var scanButton = new Button { Text = "Reescanear Hub", AutoSize = true };
            scanButton.Click += (s, e) => Scan();
            var refreshButton = new Button { Text = "Actualizar biblioteca y Camelot", AutoSize = true };
            refreshButton.Click += (s, e) => { var handler = RefreshLibraryRequested; if (handler != null) handler(); };
            var folderButton = new Button { Text = "Abrir carpeta", AutoSize = true };
            folderButton.Click += (s, e) => OpenSelectedFolder();
            top.Controls.AddRange(new Control[] { searchBox, typeBox, scanButton, refreshButton, folderButton });

            fileList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White };
            fileList.Columns.Add("Nombre real", 390);
            fileList.Columns.Add("Tipo", 90);
            fileList.Columns.Add("Formato", 80);
            fileList.Columns.Add("Tamaño", 90);
            fileList.Columns.Add("Carpeta", 260);
            fileList.ItemDrag += FileList_ItemDrag;
            fileList.MouseDoubleClick += (s, e) => PreviewSelected();
            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(8), ForeColor = Color.LightGray };

            Controls.Add(fileList);
            Controls.Add(statusLabel);
            Controls.Add(top);
            Load += (s, e) => Scan();
        }

        public void Scan()
        {
            files.Clear();
            AddFolder("Music", AppSettings.GetDownloadFolder("music"));
            AddFolder("Video", AppSettings.GetDownloadFolder("video"));
            AddFolder("Karaoke", AppSettings.GetDownloadFolder("karaoke"));
            ApplyFilter();
        }

        public void AddDownloadedFile(string path)
        {
            if (!File.Exists(path)) return;
            Scan();
        }

        private void AddFolder(string type, string folder)
        {
            if (!Directory.Exists(folder)) return;
            string[] allowed = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".mp4", ".mkv", ".webm", ".avi", ".mov" };
            try
            {
                foreach (string path in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                    if (allowed.Contains(Path.GetExtension(path).ToLowerInvariant())) files.Add(new HubFile { Path = path, Type = type });
            }
            catch (Exception ex) { statusLabel.Text = "No se pudo escanear " + folder + ": " + ex.Message; }
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
