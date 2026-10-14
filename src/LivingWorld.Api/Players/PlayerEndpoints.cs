using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

public sealed record IncarnateRequest(string PlayerId);
/// <summary><c>Action</c> é o nome da ação (Eat, Sleep, Work, Socialize, Idle, Buy); vazio devolve a escolha ao motor.</summary>
public sealed record PlayerActionRequest(string? Action);

/// <summary>Fase 25: <c>POST /players/{npcId}/incarnate</c> (200, 404, 409), <c>POST /players/{playerId}/action</c> (200, 404, 400) e
/// <c>POST /players/{playerId}/logout</c> (200, 404). Sem autenticação nem multiplayer (ADR-0015 fica para depois).</summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app, WorldHost host, PlayerService players)
    {
        app.MapPost("/players/{npcId:long}/incarnate", (long npcId, IncarnateRequest request) =>
            Map(players.Incarnate(host.Current, new NpcId(npcId), request.PlayerId ?? ""), _ => new { npcId, request.PlayerId }));

        app.MapPost("/players/{playerId}/action", (string playerId, PlayerActionRequest request) =>
        {
            ActionType? action = null;
            if (!string.IsNullOrWhiteSpace(request.Action))
            {
                if (!Enum.TryParse<ActionType>(request.Action, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                    return Results.BadRequest(new { error = "action: desconhecida" });
                action = parsed;
            }
            return Map(players.Act(host.Current, playerId, action), _ => new { playerId, Action = action?.ToString() });
        });

        app.MapPost("/players/{playerId}/logout", (string playerId) =>
            Map(players.Logout(host.Current, playerId), npc => new { npcId = npc.Id.Value, playerId, disappearedAtTick = npc.DisappearedAtTick }));
    }

    private static IResult Map<T>(Result<T> result, Func<T, object> ok)
    {
        if (result.IsSuccess) return Results.Ok(ok(result.Value!));
        string error = result.Error!;
        if (error.StartsWith(PlayerCommands.NotFound, StringComparison.Ordinal)) return Results.NotFound(new { error });
        if (error.StartsWith(PlayerCommands.Conflict, StringComparison.Ordinal)) return Results.Conflict(new { error });
        return Results.BadRequest(new { error });
    }
}
