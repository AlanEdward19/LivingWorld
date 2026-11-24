using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Integration.Visual;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api;

/// <summary>AD-197q: criador de personagem pela API (rotas e canal persistente), com as formas congeladas que o cliente lê.</summary>
public class PlayerCreatorTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerCreatorTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World() => _factory.Services.GetRequiredService<WorldHost>().Current;

    [Fact]
    public async Task Empty_aggregate_is_409_then_a_sample_and_entering_move_one_person_out_of_it()
    {
        _factory.ResetCanonicalWorld(seed: 4);
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var world = World();
        var city = world.ActiveCities().First();
        var client = _factory.CreateClient();
        string hash = WorldSnapshot.CanonicalHash(world);

        var empty = await client.PostAsJsonAsync("/players/criador/candidates", new { cityId = city.Id.Value });
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));

        city.AbsorbPool(new AggregatePopulationPool(20, 20 * 300, 20 * 70), world.ReserveNpcIdBlock(20));
        long population = CityPopulationQuery.Population(world, city.Id);
        var origins = await client.GetAsync("/players/origins");
        ApiContractTests.AssertMatchesContract("shape.player-origins.json", ApiContractTests.ShapeOf(JsonNode.Parse(await origins.Content.ReadAsStringAsync())));

        var bad = await client.PostAsJsonAsync("/players/criador/candidates", new { cityId = city.Id.Value, sex = "Outro" });
        var sample = await client.PostAsJsonAsync("/players/criador/candidates", new { cityId = city.Id.Value, count = 4 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sample.StatusCode);
        var body = JsonNode.Parse(await sample.Content.ReadAsStringAsync())!;
        ApiContractTests.AssertMatchesContract("shape.player-candidates.json", ApiContractTests.ShapeOf(body));
        var chosen = body["candidates"]!.AsArray().First(c => !c!["willPrevents"]!.GetValue<bool>())!;
        long npcId = chosen["npcId"]!.GetValue<long>();

        var create = await client.PostAsJsonAsync("/players/criador/create",
            new { cityId = city.Id.Value, npcId, sample = body["sample"]!.GetValue<long>() });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.player-create.json", ApiContractTests.ShapeOf(JsonNode.Parse(await create.Content.ReadAsStringAsync())));
        Assert.Equal("criador", world.FindNpc(new LivingWorld.Domain.Shared.NpcId(npcId))!.ControllerId);
        Assert.Equal(population, CityPopulationQuery.Population(world, city.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/players/criador/logout", null)).StatusCode);
    }

    [Fact]
    public async Task The_persistent_channel_carries_the_sample_and_the_entry()
    {
        _factory.ResetCanonicalWorld(seed: 6);
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var world = World();
        var city = world.ActiveCities().First();
        city.AbsorbPool(new AggregatePopulationPool(10, 10 * 300, 10 * 70), world.ReserveNpcIdBlock(10));
        var client = _factory.CreateClient();
        var session = (await (await client.PostAsync("/sessions", null)).Content.ReadFromJsonAsync<LivingWorld.Api.Players.SessionResponse>())!;
        var ws = _factory.Server.CreateWebSocketClient();
        ws.ConfigureRequest = r => r.Headers.Authorization = "Bearer " + session.SessionToken;
        using var socket = await ws.ConnectAsync(new Uri(_factory.Server.BaseAddress, "players/ws"), CancellationToken.None);

        var sample = await PlayerSessionTests.Send(socket, $$"""{"seq":1,"command":"candidates","cityId":"{{city.Id.Value}}","sex":"Female"}""");
        Assert.Equal(200, sample["status"]!.GetValue<int>());
        var candidates = sample["body"]!["candidates"]!.AsArray();
        Assert.All(candidates, c => Assert.Equal("Female", c!["sex"]!.GetValue<string>()));
        var chosen = candidates.First(c => !c!["willPrevents"]!.GetValue<bool>())!;
        var create = await PlayerSessionTests.Send(socket,
            $$"""{"seq":2,"command":"create","cityId":"{{city.Id.Value}}","npcId":{{chosen["npcId"]}},"sample":{{sample["body"]!["sample"]}}}""");

        Assert.Equal(200, create["status"]!.GetValue<int>());
        Assert.Equal(session.PlayerId, world.FindNpc(new LivingWorld.Domain.Shared.NpcId(chosen["npcId"]!.GetValue<long>()))!.ControllerId);
        var log = _factory.Services.GetRequiredService<LivingWorld.Api.Players.PlayerCommandGateway>().Log.Records;
        Assert.Contains(log, r => r.Kind == LivingWorld.Simulation.Players.PlayerCommandKind.Sample);
        Assert.Contains(log, r => r.Kind == LivingWorld.Simulation.Players.PlayerCommandKind.Create);
        await PlayerSessionTests.Send(socket, """{"seq":3,"command":"logout"}""");
    }

    /// <summary>AD-197t: o criador de verdade pela API: o que a cidade oferece, a prévia que só lê, a criação e a recusa sem efeito.</summary>
    [Fact]
    public async Task The_character_creator_offers_previews_without_effect_creates_and_refuses_the_incoherent_with_the_world_intact()
    {
        _factory.ResetCanonicalWorld(seed: 8);
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var world = World();
        var city = world.ActiveCities().First();
        var client = _factory.CreateClient();

        var offer = await client.GetAsync($"/players/origins/{city.Id.Value}");
        Assert.Equal(HttpStatusCode.OK, offer.StatusCode);
        var offered = JsonNode.Parse(await offer.Content.ReadAsStringAsync())!;
        ApiContractTests.AssertMatchesContract("shape.player-city-options.json", ApiContractTests.ShapeOf(offered));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/players/origins/{Guid.NewGuid()}")).StatusCode);
        var parent = offered["families"]!.AsArray().SelectMany(f => f!["members"]!.AsArray())
            .First(m => m!["roles"]!["child"]!["available"]!.GetValue<bool>())!;

        string hash = WorldSnapshot.CanonicalHash(world);
        int logged = _factory.Services.GetRequiredService<LivingWorld.Api.Players.PlayerCommandGateway>().Log.Records.Count;
        var spec = new
        {
            cityId = city.Id.Value, name = "Lia Torres", sex = "Female", ageYears = parent["roles"]!["child"]!["maxAge"]!.GetValue<int>(), seed = 3,
            money = 40, look = new { heightM = 1.63, skinTone = 30, eyeTone = 12, outfit = 2 },
            traits = new { openness = 70 }, role = new { kind = "child", relativeId = parent["npcId"]!.GetValue<long>() },
        };
        var preview = await client.PostAsJsonAsync("/players/criadora/character/preview", spec);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewed = JsonNode.Parse(await preview.Content.ReadAsStringAsync())!;
        ApiContractTests.AssertMatchesContract("shape.player-character-preview.json", ApiContractTests.ShapeOf(previewed));
        Assert.Equal(12, previewed["character"]!["look"]!["eyeTone"]!.GetValue<int>());
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world)); // a prévia só lê
        Assert.Equal(logged, _factory.Services.GetRequiredService<LivingWorld.Api.Players.PlayerCommandGateway>().Log.Records.Count);

        var tooOld = await client.PostAsJsonAsync("/players/criadora/character", new { cityId = city.Id.Value, ageYears = 150 });
        var noCity = await client.PostAsJsonAsync("/players/criadora/character", new { cityId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, tooOld.StatusCode);
        Assert.Contains("longevidade", await tooOld.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, noCity.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/players/criadora/character", new { cityId = city.Id.Value, sex = "Outro" })).StatusCode);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world)); // recusa sem efeito

        var create = await client.PostAsJsonAsync("/players/criadora/character", spec);

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = JsonNode.Parse(await create.Content.ReadAsStringAsync())!;
        ApiContractTests.AssertMatchesContract("shape.player-character.json", ApiContractTests.ShapeOf(created));
        var npc = world.FindNpc(new LivingWorld.Domain.Shared.NpcId(created["npcId"]!.GetValue<long>()))!;
        Assert.Equal("criadora", npc.ControllerId);
        Assert.Equal("Lia Torres", npc.Name);
        Assert.Equal(12, npc.Appearance!.EyeTone);
        Assert.Equal(40, npc.Wallet.Amount);
        Assert.Contains(_factory.Services.GetRequiredService<LivingWorld.Api.Players.PlayerCommandGateway>().Log.Records,
            r => r.Kind == LivingWorld.Simulation.Players.PlayerCommandKind.Character && r.Spec!.Name == "Lia Torres");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/players/criadora/logout", null)).StatusCode);
    }

    [Fact]
    public async Task The_persistent_channel_carries_the_character()
    {
        _factory.ResetCanonicalWorld(seed: 9);
        _factory.Services.GetRequiredService<SimulationHost>().Resume();
        var world = World();
        var city = world.ActiveCities().First();
        var client = _factory.CreateClient();
        var session = (await (await client.PostAsync("/sessions", null)).Content.ReadFromJsonAsync<LivingWorld.Api.Players.SessionResponse>())!;
        var ws = _factory.Server.CreateWebSocketClient();
        ws.ConfigureRequest = r => r.Headers.Authorization = "Bearer " + session.SessionToken;
        using var socket = await ws.ConnectAsync(new Uri(_factory.Server.BaseAddress, "players/ws"), CancellationToken.None);

        var created = await PlayerSessionTests.Send(socket,
            $$$"""{"seq":1,"command":"character","spec":{"cityId":"{{{city.Id.Value}}}","name":"Nina Reis","money":30,"role":{"kind":"newcomer"}}}""");

        Assert.Equal(200, created["status"]!.GetValue<int>());
        var npc = world.FindNpc(new LivingWorld.Domain.Shared.NpcId(created["body"]!["npcId"]!.GetValue<long>()))!;
        Assert.Equal(session.PlayerId, npc.ControllerId);
        Assert.NotNull(npc.ArrivedAtTick);
        var refused = await PlayerSessionTests.Send(socket, $$$"""{"seq":2,"command":"character","spec":{"cityId":"{{{city.Id.Value}}}"}}""");
        Assert.Equal(409, refused["status"]!.GetValue<int>()); // já controla alguém
        await PlayerSessionTests.Send(socket, """{"seq":3,"command":"logout"}""");
    }
}

