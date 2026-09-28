using System;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Party Steward window's host (DESIGN §1, PLAN step 7): one Gauntlet layer with one movie
    /// (<c>module\GUI\Prefabs\SmartStewardWindow.xml</c>) over the map screen, the pattern of vanilla's map views and
    /// TrainingBattles' TrainingWindow (RESEARCH §11, §14). Everything here is best-effort: a failure closes the
    /// window with a message, never the game.
    /// <list type="bullet">
    /// <item>Layer order <see cref="LayerOrder"/> = 305, BELOW the Encyclopedia (310): focus follows layer order, so
    ///   a name link can open the Encyclopedia over us; when it closes, we take the focus back.</item>
    /// <item>The game's own hot keys: Shift = <c>FiveStackModifier</c>, Ctrl = <c>EntireStackModifier</c>
    ///   (GenericCampaignPanelsGameKeyCategory, polled every frame like the inventory screen) and Escape =
    ///   <c>Exit</c> (GenericPanelGameKeyCategory) — all rebindable by the player.</item>
    /// <item>Only sprites of always-loaded categories are used, so no sprite category is loaded or unloaded
    ///   (tools\check-gui.ps1 holds the prefab to that).</item>
    /// </list>
    /// </summary>
    internal static class StewardWindow
    {
        /// <summary>Below the Encyclopedia (310), above the settlement menu (100), the map bar and overlays (≤ 206)
        /// and army management (300) — RESEARCH §7.</summary>
        public const int LayerOrder = 305;

        public const string MovieName = "SmartStewardWindow";

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static StewardWindowVM? _vm;
        private static bool _encyclopediaOpen;
        private static int _escapeGuardFrames;
        private static bool _closeRequested;
        private static Action<EncyclopediaPageChangedEvent>? _onEncyclopediaPage;
        private static TaleWorlds.Library.EventSystem.EventManager? _eventManager;

        /// <summary>The campaign the open window belongs to (<see cref="CampaignSession.Generation"/>).</summary>
        private static int _generation;

        public static bool IsOpen => _layer != null;

        /// <summary>This view model is the open window's (a settings listener of any other must let go).</summary>
        internal static bool IsCurrent(StewardWindowVM vm) => _vm == vm && _generation == CampaignSession.Generation;

        /// <summary>Shift held this frame (the game's FiveStackModifier) — a click steps ±5.</summary>
        public static bool FiveStackHeld { get; private set; }

        /// <summary>Ctrl held this frame (the game's EntireStackModifier) — a click goes all the way.</summary>
        public static bool EntireStackHeld { get; private set; }

        /// <summary>How far a [+] / [−] click goes right now (DESIGN §1.1).</summary>
        public static EditSize CurrentEditSize => UiInput.EditSizeFor(FiveStackHeld, EntireStackHeld);

        /// <summary>Opens the window for the settlement the party stands in — from the menu entry, the leave warning's
        /// Review, or the arrival popup (<paramref name="source"/> says which, in the log). <paramref name="asPopup"/>: the
        /// arrival popup's rule decides (<see cref="ArrivalPopup"/> — a closed market, no rows, or with
        /// <paramref name="onlyWithChanges"/> nothing to suggest opens nothing); <paramref name="quiet"/>: no "nothing to
        /// plan here" message. True when it opened; an opened window marks the visit reviewed (no leave warning).</summary>
        public static bool Open(Settlement? settlement, string source, bool asPopup = false, bool onlyWithChanges = false,
            bool quiet = false)
        {
            if (IsOpen)
                Close();
            try
            {
                var vm = StewardWindowVM.Create(settlement, quiet);
                if (vm == null)
                    return false; // it said why (or kept quiet)
                if (asPopup)
                {
                    var verdict = vm.PopupVerdict(onlyWithChanges);
                    if (verdict != PopupVerdict.Open)
                    {
                        ModLog.Info("window", "no popup at " + vm.Settlement.Name + " - " + ArrivalPopup.Describe(verdict)
                                              + " (" + source + ")");
                        vm.OnFinalize();
                        return false;
                    }
                }
                _vm = vm;
                _generation = CampaignSession.Generation;
                _layer = new GauntletLayer("SmartStewardWindow", LayerOrder) { IsFocusLayer = true };
                _movie = _layer.LoadMovie(MovieName, vm);
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
                _layer.InputRestrictions.SetInputRestrictions();
                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                _onEncyclopediaPage = OnEncyclopediaPageChanged;
                _eventManager = Game.Current?.EventManager;
                _eventManager?.RegisterEvent(_onEncyclopediaPage);
                _encyclopediaOpen = false;
                _escapeGuardFrames = 2;
                ModLog.Info("window", "opened at " + vm.Settlement.Name + " (" + source + ")");
                StewardTriggers.MarkReviewed(vm.Settlement);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "opening the window", ex);
                Close();
                InformationManager.DisplayMessage(new InformationMessage(UiText.S("ss_ui_open_failed",
                    "Smart Steward: the window could not open - see smart_steward.log.")));
                return false;
            }
        }

        /// <summary>Closes the window on the next tick — for a failure inside a widget's own event (a text box's key
        /// handling), where releasing the movie at once would pull the widgets from under the running code.</summary>
        public static void RequestClose() => _closeRequested = true;

        /// <summary>Closes the window: the movie released BEFORE the layer goes (vanilla asserts otherwise), the
        /// input restrictions and the focus given back, the view model finalized. Safe to call twice.</summary>
        public static void Close()
        {
            _closeRequested = false;
            var layer = _layer;
            var movie = _movie;
            var host = _host;
            var vm = _vm;
            _layer = null;
            _movie = null;
            _host = null;
            _vm = null;
            FiveStackHeld = false;
            EntireStackHeld = false;
            _encyclopediaOpen = false;

            try
            {
                if (_onEncyclopediaPage != null)
                    _eventManager?.UnregisterEvent(_onEncyclopediaPage); // the game's manager it was registered with
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "unregistering the encyclopedia event", ex);
            }
            _onEncyclopediaPage = null;
            _eventManager = null;

            try
            {
                MBInformationManager.HideInformations(); // a tooltip must not outlive the window
            }
            catch
            {
                // nothing to hide
            }

            if (layer != null)
            {
                try
                {
                    layer.IsFocusLayer = false;
                    if (movie != null)
                        layer.ReleaseMovie(movie);
                    layer.InputRestrictions.ResetInputRestrictions();
                    host?.RemoveLayer(layer);
                    ScreenManager.TryLoseFocus(layer);
                }
                catch (Exception ex)
                {
                    ModLog.Error("window", "closing the layer", ex);
                }
            }

            if (vm != null)
            {
                try
                {
                    vm.OnFinalize();
                }
                catch (Exception ex)
                {
                    ModLog.Error("window", "finalizing the view model", ex);
                }
                ModLog.Info("window", "closed");
            }
        }

        /// <summary>Every application tick (SubModule). Cheap when closed; while open: the modifier keys, Escape
        /// (ignored while the Encyclopedia is up and for two frames after it closes — its own Escape must not close
        /// us too) and the window's own checks.</summary>
        public static void Tick(float dt)
        {
            if (_layer == null)
                return;
            if (_closeRequested)
            {
                Close();
                return;
            }
            if (_generation != CampaignSession.Generation)
            {
                ModLog.Info("window", "the window belongs to an earlier campaign - closing");
                Close();
                return;
            }
            try
            {
                var input = _layer.Input;
                FiveStackHeld = input.IsHotKeyDown("FiveStackModifier");
                EntireStackHeld = input.IsHotKeyDown("EntireStackModifier");
                if (_encyclopediaOpen)
                    return;
                if (_escapeGuardFrames > 0)
                {
                    _escapeGuardFrames--;
                    return;
                }
                if (input.IsHotKeyReleased("Exit"))
                {
                    Close();
                    return;
                }
                _vm?.Tick();
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "tick", ex);
                Close();
            }
        }

        /// <summary>Opens an Encyclopedia page (a hero's or a unit's link) over the window (DESIGN §2.7).</summary>
        public static void OpenEncyclopedia(string? link)
        {
            if (string.IsNullOrEmpty(link))
                return;
            try
            {
                Campaign.Current?.EncyclopediaManager?.GoToLink(link);
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "opening the encyclopedia at " + link, ex);
            }
        }

        /// <summary>The Encyclopedia tells every page change; None = it closed. Then the focus comes back to us
        /// (TrySetFocus goes by layer order, and 305 is the highest layer left).</summary>
        private static void OnEncyclopediaPageChanged(EncyclopediaPageChangedEvent e)
        {
            try
            {
                bool open = e != null && e.NewPage != EncyclopediaPages.None;
                if (_encyclopediaOpen && !open)
                    _escapeGuardFrames = 2;
                _encyclopediaOpen = open;
                if (!open && _layer != null)
                    ScreenManager.TrySetFocus(_layer);
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "encyclopedia page changed", ex);
            }
        }
    }
}
