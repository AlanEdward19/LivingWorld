namespace LivingWorld.Domain;

/// <summary>Resolve posição/orientação de um prédio (Fase 15.1, T45; G4/backend-gaps.md):
/// autoria (T44 — <see cref="Building.Position"/> não nulo) tem precedência; sem autoria (prédio
/// construído pela simulação, <see cref="ConstructionSystem"/> ainda não escolhe site), cai num
/// fallback determinístico e estável por <see cref="BuildingId"/> — nunca a mesma célula
/// coincidindo por acaso com outro prédio na prática, nunca aleatório, nunca move ao reordenar a
/// coleção (T20 Done-when).</summary>
public static class BuildingPlacementResolver
{
    private const int DerivedRingRadius = 3;

    public static (CellCoord Position, int Orientation, bool IsDerived) Resolve(Building building, City city)
    {
        if (building.Position is { } position)
            return (position, building.Orientation ?? 0, false);

        return (DerivedPosition(building.Id, city.Location), 0, true);
    }

    private static CellCoord DerivedPosition(BuildingId id, CellCoord cityLocation)
    {
        ulong h = StableHash.Mix(id.Value);
        double angle = (h % 3600) / 3600.0 * 2 * Math.PI;
        int dx = (int)Math.Round(Math.Cos(angle) * DerivedRingRadius);
        int dy = (int)Math.Round(Math.Sin(angle) * DerivedRingRadius);
        return new CellCoord(cityLocation.X + dx, cityLocation.Y + dy);
    }
}
