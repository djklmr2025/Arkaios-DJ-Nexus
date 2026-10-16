using System;
using System.IO;
using System.Text;

namespace ArkaiosDJAssistant
{
    public static class DiagLog
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;

        public static string LogFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArkaiosDJ");
                    try
                    {
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    }
                    catch { }
                    _logFilePath = Path.Combine(folder, "diagnostico.log");
                }
                return _logFilePath;
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (_lock)
                {
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, line, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void Write(Exception ex, string context = "")
        {
            if (ex == null) return;
            string prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            Write($"{prefix}EXCEPCIÓN: {ex.GetType().Name} - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
        }

        public static void OpenLog()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogFilePath) { UseShellExecute = true });
                }
            }
            catch { }
        }
    }
}
