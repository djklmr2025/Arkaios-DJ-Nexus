using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;

namespace ArkaiosDJAssistant
{
    public static class AudioDeviceCatalog
    {
        public const string DefaultDevice = "Windows default";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WaveOutCaps
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
            public uint dwSupport;
        }

        [DllImport("winmm.dll")]
        private static extern uint waveOutGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern uint waveOutGetDevCaps(UIntPtr uDeviceID, out WaveOutCaps pwoc, uint cbwoc);

        public static List<string> GetOutputDevices()
        {
            var devices = new List<string> { DefaultDevice };
            try
            {
                uint count = waveOutGetNumDevs();
                for (uint i = 0; i < count; i++)
                {
                    WaveOutCaps caps;
                    if (waveOutGetDevCaps((UIntPtr)i, out caps, (uint)Marshal.SizeOf(typeof(WaveOutCaps))) == 0)
                    {
                        string name = (caps.szPname ?? "").Trim();
                        if (name.Length > 0 && !devices.Exists(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase)))
                            devices.Add(name);
                    }
                }
            }
            catch { }

            string vdj = GetVirtualDjHeadphoneDevice();
            if (!string.IsNullOrWhiteSpace(vdj) && !devices.Exists(d => d.IndexOf(vdj, StringComparison.OrdinalIgnoreCase) >= 0))
                devices.Add("VDJ audifonos detectado: " + vdj);

            return devices;
        }

        public static string GetSelectedLabel()
        {
            string selected = AppSettings.PreviewAudioDevice;
            return string.IsNullOrWhiteSpace(selected) ? DefaultDevice : selected;
        }

        public static string GetVirtualDjHeadphoneDevice()
        {
            try
            {
                string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VirtualDJ", "settings.xml");
                if (!File.Exists(settings))
                    settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documentos", "VirtualDJ", "settings.xml");
                if (!File.Exists(settings)) return "";

                string xml = File.ReadAllText(settings, Encoding.UTF8);
                Match match = Regex.Match(xml, "source=\"headphones\"[^>]*soundcard=\"([^\"]+)\"|soundcard=\"([^\"]+)\"[^>]*source=\"headphones\"", RegexOptions.IgnoreCase);
                if (!match.Success) return "";
                string raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                Match name = Regex.Match(raw, @"\(([^)]+)\)");
                return name.Success ? name.Groups[1].Value : raw;
            }
            catch { return ""; }
        }
    }
}
