using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Helpers;
using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// Carries a plan out through the game's own paths (DESIGN §5, RESEARCH §9), in
    /// <see cref="StewardPlan.Transactions"/> order: donations and ransoms (the ransom gold funds the buys), the dismissals
    /// (step 16), every sale then every purchase in ONE headless trade, then the wanderers, then the mercenaries, then the
    /// recruits (step 16).
    /// </summary>
    /// <remarks>
    /// Every rule is checked again at the click — access, what is held and on offer, locks, the purse, the market's
    /// gold, each unit's live price against its row's limit, the dungeon's and the party's room, the companion
    /// slots. What no longer fits is skipped and logged; the rest goes through. Every transaction runs in its own
    /// try/catch. The trade batch is all-or-nothing on an error: the headless InventoryLogic moves the rosters at
    /// once but the gold only in DoneLogic, so a failure before DoneLogic resets the logic (vanilla's Cancel) and
    /// the party never keeps goods it did not pay for.
    /// </remarks>
    internal static class PlanExecutor
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ExecutionReport Execute(StewardPlan plan, GameVisit visit)
        {
            var hero = Hero.MainHero;
            var main = MobileParty.MainParty;
            var settlement = visit.Settlement;
            var report = new ExecutionReport(hero.Gold);
            var transactions = plan.Transactions;
            Log("start at " + settlement.Name + ": " + transactions.Count.ToString(Inv) + " transactions, gold "
                + hero.Gold.ToString("N0", Inv) + ", market gold " + (settlement.SettlementComponent?.Gold ?? 0).ToString("N0", Inv));

            if (main == null || main.CurrentSettlement != settlement)
            {
                report.Abort = "the party is no longer in " + settlement.Name;
                return report;
            }
            // Never while the encounter is not a quiet visit or the menu is not the settlement's own (PLAN step 24).
            string? busy = EncounterGuard.WhyBusy(settlement, checkMenu: true);
            if (busy != null)
            {
                EncounterGuard.LogAside("the executor at " + settlement.Name, busy);
                report.Abort = "the steward stands aside - " + busy;
                return report;
            }

            Donate(transactions.Where(t => t.Kind == TransactionKind.Donate).ToList(), report, settlement, main);
            Ransom(transactions.Where(t => t.Kind == TransactionKind.Ransom).ToList(), report, settlement, main);
            foreach (var tx in transactions.Where(t => t.Kind == TransactionKind.Dismiss))
                Dismiss(report.Add(tx), main);
            Trade(plan, transactions.Where(t => t.IsItemTrade).ToList(), report, settlement, main);
            foreach (var tx in transactions.Where(t => t.Kind == TransactionKind.HireWanderer))
                HireWanderer(report.Add(tx), settlement, main);
            foreach (var tx in transactions.Where(t => t.Kind == TransactionKind.HireMercenaries))
                HireMercenaries(report.Add(tx), settlement, main);
            foreach (var tx in transactions.Where(t => t.Kind == TransactionKind.Recruit))
                Recruit(report.Add(tx), settlement, main);

            report.GoldAfter = hero.Gold;
            return report;
        }

        // ── Prisoners ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The vanilla donate screen, headless (<c>PartyScreenHelper.OpenScreenAsDonatePrisoners</c> +
        /// <c>DonatePrisonersDoneHandler</c>): a garrison is ensured, the men move from the party's prison roster to
        /// the settlement's (heroes then <c>EnterSettlementAction.ApplyForPrisoner</c>), and ONE
        /// <c>OnPrisonerDonatedToSettlement</c> for all of them grants the influence.
        /// </summary>
        private static void Donate(List<PlanTransaction> transactions, ExecutionReport report, Settlement settlement,
            MobileParty main)
        {
            if (transactions.Count == 0)
                return;
            var outcomes = transactions.Select(report.Add).ToList();
            int room;
            try
            {
                if (!SnapshotBuilder.DonateAllowedNow(settlement))
                {
                    foreach (var o in outcomes) o.Stop(SkipReason.NotAllowedHere);
                    return;
                }
                room = SnapshotBuilder.DungeonRoom(settlement);
                if (settlement.Town.GarrisonParty == null)
                    settlement.AddGarrisonParty();
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "preparing the donation", ex);
                foreach (var o in outcomes) o.Stop(SkipReason.Error, ex.Message);
                return;
            }

            var locks = SnapshotBuilder.PrisonerLocks();
            var donated = new FlattenedTroopRoster();
            bool any = false;
            foreach (var o in outcomes)
            {
                try
                {
                    var tx = o.Transaction;
                    if (!FindTroop(main.PrisonRoster, tx.TroopId, out var element))
                    {
                        o.Stop(SkipReason.NotHeld);
                        continue;
                    }
                    if (locks.Contains(element.Character.StringId))
                    {
                        o.Stop(SkipReason.Locked);
                        continue;
                    }
                    int n = Math.Min(tx.Count, element.Number);
                    if (n < tx.Count) o.Stop(SkipReason.NotHeld, "only " + element.Number.ToString(Inv) + " held");
                    if (n > room)
                    {
                        n = room;
                        o.Stop(SkipReason.DungeonFull, "room " + room.ToString(Inv));
                    }
                    if (n <= 0)
                        continue;
                    var troop = element.Character;
                    int wounded = GameRules.WoundedToMove(n, element.WoundedNumber);
                    main.PrisonRoster.AddToCounts(troop, -n, false, -wounded);
                    settlement.Party.PrisonRoster.AddToCounts(troop, n, false, wounded);
                    if (troop.IsHero)
                        EnterSettlementAction.ApplyForPrisoner(troop.HeroObject, settlement);
                    donated.Add(troop, n, wounded);
                    any = true;
                    room -= n;
                    o.AddUnits(n, 0);
                }
                catch (Exception ex)
                {
                    ModLog.Error("execute", "donating " + o.Transaction.TroopId, ex);
                    o.Stop(SkipReason.Error, ex.Message);
                }
            }
            if (!any)
                return;
            try
            {
                CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(main, donated, settlement);
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "the donation event (influence)", ex);
            }
        }

        /// <summary>The ransom broker, one call per troop: <c>SellPrisonersAction.ApplyForSelectedPrisoners(MainParty,
        /// null, roster)</c> — vanilla's "Ransom your prisoners" (a lord is freed and paid at his hero value).</summary>
        private static void Ransom(List<PlanTransaction> transactions, ExecutionReport report, Settlement settlement,
            MobileParty main)
        {
            if (transactions.Count == 0)
                return;
            var outcomes = transactions.Select(report.Add).ToList();
            if (!settlement.IsTown || !SnapshotBuilder.CanAccess(settlement, "tavern"))
            {
                foreach (var o in outcomes) o.Stop(SkipReason.NotAllowedHere, "no access to the tavern district");
                return;
            }
            var locks = SnapshotBuilder.PrisonerLocks();
            var hero = Hero.MainHero;
            foreach (var o in outcomes)
            {
                try
                {
                    var tx = o.Transaction;
                    if (!FindTroop(main.PrisonRoster, tx.TroopId, out var element))
                    {
                        o.Stop(SkipReason.NotHeld);
                        continue;
                    }
                    if (locks.Contains(element.Character.StringId))
                    {
                        o.Stop(SkipReason.Locked);
                        continue;
                    }
                    int n = Math.Min(tx.Count, element.Number);
                    if (n < tx.Count) o.Stop(SkipReason.NotHeld, "only " + element.Number.ToString(Inv) + " held");
                    if (n <= 0)
                        continue;
                    var roster = TroopRoster.CreateDummyTroopRoster();
                    roster.AddToCounts(element.Character, n, false, GameRules.WoundedToMove(n, element.WoundedNumber));
                    int before = hero.Gold;
                    SellPrisonersAction.ApplyForSelectedPrisoners(PartyBase.MainParty, null, roster);
                    o.AddUnits(n, hero.Gold - before);
                }
                catch (Exception ex)
                {
                    ModLog.Error("execute", "ransoming " + o.Transaction.TroopId, ex);
                    o.Stop(SkipReason.Error, ex.Message);
                }
            }
        }

        // ── Items ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every sale then every purchase through ONE headless <see cref="InventoryLogic"/> — the trade screen's own
        /// logic without the screen (RESEARCH §9), one <c>DoneLogic</c>: the live unit-by-unit price walk, both
        /// purses, Trickle Down and <c>OnPlayerInventoryExchange</c> (Trade XP, quests) as in vanilla.
        /// </summary>
        private static void Trade(StewardPlan plan, List<PlanTransaction> transactions, ExecutionReport report,
            Settlement settlement, MobileParty main)
        {
            if (transactions.Count == 0)
                return;
            var outcomes = transactions.Select(report.Add).ToList();
            if (!SnapshotBuilder.CanTradeNow(settlement))
            {
                foreach (var o in outcomes) o.Stop(SkipReason.NotAllowedHere, "no trade access");
                return;
            }

            var hero = Hero.MainHero;
            var component = settlement.SettlementComponent;
            int goldBefore = hero.Gold, merchantBefore = component.Gold;
            InventoryLogic logic;
            try
            {
                // As InventoryScreenHelper.OpenScreenAsTrade builds it for the trade screen.
                logic = new InventoryLogic(component.Owner);
                logic.TotalAmountChange = OnTotalAmountChange; // invoked without a null check (RESEARCH gotcha 12)
                logic.Initialize(settlement.ItemRoster, PartyBase.MainParty.ItemRoster, PartyBase.MainParty.MemberRoster,
                    isTrading: true, isSpecialActionsPermitted: true, CharacterObject.PlayerCharacter,
                    InventoryScreenHelper.InventoryCategoryType.None, MarketDataOf(settlement), useBasePrices: false,
                    InventoryScreenHelper.InventoryMode.Trade);
                // AFTER Initialize, which installs a FakeInventoryListener (gold 0) — vanilla's order.
                logic.SetInventoryListener(new StewardMerchantListener(component));
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "opening the headless trade", ex);
                foreach (var o in outcomes) o.Stop(SkipReason.Error, ex.Message);
                return;
            }

            var budget = new ExecutionBudget(hero.Gold, component.Gold);
            var locks = SnapshotBuilder.InventoryLocks();
            int moved = 0;
            bool broken = false;
            foreach (var o in outcomes)
            {
                try
                {
                    moved += TradeOne(plan, logic, o, budget, settlement, main, locks);
                }
                catch (Exception ex)
                {
                    ModLog.Error("execute", "trading " + o.Transaction.StackKey, ex);
                    o.Stop(SkipReason.Error, ex.Message);
                    broken = true;
                    break;
                }
            }

            if (broken)
            {
                ResetTrade(logic, outcomes, "an error in this trade batch - see the ERROR line");
                return;
            }
            if (moved == 0)
                return;

            bool done;
            try
            {
                done = logic.DoneLogic();
            }
            catch (Exception ex)
            {
                // The goods moved; the gold may have moved in part. Nothing safe to undo — say exactly what happened,
                // and mark every moved trade so neither the window's line nor the autonomous report calls it a success.
                ModLog.Error("execute", "DoneLogic threw - the trade may be half-applied (gold " + goldBefore.ToString(Inv)
                    + " -> " + hero.Gold.ToString(Inv) + ", market gold " + merchantBefore.ToString(Inv) + " -> "
                    + component.Gold.ToString(Inv) + ")", ex);
                foreach (var o in outcomes)
                    if (o.Done > 0)
                        o.Stop(SkipReason.Error, "the game's trade logic failed after the goods moved - check gold and goods");
                return;
            }
            if (!done)
            {
                ResetTrade(logic, outcomes, "the game refused the deal (DoneLogic)");
                return;
            }
            Log("trade done: gold " + goldBefore.ToString("N0", Inv) + " -> " + hero.Gold.ToString("N0", Inv) + " (the walk said "
                + budget.Purse.ToString("N0", Inv) + "), market gold " + merchantBefore.ToString("N0", Inv) + " -> "
                + component.Gold.ToString("N0", Inv) + ", " + moved.ToString(Inv) + " units");
        }

        /// <summary>One sale or purchase, unit by unit: each unit's price is the one the logic will charge
        /// (<c>InventoryLogic.GetItemPrice</c>), checked against the purse, the market's gold and the row's limit.</summary>
        private static int TradeOne(StewardPlan plan, InventoryLogic logic, TransactionOutcome o, ExecutionBudget budget,
            Settlement settlement, MobileParty main, HashSet<string> locks)
        {
            var tx = o.Transaction;
            var direction = ExecutionBudget.DirectionOf(tx);
            bool selling = direction == TradeDirection.Sell;
            var roster = selling ? main.ItemRoster : settlement.ItemRoster;
            var from = selling ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory;
            var to = selling ? InventoryLogic.InventorySide.OtherInventory : InventoryLogic.InventorySide.PlayerInventory;
            if (!FindElement(roster, tx.StackKey, out var element, out _))
            {
                o.Stop(selling ? SkipReason.NotHeld : SkipReason.NotOnOffer);
                return 0;
            }
            // The headless InventoryLogic knows nothing of locks (vanilla honours one only in the trade screen's
            // "transfer all" — RESEARCH §6), so the lock is ours to check: it stops the sales it guards (armour and
            // weapons; food and animals only with LocksProtectFoodAndHorses) and nothing else.
            bool lockedNow = selling && locks.Contains(GameRules.LockId(element.Item.StringId, element.ItemModifier?.StringId));
            if (ExecutionBudget.StoppedByLock(tx, lockedNow))
            {
                o.Stop(SkipReason.Locked);
                return 0;
            }
            if (lockedNow)
                Log("selling " + tx.StackKey + " although locked - locks guard food and horses only with LocksProtectFoodAndHorses");
            int? limit = ExecutionBudget.PriceLimitOf(plan, tx);
            int floor = selling ? 0 : ExecutionBudget.FloorOf(plan, tx); // the autonomous steward's purse floor
            int moved = 0;
            for (int i = 0; i < tx.Count; i++)
            {
                int have = AmountOf(roster, element);
                if (have <= 0)
                {
                    o.Stop(selling ? SkipReason.NotHeld : SkipReason.NotOnOffer);
                    break;
                }
                int price = logic.GetItemPrice(element, isBuying: !selling);
                var why = budget.Check(direction, price, limit, floor);
                if (why != SkipReason.None)
                {
                    o.Stop(why, "next unit at " + price.ToString(Inv) + (limit == null ? "" : ", limit " + limit.Value.ToString(Inv))
                                + (why == SkipReason.BelowFloor ? ", floor " + floor.ToString(Inv) + ", purse " + budget.Purse.ToString(Inv) : ""));
                    break;
                }
                logic.AddTransferCommand(TransferCommand.Transfer(1, from, to, new ItemRosterElement(element, 1),
                    EquipmentIndex.None, EquipmentIndex.None, CharacterObject.PlayerCharacter));
                if (AmountOf(roster, element) != have - 1)
                {
                    o.Stop(SkipReason.GameRefused, "the transfer did not move");
                    break;
                }
                budget.Record(direction, price);
                o.AddUnit(price);
                moved++;
            }
            return moved;
        }

        private static void ResetTrade(InventoryLogic logic, List<TransactionOutcome> outcomes, string why)
        {
            try
            {
                logic.Reset(fromCancel: true); // vanilla's Cancel: both rosters back as they were, debt 0
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "resetting the trade", ex);
            }
            foreach (var o in outcomes)
                o.RollBack(why);
            Log("trade rolled back: " + why);
        }

        private static void OnTotalAmountChange(int newTotalAmount)
        {
        }

        private static IMarketData MarketDataOf(Settlement settlement) =>
            settlement.IsVillage ? settlement.Village.MarketData : (IMarketData)settlement.Town.MarketData;

        // ── Tavern ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Vanilla's hire, no dialogue (<c>conversation_companion_hire_on_consequence</c>, as ImmersiveAI
        /// does it): pay the LIVE price, AddCompanionAction, AddHeroToPartyAction, SetHasMet.</summary>
        private static void HireWanderer(TransactionOutcome o, Settlement settlement, MobileParty main)
        {
            try
            {
                var tx = o.Transaction;
                var wanderer = settlement.HeroesWithoutParty.FirstOrDefault(h => h != null && h.StringId == tx.HeroId);
                if (!SnapshotBuilder.IsWandererForHire(wanderer, settlement))
                {
                    o.Stop(SkipReason.NotOnOffer);
                    return;
                }
                if (!SnapshotBuilder.CanAccess(settlement, "tavern"))
                {
                    o.Stop(SkipReason.NotAllowedHere);
                    return;
                }
                var hero = Hero.MainHero;
                int price = Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(wanderer);
                var block = ExecutionBudget.WandererBlock(hero.Gold, price, SnapshotBuilder.CompanionSlotsFree());
                if (block != SkipReason.None)
                {
                    o.Stop(block, "price " + price.ToString(Inv) + ", gold " + hero.Gold.ToString(Inv));
                    return;
                }
                GiveGoldAction.ApplyBetweenCharacters(hero, wanderer, price);
                AddCompanionAction.Apply(Clan.PlayerClan, wanderer);
                AddHeroToPartyAction.Apply(wanderer, main);
                wanderer!.SetHasMet();
                o.AddUnit(price);
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "hiring wanderer " + o.Transaction.HeroId, ex);
                o.Stop(SkipReason.Error, ex.Message);
            }
        }

        /// <summary>Mirrors <c>RecruitmentCampaignBehavior.BuyMercenaries</c> (the tavern dialogue's complete path —
        /// the menu one skips the recruit event): count off the band, add to the roster, pay, OnUnitRecruited.</summary>
        private static void HireMercenaries(TransactionOutcome o, Settlement settlement, MobileParty main)
        {
            try
            {
                var tx = o.Transaction;
                var data = settlement.IsTown
                    ? Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>()?.GetMercenaryData(settlement.Town)
                    : null;
                if (data == null || !data.HasAvailableMercenary() || data.TroopType.StringId != tx.TroopId)
                {
                    o.Stop(SkipReason.NotOnOffer);
                    return;
                }
                if (!SnapshotBuilder.CanAccess(settlement, "tavern"))
                {
                    o.Stop(SkipReason.NotAllowedHere);
                    return;
                }
                var hero = Hero.MainHero;
                var troop = data.TroopType;
                int price = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, hero).RoundedResultNumber;
                int n = ExecutionBudget.MercenaryCount(tx.Count, data.Number, hero.Gold, price, out var reason);
                if (reason != SkipReason.None)
                    o.Stop(reason, "price " + price.ToString(Inv) + ", on offer " + data.Number.ToString(Inv));
                if (n <= 0)
                    return;
                data.ChangeMercenaryCount(-n);
                main.AddElementToMemberRoster(troop, n);
                GiveGoldAction.ApplyBetweenCharacters(hero, null, n * price);
                CampaignEventDispatcher.Instance.OnUnitRecruited(troop, n);
                o.AddUnits(n, n * price);
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "hiring mercenaries " + o.Transaction.TroopId, ex);
                o.Stop(SkipReason.Error, ex.Message);
            }
        }

        // ── Troops (step 16) ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Vanilla's party screen without the screen (RESEARCH §21): the normal party screen moves dismissed men from the
        /// party's LIVE roster to a dummy one — the WOUNDED FIRST (<c>PartyVM.OnTransferTroop</c> with
        /// <c>TransferHealthiesGetWoundedsFirst</c> false), the stack's XP left with the men who stay — and its done handler
        /// drops the dummy roster: no gold, no event. So: <c>MemberRoster.AddToCounts(troop, −n, false, −wounded)</c>. A hero
        /// or a quest-bound troop (<c>IsNotTransferableInPartyScreen</c>) is never let go.
        /// </summary>
        private static void Dismiss(TransactionOutcome o, MobileParty main)
        {
            try
            {
                var tx = o.Transaction;
                if (!FindTroop(main.MemberRoster, tx.TroopId, out var element) || element.Character.IsHero)
                {
                    o.Stop(SkipReason.NotHeld);
                    return;
                }
                var troop = element.Character;
                if (troop.IsNotTransferableInPartyScreen)
                {
                    o.Stop(SkipReason.GameRefused, "the party screen will not let this troop go (a quest)");
                    return;
                }
                int n = ExecutionBudget.DismissCount(tx.Count, element.Number, out var reason);
                if (reason != SkipReason.None)
                    o.Stop(reason, "only " + element.Number.ToString(Inv) + " in the party");
                if (n <= 0)
                    return;
                int wounded = GameRules.WoundedToMove(n, element.WoundedNumber);
                main.MemberRoster.AddToCounts(troop, -n, false, -wounded);
                o.AddUnits(n, 0);
                Log("dismissed " + n.ToString(Inv) + " " + troop.StringId + " (" + wounded.ToString(Inv) + " wounded)");
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "dismissing " + o.Transaction.TroopId, ex);
                o.Stop(SkipReason.Error, ex.Message);
            }
        }

        /// <summary>
        /// Vanilla's recruit screen without the screen (<c>RecruitmentVM.OnDone</c>, RESEARCH §21), re-checked at the click:
        /// the recruit gate, the slots the notables still open to the player (<see cref="SnapshotBuilder.VolunteerSlots"/>),
        /// the LIVE price per man and the purse (vanilla: the cart's total ≤ the gold held). Per man: the notable's slot
        /// emptied (<c>VolunteerTypes[index] = null</c>), <c>MemberRoster.AddToCounts(troop, 1)</c>,
        /// <c>OnUnitRecruited(troop, 1)</c> (Leadership XP, Famous Commander, statistics); then the gold, once
        /// (<c>GiveGoldAction</c> to nobody — vanilla's recruits pay no notable). The party size limit is no reason.
        /// </summary>
        private static void Recruit(TransactionOutcome o, Settlement settlement, MobileParty main)
        {
            try
            {
                var tx = o.Transaction;
                if (!SnapshotBuilder.CanRecruitNow(settlement))
                {
                    o.Stop(SkipReason.NotAllowedHere);
                    return;
                }
                var troop = TaleWorlds.ObjectSystem.MBObjectManager.Instance?.GetObject<CharacterObject>(tx.TroopId);
                if (troop == null)
                {
                    o.Stop(SkipReason.NotOnOffer, "unknown troop");
                    return;
                }
                var hero = Hero.MainHero;
                var slots = SnapshotBuilder.VolunteerSlots(settlement, hero, troop);
                int price = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, hero).RoundedResultNumber;
                int n = ExecutionBudget.RecruitCount(tx.Count, slots.Count, hero.Gold, price, out var reason);
                if (reason != SkipReason.None)
                    o.Stop(reason, "price " + price.ToString(Inv) + ", open slots " + slots.Count.ToString(Inv)
                                   + ", gold " + hero.Gold.ToString(Inv));
                int done = 0;
                try
                {
                    for (int i = 0; i < n; i++)
                    {
                        var slot = slots[i];
                        slot.Notable.VolunteerTypes[slot.Index] = null;
                        main.MemberRoster.AddToCounts(troop, 1);
                        done++;
                        CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
                    }
                }
                finally
                {
                    // the men who joined are paid for, even if a later one failed
                    if (done > 0)
                        GiveGoldAction.ApplyBetweenCharacters(hero, null, done * price);
                    o.AddUnits(done, done * price);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "recruiting " + o.Transaction.TroopId, ex);
                o.Stop(SkipReason.Error, ex.Message);
            }
        }

        // ── Lookups ──────────────────────────────────────────────────────────────────────────────────────

        private static bool FindTroop(TroopRoster roster, string? troopId, out TroopRosterElement found)
        {
            foreach (var element in roster.GetTroopRoster())
                if (element.Character != null && element.Number > 0 && element.Character.StringId == troopId)
                {
                    found = element;
                    return true;
                }
            found = default;
            return false;
        }

        /// <summary>The live roster element behind a stack key (item + modifier).</summary>
        private static bool FindElement(ItemRoster roster, string? stackKey, out EquipmentElement element, out int amount)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                var e = roster.GetElementCopyAtIndex(i);
                var item = e.EquipmentElement.Item;
                if (item != null && GameRules.StackKey(item.StringId, e.EquipmentElement.ItemModifier?.StringId) == stackKey)
                {
                    element = e.EquipmentElement;
                    amount = e.Amount;
                    return true;
                }
            }
            element = default;
            amount = 0;
            return false;
        }

        private static int AmountOf(ItemRoster roster, EquipmentElement element)
        {
            int index = roster.FindIndexOfElement(element);
            return index < 0 ? 0 : roster.GetElementCopyAtIndex(index).Amount;
        }

        private static void Log(string message) => ModLog.Info("execute", message);
    }
}
