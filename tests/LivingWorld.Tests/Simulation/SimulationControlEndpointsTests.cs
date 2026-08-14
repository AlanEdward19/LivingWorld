using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api;
using LivingWorld.Api.Simulation;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Simulation;

/// <summary>Fase 15.1, T1 (VTT2-27..30): <c>POST /simulation/pause|resume|speed|step</c> e
/// <c>GET /simulation/status</c> como tradução fina sobre <see cref="SimulationHost"/>. Cada
/// teste força suas próprias precondições (pause/resume explícitos) porque o
/// <see cref="WebApplicationFactory{TEntryPoint}"/> é compartilhado (via
/// <see cref="IClassFixture{TFixture}"/>) entre todos os métodos desta classe, e
/// <see cref="SimulationHost"/> é um singleton com estado mutável.</summary>
public class SimulationControlEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SimulationControlEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Pause_marks_status_as_paused()
    {
        var client = _factory.CreateClient();

        var pauseResponse = await client.PostAsync("/simulation/pause", null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);

        var status = await client.GetFromJsonAsync<SimulationStatusResponse>("/simulation/status");
        Assert.True(status!.IsPaused);
        Assert.Equal(_factory.Services.GetRequiredService<WorldHost>().Current.CurrentDate.TotalHours, status.Tick);
        Assert.Equal(_factory.Services.GetRequiredService<WorldHost>().Current.CurrentDate.Year, status.Year);
    }

    [Fact]
    public async Task Resume_clears_paused_status()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/simulation/pause", null);

        var resumeResponse = await client.PostAsync("/simulation/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);

        var status = await client.GetFromJsonAsync<SimulationStatusResponse>("/simulation/status");
        Assert.False(status!.IsPaused);
    }

    [Fact]
    public async Task Speed_with_a_positive_value_updates_ticks_per_second()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/simulation/speed", new SetSpeedRequest(4.0));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await client.GetFromJsonAsync<SimulationStatusResponse>("/simulation/status");
        Assert.Equal(4.0, status!.TicksPerSecond);
    }

    [Fact]
    public async Task Speed_with_a_non_positive_value_returns_400_and_does_not_change_ticks_per_second()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/simulation/speed", new SetSpeedRequest(2.0));

        var response = await client.PostAsJsonAsync("/simulation/speed", new SetSpeedRequest(0.0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var status = await client.GetFromJsonAsync<SimulationStatusResponse>("/simulation/status");
        Assert.Equal(2.0, status!.TicksPerSecond);
    }

    [Fact]
    public async Task Step_while_paused_advances_the_world_clock_by_exactly_one_tick()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/simulation/pause", null);

        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var before = world.CurrentDate.TotalHours;

        var response = await client.PostAsync("/simulation/step", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 1, world.CurrentDate.TotalHours);
        var status = await client.GetFromJsonAsync<SimulationStatusResponse>("/simulation/status");
        Assert.Equal(before + 1, status!.Tick);
        Assert.Equal(world.CurrentDate.Year, status.Year);
    }

    [Fact]
    public async Task Step_while_running_returns_409_and_does_not_advance_the_clock()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/simulation/resume", null);

        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var before = world.CurrentDate.TotalHours;

        var response = await client.PostAsync("/simulation/step", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, world.CurrentDate.TotalHours);
    }

    [Fact]
    public async Task Pause_resume_and_speed_calls_never_change_the_canonical_hash()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var client = _factory.CreateClient();
        for (int i = 0; i < 3; i++)
        {
            await client.PostAsync("/simulation/pause", null);
            await client.PostAsync("/simulation/resume", null);
            await client.PostAsJsonAsync("/simulation/speed", new SetSpeedRequest(2.0 + i));
        }

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }
}
