using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Playtest round 2 (Anton 2026.09.28: "food and horses even if locked, manage them"): inventory locks keep guarding
/// armour and weapons, while locked food, pack animals and mounts are counted and sold as surplus like the rest —
/// unless LocksProtectFoodAndHorses (DESIGN §2.6, §7). The plan and the executor's lock check must agree.
/// </summary>
public class InventoryLockTests
{
    [Fact]
    public void The_rule_guards_armour_and_weapons_always_and_food_and_animals_only_with_the_switch()
    {
        foreach (var kind in new[] { ItemKind.Food, ItemKind.PackAnimal, ItemKind.Mount })
        {
            Assert.False(LockRule.Guards(kind, locksProtectFoodAndHorses: false), kind.ToString());
            Assert.True(LockRule.Guards(kind, locksProtectFoodAndHorses: true), kind.ToString());
        }
        foreach (var kind in new[] { ItemKind.Equipment, ItemKind.Other })
        {
            Assert.True(LockRule.Guards(kind, locksProtectFoodAndHorses: false), kind.ToString());
            Assert.True(LockRule.Guards(kind, locksProtectFoodAndHorses: true), kind.ToString());
        }
        Assert.False(new StewardSettings().LocksProtectFoodAndHorses); // default off: the steward manages them
        Assert.True(LockRule.Guards(ItemKind.Food, (StewardSettings)null!)); // no settings → the safe side
        var locked = new ItemStack { Kind = ItemKind.Food, IsLocked = true, Count = 3 };
        Assert.False(LockRule.IsGuarded(locked, new StewardSettings()));
        Assert.True(LockRule.IsGuarded(locked, new StewardSettings { LocksProtectFoodAndHorses = true }));
        Assert.False(LockRule.IsGuarded(new ItemStack { Kind = ItemKind.Equipment }, new StewardSettings()));
    }

    [Fact]
    public void Locked_food_counts_as_held_and_is_sold_as_surplus_most_held_first()
    {
        var plan = new Scenario().Party(10) // target 20, sold only above 25
            .Food("grain", held: 40, locked: true)
            .Food("fish", held: 10)
            .Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(40, grain.Mine);
        Assert.Equal(-30, grain.Change);   // 50 held → 20: the most-held type goes first, locked or not
        Assert.Equal(0, plan.Row("food:fish").Change);
        Assert.Equal(0, grain.Locked);     // nothing held back → no "(+N locked)"
        Assert.False(grain.LocksGuard);
        Assert.Equal(40, grain.MaxSell);
    }

    [Fact]
    public void Locked_food_below_the_tolerance_is_kept_and_counted()
    {
        // Counted as held: a locked stack at the target means nothing to buy.
        var plan = new Scenario().Party(10).Food("grain", held: 22, locked: true).Food("fish", market: 50).Plan();
        Assert.Equal(0, plan.Row("food:grain").Change);
        Assert.Equal(0, plan.Row("food:fish").Change);
    }

    [Fact]
    public void Locked_pack_animals_are_counted_and_sold_most_expensive_first()
    {
        var s = new Scenario()
            .Pack("sumpter_horse", held: 8, sell: 70, locked: true)
            .Pack("mule", held: 6, sell: 60);
        var row = s.Plan().Row("mounts:pack");
        Assert.Equal(14, row.Mine);
        Assert.Equal(-4, row.Change);                 // target 10
        Assert.Equal(-4, row.Moved("sumpter_horse")); // the dearer one, locked or not
        Assert.Equal(0, row.Locked);
        Assert.Equal(14, row.MaxSell);
        Assert.False(row.LocksGuard);
    }

    [Fact]
    public void Locked_riding_mounts_are_counted_and_sold_most_expensive_first()
    {
        var plan = new Scenario().Party(10, footmen: 5) // riding target ceil(5 × 1.1) = 6
            .Mount("hunter", "horse", held: 6, sell: 100)
            .Mount("noble", "noble_horse", held: 1, sell: 2000, locked: true)
            .Mount("charger", "war_horse", held: 1, sell: 800, locked: true)
            .Plan();
        var riding = plan.Row("mounts:riding");
        Assert.Equal(8, riding.Mine);
        Assert.Equal(-2, riding.Change);
        Assert.Equal(-1, riding.Moved("noble"));
        Assert.Equal(-1, riding.Moved("charger"));
        Assert.Equal(0, riding.Moved("hunter"));
        Assert.Equal(0, riding.Locked);
    }

    [Fact]
    public void Reserved_upgrade_horses_are_never_sold_even_when_locked()
    {
        var plan = new Scenario().Party(10, footmen: 0).Upgrade("recruit", 10, ("war_horse", 2))
            .Mount("charger", "war_horse", held: 3, buy: 1000, sell: 500, locked: true)
            .Plan();
        var upgrade = plan.Row("mounts:upgrade:war_horse");
        Assert.Equal(2, upgrade.Mine);
        Assert.Equal(0, upgrade.Change);   // reserved for the two upgrades: the steward never sells them
        Assert.Equal(0, upgrade.Locked);
        Assert.Equal(2, upgrade.MaxSell);  // the player still may, by hand
        var riding = plan.Row("mounts:riding");
        Assert.Equal(-1, riding.Change);   // the third one is riding surplus (no footmen) — sold, locked or not
        Assert.Equal(-1, riding.Moved("charger"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Locked_armour_and_weapons_are_never_sold_nor_counted_whatever_the_switch(bool protectFoodAndHorses)
    {
        var s = new Scenario().Party(10)
            .Food("grain", held: 40, locked: true)
            .Loot("rags", LootGroup.Armour, held: 10, sell: 8)
            .Loot("helm", LootGroup.Armour, held: 3, sell: 60, locked: true)
            .Loot("heirloom", LootGroup.MeleeWeapons, held: 2, sell: 500, locked: true)
            .Loot("spear", LootGroup.MeleeWeapons, held: 4, sell: 25);
        s.Settings.SellLoot = true;
        s.Settings.LocksProtectFoodAndHorses = protectFoodAndHorses;
        var plan = s.Plan();

        var armour = plan.Row("loot:Armour");
        Assert.Equal(10, armour.Mine);
        Assert.Equal(-10, armour.Change);
        Assert.Equal(0, armour.Moved("helm"));
        Assert.Equal(3, armour.Locked);
        Assert.True(armour.LocksGuard);
        var melee = plan.Row("loot:MeleeWeapons");
        Assert.Equal(-4, melee.Change);
        Assert.Equal(0, melee.Moved("heirloom"));
        Assert.Equal(2, melee.Locked);

        // By hand neither: [-] all stops at the unlocked pieces.
        plan.Decrease(armour.Id, EditSize.All);
        Assert.Equal(-10, armour.Change);
        Assert.Equal(EditBlock.AllSold, armour.DecreaseBlock);

        Assert.Equal(protectFoodAndHorses ? 0 : -20, plan.Row("food:grain").Change);
    }

    [Fact]
    public void The_switch_on_restores_the_old_way_for_food_and_horses()
    {
        var s = LockedEverything();
        s.Settings.LocksProtectFoodAndHorses = true;
        var plan = s.Plan();

        var grain = plan.Row("food:grain");
        Assert.Equal(0, grain.Change);
        Assert.Equal(40, grain.Locked);
        Assert.Equal(0, grain.MaxSell);
        Assert.True(grain.LocksGuard);
        Assert.Equal(EditBlock.NothingToSell, grain.DecreaseBlock);
        Assert.Equal(-10, plan.Row("food:fish").Change); // 50 held, target 20: only the fish can go

        var pack = plan.Row("mounts:pack");
        Assert.Equal(-4, pack.Moved("mule"));
        Assert.Equal(0, pack.Moved("sumpter_horse"));
        Assert.Equal(8, pack.Locked);

        var riding = plan.Row("mounts:riding");
        Assert.Equal(-1, riding.Moved("hunter"));
        Assert.Equal(0, riding.Moved("noble"));
        Assert.Equal(1, riding.Locked);
    }

    [Fact]
    public void The_player_may_sell_locked_food_by_hand()
    {
        var s = new Scenario().Party(10).Food("grain", held: 40, locked: true);
        s.Settings.SellFoodSurplus = false;
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(0, grain.Change);
        var all = plan.Decrease(grain.Id, EditSize.All);
        Assert.Equal(-40, all.After);
        var sale = plan.Transactions.Single(t => t.StackKey == "grain");
        Assert.Equal(TransactionKind.Sell, sale.Kind);
        Assert.Equal(40, sale.Count);
        Assert.False(sale.HonoursLock);
    }

    [Fact]
    public void Plan_to_transactions_sells_locked_food_and_horses_and_the_executor_lets_them_through()
    {
        var plan = LockedEverything().Plan();
        var sales = plan.Transactions.Where(t => t.Kind == TransactionKind.Sell).ToDictionary(t => t.StackKey!);

        // The locked food and horses are in the executor's list, not guarded by their lock...
        Assert.Equal(30, sales["grain"].Count);
        Assert.Equal(4, sales["sumpter_horse"].Count);
        Assert.Equal(1, sales["noble"].Count);
        foreach (var key in new[] { "grain", "sumpter_horse", "noble" })
        {
            Assert.False(sales[key].HonoursLock, key);
            Assert.False(ExecutionBudget.StoppedByLock(sales[key], lockedNow: true), key); // still locked at the click
        }
        Assert.DoesNotContain("fish", sales.Keys);
        Assert.DoesNotContain("mule", sales.Keys);
        Assert.DoesNotContain("hunter", sales.Keys);

        // ...while armour keeps its guard: the locked helm is not in the list, and a piece locked after the plan
        // was made is stopped at the click.
        Assert.DoesNotContain("helm", sales.Keys);
        var rags = sales["rags"];
        Assert.Equal(10, rags.Count);
        Assert.True(rags.HonoursLock);
        Assert.True(ExecutionBudget.StoppedByLock(rags, lockedNow: true));
        Assert.False(ExecutionBudget.StoppedByLock(rags, lockedNow: false));

        // A purchase is never stopped by a lock.
        foreach (var buy in plan.Transactions.Where(t => t.Kind == TransactionKind.Buy))
            Assert.False(ExecutionBudget.StoppedByLock(buy, lockedNow: true));
    }

    [Fact]
    public void Plan_to_transactions_with_the_switch_on_leaves_locked_food_and_horses_alone()
    {
        var s = LockedEverything();
        s.Settings.LocksProtectFoodAndHorses = true;
        var plan = s.Plan();
        var sales = plan.Transactions.Where(t => t.Kind == TransactionKind.Sell).ToDictionary(t => t.StackKey!);

        foreach (var key in new[] { "grain", "sumpter_horse", "noble", "helm" })
            Assert.DoesNotContain(key, sales.Keys);
        Assert.Equal(10, sales["fish"].Count);
        Assert.Equal(4, sales["mule"].Count);
        Assert.Equal(1, sales["hunter"].Count);
        // Everything sold honours a lock set after the plan was made — food and horses too, now.
        foreach (var sale in sales.Values)
        {
            Assert.True(sale.HonoursLock, sale.StackKey);
            Assert.True(ExecutionBudget.StoppedByLock(sale, lockedNow: true), sale.StackKey);
        }
    }

    [Fact]
    public void The_new_setting_lives_in_General_and_the_file()
    {
        var def = SettingsRegistry.Find(nameof(StewardSettings.LocksProtectFoodAndHorses))!;
        Assert.Equal(SettingsRegistry.General, def.Group);
        Assert.Equal("Locks protect food & horses", def.Label);
        Assert.EndsWith(" Default: off.", def.Hint);
        var text = SettingsFile.Generate(new StewardSettings { LocksProtectFoodAndHorses = true });
        Assert.Contains("  \"LocksProtectFoodAndHorses\": true,", text);
        Assert.True(SettingsFile.Parse(text).Settings.LocksProtectFoodAndHorses);
        Assert.False(SettingsFile.Parse(SettingsFile.Generate(new StewardSettings())).Settings.LocksProtectFoodAndHorses);
    }

    /// <summary>A town with a surplus in every job, and a locked stack in each: food 50 of target 20 (grain locked),
    /// pack animals 14 of 10 (the dearer sumpter horses locked), riding mounts 7 of 6 (the noble horse locked, the
    /// dearest), armour with a locked helm.</summary>
    private static Scenario LockedEverything()
    {
        var s = new Scenario().Party(10, footmen: 5)
            .Food("grain", held: 40, locked: true)
            .Food("fish", held: 10)
            .Pack("sumpter_horse", held: 8, sell: 70, locked: true)
            .Pack("mule", held: 6, sell: 60)
            .Mount("hunter", "horse", held: 6, sell: 100)
            .Mount("noble", "noble_horse", held: 1, sell: 2000, locked: true)
            .Loot("rags", LootGroup.Armour, held: 10, sell: 8)
            .Loot("helm", LootGroup.Armour, held: 3, sell: 60, locked: true);
        s.Settings.SellLoot = true;
        return s;
    }
}
