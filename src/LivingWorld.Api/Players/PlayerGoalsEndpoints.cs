namespace LivingWorld.Api.Players;

/// <summary>#235: <c>GET /players/{playerId}/goals</c> (metas do corpo e progresso das habilidades) e <c>GET /players/{playerId}/diary</c>
/// (o que aconteceu com ele e marcos de vida). 200, 401, 403, 404 (jogador não controla nenhum NPC). Só leitura: a mesma identificação
/// de <c>/reach</c> e <c>/time</c>; nenhuma entra na fila de comandos nem no registro.</summary>
public static class PlayerGoalsEndpoints
{
    public static void MapPlayerGoalsEndpoints(this WebApplication app, PlayerCommandGateway gateway, PlayerSessionTokens tokens, PlayerAuthPolicy policy)
    {
        app.MapGet("/players/{playerId}/goals", (HttpContext http, string playerId) =>
            PlayerEndpoints.Identify(http, tokens, policy.Required, playerId) is { } refused ? refused : Reply(gateway.Goals(playerId)));
        app.MapGet("/players/{playerId}/diary", (HttpContext http, string playerId) =>
            PlayerEndpoints.Identify(http, tokens, policy.Required, playerId) is { } refused ? refused : Reply(gateway.Diary(playerId)));
    }

    private static IResult Reply(PlayerReply reply) =>
        reply.Ok ? Results.Ok(reply.Body) : Results.Json(new { error = reply.Error }, statusCode: reply.Status);
}
