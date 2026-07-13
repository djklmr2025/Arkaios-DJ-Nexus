using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ArkaiosDJAssistant
{
    public static class HistoryEngine
    {
        // Dictionary mapping from a track's Path/Title to a HashSet of tracks that followed it
        public static Dictionary<string, HashSet<string>> TrackTransitions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static void ScanHistory(string vdjFolder)
        {
            TrackTransitions.Clear();
            string playlistsDir = Path.Combine(vdjFolder, "Playlists");
            string historyDir = Path.Combine(vdjFolder, "History");

            if (Directory.Exists(playlistsDir))
                ScanDirectory(playlistsDir);
            
            if (Directory.Exists(historyDir))
                ScanDirectory(historyDir);
        }

        private static void ScanDirectory(string path)
        {
            try
            {
                var files = Directory.GetFiles(path, "*.m3u", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    ParseM3U(file);
                }
            }
            catch { }
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

                    // El track puede ser una ruta completa o solo un nombre. 
                    // Tomamos el nombre del archivo sin extensión como llave para máxima compatibilidad
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
            catch { }
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
