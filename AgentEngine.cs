using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArkaiosDJAssistant
{
    public class AgentResponse
    {
        public string Text { get; set; }
        public bool IsDownload { get; set; }
        public string DownloadedPath { get; set; }
        public bool Success { get; set; }
    }

    public static class AgentEngine
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private const string ATubeMcpUrl = "http://127.0.0.1:3845/";

        public static async Task<AgentResponse> ProcessRequestAsync(string input, Action<string> statusCallback = null)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new AgentResponse { Text = "Por favor ingresa una pregunta o solicitud de música/video.", Success = false };

            string cleanInput = input.Trim();
            bool isDownloadRequest = IsDownloadCommand(cleanInput);

            if (isDownloadRequest)
            {
                return await HandleDownloadIntentAsync(cleanInput, statusCallback);
            }
            else
            {
                return await HandleMusicologyQueryAsync(cleanInput, statusCallback);
            }
        }

        private static bool IsDownloadCommand(string input)
        {
            string text = input.ToLowerInvariant();
            string[] downloadKeywords = new[]
            {
                "descarga", "descargar", "bájame", "bajame", "baja", "bájate", "bajate",
                "consígueme", "consigueme", "tráeme", "traeme", "descárgame", "descargame",
                "busca y descarga", "bajar", "consigue", "obten", "obtén", "traer", "download"
            };

            foreach (string kw in downloadKeywords)
            {
                if (text.Contains(kw)) return true;
            }
            return false;
        }

        private static async Task<AgentResponse> HandleDownloadIntentAsync(string input, Action<string> statusCallback)
        {
            string mediaType = "music";
            string lower = input.ToLowerInvariant();
            if (lower.Contains("video") || lower.Contains("mp4") || lower.Contains("hd") || lower.Contains("clip"))
                mediaType = "video";
            else if (lower.Contains("karaoke") || lower.Contains("cdg") || lower.Contains("pista"))
                mediaType = "karaoke";

            string searchQuery = CleanSearchQuery(input);
            if (string.IsNullOrWhiteSpace(searchQuery)) searchQuery = input;

            statusCallback?.Invoke("🤖 Agente ARKAIOS: Iniciando búsqueda autónoma para '" + searchQuery + "' (" + mediaType + ")...");

            string savedPath = null;

            // Intentar primero aTube Catcher MCP si está activo el servidor
            bool mcpSuccess = await TryATubeCatcherMcpAsync(searchQuery, mediaType);
            if (!mcpSuccess)
            {
                statusCallback?.Invoke("🤖 Agente ARKAIOS: Ejecutando motor de descarga (aTube Catcher / yt-dlp en segundo plano)...");
                List<YouTubeTrack> searchResults = await YouTubeEngine.SearchAsync(searchQuery, mediaType, 3);
                if (searchResults != null && searchResults.Count > 0)
                {
                    string targetUrl = searchResults[0].Url;
                    string quality = mediaType == "video" ? "720p HD" : "MP3 320 kbps";
                    savedPath = await YouTubeEngine.DownloadAsync(targetUrl, mediaType, quality);
                }
            }

            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                DownloadRegistry.Register(savedPath, "", Path.GetFileNameWithoutExtension(savedPath), "Agente DJ Assistant", mediaType);
                string fileName = Path.GetFileName(savedPath);

                StringBuilder responseText = new StringBuilder();
                responseText.AppendLine("✅ **¡Pista descargada con éxito por el Agente ARKAIOS!**");
                responseText.AppendLine("📌 **Archivo:** " + fileName);
                responseText.AppendLine("📁 **Ubicación:** " + savedPath);
                responseText.AppendLine("⚡ **Estado:** Registrada en *Descargas Recientes / Hub Local* en color **Verde Neón**. ¡Lista para arrastrar al plato de VirtualDJ!");
                responseText.AppendLine();
                responseText.AppendLine("💡 *Tip del Experto DJ:* Recuerda que puedes consultar la compatibilidad en Camelot Wheel desde la pestaña 1 para realizar un mix armónico perfecto.");

                return new AgentResponse
                {
                    Text = responseText.ToString(),
                    IsDownload = true,
                    DownloadedPath = savedPath,
                    Success = true
                };
            }
            else
            {
                return new AgentResponse
                {
                    Text = "⚠️ No pude descargar automáticamente la pista '" + searchQuery + "'. Intenta afinando el título o artista en el buscador.",
                    IsDownload = true,
                    Success = false
                };
            }
        }

        private static async Task<bool> TryATubeCatcherMcpAsync(string query, string mediaType)
        {
            try
            {
                var payload = "{\"tool\":\"download_url\",\"arguments\":{\"query\":\"" + query.Replace("\"", "\\\"") + "\",\"media_type\":\"" + mediaType + "\"}}";
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage resp = await httpClient.PostAsync(ATubeMcpUrl + "call_tool", content);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<AgentResponse> HandleMusicologyQueryAsync(string input, Action<string> statusCallback)
        {
            statusCallback?.Invoke("🤖 Agente ARKAIOS: Consultando base de conocimientos musicales...");
            await Task.Delay(400);

            string text = input.ToLowerInvariant();
            StringBuilder reply = new StringBuilder();

            reply.AppendLine("🎧 **ARKAIOS Musicology & DJ Expert:**");
            reply.AppendLine();

            if (text.Contains("bpm") || text.Contains("tempo") || text.Contains("velocidad"))
            {
                reply.AppendLine("⏱️ **Guía de Tempos y BPMs para DJs:**");
                reply.AppendLine("• **Reggaeton / Urbano:** 85 - 98 BPM (ideal para transiciones lentas o doble tiempo).");
                reply.AppendLine("• **Cumbia / Tropical:** 90 - 105 BPM (mezclas fluidas en fraseo de 8 compases).");
                reply.AppendLine("• **House / Dance / EDM:** 120 - 128 BPM (zona estándar para mezclas largas y loops).");
                reply.AppendLine("• **Tech House / Techno:** 124 - 130 BPM (enfoque en ecualización de bajos y hi-hats).");
                reply.AppendLine();
                reply.AppendLine("💡 *Consejo ARKAIOS:* Mantén las variaciones de BPM en menos del ±4% para no alterar la tonalidad percibida por el público.");
            }
            else if (text.Contains("camelot") || text.Contains("key") || text.Contains("tonalidad") || text.Contains("armon") || text.Contains("armón"))
            {
                reply.AppendLine("🎹 **Reglas de Mezcla Armónica (Camelot Wheel):**");
                reply.AppendLine("• **Misma Clave (ej: 8A -> 8A):** Compatibilidad perfecta sin choque de notas.");
                reply.AppendLine("• **Cambio Modal (ej: 8A -> 8B):** De Menor a Mayor manteniendo la nota raíz (emoción alegre).");
                reply.AppendLine("• **Quinta Justa (ej: 8A -> 9A o 7A):** Movimiento en la rueda Camelot (+1 / -1 hora).");
                reply.AppendLine("• **Energy Boost (+2 Semitonos):** Avanzar +2 horas (ej: 8A -> 10A) para subir la energía de la pista.");
            }
            else
            {
                reply.AppendLine("Respuesta sobre: *" + input + "*");
                reply.AppendLine();
                reply.AppendLine("Como tu **Agente DJ Assistant**, estoy equipado para:");
                reply.AppendLine("1. 📥 **Descargar cualquier pista, video o karaoke**: Solo dime *'bájame X'* o *'descarga el video Y'*.");
                reply.AppendLine("2. 🎵 **Consultar información técnica**: Pídeme BPMs, compatibilidad Camelot o consejos de estructura musical.");
                reply.AppendLine("3. ⚡ **Auto-Registrar en tu biblioteca**: Todo lo que me pidas descargar aparecerá automáticamente en **Verde Neón** al final de tu lista principal.");
            }

            return new AgentResponse
            {
                Text = reply.ToString(),
                IsDownload = false,
                Success = true
            };
        }

        private static string CleanSearchQuery(string input)
        {
            string text = input;
            string[] prefixes = new[]
            {
                "descarga", "descargar", "bájame", "bajame", "baja", "bájate", "bajate",
                "consígueme", "consigueme", "tráeme", "traeme", "descárgame", "descargame",
                "busca y descarga", "bajar", "el video de", "la cancion de", "el karaoke de",
                "el mp3 de", "por favor", "porfa", "en hd", "en mp3", "audio de", "video de"
            };

            foreach (string p in prefixes)
            {
                text = Regex.Replace(text, @"\b" + Regex.Escape(p) + @"\b", "", RegexOptions.IgnoreCase);
            }
            return text.Trim();
        }
    }
}
