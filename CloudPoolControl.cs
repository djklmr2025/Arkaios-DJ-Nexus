using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class CloudPoolTrack
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Type { get; set; } = "Audio"; // Audio, Video, Karaoke
        public string Category { get; set; } = "Music";
        public long Size { get; set; } = 0;
        public string Ext { get; set; } = "";
        public string Folder { get; set; } = "";
        public bool IsCached { get; set; } = false;
        public string LocalPath { get; set; } = "";

        public string SizeFormatted
        {
            get
            {
                if (Size <= 0) return "--";
                double mb = Size / (1024.0 * 1024.0);
                return mb >= 1024 ? $"{(mb / 1024.0):F2} GB" : $"{mb:F1} MB";
            }
        }
    }

    public class CloudPoolControl : UserControl
    {
        public event Action<string> TrackSentToHub;

        private readonly TextBox searchBox;
        private readonly ComboBox categoryBox;
        private readonly ComboBox typeBox;
        private readonly Button btnRefresh;
        private readonly Button btnRequestTrack;
        private readonly Button btnDownloadToCache;
        private readonly Button btnOpenCache;
        private readonly ListView trackListView;
        private readonly TreeView folderTreeView;
        private string selectedFolderFilter = "__ALL__";
        private readonly Label statusLabel;
        private readonly Label countLabel;
        private readonly ProgressBar progressBar;
        private readonly Panel lockPanel;

        private readonly List<CloudPoolTrack> allTracks = new List<CloudPoolTrack>();
        private readonly string cacheFolder;
        private readonly string catalogPath;
        private readonly string pythonExe;
        private readonly string driveToolScript;

        public CloudPoolControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);

            // Directorio seguro de caché local para VirtualDJ 8
            cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArkaiosDJAssistant", "CloudPoolCache");
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }

            catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cloud_pool_catalog.json");
            driveToolScript = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "arkaios_drive_tool.py"));
            if (!File.Exists(driveToolScript))
            {
                driveToolScript = @"C:\ARKAIOS\arkaios_drive_tool.py";
            }

            // Detectar ejecutable de Python del backend
            pythonExe = @"C:\ARKAIOS\arkaios-neural-agent-main\backend\venv\Scripts\python.exe";
            if (!File.Exists(pythonExe))
            {
                pythonExe = "python.exe";
            }

            // Toolbar superior
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(8),
                WrapContents = false,
                BackColor = Color.FromArgb(28, 28, 28)
            };

            var lblSearch = new Label
            {
                Text = "🔍 Bóveda 5TB:",
                AutoSize = true,
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(0, 6, 4, 0)
            };

            searchBox = new TextBox
            {
                Width = 260,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            searchBox.TextChanged += (s, e) => ApplyFilter();

            categoryBox = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            categoryBox.Items.AddRange(new object[] { "Todas las Bóvedas", "Music (Música)", "Videos (Clips HD)", "Peticiones" });
            categoryBox.SelectedIndex = 0;
            categoryBox.SelectedIndexChanged += (s, e) => ApplyFilter();

            typeBox = new ComboBox
            {
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };
            typeBox.Items.AddRange(new object[] { "Todos", "🎵 Audio", "🎬 Video", "🎤 Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => ApplyFilter();

            btnRefresh = new Button
            {
                Text = "🔄 Actualizar",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            btnRefresh.Click += async (s, e) => await RefreshCatalogAsync();

            btnDownloadToCache = new Button
            {
                Text = "⚡ Cargar a Plato (VDJ8)",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnDownloadToCache.Click += async (s, e) => await StreamLoadSelectedTrackAsync();

            btnRequestTrack = new Button
            {
                Text = "✨ Pedir Canción/Video",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(138, 43, 226), // Violeta Premium
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnRequestTrack.Click += (s, e) => ShowRequestDialog();

            btnOpenCache = new Button
            {
                Text = "📂 Caché Local",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.LightGray
            };
            btnOpenCache.Click += (s, e) =>
            {
                if (Directory.Exists(cacheFolder))
                    Process.Start("explorer.exe", cacheFolder);
            };

            countLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(8, 6, 0, 0),
                Text = "0 temas"
            };

            toolbar.Controls.AddRange(new Control[]
            {
                lblSearch, searchBox, categoryBox, typeBox,
                btnDownloadToCache, btnRequestTrack, btnRefresh, btnOpenCache, countLabel
            });

            // ListView de Tracks
            trackListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None
            };
            trackListView.Columns.Add("Estado", 80);
            trackListView.Columns.Add("Título / Archivo", 340);
            trackListView.Columns.Add("Carpeta Nube", 160);
            trackListView.Columns.Add("Tipo", 75);
            trackListView.Columns.Add("Tamaño", 75);
            trackListView.Columns.Add("Bóveda", 85);
            trackListView.Columns.Add("ID Nube", 110);

            trackListView.ItemDrag += TrackListView_ItemDrag;
            trackListView.MouseDoubleClick += async (s, e) => await StreamLoadSelectedTrackAsync();

            // Menú contextual para clic derecho
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("⚡ Cargar a Plato de VirtualDJ 8 (Streaming rápido)", null, async (s, e) => await StreamLoadSelectedTrackAsync());
            contextMenu.Items.Add("⬇️ Precargar a Caché Local (Solo Lectura)", null, async (s, e) => await StreamLoadSelectedTrackAsync());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("✨ Pedir nueva versión / Remezcla", null, (s, e) => ShowRequestDialog());
            contextMenu.Items.Add("📋 Copiar ID de Archivo", null, (s, e) => CopySelectedId());
            contextMenu.Items.Add("📂 Abrir Carpeta Caché", null, (s, e) => Process.Start("explorer.exe", cacheFolder));
            trackListView.ContextMenuStrip = contextMenu;

            // SplitContainer para Carpetas Nube (Izquierda) y Lista de Canciones (Derecha)
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 250,
                SplitterWidth = 5,
                BackColor = Color.FromArgb(38, 38, 42)
            };

            var leftFolderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 28)
            };

            var folderHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(32, 32, 38)
            };

            var lblFolderHeader = new Label
            {
                Text = "📁 CARPETAS NUBE (5TB)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            folderHeader.Controls.Add(lblFolderHeader);

            folderTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                ShowLines = true,
                ShowPlusMinus = true,
                FullRowSelect = true,
                HideSelection = false
            };
            folderTreeView.AfterSelect += (s, e) =>
            {
                if (e.Node?.Tag != null)
                {
                    selectedFolderFilter = e.Node.Tag.ToString();
                    // Al seleccionar una carpeta específica para ver su pureza, limpiamos el buscador
                    if (!string.IsNullOrEmpty(searchBox.Text))
                    {
                        searchBox.Text = "";
                    }
                    else
                    {
                        ApplyFilter();
                    }
                }
            };

            leftFolderPanel.Controls.Add(folderTreeView);
            leftFolderPanel.Controls.Add(folderHeader);

            splitContainer.Panel1.Controls.Add(leftFolderPanel);
            splitContainer.Panel2.Controls.Add(trackListView);

            // Panel inferior de estado
            var bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                RowCount = 1,
                ColumnCount = 2,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));

            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Bóveda ARKAIOS Cloud 5TB Conectada (Google Drive). Solo Lectura Protegido.",
                Padding = new Padding(8, 0, 0, 0)
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            bottomPanel.Controls.Add(statusLabel, 0, 0);
            bottomPanel.Controls.Add(progressBar, 1, 0);

            // Panel de Bloqueo por Membresía (se oculta si LicenseManager.IsLicensed())
            lockPanel = CreateMembershipLockPanel();

            // Insignia del Creador
            lblOwnerBadge = new Label
            {
                Text = "👑 MODO CREADOR (DJ KLMR)",
                AutoSize = true,
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(8, 6, 8, 0),
                Visible = false
            };
            toolbar.Controls.Add(lblOwnerBadge);

            this.Controls.Add(splitContainer);
            this.Controls.Add(toolbar);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(lockPanel);

            // Validar acceso
            CheckMembershipStatus();

            // Cargar catálogo inicial
            LoadInitialCatalog();
        }

        private Label lblOwnerBadge;

        public void CheckMembershipStatus()
        {
            bool isOwner = LicenseManager.IsOwner();
            bool hasAccess = LicenseManager.HasPoolAccess();

            if (isOwner)
            {
                lockPanel.Visible = false;
                if (lblOwnerBadge != null) lblOwnerBadge.Visible = true;
                statusLabel.Text = "👑 MODO CREADOR ACTIVO (DJ KLMR) | Bóveda 5TB ARKAIOS Desbloqueada de por vida.";
                statusLabel.ForeColor = Color.Gold;
            }
            else if (hasAccess)
            {
                lockPanel.Visible = false;
                if (lblOwnerBadge != null) lblOwnerBadge.Visible = false;
                statusLabel.Text = "💎 Membresía Nube ARKAIOS Pool Activa. Bóveda 5TB Conectada (Google Drive).";
                statusLabel.ForeColor = Color.LightGreen;
            }
            else
            {
                lockPanel.Visible = true;
                lockPanel.BringToFront();
                if (lblOwnerBadge != null) lblOwnerBadge.Visible = false;
                statusLabel.Text = "🔒 Acceso Bloqueado: Se requiere suscripción activa a la Nube ARKAIOS Pool.";
                statusLabel.ForeColor = Color.OrangeRed;
            }
        }

        private Panel CreateMembershipLockPanel()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 15, 15, 20),
                Visible = false
            };

            var lblTitle = new Label
            {
                Text = "👑 ACCESO EXCLUSIVO: NUBE ARKAIOS POOL 5TB",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.Gold,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblDesc = new Label
            {
                Text = "Este módulo contiene más de 5 Terabytes de Videoclips Musicales, Música para DJs y Karaokes\n" +
                       "organizados por carpetas tal cual aparecen en la nube, con streaming directo a Virtual DJ 8.\n\n" +
                       "💰 Suscripción Oficial:\n" +
                       "Membresía Anual Nube ARKAIOS Pool 5TB: $200.00 AMR / Año (1 AMR = $1.00 MXN)\n" +
                       "Acceso ilimitado por 365 días a todo el catálogo en alta definición.",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnSubscribe = new Button
            {
                Text = "💳 Pagar Membresía Anual ($200 AMR) en Pasarela",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(380, 52),
                Cursor = Cursors.Hand
            };
            btnSubscribe.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "http://localhost:3000",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MessageBox.Show("Abre tu navegador e ingresa a http://localhost:3000 para pagar tu membresía de $200 AMR.", "Pasarela de Pago AMR");
                }
            };

            var btnCheckAgain = new Button
            {
                Text = "🔄 Ya pagué / Validar Suscripción",
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(240, 36)
            };
            btnCheckAgain.Click += (s, e) => CheckMembershipStatus();

            pnl.Resize += (s, e) =>
            {
                lblTitle.Location = new Point((pnl.Width - lblTitle.Width) / 2, Math.Max(20, pnl.Height / 5));
                lblDesc.Location = new Point((pnl.Width - lblDesc.Width) / 2, lblTitle.Bottom + 20);
                btnSubscribe.Location = new Point((pnl.Width - btnSubscribe.Width) / 2, lblDesc.Bottom + 25);
                btnCheckAgain.Location = new Point((pnl.Width - btnCheckAgain.Width) / 2, btnSubscribe.Bottom + 15);
            };

            pnl.Controls.AddRange(new Control[] { lblTitle, lblDesc, btnSubscribe, btnCheckAgain });
            return pnl;
        }

        private void LoadInitialCatalog()
        {
            try
            {
                if (File.Exists(catalogPath))
                {
                    string json = File.ReadAllText(catalogPath);
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("tracks", out var tracksElem))
                        {
                            allTracks.Clear();
                            foreach (var item in tracksElem.EnumerateArray())
                            {
                                string name = item.GetProperty("name").GetString() ?? "";
                                string id = item.GetProperty("id").GetString() ?? "";
                                string type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "Audio" : "Audio";
                                string cat = item.TryGetProperty("category", out var c) ? c.GetString() ?? "Music" : "Music";
                                long size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                                string ext = item.TryGetProperty("ext", out var x) ? x.GetString() ?? "" : "";
                                string folder = item.TryGetProperty("folder", out var f) ? f.GetString() ?? "" : "";

                                string cachedFile = Path.Combine(cacheFolder, $"{id}_{SanitizeFilename(name)}");
                                bool isCached = File.Exists(cachedFile);

                                allTracks.Add(new CloudPoolTrack
                                {
                                    Id = id,
                                    Name = name,
                                    Type = type,
                                    Category = cat,
                                    Size = size,
                                    Ext = ext,
                                    Folder = folder,
                                    IsCached = isCached,
                                    LocalPath = isCached ? cachedFile : ""
                                });
                            }
                        }
                    }
                    PopulateFolderTree();
                    ApplyFilter();
                }
                else
                {
                    // Lanzar generación de catálogo en background si no existe
                    Task.Run(() => RefreshCatalogAsync());
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Aviso: " + ex.Message;
            }
        }

        private void PopulateFolderTree()
        {
            if (folderTreeView == null) return;

            folderTreeView.BeginUpdate();
            folderTreeView.Nodes.Clear();

            // 1. Nodo Raíz "Ver Todo"
            var allNode = new TreeNode($"🌐 Todas las Carpetas ({allTracks.Count})")
            {
                Tag = "__ALL__",
                ForeColor = Color.Cyan
            };
            folderTreeView.Nodes.Add(allNode);

            // 2. Módulo Música
            var musicTracks = allTracks.Where(t => t.Category == "Music").ToList();
            var musicNode = new TreeNode($"🎵 Música ({musicTracks.Count})")
            {
                Tag = "CAT_Music",
                ForeColor = Color.LightSkyBlue
            };
            var musicFolders = musicTracks.GroupBy(t => string.IsNullOrEmpty(t.Folder) ? "General" : t.Folder)
                                          .OrderBy(g => g.Key);
            foreach (var g in musicFolders)
            {
                var fn = new TreeNode($"📁 {g.Key} ({g.Count()})")
                {
                    Tag = g.Key,
                    ForeColor = Color.Gainsboro
                };
                musicNode.Nodes.Add(fn);
            }
            folderTreeView.Nodes.Add(musicNode);

            // 3. Módulo Videos HD
            var videoTracks = allTracks.Where(t => t.Category == "Videos").ToList();
            var videoNode = new TreeNode($"🎬 Videos HD ({videoTracks.Count})")
            {
                Tag = "CAT_Videos",
                ForeColor = Color.LightCoral
            };
            var videoFolders = videoTracks.GroupBy(t => string.IsNullOrEmpty(t.Folder) ? "General" : t.Folder)
                                          .OrderBy(g => g.Key);
            foreach (var g in videoFolders)
            {
                var fn = new TreeNode($"📁 {g.Key} ({g.Count()})")
                {
                    Tag = g.Key,
                    ForeColor = Color.Gainsboro
                };
                videoNode.Nodes.Add(fn);
            }
            folderTreeView.Nodes.Add(videoNode);

            // 4. Módulo Karaokes
            var karaokeTracks = allTracks.Where(t => t.Type == "Karaoke").ToList();
            if (karaokeTracks.Count > 0)
            {
                var karaokeNode = new TreeNode($"🎤 Karaokes ({karaokeTracks.Count})")
                {
                    Tag = "TYPE_Karaoke",
                    ForeColor = Color.Yellow
                };
                var karaokeFolders = karaokeTracks.GroupBy(t => string.IsNullOrEmpty(t.Folder) ? "General" : t.Folder)
                                                  .OrderBy(g => g.Key);
                foreach (var g in karaokeFolders)
                {
                    var fn = new TreeNode($"📁 {g.Key} ({g.Count()})")
                    {
                        Tag = g.Key,
                        ForeColor = Color.LightYellow
                    };
                    karaokeNode.Nodes.Add(fn);
                }
                folderTreeView.Nodes.Add(karaokeNode);
            }

            folderTreeView.SelectedNode = allNode;
            allNode.Expand();
            musicNode.Expand();
            videoNode.Expand();

            folderTreeView.EndUpdate();
        }

        private void ApplyFilter()
        {
            string query = searchBox.Text.Trim().ToLowerInvariant();
            string selectedCat = categoryBox.SelectedItem?.ToString() ?? "Todas las Bóvedas";
            string selectedType = typeBox.SelectedItem?.ToString() ?? "Todos";

            trackListView.BeginUpdate();
            trackListView.Items.Clear();

            bool isGlobalSweep = !string.IsNullOrEmpty(query);

            var filtered = allTracks.Where(t =>
            {
                // REGLA CLAVE:
                // Si el usuario escribe texto de búsqueda -> BARRIDO GLOBAL FUERA DE LAS CARPETAS
                if (isGlobalSweep)
                {
                    bool matchName = t.Name.ToLowerInvariant().Contains(query);
                    bool matchFolder = t.Folder != null && t.Folder.ToLowerInvariant().Contains(query);
                    if (!matchName && !matchFolder) return false;
                }
                else
                {
                    // PUREZA DE CARPETA: Filtrar estrictamente por la carpeta seleccionada en el árbol
                    if (selectedFolderFilter != "__ALL__")
                    {
                        if (selectedFolderFilter == "CAT_Music")
                        {
                            if (t.Category != "Music") return false;
                        }
                        else if (selectedFolderFilter == "CAT_Videos")
                        {
                            if (t.Category != "Videos") return false;
                        }
                        else if (selectedFolderFilter == "TYPE_Karaoke")
                        {
                            if (t.Type != "Karaoke") return false;
                        }
                        else
                        {
                            if (t.Folder != selectedFolderFilter) return false;
                        }
                    }
                }

                // Filtro rápido de categoría del toolbar
                if (selectedCat.StartsWith("Music") && t.Category != "Music") return false;
                if (selectedCat.StartsWith("Videos") && t.Category != "Videos") return false;
                if (selectedCat.StartsWith("Peticiones") && t.Category != "Peticiones") return false;

                // Filtro rápido de módulos/tipos del toolbar (Música, Videos, Karaokes)
                if (selectedType.Contains("Audio") && t.Type != "Audio") return false;
                if (selectedType.Contains("Video") && t.Type != "Video") return false;
                if (selectedType.Contains("Karaoke") && t.Type != "Karaoke") return false;

                return true;
            }).ToList();

            foreach (var track in filtered)
            {
                string statusText = track.IsCached ? "⚡ Listo" : "☁️ Nube";
                var lvi = new ListViewItem(statusText)
                {
                    Tag = track,
                    ForeColor = track.IsCached ? Color.LightGreen : Color.White
                };
                lvi.SubItems.Add(track.Name);
                lvi.SubItems.Add(string.IsNullOrEmpty(track.Folder) ? track.Category : track.Folder);
                lvi.SubItems.Add(track.Type);
                lvi.SubItems.Add(track.SizeFormatted);
                lvi.SubItems.Add(track.Category);
                lvi.SubItems.Add(track.Id);
                trackListView.Items.Add(lvi);
            }

            trackListView.EndUpdate();
            countLabel.Text = $"{filtered.Count} archivos";

            if (isGlobalSweep)
            {
                statusLabel.Text = $"🔍 BARRIDO GLOBAL 5TB: {filtered.Count} archivos encontrados para '{query}' (en todas las carpetas).";
                statusLabel.ForeColor = Color.Cyan;
            }
            else if (selectedFolderFilter != "__ALL__")
            {
                statusLabel.Text = $"📁 PUREZA DE CARPETA: {selectedFolderFilter} ({filtered.Count} archivos en la nube).";
                statusLabel.ForeColor = Color.LightGreen;
            }
            else
            {
                statusLabel.Text = $"Bóveda ARKAIOS Cloud 5TB Conectada (Google Drive). {allTracks.Count} pistas disponibles en total.";
                statusLabel.ForeColor = Color.Silver;
            }
        }

        private void TrackListView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (trackListView.SelectedItems.Count == 0) return;
            var lvi = trackListView.SelectedItems[0];
            if (lvi.Tag is CloudPoolTrack track)
            {
                if (track.IsCached && File.Exists(track.LocalPath))
                {
                    // Drag directo del archivo listo a los platos de Virtual DJ 8
                    string[] paths = new string[] { track.LocalPath };
                    trackListView.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
                }
                else
                {
                    statusLabel.Text = $"Descargando '{track.Name}' para cargar a Virtual DJ...";
                    // Si aún no está en caché, iniciamos streaming y le avisamos al DJ
                    Task.Run(async () =>
                    {
                        bool ok = await EnsureTrackInCacheAsync(track);
                        if (ok)
                        {
                            Invoke(new Action(() =>
                            {
                                statusLabel.Text = $"✅ Listo en caché. Arrástralo o haz doble clic para cargar en el plato.";
                                ApplyFilter();
                            }));
                        }
                    });
                }
            }
        }

        private async Task StreamLoadSelectedTrackAsync()
        {
            if (trackListView.SelectedItems.Count == 0) return;
            var lvi = trackListView.SelectedItems[0];
            if (lvi.Tag is CloudPoolTrack track)
            {
                statusLabel.Text = $"⚡ Cargando '{track.Name}' desde la nube ARKAIOS...";
                progressBar.Visible = true;
                btnDownloadToCache.Enabled = false;

                bool ok = await EnsureTrackInCacheAsync(track);
                progressBar.Visible = false;
                btnDownloadToCache.Enabled = true;

                if (ok && File.Exists(track.LocalPath))
                {
                    statusLabel.Text = $"✅ '{track.Name}' listo para VirtualDJ 8. (Copiado al portapapeles)";
                    Clipboard.SetText(track.LocalPath);

                    // Notificar al Hub
                    TrackSentToHub?.Invoke(track.LocalPath);

                    // Refrescar lista para mostrar "⚡ Listo"
                    ApplyFilter();
                }
                else
                {
                    statusLabel.Text = $"❌ No se pudo descargar el archivo de la nube.";
                }
            }
        }

        private async Task<bool> EnsureTrackInCacheAsync(CloudPoolTrack track)
        {
            string safeName = SanitizeFilename(track.Name);
            string targetPath = Path.Combine(cacheFolder, $"{track.Id}_{safeName}");

            if (File.Exists(targetPath))
            {
                track.IsCached = true;
                track.LocalPath = targetPath;
                return true;
            }

            // Ejecutar streaming vía arkaios_drive_tool.py download
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = $"\"{driveToolScript}\" download \"{track.Id}\" \"{targetPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc == null) return false;
                        proc.WaitForExit();
                        if (proc.ExitCode == 0 && File.Exists(targetPath))
                        {
                            // Protección de Solo Lectura
                            try
                            {
                                File.SetAttributes(targetPath, FileAttributes.ReadOnly);
                            }
                            catch { }

                            track.IsCached = true;
                            track.LocalPath = targetPath;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error descargando de Drive: " + ex.Message);
                }
                return false;
            });
        }

        private async Task RefreshCatalogAsync()
        {
            statusLabel.Text = "🔄 Conectando con Google Drive 5TB y actualizando catálogo...";
            progressBar.Visible = true;
            btnRefresh.Enabled = false;

            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = $"\"{driveToolScript}\" build-catalog",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            proc.WaitForExit();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error actualizando catalogo: " + ex.Message);
                }
            });

            progressBar.Visible = false;
            btnRefresh.Enabled = true;
            statusLabel.Text = "✅ Catálogo sincronizado con la Bóveda 5TB.";
            LoadInitialCatalog();
        }

        private void ShowRequestDialog()
        {
            var dialog = new Form
            {
                Text = "✨ Petición Autónoma de Canción / Video / Karaoke",
                Size = new Size(520, 290),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = Color.White
            };

            var lblPrompt = new Label
            {
                Text = "Escribe el nombre del tema / artista o pega un enlace (YouTube/Spotify):\nEl demonio autónomo lo buscará, descargará en máxima calidad y lo subirá a la Bóveda.",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f)
            };

            var txtInput = new TextBox
            {
                Location = new Point(20, 65),
                Width = 460,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            var lblType = new Label
            {
                Text = "Formato deseado:",
                Location = new Point(20, 110),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            var cmbReqType = new ComboBox
            {
                Location = new Point(150, 107),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            cmbReqType.Items.AddRange(new object[] { "🎵 Audio MP3 (320 kbps)", "🎬 Video Musical MP4 HD", "🎤 Karaoke (Vocal Off / Letra)" });
            cmbReqType.SelectedIndex = 0;

            var btnSubmit = new Button
            {
                Text = "🚀 Enviar Petición a la Nube",
                Location = new Point(140, 170),
                Size = new Size(220, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(138, 43, 226),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btnSubmit.Click += async (s, e) =>
            {
                string reqText = txtInput.Text.Trim();
                if (string.IsNullOrEmpty(reqText))
                {
                    MessageBox.Show("Por favor ingresa un título o enlace.", "Campo requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string reqType = cmbReqType.SelectedItem?.ToString() ?? "Audio";
                btnSubmit.Enabled = false;
                btnSubmit.Text = "⏳ Procesando Petición...";

                statusLabel.Text = $"🤖 Demonio ARKAIOS procesando petición: '{reqText}'...";
                dialog.Close();

                // Procesar petición en segundo plano con yt-dlp y subir a la carpeta Peticiones de Drive
                await Task.Run(() => ProcessUserRequestAutonomous(reqText, reqType));
            };

            dialog.Controls.AddRange(new Control[] { lblPrompt, txtInput, lblType, cmbReqType, btnSubmit });
            dialog.ShowDialog(this);
        }

        private void ProcessUserRequestAutonomous(string queryOrUrl, string formatType)
        {
            try
            {
                string ytdlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
                if (!File.Exists(ytdlpPath)) ytdlpPath = @"C:\ARKAIOS\DJ_Assistant\yt-dlp.exe";

                string tempDir = Path.Combine(Path.GetTempPath(), "ArkaiosDJRequests");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                bool isAudio = formatType.Contains("Audio");
                bool isKaraoke = formatType.Contains("Karaoke");

                string args = "";
                string outputTemplate = Path.Combine(tempDir, "%(title)s [%(id)s].%(ext)s");

                if (isAudio || isKaraoke)
                {
                    args = $"-x --audio-format mp3 --audio-quality 0 --no-playlist -o \"{outputTemplate}\" ";
                }
                else
                {
                    args = $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" --no-playlist -o \"{outputTemplate}\" ";
                }

                if (queryOrUrl.StartsWith("http://") || queryOrUrl.StartsWith("https://"))
                {
                    args += $"\"{queryOrUrl}\"";
                }
                else
                {
                    args += $"\"ytsearch1:{queryOrUrl}\"";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = ytdlpPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit();
                    }
                }

                // Buscar el archivo descargado
                var downloadedFiles = Directory.GetFiles(tempDir).OrderByDescending(File.GetLastWriteTime).ToList();
                if (downloadedFiles.Count > 0)
                {
                    string downloadedFile = downloadedFiles[0];

                    // Obtener folder ID de Peticiones y subirlo
                    string requestsFolderId = "11xlc155cjh3OmgetwDWbDW-wzR5cxMNM";

                    var uploadPsi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        Arguments = $"\"{driveToolScript}\" upload \"{downloadedFile}\" \"{requestsFolderId}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var upProc = Process.Start(uploadPsi))
                    {
                        if (upProc != null) upProc.WaitForExit();
                    }

                    // Limpieza y refresco
                    Invoke(new Action(async () =>
                    {
                        statusLabel.Text = $"🎉 ¡Petición '{Path.GetFileName(downloadedFile)}' subida exitosamente a la Bóveda de 5TB!";
                        await RefreshCatalogAsync();
                    }));
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    statusLabel.Text = "Error en petición: " + ex.Message;
                }));
            }
        }

        private void CopySelectedId()
        {
            if (trackListView.SelectedItems.Count > 0)
            {
                var track = trackListView.SelectedItems[0].Tag as CloudPoolTrack;
                if (track != null)
                {
                    Clipboard.SetText(track.Id);
                    statusLabel.Text = $"ID de Google Drive copiado: {track.Id}";
                }
            }
        }

        private static string SanitizeFilename(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
