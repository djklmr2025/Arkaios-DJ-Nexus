using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArkaiosDJAssistant
{
    public class UniversalDownloadResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string PlatformName { get; set; }
        public string Message { get; set; }
    }

    public static class UniversalDownloaderEngine
    {
        public static async Task<UniversalDownloadResult> DownloadFromUrlAsync(string url, string outputFolder = null, Action<string> progressCallback = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return new UniversalDownloadResult { Success = false, Message = "URL vacía." };

            string cleanUrl = url.Trim();
            progressCallback?.Invoke("🌐 Analizando enlace y detectando motor adecuado...");

            try
            {
                // 1. Archivo directo MP3 / FLAC / M4A / WAV
                if (cleanUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    cleanUrl.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                    cleanUrl.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                    cleanUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    progressCallback?.Invoke("📁 Descargando archivo de audio directo...");
                    return await DownloadDirectAudioFileAsync(cleanUrl, outputFolder);
                }

                // 2. Enlace de Google Drive
                if (cleanUrl.Contains("drive.google.com"))
                {
                    progressCallback?.Invoke("📁 Procesando enlace de Google Drive...");
                    return await DownloadGoogleDriveLinkAsync(cleanUrl, outputFolder);
                }

                // 3. Enlace de Spotify
                if (cleanUrl.Contains("spotify.com"))
                {
                    progressCallback?.Invoke("🟢 Procesando enlace de Spotify mediante motor de extracción...");
                    var daemonResp = await ArkaiosDaemonClient.EnqueueDownloadAsync(cleanUrl, "Spotify Track", "Artista");
                    if (daemonResp != null && !string.IsNullOrEmpty(daemonResp.TaskId))
                    {
                        return new UniversalDownloadResult
                        {
                            Success = true,
                            Title = "Spotify Track",
                            PlatformName = "Spotify",
                            Message = "Descarga enviada exitosamente al demonio Spotify-Arkaios en segundo plano."
                        };
                    }
                }

                // 4. Motores nativos yt-dlp (YouTube, SoundCloud, Bandcamp, Audiomack, Deezer, etc.)
                progressCallback?.Invoke("⚡ Ejecutando motor de descarga multiformato (yt-dlp en segundo plano)...");
                string downloadedPath = await YouTubeEngine.DownloadAsync(cleanUrl, "music", "MP3 320 kbps");

                if (!string.IsNullOrEmpty(downloadedPath) && File.Exists(downloadedPath))
                {
                    return new UniversalDownloadResult
                    {
                        Success = true,
                        FilePath = downloadedPath,
                        Title = Path.GetFileNameWithoutExtension(downloadedPath),
                        PlatformName = DetectPlatformName(cleanUrl),
                        Message = "Descarga completada con éxito: " + Path.GetFileName(downloadedPath)
                    };
                }

                // 5. Fallback por búsqueda de metadatos
                progressCallback?.Invoke("🔍 Buscando metadatos de audio para resolver descarga...");
                var searchResults = await YouTubeEngine.SearchAsync(cleanUrl, "music", 1);
                if (searchResults != null && searchResults.Count > 0)
                {
                    string targetUrl = searchResults[0].Url;
                    downloadedPath = await YouTubeEngine.DownloadAsync(targetUrl, "music", "MP3 320 kbps");
                    if (!string.IsNullOrEmpty(downloadedPath) && File.Exists(downloadedPath))
                    {
                        return new UniversalDownloadResult
                        {
                            Success = true,
                            FilePath = downloadedPath,
                            Title = searchResults[0].Title,
                            PlatformName = DetectPlatformName(cleanUrl),
                            Message = "Descarga completada vía motor alternativo."
                        };
                    }
                }

                return new UniversalDownloadResult
                {
                    Success = false,
                    PlatformName = DetectPlatformName(cleanUrl),
                    Message = "No se pudo procesar la descarga de la URL ingresada."
                };
            }
            catch (Exception ex)
            {
                return new UniversalDownloadResult
                {
                    Success = false,
                    Message = "Error en motor multidescargador: " + ex.Message
                };
            }
        }

        public static string DetectPlatformName(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "Desconocida";
            string u = url.ToLowerInvariant();
            if (u.Contains("spotify.com")) return "Spotify";
            if (u.Contains("youtube.com") || u.Contains("youtu.be")) return "YouTube Music";
            if (u.Contains("soundcloud.com")) return "SoundCloud";
            if (u.Contains("deezer.com")) return "Deezer";
            if (u.Contains("tidal.com")) return "TIDAL";
            if (u.Contains("apple.com")) return "Apple Music";
            if (u.Contains("bandcamp.com")) return "Bandcamp";
            if (u.Contains("audiomack.com")) return "Audiomack";
            if (u.Contains("drive.google.com")) return "Google Drive";
            return "Multi-Plataforma Web";
        }

        private static async Task<UniversalDownloadResult> DownloadDirectAudioFileAsync(string url, string outputFolder)
        {
            string targetFolder = string.IsNullOrWhiteSpace(outputFolder) ? AppSettings.GetDownloadFolder("music") : outputFolder;
            Directory.CreateDirectory(targetFolder);
            string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "track_" + DateTime.Now.Ticks + ".mp3";
            string targetPath = Path.Combine(targetFolder, fileName);

            using (var client = new HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(url);
                File.WriteAllBytes(targetPath, bytes);
            }

            return new UniversalDownloadResult
            {
                Success = true,
                FilePath = targetPath,
                Title = Path.GetFileNameWithoutExtension(targetPath),
                PlatformName = "Archivo Directo",
                Message = "Archivo de audio guardado exitosamente."
            };
        }

        private static async Task<UniversalDownloadResult> DownloadGoogleDriveLinkAsync(string url, string outputFolder)
        {
            Match m = Regex.Match(url, @"/d/([a-zA-Z0-9_-]+)");
            string fileId = m.Success ? m.Groups[1].Value : null;
            if (string.IsNullOrEmpty(fileId))
            {
                m = Regex.Match(url, @"id=([a-zA-Z0-9_-]+)");
                if (m.Success) fileId = m.Groups[1].Value;
            }

            if (!string.IsNullOrEmpty(fileId))
            {
                string directUrl = "https://drive.google.com/uc?export=download&id=" + fileId;
                return await DownloadDirectAudioFileAsync(directUrl, outputFolder);
            }

            return new UniversalDownloadResult { Success = false, Message = "ID de Google Drive inválido." };
        }
    }
}
