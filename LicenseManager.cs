using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;

namespace ArkaiosDJAssistant
{
    public static class LicenseManager
    {
        private static string licenseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArkaiosDJNexus");
        private static string licensePath = Path.Combine(licenseDir, "license.key");
        private const string SALT = "ARKAIOS_SECRET_KEY_2026_NEXUS";

        public static string GetHardwareId()
        {
            try
            {
                // Get MAC address of the first operational network interface
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        return nic.GetPhysicalAddress().ToString();
                    }
                }
            }
            catch {}
            return "HWID_NOT_FOUND_" + Environment.MachineName;
        }

        public static bool IsLicensed()
        {
            if (!File.Exists(licensePath)) return false;
            string key = File.ReadAllText(licensePath).Trim();
            
            if (!ValidateKeyLocally(key)) return false;

            // Check Cloud API (Hybrid Failover)
            string URL_PRIMARIA = "http://localhost:3000/api/licenses/validate"; // TODO: Cambiar por link Ngrok
            string URL_SECUNDARIA = "https://servidor-arkaios-api.vercel.app/api/licenses/validate"; 

            string hwid = GetHardwareId();
            string jsonPayload = string.Format("{{\"key\":\"{0}\", \"hwid\":\"{1}\"}}", key, hwid);
            string response = "";
            bool connectionSuccess = false;

            try
            {
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                    response = client.UploadString(URL_PRIMARIA, "POST", jsonPayload);
                    connectionSuccess = true;
                }
            }
            catch
            {
                // Fallback to Secondary Server
                try
                {
                    using (System.Net.WebClient client = new System.Net.WebClient())
                    {
                        client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                        response = client.UploadString(URL_SECUNDARIA, "POST", jsonPayload);
                        connectionSuccess = true;
                    }
                }
                catch { /* Offline Grace Period */ }
            }

            if (connectionSuccess)
            {
                if (response.Contains("\"success\": false"))
                {
                    System.Windows.Forms.MessageBox.Show("Tu licencia ha sido revocada o bloqueada por el servidor.", "Licencia Inválida", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    return false;
                }

                // Check for updates
                if (response.Contains("\"latestVersion\":") && !response.Contains("\"latestVersion\": \"v1.0.0\""))
                {
                    // Parse update URL rudimentary for C# 5
                    string updateUrl = "https://github.com/djklmr2025/Arkaios-DJ-Nexus/releases";
                    var result = System.Windows.Forms.MessageBox.Show(
                        "¡Hay una nueva versión de Arkaios DJ Nexus disponible!\n\nComo tu licencia está activa, puedes actualizar gratis. ¿Deseas descargarla ahora?", 
                        "Nueva Versión Disponible", 
                        System.Windows.Forms.MessageBoxButtons.YesNo, 
                        System.Windows.Forms.MessageBoxIcon.Information);
                        
                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(updateUrl);
                    }
                }
            }

            return true;
        }

        public static bool ValidateKeyLocally(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key)) return false;
                
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(key));
                string[] parts = decoded.Split('|');
                if (parts.Length != 6) return false;

                string hwid = parts[0];
                string type = parts[1]; // BASIC or LIFETIME
                string name = parts[2];
                string phone = parts[3];
                string timestamp = parts[4];
                string signature = parts[5];

                // Verify signature
                string dataToSign = hwid + "|" + type + "|" + name + "|" + phone + "|" + timestamp + "|" + SALT;
                string expectedSignature = GenerateSHA256(dataToSign);

                if (signature != expectedSignature) return false;

                // Enforce HWID for BASIC licenses
                if (type == "BASIC" && hwid != GetHardwareId()) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveLicense(string key)
        {
            if (!Directory.Exists(licenseDir))
            {
                Directory.CreateDirectory(licenseDir);
            }
            File.WriteAllText(licensePath, key);
        }

        private static string GenerateSHA256(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
