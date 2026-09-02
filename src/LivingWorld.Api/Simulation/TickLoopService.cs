using LivingWorld.Api.Realtime;
using LivingWorld.Api.Visual;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Simulation;

/// <summary>Fase 15.1, T3 (VTT2-26): decide QUANDO chamar <see cref="WorldClock.Tick"/> — nunca
/// O QUE o tick faz (essa fronteira é de <c>rules/simulation-determinism.md</c>: o loop só
/// dirige o relógio, todo o comportamento de simulação continua em <see cref="WorldClock"/> e
/// nos <c>ISimulationSystem</c>). Roda no ritmo de <see cref="SimulationHost.TicksPerSecond"/>
/// enquanto não pausado, e publica o <see cref="ScopeTickDelta"/> de cada escopo com assinante
/// ativo (nunca de todo escopo existente no mundo — ver <see cref="RealtimeGateway.SubscribedScopeKeys"/>).
/// <see cref="RunOneCycle"/> fica público para os testes acionarem um ciclo deterministicamente,
/// sem depender do agendamento de tempo real.</summary>
public sealed class TickLoopService(
    WorldHost worldHost,
    SimulationHost simulationHost,
    RealtimeGateway gateway,
    PersistentWorldRunner runner,
    BufferingWorldEventSink sink) : IHostedService
{
    private readonly Dictionary<string, LivingScopeState> _lastStates = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_loopTask is not null)
        {
            try { await _loopTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            RunOneCycle();

            double delaySeconds = 1.0 / Math.Max(simulationHost.TicksPerSecond, 0.01);
            try { await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token); }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>Um ciclo do loop: avança exatamente um tick se não pausado, depois publica delta
    /// nos escopos com assinante. Não paga custo nenhum de projeção/publish se pausado ou se
    /// ninguém está ouvindo nenhum escopo.</summary>
    public void RunOneCycle()
    {
        if (simulationHost.IsPaused) return;

        var world = worldHost.Current;
        worldHost.Clock.Tick(world);
        PublishDeltas(world);
        if (world.CurrentDate.TotalHours % 24 == 0)
            runner.Snapshot(world, sink);
    }

    private void PublishDeltas(WorldState world)
    {
        long tick = world.CurrentDate.TotalHours;

        foreach (var scopeKey in gateway.SubscribedScopeKeys)
        {
            if (scopeKey != "world" && !scopeKey.StartsWith("city:", StringComparison.Ordinal))
                continue; // T3 cobre World/City; escopos Interior ficam fora do escopo desta task.

            var scope = ParseScope(scopeKey);
            var state = LivingScopeProjector.Build(world, scope, sink.EventsAt(tick));
            var before = _lastStates.TryGetValue(scopeKey, out var previous)
                ? previous
                : LivingScopeState.Empty;

            var delta = ScopeDeltaBuilder.Diff(tick, before, state);
            _lastStates[scopeKey] = state;
            gateway.Publish(scope, delta);
        }
    }

    private static VisualScope ParseScope(string scopeKey) =>
        scopeKey == "world"
            ? new VisualScope(VisualScopeKind.World, "")
            : new VisualScope(VisualScopeKind.City, scopeKey["city:".Length..]);

}
