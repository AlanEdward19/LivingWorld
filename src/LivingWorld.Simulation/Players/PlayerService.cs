using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

public enum PlayerCommandKind { Incarnate, Act, Logout, Move, Strike }

/// <summary>Um comando de jogador, no tick do mundo em que foi aplicado (antes do tick rodar). <see cref="Sequence"/> é a ordem de
/// chegada no hospedeiro: com vários jogadores no mesmo branch, é a ordem única de aplicação. <see cref="Mode"/> é o da posse
/// (AD-197e); <see cref="Dx"/>/<see cref="Dy"/> a intenção de passo do <c>move</c>; <see cref="TargetId"/> o alvo do golpe.
/// <see cref="Refused"/>: a pessoa possuída recusou (posse comum) e o mundo não mudou; <see cref="Reason"/> diz por quê.</summary>
public sealed record PlayerCommandRecord(
    long Tick, long Sequence, string PlayerId, PlayerCommandKind Kind, long? NpcId = null, ActionType? Action = null,
    string? PowerId = null, PossessionMode? Mode = null, int? Dx = null, int? Dy = null, long? TargetId = null,
    bool Refused = false, string? Reason = null);

/// <summary>Registro de sessões do hospedeiro (AD-196): quem encarnou, agiu, andou, golpeou e saiu, na ordem em que o mundo
/// aplicou. Fica fora do snapshot e do hash (não é fato do mundo); reaplicar o registro a partir do snapshot do começo reproduz o
/// mesmo mundo (<see cref="PlayerReplay"/>). Entram os comandos aceitos e as recusas da vontade do possuído (AD-197e), estas
/// marcadas: o recusado não mudou nada, e o replay confere que ele é recusado de novo.</summary>
public sealed class PlayerSessionLog
{
    private readonly List<PlayerCommandRecord> _records = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<PlayerCommandRecord> Records
    {
        get { lock (_gate) return [.. _records]; }
    }

    /// <summary>O último comando registrado (nulo se nenhum): quem persiste o registro grava um por vez.</summary>
    public PlayerCommandRecord? Last
    {
        get { lock (_gate) return _records.Count == 0 ? null : _records[^1]; }
    }

    internal void Append(PlayerCommandRecord record)
    {
        lock (_gate) _records.Add(record with { Sequence = _records.Count });
    }
}

/// <summary>Borda dos comandos de jogador, no mesmo molde de <c>WorldAuthoringService</c>. Todo comando aceito (e toda recusa da
/// vontade do possuído) vai para o <see cref="Log"/>.</summary>
public sealed class PlayerService(IWorldEventSink sink, PlayerSessionLog? log = null)
{
    public PlayerSessionLog Log { get; } = log ?? new PlayerSessionLog();

    private TickContext Context(WorldState world) => new(world, world.Rng, world.Scheduler, sink);

    private void Record(WorldState world, string playerId, PlayerCommandKind kind, long? npcId, PlayerCommandRecord? with = null) =>
        Log.Append((with ?? new PlayerCommandRecord(0, 0, playerId, kind)) with
        {
            Tick = world.CurrentDate.TotalHours,
            PlayerId = playerId,
            Kind = kind,
            NpcId = npcId,
        });

    public Result<Unit> Incarnate(WorldState world, NpcId npcId, string playerId, PossessionMode mode = PossessionMode.Common)
    {
        var result = PlayerCommands.Incarnate(world, Context(world), npcId, playerId, mode);
        if (result.IsSuccess)
            Record(world, playerId, PlayerCommandKind.Incarnate, npcId.Value,
                new PlayerCommandRecord(0, 0, playerId, PlayerCommandKind.Incarnate, Mode: mode == PossessionMode.Full ? mode : null));
        return result;
    }

    public Result<Unit> Act(WorldState world, string playerId, ActionType? action, string? powerId = null)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is not { } npc)
            return Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");
        var result = PlayerCommands.Act(world, npc.Id, playerId, action, powerId);
        if (result.IsSuccess)
            Record(world, playerId, PlayerCommandKind.Act, npc.Id.Value,
                new PlayerCommandRecord(0, 0, playerId, PlayerCommandKind.Act, Action: action, PowerId: powerId));
        return result;
    }

    /// <summary>Intenção de passo (−1..1 em cada eixo), não posição: o motor decide onde o corpo chega.</summary>
    public Result<Unit> Move(WorldState world, string playerId, int dx, int dy)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is not { } npc)
            return Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");
        var result = PlayerCommands.Move(world, Context(world), npc.Id, playerId, dx, dy);
        if (result.IsSuccess)
            Record(world, playerId, PlayerCommandKind.Move, npc.Id.Value, new PlayerCommandRecord(0, 0, playerId, PlayerCommandKind.Move, Dx: dx, Dy: dy));
        return result;
    }

    public Result<Unit> Strike(WorldState world, string playerId, NpcId target)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is not { } npc)
            return Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");
        var result = PlayerCommands.Strike(world, Context(world), npc.Id, playerId, target);
        bool refused = !result.IsSuccess && result.Error!.StartsWith(PlayerCommands.Refused, StringComparison.Ordinal);
        if (result.IsSuccess || refused)
            Record(world, playerId, PlayerCommandKind.Strike, npc.Id.Value,
                new PlayerCommandRecord(0, 0, playerId, PlayerCommandKind.Strike, TargetId: target.Value, Refused: refused,
                    Reason: refused ? result.Error : null));
        return result;
    }

    public Result<Npc> Logout(WorldState world, string playerId)
    {
        var result = PlayerCommands.Logout(world, Context(world), playerId);
        if (result.IsSuccess) Record(world, playerId, PlayerCommandKind.Logout, result.Value!.Id.Value);
        return result;
    }
}

/// <summary>Reaplica um <see cref="PlayerSessionLog"/> sobre o mundo restaurado do snapshot do começo (AD-196): roda o relógio
/// até o tick de cada comando e o aplica na ordem de chegada. Mesmo snapshot e mesmo registro dão o mesmo mundo; a recusa
/// registrada tem de ser recusada de novo.</summary>
public static class PlayerReplay
{
    public static WorldState Run(
        string snapshotJson, IReadOnlyList<PlayerCommandRecord> records, WorldClock clock, IWorldEventSink sink, long untilTick)
    {
        var world = WorldSnapshot.Deserialize(snapshotJson);
        var service = new PlayerService(sink);
        foreach (var record in records.OrderBy(r => r.Tick).ThenBy(r => r.Sequence))
        {
            clock.Run(world, record.Tick - world.CurrentDate.TotalHours);
            bool applied = record.Kind switch
            {
                PlayerCommandKind.Incarnate => service.Incarnate(world, new NpcId(record.NpcId!.Value), record.PlayerId,
                    record.Mode ?? PossessionMode.Common).IsSuccess,
                PlayerCommandKind.Act => service.Act(world, record.PlayerId, record.Action, record.PowerId).IsSuccess,
                PlayerCommandKind.Move => service.Move(world, record.PlayerId, record.Dx ?? 0, record.Dy ?? 0).IsSuccess,
                PlayerCommandKind.Strike => service.Strike(world, record.PlayerId, new NpcId(record.TargetId!.Value)).IsSuccess,
                _ => service.Logout(world, record.PlayerId).IsSuccess,
            };
            if (applied == record.Refused) throw new InvalidOperationException($"replay divergiu no comando {record.Sequence}");
        }

        clock.Run(world, untilTick - world.CurrentDate.TotalHours);
        return world;
    }
}
