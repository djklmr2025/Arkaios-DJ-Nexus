using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ArkaiosDJAssistant
{
    public static class AppSettings
    {
        private static string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

        public static string VdjHistoryFolder { get; set; }
        public static string VdjDatabaseFile { get; set; }
        public static string VdjExecutableFile { get; set; }
        public static bool EnableTransparency { get; set; }
        public static bool ShowAdvancedTabs { get; set; }
        public static string PreviewAudioDevice { get; set; }
        public static string YouTubeCookiesBrowser { get; set; }
        public static string YouTubeCookiesFile { get; set; }
        public static string YtDlpCustomPath { get; set; }
        public static List<string> AllowedFolders { get; set; }

        public static void AutoDetectVirtualDjPaths()
        {
            if (!string.IsNullOrWhiteSpace(VdjDatabaseFile) && File.Exists(VdjDatabaseFile))
                return;

            foreach (string baseFolder in GetVirtualDjBaseCandidates())
            {
                string database = Path.Combine(baseFolder, "database.xml");
                string history = Path.Combine(baseFolder, "History");
                if (File.Exists(database))
                {
                    VdjDatabaseFile = database;
                    VdjHistoryFolder = Directory.Exists(history) ? history : baseFolder;
                    return;
                }
            }
        }

        private static IEnumerable<string> GetVirtualDjBaseCandidates()
        {
            var candidates = new List<string>();
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrWhiteSpace(documents))
            {
                candidates.Add(Path.Combine(documents, "VirtualDJ"));
                candidates.Add(Path.Combine(documents, "VirtualDJ", "Database"));
            }

            if (!string.IsNullOrWhiteSpace(profile))
            {
                candidates.Add(Path.Combine(profile, "Documents", "VirtualDJ"));
                candidates.Add(Path.Combine(profile, "Documentos", "VirtualDJ"));
                candidates.Add(Path.Combine(profile, "OneDrive", "Documents", "VirtualDJ"));
                candidates.Add(Path.Combine(profile, "OneDrive", "Documentos", "VirtualDJ"));
            }

            return candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static string MediaLibraryRoot
        {
            get
            {
                foreach (string folder in AllowedFolders)
                {
                    if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                    {
                        string leaf = new DirectoryInfo(folder).Name;
                        if (string.Equals(leaf, "music", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leaf, "musica", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leaf, "música", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leaf, "video", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(leaf, "karaoke", StringComparison.OrdinalIgnoreCase))
                        {
                            DirectoryInfo parent = Directory.GetParent(folder);
                            if (parent != null) return parent.FullName;
                        }
                        return folder;
                    }
                }

                if (!string.IsNullOrWhiteSpace(VdjDatabaseFile))
                {
                    string virtualDjFolder = Path.GetDirectoryName(VdjDatabaseFile);
                    if (!string.IsNullOrWhiteSpace(virtualDjFolder) && Directory.Exists(virtualDjFolder))
                        return virtualDjFolder;
                }

                string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                return Path.Combine(music, "VirtualDJ_Downloads");
            }
        }

        public static string GetDownloadFolder(string mediaType)
        {
            if (string.Equals(mediaType, "karaoke", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "KARAOKES");
            if (string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        }

        static AppSettings()
        {
            VdjHistoryFolder = "";
            VdjDatabaseFile = "";
            VdjExecutableFile = "";
            EnableTransparency = false;
            ShowAdvancedTabs = false;
            PreviewAudioDevice = "Windows default";
            YouTubeCookiesBrowser = "";
            YouTubeCookiesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "youtube-cookies.txt");
            YtDlpCustomPath = "";
            AllowedFolders = new List<string>();
        }

        public static void Load()
        {
            if (File.Exists(settingsFile))
            {
                var lines = File.ReadAllLines(settingsFile);
                if (lines.Length >= 2)
                {
                    VdjHistoryFolder = lines[0];
                    VdjDatabaseFile = lines[1];
                }
                if (lines.Length >= 3)
                {
                    EnableTransparency = lines[2] == "1";
                }
                if (lines.Length >= 4)
                {
                    VdjExecutableFile = lines[3];
                }
                if (lines.Length >= 5)
                {
                    for (int i = 4; i < lines.Length; i++)
                    {
                        string line = lines[i] ?? "";
                        if (line.StartsWith("youtube_cookies_browser=", StringComparison.OrdinalIgnoreCase))
                        {
                            YouTubeCookiesBrowser = line.Substring("youtube_cookies_browser=".Length).Trim();
                        }
                        else if (line.StartsWith("youtube_cookies_file=", StringComparison.OrdinalIgnoreCase))
                        {
                            YouTubeCookiesFile = line.Substring("youtube_cookies_file=".Length).Trim();
                        }
                        else if (line.StartsWith("ytdlp_path=", StringComparison.OrdinalIgnoreCase))
                        {
                            YtDlpCustomPath = line.Substring("ytdlp_path=".Length).Trim();
                        }
                        else if (line.StartsWith("show_advanced_tabs=", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowAdvancedTabs = line.Substring("show_advanced_tabs=".Length).Trim() == "1";
                        }
                        else if (line.StartsWith("preview_audio_device=", StringComparison.OrdinalIgnoreCase))
                        {
                            PreviewAudioDevice = line.Substring("preview_audio_device=".Length).Trim();
                        }
                        else if (!string.IsNullOrWhiteSpace(line))
                            AllowedFolders.Add(line);
                    }
                }
            }

            AutoDetectVirtualDjPaths();
        }

        public static void Save()
        {
            List<string> lines = new List<string>();
            lines.Add(VdjHistoryFolder);
            lines.Add(VdjDatabaseFile);
            lines.Add(EnableTransparency ? "1" : "0");
            lines.Add(VdjExecutableFile ?? "");
            lines.AddRange(AllowedFolders);
            lines.Add("youtube_cookies_browser=" + (YouTubeCookiesBrowser ?? ""));
            lines.Add("youtube_cookies_file=" + (YouTubeCookiesFile ?? ""));
            lines.Add("ytdlp_path=" + (YtDlpCustomPath ?? ""));
            lines.Add("show_advanced_tabs=" + (ShowAdvancedTabs ? "1" : "0"));
            lines.Add("preview_audio_device=" + (PreviewAudioDevice ?? ""));
            File.WriteAllLines(settingsFile, lines.ToArray());
        }
        
        public static bool IsConfigured()
        {
            return Directory.Exists(VdjHistoryFolder) && File.Exists(VdjDatabaseFile);
        }
    }
}
