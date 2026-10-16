using System.Net;
using LivingWorld.Api.Visual.Layers;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Fase 15, T9 (VTT-02, VTT-06, VTT-10..16): gate de fechamento — documento OpenAPI
/// servido de verdade, todo <see cref="VisualLayerId"/> do catálogo coberto por exatamente um builder,
/// e leitura em todo escopo/replay nunca escreve no mundo.</summary>
[Collection(ApiEndpointCollection.Name)]
public class VisualGateTests
{
    private readonly LivingWorldApiFactory _factory;

    public VisualGateTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApi_document_is_served_and_describes_the_visual_subscribe_endpoint()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/visual/subscribe", json);
    }

    [Fact]
    public void Every_visual_layer_id_is_covered_by_exactly_one_layer_builder()
    {
        var allLayers = Enum.GetValues<VisualLayerId>();
        var globalOnly = GlobalLayerBuilder.SupportedLayers;
        var cityOnly = CityLayerBuilder.SupportedLayers;

        foreach (var layer in allLayers)
        {
            bool inGlobal = globalOnly.Contains(layer);
            bool inCity = cityOnly.Contains(layer);

            Assert.True(inGlobal || inCity, $"{layer}: nenhum builder cobre esta camada");
            Assert.False(inGlobal && inCity, $"{layer}: coberta por dois builders (global e city) — ambíguo");
        }

        Assert.Equal(allLayers.Length, globalOnly.Count + cityOnly.Count);
    }

    [Fact]
    public async Task Reading_and_subscribing_across_every_scope_kind_never_changes_the_canonical_hash()
    {
        using var scope = _factory.Services.CreateScope();
        var world = scope.ServiceProvider.GetRequiredService<WorldState>();
        var hashBefore = WorldSnapshot.CanonicalHash(world);

        var client = _factory.CreateClient();
        await client.GetAsync("/visual/subscribe?scope=World&mode=Spectator");
        await client.GetAsync($"/visual/subscribe?scope=City&refId={Guid.NewGuid()}&mode=Spectator");
        await client.GetAsync("/visual/subscribe?scope=Interior&refId=999999&mode=Spectator");
        await client.GetAsync("/visual/replay?scope=World&mode=Spectator&sinceTick=0&sinceSequence=0");

        Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world));
    }
}
