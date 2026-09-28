using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using Xunit;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The footer's weight line (round 3, Anton 2026.09.28: "Weight 1,000 +120 kg » 1,120 kg · capacity land 1,500 / sea
/// 1,000", the part over a capacity in red): the load and the capacity AFTER the deal, on land and at sea — the game's
/// numbers now plus what the deal moves at the vanilla formula's rates (RESEARCH §19).
/// </summary>
public class CarryTotalsTests
{
    /// <summary>The game's numbers now, no perks (a member 20, a mount 20, a pack animal 100 on land; a member 20 at sea).</summary>
    private static CarryInfo Carry(double weight, double land, bool ships = false, double seaWeight = 0, double sea = 0,
        bool forcedLabor = false, int healthyPrisoners = 0)
    {
        var carry = new CarryInfo
        {
            WeightNow = weight,
            CapacityLandNow = land,
            HasShips = ships,
            WeightAtSeaNow = seaWeight,
            CapacitySeaNow = sea,
            HealthyPrisoners = healthyPrisoners,
        };
        GameRules.SetCarryRates(carry, 0, 0, 0, forcedLabor);
        return carry;
    }

    /// <summary>10 men, no footmen: 20 grain to buy (2 per man), 10 mules to buy (the pack target), 3 surplus hunters to
    /// sell (no footman needs them).</summary>
    private static Scenario Trading()
    {
        var s = new Scenario().Party(10)
            .Food("grain", market: 100, buy: 10, weight: 1)
            .Pack("mule", market: 20, buy: 150)
            .Mount("hunter", "horse", held: 3, sell: 150);
        return s;
    }

    [Fact]
    public void The_land_line_follows_the_goods_the_pack_animals_and_the_mounts_of_the_deal()
    {
        var s = Trading();
        s.Snap.Carry = Carry(weight: 1_000, land: 1_500);
        var plan = s.Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);
        Assert.Equal(10, plan.Row("mounts:pack").Change);
        Assert.Equal(-3, plan.Row("mounts:riding").Change);

        var c = plan.Totals.Carry;
        Assert.True(c.Known);
        Assert.False(c.ShowSea);
        Assert.Equal(1_000, c.WeightNow);
        Assert.Equal(1_020, c.WeightAfter);                     // animals weigh nothing on land
        Assert.Equal(10, c.PackAnimalsChange);
        Assert.Equal(-3, c.MountsChange);
        Assert.Equal(1_500 + 10 * 100 - 3 * 20, c.CapacityLandAfter);
        Assert.Equal(0, c.OverLand);
        Assert.Equal(0, c.OverSea);
    }

    [Fact]
    public void Every_click_moves_the_line_and_the_part_over_the_capacity_shows()
    {
        var s = Trading();
        s.Snap.Carry = Carry(weight: 1_000, land: 1_010);
        var plan = s.Plan();
        plan.Decrease("mounts:pack", EditSize.All);             // no mules: no extra capacity
        plan.Reset("mounts:riding");
        var c = plan.Totals.Carry;
        Assert.Equal(1_020, c.WeightAfter);
        Assert.Equal(1_010 - 3 * 20, c.CapacityLandAfter);
        Assert.Equal(1_020 - 950, c.OverLand);                  // +70 over

        plan.Increase("mounts:pack");                           // one mule: +100 capacity
        c = plan.Totals.Carry;
        Assert.Equal(1_050, c.CapacityLandAfter);
        Assert.Equal(0, c.OverLand);
        Assert.Contains(PlanReport.Full(plan), line => line.Contains("load 1,000 -> 1,020, capacity land 1,010 -> 1,050"));
    }

    [Fact]
    public void At_sea_the_ships_carry_the_hires_add_capacity_and_the_horses_weigh()
    {
        var s = Trading();
        s.Snap.Carry = Carry(weight: 1_000, land: 1_500, ships: true, seaWeight: 1_150, sea: 1_000);
        foreach (var stack in s.Snap.Inventory.Concat(s.Snap.Market))
            stack.UnitWeightAtSea = stack.Kind switch
            {
                ItemKind.Mount => 50,
                ItemKind.PackAnimal => 30,
                _ => stack.UnitWeight,
            };
        s.Snap.Tavern = new TavernInfo
        {
            Mercenaries = new MercenaryOffer
            {
                TroopId = "merc", Name = "Riders", Available = 10, PricePerMan = 100, SeaWeightPerMan = 50,
                IsMounted = true, // riders bring their own horses: no footmen to mount
            },
        };
        var plan = s.Plan();
        Assert.Equal(5, plan.Increase("tavern:mercenaries", EditSize.Five).After);

        var c = plan.Totals.Carry;
        Assert.True(c.ShowSea);
        // at sea: +30 grain (the 5 riders eat too — the live re-plan), +10 mules × 30, −3 hunters × 50, +5 riders' horses × 50
        Assert.Equal(1_150 + 30 + 300 - 150 + 250, c.WeightAtSeaAfter);
        Assert.Equal(1_000 + 5 * 20, c.CapacitySeaAfter);       // ships fixed; animals add nothing at sea
        Assert.Equal(1_580 - 1_100, c.OverSea);
        Assert.Equal(1_500 + 1_000 - 60 + 5 * 20, c.CapacityLandAfter);
        Assert.Equal(0, c.OverLand);
        Assert.True(c.SeaLoadDiffers);
    }

    [Fact]
    public void Forced_labor_prisoners_carry_and_the_wounded_leave_first()
    {
        // 8 prisoners, 5 healthy: ransoming all 8 takes the 5 healthy ones' capacity.
        var s = new Scenario().Party(10).Prisoner("looter", 8, 20);
        s.Snap.Carry = Carry(weight: 100, land: 500, ships: true, seaWeight: 100, sea: 400, forcedLabor: true,
            healthyPrisoners: 5);
        var plan = s.Plan();
        Assert.Equal(-8, plan.Row("prisoner:looter").Change);
        var c = plan.Totals.Carry;
        Assert.Equal(500 - 5 * 20, c.CapacityLandAfter);
        Assert.Equal(400 - 5 * 20, c.CapacitySeaAfter);

        // Only 3 ransomed: the 3 wounded go first - the healthy five stay and so does their capacity.
        Assert.Equal(-3, plan.Increase("prisoner:looter", EditSize.Five).After);
        Assert.Equal(500, plan.Totals.Carry.CapacityLandAfter);

        // Without Forced Labor prisoners carry nothing.
        s.Snap.Carry = Carry(weight: 100, land: 500, healthyPrisoners: 5);
        Assert.Equal(500, s.Plan().Totals.Carry.CapacityLandAfter);
    }

    [Fact]
    public void Without_a_capacity_read_only_the_weight_change_is_known()
    {
        var plan = Trading().Plan();                            // CarryInfo left empty (a failed read)
        var c = plan.Totals.Carry;
        Assert.False(c.Known);
        Assert.False(c.ShowSea);
        Assert.Equal(0, c.OverLand);
        Assert.Equal(20, plan.Totals.WeightChange);
    }

    [Fact]
    public void The_capacity_never_falls_below_the_games_floor()
    {
        var s = new Scenario().Party(10).Mount("hunter", "horse", held: 3, sell: 150);
        s.Snap.Carry = Carry(weight: 0, land: 30);
        Assert.Equal(GameRules.CapacityBase, s.Plan().Totals.Carry.CapacityLandAfter);
    }

    [Theory]
    [InlineData(0, 0, 0, false, 20, 20, 100, 0)]
    [InlineData(0.1, 0, 0, false, 22, 20, 100, 0)]                  // Arenicos' Horses: +10% per member
    [InlineData(0, 0.5, 0, false, 20, 20, 150, 0)]                  // Beast Whisperer 0.1 + Deeper Sacks 0.2 + Arenicos' Mules 0.2
    [InlineData(0.1, 0.5, 0.3, true, 28.6, 26, 195, 26)]            // Caravan Master +30% on the whole land total
    public void The_rates_are_the_vanilla_formula_with_the_perks(double troops, double pack, double caravan, bool forcedLabor,
        double perMember, double perMount, double perPack, double perPrisoner)
    {
        var carry = new CarryInfo();
        GameRules.SetCarryRates(carry, troops, pack, caravan, forcedLabor);
        Assert.Equal(perMember, carry.LandPerMember, 6);
        Assert.Equal(perMount, carry.LandPerMount, 6);
        Assert.Equal(perPack, carry.LandPerPackAnimal, 6);
        Assert.Equal(perPrisoner, carry.LandPerPrisoner, 6);
        Assert.Equal(20, carry.SeaPerMember);                       // no perk at sea
        Assert.Equal(forcedLabor ? 20 : 0, carry.SeaPerPrisoner);
    }

    [Theory]
    [InlineData(1_120.4, "1,120")]
    [InlineData(1_120.5, "1,121")]
    [InlineData(0, "0")]
    public void Kilos_are_whole(double kg, string expected) => Assert.Equal(expected, UiFormat.Kg(kg));

    [Theory]
    [InlineData(120, "+120")]
    [InlineData(0.2, "+1")]                                         // any part of a kilo over is over
    [InlineData(119.0000000001, "+119")]                            // float noise is not a kilo
    public void Kilos_over_round_up(double kg, string expected) => Assert.Equal(expected, UiFormat.KgOver(kg));
}
