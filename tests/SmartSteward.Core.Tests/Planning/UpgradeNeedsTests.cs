using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using Xunit;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>The upgrade horses per kind (DESIGN §2.4, playtest round 1: one number for both kinds bought 10 of each).</summary>
public class UpgradeNeedsTests
{
    private static Scenario Party() => new Scenario()
        .Upgrade("recruit", 10, (null, 6), ("horse", 6))       // foot OR horse → counts as horse
        .Upgrade("raider", 5, ("war_horse", 3), ("war_horse", 4)) // one XP pool → 4
        .Upgrade("camel_rider", 3, ("war_camel", 2));             // a category a mod adds

    [Fact]
    public void Ready_counts_the_party_screen_way_per_kind()
    {
        var needs = UpgradeNeeds.Of(Party().Snap);
        Assert.Equal(new[] { "horse", "war_camel", "war_horse" }, needs.InPlay);
        Assert.Equal(6, needs.ReadyFor(UpgradeNeeds.Horse));
        Assert.Equal(4, needs.ReadyFor(UpgradeNeeds.WarHorse));
        Assert.Equal(2, needs.ReadyFor("war_camel"));
        Assert.Equal(0, needs.ReadyFor("noble_horse"));
    }

    [Fact]
    public void Each_kind_takes_its_own_fixed_number_or_the_automatic_count()
    {
        var needs = UpgradeNeeds.Of(Party().Snap);
        var s = new StewardSettings { WarMountsExtra = 1 };
        Assert.Equal(7, needs.NeedFor(s, "horse"));
        Assert.Equal(5, needs.NeedFor(s, "war_horse"));

        s.WarMountsWarHorseTarget = 10;
        Assert.Equal(7, needs.NeedFor(s, "horse"));      // untouched by the war-horse number
        Assert.Equal(10, needs.NeedFor(s, "war_horse")); // exactly, no spares on top

        s.WarMountsHorseTarget = 0;
        Assert.Equal(0, needs.NeedFor(s, "horse"));
        Assert.Equal(3, needs.NeedFor(s, "war_camel"));  // a modded kind stays automatic (+ spares)
    }

    [Fact]
    public void Nothing_is_kept_for_a_kind_nobody_upgrades_into_or_when_off()
    {
        var needs = UpgradeNeeds.Of(new Scenario().Upgrade("raider", 5, ("war_horse", 2)).Snap);
        var s = new StewardSettings { WarMountsHorseTarget = 10, WarMountsExtra = 3 };
        Assert.Equal(0, needs.NeedFor(s, "horse"));
        Assert.Equal(new[] { "war_horse" }, needs.Need(s).Keys);
        Assert.Equal(5, needs.Need(s)["war_horse"]);

        s.WarMountsEnabled = false;
        Assert.Empty(needs.Need(s));
        Assert.Equal(0, needs.NeedFor(s, "war_horse"));
    }

    [Fact]
    public void The_manual_targets_map_to_the_two_kinds_only()
    {
        var s = new StewardSettings { WarMountsHorseTarget = 4, WarMountsWarHorseTarget = 9 };
        Assert.Equal(4, UpgradeNeeds.ManualTarget(s, "horse"));
        Assert.Equal(9, UpgradeNeeds.ManualTarget(s, "war_horse"));
        Assert.Equal(-1, UpgradeNeeds.ManualTarget(s, "war_camel"));
        Assert.Equal(-1, UpgradeNeeds.ManualTarget(new StewardSettings(), "horse")); // default: automatic
    }
}
