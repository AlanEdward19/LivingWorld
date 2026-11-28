namespace LivingWorld.Api.Players;

/// <summary>#264: <c>GET /players/{playerId}/home</c> (o modo comprar: catálogo com o estoque da loja, peças, beleza dos cômodos, tintas e preço da obra) e
/// <c>GET /players/{playerId}/home/check?verb=...</c> (o motor diz se o verbo de casa vale agora e por quê, sem mudar nada). 200, 401, 403, 404. Só leitura.</summary>
public static class PlayerHomeEndpoints
{
    public static void MapPlayerHomeEndpoints(this WebApplication app, PlayerCommandGateway gateway, PlayerSessionTokens tokens, PlayerAuthPolicy policy)
    {
        app.MapGet("/players/{playerId}/home", (HttpContext http, string playerId) =>
            PlayerEndpoints.Identify(http, tokens, policy.Required, playerId) is { } refused ? refused : Reply(gateway.HomeState(playerId)));
        app.MapGet("/players/{playerId}/home/check", (HttpContext http, string playerId, string verb) =>
            PlayerEndpoints.Identify(http, tokens, policy.Required, playerId) is { } refused ? refused : Reply(gateway.HomeCheck(playerId, verb)));
    }

    private static IResult Reply(PlayerReply reply) =>
        reply.Ok ? Results.Ok(reply.Body) : Results.Json(new { error = reply.Error }, statusCode: reply.Status);
}
