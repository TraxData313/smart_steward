using System;
using System.Collections.Generic;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Men who join (+) or leave (−) the party with the deal — what one party-changing row does to the party (DESIGN §1.1,
    /// the live re-plan). Tavern rows now; step 16's recruit and dismiss rows yield the same moves
    /// (<see cref="PartyAfter.MovesOf"/>), so the steward's food and horses follow them without any new rule.
    /// </summary>
    internal readonly struct PartyMove
    {
        public PartyMove(string? troopId, int men, bool isMounted, IReadOnlyList<string>? upgradeCategories)
        {
            TroopId = troopId;
            Men = men;
            IsMounted = isMounted;
            UpgradeCategories = upgradeCategories ?? Array.Empty<string>();
        }

        /// <summary>The troop type (null for a hero — heroes never upgrade).</summary>
        public string? TroopId { get; }

        /// <summary>+ join, − leave.</summary>
        public int Men { get; }

        /// <summary>The game's "man with a horse" (<c>CharacterObject.IsMounted</c>): a mounted man is no footman.</summary>
        public bool IsMounted { get; }

        /// <summary>The kinds of upgrade horse the troop's upgrades need (a joining troop puts them in play).</summary>
        public IReadOnlyList<string> UpgradeCategories { get; }
    }

    /// <summary>
    /// The party as the steward plans for it — the snapshot's party with every party-changing row of the plan applied
    /// (DESIGN §1.1, the live re-plan, step 15 — Anton 2026.09.28: "recalculate the food and mounts as I add more troops or
    /// remove"): the members who eat, the footmen who need a riding mount, and the upgrades that need horses. The prisoners
    /// who leave are counted by the prisoner rows themselves (<see cref="PrisonerPlanner"/> → the food target's half-eaters).
    /// </summary>
    /// <remarks>
    /// Rules [decided: Claude, 2026.09.28 — step 15]: a man joins as a footman unless his type is mounted (the game's own
    /// <c>PartyBase.NumberOfMenWithoutHorse</c> rule, RESEARCH §3 — heroes by their battle equipment's horse, troops by their
    /// formation class); new men are never ready to upgrade (their stack has no XP yet), but a troop that upgrades into a
    /// kind of horse puts that kind in play (so a fixed "horses for upgrades" number or the spares apply to it); men who
    /// leave a stack take its ready count down with them (a stack cannot have more ready men than men — step 16's dismissals).
    /// </remarks>
    internal sealed class PartyAfter
    {
        private PartyAfter(int members, int footmen, UpgradeNeeds upgrades)
        {
            Members = members;
            Footmen = footmen;
            Upgrades = upgrades;
        }

        /// <summary>Party members after the deal (heroes and wounded in, like <c>MemberRoster.TotalManCount</c>).</summary>
        public int Members { get; }

        /// <summary>Men without a horse of their own after the deal (<c>PartyBase.NumberOfMenWithoutHorse</c> + the footmen hired).</summary>
        public int Footmen { get; }

        /// <summary>The upgrades after the deal — what the upgrade horses are for.</summary>
        public UpgradeNeeds Upgrades { get; }

        /// <summary>The snapshot's own party — no party-changing row moves anything.</summary>
        public static PartyAfter Of(StewardSnapshot snapshot) => Of(snapshot, Array.Empty<PartyMove>());

        public static PartyAfter Of(StewardSnapshot snapshot, IReadOnlyCollection<PartyMove> moves)
        {
            var party = snapshot.Party ?? new PartyInfo();
            int members = Math.Max(0, party.Members);
            int footmen = Math.Max(0, party.Footmen);
            foreach (var move in moves)
            {
                members += move.Men;
                if (!move.IsMounted)
                    footmen += move.Men;
            }
            return new PartyAfter(Math.Max(0, members), Math.Max(0, footmen), UpgradeNeeds.Of(snapshot, moves));
        }

        /// <summary>Does this row change who is in the party after the deal? Then an edit of it re-plans the steward's rows
        /// (<see cref="StewardPlan"/>, the live re-plan): hires (the tavern), prisoners (a prisoner ransomed or donated no
        /// longer eats). Step 16: the troops section's recruit and dismiss rows.</summary>
        public static bool ChangesParty(PlanRow row) => row.Type == RowType.Tavern || row.Type == RowType.Prisoner;

        /// <summary>The men the rows add or take away, at their current quantities (prisoners are not members — they are
        /// counted by their own rows).</summary>
        public static List<PartyMove> MovesOf(IEnumerable<PlanRow> rows)
        {
            var moves = new List<PartyMove>();
            foreach (var row in rows)
            {
                if (row.Type != RowType.Tavern || row.Tavern == null || row.Change <= 0)
                    continue;
                var info = row.Tavern;
                moves.Add(info.Kind == TavernRowKind.Wanderer
                    ? new PartyMove(null, row.Change, info.IsMounted, null)
                    : new PartyMove(row.TroopId, row.Change, info.IsMounted, info.UpgradeCategories));
            }
            return moves;
        }
    }
}
