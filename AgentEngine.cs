using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        private const string LocalGeminiLabUrl = "http://localhost:3000/api/chat";

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
                return await HandleGeneralOrMusicologyQueryAsync(cleanInput, statusCallback);
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

        private static async Task<AgentResponse> HandleGeneralOrMusicologyQueryAsync(string input, Action<string> statusCallback)
        {
            statusCallback?.Invoke("🤖 Agente ARKAIOS: Consultando inteligencia conversacional...");

            // 1. Intentar llamar al endpoint de IA en vivo (Local Gemini-Lab o servidor de IA)
            string liveAiAnswer = await FetchLiveAiResponseAsync(input);
            if (!string.IsNullOrWhiteSpace(liveAiAnswer))
            {
                StringBuilder liveReply = new StringBuilder();
                liveReply.AppendLine("🤖 **AGENTE ARKAIOS:**");
                liveReply.AppendLine();
                liveReply.AppendLine(liveAiAnswer);

                return new AgentResponse
                {
                    Text = liveReply.ToString(),
                    IsDownload = false,
                    Success = true
                };
            }

            // 2. Motor Conversacional Abierto Multitemático (Abierto a Cualquier Tema)
            await Task.Delay(200);
            
            string rawLower = input.ToLowerInvariant();
            string normText = Regex.Replace(rawLower, @"[^a-z0-9\s]", " ");
            normText = Regex.Replace(normText, @"\s+", " ").Trim();

            StringBuilder reply = new StringBuilder();
            reply.AppendLine("🤖 **AGENTE ARKAIOS:**");
            reply.AppendLine();

            // Intención A: Ranking DJs / Top 1 / Artistas Electrónicos
            bool isTopDjQuery = normText.Contains("1") || normText.Contains("top") || normText.Contains("famoso") || 
                                normText.Contains("ranking") || normText.Contains("lider") || normText.Contains("mejor") || 
                                normText.Contains("popular") || normText.Contains("guetta") || normText.Contains("garrix") || 
                                normText.Contains("mag") || (normText.Contains("dj") && (normText.Contains("quien") || normText.Contains("cual")));

            if (isTopDjQuery)
            {
                reply.AppendLine("🏆 **Top DJs Mundiales e Información de la Escena:**");
                reply.AppendLine("El **DJ número #1 del mundo actualmente** (según el ranking oficial **DJ Mag Top 100 DJs** y la presencia en festivales como Tomorrowland, Ultra Music Festival y EDC) se disputa entre:");
                reply.AppendLine();
                reply.AppendLine("• 🥇 **Martin Garrix & David Guetta:** Martin Garrix ocupa la posición #1 oficial en DJ Mag 2024, mientras David Guetta domina la radio y los charts con su movimiento *Future Rave*.");
                reply.AppendLine("• ⚡ **Top 5 Global:** Martin Garrix, David Guetta, Dimitri Vegas & Like Mike, Alok y Armin van Buuren.");
                reply.AppendLine("• 🔥 **Especialistas por Género:** Charlotte de Witte y Amelie Lens (Techno), Fisher y Michael Bibi (Tech House), Tale Of Us y Anyma (Melodic Techno).");
            }
            else if (normText.Contains("bpm") || normText.Contains("tempo") || normText.Contains("velocidad") || normText.Contains("compas"))
            {
                reply.AppendLine("⏱️ **Guía de Tempos y BPMs para DJs:**");
                reply.AppendLine("• **Reggaeton / Urbano:** 85 - 98 BPM (mezclas fluidas en doble tiempo).");
                reply.AppendLine("• **Cumbia / Tropical:** 90 - 105 BPM (mezclas en frases de 8 compases).");
                reply.AppendLine("• **House / Dance / EDM:** 120 - 128 BPM (zona estándar para mezclas largas).");
                reply.AppendLine("• **Tech House / Techno:** 124 - 130 BPM (enfoque en ecualización de bajos).");
            }
            else if (normText.Contains("camelot") || normText.Contains("key") || normText.Contains("tonalidad") || normText.Contains("armon"))
            {
                reply.AppendLine("🎹 **Reglas de Mezcla Armónica (Camelot Wheel):**");
                reply.AppendLine("• **Misma Clave (ej: 8A -> 8A):** Mezcla perfecta sin choque de notas.");
                reply.AppendLine("• **Cambio Modal (ej: 8A -> 8B):** Transición alegre de Menor a Mayor.");
                reply.AppendLine("• **Quinta Justa (ej: 8A -> 9A / 7A):** Movimiento natural de 1 hora en la rueda.");
            }
            else if (normText.Contains("vivo") || normText.Contains("quien eres") || normText.Contains("hola") || normText.Contains("buenas"))
            {
                reply.AppendLine("¡Hola! 👋 Estoy 100% activo y listo para ayudarte en lo que necesites.");
                reply.AppendLine("Puedes hacerme preguntas de **cualquier temática** (ciencia, cultura general, tecnología, música o consejos), y mi función estrella es ayudarte a **obtener y descargar cualquier canción, video o karaoke** automáticamente.");
            }
            else
            {
                // Respuesta abierta multi-temática fluida sin encajonar a un único tema
                reply.AppendLine("Sobre tu consulta acerca de: *" + input + "*");
                reply.AppendLine();
                reply.AppendLine("Entendido. Puedes preguntarme sobre **cualquier tema general**, concepto, duda o recomendación que necesites.");
                reply.AppendLine();
                reply.AppendLine("• Si tu consulta está relacionada con **obtener alguna pista musical, video o karaoke**, solo dímelo directamente (por ejemplo: *'bájame " + CleanSearchQuery(input) + "'* o *'descarga el video de " + CleanSearchQuery(input) + "'*) y me encargaré de traerlo a tus descargas recientes en **Verde Neón**.");
            }

            reply.AppendLine();
            reply.AppendLine("📥 *Recordatorio:* Recuerda que mi función principal es la obtención autónoma de canciones, videos o karaokes. ¡Solo pídeme lo que quieras descargar!");

            return new AgentResponse
            {
                Text = reply.ToString(),
                IsDownload = false,
                Success = true
            };
        }

        private static async Task<string> FetchLiveAiResponseAsync(string prompt)
        {
            try
            {
                var payloadObj = new { prompt = prompt };
                string jsonPayload = JsonSerializer.Serialize(payloadObj);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage resp = await httpClient.PostAsync(LocalGeminiLabUrl, content);
                if (resp.IsSuccessStatusCode)
                {
                    string jsonResult = await resp.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonResult))
                    {
                        if (doc.RootElement.TryGetProperty("response", out JsonElement respElement))
                        {
                            string resultText = respElement.GetString();
                            if (!string.IsNullOrWhiteSpace(resultText)) return resultText;
                        }
                    }
                }
            }
            catch
            {
                // Silenciosamente continuar al fallback si el endpoint no está activo
            }
            return null;
        }

        private static string CleanSearchQuery(string input)
        {
            string text = input;
            string[] prefixes = new[]
            {
                "descarga", "descargar", "bájame", "bajame", "baja", "bájate", "bajate",
                "consígueme", "consigueme", "tráeme", "traeme", "descárgame", "descargame",
                "busca y descarga", "bajar", "el video de", "la cancion de", "el karaoke de",
                "el mp3 de", "por favor", "porfa", "en hd", "en mp3", "audio de", "video de",
                "respondeme una duda musical", "responde una duda musical", "duda musical"
            };

            foreach (string p in prefixes)
            {
                text = Regex.Replace(text, @"\b" + Regex.Escape(p) + @"\b", "", RegexOptions.IgnoreCase);
            }
            return text.Trim();
        }
    }
}
