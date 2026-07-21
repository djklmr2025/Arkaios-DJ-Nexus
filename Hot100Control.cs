using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    /// <summary>
    /// Pestaña "Hot 100" propia de ARKAIOS: muestra los 100 tracks que mas se repiten
    /// entre los usuarios al cargarlos al plato, segun el manifiesto de servicios de
    /// servidor-arkaios-api.vercel.app (clave "hot100"). El diseño se inspira en la
    /// vista de PulseDJ solo como referencia visual; los datos son 100% propios.
    /// </summary>
    public class Hot100Control : UserControl
    {
        private ListView listView;
        private Label lblStatus;
        private Button btnRefresh;
        private Timer refreshTimer;
        private bool isLoading = false;

        public Hot100Control()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(20, 20, 20);

            Label lblTitle = new Label
            {
                Text = "ARKAIOS Hot 100  ·  MX",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 15),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);

            Label lblSubtitle = new Label
            {
                Text = "Los 100 tracks que mas se repiten al subir al plato entre usuarios de ARKAIOS.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(20, 45),
                AutoSize = true
            };
            this.Controls.Add(lblSubtitle);

            btnRefresh = new Button
            {
                Text = "Actualizar ahora",
                Location = new Point(20, 70),
                Width = 150,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            btnRefresh.Click += async (s, e) => await LoadAsync();
            this.Controls.Add(btnRefresh);

            lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(180, 76),
                AutoSize = true
            };
            this.Controls.Add(lblStatus);

            listView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                Location = new Point(20, 108),
                Width = 640,
                Height = 420,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                OwnerDraw = false
            };
            listView.Columns.Add("#", 45);
            listView.Columns.Add("Track", 300);
            listView.Columns.Add("Artista", 220);
            listView.Columns.Add("Cambio", 70);
            this.Controls.Add(listView);

            // Refresco periodico para que la lista se sienta viva sin recargar manual.
            refreshTimer = new Timer { Interval = 5 * 60 * 1000 };
            refreshTimer.Tick += async (s, e) => await LoadAsync();
            refreshTimer.Start();

            this.Load += async (s, e) => await LoadAsync();
            this.Disposed += (s, e) => { refreshTimer.Stop(); refreshTimer.Dispose(); };
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (isLoading) return;
            isLoading = true;
            btnRefresh.Enabled = false;
            lblStatus.ForeColor = Color.Gray;
            lblStatus.Text = "Actualizando...";

            Hot100Result result = await Hot100Client.GetTopTracksAsync("MX");

            if (!result.Success)
            {
                listView.Items.Clear();
                lblStatus.ForeColor = Color.LightCoral;
                lblStatus.Text = result.Error;
                isLoading = false;
                btnRefresh.Enabled = true;
                return;
            }

            listView.BeginUpdate();
            listView.Items.Clear();
            foreach (Hot100Track track in result.Tracks)
            {
                var lvi = new ListViewItem(track.Position.ToString());
                lvi.SubItems.Add(track.Title);
                lvi.SubItems.Add(track.Artist);
                lvi.SubItems.Add(FormatChange(track.Change));
                lvi.ForeColor = ChangeColor(track.Change);
                listView.Items.Add(lvi);
            }
            listView.EndUpdate();

            lblStatus.ForeColor = Color.LightGreen;
            lblStatus.Text = result.Tracks.Count + " tracks · actualizado " + DateTime.Now.ToString("HH:mm:ss");
            isLoading = false;
            btnRefresh.Enabled = true;
        }

        private static string FormatChange(int change)
        {
            if (change > 0) return "+" + change;
            if (change < 0) return change.ToString();
            return "—";
        }

        private static Color ChangeColor(int change)
        {
            if (change > 0) return Color.LightGreen;
            if (change < 0) return Color.LightCoral;
            return Color.White;
        }
    }
}
