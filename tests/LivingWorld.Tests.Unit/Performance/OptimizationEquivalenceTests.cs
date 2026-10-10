using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Performance;

/// <summary>Provas de que as estruturas de aceleração (índices, memos, caches) são só isso: cada uma
/// é comparada com a definição direta que ela substitui, e o conjunto é preso a hashes do mundo
/// gerados pelo código anterior às otimizações. Mudar o comportamento da simulação de propósito exige
/// atualizar os hashes; qualquer outra mudança que os quebre é regressão.</summary>
public class OptimizationEquivalenceTests
{
    private const long OneYearInHours = 360L * 24;

    // --- Hashes do mundo preservados ---

    [Theory]
    [InlineData(1UL, "a9925574cf9bd8ac71032d74f5482d052f84acbdfae57ebdc4a97fed776ced74", "e15ed8df02767221d83020c9686c7bd45e03f65f4ea392565c8fe754f979c92c")]
    [InlineData(2UL, "eb0721058f1fdb5c772c905f147936a189488b7e0ea0e3fdea32c350171e78c8", "81b929f36b32d3963a3e72161a526a6862becbd365be8bb5a2434546103c286a")]
    public void Default_scenario_ninety_days_keeps_the_hashes_recorded_before_the_optimizations(
        ulong seed, string canonical, string volatileHash)
    {
        var (actualCanonical, actualVolatile) = ScenarioRunner.RunAndHash(seed, 90 * 24);

        Assert.Equal(canonical, actualCanonical);
        Assert.Equal(volatileHash, actualVolatile);
    }

    // --- Índice espacial de NPCs vivos ---

    [Fact]
    public void Alive_npc_spatial_index_matches_a_brute_force_scan_after_movement_and_death()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        clock.Run(world, 200 * 24);

        AssertSpatialIndexMatchesScan(world);

        var movers = world.AliveNpcIndex.Alive.Take(5).ToList();
        for (int i = 0; i < movers.Count; i++)
            movers[i].MoveTo(new CellCoord(2 + i, 3), tick: 1);
        AssertSpatialIndexMatchesScan(world);

        var victim = world.AliveNpcIndex.Alive[0];
        victim.Die(world.CurrentDate);
        world.AliveNpcIndex.OnDied(victim);
        AssertSpatialIndexMatchesScan(world);
        Assert.DoesNotContain(victim, world.AliveNpcIndex.At(victim.CurrentLocation));
    }

    private static void AssertSpatialIndexMatchesScan(WorldState world)
    {
        var alive = world.Npcs.Where(n => n.IsAlive).ToList();
        Assert.Equal(alive.Select(n => n.Id.Value).OrderBy(v => v), world.AliveNpcIndex.Alive.Select(n => n.Id.Value));

        int indexed = 0;
        foreach (var cell in alive.Select(n => n.CurrentLocation).Distinct())
        {
            var expected = alive.Where(n => n.CurrentLocation == cell).Select(n => n.Id.Value).OrderBy(v => v);
            var actual = world.AliveNpcIndex.At(cell).Select(n => n.Id.Value).OrderBy(v => v).ToList();
            Assert.Equal(expected, actual);
            indexed += actual.Count;
        }

        Assert.Equal(alive.Count, indexed);
    }

    // --- Índice de relações por NPC ---

    [Fact]
    public void Relationship_targets_index_is_sorted_and_matches_the_relationships_dictionary()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 2 * OneYearInHours);
        Assert.NotEmpty(world.Relationships);

        AssertRelationshipIndexMatchesDictionary(world);

        var key = world.Relationships.Keys.First();
        world.RemoveRelationshipsBetween(key.From, key.To);
        AssertRelationshipIndexMatchesDictionary(world);
    }

    private static void AssertRelationshipIndexMatchesDictionary(WorldState world)
    {
        foreach (var npc in world.Npcs)
        {
            var expected = world.Relationships.Keys
                .Where(k => k.From == npc.Id)
                .Select(k => k.To.Value)
                .OrderBy(v => v)
                .ToList();
            Assert.Equal(expected, world.RelationshipTargetsOf(npc.Id).Select(t => t.Value));
        }
    }

    // --- Listas ordenadas de flora e fauna ---

    [Fact]
    public void Flora_and_fauna_stay_sorted_by_id_and_in_sync_with_their_lookups()
    {
        var (world, clock) = ScenarioRunner.Create(5);
        clock.Run(world, 300 * 24);

        Assert.NotEmpty(world.Flora);
        Assert.NotEmpty(world.Fauna);
        Assert.Equal(world.Flora.Select(p => p.Id.Value).OrderBy(v => v), world.Flora.Select(p => p.Id.Value));
        Assert.Equal(world.Fauna.Select(a => a.Id.Value).OrderBy(v => v), world.Fauna.Select(a => a.Id.Value));
        Assert.All(world.Flora, plant => Assert.Equal(plant, world.FindPlant(plant.Id)));
        Assert.All(world.Fauna, animal => Assert.Equal(animal, world.FindAnimal(animal.Id)));
    }

    // --- Menor custo memoizado ---

    [Fact]
    public void Memoized_shortest_cost_equals_a_fresh_maps_uncached_result()
    {
        var cached = ScenarioRunner.DefaultMap(11);
        var fresh = ScenarioRunner.DefaultMap(11);
        var start = new CellCoord(1, 1);

        foreach (var goal in new[] { new CellCoord(4, 6), new CellCoord(9, 2), new CellCoord(1, 1) })
        {
            var first = MapPathfinder_ShortestCost(cached, start, goal);
            var second = MapPathfinder_ShortestCost(cached, start, goal);
            var reference = MapPathfinder_ShortestCost(fresh, start, goal);

            Assert.Equal(reference, first);
            Assert.Equal(reference, second);
        }

        Assert.Equal(
            MapPathfinder_ShortestCost(fresh, start, new CellCoord(-5, -5)),
            MapPathfinder_ShortestCost(cached, start, new CellCoord(-5, -5)));
    }

    private static string MapPathfinder_ShortestCost(
        LivingWorld.Domain.Geography.Map.WorldMap map, CellCoord start, CellCoord goal)
    {
        var result = LivingWorld.Domain.Geography.Map.MapPathfinder.ShortestCost(map, start, goal);
        return result.IsSuccess ? $"ok:{result.Value:R}" : $"fail:{result.Error}";
    }

    // --- Campo de temperatura ---

    [Fact]
    public void Temperature_field_equals_the_brute_force_sum_across_ticks_adjustment_changes_and_expiry()
    {
        var world = ScenarioRunner.Create(seed: 21, initialPopulation: 0).World;
        var regions = world.Map.Regions.Take(3).Select(r => r.Id).ToList();
        world.AddEnvironmentTemperatureAdjustment(new EnvironmentTemperatureAdjustment(regions[0], 2.5f, UntilTick: 100));
        world.AddEnvironmentTemperatureAdjustment(new EnvironmentTemperatureAdjustment(regions[0], -0.7f, UntilTick: 250));
        world.AddEnvironmentTemperatureAdjustment(new EnvironmentTemperatureAdjustment(regions[1], 1.1f, UntilTick: long.MaxValue));

        foreach (long tick in new long[] { 0, 1, 99, 100, 101, 249, 250, 251, 5_000 })
        {
            AssertTemperatureMatchesBruteForce(world, tick);

            // Muda a lista no meio da sequência: o memo precisa notar.
            if (tick == 101)
                world.AddEnvironmentTemperatureAdjustment(new EnvironmentTemperatureAdjustment(regions[2], 3.3f, UntilTick: 400));
        }

        AssertTemperatureMatchesBruteForce(world, 100); // tick que volta no tempo
    }

    private static void AssertTemperatureMatchesBruteForce(WorldState world, long tick)
    {
        foreach (var cell in world.Map.Cells)
        {
            float delta = 0;
            var region = world.Map.RegionOf(cell.Coord);
            foreach (var adjustment in world.EnvironmentTemperatureAdjustments)
            {
                if (adjustment.Region != region || adjustment.UntilTick <= tick)
                    continue;
                delta += adjustment.Delta;
            }

            Assert.Equal(cell.Temperature + delta, EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell.Coord, tick));
        }
    }

    // --- Colocação derivada de prédios ---

    [Fact]
    public void Placement_plan_matches_the_ascending_reference_pass_as_buildings_are_added()
    {
        var world = BuildWorldWithMap(width: 40, height: 40, seed: 31);
        var city = new City(world.NextCityId(), new CellCoord(20, 20), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var bounds = new CityBounds(new CellCoord(17, 17), 7, 7);

        // Mistura de autorados e derivados; tipos variados (3x3, 4x4 e L) para exercitar o overflow.
        int[] typeIds = [-1, 1, 2, 3, 7, 14, 4, 5, 1, 2, 3, 6, 8, 7, 14, 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 15, 16, 17];
        for (int i = 0; i < typeIds.Length; i++)
        {
            CellCoord? authored = i % 7 == 3 ? new CellCoord(17 + i % 5, 17 + (i / 5) % 5) : null;
            world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, typeIds[i], 0, authored));

            var buildings = world.Buildings.Where(b => b.City == city.Id).OrderBy(b => b.Id.Value).ToList();
            foreach (var placing in buildings.Select(b => b.Id).Append(new BuildingId(buildings[^1].Id.Value + 1)))
            {
                var expected = ReferenceOccupied(world, city, bounds, placing);
                var actual = CityOccupancy.OccupiedCellsOfCity(world, city, bounds, placing);
                Assert.True(expected.SetEquals(actual), $"ocupação difere (prédios={buildings.Count}, placing={placing.Value})");
            }
        }

        // Resolução final de um prédio novo sem posição: busca a partir do marco == busca desde o raio 1.
        int overflowed = 0;
        for (int round = 0; round < 40; round++)
        {
            // Poucos tipos repetidos: o marco de overflow de cada forma é reaproveitado várias vezes.
            var next = new Building(world.NextBuildingIdAndAdvance(), city.Id, new[] { -1, 3, 4 }[round % 3], 0);
            var shape = BuildingFootprintGenerator.Generate(next).Select(c => c.Cell).ToList();
            var occupied = ReferenceOccupied(world, city, bounds, next.Id);
            var expected = ReferenceScan(occupied, bounds, shape)
                ?? OverflowPlacer.ResolveOverflowPositionGiven(occupied, bounds, next.Id, shape, world.Map.Width, world.Map.Height);

            var resolved = BuildingPlacementResolver.Resolve(next, city, world, bounds);

            Assert.Equal(expected, resolved?.Position);
            if (resolved is { Position: var rp } && !bounds.Contains(rp)) overflowed++;
            world.AddBuilding(new Building(next.Id, city.Id, next.BuildingTypeId, 0, resolved?.Position));
        }

        Assert.True(overflowed >= 20, $"overflow não foi exercitado o bastante ({overflowed}/40)");
    }

    private static readonly GeographyCatalog TinyCatalog = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());

    private static readonly CostWeights TinyCostWeights = new(
        Base: 1.0, AltitudeWeight: 0.5, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });

    private static WorldState BuildWorldWithMap(int width, int height, ulong seed)
    {
        var map = LivingWorld.Domain.Geography.Map.MapGenerator
            .Generate(seed, width, height, Math.Max(width, height), TinyCatalog, TinyCostWeights, [])
            .Value ?? throw new InvalidOperationException("mapa de teste inválido");
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
    }

    /// <summary>Definição direta anterior à memoização: passe ascendente que recomputa tudo.</summary>
    private static HashSet<CellCoord> ReferenceOccupied(WorldState world, City city, CityBounds bounds, BuildingId placingId)
    {
        var occupied = new HashSet<CellCoord>();
        var siblings = world.Buildings
            .Where(b => b.City == city.Id)
            .Where(b => b.Id.Value != placingId.Value)
            .Where(b => b.Position is not null || b.Id.Value < placingId.Value);

        foreach (var building in siblings.OrderBy(b => b.Id.Value))
        {
            var shape = BuildingFootprintGenerator.Generate(building).Select(c => c.Cell).ToList();
            CellCoord? position = building.Position
                ?? ReferenceScan(occupied, bounds, shape)
                ?? OverflowPlacer.ResolveOverflowPositionGiven(occupied, bounds, building.Id, shape, world.Map.Width, world.Map.Height);
            if (position is null) continue;

            foreach (var cell in shape)
                occupied.Add(new CellCoord(cell.X + position.Value.X, cell.Y + position.Value.Y));
        }

        return occupied;
    }

    private static CellCoord? ReferenceScan(HashSet<CellCoord> occupied, CityBounds bounds, IReadOnlyList<CellCoord> shape)
    {
        for (int y = bounds.Origin.Y; y < bounds.Origin.Y + bounds.Height; y++)
            for (int x = bounds.Origin.X; x < bounds.Origin.X + bounds.Width; x++)
            {
                var candidate = shape.Select(c => new CellCoord(c.X + x, c.Y + y)).ToList();
                if (candidate.All(bounds.Contains) && candidate.All(cell => !occupied.Contains(cell)))
                    return new CellCoord(x, y);
            }

        return null;
    }
}
