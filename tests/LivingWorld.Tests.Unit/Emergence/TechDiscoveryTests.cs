using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Emergence;

/// <summary>emergence-first-slice (ADR-0013): tecnologia como composição de eixos, com limiar de novidade.</summary>
public class TechDiscoveryTests
{
    private static readonly ResourceType Iron = new(4);
    private const long Year = 17280 / 2; // horas por ano no calendário padrão
    private static readonly EmergenceRules Pressured = EmergenceRules.Mobility with { PressureTravelTicks = 2 };

    private static (WorldState World, WorldClock Clock) Village(ulong seed, EmergenceRules rules)
    {
        var (world, clock) = ScenarioRunner.Create(seed, emergenceRules: rules);
        world.ActiveCities().First().DepositStock(Iron, 5000);
        return (world, clock);
    }

    private static string Keys(WorldState world) => string.Join(";", world.Technologies.Select(t => $"{t.Culture.Id}:{t.Vector.Key}:{t.VariationOfKey}"));

    [Fact]
    public void Disabled_by_default_nothing_emerges()
    {
        var (world, clock) = ScenarioRunner.Create(1);
        world.ActiveCities().First().DepositStock(Iron, 5000);

        clock.Run(world, 17280);

        Assert.Empty(world.Technologies);
        Assert.False(world.EmergenceRules.Enabled);
    }

    [Fact]
    public void Pressure_skill_and_stock_open_a_prototype_that_spends_the_resource_and_time()
    {
        var (world, clock) = Village(2, Pressured);
        var city = world.ActiveCities().First();
        long before = city.Stock[Iron];

        clock.Run(world, world.Calendar.HoursPerYear + 1);
        Assert.Empty(world.Technologies); // protótipo em curso: ainda não é tecnologia
        Assert.Equal(before - Pressured.PrototypeResourceCost, city.Stock[Iron]);
        Assert.Contains(world.Scheduler.Snapshot(), e => e.SystemName == TechDiscoverySystem.SystemName);

        clock.Run(world, Pressured.PrototypeTicks);
        Assert.NotEmpty(world.Technologies);
        Assert.Equal(before - world.Technologies.Count * Pressured.PrototypeResourceCost, city.Stock[Iron]);
    }

    [Fact]
    public void Resources_spent_in_the_prototype_are_all_recorded_as_consumed()
    {
        var (world, clock) = Village(3, Pressured);
        var city = world.ActiveCities().First();
        long before = city.Stock[Iron];
        long consumedBefore = world.ResourceConsumed.GetValueOrDefault(Iron);

        clock.Run(world, world.Calendar.HoursPerYear * 2 + Pressured.PrototypeTicks + 1);

        long stockDelta = before - city.Stock[Iron];
        Assert.True(stockDelta > 0);
        Assert.Equal(stockDelta, world.ResourceConsumed.GetValueOrDefault(Iron) - consumedBefore);
    }

    [Fact]
    public void Without_enough_stock_no_prototype_starts()
    {
        var (world, clock) = ScenarioRunner.Create(2, emergenceRules: Pressured);
        world.ActiveCities().First().DepositStock(Iron, Pressured.PrototypeResourceCost - 1);

        clock.Run(world, world.Calendar.HoursPerYear * 2);

        Assert.Empty(world.Technologies);
        Assert.DoesNotContain(world.Scheduler.Snapshot(), e => e.SystemName == TechDiscoverySystem.SystemName);
    }

    [Fact]
    public void Same_seed_gives_the_same_vectors_and_another_seed_does_not()
    {
        string Run(ulong seed)
        {
            var (world, clock) = Village(seed, Pressured);
            clock.Run(world, world.Calendar.HoursPerYear * 2);
            return Keys(world);
        }

        string a = Run(4);
        Assert.NotEmpty(a);
        Assert.Equal(a, Run(4));
        Assert.NotEqual(a, Run(5));
    }

    [Fact]
    public void Vector_below_the_novelty_threshold_is_a_variation_and_above_it_a_new_entity()
    {
        var (world, _) = Village(6, Pressured);
        var city = world.ActiveCities().First();
        var culture = new CultureId(1);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, new BufferingWorldEventSink());
        var system = new TechDiscoverySystem();
        void Conclude(string vector) => system.HandleEvent(
            world, ctx, new ScheduledEvent(1, 1, TechDiscoverySystem.SystemName, $"{city.Id.Value}|{culture.Id}|{vector}"));

        Conclude("3.3.3");
        Conclude("3.3.4"); // distância 1 < 3
        Conclude("7.7.7"); // distância 12 >= 3

        Assert.Equal([false, true, false], world.Technologies.Select(t => t.IsVariation));
        Assert.Equal("3.3.3", world.Technologies[1].VariationOfKey);
        Assert.Equal(2, world.Technologies.Count(t => !t.IsVariation));
    }

    [Fact]
    public void Technology_is_identified_by_vector_only_never_by_a_name()
    {
        Assert.Equal(new TechnologyVector([1, 2, 3]), new TechnologyVector([1, 2, 3]));
        Assert.NotEqual(new TechnologyVector([1, 2, 3]), new TechnologyVector([1, 2, 4]));
        Assert.Equal(1, new TechnologyVector([1, 2, 3]).DistanceTo(new TechnologyVector([1, 2, 4])));
        Assert.DoesNotContain(typeof(Technology).GetProperties(), p => p.Name.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public void An_adopted_technology_shortens_the_trip_and_so_the_pantry_the_household_keeps()
    {
        var (world, _) = ScenarioRunner.Create(7, emergenceRules: Pressured);
        var buyer = world.Npcs.First(n => n.Household is not null);
        var household = world.FindHousehold(buyer.Household!.Value)!;
        var index = MarketIndex.BuildForTick(world);
        double before = BehaviorDecisionSystem.ProvisionTargetFor(world, buyer, index);

        world.AddTechnology(new Technology(
            buyer.Culture, EmergenceRules.MobilityCategory, new TechnologyVector([7, 0, 0]), 0, household.City, null, [household.City]));
        double after = BehaviorDecisionSystem.ProvisionTargetFor(world, buyer, index);

        Assert.True(Mobility.BonusPct(world, household.City) > 0);
        Assert.True(after <= before);
        Assert.Equal(100, Mobility.Apply(100, 0));
        Assert.Equal(50, Mobility.Apply(100, 100));
    }

    [Fact]
    public void A_technology_spreads_to_a_city_of_the_same_culture_through_a_strong_tie()
    {
        var (world, clock) = ScenarioRunner.Create(8, initialPopulation: 0, emergenceRules: Pressured);
        var home = world.ActiveCities().First();
        var other = new LivingWorld.Domain.Cities.City(
            world.NextCityId(), new CellCoord(8, 8), 0, null, LivingWorld.Domain.Cities.AggregatePopulationPool.Empty);
        world.AddCity(other);
        var a = NewNpc(world, home.Id);
        var b = NewNpc(world, other.Id);
        world.AddTechnology(new Technology(
            a.Culture, EmergenceRules.MobilityCategory, new TechnologyVector([4, 4, 4]), 0, home.Id, null, [home.Id]));

        clock.Run(world, world.Calendar.HoursPerYear + 1);
        Assert.Single(world.Technologies[0].AdoptedBy); // sem vínculo, não espalha

        var tie = world.GetOrCreateRelationship(new RelationshipKey(b.Id, a.Id), 0);
        tie.CopyAxesFrom(Relationship.FromAxes(60, 60, 0, 0, 0));
        clock.Run(world, world.Calendar.HoursPerYear);

        Assert.Contains(other.Id, world.Technologies[0].AdoptedBy);
    }

    private static Npc NewNpc(WorldState world, CityId city)
    {
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "n", Sex.Male, WorldDate.Epoch(world.Calendar).AddYears(-30), new CultureId(1),
            new CellCoord(1, 1), motherId: null, fatherId: null, household: null, health: 100, personality: personality,
            profession: new ProfessionType(1), currentLocation: new CellCoord(1, 1), wallet: new Money(0), city: city);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void Discovery_is_logged_without_a_name_and_changes_the_canonical_hash()
    {
        var (on, _) = Village(9, Pressured);
        var (off, clockOff) = Village(9, EmergenceRules.Disabled);
        var sink = new BufferingWorldEventSink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);

        clock.Run(on, on.Calendar.HoursPerYear * 2);
        clockOff.Run(off, off.Calendar.HoursPerYear * 2);

        Assert.Contains(sink.DrainAll(), e => e.Kind == WorldEventKind.TechnologyDiscovered);
        Assert.NotEqual(WorldSnapshot.CanonicalHash(off), WorldSnapshot.CanonicalHash(on));
        Assert.NotEmpty(on.Technologies);
    }

    [Fact]
    public void Snapshot_round_trip_keeps_rules_and_technologies()
    {
        var (world, clock) = Village(2, Pressured);
        clock.Run(world, world.Calendar.HoursPerYear * 2);
        Assert.NotEmpty(world.Technologies);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(Keys(world), Keys(restored));
        Assert.Equal(world.EmergenceRules.Enabled, restored.EmergenceRules.Enabled);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Isolated_cultures_solving_the_same_problem_diverge_in_at_least_half_of_the_seeds()
    {
        int different = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            string First(ulong s)
            {
                var (world, clock) = Village(s, Pressured);
                clock.Run(world, world.Calendar.HoursPerYear * 2);
                return world.Technologies.FirstOrDefault(t => !t.IsVariation)?.Vector.Key ?? "";
            }

            string a = First(seed), b = First(seed + 1000);
            Assert.NotEqual("", a);
            if (a != b) different++;
        }

        Assert.True(different >= 5, $"só {different}/10 seeds divergiram");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Two_isolated_villages_keep_discovering_for_30_years_and_end_with_different_technology_sets()
    {
        int different = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            string Technologies(ulong s)
            {
                var (world, clock) = Village(s, Pressured);
                clock.Run(world, world.Calendar.HoursPerYear * 30);
                Assert.True(world.Technologies.Count >= 3, "a vila deveria seguir descobrindo por 30 anos");
                return string.Join(";", world.Technologies.Where(t => !t.IsVariation).Select(t => t.Vector.Key));
            }

            if (Technologies(seed) != Technologies(seed + 1000)) different++;
        }

        Assert.True(different >= 2, $"só {different}/3 pares divergiram em 30 anos");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void A_village_under_long_trip_pressure_discovers_deterministically_for_20_years_in_10_seeds()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var (world, clock) = Village(seed, Pressured);
            clock.Run(world, world.Calendar.HoursPerYear * 20);
            var (again, clockAgain) = Village(seed, Pressured);
            clockAgain.Run(again, again.Calendar.HoursPerYear * 20);

            Assert.NotEmpty(world.Technologies);
            Assert.Equal(Keys(world), Keys(again));
            Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(again));
        }
    }

    private static string DefaultScenarioWith(string? emergenceBlock)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(FindScenario("default.json")))!.AsObject();
        root.Remove("Emergence");
        if (emergenceBlock is not null) root["Emergence"] = System.Text.Json.Nodes.JsonNode.Parse(emergenceBlock);
        return root.ToJsonString();
    }

    private static string FindScenario(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scenarios", file))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "scenarios", file);
    }

    [Fact]
    public void The_scenario_declares_axes_and_novelty_threshold_and_the_loader_hands_them_to_the_world()
    {
        var withBlock = ScenarioLoaderV2.LoadWorld(DefaultScenarioWith(
            """{ "Enabled": true, "AxisNames": ["pace", "load"], "AxisMax": 5, "NoveltyThreshold": 2, "PrototypeResourceCost": 7 }"""));
        var without = ScenarioLoaderV2.LoadWorld(DefaultScenarioWith(null));

        Assert.True(withBlock.IsSuccess, withBlock.Error);
        var rules = withBlock.Value.World.EmergenceRules;
        Assert.True(rules.Enabled);
        Assert.Equal(["pace", "load"], rules.AxisNames);
        Assert.Equal(5, rules.AxisMax);
        Assert.Equal(2, rules.NoveltyThreshold);
        Assert.Equal(7, rules.PrototypeResourceCost);
        Assert.Equal(EmergenceRules.Mobility.PrototypeTicks, rules.PrototypeTicks); // campo ausente usa o padrão
        Assert.True(without.IsSuccess, without.Error);
        Assert.False(without.Value.World.EmergenceRules.Enabled);
    }

    [Theory]
    [InlineData("""{ "AxisNames": ["pace"] }""")]
    [InlineData("""{ "Enabled": true, "AxisNames": [] }""")]
    [InlineData("""{ "Enabled": true, "AxisNames": ["a", "a"] }""")]
    [InlineData("""{ "Enabled": true, "NoveltyThreshold": 0 }""")]
    [InlineData("""{ "Enabled": true, "AxisMax": "alto" }""")]
    [InlineData("""{ "Enabled": true, "PrototypeTicks": -5 }""")]
    public void An_invalid_emergence_block_is_rejected_at_the_border(string block)
    {
        var result = ScenarioLoaderV2.LoadWorld(DefaultScenarioWith(block));

        Assert.False(result.IsSuccess);
        Assert.Contains("Emergence", result.Error, StringComparison.Ordinal);
    }
}
