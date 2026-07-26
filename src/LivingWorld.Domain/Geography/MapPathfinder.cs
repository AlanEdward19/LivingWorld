namespace LivingWorld.Domain;

/// <summary>Pathfinding mínimo entre locais (task 3): Dijkstra sobre o grid com
/// <see cref="MovementCost"/> como peso de aresta (8-vizinhança). Base de rota comercial
/// (Fase 5) e migração (Fase 8) — nada além do custo total do menor caminho.</summary>
public static class MapPathfinder
{
    private static readonly int[] Dx = [1, -1, 0, 0, 1, 1, -1, -1];
    private static readonly int[] Dy = [0, 0, 1, -1, 1, -1, 1, -1];

    public static Result<double> ShortestCost(WorldMap map, CellCoord start, CellCoord goal)
    {
        if (!map.TryGetCell(start, out _)) return Result<double>.Fail($"start: célula {start} fora do grid");
        if (!map.TryGetCell(goal, out _)) return Result<double>.Fail($"goal: célula {goal} fora do grid");

        var best = new Dictionary<CellCoord, double> { [start] = 0 };
        var queue = new PriorityQueue<CellCoord, double>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (current == goal) return Result<double>.Ok(currentCost);
            if (currentCost > best.GetValueOrDefault(current, double.PositiveInfinity)) continue;

            for (int i = 0; i < Dx.Length; i++)
            {
                var neighbor = new CellCoord(current.X + Dx[i], current.Y + Dy[i]);
                if (!map.TryGetCell(neighbor, out _)) continue;

                double candidate = currentCost + MovementCost.Between(map, current, neighbor);
                if (candidate < best.GetValueOrDefault(neighbor, double.PositiveInfinity))
                {
                    best[neighbor] = candidate;
                    queue.Enqueue(neighbor, candidate);
                }
            }
        }

        return Result<double>.Fail($"goal: {goal} inalcançável a partir de {start}");
    }
}
