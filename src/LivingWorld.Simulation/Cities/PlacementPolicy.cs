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

    /// <summary>Degraus de relaxamento quando falta terra (AD-173). De frente para a rua (<see cref="StreetLevels"/>):
    /// 0 = o lote escolhido; 1 = sem quintal de trás; 2 = parede-meia. Sem rua (os degraus antigos): 3 = folga e sem água;
    /// 4 = encostando, ainda sem água; 5 = só sem sobreposição. Um prédio desce ao próximo degrau somente depois que o
    /// anterior não achou lugar em todo o mapa — nunca por preferência. Rua e passagem de porta nunca recebem prédio, salvo
    /// no último degrau.</summary>
    public const int LevelCount = 6;

    /// <summary>Quantos degraus são de lote na rua; os seguintes são os degraus antigos (<see cref="For"/> com nível − este).</summary>
    public const int StreetLevels = 3;

    public int Gap { get; }
    private readonly HashSet<CellCoord>? _water;

    private PlacementPolicy(int gap, HashSet<CellCoord>? water)
    {
        Gap = gap;
        _water = water;
    }

    /// <summary>Política de um degrau sem rua: 0 = folga e sem água; 1 = encostando; 2 = só sem sobreposição.</summary>
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
    public bool Allows(
        IReadOnlyList<CellCoord> shape, CellCoord[] halo, CellCoord origin, HashSet<CellCoord> occupied, GroundGuard? guard = null)
    {
        for (int i = 0; i < halo.Length; i++)
            if (occupied.Contains(new CellCoord(halo[i].X + origin.X, halo[i].Y + origin.Y))) return false;
        if (guard is not null && !guard.Allows(shape, origin, occupied, _water)) return false;
        if (_water is null) return true;
        for (int i = 0; i < shape.Count; i++)
            if (_water.Contains(new CellCoord(shape[i].X + origin.X, shape[i].Y + origin.Y))) return false;
        return true;
    }
}

/// <summary>AD-173, nos degraus sem rua: a planta não cai em rua nem na passagem de porta de outro prédio, e a soleira do
/// próprio prédio (a célula na frente da porta) fica livre e seca, para a porta ter por onde sair.</summary>
internal sealed class GroundGuard(HashSet<CellCoord> streets, HashSet<CellCoord> passages, CellCoord? doorstep)
{
    public bool Allows(IReadOnlyList<CellCoord> shape, CellCoord origin, HashSet<CellCoord> occupied, HashSet<CellCoord>? water)
    {
        for (int i = 0; i < shape.Count; i++)
        {
            var abs = new CellCoord(shape[i].X + origin.X, shape[i].Y + origin.Y);
            if (streets.Contains(abs) || passages.Contains(abs)) return false;
        }
        if (doorstep is not { } step) return true;
        var at = new CellCoord(step.X + origin.X, step.Y + origin.Y);
        return !occupied.Contains(at) && (water is null || !water.Contains(at));
    }
}
