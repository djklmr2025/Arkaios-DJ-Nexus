using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class AllTracksSearchControl : UserControl
    {
        public event Action<string> TrackSentToHub;

        private readonly TextBox searchBox;
        private readonly ComboBox typeBox;
        private readonly CheckBox internetFallbackBox;
        private readonly Button searchButton;
        private readonly Button downloadButton;
        private readonly Button findButton;
        private readonly Button clearButton;
        private readonly ListView resultList;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly PreviewPlayerBar previewBar;
        private readonly string[] allowedExtensions = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".mp4", ".mkv", ".webm", ".avi", ".mov" };
        private int sortColumn = 0;
        private bool sortAscending = true;
        private int searchGeneration = 0;
        private ListViewGroup localResultGroup;
        private ListViewGroup internetResultGroup;

        public AllTracksSearchControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), WrapContents = false };
            searchBox = new TextBox { Width = 360 };
            searchBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await SearchAsync();
                }
            };

            typeBox = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Todos", "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;

            internetFallbackBox = new CheckBox { Text = "Internet", AutoSize = true, Checked = true, ForeColor = Color.LightGray, Padding = new Padding(8, 5, 0, 0) };
            searchButton = new Button { Text = "Buscar All Tracks", AutoSize = true };
            searchButton.Click += async (s, e) => await SearchAsync();
            downloadButton = new Button { Text = "Descargar seleccionado", AutoSize = true };
            downloadButton.Click += async (s, e) => await DownloadSelectedOnlineAsync();
            findButton = new Button { Text = "Encontrar archivo", AutoSize = true };
            findButton.Click += (s, e) => FindSelectedFile();
            clearButton = new Button { Text = "Limpiar", AutoSize = true };
            clearButton.Click += (s, e) => ClearResults();

            toolbar.Controls.AddRange(new Control[] { searchBox, typeBox, internetFallbackBox, searchButton, downloadButton, findButton, clearButton });

            resultList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                ShowGroups = true
            };
            resultList.Columns.Add("Nombre real", 360);
            resultList.Columns.Add("Tipo", 90);
            resultList.Columns.Add("Formato", 80);
            resultList.Columns.Add("Tamano", 90);
            resultList.Columns.Add("Origen", 140);
            resultList.Columns.Add("Ubicacion", 320);
            resultList.Columns.Add("Accion", 160);
            resultList.ColumnClick += (s, e) => { SortByColumn(e.Column); SortCurrentRows(); };
            resultList.MouseDoubleClick += async (s, e) => await HandleDoubleClickAsync();
            resultList.SelectedIndexChanged += (s, e) => UpdateSelectedStatus();
            resultList.ItemDrag += ResultList_ItemDrag;
            resultList.MouseDown += ResultList_MouseDown;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Prescuchar", null, (s, e) => PreviewSelected());
            menu.Items.Add("Pausar", null, (s, e) => PausePreview());
            menu.Items.Add("Continuar", null, (s, e) => ResumePreview());
            menu.Items.Add("Detener", null, (s, e) => StopPreview());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Descargar audio/video", null, async (s, e) => await DownloadSelectedOnlineAsync());
            menu.Items.Add("Encontrar archivo", null, (s, e) => FindSelectedFile());
            menu.Items.Add("Copiar ubicacion", null, (s, e) => CopySelectedLocation());
            menu.Items.Add("Abrir carpeta", null, (s, e) => OpenSelectedFolder());
            resultList.ContextMenuStrip = menu;

            previewBar = new PreviewPlayerBar();
            statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Padding = new Padding(8),
                ForeColor = Color.LightGray,
                Text = "En blanco. Busca local; si no esta, puede consultar internet y descargar al Hub."
            };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };

            Controls.Add(resultList);
            Controls.Add(previewBar);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
        }

        private async Task SearchAsync()
        {
            string query = searchBox.Text.Trim();
            if (query.Length < 2)
            {
                statusLabel.Text = "Escribe al menos 2 caracteres para buscar.";
                return;
            }

            int mySearch = ++searchGeneration;
            SetBusy(true, "Buscando primero en Hub local y carpetas reales...");
            try
            {
                string type = typeBox.SelectedItem == null ? "Todos" : typeBox.SelectedItem.ToString();
                List<AllTrackItem> found = await Task.Run(() => SearchLocalFiles(query, type));
                if (mySearch != searchGeneration) return;
                ApplyResults(found);

                if (internetFallbackBox.Checked)
                {
                    SetInternetLoading(true, "Local listo: " + found.Count + ". Pensando/cargando internet y versiones remix...");
                    try
                    {
                        List<AllTrackItem> internet = await SearchInternetFallbackAsync(query, type);
                        if (mySearch != searchGeneration) return;
                        found.AddRange(internet);
                        ApplyResults(found);
                    }
                    catch (Exception internetEx)
                    {
                        if (mySearch == searchGeneration)
                            statusLabel.Text = "Local listo: " + found.Count + ". Internet no respondio: " + internetEx.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "No se pudo buscar: " + ex.Message;
            }
            finally
            {
                if (mySearch == searchGeneration) SetBusy(false, null);
            }
        }

        private List<AllTrackItem> SearchLocalFiles(string query, string typeFilter)
        {
            return SearchLocalFiles(query, typeFilter, null);
        }

        private List<AllTrackItem> SearchLocalFiles(string query, string typeFilter, Action<AllTrackItem> onFound)
        {
            var results = new List<AllTrackItem>();
            string[] queries = BuildQueryVariants(query);
            foreach (string folder in GetSearchFolders())
            {
                string origin = GetOriginLabel(folder);
                foreach (string path in SafeEnumerateFiles(folder))
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension)) continue;
                    int score = MatchScore(Path.GetFileNameWithoutExtension(path), queries);
                    if (score <= 0) continue;
                    string type = GetMediaType(path);
                    if (typeFilter != "Todos" && typeFilter != type) continue;
                    var track = new AllTrackItem { Path = path, Type = type, Origin = origin, MatchScore = score };
                    results.Add(track);
                    if (onFound != null) onFound(track);
                    if (results.Count >= 1000) return results;
                }
            }
            return results;
        }

        private async Task<List<AllTrackItem>> SearchInternetFallbackAsync(string query, string typeFilter)
        {
            string mediaType = typeFilter == "Video" ? "video" : typeFilter == "Karaoke" ? "karaoke" : "music";
            var all = new List<YouTubeTrack>();
            var baseResults = await YouTubeEngine.SearchAsync(query, mediaType, 14);
            if (baseResults != null) all.AddRange(baseResults);
            if (query.IndexOf("remix", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var remixResults = await YouTubeEngine.SearchAsync(query + " remix", mediaType, 10);
                if (remixResults != null) all.AddRange(remixResults);
            }

            return all
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Url))
                .GroupBy(t => t.Url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(20)
                .Select(t => new AllTrackItem
            {
                Title = t.Title,
                Type = mediaType == "karaoke" ? "Karaoke" : "Audio/Video",
                Origin = "Internet / YouTube",
                Url = t.Url,
                OnlineTrack = t,
                IsOnline = true,
                MatchScore = 1
            }).ToList();
        }

        private static string[] BuildQueryVariants(string query)
        {
            string clean = NormalizeText(query);
            var variants = new List<string> { clean };
            string noPunct = Regex.Replace(clean, @"[^\p{L}\p{Nd}]+", " ").Trim();
            if (noPunct.Length > 0) variants.Add(noPunct);
            foreach (string token in noPunct.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (token.Length >= 3) variants.Add(token);
            return variants.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static int MatchScore(string fileName, string[] queries)
        {
            string name = NormalizeText(fileName);
            string noPunctName = Regex.Replace(name, @"[^\p{L}\p{Nd}]+", " ").Trim();
            int best = 0;
            foreach (string query in queries)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 100);
                else if (Regex.IsMatch(noPunctName, @"(^|\s)" + Regex.Escape(query) + @"($|\s)", RegexOptions.IgnoreCase)) best = Math.Max(best, 90);
                else if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) best = Math.Max(best, 60);
            }
            return best;
        }

        private static string NormalizeText(string value)
        {
            string text = (value ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (char c in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
            }
            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private IEnumerable<string> SafeEnumerateFiles(string folder)
        {
            try { return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).ToArray(); }
            catch { return new string[0]; }
        }

        private IEnumerable<string> GetSearchFolders()
        {
            var folders = new List<string>();
            folders.AddRange(AppSettings.AllowedFolders.Where(Directory.Exists));
            folders.Add(AppSettings.GetDownloadFolder("music"));
            folders.Add(AppSettings.GetDownloadFolder("video"));
            folders.Add(AppSettings.GetDownloadFolder("karaoke"));
            return folders.Where(f => !string.IsNullOrWhiteSpace(f) && Directory.Exists(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string GetOriginLabel(string folder)
        {
            if (string.Equals(folder, AppSettings.GetDownloadFolder("music"), StringComparison.OrdinalIgnoreCase)) return "Hub Music";
            if (string.Equals(folder, AppSettings.GetDownloadFolder("video"), StringComparison.OrdinalIgnoreCase)) return "Hub Video";
            if (string.Equals(folder, AppSettings.GetDownloadFolder("karaoke"), StringComparison.OrdinalIgnoreCase)) return "Hub Karaoke";
            return "Usuario";
        }

        private static string GetMediaType(string path)
        {
            string folder = Path.GetDirectoryName(path) ?? "";
            if (folder.IndexOf("KARAOKE", StringComparison.OrdinalIgnoreCase) >= 0) return "Karaoke";
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".mp4" || extension == ".mkv" || extension == ".webm" || extension == ".avi" || extension == ".mov") return "Video";
            return "Music";
        }

        private void ApplyResults(List<AllTrackItem> items)
        {
            ResetResultList();
            resultList.BeginUpdate();
            foreach (AllTrackItem track in SortItems(items))
            {
                AddResultItem(track);
            }
            resultList.EndUpdate();
            UpdateResultCounts(null);
        }

        private void ResetResultList()
        {
            resultList.BeginUpdate();
            resultList.Items.Clear();
            resultList.Groups.Clear();
            localResultGroup = new ListViewGroup("1. Hub/local vivo - ya existe en este equipo", HorizontalAlignment.Left);
            internetResultGroup = new ListViewGroup("2. Internet / YouTube - enlaces descargables al Hub", HorizontalAlignment.Left);
            resultList.Groups.Add(localResultGroup);
            resultList.Groups.Add(internetResultGroup);
            resultList.EndUpdate();
        }

        private void AddResultItem(AllTrackItem track)
        {
            FileInfo info = track.IsOnline ? null : new FileInfo(track.Path);
            var item = new ListViewItem(track.DisplayName);
            item.SubItems.Add(track.Type);
            item.SubItems.Add(track.Format);
            item.SubItems.Add(track.IsOnline ? track.SizeLabel : string.Format("{0:0.0} MB", info.Length / 1048576.0));
            item.SubItems.Add(track.Origin);
            item.SubItems.Add(track.IsOnline ? track.Location : info.DirectoryName);
            item.SubItems.Add(track.ActionText);
            item.Group = track.IsOnline ? internetResultGroup : localResultGroup;
            item.ForeColor = track.IsOnline ? Color.LightSkyBlue : Color.White;
            item.BackColor = track.IsOnline ? Color.FromArgb(24, 38, 55) : Color.FromArgb(35, 35, 35);
            item.Tag = track;
            resultList.Items.Add(item);
        }

        private void UpdateResultCounts(string prefix)
        {
            int onlineCount = resultList.Items.Cast<ListViewItem>().Count(i => ((AllTrackItem)i.Tag).IsOnline);
            string text = resultList.Items.Count + " resultados. Local: " + (resultList.Items.Count - onlineCount) + ". Internet: " + onlineCount + ".";
            statusLabel.Text = string.IsNullOrWhiteSpace(prefix) ? text : prefix + " " + text;
        }

        private void ClearResults()
        {
            searchGeneration++;
            resultList.Items.Clear();
            resultList.Groups.Clear();
            statusLabel.Text = "Limpio. Escribe un nombre para buscar.";
        }

        private void SortByColumn(int column)
        {
            if (sortColumn == column) sortAscending = !sortAscending;
            else { sortColumn = column; sortAscending = true; }
        }

        private void SortCurrentRows()
        {
            ApplyResults(resultList.Items.Cast<ListViewItem>().Select(i => (AllTrackItem)i.Tag).ToList());
        }

        private IEnumerable<AllTrackItem> SortItems(IEnumerable<AllTrackItem> items)
        {
            Comparison<AllTrackItem> comparison;
            if (sortColumn == 1) comparison = (a, b) => string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == 2) comparison = (a, b) => string.Compare(a.Format, b.Format, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == 3) comparison = (a, b) => GetFileLength(a.Path).CompareTo(GetFileLength(b.Path));
            else if (sortColumn == 4) comparison = (a, b) => string.Compare(a.Origin, b.Origin, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == 5) comparison = (a, b) => string.Compare(a.Location, b.Location, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == 6) comparison = (a, b) => string.Compare(a.ActionText, b.ActionText, StringComparison.OrdinalIgnoreCase);
            else comparison = (a, b) =>
            {
                int score = b.MatchScore.CompareTo(a.MatchScore);
                return score != 0 ? score : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            };
            var list = items.ToList();
            list.Sort((a, b) =>
            {
                int source = a.IsOnline.CompareTo(b.IsOnline);
                if (source != 0) return source;
                return sortAscending ? comparison(a, b) : -comparison(a, b);
            });
            return list;
        }

        private static long GetFileLength(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) ? 0 : new FileInfo(path).Length; }
            catch { return 0; }
        }

        private void PreviewSelected()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null) return;
            if (track.IsOnline)
            {
                statusLabel.Text = "Resultado de internet. Descargalo al Hub para prescuchar local.";
                return;
            }
            string error;
            statusLabel.Text = previewBar.PlayTrack(track.Path, out error) ? "Prescuchando desde All Tracks local." : "No se pudo prescuchar: " + error;
        }

        private async Task HandleDoubleClickAsync()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null) return;
            if (track.IsOnline)
                await DownloadSelectedOnlineAsync();
            else
                PreviewSelected();
        }

        private void UpdateSelectedStatus()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null) return;
            statusLabel.Text = track.IsOnline
                ? "Resultado azul de internet: doble clic o boton Descargar seleccionado abre Audio MP3 / Video MP4."
                : "Resultado blanco local: doble clic prescucha; tambien puedes arrastrarlo al plato.";
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

        private async Task DownloadSelectedOnlineAsync()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null)
            {
                statusLabel.Text = "Selecciona un resultado para descargar audio o video.";
                return;
            }

            string selectedMediaType = PromptDownloadType(track.DisplayName);
            if (string.IsNullOrWhiteSpace(selectedMediaType))
            {
                statusLabel.Text = "Descarga cancelada.";
                return;
            }

            SetBusy(true, "Descargando " + selectedMediaType + " desde internet al Hub...");
            OperationProgressDialog progress = new OperationProgressDialog("Descargando desde All Tracks");
            progress.Show(this);
            try
            {
                progress.SetBatchStatus(1, 1, "Descargando", track.DisplayName);
                string mediaType = selectedMediaType;
                string quality = mediaType == "music" ? "MP3 320 kbps" : "1080p estable";
                string sourceUrl = track.Url;
                YouTubeTrack resolvedTrack = track.OnlineTrack;
                if (string.IsNullOrWhiteSpace(sourceUrl))
                {
                    progress.SetBatchStatus(1, 1, "Buscando version en internet", track.DisplayName);
                    string searchType = mediaType == "video" ? "video" : mediaType == "karaoke" ? "karaoke" : "music";
                    List<YouTubeTrack> candidates = await YouTubeEngine.SearchAsync(track.DisplayName, searchType, 20);
                    resolvedTrack = candidates == null ? null : candidates.FirstOrDefault(t => t != null && !string.IsNullOrWhiteSpace(t.Url));
                    sourceUrl = resolvedTrack == null ? "" : resolvedTrack.Url;
                    if (string.IsNullOrWhiteSpace(sourceUrl))
                    {
                        progress.SetResult("No se encontro una version descargable en internet.");
                        statusLabel.Text = "No se encontro version descargable para " + track.DisplayName + ".";
                        return;
                    }
                }

                progress.SetBatchStatus(1, 1, "Descargando", track.DisplayName);
                string saved = await YouTubeEngine.DownloadAsync(sourceUrl, mediaType, quality);
                if (string.IsNullOrWhiteSpace(saved) || !File.Exists(saved))
                {
                    progress.SetResult("No se pudo descargar.");
                    statusLabel.Text = "No se pudo descargar desde internet.";
                    return;
                }
                string savedType = mediaType == "video" ? "Video" : mediaType == "karaoke" ? "Karaoke" : "Music";
                DownloadRegistry.Register(saved, sourceUrl, track.DisplayName, resolvedTrack == null ? "" : resolvedTrack.Uploader, savedType);
                progress.SetResult("Descargado al Hub: " + saved);
                statusLabel.Text = "Descargado al Hub, marcado como recien descargada y enviado a Auto Help + Camelot.";
                var handler = TrackSentToHub;
                if (handler != null) handler(saved);
            }
            finally
            {
                System.Threading.Thread.Sleep(500);
                progress.Close();
                progress.Dispose();
                SetBusy(false, null);
            }
        }

        private string PromptDownloadType(string title)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Descargar desde internet";
                dialog.Size = new Size(460, 190);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = Color.FromArgb(25, 25, 25);
                dialog.ForeColor = Color.White;

                var label = new Label
                {
                    Text = "Elige que deseas descargar para:\n" + title,
                    Dock = DockStyle.Top,
                    Height = 70,
                    Padding = new Padding(14),
                    ForeColor = Color.White
                };
                dialog.Controls.Add(label);

                var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                string result = null;
                var cancel = new Button { Text = "Cancelar", Width = 95, Height = 32 };
                var video = new Button { Text = "Video MP4", Width = 95, Height = 32 };
                var audio = new Button { Text = "Audio MP3", Width = 95, Height = 32 };
                cancel.Click += (s, e) => { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); };
                video.Click += (s, e) => { result = "video"; dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                audio.Click += (s, e) => { result = "music"; dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                panel.Controls.AddRange(new Control[] { cancel, video, audio });
                dialog.Controls.Add(panel);

                dialog.ShowDialog(this);
                return result;
            }
        }

        private AllTrackItem GetCurrentTrack()
        {
            return resultList.SelectedItems.Count == 0 ? null : resultList.SelectedItems[0].Tag as AllTrackItem;
        }

        private void FindSelectedFile()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null || track.IsOnline || !File.Exists(track.Path))
            {
                statusLabel.Text = "Selecciona un archivo local para encontrarlo.";
                return;
            }
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + track.Path + "\"");
        }

        private void CopySelectedLocation()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null) return;
            Clipboard.SetText(track.IsOnline ? track.Url : track.Path);
            statusLabel.Text = track.IsOnline ? "URL copiada." : "Ubicacion copiada.";
        }

        private void OpenSelectedFolder()
        {
            AllTrackItem track = GetCurrentTrack();
            if (track == null || track.IsOnline) return;
            string folder = Path.GetDirectoryName(track.Path);
            if (Directory.Exists(folder)) System.Diagnostics.Process.Start("explorer.exe", folder);
        }

        private void ResultList_ItemDrag(object sender, ItemDragEventArgs e)
        {
            string[] paths = resultList.SelectedItems.Cast<ListViewItem>()
                .Select(i => ((AllTrackItem)i.Tag).Path)
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .ToArray();
            if (paths.Length > 0) resultList.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
        }

        private void ResultList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            ListViewItem item = resultList.GetItemAt(e.X, e.Y);
            if (item == null) return;
            if (!item.Selected)
            {
                resultList.SelectedItems.Clear();
                item.Selected = true;
            }
        }

        private void SetBusy(bool busy, string message)
        {
            progressBar.Visible = busy;
            searchButton.Enabled = !busy;
            downloadButton.Enabled = !busy;
            findButton.Enabled = !busy;
            clearButton.Enabled = !busy;
            if (!string.IsNullOrEmpty(message)) statusLabel.Text = message;
        }

        private void SetInternetLoading(bool loading, string message)
        {
            progressBar.Visible = loading;
            searchButton.Enabled = !loading;
            downloadButton.Enabled = true;
            findButton.Enabled = true;
            clearButton.Enabled = true;
            if (!string.IsNullOrEmpty(message)) statusLabel.Text = message;
        }

        private class AllTrackItem
        {
            public string Path;
            public string Title;
            public string Type;
            public string Origin;
            public string Url;
            public bool IsOnline;
            public int MatchScore;
            public YouTubeTrack OnlineTrack;
            public string DisplayName { get { return IsOnline ? (Title ?? "") : System.IO.Path.GetFileNameWithoutExtension(Path); } }
            public string Format { get { return IsOnline ? (Type == "Karaoke" ? "MP4/MP3" : "MP3/MP4") : System.IO.Path.GetExtension(Path).ToLowerInvariant(); } }
            public string Location
            {
                get
                {
                    if (!IsOnline) return System.IO.Path.GetDirectoryName(Path);
                    if (OnlineTrack == null) return "Descarga: Audio MP3 o Video MP4";
                    string duration = string.IsNullOrWhiteSpace(OnlineTrack.Duration) ? "" : " | Duracion " + OnlineTrack.Duration;
                    string durationTag = GetDurationTag(OnlineTrack.Duration);
                    return "Descarga: Audio MP3 o Video MP4 | " + durationTag + " | " + OnlineTrack.MaximumQuality + duration;
                }
            }
            public string ActionText { get { return IsOnline ? "Descargar audio/video" : "Prescuchar / arrastrar"; } }
            public string SizeLabel
            {
                get
                {
                    if (!IsOnline || OnlineTrack == null) return "por calcular";
                    string audio = FormatBytes(OnlineTrack.EstimatedAudioBytes);
                    string video = FormatBytes(OnlineTrack.EstimatedVideoBytes);
                    if (audio != "-" && video != "-") return "A " + audio + " / V " + video;
                    if (audio != "-") return "Audio " + audio;
                    if (video != "-") return "Video " + video;
                    return string.IsNullOrWhiteSpace(OnlineTrack.Duration) ? "por calcular" : "Duracion " + OnlineTrack.Duration;
                }
            }

            private static string FormatBytes(long bytes)
            {
                if (bytes <= 0) return "-";
                return string.Format("{0:0.0} MB", bytes / 1048576.0);
            }

            private static string GetDurationTag(string duration)
            {
                int seconds = ParseDurationSeconds(duration);
                if (seconds <= 0) return "duracion desconocida";
                if (seconds < 150) return "track corto";
                if (seconds >= 600) return "mix largo";
                if (seconds >= 330) return "extended/remix probable";
                return "track normal";
            }

            private static int ParseDurationSeconds(string duration)
            {
                if (string.IsNullOrWhiteSpace(duration)) return 0;
                string[] parts = duration.Split(':');
                int total = 0;
                foreach (string part in parts)
                {
                    int value;
                    if (!int.TryParse(part, out value)) return 0;
                    total = total * 60 + value;
                }
                return total;
            }
        }
    }
}
