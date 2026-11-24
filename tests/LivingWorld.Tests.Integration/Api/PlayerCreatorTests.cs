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
}
