using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace SmartSteward
{
    /// <summary>
    /// The steward's campaign behavior, registered on every campaign start. The session launch adds the "Party
    /// Steward" menu entries (<see cref="StewardMenu"/>); the campaign events feed the triggers
    /// (<see cref="StewardTriggers"/>, PLAN step 8): arrival, menu openings (arrival popup, autonomy, and the lazy
    /// wrapping of the leave options), leaving, and vanilla's "is this settlement busy?" question.
    /// <para>
    /// SAVE-SAFE, and it must stay so: <see cref="SyncData"/> stores NOTHING. The game still files
    /// an empty vanilla record under this class name (CampaignBehaviorDataStore → BehaviorSaveData,
    /// a <c>Dictionary&lt;string, object&gt;</c> with no entries), so no type of ours ever enters a
    /// save and the mod can be added or removed mid-campaign. Settings are global (settings.json);
    /// per-visit flags live in memory.
    /// </para>
    /// </summary>
    internal sealed class SmartStewardBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
            CampaignEvents.IsSettlementBusyEvent.AddNonSerializedListener(this, OnIsSettlementBusy);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Intentionally empty — see the class comment.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            StewardTriggers.Reset();
            try
            {
                StewardMenu.AddMenus(starter); // "Party Steward" — opens the window (PLAN step 7)
            }
            catch (Exception ex)
            {
                ModLog.Error("campaign", "adding the Party Steward menu entries", ex);
            }
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                StewardTriggers.OnSettlementEntered(party, settlement);
            }
            catch (Exception ex)
            {
                ModLog.Error("trigger", "settlement entered", ex);
            }
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            try
            {
                StewardTriggers.OnSettlementLeft(party, settlement);
            }
            catch (Exception ex)
            {
                ModLog.Error("trigger", "settlement left", ex);
            }
        }

        private void OnGameMenuOpened(MenuCallbackArgs args)
        {
            try
            {
                StewardTriggers.OnGameMenuOpened(args);
            }
            catch (Exception ex)
            {
                ModLog.Error("trigger", "game menu opened", ex);
            }
        }

        private void OnIsSettlementBusy(Settlement settlement, object asker, ref int priority)
        {
            try
            {
                StewardTriggers.OnIsSettlementBusy(settlement, asker, ref priority);
            }
            catch (Exception ex)
            {
                ModLog.Error("trigger", "is settlement busy", ex);
            }
        }
    }
}
