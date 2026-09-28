using System;
using System.Linq;
using SmartSteward.Core.Planning;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// The steward's Tavern section is the tavern district for the player (round 3, RESEARCH §20). Vanilla learns about
    /// every hero in the tavern the moment the player stands in the district — <c>town_backstreet</c>'s init puts the
    /// "tavern" location in <c>GameMenuManager.MenuLocations</c>, and on the menu's <c>GameMenuOpened</c>
    /// <c>HeroKnownInformationCampaignBehavior.LearnAboutLocationCharacters</c> sets <c>Hero.IsKnownToPlayer</c> for each
    /// hero there — no talk, no click. Until then the Encyclopedia hides an unknown clanless hero's page ("???",
    /// <c>DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf</c>). So the steward does the same for the wanderers it
    /// LISTS, when the window SHOWS them (not for an arrival popup that stays shut, not for the autonomous steward, which
    /// never lists the tavern), and once more before a name opens the Encyclopedia. The setter is vanilla's own path: it
    /// fires <c>OnPlayerLearnsAboutHero</c> (their last known place, and the game's "You've learned about …" line).
    /// Mercenary troops are unit pages — never restricted, nothing to learn.
    /// </summary>
    internal static class TavernKnowledge
    {
        /// <summary>Learns about every wanderer the plan lists at <paramref name="settlement"/>.</summary>
        public static void LearnAboutListed(Settlement? settlement, StewardPlan? plan)
        {
            if (settlement == null || plan == null)
                return;
            foreach (var row in plan.Rows)
                if (row.Tavern?.Kind == TavernRowKind.Wanderer && row.HeroId != null)
                    Learn(settlement, row.HeroId);
        }

        /// <summary>Learns about one listed wanderer (still in this town's tavern — vanilla's own condition).</summary>
        public static void Learn(Settlement? settlement, string? heroId)
        {
            try
            {
                if (settlement == null || heroId == null)
                    return;
                var hero = settlement.HeroesWithoutParty.FirstOrDefault(h => h != null && h.StringId == heroId);
                if (hero == null || hero.IsKnownToPlayer || hero.CurrentSettlement != settlement)
                    return;
                hero.IsKnownToPlayer = true;
                ModLog.Info("window", "learned about " + hero.Name + " - listed in the steward's tavern section, as the tavern "
                                      + "district would show him");
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "learning about wanderer " + heroId, ex);
            }
        }
    }
}
