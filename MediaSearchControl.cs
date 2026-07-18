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
        private List<YouTubeTrack> results = new List<YouTubeTrack>();

        public MediaSearchControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(8), WrapContents = true };
            queryBox = new TextBox { Width = 270 };
            queryBox.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
            platformBox = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList };
            platformBox.Items.AddRange(new object[] { "YouTube", "Spotify", "Apple Music", "SoundCloud", "Bandcamp", "Deezer", "TIDAL", "Amazon Music", "Audiomack", "Mixcloud", "Beatport" });
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
            adjustButton = new Button { Text = "Ajustar a Google Música/Video (simulado)", AutoSize = true };
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
            resultsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Vista previa", Text = "Previsualizar", UseColumnTextForButtonValue = true, Width = 105 });
            resultsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Hub", Text = "Descargar al Hub", UseColumnTextForButtonValue = true, Width = 125 });
            resultsGrid.CellContentClick += async (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == 5) Preview(e.RowIndex);
                else if (e.ColumnIndex == 6) await DownloadAsync(e.RowIndex);
            };

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
            if (!string.Equals(SelectedPlatform, "YouTube", StringComparison.OrdinalIgnoreCase))
            {
                OpenOfficialPlatformSearch();
                return;
            }
            SetBusy(true, "Buscando y leyendo formatos reales con yt-dlp...", searchButton, "Buscando...");
            try
            {
                resultsGrid.DataSource = null;
                results = await YouTubeEngine.SearchAsync(queryBox.Text.Trim(), SelectedType, 20);
                resultsGrid.DataSource = results;
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
            finally { SetBusy(false, null, adjustButton, "Ajustar a Google Música/Video (simulado)"); }
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                statusLabel.Text = "No encontré una corrección suficientemente confiable; conserva o amplía tu consulta.";
                return;
            }

            DialogResult answer = MessageBox.Show(
                "Consulta escrita:\n" + original + "\n\n¿Quisiste decir?\n" + suggestion +
                "\n\nValidación real: metadatos públicos de YouTube. La etiqueta Google Música/Video es simulada.",
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
            statusLabel.Text = youtube ? "YouTube: búsqueda real, previsualización y descarga al Hub." :
                SelectedPlatform + ": búsqueda oficial en navegador. La tabla interna requiere API/login del servicio.";
        }

        private void OpenOfficialPlatformSearch()
        {
            string query = Uri.EscapeDataString(queryBox.Text.Trim());
            var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Spotify", "https://open.spotify.com/search/" + query },
                { "Apple Music", "https://music.apple.com/us/search?term=" + query },
                { "SoundCloud", "https://soundcloud.com/search?q=" + query },
                { "Bandcamp", "https://bandcamp.com/search?q=" + query },
                { "Deezer", "https://www.deezer.com/search/" + query },
                { "TIDAL", "https://listen.tidal.com/search?q=" + query },
                { "Amazon Music", "https://music.amazon.com/search/" + query },
                { "Audiomack", "https://audiomack.com/search?q=" + query },
                { "Mixcloud", "https://www.mixcloud.com/search/?q=" + query },
                { "Beatport", "https://www.beatport.com/search?q=" + query }
            };
            string url;
            if (!urls.TryGetValue(SelectedPlatform, out url)) return;
            try
            {
                Process.Start(url);
                statusLabel.Text = "Búsqueda abierta en " + SelectedPlatform + ". No se habilitan descargas desde servicios protegidos.";
            }
            catch (Exception ex) { statusLabel.Text = "No se pudo abrir " + SelectedPlatform + ": " + ex.Message; }
        }

        private async Task DownloadAsync(int rowIndex)
        {
            if (rowIndex >= results.Count) return;
            SetBusy(true, "Descargando " + results[rowIndex].Title + "...", null, null);
            string saved;
            try { saved = await YouTubeEngine.DownloadAsync(results[rowIndex].Url, SelectedType, qualityBox.SelectedItem.ToString()); }
            finally { SetBusy(false, null, null, null); }
            if (string.IsNullOrEmpty(saved))
            {
                statusLabel.Text = "La descarga falló; consulta yt-dlp-errors.log.";
                return;
            }

            statusLabel.Text = "Guardado y enviado al Hub: " + saved;
            var handler = TrackSentToHub;
            if (handler != null) handler(saved);
        }

        private void Preview(int rowIndex)
        {
            if (rowIndex >= results.Count || string.IsNullOrWhiteSpace(results[rowIndex].Url)) return;
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
    }
}
