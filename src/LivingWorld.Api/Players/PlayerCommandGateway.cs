using LivingWorld.Api.World;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>Um comando de jogador vindo da rota HTTP ou do canal persistente. <c>Mode</c> só com <c>Incarnate</c> (posse plena é
/// do admin); <c>Dx</c>/<c>Dy</c> com <c>Move</c>; <c>NpcId</c> é o alvo no <c>Incarnate</c> e no <c>Strike</c>.</summary>
public sealed record PlayerCommandRequest(PlayerCommandKind Kind, long? NpcId = null, ActionType? Action = null, string? PowerId = null,
    LivingWorld.Domain.Population.PossessionMode Mode = LivingWorld.Domain.Population.PossessionMode.Common, int Dx = 0, int Dy = 0);

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
            log.BeforeCommand(library.CurrentId.ToString("D"), world, players.Log.TickDeltaMs);
            var before = players.Log.Last;
            var reply = command.Kind switch
            {
                PlayerCommandKind.Incarnate => Map(players.Incarnate(world, new NpcId(command.NpcId ?? -1), playerId, command.Mode),
                    _ => new { npcId = command.NpcId, playerId }),
                PlayerCommandKind.Move => Map(players.Move(world, playerId, command.Dx, command.Dy),
                    _ => PoseOf(world, playerId)),
                PlayerCommandKind.Strike => Map(players.Strike(world, playerId, new NpcId(command.NpcId ?? -1)),
                    _ => new { playerId, targetId = command.NpcId }),
                PlayerCommandKind.Act => Map(players.Act(world, playerId, command.Action, command.PowerId),
                    _ => new { playerId, Action = command.Action?.ToString() }),
                _ => Map(players.Logout(world, playerId),
                    npc => new { npcId = npc.Id.Value, playerId, disappearedAtTick = npc.DisappearedAtTick }),
            };
            if (players.Log.Last is { } accepted && !ReferenceEquals(accepted, before)) log.Append(accepted);
            return reply;
        }
    }

    /// <summary>A posição autoritativa do corpo depois do passo (o cliente reconcilia por ela).</summary>
    private static object PoseOf(LivingWorld.Simulation.Core.WorldState world, string playerId) =>
        PlayerCommands.ControlledBy(world, playerId) is { } npc
            ? new { playerId, npcId = npc.Id.Value, x = npc.CurrentLocation.X, y = npc.CurrentLocation.Y }
            : new { playerId, npcId = -1L, x = 0, y = 0 };

    /// <summary>O que a pessoa presa vê e sente (posse plena, AD-197e), só para quem a possui; nulo se não há posse plena.</summary>
    public CaptiveView? Captive(string playerId)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            return PlayerCommands.ControlledBy(world, playerId) is { } npc ? PossessionWill.View(world, npc, world.CurrentDate.TotalHours) : null;
        }
    }

    /// <summary>Troca a duração do tick do mundo (ADR-0050) pela mesma fila e pelo mesmo registro dos comandos: o replay reproduz a
    /// sequência de Δ. Quem pede é o admin (<paramref name="actor"/>).</summary>
    public PlayerReply SetTickDelta(string actor, int deltaMs)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            log.BeforeCommand(library.CurrentId.ToString("D"), world, players.Log.TickDeltaMs);
            var before = players.Log.Last;
            var reply = Map(players.SetTickDelta(world, actor, deltaMs), _ => new { tickDeltaMs = deltaMs });
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
