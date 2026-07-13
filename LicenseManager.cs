using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;

namespace ArkaiosDJAssistant
{
    public static class LicenseManager
    {
        private static string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
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
            return ValidateKey(key);
        }

        public static bool ValidateKey(string key)
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
