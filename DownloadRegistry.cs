using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ArkaiosDJAssistant
{
    public class DownloadRecord
    {
        public string Path { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string Uploader { get; set; }
        public string MediaType { get; set; }
        public DateTime DownloadedAt { get; set; }
    }

    public static class DownloadRegistry
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloaded-hub-tracks.log");
        private static readonly List<DownloadRecord> records = new List<DownloadRecord>();
        private static bool loaded;

        public static void Register(string path, string url, string title, string uploader, string mediaType)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            EnsureLoaded();

            DownloadRecord existing = FindByPath(path);
            if (existing == null)
            {
                existing = new DownloadRecord();
                records.Add(existing);
            }

            existing.Path = path;
            if (!string.IsNullOrWhiteSpace(url)) existing.Url = url;
            else if (existing.Url == null) existing.Url = "";
            if (!string.IsNullOrWhiteSpace(title)) existing.Title = title;
            else if (string.IsNullOrWhiteSpace(existing.Title)) existing.Title = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(uploader)) existing.Uploader = uploader;
            else if (existing.Uploader == null) existing.Uploader = "";
            if (!string.IsNullOrWhiteSpace(mediaType)) existing.MediaType = mediaType;
            else if (existing.MediaType == null) existing.MediaType = "";
            existing.DownloadedAt = DateTime.Now;
            Save();
        }

        public static DownloadRecord Find(string url, string title, string uploader)
        {
            EnsureLoaded();
            string cleanUrl = NormalizeUrl(url);
            if (cleanUrl.Length > 0)
            {
                DownloadRecord byUrl = records.LastOrDefault(r => NormalizeUrl(r.Url) == cleanUrl && File.Exists(r.Path));
                if (byUrl != null) return byUrl;
            }

            string key = BuildKey(title, uploader);
            if (key.Length == 0) return null;
            return records.LastOrDefault(r => BuildKey(r.Title, r.Uploader) == key && File.Exists(r.Path));
        }

        public static List<string> ExistingPaths()
        {
            EnsureLoaded();
            return records.Select(r => r.Path).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<DownloadRecord> ExistingRecords()
        {
            EnsureLoaded();
            return records
                .Where(r => !string.IsNullOrWhiteSpace(r.Path) && File.Exists(r.Path))
                .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(r => r.DownloadedAt).First())
                .OrderByDescending(r => r.DownloadedAt)
                .ToList();
        }

        private static DownloadRecord FindByPath(string path)
        {
            return records.LastOrDefault(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            if (!File.Exists(LogPath)) return;

            foreach (string line in File.ReadAllLines(LogPath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("downloaded_at\t")) continue;
                string[] parts = line.Split('\t');
                if (parts.Length < 6) continue;
                DateTime when;
                DateTime.TryParse(parts[0], out when);
                records.Add(new DownloadRecord
                {
                    DownloadedAt = when,
                    MediaType = parts[1],
                    Path = parts[2],
                    Url = parts[3],
                    Title = parts[4],
                    Uploader = parts[5]
                });
            }
        }

        private static void Save()
        {
            var lines = new List<string> { "downloaded_at\tmedia_type\tpath\turl\ttitle\tuploader" };
            foreach (DownloadRecord record in records.Where(r => !string.IsNullOrWhiteSpace(r.Path)))
            {
                lines.Add(string.Join("\t", new[]
                {
                    record.DownloadedAt.ToString("o"),
                    CleanField(record.MediaType),
                    CleanField(record.Path),
                    CleanField(record.Url),
                    CleanField(record.Title),
                    CleanField(record.Uploader)
                }));
            }
            File.WriteAllLines(LogPath, lines.ToArray(), Encoding.UTF8);
        }

        private static string CleanField(string value)
        {
            return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string NormalizeUrl(string value)
        {
            string url = (value ?? "").Trim();
            Match id = Regex.Match(url, @"(?:v=|youtu\.be/)([A-Za-z0-9_-]{6,})", RegexOptions.IgnoreCase);
            return id.Success ? id.Groups[1].Value : url.ToLowerInvariant();
        }

        private static string BuildKey(string title, string uploader)
        {
            string combined = (title ?? "") + "|" + (uploader ?? "");
            combined = Regex.Replace(combined.ToLowerInvariant(), @"\s+", " ").Trim();
            combined = Regex.Replace(combined, @"[^\p{L}\p{Nd}]+", "");
            return combined;
        }
    }
}
