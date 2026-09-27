using System;
using TaleWorlds.CampaignSystem;

namespace SmartSteward
{
    /// <summary>
    /// The steward's campaign behavior, registered on every campaign start. The session launch adds the "Party
    /// Steward" menu entries (<see cref="StewardMenu"/>, step 7) and the TEMPORARY debug door (<see cref="DebugDoor"/>,
    /// step 6 — step 8 removes it); the triggers (step 8) hook the campaign events here.
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
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Intentionally empty — see the class comment.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                StewardMenu.AddMenus(starter); // "Party Steward" — opens the window (PLAN step 7)
            }
            catch (Exception ex)
            {
                ModLog.Error("campaign", "adding the Party Steward menu entries", ex);
            }
            try
            {
                DebugDoor.AddMenus(starter); // TEMPORARY — step 8 removes it
            }
            catch (Exception ex)
            {
                ModLog.Error("campaign", "adding the debug door", ex);
            }
        }
    }
}
