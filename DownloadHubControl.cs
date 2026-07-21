using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private readonly Label countLabel;
        private readonly ProgressBar progressBar;
        private readonly FlowLayoutPanel toolbar;
        private readonly Button refreshButton;
        private readonly PreviewPlayerBar previewBar;
        private readonly List<HubFile> files = new List<HubFile>();
        private readonly ContextMenuStrip fileMenu;
        private int sortColumn = 0;
        private bool sortAscending = true;

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
            countLabel = new Label { AutoSize = true, ForeColor = Color.LightGreen, Padding = new Padding(8, 6, 8, 0), Text = "Total: 0" };
            var scanButton = new Button { Text = "Reescanear Hub", AutoSize = true };
            scanButton.Click += (s, e) => Scan();
            refreshButton = new Button { Text = "Actualizar biblioteca y Camelot", AutoSize = true };
            refreshButton.Click += (s, e) => { var handler = RefreshLibraryRequested; if (handler != null) handler(); };
            var renameButton = new Button { Text = "Renombrar seleccionados", AutoSize = true };
            renameButton.Click += async (s, e) => await RenameSelectedAsync();
            var manualRenameButton = new Button { Text = "Renombrar manual", AutoSize = true };
            manualRenameButton.Click += (s, e) => RenameManualSelected();
            var undoRenameButton = new Button { Text = "Deshacer renombrado", AutoSize = true };
            undoRenameButton.Click += async (s, e) => await UndoLastRenameBatchAsync();
            var previewButton = new Button { Text = "Preview Track", AutoSize = true };
            previewButton.Click += (s, e) => PreviewTrack();
            var folderButton = new Button { Text = "Abrir carpeta", AutoSize = true };
            folderButton.Click += (s, e) => OpenSelectedFolder();
            toolbar.Controls.AddRange(new Control[] { searchBox, typeBox, countLabel, scanButton, refreshButton, renameButton, manualRenameButton, undoRenameButton, previewButton, folderButton });

            fileList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true, BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White };
            fileList.Columns.Add("Nombre real", 390);
            fileList.Columns.Add("Tipo", 90);
            fileList.Columns.Add("Formato", 80);
            fileList.Columns.Add("Tamaño", 90);
            fileList.Columns.Add("Origen", 100);
            fileList.Columns.Add("Obtenido", 145);
            fileList.Columns.Add("Carpeta", 260);
            fileList.ColumnClick += (s, e) => { SortByColumn(e.Column); ApplyFilter(); };
            fileList.ItemDrag += FileList_ItemDrag;
            fileList.MouseDoubleClick += (s, e) => PreviewSelected();
            fileList.MouseDown += FileList_MouseDown;
            fileMenu = new ContextMenuStrip();
            fileMenu.Items.Add("Reproducir audio", null, (s, e) => PreviewTrack());
            fileMenu.Items.Add("Pausar", null, (s, e) => PausePreview());
            fileMenu.Items.Add("Continuar", null, (s, e) => ResumePreview());
            fileMenu.Items.Add("Detener", null, (s, e) => StopPreview());
            fileMenu.Items.Add(new ToolStripSeparator());
            fileMenu.Items.Add("Renombrar automatico", null, async (s, e) => await RenameSelectedAsync());
            fileMenu.Items.Add("Renombrar manual", null, (s, e) => RenameManualSelected());
            fileMenu.Items.Add(new ToolStripSeparator());
            fileMenu.Items.Add("Crear playlist VDJ", null, (s, e) => CreateVdjPlaylistFromSelection());
            fileMenu.Items.Add("Abrir carpeta", null, (s, e) => OpenSelectedFolder());
            fileList.ContextMenuStrip = fileMenu;
            previewBar = new PreviewPlayerBar();
            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(8), ForeColor = Color.LightGray };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };

            Controls.Add(fileList);
            Controls.Add(previewBar);
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
                    Dictionary<string, DownloadRecord> downloaded = DownloadRegistry.ExistingRecords()
                        .ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
                    var scanned = new List<HubFile>();
                    scanned.AddRange(ScanFolder("Music", AppSettings.GetDownloadFolder("music"), downloaded));
                    scanned.AddRange(ScanFolder("Video", AppSettings.GetDownloadFolder("video"), downloaded));
                    scanned.AddRange(ScanFolder("Karaoke", AppSettings.GetDownloadFolder("karaoke"), downloaded));
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
            DownloadRegistry.Register(path, "", Path.GetFileNameWithoutExtension(path), "", GuessMediaType(path));
            Scan();
        }

        private static string GuessMediaType(string path)
        {
            if (Path.GetDirectoryName(path).IndexOf("KARAOKE", StringComparison.OrdinalIgnoreCase) >= 0) return "Karaoke";
            return IsVideo(path) ? "Video" : "Music";
        }

        private static List<HubFile> ScanFolder(string type, string folder, Dictionary<string, DownloadRecord> downloaded)
        {
            var found = new List<HubFile>();
            if (!Directory.Exists(folder)) return found;
            string[] allowed = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".mp4", ".mkv", ".webm", ".avi", ".mov" };
            try
            {
                foreach (string path in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    if (!allowed.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
                    DownloadRecord record;
                    downloaded.TryGetValue(path, out record);
                    found.Add(new HubFile { Path = path, Type = type, DownloadRecord = record, IsInternetDownload = record != null });
                }
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
            List<HubFile> visible = files.Where(f => (type == "Todos" || f.Type == type) && (query.Length == 0 || Path.GetFileNameWithoutExtension(f.Path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            SortVisibleFiles(visible);
            foreach (HubFile file in visible)
            {
                var info = new FileInfo(file.Path);
                var item = new ListViewItem(Path.GetFileNameWithoutExtension(file.Path));
                item.SubItems.Add(file.Type);
                item.SubItems.Add(info.Extension.ToLowerInvariant());
                item.SubItems.Add(string.Format("{0:0.0} MB", info.Length / 1048576.0));
                item.SubItems.Add(file.IsInternetDownload ? "Internet" : "Local");
                item.SubItems.Add(file.IsInternetDownload ? file.DownloadRecord.DownloadedAt.ToString("yyyy-MM-dd HH:mm") : "-");
                item.SubItems.Add(info.DirectoryName);
                if (file.IsInternetDownload)
                {
                    item.ForeColor = Color.LightGreen;
                    item.BackColor = Color.FromArgb(22, 48, 28);
                }
                item.Tag = file.Path;
                fileList.Items.Add(item);
            }
            fileList.EndUpdate();
            string countText = string.Format("Visibles: {0} / Total: {1}", fileList.Items.Count, files.Count);
            countLabel.Text = countText;
            int internetCount = visible.Count(f => f.IsInternetDownload);
            statusLabel.Text = countText + ". Recientes internet: " + internetCount + ". Clic en encabezados ordena; doble clic prescucha; arrastra al plato.";
        }

        private void SortByColumn(int column)
        {
            if (sortColumn == column) sortAscending = !sortAscending;
            else
            {
                sortColumn = column;
                sortAscending = true;
            }
        }

        private void SortVisibleFiles(List<HubFile> visible)
        {
            visible.Sort((a, b) =>
            {
                int value;
                if (sortColumn == 1) value = string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
                else if (sortColumn == 2) value = string.Compare(Path.GetExtension(a.Path), Path.GetExtension(b.Path), StringComparison.OrdinalIgnoreCase);
                else if (sortColumn == 3) value = GetFileLength(a.Path).CompareTo(GetFileLength(b.Path));
                else if (sortColumn == 4) value = a.IsInternetDownload.CompareTo(b.IsInternetDownload);
                else if (sortColumn == 5) value = a.DownloadedAt.CompareTo(b.DownloadedAt);
                else if (sortColumn == 6) value = string.Compare(Path.GetDirectoryName(a.Path), Path.GetDirectoryName(b.Path), StringComparison.OrdinalIgnoreCase);
                else value = string.Compare(Path.GetFileNameWithoutExtension(a.Path), Path.GetFileNameWithoutExtension(b.Path), StringComparison.OrdinalIgnoreCase);
                if (a.IsInternetDownload != b.IsInternetDownload)
                    return a.IsInternetDownload ? -1 : 1;
                if (a.IsInternetDownload && b.IsInternetDownload && sortColumn != 5)
                {
                    int recent = b.DownloadedAt.CompareTo(a.DownloadedAt);
                    if (recent != 0) return recent;
                }
                return sortAscending ? value : -value;
            });
        }

        private static long GetFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private void PreviewSelected()
        {
            PreviewTrack();
        }

        private void PreviewTrack()
        {
            if (fileList.SelectedItems.Count == 0)
            {
                statusLabel.Text = "Selecciona un track para previsualizar.";
                return;
            }

            string path = (string)fileList.SelectedItems[0].Tag;
            string headphone = GetVirtualDjHeadphoneDevice();
            string error;
            if (previewBar.PlayTrack(path, out error))
            {
                bool isVideo = IsVideo(path);
                statusLabel.Text = string.IsNullOrWhiteSpace(headphone)
                    ? (isVideo ? "Prescuchando audio del video dentro de ARKAIOS. No se detecto salida de audifonos VDJ." : "Preview real reproduciendo dentro de ARKAIOS. No se detecto salida de audifonos VDJ.")
                    : (isVideo ? "Prescuchando audio del video dentro de ARKAIOS. Audifonos VDJ detectados: " + headphone + "." : "Preview real reproduciendo dentro de ARKAIOS. Audifonos VDJ detectados: " + headphone + ".");
            }
            else
            {
                statusLabel.Text = "No se pudo previsualizar: " + error;
            }
        }

        private void PausePreview()
        {
            string error;
            statusLabel.Text = previewBar.PauseCurrent(out error) ? "Preview pausado." : "No se pudo pausar: " + error;
        }

        private void ResumePreview()
        {
            string error;
            statusLabel.Text = previewBar.ResumeCurrent(out error) ? "Preview reproduciendo." : "No se pudo continuar: " + error;
        }

        private void StopPreview()
        {
            previewBar.StopCurrent();
            statusLabel.Text = "Preview detenido.";
        }

        private void FileList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            ListViewItem item = fileList.GetItemAt(e.X, e.Y);
            if (item == null) return;
            if (!item.Selected)
            {
                fileList.SelectedItems.Clear();
                item.Selected = true;
            }
        }

        private void OpenSelectedFolder()
        {
            string folder = fileList.SelectedItems.Count > 0 ? Path.GetDirectoryName((string)fileList.SelectedItems[0].Tag) : AppSettings.MediaLibraryRoot;
            if (Directory.Exists(folder)) Process.Start("explorer.exe", folder);
        }

        private async Task RenameSelectedAsync()
        {
            var selected = fileList.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag).Where(File.Exists).ToList();
            if (selected.Count == 0)
            {
                statusLabel.Text = "Selecciona uno o mas archivos del Hub para renombrar.";
                return;
            }

            DialogResult answer = MessageBox.Show("Se validara metadata y se renombraran " + selected.Count + " archivos seleccionados.\n\nContinuar?", "Renombrar Hub local", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            SetBusy(true, "Renombrando archivos seleccionados del Hub...");
            OperationProgressDialog progress = new OperationProgressDialog("Renombrando Hub local");
            progress.Show(this);
            int changed = 0;
            try
            {
                changed = await RenameFilesWithMetadataAsync(selected, progress);
            }
            finally
            {
                SetBusy(false, null);
            }
            progress.SetResult("Resultado: " + changed + " archivos renombrados en el Hub.");
            await Task.Delay(600);
            progress.Close();
            progress.Dispose();
            Scan();
        }

        private void RenameManualSelected()
        {
            var selected = fileList.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag).Where(File.Exists).ToList();
            if (selected.Count == 0)
            {
                statusLabel.Text = "Selecciona uno o mas archivos del Hub para renombrar manualmente.";
                return;
            }

            using (var dialog = new ManualRenameDialog(selected))
            {
                dialog.ShowDialog(this);
            }
            Scan();
        }

        private async Task<int> RenameFilesWithMetadataAsync(List<string> selected, OperationProgressDialog progress)
        {
            string historyFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rename-history");
            Directory.CreateDirectory(historyFolder);
            string historyPath = Path.Combine(historyFolder, "hub-rename-history-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".tsv");
            var log = new List<string> { "old\tnew" };
            int changed = 0;

            for (int i = 0; i < selected.Count; i++)
            {
                string path = selected[i];
                progress.SetBatchStatus(i + 1, selected.Count, "Validando metadata", Path.GetFileNameWithoutExtension(path));
                string suggestion = await GetValidatedNameAsync(path);
                if (string.IsNullOrWhiteSpace(suggestion)) continue;

                string target = Path.Combine(Path.GetDirectoryName(path), suggestion + Path.GetExtension(path).ToLowerInvariant());
                if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase) || File.Exists(target)) continue;

                progress.SetBatchStatus(i + 1, selected.Count, "Renombrando archivo", Path.GetFileName(target));
                try
                {
                    File.Move(path, target);
                    log.Add(path + "\t" + target);
                    changed++;
                }
                catch { }
            }

            if (changed > 0) File.WriteAllLines(historyPath, log.ToArray(), Encoding.UTF8);
            return changed;
        }

        private static async Task<string> GetValidatedNameAsync(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string mediaType = IsVideo(path) ? "video" : "music";
            string cleanedQuery = CleanTitle(name);
            MetadataNameSuggestion resolved = await MetadataNameResolver.ResolveAsync(path, cleanedQuery, mediaType);
            string suggestion = resolved == null ? null : resolved.Name;
            if (string.IsNullOrWhiteSpace(suggestion)) suggestion = cleanedQuery;
            suggestion = CleanTitle(suggestion);
            if (string.IsNullOrWhiteSpace(suggestion) || suggestion.Length < 4) return null;
            return SanitizeFileName(suggestion);
        }

        private async Task UndoLastRenameBatchAsync()
        {
            string historyFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rename-history");
            if (!Directory.Exists(historyFolder))
            {
                statusLabel.Text = "No hay historial de renombrado del Hub.";
                return;
            }

            string last = Directory.GetFiles(historyFolder, "hub-rename-history-*.tsv").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
            if (string.IsNullOrEmpty(last))
            {
                statusLabel.Text = "No hay lote del Hub para deshacer.";
                return;
            }

            DialogResult answer = MessageBox.Show("Deshacer ultimo renombrado del Hub?\n" + Path.GetFileName(last), "Deshacer Hub local", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            int restored = await Task.Run(() => UndoBatch(last));
            statusLabel.Text = "Deshacer terminado: " + restored + " archivos restaurados.";
            Scan();
        }

        private static int UndoBatch(string historyPath)
        {
            int restored = 0;
            foreach (string line in File.ReadAllLines(historyPath, Encoding.UTF8).Skip(1).Reverse())
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 2) continue;
                if (!File.Exists(parts[1]) || File.Exists(parts[0])) continue;
                try { File.Move(parts[1], parts[0]); restored++; }
                catch { }
            }
            return restored;
        }

        private static bool IsVideo(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".mp4" || extension == ".mkv" || extension == ".webm" || extension == ".avi" || extension == ".mov";
        }

        private static string CleanTitle(string value)
        {
            string text = value ?? "";
            text = text.Replace('_', ' ');
            text = Regex.Replace(text, @"\s+", " ").Trim();
            text = Regex.Replace(text, @"^\s*\(?\d{1,4}\)?\s*[-_. ]+", "");
            text = Regex.Replace(text, @"\[[A-Za-z0-9_-]{8,15}\]$", "").Trim();
            text = Regex.Replace(text, @"\s*\((official\s+audio|official\s+video|official\s+music\s+video|lyrics?|visualizer|hd|hq|320kbps|mp3|m4a)\)\s*", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s*\[(official\s+audio|official\s+video|official\s+music\s+video|lyrics?|visualizer|hd|hq|320kbps|mp3|m4a)\]\s*", " ", RegexOptions.IgnoreCase);
            return Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '.');
        }

        private static string SanitizeFileName(string value)
        {
            string safe = value;
            foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, ' ');
            safe = Regex.Replace(safe, @"\s+", " ").Trim(' ', '.', '-');
            if (safe.Length > 150) safe = safe.Substring(0, 150).Trim();
            return safe;
        }

        private static string GetVirtualDjHeadphoneDevice()
        {
            try
            {
                string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VirtualDJ", "settings.xml");
                if (!File.Exists(settings))
                {
                    string spanishDocs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documentos", "VirtualDJ", "settings.xml");
                    settings = spanishDocs;
                }
                if (!File.Exists(settings)) return "";

                string xml = File.ReadAllText(settings, Encoding.UTF8);
                Match match = Regex.Match(xml, "source=\"headphones\"[^>]*soundcard=\"([^\"]+)\"|soundcard=\"([^\"]+)\"[^>]*source=\"headphones\"", RegexOptions.IgnoreCase);
                if (!match.Success) return "";
                string raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                Match name = Regex.Match(raw, @"\(([^)]+)\)");
                return name.Success ? name.Groups[1].Value : raw;
            }
            catch { return ""; }
        }

        private void CreateVdjPlaylistFromSelection()
        {
            var selected = fileList.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag).Where(File.Exists).ToList();
            if (selected.Count == 0)
            {
                statusLabel.Text = "Selecciona tracks para crear una playlist de VirtualDJ.";
                return;
            }

            try
            {
                string playlistFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documentos", "VirtualDJ", "Playlists");
                if (!Directory.Exists(playlistFolder))
                    playlistFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VirtualDJ", "Playlists");
                Directory.CreateDirectory(playlistFolder);

                string playlistPath = Path.Combine(playlistFolder, "ARKAIOS Hub " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".m3u");
                var lines = new List<string> { "#EXTM3U" };
                foreach (string path in selected) lines.Add(path);
                File.WriteAllLines(playlistPath, lines.ToArray(), Encoding.UTF8);
                statusLabel.Text = "Playlist VDJ creada: " + playlistPath;
                Process.Start("explorer.exe", playlistFolder);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "No se pudo crear playlist VDJ: " + ex.Message;
            }
        }

        private void FileList_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var paths = fileList.SelectedItems.Cast<ListViewItem>().Select(item => (string)item.Tag).Where(File.Exists).ToArray();
            if (paths.Length > 0) fileList.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
        }

        private class HubFile
        {
            public string Path;
            public string Type;
            public DownloadRecord DownloadRecord;
            public bool IsInternetDownload;
            public DateTime DownloadedAt
            {
                get { return DownloadRecord == null ? DateTime.MinValue : DownloadRecord.DownloadedAt; }
            }
        }
    }
}
