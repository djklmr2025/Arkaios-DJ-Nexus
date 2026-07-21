using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class HitsTracksControl : UserControl
    {
        public event Action<string> TrackSentToHub;

        private readonly TextBox queryBox;
        private readonly TextBox playlistBox;
        private readonly ComboBox platformBox;
        private readonly ComboBox typeBox;
        private readonly ComboBox qualityBox;
        private readonly DataGridView grid;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly FlowLayoutPanel toolbar;
        private readonly Button loadButton;
        private readonly Button downloadSelectedButton;
        private readonly Button downloadAllButton;
        private List<HitTrack> hits = new List<HitTrack>();
        private string sortColumn = "Title";
        private bool sortAscending = true;

        public HitsTracksControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(8), WrapContents = true };
            queryBox = new TextBox { Width = 260, Text = "top hits 2026 official audio" };
            playlistBox = new TextBox { Width = 360 };
            platformBox = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            platformBox.Items.AddRange(new object[] { "YouTube Hits", "Deezer Chart", "Spotify", "Apple Music", "SoundCloud", "Beatport", "Mixcloud", "Bandcamp" });
            platformBox.SelectedIndex = 0;
            typeBox = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => UpdateQualityOptions();
            qualityBox = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            loadButton = new Button { Text = "Cargar hits", AutoSize = true };
            loadButton.Click += async (s, e) => await LoadHitsAsync();
            downloadSelectedButton = new Button { Text = "Descargar seleccionadas", AutoSize = true };
            downloadSelectedButton.Click += async (s, e) => await DownloadSelectedAsync(false);
            downloadAllButton = new Button { Text = "Descargar todo", AutoSize = true };
            downloadAllButton.Click += async (s, e) => await DownloadSelectedAsync(true);
            var playlistButton = new Button { Text = "Extraer playlist", AutoSize = true };
            playlistButton.Click += async (s, e) => await ExtractPlaylistAsync();
            var folderButton = new Button { Text = "Abrir destino", AutoSize = true };
            folderButton.Click += (s, e) => OpenDestination();
            toolbar.Controls.AddRange(new Control[] { queryBox, platformBox, typeBox, qualityBox, loadButton, downloadSelectedButton, downloadAllButton, folderButton, playlistBox, playlistButton });

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Black,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "SelectedColumn", HeaderText = "Bajar", DataPropertyName = "Selected", Width = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TitleColumn", HeaderText = "Hit / track", DataPropertyName = "Title", ReadOnly = true, Width = 290 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ArtistColumn", HeaderText = "Artista / canal", DataPropertyName = "Artist", ReadOnly = true, Width = 170 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourceColumn", HeaderText = "Fuente", DataPropertyName = "Source", ReadOnly = true, Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DurationColumn", HeaderText = "Duracion", DataPropertyName = "Duration", ReadOnly = true, Width = 75 });
            var typeColumn = new DataGridViewComboBoxColumn { Name = "TypeColumn", HeaderText = "Tipo", DataPropertyName = "DownloadType", Width = 90 };
            typeColumn.Items.AddRange(new object[] { "Music", "Video", "Karaoke" });
            grid.Columns.Add(typeColumn);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DownloadColumn", HeaderText = "Descarga", DataPropertyName = "DownloadStatus", ReadOnly = true, Width = 180 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "PreviewColumn", HeaderText = "Vista previa", Text = "Abrir", UseColumnTextForButtonValue = true, Width = 80 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "HubColumn", HeaderText = "Hub", Text = "Descargar track", UseColumnTextForButtonValue = true, Width = 120 });
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.Programmatic;
            grid.ColumnHeaderMouseClick += (s, e) => SortByColumn(grid.Columns[e.ColumnIndex].DataPropertyName);
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.DataError += (s, e) => { e.ThrowException = false; };
            grid.CellContentClick += async (s, e) =>
            {
                if (e.RowIndex < 0) return;
                string name = grid.Columns[e.ColumnIndex].Name;
                if (name == "PreviewColumn") Preview(e.RowIndex);
                else if (name == "HubColumn") await DownloadToHubAsync(e.RowIndex);
            };

            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), ForeColor = Color.LightGray };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };

            Controls.Add(grid);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            UpdateQualityOptions();
            UpdateStatus();
        }

        private string SelectedType { get { return typeBox.SelectedItem.ToString().ToLowerInvariant(); } }
        private string SelectedPlatform { get { return platformBox.SelectedItem.ToString(); } }

        private void UpdateQualityOptions()
        {
            qualityBox.Items.Clear();
            if (SelectedType == "music") qualityBox.Items.AddRange(new object[] { "MP3 320 kbps", "MP3 192 kbps", "M4A maxima" });
            else qualityBox.Items.AddRange(new object[] { "Maxima disponible", "1080p estable", "720p estandar" });
            qualityBox.SelectedIndex = 0;
            UpdateStatus();
        }

        private async Task LoadHitsAsync()
        {
            SetBusy(true, "Cargando hits de " + SelectedPlatform + "...", "Cargando...");
            try
            {
                grid.DataSource = null;
                if (SelectedPlatform == "YouTube Hits") hits = await LoadYouTubeHitsAsync();
                else if (SelectedPlatform == "Deezer Chart") hits = await LoadDeezerChartAsync();
                else if (SelectedPlatform == "SoundCloud") hits = await LoadSoundCloudAsync();
                else hits = LoadPendingPlatformRows(SelectedPlatform);
                BindHits();
                statusLabel.Text = hits.Count + " resultados. Destino " + SelectedType + ": " + AppSettings.GetDownloadFolder(SelectedType);
            }
            finally { SetBusy(false, null, "Cargar hits"); }
        }

        private async Task<List<HitTrack>> LoadYouTubeHitsAsync()
        {
            var rows = new List<HitTrack>();
            List<YouTubeTrack> tracks = await YouTubeEngine.SearchAsync(queryBox.Text.Trim(), SelectedType, 20);
            foreach (YouTubeTrack track in tracks)
            {
                rows.Add(new HitTrack
                {
                    Selected = true,
                    DownloadType = typeBox.SelectedItem.ToString(),
                    Title = track.Title,
                    Artist = track.Uploader,
                    Source = "YouTube",
                    Duration = track.Duration,
                    Url = track.Url,
                    DownloadQuery = track.Title,
                    DownloadStatus = "Descargable con yt-dlp"
                });
            }
            return rows;
        }

        private async Task ExtractPlaylistAsync()
        {
            string url = playlistBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                statusLabel.Text = "Pega una URL de playlist de YouTube en el campo junto a Extraer playlist.";
                return;
            }

            SetBusy(true, "Extrayendo playlist con API Render y fallback local...", "Extrayendo...");
            OperationProgressDialog progress = new OperationProgressDialog("Extrayendo playlist");
            progress.Show(this);
            progress.SetIndeterminate("Consultando extractor", "Render API con fallback local yt-dlp.");
            PlaylistExtractResult result;
            try { result = await PlaylistExtractorClient.ExtractAsync(url); }
            finally { SetBusy(false, null, "Cargar hits"); }

            if (result == null || !result.Success || result.Items.Count == 0)
            {
                progress.SetResult("Resultado: no se pudo extraer la playlist.");
                progress.Close();
                progress.Dispose();
                statusLabel.Text = "No se pudo extraer la playlist: " + (result == null ? "sin respuesta" : result.Error);
                return;
            }

            hits = new List<HitTrack>();
            foreach (PlaylistExtractItem item in result.Items)
            {
                hits.Add(new HitTrack
                {
                    Selected = true,
                    DownloadType = typeBox.SelectedItem.ToString(),
                    Title = item.Title,
                    Artist = item.Uploader,
                    Source = "Playlist " + result.Source,
                    Duration = string.IsNullOrWhiteSpace(item.Duration) ? "-" : item.Duration,
                    Url = item.Url,
                    DownloadQuery = item.Title,
                    DownloadStatus = item.Category + " / descargable con yt-dlp"
                });
            }

            grid.DataSource = null;
            BindHits();
            progress.SetResult("Resultado: " + hits.Count + " elementos extraidos via " + result.Source + ".");
            await Task.Delay(500);
            progress.Close();
            progress.Dispose();
            statusLabel.Text = hits.Count + " elementos extraidos de playlist via " + result.Source + ". Destino: " + AppSettings.GetDownloadFolder(SelectedType);
        }

        private async Task<List<HitTrack>> LoadSoundCloudAsync()
        {
            var rows = new List<HitTrack>();
            List<YouTubeTrack> tracks = await YouTubeEngine.SearchSoundCloudAsync(queryBox.Text.Trim(), 20);
            foreach (YouTubeTrack track in tracks)
            {
                rows.Add(new HitTrack
                {
                    Selected = true,
                    DownloadType = typeBox.SelectedItem.ToString(),
                    Title = track.Title,
                    Artist = track.Uploader,
                    Source = "SoundCloud",
                    Duration = track.Duration,
                    Url = track.Url,
                    DownloadQuery = track.Title,
                    DownloadStatus = SelectedType == "music" ? "Descargable con yt-dlp si la fuente lo permite" : "Para video/karaoke se buscara version YouTube"
                });
            }
            return rows;
        }

        private async Task<List<HitTrack>> LoadDeezerChartAsync()
        {
            string defaultType = typeBox.SelectedItem.ToString();
            return await Task.Run(() =>
            {
                var rows = new List<HitTrack>();
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Encoding = System.Text.Encoding.UTF8;
                        string json = client.DownloadString("https://api.deezer.com/chart/0/tracks?limit=20");
                        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                        var root = serializer.Deserialize<Dictionary<string, object>>(json);
                        object dataObject;
                        if (root == null || !root.TryGetValue("data", out dataObject)) return rows;
                        IEnumerable data = dataObject as IEnumerable;
                        if (data == null) return rows;

                        foreach (object itemObject in data)
                        {
                            var item = itemObject as Dictionary<string, object>;
                            if (item == null) continue;
                            string title = ToString(item, "title");
                            string link = ToString(item, "link");
                            string duration = FormatDuration(ToInt(item, "duration"));
                            string artist = "";
                            object artistObject;
                            if (item.TryGetValue("artist", out artistObject))
                            {
                                var artistMap = artistObject as Dictionary<string, object>;
                                if (artistMap != null) artist = ToString(artistMap, "name");
                            }
                            rows.Add(new HitTrack
                            {
                                Selected = true,
                                DownloadType = defaultType,
                                Title = title,
                                Artist = artist,
                                Source = "Deezer Chart",
                                Duration = duration,
                                Url = link,
                                DownloadQuery = artist + " " + title + " official audio",
                                DownloadStatus = "Buscar version descargable en YouTube"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    rows.Add(new HitTrack { Selected = false, DownloadType = defaultType, Title = "No se pudo cargar Deezer Chart", Artist = ex.Message, Source = "Deezer", Duration = "-", DownloadStatus = "Error API" });
                }
                return rows;
            });
        }

        private List<HitTrack> LoadPendingPlatformRows(string platform)
        {
            var rows = new List<HitTrack>();
            rows.Add(new HitTrack
            {
                Selected = false,
                DownloadType = typeBox.SelectedItem.ToString(),
                Title = platform + " requiere login/API para hits reales",
                Artist = "Configurar OAuth o token oficial",
                Source = platform,
                Duration = "-",
                Url = GetOfficialSearchUrl(platform),
                DownloadQuery = queryBox.Text.Trim(),
                DownloadStatus = "Metadata/enlace; descarga via busqueda YouTube"
            });
            return rows;
        }

        private async Task DownloadToHubAsync(int rowIndex)
        {
            if (rowIndex >= hits.Count) return;
            grid.EndEdit();
            HitTrack hit = grid.Rows[rowIndex].DataBoundItem as HitTrack;
            if (hit == null) hit = hits[rowIndex];
            if (string.IsNullOrWhiteSpace(hit.DownloadQuery) && string.IsNullOrWhiteSpace(hit.Url)) return;

            SetBusy(true, "Buscando version descargable para: " + hit.Title, "Descargando...");
            OperationProgressDialog progress = new OperationProgressDialog("Descargando track al Hub");
            progress.Show(this);
            progress.SetStatus("Preparando descarga", hit.Title, 10);
            DownloadResult result = null;
            try
            {
                result = await DownloadHitToHubAsync(hit, progress);
            }
            finally { SetBusy(false, null, "Cargar hits"); }

            if (result == null || string.IsNullOrWhiteSpace(result.SavedPath))
            {
                progress.SetResult("Resultado: descarga fallida. Revisa yt-dlp-errors.log.");
                progress.Close();
                progress.Dispose();
                statusLabel.Text = "No se pudo descargar. Revisa yt-dlp-errors.log o cambia el tipo/calidad.";
                return;
            }

            progress.SetResult("Resultado: guardado en Hub.\n" + result.SavedPath);
            await Task.Delay(600);
            progress.Close();
            progress.Dispose();
            statusLabel.Text = "Guardado y enviado al Hub: " + result.SavedPath;
            DownloadRegistry.Register(result.SavedPath, hit.Url, hit.Title, hit.Artist, result.DownloadType);
            Action<string> handler = TrackSentToHub;
            if (handler != null) handler(result.SavedPath);
            BindHits();
        }

        private async Task DownloadSelectedAsync(bool all)
        {
            grid.EndEdit();
            List<HitTrack> selected = new List<HitTrack>();
            foreach (HitTrack hit in hits)
                if (all || hit.Selected) selected.Add(hit);

            if (selected.Count == 0)
            {
                statusLabel.Text = "Marca canciones con el checkbox Bajar o usa Descargar todo.";
                return;
            }

            DialogResult answer = MessageBox.Show(
                "Se descargaran " + selected.Count + " tracks al Hub local. Los errores se saltaran y el lote continuara.\n\nContinuar?",
                all ? "Descargar toda la playlist/lista" : "Descargar seleccionadas",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            SetBusy(true, "Descargando lote al Hub local...", "Descargando...");
            OperationProgressDialog progress = new OperationProgressDialog(all ? "Descargando todo" : "Descargando seleccionadas");
            progress.Show(this);
            int ok = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    HitTrack hit = selected[i];
                    progress.SetBatchStatus(i + 1, selected.Count, "Preparando descarga", hit.Title);
                    DownloadResult result = await DownloadHitToHubAsync(hit, progress);
                    if (result != null && !string.IsNullOrWhiteSpace(result.SavedPath))
                    {
                        ok++;
                        DownloadRegistry.Register(result.SavedPath, hit.Url, hit.Title, hit.Artist, result.DownloadType);
                        hit.Selected = false;
                        hit.DownloadStatus = "OK: " + Path.GetFileName(result.SavedPath);
                        Action<string> handler = TrackSentToHub;
                        if (handler != null) handler(result.SavedPath);
                    }
                    else
                    {
                        failed++;
                        hit.DownloadStatus = "Fallo: revisar yt-dlp-errors.log";
                    }
                    BindHits();
                }
            }
            finally
            {
                SetBusy(false, null, "Cargar hits");
            }

            progress.SetResult("Resultado lote: " + ok + " descargadas, " + failed + " fallidas.");
            await Task.Delay(900);
            progress.Close();
            progress.Dispose();
            statusLabel.Text = "Lote terminado: " + ok + " descargadas, " + failed + " fallidas. Hub actualizado.";
        }

        private async Task<DownloadResult> DownloadHitToHubAsync(HitTrack hit, OperationProgressDialog progress)
        {
            string downloadType = NormalizeDownloadType(hit.DownloadType);
            string sourceUrl = CanUseDirectUrl(hit, downloadType) ? hit.Url : null;
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                progress.SetIndeterminate("Buscando version descargable", hit.DownloadQuery);
                List<YouTubeTrack> matches = await YouTubeEngine.SearchAsync(hit.DownloadQuery, downloadType, 1);
                if (matches.Count > 0) sourceUrl = matches[0].Url;
            }

            if (string.IsNullOrWhiteSpace(sourceUrl)) return new DownloadResult();

            string quality = GetQualityForType(downloadType);
            progress.SetIndeterminate("Descargando con yt-dlp", "Destino: " + AppSettings.GetDownloadFolder(downloadType));
            string saved = await YouTubeEngine.DownloadAsync(sourceUrl, downloadType, quality);
            return new DownloadResult { SavedPath = saved, DownloadType = downloadType };
        }

        private static bool CanUseDirectUrl(HitTrack hit, string downloadType)
        {
            if (hit == null || string.IsNullOrWhiteSpace(hit.Url)) return false;
            if (hit.Source == "YouTube") return true;
            if (hit.Source != null && hit.Source.StartsWith("Playlist", StringComparison.OrdinalIgnoreCase)) return true;
            if (hit.Source == "SoundCloud" && downloadType == "music") return true;
            return false;
        }

        private string GetQualityForType(string downloadType)
        {
            string selected = qualityBox.SelectedItem == null ? "" : qualityBox.SelectedItem.ToString();
            if (downloadType == SelectedType && !string.IsNullOrWhiteSpace(selected)) return selected;
            if (downloadType == "music") return "MP3 320 kbps";
            return "Maxima disponible";
        }

        private static string NormalizeDownloadType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "music";
            value = value.ToLowerInvariant();
            if (value == "video") return "video";
            if (value == "karaoke") return "karaoke";
            return "music";
        }

        private void Preview(int rowIndex)
        {
            if (rowIndex >= hits.Count || string.IsNullOrWhiteSpace(hits[rowIndex].Url)) return;
            try { Process.Start(hits[rowIndex].Url); }
            catch (Exception ex) { statusLabel.Text = "No se pudo abrir la vista previa: " + ex.Message; }
        }

        private void OpenDestination()
        {
            string folder = AppSettings.GetDownloadFolder(SelectedType);
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", folder);
        }

        private void SetBusy(bool busy, string message, string buttonText)
        {
            progressBar.Visible = busy;
            toolbar.Enabled = !busy;
            grid.Enabled = !busy;
            loadButton.Text = buttonText;
            if (!string.IsNullOrWhiteSpace(message)) statusLabel.Text = message;
        }

        private void SortByColumn(string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return;
            if (sortColumn == column) sortAscending = !sortAscending;
            else
            {
                sortColumn = column;
                sortAscending = true;
            }
            BindHits();
        }

        private void BindHits()
        {
            hits.Sort((a, b) =>
            {
                int value;
                if (sortColumn == "Duration") value = ParseDuration(a.Duration).CompareTo(ParseDuration(b.Duration));
                else value = string.Compare(GetSortValue(a, sortColumn), GetSortValue(b, sortColumn), StringComparison.OrdinalIgnoreCase);
                return sortAscending ? value : -value;
            });
            grid.DataSource = null;
            grid.DataSource = hits;
        }

        private static string GetSortValue(HitTrack item, string column)
        {
            if (column == "Selected") return item.Selected ? "1" : "0";
            if (column == "DownloadType") return item.DownloadType ?? "";
            if (column == "Artist") return item.Artist ?? "";
            if (column == "Source") return item.Source ?? "";
            if (column == "DownloadStatus") return item.DownloadStatus ?? "";
            return item.Title ?? "";
        }

        private static int ParseDuration(string duration)
        {
            if (string.IsNullOrWhiteSpace(duration)) return 0;
            string[] parts = duration.Split(':');
            int total = 0;
            foreach (string part in parts)
            {
                int value;
                if (!int.TryParse(part, out value)) return 0;
                total = (total * 60) + value;
            }
            return total;
        }

        private void UpdateStatus()
        {
            if (statusLabel != null) statusLabel.Text = "Destino " + SelectedType + ": " + AppSettings.GetDownloadFolder(SelectedType);
        }

        private static string GetOfficialSearchUrl(string platform)
        {
            if (platform == "Spotify") return "https://open.spotify.com/search";
            if (platform == "Apple Music") return "https://music.apple.com/us/search";
            if (platform == "SoundCloud") return "https://soundcloud.com/search";
            if (platform == "Beatport") return "https://www.beatport.com/search";
            if (platform == "Mixcloud") return "https://www.mixcloud.com/search/";
            if (platform == "Bandcamp") return "https://bandcamp.com/search";
            return "";
        }

        private static string ToString(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null ? raw.ToString() : "";
        }

        private static int ToInt(Dictionary<string, object> value, string key)
        {
            int parsed;
            return int.TryParse(ToString(value, key), out parsed) ? parsed : 0;
        }

        private static string FormatDuration(int seconds)
        {
            return string.Format("{0}:{1:00}", seconds / 60, seconds % 60);
        }

        private class HitTrack
        {
            public bool Selected { get; set; }
            public string DownloadType { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Source { get; set; }
            public string Duration { get; set; }
            public string Url { get; set; }
            public string DownloadQuery { get; set; }
            public string DownloadStatus { get; set; }
        }

        private class DownloadResult
        {
            public string SavedPath { get; set; }
            public string DownloadType { get; set; }
        }
    }
}
