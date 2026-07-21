using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class PreviewPlayerBar : UserControl
    {
        private readonly Panel progressTrack;
        private readonly Panel progressFill;
        private readonly Label iconLabel;
        private readonly Label titleLabel;
        private readonly Label stateLabel;
        private readonly Label deviceLabel;
        private readonly Button playButton;
        private readonly Button pauseButton;
        private readonly Button resumeButton;
        private readonly Button stopButton;
        private string currentPath;
        private bool currentIsVideo;

        public PreviewPlayerBar()
        {
            Dock = DockStyle.Bottom;
            Height = 58;
            BackColor = Color.FromArgb(13, 18, 30);
            ForeColor = Color.White;
            Padding = new Padding(12, 8, 12, 8);

            iconLabel = new Label
            {
                Dock = DockStyle.Left,
                Width = 44,
                BackColor = Color.FromArgb(40, 50, 68),
                ForeColor = Color.FromArgb(30, 215, 96),
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Text = "♪",
                TextAlign = ContentAlignment.MiddleCenter
            };

            var textPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 8, 0) };

            titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Text = "Selecciona un track local para preview",
                TextAlign = ContentAlignment.MiddleLeft
            };

            stateLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                ForeColor = Color.FromArgb(170, 185, 205),
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                Text = "Listo para reproducir desde el Hub local",
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressTrack = new Panel
            {
                Dock = DockStyle.Top,
                Height = 5,
                BackColor = Color.FromArgb(55, 65, 82),
                Margin = new Padding(0, 4, 0, 0)
            };

            progressFill = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(30, 215, 96)
            };
            progressTrack.Controls.Add(progressFill);

            deviceLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 210,
                ForeColor = Color.FromArgb(160, 175, 198),
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 224,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            playButton = BuildPlayerButton("▶", 44);
            pauseButton = BuildPlayerButton("⏸", 44);
            resumeButton = BuildPlayerButton("▶▶", 48);
            stopButton = BuildPlayerButton("■", 44);

            playButton.Click += (s, e) => PlayCurrent();
            pauseButton.Click += (s, e) => PauseCurrent();
            resumeButton.Click += (s, e) => ResumeCurrent();
            stopButton.Click += (s, e) => StopCurrent();

            buttons.Controls.AddRange(new Control[] { playButton, pauseButton, resumeButton, stopButton });
            textPanel.Controls.Add(progressTrack);
            textPanel.Controls.Add(stateLabel);
            textPanel.Controls.Add(titleLabel);
            Controls.Add(textPanel);
            Controls.Add(deviceLabel);
            Controls.Add(buttons);
            Controls.Add(iconLabel);

            SetDeviceHint();
        }

        public void LoadTrack(string path)
        {
            currentPath = path;
            currentIsVideo = IsVideo(path);
            titleLabel.Text = File.Exists(path) ? Path.GetFileNameWithoutExtension(path) : "Track no disponible";
            stateLabel.Text = currentIsVideo ? "Video cargado: se prescuchara solo audio interno" : "Cargado para preview local";
            progressFill.Width = 0;
            SetDeviceHint();
        }

        public bool PlayTrack(string path, out string error)
        {
            LoadTrack(path);
            bool ok = AudioPreviewEngine.Play(path, out error);
            if (ok)
            {
                titleLabel.Text = Path.GetFileNameWithoutExtension(path);
                stateLabel.Text = currentIsVideo ? "Prescuchando audio del video dentro de ARKAIOS" : "Reproduciendo dentro de ARKAIOS";
                progressFill.Width = Math.Max(24, progressTrack.Width / 3);
            }
            return ok;
        }

        public bool PauseCurrent()
        {
            string error;
            bool ok = PauseCurrent(out error);
            return ok;
        }

        public bool PauseCurrent(out string error)
        {
            bool ok = AudioPreviewEngine.Pause(out error);
            if (ok) stateLabel.Text = "Pausado";
            return ok;
        }

        public bool ResumeCurrent()
        {
            string error;
            bool ok = ResumeCurrent(out error);
            return ok;
        }

        public bool ResumeCurrent(out string error)
        {
            bool ok = AudioPreviewEngine.Resume(out error);
            if (ok) stateLabel.Text = "Reproduciendo dentro de ARKAIOS";
            return ok;
        }

        public void StopCurrent()
        {
            AudioPreviewEngine.Stop();
            stateLabel.Text = "Preview detenido";
            progressFill.Width = 0;
        }

        private void PlayCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
            {
                stateLabel.Text = "Selecciona primero un track local.";
                return;
            }

            string error;
            if (!PlayTrack(currentPath, out error)) stateLabel.Text = "No se pudo reproducir: " + error;
        }

        private void SetDeviceHint()
        {
            string selected = AudioDeviceCatalog.GetSelectedLabel();
            deviceLabel.Text = "Salida preview: " + selected;
        }

        private static Button BuildPlayerButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 215, 96),
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(4, 0, 4, 0)
            };
        }

        private static bool IsVideo(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp4" || ext == ".mkv" || ext == ".webm" || ext == ".avi" || ext == ".mov";
        }

        private static string SafeName(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "sin track" : Path.GetFileNameWithoutExtension(path);
        }

    }
}
