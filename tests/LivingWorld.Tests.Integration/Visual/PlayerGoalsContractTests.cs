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

    /// <summary>#238: o ponto de partida só vem com <c>?start=true</c> (a linha de metas, lida a cada poucos segundos, não paga a busca da
    /// mata), e a pose do passo traz o chão (<c>ground</c>) de forma aditiva.</summary>
    [Fact]
    public async Task The_start_block_is_opt_in_and_the_walk_pose_carries_the_ground()
    {
        var client = _factory.CreateClient();
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var npc = world.Npcs.First(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 18 && n.Household is not null);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/players/{npc.Id.Value}/incarnate", new { playerId = "partida" })).StatusCode);
        npc.SetHunger(0, world.CurrentDate.TotalHours);

        var plain = JsonNode.Parse(await (await client.GetAsync("/players/partida/goals")).Content.ReadAsStringAsync())!;
        Assert.Null(plain["start"]);
        var withStart = JsonNode.Parse(await (await client.GetAsync("/players/partida/goals?start=true")).Content.ReadAsStringAsync())!;
        var start = withStart["start"]!;
        Assert.Equal("hunger", start["pressing"]!.GetValue<string>());
        Assert.False(start["child"]!.GetValue<bool>());
        Assert.NotNull(start["home"]);
        Assert.NotNull(start["home"]!["members"]);
        Assert.NotNull(withStart["goals"]); // o resto da resposta segue igual

        var walk = await client.PostAsJsonAsync("/players/partida/walk", new { dx = 0, dy = 0 });
        Assert.Equal(HttpStatusCode.OK, walk.StatusCode);
        var pose = JsonNode.Parse(await walk.Content.ReadAsStringAsync())!;
        var ground = pose["ground"];
        Assert.NotNull(ground);
        Assert.True(ground!["terrainPct"]!.GetValue<int>() >= 0);
        Assert.Contains(ground["cause"]!.GetValue<string>(), new[] { "none", "load", "tired", "weather", "body", "ground" });

        await client.PostAsync("/players/partida/logout", null);
    }
}
