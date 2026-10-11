using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Performance;

/// <summary>Provas de que as estruturas de aceleração (índices, memos, caches) são só isso: cada uma
/// é comparada com a definição direta que ela substitui, e o conjunto é preso a hashes do mundo.
/// Os hashes foram gravados no código anterior às otimizações (equivalência provada) e regravados
/// quando o comportamento mudou de propósito (mortalidade condicional, Midshift, custo de célula
/// 0.05, urgência de sobrevivência, abastecimento, rotina com deslocamento antecipado — AD-077/AD-078). Mudar a simulação de propósito exige atualizá-los; qualquer outra quebra é regressão.</summary>
public class OptimizationEquivalenceTests
{
    private const long OneYearInHours = 360L * 24;

    // --- Hashes do mundo preservados ---

    [Theory]
    [InlineData(1UL, "692ef5dc668b5afdbc59df2553183c23fe00e68c44cc84aed378798af93bea57", "33ca0fc50f0f13bbf700fc49499b05b48651c939c4316b08e2d1c2afe2517eb2")]
    [InlineData(2UL, "3df5e078cc8ac9cdfa69773fe725f67a6f6c96f4c18f650210b45b86abd131f2", "4253a9100dbb48f7365a3fae53ecbbdb7d6e792c84dfd90a9995158aeb9d84ff")]
    public void Default_scenario_ninety_days_keeps_the_recorded_hashes(
        ulong seed, string canonical, string volatileHash)
    {
        var (actualCanonical, actualVolatile) = ScenarioRunner.RunAndHash(seed, 90 * 24);

        Assert.Equal(canonical, actualCanonical);
        Assert.Equal(volatileHash, actualVolatile);
    }

    // --- Hash de stream sem alocação ---

    [Fact]
    public void StreamFor_matches_the_named_stream_of_the_interpolated_key_for_any_purpose_and_id()
    {
        var purposes = new[] { "flora-reproduce", "mortality", "natality-1-2", "", "ação-ç", "x-" };
        var ids = new long[] { 0, 1, 9, 10, 99, 100, 123_456_789, 1_000_000L * 7 + 3, long.MaxValue, -1, -42, long.MinValue };

        foreach (var purpose in purposes)
            foreach (var id in ids)
            {
                var direct = new WorldRngRegistry(77).StreamFor(purpose, id);
                var named = new WorldRngRegistry(77).Stream($"{purpose}-{id}");
                Assert.Equal(named.NextDouble(), direct.NextDouble());
                Assert.Equal(named.NextDouble(), direct.NextDouble());
            }
    }

    // --- Mortalidade condicional da população inicial (flag, desligada por padrão) ---

    [Fact]
    public void Conditional_initial_mortality_keeps_the_seeded_population_from_dying_in_the_first_hour()
    {
        int AliveAfterTwoDays(bool conditional)
        {
            var rules = ScenarioRunner.DefaultPopulationRules with { ConditionalInitialMortality = conditional };
            var (world, clock) = ScenarioRunner.Create(1, populationRules: rules);
            clock.Run(world, 48);
            return world.Npcs.Count(n => n.IsAlive);
        }

        int legacy = AliveAfterTwoDays(conditional: false);
        int conditional = AliveAfterTwoDays(conditional: true);

        // Legado: quem recebe idade de morte menor que a atual morre 1 hora depois (~30% da vila).
        Assert.True(legacy <= 80, $"esperava mortes imediatas no comportamento legado (vivos: {legacy})");
        Assert.True(conditional >= 95, $"flag ligada não deveria matar a população inicial (vivos: {conditional})");
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

    // --- Avanço adiado da flora ---

    /// <summary>Referência: o avanço tick a tick de sempre, pelas funções públicas que mantêm essa semântica.</summary>
    private sealed class EagerFloraSystem : ISimulationSystem
    {
        public string Name => FloraLifecycleSystem.SystemName;
        public TickFrequency Frequency => TickFrequency.Hourly;

        public void Tick(WorldState world, TickContext ctx)
        {
            FloraLifecycleSystem.AdvanceStage(world, ctx);
            FloraLifecycleSystem.TryReproduce(world, ctx);
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(3UL)]
    public void Deferred_flora_advance_matches_the_eager_reference_at_every_sampled_read(ulong seed)
    {
        var (lazyWorld, _) = ScenarioRunner.Create(seed);
        var (eagerWorld, _) = ScenarioRunner.Create(seed);
        var lazySink = new BufferingWorldEventSink();
        var eagerSink = new BufferingWorldEventSink();
        var lazyClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: lazySink);
        var eagerClock = new WorldClock(
            ScenarioRunner.DefaultSystems().Select(s => s is FloraLifecycleSystem ? new EagerFloraSystem() : s).ToList(),
            sink: eagerSink);
        var worlds = new[] { lazyWorld, eagerWorld };
        var region = lazyWorld.Map.Regions[0].Id;

        // A flora começa pequena e só lota (~dia 90): daí em diante o caminho adiado trabalha sozinho.
        // Eventos externos depois disso o obrigam a notar mudanças e a sair/voltar do regime de reprodução.
        for (int step = 0; step < 200 * 24; step++)
        {
            lazyClock.Run(lazyWorld, 1);
            eagerClock.Run(eagerWorld, 1);

            if (step == 120 * 24)
                foreach (var world in worlds)
                    world.AddEnvironmentTemperatureAdjustment(
                        new EnvironmentTemperatureAdjustment(region, 40f, world.CurrentDate.TotalHours + 20 * 24));

            if (step == 150 * 24 || step == 170 * 24)
            {
                int toRemove = step == 150 * 24 ? 30 : 100;
                foreach (var world in worlds)
                    foreach (var plant in world.Flora.Take(toRemove).ToList())
                        world.RemovePlant(plant.Id);
            }

            // Leituras em ticks arbitrários (forçam a materialização) e depois longos trechos sem leitura.
            if (step % 53 == 0 || step % 701 == 0 || step % 1913 == 0)
                Assert.Equal(
                    eagerWorld.Flora.Select(p => (p.Id.Value, p.GrowthStage)),
                    lazyWorld.Flora.Select(p => (p.Id.Value, p.GrowthStage)));
        }

        Assert.Equal(WorldSnapshot.CanonicalHash(eagerWorld), WorldSnapshot.CanonicalHash(lazyWorld));
        Assert.Equal(
            eagerSink.DrainAll().Select(e => (e.Tick, e.Kind, e.Payload)),
            lazySink.DrainAll().Select(e => (e.Tick, e.Kind, e.Payload)));
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

    // --- Percepção sem varrer o balde inteiro ---

    private static ActionType ReferencePerception(WorldState world, Npc npc, ActionType candidate, int radius)
    {
        if (radius < 0) return candidate;
        bool threat = false, otherNpc = false;
        foreach (var other in world.Npcs.Where(n => n.IsAlive))
        {
            if (other.Id == npc.Id) continue;
            if (Math.Max(Math.Abs(npc.CurrentLocation.X - other.CurrentLocation.X),
                    Math.Abs(npc.CurrentLocation.Y - other.CurrentLocation.Y)) > radius) continue;
            otherNpc = true;
            if (other.Health <= 1) threat = true;
        }
        if (threat) return ActionType.Travel;
        if (otherNpc && radius > 1) return ActionType.Socialize;
        return candidate;
    }

    [Fact]
    public void Perception_with_early_exits_matches_the_full_scan_for_every_radius_and_health_mix()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        clock.Run(world, 200 * 24);
        var alive = world.Npcs.Where(n => n.IsAlive).ToList();
        Assert.True(alive.Count > 10);

        void AssertAllMatch()
        {
            Assert.Equal(world.Npcs.Count(n => n.IsAlive && n.Health <= 1), world.AliveNpcIndex.EndangeredCount);
            foreach (var npc in alive.Where(n => n.IsAlive))
                foreach (int radius in new[] { -1, 0, 1, 2, 3, 8, 60 })
                    foreach (var candidate in new[] { ActionType.Work, ActionType.Idle })
                        Assert.Equal(
                            ReferencePerception(world, npc, candidate, radius),
                            BehaviorDecisionSystem.ApplyPerceptionWithin(world, npc, candidate, radius));
        }

        AssertAllMatch();                                   // ninguém em perigo
        alive[0].SetHealth(1);                              // um em perigo
        AssertAllMatch();
        alive[1].SetHealth(0);
        alive[2].SetHealth(50);
        AssertAllMatch();                                   // vários
        alive[0].SetHealth(80);                             // recupera
        AssertAllMatch();
        var victim = alive[3];
        victim.SetHealth(1);
        victim.Die(world.CurrentDate);
        world.AliveNpcIndex.OnDied(victim);                 // morrer em perigo sai da conta
        AssertAllMatch();
    }

    // --- Relações conhecidas preguiçosas ---

    [Fact]
    public void Lazy_relationship_facts_match_the_materialized_list_and_the_bond_probe_matches_Any()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        clock.Run(world, 120 * 24);
        Assert.True(world.Relationships.Count > 0, "o cenário precisa ter relações para o teste medir algo");

        int withFacts = 0;
        foreach (var npc in world.Npcs.Where(n => n.IsAlive))
        {
            var materialized = DecisionContextBuilder.MaterializeKnownRelationships(world, npc.Id);
            if (materialized.Length > 0) withFacts++;

            // inclui os próprios valores como mínimo: é neles que "maior ou igual" difere de "maior"
            var minimums = new[] { 0, 60, 100, 101 }
                .Concat(materialized.SelectMany(r => new[] { r.Trust, r.Affection })).Distinct();
            foreach (int minimum in minimums)
            {
                // sonda antes de materializar e depois: os dois caminhos têm de concordar com Any
                var probeFirst = new LazyRelationshipFacts(world, npc.Id);
                bool expected = materialized.Any(r => r.Trust >= minimum || r.Affection >= minimum);
                Assert.Equal(expected, probeFirst.AnyBondAtLeast(minimum));
                Assert.Equal(materialized, probeFirst.ToArray());
                Assert.Equal(expected, probeFirst.AnyBondAtLeast(minimum));
            }
        }
        Assert.True(withFacts > 0);
    }

    [Fact]
    public void Bond_probe_is_exact_at_the_boundary_for_each_axis()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var ids = world.Npcs.Select(n => n.Id).Take(4).ToList();

        // n0→n1: só a confiança chega a 40; n2→n3: só o afeto chega a 40.
        world.GetOrCreateRelationship(new RelationshipKey(ids[0], ids[1]), 0)
            .CopyAxesFrom(Relationship.FromAxes(40, 10, 0, 0, 0));
        world.GetOrCreateRelationship(new RelationshipKey(ids[2], ids[3]), 0)
            .CopyAxesFrom(Relationship.FromAxes(10, 40, 0, 0, 0));

        foreach (var owner in new[] { ids[0], ids[2] })
        {
            Assert.True(new LazyRelationshipFacts(world, owner).AnyBondAtLeast(40));
            Assert.False(new LazyRelationshipFacts(world, owner).AnyBondAtLeast(41));
        }
        Assert.False(new LazyRelationshipFacts(world, ids[1]).AnyBondAtLeast(0));
    }

    [Fact]
    public void Bond_probe_memo_follows_every_change_of_the_relationships_it_summarizes()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var ids = world.Npcs.Select(n => n.Id).Take(4).ToList();
        var owner = ids[0];
        bool Probe(NpcId who) => new LazyRelationshipFacts(world, who).AnyBondAtLeast(60);

        // Fontes criadas antes: copiar eixos não pode contar com o construtor para invalidar o cache.
        var strongAxes = Relationship.FromAxes(70, 0, 0, 0, 0);
        var weakAxes = Relationship.FromAxes(10, 0, 0, 0, 0);
        var affectionAxes = Relationship.FromAxes(0, 65, 0, 0, 0);

        var rel = world.GetOrCreateRelationship(new RelationshipKey(owner, ids[1]), 0);
        rel.CopyAxesFrom(strongAxes);
        Assert.True(Probe(owner));
        Assert.True(Probe(owner));                      // reaproveitada

        rel.CopyAxesFrom(weakAxes);                     // CopyAxesFrom invalida
        Assert.False(Probe(owner));

        var added = world.GetOrCreateRelationship(new RelationshipKey(owner, ids[2]), 0);
        Assert.False(Probe(owner));                     // novo alvo
        added.CopyAxesFrom(affectionAxes);
        Assert.True(Probe(owner));
        Assert.False(Probe(ids[3]));                    // outro dono não herda a resposta

        // Set (via evento de relação): sobe a confiança até cruzar o limiar, sem construir nada novo.
        var rules = world.FamilyRules;
        var type = Enum.GetValues<RelationshipEventType>()
            .First(t => rules.RelationshipEventDelta(t, RelationshipAxis.Trust) > 0);
        double delta = rules.RelationshipEventDelta(type, RelationshipAxis.Trust);
        var eventRel = world.GetOrCreateRelationship(new RelationshipKey(ids[3], ids[0]), 0);
        eventRel.CopyAxesFrom(Relationship.FromAxes(60 - delta, 0, 0, 0, 0));
        Assert.False(Probe(ids[3]));
        eventRel.ApplyEvent(type, rules);
        Assert.True(Probe(ids[3]));
    }

    // --- Busca em anel sem lista ---

    private static CellCoord? ReferenceRingSearch(
        HashSet<CellCoord> occupied, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> shape, int width, int height)
    {
        ulong hash = StableHash.Mix(id.Value);
        for (int radius = 1; radius <= width + height; radius++)
        {
            int minX = bounds.Origin.X - radius, maxX = bounds.Origin.X + bounds.Width - 1 + radius;
            int minY = bounds.Origin.Y - radius, maxY = bounds.Origin.Y + bounds.Height - 1 + radius;
            var ring = new List<CellCoord>();
            for (int x = minX; x <= maxX; x++) { ring.Add(new CellCoord(x, minY)); ring.Add(new CellCoord(x, maxY)); }
            for (int y = minY + 1; y <= maxY - 1; y++) { ring.Add(new CellCoord(minX, y)); ring.Add(new CellCoord(maxX, y)); }
            int offset = (int)(hash % (ulong)ring.Count);
            for (int i = 0; i < ring.Count; i++)
            {
                var origin = ring[(offset + i) % ring.Count];
                var cells = shape.Select(c => new CellCoord(c.X + origin.X, c.Y + origin.Y)).ToList();
                if (cells.All(c => c.X >= 0 && c.X < width && c.Y >= 0 && c.Y < height) && cells.All(c => !occupied.Contains(c)))
                    return origin;
            }
        }
        return null;
    }

    [Fact]
    public void Ring_search_matches_the_list_based_definition_on_crowded_and_edge_maps()
    {
        var shape = new List<CellCoord> { new(0, 0), new(1, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1), new(0, 2), new(1, 2), new(2, 2) };
        var rng = new Random(11);
        for (int trial = 0; trial < 60; trial++)
        {
            int width = 18 + trial % 7, height = 16 + trial % 5;
            var bounds = new CityBounds(new CellCoord(4 + trial % 5, 3 + trial % 4), 4 + trial % 3, 3 + trial % 4);
            var occupied = new HashSet<CellCoord>();
            for (int k = 0; k < 40 + trial * 6; k++) occupied.Add(new CellCoord(rng.Next(-2, width + 2), rng.Next(-2, height + 2)));

            for (long id = 1; id <= 12; id++)
            {
                var expected = ReferenceRingSearch(occupied, bounds, new BuildingId(id), shape, width, height);
                var actual = OverflowPlacer.ResolveOverflowPositionGiven(occupied, bounds, new BuildingId(id), shape, width, height);
                Assert.Equal(expected, actual);
            }
        }
    }

    // --- Rotina em tabela ---

    [Fact]
    public void Routine_table_matches_the_slot_search_for_every_profession_stage_and_hour()
    {
        var catalog = ScenarioRunner.DefaultActionCatalog;
        int hoursPerDay = ScenarioRunner.DefaultCalendar.HoursPerDay;
        var professions = new ProfessionType?[] { null, new ProfessionType(1), new ProfessionType(2), new ProfessionType(99) };

        foreach (var profession in professions)
            foreach (var stage in Enum.GetValues<LivingWorld.Domain.Population.Body.LifeStage>())
                for (int hour = 0; hour < hoursPerDay; hour++)
                {
                    Assert.Equal(
                        catalog.RoutineOf(profession, stage, hour),
                        RoutineBoundaries.RoutineAt(catalog, profession, stage, hour, hoursPerDay));

                    // horas até a rotina mudar: a primeira hora seguinte cuja ação difere, ou o dia todo
                    int expected = 1;
                    while (expected < hoursPerDay
                        && catalog.RoutineOf(profession, stage, (hour + expected) % hoursPerDay) == catalog.RoutineOf(profession, stage, hour))
                        expected++;
                    Assert.Equal(expected, RoutineBoundaries.HoursUntilChange(catalog, profession, stage, hour, hoursPerDay));
                }
    }

    // --- Alcançabilidade por componente conexa ---

    [Fact]
    public void Reachability_by_connected_component_matches_the_shortest_path_search()
    {
        var map = ScenarioRunner.DefaultMap(7);
        var cells = new List<CellCoord>();
        for (int x = 0; x < map.Width; x += 11)
            for (int y = 0; y < map.Height; y += 9)
                cells.Add(new CellCoord(x, y));
        cells.AddRange([new(-1, 0), new(0, -1), new(map.Width, 0), new(0, map.Height), new(999, 999)]);

        foreach (var from in cells)
            foreach (var to in cells)
                Assert.Equal(
                    MapPathfinder.ShortestCost(map, from, to).IsSuccess, MapPathfinder.IsReachable(map, from, to));
    }

    // --- Cama por domicílio ---

    [Fact]
    public void Bed_index_matches_the_linear_scan_with_beds_added_between_queries()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var households = world.Households.Take(3).Select(h => h.Id).ToList();
        long nextId = 1000;

        void AddPlace(RestPlaceKind kind, HouseholdId owner, long id) =>
            world.AddRestPlace(new RestPlace(new RestPlaceId(id), kind, new CellCoord(1, 1), owner));

        void AssertIndexMatchesScan()
        {
            foreach (var household in households)
            {
                var expected = world.RestPlaces
                    .Where(place => place.Kind == RestPlaceKind.Bed && place.OwnerHousehold == household)
                    .OrderBy(place => place.Id.Value)
                    .FirstOrDefault();
                Assert.Same(expected, world.BedOf(household));
            }
        }

        AssertIndexMatchesScan();
        AddPlace(RestPlaceKind.Bed, households[0], nextId + 5);
        AddPlace(RestPlaceKind.Dwelling, households[1], nextId + 1);
        AssertIndexMatchesScan();
        AddPlace(RestPlaceKind.Bed, households[0], nextId + 2); // id menor que a cama já indexada
        AddPlace(RestPlaceKind.Bed, households[1], nextId + 9);
        AddPlace(RestPlaceKind.Bed, households[0], nextId + 7);
        AssertIndexMatchesScan();
        Assert.Equal(nextId + 2, world.BedOf(households[0])!.Id.Value);
        Assert.Null(world.BedOf(households[2]));
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

    [Fact]
    public void Incremental_occupancy_and_boxes_match_the_from_scratch_definition_under_changing_bounds()
    {
        var world = BuildWorldWithMap(width: 60, height: 60, seed: 41);
        var city = new City(world.NextCityId(), new CellCoord(30, 30), 0, null, AggregatePopulationPool.Empty);
        var other = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        world.AddCity(other);

        CellCoord? derived = null;
        BuildingId spare = default;
        Building? visitor = null;
        for (int i = 0; i < 45; i++)
        {
            if (i == 11) visitor!.JoinCity(city.Id);
            if (i == 28) spare = world.NextBuildingIdAndAdvance();
            // Bounds que alternam entre dois valores e crescem, como população × bounds crescidos.
            var bounds = i % 2 == 0
                ? new CityBounds(new CellCoord(26, 26), 8, 8)
                : new CityBounds(new CellCoord(24 - i % 3, 25), 12 + i % 4, 10);

            // Prédio de outra cidade no meio (muda a lista do mundo sem mudar esta cidade).
            if (i % 5 == 2)
                world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), other.Id, 1, 0, new CellCoord(2 + i % 4, 2)));

            // Prédio que troca de cidade no meio (a lista da cidade encolhe e depois volta a crescer).
            if (i == 20) world.Buildings.First(b => b.City == city.Id).JoinCity(other.Id);
            if (i == 38) world.Buildings.First(b => b.City == other.Id && b.Position is { X: < 8 }).JoinCity(city.Id);

            // Autorados, exceto um no meio que depende da busca (derivado); depois dele, com os mesmos
            // bounds, cada autorado cai exatamente em cima da posição que o derivado ocupava, empurrando-o.
            CellCoord? authored = i == 30 ? null : new CellCoord(8 + (i * 3) % 40, 8 + (i * 7) % 40);
            if (i > 30 && i < 38 && derived is { } spot) authored = spot;
            if (i >= 30) bounds = i < 40 ? new CityBounds(new CellCoord(26, 26), 8, 8) : new CityBounds(new CellCoord(20, 20), 16, 16);
            if (i == 33 && derived is { } early)
            {
                // Id menor que o de todos os já posicionados depois dele: entra no meio da ordem por Id.
                world.AddBuilding(new Building(spare, city.Id, 2, 0, early));
            }
            world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, 1 + i % 3, 0, authored));
            if (i == 10)
            {
                // Visitante de outra cidade, o mais novo do mundo; entra na cidade na rodada seguinte com Id
                // maior que todo irmão atual, mas posição ANTIGA na lista do mundo.
                visitor = new Building(world.NextBuildingIdAndAdvance(), other.Id, 1, 0, new CellCoord(50, 50));
                world.AddBuilding(visitor);
            }

            var actualBoxes = CityOccupancy.OwnedBuildingFootprintBoxesWithOwners(world, city, bounds).ToList();
            var expectedBoxes = new List<(Building, CityBounds)>();
            foreach (var building in world.Buildings.Where(b => b.City == city.Id))
            {
                CellCoord? position = building.Position
                    ?? BuildingPlacementResolver.Resolve(building, city, world, bounds)?.Position;
                if (position is null) continue;
                var cells = BuildingFootprintGenerator.Generate(building)
                    .Select(c => new CellCoord(c.Cell.X + position.Value.X, c.Cell.Y + position.Value.Y)).ToList();
                if (cells.Count == 0) continue;
                int minX = cells.Min(c => c.X), minY = cells.Min(c => c.Y);
                expectedBoxes.Add((building, new CityBounds(new CellCoord(minX, minY), cells.Max(c => c.X) - minX + 1, cells.Max(c => c.Y) - minY + 1)));
            }

            Assert.Equal(expectedBoxes, actualBoxes);

            // As caixas vêm antes da ocupação: é a ordem do posicionamento real, em que as caixas do passo
            // anterior ainda estão memoizadas quando o prédio novo chega.
            var placing = new BuildingId(world.Buildings.Max(b => b.Id.Value) + 1);
            Assert.True(
                ReferenceOccupied(world, city, bounds, placing).SetEquals(CityOccupancy.OccupiedCellsOfCity(world, city, bounds, placing)),
                $"ocupação difere na rodada {i}");

            if (i >= 30 && i < 37)
                derived = BuildingPlacementResolver.Resolve(world.Buildings.First(b => b.Position is null && b.City == city.Id), city, world, bounds)?.Position;
        }
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
