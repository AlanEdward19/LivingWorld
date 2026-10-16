using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api.Admin;
using LivingWorld.Api.Simulation;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.Records;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Admin;

/// <summary>god-console (Fase 26, ADR-0015): pausa com teto, busca, velocidade e auditoria append-only.</summary>
public class AdminConsoleTests
{
    private const string Token = "admin-test-token";

    private static WebApplicationFactory<Program> NewFactory() => new LivingWorldApiFactory()
        .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_TOKEN"] = Token })));

    private static HttpClient AdminClient(WebApplicationFactory<Program> factory, string actor = "alan")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AdminEndpoints.TokenHeader, Token);
        client.DefaultRequestHeaders.Add(AdminEndpoints.ActorHeader, actor);
        return client;
    }

    [Fact]
    public async Task Without_the_admin_token_every_admin_route_is_403_and_nothing_changes()
    {
        using var factory = NewFactory();
        var host = factory.Services.GetRequiredService<SimulationHost>();
        var anonymous = factory.CreateClient();
        var wrong = factory.CreateClient();
        wrong.DefaultRequestHeaders.Add(AdminEndpoints.TokenHeader, "errado");

        foreach (var client in new[] { anonymous, wrong })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/admin/world/pause", new { reason = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/admin/world/resume", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/admin/world/speed", new { ticksPerSecond = 5 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/search?q=a")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/audit")).StatusCode);
        }

        Assert.False(host.IsPaused);
        Assert.Equal(1.0, host.TicksPerSecond);
    }

    [Fact]
    public async Task Every_admin_route_found_by_reflection_demands_the_admin_token()
    {
        using var factory = NewFactory();
        var routes = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/admin", StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(m => (m, e.RoutePattern.RawText!)))
            .ToList();
        Assert.True(routes.Count >= 5, "esperava as rotas /admin/*");

        var anonymous = factory.CreateClient();
        foreach (var (method, route) in routes)
        {
            var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), route)
            {
                Content = method == "POST" ? JsonContent.Create(new { reason = "x", ticksPerSecond = 2 }) : null,
            });
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {route} respondeu {response.StatusCode} sem token");
        }
    }

    [Fact]
    public async Task With_no_token_configured_the_console_stays_closed()
    {
        using var factory = new LivingWorldApiFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_TOKEN"] = "" })));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AdminEndpoints.TokenHeader, "qualquer");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/audit")).StatusCode);
    }

    [Fact]
    public async Task Pause_stops_the_clock_audits_the_action_and_resumes_by_itself_when_the_cap_expires()
    {
        using var factory = NewFactory();
        var loop = factory.Services.GetRequiredService<TickLoopService>();
        var worldHost = factory.Services.GetRequiredService<WorldHost>();
        var client = AdminClient(factory);

        var response = await client.PostAsJsonAsync("/admin/world/pause", new { reason = "manutenção", maxTicks = 100 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        long tick = worldHost.Current.CurrentDate.TotalHours;
        string hash = WorldSnapshot.CanonicalHash(worldHost.Current);
        for (int i = 0; i < 99; i++) loop.RunOneCycle();
        Assert.True(factory.Services.GetRequiredService<SimulationHost>().IsPaused);
        Assert.Equal(tick, worldHost.Current.CurrentDate.TotalHours);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(worldHost.Current));

        loop.RunOneCycle(); // o centésimo ciclo vence o teto
        Assert.False(factory.Services.GetRequiredService<SimulationHost>().IsPaused);
        loop.RunOneCycle();
        Assert.Equal(tick + 1, worldHost.Current.CurrentDate.TotalHours);

        var audit = await client.GetFromJsonAsync<List<AdminAuditRecord>>("/admin/audit");
        Assert.Contains(audit!, a => a.Action == "world.pause" && a.Actor == "alan" && a.Detail.Contains("maxTicks=100", StringComparison.Ordinal));
        Assert.Contains(audit!, a => a.Action == "world.pause-expired");
        Assert.Equal(audit!.OrderBy(a => a.Sequence).Select(a => a.Sequence), audit!.Select(a => a.Sequence));
    }

    [Fact]
    public async Task Pause_validates_reason_and_cap_and_defaults_to_seven_simulated_days()
    {
        using var factory = NewFactory();
        var client = AdminClient(factory);
        var simulation = factory.Services.GetRequiredService<SimulationHost>();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/admin/world/pause", new { reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/admin/world/pause", new { reason = "x", maxTicks = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/admin/world/pause", new { reason = "x", maxTicks = 100_000 })).StatusCode);
        Assert.False(simulation.IsPaused);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/admin/world/pause", new { reason = "padrão" })).StatusCode);
        long hoursPerDay = factory.Services.GetRequiredService<WorldHost>().Current.Calendar.HoursPerDay;
        Assert.Equal(hoursPerDay * 7, simulation.AdminPause!.MaxTicks);
    }

    [Fact]
    public async Task Search_is_ordered_by_id_and_rejects_unknown_kinds()
    {
        using var factory = NewFactory();
        var client = AdminClient(factory);
        var world = factory.Services.GetRequiredService<WorldHost>().Current;
        string first = world.Npcs[0].Name[..2];

        var ok = await client.GetFromJsonAsync<List<AdminNpcHit>>($"/admin/search?kind=name&q={Uri.EscapeDataString(first)}");
        Assert.NotEmpty(ok!);
        Assert.Equal(ok!.Select(h => h.Id).OrderBy(i => i), ok!.Select(h => h.Id));

        var byProfession = await client.GetFromJsonAsync<List<AdminNpcHit>>($"/admin/search?kind=profession&q={world.Npcs[0].Profession.Id}");
        Assert.Contains(byProfession!, h => h.Id == world.Npcs[0].Id.Value);
        var named = new LivingWorld.Domain.Cities.City(
            world.NextCityId(), new LivingWorld.Domain.Geography.CellCoord(2, 2), 0, null,
            LivingWorld.Domain.Cities.AggregatePopulationPool.Empty, name: "Valdoria");
        world.AddCity(named);
        world.Npcs[0].JoinCity(named.Id);
        var byCity = await client.GetFromJsonAsync<List<AdminNpcHit>>("/admin/search?kind=city&q=valdor");
        Assert.Equal(world.Npcs[0].Id.Value, Assert.Single(byCity!).Id);
        Assert.Equal("Valdoria", byCity![0].City);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/admin/search?kind=planeta&q=a")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/admin/search?kind=name")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/admin/search?kind=profession&q=abc")).StatusCode);

        var audit = await client.GetFromJsonAsync<List<AdminAuditRecord>>("/admin/audit");
        Assert.True(audit!.Count(a => a.Action == "search") >= 3, "leitura também é auditada");
    }

    [Fact]
    public async Task Changing_speed_is_audited_and_never_changes_the_world_for_the_same_number_of_ticks()
    {
        using var slow = NewFactory();
        using var fast = NewFactory();
        var fastClient = AdminClient(fast);
        Assert.Equal(HttpStatusCode.OK, (await fastClient.PostAsJsonAsync("/admin/world/speed", new { ticksPerSecond = 50 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fastClient.PostAsJsonAsync("/admin/world/speed", new { ticksPerSecond = 0 })).StatusCode);

        foreach (var factory in new[] { slow, fast })
        {
            factory.Services.GetRequiredService<WorldHost>().Replace(
                LivingWorld.Simulation.Scenarios.ScenarioRunner.Create(7, initialPopulation: 40).World,
                new WorldClock(LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultSystems()));
            var loop = factory.Services.GetRequiredService<TickLoopService>();
            for (int i = 0; i < 200; i++) loop.RunOneCycle();
        }

        Assert.Equal(50, fast.Services.GetRequiredService<SimulationHost>().TicksPerSecond);
        Assert.Equal(
            WorldSnapshot.CanonicalHash(slow.Services.GetRequiredService<WorldHost>().Current),
            WorldSnapshot.CanonicalHash(fast.Services.GetRequiredService<WorldHost>().Current));
        var audit = await fastClient.GetFromJsonAsync<List<AdminAuditRecord>>("/admin/audit");
        Assert.Contains(audit!, a => a.Action == "world.speed");
    }

    [Fact]
    public void The_audit_table_is_append_only_for_update_and_delete()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();
        context.AdminAudit.Add(new AdminAuditRecord { BranchId = 0, Sequence = 0, Tick = 1, Actor = "a", Action = "x", Target = "y", Detail = "" });
        context.SaveChanges();

        var update = Assert.Throws<SqliteException>(() => context.Database.ExecuteSqlRaw("UPDATE AdminAudit SET Actor = 'z'"));
        var delete = Assert.Throws<SqliteException>(() => context.Database.ExecuteSqlRaw("DELETE FROM AdminAudit"));

        Assert.Contains("append-only", update.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("append-only", delete.Message, StringComparison.OrdinalIgnoreCase);
    }
}
