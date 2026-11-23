using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Api.Players;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api;

/// <summary>ADR-0048 (AD-197a): sessão por token, 401/403, canal persistente do jogador e pausa do admin = 409 em toda rota.</summary>
public class PlayerSessionTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerSessionTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World() => _factory.Services.GetRequiredService<WorldHost>().Current;

    private long FreeNpc() => World().Npcs.First(n => n.IsAlive && n.ControllerId is null && n.DisappearedAtTick is null).Id.Value;

    private async Task<(HttpClient Client, SessionResponse Session)> Session()
    {
        var client = _factory.CreateClient();
        var session = (await (await client.PostAsync("/sessions", null)).Content.ReadFromJsonAsync<SessionResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.SessionToken);
        return (client, session);
    }

    [Fact]
    public async Task The_player_comes_from_the_session_and_a_foreign_or_unknown_token_is_refused()
    {
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var (client, session) = await Session();
        long npc = FreeNpc();

        var incarnate = await client.PostAsJsonAsync($"/players/{npc}/incarnate", new { });
        string? controller = World().FindNpc(new LivingWorld.Domain.Shared.NpcId(npc))!.ControllerId;
        var act = await client.PostAsJsonAsync($"/players/{session.PlayerId}/action", new { action = "Idle" });
        var foreign = await client.PostAsJsonAsync("/players/outro/action", new { action = "Idle" });
        var bad = _factory.CreateClient();
        bad.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "nao-existe");
        var unknown = await bad.PostAsync($"/players/{session.PlayerId}/logout", null);
        var logout = await client.PostAsync($"/players/{session.PlayerId}/logout", null);

        Assert.Equal(HttpStatusCode.OK, incarnate.StatusCode);
        Assert.Equal(session.PlayerId, controller);
        Assert.Equal(HttpStatusCode.OK, act.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
    }

    [Fact]
    public async Task With_auth_required_a_request_without_token_is_401_and_the_local_mode_keeps_the_old_routes()
    {
        using var required = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["PLAYER_AUTH"] = "required" })));
        var anonymous = await required.CreateClient().PostAsync("/players/alguem/logout", null);
        var local = await _factory.CreateClient().PostAsync("/players/ninguem-local/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, local.StatusCode); // modo local: o playerId da rota vale, e ele não controla ninguém
    }

    private async Task<WebSocket> Channel(string token)
    {
        var ws = _factory.Server.CreateWebSocketClient();
        ws.ConfigureRequest = r => r.Headers.Authorization = "Bearer " + token;
        return await ws.ConnectAsync(new Uri(_factory.Server.BaseAddress, "players/ws"), CancellationToken.None);
    }

    internal static async Task<JsonObject> Send(WebSocket socket, string json)
    {
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[64 * 1024];
        var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
        return JsonNode.Parse(Encoding.UTF8.GetString(buffer, 0, received.Count))!.AsObject();
    }

    [Fact]
    public async Task The_persistent_channel_carries_numbered_commands_and_their_replies()
    {
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var (_, session) = await Session();
        long npc = FreeNpc();
        using var socket = await Channel(session.SessionToken);

        var incarnate = await Send(socket, $$"""{"seq":1,"command":"incarnate","npcId":{{npc}}}""");
        var act = await Send(socket, """{"seq":2,"command":"action","action":"Eat"}""");
        var refused = await Send(socket, """{"seq":3,"command":"action","action":"Travel"}""");
        var ping = await Send(socket, """{"seq":4,"command":"ping"}""");
        var logout = await Send(socket, """{"seq":5,"command":"logout"}""");

        Assert.Equal((1L, true, 200), (incarnate["seq"]!.GetValue<long>(), incarnate["ok"]!.GetValue<bool>(), incarnate["status"]!.GetValue<int>()));
        Assert.Equal((2L, true), (act["seq"]!.GetValue<long>(), act["ok"]!.GetValue<bool>()));
        Assert.Equal((3L, false, 400), (refused["seq"]!.GetValue<long>(), refused["ok"]!.GetValue<bool>(), refused["status"]!.GetValue<int>()));
        Assert.Equal("pong", ping["body"]!.GetValue<string>());
        Assert.Equal(200, logout["status"]!.GetValue<int>());
        Assert.NotNull(World().FindNpc(new LivingWorld.Domain.Shared.NpcId(npc))!.DisappearedAtTick);
    }

    [Fact]
    public async Task The_channel_without_a_valid_session_is_refused()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Channel("nao-existe"));
    }

    /// <summary>Sob pausa do admin, cada rota POST <c>/players/*</c> (achada por reflexão nos endpoints do host) e o canal respondem
    /// 409 e o hash não muda. Devolve as rotas que deixaram passar; reprova se aparecer rota sem cobertura.</summary>
    private async Task<List<string>> LeaksUnderPause()
    {
        var simulation = _factory.Services.GetRequiredService<SimulationHost>();
        simulation.Resume();
        var (client, session) = await Session(); // controla um NPC: ação e saída mudariam o mundo
        var (fresh, freshSession) = await Session(); // não controla ninguém: encarnar mudaria o mundo
        long npc = FreeNpc();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/players/{npc}/incarnate", new { })).StatusCode);
        long other = FreeNpc();
        simulation.PauseFor("admin", "teste", 10, World().CurrentDate.TotalHours);
        string hash = WorldSnapshot.CanonicalHash(World());

        var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/players/", StringComparison.Ordinal)
                && e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods.Contains("POST"))
            .Select(e => e.RoutePattern.RawText!).OrderBy(r => r).ToList();
        Assert.Equal(["/players/{npcId:long}/incarnate", "/players/{playerId}/action", "/players/{playerId}/logout"], routes); // rota nova sem cobertura reprova

        var leaks = new List<string>();
        foreach (string route in routes)
        {
            string url = route.Replace("{npcId:long}", other.ToString()).Replace("{playerId}", session.PlayerId);
            var response = await (route.Contains("{npcId", StringComparison.Ordinal) ? fresh : client).PostAsJsonAsync(url, new { action = "Idle" });
            if (response.StatusCode != HttpStatusCode.Conflict) leaks.Add(route);
        }

        using (var freshSocket = await Channel(freshSession.SessionToken))
            if ((await Send(freshSocket, $$"""{"seq":1,"command":"incarnate","npcId":{{other}}}"""))["status"]!.GetValue<int>() != 409)
                leaks.Add("ws:incarnate");
        using (var socket = await Channel(session.SessionToken))
            foreach (string command in new[] { """{"seq":2,"command":"action","action":"Idle"}""", """{"seq":3,"command":"logout"}""" })
                if ((await Send(socket, command))["status"]!.GetValue<int>() != 409) leaks.Add("ws:" + command);

        if (WorldSnapshot.CanonicalHash(World()) != hash) leaks.Add("hash");
        simulation.Resume();
        await client.PostAsync($"/players/{session.PlayerId}/logout", null);
        await fresh.PostAsync($"/players/{freshSession.PlayerId}/logout", null);
        return leaks;
    }

    [Fact]
    public async Task Under_admin_pause_every_player_route_and_the_channel_refuse_with_409_and_the_hash_stays()
    {
        Assert.Empty(await LeaksUnderPause());
    }

    [Fact]
    public async Task Mutation_without_the_pause_check_the_pause_criterion_fails()
    {
        var gateway = _factory.Services.GetRequiredService<PlayerCommandGateway>();
        gateway.EnforcePause = false;
        try
        {
            Assert.NotEmpty(await LeaksUnderPause());
        }
        finally
        {
            gateway.EnforcePause = true;
        }
    }
}
