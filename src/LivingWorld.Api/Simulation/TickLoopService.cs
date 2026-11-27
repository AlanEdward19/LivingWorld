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
    LivingWorld.Api.Visual.Observable.PresentationNameResolver names,
    LivingWorld.Simulation.Players.PlayerService players,
    LivingWorld.Api.Players.PlayerCommandGateway playerGateway) : IHostedService
{
    private readonly Dictionary<string, LivingScopeState> _lastStates = new();
    private ScopeProjectionFrame? _lastFrame; // AD-172: o NPC que não mudou reaproveita o registro do tick anterior
    private readonly Dictionary<string, IReadOnlyList<InteriorOccupant>> _lastInteriors = new();
    private CancellationTokenSource? _cts;
    private long _cycles;
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

    /// <summary>#248: o ritmo é por PRAZO (<see cref="LoopPacer"/>), não "ciclo + espera": a espera de 1/tps depois de um ciclo que demora c ms
    /// dava um período de 1/tps + c e o mundo andava mais devagar que o relógio real (o cliente, que prevê em tempo real, passava à frente
    /// do corpo e era puxado de volta).</summary>
    private async Task RunLoopAsync(CancellationToken token)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var pacer = new LoopPacer();
        while (!token.IsCancellationRequested)
        {
            RunOneCycle();

            // AD-197c: a velocidade do laço é a do ritmo em vigor (tempo real: 20 ticks por segundo; rápido: a do admin)
            double tps = simulationHost.IsPaused ? simulationHost.TicksPerSecond : simulationHost.LastPlan.TicksPerSecond;
            double delayMs = pacer.DelayMs(clock.Elapsed.TotalMilliseconds, tps);
            try
            {
                if (delayMs > 0) await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token);
                else await Task.Yield(); // atrasado: o próximo ciclo já, sem prender a thread
            }
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

        // #240: popup aberto de quem joga sozinho: o mundo espera (sem tick, sem projeção); a pausa do admin acima tem precedência
        if (playerGateway.PopupHolding) return;

        var world = worldHost.Current;
        // AD-197c: o ritmo do mundo escolhe o Δ (fino com jogador, uma hora sem ninguém ou acelerado); o fim do pulo de tempo e a troca
        // de Δ vão para o registro do jogador e para o disco, como os comandos. O save diário só na virada da hora 0
        int recorded = players.Log.Count;
        var plan = simulationHost.Cycle(players, () => playerGateway.Log.BeforeCommand(library.CurrentId.ToString("D"), world, players.Log.TickDeltaMs));
        foreach (var record in players.Log.Since(recorded)) playerGateway.Log.Append(record);
        // no tempo real (20 ticks por segundo) a projeção sai no máximo ~5 vezes por segundo real e sempre na hora cheia
        long every = Math.Max(1, (long)Math.Round(plan.TicksPerSecond / 5));
        if (world.CurrentDate.MillisecondOfHour == 0 || ++_cycles % every == 0)
            PublishDeltas(world);
        if (world.CurrentDate.MillisecondOfHour == 0 && world.CurrentDate.TotalHours % 24 == 0)
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
