using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary><see cref="TickDelta"/> (ADR-0050) não é comando de jogador: é a troca da duração do tick do mundo, que entra no mesmo
/// registro porque o replay precisa da mesma sequência de Δ para dar o mesmo mundo.</summary>
public enum PlayerCommandKind { Incarnate, Act, Logout, TickDelta }

/// <summary>Um comando de jogador aceito, no instante do mundo em que foi aplicado (antes do tick seguinte rodar), em milissegundos
/// (<see cref="AtMs"/>; no <c>players.log</c> o campo segue <c>Tick</c>, em horas na hora cheia: registro antigo carrega
/// convertido). <see cref="Sequence"/> é a ordem de chegada no hospedeiro: com vários jogadores no mesmo branch, é a ordem única
/// de aplicação. <see cref="DeltaMs"/> só no <see cref="PlayerCommandKind.TickDelta"/>.</summary>
public sealed record PlayerCommandRecord(
    [property: JsonPropertyName("Tick"), JsonConverter(typeof(HourCompatibleMillisJsonConverter))] long AtMs,
    long Sequence, string PlayerId, PlayerCommandKind Kind, long? NpcId = null, ActionType? Action = null,
    string? PowerId = null, int? DeltaMs = null);

/// <summary>Registro de sessões do hospedeiro (AD-196): quem encarnou, agiu e saiu, na ordem em que o mundo aplicou. Fica fora
/// do snapshot e do hash (não é fato do mundo); reaplicar o registro a partir do snapshot do começo reproduz o mesmo mundo
/// (<see cref="PlayerReplay"/>). Só comandos aceitos entram: o recusado não mudou nada.</summary>
public sealed class PlayerSessionLog
{
    private readonly List<PlayerCommandRecord> _records = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<PlayerCommandRecord> Records
    {
        get { lock (_gate) return [.. _records]; }
    }

    /// <summary>O último comando aceito (nulo se nenhum): quem persiste o registro grava um por vez.</summary>
    public PlayerCommandRecord? Last
    {
        get { lock (_gate) return _records.Count == 0 ? null : _records[^1]; }
    }

    /// <summary>Duração do tick com que o hospedeiro roda o mundo agora (ADR-0050). Começa em uma hora.</summary>
    public int TickDeltaMs
    {
        get { lock (_gate) return _tickDeltaMs; }
    }

    private int _tickDeltaMs = WorldMillis.HourTick;

    internal void Append(long atMs, string playerId, PlayerCommandKind kind, long? npcId = null, ActionType? action = null,
        string? powerId = null, int? deltaMs = null)
    {
        lock (_gate)
        {
            _records.Add(new PlayerCommandRecord(atMs, _records.Count, playerId, kind, npcId, action, powerId, deltaMs));
            if (kind == PlayerCommandKind.TickDelta) _tickDeltaMs = deltaMs!.Value;
        }
    }
}

/// <summary>Borda dos comandos de jogador, no mesmo molde de <c>WorldAuthoringService</c>. Todo comando aceito vai para o
/// <see cref="Log"/>.</summary>
public sealed class PlayerService(IWorldEventSink sink, PlayerSessionLog? log = null)
{
    public PlayerSessionLog Log { get; } = log ?? new PlayerSessionLog();

    private TickContext Context(WorldState world) => new(world, world.Rng, world.Scheduler, sink);

    public Result<Unit> Incarnate(WorldState world, NpcId npcId, string playerId)
    {
        var result = PlayerCommands.Incarnate(world, Context(world), npcId, playerId);
        if (result.IsSuccess) Log.Append(world.CurrentDate.TotalMilliseconds, playerId, PlayerCommandKind.Incarnate, npcId.Value);
        return result;
    }

    public Result<Unit> Act(WorldState world, string playerId, ActionType? action, string? powerId = null)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is not { } npc)
            return Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");
        var result = PlayerCommands.Act(world, npc.Id, playerId, action, powerId);
        if (result.IsSuccess) Log.Append(world.CurrentDate.TotalMilliseconds, playerId, PlayerCommandKind.Act, npc.Id.Value, action, powerId);
        return result;
    }

    public Result<Npc> Logout(WorldState world, string playerId)
    {
        var result = PlayerCommands.Logout(world, Context(world), playerId);
        if (result.IsSuccess) Log.Append(world.CurrentDate.TotalMilliseconds, playerId, PlayerCommandKind.Logout, result.Value!.Id.Value);
        return result;
    }

    /// <summary>Troca a duração do tick do mundo a partir de agora (ADR-0050): Δ divide a hora. Quem pede é o hospedeiro (admin; na
    /// AD-197c, a troca de modo com jogador presente); vai para o registro para o replay reproduzir a mesma sequência de Δ.</summary>
    public Result<Unit> SetTickDelta(WorldState world, string requestedBy, int deltaMs)
    {
        if (!WorldMillis.IsValidTickDelta(deltaMs))
            return Result<Unit>.Fail($"Δ inválido: {deltaMs} ms não divide a hora (1 a 3.600.000 ms)");
        if (deltaMs != Log.TickDeltaMs)
            Log.Append(world.CurrentDate.TotalMilliseconds, requestedBy, PlayerCommandKind.TickDelta, deltaMs: deltaMs);
        return Result<Unit>.Ok(Unit.Value);
    }
}

/// <summary>Reaplica um <see cref="PlayerSessionLog"/> sobre o mundo restaurado do snapshot do começo (AD-196): roda o relógio
/// até o instante de cada comando, com a duração de tick em vigor (ADR-0050: começa em uma hora e muda nos registros
/// <see cref="PlayerCommandKind.TickDelta"/>), e o aplica na ordem de chegada. Mesmo snapshot, mesmo registro e mesma sequência
/// de Δ dão o mesmo mundo.</summary>
public static class PlayerReplay
{
    public static WorldState Run(
        string snapshotJson, IReadOnlyList<PlayerCommandRecord> records, WorldClock clock, IWorldEventSink sink, long untilMs)
    {
        var world = WorldSnapshot.Deserialize(snapshotJson);
        var service = new PlayerService(sink);
        int delta = WorldMillis.HourTick;
        foreach (var record in records.OrderBy(r => r.AtMs).ThenBy(r => r.Sequence))
        {
            AdvanceTo(clock, world, record.AtMs, delta);
            var applied = record.Kind switch
            {
                PlayerCommandKind.Incarnate => service.Incarnate(world, new NpcId(record.NpcId!.Value), record.PlayerId).IsSuccess,
                PlayerCommandKind.Act => service.Act(world, record.PlayerId, record.Action, record.PowerId).IsSuccess,
                PlayerCommandKind.TickDelta => service.SetTickDelta(world, record.PlayerId, record.DeltaMs!.Value).IsSuccess,
                _ => service.Logout(world, record.PlayerId).IsSuccess,
            };
            if (!applied) throw new InvalidOperationException($"replay divergiu no comando {record.Sequence}");
            if (record.Kind == PlayerCommandKind.TickDelta) delta = record.DeltaMs!.Value;
        }

        AdvanceTo(clock, world, untilMs, delta);
        return world;
    }

    /// <summary>Os mesmos ticks que o hospedeiro rodou: Δ em vigor, parando em cada hora cheia (<see cref="WorldClock.Tick(WorldState, int)"/>).</summary>
    private static void AdvanceTo(WorldClock clock, WorldState world, long atMs, int delta)
    {
        while (world.CurrentDate.TotalMilliseconds < atMs)
            clock.Tick(world, delta);
        if (world.CurrentDate.TotalMilliseconds != atMs)
            throw new InvalidOperationException($"replay passou do instante {atMs} ms com Δ = {delta} ms");
    }
}
