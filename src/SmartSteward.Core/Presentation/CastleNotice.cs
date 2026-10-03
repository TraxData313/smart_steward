using System;
using System.Linq;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Presentation
{
    /// <summary>The English of the castle's notice and the donate reasons — the window fills them through TextObject ids
    /// (<c>UiLabels.CastleWords</c>); the defaults keep the tests readable.</summary>
    public sealed class CastleWords
    {
        public string NoMarket { get; set; } = "A castle has no market - the steward only donates prisoners here.";
        public string NoPrisoners { get; set; } = "You hold no prisoners.";

        /// <summary>"Donating is not possible here: {REASON}".</summary>
        public Func<string, string> NotHere { get; set; } = reason => "Donating is not possible here: " + reason;

        public string NotYourKingdom { get; set; } = "it is not your kingdom's.";
        public string YourClansFief { get; set; } = "it is your own clan's (manage its prisoners in the dungeon).";
        public string NoDungeonAccess { get; set; } = "you may not enter its dungeon.";
        public string DungeonFull { get; set; } = "its dungeon is full.";
        public string NoDungeon { get; set; } = "there is no dungeon.";
    }

    /// <summary>
    /// Step 34 (DESIGN §9, Anton 2026.10.03): what the window says above the table at a castle — in the closed-market notice's
    /// place, never "Market closed" (a castle has no market to close). The castle line always; then why the steward cannot
    /// donate here (the game's own conditions, <see cref="DonateBlock"/>), or that there is nobody to donate. Null anywhere
    /// else.
    /// </summary>
    public static class CastleNotice
    {
        public static string? Of(StewardSnapshot snapshot, CastleWords? words = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SettlementKind != SettlementKind.Castle)
                return null;
            words ??= new CastleWords();
            bool anyPrisoner = (snapshot.Prisoners ?? new System.Collections.Generic.List<PrisonerStack>())
                .Any(p => p != null && p.Count > 0);
            var block = snapshot.Prison?.DonateBlock ?? DonateBlock.None;
            if (!anyPrisoner)
                return words.NoMarket + " " + words.NoPrisoners;
            if (block == DonateBlock.None)
                return words.NoMarket;
            return words.NoMarket + " " + words.NotHere(Reason(block, words));
        }

        /// <summary>The reason's words ("" for <see cref="DonateBlock.None"/>).</summary>
        public static string Reason(DonateBlock block, CastleWords? words = null)
        {
            words ??= new CastleWords();
            switch (block)
            {
                case DonateBlock.NotYourKingdom: return words.NotYourKingdom;
                case DonateBlock.YourClansFief: return words.YourClansFief;
                case DonateBlock.NoDungeonAccess: return words.NoDungeonAccess;
                case DonateBlock.DungeonFull: return words.DungeonFull;
                case DonateBlock.NoDungeon: return words.NoDungeon;
                default: return "";
            }
        }
    }
}
