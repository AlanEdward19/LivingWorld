using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.Admin;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

/// <summary>ADR-0027: <c>GET /world/info</c> (período e eixos de tecnologia do mundo em andamento) e os campos de pausa de admin em
/// <c>GET /simulation/status</c>, visíveis a qualquer jogador. Factory própria: criar mundo e pausar mexem no host.</summary>
public class WorldInfoAndPauseStatusTests
{
    private const string Token = "adr-0027";

    private static WebApplicationFactory<Program> NewFactory() => new LivingWorldApiFactory()
        .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_TOKEN"] = Token })));

    [Fact]
    public async Task Created_world_reports_its_period_name_and_technology_axes()
    {
        using var factory = NewFactory();
        var client = factory.CreateClient();
        var scenario = WorldCreateEndpointsTests.FullValidScenario(seed: 777).ToJsonString();

        var created = await client.PostAsJsonAsync("/worlds/create", new { scenarioJson = scenario, name = "Vale", periodId = "vila-pequena" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var info = JsonNode.Parse(await client.GetStringAsync("/world/info"))!;
        Assert.Equal("Vale", info["name"]!.GetValue<string>());
        Assert.Equal("vila-pequena", info["periodId"]!.GetValue<string>());
        var world = factory.Services.GetRequiredService<WorldHost>().Current;
        Assert.Equal(world.EmergenceRules.AxisNames, info["technologyAxes"]!.AsArray().Select(a => a!.GetValue<string>()).ToList());
    }

    [Fact]
    public async Task A_world_created_without_a_period_has_an_empty_period_id()
    {
        using var factory = NewFactory();
        var client = factory.CreateClient();
        var scenario = WorldCreateEndpointsTests.FullValidScenario(seed: 778).ToJsonString();

        await client.PostAsJsonAsync("/worlds/create", new { scenarioJson = scenario, name = "Sem período" });

        var info = JsonNode.Parse(await client.GetStringAsync("/world/info"))!;
        Assert.Equal(string.Empty, info["periodId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Status_shows_who_paused_and_why_to_any_client_and_clears_on_resume()
    {
        using var factory = NewFactory();
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add(AdminEndpoints.TokenHeader, Token);
        admin.DefaultRequestHeaders.Add(AdminEndpoints.ActorHeader, "alan");
        var anyone = factory.CreateClient();

        var running = JsonNode.Parse(await anyone.GetStringAsync("/simulation/status"))!;
        Assert.False(running["isPaused"]!.GetValue<bool>());
        Assert.Null(running["pausedBy"]);

        await admin.PostAsJsonAsync("/admin/world/pause", new { reason = "manutenção", maxTicks = 48 });

        var paused = JsonNode.Parse(await anyone.GetStringAsync("/simulation/status"))!;
        Assert.True(paused["isPaused"]!.GetValue<bool>());
        Assert.Equal("alan", paused["pausedBy"]!.GetValue<string>());
        Assert.Equal("manutenção", paused["pauseReason"]!.GetValue<string>());
        Assert.Equal(48, paused["pauseMaxTicks"]!.GetValue<long>());
        Assert.NotNull(paused["pausedAtTick"]);

        await admin.PostAsync("/admin/world/resume", null);

        var resumed = JsonNode.Parse(await anyone.GetStringAsync("/simulation/status"))!;
        Assert.False(resumed["isPaused"]!.GetValue<bool>());
        Assert.Null(resumed["pausedBy"]);
        Assert.Null(resumed["pauseReason"]);
    }

    [Fact]
    public async Task A_plain_pause_without_admin_info_shows_paused_but_no_actor()
    {
        using var factory = NewFactory();
        var host = factory.Services.GetRequiredService<SimulationHost>();
        host.Pause();

        var status = JsonNode.Parse(await factory.CreateClient().GetStringAsync("/simulation/status"))!;

        Assert.True(status["isPaused"]!.GetValue<bool>());
        Assert.Null(status["pausedBy"]);
    }
}
