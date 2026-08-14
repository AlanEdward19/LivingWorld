using LivingWorld.Api;
using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Simulation;

/// <summary>Fase 15.1, T3 (VTT2-26). Cada teste cria sua própria <see cref="WebApplicationFactory{TEntryPoint}"/>
/// (nunca <see cref="IClassFixture{TFixture}"/>) — o Parallelism Assessment de tasks.md marca
/// "integração de tick loop" como NÃO paralelo-seguro numa factory compartilhada, já que o loop
/// muta o <see cref="WorldHost"/> singleton em background. <see cref="TickLoopService.RunOneCycle"/>
/// é chamado direto (nunca <c>StartAsync</c>) — <c>TICK_LOOP_ENABLED</c> continua ausente/false
/// no processo de teste, então o loop de tempo real nunca dispara sozinho.</summary>
public class TickLoopServiceTests
{
    [Fact]
    public void RunOneCycle_advances_the_world_clock_when_not_paused()
    {
        using var factory = new WebApplicationFactory<Program>();
        var worldHost = factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = factory.Services.GetRequiredService<SimulationHost>();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Resume();
        var before = worldHost.Current.CurrentDate.TotalHours;

        loop.RunOneCycle();

        Assert.Equal(before + 1, worldHost.Current.CurrentDate.TotalHours);
    }

    [Fact]
    public void RunOneCycle_does_not_advance_the_clock_when_paused()
    {
        using var factory = new WebApplicationFactory<Program>();
        var worldHost = factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = factory.Services.GetRequiredService<SimulationHost>();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
        simulationHost.Pause();
        var before = worldHost.Current.CurrentDate.TotalHours;

        loop.RunOneCycle();

        Assert.Equal(before, worldHost.Current.CurrentDate.TotalHours);
    }

    [Fact]
    public void RunOneCycle_publishes_a_delta_only_to_the_scope_with_an_active_subscriber()
    {
        using var factory = new WebApplicationFactory<Program>();
        var simulationHost = factory.Services.GetRequiredService<SimulationHost>();
        var gateway = factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
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
        using var factory = new WebApplicationFactory<Program>();
        var worldHost = factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = factory.Services.GetRequiredService<SimulationHost>();
        var gateway = factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
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
        using var factory = new WebApplicationFactory<Program>();
        var worldHost = factory.Services.GetRequiredService<WorldHost>();
        var simulationHost = factory.Services.GetRequiredService<SimulationHost>();
        var gateway = factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
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
}
