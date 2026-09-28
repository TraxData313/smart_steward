using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Presentation
{
    /// <summary>What one line of the spreadsheet is (PLAN step 21).</summary>
    public enum SheetItemKind
    {
        /// <summary>A plan row: a wanderer, the mercenaries, a troop type, a food, a horse role, a prisoner type, a loot group.</summary>
        Row,

        /// <summary>The Recruits line: [+] <see cref="StewardPlan.RecruitBest"/>, [−] <see cref="StewardPlan.TakeBackRecruits"/>.</summary>
        Recruits,

        /// <summary>The Your troops line: [−] <see cref="StewardPlan.DismissLowest"/>, [+] <see cref="StewardPlan.ReAddDropped"/>.</summary>
        YourTroops,

        /// <summary>The Lords line: Keep | Ransom | Donate = <see cref="StewardSettings.LordPrisonerAction"/>.</summary>
        Lords,

        /// <summary>The Others line: Keep | Ransom | Donate = <see cref="StewardSettings.PrisonerAction"/>.</summary>
        OtherPrisoners,

        /// <summary>One kind inside a row's ▸ breakdown (a horse type, a good) — small, no buttons.</summary>
        SubLine,
    }

    /// <summary>Which side of a troop row a line shows (mockup choice 8: Recruits only hire, Your troops only dismiss — a type you
    /// hold that is also on offer shows under both).</summary>
    public enum TroopSide
    {
        /// <summary>Not a troop row under one of the two lines: the whole row.</summary>
        None,

        /// <summary>Under Recruits: only its recruits (Mine and Result "–", Market = on offer).</summary>
        Recruit,

        /// <summary>Under Your troops: only its dismissals.</summary>
        Dismiss,
    }

    /// <summary>The number columns of one line as the window shows them (mockup choices 4–6): zeros blank; denari green in,
    /// red out; influence small and green; Party, Prisoners and kg plain.</summary>
    public sealed class SheetCellTexts
    {
        public string Denari { get; private set; } = "";
        public string DenariColor { get; private set; } = UiColors.Muted;

        /// <summary><c>+3.6 influence</c> — shown small, left of the denari; empty below 0.05.</summary>
        public string Influence { get; private set; } = "";
        public string Party { get; private set; } = "";
        public string Prisoners { get; private set; } = "";
        public string Land { get; private set; } = "";
        public string Sea { get; private set; } = "";

        public static SheetCellTexts Of(PlanMetrics m, SheetWords? words = null) => new SheetCellTexts
        {
            Denari = m.Denari == 0 ? "" : UiFormat.SignedMoney(m.Denari),
            DenariColor = UiColors.ForMoney(m.Denari),
            Influence = SuggestionSheet.Influence(m.Influence, words),
            Party = m.Party == 0 ? "" : UiFormat.SignedMoney(m.Party),
            Prisoners = m.Prisoners == 0 ? "" : UiFormat.SignedMoney(m.Prisoners),
            Land = Kg(m.LandKg),
            Sea = Kg(m.SeaKg),
        };

        /// <summary>Whole kilos, signed; blank when it rounds to nothing.</summary>
        public static string Kg(double kg)
        {
            string text = SuggestionSheet.SignedKg(kg);
            return text == "0" ? "" : text;
        }
    }

    /// <summary>
    /// One line of the spreadsheet as the window binds it: its identity (<see cref="Key"/>), what it is, every cell as text and
    /// colour, its ▸ fold, and the live state of its buttons — [−] [+] with the editor's reasons, ⟲, or the Keep | Ransom |
    /// Donate toggle. Everything the window shows is decided here (pure, tested); the Module copies it.
    /// </summary>
    public sealed class SheetItem
    {
        internal SheetItem(string key, SheetItemKind kind)
        {
            Key = key;
            Kind = kind;
        }

        /// <summary>Unique within its section and stable across clicks and re-plans: <c>row:food:grain</c>,
        /// <c>row:troops:recruit@dismiss</c>, <c>line:recruits</c>, <c>sub:mounts:riding/palfrey</c>. The window keeps a line's
        /// widgets while its key stays.</summary>
        public string Key { get; }
        public SheetItemKind Kind { get; }

        /// <summary>The plan row (<see cref="SheetItemKind.Row"/>, and the row a <see cref="SheetItemKind.SubLine"/> belongs to).</summary>
        public PlanRow? Row { get; internal set; }
        public TroopSide Side { get; internal set; }

        /// <summary>The aggregate or prisoner line behind the item.</summary>
        public SheetLine? Line { get; internal set; }

        /// <summary>Under a line (a troop type under Recruits / Your troops) or inside a breakdown: drawn a step in.</summary>
        public bool Indented { get; internal set; }

        /// <summary>The ▸ this line opens (a troop line's rows, a row's breakdown); null = none.</summary>
        public string? FoldKey { get; internal set; }

        /// <summary>The ▸ is open (▾).</summary>
        public bool IsOpen { get; internal set; }

        public string Name { get; internal set; } = "";

        /// <summary>The name opens the Encyclopedia (wanderers, troops, prisoners) — drawn in gold.</summary>
        public bool IsLink { get; internal set; }

        /// <summary>The small grey words after the name: a unit price (<c>510 each</c>), a target (<c>keep 10</c>), what a line does.</summary>
        public string Note { get; internal set; } = "";

        public string Market { get; internal set; } = "";
        public string Mine { get; internal set; } = "";
        public string Change { get; internal set; } = "";
        public string ChangeColor { get; internal set; } = UiColors.Muted;
        public string Result { get; internal set; } = "";

        /// <summary>The line's number columns.</summary>
        public PlanMetrics Metrics { get; internal set; }
        public SheetCellTexts Cells { get; internal set; } = SheetCellTexts.Of(new PlanMetrics());

        /// <summary>The Denari cell's tooltip (mockup choice 2): <c>8 × 30–33 = –252 · your max 60</c>; empty = none.</summary>
        public string DenariHint { get; internal set; } = "";

        /// <summary>[−] n [+] in the Change column (every row and the two troop lines; not the prisoner lines, not a sub-line).</summary>
        public bool HasSpinner { get; internal set; }
        public EditBlock IncreaseBlock { get; internal set; }
        public EditBlock DecreaseBlock { get; internal set; }

        /// <summary>⟲ shows: the player's hand is on the row (on a troop line: on one of its rows, on its side).</summary>
        public bool CanReset { get; internal set; }

        /// <summary>The Lords / Others toggle: the setting's value.</summary>
        public PrisonerChoice? Choice { get; internal set; }

        /// <summary>The game allows donating here — else the Donate button greys (mockup choice 7).</summary>
        public bool DonateAllowed { get; internal set; }
    }

    /// <summary>One section as the window binds it: its title line (name, overview, subtotal) and the lines under it as they
    /// stand with the folds.</summary>
    public sealed class SheetSectionView
    {
        internal SheetSectionView(SheetSection section)
        {
            Section = section;
        }

        public SheetSection Section { get; }
        public SheetGroup Group => Section.Group;
        public string Overview => Section.Overview;
        public string OverviewNote => Section.OverviewNote;
        public bool OverviewWarning => Section.OverviewWarning;

        /// <summary>The title line's subtotal.</summary>
        public SheetCellTexts Cells { get; internal set; } = SheetCellTexts.Of(new PlanMetrics());

        /// <summary>What a click on the title folds: the section's own key, or — Troops — both troop lines (mockup choice 8).</summary>
        public IReadOnlyList<string> FoldKeys { get; internal set; } = Array.Empty<string>();
        public bool HasFold => FoldKeys.Count > 0;

        /// <summary>▾ (something of it is open) — a click then folds everything it names; ▸ opens everything.</summary>
        public bool IsOpen { get; internal set; }

        public IReadOnlyList<SheetItem> Items { get; internal set; } = Array.Empty<SheetItem>();
    }

    /// <summary>
    /// The Suggestion tab's spreadsheet as the window shows it (PLAN step 21, the mockup Anton approved on 2026.09.28 —
    /// docs/mockups/README.md, DESIGN §1.1): <see cref="SuggestionSheet"/>'s sections, lines and numbers turned into the lines
    /// on screen with the folds applied (<see cref="SheetFolds"/>), every cell as text and colour, every button's live state.
    /// Built after every click (<c>PlanPerformanceTests</c> times it): a folded part costs nothing — its rows are not asked for
    /// their buttons' blocks (the editor's trial walks).
    /// </summary>
    public sealed class SheetView
    {
        private const string Dash = UiFormat.Minus;
        private const string Sep = " " + UiFormat.Dot + " ";

        private SheetView(SuggestionSheet sheet)
        {
            Sheet = sheet;
        }

        public SuggestionSheet Sheet { get; }
        public IReadOnlyList<SheetSectionView> Sections { get; private set; } = Array.Empty<SheetSectionView>();

        /// <summary>The pinned Total line's cells.</summary>
        public SheetCellTexts Total { get; private set; } = SheetCellTexts.Of(new PlanMetrics());

        /// <param name="isFolded">The remembered folds (<see cref="WindowState.IsFolded"/>); null = everything open.</param>
        public static SheetView Build(StewardPlan plan, SheetWords? words = null, Func<string, bool>? isFolded = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            words ??= new SheetWords();
            isFolded ??= _ => false;
            var sheet = SuggestionSheet.Of(plan, words);
            var view = new SheetView(sheet)
            {
                Sections = sheet.Sections.Select(s => BuildSection(plan, sheet, s, words, isFolded)).ToList(),
                Total = SheetCellTexts.Of(sheet.Total, words),
            };
            return view;
        }

        public SheetSectionView? Section(SheetGroup group) => Sections.FirstOrDefault(s => s.Group == group);

        private static SheetSectionView BuildSection(StewardPlan plan, SuggestionSheet sheet, SheetSection section, SheetWords words,
            Func<string, bool> isFolded)
        {
            var view = new SheetSectionView(section) { Cells = SheetCellTexts.Of(section.Metrics, words) };
            var items = new List<SheetItem>();
            var keys = new List<string>();
            switch (section.Group)
            {
                case SheetGroup.Troops:
                    foreach (var line in section.Lines)
                    {
                        switch (line.Kind)
                        {
                            case SheetLineKind.Recruits:
                            case SheetLineKind.YourTroops:
                                bool recruits = line.Kind == SheetLineKind.Recruits;
                                string key = recruits ? SheetFolds.Recruits : SheetFolds.YourTroops;
                                bool open = !isFolded(key);
                                keys.Add(key);
                                items.Add(TroopLine(plan, line, key, open, words));
                                if (open)
                                    foreach (var row in line.Details)
                                        items.Add(RowItem(row, recruits ? TroopSide.Recruit : TroopSide.Dismiss, true, sheet, words));
                                break;
                            default:
                                if (line.Row != null)
                                    items.Add(RowItem(line.Row, TroopSide.None, false, sheet, words));
                                break;
                        }
                    }
                    view.IsOpen = keys.Any(k => !isFolded(k));
                    break;
                default:
                    foreach (var line in section.Lines)
                        if (line.Kind == SheetLineKind.Lords || line.Kind == SheetLineKind.OtherPrisoners)
                            items.Add(PrisonerLine(line, sheet, words));
                    string? own = SheetFolds.OfSection(section.Group);
                    bool shown = own == null || !isFolded(own);
                    if (own != null)
                        keys.Add(own);
                    view.IsOpen = shown;
                    if (shown)
                        foreach (var row in section.Details)
                            AddRow(items, row, sheet, words, isFolded);
                    break;
            }
            view.FoldKeys = keys;
            view.Items = items;
            return view;
        }

        private static void AddRow(List<SheetItem> items, PlanRow row, SuggestionSheet sheet, SheetWords words, Func<string, bool> isFolded)
        {
            var item = RowItem(row, TroopSide.None, false, sheet, words);
            items.Add(item);
            string? fold = SheetFolds.OfRow(row);
            if (fold == null || row.Breakdown.Count == 0)
                return;
            item.FoldKey = fold;
            item.IsOpen = !isFolded(fold);
            if (item.IsOpen)
                foreach (var line in row.Breakdown)
                    items.Add(SubItem(row, line, words));
        }

        // ── The lines ────────────────────────────────────────────────────────────────────────────────────

        private static SheetItem TroopLine(StewardPlan plan, SheetLine line, string foldKey, bool open, SheetWords words)
        {
            bool recruits = line.Kind == SheetLineKind.Recruits;
            var item = new SheetItem(recruits ? "line:recruits" : "line:yours", recruits ? SheetItemKind.Recruits : SheetItemKind.YourTroops)
            {
                Line = line,
                Name = recruits ? words.RecruitsLine : words.YourTroopsLine,
                Note = line.Text,
                Market = line.Market == null ? "" : UiFormat.Money(line.Market.Value),
                Mine = line.Mine == null ? Dash : UiFormat.Money(line.Mine.Value),
                Change = UiFormat.SignedCount(line.Change),
                ChangeColor = UiColors.ForChange(line.Change),
                Result = line.Result == null ? Dash : UiFormat.Money(line.Result.Value),
                Metrics = line.Metrics,
                Cells = SheetCellTexts.Of(line.Metrics, words),
                HasSpinner = true,
                // Recruits: [+] the best tier on offer, [−] the newest recruit back; Your troops: [−] the lowest tier out, [+] back
                // in reverse (step 20's TroopBulk).
                IncreaseBlock = recruits ? plan.RecruitBestBlock : plan.ReAddDroppedBlock,
                DecreaseBlock = recruits ? plan.TakeBackRecruitsBlock : plan.DismissLowestBlock,
                CanReset = line.Details.Any(r => r.IsTouched && (recruits ? r.Change >= 0 : r.Change <= 0)),
                FoldKey = foldKey,
                IsOpen = open,
            };
            return item;
        }

        private static SheetItem PrisonerLine(SheetLine line, SuggestionSheet sheet, SheetWords words)
        {
            bool lords = line.Kind == SheetLineKind.Lords;
            return new SheetItem(lords ? "line:lords" : "line:others", lords ? SheetItemKind.Lords : SheetItemKind.OtherPrisoners)
            {
                Line = line,
                Name = lords ? words.LordsLine : words.OthersLine,
                Note = line.Text,
                Mine = UiFormat.Money(line.Mine ?? 0),
                Change = UiFormat.SignedCount(line.Change),
                ChangeColor = UiColors.ForChange(line.Change),
                Result = UiFormat.Money(line.Result ?? 0),
                Metrics = line.Metrics,
                Cells = SheetCellTexts.Of(line.Metrics, words),
                Choice = line.Action,
                DonateAllowed = sheet.DonateAllowedHere,
            };
        }

        /// <summary>A plan row — on its own, or one side of a troop row under its line.</summary>
        internal static SheetItem RowItem(PlanRow row, TroopSide side, bool indented, SuggestionSheet sheet, SheetWords words)
        {
            string key = "row:" + row.Id + (side == TroopSide.Recruit ? "@recruit" : side == TroopSide.Dismiss ? "@dismiss" : "");
            int change = side == TroopSide.Recruit ? Math.Max(0, row.Change)
                : side == TroopSide.Dismiss ? Math.Min(0, row.Change)
                : row.Change;
            bool shows = side == TroopSide.None || change != 0;
            var metrics = shows ? PlanMetrics.Of(row) : new PlanMetrics();
            var item = new SheetItem(key, SheetItemKind.Row)
            {
                Row = row,
                Side = side,
                Indented = indented,
                Name = RowName(row, words),
                IsLink = row.Type == RowType.Tavern || row.Type == RowType.Troop || (row.Type == RowType.Prisoner && row.TroopId != null),
                Note = Note(row, side, words),
                Market = Market(row, side),
                Mine = side == TroopSide.Recruit ? Dash : UiFormat.Money(row.Mine),
                Change = UiFormat.SignedCount(change),
                ChangeColor = UiColors.ForChange(change),
                Result = side == TroopSide.Recruit ? Dash : UiFormat.Money(row.Mine + change),
                Metrics = metrics,
                Cells = SheetCellTexts.Of(metrics, words),
                DenariHint = shows ? Hint(row, words) : "",
                HasSpinner = true,
                CanReset = row.IsTouched,
            };
            // Each line moves only its own side of a row (step 20's rule for the lines, step 21 for the rows under them).
            switch (side)
            {
                case TroopSide.Recruit:
                    item.IncreaseBlock = row.Change < 0 ? EditBlock.DismissingThisType : row.IncreaseBlock;
                    item.DecreaseBlock = row.Change <= 0 ? EditBlock.NothingRecruited : row.DecreaseBlock;
                    break;
                case TroopSide.Dismiss:
                    item.DecreaseBlock = row.Change > 0 ? EditBlock.RecruitingThisType : row.DecreaseBlock;
                    item.IncreaseBlock = row.Change > 0 ? EditBlock.RecruitingThisType
                        : row.Change == 0 ? EditBlock.NothingDropped
                        : row.IncreaseBlock;
                    break;
                default:
                    item.IncreaseBlock = row.IncreaseBlock;
                    item.DecreaseBlock = row.DecreaseBlock;
                    break;
            }
            return item;
        }

        private static SheetItem SubItem(PlanRow row, PlanRowLine line, SheetWords words)
        {
            var metrics = new PlanMetrics(line.GoldDelta, 0, 0, 0, line.LandKg, line.SeaKg);
            return new SheetItem("sub:" + row.Id + "/" + line.StackKey, SheetItemKind.SubLine)
            {
                Row = row,
                Indented = true,
                Name = string.IsNullOrEmpty(line.Name) ? line.ItemId : line.Name,
                Note = UnitPrice(line.UnitPriceMin, line.UnitPriceMax, words),
                Market = line.Market == null ? "" : UiFormat.Money(line.Market.Value),
                Mine = UiFormat.Money(line.Mine),
                Change = UiFormat.SignedCount(line.Change),
                ChangeColor = UiColors.ForChange(line.Change),
                Result = UiFormat.Money(line.Mine + line.Change),
                Metrics = metrics,
                Cells = SheetCellTexts.Of(metrics, words),
                DenariHint = UiFormat.PriceCell(Math.Abs(line.Change), line.UnitPriceMin, line.UnitPriceMax, line.GoldDelta),
            };
        }

        // ── The words of a row ───────────────────────────────────────────────────────────────────────────

        /// <summary>The Item cell: troops and prisoners with their tier (<c>T1 Imperial Recruit</c>; a lord by name), the role
        /// and group rows by their label, the rest by the game's name.</summary>
        public static string RowName(PlanRow row, SheetWords? words = null)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            words ??= new SheetWords();
            string name = string.IsNullOrEmpty(row.Name) ? row.Id : row.Name;
            if (row.Troop != null)
                return Tiered(row.Troop.Tier, name, words);
            if (row.Prisoner != null)
                return row.Prisoner.IsHero ? name : Tiered(row.Prisoner.Tier, name, words);
            if (row.Role != null)
            {
                switch (row.Role.Value)
                {
                    case MountRole.Pack: return words.PackAnimals;
                    case MountRole.Riding: return words.RidingHorses;
                    case MountRole.War: return words.WarHorses;
                    case MountRole.Noble: return words.NobleHorses;
                    case MountRole.Lame: return words.LameHorses;
                }
            }
            if (row.Type == RowType.Loot)
            {
                switch (row.LootGroup)
                {
                    case LootGroup.Armour: return words.Armour;
                    case LootGroup.MeleeWeapons: return words.MeleeWeapons;
                    case LootGroup.Ranged: return words.Ranged;
                    case LootGroup.Shields: return words.Shields;
                    case LootGroup.OtherGoods: return words.OtherGoods;
                }
            }
            return name;
        }

        private static string Tiered(int tier, string name, SheetWords words) => words.TierPrefix + UiFormat.Money(tier) + " " + name;

        /// <summary>The small grey words after a row's name (mockup choice 2: the unit price lives here).</summary>
        public static string Note(PlanRow row, TroopSide side = TroopSide.None, SheetWords? words = null)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            words ??= new SheetWords();
            var parts = new List<string>();
            void Add(string text)
            {
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }

            if (row.Tavern != null)
            {
                var t = row.Tavern;
                if (t.Kind == TavernRowKind.Mercenaries)
                {
                    Add(words.Mercenaries);
                    Add(UiFormat.Money(t.UnitPrice) + " " + words.Each);
                }
                else
                {
                    Add(UiFormat.Money(t.UnitPrice) + " " + words.ToHire);
                    Add(t.SkillTag ?? "");
                }
            }
            else if (row.Troop != null)
            {
                var t = row.Troop;
                if (side != TroopSide.Dismiss && t.OnOffer > 0)
                    Add(UiFormat.Money(t.UnitPrice) + " " + words.Each);
                if (side != TroopSide.Recruit && t.Wounded > 0)
                    Add(UiFormat.Money(t.Wounded) + " " + words.WoundedGoFirst);
            }
            else if (row.Prisoner != null)
            {
                var p = row.Prisoner;
                if (p.IsHero)
                {
                    Add(words.Lord);
                    Add(words.Ransom + " " + UiFormat.Money(p.RansomValue));
                }
                else
                {
                    if (p.DonateCount > 0)
                        Add((p.RansomCount > 0 ? UiFormat.Money(p.DonateCount) + " " : "") + words.ToDungeon);
                    if (p.DonateCount == 0 || p.RansomCount > 0)
                        Add(words.Ransom + " " + UiFormat.Money(p.RansomValue) + " " + words.Each);
                }
            }
            else if (row.Role != null)
            {
                switch (row.Role.Value)
                {
                    case MountRole.Pack:
                    case MountRole.War:
                        if (row.Target != null)
                            Add(words.Keep + " " + UiFormat.Money(row.Target.Value));
                        break;
                    case MountRole.Noble:
                        Add(words.SellOnly);
                        Add(string.Join(", ", row.Breakdown.Select(l => l.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().Take(2)));
                        break;
                    case MountRole.Lame:
                        Add(words.SellOnly);
                        Add(words.ReplacedByHealthy);
                        break;
                }
                Add(UnitPrice(row.UnitPriceMin, row.UnitPriceMax, words, row.Change));
            }
            else if (row.Type == RowType.Loot)
            {
                if (row.LootGroup == LootGroup.OtherGoods)
                    Add(words.OtherGoodsNote);
                if (row.Locked > 0)
                    Add(UiFormat.Money(row.Locked) + " " + words.Locked);
                if (row.OverValueCap > 0)
                    Add(UiFormat.Money(row.OverValueCap) + " " + words.Kept);
            }
            else
                Add(UnitPrice(row.UnitPriceMin, row.UnitPriceMax, words, row.Change));

            if (row.StartsAtDenari != null)
                Add(words.StartsAt + " " + UiFormat.Money(row.StartsAtDenari.Value) + " " + words.Denari);
            return string.Join(Sep, parts);
        }

        /// <summary><c>30–33 each</c> — the prices the deal pays or gets for the units it moves; empty when it moves none (an
        /// untouched row was never priced: its next unit's price would cost one more trial walk per row per click).</summary>
        private static string UnitPrice(int min, int max, SheetWords words, int change = 1) =>
            change == 0 || (min <= 0 && max <= 0) ? "" : UiFormat.Range(min, max) + " " + words.Each;

        private static string Market(PlanRow row, TroopSide side)
        {
            switch (side)
            {
                case TroopSide.Recruit: return UiFormat.Money(row.Troop?.OnOffer ?? 0);
                case TroopSide.Dismiss: return "";
            }
            if (row.Type == RowType.Prisoner || row.Type == RowType.Troop)
                return "";
            return row.Market == null ? Dash : UiFormat.Money(row.Market.Value);
        }

        /// <summary>The Denari cell's tooltip: the units, the unit price and the sum; for a buy or sale of the price book, the
        /// limit it was judged by (<c>your max 60</c>).</summary>
        public static string Hint(PlanRow row, SheetWords? words = null)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            words ??= new SheetWords();
            if (row.Tavern != null)
                return row.Change > 0 ? UiFormat.PriceCell(row.Change, row.Tavern.UnitPrice, row.Tavern.UnitPrice, row.GoldDelta) : "";
            if (row.Troop != null)
                return row.Change > 0 ? UiFormat.PriceCell(row.Change, row.Troop.UnitPrice, row.Troop.UnitPrice, row.GoldDelta) : "";
            if (row.Prisoner != null)
            {
                var p = row.Prisoner;
                var parts = new List<string>();
                if (p.RansomCount > 0)
                    parts.Add(UiFormat.PriceCell(p.RansomCount, p.RansomValue, p.RansomValue, row.GoldDelta));
                if (p.DonateCount > 0)
                    parts.Add(UiFormat.Money(p.DonateCount) + " " + words.ToDungeon + " " + UiFormat.SignedInfluence(row.InfluenceDelta)
                              + " " + words.Influence);
                return string.Join(Sep, parts);
            }
            string cell = UiFormat.PriceCell(Math.Abs(row.Change), row.UnitPriceMin, row.UnitPriceMax, row.GoldDelta);
            if (cell.Length == 0)
                return "";
            if (row.Change > 0 && row.PriceBook?.FinalMaxBuy != null)
                cell += Sep + words.YourMax + " " + UiFormat.Money(row.PriceBook.FinalMaxBuy.Value);
            else if (row.Change < 0 && row.PriceBook?.FinalMinSell != null)
                cell += Sep + words.YourMin + " " + UiFormat.Money(row.PriceBook.FinalMinSell.Value);
            return cell;
        }
    }
}
