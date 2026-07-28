using System.Net;
using System.Net.Http.Json;
using LivingWorld.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T15 (CITY-06): <c>GET /npcs/{id}</c> — devolve o DTO de inspeção para NPC
/// vivo (AC #1) e 404, nunca 500, para id inexistente/morto (AC #3).</summary>
public class NpcEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public NpcEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetNpc_returns_200_with_the_inspection_dto_for_a_living_npc()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/npcs/0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<NpcInspectionDto>();
        Assert.NotNull(dto);
        Assert.Equal(0, dto!.Id.Value);
    }

    [Fact]
    public async Task GetNpc_returns_404_for_an_id_that_does_not_exist()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/npcs/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
