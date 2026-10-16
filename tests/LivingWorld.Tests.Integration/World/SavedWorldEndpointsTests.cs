using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api.World;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

public class SavedWorldEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;
    public SavedWorldEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;
    private static Task<HttpResponseMessage> Create(HttpClient client, ulong seed, string name) =>
        client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(WorldCreateEndpointsTests.FullValidScenario(seed).ToJsonString(), name));

    [Fact]
    public async Task Creating_B_preserves_A_and_loading_A_restores_its_hash()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        Assert.Equal(HttpStatusCode.OK, (await Create(client, 67001, "A")).StatusCode);
        host.Clock.Run(host.Current, 35);
        string hash = WorldSnapshot.CanonicalHash(host.Current);
        Assert.Equal(HttpStatusCode.OK, (await Create(client, 67002, "B")).StatusCode);
        var worlds = await client.GetFromJsonAsync<SavedWorldInfo[]>("/worlds");
        Assert.Contains(worlds!, w => w.Name == "A" && w.CurrentDate == 35);
        Assert.Contains(worlds!, w => w.Name == "B");
        var loaded = await client.PostAsync($"/worlds/{WorldIdentity.WorldIdFor(67001)}/load", null);
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current));
        Assert.True(_factory.Services.GetRequiredService<SimulationHost>().IsPaused);
        // Retomar após carga produz o mesmo próximo tick do estado reidratado.
        var clone = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(host.Current));
        host.Clock.Tick(host.Current);
        new WorldClock(host.Clock.Systems).Tick(clone);
        Assert.Equal(WorldSnapshot.CanonicalHash(clone), WorldSnapshot.CanonicalHash(host.Current));
    }

    [Fact]
    public async Task Loading_a_saved_world_preserves_its_server_object_catalog()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var scenario = WorldCreateEndpointsTests.FullValidScenario(67109);
        LivingWorld.Simulation.Objects.DefaultObjects.AddTo(scenario);
        var request = new CreateWorldRequest(scenario.ToJsonString(), "Catálogo salvo");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/worlds/create", request)).StatusCode);
        Assert.NotNull(host.Current.ObjectCatalog);
        host.Clock.Run(host.Current, 35);
        string before = WorldSnapshot.CanonicalHash(host.Current);
        Assert.Equal(HttpStatusCode.OK, (await Create(client, 67110, "Outro mundo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/worlds/{WorldIdentity.WorldIdFor(67109)}/load", null)).StatusCode);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(host.Current));
    }

    [Fact]
    public async Task Load_missing_world_keeps_the_current_hash()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        string before = WorldSnapshot.CanonicalHash(host.Current);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/worlds/{Guid.Empty}/load", null)).StatusCode);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(host.Current));
    }

    [Fact]
    public async Task Delete_guards_current_and_removes_only_the_requested_world()
    {
        var client = _factory.CreateClient();
        await Create(client, 67003, "remove");
        await Create(client, 67004, "current");
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/worlds/{WorldIdentity.WorldIdFor(67004)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/worlds/{WorldIdentity.WorldIdFor(67003)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/worlds/{WorldIdentity.WorldIdFor(67003)}")).StatusCode);
        var worlds = await client.GetFromJsonAsync<SavedWorldInfo[]>("/worlds");
        Assert.DoesNotContain(worlds!, w => w.Seed == 67003);
        Assert.Contains(worlds!, w => w.Seed == 67004);
    }

    [Fact]
    public async Task Duplicate_seed_is_rejected_without_overwriting()
    {
        var client = _factory.CreateClient();
        await Create(client, 67005, "original");
        var host = _factory.Services.GetRequiredService<WorldHost>();
        host.Clock.Run(host.Current, 7);
        string hash = WorldSnapshot.CanonicalHash(host.Current);
        Assert.Equal(HttpStatusCode.Conflict, (await Create(client, 67005, "replacement")).StatusCode);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current));
        Assert.Equal("original", host.Current.Name);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest("{}", ""))).StatusCode);
    }

    [Fact]
    public async Task Restart_preserves_catalog_and_current_world()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-saved-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string hash;
            using (var factory = DiskFactory(root))
            {
                var client = factory.CreateClient();
                await Create(client, 67006, "disk A");
                var host = factory.Services.GetRequiredService<WorldHost>();
                host.Clock.Run(host.Current, 13);
                hash = WorldSnapshot.CanonicalHash(host.Current);
                await Create(client, 67007, "disk B");
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/worlds/{WorldIdentity.WorldIdFor(67006)}/load", null)).StatusCode);
                host.Clock.Run(host.Current, 3); // encerramento salva também os ticks após a última fronteira periódica
                hash = WorldSnapshot.CanonicalHash(host.Current);
            }
            using (var factory = DiskFactory(root))
            {
                var client = factory.CreateClient();
                var host = factory.Services.GetRequiredService<WorldHost>();
                Assert.True(hash == WorldSnapshot.CanonicalHash(host.Current), $"restart: seed={host.Current.Seed} tick={host.Current.CurrentDate.TotalHours} name={host.Current.Name}; files={string.Join(",", Directory.GetFiles(root, "*", SearchOption.AllDirectories))}");
                var worlds = await client.GetFromJsonAsync<SavedWorldInfo[]>("/worlds");
                Assert.Contains(worlds!, w => w.Seed == 67006);
                Assert.Contains(worlds!, w => w.Seed == 67007);
                Assert.Contains(worlds!, w => w.Seed == 1); // banco legado/bootstrap incorporado
                Assert.True(File.Exists(Path.Combine(root, "worlds", $"{WorldIdentity.WorldIdFor(67006):D}.db")));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task An_existing_legacy_world_is_imported_with_its_hash_and_historical_events(long tick)
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var (world, clock) = ScenarioRunner.Create(42);
            clock.Run(world, tick);
            string hash = WorldSnapshot.CanonicalHash(world);
            using (var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>()
                .UseSqlite($"Data Source={Path.Combine(root, "legacy.db")};Pooling=False").Options))
            {
                context.Database.Migrate();
                var sink = new BufferingWorldEventSink();
                sink.Record(new WorldEvent(tick, WorldEventKind.Marriage, "1|2", 123, SourceSystem: "legacy-test"));
                new PersistentWorldRunner(new SqliteWorldRepository(context), BranchId.Root, 24).Snapshot(world, sink);
            }
            using var factory = DiskFactory(root);
            var client = factory.CreateClient();
            Assert.Equal(hash, WorldSnapshot.CanonicalHash(factory.Services.GetRequiredService<WorldHost>().Current));
            var catalog = await client.GetFromJsonAsync<SavedWorldInfo[]>("/worlds");
            Assert.Contains(catalog!, w => w.Seed == 42 && w.CurrentDate == tick);
            Assert.Contains(factory.Services.GetRequiredService<IWorldRepository>().LoadEvents(BranchId.Root),
                e => e.EventId == 123 && e.Kind == "Marriage" && e.SourceSystem == "legacy-test");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/worlds/{WorldIdentity.WorldIdFor(42)}/load", null)).StatusCode);
            Assert.Contains(factory.Services.GetRequiredService<IWorldRepository>().LoadEvents(BranchId.Root),
                e => e.EventId == 123 && e.Kind == "Marriage");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program> DiskFactory(string root) =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:World", $"Data Source={Path.Combine(root, "legacy.db")};Pooling=False");
            b.UseSetting("WorldsDirectory", Path.Combine(root, "worlds"));
            b.UseSetting("TICK_LOOP_ENABLED", "false");
        });
}
