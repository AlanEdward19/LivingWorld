using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

public sealed class ExtraordinaryLocomotionTests
{
    [Fact]
    public void Manifested_speed_moves_three_physical_cells_in_one_hour_without_teleporting_to_goal()
    {
        var (world, npc) = WorldWith("movement.speed-multiplier:3", new CellCoord(0, 0));
        var profile = ExtraordinaryLocomotion.Resolve(world, npc);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 1, [], profile);

        Assert.Equal((true, false, 3, new CellCoord(3, 0)),
            (result.Moved, result.Reached, result.Steps, npc.CurrentLocation));
    }

    [Fact]
    public void Manifested_flight_crosses_expensive_terrain_in_a_straight_physical_route()
    {
        var map = Map(coord => coord.X == 1 && coord.Y < 2 ? 2 : 1);
        var (world, npc) = WorldWith("movement.flight:1", new CellCoord(0, 0), map);
        var profile = ExtraordinaryLocomotion.Resolve(world, npc);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(2, 0), tick: 1, [], profile);

        Assert.Equal((true, false, 1, new CellCoord(1, 0)),
            (result.Moved, result.Reached, result.Steps, npc.CurrentLocation));
    }

    [Fact]
    public void Flight_does_not_move_an_npc_through_an_interior_scope()
    {
        var (world, npc) = WorldWith("movement.flight:1", new CellCoord(0, 0));
        npc.EnterBuilding(new BuildingId(1), new FloorLevel(0), new CellCoord(1, 1));
        var profile = ExtraordinaryLocomotion.Resolve(world, npc);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(2, 0), tick: 1, [], profile);

        Assert.Equal((false, false, 0, new CellCoord(0, 0)),
            (result.Moved, result.Reached, result.Steps, npc.CurrentLocation));
    }

    [Fact]
    public void Flight_cannot_land_on_a_construct_occupied_cell()
    {
        var (world, npc) = WorldWith("movement.flight:1", new CellCoord(0, 0));
        var destination = new CellCoord(1, 0);
        world.AddExtraordinaryConstruct(new ExtraordinaryConstruct(
            0, npc.Id, "flight", 1, destination, [destination], 10, 10, 0, 10, "barrier"));

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, destination, tick: 1, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, false, 0, new CellCoord(0, 0)),
            (result.Moved, result.Reached, result.Steps, npc.CurrentLocation));
    }

    [Fact]
    public void Flight_does_not_cross_an_authored_building_footprint()
    {
        var (world, npc) = WorldWith("movement.flight:3", new CellCoord(0, 0));
        var city = new City(
            world.NextCityId(), new CellCoord(1, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        world.AddBuilding(new Building(
            new BuildingId(1), city.Id, -1, 0, position: new CellCoord(1, 0), orientation: 0));

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(4, 0), 1, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, 0, new CellCoord(0, 0)), (result.Moved, result.Steps, npc.CurrentLocation));
    }

    [Fact]
    public void Flight_cannot_land_on_another_npc()
    {
        var (world, npc) = WorldWith("movement.flight:1", new CellCoord(0, 0));
        var occupied = new CellCoord(1, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, occupied, 1, [npc.CurrentLocation, occupied], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, false, new CellCoord(0, 0)),
            (result.Moved, result.Reached, npc.CurrentLocation));
    }

    [Fact]
    public void Dormant_power_produces_no_locomotion_modifier()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:4", new CellCoord(0, 0), manifested: false);

        var profile = ExtraordinaryLocomotion.Resolve(world, npc);

        Assert.Equal((false, false, 1d), (profile.HasModifier, profile.CanFly, profile.SpeedMultiplier));
    }

    [Fact]
    public void Behavior_travel_advances_a_manifested_speedster_through_authoritative_cells()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), costs: ["carrier.sleep:7"]);
        var household = new Household(
            new HouseholdId(1), new CellCoord(5, 0), npc.Id, [npc.Id]);
        npc.JoinHousehold(household.Id);
        world.AddHousehold(household);
        npc.SetCurrentAction(ActionType.Travel, tick: -1);
        SimulationWakeTestHelper.Wake(world, npc);

        new BehaviorDecisionSystem().Tick(
            world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(new CellCoord(3, 0), npc.CurrentLocation);
        Assert.Equal(ActionType.Travel, npc.CurrentAction);
        Assert.Equal(93, npc.SleepAt(world.CurrentDate.TotalHours));
    }

    private static (WorldState World, Npc Npc) WorldWith(
        string effect, CellCoord origin, WorldMap? map = null, bool manifested = true,
        IReadOnlyList<string>? costs = null)
    {
        var descriptor = new PowerDescriptor(
            "locomotion", "test", [effect], "Passive", costs ?? [], "Guaranteed", [], [], [], []);
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], manifested, manifested ? "manifested" : "dormant",
            new ExtraordinaryAppearanceState(1, "", "trail"), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, map ?? ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrier]);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, origin, null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, currentLocation: origin);
        world.AddNpc(npc);
        return (world, npc);
    }

    private static WorldMap Map(Func<CellCoord, int> terrainOf)
    {
        var catalog = new GeographyCatalog([1, 2], [], []);
        var cost = new CostWeights(1, 0, new Dictionary<int, double> { [1] = 1, [2] = 5 });
        var cells = new List<MapCell>();
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var coord = new CellCoord(x, y);
                cells.Add(MapCell.WithDerivedTemperature(
                    coord, new TerrainType(terrainOf(coord)), default, 0, false, []));
            }
        return WorldMap.Create(3, 3, 1, catalog, cost, cells, RegionGrid.Partition(3, 3, 3), []).Value!;
    }
}
