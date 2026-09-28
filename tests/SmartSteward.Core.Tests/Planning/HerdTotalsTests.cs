using SmartSteward.Core.Planning;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The footer's herd line (PLAN step 18 — Anton 2026.09.28: "Horses 110 / 200 before the herd slows you — the game's real
/// herding threshold, red when over"). The rule as the game has it (RESEARCH §23, <c>DefaultPartySpeedCalculatingModel</c>):
/// herd = pack animals + livestock + the mounts no footman rides; it slows the party only when it outnumbers the men. The line
/// shows the horses against the most the party may have before that: men + the mounts footmen ride − livestock.
/// </summary>
public class HerdTotalsTests
{
    /// <summary>A party whose market is shut: nothing is traded, the herd is the party as it stands.</summary>
    private static Scenario Closed(int members, int footmen)
    {
        var s = new Scenario().Party(members, footmen);
        s.Snap.CanTrade = false;
        return s;
    }

    [Fact]
    public void Antons_example_100_footmen_110_horses_before_200()
    {
        var herd = Closed(100, 100).Mount("hunter", "horse", held: 110).Plan().Totals.Herd;
        Assert.Equal((110, 200), (herd.Horses, herd.Room));
        Assert.Equal(10, herd.Herd);            // the ten spare horses are herd …
        Assert.False(herd.SlowsParty);         // … far fewer than the 100 men
    }

    [Fact]
    public void The_threshold_is_exact_the_herd_may_equal_the_men_one_more_slows()
    {
        var s = Closed(10, 10).Mount("hunter", "horse", held: 10).Pack("mule", held: 10);
        var herd = s.Plan().Totals.Herd;
        Assert.Equal((20, 20, 10), (herd.Horses, herd.Room, herd.Herd));
        Assert.False(herd.SlowsParty);          // herd 10 = men 10: no penalty (the game's "herd − men ≤ 0")

        s.Snap.Inventory.Single(x => x.ItemId == "mule").Count = 11;
        herd = s.Plan().Totals.Herd;
        Assert.Equal((21, 20, 1), (herd.Horses, herd.Room, herd.Over));
        Assert.True(herd.SlowsParty);
    }

    [Theory]
    // men, footmen, mounts, pack, livestock → horses, room, slows
    [InlineData(10, 10, 3, 11, 0, 14, 13, true)]    // fewer mounts than footmen: every mount ridden, 11 pack > 10 men
    [InlineData(10, 10, 3, 10, 0, 13, 13, false)]
    [InlineData(10, 0, 10, 0, 0, 10, 10, false)]    // all cavalry: every mount in the inventory is herd
    [InlineData(10, 0, 11, 0, 0, 11, 10, true)]
    [InlineData(10, 0, 0, 6, 4, 6, 6, false)]       // livestock takes room too: 6 pack + 4 cows = 10 = men
    [InlineData(10, 0, 0, 7, 4, 7, 6, true)]
    [InlineData(2, 0, 0, 0, 5, 0, 0, true)]         // more cows than men: no room at all, already slowed
    public void Horses_against_the_room_is_herd_against_the_men(int men, int footmen, int mounts, int pack, int livestock,
        int horses, int room, bool slows)
    {
        var s = Closed(men, footmen);
        if (mounts > 0) s.Mount("hunter", "horse", held: mounts);
        if (pack > 0) s.Pack("mule", held: pack);
        s.Snap.Party.LivestockAnimals = livestock;
        var herd = s.Plan().Totals.Herd;
        Assert.Equal((horses, room, slows), (herd.Horses, herd.Room, herd.SlowsParty));
        Assert.Equal(herd.SlowsParty, herd.Horses > herd.Room || herd.Herd > herd.Men);
        Assert.Equal(Math.Max(0, herd.Herd - men), herd.Over);
    }

    [Fact]
    public void Prisoners_are_not_men_to_the_herd()
    {
        var herd = Closed(10, 0).Mount("hunter", "horse", held: 10).Prisoner("looter", 50, 20).Plan().Totals.Herd;
        Assert.Equal(10, herd.Men);
        Assert.False(herd.SlowsParty);
    }

    [Fact]
    public void The_line_counts_the_party_after_the_deal()
    {
        // 10 footmen, 2 horses held, horses on sale: the steward buys 9 (110 per 100 → 11) and sells 3 surplus pack animals.
        var s = new Scenario().Village().Party(10, footmen: 10)
            .Mount("hunter", "horse", held: 2, market: 30, buy: 300)
            .Pack("mule", held: 13, market: 5, buy: 150, sell: 70);
        var plan = s.Plan();
        Assert.Equal(9, plan.Row("mounts:riding").Change);
        Assert.Equal(-3, plan.Row("mounts:pack").Change);
        var herd = plan.Totals.Herd;
        Assert.Equal((11, 10), (herd.Mounts, herd.PackAnimals));
        Assert.Equal((21, 20), (herd.Horses, herd.Room));          // 10 men + 10 ridden; 11 riding + 10 pack
        Assert.True(herd.SlowsParty);                              // one spare horse + 10 pack animals = 11 > 10 men

        // The player takes one horse back: the herd fits again, live.
        plan.Decrease("mounts:riding");
        herd = plan.Totals.Herd;
        Assert.Equal((20, 20), (herd.Horses, herd.Room));
        Assert.False(herd.SlowsParty);
    }

    [Fact]
    public void Recruits_and_dismissals_move_the_men_and_the_footmen()
    {
        var s = Closed(20, 10).Mount("hunter", "horse", held: 10)
            .Troop("footman", inParty: 5, onOffer: 10, price: 20)
            .Troop("rider", inParty: 10, mounted: true);
        var plan = s.Plan();
        Assert.Equal((20, 10, 30), (plan.Totals.Herd.Men, plan.Totals.Herd.Footmen, plan.Totals.Herd.Room));

        plan.Increase("troops:footman", EditSize.Five);             // 5 more on foot: 25 men, 15 footmen, still 10 horses
        Assert.Equal((25, 15, 35), (plan.Totals.Herd.Men, plan.Totals.Herd.Footmen, plan.Totals.Herd.Room));

        plan.Decrease("troops:rider", EditSize.All);                // 10 riders leave: men drop, footmen stay
        Assert.Equal((15, 15, 25), (plan.Totals.Herd.Men, plan.Totals.Herd.Footmen, plan.Totals.Herd.Room));
    }

    [Fact]
    public void An_armys_attached_parties_are_pooled_as_the_game_does()
    {
        var s = Closed(10, 10).Mount("hunter", "horse", held: 10);
        s.Snap.Party.Attached = new AttachedParties { Men = 30, Footmen = 20, Mounts = 5, PackAnimals = 12, Livestock = 3 };
        var herd = s.Plan().Totals.Herd;
        Assert.Equal((40, 30, 15, 12, 3), (herd.Men, herd.Footmen, herd.Mounts, herd.PackAnimals, herd.Livestock));
        Assert.Equal((27, 52), (herd.Horses, herd.Room));          // 40 men + 15 ridden − 3 cows
        Assert.False(herd.SlowsParty);
    }

    [Fact]
    public void Lame_and_war_horses_count_as_mounts_pack_animals_never_carry_a_man()
    {
        var s = Closed(10, 10).Mount("charger", "war_horse", held: 2).Mount("hunter", "horse", held: 3, modifier: "lame",
            priceFactor: Scenario.Lame).Pack("mule", held: 12);
        var herd = s.Plan().Totals.Herd;
        Assert.Equal(5, herd.Mounts);
        Assert.Equal(5, herd.Ridden);                               // 5 footmen ride; the other 5 walk — pack animals carry none
        Assert.Equal(12, herd.Herd);
        Assert.True(herd.SlowsParty);
    }
}
