using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-172: com a leitura partilhada do tick, os campos omitidos quando não mudam e os processos que avançam sozinhos, o
/// snapshot mais os deltas ainda reconstroem a projeção nova — publicando a cada tick, a cada poucos ticks ou num salto, e também
/// para quem entra no meio.</summary>
public class ScopeDeltaCadenceTests
{
    /// <summary>O redutor guarda processos e espólios por id; a projeção, na ordem de montagem. Compara o conjunto.</summary>
    private static LivingScopeState Normalized(LivingScopeState state) => state with
    {
        Processes = state.Processes.OrderBy(p => p.Id).ToList(),
        Leavings = state.Leavings.OrderBy(l => l.Id).ToList(),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(24)]
    public void Snapshot_plus_deltas_rebuild_the_fresh_projection_at_any_cadence(int ticksPerDelta)
    {
        var (world, clock) = ScenarioRunner.Create(42, initialPopulation: 40);
        VisualScope[] scopes = [new(VisualScopeKind.City, world.ActiveCities().First().Id.ToString()), new(VisualScopeKind.World, "")];
        var server = scopes.Select(scope => LivingScopeProjector.Build(world, scope)).ToArray();
        var client = server.ToArray();
        LivingScopeState[]? lateClient = null;
        int advancedWithoutUpsert = 0;
        ScopeProjectionFrame? previous = null;

        for (int step = 0; step < 16; step++)
        {
            for (int t = 0; t < ticksPerDelta; t++) clock.Tick(world);
            long tick = world.CurrentDate.TotalHours;
            if (step == 6) lateClient = scopes.Select(scope => LivingScopeProjector.Build(world, scope)).ToArray(); // entra no meio
            var frame = previous = new ScopeProjectionFrame(world, previous: previous); // como o laço: com a leitura anterior
            for (int s = 0; s < scopes.Length; s++)
            {
                var fresh = LivingScopeProjector.Build(frame, scopes[s]);
                Assert.Equal(LivingScopeProjector.Build(world, scopes[s]), fresh); // reaproveitar o tick anterior não muda nada
                var delta = ScopeDeltaBuilder.Diff(tick, server[s], fresh);
                var upserted = delta.ProcessUpserts.Select(p => p.Id).ToHashSet();
                advancedWithoutUpsert += fresh.Processes.Count(p => !upserted.Contains(p.Id)
                    && server[s].Processes.FirstOrDefault(old => old.Id == p.Id) is { } old && old.RemainingHours != p.RemainingHours);
                client[s] = LivingDeltaReducer.Apply(client[s], delta);
                Assert.Equal(Normalized(fresh), Normalized(client[s]));
                if (lateClient is not null && step > 6)
                {
                    lateClient[s] = LivingDeltaReducer.Apply(lateClient[s], delta);
                    Assert.Equal(Normalized(fresh), Normalized(lateClient[s]));
                }

                server[s] = fresh;
            }
        }

        // Num salto de um dia o sono (8 h) já acabou: o avanço sem reenvio só aparece com delta curto.
        if (ticksPerDelta < 8)
            Assert.True(advancedWithoutUpsert > 0, "nenhum processo avançou sem reenvio: o teste não exercitou a regra");
    }

    [Fact]
    public void Shared_frame_projects_the_same_scopes_as_a_standalone_build()
    {
        var (world, clock) = ScenarioRunner.Create(7, initialPopulation: 30);
        for (int t = 0; t < 30; t++) clock.Tick(world);
        var city = new VisualScope(VisualScopeKind.City, world.ActiveCities().First().Id.ToString());
        var global = new VisualScope(VisualScopeKind.World, "");
        var frame = new ScopeProjectionFrame(world);

        Assert.Equal(
            (LivingScopeProjector.Build(world, city), LivingScopeProjector.Build(world, global)),
            (LivingScopeProjector.Build(frame, city), LivingScopeProjector.Build(frame, global)));
    }

    [Fact]
    public void City_indicators_read_in_one_pass_match_each_query()
    {
        var (world, clock) = ScenarioRunner.Create(42, initialPopulation: 40);
        for (int t = 0; t < 30; t++) clock.Tick(world);
        var id = world.ActiveCities().First().Id;

        Assert.Equal(
            new CityIndicatorValues(CityPopulationQuery.Population(world, id), CityPopulationQuery.Wealth(world, id),
                CityPopulationQuery.Health(world, id), CityPopulationQuery.Inequality(world, id), CityPopulationQuery.Economy(world, id),
                CityPopulationQuery.Housing(world, id)),
            CityPopulationQuery.Indicators(world, id));
    }

    [Fact]
    public void Scope_left_without_subscribers_restarts_from_an_empty_base()
    {
        using var factory = new LivingWorldApiFactory();
        var gateway = factory.Services.GetRequiredService<RealtimeGateway>();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
        factory.Services.GetRequiredService<SimulationHost>().Resume();
        var world = factory.Services.GetRequiredService<WorldHost>().Current;
        var scope = new VisualScope(VisualScopeKind.City, world.ActiveCities().First().Id.ToString());
        var (_, first) = gateway.SubscribeChannel(scope);
        loop.RunOneCycle();
        first();
        loop.RunOneCycle(); // ninguém assiste: a base velha sai

        var (reader, again) = gateway.SubscribeChannel(scope);
        loop.RunOneCycle();

        Assert.True(reader.TryRead(out var envelope));
        var delta = Assert.IsType<ScopeTickDelta>(envelope!.Payload);
        Assert.Equal(LivingScopeProjector.Build(world, scope).Npcs.Count, delta.NpcUpserts.Count); // todos, como para quem chega
        again();
    }
}
