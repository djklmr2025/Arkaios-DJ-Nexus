using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace ArkaiosDJAssistant
{
    public static class HistoryEngine
    {
        // Dictionary mapping from a track's Path/Title to a HashSet of tracks that followed it
        public static Dictionary<string, HashSet<string>> TrackTransitions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static void ScanHistory(string vdjFolder)
        {
            try
            {
                TrackTransitions.Clear();
                string playlistsDir = Path.Combine(vdjFolder, "Playlists");
                string historyDir = Path.Combine(vdjFolder, "History");

                int scannedCount = 0;
                if (Directory.Exists(playlistsDir))
                    scannedCount += ScanDirectory(playlistsDir);
                
                if (Directory.Exists(historyDir))
                    scannedCount += ScanDirectory(historyDir);

                DiagLog.Write($"[HistoryEngine] Escaneo completado en '{vdjFolder}'. Archivos procesados: {scannedCount}, Transiciones aprendidas: {TrackTransitions.Count}");
            }
            catch (Exception ex)
            {
                DiagLog.Write(ex, "HistoryEngine.ScanHistory");
            }
        }

        private static int ScanDirectory(string path)
        {
            int count = 0;
            try
            {
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".vdjfolder", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    if (file.EndsWith(".vdjfolder", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseVdjFolder(file);
                    }
                    else
                    {
                        ParseM3U(file);
                    }
                    count++;
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write(ex, $"HistoryEngine.ScanDirectory ({path})");
            }
            return count;
        }

        private static void ParseVdjFolder(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = XmlReader.Create(fs, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true }))
                {
                    string previousTrack = null;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && (reader.Name == "Song" || reader.Name == "File"))
                        {
                            string songPath = reader.GetAttribute("FilePath") ?? reader.GetAttribute("Path");
                            if (!string.IsNullOrWhiteSpace(songPath))
                            {
                                string currentTrack = Path.GetFileNameWithoutExtension(songPath);
                                if (previousTrack != null && currentTrack != null)
                                {
                                    if (!TrackTransitions.ContainsKey(previousTrack))
                                    {
                                        TrackTransitions[previousTrack] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    }
                                    TrackTransitions[previousTrack].Add(currentTrack);
                                }
                                previousTrack = currentTrack;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write(ex, $"HistoryEngine.ParseVdjFolder ({filePath})");
            }
        }

        private static void ParseM3U(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                string previousTrack = null;

                foreach (var line in lines)
                {
                    string currentLine = line.Trim();
                    if (string.IsNullOrEmpty(currentLine) || currentLine.StartsWith("#")) continue;

                    string currentTrack = Path.GetFileNameWithoutExtension(currentLine);

                    if (previousTrack != null && currentTrack != null)
                    {
                        if (!TrackTransitions.ContainsKey(previousTrack))
                        {
                            TrackTransitions[previousTrack] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        TrackTransitions[previousTrack].Add(currentTrack);
                    }
                    previousTrack = currentTrack;
                }
            }
            catch (Exception ex)
            {
                DiagLog.Write(ex, $"HistoryEngine.ParseM3U ({filePath})");
            }
        }

        public static bool HasHistoricalTransition(string currentTrackPath, string recommendedTrackPath)
        {
            if (string.IsNullOrEmpty(currentTrackPath) || string.IsNullOrEmpty(recommendedTrackPath)) return false;

            string currentKey = Path.GetFileNameWithoutExtension(currentTrackPath);
            string recKey = Path.GetFileNameWithoutExtension(recommendedTrackPath);

            if (TrackTransitions.ContainsKey(currentKey))
            {
                return TrackTransitions[currentKey].Contains(recKey);
            }
            return false;
        }
    }
}
