using System;
using System.IO;
using SmartSteward.Core;

namespace SmartSteward
{
    /// <summary>
    /// The mod's rolling log: <c>Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\smart_steward.log</c>,
    /// one timestamped line per event (what was planned, what was executed — DESIGN §8), kept for
    /// bug reports. Trimmed to its newest half once it tops ~1 MB. Best-effort by design: a failed
    /// write must never cost gameplay, so every call swallows its own errors.
    /// </summary>
    internal static class ModLog
    {
        private const long TrimAtBytes = 1_000_000;
        private static readonly object Gate = new object();

        /// <summary>Shared by the settings file (step 5). MyDocuments follows a redirected
        /// (e.g. OneDrive) Documents folder, as the game itself does.</summary>
        public static string ConfigDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", ModInfo.ConfigFolderName);

        public static string LogFilePath => Path.Combine(ConfigDirectory, ModInfo.LogFileName);

        /// <summary>One line: <c>2026.09.27 20:15:03 [area] message</c>. The area is a short fixed
        /// tag (load, campaign, plan, execute…) so the file greps clean.</summary>
        public static void Info(string area, string message) => Write(area, message);

        /// <summary>An error line with the exception's type, message and stack.</summary>
        public static void Error(string area, string message, Exception ex) =>
            Write(area, "ERROR " + message + " — " + ex);

        private static void Write(string area, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(ConfigDirectory);
                    File.AppendAllText(LogFilePath,
                        DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")
                        + " [" + area + "] " + message + Environment.NewLine);
                    TrimIfHuge();
                }
            }
            catch { /* the log is a luxury, the game is not */ }
        }

        /// <summary>Keeps the newest half once the file tops <see cref="TrimAtBytes"/>, cut at a
        /// line break so no half-line survives.</summary>
        private static void TrimIfHuge()
        {
            var info = new FileInfo(LogFilePath);
            if (!info.Exists || info.Length <= TrimAtBytes) return;
            var text = File.ReadAllText(LogFilePath);
            var cut = text.IndexOf('\n', text.Length / 2);
            if (cut < 0) return;
            File.WriteAllText(LogFilePath,
                "(older lines trimmed " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + ")"
                + Environment.NewLine + text.Substring(cut + 1));
        }
    }
}
