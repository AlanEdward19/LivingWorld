using System.Net.Http.Json;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T7 (VTT-08, VTT-09): drill-down de cidade (T5) por <c>/visual/subscribe</c>
/// aplica FOW quando <c>mode=Player</c> — só residentes dentro do raio do <c>playerNpcId</c>
/// sobrevivem; <c>mode=Spectator</c> continua vendo tudo (override), sem mudar T5.
/// Factory própria: <c>ResetCanonicalWorld</c> + seed de cidade mutam <c>WorldHost</c>.</summary>
public class CityFowSubscribeEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public CityFowSubscribeEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private (CityId City, long NearNpcId, long FarNpcId) SeedCityWithNearAndFarResident()
    {
        _factory.ResetCanonicalWorld();
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var npcs = world.Npcs.Take(2).ToList();
        var near = npcs[0];
        var far = npcs[1];

        var city = new City(world.NextCityId(), near.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        near.JoinCity(city.Id);
        far.JoinCity(city.Id);
        far.MoveTo(new CellCoord(near.CurrentLocation.X + 200, near.CurrentLocation.Y), tick: 0);

        return (city.Id, near.Id.Value, far.Id.Value);
    }

    [Fact]
    public async Task Player_mode_only_sees_residents_within_sight_radius_of_their_own_npc()
    {
        var (cityId, nearId, farId) = SeedCityWithNearAndFarResident();
        var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/visual/subscribe?scope=City&refId={cityId.Value}&mode=Player&playerNpcId={nearId}");
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var residents = json.GetProperty("payload").GetProperty("residents");

        var ids = residents.EnumerateArray().Select(r => r.GetProperty("id").GetProperty("value").GetInt64()).ToList();
        Assert.Contains(nearId, ids);
        Assert.DoesNotContain(farId, ids);
    }

    [Fact]
    public async Task Spectator_mode_sees_every_resident_regardless_of_distance()
    {
        var (cityId, nearId, farId) = SeedCityWithNearAndFarResident();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/visual/subscribe?scope=City&refId={cityId.Value}&mode=Spectator");
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var residents = json.GetProperty("payload").GetProperty("residents");

        var ids = residents.EnumerateArray().Select(r => r.GetProperty("id").GetProperty("value").GetInt64()).ToList();
        Assert.Contains(nearId, ids);
        Assert.Contains(farId, ids);
    }

    [Fact]
    public async Task Player_mode_without_a_playerNpcId_sees_no_residents()
    {
        var (cityId, nearId, _) = SeedCityWithNearAndFarResident();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/visual/subscribe?scope=City&refId={cityId.Value}&mode=Player");
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var residents = json.GetProperty("payload").GetProperty("residents");

        Assert.Empty(residents.EnumerateArray());
    }
}
