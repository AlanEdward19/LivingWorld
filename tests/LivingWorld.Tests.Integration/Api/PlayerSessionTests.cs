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

    /// <summary>#252 (sandbox do casal): trocar de corpo pela rota solta o NPC sem fazê-lo sumir e passa ao outro; sem corpo, 404; alvo sem dono é o único aceito.</summary>
    [Fact]
    public async Task Switching_bodies_releases_the_old_one_without_it_disappearing_and_takes_the_other()
    {
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var (client, session) = await Session();
        var free = World().Npcs.Where(n => n.IsAlive && n.ControllerId is null && n.DisappearedAtTick is null).Take(2).ToList();
        long first = free[0].Id.Value, second = free[1].Id.Value;

        var without = await client.PostAsJsonAsync($"/players/{session.PlayerId}/switch", new { npcId = second }); // ainda sem corpo
        await client.PostAsJsonAsync($"/players/{first}/incarnate", new { });
        var same = await client.PostAsJsonAsync($"/players/{session.PlayerId}/switch", new { npcId = first });
        var switched = await client.PostAsJsonAsync($"/players/{session.PlayerId}/switch", new { npcId = second });
        var body = (await switched.Content.ReadFromJsonAsync<JsonObject>())!;

        Assert.Equal(HttpStatusCode.NotFound, without.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        Assert.Equal(second, body["npcId"]!.GetValue<long>());
        var old = World().FindNpc(new LivingWorld.Domain.Shared.NpcId(first))!;
        Assert.Null(old.ControllerId);
        Assert.Null(old.DisappearedAtTick); // não é o logout
        Assert.Equal(session.PlayerId, World().FindNpc(new LivingWorld.Domain.Shared.NpcId(second))!.ControllerId);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/players/{session.PlayerId}/logout", null)).StatusCode);
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
    public async Task The_move_command_steps_only_the_body_the_session_controls_and_answers_the_pose()
    {
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var (client, session) = await Session();
        var (_, idle) = await Session();
        long npc = FreeNpc();
        using var socket = await Channel(session.SessionToken);
        using var idleSocket = await Channel(idle.SessionToken);

        var nobody = await Send(idleSocket, """{"seq":1,"command":"move","dx":1,"dy":0}""");
        Assert.Equal(200, (await Send(socket, $$"""{"seq":1,"command":"incarnate","npcId":{{npc}}}"""))["status"]!.GetValue<int>());
        var body = World().FindNpc(new LivingWorld.Domain.Shared.NpcId(npc))!;
        var start = body.CurrentLocation;
        JsonObject step = null!;
        int seq = 2;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) }) // borda, parede (AD-197d) ou gente
        {
            step = await Send(socket, $$"""{"seq":{{seq++}},"command":"move","dx":{{dx}},"dy":{{dy}}}""");
            if (step["status"]!.GetValue<int>() == 200) break;
        }
        var http = await client.PostAsJsonAsync($"/players/{session.PlayerId}/move", new { dx = 0, dy = 3 });

        Assert.Equal(404, nobody["status"]!.GetValue<int>());
        Assert.Equal(200, step["status"]!.GetValue<int>());
        Assert.Equal((body.CurrentLocation.X, body.CurrentLocation.Y), (step["body"]!["x"]!.GetValue<int>(), step["body"]!["y"]!.GetValue<int>()));
        Assert.NotEqual(start, body.CurrentLocation);
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode); // intenção é um passo, não um salto
        await client.PostAsync($"/players/{session.PlayerId}/logout", null);
    }

    [Fact]
    public async Task Full_possession_is_admin_only_and_the_possessor_can_look_at_the_captive()
    {
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var (client, session) = await Session();
        long npc = FreeNpc();

        var withoutAdmin = await client.PostAsync($"/players/{npc}/possess", null);
        client.DefaultRequestHeaders.Add("X-Admin-Token", LivingWorldApiFactory.AdminToken);
        var possess = await client.PostAsync($"/players/{npc}/possess", null);
        var captive = await client.GetAsync($"/players/{session.PlayerId}/captive");
        var (other, _) = await Session();
        var foreign = await other.GetAsync($"/players/{session.PlayerId}/captive");
        await client.PostAsync($"/players/{session.PlayerId}/logout", null);
        var released = await client.GetAsync($"/players/{session.PlayerId}/captive");

        Assert.Equal(HttpStatusCode.Forbidden, withoutAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, possess.StatusCode);
        Assert.Equal(npc, JsonNode.Parse(await captive.Content.ReadAsStringAsync())!["npcId"]!.GetValue<long>());
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode); // só quem possui vê a pessoa presa
        Assert.Equal(HttpStatusCode.NotFound, released.StatusCode);
    }

    /// <summary>#234: o golpe responde o que o cliente anima (resultado, dano, reação do alvo, recarga) e a pose com o combate; a
    /// esquiva e o bloqueio têm rota e canal, e a recarga da guarda e do golpe recusa com 409 e o motivo.</summary>
    [Fact]
    public async Task Strike_dodge_and_block_answer_what_the_client_animates_over_http_and_the_channel()
    {
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var (client, session) = await Session();
        client.DefaultRequestHeaders.Add("X-Admin-Token", LivingWorldApiFactory.AdminToken); // posse plena: sem recusa de valores
        var world = World();
        var body = world.Npcs.First(n => n.IsAlive && n.ControllerId is null && n.DisappearedAtTick is null);
        var target = world.Npcs.First(n => n.IsAlive && n.ControllerId is null && n.DisappearedAtTick is null && n.Id != body.Id && n.City == body.City);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/players/{body.Id.Value}/possess", null)).StatusCode);
        target.ExitBuilding();
        target.MoveTo(body.CurrentLocation, world.CurrentDate.TotalHours);

        var strike = await client.PostAsJsonAsync($"/players/{session.PlayerId}/strike", new { npcId = target.Id.Value });
        Assert.Equal(HttpStatusCode.OK, strike.StatusCode);
        var hit = JsonNode.Parse(await strike.Content.ReadAsStringAsync())!;
        Assert.Equal("Hit", hit["outcome"]!.GetValue<string>());
        Assert.True(hit["damage"]!.GetValue<int>() > 0 || hit["lethal"]!.GetValue<bool>());
        Assert.True(hit["readyInMs"]!.GetValue<long>() > 0);
        var shape = LivingWorld.Tests.Integration.Visual.ApiContractTests.ShapeOf(hit)!;
        LivingWorld.Tests.Integration.Visual.ApiContractTests.Optional(shape, "reaction", "string"); // só quem apanhou e ficou vivo reage
        LivingWorld.Tests.Integration.Visual.ApiContractTests.Optional(shape["pose"]!, "buildingId", "number"); // nulo na rua
        var combat = shape["pose"]!["combat"]!;
        LivingWorld.Tests.Integration.Visual.ApiContractTests.Optional(combat, "guard", "string");
        LivingWorld.Tests.Integration.Visual.ApiContractTests.Optional(combat, "lastIncoming", new JsonObject { ["agoMs"] = "number", ["by"] = "number", ["damage"] = "number", ["outcome"] = "string" });
        LivingWorld.Tests.Integration.Visual.ApiContractTests.AssertMatchesContract("shape.player-strike.json", shape);

        target.MoveTo(body.CurrentLocation, world.CurrentDate.TotalHours); // quem apanhou pode ter recuado ou fugido
        var again = await client.PostAsJsonAsync($"/players/{session.PlayerId}/strike", new { npcId = target.Id.Value });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode); // ainda se recupera do golpe
        Assert.Contains("ainda se recupera", await again.Content.ReadAsStringAsync());

        var dodge = await client.PostAsync($"/players/{session.PlayerId}/dodge", null);
        Assert.Equal(HttpStatusCode.OK, dodge.StatusCode);
        var pose = JsonNode.Parse(await dodge.Content.ReadAsStringAsync())!;
        Assert.Equal("Dodge", pose["combat"]!["guard"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/players/{session.PlayerId}/block", null)).StatusCode); // a guarda recarrega

        using var socket = await Channel(session.SessionToken);
        var blocked = await Send(socket, """{"seq":1,"command":"block"}""");
        Assert.Equal(409, blocked["status"]!.GetValue<int>());
        await client.PostAsync($"/players/{session.PlayerId}/logout", null);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5214", false)]
    [InlineData("http://localhost:5214", false)]
    [InlineData("http://[::1]:5214", false)]
    [InlineData("http://0.0.0.0:5214", true)]
    [InlineData("http://+:80", true)]
    [InlineData("http://*:80", true)]
    [InlineData("http://192.168.1.20:5214", true)]
    [InlineData("http://servidor.exemplo:5214", true)]
    public void A_server_listening_outside_loopback_is_public_and_requires_a_token(string address, bool isPublic)
    {
        Assert.Equal(isPublic, PlayerAuthPolicy.IsPublic([address]));
    }

    [Fact]
    public async Task A_loopback_kestrel_keeps_the_local_mode_and_explicit_configuration_wins()
    {
        using var local = new LivingWorldApiFactory();
        local.UseKestrel(0); // escuta só em loopback
        local.StartServer();
        string address = local.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(address) };

        var anonymous = await http.PostAsync("/players/ninguem/logout", null);

        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode); // sem token, modo local: chega à fila e não controla ninguém
        Assert.True(new PlayerAuthPolicy("required", local.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()).Required);
        Assert.False(new PlayerAuthPolicy("local", local.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()).Required);
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
        Assert.Equal(["/players/{npcId:long}/incarnate", "/players/{npcId:long}/possess", "/players/{playerId}/accelerate",
            "/players/{playerId}/action", "/players/{playerId}/block", "/players/{playerId}/candidates", "/players/{playerId}/character", "/players/{playerId}/character/preview", "/players/{playerId}/create", "/players/{playerId}/dodge", "/players/{playerId}/go", "/players/{playerId}/interact", "/players/{playerId}/interrupt", "/players/{playerId}/jump", "/players/{playerId}/logout", "/players/{playerId}/move", "/players/{playerId}/persuade",
            "/players/{playerId}/sleep", "/players/{playerId}/speed", "/players/{playerId}/strike", "/players/{playerId}/switch", "/players/{playerId}/wake", "/players/{playerId}/walk"], routes); // rota nova sem cobertura reprova
        fresh.DefaultRequestHeaders.Add("X-Admin-Token", LivingWorldApiFactory.AdminToken); // a posse plena é do admin

        var leaks = new List<string>();
        foreach (string route in routes)
        {
            string url = route.Replace("{npcId:long}", other.ToString()).Replace("{playerId}", session.PlayerId);
            var response = await (route.Contains("{npcId", StringComparison.Ordinal) ? fresh : client).PostAsJsonAsync(url, new { action = "Idle", dx = 1, dy = 0, npcId = other });
            if (response.StatusCode != HttpStatusCode.Conflict) leaks.Add(route);
        }

        using (var freshSocket = await Channel(freshSession.SessionToken))
            if ((await Send(freshSocket, $$"""{"seq":1,"command":"incarnate","npcId":{{other}}}"""))["status"]!.GetValue<int>() != 409)
                leaks.Add("ws:incarnate");
        using (var socket = await Channel(session.SessionToken))
            foreach (string command in new[] { """{"seq":2,"command":"action","action":"Idle"}""", """{"seq":3,"command":"move","dx":1,"dy":0}""",
                $$"""{"seq":4,"command":"strike","npcId":{{other}}}""", """{"seq":6,"command":"sleep","untilHour":6}""",
                $$"""{"seq":9,"command":"interact","kind":"person","id":{{other}},"verb":"talk"}""", """{"seq":10,"command":"walk","dx":1000,"dy":0,"run":true}""", """{"seq":13,"command":"jump"}""", $$"""{"seq":16,"command":"switch","npcId":{{other}}}""", """{"seq":14,"command":"dodge"}""", """{"seq":15,"command":"block"}""",
                """{"seq":7,"command":"accelerate"}""", """{"seq":17,"command":"speed","speed":2}""", """{"seq":8,"command":"wake"}""", """{"seq":5,"command":"logout"}""",
                $$"""{"seq":11,"command":"candidates","cityId":"{{World().ActiveCities().First().Id.Value}}"}""",
                $$"""{"seq":12,"command":"create","cityId":"{{World().ActiveCities().First().Id.Value}}","npcId":{{other}},"sample":0}""" })
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
