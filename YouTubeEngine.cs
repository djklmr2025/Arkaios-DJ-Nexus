using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

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
        public string AvailableOutputs { get; set; }
        public string MaximumQuality { get { return MaxHeight > 0 ? MaxHeight + "p / audio " + MaxAudioKbps + " kbps" : "audio " + MaxAudioKbps + " kbps"; } }
    }

    public static class YouTubeEngine
    {
        private static readonly string YtDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
        private static readonly string ErrorLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp-errors.log");

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
            if (string.IsNullOrWhiteSpace(query) || !File.Exists(YtDlpPath)) return tracks;

            string suffix = mediaType == "karaoke" ? " karaoke lyrics" : mediaType == "video" ? " official music video" : " official audio";
            string search = "ytsearch" + Math.Max(1, Math.Min(limit, 20)) + ":" + query + suffix;
            var result = await RunAsync(new[] { "--dump-single-json", "--no-warnings", "--no-playlist", search });
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output)) return tracks;

            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.Deserialize<Dictionary<string, object>>(result.Output);
                object entriesObject;
                if (root == null || !root.TryGetValue("entries", out entriesObject)) return tracks;
                var entries = entriesObject as IEnumerable;
                if (entries == null) return tracks;

                foreach (object entryObject in entries)
                {
                    var entry = entryObject as Dictionary<string, object>;
                    if (entry == null) continue;
                    int duration = ToInt(entry, "duration");
                    int maxHeight = 0;
                    int maxAudio = 0;
                    object formatsObject;
                    if (entry.TryGetValue("formats", out formatsObject))
                    {
                        var formats = formatsObject as IEnumerable;
                        if (formats != null) foreach (object formatObject in formats)
                        {
                            var format = formatObject as Dictionary<string, object>;
                            if (format == null) continue;
                            maxHeight = Math.Max(maxHeight, ToInt(format, "height"));
                            maxAudio = Math.Max(maxAudio, ToInt(format, "abr"));
                        }
                    }

                    string url = ToString(entry, "webpage_url");
                    if (string.IsNullOrEmpty(url)) url = ToString(entry, "url");
                    if (string.IsNullOrEmpty(url)) continue;
                    tracks.Add(new YouTubeTrack
                    {
                        Title = ToString(entry, "title"), Url = url, Uploader = ToString(entry, "uploader"),
                        Duration = string.Format("{0}:{1:00}", duration / 60, duration % 60),
                        ViewCount = ToLong(entry, "view_count"), MaxHeight = maxHeight, MaxAudioKbps = maxAudio,
                        AvailableOutputs = mediaType == "music" ? "MP3 / M4A" : "MP4"
                    });
                }
            }
            catch (Exception ex) { AppendError("Search JSON: " + ex); }
            return tracks;
        }

        public static Task<string> DownloadAudioAsync(string url)
        {
            return DownloadAsync(url, "music", "MP3 320 kbps");
        }

        public static async Task<string> DownloadAsync(string url, string mediaType, string quality)
        {
            if (!File.Exists(YtDlpPath) || string.IsNullOrWhiteSpace(url)) return null;
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
                var psi = new ProcessStartInfo { FileName = YtDlpPath, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                psi.Arguments = JoinArguments(args);
                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0) AppendError(DateTime.Now + Environment.NewLine + error);
                    return new ProcessResult { ExitCode = process.ExitCode, Output = output };
                }
            });
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
        private class ProcessResult { public int ExitCode; public string Output; }
    }
}
