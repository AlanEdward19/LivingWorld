using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Formas congeladas de <c>/players/*</c> que o HUD do cliente Godot interpreta (#59 fatia D). Factory própria:
/// encarnar e sair mexe no NPC controlado do mundo.</summary>
public class PlayerContractTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerContractTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Incarnate_action_and_logout_shapes_match_the_committed_contracts()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        long npcId = world.Npcs.First(n => n.IsAlive).Id.Value;
        var client = _factory.CreateClient();

        var incarnate = await client.PostAsJsonAsync($"/players/{npcId}/incarnate", new { playerId = "contrato" });
        Assert.Equal(HttpStatusCode.OK, incarnate.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.player-incarnate.json", ApiContractTests.ShapeOf(JsonNode.Parse(await incarnate.Content.ReadAsStringAsync())));

        var action = await client.PostAsJsonAsync("/players/contrato/action", new { action = "Eat" });
        Assert.Equal(HttpStatusCode.OK, action.StatusCode);
        var actionShape = ApiContractTests.ShapeOf(JsonNode.Parse(await action.Content.ReadAsStringAsync()))!;
        actionShape["action"] = "null"; // devolver a escolha ao motor responde action nulo
        ApiContractTests.AssertMatchesContract("shape.player-action.json", actionShape);

        var logout = await client.PostAsync("/players/contrato/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.player-logout.json", ApiContractTests.ShapeOf(JsonNode.Parse(await logout.Content.ReadAsStringAsync())));
    }
}
