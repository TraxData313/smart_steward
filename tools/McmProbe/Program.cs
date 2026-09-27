using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MCM.Abstractions;
using MCM.Abstractions.FluentBuilder;
using MCM.Common;
using SmartSteward.Core.Settings;

// Drives MCM's real fluent builder with SmartSteward.dll's McmDefaults / McmGroup outside the game and
// checks what MCM would show and how its writes reach the settings service (see McmProbe.csproj).
// Everything MCM-typed lives in Run (NoInlining), JIT-compiled only after the resolver is in place.
static class Program
{
    static readonly Dictionary<string, string> Paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static int _fails;

    static int Main()
    {
        var folders = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value);
        // Our build first, then the game, MCM and Harmony — the order the game would find them in.
        foreach (var key in new[] { "ModuleBin", "GameBin", "McmBin", "HarmonyBin" })
        {
            if (!Directory.Exists(folders[key])) { Console.WriteLine("missing folder " + key + ": " + folders[key]); return 2; }
            foreach (var f in Directory.GetFiles(folders[key], "*.dll"))
                if (!Paths.ContainsKey(Path.GetFileNameWithoutExtension(f))) Paths[Path.GetFileNameWithoutExtension(f)] = f;
        }
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try { Run(Path.Combine(folders["ModuleBin"], "SmartSteward.dll")); }
        catch (Exception ex) { Console.WriteLine("CRASH " + ex); return 2; }
        Console.WriteLine(_fails == 0 ? "ALL CHECKS PASSED" : _fails + " CHECKS FAILED");
        return _fails == 0 ? 0 : 1;
    }

    static Assembly Resolve(object sender, ResolveEventArgs e) =>
        Paths.TryGetValue(new AssemblyName(e.Name).Name, out var p) ? Assembly.LoadFrom(p) : null;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
        if (!ok) _fails++;
    }

    sealed class Mem : ISettingsStorage
    {
        public string Text;
        int _v;
        public string ReadText() => Text;
        public void WriteText(string t) { Text = t; _v++; }
        public void WriteBackup(string t) { }
        public string Stamp() => Text == null ? null : _v.ToString();
    }

    static IEnumerable<ISettingsPropertyDefinition> All(SettingsPropertyGroupDefinition g) =>
        g.SettingProperties.Concat(g.SubGroups.SelectMany(All));

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string moduleDll)
    {
        var storage = new Mem();
        var service = new SettingsService(storage, m => Console.WriteLine("  log: " + m));
        service.Load();
        service.Set(SettingsRegistry.Find("MountMaxPrice"), 777); // current differs from the default

        var mod = Assembly.LoadFrom(moduleDll);
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var builderType = typeof(ISettingsBuilder).Assembly.GetType("MCM.Implementation.FluentBuilder.DefaultSettingsBuilder");
        var builder = (ISettingsBuilder)Activator.CreateInstance(builderType, any, null,
            new object[] { "SmartSteward", "{=ss_mcm_title}Smart Steward" }, null);
        builder.SetFormat("none").SetUIVersion(1).SetSubGroupDelimiter('/');

        var defaults = Activator.CreateInstance(mod.GetType("SmartSteward.McmDefaults"), true);
        builder.CreatePreset("default", "{=BaseSettings_Default}Default",
            (Action<ISettingsPresetBuilder>)Delegate.CreateDelegate(typeof(Action<ISettingsPresetBuilder>), defaults, "Fill"));
        var groupType = mod.GetType("SmartSteward.McmGroup");
        for (int i = 0; i < SettingsRegistry.Groups.Count; i++)
        {
            var g = Activator.CreateInstance(groupType, any, null, new object[] { service, SettingsRegistry.Groups[i], i }, null);
            if (!(bool)groupType.GetProperty("HasControls", any).GetValue(g)) continue;
            builder.CreateGroup((string)groupType.GetProperty("Name", any).GetValue(g),
                (Action<ISettingsPropertyGroupBuilder>)Delegate.CreateDelegate(typeof(Action<ISettingsPropertyGroupBuilder>), g, "Fill"));
        }
        var settings = builder.BuildAsGlobal();

        var groups = settings.SettingPropertyGroups.Where(g => All(g).Any()).ToList();
        var props = groups.SelectMany(All).ToList();
        var scalars = SettingsRegistry.All.Where(d => d.IsScalar).ToList();
        Check(groups.Count == 10, groups.Count + " groups with controls (10 expected: every group of DESIGN 7)");
        Check(props.Count == scalars.Count, props.Count + " controls for " + scalars.Count + " scalar settings");
        int wrong = 0;
        foreach (var def in scalars)
        {
            var p = props.SingleOrDefault(x => x.Id == def.Key);
            if (p == null) { Check(false, def.Key + " missing"); wrong++; continue; }
            var want = def.Kind == SettingKind.Bool ? SettingType.Bool
                : def.Kind == SettingKind.Int ? SettingType.Int
                : def.Kind == SettingKind.Float ? SettingType.Float : SettingType.Dropdown;
            if (p.SettingType != want || !p.DisplayName.StartsWith("{=ss_set_" + def.Key + "}") || !p.HintText.Contains("Default: "))
            {
                Check(false, def.Key + ": " + p.SettingType + " [" + p.DisplayName + "] [" + p.HintText + "]");
                wrong++;
            }
        }
        Check(wrong == 0, "every control has its type, its {=id} label and a hint with the default");
        var general = settings.SettingPropertyGroups.First(g => All(g).Any(p => p.Id == "ModEnabled"));
        Check(general.GroupNameRaw == "{=ss_grp_General}General", "group name " + general.GroupNameRaw);
        var target = props.Single(p => p.Id == "WarMountsManualTarget");
        Check(target.MinValue == -1 && target.MaxValue == 500, "int range " + target.MinValue + ".." + target.MaxValue);
        Console.WriteLine("  sample hint: " + props.Single(p => p.Id == "FoodPerMan").HintText);

        ISettingsPropertyDefinition P(string key) => props.Single(p => p.Id == key);

        // Live reads and writes, as MCM's SettingsPropertyVM does them (Value get / SetValueTypeAction).
        Check((int)P("MountMaxPrice").PropertyReference.Value == 777, "int reads the live value");
        P("SellLoot").PropertyReference.Value = true;
        Check(service.Current.SellLoot && storage.Text.Contains("\"SellLoot\": true"), "bool write reaches the service and the file");
        P("BuyPriceMultiplier").PropertyReference.Value = 1.5f;
        Check(service.Current.BuyPriceMultiplier == 1.5 && (float)P("BuyPriceMultiplier").PropertyReference.Value == 1.5f,
            "float write, read back as float");
        P("FoodPerMan").PropertyReference.Value = 1.2f;
        Check(service.Current.FoodPerMan == 1.2, "float 1.2f is stored as 1.2 (" + service.Current.FoodPerMan + ")");
        P("PackAnimalsTarget").PropertyReference.Value = 9999;
        Check(service.Current.PackAnimalsTarget == 500, "int write is clamped");

        // Dropdowns: MCM's SetSelectedIndexAction sets SelectedIndex on the object the getter returns.
        var strategy = P("FoodStrategy").PropertyReference;
        var wrapper = new SelectedIndexWrapper(strategy.Value);
        wrapper.SelectedIndex = 1;
        Check(service.Current.FoodStrategy == FoodStrategy.Cheapest, "dropdown pick via SelectedIndexWrapper reaches the service");
        Check(storage.Text.Contains("\"FoodStrategy\": \"Cheapest\""), "... and the file");
        service.Update(s => s.FoodStrategy = FoodStrategy.Balanced); // e.g. the file was edited
        Check(new SelectedIndexWrapper(strategy.Value).SelectedIndex == 0, "dropdown follows a change made elsewhere");
        var labels = ((Dropdown<string>)strategy.Value).ToList();
        Check(labels.SequenceEqual(new[] { "{=ss_opt_FoodStrategy_Balanced}Balanced (variety first)", "{=ss_opt_FoodStrategy_Cheapest}Cheapest" }),
            "dropdown labels: " + string.Join(" | ", labels));
        strategy.Value = new Dropdown<string>(labels, 1); // a preset hands a whole dropdown to the setter
        Check(service.Current.FoodStrategy == FoodStrategy.Cheapest, "dropdown setter (presets) reaches the service");

        // The Default preset holds the registry's defaults, not the values current at build time.
        var presets = settings.GetBuiltInPresets().ToList();
        Check(presets.Count == 1 && presets[0].Id == "default", "one built-in preset: default");
        // MCM's own preset loading needs its DI container (absent outside the game): read the preset
        // builder's values directly, then apply them the way SettingsUtils.OverrideValues does —
        // PropertyReference.Value = the preset's value, property by property.
        var builders = (System.Collections.IEnumerable)settings.GetType().GetProperty("Presets", any).GetValue(settings);
        var presetBuilder = builders.Cast<object>().Single();
        var values = (IDictionary<string, object>)presetBuilder.GetType().GetProperty("PropertyValues", any).GetValue(presetBuilder);
        Check(values.Count == scalars.Count, values.Count + " preset values");
        Check((int)values["MountMaxPrice"] == 500, "Default preset: MountMaxPrice 500 (current was 777)");
        Check((float)values["FoodPerMan"] == 2.0f, "Default preset: FoodPerMan 2.0");
        Check(((Dropdown<string>)values["FoodStrategy"]).SelectedIndex == 0, "Default preset: FoodStrategy Balanced");
        Check((bool)values["SellLoot"] == false, "Default preset: SellLoot off (current was on)");
        foreach (var pair in values)
            P(pair.Key).PropertyReference.Value = pair.Value;
        Check(service.Current.MountMaxPrice == 500 && service.Current.BuyPriceMultiplier == 1.2 && !service.Current.SellLoot
              && service.Current.PackAnimalsTarget == 10, "applying the Default preset resets the live settings");
        Check(service.Current.FoodStrategy == FoodStrategy.Balanced, "... dropdowns included (" + service.Current.FoodStrategy + ")");
    }
}
