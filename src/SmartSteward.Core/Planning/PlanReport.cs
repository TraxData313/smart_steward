using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// A plan as plain English text for smart_steward.log (<see cref="Full"/>: every row, the facts, the totals, every
    /// transaction) — ASCII only, so the log reads anywhere. The player never sees it: the window labels rows through
    /// TextObject ids. (The debug door's compact popup text went with the door — PLAN step 9.)
    /// </summary>
    public static class PlanReport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The log: every section and row (zeros too) with its numbers and breakdown, the facts, the totals
        /// and the executor's transaction list.</summary>
        public static IEnumerable<string> Full(StewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var t = plan.Totals;
            yield return "gold " + Money(t.GoldNow) + " -> " + Money(t.GoldAfter) + " (" + Signed(t.GoldChange)
                         + "), earned " + Money(t.Earned) + ", spent " + Money(t.Spent) + ", market sales "
                         + Money(t.MarketSales) + " of market gold " + Money(t.MarketGold);
            var f = plan.Facts;
            yield return "facts: eaters " + f.FoodEaters.ToString(Inv) + ", food target " + f.FoodTarget.ToString(Inv)
                         + " (sell above " + f.FoodSellAbove.ToString("0.##", Inv) + ", " + f.FoodShare.ToString(Inv)
                         + " per kind), pack target "
                         + f.PackTarget.ToString(Inv) + ", footmen " + f.Footmen.ToString(Inv) + ", horses to keep "
                         + f.MountTarget.ToString(Inv) + " (riding target " + f.RidingTarget.ToString(Inv) + ", war horses "
                         + f.WarTarget.ToString(Inv) + (f.NobleTarget > 0 ? ", noble horses " + f.NobleTarget.ToString(Inv) : "")
                         + ")";
            foreach (var section in plan.Sections)
            {
                yield return "[" + section.Kind + "]";
                foreach (var row in section.Rows)
                {
                    yield return "  " + FullRow(row);
                    foreach (var line in row.Breakdown)
                        yield return "      - " + line.Name + (string.IsNullOrEmpty(line.ModifierId) ? "" : " (" + line.ModifierId + ")")
                                     + ": mine " + line.Mine.ToString(Inv) + ", change " + SignedCount(line.Change)
                                     + (line.Market == null ? "" : ", market " + line.Market.Value.ToString(Inv))
                                     + (line.Change == 0 ? "" : ", " + Signed(line.GoldDelta) + " gold at " + PriceRange(line.UnitPriceMin, line.UnitPriceMax));
                }
            }
            yield return Footer(plan);
            var flags = Flags(t);
            if (flags.Count > 0)
                yield return "flags: " + string.Join("; ", flags);
            var transactions = plan.Transactions;
            yield return "transactions: " + transactions.Count.ToString(Inv);
            int n = 0;
            foreach (var tx in transactions)
                yield return "  " + (++n).ToString(Inv) + ". " + DescribeTransaction(tx);
        }

        /// <summary>A row's name for text: the item, troop or hero name; role and group rows get an English label.</summary>
        public static string RowLabel(PlanRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (!string.IsNullOrEmpty(row.Name))
                return row.Name;
            if (row.Role != null)
            {
                switch (row.Role.Value)
                {
                    case MountRole.Pack: return "Pack animals";
                    case MountRole.Riding: return "Riding mounts";
                    case MountRole.War: return "War horses";
                    case MountRole.Noble: return "Noble horses";
                    case MountRole.Lame: return "Lame horses";
                }
            }
            if (row.Type == RowType.Loot)
            {
                switch (row.LootGroup)
                {
                    case Snapshot.LootGroup.Armour: return "Armour";
                    case Snapshot.LootGroup.MeleeWeapons: return "Melee weapons";
                    case Snapshot.LootGroup.Ranged: return "Ranged";
                    case Snapshot.LootGroup.Shields: return "Shields";
                    case Snapshot.LootGroup.OtherGoods: return "Other goods";
                }
            }
            return row.Id;
        }

        /// <summary>"Sell 12 x grain| [food:grain] = 144 (10..13)".</summary>
        public static string DescribeTransaction(PlanTransaction tx)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            string subject = tx.IsItemTrade ? tx.StackKey ?? "?"
                : tx.Kind == TransactionKind.HireWanderer ? tx.HeroId ?? "?"
                : tx.TroopId ?? "?";
            string text = tx.Kind + " " + tx.Count.ToString(Inv) + " x " + subject + " [" + tx.RowId + "]";
            if (tx.Kind == TransactionKind.Donate)
                return text + " = +" + tx.Influence.ToString("0.##", Inv) + " influence";
            if (tx.Kind == TransactionKind.Dismiss)
                return text + " (free, the wounded first)";
            text += " = " + Money(tx.Gold);
            if (tx.UnitPrices.Count > 0)
                text += " (" + PriceRange(tx.UnitPrices.Min(), tx.UnitPrices.Max()) + ")";
            return text;
        }

        private static string FullRow(PlanRow row)
        {
            var sb = new StringBuilder();
            sb.Append(row.Id).Append(" \"").Append(RowLabel(row)).Append("\" ").Append(row.Type)
                .Append(": mine ").Append(row.Mine.ToString(Inv));
            if (row.Locked > 0) sb.Append(" (+").Append(row.Locked.ToString(Inv)).Append(" locked)");
            if (row.OverValueCap > 0) sb.Append(" (+").Append(row.OverValueCap.ToString(Inv)).Append(" over value cap)");
            sb.Append(", change ").Append(SignedCount(row.Change));
            if (row.IsTouched) sb.Append(" (yours, suggested ").Append(SignedCount(row.SuggestedChange)).Append(')');
            if (row.Quest != null) // step 26: what the player's quests keep here (and a food's quest goal, bought toward)
                sb.Append(" (quests keep ").Append(row.Quest.Kept.ToString(Inv))
                    .Append(row.Quest.FoodGoal == null ? "" : ", quest goal " + row.Quest.FoodGoal.Value.ToString(Inv)
                                                              + (row.Quest.Buys ? " - buying" : ""))
                    .Append(')');
            sb.Append(", result ").Append(row.Result.ToString(Inv))
                .Append(", market ").Append(row.Market == null ? "-" : row.Market.Value.ToString(Inv))
                .Append(", max buy ").Append(row.MaxBuy.ToString(Inv)).Append(" / sell ").Append(row.MaxSell.ToString(Inv));
            if (row.Change != 0)
                sb.Append(", ").Append(Signed(row.GoldDelta)).Append(" gold at ").Append(PriceRange(row.UnitPriceMin, row.UnitPriceMax));
            if (Math.Abs(row.WeightDelta) > 0.0001) sb.Append(", weight ").Append(row.WeightDelta.ToString("+0.#;-0.#", Inv));
            if (row.InfluenceDelta > 0) sb.Append(", influence +").Append(row.InfluenceDelta.ToString("0.##", Inv));
            if (row.Target != null) sb.Append(", target ").Append(row.Target.Value.ToString(Inv));
            if (row.PriceBook != null)
                sb.Append(", book buy ").Append(row.PriceBook.BuyTicked ? Limit(row.PriceBook.FinalMaxBuy) : "off")
                    .Append(" / sell ").Append(row.PriceBook.SellTicked ? Limit(row.PriceBook.FinalMinSell) : "off");
            if (row.Prisoner != null)
                sb.Append(", ransom ").Append(row.Prisoner.RansomValue.ToString(Inv)).Append(" each (").Append(row.Prisoner.RansomCount.ToString(Inv))
                    .Append("), donate ").Append(row.Prisoner.DonateCount.ToString(Inv))
                    .Append(row.Prisoner.IsHero ? ", hero" : "");
            if (row.Tavern != null)
                sb.Append(", ").Append(row.Tavern.Kind).Append(" price ").Append(row.Tavern.UnitPrice.ToString(Inv))
                    .Append(", wage ").Append(row.Tavern.DailyWage.ToString(Inv))
                    .Append(row.Tavern.Block == HireBlock.None ? "" : ", blocked " + row.Tavern.Block)
                    .Append(string.IsNullOrEmpty(row.Tavern.SkillTag) ? "" : ", " + row.Tavern.SkillTag);
            if (row.Troop != null)
                sb.Append(row.Troop.OnOffer > 0 ? ", on offer " + row.Troop.OnOffer.ToString(Inv) + " at " + row.Troop.UnitPrice.ToString(Inv) : ", not on offer")
                    .Append(", wage ").Append(row.Troop.DailyWage.ToString(Inv))
                    .Append(row.Troop.Wounded > 0 ? ", " + row.Troop.Wounded.ToString(Inv) + " wounded" : "")
                    .Append(row.Troop.IsMounted ? ", mounted" : ", on foot");
            return sb.ToString();
        }

        private static string Footer(StewardPlan plan)
        {
            var t = plan.Totals;
            var sb = new StringBuilder();
            sb.Append("Food ").Append(t.FoodUnitsNow.ToString(Inv)).Append(" -> ").Append(t.FoodUnitsAfter.ToString(Inv));
            if (t.FoodDaysAfter != null)
                sb.Append(" (~").Append(Math.Floor(t.FoodDaysAfter.Value).ToString(Inv)).Append(" days)");
            sb.Append(" | weight ").Append(t.WeightChange.ToString("+0.#;-0.#;0", Inv));
            var c = t.Carry;
            if (c.Known)
            {
                sb.Append(" (load ").Append(Kg(c.WeightNow)).Append(" -> ").Append(Kg(c.WeightAfter))
                    .Append(", capacity land ").Append(Kg(c.CapacityLandNow)).Append(" -> ").Append(Kg(c.CapacityLandAfter));
                if (c.ShowSea)
                    sb.Append(", at sea ").Append(Kg(c.WeightAtSeaNow)).Append(" -> ").Append(Kg(c.WeightAtSeaAfter))
                        .Append(" of ").Append(Kg(c.CapacitySeaNow)).Append(" -> ").Append(Kg(c.CapacitySeaAfter));
                sb.Append(')');
                if (c.OverLand > 0) sb.Append(" OVER on land by ").Append(Kg(Math.Ceiling(c.OverLand)));
                if (c.OverSea > 0) sb.Append(" OVER at sea by ").Append(Kg(Math.Ceiling(c.OverSea)));
                // Step 20 (RESEARCH section 25): the overburden's speed, share of the base and the tooltip's points.
                if (c.LandSpeedLoss > 0)
                    sb.Append(" (land speed -").Append((c.LandSlowdown * 100).ToString("0.#", Inv)).Append("%, -")
                        .Append(c.LandSpeedLoss.ToString("0.00", Inv)).Append(')');
                if (c.SeaSpeedLoss > 0)
                    sb.Append(" (sea speed ").Append(c.SeaSlowdownKnown ? "-" + (c.SeaSlowdown * 100).ToString("0.#", Inv) + "%, " : "")
                        .Append('-').Append(c.SeaSpeedLoss.ToString("0.00", Inv)).Append(')');
            }
            if (t.InfluenceGained > 0)
                sb.Append(" | influence +").Append(t.InfluenceGained.ToString("0.#", Inv));
            sb.Append(" | party ").Append(t.MembersAfter.ToString(Inv)).Append('/').Append(t.PartySizeLimit.ToString(Inv))
                .Append(t.OverPartyLimit ? " (over the limit)" : "");
            var h = t.Herd;
            sb.Append(" | horses ").Append(h.Horses.ToString(Inv)).Append(" of ").Append(h.Room.ToString(Inv))
                .Append(" before the herd slows (herd ").Append(h.Herd.ToString(Inv)).Append(" vs ").Append(h.Men.ToString(Inv))
                .Append(" men; ").Append(h.Footmen.ToString(Inv)).Append(" footmen, ").Append(h.Mounts.ToString(Inv)).Append(" mounts, ")
                .Append(h.PackAnimals.ToString(Inv)).Append(" pack, ").Append(h.Livestock.ToString(Inv)).Append(" livestock)")
                .Append(h.SlowsParty ? " SLOWED by " + h.Over.ToString(Inv) : "");
            sb.Append(" | ").Append(plan.Transactions.Count.ToString(Inv)).Append(" transactions");
            return sb.ToString();
        }

        private static List<string> Flags(PlanTotals t)
        {
            var flags = new List<string>();
            if (t.CannotAfford) flags.Add("cannot afford this deal");
            if (t.BelowMinGoldAfterDeal) flags.Add("below the minimum gold after the deal");
            if (t.BelowMinGoldForHorses) flags.Add("below the minimum gold for horses");
            if (t.ExceedsMarketGold) flags.Add("the market cannot pay for all the sales");
            return flags;
        }

        private static string Limit(int? value) => value == null ? "any" : value.Value.ToString(Inv);

        private static string PriceRange(int min, int max) =>
            min == max ? min.ToString(Inv) : min.ToString(Inv) + ".." + max.ToString(Inv);

        private static string Money(int value) => value.ToString("N0", Inv);

        private static string Kg(double kg) => Math.Round(kg).ToString("N0", Inv);

        private static string Signed(int value) => (value >= 0 ? "+" : "-") + Math.Abs(value).ToString("N0", Inv);

        private static string SignedCount(int value) => (value >= 0 ? "+" : "-") + Math.Abs(value).ToString(Inv);
    }
}
