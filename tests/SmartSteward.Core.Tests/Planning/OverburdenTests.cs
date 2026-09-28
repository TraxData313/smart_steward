using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The overburden slowdown of the footer's weight table (PLAN step 20, RESEARCH §25): the game ADDS
/// −rate × over / capacity × (1 + perks) to the party's base speed — land 0.4 (base 4 × (200 / (200 + men))^0.4, perks Energetic
/// and Unburdened), sea 1.0 (base the fleet's (average + slowest) / 2, perk Veteran's Wisdom) — so the share of speed lost is the
/// penalty over the base.
/// </summary>
public class OverburdenTests
{
    [Fact]
    public void The_penalty_is_the_games_formula_on_the_whole_number_capacity()
    {
        Assert.Equal(0, Overburden.Penalty(1_000, 1_000, Overburden.LandRate, 0));
        Assert.Equal(0.4, Overburden.Penalty(2_000, 1_000, Overburden.LandRate, 0), 9);           // twice the capacity
        Assert.Equal(0.24, Overburden.Penalty(2_000, 1_000, Overburden.LandRate, -0.4), 9);       // both land perks
        Assert.Equal(0.4 * 1.0 / 999, Overburden.Penalty(1_000, 999.9, Overburden.LandRate, 0), 9); // (int) capacity
        Assert.Equal(0.5, Overburden.Penalty(1_500, 1_000, Overburden.SeaRate, 0), 9);
        Assert.Equal(0, Overburden.Penalty(10, 0, Overburden.LandRate, 0));
    }

    [Theory]
    [InlineData(0, 4.0)]
    [InlineData(104, 3.3834)]
    [InlineData(200, 3.0314)]
    public void The_land_base_speed_shrinks_with_the_party(int men, double speed)
    {
        Assert.Equal(speed, Overburden.LandBaseSpeed(men), 3);
    }

    private static Scenario Loaded(int weightNow, int capacity)
    {
        var s = new Scenario().Party(104).Loot("rags", LootGroup.Armour, held: 100, sell: 8, weight: 10);
        s.Snap.Carry = new CarryInfo { WeightNow = weightNow, CapacityLandNow = capacity, LandPerMember = 20 };
        return s;
    }

    [Fact]
    public void Within_capacity_there_is_no_slowdown()
    {
        var plan = Loaded(1_000, 2_000).Plan();
        Assert.Equal(0, plan.Totals.Carry.LandSlowdown);
        Assert.Equal("none", SuggestionSheet.Of(plan).Weights[0].SlowdownText);
    }

    [Fact]
    public void Over_capacity_the_share_of_speed_lost_is_the_penalty_over_the_base()
    {
        var plan = Loaded(4_000, 2_000).Plan(); // the loot is not for sale (SellLoot off): twice the capacity
        var c = plan.Totals.Carry;
        Assert.Equal(0.4, c.LandSpeedLoss, 9);
        Assert.Equal(0.4 / Overburden.LandBaseSpeed(104), c.LandSlowdown, 9);
        var land = SuggestionSheet.Of(plan).Weights[0];
        Assert.Equal("–12%", land.SlowdownText);
        Assert.True(land.Over);
        Assert.Equal("–2,000", land.LeftText);
        Assert.Equal("2,000", land.CapacityText);
    }

    [Fact]
    public void Selling_the_load_takes_the_slowdown_away_and_the_table_shows_before_change_after()
    {
        var s = Loaded(2_500, 2_000);
        s.Settings.SellLoot = true;
        var plan = s.Plan();
        var land = SuggestionSheet.Of(plan).Weights[0];
        Assert.Equal(("2,500", "–1,000", "1,500"), (land.BeforeText, land.ChangeText, land.AfterText));
        Assert.Equal("500", land.LeftText);
        Assert.Equal("none", land.SlowdownText);
    }

    [Fact]
    public void The_land_perks_soften_it()
    {
        var s = Loaded(4_000, 2_000);
        s.Snap.Carry.OverburdenPerksLand = -0.4;
        Assert.Equal(0.24, s.Plan().Totals.Carry.LandSpeedLoss, 9);
    }

    [Fact]
    public void An_army_pools_its_loads_and_capacities_on_land()
    {
        var s = Loaded(2_500, 2_000);
        s.Snap.Party.Attached = new AttachedParties { Men = 50, Weight = 500, Capacity = 2_000 };
        var c = s.Plan().Totals.Carry;
        Assert.Equal(0, c.LandSpeedLoss); // 3,000 of 4,000 — the army is within its pooled capacity
    }

    [Fact]
    public void At_sea_the_fleets_speed_gives_the_share_and_without_it_the_points()
    {
        var s = Loaded(1_000, 3_000);
        s.Snap.Carry.HasShips = true;
        s.Snap.Carry.WeightAtSeaNow = 1_500;
        s.Snap.Carry.CapacitySeaNow = 1_000;
        s.Snap.Carry.FleetBaseSpeed = 5;
        var sea = SuggestionSheet.Of(s.Plan()).Weights[1];
        Assert.True(sea.IsSea);
        Assert.Equal(0.5, sea.SpeedLoss, 9);   // 1.0 × 500 / 1,000
        Assert.Equal("–10%", sea.SlowdownText); // of the fleet's 5

        s.Snap.Carry.FleetBaseSpeed = 0;
        Assert.Equal("–0.50 speed", SuggestionSheet.Of(s.Plan()).Weights[1].SlowdownText);
    }

    [Fact]
    public void A_small_slowdown_keeps_a_decimal()
    {
        Assert.Equal("–0.4%", SuggestionSheet.SlowdownText(0.004, 0.01, true));
        Assert.Equal("–0.1%", SuggestionSheet.SlowdownText(0.0001, 0.001, true));
        Assert.Equal("none", SuggestionSheet.SlowdownText(0, 0, true));
    }
}
