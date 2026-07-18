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
        private readonly TextBox queryBox;
        private readonly ComboBox typeBox;
        private readonly ComboBox qualityBox;
        private readonly DataGridView resultsGrid;
        private readonly Label statusLabel;
        private List<YouTubeTrack> results = new List<YouTubeTrack>();

        public MediaSearchControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8), WrapContents = false };
            queryBox = new TextBox { Width = 360 };
            queryBox.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };
            typeBox = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => UpdateQualityOptions();
            qualityBox = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            var searchButton = new Button { Text = "Buscar", AutoSize = true };
            searchButton.Click += async (s, e) => await SearchAsync();
            var folderButton = new Button { Text = "Abrir destino", AutoSize = true };
            folderButton.Click += (s, e) => OpenDestination();
            top.Controls.AddRange(new Control[] { queryBox, typeBox, qualityBox, searchButton, folderButton });

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
            resultsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Acción", Text = "Descargar", UseColumnTextForButtonValue = true, Width = 95 });
            resultsGrid.CellContentClick += async (s, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 4) await DownloadAsync(e.RowIndex); };

            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), ForeColor = Color.LightGray };
            Controls.Add(resultsGrid);
            Controls.Add(statusLabel);
            Controls.Add(top);
            UpdateQualityOptions();
            UpdateDestinationStatus();
        }

        private string SelectedType { get { return typeBox.SelectedItem.ToString().ToLowerInvariant(); } }

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
            statusLabel.Text = "Buscando y leyendo formatos reales con yt-dlp...";
            resultsGrid.DataSource = null;
            results = await YouTubeEngine.SearchAsync(queryBox.Text.Trim(), SelectedType, 10);
            resultsGrid.DataSource = results;
            statusLabel.Text = results.Count == 0 ? "No hubo resultados. Verifica yt-dlp o la conexión." :
                string.Format("{0} resultados. Destino: {1}", results.Count, AppSettings.GetDownloadFolder(SelectedType));
        }

        private async Task DownloadAsync(int rowIndex)
        {
            if (rowIndex >= results.Count) return;
            resultsGrid.Enabled = false;
            statusLabel.Text = "Descargando " + results[rowIndex].Title + "...";
            string saved = await YouTubeEngine.DownloadAsync(results[rowIndex].Url, SelectedType, qualityBox.SelectedItem.ToString());
            resultsGrid.Enabled = true;
            statusLabel.Text = string.IsNullOrEmpty(saved) ? "La descarga falló; consulta yt-dlp-errors.log." : "Guardado: " + saved;
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
    }
}
