using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Sandbox;

/// <summary>AD-160b: <c>GET /npcs/{id}/brain</c> lê a rede termo → grupo → ação de um NPC real; a soma das sinapses de cada ação é
/// a nota dela; ler nunca escreve no mundo.</summary>
public sealed class BrainEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public BrainEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World => _factory.Services.GetRequiredService<WorldHost>().Current;

    [Fact]
    public async Task The_synapses_of_each_action_add_up_to_its_score_and_reading_writes_nothing()
    {
        _factory.ResetCanonicalWorld();
        long id = World.Npcs.First(n => n.IsAlive).Id.Value;
        var before = WorldSnapshot.CanonicalHash(World);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/npcs/{id}/brain");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var sums = new Dictionary<string, double>();
        foreach (var s in body.GetProperty("synapses").EnumerateArray())
        {
            string action = s.GetProperty("action").ToString();
            sums[action] = sums.GetValueOrDefault(action) + s.GetProperty("value").GetDouble();
        }
        foreach (var score in body.GetProperty("scores").EnumerateArray())
            Assert.Equal(score.GetProperty("score").GetDouble(), sums.GetValueOrDefault(score.GetProperty("action").ToString()), 6);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(World));
    }

    /// <summary>AD-184: o painel mostra a decisão sobre a obra com os termos — quem ajuda na obra da própria casa aparece como família,
    /// com o id da obra, a célula e os termos da oferta (jornal, salário, necessidade, ida, valor e o mínimo).</summary>
    [Fact]
    public async Task The_brain_shows_the_site_work_decision_with_its_terms()
    {
        _factory.ResetCanonicalWorld();
        var world = World;
        Assert.True(world.StreetsOn);
        var city = world.ActiveCities().First();
        var home = world.Households.First(h => !h.IsEmpty && h.Members.Any(m => world.FindNpc(m) is { IsAlive: true } n
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult));
        var work = LivingWorld.Simulation.Cities.Works.BuildWorks.Open(world, new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler),
            city, LivingWorld.Simulation.Cities.Works.BuildKind.House, -1, city.Location, 0, 0, home.Id.Value, home.Head.Value, 400, 0, "teste");
        long helper = work.Helpers.First();

        var body = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/npcs/{helper}/brain");
        var site = body.GetProperty("siteWork");

        Assert.Equal(work.Id, site.GetProperty("workId").GetInt64());
        Assert.Equal("familia", site.GetProperty("role").GetString());
        Assert.True(site.GetProperty("joined").GetBoolean());
        Assert.True(site.GetProperty("offerDay").GetDouble() > 0);
        Assert.True(site.GetProperty("joinMin").GetDouble() > 0);
        _factory.ResetCanonicalWorld();
    }

    [Fact]
    public async Task An_unknown_npc_is_not_found()
    {
        var response = await _factory.CreateClient().GetAsync("/npcs/999999999/brain");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
