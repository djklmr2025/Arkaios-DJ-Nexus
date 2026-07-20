using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class MediaSearchControl : UserControl
    {
        public event Action<string> TrackSentToHub;
        public event Action<string> TrackViewRequested;
        private readonly TextBox queryBox;
        private readonly ComboBox platformBox;
        private readonly ComboBox typeBox;
        private readonly ComboBox qualityBox;
        private readonly DataGridView resultsGrid;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly FlowLayoutPanel toolbar;
        private readonly Button searchButton;
        private readonly Button adjustButton;
        private readonly List<PlatformProfile> platforms;
        private List<YouTubeTrack> results = new List<YouTubeTrack>();
        private string sortColumn = "Title";
        private bool sortAscending = true;

        public MediaSearchControl()
        {
            platforms = BuildPlatformProfiles();
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(8), WrapContents = true };
            queryBox = new TextBox { Width = 270 };
            queryBox.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
            platformBox = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (PlatformProfile platform in platforms) platformBox.Items.Add(platform.Name);
            platformBox.SelectedIndex = 0;
            platformBox.SelectedIndexChanged += (s, e) => UpdatePlatformMode();
            typeBox = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => UpdateQualityOptions();
            qualityBox = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            searchButton = new Button { Text = "Buscar", AutoSize = true };
            searchButton.Click += async (s, e) => await SearchAsync();
            var folderButton = new Button { Text = "Abrir destino", AutoSize = true };
            folderButton.Click += (s, e) => OpenDestination();
            adjustButton = new Button { Text = "Ajustar con Arkaios World", AutoSize = true };
            adjustButton.Click += async (s, e) => await AdjustQueryAsync();
            toolbar.Controls.AddRange(new Control[] { queryBox, platformBox, typeBox, qualityBox, searchButton, folderButton, adjustButton });

            resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoGenerateColumns = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Black, SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Título / metadatos", DataPropertyName = "Title", Width = 360 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Canal", DataPropertyName = "Uploader", Width = 150 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duración", DataPropertyName = "Duration", Width = 75 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Máximo real", DataPropertyName = "MaximumQuality", Width = 120 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Salida", DataPropertyName = "AvailableOutputs", Width = 90 });
            resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estado", DataPropertyName = "DownloadState", Width = 105 });
            resultsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Vista previa", DataPropertyName = "PreviewAction", UseColumnTextForButtonValue = false, Width = 105 });
            resultsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "↓ Hub", DataPropertyName = "HubAction", UseColumnTextForButtonValue = false, Width = 155 });
            foreach (DataGridViewColumn column in resultsGrid.Columns) column.SortMode = DataGridViewColumnSortMode.Programmatic;
            resultsGrid.ColumnHeaderMouseClick += (s, e) => SortByColumn(resultsGrid.Columns[e.ColumnIndex].DataPropertyName);
            resultsGrid.CellContentClick += async (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == 6) Preview(e.RowIndex);
                else if (e.ColumnIndex == 7) await DownloadAsync(e.RowIndex);
            };
            resultsGrid.CellFormatting += ResultsGrid_CellFormatting;

            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), ForeColor = Color.LightGray };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
            Controls.Add(resultsGrid);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            UpdateQualityOptions();
            UpdateDestinationStatus();
        }

        private string SelectedType { get { return typeBox.SelectedItem.ToString().ToLowerInvariant(); } }
        private string SelectedPlatform { get { return platformBox.SelectedItem.ToString(); } }

        private PlatformProfile CurrentPlatform
        {
            get
            {
                foreach (PlatformProfile platform in platforms)
                    if (string.Equals(platform.Name, SelectedPlatform, StringComparison.OrdinalIgnoreCase)) return platform;
                return platforms[0];
            }
        }

        private void UpdateQualityOptions()
        {
            qualityBox.Items.Clear();
            if (SelectedType == "music") qualityBox.Items.AddRange(new object[] { "MP3 320 kbps", "MP3 192 kbps", "M4A máxima" });
            else qualityBox.Items.AddRange(new object[] { "Máxima disponible", "1080p estable", "720p estándar" });
            qualityBox.SelectedIndex = 0;
            UpdateDestinationStatus();
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(queryBox.Text)) return;
            PlatformProfile platform = CurrentPlatform;
            if (!platform.DownloadsToHub)
            {
                ShowPlatformBridgeResult(platform);
                return;
            }
            SetBusy(true, "Buscando y leyendo formatos reales con yt-dlp...", searchButton, "Buscando...");
            try
            {
                resultsGrid.DataSource = null;
                results = await YouTubeEngine.SearchAsync(queryBox.Text.Trim(), SelectedType, 20);
                MarkDownloadedResults();
                BindResults();
                statusLabel.Text = results.Count == 0 ? "No hubo resultados. Verifica yt-dlp o la conexión." :
                    string.Format("{0} resultados reales. Salida: {1}. Destino: {2}", results.Count, SelectedType == "music" ? "MP3/M4A" : "MP4", AppSettings.GetDownloadFolder(SelectedType));
            }
            finally { SetBusy(false, null, searchButton, "Buscar"); }
        }

        private async Task AdjustQueryAsync()
        {
            string original = queryBox.Text.Trim();
            if (original.Length == 0) return;
            SetBusy(true, "Validando escritura con metadatos públicos de música/video...", adjustButton, "Ajustando...");
            string suggestion;
            try { suggestion = await YouTubeEngine.SuggestCanonicalQueryAsync(original, SelectedType); }
            finally { SetBusy(false, null, adjustButton, "Ajustar con Arkaios World"); }
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                statusLabel.Text = "No encontré una corrección suficientemente confiable; conserva o amplía tu consulta.";
                return;
            }

            DialogResult answer = MessageBox.Show(
                "Consulta escrita:\n" + original + "\n\n¿Quisiste decir?\n" + suggestion +
                "\n\nArkaios World validó la sugerencia contra metadatos públicos reales de YouTube.",
                "Ajustar metadatos antes de buscar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes)
            {
                queryBox.Text = suggestion;
                statusLabel.Text = "Consulta ajustada. Ya puedes enviarla al motor de búsqueda.";
            }
            else statusLabel.Text = "Se conservó el texto original.";
        }

        private void UpdatePlatformMode()
        {
            bool youtube = string.Equals(SelectedPlatform, "YouTube", StringComparison.OrdinalIgnoreCase);
            typeBox.Enabled = youtube;
            qualityBox.Enabled = youtube;
            resultsGrid.DataSource = null;
            results.Clear();
            PlatformProfile platform = CurrentPlatform;
            statusLabel.Text = youtube ? "YouTube: busqueda real, previsualizacion y descarga al Hub con yt-dlp." :
                platform.Name + ": " + platform.Capability + ". Usa enlace/API oficial; sin descarga directa al Hub.";
        }

        private void ShowPlatformBridgeResult(PlatformProfile platform)
        {
            string query = Uri.EscapeDataString(queryBox.Text.Trim());
            string url = string.Format(platform.SearchUrlTemplate, query);
            resultsGrid.DataSource = null;
            results = new List<YouTubeTrack>
            {
                new YouTubeTrack
                {
                    Title = "Buscar en " + platform.Name + ": " + queryBox.Text.Trim(),
                    Url = url,
                    Uploader = platform.ApiMode,
                    Duration = "-",
                    MaximumQualityOverride = platform.Capability,
                    AvailableOutputs = platform.Outputs
                }
            };
            MarkDownloadedResults();
            BindResults();
            statusLabel.Text = platform.Name + ": resultado puente listo. Previsualizar abre la busqueda oficial; Hub no descarga desde servicios protegidos.";
        }

        private void OpenOfficialPlatformSearch(PlatformProfile platform)
        {
            string query = Uri.EscapeDataString(queryBox.Text.Trim());
            string url = string.Format(platform.SearchUrlTemplate, query);
            try
            {
                Process.Start(url);
                statusLabel.Text = "Busqueda abierta en " + platform.Name + ". No se habilitan descargas desde servicios protegidos.";
            }
            catch (Exception ex) { statusLabel.Text = "No se pudo abrir " + platform.Name + ": " + ex.Message; }
        }

        private async Task DownloadAsync(int rowIndex)
        {
            if (rowIndex >= results.Count) return;
            YouTubeTrack track = results[rowIndex];
            if (track.Downloaded && !string.IsNullOrWhiteSpace(track.DownloadedPath) && File.Exists(track.DownloadedPath))
            {
                var viewHandler = TrackViewRequested;
                if (viewHandler != null) viewHandler(track.DownloadedPath);
                statusLabel.Text = "Pista ya descargada: mostrada en AutoHelp + Camelot.";
                return;
            }
            PlatformProfile platform = CurrentPlatform;
            if (!platform.DownloadsToHub)
            {
                OpenOfficialPlatformSearch(platform);
                return;
            }
            SetBusy(true, "Descargando " + track.Title + "...", null, null);
            OperationProgressDialog progress = new OperationProgressDialog("Descargando track al Hub");
            progress.Show(this);
            progress.SetIndeterminate("Descargando con yt-dlp", track.Title + "\nDestino: " + AppSettings.GetDownloadFolder(SelectedType));
            string saved;
            try { saved = await YouTubeEngine.DownloadAsync(track.Url, SelectedType, qualityBox.SelectedItem.ToString()); }
            finally { SetBusy(false, null, null, null); }
            if (string.IsNullOrEmpty(saved))
            {
                progress.SetResult("Resultado: descarga fallida. Revisa yt-dlp-errors.log.");
                progress.Close();
                progress.Dispose();
                statusLabel.Text = "La descarga falló; consulta yt-dlp-errors.log.";
                return;
            }

            progress.SetResult("Resultado: guardado en Hub.\n" + saved);
            await Task.Delay(600);
            progress.Close();
            progress.Dispose();
            statusLabel.Text = "Guardado y enviado al Hub: " + saved;
            DownloadRegistry.Register(saved, track.Url, track.Title, track.Uploader, SelectedType);
            track.Downloaded = true;
            track.DownloadedPath = saved;
            BindResults();
            var handler = TrackSentToHub;
            if (handler != null) handler(saved);
        }

        private void Preview(int rowIndex)
        {
            if (rowIndex >= results.Count || string.IsNullOrWhiteSpace(results[rowIndex].Url)) return;
            if (results[rowIndex].Downloaded && !string.IsNullOrWhiteSpace(results[rowIndex].DownloadedPath) && File.Exists(results[rowIndex].DownloadedPath))
            {
                string error;
                if (AudioPreviewEngine.Play(results[rowIndex].DownloadedPath, out error))
                    statusLabel.Text = "Prescuchando archivo local descargado.";
                else
                    statusLabel.Text = "No se pudo prescuchar localmente: " + error;
                return;
            }
            try { Process.Start(results[rowIndex].Url); }
            catch (Exception ex) { statusLabel.Text = "No se pudo abrir la vista previa: " + ex.Message; }
        }

        private void OpenDestination()
        {
            string folder = AppSettings.GetDownloadFolder(SelectedType);
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", folder);
        }

        private void UpdateDestinationStatus()
        {
            if (statusLabel != null) statusLabel.Text = "Destino anfitrión: " + AppSettings.GetDownloadFolder(SelectedType);
        }

        private void SetBusy(bool busy, string message, Button activeButton, string buttonText)
        {
            progressBar.Visible = busy;
            toolbar.Enabled = !busy;
            resultsGrid.Enabled = !busy;
            if (activeButton != null && buttonText != null) activeButton.Text = buttonText;
            if (!string.IsNullOrEmpty(message)) statusLabel.Text = message;
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
            BindResults();
        }

        private void BindResults()
        {
            results.Sort((a, b) =>
            {
                int value;
                if (sortColumn == "Duration") value = ParseDuration(a.Duration).CompareTo(ParseDuration(b.Duration));
                else if (sortColumn == "MaximumQuality") value = a.MaxHeight.CompareTo(b.MaxHeight);
                else value = string.Compare(GetSortValue(a, sortColumn), GetSortValue(b, sortColumn), StringComparison.OrdinalIgnoreCase);
                return sortAscending ? value : -value;
            });
            resultsGrid.DataSource = null;
            resultsGrid.DataSource = results;
        }

        private void MarkDownloadedResults()
        {
            foreach (YouTubeTrack track in results)
            {
                DownloadRecord record = DownloadRegistry.Find(track.Url, track.Title, track.Uploader);
                track.Downloaded = record != null;
                track.DownloadedPath = record == null ? null : record.Path;
            }
        }

        private void ResultsGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= results.Count) return;
            YouTubeTrack track = results[e.RowIndex];
            if (!track.Downloaded) return;
            DataGridViewRow row = resultsGrid.Rows[e.RowIndex];
            row.DefaultCellStyle.BackColor = Color.FromArgb(210, 255, 210);
            row.DefaultCellStyle.ForeColor = Color.Black;
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(65, 145, 70);
            row.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        private static string GetSortValue(YouTubeTrack item, string column)
        {
            if (column == "Uploader") return item.Uploader ?? "";
            if (column == "AvailableOutputs") return item.AvailableOutputs ?? "";
            if (column == "MaximumQuality") return item.MaximumQuality ?? "";
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

        private static List<PlatformProfile> BuildPlatformProfiles()
        {
            return new List<PlatformProfile>
            {
                new PlatformProfile("YouTube", "yt-dlp real", "Descarga audio/video compatible", "MP3/M4A/MP4", "https://www.youtube.com/results?search_query={0}", true),
                new PlatformProfile("Spotify", "Web API / login", "Catalogo, playlists y playback autorizado", "Metadata/enlace", "https://open.spotify.com/search/{0}", false),
                new PlatformProfile("Apple Music", "MusicKit / Apple Music API", "Catalogo, charts, biblioteca y previews autorizados", "Metadata/enlace", "https://music.apple.com/us/search?term={0}", false),
                new PlatformProfile("SoundCloud", "API/enlace oficial", "Catalogo publico y preview segun disponibilidad", "Metadata/enlace", "https://soundcloud.com/search?q={0}", false),
                new PlatformProfile("Bandcamp", "Busqueda publica", "Catalogo publico del artista/album", "Metadata/enlace", "https://bandcamp.com/search?q={0}", false),
                new PlatformProfile("Deezer", "API publica/OAuth", "Catalogo, preview corto y metadata", "Metadata/preview", "https://www.deezer.com/search/{0}", false),
                new PlatformProfile("TIDAL", "API/login", "Catalogo y playback autorizado", "Metadata/enlace", "https://listen.tidal.com/search?q={0}", false),
                new PlatformProfile("Amazon Music", "Enlace oficial", "Busqueda y playback en app/web oficial", "Metadata/enlace", "https://music.amazon.com/search/{0}", false),
                new PlatformProfile("Audiomack", "API/enlace oficial", "Catalogo publico y streaming autorizado", "Metadata/enlace", "https://audiomack.com/search?q={0}", false),
                new PlatformProfile("Mixcloud", "API/enlace oficial", "Sets, mixes y metadata publica", "Metadata/enlace", "https://www.mixcloud.com/search/?q={0}", false),
                new PlatformProfile("Beatport", "Catalogo DJ oficial", "Catalogo de tracks DJ y compra/licencia", "Metadata/enlace", "https://www.beatport.com/search?q={0}", false)
            };
        }

        private class PlatformProfile
        {
            public PlatformProfile(string name, string apiMode, string capability, string outputs, string searchUrlTemplate, bool downloadsToHub)
            {
                Name = name;
                ApiMode = apiMode;
                Capability = capability;
                Outputs = outputs;
                SearchUrlTemplate = searchUrlTemplate;
                DownloadsToHub = downloadsToHub;
            }

            public string Name { get; private set; }
            public string ApiMode { get; private set; }
            public string Capability { get; private set; }
            public string Outputs { get; private set; }
            public string SearchUrlTemplate { get; private set; }
            public bool DownloadsToHub { get; private set; }
        }
    }
}
