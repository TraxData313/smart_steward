namespace SmartSteward.Core
{
    /// <summary>
    /// The mod's fixed identity — the names the manifest, the Module and the tools must agree on.
    /// <c>module\SubModule.xml</c> repeats Id, Name, DLL and class; a test holds them together.
    /// The VERSION is deliberately not here: it lives only in SubModule.xml and is bumped once, on
    /// release day (CLAUDE.md, release rhythm).
    /// </summary>
    public static class ModInfo
    {
        /// <summary>Module id of the release build. The local dev deploy installs as
        /// <c>SmartSteward.Dev</c> (tools\deploy.ps1) so it can sit beside a Workshop copy.</summary>
        public const string Id = "SmartSteward";

        public const string Name = "Smart Steward";

        /// <summary>The Module project's assembly name — SubModule.xml's DLLName is this + ".dll".</summary>
        public const string AssemblyName = "SmartSteward";

        /// <summary>The MBSubModuleBase class the game instantiates (SubModule.xml's SubModuleClassType).</summary>
        public const string SubModuleClassType = "SmartSteward.SubModule";

        /// <summary>Folder under <c>Documents\Mount and Blade II Bannerlord\Configs\</c> holding the
        /// settings file and the log — shared by the dev and the release build.</summary>
        public const string ConfigFolderName = "SmartSteward";

        public const string LogFileName = "smart_steward.log";
    }
}
