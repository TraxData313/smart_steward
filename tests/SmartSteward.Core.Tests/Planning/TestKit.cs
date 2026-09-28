using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// A price oracle with a fixed base buy/sell price per stack. <see cref="Slope"/> > 0 emulates a town's
/// walk (the price moves by Slope × base per unit of category store value moved: buying raises it,
/// selling lowers it); 0 = a village's flat prices.
/// </summary>
internal sealed class FakeOracle : IPriceOracle
{
    private readonly Dictionary<string, (int Buy, int Sell)> _prices = new(StringComparer.Ordinal);

    public double Slope { get; set; }
    public List<(string Key, bool Selling, int Delta)> Calls { get; } = new();

    public void Set(string key, int buy, int sell) => _prices[key] = (buy, sell);

    public int GetPrice(string stackKey, bool isSelling, int categoryStoreValueDelta)
    {
        Calls.Add((stackKey, isSelling, categoryStoreValueDelta));
        var (buy, sell) = _prices[stackKey];
        double factor = 1 - categoryStoreValueDelta * Slope;
        double raw = Math.Round((isSelling ? sell : buy) * factor, 6);
        return Math.Max(1, (int)(isSelling ? Math.Floor(raw) : Math.Ceiling(raw)));
    }
}

/// <summary>Builds a snapshot, settings and oracle together. Defaults: a town, rich party and market, 10
/// members, no footmen — so each test switches on only what it looks at.
/// <para>The settings are the defaults EXCEPT the round-4 changes that would change what the old tests look at
/// (<see cref="LegacyDefaults"/>): the activation thresholds are 0 (every job acts, whatever the purse), WarMountsToKeep is 0
/// and food uses the old 1.2 / 0.8 multipliers — the defaults every test before step 20 was written against; and (round 5)
/// the goals do not keep the purse floors, as the player's hand never did before step 22.
/// <c>JobThresholdTests</c> and <c>FoodMultiplierTests</c> test the real defaults.</para></summary>
internal sealed class Scenario
{
    /// <summary>The defaults, with the activation thresholds at 0 and no war horses kept (the pre-round-4 defaults).</summary>
    public static StewardSettings LegacyDefaults() => new()
    {
        FoodMinDenari = 0,
        PackAnimalsMinDenari = 0,
        MountsMinDenari = 0,
        WarHorsesMinDenari = 0,
        WarMountsToKeep = 0,
        // Food shared the horses' 1.2 / 0.8 until round 4 (now 2.0 / 0.5): the food tests keep the old limits.
        FoodBuyPriceMultiplier = 1.2,
        FoodSellPriceMultiplier = 0.8,
        // Until round 5 the player's hand on a food or horse row never answered to the floors (red flags only); now a goal
        // does by default (ManualGoalsKeepPurseFloor). The old tests keep the old way; GoalPlanTests test the real default.
        ManualGoalsKeepPurseFloor = false,
    };

    public StewardSnapshot Snap { get; } = new()
    {
        SettlementKind = SettlementKind.Town,
        PlayerGold = 100_000,
        MarketGold = 100_000,
        Party = new PartyInfo
        {
            Members = 10,
            Footmen = 0,
            PartySizeLimit = 100,
            CompanionSlotsFree = 3,
            DailyFoodUse = 0.5,
        },
        Prison = new PrisonInfo { CanRansom = true },
    };

    public StewardSettings Settings { get; } = LegacyDefaults();
    public FakeOracle Oracle { get; } = new();

    public StewardPlan Plan() => StewardPlanner.Plan(Snap, Settings, Oracle);

    public Scenario Village()
    {
        Snap.SettlementKind = SettlementKind.Village;
        return this;
    }

    public Scenario Gold(int gold, int? marketGold = null)
    {
        Snap.PlayerGold = gold;
        if (marketGold != null) Snap.MarketGold = marketGold.Value;
        return this;
    }

    public Scenario Party(int members, int footmen = 0)
    {
        Snap.Party.Members = members;
        Snap.Party.Footmen = footmen;
        VanillaDailyFood();
        return this;
    }

    /// <summary>The party eats at vanilla's rate — (members + prisoners/2) / 20 a day — so the food goal in days
    /// (FoodDays 40) keeps 2 food per eater, like the old FoodPerMan 2.0. A test that wants another rate sets
    /// <c>Snap.Party.DailyFoodUse</c> after building.</summary>
    private void VanillaDailyFood() =>
        Snap.Party.DailyFoodUse = PlanTotals.GameEaters(Snap.Party.Members, Snap.Prisoners.Sum(p => p.Count)) / 20.0;

    /// <summary>A food item. Averages default to the prices (so the auto-filled book allows buying at up to
    /// 1.2 × buy and selling at down to 0.8 × sell); pass <paramref name="noAverage"/> to leave them out.</summary>
    public Scenario Food(string id, int held = 0, int market = 0, int buy = 10, int sell = 8,
        double weight = 1, bool locked = false, bool noAverage = false, string? category = null)
    {
        Item(id, id, null, ItemKind.Food, category ?? id, held, market, buy, sell, weight, locked);
        if (!noAverage) Snap.AveragePrices[id] = new AveragePrices(buy, sell);
        return this;
    }

    /// <summary>A pack animal. <paramref name="modifier"/> with its <paramref name="priceFactor"/> (the game's
    /// <c>ItemModifier.PriceMultiplier</c>): vanilla's lame horse is 0.1 — pass <see cref="Lame"/>; a modifier without a factor
    /// is a neutral one (1.0).</summary>
    public Scenario Pack(string id, int held = 0, int market = 0, int buy = 150, int sell = 70,
        string? modifier = null, bool locked = false, bool noAverage = false, double priceFactor = 1.0)
    {
        Item(id, id, modifier, ItemKind.PackAnimal, "sumpter_horse", held, market, buy, sell, 0, locked, priceFactor: priceFactor);
        Average(id, modifier, buy, sell, noAverage, priceFactor);
        return this;
    }

    /// <summary>The price factor of vanilla's "Lame" horse modifier (<c>lame_horse</c>, RESEARCH §22).</summary>
    public const double Lame = 0.1;

    /// <summary>A riding animal of a category (horse, war_horse, noble_horse…). War-horse-group items get no
    /// average unless asked (AutoFillWarMountPrices is off by default anyway).</summary>
    public Scenario Mount(string id, string category, int held = 0, int market = 0, int buy = 300, int sell = 150,
        string? modifier = null, bool locked = false, bool noAverage = false, double priceFactor = 1.0)
    {
        Item(id, id, modifier, ItemKind.Mount, category, held, market, buy, sell, 0, locked, priceFactor: priceFactor);
        Average(id, modifier, buy, sell, noAverage, priceFactor);
        return this;
    }

    /// <summary>The price book's placeholder is the PLAIN item's average (the game's, per item): a modified stack sets it
    /// only when the plain one did not, as the plain equivalent of its own prices; a plain stack always sets it.</summary>
    private void Average(string id, string? modifier, int buy, int sell, bool noAverage, double priceFactor)
    {
        if (noAverage)
            return;
        if (modifier == null)
            Snap.AveragePrices[id] = new AveragePrices(buy, sell);
        else if (!Snap.AveragePrices.ContainsKey(id))
            Snap.AveragePrices[id] = new AveragePrices((int)Math.Round(buy / priceFactor), (int)Math.Round(sell / priceFactor));
    }

    public Scenario Loot(string id, LootGroup group, int held, int sell, int? value = null, double weight = 5,
        bool locked = false, string? category = null)
    {
        Item(id, id, null, ItemKind.Equipment, category ?? group.ToString(), held, 0, sell * 3, sell, weight, locked,
            value ?? sell * 3);
        Snap.Inventory[^1].LootGroup = group;
        return this;
    }

    /// <summary>A trade good that is not food (round 4: the Other goods line) — 10 kg a unit like vanilla's goods.</summary>
    public Scenario Goods(string id, int held, int sell, double weight = 10, bool locked = false, int? value = null)
    {
        Item(id, id, null, ItemKind.Goods, id, held, 0, sell * 2, sell, weight, locked, value ?? sell * 2);
        Snap.Inventory[^1].LootGroup = LootGroup.OtherGoods;
        return this;
    }

    public Scenario Prisoner(string troop, int count, int ransom, bool hero = false, bool locked = false,
        double influence = 1.0)
    {
        Snap.Prisoners.Add(new PrisonerStack
        {
            TroopId = troop,
            Name = troop,
            Count = count,
            RansomValue = ransom,
            IsHero = hero,
            IsLocked = locked,
            InfluencePerMan = influence,
        });
        VanillaDailyFood();
        return this;
    }

    /// <summary>A troop type of the troops section (step 16): <paramref name="inParty"/> men in the party (dismissable unless
    /// said), <paramref name="onOffer"/> volunteers the notables offer the player at <paramref name="price"/> a man.</summary>
    public Scenario Troop(string id, int inParty = 0, int onOffer = 0, int price = 20, bool mounted = false, int wounded = 0,
        bool canDismiss = true, int wage = 2, double seaWeight = 0, int tier = 0, string? name = null)
    {
        Snap.Troops.Add(new TroopStack
        {
            TroopId = id,
            Name = name ?? id,
            Tier = tier,
            InParty = inParty,
            Wounded = wounded,
            CanDismiss = canDismiss && inParty > 0,
            OnOffer = onOffer,
            PricePerMan = price,
            WagePerMan = wage,
            IsMounted = mounted,
            SeaWeightPerMan = seaWeight,
        });
        return this;
    }

    /// <summary>A town where every job has something to do.</summary>
    public static Scenario BusyTown()
    {
        var s = new Scenario().Party(20, footmen: 10).Gold(30_000, marketGold: 5_000)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Food("fish", market: 50, buy: 14)
            .Food("cheese", held: 2, market: 10, buy: 25)
            .Pack("mule", held: 3, market: 20, buy: 140)
            .Pack("sumpter_horse", market: 10, buy: 160)
            .Mount("hunter", "horse", held: 2, market: 30, buy: 210)
            .Mount("aserai_horse", "horse", market: 30, buy: 260)
            .Mount("charger", "war_horse", held: 1, market: 6, buy: 1500)
            .Loot("rags", LootGroup.Armour, held: 30, sell: 8)
            .Loot("helmet", LootGroup.Armour, held: 4, sell: 60, weight: 3)
            .Loot("spear", LootGroup.MeleeWeapons, held: 6, sell: 25)
            .Prisoner("looter", 8, 20)
            .Prisoner("lord_x", 1, 3000, hero: true);
        s.Settings.SellLoot = true;
        s.Settings.WarMountsToKeep = 4;
        s.Oracle.Slope = 0.0001;
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers =
            {
                new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 },
                new WandererForHire { HeroId = "w2", Name = "Bea", HirePrice = 800, DailyWage = 12 },
            },
            Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Blades", Available = 8, PricePerMan = 100 },
        };
        return s;
    }

    private void Item(string key, string id, string? modifier, ItemKind kind, string category, int held, int market,
        int buy, int sell, double weight, bool locked, int? value = null, double priceFactor = 1.0)
    {
        string stackKey = key + (modifier ?? "");
        Oracle.Set(stackKey, buy, sell);
        ItemStack Stack(int count, bool isLocked) => new()
        {
            Key = stackKey,
            ItemId = id,
            Name = id,
            ModifierId = modifier,
            ModifierPriceFactor = modifier == null ? 1.0 : priceFactor,
            Kind = kind,
            CategoryId = category,
            Count = count,
            IsLocked = isLocked,
            UnitWeight = weight,
            UnitValue = value ?? buy,
            // The game walks a town's prices by ItemObject.Value — the PLAIN item's worth, whatever the modifier.
            StoreValueStep = value ?? (modifier == null ? buy : (int)Math.Round(buy / priceFactor)),
        };
        if (held > 0) Snap.Inventory.Add(Stack(held, locked));
        if (market > 0) Snap.Market.Add(Stack(market, false));
    }
}

internal static class PlanExtensions
{
    public static PlanRow Row(this StewardPlan plan, string id) =>
        plan.FindRow(id) ?? throw new Xunit.Sdk.XunitException(
            $"no row '{id}'; rows: {string.Join(", ", plan.Rows.Select(r => r.Id))}");

    /// <summary>Every row's quantity, prices and tallies, and the purse — equal strings = equal plans.</summary>
    public static string Describe(this StewardPlan plan) =>
        string.Join("\n", plan.Rows.Select(r =>
            $"{r.Id} mine={r.Mine} change={r.Change} gold={r.GoldDelta} min={r.UnitPriceMin} max={r.UnitPriceMax} " +
            $"w={r.WeightDelta} inf={r.InfluenceDelta} " +
            string.Join(",", r.Tallies.Select(t => $"{t.Stack.Key}:{t.Direction}:{t.Count}:{t.Gold}:{t.MinPrice}-{t.MaxPrice}")) +
            " " + string.Join(",", r.Breakdown.Select(l => $"{l.StackKey}:{l.Mine}:{l.Change}:{l.GoldDelta}"))))
        + $"\ngold={plan.Totals.GoldAfter} earned={plan.Totals.Earned} spent={plan.Totals.Spent} " +
          $"sales={plan.Totals.MarketSales} food={plan.Totals.FoodUnitsAfter} days={plan.Totals.FoodDaysAfter} " +
          $"flags={plan.Totals.BelowMinGoldAfterDeal}{plan.Totals.BelowMinGoldForHorses}{plan.Totals.CannotAfford}";

    /// <summary>Units of one stack a row moved (+ bought, − sold).</summary>
    public static int Moved(this PlanRow row, string stackKey) =>
        row.Tallies.Where(t => t.Stack.Key == stackKey)
            .Sum(t => t.Direction == TradeDirection.Buy ? t.Count : -t.Count);
}
