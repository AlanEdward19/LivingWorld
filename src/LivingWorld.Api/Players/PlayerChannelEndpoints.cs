using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>Canal persistente do jogador (AD-197a, pedido do dono: "manter conexão após conectado, sem demora para interagir"):
/// <c>GET /players/ws</c> com <c>Authorization: Bearer {token}</c> (401 sem sessão válida) abre um WebSocket do próprio ASP.NET Core.
/// Cada mensagem do cliente é um comando com número de sequência — <c>{"seq":1,"command":"incarnate","npcId":7}</c>,
/// <c>{"seq":2,"command":"action","action":"Eat","powerId":null}</c>, <c>{"seq":3,"command":"logout"}</c>, <c>{"seq":4,"command":"ping"}</c>
/// — e a resposta vem pelo mesmo canal: <c>{"seq":1,"ok":true,"status":200,"body":{...}}</c> ou
/// <c>{"seq":2,"ok":false,"status":409,"error":"..."}</c>. Os comandos passam pela mesma fila das rotas HTTP
/// (<see cref="PlayerCommandGateway"/>), com os mesmos status (pausa do admin = 409). As rotas HTTP continuam valendo.</summary>
public static class PlayerChannelEndpoints
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static void MapPlayerChannelEndpoints(this WebApplication app, PlayerCommandGateway gateway, PlayerSessionTokens tokens)
    {
        app.MapGet("/players/ws", async (HttpContext http) =>
        {
            if (tokens.PlayerOf(PlayerEndpoints.BearerOf(http)) is not { } playerId)
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (!http.WebSockets.IsWebSocketRequest)
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[16 * 1024];
            var message = new MemoryStream();
            while (socket.State == WebSocketState.Open && !http.RequestAborted.IsCancellationRequested)
            {
                WebSocketReceiveResult received;
                try { received = await socket.ReceiveAsync(buffer, http.RequestAborted); }
                catch (Exception e) when (e is OperationCanceledException or WebSocketException) { break; }
                if (received.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, received.Count);
                if (!received.EndOfMessage) continue;

                string text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                // a sessão pode vencer com o canal aberto: cada comando confere o token de novo
                var reply = tokens.PlayerOf(PlayerEndpoints.BearerOf(http)) is null
                    ? new JsonObject { ["seq"] = SeqOf(text), ["ok"] = false, ["status"] = 401, ["error"] = "sessão vencida" }
                    : Handle(gateway, playerId, text);
                await socket.SendAsync(Encoding.UTF8.GetBytes(reply.ToJsonString()), WebSocketMessageType.Text, true, http.RequestAborted);
            }

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        });
    }

    private static long? SeqOf(string text)
    {
        try { return JsonNode.Parse(text)?["seq"]?.GetValue<long>(); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    internal static JsonObject Handle(PlayerCommandGateway gateway, string playerId, string text)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(text); }
        catch (JsonException) { node = null; }
        long? seq = null;
        try { seq = node?["seq"]?.GetValue<long>(); }
        catch (Exception e) when (e is InvalidOperationException or FormatException) { }
        string command;
        PlayerCommandRequest? request;
        try
        {
            command = node?["command"]?.GetValue<string>() ?? "";
            request = command switch
            {
            "incarnate" when node?["npcId"] is { } id => new PlayerCommandRequest(PlayerCommandKind.Incarnate, id.GetValue<long>()),
            "action" when PlayerEndpoints.TryParseAction(node?["action"]?.GetValue<string>(), out var action) =>
                new PlayerCommandRequest(PlayerCommandKind.Act, Action: action, PowerId: node?["powerId"]?.GetValue<string>()),
            "logout" => new PlayerCommandRequest(PlayerCommandKind.Logout),
            _ => null,
            };
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        {
            return new JsonObject { ["seq"] = seq, ["ok"] = false, ["status"] = 400, ["error"] = "campo com tipo errado" };
        }
        if (command == "ping") return new JsonObject { ["seq"] = seq, ["ok"] = true, ["status"] = 200, ["body"] = "pong" };
        if (request is null) return new JsonObject { ["seq"] = seq, ["ok"] = false, ["status"] = 400, ["error"] = "comando desconhecido ou incompleto" };

        var reply = gateway.Execute(playerId, request);
        var result = new JsonObject { ["seq"] = seq, ["ok"] = reply.Ok, ["status"] = reply.Status };
        if (reply.Ok) result["body"] = JsonSerializer.SerializeToNode(reply.Body, Wire);
        else result["error"] = reply.Error;
        return result;
    }
}
