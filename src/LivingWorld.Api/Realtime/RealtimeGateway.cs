using System.Threading.Channels;
using LivingWorld.Api.Visual;
using LivingWorld.Api.Visual.Layers;
using LivingWorld.Domain;

namespace LivingWorld.Api.Realtime;

/// <summary>Fase 15, T3 (VTT-02, VTT-10): pub/sub por escopo com replay por cursor — a única
/// porta de leitura visual em tempo real. Nunca escreve no <c>WorldState</c>; <see cref="Publish"/>
/// só é chamado pelos projectors (T4/T5) com o resultado de uma leitura já feita.</summary>
public sealed class RealtimeGateway(Func<long> currentTick)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<VisualDeltaEnvelope<object?>>> _log = new();
    private readonly Dictionary<string, List<Channel<VisualDeltaEnvelope<object?>>>> _subscribers = new();

    /// <summary>Fase 15, edge case (spec.md): personagem não pode assinar o mapa-múndi global —
    /// aquele escopo é espectador/admin (VTT-01). Única regra de permissão decidível sem depender
    /// de FOW por conhecimento (T7).</summary>
    public Result<Unit> Authorize(VisualScope scope, ViewerMode mode) =>
        mode == ViewerMode.Player && scope.Kind == VisualScopeKind.World
            ? Result<Unit>.Fail("player mode não pode assinar o escopo world")
            : Result<Unit>.Ok(Unit.Value);

    public Result<VisualSnapshotEnvelope<object?>> Snapshot(VisualScope scope, ViewerMode mode)
    {
        var auth = Authorize(scope, mode);
        if (!auth.IsSuccess) return Result<VisualSnapshotEnvelope<object?>>.Fail(auth.Error!);

        lock (_gate)
        {
            long sequence = _log.TryGetValue(scope.ScopeKey, out var entries) ? entries.Count : 0;
            var cursor = new VisualCursor(currentTick(), scope.ScopeKey, sequence);
            return Result<VisualSnapshotEnvelope<object?>>.Ok(
                new VisualSnapshotEnvelope<object?>(scope, mode, cursor, LayerProjectionCatalog.ListLayers(), null));
        }
    }

    public Result<IReadOnlyList<VisualDeltaEnvelope<object?>>> Replay(VisualScope scope, ViewerMode mode, VisualCursor since)
    {
        var auth = Authorize(scope, mode);
        if (!auth.IsSuccess) return Result<IReadOnlyList<VisualDeltaEnvelope<object?>>>.Fail(auth.Error!);

        lock (_gate)
        {
            if (!_log.TryGetValue(scope.ScopeKey, out var entries))
                return Result<IReadOnlyList<VisualDeltaEnvelope<object?>>>.Ok(Array.Empty<VisualDeltaEnvelope<object?>>());

            IReadOnlyList<VisualDeltaEnvelope<object?>> pending =
                entries.Where(e => e.ToCursor.Sequence > since.Sequence).ToList();
            return Result<IReadOnlyList<VisualDeltaEnvelope<object?>>>.Ok(pending);
        }
    }

    /// <summary>Anexa uma entrada ao log do escopo e distribui para assinantes conectados
    /// (T4/T5 chamam isso após montar a projeção de um tick).</summary>
    public void Publish(VisualScope scope, object? payload)
    {
        lock (_gate)
        {
            var entries = _log.TryGetValue(scope.ScopeKey, out var e) ? e : (_log[scope.ScopeKey] = []);
            var from = new VisualCursor(currentTick(), scope.ScopeKey, entries.Count);
            var to = new VisualCursor(currentTick(), scope.ScopeKey, entries.Count + 1);
            var envelope = new VisualDeltaEnvelope<object?>(scope, from, to, payload);
            entries.Add(envelope);

            if (_subscribers.TryGetValue(scope.ScopeKey, out var channels))
                foreach (var channel in channels)
                    channel.Writer.TryWrite(envelope);
        }
    }

    /// <summary>Abre um canal de push para o escopo; o chamador deve invocar o <c>Unsubscribe</c>
    /// devolvido ao encerrar a conexão, ou o canal continua recebendo escritas indefinidamente.</summary>
    public (ChannelReader<VisualDeltaEnvelope<object?>> Reader, Action Unsubscribe) SubscribeChannel(VisualScope scope)
    {
        var channel = Channel.CreateUnbounded<VisualDeltaEnvelope<object?>>();
        lock (_gate)
        {
            var list = _subscribers.TryGetValue(scope.ScopeKey, out var l) ? l : (_subscribers[scope.ScopeKey] = []);
            list.Add(channel);
        }

        void Unsubscribe()
        {
            lock (_gate)
                if (_subscribers.TryGetValue(scope.ScopeKey, out var list))
                    list.Remove(channel);
            channel.Writer.TryComplete();
        }

        return (channel.Reader, Unsubscribe);
    }
}
