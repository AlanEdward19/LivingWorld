using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Stage4;

public sealed class LivingDeltaContractTests
{
    private static readonly VisualScope WorldScope = new(VisualScopeKind.World, "");

    [Fact]
    public void Snapshot_contains_current_typed_entities_indicators_and_events()
    {
        var (world, _) = ScenarioRunner.Create(seed: 31, initialPopulation: 1);
        var npc = Assert.Single(world.Npcs);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, 7, 0, position: new CellCoord(6, 6));
        world.AddBuilding(building);
        var evt = new WorldEvent(4, WorldEventKind.Hired, $"{npc.Id.Value}|1");

        var state = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()), [evt]);

        Assert.Equal(new NpcVisual(npc.Id, npc.CurrentLocation, npc.CurrentAction), Assert.Single(state.Npcs));
        Assert.Equal(new BuildingVisual(building.Id, city.Id, 7, new CellCoord(6, 6)), Assert.Single(state.Buildings));
        Assert.Equal(6, state.Indicators.Count);
        Assert.Equal(new NotableVisualEvent(4, WorldEventKind.Hired, "Um habitante começou um novo trabalho"), Assert.Single(state.Events));
    }

    [Fact]
    public void Delta_contains_typed_final_state_upserts_and_removals()
    {
        var npc = new NpcVisual(new NpcId(2), new CellCoord(3, 4), ActionType.Work);
        var city = new CityVisual(new CityId(Guid.Parse("00000000-0000-0000-0000-000000000032")), new CellCoord(5, 5), 8, new CellBounds(3, 3, 5, 5));
        var before = LivingScopeState.Empty with { Npcs = [new NpcVisual(new NpcId(1), new CellCoord(0, 0), null)] };
        var after = LivingScopeState.Empty with { Npcs = [npc], Cities = [city] };

        var delta = ScopeDeltaBuilder.Diff(12, before, after);

        Assert.Equal([npc], delta.NpcUpserts);
        Assert.Equal([new NpcId(1)], delta.NpcRemoved);
        Assert.Equal([city], delta.CityUpserts);
        Assert.Equal(12, delta.Tick);
    }

    [Fact]
    public void Applying_ordered_deltas_reconstructs_the_fresh_projection()
    {
        var first = LivingScopeState.Empty with
        {
            Npcs = [new NpcVisual(new NpcId(1), new CellCoord(0, 0), ActionType.Idle)],
        };
        var second = first with
        {
            Npcs = [new NpcVisual(new NpcId(1), new CellCoord(1, 0), ActionType.Work)],
            Indicators = [new IndicatorUpdate("population", 1)],
        };
        var third = second with
        {
            Npcs = [],
            Events = [new NotableVisualEvent(3, WorldEventKind.Death, "Um habitante faleceu")],
        };

        var replayed = LivingDeltaReducer.Apply(first, ScopeDeltaBuilder.Diff(2, first, second));
        replayed = LivingDeltaReducer.Apply(replayed, ScopeDeltaBuilder.Diff(3, second, third));

        Assert.Equal(third, replayed);
    }

    [Fact]
    public void Applying_the_same_delta_twice_is_idempotent()
    {
        var before = LivingScopeState.Empty;
        var after = before with
        {
            Npcs = [new NpcVisual(new NpcId(9), new CellCoord(2, 2), ActionType.Sleep)],
            Indicators = [new IndicatorUpdate("health", 75)],
        };
        var delta = ScopeDeltaBuilder.Diff(7, before, after);

        var once = LivingDeltaReducer.Apply(before, delta);
        var twice = LivingDeltaReducer.Apply(once, delta);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Replay_gap_requires_a_fresh_snapshot()
    {
        var gateway = new RealtimeGateway(() => 10, retentionPerScope: 2);
        var (_, unsubscribe) = gateway.SubscribeChannel(WorldScope);
        gateway.Publish(WorldScope, ScopeTickDelta.Empty(8));
        gateway.Publish(WorldScope, ScopeTickDelta.Empty(9));
        gateway.Publish(WorldScope, ScopeTickDelta.Empty(10));

        var replay = gateway.ReplayState(WorldScope, ViewerMode.Spectator, new VisualCursor(7, "world", 0));

        Assert.True(replay.Value!.RequiresSnapshot);
        Assert.Empty(replay.Value.Deltas);
        unsubscribe();
    }

    [Fact]
    public void Replay_without_a_gap_is_ordered_by_sequence()
    {
        var gateway = new RealtimeGateway(() => 10);
        var (_, unsubscribe) = gateway.SubscribeChannel(WorldScope);
        gateway.Publish(WorldScope, ScopeTickDelta.Empty(8));
        gateway.Publish(WorldScope, ScopeTickDelta.Empty(9));

        var replay = gateway.ReplayState(WorldScope, ViewerMode.Spectator, new VisualCursor(8, "world", 0));

        Assert.False(replay.Value!.RequiresSnapshot);
        Assert.Equal([1L, 2L], replay.Value.Deltas.Select(delta => delta.ToCursor.Sequence));
        unsubscribe();
    }

    [Fact]
    public void Scope_crossing_removes_from_origin_and_upserts_in_destination_at_the_same_tick()
    {
        var (world, _) = ScenarioRunner.Create(seed: 44, initialPopulation: 1);
        var npc = Assert.Single(world.Npcs);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        var cityScope = new VisualScope(VisualScopeKind.City, city.Id.ToString());
        var originBefore = LivingScopeProjector.Build(world, cityScope);
        var destinationBefore = LivingScopeProjector.Build(world, WorldScope);

        npc.MoveTo(new CellCoord(9, 9), tick: 22);
        var originAfter = LivingScopeProjector.Build(world, cityScope);
        var destinationAfter = LivingScopeProjector.Build(world, WorldScope);

        var origin = ScopeDeltaBuilder.Diff(22, originBefore, originAfter);
        var destination = ScopeDeltaBuilder.Diff(22, destinationBefore, destinationAfter);

        Assert.Equal(22, origin.Tick);
        Assert.Equal(22, destination.Tick);
        Assert.Equal([npc.Id], origin.NpcRemoved);
        Assert.Equal([new NpcVisual(npc.Id, new CellCoord(9, 9), npc.CurrentAction)], destination.NpcUpserts);
    }

    [Fact]
    public void Tick_loop_publishes_the_typed_final_state_delta()
    {
        using var factory = new WebApplicationFactory<Program>();
        var services = factory.Services;
        var worldHost = services.GetRequiredService<WorldHost>();
        var simulationHost = services.GetRequiredService<SimulationHost>();
        var gateway = services.GetRequiredService<RealtimeGateway>();
        var loop = services.GetRequiredService<TickLoopService>();
        var (world, _) = ScenarioRunner.Create(seed: 45, initialPopulation: 1);
        var npc = Assert.Single(world.Npcs);
        var city = new City(world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);
        worldHost.Replace(world, new WorldClock([]));
        simulationHost.Resume();
        var cityScope = new VisualScope(VisualScopeKind.City, city.Id.ToString());
        var (reader, unsubscribe) = gateway.SubscribeChannel(cityScope);

        loop.RunOneCycle();

        Assert.True(reader.TryRead(out var envelope));
        var delta = Assert.IsType<ScopeTickDelta>(envelope!.Payload);
        Assert.Equal(new NpcVisual(npc.Id, npc.CurrentLocation, npc.CurrentAction), Assert.Single(delta.NpcUpserts));
        Assert.Equal(6, delta.Indicators.Count);
        unsubscribe();
    }
}
