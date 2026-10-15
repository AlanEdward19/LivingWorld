using System.Net;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Formas congeladas de <c>/narratives/*</c> que o cliente Godot interpreta (#59 fatia B). Factory própria:
/// semeia cidade, fatos e relato no mundo, o que não pode vazar para os outros contratos.</summary>
public class NarrativeContractTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public NarrativeContractTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task History_beliefs_shape_matches_the_committed_contract()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var city = world.ActiveCities().First();
        var fact = new Fact(world.NextFactIdAndAdvance(), 3, WorldEventKind.Death, [], city.Id, 0.9, "p");
        world.AddFact(fact);
        CanonSlotManager.Admit(
            city, new ReportState(world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition, 2, 0.9, 3, 3),
            world.HistoryRules, 3);

        var response = await _factory.CreateClient().GetAsync($"/history/beliefs?communityId={city.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.history-beliefs.json", ApiContractTests.ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync())));
    }

    [Fact]
    public async Task Biography_chronicle_and_reports_shapes_match_the_committed_contracts()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var city = new CityId(new Guid("00000000-0000-0000-0000-0000000000aa"));
        world.AddCity(new City(city, new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null, AggregatePopulationPool.Empty));
        var npc = world.Npcs.First(n => n.IsAlive);
        var death = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [npc.Id], city, 0.9, "causa");
        world.AddFact(death);
        var report = new ReportState(
            world.NextReportIdAndAdvance(), death.Id, city, TransmissionMediumType.Song,
            HopCount: 1, Weight: death.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(world.FindCity(city)!, report, HistoryRules.Default, nowTick: 20);
        var client = _factory.CreateClient();

        var biography = await client.GetAsync($"/narratives/biographies/{npc.Id.Value}");
        Assert.Equal(HttpStatusCode.OK, biography.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.narrative-biography.json", ApiContractTests.ShapeOf(JsonNode.Parse(await biography.Content.ReadAsStringAsync())));

        var chronicle = await client.GetAsync($"/narratives/chronicles?location={city.Value}&periodStart=0&periodEnd=100&topK=5");
        Assert.Equal(HttpStatusCode.OK, chronicle.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.narrative-chronicle.json", ApiContractTests.ShapeOf(JsonNode.Parse(await chronicle.Content.ReadAsStringAsync())));

        var reports = await client.GetAsync("/narratives/reports");
        Assert.Equal(HttpStatusCode.OK, reports.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.narrative-reports.json", ApiContractTests.ShapeOf(JsonNode.Parse(await reports.Content.ReadAsStringAsync())));
    }
}
