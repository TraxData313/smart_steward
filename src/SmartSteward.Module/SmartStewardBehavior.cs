using System;
using TaleWorlds.CampaignSystem;

namespace SmartSteward
{
    /// <summary>
    /// The steward's campaign behavior, registered on every campaign start. Step 6 hooks the session launch to add
    /// the TEMPORARY debug door (<see cref="DebugDoor"/>); the triggers (step 8) hook the campaign events here.
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
                DebugDoor.AddMenus(starter); // TEMPORARY — step 8 replaces it with the real menu entries
            }
            catch (Exception ex)
            {
                ModLog.Error("campaign", "adding the debug door", ex);
            }
        }
    }
}
