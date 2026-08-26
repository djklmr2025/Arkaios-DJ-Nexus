using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ArkaiosDJAssistant
{
    public class ArkaiosDaemonStatus
    {
        public string Status { get; set; }
        public string Service { get; set; }
        public string Version { get; set; }
        public int Port { get; set; }
        public int ActiveDownloadsCount { get; set; }
        public int QueueLength { get; set; }
        public int CompletedCount { get; set; }
    }

    public class ArkaiosTrackItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Duration { get; set; }
        public string Url { get; set; }
        public string Cover { get; set; }
    }

    public class ArkaiosSearchResponse
    {
        public string Query { get; set; }
        public int Count { get; set; }
        public List<ArkaiosTrackItem> Results { get; set; }
    }

    public class ArkaiosDownloadRequest
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Format { get; set; }
    }

    public class ArkaiosDownloadResponse
    {
        public string Message { get; set; }
        public string TaskId { get; set; }
    }

    public static class ArkaiosDaemonClient
    {
        private const string BaseUrl = "http://localhost:8788/api";
        private static readonly HttpClient client = new HttpClient();

        public static async Task<ArkaiosDaemonStatus> CheckStatusAsync()
        {
            try
            {
                var json = await client.GetStringAsync($"{BaseUrl}/status");
                return JsonConvert.DeserializeObject<ArkaiosDaemonStatus>(json);
            }
            catch (Exception ex)
            {
                return new ArkaiosDaemonStatus
                {
                    Status = "offline",
                    Service = "Spotify-Arkaios Daemon Engine",
                    Version = "1.2.0 (Disconnected: " + ex.Message + ")"
                };
            }
        }

        public static async Task<List<ArkaiosTrackItem>> SearchTracksAsync(string query)
        {
            try
            {
                var json = await client.GetStringAsync($"{BaseUrl}/search?q={Uri.EscapeDataString(query ?? "")}");
                var resp = JsonConvert.DeserializeObject<ArkaiosSearchResponse>(json);
                return resp?.Results ?? new List<ArkaiosTrackItem>();
            }
            catch
            {
                return new List<ArkaiosTrackItem>();
            }
        }

        public static async Task<ArkaiosDownloadResponse> EnqueueDownloadAsync(string url, string title, string artist, string format = "mp3")
        {
            try
            {
                var req = new ArkaiosDownloadRequest
                {
                    Url = url,
                    Title = title,
                    Artist = artist,
                    Format = format
                };
                var jsonReq = JsonConvert.SerializeObject(req);
                var content = new StringContent(jsonReq, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{BaseUrl}/download", content);
                var jsonResp = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ArkaiosDownloadResponse>(jsonResp);
            }
            catch (Exception ex)
            {
                return new ArkaiosDownloadResponse
                {
                    Message = "Error al conectar con demonio Arkaios: " + ex.Message,
                    TaskId = null
                };
            }
        }
    }
}
