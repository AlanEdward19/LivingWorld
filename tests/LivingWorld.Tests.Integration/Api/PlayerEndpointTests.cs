using System.Net;
using System.Net.Http.Json;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api;

/// <summary>player-incarnation: <c>/players/*</c> (200, 404, 409).</summary>
public class PlayerEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<WorldHost>().Current;
    }

    [Fact]
    public async Task Incarnate_act_and_logout_walk_the_whole_flow()
    {
        var npc = World().Npcs.First(n => n.IsAlive && n.ControllerId is null);
        var client = _factory.CreateClient();

        var incarnate = await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "e2e" });
        var conflict = await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "outro" });
        var act = await client.PostAsJsonAsync("/players/e2e/action", new { action = "Idle" });
        var badAct = await client.PostAsJsonAsync("/players/e2e/action", new { action = "UsePower" });
        var logout = await client.PostAsync("/players/e2e/logout", null);
        var again = await client.PostAsync("/players/e2e/logout", null);

        Assert.Equal(HttpStatusCode.OK, incarnate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.OK, act.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badAct.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.NotNull(npc.DisappearedAtTick);
    }

    [Fact]
    public async Task Incarnating_an_unknown_npc_is_404()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/players/987654321/incarnate", new { playerId = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
