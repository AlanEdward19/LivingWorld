using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Invention;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Invention;

/// <summary>Água no caminho (AD-141): células com <see cref="MapCell.HasWater"/> que a reta entre origem e destino atravessa (sem
/// contar as pontas, onde a pessoa já está ou chega). A pé, cada uma custa achar o vau, contornar ou vadear; com a embarcação da
/// casa, custa remar a célula na velocidade medida pelo avaliador, mais pôr e tirar da água. Contagem memorizada por par de
/// células (função pura do mapa, volátil).</summary>
public static class WaterCrossing
{
    private const int MaxCached = 250_000;

    /// <summary>Estado volátil por mapa (AD-143, caminho quente de toda viagem): grade densa de água, soma acumulada 2D (diz em O(1)
    /// que o retângulo do trajeto não tem água nenhuma, o caso comum) e a contagem por par (chave = as quatro coordenadas num long).</summary>
    private sealed class MapWater
    {
        public readonly WorldMap Map;
        public readonly bool Any;
        public readonly int Width, Height;
        public readonly bool[] Water;
        public readonly int[] Prefix; // (Width+1) × (Height+1)
        public readonly ConcurrentDictionary<long, int> Pairs = new();

        public MapWater(WorldMap map)
        {
            Map = map;
            Width = map.Width;
            Height = map.Height;
            Water = new bool[Width * Height];
            foreach (var c in map.Cells)
                if (c.HasWater && c.Coord.X >= 0 && c.Coord.X < Width && c.Coord.Y >= 0 && c.Coord.Y < Height)
                    Water[c.Coord.Y * Width + c.Coord.X] = Any = true;
            Prefix = new int[(Width + 1) * (Height + 1)];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    Prefix[(y + 1) * (Width + 1) + x + 1] = (Water[y * Width + x] ? 1 : 0)
                        + Prefix[y * (Width + 1) + x + 1] + Prefix[(y + 1) * (Width + 1) + x] - Prefix[y * (Width + 1) + x];
        }

        /// <summary>Células d'água no retângulo [x0..x1] × [y0..y1], cortado ao mapa.</summary>
        public int InRect(int x0, int y0, int x1, int y1)
        {
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(Width - 1, x1); y1 = Math.Min(Height - 1, y1);
            if (x0 > x1 || y0 > y1) return 0;
            int w = Width + 1;
            return Prefix[(y1 + 1) * w + x1 + 1] - Prefix[y0 * w + x1 + 1] - Prefix[(y1 + 1) * w + x0] + Prefix[y0 * w + x0];
        }

        public bool IsWater(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height && Water[y * Width + x];
    }

    private static readonly ConditionalWeakTable<WorldMap, MapWater> Memo = new();

    // AD-143: o caminho quente (toda viagem) evita a busca na tabela fraca quando o mapa é o mesmo da última chamada
    private static MapWater? _last;

    private static MapWater StateOf(WorldMap map)
    {
        var last = _last;
        if (last is not null && ReferenceEquals(last.Map, map)) return last;
        last = Memo.GetValue(map, static m => new MapWater(m));
        _last = last;
        return last;
    }

    public static int CellsBetween(WorldMap map, CellCoord origin, CellCoord destination)
    {
        if (origin == destination) return 0;
        var state = StateOf(map);
        if (!state.Any) return 0;
        if (state.InRect(Math.Min(origin.X, destination.X), Math.Min(origin.Y, destination.Y),
                Math.Max(origin.X, destination.X), Math.Max(origin.Y, destination.Y)) == 0) return 0;
        long key = ((long)(ushort)origin.X << 48) | ((long)(ushort)origin.Y << 32) | ((long)(ushort)destination.X << 16) | (ushort)destination.Y;
        if (state.Pairs.TryGetValue(key, out int count)) return count;
        count = Count(state, origin, destination);
        if (state.Pairs.Count >= MaxCached) state.Pairs.Clear();
        state.Pairs[key] = count;
        return count;
    }

    /// <summary>Supercobertura da reta (Bresenham generalizado): toda célula que o segmento toca, uma vez cada.</summary>
    private static int Count(MapWater state, CellCoord a, CellCoord b)
    {
        int dx = Math.Abs(b.X - a.X), dy = Math.Abs(b.Y - a.Y);
        int sx = b.X > a.X ? 1 : -1, sy = b.Y > a.Y ? 1 : -1;
        int x = a.X, y = a.Y, count = 0;
        int steps = dx + dy, error = dx - dy;
        dx *= 2; dy *= 2;
        for (int i = 1; i < steps; i++)
        {
            if (error > 0) { x += sx; error -= dy; }
            else { y += sy; error += dx; }
            if (state.IsWater(x, y)) count++;
        }
        return count;
    }

    /// <summary>Custo extra do trajeto (unidade de <c>MovementCost</c>) por causa da água, a pé ou, havendo embarcação de
    /// <paramref name="craftSpeedMmS"/> (0 = nenhuma), o menor entre ir a pé e remar (quem tem escolhe o mais curto). Remar
    /// pode sair mais barato que andar a mesma distância (negativo) se a embarcação for mais rápida que o passo.</summary>
    public static double ExtraCost(WorldMap map, InventionRules rules, int waterCells, int craftSpeedMmS)
    {
        double cell = map.Cost.Base;
        double foot = cell * waterCells * rules.FordExtraCellsPm / 1000.0;
        if (craftSpeedMmS <= 0) return foot;
        double rowedCells = waterCells * (double)rules.WalkPaceMmS / craftSpeedMmS;
        return Math.Min(foot, cell * (rowedCells - waterCells + rules.BoardingCellsPm / 1000.0));
    }

    /// <summary>Horas que a água tira a pé deste trajeto (o que vira frustração) e as que a embarcação poupa em relação a ir a pé.</summary>
    public static (double FootHours, double CraftHours) Hours(WorldState world, int waterCells, int craftSpeedMmS)
    {
        var rules = world.InventionRules!;
        double foot = ExtraCost(world.Map, rules, waterCells, 0);
        double craft = craftSpeedMmS > 0 ? ExtraCost(world.Map, rules, waterCells, craftSpeedMmS) : foot;
        return (foot, craft);
    }
}
