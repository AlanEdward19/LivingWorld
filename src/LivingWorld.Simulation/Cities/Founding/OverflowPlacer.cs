using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Founding;

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
    // dynamic-city-growth, fix (major, CITYGROW-02b): o raio agora TEM teto, amarrado ao mapa
    // real (world.Map.Width/Height) -- antes crescia sem limite e podia devolver uma célula fora
    // do mapa (negativa ou >= largura/altura) num mundo totalmente construído, "posicionando" em
    // vez de corretamente recusar. `null` significa exatamente isso: nenhuma célula livre em
    // lugar nenhum do mapa alcançável a partir destes bounds -- o chamador (BuildingPlacementResolver)
    // trata isso como escassez de terra e simplesmente deixa o prédio sem posição por esta
    // chamada (ver design.md, Error Handling Strategy).
    public static CellCoord? ResolveOverflowPosition(
        WorldState world, City city, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> footprintShape)
    {
        // dynamic-city-growth, fix (blocker): antes, cada célula do anel chamava
        // CityOccupancy.IsFree, que recomputava a ocupação da cidade inteira do zero -- em cima da
        // recursão de OccupiedCellsOfCity, isso tornava o overflow O(anel * 2^N). O conjunto
        // ocupado é o mesmo pra toda a busca do anel, então computa uma vez só.
        var occupied = CityOccupancy.OccupiedCellsOfCity(world, city, bounds, id);
        return ResolveOverflowPositionGiven(occupied, bounds, id, footprintShape, world.Map.Width, world.Map.Height);
    }

    /// <summary>Mesma busca em anéis crescentes de <see cref="ResolveOverflowPosition"/>, mas
    /// contra um conjunto de células já ocupadas conhecido -- não recomputa ocupação e não
    /// chama de volta em <see cref="CityOccupancy"/>. Usada pelo próprio
    /// <see cref="CityOccupancy.OccupiedCellsOfCity"/> ao resolver a posição de vizinhos ainda
    /// sem posição autorada, sem reentrar em nenhum caminho recursivo. Só considera candidatos
    /// cujo footprint inteiro cai dentro de <c>[0, mapWidth) x [0, mapHeight)</c> -- fora disso
    /// não é uma célula real do mapa, então nunca pode "ganhar". <c>null</c> quando o raio já
    /// cresceu além de <paramref name="mapWidth"/> + <paramref name="mapHeight"/> (a essa
    /// distância o anel já saiu inteiramente do mapa nas duas dimensões — continuar não acha mais
    /// nada, é escassez de terra real).</summary>
    internal static CellCoord? ResolveOverflowPositionGiven(
        HashSet<CellCoord> occupied, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> footprintShape,
        int mapWidth, int mapHeight)
    {
        ulong hash = StableHash.Mix(id.Value);
        int maxRadius = mapWidth + mapHeight;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            var ring = RingCells(bounds, radius);
            int offset = (int)(hash % (ulong)ring.Count);
            for (int i = 0; i < ring.Count; i++)
            {
                var origin = ring[(offset + i) % ring.Count];
                var candidate = CityOccupancy.Translate(footprintShape, origin);
                if (candidate.All(cell => WithinMap(cell, mapWidth, mapHeight)) && candidate.All(cell => !occupied.Contains(cell)))
                    return origin;
            }
        }
        return null;
    }

    private static bool WithinMap(CellCoord cell, int mapWidth, int mapHeight) =>
        cell.X >= 0 && cell.X < mapWidth && cell.Y >= 0 && cell.Y < mapHeight;

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
