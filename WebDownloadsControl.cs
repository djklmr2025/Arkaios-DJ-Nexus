using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ArkaiosDJAssistant
{
    // Navigation only: websites own their forms, accounts and download workflow.
    public sealed class WebDownloadsControl : UserControl
    {
        private readonly WebView2 browser = new WebView2 { Dock = DockStyle.Fill };
        private readonly ComboBox sites = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 28, AutoEllipsis = true };
        private readonly string[] urls = {
            "https://ytdlp.online/es/playlist-downloader",
            "https://flacdownloader.com",
            "https://spotidownloader.com/en19"
        };
        private bool initializing;
        private bool ready;

        public WebDownloadsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, AutoScroll = true };
            sites.Items.AddRange(new object[] { "yt-dlp · Listas", "FLAC Downloader", "SpotiDownloader" });
            sites.SelectedIndex = 0;
            toolbar.Controls.Add(sites);
            AddButton(toolbar, "Abrir", async () => await OpenSelectedAsync());
            AddButton(toolbar, "Atrás", () => { if (ready && browser.CanGoBack) browser.GoBack(); });
            AddButton(toolbar, "Adelante", () => { if (ready && browser.CanGoForward) browser.GoForward(); });
            AddButton(toolbar, "Recargar", () => { if (ready) browser.Reload(); });
            AddButton(toolbar, "Abrir en navegador", () => {
                try {
                    var url = ready ? browser.Source?.AbsoluteUri : null;
                    if (!IsWebUrl(url)) url = urls[sites.SelectedIndex];
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                } catch (Exception ex) { status.Text = ex.Message; }
            });
            status.Text = "Selecciona una web y pulsa Abrir.";
            Controls.Add(browser);
            Controls.Add(toolbar);
            Controls.Add(status);
        }

        private static bool IsWebUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        private static void AddButton(FlowLayoutPanel panel, string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true, ForeColor = Color.Black };
            button.Click += (sender, args) => action();
            panel.Controls.Add(button);
        }

        private async System.Threading.Tasks.Task OpenSelectedAsync()
        {
            if (initializing || IsDisposed) return;
            initializing = true;
            try {
                if (!ready) {
                    status.Text = "Inicializando WebView2…";
                    var profile = Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData), "ArkaiosDJ", "WebDownloads");
                    var environment = await CoreWebView2Environment.CreateAsync(null, profile);
                    if (IsDisposed) return;
                    await browser.EnsureCoreWebView2Async(environment);
                    if (IsDisposed) return;
                    browser.CoreWebView2.NewWindowRequested += (sender, args) => {
                        // Keep only user-initiated links; suppress unsolicited popups.
                        args.Handled = true;
                        if (args.IsUserInitiated && IsWebUrl(args.Uri))
                            browser.CoreWebView2.Navigate(args.Uri);
                    };
                    browser.CoreWebView2.NavigationStarting += (sender, args) => {
                        if (!IsWebUrl(args.Uri) && args.Uri != "about:blank")
                            args.Cancel = true;
                    };
                    browser.CoreWebView2.NavigationCompleted += (sender, args) => {
                        if (!IsDisposed) status.Text = args.IsSuccess
                            ? browser.Source?.AbsoluteUri
                            : "No se pudo cargar: " + args.WebErrorStatus + ". Puedes abrir en navegador.";
                    };
                    ready = true;
                }
                status.Text = "Cargando…";
                browser.CoreWebView2.Navigate(urls[sites.SelectedIndex]);
            } catch (Exception ex) {
                if (!IsDisposed) status.Text = "WebView2 no disponible: " + ex.Message
                    + ". Instala Microsoft Edge WebView2 Runtime o abre en navegador.";
            } finally { initializing = false; }
        }
    }
}
