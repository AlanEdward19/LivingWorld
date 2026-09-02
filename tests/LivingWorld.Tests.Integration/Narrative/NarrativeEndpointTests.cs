using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Narrative;

/// <summary>Fase 12, T7 (NARR-19..21): <c>GET /narratives/chronicles|biographies/{npcId}|reports</c>
/// — mesmo padrão de <c>NpcEndpointTests</c>/<c>ConversationEndpointTests</c>
/// (<c>WebApplicationFactory&lt;Program&gt;</c>), nenhuma lógica de decisão nova, só tradução
/// request/response sobre o pipeline já pronto (T2/T4/T5/T6).
/// Factory própria: os casos felizes semeiam cidades/facts no mundo canônico.</summary>
public class NarrativeEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public NarrativeEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    /// <summary>Resolve <see cref="WorldHost.Current"/> no momento do teste — não cacheia no
    /// construtor (Transient DI + possível <c>Replace</c> deixariam um ponteiro velho).</summary>
    private WorldState CurrentWorld()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<WorldHost>().Current;
    }

    private static City MakeCity(CityId id) =>
        new(id, new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null, AggregatePopulationPool.Empty);

    // --- /narratives/chronicles (NARR-19) ---

    [Fact]
    public async Task Chronicles_endpoint_returns_prose_and_anchoring_metadata_for_location_and_period()
    {
        var world = CurrentWorld();
        var city = new CityId(Guid.NewGuid());
        world.AddCity(MakeCity(city));
        var fact = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.9, "some-death");
        world.AddFact(fact);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/narratives/chronicles?location={city.Value}&periodStart=0&periodEnd=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChronicleResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Claims);
        Assert.Contains(fact.Id.Value, body.Claims.SelectMany(c => c.EventIds));
        Assert.NotEmpty(body.Prose);
    }

    [Fact]
    public async Task Chronicles_endpoint_returns_400_when_period_is_not_supplied()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/narratives/chronicles");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- /narratives/biographies/{npcId} (NARR-20) ---

    [Fact]
    public async Task Biographies_endpoint_returns_narrative_timeline_referencing_the_npcs_events()
    {
        var world = CurrentWorld();
        var npc = world.Npcs.First(n => n.IsAlive);
        var fact = new Fact(world.NextFactIdAndAdvance(), 5, WorldEventKind.Marriage, [npc.Id], null, 0.6, "married");
        world.AddFact(fact);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/narratives/biographies/{npc.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BiographyResponse>();
        Assert.NotNull(body);
        Assert.Contains(fact.Id.Value, body!.Claims.SelectMany(c => c.EventIds));
    }

    [Fact]
    public async Task Biographies_endpoint_returns_404_for_an_npc_that_does_not_exist()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/narratives/biographies/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- /narratives/reports (NARR-21) ---

    [Fact]
    public async Task Reports_endpoint_returns_items_with_confidence_and_transmission_medium()
    {
        var world = CurrentWorld();
        var city = new CityId(Guid.NewGuid());
        world.AddCity(MakeCity(city));
        var fact = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.8, "cause");
        world.AddFact(fact);
        var report = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city, TransmissionMediumType.Song,
            HopCount: 1, Weight: fact.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(world.FindCity(city)!, report, HistoryRules.Default, nowTick: 20);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/narratives/reports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ReportResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body!, r => r.ReportId == report.Id.Value);
        Assert.Equal(nameof(TransmissionMediumType.Song), item.Medium);
        Assert.InRange(item.Confidence, 0.0, 1.0);
    }
}
