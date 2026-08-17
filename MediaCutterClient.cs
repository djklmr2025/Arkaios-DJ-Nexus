using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ArkaiosDJAssistant
{
    /// <summary>
    /// Cliente de integración API entre DJ Assistant (descarga de mezclas/audio)
    /// y Media Cutter Studio (corte y separación de tracks en lotes).
    /// </summary>
    public class MediaCutterClient
    {
        private static readonly HttpClient client = new HttpClient();
        private readonly string baseUrl;

        public MediaCutterClient(string serverUrl = "http://localhost:3000")
        {
            baseUrl = serverUrl.TrimEnd('/');
            client.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Verifica si el servidor de Media Cutter Studio está en línea.
        /// </summary>
        public async Task<bool> IsServerOnlineAsync()
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}/api/batch-status?jobId=ping");
                return response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Solicita a Media Cutter Studio procesar y separar un archivo de audio/mezcla descargado.
        /// </summary>
        public async Task<string> SendAudioForSlicingAsync(string audioFilePath, string timestampsOrMarkers = "")
        {
            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException($"El archivo de audio no existe: {audioFilePath}");
            }

            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    var fileStream = File.OpenRead(audioFilePath);
                    var fileContent = new StreamContent(fileStream);
                    content.Add(fileContent, "audioFile", Path.GetFileName(audioFilePath));
                    
                    if (!string.IsNullOrWhiteSpace(timestampsOrMarkers))
                    {
                        content.Add(new StringContent(timestampsOrMarkers), "timestamps");
                    }

                    var response = await client.PostAsync($"{baseUrl}/api/slice-local-file", content);
                    var resultJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return resultJson;
                    }
                    else
                    {
                        return $"Error del servidor Media Cutter Studio ({response.StatusCode}): {resultJson}";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error de conexión con Media Cutter Studio: {ex.Message}";
            }
        }
    }
}
