using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkaiosDJAssistant
{
    public class MetadataNameSuggestion
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public int Confidence { get; set; }
    }

    public static class MetadataNameResolver
    {
        private static readonly string TagEditorRoot = @"C:\ARKAIOS\tageditor-master";
        private static readonly object tagEditorSearchLock = new object();
        private static string cachedTagEditorCli;
        private static string incompatibleTagEditorCli;
        private static bool tagEditorSearched;

        public static async Task<MetadataNameSuggestion> ResolveAsync(string filePath, string fallbackQuery, string mediaType)
        {
            MetadataNameSuggestion embedded = await TryFromTagEditorAsync(filePath);
            if (IsUsable(embedded)) return embedded;

            MetadataNameSuggestion musicBrainz = await TryFromMusicBrainzAsync(fallbackQuery);
            if (IsUsable(musicBrainz)) return musicBrainz;

            string youtube = await YouTubeEngine.SuggestCanonicalQueryAsync(fallbackQuery, mediaType);
            if (!string.IsNullOrWhiteSpace(youtube))
            {
                return new MetadataNameSuggestion
                {
                    Name = youtube,
                    Source = "YouTube",
                    Confidence = 82
                };
            }

            return null;
        }

        public static bool IsTagEditorCliAvailable()
        {
            return !string.IsNullOrWhiteSpace(FindTagEditorCli());
        }

        public static string GetTagEditorStatus()
        {
            string cli = FindTagEditorCli();
            if (!string.IsNullOrWhiteSpace(cli)) return "TagEditor CLI activo: " + cli;
            if (!string.IsNullOrWhiteSpace(incompatibleTagEditorCli)) return "TagEditor CLI incompatible con este Windows: " + incompatibleTagEditorCli;
            return "TagEditor CLI no encontrado: coloca tageditor-cli.exe x64 en tools\\tageditor-cli.exe o compila C:\\ARKAIOS\\tageditor-master para x64.";
        }

        private static bool IsUsable(MetadataNameSuggestion suggestion)
        {
            return suggestion != null && !string.IsNullOrWhiteSpace(suggestion.Name) && suggestion.Name.Trim().Length >= 4;
        }

        private static async Task<MetadataNameSuggestion> TryFromTagEditorAsync(string filePath)
        {
            string cli = FindTagEditorCli();
            if (string.IsNullOrWhiteSpace(cli) || !File.Exists(filePath)) return null;

            return await Task.Run(() =>
            {
                try
                {
                    string output = RunProcess(cli, "get title artist album --file " + Quote(filePath), 8000);
                    if (string.IsNullOrWhiteSpace(output)) return null;

                    string title = ExtractField(output, "title");
                    string artist = ExtractField(output, "artist");
                    if (string.IsNullOrWhiteSpace(title)) return null;

                    string name = string.IsNullOrWhiteSpace(artist) ? title : artist + " - " + title;
                    return new MetadataNameSuggestion
                    {
                        Name = name,
                        Source = "TagEditor tags",
                        Confidence = string.IsNullOrWhiteSpace(artist) ? 86 : 96
                    };
                }
                catch { return null; }
            });
        }

        private static async Task<MetadataNameSuggestion> TryFromMusicBrainzAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3) return null;

            return await Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    string url = "https://musicbrainz.org/ws/2/recording?fmt=json&limit=1&query=" + Uri.EscapeDataString(query);
                    using (var client = new TimeoutWebClient(9000))
                    {
                        client.Headers[HttpRequestHeader.UserAgent] = "ARKAIOS-DJ-Assistant/1.0 (local metadata resolver)";
                        string json = client.DownloadString(url);
                        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                        var root = serializer.Deserialize<Dictionary<string, object>>(json);
                        object recordingsObj;
                        if (!root.TryGetValue("recordings", out recordingsObj)) return null;
                        var recordings = ToObjectList(recordingsObj);
                        if (recordings == null || recordings.Length == 0) return null;
                        var recording = recordings[0] as Dictionary<string, object>;
                        if (recording == null) return null;

                        string title = GetString(recording, "title");
                        string artist = ExtractArtistCredit(recording);
                        if (string.IsNullOrWhiteSpace(title)) return null;

                        return new MetadataNameSuggestion
                        {
                            Name = string.IsNullOrWhiteSpace(artist) ? title : artist + " - " + title,
                            Source = "MusicBrainz",
                            Confidence = string.IsNullOrWhiteSpace(artist) ? 78 : 90
                        };
                    }
                }
                catch { return null; }
            });
        }

        private static string ExtractArtistCredit(Dictionary<string, object> recording)
        {
            object creditObj;
            if (!recording.TryGetValue("artist-credit", out creditObj)) return null;
            var credits = ToObjectList(creditObj);
            if (credits == null || credits.Length == 0) return null;
            var names = new List<string>();
            foreach (object entry in credits)
            {
                var dict = entry as Dictionary<string, object>;
                if (dict == null) continue;
                string name = GetString(dict, "name");
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
            return string.Join(", ", names.ToArray());
        }

        private static object[] ToObjectList(object value)
        {
            if (value == null) return null;
            var array = value as object[];
            if (array != null) return array;
            var list = value as ArrayList;
            if (list != null) return list.Cast<object>().ToArray();
            return null;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object value;
            return dict.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static string FindTagEditorCli()
        {
            lock (tagEditorSearchLock)
            {
                if (tagEditorSearched) return cachedTagEditorCli;
                tagEditorSearched = true;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] direct =
                {
                    Path.Combine(baseDir, "tools", "tageditor-cli.exe"),
                    Path.Combine(baseDir, "tageditor-cli.exe"),
                    Path.Combine(TagEditorRoot, "tageditor-cli.exe"),
                    Path.Combine(TagEditorRoot, "bin", "tageditor-cli.exe")
                };

                foreach (string path in direct)
                {
                    if (File.Exists(path) && IsCompatibleWindowsExe(path))
                    {
                        cachedTagEditorCli = path;
                        return cachedTagEditorCli;
                    }
                    if (File.Exists(path)) incompatibleTagEditorCli = path;
                }

                try
                {
                    if (Directory.Exists(TagEditorRoot))
                    {
                        foreach (string path in Directory.GetFiles(TagEditorRoot, "tageditor-cli.exe", SearchOption.AllDirectories))
                        {
                            if (IsCompatibleWindowsExe(path))
                            {
                                cachedTagEditorCli = path;
                                break;
                            }
                            if (string.IsNullOrWhiteSpace(incompatibleTagEditorCli)) incompatibleTagEditorCli = path;
                        }
                    }
                }
                catch { cachedTagEditorCli = null; }

                return cachedTagEditorCli;
            }
        }

        private static bool IsCompatibleWindowsExe(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 0x40 || bytes[0] != 'M' || bytes[1] != 'Z') return false;
                int pe = BitConverter.ToInt32(bytes, 0x3C);
                if (pe < 0 || pe + 6 >= bytes.Length) return false;
                ushort machine = BitConverter.ToUInt16(bytes, pe + 4);
                return machine == 0x8664 || machine == 0x014c;
            }
            catch { return false; }
        }

        private static string ExtractField(string output, string field)
        {
            string pattern = @"(?im)^\s*" + Regex.Escape(field) + @"\s*[:=]\s*(.+?)\s*$";
            Match match = Regex.Match(output, pattern);
            if (match.Success) return match.Groups[1].Value.Trim();

            pattern = @"(?im)^\s*" + Regex.Escape(ToTitle(field)) + @"\s*[:=]\s*(.+?)\s*$";
            match = Regex.Match(output, pattern);
            if (match.Success) return match.Groups[1].Value.Trim();

            return null;
        }

        private static string ToTitle(string value)
        {
            return string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
        }

        private static string RunProcess(string fileName, string arguments, int timeoutMs)
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(start))
            {
                if (process == null) return null;
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    return null;
                }
                return output + Environment.NewLine + error;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int timeoutMs;
            public TimeoutWebClient(int timeoutMs) { this.timeoutMs = timeoutMs; }
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                request.Timeout = timeoutMs;
                return request;
            }
        }
    }
}
