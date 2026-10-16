using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Simulation;

/// <summary>Fase 15.1, T3 (VTT2-26). Uma <see cref="LivingWorldApiFactory"/> por classe (não por
/// teste): o Parallelism Assessment original temia factory compartilhada com loop em background,
/// mas aqui só chamamos <see cref="TickLoopService.RunOneCycle"/> — <c>TICK_LOOP_ENABLED</c> fica
/// false. <see cref="LivingWorldApiFactory.ResetCanonicalWorld"/> isola mutações entre casos.</summary>
public class TickLoopServiceTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public TickLoopServiceTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public void RunOneCycle_advances_the_world_clock_when_not_paused()
    {
        _factory.ResetCanonicalWorld();
        var worldHost = _factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Resume();
        var before = worldHost.Current.CurrentDate.TotalHours;

        loop.RunOneCycle();

        Assert.Equal(before + 1, worldHost.Current.CurrentDate.TotalHours);
    }

    [Fact]
    public void RunOneCycle_does_not_advance_the_clock_when_paused()
    {
        _factory.ResetCanonicalWorld();
        var worldHost = _factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Pause();
        var before = worldHost.Current.CurrentDate.TotalHours;

        loop.RunOneCycle();

        Assert.Equal(before, worldHost.Current.CurrentDate.TotalHours);
    }

    [Fact]
    public void RunOneCycle_publishes_a_delta_only_to_the_scope_with_an_active_subscriber()
    {
        _factory.ResetCanonicalWorld();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Resume();

        var worldScope = new VisualScope(VisualScopeKind.World, "");
        var unsubscribedCityScope = new VisualScope(VisualScopeKind.City, Guid.NewGuid().ToString());
        var (reader, _) = gateway.SubscribeChannel(worldScope);

        loop.RunOneCycle();

        Assert.True(reader.TryRead(out var envelope));
        Assert.IsType<ScopeTickDelta>(envelope!.Payload);

        var replay = gateway.Replay(unsubscribedCityScope, ViewerMode.Spectator, new VisualCursor(0, unsubscribedCityScope.ScopeKey, 0));
        Assert.Empty(replay.Value!);
    }

    [Fact]
    public void The_published_delta_carries_the_tick_it_was_just_computed_for()
    {
        _factory.ResetCanonicalWorld();
        var worldHost = _factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Resume();

        var worldScope = new VisualScope(VisualScopeKind.World, "");
        var (reader, _) = gateway.SubscribeChannel(worldScope);

        loop.RunOneCycle();

        Assert.True(reader.TryRead(out var envelope));
        var delta = Assert.IsType<ScopeTickDelta>(envelope!.Payload);
        Assert.Equal(worldHost.Current.CurrentDate.TotalHours, delta.Tick);
    }

    [Fact]
    public void World_delta_does_not_publish_a_resident_inside_the_city_footprint()
    {
        _factory.ResetCanonicalWorld();
        var worldHost = _factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        var (world, _) = ScenarioRunner.Create(seed: 17, initialPopulation: 1);
        var resident = Assert.Single(world.Npcs);
        var city = new City(
            world.NextCityId(), resident.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        resident.JoinCity(city.Id);
        resident.MoveTo(new CellCoord(city.Location.X + 1, city.Location.Y + 1), tick: 0);
        worldHost.Replace(world, new WorldClock([]));
        simulationHost.Resume();
        var (reader, _) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.World, ""));

        loop.RunOneCycle();

        Assert.True(reader.TryRead(out var envelope));
        var delta = Assert.IsType<ScopeTickDelta>(envelope!.Payload);
        Assert.Empty(delta.Moved);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public void An_observed_interior_publishes_only_what_changed_and_nothing_when_nobody_moves()
    {
        _factory.ResetCanonicalWorld();
        var worldHost = _factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Resume();
        var building = worldHost.Current.Buildings.First();
        var scope = new VisualScope(VisualScopeKind.Interior, building.Id.Value.ToString());
        var (reader, _) = gateway.SubscribeChannel(scope);

        for (int i = 0; i < 20; i++) loop.RunOneCycle();

        var deltas = new List<InteriorTickDelta>();
        while (reader.TryRead(out var envelope))
            deltas.Add(Assert.IsType<InteriorTickDelta>(envelope.Payload));
        Assert.All(deltas, d => Assert.False(d.IsEmpty));
        Assert.All(deltas, d => Assert.Equal(building.Id, d.BuildingId));
        Assert.True(deltas.Count <= 20);
    }

    [Fact]
    public void An_observed_interior_publishes_a_delta_in_the_very_tick_its_occupants_change_and_never_otherwise()
    {
        _factory.ResetCanonicalWorld();
        var worldHost = _factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
        var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = _factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Resume();
        var world = worldHost.Current;

        // um prédio que algum NPC ocupa ao longo do dia (a ocupação é decidida pela simulação, não pelo teste)
        BuildingId? chosen = null;
        for (int i = 0; i < 18 && chosen is null; i++)
        {
            loop.RunOneCycle();
            chosen = world.Npcs.FirstOrDefault(n => n.Interior is not null)?.Interior!.Building;
        }
        Assert.NotNull(chosen);
        var building = chosen!.Value;
        var (reader, unsubscribe) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.Interior, building.Value.ToString()));
        loop.RunOneCycle(); // base: tudo que já estava lá entra
        while (reader.TryRead(out _)) { }

        int published = 0, changes = 0;
        for (int i = 0; i < 20; i++)
        {
            var before = LivingWorld.Api.Visual.Projection.InteriorProjector.BuildOccupants(world, building)!;
            loop.RunOneCycle();
            var after = LivingWorld.Api.Visual.Projection.InteriorProjector.BuildOccupants(world, building)!;
            bool changed = !LivingWorld.Api.Visual.Projection.InteriorOccupancyDiffer.Diff(before, after).Equals(null)
                && !before.SequenceEqual(after);
            bool got = reader.TryRead(out var envelope);
            while (reader.TryRead(out _)) { }
            Assert.Equal(changed, got);
            if (got) { Assert.IsType<InteriorTickDelta>(envelope!.Payload); published++; }
            if (changed) changes++;
        }

        Assert.Equal(changes, published);
        Assert.True(changes > 0, "o prédio escolhido não mudou de ocupação em 20 ticks: o teste não mediu nada");
        unsubscribe();
    }

    [Fact]
    public void The_canonical_hash_is_the_same_with_and_without_an_interior_subscriber()
    {
        string Run(bool observe)
        {
            _factory.ResetCanonicalWorld();
            var worldHost = _factory.Services.GetRequiredService<WorldHost>();
            var simulationHost = _factory.Services.GetRequiredService<SimulationHost>();
            var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
            var loop = _factory.Services.GetRequiredService<TickLoopService>();
            simulationHost.Resume();
            Action? unsubscribe = null;
            if (observe)
                (_, unsubscribe) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.Interior, worldHost.Current.Buildings.First().Id.Value.ToString()));
            for (int i = 0; i < 5; i++) loop.RunOneCycle();
            unsubscribe?.Invoke();
            return WorldSnapshot.CanonicalHash(worldHost.Current);
        }

        Assert.Equal(Run(observe: false), Run(observe: true));
    }
}

