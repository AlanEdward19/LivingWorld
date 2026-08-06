namespace LivingWorld.Api.Visual.Layers;

/// <summary>Fase 15, T5 (VTT-05): camadas derivadas locais exclusivas de escopo cidade/interior.
/// Nenhuma tem dado canônico no motor (sem classe de Village/Route/Migration/Conflict) — mesmo
/// padrão de fallback de <see cref="GlobalLayerBuilder"/>. <c>Climate</c> não é exclusiva daqui:
/// é overlay reusado do escopo global (spec.md: "+ overlays climáticos"), montada via <see
/// cref="GlobalLayerBuilder"/> por quem chama, não por este builder.</summary>
public static class CityLayerBuilder
{
    public static readonly IReadOnlyList<VisualLayerId> SupportedLayers =
    [
        VisualLayerId.Cities, VisualLayerId.Villages, VisualLayerId.Routes,
        VisualLayerId.Migrations, VisualLayerId.Conflicts
    ];

    public static LayerBuildResult Build(VisualLayerId layerId) => SupportedLayers.Contains(layerId)
        ? LayerBuildResult.NotYetModeled
        : throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "camada não pertence ao escopo cidade/interior (T5)");
}
