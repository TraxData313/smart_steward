using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Snapshot;

/// <summary>The pure rules the Module applies while reading the game (PLAN step 6) — each mirrors v1.4.8 code.</summary>
public class GameRulesTests
{
    [Fact]
    public void Stack_key_is_item_and_modifier_with_a_separator()
    {
        Assert.Equal("grain|", GameRules.StackKey("grain", null));
        Assert.Equal("hunter|lame", GameRules.StackKey("hunter", "lame"));
        // "ab" + "c" and "a" + "bc" must not collide
        Assert.NotEqual(GameRules.StackKey("ab", "c"), GameRules.StackKey("a", "bc"));
    }

    [Fact]
    public void Lock_id_is_vanillas_item_plus_modifier_without_separator()
    {
        // CampaignUIHelper.GetItemLockStringID: item.StringId + modifier.StringId
        Assert.Equal("hunterlame", GameRules.LockId("hunter", "lame"));
        Assert.Equal("grain", GameRules.LockId("grain", null));
    }

    [Theory]
    [InlineData(true, false, false, false, LootGroup.None, false, true, ItemKind.Food)]
    [InlineData(false, true, true, false, LootGroup.None, false, true, ItemKind.PackAnimal)]
    [InlineData(false, true, false, true, LootGroup.None, false, true, ItemKind.Mount)]
    [InlineData(false, true, false, false, LootGroup.None, false, true, ItemKind.Other)] // livestock
    [InlineData(false, false, false, false, LootGroup.Armour, false, true, ItemKind.Equipment)]
    [InlineData(false, false, false, false, LootGroup.Ranged, false, true, ItemKind.Equipment)]
    [InlineData(false, false, false, false, LootGroup.None, false, true, ItemKind.Other)] // trade goods, banners…
    [InlineData(true, false, false, false, LootGroup.None, true, true, ItemKind.Other)] // quest item
    [InlineData(false, false, false, false, LootGroup.MeleeWeapons, false, false, ItemKind.Other)] // not transferable
    public void Classify_follows_the_design_terms(bool food, bool horse, bool pack, bool mount, LootGroup group,
        bool quest, bool transferable, ItemKind expected)
    {
        Assert.Equal(expected, GameRules.Classify(food, horse, pack, mount, group, quest, transferable));
    }

    [Theory]
    [InlineData(0.1, true)]    // vanilla's lame_horse
    [InlineData(0.2, true)]    // vanilla's companion_horse ("Old")
    [InlineData(0.8, true)]    // a mod's "Badly Tempered" (commented out in vanilla's data)
    [InlineData(1.0, false)]   // plain
    [InlineData(1.2, false)]   // a better one
    public void A_bad_modifier_is_one_worth_less_than_the_plain_item(double priceFactor, bool bad)
    {
        // The game's own test (BattleCampaignBehavior, the Metallurgy perk): ItemModifier.PriceMultiplier < 1.
        Assert.Equal(bad, GameRules.IsBadModifier(priceFactor));
        var stack = new ItemStack { ModifierId = "m", ModifierPriceFactor = priceFactor };
        Assert.Equal(bad, stack.HasBadModifier);
        Assert.False(new ItemStack { ModifierPriceFactor = priceFactor }.HasBadModifier); // no modifier, never bad
    }

    [Fact]
    public void Average_prices_run_the_factor_through_the_trade_penalty_like_the_model()
    {
        // buy = max(1, ceil(value × factor × (1 + penalty))), float like the game
        Assert.Equal(11, GameRules.AverageBuyPrice(10, 1f, 0.06f));
        Assert.Equal(159, GameRules.AverageBuyPrice(150, 1f, 0.06f));
        Assert.Equal(175, GameRules.AverageBuyPrice(150, 1.1f, 0.06f)); // 150 × 1.1 × 1.06 = 174.9
        // sell = max(1, floor(value × factor / (1 + penalty))): an animal pays 0.06 + 0.8
        Assert.Equal(80, GameRules.AverageSellPrice(150, 1f, 0.86f));
        Assert.Equal(9, GameRules.AverageSellPrice(10, 1f, 0.06f));
        // never below 1
        Assert.Equal(1, GameRules.AverageBuyPrice(0, 1f, 0.06f));
        Assert.Equal(1, GameRules.AverageSellPrice(1, 0.5f, 1.86f));
    }

    [Fact]
    public void Mean_factor_averages_the_towns_summed()
    {
        Assert.Equal(1f, GameRules.MeanFactor(Array.Empty<float>()));
        Assert.Equal(1.1f, GameRules.MeanFactor(new[] { 1.0f, 1.2f }), 5);
        Assert.Equal(0.9f, GameRules.MeanFactor(new[] { 0.9f }), 5);
    }

    [Fact]
    public void Donation_influence_is_the_models_number_boosted_by_military_coronae()
    {
        Assert.Equal(1.0, GameRules.DonationInfluence(1.0f, inKingdom: true, militaryCoronae: false), 4);
        Assert.Equal(1.2, GameRules.DonationInfluence(1.0f, inKingdom: true, militaryCoronae: true), 4);
        Assert.Equal(0.0, GameRules.DonationInfluence(1.0f, inKingdom: false, militaryCoronae: true), 4);
        Assert.Equal(0.0, GameRules.DonationInfluence(-1f, inKingdom: true, militaryCoronae: false), 4);
    }

    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(false, true, false, true, false)] // villages have no dungeon
    [InlineData(true, false, false, true, false)] // another faction's town
    [InlineData(true, true, true, true, false)]   // own clan's fief: "Manage prisoners", no influence
    [InlineData(true, true, false, false, false)] // no dungeon access (bribe unpaid)
    public void Donating_needs_own_faction_not_own_clan_and_the_dungeon(bool town, bool faction, bool ownClan,
        bool dungeon, bool expected)
    {
        Assert.Equal(expected, GameRules.DonateAllowed(town, faction, ownClan, dungeon));
    }

    [Theory] // step 34: the first failing condition in the game's order, then the room (RESEARCH §31)
    [InlineData(true, true, false, true, 5, DonateBlock.None)]
    [InlineData(false, true, false, true, 5, DonateBlock.NoDungeon)]
    [InlineData(true, false, true, false, 5, DonateBlock.NotYourKingdom)]
    [InlineData(true, true, true, false, 5, DonateBlock.YourClansFief)]
    [InlineData(true, true, false, false, 5, DonateBlock.NoDungeonAccess)]
    [InlineData(true, true, false, true, 0, DonateBlock.DungeonFull)]
    public void Why_donating_is_not_possible(bool dungeon, bool faction, bool ownClan, bool access, int room, DonateBlock expected)
    {
        Assert.Equal(expected, GameRules.DonateBlockOf(dungeon, faction, ownClan, access, room));
    }

    [Fact]
    public void Skill_tag_names_the_best_two()
    {
        var skills = new Dictionary<string, int>
        {
            ["Riding"] = 95, ["Scouting"] = 120, ["Bow"] = 95, ["Trade"] = 0, ["Medicine"] = 40,
        };
        Assert.Equal("Scouting 120, Bow 95", GameRules.SkillTag(skills));
        Assert.Equal("Scouting 120", GameRules.SkillTag(skills, 1));
        Assert.Null(GameRules.SkillTag(new Dictionary<string, int> { ["Trade"] = 0 }));
        Assert.Null(GameRules.SkillTag(Array.Empty<KeyValuePair<string, int>>()));
    }

    [Theory]
    [InlineData(5, 2, 2)]
    [InlineData(5, 9, 5)]
    [InlineData(0, 3, 0)]
    [InlineData(4, 0, 0)]
    public void Wounded_go_first(int count, int wounded, int expected)
    {
        Assert.Equal(expected, GameRules.WoundedToMove(count, wounded));
    }
}
