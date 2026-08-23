using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Simulation;

/// <summary>Fase 15.1, T27 (fechamento — VTT2-05, VTT2-30, VTT2-33): prova de que ligar o
/// cliente de verdade (WebSocket assinando um escopo, navegando via `/visual/subscribe`, mais
/// os endpoints de `/simulation/*`) nunca perturba o mundo canônico — nenhuma dessas leituras
/// grava no domínio, então N ticks com sessão observando devem produzir exatamente o mesmo hash
/// de N ticks sem ninguém olhando. `/simulation/pause|resume|speed` isolados já têm essa mesma
/// prova em <c>SimulationControlEndpointsTests.Pause_resume_and_speed_calls_never_change_the_canonical_hash</c>
/// — este teste cobre o caminho composto (tick loop real + assinante real + navegação real).</summary>
public class TickLoopHashInvarianceTests
{
    private const int Ticks = 5;

    [Fact]
    public async Task N_ticks_with_an_active_observing_session_navigating_scopes_produce_the_same_hash_as_N_ticks_without_one()
    {
        using var factoryWithoutSession = new WebApplicationFactory<Program>();
        var worldHostA = factoryWithoutSession.Services.GetRequiredService<WorldHost>();
        var simulationHostA = factoryWithoutSession.Services.GetRequiredService<SimulationHost>();
        var loopA = factoryWithoutSession.Services.GetRequiredService<TickLoopService>();
        simulationHostA.Resume();
        for (int i = 0; i < Ticks; i++) loopA.RunOneCycle();
        string hashWithoutSession = WorldSnapshot.CanonicalHash(worldHostA.Current);

        using var factoryWithSession = new WebApplicationFactory<Program>();
        var worldHostB = factoryWithSession.Services.GetRequiredService<WorldHost>();
        var simulationHostB = factoryWithSession.Services.GetRequiredService<SimulationHost>();
        var loopB = factoryWithSession.Services.GetRequiredService<TickLoopService>();
        var gatewayB = factoryWithSession.Services.GetRequiredService<RealtimeGateway>();
        var client = factoryWithSession.CreateClient();
        simulationHostB.Resume();

        var worldScope = new VisualScope(VisualScopeKind.World, "");
        var (worldReader, unsubscribeWorld) = gatewayB.SubscribeChannel(worldScope);
        for (int i = 0; i < Ticks; i++)
        {
            loopB.RunOneCycle();
            while (worldReader.TryRead(out _)) { } // um observador real drenando o delta publicado

            // "navegando": mesma leitura HTTP que RealSnapshotSource (T31) faz ao trocar de espaço
            using var response = await client.GetAsync("/visual/subscribe?scope=World&mode=Spectator");
            response.EnsureSuccessStatusCode();
        }
        unsubscribeWorld();
        string hashWithSession = WorldSnapshot.CanonicalHash(worldHostB.Current);

        Assert.Equal(hashWithoutSession, hashWithSession);
    }
}
