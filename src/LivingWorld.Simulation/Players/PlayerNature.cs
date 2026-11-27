using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>#239/#241: onde há árvore para o corpo conduzido cortar lenha. A árvore é a que o cliente desenha (<c>scenery.gd</c>): terra seca,
/// fora do contorno das cidades (já crescido, mais uma célula de folga) e com o sorteio da célula abaixo de 16%; o motor diz onde cada
/// árvore está (o sorteio é função pura da célula, o mesmo <c>cell_hash</c> do cliente, travado por teste de paridade) e o cliente
/// desenha o que o motor aceita. A oferta <c>nature</c>/<c>gather</c> só aparece a até 1,5 m do tronco. Só lê; o limite das cidades
/// fica em memória por hora de mundo.</summary>
internal static class PlayerNature
{
    /// <summary>Chance de uma célula ter árvore (o <c>roll &lt; 0.16</c> do cliente).</summary>
    public const double TreeChance = 0.16;

    /// <summary>Até onde se alcança o tronco com o machado: 1,5 m (uma célula na escala de 1,5 m).</summary>
    public const double ReachMeters = 1.5;

    /// <summary>Uma árvore: a célula e o tronco em milésimos de célula absolutos (o jitter do cliente, ±0,3 célula).</summary>
    public readonly record struct Tree(CellCoord Cell, long FineX, long FineY);

    private sealed class Cache
    {
        public long Hour = -1;
        public List<CityBounds> Cities = [];
    }

    private static readonly ConditionalWeakTable<WorldState, Cache> Caches = new();

    /// <summary>O mesmo <c>Scenery.cell_hash</c> do cliente (inteiros de 64 bits, deslocamento aritmético): 0..1 por célula e sal.</summary>
    public static double CellHash(CellCoord cell, int salt = 0)
    {
        unchecked
        {
            long h = ((long)cell.X * 73856093L) ^ ((long)cell.Y * 19349663L) ^ ((long)salt * 83492791L);
            h = (h ^ (h >> 13)) * 1274126177L;
            long a = h == long.MinValue ? h : Math.Abs(h);
            return (a % 10000) / 10000.0;
        }
    }

    /// <summary>A árvore da célula, ou nula: seca, fora das cidades e sem prédio, com o sorteio do cliente.</summary>
    public static Tree? TreeAt(WorldState world, CellCoord coord)
    {
        if (CellHash(coord) >= TreeChance || !world.Map.TryGetCell(coord, out var cell) || cell.HasWater) return null;
        foreach (var b in Known(world).Cities)
            if (coord.X >= b.Origin.X - 1 && coord.X <= b.Origin.X + b.Width && coord.Y >= b.Origin.Y - 1 && coord.Y <= b.Origin.Y + b.Height) return null;
        if (PlayerGround.At(world, coord) is not null) return null;
        long x = coord.X * (long)FinePose.PerCell + FinePose.PerCell / 2 + (long)Math.Round((CellHash(coord, 1) - 0.5) * 0.6 * FinePose.PerCell);
        long y = coord.Y * (long)FinePose.PerCell + FinePose.PerCell / 2 + (long)Math.Round((CellHash(coord, 2) - 0.5) * 0.6 * FinePose.PerCell);
        return new Tree(coord, x, y);
    }

    /// <summary>A célula é mata: tem árvore.</summary>
    public static bool IsWild(WorldState world, MapCell cell, ResourceType resource) => TreeAt(world, cell.Coord) is not null;

    /// <summary>A árvore mais perto ao alcance do corpo (do ponto fino ao tronco, <see cref="ReachMeters"/>); empate: menor linha, depois coluna.</summary>
    public static Tree? NearestWithinReach(WorldState world, CellCoord at, FinePose? fine)
    {
        var from = fine is { } f && f.CellX == at.X && f.CellY == at.Y ? f : new FinePose(at.X * FinePose.PerCell + 500, at.Y * FinePose.PerCell + 500, 0, 0, 0);
        double reach = ReachMeters / Math.Max(0.1, world.Players.CellM) * FinePose.PerCell; // milésimos de célula
        Tree? best = null;
        double bestD2 = double.MaxValue;
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (TreeAt(world, new CellCoord(at.X + dx, at.Y + dy)) is not { } tree) continue;
                double ex = tree.FineX - from.X, ey = tree.FineY - from.Y, d2 = ex * ex + ey * ey;
                if (d2 > reach * reach || d2 >= bestD2) continue;
                (best, bestD2) = (tree, d2);
            }
        return best;
    }

    private static Cache Known(WorldState world)
    {
        var cache = Caches.GetOrCreateValue(world);
        long hour = world.CurrentDate.TotalHours;
        if (cache.Hour == hour) return cache;
        cache.Hour = hour;
        cache.Cities = [];
        foreach (var city in world.ActiveCities())
            cache.Cities.Add(LivingWorld.Simulation.Cities.CityOccupancy
                .ResolveGrownBounds(world, city, LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds);
        return cache;
    }
}
