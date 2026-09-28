using SmartSteward.Core.Presentation;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>The Prices tab's order (PLAN step 20 — Anton 2026.09.28: "order all items in the Prices tab in each type by
/// ascending price, cheapest on top pricier at the bottom").</summary>
public class PriceBookOrderTests
{
    private sealed record Item(string Id, string Name, PriceBookGroup Group, AveragePrices? Avg);

    private static List<string> Order(params Item[] items) =>
        PriceBookOrder.Sort(items, i => i.Group, i => i.Avg, i => i.Name, i => i.Id).Select(i => i.Id).ToList();

    [Fact]
    public void Cheapest_first_inside_each_group_and_the_groups_keep_their_order()
    {
        var order = Order(
            new Item("mule", "Mule", PriceBookGroup.PackAnimals, new AveragePrices(160, 80)),
            new Item("cheese", "Cheese", PriceBookGroup.Food, new AveragePrices(42, 30)),
            new Item("grain", "Grain", PriceBookGroup.Food, new AveragePrices(11, 7)),
            new Item("sumpter", "Sumpter Horse", PriceBookGroup.PackAnimals, new AveragePrices(120, 60)),
            new Item("fish", "Fish", PriceBookGroup.Food, new AveragePrices(13, 9)));
        Assert.Equal(new[] { "grain", "fish", "cheese", "sumpter", "mule" }, order);
    }

    [Fact]
    public void Ties_go_by_name_and_items_without_a_price_go_last_by_name()
    {
        var order = Order(
            new Item("zeta", "Zeta Palfrey", PriceBookGroup.Mounts, null),
            new Item("b", "Steppe Horse", PriceBookGroup.Mounts, new AveragePrices(230, 110)),
            new Item("a", "Midlands Palfrey", PriceBookGroup.Mounts, new AveragePrices(230, 115)),
            new Item("alpha", "Alpha Horse", PriceBookGroup.Mounts, new AveragePrices(0, 0)),
            new Item("cheap", "Sand Horse", PriceBookGroup.Mounts, new AveragePrices(210, 100)));
        Assert.Equal(new[] { "cheap", "a", "b", "alpha", "zeta" }, order);
    }

    [Fact]
    public void Noble_horses_sort_by_what_they_fetch_they_are_only_sold()
    {
        var order = Order(
            new Item("x", "Palmatian", PriceBookGroup.NobleHorses, new AveragePrices(9_000, 2_000)),
            new Item("y", "Pharaoh's Horse", PriceBookGroup.NobleHorses, new AveragePrices(8_000, 2_500)));
        Assert.Equal(new[] { "x", "y" }, order);
        Assert.Equal(2_000, PriceBookOrder.SortPrice(PriceBookGroup.NobleHorses, new AveragePrices(9_000, 2_000)));
        Assert.Equal(9_000, PriceBookOrder.SortPrice(PriceBookGroup.WarMounts, new AveragePrices(9_000, 2_000)));
        Assert.Null(PriceBookOrder.SortPrice(PriceBookGroup.Food, null));
    }
}
