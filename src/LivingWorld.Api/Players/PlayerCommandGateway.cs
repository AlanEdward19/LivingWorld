using LivingWorld.Api.World;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>Um comando de jogador vindo da rota HTTP ou do canal persistente.</summary>
public sealed record PlayerCommandRequest(PlayerCommandKind Kind, long? NpcId = null, ActionType? Action = null, string? PowerId = null);

/// <summary>A resposta de um comando: status HTTP (200, 400, 404, 409) e o corpo (sucesso) ou o erro.</summary>
public sealed record PlayerReply(int Status, object? Body, string? Error)
{
    public bool Ok => Status == StatusCodes.Status200OK;
}

/// <summary>Fila única de aplicação dos comandos de jogador por branch (ADR-0048, AD-197a): todo comando, venha da rota HTTP ou do
/// canal persistente, é aplicado sob a mesma trava do laço do relógio (<see cref="SavedWorldLibrary.Gate"/>), no limite entre
/// ticks, na ordem em que chega à trava; o aceito vai para o registro do hospedeiro e para o disco antes da resposta. Sob pausa do
/// admin todo comando é recusado com 409 sem mudar o mundo (aprovado pelo dono em 2026-11-23).</summary>
public sealed class PlayerCommandGateway(
    WorldHost host, SimulationHost simulation, PlayerService players, SavedWorldLibrary library, PlayerLogStore log)
{
    public const string PausedError = "conflict: mundo pausado pelo admin";

    /// <summary>Só para o par de mutação do teste: falso desliga a recusa sob pausa (o teste da pausa tem de reprovar).</summary>
    public bool EnforcePause { get; set; } = true;

    public PlayerLogStore Log => log;

    public PlayerReply Execute(string playerId, PlayerCommandRequest command)
    {
        lock (library.Gate)
        {
            if (EnforcePause && simulation.IsPaused) return new PlayerReply(StatusCodes.Status409Conflict, null, PausedError);
            var world = host.Current;
            log.BeforeCommand(library.CurrentId.ToString("D"), world);
            var before = players.Log.Last;
            var reply = command.Kind switch
            {
                PlayerCommandKind.Incarnate => Map(players.Incarnate(world, new NpcId(command.NpcId ?? -1), playerId),
                    _ => new { npcId = command.NpcId, playerId }),
                PlayerCommandKind.Act => Map(players.Act(world, playerId, command.Action, command.PowerId),
                    _ => new { playerId, Action = command.Action?.ToString() }),
                _ => Map(players.Logout(world, playerId),
                    npc => new { npcId = npc.Id.Value, playerId, disappearedAtTick = npc.DisappearedAtTick }),
            };
            if (players.Log.Last is { } accepted && !ReferenceEquals(accepted, before)) log.Append(accepted);
            return reply;
        }
    }

    private static PlayerReply Map<T>(Result<T> result, Func<T, object> ok)
    {
        if (result.IsSuccess) return new PlayerReply(StatusCodes.Status200OK, ok(result.Value!), null);
        string error = result.Error!;
        if (error.StartsWith(PlayerCommands.NotFound, StringComparison.Ordinal)) return new PlayerReply(StatusCodes.Status404NotFound, null, error);
        if (error.StartsWith(PlayerCommands.Conflict, StringComparison.Ordinal)) return new PlayerReply(StatusCodes.Status409Conflict, null, error);
        return new PlayerReply(StatusCodes.Status400BadRequest, null, error);
    }
}
