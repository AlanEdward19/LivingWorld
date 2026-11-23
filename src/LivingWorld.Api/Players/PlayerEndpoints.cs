using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary><c>PlayerId</c> é opcional com sessão (o jogador vem do token); no modo local é obrigatório.</summary>
public sealed record IncarnateRequest(string? PlayerId);
/// <summary><c>Action</c> é o nome da ação (Eat, Sleep, Work, Socialize, Idle, Buy, UsePower); vazio devolve a escolha ao motor.
/// <c>PowerId</c> (AD-196, opcional) é o poder que o NPC porta, exigido com UsePower.</summary>
public sealed record PlayerActionRequest(string? Action, string? PowerId = null);
/// <summary>Sessão nova (ADR-0048): <c>Authorization: Bearer {SessionToken}</c> nas rotas e no canal do jogador.</summary>
public sealed record SessionResponse(string SessionToken, string PlayerId);

/// <summary>Fase 25: <c>POST /sessions</c> (200) emite a sessão do jogador (ADR-0048, AD-197a). <c>POST /players/{npcId}/incarnate</c>
/// (200, 401, 403, 404, 409), <c>POST /players/{playerId}/action</c> (200, 400, 401, 403, 404, 409) e
/// <c>POST /players/{playerId}/logout</c> (200, 401, 403, 404, 409). Com token, o jogador vem da sessão: token inválido 401,
/// <c>playerId</c> da rota ou do corpo que não é o da sessão 403. Sem token: 401 com <c>PLAYER_AUTH=required</c>; no modo local (o
/// padrão) vale o <c>playerId</c> da rota, como antes. Sob pausa do admin, 409. Tudo passa pela fila única
/// (<see cref="PlayerCommandGateway"/>), a mesma do canal <c>/players/ws</c>.</summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app, PlayerCommandGateway gateway, PlayerSessionTokens tokens, bool authRequired)
    {
        app.MapPost("/sessions", () =>
        {
            var session = tokens.Create();
            return Results.Ok(new SessionResponse(session.Token, session.PlayerId));
        });

        app.MapPost("/players/{npcId:long}/incarnate", (HttpContext http, long npcId, IncarnateRequest request) =>
            Run(http, tokens, authRequired, request.PlayerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Incarnate, npcId)));

        app.MapPost("/players/{playerId}/action", (HttpContext http, string playerId, PlayerActionRequest request) =>
        {
            if (!TryParseAction(request.Action, out var action)) return Results.BadRequest(new { error = "action: desconhecida" });
            return Run(http, tokens, authRequired, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Act, Action: action, PowerId: request.PowerId));
        });

        app.MapPost("/players/{playerId}/logout", (HttpContext http, string playerId) =>
            Run(http, tokens, authRequired, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Logout)));
    }

    internal static bool TryParseAction(string? text, out ActionType? action)
    {
        action = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!Enum.TryParse<ActionType>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed)) return false;
        action = parsed;
        return true;
    }

    /// <summary>O token do cabeçalho <c>Authorization: Bearer</c>, ou nulo sem cabeçalho.</summary>
    internal static string? BearerOf(HttpContext http)
    {
        string header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }

    private static IResult Run(HttpContext http, PlayerSessionTokens tokens, bool authRequired, string? claimed, PlayerCommandGateway gateway,
        PlayerCommandRequest command)
    {
        string? playerId;
        if (BearerOf(http) is { } token)
        {
            playerId = tokens.PlayerOf(token);
            if (playerId is null) return Results.StatusCode(StatusCodes.Status401Unauthorized);
            if (!string.IsNullOrWhiteSpace(claimed) && claimed != playerId) return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        else if (authRequired)
        {
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }
        else
        {
            playerId = claimed;
            if (string.IsNullOrWhiteSpace(playerId)) return Results.BadRequest(new { error = "playerId: obrigatório" });
        }

        var reply = gateway.Execute(playerId, command);
        return reply.Ok ? Results.Ok(reply.Body) : Results.Json(new { error = reply.Error }, statusCode: reply.Status);
    }
}
