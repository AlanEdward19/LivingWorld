using System.Net;
using System.Net.Http.Json;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Performance;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Population.Archive;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api.Watchlist;

/// <summary>Fase 28 T7 (COG-20, COG-21): <c>POST|DELETE /npcs/{id}/watchlist</c>.</summary>
public sealed class WatchlistEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public WatchlistEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World =>
        _factory.Services.GetRequiredService<WorldHost>().Current;

    private long FirstLivingNpcId() =>
        World.Npcs.First(n => n.IsAlive).Id.Value;

    private static async Task<string?> ErrorBody(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return payload?.GetValueOrDefault("error");
    }

    private void InstallIsolatedNpcWorld()
    {
        var perf = PerfRules.Create(1.0, 100, 2000, coldArchiveAfterYears: 1).Value!;
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            perfRules: perf);
        world.AddNpc(new Npc(
            new NpcId(1), "archived", Sex.Female, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, new CellCoord(0, 0)));
        _factory.Services.GetRequiredService<WorldHost>()
            .Replace(world, new WorldClock([new ColdArchiveSystem()]));
    }

    private static long ArchiveOnlyNpc(WorldState world)
    {
        var npc = world.Npcs.Single();
        npc.Die(world.CurrentDate);
        long id = npc.Id.Value;
        new WorldClock([new ColdArchiveSystem()]).Run(world, world.Calendar.HoursPerYear);
        Assert.Null(world.FindNpc(npc.Id));
        Assert.NotNull(world.ColdArchive.Lookup(id));
        return id;
    }

    [Fact]
    public async Task Post_watchlist_marks_a_living_npc()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        long id = FirstLivingNpcId();

        var response = await client.PostAsync($"/npcs/{id}/watchlist", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(World.CognitionLog.IsWatchlisted(new NpcId(id)));
    }

    [Fact]
    public async Task Post_watchlist_returns_bad_request_for_a_dead_npc()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        var npc = World.Npcs.First(n => n.IsAlive);
        npc.Die(World.CurrentDate);

        var response = await client.PostAsync($"/npcs/{npc.Id.Value}/watchlist", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("NpcId: NPC ausente ou morto", await ErrorBody(response));
        Assert.False(World.CognitionLog.IsWatchlisted(npc.Id));
    }

    [Fact]
    public async Task Post_watchlist_returns_bad_request_for_an_archived_npc()
    {
        InstallIsolatedNpcWorld();
        var client = _factory.CreateClient();
        long archivedId = ArchiveOnlyNpc(World);

        var response = await client.PostAsync($"/npcs/{archivedId}/watchlist", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("NpcId: NPC arquivado", await ErrorBody(response));
        Assert.False(World.CognitionLog.IsWatchlisted(new NpcId(archivedId)));
    }

    [Fact]
    public async Task Post_watchlist_returns_bad_request_for_an_unknown_npc()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/npcs/999999999/watchlist", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("NpcId: NPC ausente ou morto", await ErrorBody(response));
    }

    [Fact]
    public async Task Delete_watchlist_unmarks_a_watchlisted_npc()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        long id = FirstLivingNpcId();
        World.CognitionLog.MarkWatchlisted(new NpcId(id), World.CurrentDate.TotalHours);

        var response = await client.DeleteAsync($"/npcs/{id}/watchlist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(World.CognitionLog.IsWatchlisted(new NpcId(id)));
    }

    [Fact]
    public async Task Delete_watchlist_returns_bad_request_for_a_dead_npc()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        var npc = World.Npcs.First(n => n.IsAlive);
        World.CognitionLog.MarkWatchlisted(npc.Id, World.CurrentDate.TotalHours);
        npc.Die(World.CurrentDate);

        var response = await client.DeleteAsync($"/npcs/{npc.Id.Value}/watchlist");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("NpcId: NPC ausente ou morto", await ErrorBody(response));
    }

    [Fact]
    public async Task Post_watchlist_is_idempotent_for_an_already_watchlisted_npc()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        long id = FirstLivingNpcId();

        var first = await client.PostAsync($"/npcs/{id}/watchlist", content: null);
        var second = await client.PostAsync($"/npcs/{id}/watchlist", content: null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(World.CognitionLog.IsWatchlisted(new NpcId(id)));
    }
}
