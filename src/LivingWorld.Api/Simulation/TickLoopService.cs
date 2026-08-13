using LivingWorld.Api.Realtime;
using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Simulation;

/// <summary>Fase 15.1, T3 (VTT2-26): decide QUANDO chamar <see cref="WorldClock.Tick"/> — nunca
/// O QUE o tick faz (essa fronteira é de <c>rules/simulation-determinism.md</c>: o loop só
/// dirige o relógio, todo o comportamento de simulação continua em <see cref="WorldClock"/> e
/// nos <c>ISimulationSystem</c>). Roda no ritmo de <see cref="SimulationHost.TicksPerSecond"/>
/// enquanto não pausado, e publica o <see cref="ScopeTickDelta"/> de cada escopo com assinante
/// ativo (nunca de todo escopo existente no mundo — ver <see cref="RealtimeGateway.SubscribedScopeKeys"/>).
/// <see cref="RunOneCycle"/> fica público para os testes acionarem um ciclo deterministicamente,
/// sem depender do agendamento de tempo real.</summary>
public sealed class TickLoopService(WorldHost worldHost, SimulationHost simulationHost, RealtimeGateway gateway) : IHostedService
{
    private readonly Dictionary<string, IReadOnlyDictionary<NpcId, CellCoord>> _lastPositions = new();
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
    }

    private void PublishDeltas(WorldState world)
    {
        long tick = world.CurrentDate.TotalHours;

        foreach (var scopeKey in gateway.SubscribedScopeKeys)
        {
            if (scopeKey != "world" && !scopeKey.StartsWith("city:", StringComparison.Ordinal))
                continue; // T3 cobre World/City; escopos Interior ficam fora do escopo desta task.

            var scope = ParseScope(scopeKey);
            var positions = PositionsOf(world, scope);
            var before = _lastPositions.TryGetValue(scopeKey, out var previous)
                ? previous
                : new Dictionary<NpcId, CellCoord>();

            var delta = ScopeDeltaBuilder.Diff(tick, before, positions);
            _lastPositions[scopeKey] = positions;
            gateway.Publish(scope, delta);
        }
    }

    private static VisualScope ParseScope(string scopeKey) =>
        scopeKey == "world"
            ? new VisualScope(VisualScopeKind.World, "")
            : new VisualScope(VisualScopeKind.City, scopeKey["city:".Length..]);

    private static IReadOnlyDictionary<NpcId, CellCoord> PositionsOf(WorldState world, VisualScope scope)
    {
        if (scope.Kind == VisualScopeKind.City)
        {
            var cityId = new CityId(Guid.Parse(scope.RefId));
            return world.Npcs
                .Where(n => n.IsAlive && n.City == cityId)
                .ToDictionary(n => n.Id, n => n.CurrentLocation);
        }

        // World: mesma semântica de GlobalProjector.ExternalNpcs (NPC fora da célula da própria
        // cidade) — sem invocar GlobalProjector.Build, que também monta camadas.
        var cityLocationById = world.Cities.ToDictionary(c => c.Id, c => c.Location);
        return world.Npcs
            .Where(n => n.IsAlive && cityLocationById.TryGetValue(n.City, out var home) && n.CurrentLocation != home)
            .ToDictionary(n => n.Id, n => n.CurrentLocation);
    }
}
