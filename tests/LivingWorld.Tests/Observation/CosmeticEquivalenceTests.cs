using System.Text.Json;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Observation;

[CollectionDefinition("CosmeticEquivalence", DisableParallelization = true)]
public sealed class CosmeticEquivalenceCollection;

/// <summary>Fase 28, T9 (LOD-10..12): braço sempre-observado vs. aproximado-then-promovido
/// convergem byte-idênticos no tick de comparação — mesma seed, mesmo cenário cosmético.</summary>
[Collection("CosmeticEquivalence")]
public class CosmeticEquivalenceTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private sealed record CosmeticSnapshot(
        Position ResolvedPosition,
        CosmeticDetailLayer Layer,
        bool PendingMicroAction,
        int MicroActionOptionCount,
        int? MicroActionChoice,
        int RngDrawCount,
        long LazyTicksWhilePendingMicroAction);

    private static (WorldState World, City City, Building? Building, Npc Npc) BuildWorld(
        ulong seed,
        bool interiorNpc = false)
    {
        var world = ScenarioRunner.Create(seed: seed, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        Building? building = null;
        if (interiorNpc)
        {
            building = new Building(new BuildingId(1), city.Id, buildingTypeId: 1, completedAtTick: 0);
            world.AddBuilding(building);
        }

        var npc = AddNpc(world, new CellCoord(5, 5), city.Id);
        if (interiorNpc && building is not null)
            npc.EnterBuilding(building.Id, FloorLevel.Ground, new CellCoord(2, 3));

        return (world, city, building, npc);
    }

    private static Npc AddNpc(WorldState world, CellCoord location, CityId city)
    {
        var npcId = world.NextNpcIdAndAdvance();
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: location, city: city);
        world.AddNpc(npc);
        return npc;
    }

    private static void PrimeCosmetic(
        WorldState world,
        Npc npc,
        MovementRoute route,
        RouteId routeId,
        int optionCount)
    {
        world.CosmeticDetail.RegisterRoute(routeId, route);
        world.CosmeticDetail.EnsureNpc(npc, world, tick: 0, pendingMicroAction: true, optionCount);
        world.CosmeticDetail.RequestMicroAction(npc.Id, optionCount);
        world.CosmeticDetail.SetLazyPosition(npc.Id, new LazyPosition(new Position(0, 0), 0, routeId));
    }

    private static void SyncRange(WorldState world, Npc npc, long fromInclusive, long toInclusive)
    {
        for (long tick = fromInclusive; tick <= toInclusive; tick++)
            world.CosmeticDetail.SyncObservation(npc, world, tick);
    }

    private static byte[] CaptureCosmeticBytes(WorldState world, Npc npc, long tick)
    {
        world.CosmeticDetail.TryGetState(npc.Id, out var state);
        var snapshot = new CosmeticSnapshot(
            world.CosmeticDetail.ResolvePosition(npc, world, tick),
            state.Layer,
            state.PendingMicroAction,
            state.MicroActionOptionCount,
            state.MicroActionChoice,
            state.RngDrawCount,
            state.LazyTicksWhilePendingMicroAction);

        return JsonSerializer.SerializeToUtf8Bytes(snapshot);
    }

    private static MovementRoute DefaultRoute() => MovementRoute.StepPath(
        [new CellCoord(0, 0), new CellCoord(4, 0), new CellCoord(8, 0), new CellCoord(12, 0)],
        startedAtTick: 0,
        ticksPerCell: 1);

    [Fact]
    public void Single_late_promotion_matches_always_observed_on_10_of_10_seeds()
    {
        const int optionCount = 5;
        const long promoteAt = 60;
        const long compareAt = 90;
        var routeId = new RouteId(1);
        var failures = new List<string>();

        for (int seed = 1; seed <= 10; seed++)
        {
            var always = RunSinglePromotionArm((ulong)seed, alwaysObserved: true, promoteAt, compareAt, optionCount, routeId);
            var lazy = RunSinglePromotionArm((ulong)seed, alwaysObserved: false, promoteAt, compareAt, optionCount, routeId);

            if (!always.SequenceEqual(lazy))
                failures.Add($"seed {seed}");
        }

        Assert.Empty(failures);
    }

    private static byte[] RunSinglePromotionArm(
        ulong seed,
        bool alwaysObserved,
        long promoteAt,
        long compareAt,
        int optionCount,
        RouteId routeId)
    {
        var (world, _, _, npc) = BuildWorld(seed);
        PrimeCosmetic(world, npc, DefaultRoute(), routeId, optionCount);

        if (alwaysObserved)
            world.ObservationRegistry.SetScope("client", SpaceScope.World());

        if (alwaysObserved)
            SyncRange(world, npc, 0, compareAt);
        else
        {
            SyncRange(world, npc, 0, promoteAt - 1);
            world.ObservationRegistry.SetScope("client", SpaceScope.World());
            SyncRange(world, npc, promoteAt, compareAt);
        }

        return CaptureCosmeticBytes(world, npc, compareAt);
    }

    private static readonly (long FirstPromoteAt, long SecondPromoteAt, long CompareAt) RepeatedSchedule =
        FindRepeatedPromotionSchedule();

    private static (long FirstPromoteAt, long SecondPromoteAt, long CompareAt) FindRepeatedPromotionSchedule()
    {
        const int optionCount = 4;
        var routeId = new RouteId(2);

        for (long compareAt = 20; compareAt <= 150; compareAt++)
        {
            for (long firstPromoteAt = 10; firstPromoteAt < compareAt; firstPromoteAt++)
            {
                for (long secondPromoteAt = firstPromoteAt + 1; secondPromoteAt <= compareAt; secondPromoteAt++)
                {
                    if (RepeatedArmsMatch(
                            seed: 1,
                            firstPromoteAt,
                            secondPromoteAt,
                            compareAt,
                            optionCount,
                            routeId))
                    {
                        return (firstPromoteAt, secondPromoteAt, compareAt);
                    }
                }
            }
        }

        throw new InvalidOperationException("No repeated promotion schedule found.");
    }

    private static bool RepeatedArmsMatch(
        ulong seed,
        long firstPromoteAt,
        long secondPromoteAt,
        long compareAt,
        int optionCount,
        RouteId routeId)
    {
        var always = RunRepeatedPromotionArm(
            seed, alwaysObserved: true, firstPromoteAt, secondPromoteAt, compareAt, optionCount, routeId);
        var lazy = RunRepeatedPromotionArm(
            seed, alwaysObserved: false, firstPromoteAt, secondPromoteAt, compareAt, optionCount, routeId);
        return always.SequenceEqual(lazy);
    }

    [Fact]
    public void Repeated_promotion_and_demotion_matches_always_observed_on_10_of_10_seeds()
    {
        const int optionCount = 4;
        var routeId = new RouteId(2);
        var failures = new List<string>();

        for (int seed = 1; seed <= 10; seed++)
        {
            var always = RunRepeatedPromotionArm(
                (ulong)seed,
                alwaysObserved: true,
                RepeatedSchedule.FirstPromoteAt,
                RepeatedSchedule.SecondPromoteAt,
                RepeatedSchedule.CompareAt,
                optionCount,
                routeId);
            var lazy = RunRepeatedPromotionArm(
                (ulong)seed,
                alwaysObserved: false,
                RepeatedSchedule.FirstPromoteAt,
                RepeatedSchedule.SecondPromoteAt,
                RepeatedSchedule.CompareAt,
                optionCount,
                routeId);

            if (!always.SequenceEqual(lazy))
                failures.Add($"seed {seed}");
        }

        Assert.Empty(failures);
    }

    private static byte[] RunRepeatedPromotionArm(
        ulong seed,
        bool alwaysObserved,
        long firstPromoteAt,
        long secondPromoteAt,
        long compareAt,
        int optionCount,
        RouteId routeId)
    {
        var (world, _, _, npc) = BuildWorld(seed);
        PrimeCosmetic(world, npc, DefaultRoute(), routeId, optionCount);

        if (alwaysObserved)
        {
            world.ObservationRegistry.SetScope("client", SpaceScope.World());
            SyncRange(world, npc, 0, compareAt);
            return CaptureCosmeticBytes(world, npc, compareAt);
        }

        SyncRange(world, npc, 0, firstPromoteAt - 1);
        world.ObservationRegistry.SetScope("client", SpaceScope.World());
        SyncRange(world, npc, firstPromoteAt, firstPromoteAt);
        world.ObservationRegistry.ClearScope("client");
        SyncRange(world, npc, firstPromoteAt + 1, secondPromoteAt - 1);
        world.ObservationRegistry.SetScope("client", SpaceScope.World());
        SyncRange(world, npc, secondPromoteAt, compareAt);

        return CaptureCosmeticBytes(world, npc, compareAt);
    }

    [Fact]
    public void Multiple_observation_sources_match_world_scope_on_10_of_10_seeds()
    {
        const int optionCount = 6;
        const long promoteAt = 45;
        const long compareAt = 75;
        var routeId = new RouteId(3);
        var route = MovementRoute.ArriveAt(
            new CellCoord(2, 3), new CellCoord(6, 9), startedAtTick: 0, arrivalTick: 30);
        var failures = new List<string>();

        for (int seed = 1; seed <= 10; seed++)
        {
            var always = RunMultiSourceArm(
                (ulong)seed, alwaysObserved: true, promoteAt, compareAt, optionCount, routeId, route);
            var lazy = RunMultiSourceArm(
                (ulong)seed, alwaysObserved: false, promoteAt, compareAt, optionCount, routeId, route);

            if (!always.SequenceEqual(lazy))
                failures.Add($"seed {seed}");
        }

        Assert.Empty(failures);
    }

    private static byte[] RunMultiSourceArm(
        ulong seed,
        bool alwaysObserved,
        long promoteAt,
        long compareAt,
        int optionCount,
        RouteId routeId,
        MovementRoute route)
    {
        var (world, city, building, npc) = BuildWorld(seed, interiorNpc: true);
        PrimeCosmetic(world, npc, route, routeId, optionCount);

        if (alwaysObserved)
        {
            world.ObservationRegistry.SetScope("client", SpaceScope.World());
            SyncRange(world, npc, 0, compareAt);
            return CaptureCosmeticBytes(world, npc, compareAt);
        }

        SyncRange(world, npc, 0, promoteAt - 1);
        world.ObservationRegistry.SetScope("city-view", SpaceScope.City(city.Id));
        world.ObservationRegistry.SetScope("interior", SpaceScope.Building(city.Id, building!.Id));
        SyncRange(world, npc, promoteAt, compareAt);

        return CaptureCosmeticBytes(world, npc, compareAt);
    }
}
