using System.Net;
using System.Net.Http.Json;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T4 (VTT-01): <c>GET /visual/subscribe</c> no escopo world entrega a projeção
/// global (não mais <c>Payload: null</c> do gateway genérico de T3) — o espectador que abre o
/// mapa-múndi recebe cidades/camadas de verdade.</summary>
[Collection(ApiEndpointCollection.Name)]
public class GlobalProjectionEndpointTests
{
    private readonly LivingWorldApiFactory _factory;

    public GlobalProjectionEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Subscribing_to_world_scope_returns_a_populated_global_projection()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/visual/subscribe?scope=World&mode=Spectator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var payload = json.GetProperty("payload");
        Assert.True(payload.TryGetProperty("cities", out _));
        Assert.True(payload.TryGetProperty("layers", out var layers));
        var terrain = layers.GetProperty("Terrain");
        Assert.True(terrain.GetProperty("isModeled").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Array, terrain.GetProperty("payload").ValueKind);
        Assert.False(layers.GetProperty("Roads").GetProperty("isModeled").GetBoolean());
    }
}
