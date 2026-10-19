using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArkaiosDJAssistant
{
    public class BatchDownloadItem
    {
        public bool Selected { get; set; } = true;
        public int Index { get; set; }
        public string RequestQuery { get; set; } = "";
        public string MatchedTitle { get; set; } = "";
        public string Url { get; set; } = "";
        public string Duration { get; set; } = "-";
        public string Status { get; set; } = "Pendiente";
        public string DownloadedPath { get; set; } = "";
        public string MediaType { get; set; } = "music";
    }

    public sealed class WebDownloadsControl : UserControl
    {
        public event Action<string> TrackSentToHub;

        // Sub-tabs principales
        private readonly TabControl modeTabs;
        private readonly TabPage batchTabPage;
        private readonly TabPage webTabPage;

        // --- MÓDULO 1: Descargas en Lote (Nombres y Playlists) ---
        private TextBox playlistUrlBox;
        private TextBox trackNamesBox;
        private ComboBox mediaTypeCombo;
        private ComboBox qualityCombo;
        private Button btnExtractPlaylist;
        private Button btnSearchTracks;
        private Button btnDownloadBatch;
        private Button btnStopBatch;
        private Button btnClearGrid;
        private Button btnOpenFolder;
        private DataGridView grid;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label countLabel;

        private readonly List<BatchDownloadItem> items = new List<BatchDownloadItem>();
        private readonly BindingSource bindingSource = new BindingSource();
        private CancellationTokenSource cts;
        private bool isProcessing = false;

        // --- MÓDULO 2: Navegador Web (WebView2) ---
        private readonly WebView2 browser = new WebView2 { Dock = DockStyle.Fill };
        private readonly ComboBox sites = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly Label webStatus = new Label { Dock = DockStyle.Bottom, Height = 28, AutoEllipsis = true };
        private readonly string[] urls = {
            "https://ytdlp.online/es/playlist-downloader",
            "https://flacdownloader.com",
            "https://spotidownloader.com/en19"
        };
        private bool initializingWeb;
        private bool webReady;

        public WebDownloadsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            modeTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Point(12, 6)
            };

            batchTabPage = new TabPage("⚡ Descargador en Lote (Nombres y Playlists)")
            {
                BackColor = Color.FromArgb(22, 22, 22),
                ForeColor = Color.White
            };

            webTabPage = new TabPage("🌐 Navegador Web de Descargas (WebView2)")
            {
                BackColor = Color.FromArgb(22, 22, 22),
                ForeColor = Color.White
            };

            // Construir panel de Descarga en Lote
            BuildBatchModule();

            // Construir panel de WebView2
            BuildWebModule();

            modeTabs.TabPages.Add(batchTabPage);
            modeTabs.TabPages.Add(webTabPage);
            Controls.Add(modeTabs);
        }

        private void BuildBatchModule()
        {
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(8)
            };
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 130f)); // Panel superior de entrada
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));  // Barra de herramientas / acciones
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Grilla de items
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));  // Barra de progreso y estado

            // 1. Panel de Entradas (Playlist URL + Track List)
            var inputSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            inputSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            inputSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            // Columna Izquierda: Nombres de pistas multilínea
            var leftBox = new GroupBox
            {
                Text = "📝 Lista de Canciones o Nombres (uno por línea)",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(0, 190, 255),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(2)
            };
            trackNamesBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "La ventanita - Garibaldi\r\nMagdalena\r\nVisita nuestro bar\r\nIsabel\r\nLiving la vida loca\r\nSopa de caracol\r\nLa morena pide más\r\nBanana Garibaldi\r\nGloria Gaynor - sobreviviré\r\nDanza kuduro\r\nMi dulce niña\r\nGood night - black eyed peas\r\nDespacito\r\nLa bomba\r\nEl noa noa\r\nMuchachita triste - los piratas del caribe\r\nEl baile del perrito\r\nEl baile del mono\r\nLa reina del swing\r\nEl baile del gorila\r\nFiesta en america\r\nRabiosa - shakira\r\nTalento de tv\r\nSerá que no me amas - Luis Miguel\r\nBanda blanca - Fiesta\r\nJuan Gabriel - Debo hacerlo\r\nLa bala - los tigres del norte\r\nBamboleo - Gipsy Kings"
            };
            leftBox.Controls.Add(trackNamesBox);

            // Columna Derecha: Enlace directo de Playlist (YouTube / Spotify)
            var rightBox = new GroupBox
            {
                Text = "🔗 Enlace de Playlist de YouTube / Spotify",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(80, 255, 140),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(2)
            };
            var rightLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(4)
            };
            var lblUrlPrompt = new Label
            {
                Text = "Pega aquí una URL completa de Playlist de YouTube o Spotify:",
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            playlistUrlBox = new TextBox
            {
                Width = 460,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "https://youtube.com/playlist?list=PLZYxWY3CdJAU"
            };
            btnExtractPlaylist = new Button
            {
                Text = "📂 Extraer Pistas de Playlist",
                AutoSize = true,
                Height = 32,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnExtractPlaylist.Click += async (s, e) => await ExtractFromPlaylistUrlAsync();

            rightLayout.Controls.Add(lblUrlPrompt);
            rightLayout.Controls.Add(playlistUrlBox);
            rightLayout.Controls.Add(btnExtractPlaylist);
            rightBox.Controls.Add(rightLayout);

            inputSplit.Controls.Add(leftBox, 0, 0);
            inputSplit.Controls.Add(rightBox, 1, 0);

            // 2. Barra de Herramientas y Acciones
            var actionToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 4),
                BackColor = Color.FromArgb(28, 28, 28)
            };

            btnSearchTracks = new Button
            {
                Text = "🔍 1. Cargar / Buscar Nombres",
                AutoSize = true,
                Height = 32,
                BackColor = Color.FromArgb(45, 95, 150),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSearchTracks.Click += async (s, e) => await LoadTracksFromTextBoxAsync();

            btnDownloadBatch = new Button
            {
                Text = "⚡ 2. Descargar Todo en Lote",
                AutoSize = true,
                Height = 32,
                BackColor = Color.FromArgb(0, 153, 76),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnDownloadBatch.Click += async (s, e) => await StartBatchDownloadAsync();

            btnStopBatch = new Button
            {
                Text = "⏹ Detener",
                AutoSize = true,
                Height = 32,
                BackColor = Color.FromArgb(180, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnStopBatch.Click += (s, e) => StopBatchDownload();

            btnClearGrid = new Button
            {
                Text = "🗑 Limpiar Lista",
                AutoSize = true,
                Height = 32,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnClearGrid.Click += (s, e) => ClearGrid();

            btnOpenFolder = new Button
            {
                Text = "📁 Abrir Destino",
                AutoSize = true,
                Height = 32,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat
            };
            btnOpenFolder.Click += (s, e) => OpenDestination();

            var lblFormat = new Label { Text = " Formato:", AutoSize = true, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 8, 0, 0) };
            mediaTypeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            mediaTypeCombo.Items.AddRange(new object[] { "Música (Audio)", "Video", "Karaoke" });
            mediaTypeCombo.SelectedIndex = 0;
            mediaTypeCombo.SelectedIndexChanged += (s, e) => UpdateQualityCombo();

            qualityCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };
            UpdateQualityCombo();

            actionToolbar.Controls.Add(btnSearchTracks);
            actionToolbar.Controls.Add(btnDownloadBatch);
            actionToolbar.Controls.Add(btnStopBatch);
            actionToolbar.Controls.Add(btnClearGrid);
            actionToolbar.Controls.Add(btnOpenFolder);
            actionToolbar.Controls.Add(lblFormat);
            actionToolbar.Controls.Add(mediaTypeCombo);
            actionToolbar.Controls.Add(qualityCombo);

            // 3. Grilla de Resultados
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.FromArgb(18, 18, 18),
                ForeColor = Color.Black,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None
            };
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 24, 24);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 90, 160);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "SelectedCol", HeaderText = "Bajar", DataPropertyName = "Selected", Width = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IndexCol", HeaderText = "#", DataPropertyName = "Index", ReadOnly = true, Width = 40 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReqCol", HeaderText = "Canción Solicitada / Pista", DataPropertyName = "RequestQuery", Width = 280 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MatchCol", HeaderText = "Coincidencia en YouTube", DataPropertyName = "MatchedTitle", ReadOnly = true, Width = 300 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DurCol", HeaderText = "Duración", DataPropertyName = "Duration", ReadOnly = true, Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StatusCol", HeaderText = "Estado de Descarga", DataPropertyName = "Status", ReadOnly = true, Width = 220 });
            
            var actionCol = new DataGridViewButtonColumn
            {
                Name = "ActionCol",
                HeaderText = "Acción",
                Text = "▶ Escuchar / Ver",
                UseColumnTextForButtonValue = true,
                Width = 110
            };
            grid.Columns.Add(actionCol);

            grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["ActionCol"].Index)
                {
                    var item = items[e.RowIndex];
                    if (!string.IsNullOrWhiteSpace(item.DownloadedPath) && File.Exists(item.DownloadedPath))
                    {
                        try { Process.Start(new ProcessStartInfo(item.DownloadedPath) { UseShellExecute = true }); }
                        catch { }
                    }
                    else if (!string.IsNullOrWhiteSpace(item.Url))
                    {
                        try { Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true }); }
                        catch { }
                    }
                }
            };

            bindingSource.DataSource = items;
            grid.DataSource = bindingSource;

            // 4. Barra de Progreso y Estado Inferior
            var bottomBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 3,
                Margin = new Padding(0)
            };
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Listo. Pega nombres de canciones o una URL de playlist para comenzar.",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9f)
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 20,
                Visible = false
            };

            countLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Pistas: 0",
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            bottomBar.Controls.Add(statusLabel, 0, 0);
            bottomBar.Controls.Add(progressBar, 1, 0);
            bottomBar.Controls.Add(countLabel, 2, 0);

            mainContainer.Controls.Add(inputSplit, 0, 0);
            mainContainer.Controls.Add(actionToolbar, 0, 1);
            mainContainer.Controls.Add(grid, 0, 2);
            mainContainer.Controls.Add(bottomBar, 0, 3);

            batchTabPage.Controls.Add(mainContainer);
        }

        private void BuildWebModule()
        {
            var webToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(28, 28, 28)
            };

            sites.Items.AddRange(new object[] { "yt-dlp · Listas (ytdlp.online)", "FLAC Downloader", "SpotiDownloader" });
            sites.SelectedIndex = 0;
            webToolbar.Controls.Add(sites);

            AddButton(webToolbar, "🌐 Abrir Web", async () => await OpenWebSelectedAsync());
            AddButton(webToolbar, "◀ Atrás", () => { if (webReady && browser.CanGoBack) browser.GoBack(); });
            AddButton(webToolbar, "▶ Adelante", () => { if (webReady && browser.CanGoForward) browser.GoForward(); });
            AddButton(webToolbar, "🔄 Recargar", () => { if (webReady) browser.Reload(); });
            AddButton(webToolbar, "↗ Navegador Externo", () => {
                try {
                    var url = webReady ? browser.Source?.AbsoluteUri : null;
                    if (!IsWebUrl(url)) url = urls[sites.SelectedIndex];
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                } catch (Exception ex) { webStatus.Text = ex.Message; }
            });

            webStatus.Text = "Selecciona una web y pulsa Abrir Web para explorarla con WebView2.";
            webStatus.ForeColor = Color.LightGray;

            webTabPage.Controls.Add(browser);
            webTabPage.Controls.Add(webToolbar);
            webTabPage.Controls.Add(webStatus);
        }

        private void UpdateQualityCombo()
        {
            qualityCombo.Items.Clear();
            if (mediaTypeCombo.SelectedIndex == 0) // Música
            {
                qualityCombo.Items.AddRange(new object[] { "MP3 320 kbps", "MP3 192 kbps", "M4A original" });
                qualityCombo.SelectedIndex = 0;
            }
            else if (mediaTypeCombo.SelectedIndex == 1) // Video
            {
                qualityCombo.Items.AddRange(new object[] { "720p HD", "1080p Full HD", "Máxima disponible" });
                qualityCombo.SelectedIndex = 0;
            }
            else // Karaoke
            {
                qualityCombo.Items.AddRange(new object[] { "720p Karaoke", "MP3 Karaoke" });
                qualityCombo.SelectedIndex = 0;
            }
        }

        private async Task ExtractFromPlaylistUrlAsync()
        {
            string url = (playlistUrlBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Por favor ingresa un enlace de Playlist válido.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Extrayendo pistas de la playlist en tiempo real...", true);
            try
            {
                List<YouTubeTrack> extracted = await YouTubeEngine.ExtractPlaylistFlatAsync(url, 200);
                if (extracted.Count == 0)
                {
                    // Intento con PlaylistExtractorClient
                    var extResult = await PlaylistExtractorClient.ExtractAsync(url);
                    if (extResult != null && extResult.Items != null && extResult.Items.Count > 0)
                    {
                        foreach (var it in extResult.Items)
                        {
                            extracted.Add(new YouTubeTrack
                            {
                                Title = it.Title,
                                Url = it.Url,
                                Duration = it.Duration ?? "-",
                                Uploader = it.Uploader ?? "YouTube"
                            });
                        }
                    }
                }

                if (extracted.Count == 0)
                {
                    statusLabel.Text = "No se pudieron extraer pistas de esa URL. Intenta con los nombres en la columna izquierda.";
                    return;
                }

                items.Clear();
                int num = 1;
                foreach (var track in extracted)
                {
                    items.Add(new BatchDownloadItem
                    {
                        Index = num++,
                        RequestQuery = track.Title,
                        MatchedTitle = track.Title + (string.IsNullOrWhiteSpace(track.Uploader) ? "" : " (" + track.Uploader + ")"),
                        Url = track.Url,
                        Duration = track.Duration,
                        Status = "En cola para descarga",
                        MediaType = GetCurrentMediaType()
                    });
                }
                bindingSource.ResetBindings(false);
                statusLabel.Text = "✔ Se extrajeron " + items.Count + " canciones de la playlist. Pulsa 'Descargar Todo en Lote'.";
                countLabel.Text = "Pistas: " + items.Count;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Error al extraer playlist: " + ex.Message;
            }
            finally
            {
                SetBusy(false, null, false);
            }
        }

        private async Task LoadTracksFromTextBoxAsync()
        {
            string text = (trackNamesBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Por favor ingresa una o más canciones en la lista.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanLines = lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();

            if (cleanLines.Count == 0) return;

            items.Clear();
            int num = 1;
            foreach (var line in cleanLines)
            {
                items.Add(new BatchDownloadItem
                {
                    Index = num++,
                    RequestQuery = line,
                    MatchedTitle = "Búsqueda pendiente...",
                    Url = line.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? line : "",
                    Duration = "-",
                    Status = "En cola",
                    MediaType = GetCurrentMediaType()
                });
            }
            bindingSource.ResetBindings(false);
            countLabel.Text = "Pistas: " + items.Count;
            statusLabel.Text = "Cargadas " + items.Count + " canciones. Puedes iniciar la descarga directa o buscar en YouTube.";
        }

        private async Task StartBatchDownloadAsync()
        {
            if (items.Count == 0)
            {
                await LoadTracksFromTextBoxAsync();
                if (items.Count == 0) return;
            }

            var selected = items.Where(i => i.Selected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No hay ninguna canción marcada con la casilla [Bajar].", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isProcessing = true;
            cts = new CancellationTokenSource();
            btnDownloadBatch.Enabled = false;
            btnSearchTracks.Enabled = false;
            btnExtractPlaylist.Enabled = false;
            btnStopBatch.Enabled = true;
            progressBar.Visible = true;
            progressBar.Minimum = 0;
            progressBar.Maximum = selected.Count;
            progressBar.Value = 0;

            int okCount = 0;
            int failCount = 0;
            string mediaType = GetCurrentMediaType();
            string quality = (qualityCombo.SelectedItem ?? "MP3 320 kbps").ToString();

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (cts.Token.IsCancellationRequested) break;

                    var item = selected[i];
                    progressBar.Value = i;
                    statusLabel.Text = $"[{i + 1}/{selected.Count}] Procesando: {item.RequestQuery}";
                    item.Status = "⏳ Buscando / Descargando...";
                    bindingSource.ResetBindings(false);

                    string downloadUrl = item.Url;

                    // Si no tiene URL directa, buscarla en YouTube
                    if (string.IsNullOrWhiteSpace(downloadUrl) || !downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var matches = await YouTubeEngine.SearchAsync(item.RequestQuery, mediaType, 1);
                            if (matches.Count > 0)
                            {
                                downloadUrl = matches[0].Url;
                                item.Url = downloadUrl;
                                item.MatchedTitle = matches[0].Title + " (" + matches[0].Uploader + ")";
                                item.Duration = matches[0].Duration;
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        item.Status = "❌ No se encontró en YouTube";
                        failCount++;
                        bindingSource.ResetBindings(false);
                        continue;
                    }

                    // Descargar con el motor oficial yt-dlp
                    item.Status = "⬇ Descargando audio alta calidad...";
                    bindingSource.ResetBindings(false);

                    string savedPath = null;
                    try
                    {
                        savedPath = await YouTubeEngine.DownloadAsync(downloadUrl, mediaType, quality);
                    }
                    catch (Exception ex)
                    {
                        item.Status = "❌ Error: " + ex.Message;
                    }

                    if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
                    {
                        okCount++;
                        item.DownloadedPath = savedPath;
                        item.Status = "✔ OK: " + Path.GetFileName(savedPath);

                        // Registrar en VirtualDJ Hub
                        DownloadRegistry.Register(savedPath, downloadUrl, item.RequestQuery, item.MatchedTitle, mediaType);
                        TrackSentToHub?.Invoke(savedPath);
                    }
                    else
                    {
                        failCount++;
                        if (!item.Status.StartsWith("❌")) item.Status = "❌ Falló descarga";
                    }

                    bindingSource.ResetBindings(false);
                    await Task.Delay(200); // Pequeña pausa para no saturar requests
                }
            }
            finally
            {
                progressBar.Value = progressBar.Maximum;
                isProcessing = false;
                btnDownloadBatch.Enabled = true;
                btnSearchTracks.Enabled = true;
                btnExtractPlaylist.Enabled = true;
                btnStopBatch.Enabled = false;

                statusLabel.Text = $"Descarga finalizada: {okCount} exitosas, {failCount} fallidas. Archivos indexados en Hub.";
                MessageBox.Show($"Lote completado exitosamente.\n\n✔ Descargadas: {okCount}\n❌ Fallidas: {failCount}\n\nLas canciones están listas en tu carpeta y en el Hub de VirtualDJ.", "Descarga en Lote ARKAIOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void StopBatchDownload()
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
                statusLabel.Text = "Detención solicitada. Finalizando pista en curso...";
            }
        }

        private void ClearGrid()
        {
            if (isProcessing) return;
            items.Clear();
            bindingSource.ResetBindings(false);
            countLabel.Text = "Pistas: 0";
            statusLabel.Text = "Lista limpiada.";
        }

        private void OpenDestination()
        {
            try
            {
                string folder = AppSettings.GetDownloadFolder(GetCurrentMediaType());
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir la carpeta: " + ex.Message);
            }
        }

        private string GetCurrentMediaType()
        {
            if (mediaTypeCombo.SelectedIndex == 1) return "video";
            if (mediaTypeCombo.SelectedIndex == 2) return "karaoke";
            return "music";
        }

        private void SetBusy(bool busy, string message, bool showProgress)
        {
            progressBar.Visible = showProgress;
            if (showProgress) progressBar.Style = ProgressBarStyle.Marquee;
            else progressBar.Style = ProgressBarStyle.Continuous;
            btnSearchTracks.Enabled = !busy;
            btnDownloadBatch.Enabled = !busy;
            btnExtractPlaylist.Enabled = !busy;
            if (message != null) statusLabel.Text = message;
        }

        // --- MÉTODOS DEL MÓDULO WEBVIEW2 ---
        private static bool IsWebUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        private static void AddButton(FlowLayoutPanel panel, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            button.Click += (sender, args) => action();
            panel.Controls.Add(button);
        }

        private async Task OpenWebSelectedAsync()
        {
            if (initializingWeb || IsDisposed) return;
            initializingWeb = true;
            try
            {
                if (!webReady)
                {
                    webStatus.Text = "Inicializando WebView2…";
                    var profile = Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData), "ArkaiosDJ", "WebDownloads");
                    var environment = await CoreWebView2Environment.CreateAsync(null, profile);
                    if (IsDisposed) return;
                    await browser.EnsureCoreWebView2Async(environment);
                    if (IsDisposed) return;
                    browser.CoreWebView2.NewWindowRequested += (sender, args) =>
                    {
                        args.Handled = true;
                        if (args.IsUserInitiated && IsWebUrl(args.Uri))
                            browser.CoreWebView2.Navigate(args.Uri);
                    };
                    browser.CoreWebView2.NavigationStarting += (sender, args) =>
                    {
                        if (!IsWebUrl(args.Uri) && args.Uri != "about:blank")
                            args.Cancel = true;
                    };
                    browser.CoreWebView2.NavigationCompleted += (sender, args) =>
                    {
                        if (!IsDisposed) webStatus.Text = args.IsSuccess
                            ? browser.Source?.AbsoluteUri
                            : "No se pudo cargar: " + args.WebErrorStatus + ". Puedes abrir en navegador externo.";
                    };
                    webReady = true;
                }
                webStatus.Text = "Cargando…";
                browser.CoreWebView2.Navigate(urls[sites.SelectedIndex]);
            }
            catch (Exception ex)
            {
                if (!IsDisposed) webStatus.Text = "WebView2 no disponible: " + ex.Message
                    + ". Instala Microsoft Edge WebView2 Runtime o abre en navegador.";
            }
            finally { initializingWeb = false; }
        }
    }
}
