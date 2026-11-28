using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

public static partial class PlayerWalk
{
    /// <summary>Resultado de <see cref="Approach"/>: a pose depois do trecho, se chegou ao ponto e se o caminho está barrado (sem rota, ou gente na frente).</summary>
    public readonly record struct Approached(FinePose Pose, bool Arrived, bool Blocked);

    /// <summary>#255: o motor leva o corpo até o ponto fino (<paramref name="toX"/>, <paramref name="toY"/>, milésimos de célula) por <paramref name="elapsedMs"/> de mundo: o
    /// caminho é uma busca em largura pela MESMA regra do chão do passo do jogador (<see cref="PlayerGround.TerrainRefusal"/>: parede em aresta, porta, canto),
    /// então passa pelas portas e nunca atravessa parede; o corpo anda do centro de uma célula ao da seguinte, à velocidade real do corpo
    /// (<see cref="SpeedMps"/>), e cada troca de célula passa por <see cref="PlayerGround.Step"/> como no andar do jogador. Determinístico: inteiros em milésimos e
    /// o tempo do mundo; a rota se refaz a cada trecho (a casa tem poucas células).</summary>
    public static Approached Approach(WorldState world, TickContext ctx, Npc npc, FinePose pose, long toX, long toY, long elapsedMs)
    {
        var goal = new CellCoord((int)Math.Floor(toX / (double)FinePose.PerCell), (int)Math.Floor(toY / (double)FinePose.PerCell));
        long x = pose.X, y = pose.Y, remaining = Math.Max(0, elapsedMs);
        int floor = PlayerGround.FloorOf(npc);
        var cache = new Dictionary<CellCoord, PlayerGround.Spot?>();
        PlayerGround.Spot? At(CellCoord c)
        {
            if (!cache.TryGetValue(c, out var spot)) cache[c] = spot = PlayerGround.At(world, c, floor);
            return spot;
        }

        for (int guard = 0; guard < 64; guard++)
        {
            var cell = new CellCoord((int)Math.Floor(x / (double)FinePose.PerCell), (int)Math.Floor(y / (double)FinePose.PerCell));
            if (cell == goal && x == toX && y == toY) return new Approached(pose with { X = x, Y = y, HeadingX = 0, HeadingY = 0 }, true, false);
            if (remaining <= 0) return new Approached(pose with { X = x, Y = y }, false, false);
            long wx, wy;
            CellCoord toward;
            if (cell == goal)
            {
                (wx, wy, toward) = (toX, toY, goal);
            }
            else
            {
                var route = Route(world, npc, floor, cell, goal, At);
                if (route is null) return new Approached(pose with { X = x, Y = y }, false, true);
                toward = route[0];
                // o ponto onde a rota cruza para a próxima célula: o meio da aresta (passo reto) ou o canto (diagonal). Dentro da célula atual a reta até ele nunca
                // sai dela; para entrar na vizinha de menor coordenada o ponto fica um milésimo para dentro dela
                int sx = toward.X - cell.X, sy = toward.Y - cell.Y;
                wx = sx > 0 ? toward.X * FinePose.PerCell : sx < 0 ? cell.X * FinePose.PerCell - 1 : x;
                wy = sy > 0 ? toward.Y * FinePose.PerCell : sy < 0 ? cell.Y * FinePose.PerCell - 1 : y;
                if (sx == 0) wx = cell.X * FinePose.PerCell + 500;
                if (sy == 0) wy = cell.Y * FinePose.PerCell + 500;
            }

            long dx = wx - x, dy = wy - y;
            double dist = Math.Sqrt((double)dx * dx + (double)dy * dy);
            double perMs = SpeedMps(world, npc, cell, toward) / world.Players.CellM * FinePose.PerCell / 1000.0; // milésimos de célula por ms
            double need = dist / Math.Max(perMs, 1e-9);
            bool finishes = need <= remaining;
            long nx = finishes ? wx : x + (long)Math.Round(dx * perMs * remaining / dist);
            long ny = finishes ? wy : y + (long)Math.Round(dy * perMs * remaining / dist);
            if (!TryEnter(world, ctx, npc, ref x, ref y, nx, ny)
                && !TryEnter(world, ctx, npc, ref x, ref y, nx, y)
                && !TryEnter(world, ctx, npc, ref x, ref y, x, ny))
                return new Approached(pose with { X = x, Y = y }, false, true);
            int hx = (int)Math.Round(dx * 1000 / dist), hy = (int)Math.Round(dy * 1000 / dist);
            pose = pose with { HeadingX = hx, HeadingY = hy };
            remaining = finishes ? remaining - (long)Math.Ceiling(need) : 0;
        }

        return new Approached(pose with { X = x, Y = y }, false, false);
    }

    /// <summary>Busca em largura de <paramref name="from"/> a <paramref name="to"/> pela regra do chão; devolve as células depois de <paramref name="from"/> até
    /// <paramref name="to"/>, ou nulo sem caminho. Vizinhas em ordem fixa (determinismo).</summary>
    internal static List<CellCoord>? Route(WorldState world, Npc npc, int floor, CellCoord from, CellCoord to, Func<CellCoord, PlayerGround.Spot?> at)
    {
        var own = npc.Interior?.Building;
        var previous = new Dictionary<CellCoord, CellCoord> { [from] = from };
        var queue = new Queue<CellCoord>();
        queue.Enqueue(from);
        while (queue.Count > 0 && previous.Count < 4000)
        {
            var cell = queue.Dequeue();
            if (cell == to) break;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var next = new CellCoord(cell.X + dx, cell.Y + dy);
                    if (previous.ContainsKey(next) || PlayerGround.TerrainRefusal(world, floor, own, cell, next, at) is not null) continue;
                    previous[next] = cell;
                    queue.Enqueue(next);
                }
        }

        if (!previous.ContainsKey(to)) return null;
        var path = new List<CellCoord>();
        for (var c = to; c != from; c = previous[c]) path.Add(c);
        path.Reverse();
        return path;
    }
}
