using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Visual.Layers;

/// <summary>Fase 15, T4 (VTT-04..06): camadas derivadas globais sobre o grid de <see
/// cref="WorldMap"/>. Terrain/Biome/Resources vêm direto de <c>MapCell</c>; Rivers usa
/// <c>MapCell.HasWater</c> como aproximação (cobre lago/oceano também, não só rio — é o único
/// sinal de água que o domínio tem hoje). Mountains/Roads/Borders/Kingdoms/Climate voltam <see
/// cref="LayerBuildResult.NotYetModeled"/>: não há limiar documentado para "montanha" em
/// <c>Altitude</c>, e Kingdom/Border/Road/Climate não existem como conceito no motor.</summary>
public static class GlobalLayerBuilder
{
    public static readonly IReadOnlyList<VisualLayerId> SupportedLayers =
    [
        VisualLayerId.Terrain, VisualLayerId.Biome, VisualLayerId.Rivers, VisualLayerId.Mountains,
        VisualLayerId.Resources, VisualLayerId.Roads, VisualLayerId.Borders, VisualLayerId.Kingdoms,
        VisualLayerId.Climate
    ];

    public static LayerBuildResult Build(VisualLayerId layerId, WorldState world) => layerId switch
    {
        VisualLayerId.Terrain => LayerBuildResult.Available(
            world.Map.Cells.Select(c => new KeyValuePair<CellCoord, TerrainType>(c.Coord, c.Terrain)).ToList()),
        VisualLayerId.Biome => LayerBuildResult.Available(
            world.Map.Cells.Select(c => new KeyValuePair<CellCoord, BiomeType>(c.Coord, c.Biome)).ToList()),
        VisualLayerId.Resources => LayerBuildResult.Available(
            world.Map.Cells.Where(c => c.Resources.Count > 0)
                .Select(c => new KeyValuePair<CellCoord, IReadOnlyList<ResourceType>>(c.Coord, c.Resources)).ToList()),
        VisualLayerId.Rivers => LayerBuildResult.Available(
            world.Map.Cells.Where(c => c.HasWater).Select(c => c.Coord).ToList()),
        VisualLayerId.Mountains or VisualLayerId.Roads or VisualLayerId.Borders
            or VisualLayerId.Kingdoms or VisualLayerId.Climate => LayerBuildResult.NotYetModeled,
        _ => throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "camada não pertence ao escopo global (T4)"),
    };
}
