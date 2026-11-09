using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
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
        var (world, npc) = WorldWith("movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap());
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
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(), costs: ["carrier.sleep:7"]);
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

    [Fact]
    public void Movement_cost_above_the_balance_blocks_the_step_and_debits_nothing()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(), costs: ["carrier.hunger:11"]);
        npc.SetHunger(10, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, new CellCoord(0, 0), 10), (result.Moved, npc.CurrentLocation, npc.HungerAt(0)));
    }

    [Fact]
    public void Movement_costs_on_the_same_stat_are_summed_against_the_balance()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: ["carrier.thirst:6", "carrier.thirst:6"]);
        npc.SetThirst(10, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, 10), (result.Moved, npc.ThirstAt(0)));
    }

    [Fact]
    public void Movement_cost_equal_to_the_balance_is_paid_and_each_stat_is_debited_independently()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: ["carrier.hunger:50", "carrier.thirst:4", "carrier.social:3", "carrier.health:2"]);
        npc.SetHunger(50, 0);
        npc.SetThirst(60, 0);
        npc.SetSleep(70, 0);
        npc.SetSocial(80, 0);
        npc.SetHealth(90);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((true, 0, 56, 70, 77, 88),
            (result.Moved, npc.HungerAt(0), npc.ThirstAt(0), npc.SleepAt(0), npc.SocialAt(0), npc.Health));
    }

    [Theory]
    [InlineData(2, true, 3L)]
    [InlineData(6, false, 5L)]
    public void Movement_cost_in_household_stock_is_debited_only_when_the_stock_covers_it(
        int cost, bool moves, long stockAfter)
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: [$"household.resource.9:{cost}"]);
        var household = new Household(
            new HouseholdId(1), new CellCoord(0, 0), npc.Id, [npc.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        npc.JoinHousehold(household.Id);
        world.AddHousehold(household);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((moves, stockAfter), (result.Moved, household.Stock[new ResourceType(9)]));
    }

    [Fact]
    public void Typed_movement_cost_loaded_and_charged_by_invocation_is_also_paid_by_locomotion()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(), costs: ["carrier.hunger:fire:5"]);
        npc.SetHunger(50, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((true, 45), (result.Moved, npc.HungerAt(0)));
    }

    [Fact]
    public void Typed_movement_cost_debits_the_same_scaled_amount_as_the_invocation()
    {
        string[] costs = ["carrier.hunger:fire:5"];
        string[] vulnerabilities = ["fire:3"];
        var (walked, walker) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(), costs: costs, vulnerabilities: vulnerabilities);
        var (invoked, invoker) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(), costs: costs, vulnerabilities: vulnerabilities, mode: "Active");
        walker.SetHunger(50, 0);
        invoker.SetHunger(50, 0);

        ExtraordinaryLocomotion.Advance(
            walked, walker, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(walked, walker));
        var invocation = ExtraordinaryInvocationEngine.Invoke(
            invoked, new TickContext(invoked, invoked.Rng, invoked.Scheduler),
            new ExtraordinaryInvocation(1, invoker.Id, "locomotion", invoker.Id));

        Assert.True(invocation.IsSuccess, invocation.Error);
        Assert.Equal((35, 35), (walker.HungerAt(0), invoker.HungerAt(0)));
    }

    [Fact]
    public void Typed_movement_cost_logs_the_scaled_token_and_untyped_cost_logs_its_declaration()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: ["carrier.hunger:fire:5", "carrier.thirst:+4"], vulnerabilities: ["fire:3"]);
        npc.SetHunger(50, 0);
        npc.SetThirst(50, 0);
        var sink = new RecordingSink();

        ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc),
            new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal(["1|movement|carrier.hunger:15", "1|movement|carrier.thirst:+4"],
            sink.Events.Where(evt => evt.Kind == WorldEventKind.ExtraordinaryCostPaid).Select(evt => evt.Payload));
    }

    [Fact]
    public void Typed_movement_cost_scaled_above_the_balance_blocks_the_step_and_debits_nothing()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: ["carrier.hunger:fire:5"], vulnerabilities: ["fire:3"]);
        npc.SetHunger(14, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, new CellCoord(0, 0), 14), (result.Moved, npc.CurrentLocation, npc.HungerAt(0)));
    }

    [Fact]
    public void Typed_and_untyped_costs_on_the_same_stat_are_summed_against_the_balance()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: ["carrier.thirst:fire:6", "carrier.thirst:6"]);
        npc.SetThirst(10, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, 10), (result.Moved, npc.ThirstAt(0)));
    }

    [Fact]
    public void Typed_movement_cost_in_household_stock_is_debited()
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(),
            costs: ["household.resource.9:fire:2"]);
        var household = new Household(
            new HouseholdId(1), new CellCoord(0, 0), npc.Id, [npc.Id],
            new Dictionary<ResourceType, long> { [new ResourceType(9)] = 5 });
        npc.JoinHousehold(household.Id);
        world.AddHousehold(household);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((true, 3L), (result.Moved, household.Stock[new ResourceType(9)]));
    }

    [Theory]
    [InlineData("carrier.mana:5")]
    [InlineData("carrier.mana:fire:5")]
    [InlineData("carrier.hunger")]
    [InlineData("carrier.hunger:abc")]
    [InlineData("carrier.hunger:0")]
    [InlineData("carrier.hunger:-5")]
    [InlineData("carrier.hunger:7:3")]
    [InlineData("household.resource.abc:1")]
    [InlineData("household.resource.9:2")]
    [InlineData("objeto.desconhecido:1")]
    public void Invalid_or_unpayable_movement_cost_blocks_the_step_and_debits_nothing(string cost)
    {
        var (world, npc) = WorldWith(
            "movement.speed-multiplier:3", new CellCoord(0, 0), FlatMap(), costs: [cost]);
        npc.SetHunger(50, 0);

        var result = ExtraordinaryLocomotion.Advance(
            world, npc, new CellCoord(5, 0), tick: 0, [], ExtraordinaryLocomotion.Resolve(world, npc));

        Assert.Equal((false, new CellCoord(0, 0), 50), (result.Moved, npc.CurrentLocation, npc.HungerAt(0)));
    }

    private static (WorldState World, Npc Npc) WorldWith(
        string effect, CellCoord origin, WorldMap? map = null, bool manifested = true,
        IReadOnlyList<string>? costs = null, IReadOnlyList<string>? vulnerabilities = null, string mode = "Passive")
    {
        var descriptor = new PowerDescriptor(
            "locomotion", "test", [effect], mode, costs ?? [], "Guaranteed", [], vulnerabilities ?? [], [], []);
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

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    // Mapa plano e uniforme: a rota reta esperada não depende dos pesos de custo do cenário default.
    private static WorldMap FlatMap() => Map(_ => 1, size: 8);

    private static WorldMap Map(Func<CellCoord, int> terrainOf, int size = 3)
    {
        var catalog = new GeographyCatalog([1, 2], [], []);
        var cost = new CostWeights(1, 0, new Dictionary<int, double> { [1] = 1, [2] = 5 });
        var cells = new List<MapCell>();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var coord = new CellCoord(x, y);
                cells.Add(MapCell.WithDerivedTemperature(
                    coord, new TerrainType(terrainOf(coord)), default, 0, false, []));
            }
        return WorldMap.Create(size, size, 1, catalog, cost, cells, RegionGrid.Partition(size, size, size), []).Value!;
    }
}
