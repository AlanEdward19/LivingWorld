using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>#235: <c>GET /players/{id}/goals</c> e <c>/diary</c> seguem as formas congeladas e só existem para quem controla um corpo.
/// Factory própria: encarnar mexe no NPC controlado do mundo.</summary>
public class PlayerGoalsContractTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PlayerGoalsContractTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Goals_and_diary_shapes_match_the_committed_contracts_and_need_a_body()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/players/sem-corpo/goals")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/players/sem-corpo/diary")).StatusCode);

        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var npc = world.Npcs.First(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 18);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "metas" })).StatusCode);
        npc.SetHunger(0, world.CurrentDate.TotalHours); // uma meta garantida
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/players/metas/action", new { action = "Work" })).StatusCode); // uma linha do diário

        var goals = await client.GetAsync("/players/metas/goals");
        Assert.Equal(HttpStatusCode.OK, goals.StatusCode);
        var goalsBody = JsonNode.Parse(await goals.Content.ReadAsStringAsync())!;
        Assert.Equal("need.hunger", goalsBody["goals"]![0]!["kind"]!.GetValue<string>());
        ApiContractTests.AssertMatchesContract("shape.player-goals.json", ApiContractTests.ShapeOf(goalsBody));

        var diary = await client.GetAsync("/players/metas/diary");
        Assert.Equal(HttpStatusCode.OK, diary.StatusCode);
        var diaryBody = JsonNode.Parse(await diary.Content.ReadAsStringAsync())!;
        Assert.Contains(diaryBody["entries"]!.AsArray(), e => e!["source"]!.GetValue<string>() == "own");
        ApiContractTests.AssertMatchesContract("shape.player-diary.json", ApiContractTests.ShapeOf(diaryBody));

        await client.PostAsync("/players/metas/logout", null);
    }
}
