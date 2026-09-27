using System;
using System.IO;
using SmartSteward.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SmartSteward
{
    /// <summary>
    /// The game's entry point (SubModule.xml → SubModuleClassType). Loads the settings, offers them to
    /// MCM when it is installed, registers the steward's campaign behavior on every campaign start and
    /// says hello once the campaign map is up.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        // Armed on every campaign start (new or loaded), fired on the first frame the campaign map
        // is showing: a message sent while the loading screen is still up may never be seen.
        private static bool _announcePending;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("load", ModInfo.Name + " loaded — " + DescribeBuild());
            try
            {
                // Read settings.json now, so a broken file is repaired and logged before anything asks.
                SettingsHost.EnsureLoaded();
            }
            catch (Exception ex)
            {
                ModLog.Error("settings", "loading at module load", ex);
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            // MCM builds its services in its own OnBeforeInitialModuleScreenSetAsRoot; as an optional
            // dependency it loads (and runs this hook) before us. No MCM → a log line, nothing else.
            McmBridge.TryRegister();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            // Custom battles and multiplayer hand over other starters — the steward lives on the
            // campaign map only.
            if (gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new SmartStewardBehavior());
                _announcePending = true;
                ModLog.Info("campaign", "campaign starting — behavior registered");
                try
                {
                    SettingsHost.ReloadIfChanged(); // edited at the main menu? pick it up
                    SettingsHost.LogInEffect("settings in effect");
                    McmBridge.TryRegister();        // in case MCM was not ready at the main menu
                }
                catch (Exception ex)
                {
                    ModLog.Error("settings", "campaign start", ex);
                }
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            UI.StewardWindow.Tick(dt); // cheap when the window is closed
            if (!_announcePending) return;
            try
            {
                if (Game.Current?.GameStateManager?.ActiveState is MapState)
                {
                    _announcePending = false;
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=ss_loaded}Smart Steward loaded.").ToString()));
                    ModLog.Info("campaign", "campaign map up — announced");
                }
            }
            catch (Exception ex)
            {
                _announcePending = false;
                ModLog.Error("campaign", "announcing", ex);
            }
        }

        public override void OnGameEnd(Game game)
        {
            _announcePending = false;
            UI.StewardWindow.Close(); // a window must not outlive its campaign
            base.OnGameEnd(game);
        }

        /// <summary>"SmartSteward.Dev, build 2026.09.27 20:15" — which install and which build is
        /// running, so a playtest log always says what was tested.</summary>
        private static string DescribeBuild()
        {
            try
            {
                var dll = typeof(SubModule).Assembly.Location;
                // <module folder>\bin\Win64_Shipping_Client\SmartSteward.dll
                var moduleFolder = new FileInfo(dll).Directory?.Parent?.Parent?.Name ?? "?";
                return moduleFolder + ", build " + File.GetLastWriteTime(dll).ToString("yyyy.MM.dd HH:mm");
            }
            catch
            {
                return "build unknown";
            }
        }
    }
}
