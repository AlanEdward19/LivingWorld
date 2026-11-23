using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

public enum PlayerCommandKind { Incarnate, Act, Logout }

/// <summary>Um comando de jogador aceito, no tick do mundo em que foi aplicado (antes do tick rodar). <see cref="Sequence"/> é a
/// ordem de chegada no hospedeiro: com vários jogadores no mesmo branch, é a ordem única de aplicação.</summary>
public sealed record PlayerCommandRecord(
    long Tick, long Sequence, string PlayerId, PlayerCommandKind Kind, long? NpcId = null, ActionType? Action = null,
    string? PowerId = null);

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

    internal void Append(long tick, string playerId, PlayerCommandKind kind, long? npcId = null, ActionType? action = null,
        string? powerId = null)
    {
        lock (_gate) _records.Add(new PlayerCommandRecord(tick, _records.Count, playerId, kind, npcId, action, powerId));
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
        if (result.IsSuccess) Log.Append(world.CurrentDate.TotalHours, playerId, PlayerCommandKind.Incarnate, npcId.Value);
        return result;
    }

    public Result<Unit> Act(WorldState world, string playerId, ActionType? action, string? powerId = null)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is not { } npc)
            return Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");
        var result = PlayerCommands.Act(world, npc.Id, playerId, action, powerId);
        if (result.IsSuccess) Log.Append(world.CurrentDate.TotalHours, playerId, PlayerCommandKind.Act, npc.Id.Value, action, powerId);
        return result;
    }

    public Result<Npc> Logout(WorldState world, string playerId)
    {
        var result = PlayerCommands.Logout(world, Context(world), playerId);
        if (result.IsSuccess) Log.Append(world.CurrentDate.TotalHours, playerId, PlayerCommandKind.Logout, result.Value!.Id.Value);
        return result;
    }
}

/// <summary>Reaplica um <see cref="PlayerSessionLog"/> sobre o mundo restaurado do snapshot do começo (AD-196): roda o relógio
/// até o tick de cada comando e o aplica na ordem de chegada. Mesmo snapshot e mesmo registro dão o mesmo mundo.</summary>
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
            var applied = record.Kind switch
            {
                PlayerCommandKind.Incarnate => service.Incarnate(world, new NpcId(record.NpcId!.Value), record.PlayerId).IsSuccess,
                PlayerCommandKind.Act => service.Act(world, record.PlayerId, record.Action, record.PowerId).IsSuccess,
                _ => service.Logout(world, record.PlayerId).IsSuccess,
            };
            if (!applied) throw new InvalidOperationException($"replay divergiu no comando {record.Sequence}");
        }

        clock.Run(world, untilTick - world.CurrentDate.TotalHours);
        return world;
    }
}
