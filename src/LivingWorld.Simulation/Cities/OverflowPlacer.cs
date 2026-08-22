using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (dynamic-city-growth, T2): mesmo motivo de CityOccupancy.cs — design.md declara
// este tipo em src/LivingWorld.Domain/Cities/, mas WorldState só existe em
// LivingWorld.Simulation (Domain não referencia Simulation).

/// <summary>Busca em anéis crescentes a partir da borda dos bounds da cidade (dynamic-city-growth,
/// T2, CITYGROW-02) — quando <see cref="CityOccupancy.FindFreeCellInBounds"/> não acha célula
/// livre dentro dos bounds atuais, este é o fallback: primeira célula livre fora deles, raio
/// crescente, ordem angular determinística por <see cref="BuildingId"/> (mesmo estilo de
/// <c>BuildingPlacementResolver.DerivedPosition</c>, generalizado pra um raio que cresce em vez de
/// fixo).</summary>
public static class OverflowPlacer
{
    // ponytail: raio sem teto — o mapa é finito na prática (bounds de cidade nunca passam de
    // mapWidth/mapHeight), então o loop sempre termina antes de esgotar o mapa; o caso "não há
    // célula livre em lugar nenhum do mapa" é responsabilidade de CityOccupancy.IsLandScarce
    // (checado pelo chamador antes de cair aqui), não deste método.
    public static CellCoord ResolveOverflowPosition(
        WorldState world, City city, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> footprintShape)
    {
        ulong hash = StableHash.Mix(id.Value);

        for (int radius = 1; ; radius++)
        {
            var ring = RingCells(bounds, radius);
            int offset = (int)(hash % (ulong)ring.Count);
            for (int i = 0; i < ring.Count; i++)
            {
                var origin = ring[(offset + i) % ring.Count];
                var candidate = CityOccupancy.Translate(footprintShape, origin);
                if (CityOccupancy.IsFree(world, city, candidate))
                    return origin;
            }
        }
    }

    /// <summary>Perímetro do retângulo dos bounds expandido por <paramref name="radius"/> células
    /// em cada direção — o "anel" a essa distância da borda atual.</summary>
    private static List<CellCoord> RingCells(CityBounds bounds, int radius)
    {
        int minX = bounds.Origin.X - radius;
        int maxX = bounds.Origin.X + bounds.Width - 1 + radius;
        int minY = bounds.Origin.Y - radius;
        int maxY = bounds.Origin.Y + bounds.Height - 1 + radius;

        var cells = new List<CellCoord>();
        for (int x = minX; x <= maxX; x++)
        {
            cells.Add(new CellCoord(x, minY));
            cells.Add(new CellCoord(x, maxY));
        }
        for (int y = minY + 1; y <= maxY - 1; y++)
        {
            cells.Add(new CellCoord(minX, y));
            cells.Add(new CellCoord(maxX, y));
        }
        return cells;
    }
}
