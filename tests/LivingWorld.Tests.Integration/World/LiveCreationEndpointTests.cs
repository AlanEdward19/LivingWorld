using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Domain.History;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Integration.Visual;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

/// <summary>#46 fatia 4 (ADR-0033): <c>POST /authoring/households</c> e <c>/authoring/workplaces</c> criam gente e prédio no mundo em
/// andamento. Factory própria: muda o mundo e a pausa do host.</summary>
public class LiveCreationEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private const string Family =
        "{\"Members\":[{\"Name\":\"Ana\",\"Sex\":\"Female\",\"AgeYears\":30,\"ProfessionId\":1,\"Spouse\":1}," +
        "{\"Name\":\"Beto\",\"Sex\":\"Male\",\"AgeYears\":32,\"ProfessionId\":2,\"Spouse\":0}]}";

    private const string Workplace =
        "{\"LocationTypeId\":1,\"X\":5,\"Y\":5,\"MaxVacancies\":4,\"Treasury\":1000,\"Stock\":{},\"Prices\":{}}";

    private readonly LivingWorldApiFactory _factory;

    public LiveCreationEndpointTests(LivingWorldApiFactory factory)
    {
        _factory = factory;
        _factory.ResetCanonicalWorld();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private string Hash() => WorldSnapshot.CanonicalHash(_factory.Services.GetRequiredService<WorldHost>().Current);

    [Fact]
    public async Task Without_the_admin_token_both_routes_answer_403_and_the_world_is_untouched()
    {
        var client = _factory.CreateClient();
        string before = Hash();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/authoring/households", Json(Family))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/authoring/workplaces", Json(Workplace))).StatusCode);

        Assert.Equal(before, Hash());
    }

    [Fact]
    public async Task A_valid_family_arrives_changes_the_hash_and_appears_in_the_causal_log()
    {
        var client = _factory.CreateAdminClient();
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        int npcsBefore = world.Npcs.Count;
        string before = Hash();

        var response = await client.PostAsync("/authoring/households", Json(Family));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var ids = created["npcIds"]!.AsArray().Select(n => n!.GetValue<long>()).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(npcsBefore + 2, world.Npcs.Count);
        Assert.NotEqual(before, Hash());
        Assert.Equal("Ana", JsonNode.Parse(await client.GetStringAsync($"/npcs/{ids[0]}"))!["name"]!.GetValue<string>());

        var events = _factory.Services.GetRequiredService<BufferingWorldEventSink>().DrainAll();
        var arrival = Assert.Single(events, e => e.Kind == WorldEventKind.AuthoredImmigration);
        Assert.Contains(ids[0].ToString(), arrival.Payload);
    }

    [Fact]
    public async Task The_response_shapes_match_the_committed_contracts()
    {
        var client = _factory.CreateAdminClient();

        var household = JsonNode.Parse(await (await client.PostAsync("/authoring/households", Json(Family))).Content.ReadAsStringAsync())!;
        var workplace = JsonNode.Parse(await (await client.PostAsync("/authoring/workplaces", Json(Workplace))).Content.ReadAsStringAsync())!;

        ApiContractTests.AssertMatchesContract("shape.authoring-household.json", ApiContractTests.ShapeOf(household));
        var workplaceShape = ApiContractTests.ShapeOf(workplace)!;
        ApiContractTests.Optional(workplaceShape, "buildingId", "number"); // sem nenhuma cidade o local nasce sem edifício
        ApiContractTests.AssertMatchesContract("shape.authoring-workplace.json", workplaceShape);
    }

    [Fact]
    public async Task An_invalid_family_answers_400_naming_the_field_and_leaves_the_hash_alone()
    {
        var client = _factory.CreateAdminClient();
        string before = Hash();
        string badProfession = Family.Replace("\"ProfessionId\":1", "\"ProfessionId\":999");

        var response = await client.PostAsync("/authoring/households", Json(badProfession));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ProfessionId", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, Hash());
    }

    [Fact]
    public async Task Admin_pause_refuses_creation_and_resuming_allows_it_again()
    {
        var client = _factory.CreateAdminClient();
        var simulation = _factory.Services.GetRequiredService<SimulationHost>();
        simulation.PauseFor("teste", "congelado", maxTicks: 48, worldTick: 0);
        string before = Hash();
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/authoring/households", Json(Family))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/authoring/workplaces", Json(Workplace))).StatusCode);
            Assert.Equal(before, Hash());
        }
        finally
        {
            simulation.Resume();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/authoring/households", Json(Family))).StatusCode);
    }

    [Fact]
    public async Task A_workplace_is_founded_with_a_building_and_logged()
    {
        var client = _factory.CreateAdminClient();
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        int workplacesBefore = world.Workplaces.Count;
        string before = Hash();

        var response = await client.PostAsync("/authoring/workplaces", Json(Workplace));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotNull(created["buildingId"]);
        Assert.Equal(workplacesBefore + 1, world.Workplaces.Count);
        Assert.NotEqual(before, Hash());
        Assert.Contains(
            _factory.Services.GetRequiredService<BufferingWorldEventSink>().DrainAll(),
            e => e.Kind == WorldEventKind.AuthoredWorkplaceFounded);
    }

    [Theory]
    [InlineData("\"MaxVacancies\":4", "\"MaxVacancies\":0", "MaxVacancies")]
    [InlineData("\"X\":5", "\"X\":500", "X/Y")]
    [InlineData("\"Treasury\":1000", "\"Treasury\":-5", "Treasury")]
    public async Task An_invalid_workplace_answers_400_and_leaves_the_hash_alone(string from, string to, string field)
    {
        var client = _factory.CreateAdminClient();
        string before = Hash();

        var response = await client.PostAsync("/authoring/workplaces", Json(Workplace.Replace(from, to)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.Content.ReadAsStringAsync());
        Assert.Equal(before, Hash());
    }
}
