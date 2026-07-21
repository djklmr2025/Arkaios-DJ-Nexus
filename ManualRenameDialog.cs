using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class ManualRenameDialog : Form
    {
        private readonly List<string> paths;
        private readonly Label currentLabel;
        private readonly TextBox nameBox;
        private readonly Label statusLabel;
        private int index;
        private readonly List<string> log = new List<string>();

        public ManualRenameDialog(List<string> selectedPaths)
        {
            paths = selectedPaths ?? new List<string>();
            Text = "Renombramiento manual Arkaios";
            Width = 680;
            Height = 230;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(25, 25, 25);
            ForeColor = Color.White;

            currentLabel = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(10), ForeColor = Color.White };
            nameBox = new TextBox { Dock = DockStyle.Top, Margin = new Padding(10), Font = new Font("Segoe UI", 10) };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(10), WrapContents = false };
            var adjustButton = new Button { Text = "Ajustar", AutoSize = true };
            adjustButton.Click += (s, e) => nameBox.Text = CleanTitle(nameBox.Text);
            var applyButton = new Button { Text = "Fijar nuevo nombre", AutoSize = true };
            applyButton.Click += (s, e) => ApplyCurrent();
            var skipButton = new Button { Text = "Omitir", AutoSize = true };
            skipButton.Click += (s, e) => MoveNext();
            var closeButton = new Button { Text = "Cerrar", AutoSize = true };
            closeButton.Click += (s, e) => Close();
            buttons.Controls.AddRange(new Control[] { adjustButton, applyButton, skipButton, closeButton });

            statusLabel = new Label { Dock = DockStyle.Fill, Padding = new Padding(10), ForeColor = Color.LightGray };
            Controls.Add(statusLabel);
            Controls.Add(buttons);
            Controls.Add(nameBox);
            Controls.Add(currentLabel);
            LoadCurrent();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (log.Count == 0) return;
            string historyFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rename-history");
            Directory.CreateDirectory(historyFolder);
            string historyPath = Path.Combine(historyFolder, "manual-hub-rename-history-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".tsv");
            var lines = new List<string> { "old\tnew" };
            lines.AddRange(log);
            File.WriteAllLines(historyPath, lines.ToArray(), Encoding.UTF8);
        }

        private void LoadCurrent()
        {
            if (index >= paths.Count)
            {
                currentLabel.Text = "No hay mas archivos seleccionados.";
                nameBox.Text = "";
                statusLabel.Text = "Proceso terminado. Cambios aplicados: " + log.Count;
                return;
            }

            string path = paths[index];
            currentLabel.Text = string.Format("{0}/{1} - Archivo actual:\n{2}", index + 1, paths.Count, Path.GetFileName(path));
            nameBox.Text = Path.GetFileNameWithoutExtension(path);
            statusLabel.Text = "Escribe el nuevo nombre sin extension. Ajustar limpia texto; Fijar aplica el cambio.";
        }

        private void ApplyCurrent()
        {
            if (index >= paths.Count) return;
            string path = paths[index];
            if (!File.Exists(path))
            {
                statusLabel.Text = "El archivo ya no existe: " + path;
                MoveNext();
                return;
            }

            string cleanName = SanitizeFileName(nameBox.Text);
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                statusLabel.Text = "Nombre vacio o invalido.";
                return;
            }

            string target = Path.Combine(Path.GetDirectoryName(path), cleanName + Path.GetExtension(path).ToLowerInvariant());
            if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
            {
                statusLabel.Text = "Sin cambio: el nombre es igual al actual.";
                MoveNext();
                return;
            }
            if (File.Exists(target))
            {
                statusLabel.Text = "Ya existe un archivo con ese nombre.";
                return;
            }

            try
            {
                File.Move(path, target);
                log.Add(path + "\t" + target);
                paths[index] = target;
                statusLabel.Text = "Renombrado: " + Path.GetFileName(target);
                MoveNext();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "No se pudo renombrar: " + ex.Message;
            }
        }

        private void MoveNext()
        {
            index++;
            LoadCurrent();
        }

        private static string CleanTitle(string value)
        {
            string text = value ?? "";
            text = text.Replace('_', ' ');
            text = Regex.Replace(text, @"\s+", " ").Trim();
            text = Regex.Replace(text, @"^\s*\(?\d{1,4}\)?\s*[-_. ]+", "");
            text = Regex.Replace(text, @"\[[A-Za-z0-9_-]{8,15}\]$", "").Trim();
            text = Regex.Replace(text, @"\s+", " ").Trim(' ', '-', '.');
            return text;
        }

        private static string SanitizeFileName(string value)
        {
            string safe = CleanTitle(value);
            foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, ' ');
            safe = Regex.Replace(safe, @"\s+", " ").Trim(' ', '.', '-');
            if (safe.Length > 150) safe = safe.Substring(0, 150).Trim();
            return safe;
        }
    }
}
