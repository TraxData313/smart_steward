using System.Text.RegularExpressions;
using System.Xml.Linq;
using SmartSteward.Core;

namespace SmartSteward.Core.Tests;

/// <summary>
/// Holds module\SubModule.xml, the Module project and <see cref="ModInfo"/> together: the game
/// finds our code only through the manifest's Id / DLLName / SubModuleClassType, and a drift
/// between them fails silently in the launcher. Also guards two of Anton's musts at the manifest
/// level: nothing but the official campaign modules is required, and MCM stays optional.
/// </summary>
public class ManifestTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly XElement Manifest = XDocument.Load(Path.Combine(RepoRoot, "module", "SubModule.xml")).Root!;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartSteward.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("SmartSteward.sln not found above " + AppContext.BaseDirectory);
    }

    private static string Value(XElement parent, string name) =>
        parent.Element(name)?.Attribute("value")?.Value ?? throw new InvalidOperationException(name + " missing");

    [Fact]
    public void Id_and_name_match_ModInfo()
    {
        Assert.Equal(ModInfo.Id, Value(Manifest, "Id"));
        Assert.Equal(ModInfo.Name, Value(Manifest, "Name"));
    }

    [Fact]
    public void Version_has_the_game_launcher_shape()
    {
        Assert.Matches(new Regex(@"^v\d+\.\d+\.\d+$"), Value(Manifest, "Version"));
    }

    [Fact]
    public void The_single_submodule_points_at_the_module_assembly_and_class()
    {
        var subModule = Assert.Single(Manifest.Element("SubModules")!.Elements("SubModule"));
        Assert.Equal(ModInfo.AssemblyName + ".dll", Value(subModule, "DLLName"));
        Assert.Equal(ModInfo.SubModuleClassType, Value(subModule, "SubModuleClassType"));

        var csproj = XDocument.Load(Path.Combine(RepoRoot, "src", "SmartSteward.Module", "SmartSteward.Module.csproj"));
        Assert.Equal(ModInfo.AssemblyName, csproj.Descendants("AssemblyName").Single().Value);
    }

    [Fact]
    public void Only_official_campaign_modules_are_required_and_MCM_is_optional()
    {
        var required = new[] { "Native", "SandBoxCore", "Sandbox" };
        var deps = Manifest.Element("DependedModules")!.Elements("DependedModule").ToList();
        foreach (var dep in deps)
        {
            var id = dep.Attribute("Id")!.Value;
            var optional = string.Equals(dep.Attribute("Optional")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            Assert.True(required.Contains(id) || optional, id + " must not be a hard dependency");
        }
        foreach (var id in required)
            Assert.Contains(deps, d => d.Attribute("Id")!.Value == id);

        // MCM: optional in BOTH lists (vanilla launcher reads the first, BUTR/BLSE the second).
        var mcm = Assert.Single(deps, d => d.Attribute("Id")!.Value == "Bannerlord.MBOptionScreen");
        Assert.Equal("true", mcm.Attribute("Optional")?.Value);
        var mcmMeta = Assert.Single(Manifest.Element("DependedModuleMetadatas")!.Elements("DependedModuleMetadata"),
            m => m.Attribute("id")!.Value == "Bannerlord.MBOptionScreen");
        Assert.Equal("true", mcmMeta.Attribute("optional")?.Value);
        Assert.Equal("LoadBeforeThis", mcmMeta.Attribute("order")?.Value);
    }

    [Fact]
    public void Both_dependency_lists_name_the_same_modules()
    {
        var deps = Manifest.Element("DependedModules")!.Elements("DependedModule")
            .Select(d => d.Attribute("Id")!.Value).OrderBy(x => x);
        var meta = Manifest.Element("DependedModuleMetadatas")!.Elements("DependedModuleMetadata")
            .Select(d => d.Attribute("id")!.Value).OrderBy(x => x);
        Assert.Equal(deps, meta);
    }
}
