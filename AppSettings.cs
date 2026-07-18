using System;
using System.IO;
using System.Collections.Generic;

namespace ArkaiosDJAssistant
{
    public static class AppSettings
    {
        private static string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

        public static string VdjHistoryFolder { get; set; }
        public static string VdjDatabaseFile { get; set; }
        public static string VdjExecutableFile { get; set; }
        public static bool EnableTransparency { get; set; }
        public static List<string> AllowedFolders { get; set; }

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
            string subfolder = string.Equals(mediaType, "karaoke", StringComparison.OrdinalIgnoreCase) ? "Karaoke" :
                               string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase) ? "Video" : "Music";
            return Path.Combine(MediaLibraryRoot, subfolder);
        }

        static AppSettings()
        {
            VdjHistoryFolder = "";
            VdjDatabaseFile = "";
            VdjExecutableFile = "";
            EnableTransparency = false;
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
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                            AllowedFolders.Add(lines[i]);
                    }
                }
            }
        }

        public static void Save()
        {
            List<string> lines = new List<string>();
            lines.Add(VdjHistoryFolder);
            lines.Add(VdjDatabaseFile);
            lines.Add(EnableTransparency ? "1" : "0");
            lines.Add(VdjExecutableFile ?? "");
            lines.AddRange(AllowedFolders);
            File.WriteAllLines(settingsFile, lines.ToArray());
        }
        
        public static bool IsConfigured()
        {
            return Directory.Exists(VdjHistoryFolder) && File.Exists(VdjDatabaseFile);
        }
    }
}
