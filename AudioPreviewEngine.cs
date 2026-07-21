using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ArkaiosDJAssistant
{
    public static class AudioPreviewEngine
    {
        private const string Alias = "arkaios_preview";

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr winHandle);

        public static string CurrentPath { get; private set; }
        public static bool IsOpen { get; private set; }
        public static bool IsPaused { get; private set; }
        private static object wmpPlayer;
        private static bool usingWmp;

        public static bool Play(string path, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "Archivo no encontrado.";
                return false;
            }

            Stop();
            string mciError;
            if (PlayWithMci(path, out mciError))
            {
                CurrentPath = path;
                IsOpen = true;
                IsPaused = false;
                usingWmp = false;
                return true;
            }

            string wmpError;
            if (PlayWithWindowsMediaPlayer(path, out wmpError))
            {
                CurrentPath = path;
                IsOpen = true;
                IsPaused = false;
                usingWmp = true;
                return true;
            }

            error = "MCI: " + mciError + " | WMP: " + wmpError;
            return false;
        }

        public static bool Pause(out string error)
        {
            error = "";
            if (!IsOpen) return true;
            if (usingWmp) return InvokeWmpControl("pause", out error);
            int code = Send("pause " + Alias);
            if (code != 0)
            {
                error = GetError(code);
                return false;
            }
            IsPaused = true;
            return true;
        }

        public static bool Resume(out string error)
        {
            error = "";
            if (!IsOpen) return true;
            if (usingWmp) return InvokeWmpControl("play", out error);
            int code = Send("resume " + Alias);
            if (code != 0) code = Send("play " + Alias);
            if (code != 0)
            {
                error = GetError(code);
                return false;
            }
            IsPaused = false;
            return true;
        }

        public static void Stop()
        {
            if (usingWmp && wmpPlayer != null)
            {
                string ignored;
                InvokeWmpControl("stop", out ignored);
                TrySetWmpUrl("");
            }
            if (IsOpen)
            {
                Send("stop " + Alias);
                Send("close " + Alias);
            }
            CurrentPath = null;
            IsOpen = false;
            IsPaused = false;
            usingWmp = false;
        }

        private static bool PlayWithMci(string path, out string error)
        {
            error = "";
            int open = Send("open \"" + path + "\" alias " + Alias);
            if (open != 0)
            {
                error = GetError(open);
                if (string.IsNullOrWhiteSpace(error)) error = "No acepto abrir el archivo.";
                return false;
            }

            int play = Send("play " + Alias);
            if (play != 0)
            {
                error = GetError(play);
                if (string.IsNullOrWhiteSpace(error)) error = "No acepto reproducir el archivo.";
                Send("close " + Alias);
                return false;
            }

            return true;
        }

        private static bool PlayWithWindowsMediaPlayer(string path, out string error)
        {
            error = "";
            try
            {
                if (wmpPlayer == null)
                {
                    Type type = Type.GetTypeFromProgID("WMPlayer.OCX");
                    if (type == null)
                    {
                        error = "Windows Media Player OCX no esta registrado.";
                        return false;
                    }
                    wmpPlayer = Activator.CreateInstance(type);
                    TrySetWmpProperty("autoStart", true);
                }

                TrySetWmpProperty("URL", path);
                return InvokeWmpControl("play", out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool InvokeWmpControl(string action, out string error)
        {
            error = "";
            try
            {
                if (wmpPlayer == null)
                {
                    error = "Motor WMP no inicializado.";
                    return false;
                }

                object controls = wmpPlayer.GetType().InvokeMember("controls", BindingFlags.GetProperty, null, wmpPlayer, null);
                controls.GetType().InvokeMember(action, BindingFlags.InvokeMethod, null, controls, null);
                IsPaused = action == "pause";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void TrySetWmpUrl(string value)
        {
            TrySetWmpProperty("URL", value);
        }

        private static void TrySetWmpProperty(string name, object value)
        {
            try
            {
                if (wmpPlayer != null)
                    wmpPlayer.GetType().InvokeMember(name, BindingFlags.SetProperty, null, wmpPlayer, new object[] { value });
            }
            catch { }
        }

        private static int Send(string command)
        {
            return mciSendString(command, null, 0, IntPtr.Zero);
        }

        private static string GetError(int code)
        {
            var buffer = new StringBuilder(256);
            mciSendString("error " + code, buffer, buffer.Capacity, IntPtr.Zero);
            return buffer.ToString();
        }
    }
}
