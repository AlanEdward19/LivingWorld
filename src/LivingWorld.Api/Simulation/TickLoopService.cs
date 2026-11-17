using LivingWorld.Api.Realtime;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Api.World;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Simulation;

/// <summary>Decide QUANDO chamar <see cref="WorldClock.Tick"/> — nunca
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
    SavedWorldLibrary library,
    BufferingWorldEventSink sink,
    LivingWorld.Api.Visual.Observable.PresentationNameResolver names) : IHostedService
{
    private readonly Dictionary<string, LivingScopeState> _lastStates = new();
    private ScopeProjectionFrame? _lastFrame; // AD-172: o NPC que não mudou reaproveita o registro do tick anterior
    private readonly Dictionary<string, IReadOnlyList<InteriorOccupant>> _lastInteriors = new();
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
        lock (library.Gate) RunLockedCycle();
    }

    private void RunLockedCycle()
    {
        if (simulationHost.IsPaused)
        {
            simulationHost.CountPausedCycle();
            return;
        }

        var world = worldHost.Current;
        worldHost.Clock.Tick(world);
        PublishDeltas(world);
        if (world.CurrentDate.TotalHours % 24 == 0)
            library.Save(world);
    }

    /// <summary>Publica o delta dos escopos com assinante para o estado atual do mundo. Público para que
    /// <c>step</c> e <c>advance-year</c> (que avançam o relógio fora do laço) também avisem os clientes.</summary>
    public void PublishDeltas() => PublishDeltas(worldHost.Current);

    private void PublishDeltas(WorldState world)
    {
        long tick = world.CurrentDate.TotalHours;

        var subscribed = gateway.SubscribedScopeKeys;
        if (subscribed.Count == 0) // sem assinante, nenhuma projeção (AD-170: custo por NPC-tick igual)
        {
            _lastInteriors.Clear();
            _lastStates.Clear();
            _lastFrame = null;
            return;
        }
        // AD-172: uma leitura do tick para todos os escopos (limites, quem está onde, índices do observável).
        ScopeProjectionFrame? frame = null;
        foreach (var scopeKey in subscribed)
        {
            if (scopeKey.StartsWith("interior:", StringComparison.Ordinal))
            {
                PublishInterior(world, scopeKey, tick);
                continue;
            }

            if (scopeKey != "world" && !scopeKey.StartsWith("city:", StringComparison.Ordinal))
                continue;

            var scope = ParseScope(scopeKey);
            frame ??= new ScopeProjectionFrame(world, names.For(world.PeriodId), _lastFrame);
            var state = LivingScopeProjector.Build(frame, scope, sink.EventsAt(tick));
            var before = _lastStates.TryGetValue(scopeKey, out var previous)
                ? previous
                : LivingScopeState.Empty;

            var delta = ScopeDeltaBuilder.Diff(tick, before, state);
            _lastStates[scopeKey] = state;
            gateway.Publish(scope, delta);
        }

        if (frame is not null) _lastFrame = frame;
        if (_lastInteriors.Count > 0)
            foreach (var gone in _lastInteriors.Keys.Where(key => !subscribed.Contains(key)).ToList())
                _lastInteriors.Remove(gone); // sem assinante, o próximo a assinar começa de uma base nova
        if (_lastStates.Count > 0)
            foreach (var gone in _lastStates.Keys.Where(key => !subscribed.Contains(key)).ToList())
                _lastStates.Remove(gone); // AD-172: idem para Cidade e Mundo (a base velha daria avanço de processo errado)
    }

    /// <summary>Delta de interior: diferença das ocupações entre dois ticks, publicada só quando mudou.</summary>
    private void PublishInterior(WorldState world, string scopeKey, long tick)
    {
        if (!long.TryParse(scopeKey["interior:".Length..], out long id)) return;
        var built = InteriorProjector.BuildOccupants(world, new BuildingId(id));
        if (built is null) return;

        var before = _lastInteriors.TryGetValue(scopeKey, out var previous) ? previous : [];
        _lastInteriors[scopeKey] = built;
        var diff = InteriorOccupancyDiffer.Diff(before, built);
        var delta = new InteriorTickDelta(tick, new BuildingId(id), diff.Entered, diff.Moved, diff.ChangedFloor, diff.Exited);
        if (!delta.IsEmpty)
            gateway.Publish(new VisualScope(VisualScopeKind.Interior, id.ToString()), delta);
    }

    private static VisualScope ParseScope(string scopeKey) =>
        scopeKey == "world"
            ? new VisualScope(VisualScopeKind.World, "")
            : new VisualScope(VisualScopeKind.City, scopeKey["city:".Length..]);

}
