using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ArkaiosDJAssistant
{
    public static class Mp3tagBridge
    {
        private static readonly string[] CandidatePaths =
        {
            @"C:\ARKAIOS\tageditor-master\Mp3tag.exe",
            @"C:\Program Files\Mp3tag\Mp3tag.exe",
            @"C:\Program Files (x86)\Mp3tag\Mp3tag.exe"
        };

        public static bool IsAvailable()
        {
            return !string.IsNullOrWhiteSpace(FindExecutable());
        }

        public static string GetStatus()
        {
            string exe = FindExecutable();
            return string.IsNullOrWhiteSpace(exe)
                ? "Mp3tag no encontrado."
                : "Mp3tag activo: " + exe;
        }

        public static bool OpenFiles(IEnumerable<string> paths, out string error)
        {
            error = null;
            string exe = FindExecutable();
            if (string.IsNullOrWhiteSpace(exe))
            {
                error = "No se encontro Mp3tag.exe.";
                return false;
            }

            string[] existing = (paths ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(40)
                .ToArray();

            if (existing.Length == 0)
            {
                error = "No hay archivos validos para abrir en Mp3tag.";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = string.Join(" ", existing.Select(Quote).ToArray()),
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe)
                });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string FindExecutable()
        {
            foreach (string path in CandidatePaths)
                if (File.Exists(path)) return path;
            return null;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
