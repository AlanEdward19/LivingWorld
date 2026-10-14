using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Founding;

// SPEC_DEVIATION (dynamic-city-growth, T2): mesmo motivo de CityOccupancy.cs — design.md declara
// este tipo em src/LivingWorld.Domain/Cities/, mas WorldState só existe em
// LivingWorld.Simulation (Domain não referencia Simulation).

/// <summary>Busca em anéis crescentes a partir da borda dos bounds da cidade — quando
/// <see cref="CityOccupancy.FindFreeCellInBounds"/> não acha célula
/// livre dentro dos bounds atuais, este é o fallback: primeira célula livre fora deles, raio
/// crescente, ordem angular determinística por <see cref="BuildingId"/> (mesmo estilo de
/// <c>BuildingPlacementResolver.DerivedPosition</c>, generalizado pra um raio que cresce em vez de
/// fixo).</summary>
public static class OverflowPlacer
{
    // fix (major): o raio agora TEM teto, amarrado ao mapa
    // real (world.Map.Width/Height) -- antes crescia sem limite e podia devolver uma célula fora
    // do mapa (negativa ou >= largura/altura) num mundo totalmente construído, "posicionando" em
    // vez de corretamente recusar. `null` significa exatamente isso: nenhuma célula livre em
    // lugar nenhum do mapa alcançável a partir destes bounds -- o chamador (BuildingPlacementResolver)
    // trata isso como escassez de terra e simplesmente deixa o prédio sem posição por esta
    // chamada (ver design.md, Error Handling Strategy).
    public static CellCoord? ResolveOverflowPosition(
        WorldState world, City city, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> footprintShape) =>
        ResolveOverflowPosition(world, city, bounds, id, footprintShape, 1, out _, 0);

    internal static CellCoord? ResolveOverflowPosition(
        WorldState world, City city, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> footprintShape,
        int startRadius, out int foundRadius, int level)
    {
        // dynamic-city-growth, fix (blocker): antes, cada célula do anel chamava
        // CityOccupancy.IsFree, que recomputava a ocupação da cidade inteira do zero -- em cima da
        // recursão de OccupiedCellsOfCity, isso tornava o overflow O(anel * 2^N). O conjunto
        // ocupado é o mesmo pra toda a busca do anel, então computa uma vez só.
        var occupied = CityOccupancy.OccupiedCellsOfCity(world, city, bounds, id);
        return ResolveOverflowPositionGiven(
            occupied, bounds, id, footprintShape, world.Map.Width, world.Map.Height, startRadius, out foundRadius,
            PlacementPolicy.For(world.Map, level));
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
        int mapWidth, int mapHeight) =>
        ResolveOverflowPositionGiven(occupied, bounds, id, footprintShape, mapWidth, mapHeight, 1, out _, PlacementPolicy.None);

    /// <summary><paramref name="startRadius"/> permite pular anéis que o chamador já sabe inteiramente
    /// ocupados para esta forma (a ocupação só cresce dentro de um passe). <paramref name="foundRadius"/>
    /// é o raio do anel onde achou (ou além do último, se não achou) — todo anel menor falhou.</summary>
    internal static CellCoord? ResolveOverflowPositionGiven(
        HashSet<CellCoord> occupied, CityBounds bounds, BuildingId id, IReadOnlyList<CellCoord> footprintShape,
        int mapWidth, int mapHeight, int startRadius, out int foundRadius, PlacementPolicy policy)
    {
        var halo = policy.HaloOf(footprintShape);
        ulong hash = StableHash.Mix(id.Value);
        int maxRadius = mapWidth + mapHeight;

        for (int radius = Math.Max(1, startRadius); radius <= maxRadius; radius++)
        {
            int count = RingCount(bounds, radius);
            int offset = (int)(hash % (ulong)count);
            for (int i = 0; i < count; i++)
            {
                var origin = RingCellAt(bounds, radius, (offset + i) % count);
                if (FitsAt(footprintShape, halo, origin, occupied, mapWidth, mapHeight, policy))
                {
                    foundRadius = radius;
                    return origin;
                }
            }
        }
        foundRadius = maxRadius + 1;
        return null;
    }

    /// <summary>O footprint inteiro, transladado para <paramref name="origin"/>, cai no mapa e em
    /// células livres. Sem lista intermediária: o laço de anéis chama isto milhões de vezes.</summary>
    private static bool FitsAt(
        IReadOnlyList<CellCoord> shape, CellCoord[] halo, CellCoord origin, HashSet<CellCoord> occupied,
        int mapWidth, int mapHeight, PlacementPolicy policy)
    {
        for (int i = 0; i < shape.Count; i++)
        {
            var cell = new CellCoord(shape[i].X + origin.X, shape[i].Y + origin.Y);
            if (!WithinMap(cell, mapWidth, mapHeight)) return false;
        }
        return policy.Allows(shape, halo, origin, occupied);
    }

    private static bool WithinMap(CellCoord cell, int mapWidth, int mapHeight) =>
        cell.X >= 0 && cell.X < mapWidth && cell.Y >= 0 && cell.Y < mapHeight;

    // O anel é o perímetro do retângulo dos bounds expandido por `radius` células em cada direção.
    // Ordem (contrato com o hash de posicionamento): para cada x, (x, minY) e (x, maxY); depois, para
    // cada y interno, (minX, y) e (maxX, y). Acesso por índice, sem materializar a lista.
    private static int RingCount(CityBounds bounds, int radius) =>
        2 * (bounds.Width + 2 * radius) + 2 * (bounds.Height + 2 * radius - 2);

    private static CellCoord RingCellAt(CityBounds bounds, int radius, int index)
    {
        int minX = bounds.Origin.X - radius;
        int maxX = bounds.Origin.X + bounds.Width - 1 + radius;
        int minY = bounds.Origin.Y - radius;
        int maxY = bounds.Origin.Y + bounds.Height - 1 + radius;

        int horizontal = 2 * (maxX - minX + 1);
        if (index < horizontal)
            return new CellCoord(minX + index / 2, index % 2 == 0 ? minY : maxY);

        int j = index - horizontal;
        return new CellCoord(j % 2 == 0 ? minX : maxX, minY + 1 + j / 2);
    }
}
