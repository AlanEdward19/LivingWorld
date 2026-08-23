namespace LivingWorld.Domain;

/// <summary>Resolução de bounds unificada pelos três níveis de <see cref="SpaceKind"/> (Fase
/// 15.1, T46/ADR-0018) — nenhuma fórmula nova: World vem de <see cref="WorldMap"/>, City delega
/// a <see cref="CityBoundsResolver"/> (T45), Building usa as dimensões do próprio footprint
/// (T45) — não existe um segundo número de "tamanho do prédio" no domínio.</summary>
public static class SpatialBoundsResolver
{
    public static CityBounds ResolveWorld(WorldMap map) => new(new CellCoord(0, 0), map.Width, map.Height);

    // dynamic-city-growth, T4b: repassa os dois parâmetros opcionais que CityBoundsResolver.Resolve
    // já define (T4) — sem eles aqui, todo call site real ficava travado no comportamento
    // "sem overflow" de sempre, mesmo depois de T4 tornar Resolve capaz de crescer.
    // Post-ship fix: repassa o novo parâmetro opcional otherCityBoundsToAvoid que
    // CityBoundsResolver.Resolve ganhou — mesmo padrão de repasse já usado pelos dois anteriores.
    public static (CityBounds Bounds, bool IsDerived) ResolveCity(
        City city, long population, int mapWidth, int mapHeight,
        IReadOnlyList<CityBounds>? ownedBuildingFootprintBoxes = null, int absorptionRingCells = 3,
        IReadOnlyList<CityBounds>? otherCityBoundsToAvoid = null) =>
        CityBoundsResolver.Resolve(
            city, population, mapWidth, mapHeight, ownedBuildingFootprintBoxes, absorptionRingCells, otherCityBoundsToAvoid);

    public static CityBounds ResolveBuilding(Building building)
    {
        var footprint = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId);
        int width = footprint.Count == 0 ? 0 : footprint.Max(c => c.Cell.X) + 1;
        int height = footprint.Count == 0 ? 0 : footprint.Max(c => c.Cell.Y) + 1;
        return new CityBounds(new CellCoord(0, 0), width, height);
    }
}
