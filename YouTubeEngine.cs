using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ArkaiosDJAssistant
{
    public class YouTubeTrack
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Duration { get; set; }
        public string Uploader { get; set; }
        public long ViewCount { get; set; }
        public int MaxHeight { get; set; }
        public int MaxAudioKbps { get; set; }
        public long EstimatedAudioBytes { get; set; }
        public long EstimatedVideoBytes { get; set; }
        public string AvailableOutputs { get; set; }
        public bool Downloaded { get; set; }
        public string DownloadedPath { get; set; }
        public string PreviewAction { get { return Downloaded ? "Prescuchar" : "Previsualizar"; } }
        public string HubAction { get { return Downloaded ? "Ver en AutoHelp + Camelot" : "↓ Descargar"; } }
        public string DownloadState { get { return Downloaded ? "YA DESCARGADA" : "En linea"; } }
        public string MaximumQualityOverride { get; set; }
        public string MaximumQuality
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(MaximumQualityOverride)) return MaximumQualityOverride;
                return MaxHeight > 0 ? MaxHeight + "p / audio " + MaxAudioKbps + " kbps" : "audio " + MaxAudioKbps + " kbps";
            }
        }
    }

    public static class YouTubeEngine
    {
        private static readonly string ErrorLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp-errors.log");
        private static string _resolvedYtDlpPath = null;

        public static string GetYtDlpExecutablePath()
        {
            return EnsureYtDlpAvailable().Path;
        }

        public static (bool Available, string Path, string StatusMessage) EnsureYtDlpAvailable()
        {
            if (!string.IsNullOrWhiteSpace(AppSettings.YtDlpCustomPath) && File.Exists(AppSettings.YtDlpCustomPath))
            {
                _resolvedYtDlpPath = AppSettings.YtDlpCustomPath;
                return (true, _resolvedYtDlpPath, "✔ Usando ruta personalizada de yt-dlp: " + _resolvedYtDlpPath);
            }

            if (!string.IsNullOrWhiteSpace(_resolvedYtDlpPath) && File.Exists(_resolvedYtDlpPath))
            {
                return (true, _resolvedYtDlpPath, "✔ yt-dlp listo en: " + _resolvedYtDlpPath);
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetLocalPath = Path.Combine(baseDir, "yt-dlp.exe");

            if (File.Exists(targetLocalPath))
            {
                _resolvedYtDlpPath = targetLocalPath;
                return (true, _resolvedYtDlpPath, "✔ yt-dlp listo en carpeta local: " + _resolvedYtDlpPath);
            }

            string[] fileNames = new[] { "yt-dlp.exe", "ytdl.exe", "yt-dlp", "youtube-dl.exe" };
            string[] subFolders = new[] { "", "tools", "payload", @"installer\payload", "dist", "bin", "external" };

            foreach (string sub in subFolders)
            {
                string folder = string.IsNullOrEmpty(sub) ? baseDir : Path.Combine(baseDir, sub);
                foreach (string fn in fileNames)
                {
                    string candidate = Path.Combine(folder, fn);
                    if (File.Exists(candidate))
                    {
                        TryAutoCopy(candidate, targetLocalPath);
                        _resolvedYtDlpPath = File.Exists(targetLocalPath) ? targetLocalPath : candidate;
                        return (true, _resolvedYtDlpPath, "✔ yt-dlp enlazado desde: " + candidate);
                    }
                }
            }

            DirectoryInfo parent = Directory.GetParent(baseDir);
            int level = 0;
            while (parent != null && level < 4)
            {
                foreach (string sub in subFolders)
                {
                    string folder = string.IsNullOrEmpty(sub) ? parent.FullName : Path.Combine(parent.FullName, sub);
                    foreach (string fn in fileNames)
                    {
                        string candidate = Path.Combine(folder, fn);
                        if (File.Exists(candidate))
                        {
                            TryAutoCopy(candidate, targetLocalPath);
                            _resolvedYtDlpPath = File.Exists(targetLocalPath) ? targetLocalPath : candidate;
                            return (true, _resolvedYtDlpPath, "✔ yt-dlp vinculado desde directorio superior: " + candidate);
                        }
                    }
                }
                parent = parent.Parent;
                level++;
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] commonPaths = new[]
            {
                @"C:\ARKAIOS\yt-dlp.exe",
                @"C:\ARKAIOS\DJ_Assistant\yt-dlp.exe",
                @"C:\ARKAIOS\DJ_Assistant\installer\payload\yt-dlp.exe",
                Path.Combine(localAppData, @"Programs\Arkaios DJ Nexus\yt-dlp.exe"),
                Path.Combine(localAppData, @"ArkaiosDJNexus\yt-dlp.exe"),
                Path.Combine(appData, @"ArkaiosDJNexus\yt-dlp.exe")
            };

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    TryAutoCopy(path, targetLocalPath);
                    _resolvedYtDlpPath = File.Exists(targetLocalPath) ? targetLocalPath : path;
                    return (true, _resolvedYtDlpPath, "✔ yt-dlp vinculado desde sistema: " + path);
                }
            }

            try
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string dir in pathEnv.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    foreach (string fn in fileNames)
                    {
                        string candidate = Path.Combine(dir.Trim(), fn);
                        if (File.Exists(candidate))
                        {
                            _resolvedYtDlpPath = candidate;
                            return (true, _resolvedYtDlpPath, "✔ yt-dlp detectado en PATH del sistema: " + candidate);
                        }
                    }
                }
            }
            catch { }

            if (ExtractEmbeddedResource("ArkaiosDJAssistant.yt-dlp.exe", targetLocalPath))
            {
                _resolvedYtDlpPath = targetLocalPath;
                return (true, _resolvedYtDlpPath, "✔ yt-dlp auto-extraído exitosamente desde recurso incrustado en ejecutable.");
            }

            _resolvedYtDlpPath = targetLocalPath;
            return (false, targetLocalPath, "⚠ No se encontró el complemento yt-dlp.exe (requerido para descargas).\nColoca yt-dlp.exe en " + baseDir + " o especifícalo en Opciones.");
        }

        private static void TryAutoCopy(string source, string destination)
        {
            try
            {
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return;
                if (!File.Exists(destination))
                {
                    File.Copy(source, destination, true);
                }
            }
            catch { }
        }

        private static bool ExtractEmbeddedResource(string resourceName, string targetPath)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return false;
                    using (var fileStream = File.Create(targetPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                    return File.Exists(targetPath);
                }
            }
            catch (Exception ex)
            {
                AppendError("Fallo extrayendo recurso incrustado " + resourceName + ": " + ex.Message);
                return false;
            }
        }

        public static Task<List<YouTubeTrack>> SearchArtistAsync(string artist)
        {
            return SearchAsync(artist, "music", 5);
        }

        public static async Task<string> SuggestCanonicalQueryAsync(string query, string mediaType)
        {
            List<YouTubeTrack> matches = await SearchAsync(query, mediaType, 3);
            if (matches.Count == 0) return null;
            YouTubeTrack best = matches[0];
            string title = (best.Title ?? "").Trim();
            string uploader = (best.Uploader ?? "").Trim();
            if (title.Length == 0) return null;
            if (uploader.Length > 0 && title.IndexOf(uploader, StringComparison.OrdinalIgnoreCase) < 0)
                return uploader + " - " + title;
            return title;
        }

        public static async Task<List<YouTubeTrack>> SearchAsync(string query, string mediaType, int limit)
        {
            var tracks = new List<YouTubeTrack>();
            if (string.IsNullOrWhiteSpace(query) || !EnsureYtDlpAvailable().Available) return tracks;

            string suffix = mediaType == "karaoke" ? " karaoke lyrics" : mediaType == "video" ? " official music video" : " official audio";
            string search = "ytsearch" + Math.Max(1, Math.Min(limit, 20)) + ":" + query + suffix;

            // Método 1: Búsqueda ultra rápida y estable usando --flat-playlist --print
            var result = await RunAsync(new[] { "--flat-playlist", "--print", "%(title)s|%(webpage_url)s|%(uploader)s|%(duration_string)s", search });
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                string[] lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1])) continue;

                    string title = parts[0].Trim();
                    string url = parts[1].Trim();
                    string uploader = parts.Length > 2 ? parts[2].Trim() : "";
                    string duration = parts.Length > 3 ? parts[3].Trim() : "";

                    if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                    tracks.Add(new YouTubeTrack
                    {
                        Title = title,
                        Url = url,
                        Uploader = string.IsNullOrWhiteSpace(uploader) ? "YouTube" : uploader,
                        Duration = string.IsNullOrWhiteSpace(duration) ? "3:30" : duration,
                        AvailableOutputs = mediaType == "music" ? "MP3 / M4A" : "MP4",
                        MaximumQualityOverride = mediaType == "music" ? "audio 320 kbps" : "1080p / 720p"
                    });
                }
            }

            // Fallback: Si --flat-playlist no retornó resultados, intentar con --dump-single-json
            if (tracks.Count == 0)
            {
                var jsonResult = await RunAsync(new[] { "--dump-single-json", "--no-warnings", "--no-playlist", search });
                if (jsonResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(jsonResult.Output))
                {
                    try
                    {
                        var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResult.Output);
                        object entriesObject;
                        if (root != null && root.TryGetValue("entries", out entriesObject))
                        {
                            var entries = entriesObject as IEnumerable;
                            if (entries != null)
                            {
                                foreach (object entryObject in entries)
                                {
                                    var entry = entryObject as Dictionary<string, object>;
                                    if (entry == null) continue;
                                    int duration = ToInt(entry, "duration");
                                    string url = ToString(entry, "webpage_url");
                                    if (string.IsNullOrEmpty(url)) url = ToString(entry, "url");
                                    if (string.IsNullOrEmpty(url)) continue;
                                    tracks.Add(new YouTubeTrack
                                    {
                                        Title = ToString(entry, "title"),
                                        Url = url,
                                        Uploader = ToString(entry, "uploader"),
                                        Duration = string.Format("{0}:{1:00}", duration / 60, duration % 60),
                                        AvailableOutputs = mediaType == "music" ? "MP3 / M4A" : "MP4"
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex) { AppendError("Search JSON: " + ex); }
                }
            }

            return tracks;
        }

        public static async Task<List<YouTubeTrack>> SearchSoundCloudAsync(string query, int limit)
        {
            var tracks = new List<YouTubeTrack>();
            if (string.IsNullOrWhiteSpace(query) || !EnsureYtDlpAvailable().Available) return tracks;

            string search = "scsearch" + Math.Max(1, Math.Min(limit, 20)) + ":" + query;
            var result = await RunAsync(new[] { "--flat-playlist", "--print", "%(title)s|%(webpage_url)s|%(uploader)s|%(duration_string)s", search });
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output)) return tracks;

            string[] lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1])) continue;
                tracks.Add(new YouTubeTrack
                {
                    Title = parts[0],
                    Url = parts[1],
                    Uploader = parts.Length > 2 ? parts[2] : "SoundCloud",
                    Duration = parts.Length > 3 ? parts[3] : "-",
                    MaxHeight = 0,
                    MaxAudioKbps = 0,
                    AvailableOutputs = "MP3 / M4A"
                });
            }
            return tracks;
        }

        public static async Task<List<YouTubeTrack>> ExtractPlaylistFlatAsync(string playlistUrl, int limit)
        {
            var tracks = new List<YouTubeTrack>();
            if (string.IsNullOrWhiteSpace(playlistUrl) || !EnsureYtDlpAvailable().Available) return tracks;

            var result = await RunAsync(new[] { "--dump-single-json", "--flat-playlist", "--no-warnings", "--playlist-end", Math.Max(1, Math.Min(limit, 50)).ToString(), playlistUrl });
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output)) return tracks;

            try
            {
                var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(result.Output);
                object entriesObject;
                if (root == null || !root.TryGetValue("entries", out entriesObject)) return tracks;
                var entries = entriesObject as IEnumerable;
                if (entries == null) return tracks;

                foreach (object entryObject in entries)
                {
                    var entry = entryObject as Dictionary<string, object>;
                    if (entry == null) continue;
                    string url = ToString(entry, "url");
                    string id = ToString(entry, "id");
                    if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && id.Length > 0)
                        url = "https://www.youtube.com/watch?v=" + id;
                    if (string.IsNullOrWhiteSpace(url)) continue;

                    tracks.Add(new YouTubeTrack
                    {
                        Title = ToString(entry, "title"),
                        Url = url,
                        Uploader = ToString(entry, "uploader"),
                        Duration = "-",
                        AvailableOutputs = "MP3 / M4A / MP4"
                    });
                }
            }
            catch (Exception ex) { AppendError("Playlist JSON: " + ex); }
            return tracks;
        }

        public static Task<string> DownloadAudioAsync(string url)
        {
            return DownloadAsync(url, "music", "MP3 320 kbps");
        }

        public static async Task<string> DownloadAsync(string url, string mediaType, string quality)
        {
            var status = EnsureYtDlpAvailable();
            if (!status.Available || string.IsNullOrWhiteSpace(url)) return null;
            string folder = AppSettings.GetDownloadFolder(mediaType);
            Directory.CreateDirectory(folder);
            string template = Path.Combine(folder, "%(artist,uploader)s - %(title)s [%(id)s].%(ext)s");
            var args = new List<string> { "--no-playlist", "--windows-filenames", "--print", "after_move:filepath", "-o", template };

            if (mediaType == "music")
            {
                if (quality.StartsWith("M4A", StringComparison.OrdinalIgnoreCase))
                    args.AddRange(new[] { "-f", "bestaudio[ext=m4a]/bestaudio", "--remux-video", "m4a" });
                else
                {
                    string bitrate = quality.Contains("192") ? "192" : "320";
                    args.AddRange(new[] { "-f", "bestaudio", "-x", "--audio-format", "mp3", "--audio-quality", bitrate + "K" });
                }
            }
            else
            {
                string cap = quality.StartsWith("1080") ? "[height<=1080]" : quality.StartsWith("720") ? "[height<=720]" : "";
                args.AddRange(new[] { "-f", "bestvideo" + cap + "+bestaudio/best" + cap, "--merge-output-format", "mp4" });
            }
            args.Add(url);

            var result = await RunAsync(args.ToArray());
            if (result.ExitCode != 0) return null;
            string[] lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--) if (File.Exists(lines[i].Trim())) return lines[i].Trim();
            return null;
        }

        private static async Task<ProcessResult> RunAsync(string[] args)
        {
            return await Task.Run(() =>
            {
                string cookiesFile = AppSettings.YouTubeCookiesFile;
                if (!string.IsNullOrWhiteSpace(cookiesFile) && File.Exists(cookiesFile))
                {
                    ProcessResult withCookieFile = Execute(BuildCookieFileArgs(args, cookiesFile));
                    if (withCookieFile.ExitCode == 0) return withCookieFile;
                    AppendError(DateTime.Now + Environment.NewLine + "Fallo usando youtube_cookies_file=" + cookiesFile + Environment.NewLine + withCookieFile.Error);
                }

                string browser = AppSettings.YouTubeCookiesBrowser;
                if (!string.IsNullOrWhiteSpace(browser) && !string.Equals(browser.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessResult withCookies = Execute(BuildCookieArgs(args, browser));
                    if (withCookies.ExitCode == 0) return withCookies;
                    AppendError(DateTime.Now + Environment.NewLine + "Reintentando sin cookies porque fallo cookies-from-browser " + browser + "." + Environment.NewLine + withCookies.Error);
                }

                ProcessResult cleanResult = Execute(BuildCleanArgs(args));
                if (cleanResult.ExitCode == 0) return cleanResult;

                // Self-healing: si falla por 403 o desactualización, intentar auto-actualizar yt-dlp y reintentar
                if (LooksLikeUpdateNeeded(cleanResult.Error))
                {
                    AppendError(DateTime.Now + Environment.NewLine + "Intentando auto-actualizar yt-dlp (-U) tras fallo: " + cleanResult.Error);
                    Execute(new[] { "-U" });
                    return Execute(BuildCleanArgs(args));
                }

                return cleanResult;
            });
        }

        private static string[] BuildCleanArgs(string[] args)
        {
            var list = new List<string>();
            list.Add("--no-cookies");
            list.AddRange(args);
            return list.ToArray();
        }

        private static bool LooksLikeUpdateNeeded(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return false;
            return error.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("SABR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("outdated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("update", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ProcessResult Execute(string[] args)
        {
            var status = EnsureYtDlpAvailable();
            if (!status.Available)
            {
                string err = "ERROR: Complemento yt-dlp.exe no disponible. " + status.StatusMessage;
                AppendError(err);
                return new ProcessResult { ExitCode = -1, Output = "", Error = err };
            }

            var psi = new ProcessStartInfo
            {
                FileName = status.Path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.Arguments = JoinArguments(args);
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) AppendError(DateTime.Now + Environment.NewLine + error);
                return new ProcessResult { ExitCode = process.ExitCode, Output = output, Error = error };
            }
        }

        private static string[] BuildCookieArgs(string[] args, string browser)
        {
            var list = new List<string>();
            list.Add("--cookies-from-browser");
            list.Add(browser.Trim());
            list.AddRange(args);
            return list.ToArray();
        }

        private static string[] BuildCookieFileArgs(string[] args, string cookiesFile)
        {
            var list = new List<string>();
            list.Add("--cookies");
            list.Add(cookiesFile);
            list.AddRange(args);
            return list.ToArray();
        }

        private static bool LooksLikeCookieProblem(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return false;
            return error.IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("cookies", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("chrome", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("edge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("firefox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("dpapi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("decrypt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("keyring", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("sqlite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToString(Dictionary<string, object> value, string key) { object raw; return value.TryGetValue(key, out raw) && raw != null ? raw.ToString() : ""; }
        private static int ToInt(Dictionary<string, object> value, string key) { int parsed; return int.TryParse(ToString(value, key), out parsed) ? parsed : 0; }
        private static long ToLong(Dictionary<string, object> value, string key) { long parsed; return long.TryParse(ToString(value, key), out parsed) ? parsed : 0; }
        private static void AppendError(string error) { try { File.AppendAllText(ErrorLogPath, error + Environment.NewLine, Encoding.UTF8); } catch { } }
        private static string JoinArguments(string[] args)
        {
            var joined = new StringBuilder();
            foreach (string arg in args)
            {
                if (joined.Length > 0) joined.Append(' ');
                joined.Append('"').Append((arg ?? "").Replace("\"", "\\\"")).Append('"');
            }
            return joined.ToString();
        }
        private class ProcessResult { public int ExitCode; public string Output; public string Error; }
    }
}
