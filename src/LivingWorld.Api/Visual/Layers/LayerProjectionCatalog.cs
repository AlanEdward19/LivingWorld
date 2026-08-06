namespace LivingWorld.Api.Visual.Layers;

/// <summary>Fase 15, T1 (VTT-04): registro estático das camadas derivadas suportadas.
/// A construção do conteúdo de cada camada (<c>BuildLayer</c>) chega com os projectors
/// de mundo/cidade (T4/T5) — este catálogo só garante que toda camada do enum está listada.</summary>
public static class LayerProjectionCatalog
{
    private static readonly IReadOnlyList<VisualLayerId> AllLayers = Enum.GetValues<VisualLayerId>();

    public static IReadOnlyList<VisualLayerId> ListLayers() => AllLayers;
}
