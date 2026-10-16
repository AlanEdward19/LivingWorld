using LivingWorld.Api.Visual.Layers;

namespace LivingWorld.Tests.Unit.Visual.Layers;

/// <summary>Fase 15, T1 (VTT-04): o catálogo deve listar exatamente as camadas derivadas
/// definidas pela spec (terreno, bioma, rios, montanhas, recursos, estradas, fronteiras,
/// reinos, cidades, aldeias, rotas, migrações, conflitos, clima), sem duplicatas.</summary>
public class LayerProjectionCatalogTests
{
    private static readonly VisualLayerId[] ExpectedLayers =
    {
        VisualLayerId.Terrain, VisualLayerId.Biome, VisualLayerId.Rivers, VisualLayerId.Mountains,
        VisualLayerId.Resources, VisualLayerId.Roads, VisualLayerId.Borders, VisualLayerId.Kingdoms,
        VisualLayerId.Cities, VisualLayerId.Villages, VisualLayerId.Routes, VisualLayerId.Migrations,
        VisualLayerId.Conflicts, VisualLayerId.Climate
    };

    [Fact]
    public void ListLayers_returns_exactly_the_fourteen_layers_from_the_spec()
    {
        var layers = LayerProjectionCatalog.ListLayers();

        Assert.Equal(ExpectedLayers.Length, layers.Count);
        foreach (var expected in ExpectedLayers)
            Assert.Contains(expected, layers);
    }

    [Fact]
    public void ListLayers_has_no_duplicate_entries()
    {
        var layers = LayerProjectionCatalog.ListLayers();

        Assert.Equal(layers.Count, layers.Distinct().Count());
    }
}
