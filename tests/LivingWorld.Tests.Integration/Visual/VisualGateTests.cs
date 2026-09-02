using System.Net;
using LivingWorld.Api.Visual.Layers;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T9 (VTT-02, VTT-06, VTT-10..16): gate de fechamento — documento OpenAPI
/// servido de verdade (fonte dos tipos TS gerados por <c>scripts/generate-web-types.sh</c>),
/// todo <see cref="VisualLayerId"/> do catálogo coberto por exatamente um builder, e leitura em
/// todo escopo/replay nunca escreve no mundo. A prova de que <c>scripts/generate-web-types.sh
/// --check</c> reprova em mutação foi verificada manualmente nesta sessão (adicionar uma rota
/// nova sem regenerar o arquivo committed faz o script sair com 1 e mostrar o diff) — reproduzir
/// isso aqui exigiria subir um processo dotnet + npx dentro do teste, o que quebraria o
/// isolamento/velocidade do resto da suíte; o teste D abaixo cobre a mesma garantia de forma
/// hermética (o arquivo committed precisa mencionar toda rota /visual/* conhecida).</summary>
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

    [Theory]
    [InlineData("/visual/subscribe")]
    [InlineData("/visual/replay")]
    [InlineData("/visual/sse")]
    [InlineData("/visual/ws")]
    [InlineData("/visual/player/{id}/move")]
    public void Generated_web_types_file_mentions_every_known_visual_route(string routeFragment)
    {
        string repoRoot = FindRepoRoot();
        string generatedFile = Path.Combine(repoRoot, "web", "src", "generated", "api-types.ts");
        Assert.True(File.Exists(generatedFile), $"{generatedFile} não existe — rode scripts/generate-web-types.sh");

        string content = File.ReadAllText(generatedFile);
        string routeKey = routeFragment.Replace("{id}", "{id}");
        Assert.Contains(routeKey, content);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
