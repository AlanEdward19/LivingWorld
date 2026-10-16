using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#47 fatia 4 (ADR-0039): recursos do cenário como objetos; o efeito ao consumir (nível a que a fome/sede é restaurada) vem do descritor.</summary>
public class ObjectResourceTests
{
    private const int Food = 1; // ScenarioRunner.DefaultEconomyRules
    private const int Water = 2;

    private static JsonObject Root(string? foodEffect = null, string? waterEffect = null, Action<JsonObject>? tweak = null)
    {
        var root = new JsonObject();
        DefaultObjects.AddTo(root);
        if (foodEffect is not null) Descriptor(root, "food")["effects"] = new JsonArray(foodEffect);
        if (waterEffect is not null) Descriptor(root, "water")["effects"] = new JsonArray(waterEffect);
        tweak?.Invoke(root);
        return root;
    }

    private static JsonObject Descriptor(JsonObject root, string id) => (JsonObject)((JsonArray)root["Objects"]!).Single(o => (string)o!["id"]! == id)!;

    private static ObjectCatalog Catalog(string? foodEffect = null, string? waterEffect = null, Action<JsonObject>? tweak = null) =>
        ObjectCatalogScenarioLoader.Load(Root(foodEffect, waterEffect, tweak)).Value!;

    private static (WorldState World, WorldClock Clock) World(ulong seed, ObjectCatalog? catalog, int population = 30) =>
        ScenarioRunner.Create(seed: seed, initialPopulation: population, objectCatalog: catalog);

    private static Npc Resident(WorldState world) => world.Npcs.First(n => n.IsAlive && n.Household is not null);

    // ---- formato ----

    [Fact]
    public void A_resource_object_keeps_its_resource_id_and_consume_levels()
    {
        var catalog = Catalog(foodEffect: "hunger:40", waterEffect: "thirst:75");

        Assert.Equal(Food, catalog.ForResource(Food)!.ResourceId);
        Assert.Equal(new ObjectEffect("hunger", 40), catalog.ForResource(Food)!.Effects!.Single());
        Assert.Equal(new ObjectEffect("thirst", 75), catalog.ForResource(Water)!.Effects!.Single());
        Assert.Null(catalog.ForResource(99));
    }

    [Fact]
    public void Furniture_has_no_resource_id_and_the_json_omits_it()
    {
        var table = Catalog().Find("table")!;

        Assert.Null(table.ResourceId);
        Assert.DoesNotContain("resourceId", System.Text.Json.JsonSerializer.Serialize(table, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_non_positive_resource_id_is_refused(int resourceId)
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(tweak: r => Descriptor(r, "food")["resourceId"] = resourceId));

        Assert.False(result.IsSuccess);
        Assert.Contains("resourceId", result.Error);
    }

    [Fact]
    public void A_text_resource_id_is_refused_and_a_resource_cannot_have_two_descriptors()
    {
        var text = ObjectCatalogScenarioLoader.Load(Root(tweak: r => Descriptor(r, "food")["resourceId"] = "trigo"));
        var twice = ObjectCatalogScenarioLoader.Load(Root(tweak: r => Descriptor(r, "water")["resourceId"] = Food));

        Assert.Contains("resourceId", text.Error);
        Assert.Contains("já tem descritor", twice.Error);
    }

    [Theory]
    [InlineData("hunger:-1")]
    [InlineData("hunger:101")]
    [InlineData("hunger:abc")]
    [InlineData("hunger:NaN")]
    public void A_consume_level_outside_zero_to_one_hundred_is_refused(string token)
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(foodEffect: token));

        Assert.False(result.IsSuccess);
        Assert.Contains("0 a 100", result.Error);
    }

    [Theory]
    [InlineData("hunger:0")]
    [InlineData("hunger:100")]
    [InlineData("hunger:62.5")]
    public void The_consume_level_bounds_are_inclusive(string token)
    {
        Assert.True(ObjectCatalogScenarioLoader.Load(Root(foodEffect: token)).IsSuccess);
    }

    [Fact]
    public void A_resource_object_only_takes_resource_targets_and_furniture_only_its_own()
    {
        var sleepOnResource = ObjectCatalogScenarioLoader.Load(Root(foodEffect: "sleep:0.5"));
        var hungerOnFurniture = ObjectCatalogScenarioLoader.Load(Root(tweak: r => Descriptor(r, "bed")["effects"] = new JsonArray("hunger:50")));

        Assert.False(sleepOnResource.IsSuccess);
        Assert.Contains("objeto de recurso", sleepOnResource.Error);
        Assert.False(hungerOnFurniture.IsSuccess);
        Assert.Contains("sem resourceId", hungerOnFurniture.Error);
    }

    [Fact]
    public void The_same_target_twice_on_one_resource_is_refused()
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(tweak: r => Descriptor(r, "food")["effects"] = new JsonArray("hunger:50", "hunger:60")));

        Assert.False(result.IsSuccess);
        Assert.Contains("repetido", result.Error);
    }

    [Fact]
    public void A_resource_object_may_carry_both_needs()
    {
        var catalog = Catalog(tweak: r => Descriptor(r, "food")["effects"] = new JsonArray("hunger:60", "thirst:20"));

        Assert.Equal([new ObjectEffect("hunger", 60), new ObjectEffect("thirst", 20)], catalog.ForResource(Food)!.Effects);
    }

    [Fact]
    public void The_resource_id_and_levels_survive_the_snapshot_round_trip()
    {
        var world = World(7, Catalog(foodEffect: "hunger:40")).World;

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(Food, restored.ObjectCatalog!.ForResource(Food)!.ResourceId);
        Assert.Equal(40, restored.ObjectCatalog.ForResource(Food)!.Effects!.Single().Magnitude);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void The_default_objects_describe_food_and_water_with_the_full_restore_of_always()
    {
        var catalog = Catalog();

        Assert.Equal(new ObjectEffect("hunger", 100), catalog.ForResource(Food)!.Effects!.Single());
        Assert.Equal(new ObjectEffect("thirst", 100), catalog.ForResource(Water)!.Effects!.Single());
        Assert.Equal("resource", catalog.ForResource(Food)!.Category);
    }

    // ---- consumo ----

    [Fact]
    public void Without_a_descriptor_or_a_catalog_the_consume_level_is_one_hundred()
    {
        var (without, _) = World(3, null);
        var (partial, _) = World(3, Catalog(tweak: r => ((JsonArray)r["Objects"]!).Remove(Descriptor(r, "water"))));

        Assert.Equal(100, ObjectUse.ConsumeLevel(without, new ResourceType(Food), ObjectEffectTargets.Hunger));
        Assert.Equal(100, ObjectUse.ConsumeLevel(partial, new ResourceType(Water), ObjectEffectTargets.Thirst));
        Assert.Equal(100, ObjectUse.ConsumeLevel(partial, new ResourceType(99), ObjectEffectTargets.Hunger));
    }

    [Fact]
    public void A_descriptor_without_the_target_leaves_the_level_at_one_hundred()
    {
        var (world, _) = World(3, Catalog(foodEffect: "thirst:30"));

        Assert.Equal(100, ObjectUse.ConsumeLevel(world, new ResourceType(Food), ObjectEffectTargets.Hunger));
        Assert.Equal(30, ObjectUse.ConsumeLevel(world, new ResourceType(Food), ObjectEffectTargets.Thirst));
    }

    [Fact]
    public void The_level_rounds_halves_away_from_zero()
    {
        var (world, _) = World(3, Catalog(foodEffect: "hunger:62.5"));

        Assert.Equal(63, ObjectUse.ConsumeLevel(world, new ResourceType(Food), ObjectEffectTargets.Hunger));
    }

    private static Npc Hungry(WorldState world, int hunger = 10, int thirst = 10)
    {
        var npc = Resident(world);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(hunger, now);
        npc.SetThirst(thirst, now);
        var household = world.FindHousehold(npc.Household!.Value)!;
        household.Deposit(new ResourceType(Food), 20);
        household.Deposit(new ResourceType(Water), 20);
        return npc;
    }

    private static void Eat(WorldState world, Npc npc) =>
        BehaviorDecisionSystem.ResolveFoodPlan(world, npc, MarketIndex.BuildForTick(world), world.CurrentDate.TotalHours, ActionType.Eat);

    [Fact]
    public void Eating_restores_hunger_to_the_descriptors_level_and_thirst_to_its_own()
    {
        var (world, _) = World(3, Catalog(foodEffect: "hunger:55", waterEffect: "thirst:35"));
        var npc = Hungry(world);

        Eat(world, npc);

        long now = world.CurrentDate.TotalHours;
        Assert.Equal(55, npc.HungerAt(now));
        Assert.Equal(35, npc.ThirstAt(now));
    }

    [Fact]
    public void Eating_never_lowers_a_need_that_is_already_above_the_level()
    {
        var (world, _) = World(3, Catalog(foodEffect: "hunger:40", waterEffect: "thirst:40"));
        var npc = Hungry(world, hunger: 80, thirst: 90);

        Eat(world, npc);

        long now = world.CurrentDate.TotalHours;
        Assert.Equal(80, npc.HungerAt(now));
        Assert.Equal(90, npc.ThirstAt(now));
    }

    [Fact]
    public void The_default_levels_restore_fully_exactly_as_before()
    {
        var (world, _) = World(3, Catalog());
        var npc = Hungry(world, hunger: 5, thirst: 5);

        Eat(world, npc);

        long now = world.CurrentDate.TotalHours;
        Assert.Equal(100, npc.HungerAt(now));
        Assert.Equal(100, npc.ThirstAt(now));
    }

    [Fact]
    public void A_smaller_meal_still_consumes_one_unit_of_each_resource()
    {
        var (world, _) = World(3, Catalog(foodEffect: "hunger:20", waterEffect: "thirst:20"));
        var npc = Hungry(world);
        var household = world.FindHousehold(npc.Household!.Value)!;
        long foodBefore = household.Stock.GetValueOrDefault(new ResourceType(Food));
        long waterBefore = household.Stock.GetValueOrDefault(new ResourceType(Water));
        long consumedBefore = world.ResourceConsumed.GetValueOrDefault(new ResourceType(Food));

        Eat(world, npc);

        Assert.Equal(foodBefore - 1, household.Stock.GetValueOrDefault(new ResourceType(Food)));
        Assert.Equal(waterBefore - 1, household.Stock.GetValueOrDefault(new ResourceType(Water)));
        Assert.Equal(consumedBefore + 1, world.ResourceConsumed.GetValueOrDefault(new ResourceType(Food)));
    }

    // ---- o mundo ----

    private static string NpcDigest(WorldState world)
    {
        var sb = new StringBuilder();
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
            sb.Append(npc.Id.Value).Append('|').Append(npc.IsAlive).Append('|').Append(npc.CurrentAction).Append('|').Append(npc.ActionStartedAtTick)
                .Append('|').Append(npc.SleepAt(world.CurrentDate.TotalHours)).Append('|').Append(npc.HungerAt(world.CurrentDate.TotalHours))
                .Append('|').Append(npc.ThirstAt(world.CurrentDate.TotalHours))
                .Append('|').Append(npc.CurrentLocation.X).Append(',').Append(npc.CurrentLocation.Y).Append('\n');
        foreach (var (resource, amount) in world.ResourceConsumed.OrderBy(p => p.Key.Id))
            sb.Append("consumed|").Append(resource.Id).Append('|').Append(amount).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Run(ulong seed, ObjectCatalog? catalog, int ticks)
    {
        var (world, clock) = World(seed, catalog);
        clock.Run(world, ticks);
        return NpcDigest(world);
    }

    [Fact]
    public void Equivalent_descriptors_leave_the_npc_world_exactly_as_without_a_catalog()
    {
        Assert.Equal(Run(11, null, 24 * 8), Run(11, Catalog(foodEffect: "hunger:100", waterEffect: "thirst:100"), 24 * 8));
        Assert.Equal(Run(12, null, 24 * 8), Run(12, Catalog(), 24 * 8));
    }

    [Fact]
    public void A_world_without_Objects_keeps_the_canonical_hash_and_json_of_before()
    {
        var (world, clock) = World(5, null);
        clock.Run(world, 24 * 4);

        Assert.DoesNotContain("ObjectCatalog", WorldSnapshot.Serialize(world));
        Assert.DoesNotContain("resourceId", WorldSnapshot.Serialize(world));
    }

    [Fact]
    public void Changing_the_descriptors_level_changes_the_world()
    {
        Assert.NotEqual(Run(11, Catalog(), 24 * 8), Run(11, Catalog(foodEffect: "hunger:40", waterEffect: "thirst:40"), 24 * 8));
    }

    [Fact]
    public void The_same_seed_and_descriptors_give_the_same_world_twice()
    {
        Assert.Equal(Run(5, Catalog(foodEffect: "hunger:50"), 24 * 6), Run(5, Catalog(foodEffect: "hunger:50"), 24 * 6));
        Assert.NotEqual(Run(5, Catalog(foodEffect: "hunger:50"), 24 * 6), Run(6, Catalog(foodEffect: "hunger:50"), 24 * 6));
    }

    private static long FoodConsumed(ulong seed, ObjectCatalog? catalog, int ticks)
    {
        var (world, clock) = World(seed, catalog);
        clock.Run(world, ticks);
        return world.ResourceConsumed.GetValueOrDefault(new ResourceType(Food));
    }

    [Fact]
    public void A_food_that_restores_less_makes_the_npcs_eat_more_in_at_least_18_of_20_seeds()
    {
        int more = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            long full = FoodConsumed(seed, Catalog(foodEffect: "hunger:100"), 24 * 12);
            long half = FoodConsumed(seed, Catalog(foodEffect: "hunger:50"), 24 * 12);
            if (half > full) more++;
        }

        Assert.True(more >= 18, $"comida que restaura menos fez consumir mais em {more}/20 seeds");
    }

    [Fact]
    public void Smaller_meals_keep_the_money_and_stock_accounting_closed_every_day()
    {
        var (world, clock) = World(9, Catalog(foodEffect: "hunger:70", waterEffect: "thirst:70"), population: 40);
        long Total() => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);
        long initialTotal = Total() - (world.MoneyMinted.Amount - world.MoneyDestroyed.Amount);

        for (int day = 0; day < 90; day++)
        {
            clock.Run(world, 24);
            Assert.Equal(world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, Total() - initialTotal);
            Assert.All(world.Households, h => Assert.All(h.Stock.Values, q => Assert.True(q >= 0, "estoque nunca fica negativo")));
        }

        Assert.True(world.ResourceConsumed.GetValueOrDefault(new ResourceType(Food)) > 0);
    }
}
