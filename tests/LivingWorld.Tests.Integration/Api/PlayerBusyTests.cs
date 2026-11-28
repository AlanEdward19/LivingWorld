using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;
using LivingWorld.Tests.Integration.Visual;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api;

/// <summary>#241: a ação que ocupa o corpo aparece na pose (<c>busy</c>, aditivo e opcional), o passo recusado é 409 e
/// <c>POST /players/{id}/interrupt</c> (também no canal) devolve a pose livre (o que já saiu da árvore fica nas mãos).</summary>
public class PlayerBusyTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerBusyTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cutting_wood_shows_busy_in_the_pose_blocks_the_walk_and_interrupt_frees_the_body()
    {
        var client = _factory.CreateClient();
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var npc = world.Npcs.First(n => n.IsAlive && n.ControllerId is null && n.AgeYears(world.CurrentDate) >= 18 && n.Household is not null);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "lenha" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/players/lenha/interrupt", null)).StatusCode); // nada em andamento

        var tree = world.Map.Cells.Select(c => c.Coord).Where(c => PlayerNature.TreeAt(world, c) is not null)
            .OrderBy(c => c.Y).ThenBy(c => c.X).First();
        npc.ExitBuilding();
        npc.MoveTo(tree, world.CurrentDate.TotalHours);
        npc.SetFine(null);

        var reach = JsonNode.Parse(await (await client.GetAsync("/players/lenha/reach")).Content.ReadAsStringAsync())!;
        var offer = reach["offers"]!.AsArray().Single(o => o!["kind"]!.GetValue<string>() == "nature")!;
        Assert.Equal("Árvore", offer["label"]!.GetValue<string>());
        Assert.True(offer["fx"]!.GetValue<double>() > tree.X - 0.01); // o tronco, em células

        var cut = await client.PostAsJsonAsync("/players/lenha/interact", new { kind = "nature", id = offer["id"]!.GetValue<long>(), verb = "gather" });
        Assert.Equal(HttpStatusCode.OK, cut.StatusCode);
        var pose = JsonNode.Parse(await cut.Content.ReadAsStringAsync())!["pose"]!;
        var busy = pose["busy"]!;
        Assert.Equal("gather", busy["action"]!.GetValue<string>());
        long total = busy["totalMs"]!.GetValue<long>();
        Assert.True(total >= 1);
        // #243: a sessão é o período todo até a árvore acabar (as unidades que a oferta disse); nada entregue de antemão, a madeira sai aos poucos
        Assert.Equal(offer["resources"]![0]!["quantity"]!.GetValue<long>(), busy["units"]!.GetValue<long>());
        Assert.Equal(0, busy["done"]!.GetValue<long>());
        Assert.Equal(world.StreetRules.HouseMaterialResourceId, busy["resourceId"]!.GetValue<int>());
        Assert.False(npc.IsCarrying);
        Assert.Equal(total, busy["remainingMs"]!.GetValue<long>());
        Assert.Equal(busy["untilMs"]!.GetValue<long>() - total, world.CurrentDate.TotalMilliseconds);

        var shape = ApiContractTests.ShapeOf(JsonNode.Parse(await (await client.PostAsJsonAsync("/players/lenha/walk", new { dx = 0, dy = 0 })).Content.ReadAsStringAsync())!)!;
        Assert.NotNull(shape["busy"]); // a pose parada também diz que o corpo está ocupado
        var blocked = await client.PostAsJsonAsync("/players/lenha/walk", new { dx = 1000, dy = 0 });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var trees = JsonNode.Parse(await client.GetStringAsync("/world/trees"))!; // #243: a árvore ainda não foi tocada: o livro está vazio
        Assert.Empty(trees["trees"]!.AsArray());

        var stopped = await client.PostAsync("/players/lenha/interrupt", null);
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        var free = JsonNode.Parse(await stopped.Content.ReadAsStringAsync())!;
        Assert.Null(free["busy"]);
        Assert.Equal(0, free["readyInMs"]!.GetValue<long>());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/players/lenha/walk", new { dx = 1000, dy = 0 })).StatusCode);

        await client.PostAsync("/players/lenha/logout", null);
    }
}
