using System.Collections.Generic;
using SmartSteward.Core.Snapshot;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// One look at the settlement the party stands in (PLAN step 6): the game-free snapshot Core plans with, the
    /// game's roster elements behind its stack keys, and the price oracle over them. Valid while nothing trades —
    /// the executor re-reads the live rosters at the click.
    /// </summary>
    internal sealed class GameVisit
    {
        public GameVisit(Settlement settlement, StewardSnapshot snapshot, IReadOnlyDictionary<string, EquipmentElement> elements)
        {
            Settlement = settlement;
            Snapshot = snapshot;
            Elements = elements;
            Oracle = new GamePriceOracle(settlement, elements);
        }

        public Settlement Settlement { get; }
        public StewardSnapshot Snapshot { get; }

        /// <summary>Stack key → the element (item + modifier), from the party's and the market's rosters.</summary>
        public IReadOnlyDictionary<string, EquipmentElement> Elements { get; }

        public GamePriceOracle Oracle { get; }
    }
}
