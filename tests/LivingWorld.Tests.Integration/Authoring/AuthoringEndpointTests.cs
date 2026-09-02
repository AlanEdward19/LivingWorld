using System.Net;
using System.Net.Http.Json;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Authoring;

public sealed class AuthoringEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public AuthoringEndpointTests(LivingWorldApiFactory factory)
    {
        _factory = factory;
        var descriptor = new PowerDescriptor(
            "shape", "artifact", ["construct.create:1x1:5:12:cyan"], "Active", [],
            "Guaranteed", [], [], ["cyan"], []);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]));
        world.AddNpc(new Npc(
            new NpcId(1), "author", Sex.Female, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, new CellCoord(0, 0)));
        _factory.Services.GetRequiredService<WorldHost>()
            .Replace(world, new WorldClock([new ExtraordinaryStateSystem()]));
    }

    [Fact]
    public async Task Catalog_grant_and_cell_invocation_mutate_the_authoritative_world()
    {
        var client = _factory.CreateClient();
        var catalog = await client.GetFromJsonAsync<List<PowerDescriptor>>("/authoring/extraordinary/catalog");
        Assert.Equal("shape", Assert.Single(catalog!).Id);

        var grant = await client.PostAsJsonAsync(
            "/authoring/npcs/1/extraordinary/grant", new { PowerId = "shape" });
        var invoke = await client.PostAsJsonAsync(
            "/authoring/npcs/1/extraordinary/invoke",
            new { PowerId = "shape", TargetNpcId = 1, TargetCell = new { X = 4, Y = 5 } });

        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invoke.StatusCode);
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        Assert.Equal("shape", Assert.Single(Assert.Single(world.ExtraordinaryCarriers).PowerIds));
        Assert.Equal(new CellCoord(4, 5), Assert.Single(world.ExtraordinaryConstructs).Origin);

        var revoke = await client.PostAsJsonAsync(
            "/authoring/npcs/1/extraordinary/revoke", new { PowerId = "shape" });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Empty(world.ExtraordinaryCarriers);
    }

    [Fact]
    public async Task Invalid_personality_returns_bad_request_without_partial_rewrite()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var before = Assert.Single(world.Npcs).Personality;
        var response = await _factory.CreateClient().PutAsJsonAsync(
            "/authoring/npcs/1/personality",
            new
            {
                Extroversion = 500,
                Agreeableness = 1,
                Conscientiousness = 1,
                EmotionalStability = 1,
                Openness = 1,
                Ambition = 1,
                Loyalty = 1,
                Altruism = 1,
                Impulsivity = 1,
                RiskAversion = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, Assert.Single(world.Npcs).Personality);
    }
}
