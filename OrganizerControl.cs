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
    public class OrganizerControl : UserControl
    {
        private readonly TextBox searchBox;
        private readonly ComboBox typeBox;
        private readonly DataGridView grid;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;
        private readonly FlowLayoutPanel toolbar;
        private readonly PreviewPlayerBar previewBar;
        private readonly ContextMenuStrip gridMenu;
        private readonly List<OrganizerItem> items = new List<OrganizerItem>();
        private List<OrganizerItem> visibleItems = new List<OrganizerItem>();
        private string sortColumn = "Status";
        private bool sortAscending = false;
        private readonly string[] allowedExtensions = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".mp4", ".mkv", ".webm", ".avi", ".mov" };

        public OrganizerControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(8), WrapContents = true };
            searchBox = new TextBox { Width = 300 };
            searchBox.TextChanged += (s, e) => ApplyFilter();
            typeBox = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "Todos", "Music", "Video", "Karaoke" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => ApplyFilter();

            var analyzeButton = new Button { Text = "Analizar biblioteca", AutoSize = true };
            analyzeButton.Click += async (s, e) => await AnalyzeAsync();
            var markButton = new Button { Text = "Marcar seguros", AutoSize = true };
            markButton.Click += (s, e) => MarkSafeItems();
            var applyButton = new Button { Text = "Renombrar", AutoSize = true };
            applyButton.Click += async (s, e) => await RenameSelectedWithMetadataAsync(false);
            var safeButton = new Button { Text = "Renombrar seguros", AutoSize = true };
            safeButton.Click += async (s, e) => await RenameSelectedWithMetadataAsync(true);
            var undoButton = new Button { Text = "Deshacer ultimo lote", AutoSize = true };
            undoButton.Click += async (s, e) => await UndoLastBatchAsync();
            var manualButton = new Button { Text = "Renombrar manual", AutoSize = true };
            manualButton.Click += (s, e) => RenameManualSelected();
            var previewButton = new Button { Text = "Preview Track", AutoSize = true };
            previewButton.Click += (s, e) => PreviewSelected();
            var folderButton = new Button { Text = "Abrir carpeta", AutoSize = true };
            folderButton.Click += (s, e) => OpenSelectedFolder();
            toolbar.Controls.AddRange(new Control[] { searchBox, typeBox, analyzeButton, markButton, applyButton, safeButton, undoButton, manualButton, previewButton, folderButton });

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
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Renombrar", DataPropertyName = "Selected", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nombre actual", DataPropertyName = "CurrentName", ReadOnly = true, Width = 260 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nombre sugerido", DataPropertyName = "SuggestedName", ReadOnly = true, Width = 300 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Confianza", DataPropertyName = "ConfidenceText", ReadOnly = true, Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estado", DataPropertyName = "Status", ReadOnly = true, Width = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tipo", DataPropertyName = "Type", ReadOnly = true, Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Carpeta", DataPropertyName = "Folder", ReadOnly = true, Width = 280 });
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.Programmatic;
            grid.ColumnHeaderMouseClick += (s, e) => SortByColumn(grid.Columns[e.ColumnIndex].DataPropertyName);
            grid.MouseDoubleClick += (s, e) => PreviewSelected();
            grid.CellMouseDown += Grid_CellMouseDown;

            gridMenu = new ContextMenuStrip();
            gridMenu.Items.Add("Reproducir audio", null, (s, e) => PreviewSelected());
            gridMenu.Items.Add("Pausar", null, (s, e) => PausePreview());
            gridMenu.Items.Add("Continuar", null, (s, e) => ResumePreview());
            gridMenu.Items.Add("Detener", null, (s, e) => StopPreview());
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add("Renombrar automatico", null, async (s, e) => await RenameSelectedWithMetadataAsync(false));
            gridMenu.Items.Add("Renombrar manual", null, (s, e) => RenameManualSelected());
            gridMenu.Items.Add("Deshacer ultimo lote", null, async (s, e) => await UndoLastBatchAsync());
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add("Crear playlist VDJ", null, (s, e) => CreateVdjPlaylistFromSelection());
            gridMenu.Items.Add("Abrir carpeta", null, (s, e) => OpenSelectedFolder());
            grid.ContextMenuStrip = gridMenu;

            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8), ForeColor = Color.LightGray };
            progressBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
            previewBar = new PreviewPlayerBar();

            Controls.Add(grid);
            Controls.Add(previewBar);
            Controls.Add(progressBar);
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            Load += async (s, e) => await AnalyzeAsync();
        }

        private async Task AnalyzeAsync()
        {
            SetBusy(true, "Analizando nombres locales sin modificar archivos...");
            try
            {
                List<OrganizerItem> found = await Task.Run(() =>
                {
                    var scanned = new List<OrganizerItem>();
                    scanned.AddRange(ScanFolder("Music", AppSettings.GetDownloadFolder("music")));
                    scanned.AddRange(ScanFolder("Video", AppSettings.GetDownloadFolder("video")));
                    scanned.AddRange(ScanFolder("Karaoke", AppSettings.GetDownloadFolder("karaoke")));
                    return scanned;
                });
                items.Clear();
                items.AddRange(found);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "No se pudo analizar la biblioteca: " + ex.Message;
            }
            finally { SetBusy(false, null); }
        }

        private IEnumerable<OrganizerItem> ScanFolder(string type, string folder)
        {
            if (!Directory.Exists(folder)) yield break;
            IEnumerable<string> paths;
            try { paths = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories); }
            catch { yield break; }

            foreach (string path in paths)
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension)) continue;

                OrganizerItem item = AnalyzeFile(path, type);
                yield return item;
            }
        }

        private static OrganizerItem AnalyzeFile(string path, string type)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string cleaned = CleanTitle(name);
            int confidence = EstimateConfidence(name, cleaned);
            bool same = string.Equals(name, cleaned, StringComparison.OrdinalIgnoreCase);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            string status = same ? "Sin cambio" : confidence >= 85 ? "Seguro" : confidence >= 70 ? "Revisar" : "No tocar";

            return new OrganizerItem
            {
                Path = path,
                Type = type,
                CurrentName = name,
                SuggestedName = cleaned,
                Confidence = same ? 100 : confidence,
                Status = status,
                Folder = Path.GetDirectoryName(path),
                Extension = extension,
                Selected = false
            };
        }

        private static string CleanTitle(string value)
        {
            string text = value ?? "";
            text = text.Replace('_', ' ');
            text = Regex.Replace(text, @"\s+", " ").Trim();
            text = Regex.Replace(text, @"^\s*\(?\d{1,4}\)?\s*[-_. ]+", "");
            text = Regex.Replace(text, @"\[[A-Za-z0-9_-]{8,15}\]$", "").Trim();
            text = Regex.Replace(text, @"\s*\((official\s+audio|official\s+video|official\s+music\s+video|lyrics?|visualizer|hd|hq|320kbps|mp3)\)\s*", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s*\[(official\s+audio|official\s+video|official\s+music\s+video|lyrics?|visualizer|hd|hq|320kbps|mp3)\]\s*", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '.');
            return ToTitleCasePreserveSeparators(text);
        }

        private static string ToTitleCasePreserveSeparators(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string[] words = value.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length == 0 || word.ToUpperInvariant() == word) continue;
                words[i] = char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word.Substring(1) : "");
            }
            return string.Join(" ", words);
        }

        private static int EstimateConfidence(string original, string cleaned)
        {
            if (string.IsNullOrWhiteSpace(cleaned)) return 0;
            if (!cleaned.Contains("-")) return 65;
            int score = 80;
            if (Regex.IsMatch(original, @"\[[A-Za-z0-9_-]{8,15}\]$")) score += 8;
            if (Regex.IsMatch(original, @"^\s*\(?\d{1,4}\)?\s*[-_. ]+")) score += 5;
            if (Regex.IsMatch(original, @"official|lyrics?|visualizer|320kbps|mp3", RegexOptions.IgnoreCase)) score += 5;
            if (cleaned.Length < 6) score -= 25;
            return Math.Max(0, Math.Min(98, score));
        }

        private void MarkSafeItems()
        {
            foreach (OrganizerItem item in items)
                item.Selected = item.Status == "Seguro" && !TargetExists(item);
            ApplyFilter();
        }

        private async Task RenameSelectedWithMetadataAsync(bool safeOnly)
        {
            grid.EndEdit();
            List<OrganizerItem> selected = GetRequestedRenameItems(safeOnly);
            if (selected.Count == 0)
            {
                statusLabel.Text = safeOnly ? "No hay tracks seguros marcados." : "Selecciona filas o marca checkboxes para renombrar.";
                return;
            }

            DialogResult answer = MessageBox.Show(
                "Se validaran metadatos reales y despues se renombraran " + selected.Count + " archivos. Se guardara historial para deshacer el lote.\n\nContinuar?",
                "Confirmar renombrado con metadata", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            SetBusy(true, "Validando metadata y renombrando lote seleccionado...");
            OperationProgressDialog progress = new OperationProgressDialog("Renombrando con metadata real");
            progress.Show(this);
            try
            {
                int changed = await RenameBatchWithMetadataAsync(selected, progress);
                await AnalyzeAsync();
                statusLabel.Text = "Renombrado terminado: " + changed + " archivos modificados. Los no confiables quedaron sin tocar.";
            }
            finally
            {
                progress.Close();
                progress.Dispose();
                SetBusy(false, null);
            }
        }

        private List<OrganizerItem> GetRequestedRenameItems(bool safeOnly)
        {
            var requested = new List<OrganizerItem>();
            foreach (OrganizerItem item in items)
                if (item.Selected) requested.Add(item);

            if (requested.Count == 0 && grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    OrganizerItem item = row.DataBoundItem as OrganizerItem;
                    if (item != null && !requested.Contains(item)) requested.Add(item);
                }
            }

            return requested
                .Where(i => (!safeOnly || i.Status == "Seguro") && i.Status != "Sin cambio" && i.Status != "No tocar")
                .ToList();
        }

        private async Task<int> RenameBatchWithMetadataAsync(List<OrganizerItem> selected, OperationProgressDialog progress)
        {
            string historyFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rename-history");
            Directory.CreateDirectory(historyFolder);
            string historyPath = Path.Combine(historyFolder, "rename-history-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".tsv");
            var log = new List<string> { "old\tnew" };
            int changed = 0;
            int index = 0;

            foreach (OrganizerItem item in selected)
            {
                index++;
                progress.SetBatchStatus(index, selected.Count, "Validando metadata", item.CurrentName);
                if (!File.Exists(item.Path)) continue;

                string validatedName = await ValidateRealMetadataNameAsync(item);
                if (string.IsNullOrWhiteSpace(validatedName))
                {
                    if (item.Status == "Seguro" && !string.IsNullOrWhiteSpace(item.SuggestedName))
                        validatedName = item.SuggestedName;
                    else
                    {
                        item.Status = "No tocar";
                        continue;
                    }
                }

                item.SuggestedName = validatedName;
                progress.SetBatchStatus(index, selected.Count, "Renombrando archivo", item.SuggestedName);

                string target = Path.Combine(Path.GetDirectoryName(item.Path), item.SuggestedName + item.Extension);
                if (string.Equals(item.Path, target, StringComparison.OrdinalIgnoreCase) || File.Exists(target)) continue;
                try
                {
                    File.Move(item.Path, target);
                    log.Add(item.Path + "\t" + target);
                    changed++;
                }
                catch { }
            }

            if (changed > 0) File.WriteAllLines(historyPath, log.ToArray(), Encoding.UTF8);
            progress.SetResult("Resultado: " + changed + " archivos renombrados. Historial: " + Path.GetFileName(historyPath));
            return changed;
        }

        private async Task<string> ValidateRealMetadataNameAsync(OrganizerItem item)
        {
            string baseQuery = item.CurrentName;
            if (!string.IsNullOrWhiteSpace(item.SuggestedName))
                baseQuery = item.SuggestedName;

            string mediaType = string.Equals(item.Type, "Video", StringComparison.OrdinalIgnoreCase) ? "video" :
                               string.Equals(item.Type, "Karaoke", StringComparison.OrdinalIgnoreCase) ? "karaoke" : "music";
            MetadataNameSuggestion resolved = await MetadataNameResolver.ResolveAsync(item.Path, baseQuery, mediaType);
            string suggestion = resolved == null ? null : resolved.Name;
            if (string.IsNullOrWhiteSpace(suggestion)) return null;

            string cleaned = CleanTitle(suggestion);
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 6) return null;
            int sourceConfidence = resolved == null ? 0 : resolved.Confidence;
            if (Math.Max(sourceConfidence, EstimateConfidence(item.CurrentName, cleaned)) < 70 && !cleaned.Contains("-")) return null;

            string safeName = SanitizeFileName(cleaned);
            return safeName;
        }

        private static string SanitizeFileName(string value)
        {
            string safe = value;
            foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, ' ');
            safe = Regex.Replace(safe, @"\s+", " ").Trim(' ', '.', '-');
            if (safe.Length > 150) safe = safe.Substring(0, 150).Trim();
            return safe;
        }

        private async Task UndoLastBatchAsync()
        {
            string historyFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rename-history");
            if (!Directory.Exists(historyFolder))
            {
                statusLabel.Text = "No hay historial de renombrado.";
                return;
            }

            string last = Directory.GetFiles(historyFolder, "rename-history-*.tsv").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
            if (string.IsNullOrEmpty(last))
            {
                statusLabel.Text = "No hay lote para deshacer.";
                return;
            }

            DialogResult answer = MessageBox.Show("Deshacer el ultimo lote?\n" + Path.GetFileName(last), "Deshacer renombrado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            SetBusy(true, "Deshaciendo ultimo lote...");
            int restored = await Task.Run(() => UndoBatch(last));
            await AnalyzeAsync();
            SetBusy(false, null);
            statusLabel.Text = "Deshacer terminado: " + restored + " archivos restaurados.";
        }

        private static int UndoBatch(string historyPath)
        {
            int restored = 0;
            foreach (string line in File.ReadAllLines(historyPath, Encoding.UTF8).Skip(1).Reverse())
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 2) continue;
                string oldPath = parts[0];
                string newPath = parts[1];
                if (!File.Exists(newPath) || File.Exists(oldPath)) continue;
                try
                {
                    File.Move(newPath, oldPath);
                    restored++;
                }
                catch { }
            }
            return restored;
        }

        private bool TargetExists(OrganizerItem item)
        {
            string target = Path.Combine(Path.GetDirectoryName(item.Path), item.SuggestedName + item.Extension);
            return !string.Equals(item.Path, target, StringComparison.OrdinalIgnoreCase) && File.Exists(target);
        }

        private void ApplyFilter()
        {
            if (grid == null) return;
            string query = searchBox.Text.Trim();
            string type = typeBox.SelectedItem == null ? "Todos" : typeBox.SelectedItem.ToString();
            visibleItems = items
                .Where(i => (type == "Todos" || i.Type == type) &&
                            (query.Length == 0 || i.CurrentName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || i.SuggestedName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            SortVisibleItems();
            grid.DataSource = null;
            grid.DataSource = visibleItems;
            statusLabel.Text = visibleItems.Count + " visibles de " + items.Count + " analizados. Clic en encabezados ordena la tabla. " + MetadataNameResolver.GetTagEditorStatus();
        }

        private void SortByColumn(string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return;
            if (sortColumn == column) sortAscending = !sortAscending;
            else
            {
                sortColumn = column;
                sortAscending = column != "ConfidenceText" && column != "Status";
            }
            ApplyFilter();
        }

        private void SortVisibleItems()
        {
            Comparison<OrganizerItem> comparison = (a, b) => string.Compare(a.CurrentName, b.CurrentName, StringComparison.OrdinalIgnoreCase);
            if (sortColumn == "Selected") comparison = (a, b) => a.Selected.CompareTo(b.Selected);
            else if (sortColumn == "CurrentName") comparison = (a, b) => string.Compare(a.CurrentName, b.CurrentName, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == "SuggestedName") comparison = (a, b) => string.Compare(a.SuggestedName, b.SuggestedName, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == "ConfidenceText") comparison = (a, b) => a.Confidence.CompareTo(b.Confidence);
            else if (sortColumn == "Status") comparison = (a, b) => StatusRank(a.Status).CompareTo(StatusRank(b.Status));
            else if (sortColumn == "Type") comparison = (a, b) => string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
            else if (sortColumn == "Folder") comparison = (a, b) => string.Compare(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);

            visibleItems.Sort((a, b) =>
            {
                int value = comparison(a, b);
                if (!sortAscending) value = -value;
                if (value == 0) value = string.Compare(a.CurrentName, b.CurrentName, StringComparison.OrdinalIgnoreCase);
                return value;
            });
        }

        private static int StatusRank(string status)
        {
            if (status == "Seguro") return 3;
            if (status == "Revisar") return 2;
            if (status == "Sin cambio") return 1;
            return 0;
        }

        private void PreviewSelected()
        {
            if (grid.CurrentRow == null) return;
            OrganizerItem item = grid.CurrentRow.DataBoundItem as OrganizerItem;
            if (item == null || !File.Exists(item.Path))
            {
                statusLabel.Text = "Selecciona un track local para previsualizar.";
                return;
            }

            string error;
            if (previewBar.PlayTrack(item.Path, out error))
            {
                statusLabel.Text = "Preview real reproduciendo dentro de ARKAIOS. En este modo usa la salida default de Windows.";
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

        private void RenameManualSelected()
        {
            List<string> selected = GetSelectedPaths();
            if (selected.Count == 0)
            {
                statusLabel.Text = "Selecciona uno o mas archivos locales para renombrar manualmente.";
                return;
            }

            using (var dialog = new ManualRenameDialog(selected))
            {
                dialog.ShowDialog(this);
            }
            var _ = AnalyzeAsync();
        }

        private List<string> GetSelectedPaths()
        {
            var selected = new List<string>();
            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                OrganizerItem item = row.DataBoundItem as OrganizerItem;
                if (item != null && File.Exists(item.Path) && !selected.Contains(item.Path)) selected.Add(item.Path);
            }

            if (selected.Count == 0 && grid.CurrentRow != null)
            {
                OrganizerItem item = grid.CurrentRow.DataBoundItem as OrganizerItem;
                if (item != null && File.Exists(item.Path)) selected.Add(item.Path);
            }

            return selected;
        }

        private void CreateVdjPlaylistFromSelection()
        {
            List<string> selected = GetSelectedPaths();
            if (selected.Count == 0)
            {
                statusLabel.Text = "Selecciona tracks locales para crear una playlist de VirtualDJ.";
                return;
            }

            try
            {
                string playlistFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documentos", "VirtualDJ", "Playlists");
                if (!Directory.Exists(playlistFolder))
                    playlistFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VirtualDJ", "Playlists");
                Directory.CreateDirectory(playlistFolder);

                string playlistPath = Path.Combine(playlistFolder, "ARKAIOS Organizer " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".m3u");
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

        private void Grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            if (!grid.Rows[e.RowIndex].Selected)
            {
                grid.ClearSelection();
                grid.Rows[e.RowIndex].Selected = true;
            }
            if (e.ColumnIndex >= 0) grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        }

        private void OpenSelectedFolder()
        {
            string folder = AppSettings.MediaLibraryRoot;
            if (grid.CurrentRow != null)
            {
                OrganizerItem item = grid.CurrentRow.DataBoundItem as OrganizerItem;
                if (item != null) folder = item.Folder;
            }
            if (Directory.Exists(folder)) Process.Start("explorer.exe", folder);
        }

        private void SetBusy(bool busy, string message)
        {
            progressBar.Visible = busy;
            toolbar.Enabled = !busy;
            grid.Enabled = !busy;
            if (!string.IsNullOrWhiteSpace(message)) statusLabel.Text = message;
        }

        private class OrganizerItem
        {
            public bool Selected { get; set; }
            public string Path { get; set; }
            public string CurrentName { get; set; }
            public string SuggestedName { get; set; }
            public int Confidence { get; set; }
            public string ConfidenceText { get { return Confidence + "%"; } }
            public string Status { get; set; }
            public string Type { get; set; }
            public string Folder { get; set; }
            public string Extension { get; set; }
        }

    }
}
