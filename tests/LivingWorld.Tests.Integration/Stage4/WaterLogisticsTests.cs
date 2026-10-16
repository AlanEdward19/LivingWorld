using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>Fase 15.1, Stage 4, T15 (LWV-03.4/LWV-06): cadeia de água
/// travel→collect→carry→deliver — sem uso remoto, rota/fonte ausente bloqueia, quantidade
/// conservada, progresso visível e replayável.</summary>
public class WaterLogisticsTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly ResourceType Water = new(2);
    private static readonly GeographyCatalog Geo = new(
        TerrainIds: new HashSet<int> { 1 }, BiomeIds: new HashSet<int> { 1 }, ResourceIds: new HashSet<int>());
    private static readonly EconomyRules Economy = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static WorldMap Map(bool withWater)
    {
        var cost = new CostWeights(Base: 2.5, AltitudeWeight: 0, TerrainWeight: new Dictionary<int, double> { [1] = 1.0 });
        var cells = new List<MapCell>
        {
            new(new CellCoord(0, 0), new TerrainType(1), new BiomeType(1), 0, false, []),
            new(new CellCoord(1, 0), new TerrainType(1), new BiomeType(1), 0, withWater, []),
        };
        return WorldMap.Create(2, 1, 1, Geo, cost, cells, RegionGrid.Partition(2, 1, 2), []).Value!;
    }

    private static (WorldState World, Npc Npc, Household Household) Build(bool withWater, int thirst = 0)
    {
        var world = new WorldState(
            Calendar, 15, Map(withWater), ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: Economy);
        var home = new CellCoord(0, 0);
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "porter", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            home, null, null, world.NextHouseholdIdAndAdvance(), 100, Neutral, ProfessionType.None, home, thirst: thirst);
        var household = new Household(npc.Household!.Value, home, npc.Id, [npc.Id]);
        world.AddNpc(npc);
        world.AddHousehold(household);
        return (world, npc, household);
    }

    private static void Finish(WorldState world, ResourceProcess process)
    {
        world.CurrentDate = new WorldDate(Calendar, process.CompletesAtTick);
        new ResourceProcessSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
    }

    [Fact]
    public void Missing_water_source_blocks_collection()
    {
        var (world, npc, _) = Build(withWater: false);

        var source = WaterLogistics.NearestSource(world.Map, npc.CurrentLocation);
        var collected = WaterLogistics.Collect(world, npc, now: 0);

        Assert.Contains("source", source.Error);
        Assert.Contains("source", collected.Error);
        Assert.False(npc.IsCarrying);
    }

    [Fact]
    public void Missing_route_does_not_teleport_or_deliver()
    {
        var (world, npc, household) = Build(withWater: true);
        var unreachable = new CellCoord(9, 9);

        Assert.False(MapPathfinder.ShortestCost(world.Map, npc.CurrentLocation, unreachable).IsSuccess);

        var delivered = WaterLogistics.Deliver(world, npc, now: 0);

        Assert.Equal(new CellCoord(0, 0), npc.CurrentLocation);
        Assert.Contains("carry", delivered.Error);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Water));
    }

    [Fact]
    public void Remote_collection_does_not_fill_household_stock()
    {
        var (world, npc, household) = Build(withWater: true);

        var collected = WaterLogistics.Collect(world, npc, now: 0);

        Assert.Contains("source", collected.Error);
        Assert.Equal(new CellCoord(0, 0), npc.CurrentLocation);
        Assert.False(npc.IsCarrying);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Water));
    }

    [Fact]
    public void Travel_collect_carry_deliver_conserves_quantity()
    {
        var (world, npc, household) = Build(withWater: true);
        var source = new CellCoord(1, 0);
        long ticksNeeded = TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, source);

        world.CurrentDate = new WorldDate(Calendar, ticksNeeded - 1);
        Assert.Equal(new CellCoord(0, 0), npc.CurrentLocation);
        Assert.False(WaterLogistics.Collect(world, npc, world.CurrentDate.TotalHours).IsSuccess);

        npc.MoveTo(source, ticksNeeded);
        var collect = WaterLogistics.Collect(world, npc, ticksNeeded);
        Assert.True(collect.IsSuccess);
        Finish(world, collect.Value!);

        Assert.True(npc.IsCarrying);
        Assert.Equal(1, npc.CarriedQuantity);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Water));

        npc.MoveTo(household.Location, world.CurrentDate.TotalHours);
        var deliver = WaterLogistics.Deliver(world, npc, world.CurrentDate.TotalHours);
        Assert.True(deliver.IsSuccess);
        Finish(world, deliver.Value!);

        Assert.False(npc.IsCarrying);
        Assert.Equal(1, household.Stock[Water]);
    }

    [Fact]
    public void Water_at_the_source_cannot_be_drunk_until_delivered()
    {
        var (world, npc, household) = Build(withWater: true, thirst: 0);
        npc.MoveTo(new CellCoord(1, 0), 0);
        var collect = WaterLogistics.Collect(world, npc, 0).Value!;
        Finish(world, collect);
        npc.SetCurrentAction(ActionType.Eat, 0);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, 1);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        int hours = world.ActionCatalog.MaxDurationHours[ActionType.Eat] + 1;
        for (int i = 0; i < hours; i++)
            clock.Tick(world);

        Assert.Equal(0, npc.Thirst);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Water));
        Assert.True(npc.IsCarrying);
    }

    [Fact]
    public void Delivered_household_water_satisfies_thirst_when_eating_at_home()
    {
        var (world, npc, household) = Build(withWater: true, thirst: 0);
        npc.MoveTo(new CellCoord(1, 0), 0);
        var collect = WaterLogistics.Collect(world, npc, 0).Value!;
        Finish(world, collect);
        npc.MoveTo(household.Location, world.CurrentDate.TotalHours);
        var deliver = WaterLogistics.Deliver(world, npc, world.CurrentDate.TotalHours).Value!;
        Finish(world, deliver);
        Assert.Equal(1, household.Stock[Water]);

        npc.SetCurrentAction(ActionType.Eat, world.CurrentDate.TotalHours);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        var clock = new WorldClock([new BehaviorDecisionSystem()]);
        int hours = world.ActionCatalog.MaxDurationHours[ActionType.Eat] + 1;
        for (int i = 0; i < hours; i++)
            clock.Tick(world);

        Assert.Equal(100, npc.Thirst);
        Assert.Equal(0, household.Stock.GetValueOrDefault(Water));
        Assert.False(npc.IsCarrying);
    }

    [Fact]
    public void Collect_process_projects_progress_with_a_friendly_descriptor()
    {
        var (world, npc, _) = Build(withWater: true);
        npc.MoveTo(new CellCoord(1, 0), 0);
        var collect = WaterLogistics.Collect(world, npc, 0).Value!;
        world.CurrentDate = new WorldDate(Calendar, 0);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);

        var state = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, ""));
        var process = Assert.Single(state.Processes, item => item.Kind == "water");

        Assert.Equal("collect-water", process.DescriptorKey);
        Assert.DoesNotContain("CollectWater", process.DescriptorKey);
        Assert.Equal(npc.CurrentLocation, process.Location);
        Assert.True(process.RemainingHours >= 0);
    }

    [Fact]
    public void Water_process_delta_replays_to_the_fresh_projection()
    {
        var (world, npc, _) = Build(withWater: true);
        npc.MoveTo(new CellCoord(1, 0), 0);
        WaterLogistics.Collect(world, npc, 0);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var scope = new VisualScope(VisualScopeKind.World, "");

        var before = LivingScopeProjector.Build(world, scope);
        world.CurrentDate = new WorldDate(Calendar, 1);
        var after = LivingScopeProjector.Build(world, scope);
        var replayed = LivingDeltaReducer.Apply(before, ScopeDeltaBuilder.Diff(1, before, after));

        Assert.Equal(after, replayed);
    }
}
