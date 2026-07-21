using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class OperationProgressDialog : Form
    {
        private readonly Label titleLabel;
        private readonly Label detailLabel;
        private readonly ProgressBar progressBar;
        private readonly Label resultLabel;

        public OperationProgressDialog(string title)
        {
            Text = title;
            Width = 560;
            Height = 185;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            titleLabel = new Label { Dock = DockStyle.Top, Height = 34, Padding = new Padding(12, 10, 12, 0), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            detailLabel = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 4, 12, 0), ForeColor = Color.White };
            progressBar = new ProgressBar { Dock = DockStyle.Top, Height = 18, Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100, Margin = new Padding(12) };
            resultLabel = new Label { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0), ForeColor = Color.LightGray };

            Controls.Add(resultLabel);
            Controls.Add(progressBar);
            Controls.Add(detailLabel);
            Controls.Add(titleLabel);
            SetStatus("Preparando...", "Esperando respuesta del proceso.", 0);
        }

        public void SetStatus(string action, string detail, int percent)
        {
            titleLabel.Text = action;
            detailLabel.Text = detail;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = Math.Max(0, Math.Min(100, percent));
            Application.DoEvents();
        }

        public void SetIndeterminate(string action, string detail)
        {
            titleLabel.Text = action;
            detailLabel.Text = detail;
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.MarqueeAnimationSpeed = 25;
            Application.DoEvents();
        }

        public void SetResult(string result)
        {
            resultLabel.Text = result;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 100;
            Application.DoEvents();
        }

        public void SetBatchStatus(int index, int total, string action, string detail)
        {
            if (total <= 0) total = 1;
            int percent = Math.Max(0, Math.Min(100, (index * 100) / total));
            SetStatus(string.Format("{0}/{1} - {2}", index, total, action), detail, percent);
        }
    }
}
