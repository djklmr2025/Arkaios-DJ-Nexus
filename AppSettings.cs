using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace ArkaiosDJAssistant
{
    public static class AppSettings
    {
        public static string SettingsFilePath
        {
            get
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArkaiosDJ");
                try
                {
                    if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
                }
                catch (Exception ex)
                {
                    DiagLog.Write(ex, "AppSettings.SettingsFilePath CreateDirectory");
                }
                return Path.Combine(appDataFolder, "config.txt");
            }
        }

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

        public class VirtualDjCandidateInfo
        {
            public string BaseFolder { get; set; }
            public string DatabaseFile { get; set; }
            public string HistoryFolder { get; set; }
            public bool ExistsFolder { get; set; }
            public bool ExistsDatabase { get; set; }
            public bool ExistsHistory { get; set; }
            public long DatabaseSizeBytes { get; set; }
            public DateTime DatabaseLastWrite { get; set; }
            public int SongCount { get; set; }
            public int M3uCount { get; set; }
            public string SourceDescription { get; set; }
        }

        public static bool IsUnder(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder)) return false;
            string f = folder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return path.StartsWith(f, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PointsToForeignUser(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(path, @"[\\/]Users[\\/]([^\\/]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string pathUser = match.Groups[1].Value;
                    if (!string.Equals(pathUser, Environment.UserName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static int QuickCountSongs(string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) return 0;
            int count = 0;
            try
            {
                using (var fs = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = XmlReader.Create(fs, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true }))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "Song")
                            count++;
                    }
                }
                return count;
            }
            catch
            {
                // Fallback de respaldo rápido si el XML tuviera caracteres no estándar
                try
                {
                    using (var fs = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.IndexOf("<Song ", StringComparison.OrdinalIgnoreCase) >= 0)
                                count++;
                        }
                    }
                }
                catch { }
            }
            return count;
        }

        public static List<VirtualDjCandidateInfo> GetDetailedCandidates()
        {
            var results = new List<VirtualDjCandidateInfo>();
            var candidateFolders = GetVirtualDjBaseCandidates().ToList();

            foreach (var folder in candidateFolders)
            {
                var info = new VirtualDjCandidateInfo
                {
                    BaseFolder = folder,
                    DatabaseFile = Path.Combine(folder, "database.xml"),
                    HistoryFolder = Path.Combine(folder, "History"),
                    ExistsFolder = Directory.Exists(folder),
                    SourceDescription = "Ruta detectada en sistema"
                };

                if (File.Exists(info.DatabaseFile))
                {
                    info.ExistsDatabase = true;
                    try
                    {
                        var fi = new FileInfo(info.DatabaseFile);
                        info.DatabaseSizeBytes = fi.Length;
                        info.DatabaseLastWrite = fi.LastWriteTime;
                        info.SongCount = QuickCountSongs(info.DatabaseFile);
                    }
                    catch (Exception ex)
                    {
                        DiagLog.Write(ex, $"Leyendo tamaño/canciones de {info.DatabaseFile}");
                    }
                }

                if (Directory.Exists(info.HistoryFolder))
                {
                    info.ExistsHistory = true;
                    try
                    {
                        info.M3uCount = Directory.GetFiles(info.HistoryFolder, "*.*")
                            .Count(f => f.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase));
                    }
                    catch { }
                }

                results.Add(info);
            }

            return results;
        }

        public static void AutoDetectVirtualDjPaths(bool force = false)
        {
            int currentSongs = 0;
            if (!force && !string.IsNullOrWhiteSpace(VdjDatabaseFile) && File.Exists(VdjDatabaseFile))
            {
                currentSongs = QuickCountSongs(VdjDatabaseFile);
                if (currentSongs > 0 && !string.IsNullOrWhiteSpace(VdjHistoryFolder) && Directory.Exists(VdjHistoryFolder))
                {
                    DiagLog.Write($"[AutoDetectVirtualDjPaths] BD actual es válida: '{VdjDatabaseFile}' ({currentSongs} canciones). No se requiere redetección.");
                    return;
                }
            }

            DiagLog.Write($"[AutoDetectVirtualDjPaths] Iniciando escaneo de candidatos (force={force}, currentSongs={currentSongs})...");

            var candidates = GetDetailedCandidates();
            foreach (var c in candidates)
            {
                DiagLog.Write($"  -> Candidato: '{c.BaseFolder}' | DB: '{c.DatabaseFile}' (Existe={c.ExistsDatabase}, Canciones={c.SongCount}, Tamaño={c.DatabaseSizeBytes / 1024} KB, Fecha={c.DatabaseLastWrite}) | Historial: '{c.HistoryFolder}' (Existe={c.ExistsHistory}, M3U={c.M3uCount})");
            }

            // Elegir el mejor candidato:
            // 1. Debe existir database.xml
            // 2. Priorizar candidatos con canciones > 0
            // 3. Ordenar por número de canciones descendente
            // 4. Desempate por fecha de modificación más reciente
            var bestCandidate = candidates
                .Where(c => c.ExistsDatabase)
                .OrderByDescending(c => c.SongCount > 0 ? 1 : 0)
                .ThenByDescending(c => c.SongCount)
                .ThenByDescending(c => c.DatabaseLastWrite)
                .FirstOrDefault();

            if (bestCandidate != null)
            {
                VdjDatabaseFile = bestCandidate.DatabaseFile;
                VdjHistoryFolder = bestCandidate.ExistsHistory ? bestCandidate.HistoryFolder : bestCandidate.BaseFolder;
                DiagLog.Write($"[AutoDetectVirtualDjPaths] GANADOR SELECCIONADO: DB='{VdjDatabaseFile}' ({bestCandidate.SongCount} canciones, {bestCandidate.DatabaseSizeBytes / 1024} KB, Modificado={bestCandidate.DatabaseLastWrite}), Historial='{VdjHistoryFolder}'");
                return;
            }

            // Si ninguno tenía database.xml existente, buscar cualquier carpeta de VirtualDJ que exista
            var existingFolderCandidate = candidates.FirstOrDefault(c => c.ExistsFolder);
            if (existingFolderCandidate != null)
            {
                VdjDatabaseFile = existingFolderCandidate.DatabaseFile;
                VdjHistoryFolder = existingFolderCandidate.ExistsHistory ? existingFolderCandidate.HistoryFolder : Path.Combine(existingFolderCandidate.BaseFolder, "History");
                DiagLog.Write($"[AutoDetectVirtualDjPaths] Ninguna BD existe aún. Usando carpeta existente de VirtualDJ: '{existingFolderCandidate.BaseFolder}'");
                return;
            }

            // Fallback estándar en Documents
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs) || !Directory.Exists(docs))
                docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");

            string defaultVdj = Path.Combine(docs, "VirtualDJ");
            VdjDatabaseFile = Path.Combine(defaultVdj, "database.xml");
            VdjHistoryFolder = Path.Combine(defaultVdj, "History");
            DiagLog.Write($"[AutoDetectVirtualDjPaths] Fallback por defecto: DB='{VdjDatabaseFile}', Historial='{VdjHistoryFolder}'");
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

            // Consultar registro de Windows por si el usuario configuró una ruta personalizada
            try
            {
                string[] regKeys = new[]
                {
                    @"Software\VirtualDJ",
                    @"Software\Atomix Productions\VirtualDJ"
                };

                foreach (var subKey in regKeys)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(subKey))
                    {
                        if (key != null)
                        {
                            string[] valueNames = new[] { "PersonalFolder", "HomeDir", "InstallDir", "UserFolder", "Path" };
                            foreach (var vn in valueNames)
                            {
                                var val = key.GetValue(vn) as string;
                                if (!string.IsNullOrWhiteSpace(val))
                                    candidates.Add(val.Trim());
                            }
                        }
                    }
                }
            }
            catch { }

            // Escaneo de unidades fijas y extraíbles por si VirtualDJ tiene carpeta en disco secundario (D:\VirtualDJ, E:\VirtualDJ, etc.)
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                        {
                            string driveVdj = Path.Combine(drive.RootDirectory.FullName, "VirtualDJ");
                            candidates.Add(driveVdj);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return candidates
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(p => p.TrimEnd('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static string GetVirtualDjPlaylistsFolder()
        {
            if (!string.IsNullOrWhiteSpace(VdjDatabaseFile))
            {
                string dir = Path.GetDirectoryName(VdjDatabaseFile);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    string p = Path.Combine(dir, "Playlists");
                    if (Directory.Exists(p)) return p;
                }
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

            try
            {
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
            catch (Exception ex)
            {
                DiagLog.Write(ex, $"AppSettings.GetDownloadFolder mediaType={mediaType}");
                string userMusic = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                return userMusic;
            }
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

                // NOTA CRÍTICA FIX: NO agregamos automáticamente C:\ARKAIOS\Biblioteca_DJ a AllowedFolders
                // para evitar que en otras PCs se descarte toda la música del usuario de sus discos locales.

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
            catch (Exception ex)
            {
                DiagLog.Write(ex, "AppSettings.EnsureVirtualDjIntegration");
            }
        }

        public static void EnsureATubeCatcherIntegration()
        {
            try
            {
                string targetDir = @"C:\ARKAIOS\Biblioteca_DJ\Musica";
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                Registry.SetValue(@"HKEY_CURRENT_USER\Software\DsNET Corp\aTube Catcher", "SaveDirectory", targetDir);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\aTube Catcher", "SaveDirectory", targetDir);
            }
            catch (Exception ex)
            {
                DiagLog.Write(ex, "AppSettings.EnsureATubeCatcherIntegration");
            }
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
            catch (Exception ex)
            {
                DiagLog.Write(ex, $"AppSettings.WriteVdjFolderXml ({filePath})");
            }
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
            string targetFile = SettingsFilePath;
            string legacyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

            // Migración: si no existe en AppData pero existe el antiguo junto al .exe, copiarlo una vez
            if (!File.Exists(targetFile) && File.Exists(legacyFile))
            {
                try
                {
                    File.Copy(legacyFile, targetFile, true);
                    DiagLog.Write($"[AppSettings.Load] Migrado config.txt inicial desde '{legacyFile}' a '{targetFile}'");
                }
                catch (Exception ex)
                {
                    DiagLog.Write(ex, "AppSettings.Load Migración inicial");
                }
            }

            if (File.Exists(targetFile))
            {
                try
                {
                    var lines = File.ReadAllLines(targetFile, Encoding.UTF8);
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
                                if (Directory.Exists(expandedFolder) && !AllowedFolders.Contains(expandedFolder, StringComparer.OrdinalIgnoreCase))
                                    AllowedFolders.Add(expandedFolder);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagLog.Write(ex, "AppSettings.Load Leyendo líneas");
                }
            }

            // Saneamiento proactivo universal:
            // 1. Si la ruta proviene de otro usuario distinto al actual (ej. '\Users\djklm' en PC de otro usuario).
            // 2. Si la ruta de BD no existe o está en blanco.
            // 3. Si la ruta de Historial no existe o está en blanco.
            // 4. Si la BD existe pero tiene 0 canciones o tamaño < 100 bytes (BD vacía o truncada).
            bool isForeign = PointsToForeignUser(VdjDatabaseFile) || PointsToForeignUser(VdjHistoryFolder);
            bool isMissing = string.IsNullOrWhiteSpace(VdjDatabaseFile) || !File.Exists(VdjDatabaseFile) ||
                             string.IsNullOrWhiteSpace(VdjHistoryFolder) || !Directory.Exists(VdjHistoryFolder);
            bool isEmptyDb = false;
            if (!isMissing && File.Exists(VdjDatabaseFile))
            {
                try
                {
                    var fi = new FileInfo(VdjDatabaseFile);
                    if (fi.Length < 100 || QuickCountSongs(VdjDatabaseFile) == 0)
                    {
                        isEmptyDb = true;
                    }
                }
                catch { }
            }

            bool needsRedetect = isForeign || isMissing || isEmptyDb;
            if (needsRedetect)
            {
                DiagLog.Write($"[AppSettings.Load] Saneamiento activado: foreign={isForeign}, missing={isMissing}, emptyDb={isEmptyDb}. Forzando autodetección...");
            }

            AutoDetectVirtualDjPaths(force: needsRedetect);

            // Guardar si hubo cambios o se redetectó
            if (needsRedetect)
            {
                Save();
            }
        }

        public static void Save()
        {
            try
            {
                string file = SettingsFilePath;
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                List<string> lines = new List<string>();
                lines.Add(VdjHistoryFolder ?? "");
                lines.Add(VdjDatabaseFile ?? "");
                lines.Add(EnableTransparency ? "1" : "0");
                lines.Add(VdjExecutableFile ?? "");
                lines.AddRange(AllowedFolders ?? new List<string>());
                lines.Add("youtube_cookies_browser=" + (YouTubeCookiesBrowser ?? ""));
                lines.Add("youtube_cookies_file=" + (YouTubeCookiesFile ?? ""));
                lines.Add("ytdlp_path=" + (YtDlpCustomPath ?? ""));
                lines.Add("show_advanced_tabs=" + (ShowAdvancedTabs ? "1" : "0"));
                lines.Add("preview_audio_device=" + (PreviewAudioDevice ?? ""));
                File.WriteAllLines(file, lines.ToArray(), Encoding.UTF8);
                DiagLog.Write($"[AppSettings.Save] Configuración guardada en: {file}");
            }
            catch (Exception ex)
            {
                DiagLog.Write(ex, "AppSettings.Save");
            }
        }
        
        public static bool IsConfigured()
        {
            return Directory.Exists(VdjHistoryFolder) && File.Exists(VdjDatabaseFile);
        }
    }
}
