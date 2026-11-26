using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>Canal persistente do jogador (AD-197a, pedido do dono: "manter conexão após conectado, sem demora para interagir"):
/// <c>GET /players/ws</c> com <c>Authorization: Bearer {token}</c> (401 sem sessão válida) abre um WebSocket do próprio ASP.NET Core.
/// Cada mensagem do cliente é um comando com número de sequência — <c>{"seq":1,"command":"incarnate","npcId":7}</c>,
/// <c>{"seq":2,"command":"action","action":"Eat","powerId":null}</c>, <c>{"seq":3,"command":"move","dx":1,"dy":0}</c> (AD-197e), <c>{"seq":4,"command":"strike","npcId":9}</c>,
/// <c>{"seq":5,"command":"logout"}</c>, <c>{"seq":6,"command":"ping"}</c>, AD-197d: <c>{"seq":7,"command":"reach"}</c> e
/// <c>{"seq":8,"command":"interact","kind":"stall","id":3,"verb":"buy","resourceId":1,"quantity":2}</c>
/// — e a resposta vem pelo mesmo canal: <c>{"seq":1,"ok":true,"status":200,"body":{...}}</c> ou
/// <c>{"seq":2,"ok":false,"status":409,"error":"..."}</c>. Os comandos passam pela mesma fila das rotas HTTP
/// (<see cref="PlayerCommandGateway"/>), com os mesmos status (pausa do admin = 409). As rotas HTTP continuam valendo.</summary>
public static class PlayerChannelEndpoints
{
    private static readonly JsonSerializerOptions Wire = LivingWorld.Api.Realtime.WireMillis.Apply(new(JsonSerializerDefaults.Web));

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
                "persuade" => new PlayerCommandRequest(PlayerCommandKind.Persuade),
                // AD-197k: o rumo do passo solto, em milésimos (0, 0 para)
                // AD-197u: "run": true corre (só com fôlego e saúde); "jump" pula
                "walk" => new PlayerCommandRequest(PlayerCommandKind.Walk, Dx: node?["dx"]?.GetValue<int>() ?? 0, Dy: node?["dy"]?.GetValue<int>() ?? 0,
                    Run: node?["run"]?.GetValue<bool>() ?? false),
                "jump" => new PlayerCommandRequest(PlayerCommandKind.Jump),
                "move" => new PlayerCommandRequest(PlayerCommandKind.Move, Dx: node?["dx"]?.GetValue<int>() ?? 0, Dy: node?["dy"]?.GetValue<int>() ?? 0),
                "strike" when node?["npcId"] is { } target => new PlayerCommandRequest(PlayerCommandKind.Strike, target.GetValue<long>()),
                // AD-197c: dormir (até a hora do dia "untilHour", ou acelerando), acelerar parado e acordar: só quem joga sozinho
                "sleep" => new PlayerCommandRequest(PlayerCommandKind.Sleep, UntilHour: node?["untilHour"]?.GetValue<int>()),
                "accelerate" => new PlayerCommandRequest(PlayerCommandKind.Accelerate),
                "wake" => new PlayerCommandRequest(PlayerCommandKind.Wake),
                // AD-197d: interagir com o que está perto, pela oferta do motor ("reach" lista o que se alcança)
                "interact" when node?["kind"] is { } kind && node?["id"] is { } id && node?["verb"] is { } verb =>
                    new PlayerCommandRequest(PlayerCommandKind.Interact, Interaction: kind.GetValue<string>(), TargetId: id.GetValue<long>(),
                        Verb: verb.GetValue<string>(), ResourceId: node?["resourceId"]?.GetValue<int>(), Quantity: node?["quantity"]?.GetValue<long>()),
                // AD-197q: criador de personagem — amostra do agregado (registrada) e entrar como um dos candidatos
                "candidates" when node?["cityId"] is { } city && PlayerEndpoints.TryParseFilter(node?["sex"]?.GetValue<string>(),
                        node?["minAge"]?.GetValue<int>(), node?["maxAge"]?.GetValue<int>(), node?["professionId"]?.GetValue<int>(), out var filter) =>
                    new PlayerCommandRequest(PlayerCommandKind.Sample, CityId: Guid.Parse(city.GetValue<string>()), Filter: filter,
                        Count: node?["count"]?.GetValue<int>()),
                "create" when node?["cityId"] is { } city && node?["npcId"] is { } id && node?["sample"] is { } sample =>
                    new PlayerCommandRequest(PlayerCommandKind.Create, id.GetValue<long>(), CityId: Guid.Parse(city.GetValue<string>()),
                        SampleNumber: sample.GetValue<long>()),
                // AD-197t: criador de personagem de verdade — a escolha inteira em "spec" (a mesma forma da rota)
                "character" when PlayerEndpoints.TryParseSpec(node?["spec"], out var spec, out _) =>
                    new PlayerCommandRequest(PlayerCommandKind.Character, Spec: spec),
                _ => null,
            };
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        {
            return new JsonObject { ["seq"] = seq, ["ok"] = false, ["status"] = 400, ["error"] = "campo com tipo errado" };
        }
        if (command == "ping") return new JsonObject { ["seq"] = seq, ["ok"] = true, ["status"] = 200, ["body"] = "pong" };
        if (command == "reach")
            return gateway.Reach(playerId) is { } offers
                ? new JsonObject { ["seq"] = seq, ["ok"] = true, ["status"] = 200, ["body"] = JsonSerializer.SerializeToNode(new { offers }, Wire) }
                : new JsonObject { ["seq"] = seq, ["ok"] = false, ["status"] = 404, ["error"] = $"{PlayerCommands.NotFound} jogador não controla nenhum NPC" };
        if (request is null) return new JsonObject { ["seq"] = seq, ["ok"] = false, ["status"] = 400, ["error"] = "comando desconhecido ou incompleto" };

        var reply = gateway.Execute(playerId, request);
        var result = new JsonObject { ["seq"] = seq, ["ok"] = reply.Ok, ["status"] = reply.Status };
        if (reply.Ok) result["body"] = JsonSerializer.SerializeToNode(reply.Body, Wire);
        else result["error"] = reply.Error;
        return result;
    }
}
