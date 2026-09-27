using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Settings
{
    /// <summary>Where the settings text lives. The Module implements it on the disk (settings.json and
    /// settings.json.bak); tests keep it in memory. Any method may throw — the service catches.</summary>
    public interface ISettingsStorage
    {
        /// <summary>The file's text, or null when there is no file.</summary>
        string? ReadText();

        void WriteText(string text);

        /// <summary>Keeps <paramref name="text"/> as the backup (settings.json.bak), replacing an older one.</summary>
        void WriteBackup(string text);

        /// <summary>Anything that changes whenever the file changes (size + last write time); null when
        /// there is no file.</summary>
        string? Stamp();
    }

    /// <summary>
    /// The one live set of settings the whole mod reads (DESIGN §8), kept in step with the file:
    /// <see cref="Load"/> at start, <see cref="ReloadIfChanged"/> when the player may have edited the file
    /// (campaign start, the Party Steward window opening), and every change through <see cref="Set"/> /
    /// <see cref="Update"/> saved at once and announced through <see cref="Changed"/>.
    /// <para>Never throws and never loses the game: a file it cannot read keeps the current values (nothing
    /// is written over it); a file it CAN read but not understand is kept as the backup and replaced; a
    /// failed write is logged and the new values still apply for this session.</para>
    /// <para>Always read <see cref="Current"/> afresh — a reload replaces the object.</para>
    /// </summary>
    public sealed class SettingsService
    {
        private readonly ISettingsStorage _storage;
        private readonly Action<string> _log;
        private string _text;
        private string? _stamp;

        public SettingsService(ISettingsStorage storage, Action<string> log)
        {
            _storage = storage;
            _log = log;
            Current = new StewardSettings();
            _text = SettingsFile.Generate(Current);
        }

        public StewardSettings Current { get; private set; }

        /// <summary>Raised after the values changed — a reload that changed something, or a change through
        /// this service. The Party Steward window re-plans on it.</summary>
        public event Action? Changed;

        /// <summary>Reads the file into <see cref="Current"/>, writing it back when it was missing, broken,
        /// out of range or not in the current layout (new keys, old formatting). See the class comment.</summary>
        public void Load()
        {
            string? text;
            try
            {
                text = _storage.ReadText();
            }
            catch (Exception ex)
            {
                _log("settings file could not be read (" + ex.Message + ") - keeping the current settings");
                return;
            }

            StewardSettings loaded;
            bool backup = false;
            if (text == null)
            {
                _log("no settings file yet - writing one with the defaults");
                loaded = new StewardSettings();
            }
            else
            {
                var parsed = SettingsFile.Parse(text);
                if (parsed.Unreadable)
                    _log("settings file unreadable (" + parsed.Error + ") - kept as the .bak, every setting back to its default");
                foreach (var problem in parsed.Problems) _log("settings file, " + problem);
                foreach (var note in parsed.Renamed) _log("settings file, " + note);
                if (!parsed.Unreadable && parsed.MissingKeys.Count > 0)
                    _log("settings file had no " + string.Join(", ", parsed.MissingKeys) + " - default used");
                backup = parsed.LosesSomething;
                loaded = parsed.Settings;
            }

            var generated = SettingsFile.Generate(loaded);
            if (text != generated)
            {
                if (backup && text != null) TryBackup(text);
                TryWrite(generated);
            }
            _stamp = TryStamp();
            Replace(loaded, generated);
        }

        /// <summary>Reloads when the file changed on disk since this service last read or wrote it (edited,
        /// or deleted — then a fresh default file is written). True when it reloaded.</summary>
        public bool ReloadIfChanged()
        {
            var stamp = TryStamp();
            if (stamp == _stamp) return false;
            _log("settings file changed on disk - reloading");
            Load();
            return true;
        }

        /// <summary>Sets one scalar setting (clamped by its definition), saves and announces when the value
        /// changed. <paramref name="value"/> is bool, a number or the enum's index / name / value. False when
        /// nothing changed.</summary>
        public bool Set(SettingDefinition def, object value)
        {
            switch (def)
            {
                case BoolSetting b when value is bool flag:
                    b.Set(Current, flag);
                    break;
                case IntSetting n when IsNumber(value):
                    n.Set(Current, n.Clamp((long)Math.Round(Convert.ToDouble(value))));
                    break;
                case FloatSetting f when IsNumber(value):
                    f.Set(Current, Convert.ToDouble(value));
                    break;
                case EnumSetting e when value is int index:
                    e.SetIndex(Current, index);
                    break;
                case EnumSetting e when value is string name:
                    e.SetIndex(Current, e.IndexOf(name));
                    break;
                case EnumSetting e when value is Enum:
                    e.SetIndex(Current, e.IndexOf(value.ToString()));
                    break;
                default:
                    _log("settings: " + def.Key + " cannot take " + (value?.GetType().Name ?? "null") + " - ignored");
                    return false;
            }
            return Commit();
        }

        /// <summary>Any edit of <see cref="Current"/> — the price book, the prisoner list, several values at
        /// once. The result is normalised (clamped, empty entries dropped) the same way the file is read,
        /// then saved and announced if anything changed.</summary>
        public bool Update(Action<StewardSettings> edit)
        {
            try
            {
                edit(Current);
            }
            catch (Exception ex)
            {
                _log("settings: an edit failed (" + ex.Message + ") - reloading the saved values");
                Current = SettingsFile.Parse(_text).Settings;
                return false;
            }
            var normalized = SettingsFile.Parse(SettingsFile.Generate(Current));
            foreach (var problem in normalized.Problems) _log("settings: " + problem);
            Current = normalized.Settings;
            return Commit();
        }

        private static bool IsNumber(object value) =>
            value is int || value is long || value is float || value is double || value is decimal
            || value is short || value is byte;

        /// <summary>Saves and announces when the file's text for <see cref="Current"/> differs from the last
        /// one — the text is the one complete fingerprint of every value.</summary>
        private bool Commit()
        {
            var generated = SettingsFile.Generate(Current);
            if (generated == _text) return false;
            TryWrite(generated);
            _stamp = TryStamp();
            _text = generated;
            RaiseChanged();
            return true;
        }

        private void Replace(StewardSettings settings, string text)
        {
            Current = settings;
            if (text == _text) return;
            _text = text;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    _log("settings: a change listener failed - " + ex);
                }
            }
        }

        private void TryWrite(string text)
        {
            try
            {
                _storage.WriteText(text);
            }
            catch (Exception ex)
            {
                _log("settings file could not be written (" + ex.Message + ") - the values still apply until the game quits");
            }
        }

        private void TryBackup(string text)
        {
            try
            {
                _storage.WriteBackup(text);
            }
            catch (Exception ex)
            {
                _log("settings backup could not be written (" + ex.Message + ")");
            }
        }

        private string? TryStamp()
        {
            try
            {
                return _storage.Stamp();
            }
            catch
            {
                return null;
            }
        }
    }
}
