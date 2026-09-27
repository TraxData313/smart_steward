using System;
using System.IO;
using System.Text;
using SmartSteward.Core;
using SmartSteward.Core.Settings;

namespace SmartSteward
{
    /// <summary>
    /// The settings the whole Module reads — ONE <see cref="SettingsService"/> over
    /// <c>Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\settings.json</c> (DESIGN §8).
    /// Loaded when the module loads; reloaded when the file changed on disk at campaign start and when
    /// the Party Steward window opens (step 7 calls <see cref="ReloadIfChanged"/>); every change from MCM
    /// or the Instructions tab goes through <see cref="Service"/>, which saves at once and raises
    /// <see cref="SettingsService.Changed"/> for the window. Always read <see cref="Current"/> afresh — a
    /// reload replaces the object.
    /// </summary>
    internal static class SettingsHost
    {
        private static SettingsService? _service;

        public static string FilePath => Path.Combine(ModLog.ConfigDirectory, ModInfo.SettingsFileName);

        /// <summary>The service, loading the file on first use.</summary>
        public static SettingsService Service => _service ??= Create();

        public static StewardSettings Current => Service.Current;

        /// <summary>Loads the file now (module load) so its problems reach the log early.</summary>
        public static void EnsureLoaded()
        {
            _ = Service;
        }

        /// <summary>Picks up an edit made outside the game. True when it reloaded.</summary>
        public static bool ReloadIfChanged() => Service.ReloadIfChanged();

        /// <summary>One log line naming every value that differs from the defaults.</summary>
        public static void LogInEffect(string when) =>
            ModLog.Info("settings", when + ": " + SettingsRegistry.DescribeNonDefaults(Current));

        private static SettingsService Create()
        {
            var service = new SettingsService(new FileSettingsStorage(FilePath), Log);
            service.Load();
            Log("using " + FilePath);
            return service;
        }

        private static void Log(string message) => ModLog.Info("settings", message);
    }

    /// <summary>settings.json and settings.json.bak on the disk — text in and out, nothing else (the
    /// rules live in Core's <see cref="SettingsService"/>, which also catches whatever this throws).</summary>
    internal sealed class FileSettingsStorage : ISettingsStorage
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;

        public FileSettingsStorage(string path)
        {
            _path = path;
        }

        /// <summary>UTF-8; a byte-order mark a player's editor added is skipped.</summary>
        public string? ReadText() => File.Exists(_path) ? File.ReadAllText(_path, Encoding.UTF8) : null;

        /// <summary>Written beside the file and swapped in, so a crash mid-write never leaves half a file.</summary>
        public void WriteText(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, text, Utf8NoBom);
            try
            {
                if (File.Exists(_path)) File.Replace(temp, _path, null);
                else File.Move(temp, _path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Some folders (sync clients, odd file systems) refuse the swap — write in place instead.
                File.WriteAllText(_path, text, Utf8NoBom);
                TryDelete(temp);
            }
        }

        public void WriteBackup(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".bak", text, Utf8NoBom);
        }

        public string? Stamp()
        {
            var info = new FileInfo(_path);
            return info.Exists ? info.Length + ":" + info.LastWriteTimeUtc.Ticks : null;
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // a stray .tmp is harmless
            }
        }
    }
}
