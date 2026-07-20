using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkaiosDJAssistant
{
    public class PlaylistExtractResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string Source { get; set; }
        public List<PlaylistExtractItem> Items { get; set; }

        public PlaylistExtractResult()
        {
            Items = new List<PlaylistExtractItem>();
        }
    }

    public class PlaylistExtractItem
    {
        public string Title { get; set; }
        public string Uploader { get; set; }
        public string Url { get; set; }
        public string Duration { get; set; }
        public string Category { get; set; }
        public int DurationSeconds { get; set; }
    }

    public static class PlaylistExtractorClient
    {
        private const string Endpoint = "https://youtube-playlist-extractor.onrender.com/extract";

        public static async Task<PlaylistExtractResult> ExtractAsync(string playlistUrl)
        {
            string normalizedUrl = NormalizeYouTubeUrl(playlistUrl);
            PlaylistExtractResult remote = await ExtractRemoteAsync(normalizedUrl);
            if (remote.Success && remote.Items.Count > 0) return remote;

            PlaylistExtractResult local = await ExtractLocalAsync(normalizedUrl);
            if (local.Success && local.Items.Count > 0) return local;

            return new PlaylistExtractResult
            {
                Success = false,
                Source = "Render/local",
                Error = BuildCombinedError(remote, local)
            };
        }

        private static async Task<PlaylistExtractResult> ExtractRemoteAsync(string playlistUrl)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ForceModernTls();
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    string body = serializer.Serialize(new Dictionary<string, object>
                    {
                        { "url", playlistUrl },
                        { "max_duration_minutes", 10 },
                        { "filter_non_music", true },
                        { "filter_mixes", true }
                    });

                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        client.Headers[HttpRequestHeader.ContentType] = "application/json";
                        string json = client.UploadString(Endpoint, "POST", body);
                        return ParseRemoteJson(json, "Render API");
                    }
                }
                catch (WebException ex)
                {
                    try
                    {
                        if (ex.Response != null)
                        {
                            using (var stream = ex.Response.GetResponseStream())
                            using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                            {
                                PlaylistExtractResult parsed = ParseRemoteJson(reader.ReadToEnd(), "Render API");
                                if (!parsed.Success && string.IsNullOrWhiteSpace(parsed.Error)) parsed.Error = ex.Message;
                                return parsed;
                            }
                        }
                    }
                    catch { }
                    return new PlaylistExtractResult { Success = false, Source = "Render API", Error = ex.Message };
                }
                catch (Exception ex)
                {
                    return new PlaylistExtractResult { Success = false, Source = "Render API", Error = ex.Message };
                }
            });
        }

        private static void ForceModernTls()
        {
            try
            {
                const SecurityProtocolType tls12 = (SecurityProtocolType)3072;
                const SecurityProtocolType tls13 = (SecurityProtocolType)12288;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | tls12 | tls13;
            }
            catch
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; }
                catch { }
            }
        }

        private static string NormalizeYouTubeUrl(string value)
        {
            string url = (value ?? "").Trim();
            if (url.Length == 0) return url;

            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            else if (url.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase)) url = "https://www." + url;
            else if (url.StartsWith("/watch", StringComparison.OrdinalIgnoreCase)) url = "https://www.youtube.com" + url;
            else if (url.StartsWith("watch", StringComparison.OrdinalIgnoreCase)) url = "https://www.youtube.com/" + url;
            else if (url.StartsWith("m/watch", StringComparison.OrdinalIgnoreCase)) url = "https://www.youtube.com/" + url.Substring(2);

            Match list = Regex.Match(url, @"[?&]list=([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);
            if (list.Success) return "https://www.youtube.com/playlist?list=" + list.Groups[1].Value;

            return url;
        }

        private static string BuildCombinedError(PlaylistExtractResult remote, PlaylistExtractResult local)
        {
            string remoteError = remote == null ? "" : remote.Error;
            string localError = local == null ? "" : local.Error;
            if (!string.IsNullOrWhiteSpace(remoteError) && !string.IsNullOrWhiteSpace(localError))
                return "Render API: " + remoteError + " | Fallback local: " + localError;
            if (!string.IsNullOrWhiteSpace(remoteError)) return remoteError;
            if (!string.IsNullOrWhiteSpace(localError)) return localError;
            return "No se pudo extraer la playlist.";
        }

        private static async Task<PlaylistExtractResult> ExtractLocalAsync(string playlistUrl)
        {
            var result = new PlaylistExtractResult { Source = "yt-dlp local" };
            List<YouTubeTrack> tracks = await YouTubeEngine.ExtractPlaylistFlatAsync(playlistUrl, 50);
            foreach (YouTubeTrack track in tracks)
            {
                result.Items.Add(new PlaylistExtractItem
                {
                    Title = track.Title,
                    Uploader = track.Uploader,
                    Url = track.Url,
                    Duration = track.Duration,
                    Category = "local_fallback"
                });
            }
            result.Success = result.Items.Count > 0;
            if (!result.Success) result.Error = "Fallback local sin resultados.";
            return result;
        }

        private static PlaylistExtractResult ParseRemoteJson(string json, string source)
        {
            var result = new PlaylistExtractResult { Source = source };
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Error = "Respuesta vacia.";
                return result;
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = serializer.Deserialize<Dictionary<string, object>>(json);
            if (root == null)
            {
                result.Error = "JSON invalido.";
                return result;
            }

            object successObject;
            result.Success = root.TryGetValue("success", out successObject) && successObject is bool && (bool)successObject;
            result.Error = ToString(root, "error");
            AddItems(result, root, "videos", "track_natural");

            object filterStatsObject;
            if (root.TryGetValue("filter_stats", out filterStatsObject))
            {
                var filterStats = filterStatsObject as Dictionary<string, object>;
                object detailsObject;
                if (filterStats != null && filterStats.TryGetValue("details", out detailsObject))
                {
                    var details = detailsObject as Dictionary<string, object>;
                    if (details != null)
                    {
                        AddItems(result, details, "long_videos_and_over_7m", "video_largo");
                        AddItems(result, details, "mixes", "mix");
                    }
                }
            }

            if (result.Items.Count > 0) result.Success = true;
            return result;
        }

        private static void AddItems(PlaylistExtractResult result, Dictionary<string, object> root, string key, string category)
        {
            object listObject;
            if (!root.TryGetValue(key, out listObject)) return;
            var list = listObject as IEnumerable;
            if (list == null) return;

            foreach (object itemObject in list)
            {
                var item = itemObject as Dictionary<string, object>;
                if (item == null) continue;
                result.Items.Add(new PlaylistExtractItem
                {
                    Title = ToString(item, "title"),
                    Uploader = ToString(item, "uploader"),
                    Url = ToString(item, "url"),
                    Duration = ToString(item, "duration"),
                    DurationSeconds = ToInt(item, "duration_seconds"),
                    Category = category
                });
            }
        }

        private static string ToString(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null ? raw.ToString() : "";
        }

        private static int ToInt(Dictionary<string, object> value, string key)
        {
            int parsed;
            return int.TryParse(ToString(value, key), out parsed) ? parsed : 0;
        }
    }
}
