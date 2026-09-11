using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public static void AutoDetectVirtualDjPaths(bool force = false)
        {
            if (!force && !string.IsNullOrWhiteSpace(VdjDatabaseFile) && File.Exists(VdjDatabaseFile) &&
                !string.IsNullOrWhiteSpace(VdjHistoryFolder) && Directory.Exists(VdjHistoryFolder))
            {
                return;
            }

            string bestExistingBase = null;
            foreach (string baseFolder in GetVirtualDjBaseCandidates())
            {
                if (Directory.Exists(baseFolder))
                {
                    string database = Path.Combine(baseFolder, "database.xml");
                    string history = Path.Combine(baseFolder, "History");
                    if (File.Exists(database))
                    {
                        VdjDatabaseFile = database;
                        VdjHistoryFolder = Directory.Exists(history) ? history : baseFolder;
                        return;
                    }
                    if (Directory.Exists(history))
                    {
                        VdjHistoryFolder = history;
                        VdjDatabaseFile = database;
                        return;
                    }
                    if (bestExistingBase == null)
                        bestExistingBase = baseFolder;
                }
            }

            if (bestExistingBase != null)
            {
                VdjDatabaseFile = Path.Combine(bestExistingBase, "database.xml");
                VdjHistoryFolder = Path.Combine(bestExistingBase, "History");
                return;
            }

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs) || !Directory.Exists(docs))
                docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");

            string defaultVdj = Path.Combine(docs, "VirtualDJ");
            VdjDatabaseFile = Path.Combine(defaultVdj, "database.xml");
            VdjHistoryFolder = Path.Combine(defaultVdj, "History");
        }

        public static IEnumerable<string> GetVirtualDjBaseCandidates()
        {
            var candidates = new List<string>();
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

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
                candidates.Add(Path.Combine(profile, "VirtualDJ"));
            }

            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "VirtualDJ"));
            }

            if (!string.IsNullOrWhiteSpace(appData))
            {
                candidates.Add(Path.Combine(appData, "VirtualDJ"));
            }

            return candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static string GetVirtualDjPlaylistsFolder()
        {
            if (!string.IsNullOrWhiteSpace(VdjDatabaseFile))
            {
                string dir = Path.GetDirectoryName(VdjDatabaseFile);
                if (!string.IsNullOrWhiteSpace(dir))
                    return Path.Combine(dir, "Playlists");
            }
            foreach (string baseDir in GetVirtualDjBaseCandidates())
            {
                string playlists = Path.Combine(baseDir, "Playlists");
                if (Directory.Exists(playlists)) return playlists;
            }
            string defaultDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(defaultDocs, "VirtualDJ", "Playlists");
        }

        public static string GetVirtualDjSettingsXmlPath()
        {
            if (!string.IsNullOrWhiteSpace(VdjDatabaseFile))
            {
                string dir = Path.GetDirectoryName(VdjDatabaseFile);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    string s = Path.Combine(dir, "settings.xml");
                    if (File.Exists(s)) return s;
                }
            }
            foreach (string baseDir in GetVirtualDjBaseCandidates())
            {
                string s = Path.Combine(baseDir, "settings.xml");
                if (File.Exists(s)) return s;
            }
            return "";
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
            EnsureVirtualDjIntegration();
            string root = @"C:\ARKAIOS\Biblioteca_DJ";

            if (string.Equals(mediaType, "karaoke", StringComparison.OrdinalIgnoreCase))
            {
                string path = Path.Combine(root, "Karaokes");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
            if (string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase))
            {
                string path = Path.Combine(root, "Videos");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }

            string musicPath = Path.Combine(root, "Musica");
            if (!Directory.Exists(musicPath)) Directory.CreateDirectory(musicPath);
            return musicPath;
        }

        public static void EnsureVirtualDjIntegration()
        {
            try
            {
                string root = @"C:\ARKAIOS\Biblioteca_DJ";
                string musicDir = Path.Combine(root, "Musica");
                string videoDir = Path.Combine(root, "Videos");
                string karaokeDir = Path.Combine(root, "Karaokes");

                if (!Directory.Exists(root)) Directory.CreateDirectory(root);
                if (!Directory.Exists(musicDir)) Directory.CreateDirectory(musicDir);
                if (!Directory.Exists(videoDir)) Directory.CreateDirectory(videoDir);
                if (!Directory.Exists(karaokeDir)) Directory.CreateDirectory(karaokeDir);

                if (!AllowedFolders.Contains(root, StringComparer.OrdinalIgnoreCase)) AllowedFolders.Add(root);
                if (!AllowedFolders.Contains(musicDir, StringComparer.OrdinalIgnoreCase)) AllowedFolders.Add(musicDir);
                if (!AllowedFolders.Contains(videoDir, StringComparer.OrdinalIgnoreCase)) AllowedFolders.Add(videoDir);
                if (!AllowedFolders.Contains(karaokeDir, StringComparer.OrdinalIgnoreCase)) AllowedFolders.Add(karaokeDir);

                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                List<string> candidateVdjUserDirs = new List<string>
                {
                    Path.Combine(documents, "VirtualDJ"),
                    Path.Combine(userProfile, "Documents", "VirtualDJ"),
                    Path.Combine(userProfile, "Documentos", "VirtualDJ"),
                    Path.Combine(userProfile, "OneDrive", "Documents", "VirtualDJ"),
                    Path.Combine(userProfile, "OneDrive", "Documentos", "VirtualDJ")
                };

                foreach (string vdjUserDir in candidateVdjUserDirs)
                {
                    if (Directory.Exists(vdjUserDir))
                    {
                        string vdjFoldersDir = Path.Combine(vdjUserDir, "Folders");
                        string vdjPlaylistsDir = Path.Combine(vdjUserDir, "Playlists");

                        if (!Directory.Exists(vdjFoldersDir)) Directory.CreateDirectory(vdjFoldersDir);
                        if (!Directory.Exists(vdjPlaylistsDir)) Directory.CreateDirectory(vdjPlaylistsDir);

                        WriteVdjFolderXml(Path.Combine(vdjFoldersDir, "ARKAIOS_MUSICA.vdjfolder"), "ARKAIOS MUSICA", musicDir, "type = \"audio\" or extension = \"mp3\" or extension = \"wav\" or extension = \"m4a\" or extension = \"flac\"");
                        WriteVdjFolderXml(Path.Combine(vdjFoldersDir, "ARKAIOS_VIDEOS.vdjfolder"), "ARKAIOS VIDEOS", videoDir, "type = \"video\" or extension = \"mp4\" or extension = \"avi\" or extension = \"vob\" or extension = \"mkv\"");
                        WriteVdjFolderXml(Path.Combine(vdjFoldersDir, "ARKAIOS_KARAOKES.vdjfolder"), "ARKAIOS KARAOKES", karaokeDir, "type = \"karaoke\" or extension = \"cdg\" or extension = \"kfn\" or extension = \"zip\"");
                    }
                }

                EnsureATubeCatcherIntegration();
            }
            catch {}
        }

        public static void EnsureATubeCatcherIntegration()
        {
            try
            {
                string targetDir = @"C:\ARKAIOS\Biblioteca_DJ\Musica";
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\DsNET Corp\aTube Catcher", "SaveDirectory", targetDir);
                Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Software\aTube Catcher", "SaveDirectory", targetDir);
            }
            catch {}
        }

        private static void WriteVdjFolderXml(string filePath, string folderName, string folderPath, string filterQuery)
        {
            try
            {
                string xmlContent = string.Format(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
                    "<VirtualFolder name=\"{0}\">\r\n" +
                    "  <Filter>folder \"{1}\" or ({2})</Filter>\r\n" +
                    "</VirtualFolder>", folderName, folderPath, filterQuery);

                File.WriteAllText(filePath, xmlContent, Encoding.UTF8);
            }
            catch {}
        }

        static AppSettings()
        {
            VdjHistoryFolder = "";
            VdjDatabaseFile = "";
            VdjExecutableFile = "";
            EnableTransparency = false;
            ShowAdvancedTabs = true;
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
                    VdjHistoryFolder = Environment.ExpandEnvironmentVariables(lines[0] ?? "").Trim();
                    VdjDatabaseFile = Environment.ExpandEnvironmentVariables(lines[1] ?? "").Trim();
                }
                if (lines.Length >= 3)
                {
                    EnableTransparency = lines[2] == "1";
                }
                if (lines.Length >= 4)
                {
                    VdjExecutableFile = Environment.ExpandEnvironmentVariables(lines[3] ?? "").Trim();
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
                            YouTubeCookiesFile = Environment.ExpandEnvironmentVariables(line.Substring("youtube_cookies_file=".Length).Trim());
                        }
                        else if (line.StartsWith("ytdlp_path=", StringComparison.OrdinalIgnoreCase))
                        {
                            YtDlpCustomPath = Environment.ExpandEnvironmentVariables(line.Substring("ytdlp_path=".Length).Trim());
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
                        {
                            string expandedFolder = Environment.ExpandEnvironmentVariables(line).Trim();
                            if (Directory.Exists(expandedFolder) && !AllowedFolders.Contains(expandedFolder))
                                AllowedFolders.Add(expandedFolder);
                        }
                    }
                }
            }

            // Saneamiento proactivo: si el path proviene de la máquina de otro usuario (ej. 'djklm' en PC ajena) o no existe físicamente
            bool isForeignOrBroken = (VdjDatabaseFile != null && VdjDatabaseFile.IndexOf(@"\Users\djklm", StringComparison.OrdinalIgnoreCase) >= 0 && !string.Equals(Environment.UserName, "djklm", StringComparison.OrdinalIgnoreCase)) ||
                                    (VdjHistoryFolder != null && VdjHistoryFolder.IndexOf(@"\Users\djklm", StringComparison.OrdinalIgnoreCase) >= 0 && !string.Equals(Environment.UserName, "djklm", StringComparison.OrdinalIgnoreCase)) ||
                                    (string.IsNullOrWhiteSpace(VdjDatabaseFile) || !File.Exists(VdjDatabaseFile)) ||
                                    (string.IsNullOrWhiteSpace(VdjHistoryFolder) || !Directory.Exists(VdjHistoryFolder));

            AutoDetectVirtualDjPaths(force: isForeignOrBroken);
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
