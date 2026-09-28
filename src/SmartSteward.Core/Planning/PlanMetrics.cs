using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The spreadsheet's number columns for one row, one line, one section or the Total (PLAN step 20 — Anton 2026.09.28, round
    /// 4: "add new cols Land Weight and Sea Weight, Denari and Souls, and have one most lower line type Total that aggregates the
    /// totals"; at the mockup's approval the same day: "Souls" became <b>Party</b> — members only — and a <b>Prisoners</b> column
    /// of its own). Every number is the CHANGE the deal makes; sums are plain additions, so a section is the sum of its rows and
    /// the Total the sum of everything.
    /// </summary>
    public readonly struct PlanMetrics
    {
        public PlanMetrics(int denari, double influence, int party, int prisoners, double landKg, double seaKg)
        {
            Denari = denari;
            Influence = influence;
            Party = party;
            Prisoners = prisoners;
            LandKg = landKg;
            SeaKg = seaKg;
        }

        /// <summary>Net denari: + earned, − spent (<see cref="PlanRow.GoldDelta"/>).</summary>
        public int Denari { get; }

        /// <summary>Influence gained (prisoners donated).</summary>
        public double Influence { get; }

        /// <summary>Party members who join (+: hires, recruits) or leave (−: dismissals). Prisoners are NOT members.</summary>
        public int Party { get; }

        /// <summary>Prisoners who leave (−: ransomed, donated).</summary>
        public int Prisoners { get; }

        /// <summary>Load on land: + added, − freed (animals weigh nothing on land).</summary>
        public double LandKg { get; }

        /// <summary>Load at sea (War Sails): the goods, and every animal and every mounted troop's horse there (RESEARCH §19).
        /// 0 without ships (the party's sea weights are not read then).</summary>
        public double SeaKg { get; }

        public static PlanMetrics operator +(PlanMetrics a, PlanMetrics b) =>
            new PlanMetrics(a.Denari + b.Denari, a.Influence + b.Influence, a.Party + b.Party, a.Prisoners + b.Prisoners,
                a.LandKg + b.LandKg, a.SeaKg + b.SeaKg);

        /// <summary>One row's effect at its current quantity.</summary>
        public static PlanMetrics Of(PlanRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            int party = 0, prisoners = 0;
            double sea = 0;
            foreach (var tally in row.Tallies)
                if (tally.Count > 0)
                    sea += (tally.Direction == TradeDirection.Buy ? 1 : -1) * tally.Count * tally.Stack.UnitWeightAtSea;
            switch (row.Type)
            {
                case RowType.Tavern:
                    party = Math.Max(0, row.Change);
                    if (row.Tavern?.Kind == TavernRowKind.Mercenaries)
                        sea += party * Math.Max(0, row.Tavern.SeaWeightPerMan);
                    break;
                case RowType.Troop:
                    party = row.Change;
                    // At sea every mounted man's horse weighs, wounded too (step 16).
                    sea += row.Change * Math.Max(0, row.Troop?.SeaWeightPerMan ?? 0);
                    break;
                case RowType.Prisoner:
                    prisoners = row.Change;
                    break;
            }
            return new PlanMetrics(row.GoldDelta, row.InfluenceDelta, party, prisoners, row.WeightDelta, sea);
        }

        /// <summary>The sum over <paramref name="rows"/>.</summary>
        public static PlanMetrics Sum(IEnumerable<PlanRow> rows)
        {
            var sum = new PlanMetrics();
            foreach (var row in rows ?? Array.Empty<PlanRow>())
                sum += Of(row);
            return sum;
        }
    }
}
