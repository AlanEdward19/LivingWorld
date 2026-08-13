namespace LivingWorld.Domain;

/// <summary>Resolução de bounds unificada pelos três níveis de <see cref="SpaceKind"/> (Fase
/// 15.1, T46/ADR-0018) — nenhuma fórmula nova: World vem de <see cref="WorldMap"/>, City delega
/// a <see cref="CityBoundsResolver"/> (T45), Building usa as dimensões do próprio footprint
/// (T45) — não existe um segundo número de "tamanho do prédio" no domínio.</summary>
public static class SpatialBoundsResolver
{
    public static CityBounds ResolveWorld(WorldMap map) => new(new CellCoord(0, 0), map.Width, map.Height);

    public static (CityBounds Bounds, bool IsDerived) ResolveCity(City city, long population, int mapWidth, int mapHeight) =>
        CityBoundsResolver.Resolve(city, population, mapWidth, mapHeight);

    public static CityBounds ResolveBuilding(Building building)
    {
        var footprint = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId);
        int width = footprint.Count == 0 ? 0 : footprint.Max(c => c.Cell.X) + 1;
        int height = footprint.Count == 0 ? 0 : footprint.Max(c => c.Cell.Y) + 1;
        return new CityBounds(new CellCoord(0, 0), width, height);
    }
}
