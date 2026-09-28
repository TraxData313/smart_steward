using System;
using System.IO;
using SmartSteward.Core;
using SmartSteward.Core.Presentation;

namespace SmartSteward.UI
{
    /// <summary>
    /// The window's remembered state on the disk (PLAN step 18): <c>Configs\SmartSteward\window_state.json</c> beside the
    /// settings — which Suggestion sections are folded (Core <see cref="WindowState"/> reads and writes the text; why it is
    /// its own file is written there). Read at the first window of a session and again whenever the file changed on disk
    /// (deleted by hand → every section unfolded); written on every fold / unfold. Never throws: a file it cannot read or
    /// write is a log line, and the state still holds for this session. Nothing is ever stored in the save.
    /// </summary>
    internal static class WindowStateHost
    {
        private static WindowState? _state;
        private static string? _stamp;

        private static FileSettingsStorage? _storage;

        public static string FilePath => Path.Combine(ModLog.ConfigDirectory, ModInfo.WindowStateFileName);

        /// <summary>The state as it stands — the file read afresh when it changed on disk since we last read or wrote it.</summary>
        public static WindowState Current
        {
            get
            {
                try
                {
                    var storage = Storage();
                    string? stamp = storage.Stamp();
                    if (_state == null || stamp != _stamp)
                    {
                        _state = WindowState.Parse(storage.ReadText(), out string? problem);
                        _stamp = stamp;
                        if (problem != null)
                            ModLog.Info("window", FilePath + ": " + problem);
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error("window", "reading " + FilePath, ex);
                    _state ??= new WindowState();
                }
                return _state;
            }
        }

        public static bool IsCollapsed(SectionGroup group) => Current.IsCollapsed(group);

        /// <summary>Folds or unfolds a section and writes the file at once (only when that changed something).</summary>
        public static void SetCollapsed(SectionGroup group, bool collapsed)
        {
            var state = Current;
            if (!state.SetCollapsed(group, collapsed))
                return;
            try
            {
                var storage = Storage();
                storage.WriteText(state.Generate());
                _stamp = storage.Stamp();
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "writing " + FilePath + " (the fold holds for this session)", ex);
            }
        }

        /// <summary>The same careful text file as settings.json (written beside and swapped in) — nothing settings-specific.</summary>
        private static FileSettingsStorage Storage() => _storage ??= new FileSettingsStorage(FilePath);
    }
}
