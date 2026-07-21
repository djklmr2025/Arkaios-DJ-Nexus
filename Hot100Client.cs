using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ArkaiosDJAssistant
{
    public class Hot100Track
    {
        public int Position { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public int Change { get; set; }
    }

    public class Hot100Result
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<Hot100Track> Tracks { get; set; }

        public Hot100Result()
        {
            Tracks = new List<Hot100Track>();
        }
    }

    /// <summary>
    /// Lee el Top 100 de tracks propios de ARKAIOS (segun eventos reales de carga
    /// al plato reportados por los usuarios). El endpoint se resuelve dinamicamente
    /// desde el manifiesto de servicios en servidor-arkaios-api.vercel.app, igual que
    /// PlaylistExtractorClient resuelve el extractor de playlists. No tiene relacion
    /// con PulseDJ ni con ningun otro chart de terceros; esos solo sirven como
    /// referencia visual de diseño.
    /// </summary>
    public static class Hot100Client
    {
        private const string ServicesEndpoint = "https://servidor-arkaios-api.vercel.app/api/ecosystem/services";
        private const int MaxTracks = 100;
        private static readonly Regex RawIdPattern = new Regex(@"^[A-Za-z0-9_-]{6,20}$", RegexOptions.Compiled);
        private static readonly Regex HasLetterPattern = new Regex(@"[A-Za-zÀ-ÿ]", RegexOptions.Compiled);

        public static async Task<Hot100Result> GetTopTracksAsync(string countryCode)
        {
            return await Task.Run(() =>
            {
                string endpoint = ResolveHot100Endpoint(countryCode);
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    return new Hot100Result
                    {
                        Success = false,
                        Error = "El manifiesto de servicios todavia no expone 'hot100'. Agrega esa entrada en el backend para activar esta pestaña."
                    };
                }

                try
                {
                    ForceModernTls();
                    using (var client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        string json = client.DownloadString(endpoint);
                        return ParseJson(json);
                    }
                }
                catch (Exception ex)
                {
                    return new Hot100Result { Success = false, Error = ex.Message };
                }
            });
        }

        private static string ResolveHot100Endpoint(string countryCode)
        {
            try
            {
                ForceModernTls();
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    string json = client.DownloadString(ServicesEndpoint);
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var root = serializer.Deserialize<Dictionary<string, object>>(json);
                    var services = GetDictionary(root, "services");
                    var hot100 = GetDictionary(services, "hot100");
                    string baseUrl = ToString(hot100, "url").TrimEnd('/');
                    if (string.IsNullOrWhiteSpace(baseUrl)) return null;

                    string path = ToString(hot100, "path");
                    if (string.IsNullOrWhiteSpace(path)) path = "/api/hot100";

                    string url = baseUrl + path;
                    string countryParam = string.IsNullOrWhiteSpace(countryCode) ? "MX" : countryCode;
                    url += (url.Contains("?") ? "&" : "?") + "countryCode=" + Uri.EscapeDataString(countryParam);
                    return url;
                }
            }
            catch
            {
                return null;
            }
        }

        private static Hot100Result ParseJson(string json)
        {
            var result = new Hot100Result();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Error = "Respuesta vacia del servidor.";
                return result;
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = serializer.Deserialize<Dictionary<string, object>>(json);
            if (root == null)
            {
                result.Error = "JSON invalido.";
                return result;
            }

            object listObject;
            if (!root.TryGetValue("tracks", out listObject))
            {
                root.TryGetValue("data", out listObject);
            }
            var list = listObject as IEnumerable;
            if (list == null)
            {
                result.Error = "El servidor no devolvio una lista de tracks.";
                return result;
            }

            int position = 0;
            foreach (object itemObject in list)
            {
                if (position >= MaxTracks) break;

                var item = itemObject as Dictionary<string, object>;
                if (item == null) continue;

                string title = ToString(item, "title");
                if (string.IsNullOrWhiteSpace(title)) title = ToString(item, "track");
                string artist = ToString(item, "artist");

                if (!IsRealTrackName(title) || !IsRealTrackName(artist)) continue;

                position++;
                result.Tracks.Add(new Hot100Track
                {
                    Position = position,
                    Title = title.Trim(),
                    Artist = artist.Trim(),
                    Change = ToInt(item, "change")
                });
            }

            result.Success = result.Tracks.Count > 0;
            if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
                result.Error = "Sin resultados validos todavia.";
            return result;
        }

        /// <summary>
        /// Descarta entradas sin nombre real: vacias, o que parecen un ID/codigo
        /// crudo (hash, video id, GUID) en vez de un titulo o artista legible.
        /// </summary>
        private static bool IsRealTrackName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();
            if (trimmed.Length < 2) return false;
            if (!HasLetterPattern.IsMatch(trimmed)) return false;
            if (!trimmed.Contains(" ") && RawIdPattern.IsMatch(trimmed) && !trimmed.Contains("'"))
            {
                bool hasVowel = Regex.IsMatch(trimmed, "[aeiouAEIOUáéíóúÁÉÍÓÚ]");
                bool looksHashy = Regex.IsMatch(trimmed, @"[0-9]") && Regex.IsMatch(trimmed, @"[A-Za-z]") && trimmed.Length >= 9;
                if (!hasVowel || looksHashy) return false;
            }
            return true;
        }

        private static void ForceModernTls()
        {
            try
            {
                const SecurityProtocolType tls12 = (SecurityProtocolType)3072;
                const SecurityProtocolType tls13 = (SecurityProtocolType)12288;
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | tls12 | tls13;
            }
            catch
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; }
                catch { }
            }
        }

        private static string ToString(Dictionary<string, object> value, string key)
        {
            if (value == null) return "";
            object raw;
            return value.TryGetValue(key, out raw) && raw != null ? raw.ToString() : "";
        }

        private static int ToInt(Dictionary<string, object> value, string key)
        {
            if (value == null) return 0;
            object raw;
            if (!value.TryGetValue(key, out raw) || raw == null) return 0;
            int parsed;
            return int.TryParse(raw.ToString(), out parsed) ? parsed : 0;
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> value, string key)
        {
            if (value == null) return null;
            object raw;
            return value.TryGetValue(key, out raw) ? raw as Dictionary<string, object> : null;
        }
    }
}
