using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.NpcDirectory;

/// <summary>godot-api-readiness C7–C10: <c>GET /npcs</c> — lista paginada de NPCs vivos.</summary>
public class NpcDirectoryEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public NpcDirectoryEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private CityId SeedCity(params string[] names) => SeedCity(deadNames: [], names);

    private CityId SeedCity(string[] deadNames, string[] names)
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        foreach (var name in names)
        {
            var npc = new Npc(
                world.NextNpcIdAndAdvance(), name, Sex.Female, WorldDate.Epoch(new WorldCalendar(24, 30, 12)), new CultureId(1),
                new CellCoord(0, 0), motherId: null, fatherId: null, household: null, health: 100,
                personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
                profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0), city: city.Id);
            world.AddNpc(npc);
        }
        foreach (var name in deadNames)
        {
            var dead = new Npc(
                world.NextNpcIdAndAdvance(), name, Sex.Male, WorldDate.Epoch(new WorldCalendar(24, 30, 12)), new CultureId(1),
                new CellCoord(0, 0), motherId: null, fatherId: null, household: null, health: 100,
                personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
                profession: new ProfessionType(1), currentLocation: new CellCoord(0, 0), city: city.Id);
            world.AddNpc(dead);
            dead.Die(WorldDate.Epoch(new WorldCalendar(24, 30, 12)));
        }
        return city.Id;
    }

    private async Task<JsonElement> GetPage(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/npcs?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task City_filter_returns_living_sorted()
    {
        var city = SeedCity("Caio", "Ana", "Bia");
        var empty = SeedCity();

        var page = await GetPage($"cityId={city.Value}");
        var names = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToArray();
        var emptyPage = await GetPage($"cityId={empty.Value}");

        Assert.Equal(new[] { "Ana", "Bia", "Caio" }, names);
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(0, emptyPage.GetProperty("items").GetArrayLength());
        Assert.Equal(0, emptyPage.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Dead_npcs_are_excluded_and_name_ties_break_by_id()
    {
        var city = SeedCity(deadNames: ["Zed"], names: ["Ana", "Ana", "Bia"]);

        var page = await GetPage($"cityId={city.Value}");
        var items = page.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.DoesNotContain(items, i => i.GetProperty("name").GetString() == "Zed");
        var anaIds = items.Where(i => i.GetProperty("name").GetString() == "Ana")
            .Select(i => i.GetProperty("id").GetProperty("value").GetInt64()).ToArray();
        Assert.Equal(anaIds.Order().ToArray(), anaIds);
        Assert.Equal(city.Value.ToString(), items[0].GetProperty("cityId").GetProperty("value").GetString());
    }

    [Fact]
    public async Task Query_is_case_insensitive()
    {
        var city = SeedCity("Ana", "ANA", "Bia");

        var page = await GetPage($"cityId={city.Value}&q=ana");

        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.All(page.GetProperty("items").EnumerateArray(),
            i => Assert.Equal("ana", i.GetProperty("name").GetString()!.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("take=201")]
    [InlineData("skip=-1")]
    [InlineData("take=0")]
    public async Task Invalid_paging_returns_400(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/npcs?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Total_ignores_paging()
    {
        var city = SeedCity(Enumerable.Range(0, 8).Select(i => $"N{i}").ToArray());

        var first = await GetPage($"cityId={city.Value}&skip=0&take=1");
        var later = await GetPage($"cityId={city.Value}&skip=5&take=50");

        Assert.Equal(8, first.GetProperty("total").GetInt32());
        Assert.Equal(8, later.GetProperty("total").GetInt32());
        Assert.Equal(1, first.GetProperty("items").GetArrayLength());
        Assert.Equal(3, later.GetProperty("items").GetArrayLength());
    }
}
