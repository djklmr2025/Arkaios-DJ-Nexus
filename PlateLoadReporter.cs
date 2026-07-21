using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ArkaiosDJAssistant
{
    public static class PlateLoadReporter
    {
        private const string Endpoint = "https://servidor-arkaios-api.vercel.app/api/dj/plate-loads";

        public static void ReportAsync(Track track, string loadedPath)
        {
            Task.Run(() => Report(track, loadedPath));
        }

        private static void Report(Track track, string loadedPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(loadedPath)) return;

                string fileName = Path.GetFileName(loadedPath);
                string fileType = Path.GetExtension(loadedPath).ToLowerInvariant();
                string title = track == null || string.IsNullOrWhiteSpace(track.Title)
                    ? Path.GetFileNameWithoutExtension(loadedPath)
                    : track.Title;
                string artist = track == null ? "" : (track.Artist ?? "");
                string displayName = string.IsNullOrWhiteSpace(artist) ? title : artist + " - " + title;
                string mediaType = IsVideo(fileType) ? "Video" : "Music";
                string bpm = track == null || track.Bpm <= 0 ? "null" : track.Bpm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                string json = "{"
                    + "\"source\":\"arkaios-dj-assistant\","
                    + "\"stationId\":\"" + Json(Environment.MachineName) + "\","
                    + "\"deck\":\"virtualdj-history\","
                    + "\"loadedAt\":\"" + Json(DateTime.UtcNow.ToString("o")) + "\","
                    + "\"track\":{"
                    + "\"title\":\"" + Json(title) + "\","
                    + "\"artist\":\"" + Json(artist) + "\","
                    + "\"displayName\":\"" + Json(displayName) + "\","
                    + "\"filePath\":\"" + Json(loadedPath) + "\","
                    + "\"fileName\":\"" + Json(fileName) + "\","
                    + "\"fileType\":\"" + Json(fileType) + "\","
                    + "\"mediaType\":\"" + Json(mediaType) + "\","
                    + "\"bpm\":" + bpm + ","
                    + "\"camelotKey\":\"" + Json(track == null ? "" : track.CamelotKey) + "\","
                    + "\"genre\":\"" + Json(track == null ? "" : track.Genre) + "\""
                    + "}"
                    + "}";

                using (WebClient client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.UploadString(Endpoint, "POST", json);
                }
            }
            catch
            {
                // No bloquear VirtualDJ ni el sistema de recomendaciones si la nube no responde.
            }
        }

        private static bool IsVideo(string extension)
        {
            string ext = (extension ?? "").ToLowerInvariant();
            return ext == ".mp4" || ext == ".mkv" || ext == ".webm" || ext == ".avi" || ext == ".mov";
        }

        private static string Json(string value)
        {
            if (value == null) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
