using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Presentation
{
    /// <summary>The spreadsheet's sections, in the approved mockup's order (docs/mockups/README.md, Anton 2026.09.28).</summary>
    public enum SheetGroup
    {
        /// <summary>The tavern's wanderers and mercenaries, the Recruits line and the Your troops line.</summary>
        Troops,
        Food,

        /// <summary>The horse role rows (was "Mounts").</summary>
        Horses,
        Prisoners,

        /// <summary>The loot groups and the Other goods (was "Armour &amp; weapons").</summary>
        Other,
    }

    /// <summary>What a section's always-visible line is.</summary>
    public enum SheetLineKind
    {
        /// <summary>One plan row shown as a line of its own (a wanderer, the mercenaries).</summary>
        Row,

        /// <summary>The Recruits line — [+] <see cref="StewardPlan.RecruitBest"/>, [−] <see cref="StewardPlan.TakeBackRecruits"/>.</summary>
        Recruits,

        /// <summary>The Your troops line — [−] <see cref="StewardPlan.DismissLowest"/>, [+] <see cref="StewardPlan.ReAddDropped"/>.</summary>
        YourTroops,

        /// <summary>The Lords line — its toggle is <see cref="StewardSettings.LordPrisonerAction"/>.</summary>
        Lords,

        /// <summary>The Others line — its toggle is <see cref="StewardSettings.PrisonerAction"/>.</summary>
        OtherPrisoners,
    }

    /// <summary>The words of the spreadsheet's texts — English by default; the window (step 21) fills them from TextObjects so
    /// a translation replaces them. Numbers are formatted by <see cref="UiFormat"/>.</summary>
    public sealed class SheetWords
    {
        public string MenAfterNote { get; set; } = "men after the deal / party limit";
        public string Kinds { get; set; } = "kinds";
        public string Avg { get; set; } = "avg";
        public string PerKind { get; set; } = "per kind";
        public string Min { get; set; } = "min";
        public string Days { get; set; } = "days";

        /// <summary>Before a day count the game's fonts cannot draw "≈" for.</summary>
        public string About { get; set; } = "~";
        public string BeforeHerd { get; set; } = "before the herd slows you";
        public string OnFoot { get; set; } = "on foot";
        public string HorsesToKeep { get; set; } = "horses to keep";
        public string StartsAt { get; set; } = "starts at";
        public string Denari { get; set; } = "denari";
        public string PackAnimalsStart { get; set; } = "pack animals start at";
        public string RidingHorsesStart { get; set; } = "riding horses start at";
        public string WarHorsesStart { get; set; } = "war horses start at";
        public string NobleHorsesStart { get; set; } = "noble horses start at";
        public string Lord { get; set; } = "lord";
        public string Lords { get; set; } = "lords";
        public string TierPrefix { get; set; } = "T";
        public string Sold { get; set; } = "sold";
        public string Pieces { get; set; } = "pieces";
        public string Goods { get; set; } = "goods";
        public string NothingSold { get; set; } = "nothing sold";
        public string CheapestFirst { get; set; } = "cheapest first";
        public string LowestPerWeightFirst { get; set; } = "lowest price per weight first";
        public string DearestFirst { get; set; } = "dearest first";
        public string OnOffer { get; set; } = "on offer";
        public string BestTierFirst { get; set; } = "[+] takes the best tier first";
        public string Recruiting { get; set; } = "recruiting";
        public string LowestTierFirst { get; set; } = "[–] drops the lowest tier first";
        public string Dropping { get; set; } = "dropping";
        public string Types { get; set; } = "types";
        public string Ransomed { get; set; } = "ransomed";
        public string ToDungeon { get; set; } = "to the dungeon";
        public string Full { get; set; } = "full";
        public string Kept { get; set; } = "kept";
        public string Party { get; set; } = "party";
        public string Prisoners { get; set; } = "prisoners";
        public string Influence { get; set; } = "influence";
        public string NoSlowdown { get; set; } = "none";
        public string Speed { get; set; } = "speed";

        // ── Step 21: the window's lines (names and the small grey words after them) ──

        /// <summary>The troops section's two aggregate lines and the prisoners' two action lines.</summary>
        public string RecruitsLine { get; set; } = "Recruits";
        public string YourTroopsLine { get; set; } = "Your troops";
        public string LordsLine { get; set; } = "Lords";
        public string OthersLine { get; set; } = "Others";

        /// <summary>The horse role rows.</summary>
        public string PackAnimals { get; set; } = "Pack animals";
        public string RidingHorses { get; set; } = "Riding horses";
        public string WarHorses { get; set; } = "War horses";
        public string NobleHorses { get; set; } = "Noble horses";
        public string LameHorses { get; set; } = "Lame horses";

        /// <summary>The Other section's rows.</summary>
        public string Armour { get; set; } = "Armour";
        public string MeleeWeapons { get; set; } = "Melee weapons";
        public string Ranged { get; set; } = "Ranged";
        public string Shields { get; set; } = "Shields";
        public string OtherGoods { get; set; } = "Other goods";

        /// <summary>After a unit price: <c>510 each</c>.</summary>
        public string Each { get; set; } = "each";

        /// <summary>A wanderer's price: <c>444 to hire</c>.</summary>
        public string ToHire { get; set; } = "to hire";
        public string Mercenaries { get; set; } = "mercenaries";

        /// <summary>A role row's target: <c>keep 10</c>.</summary>
        public string Keep { get; set; } = "keep";
        public string SellOnly { get; set; } = "sell only";
        public string ReplacedByHealthy { get; set; } = "replaced by healthy ones";

        /// <summary>A troop type's wounded: <c>2 wounded - they go first</c>.</summary>
        public string WoundedGoFirst { get; set; } = "wounded - they go first";

        /// <summary>Loot the locks keep: <c>2 locked</c>.</summary>
        public string Locked { get; set; } = "locked";

        /// <summary>A prisoner's ransom: <c>ransom 24 each</c>, a lord's <c>lord · ransom 8,210</c>.</summary>
        public string Ransom { get; set; } = "ransom";
        public string OtherGoodsNote { get; set; } = "every unlocked good that is not food, animal or gear";

        /// <summary>The Denari cell's tooltip: <c>8 × 30–33 = –252 · your max 60</c>.</summary>
        public string YourMax { get; set; } = "your max";
        public string YourMin { get; set; } = "your min";

        // ── Round 5: the Goal column (DESIGN §1.1 "THE GOAL") ──

        /// <summary>A row whose job waits for its threshold and has no goal of yours: "no goal yet" [Claude's call — Anton
        /// suggested <c>0*</c>].</summary>
        public string HandsOffMark { get; set; } = UiFormat.Minus + "*";

        /// <summary>The hands-off hover: <c>Not managed yet: the steward starts on food at 2,000 denari – you have 1,450. Type a
        /// goal to order it anyway.</c></summary>
        public string NotManagedYet { get; set; } = "Not managed yet: the steward starts on";
        public string FoodJob { get; set; } = "food";
        public string PackAnimalsJob { get; set; } = "pack animals";
        public string RidingHorsesJob { get; set; } = "riding horses";
        public string WarHorsesJob { get; set; } = "war horses";
        public string NobleHorsesJob { get; set; } = "noble horses";
        public string At { get; set; } = "at";
        public string YouHave { get; set; } = "you have";
        public string TypeGoalAnyway { get; set; } = "Type a goal to order it anyway.";

        /// <summary>The Result's hover when it stops short of the Goal: <c>Short of the goal: the market has no more on offer.</c></summary>
        public string ShortOfGoal { get; set; } = "Short of the goal:";
        public string ShortMarketStock { get; set; } = "the market has no more on offer";
        public string ShortStockTaken { get; set; } = "another row took the rest";
        public string ShortPriceCap { get; set; } = "the next one costs more than your max price";
        public string ShortMinSellPrice { get; set; } = "the next one would fetch less than your min price";
        public string ShortMarketGold { get; set; } = "the market is out of denari";

        /// <summary><c>keeps your purse at 1,000 denari</c>.</summary>
        public string ShortPurseFloor { get; set; } = "keeps your purse at";

        /// <summary><c>waits for 2,000 denari</c>.</summary>
        public string ShortThreshold { get; set; } = "waits for";
        public string ShortNoneEligible { get; set; } = "nothing on offer the steward may buy";
        public string ShortNothingToSell { get; set; } = "nothing more it may sell (locked or unticked)";
        public string ShortSurplusKept { get; set; } = "selling the surplus is off in the Instructions";
        public string ShortNotPossibleHere { get; set; } = "not possible here";

        // ── Step 26: quest needs in the Goal column (DESIGN §2.9) ──

        /// <summary>The quest hover's head: <c>Kept for your quests:</c>, then one line per quest.</summary>
        public string QuestKeptFor { get; set; } = "Kept for your quests:";

        /// <summary>A goal of yours below the quests' need: <c>Below what your quests need:</c>, then the quests.</summary>
        public string QuestBelow { get; set; } = "Below what your quests need:";

        /// <summary><c>, you hold 5</c> — the party holds fewer than the quest asks for.</summary>
        public string QuestYouHold { get; set; } = "you hold";

        // ── Step 32: the quest note after a name (DESIGN §1.1 / §2.9) ──

        /// <summary><c>120 needed for quest</c> — one quest, not yet held in full.</summary>
        public string QuestNoteNeeded { get; set; } = "needed for quest";

        /// <summary><c>220 needed for quests</c> — several quests on the line, summed.</summary>
        public string QuestNoteNeededMany { get; set; } = "needed for quests";

        /// <summary><c>120 for quest, held</c> — what the party holds covers it.</summary>
        public string QuestNoteHeld { get; set; } = "for quest, held";

        /// <summary><c>220 for quests, held</c>.</summary>
        public string QuestNoteHeldMany { get; set; } = "for quests, held";

        /// <summary>The quest note's hover head: <c>Your quests ask for:</c>, then one line per quest.</summary>
        public string QuestAskFor { get; set; } = "Your quests ask for:";
    }

    /// <summary>One always-visible line of a section: a plan row shown on its own, or an aggregate line over detail rows.</summary>
    public sealed class SheetLine
    {
        internal SheetLine(SheetLineKind kind)
        {
            Kind = kind;
        }

        public SheetLineKind Kind { get; }

        /// <summary>The row, for <see cref="SheetLineKind.Row"/>.</summary>
        public PlanRow? Row { get; internal set; }

        /// <summary>The small words after the line's name: <c>dropping 2 T0 Empire Peasant, 1 T1 Imperial Recruit</c>,
        /// <c>18 on offer · [+] takes the best tier first</c>, <c>2 lords · 2 ransomed</c>, <c>10 to the dungeon (full), 40
        /// ransomed</c>; empty for a plain row.</summary>
        public string Text { get; internal set; } = "";

        /// <summary>Men held (null = "–": the Recruits line).</summary>
        public int? Mine { get; internal set; }

        /// <summary>The line's change: recruits (+), men dropped (−), prisoners moved (−).</summary>
        public int Change { get; internal set; }

        public int? Result => Mine + Change;

        /// <summary>Volunteers on offer (the Recruits line); null = "–".</summary>
        public int? Market { get; internal set; }

        /// <summary>The line's columns: Denari, Influence, Party, Prisoners, Land kg, Sea kg.</summary>
        public PlanMetrics Metrics { get; internal set; }

        /// <summary>The detail rows the line unfolds to, in the line's order (Recruits highest tier first, Your troops lowest).</summary>
        public IReadOnlyList<PlanRow> Details { get; internal set; } = Array.Empty<PlanRow>();

        /// <summary>The Lords / Others toggle: the setting's value (Keep | Ransom | Donate).</summary>
        public PrisonerChoice? Action { get; internal set; }
    }

    /// <summary>One section of the spreadsheet: its title line (the overview + its subtotal), the lines that always show, and
    /// the detail rows that fold.</summary>
    public sealed class SheetSection
    {
        internal SheetSection(SheetGroup group)
        {
            Group = group;
        }

        public SheetGroup Group { get; }

        /// <summary>The title line's stats: Troops <c>104/101</c>; Food <c>7/8 kinds · avg 29 ± 19 per kind · min 4 Date Fruit ·
        /// 188 » 200 (+12) · ~32 » 42 days</c>; Horses <c>111 / 196 before the herd slows you · 92 on foot, 101 horses to
        /// keep</c>; Prisoners <c>52/60 · 2 lords · avg T2.0 ± 1.1 · T1–T5</c>; Other <c>49 sold: 36 pieces, 13 goods · cheapest
        /// first</c>. A waiting job adds <c>starts at 2,000 denari</c>.</summary>
        public string Overview { get; internal set; } = "";

        /// <summary>A small grey note after the overview (Troops: <c>men after the deal / party limit</c>).</summary>
        public string OverviewNote { get; internal set; } = "";

        /// <summary>The overview's number is past a limit (the party over its size limit, the herd slowing the party) — red.</summary>
        public bool OverviewWarning { get; internal set; }

        /// <summary>The section's subtotal: the sum of its rows (each row once).</summary>
        public PlanMetrics Metrics { get; internal set; }

        /// <summary>The lines that stay when the section is folded (the tavern rows and the two troop lines; Lords, Others).</summary>
        public IReadOnlyList<SheetLine> Lines { get; internal set; } = Array.Empty<SheetLine>();

        /// <summary>The rows that fold under the title (Food, Horses, Other; the prisoner rows — lowest tier first, lords last).</summary>
        public IReadOnlyList<PlanRow> Details { get; internal set; } = Array.Empty<PlanRow>();

        /// <summary>Every plan row of the section, each once.</summary>
        public IReadOnlyList<PlanRow> Rows { get; internal set; } = Array.Empty<PlanRow>();
    }

    /// <summary>One row of the footer's weight table (Anton 2026.09.28, round 4: "in cols again, before, change, after,
    /// capacity, capacity left, try adding a col slowdown").</summary>
    public sealed class WeightTableRow
    {
        internal WeightTableRow(bool isSea)
        {
            IsSea = isSea;
        }

        /// <summary>The Sea row (with ships — War Sails); else Land.</summary>
        public bool IsSea { get; }

        /// <summary>The capacity was read — else only the change is known (the other cells are empty).</summary>
        public bool Known { get; internal set; }

        public double Before { get; internal set; }
        public double Change { get; internal set; }
        public double After { get; internal set; }
        public double CapacityBefore { get; internal set; }
        public double CapacityAfter { get; internal set; }

        /// <summary>Capacity after − load after; negative = over (red).</summary>
        public double Left => CapacityAfter - After;
        public bool Over => Known && Left < 0;

        /// <summary>The share of speed the load after the deal takes (0 = none) — <see cref="CarryTotals.LandSlowdown"/>.</summary>
        public double Slowdown { get; internal set; }

        /// <summary>The same in the game's speed points (its tooltip's "Overburdened" line).</summary>
        public double SpeedLoss { get; internal set; }

        public string BeforeText { get; internal set; } = "";
        public string ChangeText { get; internal set; } = "";
        public string AfterText { get; internal set; } = "";

        /// <summary><c>6,185 » 6,305</c>, or <c>6,305</c> when the deal does not move it.</summary>
        public string CapacityText { get; internal set; } = "";
        public string LeftText { get; internal set; } = "";

        /// <summary><c>none</c>, <c>–12%</c>, <c>–0.4%</c> — or, at sea without the fleet's speed, <c>–0.40 speed</c>.</summary>
        public string SlowdownText { get; internal set; } = "";
    }

    /// <summary>
    /// The Suggestion tab as ONE spreadsheet (PLAN step 20 → the window of step 21; the mockup Anton approved on 2026.09.28 —
    /// docs/mockups/README.md, DESIGN §1.1): the header's denari and influence, the sections in the mockup's order with their
    /// overview lines, always-visible lines and detail rows, every number column (<see cref="PlanMetrics"/>: Denari, Influence,
    /// Party, Prisoners, Land kg, Sea kg) per row, line, section and the Total, and the footer's weight table. Built from the
    /// plan as it stands — call it again after every click. Pure: the window only binds it.
    /// </summary>
    public sealed class SuggestionSheet
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private SuggestionSheet(StewardPlan plan)
        {
            Plan = plan;
        }

        /// <summary>The plan the sheet was built from.</summary>
        public StewardPlan Plan { get; }

        public IReadOnlyList<SheetSection> Sections { get; private set; } = Array.Empty<SheetSection>();

        /// <summary>The Total line: every row once.</summary>
        public PlanMetrics Total { get; private set; }

        /// <summary><c>party 104 » 105 · prisoners 52 » 0</c> (prisoners left out when there are none).</summary>
        public string TotalText { get; private set; } = "";

        /// <summary>The header: <c>69,358 » 89,189 (+19,831)</c> (just <c>69,358</c> when the deal moves no denari).</summary>
        public string DenariText { get; private set; } = "";

        /// <summary>The header's small influence: <c>+11.2 influence</c>; empty when none.</summary>
        public string InfluenceText { get; private set; } = "";

        /// <summary>The header in its two colours (step 21): <c>69,358 » 89,189</c> (just <c>69,358</c> when the deal moves no
        /// denari) …</summary>
        public string DenariFlowText { get; private set; } = "";

        /// <summary>… and <c>(+19,831)</c> — green in, red out (<see cref="DenariChange"/>); empty when even.</summary>
        public string DenariChangeText { get; private set; } = "";

        /// <summary>The deal's net denari: + earned, − spent.</summary>
        public int DenariChange { get; private set; }

        /// <summary>The weight table: Land, then Sea with ships.</summary>
        public IReadOnlyList<WeightTableRow> Weights { get; private set; } = Array.Empty<WeightTableRow>();

        /// <summary>The party has ships (War Sails): the Sea kg column and the Sea row mean something.</summary>
        public bool ShowSea { get; private set; }

        /// <summary>The ransom broker is open here — the Ransom toggle works.</summary>
        public bool RansomAllowedHere { get; private set; }

        /// <summary>The game allows donating prisoners here — else the Donate toggle greys (mockup choice 7).</summary>
        public bool DonateAllowedHere { get; private set; }

        /// <summary>Why not (step 34): the Donate toggle's hover and a castle's notice say it.</summary>
        public DonateBlock DonateBlock { get; private set; }

        /// <summary>Step 34: the plan is a castle's — the Prisoners section alone, donations only.</summary>
        public bool IsCastle { get; private set; }

        public SheetSection? Section(SheetGroup group) => Sections.FirstOrDefault(s => s.Group == group);

        public static SuggestionSheet Of(StewardPlan plan, SheetWords? words = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            words ??= new SheetWords();
            var snapshot = plan.Snapshot ?? new StewardSnapshot();
            var settings = plan.Settings ?? new StewardSettings();
            var t = plan.Totals;
            var sheet = new SuggestionSheet(plan)
            {
                ShowSea = t.Carry.ShowSea,
                RansomAllowedHere = snapshot.SettlementKind == SettlementKind.Town && (snapshot.Prison?.CanRansom ?? false),
                // Step 34: a castle's dungeon takes donations by the town's own rule (RESEARCH §31).
                DonateAllowedHere = snapshot.SettlementKind != SettlementKind.Village && (snapshot.Prison?.DonateAllowed ?? false),
                DonateBlock = snapshot.SettlementKind == SettlementKind.Village ? DonateBlock.NoDungeon
                    : snapshot.Prison?.DonateBlock ?? DonateBlock.None,
                IsCastle = snapshot.SettlementKind == SettlementKind.Castle,
            };

            var sections = new List<SheetSection>();
            void Add(SheetSection? s)
            {
                if (s != null && s.Rows.Count > 0)
                    sections.Add(s);
            }
            Add(Troops(plan, words));
            Add(Food(plan, snapshot, words));
            Add(Horses(plan, words));
            Add(Prisoners(plan, snapshot, words));
            Add(Other(plan, settings, words));
            sheet.Sections = sections;

            sheet.Total = PlanMetrics.Sum(plan.Rows);
            sheet.TotalText = TotalLine(t, words);
            sheet.DenariText = t.GoldChange == 0
                ? UiFormat.Money(t.GoldNow)
                : UiFormat.Money(t.GoldNow) + " " + UiFormat.Arrow + " " + UiFormat.Money(t.GoldAfter) + " ("
                  + UiFormat.SignedMoney(t.GoldChange) + ")";
            sheet.InfluenceText = Influence(t.InfluenceGained, words);
            sheet.DenariChange = t.GoldChange;
            sheet.DenariFlowText = t.GoldChange == 0
                ? UiFormat.Money(t.GoldNow)
                : UiFormat.Money(t.GoldNow) + " " + UiFormat.Arrow + " " + UiFormat.Money(t.GoldAfter);
            sheet.DenariChangeText = t.GoldChange == 0 ? "" : "(" + UiFormat.SignedMoney(t.GoldChange) + ")";
            sheet.Weights = WeightTable(t.Carry, words);
            return sheet;
        }

        // ── Sections ─────────────────────────────────────────────────────────────────────────────────────

        private static List<PlanRow> RowsOf(StewardPlan plan, params PlanSectionKind[] kinds) =>
            plan.Sections.Where(s => Array.IndexOf(kinds, s.Kind) >= 0).SelectMany(s => s.Rows).ToList();

        private static SheetSection Troops(StewardPlan plan, SheetWords words)
        {
            var tavern = RowsOf(plan, PlanSectionKind.Tavern);
            var troops = RowsOf(plan, PlanSectionKind.Recruits, PlanSectionKind.Troops);
            var t = plan.Totals;
            var lines = tavern.Select(r => new SheetLine(SheetLineKind.Row) { Row = r, Metrics = PlanMetrics.Of(r) }).ToList();

            var recruits = plan.RecruitRows;
            if (recruits.Count > 0)
            {
                var joining = recruits.Where(r => r.Change > 0).ToList();
                lines.Add(new SheetLine(SheetLineKind.Recruits)
                {
                    Text = joining.Count > 0
                        ? words.Recruiting + " " + Men(joining, r => r.Change, words)
                        : UiFormat.Money(recruits.Sum(r => r.Troop!.OnOffer)) + " " + words.OnOffer + Sep + words.BestTierFirst,
                    Mine = null,
                    Change = joining.Sum(r => r.Change),
                    Market = recruits.Sum(r => r.Troop!.OnOffer),
                    Metrics = PlanMetrics.Sum(joining),
                    Details = recruits,
                });
            }
            var yours = plan.YourTroopRows;
            if (yours.Count > 0)
            {
                var leaving = yours.Where(r => r.Change < 0).ToList();
                lines.Add(new SheetLine(SheetLineKind.YourTroops)
                {
                    Text = leaving.Count > 0 ? words.Dropping + " " + Men(leaving, r => -r.Change, words) : words.LowestTierFirst,
                    Mine = yours.Sum(r => r.Mine),
                    Change = leaving.Sum(r => r.Change),
                    Metrics = PlanMetrics.Sum(leaving),
                    Details = yours,
                });
            }

            var all = tavern.Concat(troops).ToList();
            return new SheetSection(SheetGroup.Troops)
            {
                Overview = UiFormat.Money(t.MembersAfter) + (t.PartySizeLimit > 0 ? "/" + UiFormat.Money(t.PartySizeLimit) : ""),
                OverviewNote = t.PartySizeLimit > 0 ? words.MenAfterNote : "",
                OverviewWarning = t.OverPartyLimit,
                Metrics = PlanMetrics.Sum(all),
                Lines = lines,
                Rows = all,
            };
        }

        private static SheetSection Food(StewardPlan plan, StewardSnapshot snapshot, SheetWords words)
        {
            var rows = RowsOf(plan, PlanSectionKind.Food);
            var t = plan.Totals;
            var parts = new List<string>();
            var held = rows.Where(r => r.Result > 0).ToList();
            var possible = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in (snapshot.Inventory ?? new List<ItemStack>()).Concat(snapshot.Market ?? new List<ItemStack>()))
                if (s != null && s.Kind == ItemKind.Food && s.Count > 0)
                    possible.Add(s.ItemId);
            foreach (var r in rows)
                if (r.ItemId != null)
                    possible.Add(r.ItemId);
            parts.Add(UiFormat.Money(held.Count) + "/" + UiFormat.Money(possible.Count) + " " + words.Kinds);
            if (held.Count > 0)
            {
                var (mean, std) = MeanStd(held.Select(r => ((double)r.Result, 1)));
                parts.Add(words.Avg + " " + Stat(mean, std) + " " + words.PerKind);
                var least = held.OrderBy(r => r.Result).ThenBy(r => r.Name, StringComparer.Ordinal).First();
                parts.Add(words.Min + " " + UiFormat.Money(least.Result) + " " + least.Name);
            }
            int change = t.FoodUnitsAfter - t.FoodUnitsNow;
            parts.Add(change == 0
                ? UiFormat.Money(t.FoodUnitsAfter)
                : UiFormat.Money(t.FoodUnitsNow) + " " + UiFormat.Arrow + " " + UiFormat.Money(t.FoodUnitsAfter) + " ("
                  + UiFormat.SignedMoney(change) + ")");
            if (t.FoodDaysAfter != null)
            {
                string now = UiFormat.Days(t.FoodDaysNow), after = UiFormat.Days(t.FoodDaysAfter);
                parts.Add(words.About + (now == after ? after : now + " " + UiFormat.Arrow + " " + after) + " " + words.Days);
            }
            int? waits = plan.Facts.StartsAt(ManagedJob.Food);
            if (waits != null)
                parts.Add(words.StartsAt + " " + UiFormat.Money(waits.Value) + " " + words.Denari);
            return new SheetSection(SheetGroup.Food)
            {
                Overview = string.Join(Sep, parts),
                Metrics = PlanMetrics.Sum(rows),
                Details = rows,
                Rows = rows,
            };
        }

        private static SheetSection Horses(StewardPlan plan, SheetWords words)
        {
            var rows = RowsOf(plan, PlanSectionKind.Mounts);
            var herd = plan.Totals.Herd;
            var facts = plan.Facts;
            var parts = new List<string>
            {
                UiFormat.Money(herd.Horses) + " / " + UiFormat.Money(herd.Room) + " " + words.BeforeHerd,
                UiFormat.Money(facts.Footmen) + " " + words.OnFoot + ", " + UiFormat.Money(facts.MountTarget) + " " + words.HorsesToKeep,
            };
            var waiting = new List<string>();
            void Wait(ManagedJob job, string label)
            {
                int? at = facts.StartsAt(job);
                if (at != null)
                    waiting.Add(label + " " + UiFormat.Money(at.Value) + " " + words.Denari);
            }
            Wait(ManagedJob.PackAnimals, words.PackAnimalsStart);
            Wait(ManagedJob.Mounts, words.RidingHorsesStart);
            Wait(ManagedJob.WarHorses, words.WarHorsesStart);
            Wait(ManagedJob.NobleHorses, words.NobleHorsesStart); // step 28: only while noble horses are kept
            if (waiting.Count > 0)
                parts.Add(string.Join(", ", waiting));
            return new SheetSection(SheetGroup.Horses)
            {
                Overview = string.Join(Sep, parts),
                OverviewWarning = herd.SlowsParty,
                Metrics = PlanMetrics.Sum(rows),
                Details = rows,
                Rows = rows,
            };
        }

        private static SheetSection Prisoners(StewardPlan plan, StewardSnapshot snapshot, SheetWords words)
        {
            var rows = RowsOf(plan, PlanSectionKind.Prisoners);
            var all = (snapshot.Prisoners ?? new List<PrisonerStack>()).Where(p => p != null && p.Count > 0).ToList();
            int count = all.Sum(p => p.Count);
            int limit = Math.Max(0, snapshot.Party?.PrisonerSizeLimit ?? 0);
            var parts = new List<string> { UiFormat.Money(count) + (limit > 0 ? "/" + UiFormat.Money(limit) : "") };
            int lords = all.Where(p => p.IsHero).Sum(p => p.Count);
            if (lords > 0)
                parts.Add(UiFormat.Money(lords) + " " + (lords == 1 ? words.Lord : words.Lords));
            var others = all.Where(p => !p.IsHero).ToList();
            if (others.Count > 0)
            {
                var (mean, std) = MeanStd(others.Select(p => ((double)p.Tier, p.Count)));
                parts.Add(words.Avg + " " + words.TierPrefix + Decimal1(mean) + " " + UiFormat.PlusMinus + " " + Decimal1(std));
                int lo = others.Min(p => p.Tier), hi = others.Max(p => p.Tier);
                parts.Add(words.TierPrefix + lo.ToString(Inv) + (hi > lo ? UiFormat.RangeDash + words.TierPrefix + hi.ToString(Inv) : ""));
            }

            var settings = plan.Settings ?? new StewardSettings();
            int room = plan.DungeonRoom;
            var lines = new List<SheetLine>();
            var lordRows = rows.Where(r => r.Prisoner!.IsHero).ToList();
            var otherRows = rows.Where(r => !r.Prisoner!.IsHero).ToList();
            int donated = rows.Sum(r => r.Prisoner!.DonateCount);
            bool full = donated > 0 && donated >= room;
            if (lordRows.Count > 0)
                lines.Add(PrisonerLine(SheetLineKind.Lords, lordRows, settings.LordPrisonerAction, full,
                    UiFormat.Money(lordRows.Sum(r => r.Mine)) + " " + (lordRows.Sum(r => r.Mine) == 1 ? words.Lord : words.Lords), words));
            if (otherRows.Count > 0)
                lines.Add(PrisonerLine(SheetLineKind.OtherPrisoners, otherRows, settings.PrisonerAction, full, "", words));
            return new SheetSection(SheetGroup.Prisoners)
            {
                Overview = string.Join(Sep, parts),
                Metrics = PlanMetrics.Sum(rows),
                Lines = lines,
                Details = rows,
                Rows = rows,
            };
        }

        private static SheetLine PrisonerLine(SheetLineKind kind, List<PlanRow> rows, PrisonerChoice action, bool full, string head,
            SheetWords words)
        {
            int donated = rows.Sum(r => r.Prisoner!.DonateCount), ransomed = rows.Sum(r => r.Prisoner!.RansomCount);
            var moves = new List<string>();
            if (donated > 0)
                moves.Add(UiFormat.Money(donated) + " " + words.ToDungeon + (full ? " (" + words.Full + ")" : ""));
            if (ransomed > 0)
                moves.Add(UiFormat.Money(ransomed) + " " + words.Ransomed);
            string what = moves.Count > 0 ? string.Join(", ", moves) : words.Kept;
            return new SheetLine(kind)
            {
                Text = head.Length == 0 ? what : head + Sep + what,
                Mine = rows.Sum(r => r.Mine),
                Change = rows.Sum(r => r.Change),
                Metrics = PlanMetrics.Sum(rows),
                Details = rows,
                Action = action,
            };
        }

        private static SheetSection Other(StewardPlan plan, StewardSettings settings, SheetWords words)
        {
            var rows = RowsOf(plan, PlanSectionKind.Other);
            int pieces = rows.Where(r => r.LootGroup != LootGroup.OtherGoods).Sum(r => Math.Max(0, -r.Change));
            int goods = rows.Where(r => r.LootGroup == LootGroup.OtherGoods).Sum(r => Math.Max(0, -r.Change));
            string sold = pieces > 0 && goods > 0
                ? UiFormat.Money(pieces + goods) + " " + words.Sold + ": " + UiFormat.Money(pieces) + " " + words.Pieces + ", "
                  + UiFormat.Money(goods) + " " + words.Goods
                : pieces > 0 ? UiFormat.Money(pieces) + " " + words.Pieces + " " + words.Sold
                : goods > 0 ? UiFormat.Money(goods) + " " + words.Goods + " " + words.Sold
                : words.NothingSold;
            string order = settings.SellLootOrder == SellLootOrder.MostExpensive ? words.DearestFirst
                : settings.SellLootOrder == SellLootOrder.LowestPricePerWeight ? words.LowestPerWeightFirst
                : words.CheapestFirst;
            return new SheetSection(SheetGroup.Other)
            {
                Overview = sold + Sep + order,
                Metrics = PlanMetrics.Sum(rows),
                Details = rows,
                Rows = rows,
            };
        }

        // ── The Total, the header, the weight table ──────────────────────────────────────────────────────

        private static string TotalLine(PlanTotals t, SheetWords words)
        {
            string party = words.Party + " " + (t.MembersAfter == t.MembersNow
                ? UiFormat.Money(t.MembersNow)
                : UiFormat.Money(t.MembersNow) + " " + UiFormat.Arrow + " " + UiFormat.Money(t.MembersAfter));
            if (t.PrisonersNow == 0 && t.PrisonersAfter == 0)
                return party;
            string prisoners = words.Prisoners + " " + (t.PrisonersAfter == t.PrisonersNow
                ? UiFormat.Money(t.PrisonersNow)
                : UiFormat.Money(t.PrisonersNow) + " " + UiFormat.Arrow + " " + UiFormat.Money(t.PrisonersAfter));
            return party + Sep + prisoners;
        }

        /// <summary><c>+11.2 influence</c>; empty below 0.05.</summary>
        public static string Influence(double influence, SheetWords? words = null) =>
            Math.Abs(influence) < 0.05 ? "" : UiFormat.SignedInfluence(influence) + " " + (words ?? new SheetWords()).Influence;

        private static List<WeightTableRow> WeightTable(CarryTotals c, SheetWords words)
        {
            var land = new WeightTableRow(false)
            {
                Known = c.Known,
                Before = c.WeightNow,
                Change = c.WeightAfter - c.WeightNow,
                After = c.WeightAfter,
                CapacityBefore = c.CapacityLandNow,
                CapacityAfter = c.CapacityLandAfter,
                Slowdown = c.LandSlowdown,
                SpeedLoss = c.LandSpeedLoss,
            };
            Fill(land, true, words);
            var rows = new List<WeightTableRow> { land };
            if (c.ShowSea)
            {
                var sea = new WeightTableRow(true)
                {
                    Known = true,
                    Before = c.WeightAtSeaNow,
                    Change = c.WeightAtSeaAfter - c.WeightAtSeaNow,
                    After = c.WeightAtSeaAfter,
                    CapacityBefore = c.CapacitySeaNow,
                    CapacityAfter = c.CapacitySeaAfter,
                    Slowdown = c.SeaSlowdown,
                    SpeedLoss = c.SeaSpeedLoss,
                };
                Fill(sea, c.SeaSlowdownKnown, words);
                rows.Add(sea);
            }
            return rows;
        }

        private static void Fill(WeightTableRow row, bool shareKnown, SheetWords words)
        {
            row.ChangeText = SignedKg(row.Change);
            if (!row.Known)
                return;
            row.BeforeText = UiFormat.Kg(row.Before);
            row.AfterText = UiFormat.Kg(row.After);
            string before = UiFormat.Kg(row.CapacityBefore), after = UiFormat.Kg(row.CapacityAfter);
            row.CapacityText = before == after ? after : before + " " + UiFormat.Arrow + " " + after;
            row.LeftText = UiFormat.Kg(row.Left);
            row.SlowdownText = SlowdownText(row.Slowdown, row.SpeedLoss, shareKnown, words);
        }

        /// <summary><c>none</c>, <c>–12%</c>, <c>–0.4%</c>; without the base speed the points: <c>–0.40 speed</c>.</summary>
        public static string SlowdownText(double share, double points, bool shareKnown, SheetWords? words = null)
        {
            words ??= new SheetWords();
            if (points <= 0)
                return words.NoSlowdown;
            if (!shareKnown)
                return UiFormat.Minus + points.ToString("0.00", Inv) + " " + words.Speed;
            double percent = share * 100;
            string text = percent < 1 ? Math.Max(0.1, Math.Round(percent, 1, MidpointRounding.AwayFromZero)).ToString("0.#", Inv)
                : Math.Round(percent, MidpointRounding.AwayFromZero).ToString("#,0", Inv);
            return UiFormat.Minus + text + "%";
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────

        private const string Sep = " " + UiFormat.Dot + " ";

        /// <summary>A kg change as a whole number: <c>+120</c>, <c>–318</c>, <c>0</c>.</summary>
        public static string SignedKg(double kg) => UiFormat.SignedMoney((long)Math.Round(kg, MidpointRounding.AwayFromZero));

        /// <summary><c>2 T0 Empire Peasant, 1 T1 Imperial Recruit</c> — past three types, <c>12 (5 types)</c>.</summary>
        private static string Men(List<PlanRow> rows, Func<PlanRow, int> men, SheetWords words)
        {
            if (rows.Count > 3)
                return UiFormat.Money(rows.Sum(men)) + " (" + UiFormat.Money(rows.Count) + " " + words.Types + ")";
            var summary = new SummaryWords { TierPrefix = words.TierPrefix };
            return string.Join(", ", rows.Select(r => UiFormat.Money(men(r)) + " " + SectionSummary.TroopName(r, summary)));
        }

        /// <summary>The mean and POPULATION standard deviation of weighted values.</summary>
        internal static (double Mean, double Std) MeanStd(IEnumerable<(double Value, int Weight)> values)
        {
            double n = 0, sum = 0, sq = 0;
            foreach (var (value, weight) in values)
            {
                if (weight <= 0) continue;
                n += weight;
                sum += value * weight;
                sq += value * value * weight;
            }
            if (n <= 0)
                return (0, 0);
            double mean = sum / n;
            return (mean, Math.Sqrt(Math.Max(0, sq / n - mean * mean)));
        }

        /// <summary><c>29 ± 19</c> — one decimal where it reads better: below 10.</summary>
        private static string Stat(double mean, double std) =>
            mean < 10
                ? Decimal1(mean) + " " + UiFormat.PlusMinus + " " + Decimal1(std)
                : Whole(mean) + " " + UiFormat.PlusMinus + " " + Whole(std);

        private static string Whole(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("#,0", Inv);

        private static string Decimal1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", Inv);
    }
}
