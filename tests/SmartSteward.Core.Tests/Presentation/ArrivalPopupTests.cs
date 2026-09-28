using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;
using Xunit;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>The arrival popup's rule (DESIGN §6, playtest round 1 — Hiblet: a village with no trade and nothing to do).</summary>
public class ArrivalPopupTests
{
    [Theory]
    [InlineData(false, true, true, false, PopupVerdict.MarketClosed)]  // closed market wins over everything
    [InlineData(false, true, true, true, PopupVerdict.MarketClosed)]
    [InlineData(true, false, false, false, PopupVerdict.NothingPlanned)] // no rows: no popup even when always-open
    [InlineData(true, true, false, true, PopupVerdict.NoChanges)]
    [InlineData(true, true, false, false, PopupVerdict.Open)]            // PopupOnlyWithChanges off: rows are enough
    [InlineData(true, true, true, true, PopupVerdict.Open)]
    public void The_rule(bool marketOpen, bool hasRows, bool hasChanges, bool onlyWithChanges, PopupVerdict expected) =>
        Assert.Equal(expected, ArrivalPopup.Decide(marketOpen, hasRows, hasChanges, onlyWithChanges));

    [Fact]
    public void A_village_without_trade_never_pops_up()
    {
        // Hiblet: NO TRADE, food and horses held, prisoners aboard (no ransom in a village).
        var s = new Scenario().Village().Party(89, footmen: 67)
            .Food("grain", held: 347).Mount("hunter", "horse", held: 93).Prisoner("looter", 37, 20);
        s.Snap.CanTrade = false;
        s.Snap.Prison.CanRansom = false;
        var plan = s.Plan();

        Assert.False(plan.HasChanges);
        Assert.Equal(PopupVerdict.MarketClosed, ArrivalPopup.Decide(plan, s.Snap.CanTrade, onlyWithChanges: true));
        Assert.Equal(PopupVerdict.MarketClosed, ArrivalPopup.Decide(plan, s.Snap.CanTrade, onlyWithChanges: false));
    }

    [Fact]
    public void A_town_with_work_pops_up_and_one_without_does_not()
    {
        var busy = Scenario.BusyTown();
        Assert.Equal(PopupVerdict.Open, ArrivalPopup.Decide(busy.Plan(), marketOpen: true, onlyWithChanges: true));

        var idle = new Scenario().Party(10);
        var plan = idle.Plan();
        Assert.False(plan.HasChanges);
        Assert.NotEqual(PopupVerdict.Open, ArrivalPopup.Decide(plan, marketOpen: true, onlyWithChanges: true));
    }

    [Fact]
    public void Every_verdict_reads_for_the_log()
    {
        foreach (PopupVerdict v in Enum.GetValues(typeof(PopupVerdict)))
            Assert.False(string.IsNullOrWhiteSpace(ArrivalPopup.Describe(v)));
        Assert.Equal("the market is closed", ArrivalPopup.Describe(PopupVerdict.MarketClosed));
    }
}
