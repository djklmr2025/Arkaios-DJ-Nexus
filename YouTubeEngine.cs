using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkaiosDJAssistant
{
    public class YouTubeTrack
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Duration { get; set; }
    }

    public static class YouTubeEngine
    {
        private static string ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");

        public static async Task<List<YouTubeTrack>> SearchArtistAsync(string artist)
        {
            List<YouTubeTrack> tracks = new List<YouTubeTrack>();
            if (string.IsNullOrEmpty(artist)) return tracks;
            if (!File.Exists(ytDlpPath)) return tracks;

            try
            {
                return await Task.Run(() =>
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = string.Format("\"ytsearch5:{0} official audio\" --dump-json --no-playlist", artist),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };

                    using (Process p = Process.Start(psi))
                    {
                        JavaScriptSerializer jss = new JavaScriptSerializer();
                        string line;
                        while ((line = p.StandardOutput.ReadLine()) != null)
                        {
                            try
                            {
                                var dict = jss.Deserialize<Dictionary<string, object>>(line);
                                if (dict != null)
                                {
                                    string title = dict.ContainsKey("title") ? dict["title"].ToString() : "";
                                    string url = dict.ContainsKey("webpage_url") ? dict["webpage_url"].ToString() : "";
                                    int duration = dict.ContainsKey("duration") ? Convert.ToInt32(dict["duration"]) : 0;
                                    
                                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                                    {
                                        string time = string.Format("{0}:{1:00}", duration / 60, duration % 60);
                                        tracks.Add(new YouTubeTrack { Title = title, Url = url, Duration = time });
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    return tracks;
                });
            }
            catch
            {
                return tracks;
            }
        }

        public static async Task<string> DownloadAudioAsync(string url)
        {
            if (!File.Exists(ytDlpPath)) return null;
            
            // Usamos la carpeta de música del usuario
            string downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "VirtualDJ_Downloads");
            if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

            try
            {
                return await Task.Run(() =>
                {
                    // Guardar como: "VirtualDJ_Downloads\Artista - Titulo.mp3"
                    string outputTemplate = Path.Combine(downloadFolder, "%(title)s.%(ext)s");
                    
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = string.Format("-x --audio-format mp3 -o \"{0}\" \"{1}\"", outputTemplate, url),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using (Process p = Process.Start(psi))
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        var match = Regex.Match(output, @"Destination:\s*(.+?\.mp3)");
                        if (match.Success) return match.Groups[1].Value.Trim();

                        match = Regex.Match(output, @"\[download\]\s*(.+?\.mp3)\s*has already been downloaded");
                        if (match.Success) return match.Groups[1].Value.Trim();

                        match = Regex.Match(output, @"\[ExtractAudio\] Destination:\s*(.+?\.mp3)");
                        if (match.Success) return match.Groups[1].Value.Trim();
                    }
                    return null;
                });
            }
            catch
            {
                return null;
            }
        }
    }
}
