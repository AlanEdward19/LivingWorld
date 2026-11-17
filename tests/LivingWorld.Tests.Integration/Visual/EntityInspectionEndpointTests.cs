using System.Net;
using System.Text.Json.Nodes;
using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-170: <c>GET /entities/{kind}/{id}</c> (ficha genérica com vínculos e história) e o invariante de que assinar escopos
/// e receber deltas não muda o mundo.</summary>
public class EntityInspectionEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public EntityInspectionEndpointTests(LivingWorldApiFactory factory)
    {
        _factory = factory;
        _factory.ResetCanonicalWorld();
    }

    private WorldState World => _factory.Services.GetRequiredService<WorldHost>().Current;

    private async Task<JsonNode> Get(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    [Fact]
    public async Task Person_sheet_reuses_the_npc_inspection_and_links_household_and_city()
    {
        var npc = World.Npcs.First(n => n.IsAlive && n.Household is not null);

        var sheet = await Get($"/entities/npc/{npc.Id.Value}");

        var relations = sheet["links"]!.AsArray().Select(l => l!["relation"]!.GetValue<string>()).ToHashSet();
        Assert.True(sheet["state"]!["npc"]!["name"]!.GetValue<string>() == npc.Name && relations.IsSupersetOf(["household", "city"]));
    }

    [Fact]
    public async Task Portuguese_kind_resolves_to_the_same_sheet()
    {
        var npc = World.Npcs.First(n => n.IsAlive);

        var sheet = await Get($"/entities/pessoa/{npc.Id.Value}");

        Assert.Equal("npc", sheet["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task Building_sheet_lists_its_residents_as_navigable_links()
    {
        var home = World.Households.First(h => h.Dwelling is not null && !h.IsEmpty);

        var sheet = await Get($"/entities/building/{home.Dwelling!.Value.Value}");

        var residents = sheet["links"]!.AsArray().Where(l => l!["relation"]!.GetValue<string>() == "resident")
            .Select(l => long.Parse(l!["id"]!.GetValue<string>()));
        Assert.Subset(residents.ToHashSet(), home.Members.Select(m => m.Value).ToHashSet());
    }

    [Fact]
    public async Task City_sheet_is_a_light_summary_with_population_and_climate()
    {
        var city = World.ActiveCities().First();

        var sheet = await Get($"/entities/city/{city.Id}");

        Assert.True(sheet["state"]!["population"] is not null && sheet["state"]!["climate"]!["seasonKey"] is not null);
    }

    [Fact]
    public async Task Household_sheet_links_its_members()
    {
        var home = World.Households.First(h => !h.IsEmpty);

        var sheet = await Get($"/entities/casa/{home.Id.Value}");

        Assert.Equal(home.Members.Count, sheet["links"]!.AsArray().Count(l => l!["relation"]!.GetValue<string>() == "member"));
    }

    [Theory]
    [InlineData("/entities/npc/987654321")]
    [InlineData("/entities/dragon/1")]
    [InlineData("/entities/city/not-a-guid")]
    public async Task Unknown_kind_or_id_is_404(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Npc_route_exposes_the_building_where_the_household_lives()
    {
        var home = World.Households.First(h => h.Dwelling is not null && !h.IsEmpty);

        var dto = await Get($"/npcs/{home.Members[0].Value}");

        Assert.Equal(home.Dwelling!.Value.Value, dto["householdBuildingId"]!["value"]!.GetValue<long>());
    }

    [Fact]
    public async Task Npc_route_lists_living_children()
    {
        var parent = World.Npcs.First(n => n.IsAlive);
        var child = World.Npcs.First(n => n.IsAlive && n.Id != parent.Id);
        var baby = new LivingWorld.Domain.Population.Npc(new LivingWorld.Domain.Shared.NpcId(World.Npcs.Max(n => n.Id.Value) + 1), "bebê",
            LivingWorld.Domain.Population.Sex.Female, World.CurrentDate, parent.Culture, parent.CurrentLocation, parent.Id, child.Id,
            parent.Household, 100, parent.Personality, parent.Profession, parent.CurrentLocation);
        World.AddNpc(baby);

        var dto = await Get($"/npcs/{parent.Id.Value}");

        Assert.Contains(dto["childIds"]!.AsArray(), id => id!["value"]!.GetValue<long>() == baby.Id.Value);
    }

    /// <summary>Assinar escopos e projetar o delta a cada tick é leitura: o mundo chega ao mesmo hash que sem assinante.</summary>
    [Fact]
    public void Subscribing_scopes_and_publishing_deltas_does_not_change_the_world_hash()
    {
        string RunTicks(bool subscribe)
        {
            _factory.ResetCanonicalWorld();
            var gateway = _factory.Services.GetRequiredService<RealtimeGateway>();
            var loop = _factory.Services.GetRequiredService<TickLoopService>();
            var host = _factory.Services.GetRequiredService<SimulationHost>();
            var unsubscribes = new List<Action>();
            if (subscribe)
            {
                unsubscribes.Add(gateway.SubscribeChannel(new VisualScope(VisualScopeKind.World, "")).Unsubscribe);
                unsubscribes.Add(gateway.SubscribeChannel(new VisualScope(VisualScopeKind.City, World.ActiveCities().First().Id.ToString())).Unsubscribe);
            }
            host.Resume();
            for (int i = 0; i < 20; i++) loop.RunOneCycle(); // abaixo de 24: sem gravação diária no banco do teste
            host.Pause();
            unsubscribes.ForEach(u => u());
            return WorldSnapshot.CanonicalHash(World);
        }

        Assert.Equal(RunTicks(subscribe: false), RunTicks(subscribe: true));
    }
}
