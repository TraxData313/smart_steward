using System;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// A row's Goal cell (DESIGN §1.1 "THE GOAL" — Anton 2026.09.28, round 5): where the row should END. For food and the pack,
    /// riding and war horse rows a goal the player typed (or clicked) — a standing order — or the steward's own; for the rest what
    /// the steward's rules make of the line (prisoners 0 or Mine, Other 0, noble and lame horses 0; tavern and troop rows none).
    /// </summary>
    public sealed class RowGoal
    {
        private RowGoal()
        {
        }

        /// <summary>The goal; null = an empty cell (a tavern or troop row) or hands-off (<see cref="HandsOff"/>).</summary>
        public int? Value { get; private set; }

        /// <summary>The player may type a goal here (and a click on [–]/[+] edits it): food, pack, riding and war rows.</summary>
        public bool Editable { get; private set; }

        /// <summary>A goal of the player's — marked as his (gold), with its ⟲.</summary>
        public bool IsYours { get; private set; }

        /// <summary>No goal yet: the row's job waits for its activation threshold (and no goal of yours) — shown <c>–*</c>.</summary>
        public bool HandsOff { get; private set; }

        /// <summary>The threshold a hands-off row waits for.</summary>
        public int? StartsAt { get; private set; }

        /// <summary>Why the Result stops short of <see cref="Value"/> (<see cref="GoalShort.None"/> when it reaches it).</summary>
        public GoalShort Short { get; private set; }

        /// <summary>The quests decide this goal (step 26, DESIGN §2.9): a quest keeps units on the row (or a food's automatic goal) and
        /// no goal of yours is typed — the quest colour; <see cref="Value"/> is the larger of the policy's goal and the quests' need.</summary>
        public bool IsQuest { get; private set; }

        /// <summary>The quests on the row (the hover names them) — also when a goal of yours wins; null = none.</summary>
        public QuestRowInfo? Quest { get; private set; }

        /// <summary>A goal of yours below what the quests need (it wins; the hover says so).</summary>
        public bool BelowQuest { get; private set; }

        /// <summary>The Result differs from the goal and the plan knows why.</summary>
        public bool IsShort => Short != GoalShort.None;

        public static RowGoal Of(PlanRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            var goal = new RowGoal();
            var quest = row.Quest; // step 26: what the player's quests keep on the row
            goal.Quest = quest;
            switch (row.Type)
            {
                case RowType.Tavern:
                    return goal; // Anton: "no goal changes here"
                case RowType.Troop:
                    // Anton: "no goal changes here" — the Troops title carries the party limit; only the men a quest keeps show.
                    if (quest != null)
                    {
                        goal.Value = quest.Kept;
                        goal.IsQuest = true;
                    }
                    return goal;
                case RowType.Loot:
                    goal.Value = 0; // everything unlocked is sold (Anton: "others goal=0")
                    break;
                case RowType.Prisoner:
                    bool keep = row.Prisoner?.Action == PrisonerChoice.Keep;
                    goal.Value = keep ? row.Mine : 0; // "prisoners course goal=0"
                    if (quest != null)
                    {
                        goal.Value = Math.Max(goal.Value.Value, quest.Kept);
                        goal.IsQuest = true;
                    }
                    // The steward moves every prisoner of a Ransom / Donate row it can; one left on an untouched row cannot go
                    // here (no ransom broker in a village, a lord to donate where the game forbids it or the dungeon is full).
                    if (row.Result > goal.Value && !row.IsTouched)
                        goal.Short = GoalShort.NotPossibleHere;
                    return goal;
                default:
                    if (row.TakesGoal)
                    {
                        goal.Editable = true;
                        if (row.ManualGoal != null)
                        {
                            goal.Value = row.ManualGoal;
                            goal.IsYours = true;
                            goal.BelowQuest = quest != null && row.ManualGoal.Value < quest.Need; // yours wins (DESIGN §2.9)
                            break;
                        }
                        if (quest != null)
                        {
                            // The quests' goal: at least what they need — above the steward's where it acts.
                            goal.Value = row.StartsAtDenari != null ? quest.Need : Math.Max(row.StewardGoal ?? row.Result, quest.Need);
                            goal.IsQuest = true;
                            break;
                        }
                        if (row.StartsAtDenari != null)
                            return HandsOffAt(goal, row.StartsAtDenari.Value);
                        // The steward's: a role row's target, a food row's planned Result (its share of the days goal).
                        goal.Value = row.StewardGoal ?? row.Result;
                        break;
                    }
                    // The noble and lame horse rows: only ever sold — but what a quest keeps stays.
                    if (quest != null)
                    {
                        goal.Value = quest.Kept;
                        goal.IsQuest = true;
                        break;
                    }
                    if (row.StartsAtDenari != null)
                        return HandsOffAt(goal, row.StartsAtDenari.Value);
                    goal.Value = 0;
                    break;
            }
            if (row.Type == RowType.Loot && quest != null)
            {
                goal.Value = quest.Kept; // the pieces a quest keeps instead of 0
                goal.IsQuest = true;
            }
            if (goal.Value != null && row.Result != goal.Value)
                goal.Short = row.GoalShort;
            return goal;
        }

        private static RowGoal HandsOffAt(RowGoal goal, int startsAt)
        {
            goal.HandsOff = true;
            goal.StartsAt = startsAt;
            return goal;
        }
    }

    /// <summary>Why a walk stopped short, in the Goal's words (round 5).</summary>
    internal static class GoalReasons
    {
        /// <summary>A line walked with a quota (a goal of the player's) that stopped short.</summary>
        public static GoalShort Of(WalkLine line, bool floorApplies) => Of(line.Short, line.Cursor.Lane, floorApplies);

        /// <summary>Why the next unit of a lane cannot move now — the steward's own walks, which carry no quota.</summary>
        public static GoalShort Now(LaneCursor cursor, MarketState market, int? ceiling) =>
            Of(cursor.WhyNot(market, ceiling), cursor.Lane, true);

        public static GoalShort Of(LaneStop stop, TradeLane lane, bool floorApplies)
        {
            bool buying = lane.Direction == TradeDirection.Buy;
            switch (stop)
            {
                case LaneStop.Exhausted:
                    return buying ? lane.Capacity == 0 ? GoalShort.NoneEligible : GoalShort.MarketStock : GoalShort.NothingToSell;
                case LaneStop.StockTaken:
                    return GoalShort.StockTaken;
                case LaneStop.PriceLimit:
                    return buying ? GoalShort.PriceCap : GoalShort.MinSellPrice;
                case LaneStop.Ceiling:
                    return buying ? floorApplies ? GoalShort.PurseFloor : GoalShort.None : GoalShort.MarketGold;
                default:
                    return GoalShort.None;
            }
        }
    }
}
