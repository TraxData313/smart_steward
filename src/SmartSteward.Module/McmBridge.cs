using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MCM.Abstractions.FluentBuilder;
using MCM.Abstractions.FluentBuilder.Models;
using MCM.Common;
using SmartSteward.Core;
using SmartSteward.Core.Settings;

namespace SmartSteward
{
    /// <summary>
    /// The Mod Options page, built with MCM v5's FLUENT builder from <see cref="SettingsRegistry"/> and bound
    /// live to <see cref="SettingsHost"/>: each control reads the current value and every change goes
    /// through the settings service, which clamps it, saves settings.json and tells the window. MCM stores
    /// nothing of its own (format "none") — settings.json stays the one store.
    /// <para><b>MCM is a SOFT dependency — the rules this file keeps.</b> The 1.4.8 loader calls
    /// <c>GetTypes()</c> on our DLL and unloads the WHOLE module if any type fails to load (RESEARCH §12).
    /// Loading a type resolves its base type, interfaces and field types; method bodies (and signatures)
    /// are resolved only when a method runs. So here: MCM types appear only inside method bodies and
    /// signatures, never as a base type, a field (the built settings object is held as <c>object</c>) or a
    /// generic argument of a field; NO lambdas or iterators (their compiler classes get fields — a cached
    /// <c>Action&lt;ISettingsPropertyGroupBuilder&gt;</c> is one) — every delegate is made from an INSTANCE
    /// method of a small class whose fields are our own types; and nothing reaches an MCM-touching method
    /// unless <see cref="IsMcmLoaded"/> said yes. The out-of-game check (GetTypes without MCM) is in
    /// TASKS_DONE, step 5.</para>
    /// </summary>
    internal static class McmBridge
    {
        private const string McmAssemblyName = "MCMv5";

        /// <summary>The registered <c>FluentGlobalSettings</c>, held as object (see the class comment).</summary>
        private static object? _registered;

        private static bool _gaveUp;
        private static bool _saidAbsent;

        /// <summary>MCM's runtime assembly is loaded (the module is installed and enabled).</summary>
        public static bool IsMcmLoaded()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (string.Equals(assembly.GetName().Name, McmAssemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Registers the Mod Options page once, if MCM is there. Safe to call any time and again —
        /// MCM's services exist only after its own OnBeforeInitialModuleScreenSetAsRoot, so a call before
        /// that just waits for the next one. Any failure costs the page, never the mod.</summary>
        public static void TryRegister()
        {
            if (_registered != null || _gaveUp) return;
            if (!IsMcmLoaded())
            {
                if (!_saidAbsent) ModLog.Info("mcm", "Mod Configuration Menu not loaded - settings.json is the way in");
                _saidAbsent = true;
                return;
            }
            try
            {
                _registered = Register(SettingsHost.Service);
                ModLog.Info("mcm", _registered != null
                    ? "Mod Options page registered"
                    : "MCM is loaded but not ready yet - trying again later");
            }
            catch (Exception ex)
            {
                _gaveUp = true; // a real exception will not heal itself
                ModLog.Error("mcm", "registering the Mod Options page failed - settings.json still works", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object? Register(SettingsService service)
        {
            // MCM's mod list shows the display name as it is (its own MCMSettings resolves the TextObject
            // itself), so the title is resolved here — groups and settings MCM resolves on its own.
            var title = new TaleWorlds.Localization.TextObject("{=ss_mcm_title}" + ModInfo.Name).ToString();
            ISettingsBuilder? builder = BaseSettingsBuilder.Create(ModInfo.Id, title);
            if (builder == null) return null;

            // "none" is a format no MCM service claims, so MCM neither loads nor saves anything for us.
            // (Its default "memory" format crashes on the first registration in 5.12.3: MemorySettingsFormat.Load
            // hands a null to OverrideSettings when it has no copy yet — RESEARCH §12.)
            builder.SetFormat("none").SetUIVersion(1).SetSubGroupDelimiter('/');

            // MCM's "Default" preset (and its reset buttons) snapshots the CURRENT values when the page is
            // built; fill it first with the registry's real defaults — later values never overwrite a set one.
            builder.CreatePreset("default", "{=BaseSettings_Default}Default",
                new Action<ISettingsPresetBuilder>(new McmDefaults().Fill));

            for (int i = 0; i < SettingsRegistry.Groups.Count; i++)
            {
                var group = new McmGroup(service, SettingsRegistry.Groups[i], i);
                if (group.HasControls)
                    builder.CreateGroup(group.Name, new Action<ISettingsPropertyGroupBuilder>(group.Fill));
            }

            var settings = builder.BuildAsGlobal();
            settings.Register();
            return settings;
        }

        /// <summary>A text MCM shows through TextObject: a string id (translations, step 9) and the English.</summary>
        internal static string Text(string id, string english) => "{=" + id + "}" + english;
    }

    /// <summary>The registry's defaults as MCM's Default preset.</summary>
    internal sealed class McmDefaults
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Fill(ISettingsPresetBuilder preset)
        {
            foreach (var def in SettingsRegistry.All)
            {
                switch (def)
                {
                    case BoolSetting b:
                        preset.SetPropertyValue(def.Key, b.Default);
                        break;
                    case IntSetting n:
                        preset.SetPropertyValue(def.Key, n.Default);
                        break;
                    case FloatSetting f:
                        preset.SetPropertyValue(def.Key, (float)f.Default);
                        break;
                    case EnumSetting e:
                        preset.SetPropertyValue(def.Key, new Dropdown<string>(McmBinding.DropdownLabels(e), e.DefaultIndex));
                        break;
                }
            }
        }
    }

    /// <summary>One §7 group as an MCM group: its scalar settings in registry order.</summary>
    internal sealed class McmGroup
    {
        private readonly SettingsService _service;
        private readonly string _group;
        private readonly int _order;

        public McmGroup(SettingsService service, string group, int order)
        {
            _service = service;
            _group = group;
            _order = order;
        }

        public string Name => McmBridge.Text("ss_grp_" + _group.Replace(" ", ""), SettingsRegistry.GroupLabel(_group));

        /// <summary>The group has something MCM can show (the price book and the prisoner list are not).</summary>
        public bool HasControls
        {
            get
            {
                foreach (var def in SettingsRegistry.All)
                    if (def.Group == _group && def.IsScalar)
                        return true;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Fill(ISettingsPropertyGroupBuilder group)
        {
            group.SetGroupOrder(_order);
            int order = 0;
            foreach (var def in SettingsRegistry.All)
            {
                if (def.Group != _group || !def.IsScalar) continue;
                var name = McmBridge.Text("ss_set_" + def.Key, def.Label);
                var look = new McmLook(McmBridge.Text("ss_hint_" + def.Key, def.Hint), order++);
                var binding = new McmBinding(_service, def);
                switch (def)
                {
                    case BoolSetting _:
                        group.AddBool(def.Key, name,
                            new ProxyRef<bool>(new Func<bool>(binding.GetBool), new Action<bool>(binding.SetBool)),
                            new Action<ISettingsPropertyBoolBuilder>(look.Bool));
                        break;
                    case IntSetting n:
                        group.AddInteger(def.Key, name, n.Min, n.Max,
                            new ProxyRef<int>(new Func<int>(binding.GetInt), new Action<int>(binding.SetInt)),
                            new Action<ISettingsPropertyIntegerBuilder>(look.Int));
                        break;
                    case FloatSetting f:
                        group.AddFloatingInteger(def.Key, name, (float)f.Min, (float)f.Max,
                            new ProxyRef<float>(new Func<float>(binding.GetFloat), new Action<float>(binding.SetFloat)),
                            new Action<ISettingsPropertyFloatingIntegerBuilder>(look.Float));
                        break;
                    case EnumSetting e:
                        group.AddDropdown(def.Key, name, e.DefaultIndex,
                            new ProxyRef<Dropdown<string>>(new Func<Dropdown<string>>(binding.GetDropdown),
                                new Action<Dropdown<string>>(binding.SetDropdown)),
                            new Action<ISettingsPropertyDropdownBuilder>(look.Dropdown));
                        break;
                }
            }
        }
    }

    /// <summary>A control's hint and position.</summary>
    internal sealed class McmLook
    {
        private readonly string _hint;
        private readonly int _order;

        public McmLook(string hint, int order)
        {
            _hint = hint;
            _order = order;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Bool(ISettingsPropertyBoolBuilder property) => property.SetHintText(_hint).SetOrder(_order);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Int(ISettingsPropertyIntegerBuilder property) => property.SetHintText(_hint).SetOrder(_order);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Float(ISettingsPropertyFloatingIntegerBuilder property) =>
            property.SetHintText(_hint).SetOrder(_order).AddValueFormat("0.00");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Dropdown(ISettingsPropertyDropdownBuilder property) => property.SetHintText(_hint).SetOrder(_order);
    }

    /// <summary>
    /// One control's live binding: the getter reads <see cref="SettingsService.Current"/> every time (a
    /// reload replaces it), the setter goes through the service (clamp, save, Changed).
    /// <para>Dropdowns are different: MCM never calls the setter for a pick — it sets <c>SelectedIndex</c> on
    /// the object the getter returned. So each enum keeps ONE dropdown (held as object), listens to its
    /// SelectedIndex, and is re-synced to the current value whenever MCM reads it.</para>
    /// </summary>
    internal sealed class McmBinding
    {
        private readonly SettingsService _service;
        private readonly SettingDefinition _def;

        /// <summary>The enum's <c>Dropdown&lt;string&gt;</c>, held as object (see McmBridge).</summary>
        private object? _dropdown;

        private bool _syncing;

        public McmBinding(SettingsService service, SettingDefinition def)
        {
            _service = service;
            _def = def;
        }

        public bool GetBool() => ((BoolSetting)_def).Get(_service.Current);

        public void SetBool(bool value) => _service.Set(_def, value);

        public int GetInt() => ((IntSetting)_def).Get(_service.Current);

        public void SetInt(int value) => _service.Set(_def, value);

        public float GetFloat() => (float)((FloatSetting)_def).Get(_service.Current);

        /// <summary>MCM's float (1.2f = 1.2000000476…) — the registry rounds it back to 1.2.</summary>
        public void SetFloat(float value) => _service.Set(_def, (double)value);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Dropdown<string> GetDropdown()
        {
            var setting = (EnumSetting)_def;
            var index = setting.GetIndex(_service.Current);
            if (!(_dropdown is Dropdown<string> dropdown))
            {
                dropdown = new Dropdown<string>(DropdownLabels(setting), index);
                ((INotifyPropertyChanged)dropdown).PropertyChanged += OnDropdownChanged;
                _dropdown = dropdown;
            }
            if (dropdown.SelectedIndex != index)
            {
                _syncing = true;
                try
                {
                    dropdown.SelectedIndex = index;
                }
                finally
                {
                    _syncing = false;
                }
            }
            return dropdown;
        }

        /// <summary>Called when a preset hands over a whole dropdown.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetDropdown(Dropdown<string> value)
        {
            if (value != null) _service.Set(_def, value.SelectedIndex);
        }

        private void OnDropdownChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_syncing || e.PropertyName != "SelectedIndex") return;
            ReadDropdown();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReadDropdown()
        {
            if (_dropdown is Dropdown<string> dropdown) _service.Set(_def, dropdown.SelectedIndex);
        }

        /// <summary>The dropdown's texts, each with its string id.</summary>
        internal static List<string> DropdownLabels(EnumSetting setting)
        {
            var labels = new List<string>();
            for (int i = 0; i < setting.Names.Count; i++)
                labels.Add(McmBridge.Text("ss_opt_" + setting.Key + "_" + setting.Names[i], setting.Labels[i]));
            return labels;
        }
    }
}
