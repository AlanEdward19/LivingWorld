using System.Runtime.CompilerServices;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;

namespace LivingWorld.Simulation.Cities;

/// <summary>Regra de onde um prédio derivado pode pousar além de "célula livre" (city-building-separation):
/// folga mínima de <see cref="Gap"/> células de qualquer outro prédio e nenhuma célula de água.
/// Derivada só do mapa (nada persistido), então a posição de um prédio continua sendo função pura
/// do mundo — mesma seed, mesma posição. <see cref="None"/> é a regra antiga (só sobreposição),
/// mantida para chamadas que passam um conjunto ocupado cru.</summary>
internal sealed class PlacementPolicy
{
    public static readonly PlacementPolicy None = new(0, null);

    private static readonly ConditionalWeakTable<WorldMap, PlacementPolicy[]> ByMap = new();

    /// <summary>Degraus de relaxamento quando falta terra: 0 = folga e sem água (o normal); 1 = encostando,
    /// ainda sem água; 2 = só sem sobreposição (regra antiga). Um prédio desce ao próximo degrau somente
    /// depois que o degrau anterior não achou nenhuma célula em todo o mapa — nunca por preferência.</summary>
    public const int LevelCount = 3;

    public int Gap { get; }
    private readonly HashSet<CellCoord>? _water;

    private PlacementPolicy(int gap, HashSet<CellCoord>? water)
    {
        Gap = gap;
        _water = water;
    }

    public static PlacementPolicy For(WorldMap map, int level = 0) =>
        ByMap.GetValue(map, m =>
        {
            var water = m.Cells.Where(c => c.HasWater).Select(c => c.Coord).ToHashSet();
            return [new PlacementPolicy(CityOccupancy.MinGap, water), new PlacementPolicy(0, water), None];
        })[level];

    /// <summary>Footprint dilatado em <see cref="Gap"/> células (Chebyshev), sem repetição e em ordem estável.
    /// É o que não pode tocar em nada ocupado; calculado uma vez por busca, não por candidato.</summary>
    public CellCoord[] HaloOf(IReadOnlyList<CellCoord> shape)
    {
        if (Gap == 0) return shape as CellCoord[] ?? shape.ToArray();
        var seen = new HashSet<CellCoord>();
        var halo = new List<CellCoord>();
        foreach (var cell in shape)
            for (int dy = -Gap; dy <= Gap; dy++)
                for (int dx = -Gap; dx <= Gap; dx++)
                {
                    var neighbor = new CellCoord(cell.X + dx, cell.Y + dy);
                    if (seen.Add(neighbor)) halo.Add(neighbor);
                }
        return halo.ToArray();
    }

    /// <summary>Sem sobreposição nem encosto (<paramref name="halo"/> livre) e sem água sob o footprint.
    /// Limites do mapa e dos bounds são problema do chamador.</summary>
    public bool Allows(IReadOnlyList<CellCoord> shape, CellCoord[] halo, CellCoord origin, HashSet<CellCoord> occupied)
    {
        for (int i = 0; i < halo.Length; i++)
            if (occupied.Contains(new CellCoord(halo[i].X + origin.X, halo[i].Y + origin.Y))) return false;
        if (_water is null) return true;
        for (int i = 0; i < shape.Count; i++)
            if (_water.Contains(new CellCoord(shape[i].X + origin.X, shape[i].Y + origin.Y))) return false;
        return true;
    }
}
