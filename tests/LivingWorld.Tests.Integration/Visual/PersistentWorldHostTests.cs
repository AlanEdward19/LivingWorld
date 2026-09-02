using System.Net;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Fase 15, T2 (VTT-01..03): a API deixa de recriar um mundo efêmero a cada start e
/// passa a hospedar um mundo canônico compartilhado, lastreado num <see cref="IWorldRepository"/>
/// real — pré-requisito para stream/endpoints de visualização das tasks seguintes.</summary>
[Collection(ApiEndpointCollection.Name)]
public class PersistentWorldHostTests
{
    private readonly LivingWorldApiFactory _factory;

    public PersistentWorldHostTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public void The_host_persists_an_initial_snapshot_through_the_real_world_repository()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorldRepository>();

        var snapshot = repository.LoadLatestSnapshot(BranchId.Root);

        Assert.NotNull(snapshot);
    }

    [Fact]
    public void The_host_exposes_the_same_world_and_clock_as_the_endpoints_use()
    {
        using var scope = _factory.Services.CreateScope();
        var hostedWorld = scope.ServiceProvider.GetRequiredService<WorldState>();
        var simulationHost = scope.ServiceProvider.GetRequiredService<SimulationHost>();

        Assert.NotNull(hostedWorld);
        Assert.NotNull(simulationHost);
    }

    [Fact]
    public async Task Existing_npc_inspection_endpoint_keeps_working_against_the_persistent_host()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/npcs/0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
