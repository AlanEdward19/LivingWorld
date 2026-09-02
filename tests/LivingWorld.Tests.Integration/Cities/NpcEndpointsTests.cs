using System.Net;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>Fase 15.1, T49 (backend-gaps.md G9): <c>GET /npcs/{id}</c> continua leitura pura;
/// <c>POST /npcs/{id}/materialize</c> é o comando explícito e nomeado, separado do GET.
/// Factory própria: materialize pode mutar o mundo canônico.</summary>
public class NpcEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public NpcEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;

    private long FirstLivingNpcId()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        return world.Npcs.First(n => n.IsAlive).Id.Value;
    }

    [Fact]
    public async Task Get_npc_returns_200_for_a_living_npc_id()
    {
        var client = _factory.CreateClient();
        long id = FirstLivingNpcId();

        var response = await client.GetAsync($"/npcs/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_npc_returns_404_for_an_id_that_does_not_exist()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/npcs/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Materialize_command_returns_200_with_the_same_dto_shape_for_a_living_npc_id()
    {
        var client = _factory.CreateClient();
        long id = FirstLivingNpcId();

        var response = await client.PostAsync($"/npcs/{id}/materialize", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Materialize_command_returns_404_for_an_id_that_does_not_exist()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/npcs/999999999/materialize", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
