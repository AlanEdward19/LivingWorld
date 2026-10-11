using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Geography.Map;

/// <summary>Pathfinding mínimo entre locais: Dijkstra sobre o grid com
/// <see cref="MovementCost"/> como peso de aresta (8-vizinhança). Base de rota comercial
/// e migração — nada além do custo total do menor caminho.</summary>
public static class MapPathfinder
{
    private static readonly int[] Dx = [1, -1, 0, 0, 1, 1, -1, -1];
    private static readonly int[] Dy = [0, 0, 1, -1, 1, -1, 1, -1];

    private const int MaxCachedCosts = 250_000;

    public static Result<double> ShortestCost(WorldMap map, CellCoord start, CellCoord goal)
    {
        var cache = map.ShortestCostCache;
        if (cache.TryGetValue((start, goal), out var cached))
            return cached;

        var result = ComputeShortestCost(map, start, goal);
        if (cache.Count >= MaxCachedCosts)
            cache.Clear();
        cache[(start, goal)] = result;
        return result;
    }

    /// <summary>Existe caminho entre as células? Equivale a <c>ShortestCost(...).IsSuccess</c> — toda célula
    /// vizinha existente é transitável a custo finito, então alcançável é "mesma componente conexa" — sem
    /// rodar um Dijkstra por consulta (a origem muda a cada passo do NPC, então o memo de custo falha muito).</summary>
    public static bool IsReachable(WorldMap map, CellCoord start, CellCoord goal)
    {
        var labels = map.ComponentLabels;
        return labels.TryGetValue(start, out var from) && labels.TryGetValue(goal, out var to) && from == to;
    }

    internal static Dictionary<CellCoord, int> LabelComponents(WorldMap map)
    {
        var labels = new Dictionary<CellCoord, int>();
        int next = 0;
        var stack = new Stack<CellCoord>();
        foreach (var cell in map.Cells)
        {
            if (labels.ContainsKey(cell.Coord)) continue;
            labels[cell.Coord] = next;
            stack.Push(cell.Coord);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                for (int i = 0; i < Dx.Length; i++)
                {
                    var neighbor = new CellCoord(current.X + Dx[i], current.Y + Dy[i]);
                    if (labels.ContainsKey(neighbor) || !map.TryGetCell(neighbor, out _)) continue;
                    labels[neighbor] = next;
                    stack.Push(neighbor);
                }
            }
            next++;
        }
        return labels;
    }

    private static Result<double> ComputeShortestCost(WorldMap map, CellCoord start, CellCoord goal)
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

    /// <summary>Rota mínima incluindo origem e destino. Empates usam coordenada Y/X como
    /// prioridade estável, pois a rota passa a alimentar posição canônica por tick.</summary>
    public static Result<IReadOnlyList<CellCoord>> ShortestPath(
        WorldMap map, CellCoord start, CellCoord goal)
    {
        if (!map.TryGetCell(start, out _))
            return Result<IReadOnlyList<CellCoord>>.Fail($"start: célula {start} fora do grid");
        if (!map.TryGetCell(goal, out _))
            return Result<IReadOnlyList<CellCoord>>.Fail($"goal: célula {goal} fora do grid");
        if (start == goal) return Result<IReadOnlyList<CellCoord>>.Ok([start]);

        var best = new Dictionary<CellCoord, double> { [start] = 0 };
        var previous = new Dictionary<CellCoord, CellCoord>();
        var queue = new PriorityQueue<CellCoord, (double Cost, int Y, int X)>();
        queue.Enqueue(start, (0, start.Y, start.X));

        while (queue.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost > best.GetValueOrDefault(current, double.PositiveInfinity)) continue;
            if (current == goal) return Result<IReadOnlyList<CellCoord>>.Ok(Reconstruct(previous, start, goal));

            for (int i = 0; i < Dx.Length; i++)
            {
                var neighbor = new CellCoord(current.X + Dx[i], current.Y + Dy[i]);
                if (!map.TryGetCell(neighbor, out _)) continue;
                double candidate = priority.Cost + MovementCost.Between(map, current, neighbor);
                if (candidate >= best.GetValueOrDefault(neighbor, double.PositiveInfinity)) continue;
                best[neighbor] = candidate;
                previous[neighbor] = current;
                queue.Enqueue(neighbor, (candidate, neighbor.Y, neighbor.X));
            }
        }

        return Result<IReadOnlyList<CellCoord>>.Fail($"goal: {goal} inalcançável a partir de {start}");
    }

    private static IReadOnlyList<CellCoord> Reconstruct(
        IReadOnlyDictionary<CellCoord, CellCoord> previous, CellCoord start, CellCoord goal)
    {
        var path = new List<CellCoord> { goal };
        var current = goal;
        while (current != start)
        {
            current = previous[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
