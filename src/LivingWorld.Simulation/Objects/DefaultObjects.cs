using System.Text.Json.Nodes;

namespace LivingWorld.Simulation.Objects;

/// <summary>Mobília que o cliente desenhava a partir de <c>assets.json</c> (#40), como blocos de cenário (#47 fatia 1, ADR-0034). Desde o #255 o catálogo
/// padrão não vive mais no código: é o dado <c>scenarios/catalog/default-objects.json</c>, embutido e lido em tempo de execução
/// (<see cref="HomeLifeLoader.Embedded"/>); o cenário o sobrescreve ou estende por id. <c>offset</c> é o centro da parte relativo à base do objeto
/// (chão), por isso y = altura/2.</summary>
public static class DefaultObjects
{
    public const string CatalogName = "default-objects";

    /// <summary>Acrescenta <c>objectsFormat</c>, <c>Objects</c>, <c>Furnishings</c> e <c>Rooms</c> do catálogo padrão ao JSON do período
    /// (o que o período já declara vence por id).</summary>
    public static void AddTo(JsonObject root)
    {
        var catalog = HomeLifeLoader.Embedded(CatalogName) ?? throw new InvalidOperationException($"catálogo padrão '{CatalogName}' não está embutido");
        HomeLifeLoader.Merge(root, catalog);
    }
}
