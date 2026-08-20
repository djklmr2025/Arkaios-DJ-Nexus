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

        // Memoria contextual del hilo conversacional
        private static string lastSubjectDJ = "Tiësto";
        private static readonly List<string> historyContext = new List<string>();

        public static async Task<AgentResponse> ProcessRequestAsync(string input, Action<string> statusCallback = null)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new AgentResponse { Text = "Por favor ingresa una pregunta o solicitud de música/video.", Success = false };

            string cleanInput = input.Trim();
            
            // Guardar contexto en memoria del hilo conversacional
            historyContext.Add("USER: " + cleanInput);
            if (historyContext.Count > 10) historyContext.RemoveAt(0);

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

            // 1. Intentar llamar al endpoint de IA en vivo (Local Gemini-Lab)
            string liveAiAnswer = await FetchLiveAiResponseAsync(input);
            if (!string.IsNullOrWhiteSpace(liveAiAnswer))
            {
                historyContext.Add("AGENT: " + liveAiAnswer);
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

            // 2. Motor Conversacional Inteligente Contextual (Mantiene el Hilo de la Conversación)
            await Task.Delay(150);
            
            string rawLower = input.ToLowerInvariant();
            string normText = Regex.Replace(rawLower, @"[^a-z0-9\s]", " ");
            normText = Regex.Replace(normText, @"\s+", " ").Trim();

            // Detectar si el usuario nombra a un DJ específico en la consulta actual y actualizar el sujeto activo
            if (normText.Contains("tiesto") || normText.Contains("tiësto")) lastSubjectDJ = "Tiësto";
            else if (normText.Contains("armin") || normText.Contains("buuren")) lastSubjectDJ = "Armin van Buuren";
            else if (normText.Contains("guetta")) lastSubjectDJ = "David Guetta";
            else if (normText.Contains("garrix")) lastSubjectDJ = "Martin Garrix";
            else if (normText.Contains("skrillex")) lastSubjectDJ = "Skrillex";
            else if (normText.Contains("harris")) lastSubjectDJ = "Calvin Harris";
            else if (normText.Contains("hardwell")) lastSubjectDJ = "Hardwell";

            StringBuilder reply = new StringBuilder();
            reply.AppendLine("🤖 **AGENTE ARKAIOS:**");
            reply.AppendLine();

            // Intención 1: Cobro / Facturación / Ganancias / Presentación / Cuánto cobra
            bool isFeeQuery = normText.Contains("cobra") || normText.Contains("factura") || normText.Contains("ganancia") ||
                              normText.Contains("presentacion") || normText.Contains("show") || normText.Contains("cuanto") ||
                              normText.Contains("tarifa") || normText.Contains("precio") || normText.Contains("costo") || normText.Contains("dinero");

            if (isFeeQuery)
            {
                reply.AppendLine("💰 **Facturación y Tarifas por Presentación (" + lastSubjectDJ + "):**");
                reply.AppendLine();

                if (lastSubjectDJ == "Tiësto")
                {
                    reply.AppendLine("• **Tarifa por Show / Set:** **Tiësto** es históricamente uno de los DJs más cotizados de la industria. Cobra entre **$250,000 USD y $500,000 USD por presentación** estándar de 90 a 120 minutos.");
                    reply.AppendLine("• **Festivales Masivos y Residencias:** En festivales estelares (Tomorrowland, Ultra Music Festival, EDC Las Vegas) o fechas exclusivas en clubes de alto nivel en Las Vegas (Zouk / LIV), su cobro puede ascender hasta **$1,000,000 USD por fecha**.");
                    reply.AppendLine("• **Patrimonio Neto Estimado:** Su fortuna acumulada ronda los **$170 Millones de USD**, situándolo en el Top 3 de los DJs más acaudalados del planeta junto a Calvin Harris y David Guetta.");
                }
                else if (lastSubjectDJ == "David Guetta" || lastSubjectDJ == "Calvin Harris")
                {
                    reply.AppendLine("• **Tarifa por Show:** " + lastSubjectDJ + " cobra entre **$300,000 USD y $450,000 USD** por set en festivales internacionales y residencias exclusivas.");
                    reply.AppendLine("• **Facturación Anual:** Generan entre **$25 y $40 Millones de USD al año** entre shows en vivo, derechos de producción y patrocinio de marcas.");
                }
                else if (lastSubjectDJ == "Martin Garrix")
                {
                    reply.AppendLine("• **Tarifa por Show:** Martin Garrix cobra entre **$200,000 USD y $350,000 USD** por fecha en su gira mundial y festivales estelares.");
                }
                else
                {
                    reply.AppendLine("• **Rango General de Top DJs:** Los DJs dentro del Top 10 Mundial facturan entre **$150,000 USD y $400,000 USD por presentación** en eventos estelares.");
                }
            }
            // Intención 2: Tiësto Específico (Biografía / Posición / Hits)
            else if (normText.Contains("tiesto") || normText.Contains("tiësto"))
            {
                reply.AppendLine("🎧 **Información sobre DJ Tiësto:**");
                reply.AppendLine();
                reply.AppendLine("• **Posición Actual:** Tiësto (Tijs Verwest) se mantiene en el **Top 25 Mundial de DJ Mag** (actualmente posición #23 en el ranking 2024) y sigue siendo uno de los DJs estelares con mayor facturación en festivales como Tomorrowland, Ultra y EDC Las Vegas.");
                reply.AppendLine("• **Legado Inigualable:** Fue votado **el DJ #1 del Mundo durante 3 años consecutivos** (2002, 2003, 2004) y coronado por DJ Mag como *'The Greatest DJ of All Time'*.");
                reply.AppendLine("• **Hits Emblemáticos:** Pionero del Trance (*Adagio for Strings*, *Lethal Industry*) y del Dance Pop comercial (*The Business*, *10:35*, *Don't Be Shy*).");
            }
            // Intención 3: Armin van Buuren
            else if (normText.Contains("armin") || normText.Contains("buuren"))
            {
                reply.AppendLine("🎧 **Información sobre Armin van Buuren:**");
                reply.AppendLine();
                reply.AppendLine("• **Posición Actual:** Se ubica actualmente en el puesto **#5 Global** en el DJ Mag Top 100.");
                reply.AppendLine("• **Récord Histórico:** Es el único DJ en la historia que ha ganado **5 veces el puesto #1 del Mundo** (2007, 2008, 2009, 2010 y 2012).");
                reply.AppendLine("• **A State of Trance:** Es el creador y locutor del show de radio semanal *A State of Trance (ASOT)* con más de 1100 episodios transmitidos.");
            }
            // Intención 4: Ranking Estricto DJ #1 (Sin confundir "mejores facturados")
            else if ((normText.Contains("quien") || normText.Contains("cual")) && (normText.Contains("1") || normText.Contains("primero") || normText.Contains("lider")) && normText.Contains("dj"))
            {
                reply.AppendLine("🏆 **Top DJs Mundiales e Información de la Escena:**");
                reply.AppendLine("El **DJ número #1 del mundo actualmente** (según el ranking oficial **DJ Mag Top 100 DJs** 2024) se disputa entre:");
                reply.AppendLine();
                reply.AppendLine("• 🥇 **Martin Garrix & David Guetta:** Martin Garrix ocupa el puesto #1 oficial en 2024, mientras David Guetta domina las listas globales con el movimiento *Future Rave*.");
                reply.AppendLine("• ⚡ **Top 5 Global:** Martin Garrix, David Guetta, Dimitri Vegas & Like Mike, Alok y Armin van Buuren.");
            }
            // Intención 5: BPMs / Tempos
            else if (normText.Contains("bpm") || normText.Contains("tempo") || normText.Contains("velocidad"))
            {
                reply.AppendLine("⏱️ **Guía de Tempos y BPMs para DJs:**");
                reply.AppendLine("• **Reggaeton / Urbano:** 85 - 98 BPM.");
                reply.AppendLine("• **House / EDM:** 120 - 128 BPM.");
                reply.AppendLine("• **Tech House / Techno:** 124 - 130 BPM.");
            }
            // Intención 6: Camelot Wheel
            else if (normText.Contains("camelot") || normText.Contains("key") || normText.Contains("tonalidad"))
            {
                reply.AppendLine("🎹 **Reglas de Mezcla Armónica (Camelot Wheel):**");
                reply.AppendLine("• **Misma Clave (8A -> 8A):** Mezcla perfecta.");
                reply.AppendLine("• **Quinta Justa (8A -> 9A / 7A):** Transición armónica fluida de 1 hora en la rueda.");
            }
            // Intención 7: Saludos / Estado
            else if (normText.Contains("vivo") || normText.Contains("hola") || normText.Contains("quien eres"))
            {
                reply.AppendLine("¡Hola! 👋 Estoy 100% activo y listo para ayudarte en lo que necesites.");
                reply.AppendLine("Mantenemos el hilo sobre **" + lastSubjectDJ + "** o cualquier otra consulta de música, DJs o temática general.");
            }
            // Intención 8: Respuesta contextual continua fluida
            else
            {
                reply.AppendLine("Siguiendo nuestra conversación sobre **" + lastSubjectDJ + "** y la escena musical:");
                reply.AppendLine();
                reply.AppendLine("Puedo darte detalles sobre canciones representativas, BPMs recomendados para mezclar sus temas o cualquier otra información de la industria.");
            }

            reply.AppendLine();
            reply.AppendLine("📥 *Recordatorio:* Mi función principal es la obtención autónoma de canciones, videos o karaokes. Si deseas descargar algo de " + lastSubjectDJ + ", dime *'bájame " + lastSubjectDJ + "'* o *'descarga el video de " + lastSubjectDJ + "'* y lo guardaré en Verde Neón.");

            string finalAnswer = reply.ToString();
            historyContext.Add("AGENT: " + finalAnswer);

            return new AgentResponse
            {
                Text = finalAnswer,
                IsDownload = false,
                Success = true
            };
        }

        private static async Task<string> FetchLiveAiResponseAsync(string prompt)
        {
            try
            {
                // Construir el prompt enviando los últimos turnos de conversación para dar contexto completo
                StringBuilder fullPrompt = new StringBuilder();
                fullPrompt.AppendLine("Contexto de la conversación previa:");
                foreach (string turn in historyContext)
                {
                    fullPrompt.AppendLine(turn);
                }
                fullPrompt.AppendLine("Pregunta actual del usuario: " + prompt);

                var payloadObj = new { prompt = fullPrompt.ToString() };
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
                // Silenciosamente continuar al motor directo si el endpoint no está activo
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
