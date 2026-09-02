using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Api;

/// <summary>Fase 28, T8 (LOD-04): <c>POST /observation/scope</c> alimenta
/// <see cref="LivingWorld.Simulation.Observation.ObservationRegistry"/> com validação de borda e
/// purge por heartbeat.</summary>
public class ObservationScopeEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private readonly LivingWorldApiFactory _factory;

    public ObservationScopeEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;

    private sealed class ShortHeartbeatApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:World"] = "Data Source=:memory:",
                    ["TICK_LOOP_ENABLED"] = "false",
                    ["Observation:HeartbeatTimeoutSeconds"] = "1",
                });
            });
        }
    }

    private static (City CityA, City CityB, Building BuildingA, Npc InCityA, Npc InBuildingA, Npc InCityB)
        BuildFixture(WorldState world)
    {
        var cityA = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        var cityB = new City(world.NextCityId(), new CellCoord(20, 20), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(cityA);
        world.AddCity(cityB);

        var buildingA = new Building(new BuildingId(9_001), cityA.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(buildingA);

        var inCityA = AddNpc(world, new CellCoord(5, 5), cityA.Id);
        var inBuildingA = AddNpc(world, new CellCoord(5, 5), cityA.Id);
        inBuildingA.EnterBuilding(buildingA.Id, FloorLevel.Ground, new CellCoord(1, 1));
        var inCityB = AddNpc(world, new CellCoord(20, 20), cityB.Id);

        return (cityA, cityB, buildingA, inCityA, inBuildingA, inCityB);
    }

    private static Npc AddNpc(WorldState world, CellCoord location, CityId city)
    {
        var npcId = world.NextNpcIdAndAdvance();
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: location, city: city);
        world.AddNpc(npc);
        return npc;
    }

    private async Task<HttpResponseMessage> PostScopeAsync(
        HttpClient client, string sourceId, ObservationScopeDto? scope) =>
        await client.PostAsJsonAsync("/observation/scope", new ObservationScopeRequest(sourceId, scope));

    private WorldState CurrentWorld()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<WorldState>();
    }

    [Fact]
    public async Task World_scope_makes_every_npc_observed()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (_, _, _, inCityA, inBuildingA, inCityB) = BuildFixture(world);

        var response = await PostScopeAsync(client, "spectator-a", new ObservationScopeDto("World"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(world.ObservationRegistry.IsObserved(inCityA, world));
        Assert.True(world.ObservationRegistry.IsObserved(inBuildingA, world));
        Assert.True(world.ObservationRegistry.IsObserved(inCityB, world));
    }

    [Fact]
    public async Task City_scope_observes_only_npcs_in_that_city()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (cityA, _, _, inCityA, inBuildingA, inCityB) = BuildFixture(world);

        var response = await PostScopeAsync(
            client, "spectator-a", new ObservationScopeDto("City", cityA.Id.Value.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(world.ObservationRegistry.IsObserved(inCityA, world));
        Assert.True(world.ObservationRegistry.IsObserved(inBuildingA, world));
        Assert.False(world.ObservationRegistry.IsObserved(inCityB, world));
    }

    [Fact]
    public async Task Building_scope_observes_only_npcs_inside_that_building()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (cityA, _, buildingA, inCityA, inBuildingA, _) = BuildFixture(world);

        var response = await PostScopeAsync(
            client,
            "spectator-a",
            new ObservationScopeDto("Building", cityA.Id.Value.ToString(), buildingA.Id.Value.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(world.ObservationRegistry.IsObserved(inCityA, world));
        Assert.True(world.ObservationRegistry.IsObserved(inBuildingA, world));
    }

    [Fact]
    public async Task Invalid_building_is_rejected_naming_the_field()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (cityA, _, _, _, _, _) = BuildFixture(world);

        var response = await PostScopeAsync(
            client,
            "spectator-a",
            new ObservationScopeDto("Building", cityA.Id.Value.ToString(), "999999"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("scope.buildingId", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_building_keeps_the_sources_previous_scope()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (_, _, _, _, _, inCityB) = BuildFixture(world);

        await PostScopeAsync(client, "spectator-a", new ObservationScopeDto("World"));
        Assert.True(world.ObservationRegistry.IsObserved(inCityB, world));

        var response = await PostScopeAsync(
            client,
            "spectator-a",
            new ObservationScopeDto("Building", Guid.NewGuid().ToString(), "999999"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(world.ObservationRegistry.IsObserved(inCityB, world));
    }

    [Fact]
    public async Task Invalid_city_is_rejected_naming_the_field()
    {
        var client = _factory.CreateClient();
        CurrentWorld();

        var response = await PostScopeAsync(
            client, "spectator-a", new ObservationScopeDto("City", Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("scope.cityId", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_source_id_returns_400()
    {
        var client = _factory.CreateClient();

        var response = await PostScopeAsync(client, "   ", new ObservationScopeDto("World"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("sourceId", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Union_of_multiple_sources_observes_if_any_source_covers_the_npc()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (cityA, cityB, _, inCityA, _, inCityB) = BuildFixture(world);
        var buildingB = new Building(new BuildingId(9_002), cityB.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(buildingB);
        var inBuildingB = AddNpc(world, new CellCoord(20, 20), cityB.Id);
        inBuildingB.EnterBuilding(buildingB.Id, FloorLevel.Ground, new CellCoord(2, 2));

        await PostScopeAsync(client, "spectator-a", new ObservationScopeDto("City", cityA.Id.Value.ToString()));
        await PostScopeAsync(
            client,
            "spectator-b",
            new ObservationScopeDto("Building", cityB.Id.Value.ToString(), buildingB.Id.Value.ToString()));

        Assert.True(world.ObservationRegistry.IsObserved(inCityA, world));
        Assert.False(world.ObservationRegistry.IsObserved(inCityB, world));
        Assert.True(world.ObservationRegistry.IsObserved(inBuildingB, world));
    }

    [Fact]
    public async Task Null_scope_clears_the_source()
    {
        var client = _factory.CreateClient();
        var world = CurrentWorld();
        var (_, _, _, _, _, inCityB) = BuildFixture(world);

        await PostScopeAsync(client, "spectator-a", new ObservationScopeDto("World"));
        Assert.True(world.ObservationRegistry.IsObserved(inCityB, world));

        var response = await PostScopeAsync(client, "spectator-a", scope: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(world.ObservationRegistry.IsObserved(inCityB, world));
    }

    [Fact]
    public async Task Heartbeat_timeout_removes_a_stale_source_from_the_registry()
    {
        using var factory = new ShortHeartbeatApiFactory();
        var client = factory.CreateClient();
        WorldState world;
        City cityA;
        Npc inCityA;
        City cityB;
        using (var scope = factory.Services.CreateScope())
        {
            world = scope.ServiceProvider.GetRequiredService<WorldState>();
            (cityA, cityB, _, inCityA, _, _) = BuildFixture(world);
        }

        await PostScopeAsync(
            client, "spectator-a", new ObservationScopeDto("City", cityA.Id.Value.ToString()));
        Assert.True(world.ObservationRegistry.IsObserved(inCityA, world));

        await Task.Delay(1_100);

        await PostScopeAsync(
            client, "spectator-b", new ObservationScopeDto("City", cityB.Id.Value.ToString()));

        Assert.False(world.ObservationRegistry.IsObserved(inCityA, world));
    }
}
