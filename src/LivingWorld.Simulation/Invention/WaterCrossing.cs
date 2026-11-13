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
    private static readonly ConditionalWeakTable<WorldMap, ConcurrentDictionary<(CellCoord, CellCoord), int>> Memo = new();
    private static readonly ConditionalWeakTable<WorldMap, StrongBox<bool>> AnyWater = new();

    public static int CellsBetween(WorldMap map, CellCoord origin, CellCoord destination)
    {
        if (origin == destination) return 0;
        if (!AnyWater.GetValue(map, static m => new StrongBox<bool>(m.Cells.Any(c => c.HasWater))).Value) return 0;
        var byPair = Memo.GetOrCreateValue(map);
        if (byPair.TryGetValue((origin, destination), out int count)) return count;
        count = Count(map, origin, destination);
        if (byPair.Count >= MaxCached) byPair.Clear();
        byPair[(origin, destination)] = count;
        return count;
    }

    /// <summary>Supercobertura da reta (Bresenham generalizado): toda célula que o segmento toca, uma vez cada.</summary>
    private static int Count(WorldMap map, CellCoord a, CellCoord b)
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
            if (map.TryGetCell(new CellCoord(x, y), out var cell) && cell.HasWater) count++;
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
