using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The footer's herd line (PLAN step 18 — Anton 2026.09.28: "Horses 110 / 200 before the herd slows you", red when over)
    /// for the party AFTER the deal, by the game's own rule (RESEARCH §23, <c>DefaultPartySpeedCalculatingModel.
    /// CalculateLandBaseSpeed</c> + <c>GetHerdingModifier</c>, land only — War Sails' speed at sea has no herd):
    /// <list type="bullet">
    /// <item>footmen ride the mounts: <c>ridden = min(footmen, mounts)</c> (pack animals never carry a man);</item>
    /// <item>the herd = pack animals + livestock + the mounts nobody rides;</item>
    /// <item>it slows the party only when it outnumbers the men (<c>MemberRoster.TotalManCount</c>, heroes and wounded in,
    /// prisoners out): −30% × (herd − men) / men, at most −80% (Riding.Shepherd softens it).</item>
    /// </list>
    /// So the line reads <c>Horses {H} / {R}</c>: H = the horses (mounts and pack animals) and R = the most the party can
    /// have before the herd slows it = men + the mounts its footmen ride − its livestock (H ≤ R exactly when herd ≤ men).
    /// In an army the attached parties' men, footmen and animals are pooled in, as the game does.
    /// </summary>
    public sealed class HerdTotals
    {
        /// <summary>Men after the deal (the party's and any attached parties').</summary>
        public int Men { get; private set; }

        /// <summary>Men on foot after the deal (the game's <c>NumberOfMenWithoutHorse</c> + the footmen who join).</summary>
        public int Footmen { get; private set; }

        /// <summary>Riding animals after the deal (every <c>IsMount</c> — war, noble and lame horses too).</summary>
        public int Mounts { get; private set; }

        /// <summary>Pack animals after the deal.</summary>
        public int PackAnimals { get; private set; }

        /// <summary>Head of livestock (never traded by the steward).</summary>
        public int Livestock { get; private set; }

        /// <summary>The mounts a footman rides — they are not herd.</summary>
        public int Ridden => Math.Min(Footmen, Mounts);

        /// <summary>The game's herd: pack animals, livestock and the mounts nobody rides.</summary>
        public int Herd => PackAnimals + Livestock + Mounts - Ridden;

        /// <summary>H: the horses of the line — mounts and pack animals.</summary>
        public int Horses => Mounts + PackAnimals;

        /// <summary>R: the most horses before the herd slows the party — men + the mounts footmen ride − livestock (never
        /// below 0).</summary>
        public int Room => Math.Max(0, Men + Ridden - Livestock);

        /// <summary>How far the herd outnumbers the men (0 = it does not slow the party).</summary>
        public int Over => Math.Max(0, Herd - Men);

        /// <summary>The herd slows the party after the deal — the line shows red.</summary>
        public bool SlowsParty => Over > 0;

        internal static HerdTotals Compute(IEnumerable<PlanRow> rows, StewardSnapshot snapshot)
        {
            var list = rows as IList<PlanRow> ?? new List<PlanRow>(rows);
            var party = PartyAfter.Of(snapshot, PartyAfter.MovesOf(list));
            int mounts = 0, pack = 0;
            foreach (var stack in snapshot.Inventory ?? new List<ItemStack>())
            {
                if (stack == null || stack.Count <= 0)
                    continue;
                if (stack.Kind == ItemKind.Mount) mounts += stack.Count;
                else if (stack.Kind == ItemKind.PackAnimal) pack += stack.Count;
            }
            foreach (var row in list)
                foreach (var tally in row.Tallies)
                {
                    if (tally.Count <= 0)
                        continue;
                    int moved = tally.Direction == TradeDirection.Buy ? tally.Count : -tally.Count;
                    if (tally.Stack.Kind == ItemKind.Mount) mounts += moved;
                    else if (tally.Stack.Kind == ItemKind.PackAnimal) pack += moved;
                }
            var info = snapshot.Party ?? new PartyInfo();
            var attached = info.Attached ?? new AttachedParties();
            return new HerdTotals
            {
                Men = party.Members + Math.Max(0, attached.Men),
                Footmen = party.Footmen + Math.Max(0, attached.Footmen),
                Mounts = Math.Max(0, mounts) + Math.Max(0, attached.Mounts),
                PackAnimals = Math.Max(0, pack) + Math.Max(0, attached.PackAnimals),
                Livestock = Math.Max(0, info.LivestockAnimals) + Math.Max(0, attached.Livestock),
            };
        }
    }
}
