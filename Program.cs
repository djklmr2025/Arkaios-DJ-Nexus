using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Globalization;

namespace ArkaiosDJAssistant
{
    public class Track
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public double Bpm { get; set; }
        public string Key { get; set; }
        public string CamelotKey { get; set; }
        public string Genre { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string RankIcon { get; set; }
        public int MatchScore { get; set; }
    }

    public static class HarmonicEngine
    {
        // Maps traditional keys to Camelot Wheel keys
        private static readonly Dictionary<string, string> KeyToCamelot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"B", "1B"}, {"G#m", "1A"}, {"Abm", "1A"},
            {"F#", "2B"}, {"Gb", "2B"}, {"D#m", "2A"}, {"Ebm", "2A"},
            {"Db", "3B"}, {"C#", "3B"}, {"Bbm", "3A"}, {"A#m", "3A"},
            {"Ab", "4B"}, {"G#", "4B"}, {"Fm", "4A"},
            {"Eb", "5B"}, {"D#", "5B"}, {"Cm", "5A"},
            {"Bb", "6B"}, {"A#", "6B"}, {"Gm", "6A"},
            {"F", "7B"}, {"Dm", "7A"},
            {"C", "8B"}, {"Am", "8A"},
            {"G", "9B"}, {"Em", "9A"},
            {"D", "10B"}, {"Bm", "10A"},
            {"A", "11B"}, {"F#m", "11A"}, {"Gbm", "11A"},
            {"E", "12B"}, {"C#m", "12A"}, {"Dbm", "12A"}
        };

        public static string ConvertToCamelot(string rawKey)
        {
            if (string.IsNullOrEmpty(rawKey)) return "";
            string camelot;
            if (KeyToCamelot.TryGetValue(rawKey, out camelot)) return camelot;

            // Si ya es un valor Camelot (ej: 8A), regresarlo directo
            if (rawKey.Length >= 2 && char.IsDigit(rawKey[0]) && (rawKey.EndsWith("A", StringComparison.OrdinalIgnoreCase) || rawKey.EndsWith("B", StringComparison.OrdinalIgnoreCase)))
            {
                return rawKey.ToUpper();
            }
            return rawKey;
        }


        public static bool IsCompatible(string currentKey, string targetKey)
        {
            return CalculateCamelotScore(currentKey, targetKey) > 0;
        }

        public static int CalculateCamelotScore(string key1, string key2)
        {
            if (string.IsNullOrEmpty(key1) || string.IsNullOrEmpty(key2)) return 0;
            int num1, num2;
            char letter1, letter2;
            if (!ParseCamelot(key1, out num1, out letter1)) return 0;
            if (!ParseCamelot(key2, out num2, out letter2)) return 0;

            if (num1 == num2 && letter1 == letter2) return 4; // Perfect Match
            if (num1 == num2 && letter1 != letter2) return 4; // Modal Change
            
            if (letter1 == letter2)
            {
                // Adjacent on wheel (Perfect Fifth up/down)
                if (Math.Abs(num1 - num2) == 1 || (num1 == 1 && num2 == 12) || (num1 == 12 && num2 == 1)) return 3;
                
                // Energy Boost +1 Semitone (+7 on wheel)
                int boost1 = (num1 + 7) > 12 ? (num1 + 7) - 12 : (num1 + 7);
                if (num2 == boost1) return 2;

                // Energy Boost +2 Semitones (+2 on wheel)
                int boost2 = (num1 + 2) > 12 ? (num1 + 2) - 12 : (num1 + 2);
                if (num2 == boost2) return 2;
            }
            
            return 0;
        }

        private static bool ParseCamelot(string key, out int num, out char letter)
        {
            num = 0; letter = ' ';
            if (key.Length < 2) return false;
            letter = key[key.Length - 1];
            if (letter != 'A' && letter != 'B') return false;
            return int.TryParse(key.Substring(0, key.Length - 1), out num);
        }
    }

    public class MainForm : Form
    {
        private ListView trackList;
        private Label lblNowPlaying;
        private Label lblStatus;
        private List<Track> allTracks = new List<Track>();
        private Timer historyTimer;
        private string vdjHistoryFolder;
        private string currentPlayingFile = "";

        private TableLayoutPanel mainPanel;
        private ContextMenuStrip columnMenu;
        private Panel idlePanel;
        private Button btnSettings;
        private bool isIdle = true;
        private double baseOpacity = 1.0;
        private readonly bool automaticOnlineSearchEnabled = false;
        private readonly List<string> downloadedHubTracks = new List<string>();
        private DownloadHubControl downloadHub;
        private TabControl mainTabs;
        private TabPage assistantTab;
        private TabPage allTracksTab;
        private TabPage mediaTab;
        private TabPage hitsTab;
        private TabPage hubTab;
        private TabPage organizerTab;
        private int trackSortColumn = 0;
        private bool trackSortAscending = true;

        public MainForm()
        {
            this.Text = "ARKAIOS DJ Assistant (VirtualDJ 8 Drag&Drop)";
            this.Size = new Size(900, 600);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.ForeColor = Color.White;
            this.MinimumSize = new Size(400, 300);

            mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainTabs = new TabControl { Dock = DockStyle.Fill };
            assistantTab = new TabPage("Auto Help + Camelot") { BackColor = Color.FromArgb(20, 20, 20) };
            allTracksTab = new TabPage("Buscar All Tracks") { BackColor = Color.FromArgb(20, 20, 20) };
            mediaTab = new TabPage("Buscar y descargar") { BackColor = Color.FromArgb(20, 20, 20) };
            hitsTab = new TabPage("Hits / Plataformas") { BackColor = Color.FromArgb(20, 20, 20) };
            hubTab = new TabPage("Descargas / Hub local") { BackColor = Color.FromArgb(20, 20, 20) };
            organizerTab = new TabPage("Organizador IA / Renombrador") { BackColor = Color.FromArgb(20, 20, 20) };
            assistantTab.Controls.Add(mainPanel);
            var allTracksControl = new AllTracksSearchControl();
            allTracksControl.TrackSentToHub += path => { AddTrackToHub(path, true); downloadHub.AddDownloadedFile(path); };
            allTracksTab.Controls.Add(allTracksControl);
            var mediaSearch = new MediaSearchControl();
            mediaSearch.TrackSentToHub += path => { AddTrackToHub(path, true); downloadHub.AddDownloadedFile(path); };
            mediaSearch.TrackViewRequested += path => AddTrackToHub(path, true);
            mediaTab.Controls.Add(mediaSearch);
            var hitsControl = new HitsTracksControl();
            hitsControl.TrackSentToHub += path => { AddTrackToHub(path, true); downloadHub.AddDownloadedFile(path); };
            hitsTab.Controls.Add(hitsControl);
            downloadHub = new DownloadHubControl();
            downloadHub.RefreshLibraryRequested += RefreshVirtualDjLibrary;
            hubTab.Controls.Add(downloadHub);
            organizerTab.Controls.Add(new OrganizerControl());
            ApplyTabVisibility();
            this.Controls.Add(mainTabs);

            lblNowPlaying = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(10),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Cargando Base de Datos de VirtualDJ...",
                BackColor = Color.FromArgb(30, 30, 30),
                MaximumSize = new Size(this.ClientSize.Width - 20, 0)
            };

            // Botón de Settings
            btnSettings = new Button 
            {
                Text = "⚙ Settings",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.LightGray,
                BackColor = Color.FromArgb(40, 40, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true
            };
            btnSettings.Click += (s, e) => {
                var sf = new SettingsForm();
                if (sf.ShowDialog() == DialogResult.OK) {
                    ApplySettings();
                }
            };
            // Se inserta en un panel superior con layout correcto
            TableLayoutPanel pnlTop = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, RowCount = 1, ColumnCount = 2 };
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblNowPlaying.Dock = DockStyle.Fill;
            pnlTop.Controls.Add(lblNowPlaying, 0, 0);
            pnlTop.Controls.Add(btnSettings, 1, 0);
            mainPanel.Controls.Add(pnlTop, 0, 0);

            trackList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                HideSelection = false,
                Visible = false // Inicia oculto por el Idle Mode
            };
            
            var colRank = trackList.Columns.Add("Rank", 60);
            var colTitle = trackList.Columns.Add("Title", 250);
            var colArtist = trackList.Columns.Add("Artist", 150);
            var colBpm = trackList.Columns.Add("BPM", 60);
            var colKey = trackList.Columns.Add("Key", 60);
            var colFileName = trackList.Columns.Add("File Name", 200);
            var colFileType = trackList.Columns.Add("Type", 60);
            trackList.ColumnClick += (s, ev) => SortTrackListByColumn(ev.Column);

            // Menú contextual para ocultar/mostrar columnas
            columnMenu = new ContextMenuStrip();
            foreach (ColumnHeader col in trackList.Columns)
            {
                var item = new ToolStripMenuItem(col.Text)
                {
                    Checked = true,
                    CheckOnClick = true,
                    Tag = col
                };
                item.CheckedChanged += (s, ev) => 
                {
                    var c = (ColumnHeader)((ToolStripMenuItem)s).Tag;
                    if (item.Checked) c.Width = c.Text == "BPM" || c.Text == "Key" || c.Text == "Type" || c.Text == "Rank" ? 60 : 150;
                    else c.Width = 0;
                };
                columnMenu.Items.Add(item);
            }
            trackList.ContextMenuStrip = columnMenu;
            downloadedHubTracks.AddRange(DownloadRegistry.ExistingPaths());

            // OwnerDraw for colored stars
            trackList.OwnerDraw = true;
            trackList.DrawColumnHeader += (s, ev) => ev.DrawDefault = true;
            trackList.DrawItem += (s, ev) => { /* NO setear DrawDefault=true aqui, o DrawSubItem no se ejecuta */ };
            trackList.DrawSubItem += (s, ev) => 
            {
                if (ev.ColumnIndex == 0) // Columna "Rank"
                {
                    ev.DrawBackground();
                    
                    string text = ev.SubItem.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Color starColor = Color.White;
                        if (text.Contains("GOLD")) { text = "★★★★★"; starColor = Color.Gold; }
                        else if (text.Contains("SILVER")) { text = "★★★★"; starColor = Color.Silver; }
                        else if (text.Contains("BRONZE")) { text = "★★★"; starColor = Color.Chocolate; }

                        using (SolidBrush b = new SolidBrush(starColor))
                        {
                            ev.Graphics.DrawString(text, ev.Item.Font, b, ev.Bounds.X, ev.Bounds.Y + 2);
                        }
                    }
                }
                else
                {
                    ev.DrawDefault = true;
                }
            };

            // Autoajustar columnas y cabecera al redimensionar ventana
            this.Resize += (s, ev) => 
            {
                // Forzar que el texto del título se envuelva (wrap) al achicar la ventana
                lblNowPlaying.MaximumSize = new Size(this.ClientSize.Width - 20, 0);
            };

            trackList.MouseDoubleClick += async (s, ev) =>
            {
                if (trackList.SelectedItems.Count > 0)
                {
                    var item = trackList.SelectedItems[0];
                    string tagStr = item.Tag as string;
                    
                    if (tagStr != null && tagStr.StartsWith("YOUTUBE:"))
                    {
                        string url = tagStr.Substring(8);
                        item.Text = "⏳";
                        item.ForeColor = Color.Yellow;
                        
                        string localPath = await YouTubeEngine.DownloadAudioAsync(url);
                        
                        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                        {
                            item.Text = "🎬"; // Listo
                            item.ForeColor = Color.LightGreen;
                            item.SubItems[6].Text = ".mp3"; // Type
                            item.Tag = localPath; // Ahora es arrastrable!
                            MessageBox.Show("¡Descarga completada!\nYa puedes arrastrar la pista a VirtualDJ.", "Descarga Exitosa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            item.Text = "❌";
                            item.ForeColor = Color.Red;
                        }
                    }
                    else if (tagStr != null && tagStr.StartsWith("MISSING:"))
                    {
                        await DownloadMissingRecommendationAsync(item, tagStr.Substring(8));
                        if (DateTime.Now.Ticks < 0)
                        {
                        string query = tagStr.Substring(8);
                        item.Text = "🔍"; // Buscando
                        item.ForeColor = Color.Orange;
                        
                        var ytTracks = await YouTubeEngine.SearchArtistAsync(query);
                        if (ytTracks != null && ytTracks.Count > 0)
                        {
                            string url = ytTracks[0].Url;
                            item.Text = "⏳";
                            item.ForeColor = Color.Yellow;
                            
                            string localPath = await YouTubeEngine.DownloadAudioAsync(url);
                            
                            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                            {
                                item.Text = "🎬"; // Listo
                                item.ForeColor = Color.LightGreen;
                                item.SubItems[6].Text = ".mp3"; // Type
                                item.Tag = localPath; // Ahora es arrastrable!
                            }
                            else
                            {
                                item.Text = "❌";
                                item.ForeColor = Color.Red;
                            }
                        }
                        else
                        {
                            item.Text = "❌";
                            item.ForeColor = Color.Red;
                            MessageBox.Show("No se encontró el track en YouTube.", "No encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        }
                    }
                }
            };

            trackList.Resize += (s, ev) => 
            {
                if (trackList.Columns.Count > 1 && trackList.Columns[1].Width > 0)
                {
                    int totalOther = 0;
                    for (int i = 0; i < trackList.Columns.Count; i++) {
                        if (i != 1) totalOther += trackList.Columns[i].Width;
                    }
                    int newWidth = trackList.ClientSize.Width - totalOther - 25; // 25 para evitar scroll horizontal
                    trackList.Columns[1].Width = newWidth > 50 ? newWidth : 50;
                }
            };

            // Activar Drag and Drop
            trackList.ItemDrag += TrackList_ItemDrag;
            
            // Idle Panel
            idlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 20) };
            
            TableLayoutPanel idleLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            idleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            idleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            idleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            idlePanel.Controls.Add(idleLayout);

            Label lblIdle = new Label 
            {
                Text = "No track playing\n\nPlay a track on your\nVirtualDJ",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomCenter,
                Font = new Font("Segoe UI", 14),
                ForeColor = Color.LightGreen,
                AutoSize = true
            };
            idleLayout.Controls.Add(lblIdle, 0, 0);

            Button btnLaunchVdj = new Button
            {
                Text = "Abrir VirtualDJ",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 40),
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 20, 0, 0)
            };
            btnLaunchVdj.Click += (s, ev) =>
            {
                if (!string.IsNullOrEmpty(AppSettings.VdjExecutableFile) && File.Exists(AppSettings.VdjExecutableFile))
                {
                    try { System.Diagnostics.Process.Start(AppSettings.VdjExecutableFile); }
                    catch { MessageBox.Show("Error al intentar abrir VirtualDJ.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
                else
                {
                    MessageBox.Show("Por favor, configura la ruta del ejecutable de VirtualDJ en Settings primero.", "Falta configuración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            idleLayout.Controls.Add(btnLaunchVdj, 0, 1);
            
            Panel container = new Panel { Dock = DockStyle.Fill };
            container.Controls.Add(idlePanel);
            container.Controls.Add(trackList);
            idlePanel.BringToFront();
            
            mainPanel.Controls.Add(container, 0, 1);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(5),
                Text = "ARKAIOS Engine Idle",
                BackColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            mainPanel.Controls.Add(lblStatus, 0, 2);

            this.Load += MainForm_Load;
            
            // Transparencia Hover
            this.MouseEnter += (s, e) => { if(AppSettings.EnableTransparency) this.Opacity = 1.0; };
            this.MouseLeave += (s, e) => { if(AppSettings.EnableTransparency) this.Opacity = 0.7; };
            // Propagar a los controles hijos
            foreach(Control c in this.Controls) {
                c.MouseEnter += (s, e) => { if(AppSettings.EnableTransparency) this.Opacity = 1.0; };
                // MouseLeave es más complejo en hijos, WinForms se basa en Bounds
            }
        }

        private void ApplySettings()
        {
            ApplyTabVisibility();
            if (AppSettings.EnableTransparency)
            {
                this.Opacity = this.ClientRectangle.Contains(this.PointToClient(Cursor.Position)) ? 1.0 : 0.7;
            }
            else
            {
                this.Opacity = 1.0;
            }
            // Recargar DB con nuevos filtros si es necesario
            Task.Run(() => LoadDatabase(AppSettings.VdjDatabaseFile));
        }

        private void ApplyTabVisibility()
        {
            if (mainTabs == null || assistantTab == null || allTracksTab == null) return;
            TabPage selected = mainTabs.SelectedTab;
            mainTabs.TabPages.Clear();
            mainTabs.TabPages.Add(assistantTab);
            mainTabs.TabPages.Add(allTracksTab);
            if (AppSettings.ShowAdvancedTabs)
            {
                mainTabs.TabPages.Add(mediaTab);
                mainTabs.TabPages.Add(hitsTab);
                mainTabs.TabPages.Add(hubTab);
                mainTabs.TabPages.Add(organizerTab);
            }
            if (selected != null && mainTabs.TabPages.Contains(selected))
                mainTabs.SelectedTab = selected;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            string dbPath = AppSettings.VdjDatabaseFile;
            vdjHistoryFolder = AppSettings.VdjHistoryFolder;

            if (File.Exists(dbPath))
            {
                lblStatus.Text = "Cargando " + dbPath;
                ApplySettings();
                
                // Cargar el historial en segundo plano
                Task.Run(() => HistoryEngine.ScanHistory(AppSettings.VdjHistoryFolder));
                
                StartHistoryMonitor();
            }
            else
            {
                lblNowPlaying.Text = "Error: database.xml no encontrado en " + dbPath;
            }
        }

        private void LoadDatabase(string xmlPath)
        {
            try
            {
                var loadedTracks = new List<Track>();
                using (FileStream fs = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (XmlReader reader = XmlReader.Create(fs))
                {
                    Track currentTrack = null;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "Song")
                        {
                            currentTrack = new Track();
                            currentTrack.FilePath = reader.GetAttribute("FilePath");
                        }
                        else if (reader.NodeType == XmlNodeType.Element && reader.Name == "Tags" && currentTrack != null)
                        {
                            currentTrack.Title = reader.GetAttribute("Title");
                            currentTrack.Artist = reader.GetAttribute("Author");
                            currentTrack.Genre = reader.GetAttribute("Genre");
                            string rawBpm = reader.GetAttribute("Bpm");
                            double bpmVal;
                            if (!string.IsNullOrEmpty(rawBpm) && double.TryParse(rawBpm, NumberStyles.Float, CultureInfo.InvariantCulture, out bpmVal))
                            {
                                if (bpmVal > 0 && bpmVal < 2) currentTrack.Bpm = Math.Round(60.0 / bpmVal, 2);
                            }
                        }
                        else if (reader.NodeType == XmlNodeType.Element && reader.Name == "Scan" && currentTrack != null)
                        {
                            string rawBpm = reader.GetAttribute("Bpm");
                            double bpmVal2;
                            if (!string.IsNullOrEmpty(rawBpm) && double.TryParse(rawBpm, NumberStyles.Float, CultureInfo.InvariantCulture, out bpmVal2))
                            {
                                if (bpmVal2 > 0 && bpmVal2 < 2) currentTrack.Bpm = Math.Round(60.0 / bpmVal2, 2);
                            }
                            currentTrack.Key = reader.GetAttribute("Key");
                            currentTrack.CamelotKey = HarmonicEngine.ConvertToCamelot(currentTrack.Key);
                        }
                        else if (currentTrack != null && reader.Name == "Song")
                        {
                            if (!string.IsNullOrEmpty(currentTrack.FilePath))
                            {
                                bool allowed = AppSettings.AllowedFolders.Count == 0;
                                if (!allowed) {
                                    foreach(var f in AppSettings.AllowedFolders) {
                                        if (currentTrack.FilePath.StartsWith(f, StringComparison.OrdinalIgnoreCase)) { allowed = true; break; }
                                    }
                                }

                                if (allowed) {
                                    currentTrack.FileName = Path.GetFileName(currentTrack.FilePath);
                                    currentTrack.FileType = Path.GetExtension(currentTrack.FilePath).ToLower();
                                    if (string.IsNullOrEmpty(currentTrack.Title)) currentTrack.Title = Path.GetFileNameWithoutExtension(currentTrack.FilePath);
                                    if (currentTrack.CamelotKey == null) currentTrack.CamelotKey = "";
                                    loadedTracks.Add(currentTrack);
                                }
                            }
                            currentTrack = null;
                        }
                    }
                }
                allTracks = loadedTracks;
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate { MessageBox.Show("Error leyendo XML: " + ex.Message); });
            }
        }

        private void StartHistoryMonitor()
        {
            historyTimer = new Timer();
            historyTimer.Interval = 2000; // Check every 2 seconds
            historyTimer.Tick += CheckHistory;
            historyTimer.Start();
            CheckHistory(null, null); // Check immediately
        }

        private void CheckHistory(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(vdjHistoryFolder)) return;

                var m3uFiles = Directory.GetFiles(vdjHistoryFolder, "*.m3u")
                                        .Select(f => new FileInfo(f))
                                        .OrderByDescending(f => f.LastWriteTime)
                                        .ToList();

                if (m3uFiles.Count == 0) return;

                // Leer el archivo más reciente (hoy)
                string latestTrackPath = "";
                using (var fs = new FileStream(m3uFiles[0].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string content = sr.ReadToEnd();
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = lines.Length - 1; i >= 0; i--)
                    {
                        string line = lines[i].Trim();
                        if (line.Length > 0 && !line.StartsWith("#"))
                        {
                            latestTrackPath = line;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(latestTrackPath) && latestTrackPath != currentPlayingFile)
                {
                    currentPlayingFile = latestTrackPath;
                    UpdateRecommendations(currentPlayingFile);
                }
            }
            catch { /* Ignorar errores de lectura si el archivo esta bloqueado */ }
        }

        private void UpdateRecommendations(string filePath)
        {
            Task.Run(() =>
            {
                var playingTrack = allTracks.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                if (playingTrack == null)
                {
                    // Fallback: buscar por el puro nombre del archivo (útil si VDJ cambió la ruta o la unidad)
                    string fileName = Path.GetFileName(filePath);
                    playingTrack = allTracks.FirstOrDefault(t => string.Equals(Path.GetFileName(t.FilePath), fileName, StringComparison.OrdinalIgnoreCase));
                }

                if (playingTrack == null)
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.Invoke(new Action(() => {
                            lblNowPlaying.Text = "No se encontró metadata en DB para:\n" + Path.GetFileName(filePath);
                        }));
                    }
                    PlateLoadReporter.ReportAsync(null, filePath);
                    return;
                }

                PlateLoadReporter.ReportAsync(playingTrack, filePath);

                var distinctTracks = allTracks
                    .Where(t => t.FilePath != playingTrack.FilePath)
                    .GroupBy(t => (t.Artist ?? "") + "|" + (t.Title ?? ""))
                    .Select(g => g.First())
                    .ToList();

                List<Track> gold = new List<Track>();
                List<Track> silver = new List<Track>();
                List<Track> bronze = new List<Track>();
                List<Track> compatible = new List<Track>();
                List<Track> sameArtist = new List<Track>();

                string currentArtist = playingTrack.Artist ?? "";

                foreach (var t in distinctTracks)
                {
                    double currentBpm = playingTrack.Bpm;
                    double targetBpm = t.Bpm;

                    bool bpmMatch = Math.Abs(currentBpm - targetBpm) <= (currentBpm * 0.04) ||
                                    Math.Abs((currentBpm * 2) - targetBpm) <= ((currentBpm * 2) * 0.04) ||
                                    Math.Abs((currentBpm / 2) - targetBpm) <= ((currentBpm / 2) * 0.04);

                    if (bpmMatch)
                    {
                        int camelotScore = HarmonicEngine.CalculateCamelotScore(playingTrack.CamelotKey, t.CamelotKey);
                        bool historyMatch = HistoryEngine.HasHistoricalTransition(playingTrack.FilePath, t.FilePath);

                        if (historyMatch) { t.RankIcon = "GOLD"; t.MatchScore = 100 + camelotScore; gold.Add(t); }
                        else if (camelotScore >= 3) { t.RankIcon = "SILVER"; t.MatchScore = camelotScore; silver.Add(t); }
                        else if (camelotScore == 2) { t.RankIcon = "BRONZE"; t.MatchScore = camelotScore; bronze.Add(t); }
                        else if (camelotScore == 1 || bpmMatch) { t.RankIcon = ""; t.MatchScore = camelotScore; compatible.Add(t); }
                    }
                    else if (!string.IsNullOrEmpty(currentArtist) && string.Equals(t.Artist, currentArtist, StringComparison.OrdinalIgnoreCase))
                    {
                        t.RankIcon = "";
                        t.MatchScore = 0;
                        sameArtist.Add(t);
                    }
                }

                gold = gold.OrderByDescending(t => t.MatchScore).Take(3).ToList();
                silver = silver.OrderByDescending(t => t.MatchScore).Take(2).ToList();
                bronze = bronze.OrderByDescending(t => t.MatchScore).Take(2).ToList();

                // Llenar compatibles
                int neededFor20 = 20 - (gold.Count + silver.Count + bronze.Count);
                compatible = compatible.OrderByDescending(t => t.MatchScore).Take(Math.Max(0, neededFor20)).ToList();

                // Mismo Artista
                int neededForArtist = 5;
                sameArtist = sameArtist.Take(neededForArtist).ToList();

                // Rellenar si faltan para 25
                int totalSoFar = gold.Count + silver.Count + bronze.Count + compatible.Count + sameArtist.Count;
                if (totalSoFar < 25) {
                    var extraCompatible = distinctTracks.Except(gold).Except(silver).Except(bronze).Except(compatible).Except(sameArtist)
                                            .Where(t => t.Bpm > 0 && Math.Abs(playingTrack.Bpm - t.Bpm) <= (playingTrack.Bpm * 0.04))
                                            .Take(25 - totalSoFar).ToList();
                    compatible.AddRange(extraCompatible);
                }

                List<Track> finalRecommendations = new List<Track>();
                finalRecommendations.AddRange(gold);
                finalRecommendations.AddRange(silver);
                finalRecommendations.AddRange(bronze);
                finalRecommendations.AddRange(compatible);
                finalRecommendations.AddRange(sameArtist);

                // Orden final estricto: 1. BPM, 2. Genero, 3. Camelot
                finalRecommendations = finalRecommendations
                    .OrderBy(t => Math.Abs(playingTrack.Bpm - t.Bpm))
                    .ThenByDescending(t => IsSameGenreFamily(playingTrack.Genre, t.Genre) ? 1 : 0)
                    .ThenByDescending(t => t.MatchScore)
                    .ToList();

                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblNowPlaying.Text = string.Format("SONANDO: {0} - {1}\nBPM: {2} | Key: {3} ({4})", playingTrack.Title, playingTrack.Artist, playingTrack.Bpm, playingTrack.CamelotKey, playingTrack.Key);

                        if (isIdle) {
                            isIdle = false;
                            idlePanel.Visible = false;
                            trackList.Visible = true;
                        }

                        trackList.Items.Clear();
                        foreach (var t in finalRecommendations.Take(25))
                        {
                            bool exists = File.Exists(t.FilePath);

                            // TODO [NUBE]: Implementar para estas canciones faltantes (☁) el servidor de descarga remoto / validador en tiempo real.
                            // TODO [NUBE]: El motor de las 5 canciones del mismo artista tambiÃ©n debe migrar a validaciÃ³n por API del servidor en lÃ­nea.
                            // TODO [NUBE]: Integrar API de Spotify/Amazon Music para inyectar "Top Tracks" mundiales como recomendaciones de descarga.

                            var item = new ListViewItem(exists ? (t.RankIcon ?? "") : "☁"); // Icono de Nube (soporte WinForms)
                            item.SubItems.Add(t.Title ?? "");
                            item.SubItems.Add(t.Artist ?? "");
                            item.SubItems.Add(t.Bpm.ToString("0.00"));
                            item.SubItems.Add(t.CamelotKey ?? "");
                            item.SubItems.Add(t.FileName ?? "");
                            string missingKind = IsVideoExtension(t.FileType) ? "video" : "music";
                            item.SubItems.Add(exists ? (t.FileType ?? "") : ("FALTA / doble clic descarga " + missingKind));
                            item.Tag = exists ? t.FilePath : ("MISSING:" + missingKind + "|" + (t.Artist ?? "") + " " + (t.Title ?? ""));

                            if (!exists) item.ForeColor = Color.Gray; // Poner todo en gris

                            trackList.Items.Add(item);
                        }
                        lblStatus.Text = string.Format("Se encontraron {0} tracks armónicamente compatibles.", finalRecommendations.Count);

                        // Iniciar búsqueda asíncrona en YouTube si hay artista
                        AppendHubTracks();
                        if (automaticOnlineSearchEnabled && !string.IsNullOrEmpty(currentArtist))
                        {
                            string searchArtist = currentArtist;
                            Task.Run(async () =>
                            {
                                var ytTracks = await YouTubeEngine.SearchArtistAsync(searchArtist);
                                if (ytTracks != null && ytTracks.Count > 0)
                                {
                                    if (this.IsHandleCreated && !this.IsDisposed)
                                    {
                                        this.Invoke(new Action(() => {
                                            // Verificar que no haya cambiado la pista actual mientras buscábamos
                                            if (lblNowPlaying.Text.Contains(searchArtist))
                                            {
                                                foreach (var yt in ytTracks)
                                                {
                                                    var item = new ListViewItem("🌐");
                                                    item.SubItems.Add(yt.Title);
                                                    item.SubItems.Add(searchArtist);
                                                    item.SubItems.Add("YT"); // BPM placeholder
                                                    item.SubItems.Add("-"); // Key placeholder
                                                    item.SubItems.Add("YouTube: " + yt.Duration);
                                                    item.SubItems.Add("yt");
                                                    item.Tag = "YOUTUBE:" + yt.Url;
                                                    item.ForeColor = Color.LightSkyBlue;
                                                    trackList.Items.Add(item);
                                                }
                                                lblStatus.Text += string.Format(" (+{0} tracks en YouTube listos para descargar)", ytTracks.Count);
                                            }
                                        }));
                                    }
                                }
                            });
                        }
                    }));
                }
            });
        }

        private static bool IsSameGenreFamily(string genre1, string genre2)
        {
            if (string.IsNullOrEmpty(genre1) || string.IsNullOrEmpty(genre2)) return false;
            if (string.Equals(genre1, genre2, StringComparison.OrdinalIgnoreCase)) return true;
            
            string g1 = genre1.ToLower();
            string g2 = genre2.ToLower();
            
            string[] latinos = { "cumbia", "reggaeton", "salsa", "merengue", "bachata", "latin", "urbano", "dembow", "regional", "banda" };
            string[] electronicos = { "house", "techno", "trance", "electro", "dance", "edm", "dubstep", "trap", "hardstyle" };
            
            bool g1Latino = false, g2Latino = false;
            foreach(var l in latinos) { if (g1.Contains(l)) g1Latino = true; if (g2.Contains(l)) g2Latino = true; }
            if (g1Latino && g2Latino) return true;
            
            bool g1Electro = false, g2Electro = false;
            foreach(var e in electronicos) { if (g1.Contains(e)) g1Electro = true; if (g2.Contains(e)) g2Electro = true; }
            if (g1Electro && g2Electro) return true;
            
            return false;
        }

        private void AddTrackToHub(string filePath)
        {
            AddTrackToHub(filePath, true);
        }

        private void AddTrackToHub(string filePath, bool focusAssistantTab)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            DownloadRegistry.Register(filePath, "", Path.GetFileNameWithoutExtension(filePath), "Descarga nueva", Path.GetExtension(filePath).ToLowerInvariant());
            if (!downloadedHubTracks.Any(path => string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase)))
                downloadedHubTracks.Add(filePath);

            if (isIdle)
            {
                isIdle = false;
                idlePanel.Visible = false;
                trackList.Visible = true;
            }

            ListViewItem item = AppendHubTrack(filePath);
            if (focusAssistantTab && mainTabs != null && mainTabs.TabPages.Count > 0)
                mainTabs.SelectedIndex = 0;
            SelectHubTrack(item);
            lblStatus.Text = "↓ Track descargado en descargas recientes. Arrástralo desde el final de la lista hacia el plato.";
        }

        private async Task DownloadMissingRecommendationAsync(ListViewItem item, string payload)
        {
            string preferredType = "music";
            string query = payload ?? "";
            int separator = query.IndexOf('|');
            if (separator >= 0)
            {
                preferredType = query.Substring(0, separator);
                query = query.Substring(separator + 1);
            }
            if (string.IsNullOrWhiteSpace(query)) return;

            string mediaType = AskMissingDownloadType(query, preferredType);
            if (string.IsNullOrWhiteSpace(mediaType)) return;

            item.Text = "...";
            item.ForeColor = Color.Orange;
            lblStatus.Text = "Buscando pista faltante en internet: " + query;

            OperationProgressDialog progress = new OperationProgressDialog("Descargando pista faltante de Auto Help + Camelot");
            progress.Show(this);
            try
            {
                progress.SetBatchStatus(1, 3, "Buscando en YouTube", query);
                List<YouTubeTrack> ytTracks = await YouTubeEngine.SearchAsync(query, mediaType, 5);
                if (ytTracks == null || ytTracks.Count == 0)
                {
                    string alternateType = mediaType == "video" ? "music" : "video";
                    if (!AskTryAlternate(mediaType, alternateType))
                    {
                        item.Text = "FALTA";
                        item.ForeColor = Color.Gray;
                        progress.SetResult("No se encontro el formato solicitado. Sigue como FALTA.");
                        lblStatus.Text = "No se encontro el formato solicitado.";
                        return;
                    }
                    mediaType = alternateType;
                    progress.SetBatchStatus(1, 3, "Buscando formato alterno en YouTube", query);
                    ytTracks = await YouTubeEngine.SearchAsync(query, mediaType, 5);
                    if (ytTracks == null || ytTracks.Count == 0)
                    {
                        item.Text = "FALTA";
                        item.ForeColor = Color.Gray;
                        progress.SetResult("No se encontro ni audio ni video.");
                        lblStatus.Text = "No se encontro ni audio ni video.";
                        return;
                    }
                }

                YouTubeTrack best = ytTracks[0];
                progress.SetBatchStatus(2, 3, "Descargando", best.Title);
                string quality = mediaType == "video" ? "1080p estable" : "MP3 320 kbps";
                string localPath = await YouTubeEngine.DownloadAsync(best.Url, mediaType, quality);
                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                {
                    string alternateType = mediaType == "video" ? "music" : "video";
                    if (!AskTryAlternate(mediaType, alternateType))
                    {
                        item.Text = "FALTA";
                        item.ForeColor = Color.Gray;
                        progress.SetResult("No se pudo descargar el formato solicitado. Sigue como FALTA.");
                        lblStatus.Text = "No se pudo descargar el formato solicitado.";
                        return;
                    }
                    mediaType = alternateType;
                    progress.SetBatchStatus(1, 3, "Buscando formato alterno en YouTube", query);
                    ytTracks = await YouTubeEngine.SearchAsync(query, mediaType, 5);
                    if (ytTracks == null || ytTracks.Count == 0)
                    {
                        item.Text = "FALTA";
                        item.ForeColor = Color.Gray;
                        progress.SetResult("No se encontro el formato alterno.");
                        lblStatus.Text = "No se encontro el formato alterno.";
                        return;
                    }
                    best = ytTracks[0];
                    progress.SetBatchStatus(2, 3, "Descargando formato alterno", best.Title);
                    quality = mediaType == "video" ? "1080p estable" : "MP3 320 kbps";
                    localPath = await YouTubeEngine.DownloadAsync(best.Url, mediaType, quality);
                    if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                    {
                        item.Text = "FALTA";
                        item.ForeColor = Color.Gray;
                        progress.SetResult("Tampoco se pudo descargar el formato alterno.");
                        lblStatus.Text = "No se pudo descargar ni audio ni video.";
                        return;
                    }
                }

                progress.SetBatchStatus(3, 3, "Registrando en Hub", Path.GetFileName(localPath));
                DownloadRegistry.Register(localPath, best.Url, best.Title, best.Uploader, mediaType);
                item.Text = "↓";
                item.ForeColor = Color.LightGreen;
                item.SubItems[5].Text = Path.GetFileName(localPath);
                item.SubItems[6].Text = Path.GetExtension(localPath).ToLowerInvariant();
                item.Tag = localPath;
                AddTrackToHub(localPath, true);
                if (downloadHub != null) downloadHub.AddDownloadedFile(localPath);
                progress.SetResult("Descargada y agregada como reciente: " + localPath);
                lblStatus.Text = "Pista faltante descargada, registrada y agregada a descargas recientes.";
            }
            finally
            {
                System.Threading.Thread.Sleep(500);
                progress.Close();
                progress.Dispose();
            }
        }

        private string AskMissingDownloadType(string query, string preferredType)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Descargar pista faltante";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(480, 175);
                dialog.BackColor = Color.FromArgb(25, 25, 25);
                dialog.ForeColor = Color.White;

                var label = new Label
                {
                    Text = "VirtualDJ tiene esta metadata, pero falta el archivo fisico:\n\n" + query + "\n\nElige que deseas buscar y descargar:",
                    Dock = DockStyle.Top,
                    Height = 100,
                    Padding = new Padding(12),
                    ForeColor = Color.White
                };
                dialog.Controls.Add(label);

                string selected = null;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 55, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                var cancel = new Button { Text = "Cancelar", Width = 100, Height = 32 };
                var video = new Button { Text = "Video MP4", Width = 115, Height = 32 };
                var audio = new Button { Text = "Audio MP3", Width = 115, Height = 32 };
                cancel.Click += (s, e) => { selected = null; dialog.DialogResult = DialogResult.Cancel; dialog.Close(); };
                video.Click += (s, e) => { selected = "video"; dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                audio.Click += (s, e) => { selected = "music"; dialog.DialogResult = DialogResult.OK; dialog.Close(); };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(video);
                buttons.Controls.Add(audio);
                dialog.Controls.Add(buttons);

                dialog.Shown += (s, e) => { if (preferredType == "video") video.Focus(); else audio.Focus(); };
                return dialog.ShowDialog(this) == DialogResult.OK ? selected : null;
            }
        }

        private bool AskTryAlternate(string failedType, string alternateType)
        {
            string failedLabel = failedType == "video" ? "video MP4" : "audio MP3";
            string alternateLabel = alternateType == "video" ? "video MP4" : "audio MP3";
            DialogResult answer = MessageBox.Show(
                "No se pudo obtener como " + failedLabel + ".\n\nDeseas intentar descargarlo como " + alternateLabel + "?",
                "Intentar formato alterno",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            return answer == DialogResult.Yes;
        }

        private static bool IsVideoExtension(string extension)
        {
            string ext = (extension ?? "").ToLowerInvariant();
            return ext == ".mp4" || ext == ".mkv" || ext == ".webm" || ext == ".avi" || ext == ".mov";
        }

        private void RefreshVirtualDjLibrary()
        {
            lblStatus.Text = "Actualizando database.xml y motor Camelot...";
            downloadHub.SetLibraryRefreshBusy(true, "Actualizando database.xml y motor Camelot...");
            Task.Run(() =>
            {
                LoadDatabase(AppSettings.VdjDatabaseFile);
                if (IsHandleCreated && !IsDisposed) Invoke(new Action(() =>
                {
                    lblStatus.Text = string.Format("Biblioteca actualizada: {0} tracks con metadatos de VirtualDJ.", allTracks.Count);
                    downloadHub.SetLibraryRefreshBusy(false, lblStatus.Text);
                }));
            });
        }

        private void AppendHubTracks()
        {
            foreach (string path in downloadedHubTracks.Where(File.Exists)) AppendHubTrack(path);
        }

        private ListViewItem AppendHubTrack(string filePath)
        {
            foreach (ListViewItem existing in trackList.Items)
                if (existing.Tag is string && string.Equals((string)existing.Tag, filePath, StringComparison.OrdinalIgnoreCase)) return existing;

            EnsureHubSeparator();
            var item = new ListViewItem("↓");
            item.SubItems.Add(Path.GetFileNameWithoutExtension(filePath));
            item.SubItems.Add("Descarga reciente");
            item.SubItems.Add("-");
            item.SubItems.Add("-");
            item.SubItems.Add(Path.GetFileName(filePath));
            item.SubItems.Add(Path.GetExtension(filePath).ToLowerInvariant());
            item.Tag = filePath;
            item.BackColor = Color.FromArgb(24, 65, 36);
            item.ForeColor = Color.LightGreen;
            trackList.Items.Add(item);
            return item;
        }

        private void EnsureHubSeparator()
        {
            foreach (ListViewItem existing in trackList.Items)
                if (existing.Tag is string && string.Equals((string)existing.Tag, "ARKAIOS_DOWNLOAD_SEPARATOR", StringComparison.OrdinalIgnoreCase)) return;

            var separator = new ListViewItem("↓");
            separator.SubItems.Add("DESCARGAS RECIENTES / HUB LOCAL");
            separator.SubItems.Add("Separador");
            separator.SubItems.Add("");
            separator.SubItems.Add("");
            separator.SubItems.Add("Arrastra las pistas de abajo al plato");
            separator.SubItems.Add("");
            separator.Tag = "ARKAIOS_DOWNLOAD_SEPARATOR";
            separator.BackColor = Color.FromArgb(12, 90, 42);
            separator.ForeColor = Color.White;
            trackList.Items.Add(separator);
        }

        private void SelectHubTrack(ListViewItem item)
        {
            if (item == null) return;
            trackList.SelectedItems.Clear();
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            trackList.Focus();
        }

        private void SortTrackListByColumn(int column)
        {
            if (trackSortColumn == column) trackSortAscending = !trackSortAscending;
            else
            {
                trackSortColumn = column;
                trackSortAscending = true;
            }

            var items = trackList.Items.Cast<ListViewItem>().ToList();
            items.Sort((a, b) =>
            {
                int value;
                if (column == 3) value = ParseListDouble(a, column).CompareTo(ParseListDouble(b, column));
                else value = string.Compare(GetListText(a, column), GetListText(b, column), StringComparison.OrdinalIgnoreCase);
                return trackSortAscending ? value : -value;
            });

            trackList.BeginUpdate();
            trackList.Items.Clear();
            trackList.Items.AddRange(items.ToArray());
            trackList.EndUpdate();
            lblStatus.Text = "Lista ordenada por " + trackList.Columns[column].Text + ".";
        }

        private static string GetListText(ListViewItem item, int column)
        {
            if (column < item.SubItems.Count) return item.SubItems[column].Text ?? "";
            return "";
        }

        private static double ParseListDouble(ListViewItem item, int column)
        {
            double parsed;
            return double.TryParse(GetListText(item, column), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private void TrackList_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var selectedItems = trackList.SelectedItems;
            if (selectedItems.Count > 0)
            {
                var fileList = new List<string>();
                for (int i = 0; i < selectedItems.Count; i++)
                {
                    string path = selectedItems[i].Tag == null ? "" : selectedItems[i].Tag.ToString();
                    if (File.Exists(path)) fileList.Add(path);
                }
                if (fileList.Count == 0) return;

                // Generar un DataObject con el arreglo de archivos (CF_HDROP = FileDrop)
                DataObject data = new DataObject(DataFormats.FileDrop, fileList.ToArray());
                
                // Iniciar la operación de arrastrar y soltar de forma nativa (Win32 compatible)
                trackList.DoDragDrop(data, DragDropEffects.Copy);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            if (!LicenseManager.IsLicensed())
            {
                var actForm = new ActivationForm();
                Application.Run(actForm);
                if (!actForm.IsActivated) return; // Exit if not activated
            }

            AppSettings.Load();
            
            if (AppSettings.IsConfigured())
            {
                Application.Run(new MainForm());
            }
            else
            {
                Application.Run(new SetupForm());
            }
        }
    }
}
