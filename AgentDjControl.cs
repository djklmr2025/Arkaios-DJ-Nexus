using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class AgentDjControl : UserControl
    {
        public event Action<string> TrackSentToHub;

        private readonly RichTextBox chatHistory;
        private readonly TextBox inputBox;
        private readonly Button sendButton;
        private readonly Button clearButton;
        private readonly Label statusLabel;
        private readonly ProgressBar progressBar;

        public AgentDjControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 48,
                BackColor = Color.FromArgb(30, 30, 30),
                Margin = new Padding(0, 0, 0, 8)
            };
            var titleLabel = new Label
            {
                Text = "🤖 AGENTE VIRTUAL ARKAIOS DJ ASSISTANT",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 255, 140),
                AutoSize = true,
                Location = new Point(10, 6)
            };
            var subTitleLabel = new Label
            {
                Text = "Asistente Musical Experto y Motor Autónomo de Descarga | Escribe consultas o pide 'Descarga X'",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.LightGray,
                AutoSize = true,
                Location = new Point(10, 26)
            };
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subTitleLabel);

            // Chat History RichTextBox
            chatHistory = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(15, 15, 15),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 0, 8)
            };

            // Bottom Input & Controls Panel
            var inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 3,
                AutoSize = true,
                Margin = new Padding(0)
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inputPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inputPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            inputBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Height = 32
            };
            inputBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendUserMessageAsync();
                }
            };

            sendButton = new Button
            {
                Text = "Enviar Petición 🚀",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                AutoSize = true,
                Height = 32,
                Margin = new Padding(4, 0, 0, 0)
            };
            sendButton.Click += async (s, e) => await SendUserMessageAsync();

            clearButton = new Button
            {
                Text = "Limpiar Chat",
                Font = new Font("Segoe UI", 9f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.LightGray,
                AutoSize = true,
                Height = 32,
                Margin = new Padding(4, 0, 0, 0)
            };
            clearButton.Click += (s, e) => chatHistory.Clear();

            statusLabel = new Label
            {
                Text = "Agente Listo. Escribe cualquier orden o consulta.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.LightGray,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };

            progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Height = 6,
                Dock = DockStyle.Bottom
            };

            inputPanel.Controls.Add(inputBox, 0, 0);
            inputPanel.Controls.Add(sendButton, 1, 0);
            inputPanel.Controls.Add(clearButton, 2, 0);
            inputPanel.Controls.Add(statusLabel, 0, 1);
            inputPanel.SetColumnSpan(statusLabel, 3);

            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.Controls.Add(chatHistory, 0, 1);
            mainLayout.Controls.Add(inputPanel, 0, 2);

            Controls.Add(mainLayout);
            Controls.Add(progressBar);

            AppendAgentWelcomeMessage();
        }

        private void AppendAgentWelcomeMessage()
        {
            AppendMessage("🤖 AGENTE ARKAIOS DJ ASSISTANT",
                "¡Hola DJ! Soy tu Agente Inteligente Experto. ¿En qué te ayudo hoy?\n\n" +
                "• 📥 **Descargas Autónomas:** Dime *'bájame la canción X'* o *'descarga el video Y'* y lo guardaré en tus descargas recientes en Verde Neón.\n" +
                "• 🎧 **Consultas Musicales:** Pregúntame sobre BPMs, compatibilidad Camelot o datos curiosos de pistas.",
                Color.FromArgb(80, 255, 140));
        }

        private async Task SendUserMessageAsync()
        {
            string userText = inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(userText)) return;

            inputBox.Clear();
            AppendMessage("👤 DJ USER", userText, Color.LightCyan);

            SetBusy(true, "El Agente DJ está procesando la solicitud...");

            try
            {
                AgentResponse response = await AgentEngine.ProcessRequestAsync(userText, status =>
                {
                    if (InvokeRequired) Invoke(new Action(() => statusLabel.Text = status));
                    else statusLabel.Text = status;
                });

                AppendMessage("🤖 AGENTE ARKAIOS", response.Text, response.Success ? Color.FromArgb(80, 255, 140) : Color.SandyBrown);

                if (response.IsDownload && response.Success && !string.IsNullOrEmpty(response.DownloadedPath) && File.Exists(response.DownloadedPath))
                {
                    var handler = TrackSentToHub;
                    if (handler != null) handler(response.DownloadedPath);
                }
            }
            catch (Exception ex)
            {
                AppendMessage("🤖 AGENTE ARKAIOS", "⚠️ Ocurrió un error al procesar tu solicitud: " + ex.Message, Color.Red);
            }
            finally
            {
                SetBusy(false, "Agente Listo. Escribe cualquier orden o consulta.");
            }
        }

        private void AppendMessage(string sender, string message, Color color)
        {
            chatHistory.SelectionStart = chatHistory.TextLength;
            chatHistory.SelectionLength = 0;

            chatHistory.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            chatHistory.SelectionColor = color;
            chatHistory.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + sender + ":\n");

            chatHistory.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            chatHistory.SelectionColor = Color.White;
            chatHistory.AppendText(message + "\n\n");

            chatHistory.ScrollToCaret();
        }

        private void SetBusy(bool busy, string message)
        {
            progressBar.Visible = busy;
            inputBox.Enabled = !busy;
            sendButton.Enabled = !busy;
            clearButton.Enabled = !busy;
            if (!string.IsNullOrEmpty(message)) statusLabel.Text = message;
        }
    }
}
