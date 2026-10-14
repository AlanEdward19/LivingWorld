using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api.Narrative;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Narrative;

/// <summary>Plano history-narrative-close, AC 5: <c>GET /history/truth</c> (403 sem escopo de motor) e
/// <c>GET /history/beliefs</c> (200 com relatos vivos, 404 sem comunidade).</summary>
public class HistoryEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private const string Token = "engine-test-token";
    private readonly LivingWorldApiFactory _factory;

    public HistoryEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private WebApplicationFactory<Program> ScopedFactory() => _factory
        .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["HISTORY_ENGINE_SCOPE_TOKEN"] = Token })));

    private WorldState World(WebApplicationFactory<Program>? factory = null)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<WorldHost>().Current;
    }

    [Fact]
    public async Task Truth_without_engine_scope_is_forbidden()
    {
        var response = await _factory.CreateClient().GetAsync("/history/truth?scope=engine&fromTick=0");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var wrong = ScopedFactory().CreateClient();
        wrong.DefaultRequestHeaders.Add(HistoryEndpoints.ScopeHeader, "outro");
        Assert.Equal(HttpStatusCode.Forbidden, (await wrong.GetAsync("/history/truth")).StatusCode);
    }

    [Fact]
    public async Task Truth_with_engine_scope_returns_the_fact_skeleton()
    {
        var scoped = ScopedFactory();
        var world = World(scoped);
        var fact = new Fact(world.NextFactIdAndAdvance(), 3, WorldEventKind.Death, [], null, 0.9, "p");
        world.AddFact(fact);
        var client = scoped.CreateClient();
        client.DefaultRequestHeaders.Add(HistoryEndpoints.ScopeHeader, Token);

        var response = await client.GetAsync("/history/truth?scope=engine&fromTick=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<TruthFactView>>();
        Assert.Contains(body!, f => f.Id == fact.Id.Value);
    }

    [Fact]
    public async Task Beliefs_returns_live_reports_of_the_community_and_404_for_unknown_one()
    {
        var world = World();
        var city = world.ActiveCities().First();
        var fact = new Fact(world.NextFactIdAndAdvance(), 3, WorldEventKind.Death, [], city.Id, 0.9, "p");
        world.AddFact(fact);
        CanonSlotManager.Admit(city,
            new ReportState(world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition, 2, 0.9, 3, 3),
            world.HistoryRules, 3);
        var client = _factory.CreateClient();

        var ok = await client.GetAsync($"/history/beliefs?communityId={city.Id.Value}");
        var missing = await client.GetAsync($"/history/beliefs?communityId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.NotEmpty((await ok.Content.ReadFromJsonAsync<List<BeliefResponse>>())!);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
