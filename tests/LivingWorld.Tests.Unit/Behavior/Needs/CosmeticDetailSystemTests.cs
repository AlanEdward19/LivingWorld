using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior.Needs;

/// <summary>Fase 28, T5 (LOD-10..12): <see cref="CosmeticDetailSystem"/> — posição lazy vs.
/// exata, promoção com fórmula fechada, RNG <c>StreamFor("cosmetic")</c> e exclusividade de
/// camadas.</summary>
public class CosmeticDetailSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static (WorldState World, Npc Npc, ObservationRegistry Registry, CosmeticDetailSystem System) BuildFixture(
        ulong seed = 28,
        CellCoord location = default)
    {
        var world = ScenarioRunner.Create(seed: seed, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), location, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: location,
            city: city.Id);

        world.AddNpc(npc);

        var registry = new ObservationRegistry();
        var system = new CosmeticDetailSystem(registry);
        return (world, npc, registry, system);
    }

    [Fact]
    public void ResolvePosition_when_observed_returns_exact_current_location()
    {
        var (world, npc, registry, system) = BuildFixture();
        registry.SetScope("client", SpaceScope.World());
        long tick = 42;
        npc.MoveTo(new CellCoord(7, 9), tick);

        var position = system.ResolvePosition(npc, world, tick);

        Assert.Equal(new Position(7, 9), position);
        Assert.Equal(CosmeticDetailLayer.FullDetail, system.TryGetState(npc.Id, out var state) ? state.Layer : default);
    }

    [Fact]
    public void ResolvePosition_when_not_observed_uses_lazy_position_formula()
    {
        var (world, npc, _, system) = BuildFixture();
        var route = MovementRoute.StepPath(
            [new CellCoord(0, 0), new CellCoord(5, 0), new CellCoord(10, 0)],
            startedAtTick: 100,
            ticksPerCell: 1);
        var routeId = new RouteId(1);
        system.RegisterRoute(routeId, route);
        system.SetLazyPosition(npc.Id, new LazyPosition(new Position(0, 0), 100, routeId));

        var position = system.ResolvePosition(npc, world, tick: 101);

        Assert.Equal(new Position(5, 0), position);
        Assert.NotEqual(npc.CurrentLocation, (CellCoord)position);
    }

    [Fact]
    public void OnPromoted_recalculates_position_from_closed_formula_exactly()
    {
        var (world, npc, registry, system) = BuildFixture();
        var route = MovementRoute.ArriveAt(
            new CellCoord(1, 1), new CellCoord(9, 9), startedAtTick: 10, arrivalTick: 20);
        var routeId = new RouteId(2);
        system.RegisterRoute(routeId, route);
        system.SetLazyPosition(npc.Id, new LazyPosition(new Position(1, 1), 10, routeId));

        registry.SetScope("client", SpaceScope.World());
        system.OnPromoted(npc, world, tick: 25);

        Assert.Equal(new CellCoord(9, 9), npc.CurrentLocation);
        Assert.True(system.TryGetState(npc.Id, out var state));
        Assert.Equal(CosmeticDetailLayer.FullDetail, state.Layer);
        Assert.Equal(new Position(9, 9), state.LazyPosition.LastKnown);
    }

    [Fact]
    public void Promotion_and_demotion_never_keep_both_layers_active()
    {
        var (world, npc, registry, system) = BuildFixture();
        registry.SetScope("client", SpaceScope.World());
        system.EnsureNpc(npc, world, tick: 0);

        Assert.True(system.TryGetState(npc.Id, out var observed));
        Assert.Equal(CosmeticDetailLayer.FullDetail, observed.Layer);

        registry.ClearScope("client");
        system.SyncObservation(npc, world, tick: 1);

        Assert.True(system.TryGetState(npc.Id, out var demoted));
        Assert.Equal(CosmeticDetailLayer.Approximate, demoted.Layer);

        registry.SetScope("client", SpaceScope.World());
        system.SyncObservation(npc, world, tick: 2);

        Assert.True(system.TryGetState(npc.Id, out var promoted));
        Assert.Equal(CosmeticDetailLayer.FullDetail, promoted.Layer);
        Assert.NotEqual(CosmeticDetailLayer.Approximate, promoted.Layer);
    }

    [Fact]
    public void Lazy_period_does_not_consume_cosmetic_rng_until_promotion()
    {
        var (world, npc, registry, system) = BuildFixture(seed: 99);
        system.EnsureNpc(npc, world, tick: 0, pendingMicroAction: true, optionCount: 5);
        system.RequestMicroAction(npc.Id, optionCount: 5);

        for (long tick = 1; tick <= 10; tick++)
            system.SyncObservation(npc, world, tick);

        Assert.True(system.TryGetState(npc.Id, out var lazy));
        Assert.Equal(CosmeticDetailLayer.Approximate, lazy.Layer);
        Assert.Null(lazy.MicroActionChoice);
        Assert.Equal(10, lazy.LazyTicksWhilePendingMicroAction);

        registry.SetScope("client", SpaceScope.World());
        system.SyncObservation(npc, world, tick: 11);

        Assert.True(system.TryGetState(npc.Id, out var promoted));
        Assert.NotNull(promoted.MicroActionChoice);
    }

    [Fact]
    public void Late_promotion_matches_always_observed_micro_action_choice_for_same_seed()
    {
        const ulong seed = 77;
        const int optionCount = 4;
        const long promoteAt = 60;

        int AlwaysObservedChoice()
        {
            var (world, npc, registry, system) = BuildFixture(seed);
            registry.SetScope("client", SpaceScope.World());
            system.EnsureNpc(npc, world, tick: 0, pendingMicroAction: true, optionCount);
            system.RequestMicroAction(npc.Id, optionCount);

            for (long tick = 0; tick <= promoteAt; tick++)
                system.SyncObservation(npc, world, tick);

            return system.TryGetState(npc.Id, out var state) ? state.MicroActionChoice!.Value : -1;
        }

        int LatePromotionChoice()
        {
            var (world, npc, registry, system) = BuildFixture(seed);
            system.EnsureNpc(npc, world, tick: 0, pendingMicroAction: true, optionCount);
            system.RequestMicroAction(npc.Id, optionCount);

            for (long tick = 0; tick < promoteAt; tick++)
                system.SyncObservation(npc, world, tick);

            registry.SetScope("client", SpaceScope.World());
            system.SyncObservation(npc, world, promoteAt);

            return system.TryGetState(npc.Id, out var state) ? state.MicroActionChoice!.Value : -1;
        }

        Assert.Equal(AlwaysObservedChoice(), LatePromotionChoice());
    }

    [Fact]
    public void Late_promotion_position_matches_always_observed_route_at_same_tick()
    {
        const ulong seed = 55;
        const long compareAt = 40;

        var route = MovementRoute.StepPath(
            [new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0), new CellCoord(3, 0)],
            startedAtTick: 0,
            ticksPerCell: 1);
        var routeId = new RouteId(9);

        Position AlwaysObservedPosition()
        {
            var (world, npc, registry, system) = BuildFixture(seed);
            system.RegisterRoute(routeId, route);
            system.SetLazyPosition(npc.Id, new LazyPosition(new Position(0, 0), 0, routeId));
            registry.SetScope("client", SpaceScope.World());

            for (long tick = 0; tick <= compareAt; tick++)
                system.SyncObservation(npc, world, tick);

            return system.ResolvePosition(npc, world, compareAt);
        }

        Position LatePromotionPosition()
        {
            var (world, npc, registry, system) = BuildFixture(seed);
            system.RegisterRoute(routeId, route);
            system.SetLazyPosition(npc.Id, new LazyPosition(new Position(0, 0), 0, routeId));

            for (long tick = 0; tick < compareAt; tick++)
                system.SyncObservation(npc, world, tick);

            registry.SetScope("client", SpaceScope.World());
            system.SyncObservation(npc, world, compareAt);

            return system.ResolvePosition(npc, world, compareAt);
        }

        Assert.Equal(AlwaysObservedPosition(), LatePromotionPosition());
    }

    [Fact]
    public void StreamFor_uses_cosmetic_purpose_and_npc_id()
    {
        var (world, npc, _, system) = BuildFixture(seed: 12);
        system.EnsureNpc(npc, world, tick: 0, pendingMicroAction: true, optionCount: 3);
        system.RequestMicroAction(npc.Id, optionCount: 3);

        int fromSystem = CosmeticDetailSystem.RollMicroAction(world.Rng, npc.Id, drawCount: 3, optionCount: 3);
        int direct = CosmeticDetailSystem.RollMicroAction(world.Rng, npc.Id, drawCount: 3, optionCount: 3);

        Assert.Equal(fromSystem, direct);

        var otherNpc = new NpcId(npc.Id.Value + 1);
        int other = CosmeticDetailSystem.RollMicroAction(world.Rng, otherNpc, drawCount: 3, optionCount: 3);
        Assert.NotEqual(fromSystem, other);
    }

    [Fact]
    public void ResolvePosition_when_observed_inside_building_uses_interior_local_cell()
    {
        var (world, npc, registry, system) = BuildFixture();
        var building = new Building(new BuildingId(1), npc.City, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(building);
        npc.EnterBuilding(building.Id, FloorLevel.Ground, new CellCoord(4, 6));
        registry.SetScope("client", SpaceScope.Building(npc.City, building.Id));

        var position = system.ResolvePosition(npc, world, tick: 5);

        Assert.Equal(new Position(4, 6), position);
    }
}
