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

    /// <summary>AD-197u: <c>walk</c> com <c>run</c> responde a pose com <c>running</c> e a velocidade de verdade; <c>jump</c> responde a
    /// pose com <c>airMs</c> e recusa o segundo pulo no ar com 409.</summary>
    [Fact]
    public async Task Walk_with_run_and_jump_answer_the_pose_with_running_and_air_time()
    {
        var npc = World().Npcs.First(n => n.IsAlive && n.ControllerId is null
            && n.AgeYears(World().CurrentDate) is >= 25 and <= 45 && n.Health >= 90);
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "runner" })).StatusCode);
        npc.SetSleep(90, World().CurrentDate.TotalHours);

        var walk = await client.PostAsJsonAsync("/players/runner/walk", new { dx = 1000, dy = 0 });
        var walkBody = await walk.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var run = await client.PostAsJsonAsync("/players/runner/walk", new { dx = 1000, dy = 0, run = true });
        var runBody = await run.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var jump = await client.PostAsJsonAsync("/players/runner/jump", new { });
        var jumpBody = await jump.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var again = await client.PostAsync("/players/runner/jump", null);
        await client.PostAsJsonAsync("/players/runner/walk", new { dx = 0, dy = 0 });
        await client.PostAsync("/players/runner/logout", null);

        Assert.Equal(HttpStatusCode.OK, walk.StatusCode);
        Assert.False(walkBody.GetProperty("running").GetBoolean());
        Assert.False(walkBody.TryGetProperty("airMs", out _));
        Assert.True(runBody.GetProperty("running").GetBoolean());
        Assert.True(runBody.GetProperty("speedMps").GetDouble() > walkBody.GetProperty("speedMps").GetDouble() * 1.5);
        Assert.Equal(HttpStatusCode.OK, jump.StatusCode);
        Assert.InRange(jumpBody.GetProperty("airMs").GetInt32(), 400, 600);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }
}
