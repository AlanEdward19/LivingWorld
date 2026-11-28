using System.Net;
using System.Net.Http.Json;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>#264: <c>GET /players/{id}/home</c> e <c>/home/check</c> só existem para quem controla um corpo com casa própria num mundo com o modo comprar (bloco
/// <c>Home</c> do cenário); sem isso são 404 com o motivo, e perguntar nunca muda o mundo.</summary>
public class PlayerHomeEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerHomeEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Home_needs_a_body_and_a_world_with_the_buy_mode()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/players/sem-corpo/home")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/players/sem-corpo/home/check?verb=buy:plant:0:1:1:0")).StatusCode);

        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var npc = world.Npcs.First(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 18);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "casa" })).StatusCode);
        var home = await client.GetAsync("/players/casa/home");
        Assert.Equal(HttpStatusCode.NotFound, home.StatusCode); // o mundo padrão não tem o bloco Home
        Assert.Contains("modo comprar", await home.Content.ReadAsStringAsync());
        var check = await client.GetAsync("/players/casa/home/check?verb=buy:plant:0:1:1:0");
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.Contains("\"ok\":false", await check.Content.ReadAsStringAsync());
        await client.PostAsync("/players/casa/logout", null);
    }
}
