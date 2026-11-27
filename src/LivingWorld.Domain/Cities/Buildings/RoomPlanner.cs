using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Um cômodo: o tipo do catálogo (nulo = andar sem divisão) e as células de piso dele (coordenadas locais da planta).</summary>
public sealed record Room(string? Kind, IReadOnlyList<CellCoord> Cells);

/// <summary>Os cômodos de um andar, as paredes de dentro (nas arestas entre cômodos) e as portas de dentro (aberturas nessas arestas).</summary>
public sealed record FloorRooms(IReadOnlyList<Room> Rooms, IReadOnlyList<WallSegment> Walls, IReadOnlyList<WallSegment> Doors)
{
    public static FloorRooms Single(IReadOnlyList<CellCoord> cells) => new([new Room(null, cells)], [], []);
}

/// <summary>#226: divide um andar em cômodos com paredes de dentro e portas, só pela planta, pelo plano do catálogo e por uma semente (função pura:
/// sem RNG, sem tick). O código só conhece o algoritmo (cortes retos proporcionais ao peso de cada cômodo, cada lado conexo, o primeiro cômodo do
/// plano do lado da entrada, uma porta por ligação de uma árvore que parte da entrada); quais cômodos existem, o que cada um atende e as proporções
/// são dado do cenário (<see cref="RoomCatalog"/>). Andar pequeno demais para o plano perde cômodos do fim da lista até caber; com menos de dois,
/// fica um cômodo só.</summary>
public static class RoomPlanner
{
    private static readonly CellCoord[] Around = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    public static FloorRooms Plan(IReadOnlyList<CellCoord> floorCells, CellCoord entry, IReadOnlyList<RoomKind> kinds, BuildingMaterial wall, long seed)
    {
        var all = floorCells.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        int usable = 0, needed = 0;
        for (; usable < kinds.Count; usable++)
        {
            needed += Math.Max(kinds[usable].MinCells, 1);
            if (needed > all.Count) break;
        }

        if (usable < 2) return new FloorRooms([new Room(kinds.Count > 0 ? kinds[0].Id : null, all)], [], []);
        var chosen = kinds.Take(usable).ToList();
        List<(RoomKind Kind, List<CellCoord> Cells)>? parts = null;
        while (chosen.Count >= 2 && (parts = Split(all, entry, chosen, seed, 0)) is null) chosen.RemoveAt(chosen.Count - 1);
        if (parts is null) return new FloorRooms([new Room(kinds[0].Id, all)], [], []);

        var rooms = parts.Select(p => new Room(p.Kind.Id, p.Cells)).ToArray();
        var (walls, doors) = Partitions(rooms, entry, wall, seed);
        return new FloorRooms(rooms, walls, doors);
    }

    /// <summary>Corte reto recursivo: o primeiro grupo de cômodos do plano fica do lado da entrada, o resto do outro; o corte é o que deixa a área
    /// do lado mais perto da proporção dos pesos, sem desfazer a conexão de nenhum dos lados nem baixar do mínimo de cada cômodo. Nulo se nenhum
    /// corte serve.</summary>
    private static List<(RoomKind, List<CellCoord>)>? Split(List<CellCoord> cells, CellCoord entry, List<RoomKind> kinds, long seed, int depth)
    {
        if (kinds.Count == 1) return [(kinds[0], cells)];
        int p = (kinds.Count + 1) / 2;
        var first = kinds.Take(p).ToList();
        var rest = kinds.Skip(p).ToList();
        int minFirst = first.Sum(k => Math.Max(k.MinCells, 1)), minRest = rest.Sum(k => Math.Max(k.MinCells, 1));
        double wFirst = first.Sum(k => Math.Max(k.Weight, 0.01)), wRest = rest.Sum(k => Math.Max(k.Weight, 0.01));
        int target = (int)Math.Round(cells.Count * wFirst / (wFirst + wRest));

        int spanX = cells.Max(c => c.X) - cells.Min(c => c.X), spanY = cells.Max(c => c.Y) - cells.Min(c => c.Y);
        bool xFirst = spanX != spanY ? spanX > spanY : (StableHash.Mix(seed ^ (depth * 0x9E37L) ^ cells.Count) & 1) == 0;
        foreach (bool byX in new[] { xFirst, !xFirst })
        {
            int low = byX ? cells.Min(c => c.X) : cells.Min(c => c.Y), high = byX ? cells.Max(c => c.X) : cells.Max(c => c.Y);
            (int Cut, int Score)? best = null;
            for (int cut = low + 1; cut <= high; cut++)
            {
                var side = cells.Where(c => (byX ? c.X : c.Y) < cut).ToList();
                var other = cells.Where(c => (byX ? c.X : c.Y) >= cut).ToList();
                bool entryLow = (byX ? entry.X : entry.Y) < cut;
                var withEntry = entryLow ? side : other;
                var away = entryLow ? other : side;
                if (withEntry.Count < minFirst || away.Count < minRest || !Connected(side) || !Connected(other)) continue;
                int score = Math.Abs(withEntry.Count - target);
                if (best is null || score < best.Value.Score) best = (cut, score);
            }

            if (best is not { } chosen) continue;
            var lowSide = cells.Where(c => (byX ? c.X : c.Y) < chosen.Cut).ToList();
            var highSide = cells.Where(c => (byX ? c.X : c.Y) >= chosen.Cut).ToList();
            bool entryInLow = (byX ? entry.X : entry.Y) < chosen.Cut;
            var near = entryInLow ? lowSide : highSide;
            var far = entryInLow ? highSide : lowSide;
            var nearSet = near.ToHashSet();
            var farEntry = far.FirstOrDefault(c => Around.Any(d => nearSet.Contains(new CellCoord(c.X + d.X, c.Y + d.Y))));
            var a = Split(near, entry, first, seed, depth + 1);
            var b = Split(far, farEntry, rest, seed, depth + 1);
            if (a is null || b is null) continue;
            return [.. a, .. b];
        }

        return null;
    }

    private static bool Connected(List<CellCoord> cells)
    {
        if (cells.Count == 0) return false;
        var set = cells.ToHashSet();
        var seen = new HashSet<CellCoord> { cells[0] };
        var queue = new Queue<CellCoord>([cells[0]]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in Around)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (set.Contains(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }

        return seen.Count == cells.Count;
    }

    /// <summary>Paredes entre cômodos vizinhos, com uma porta por ligação da árvore que parte do cômodo da entrada (a porta é a aresta compartilhada de menor hash).</summary>
    private static (WallSegment[] Walls, WallSegment[] Doors) Partitions(Room[] rooms, CellCoord entry, BuildingMaterial wall, long seed)
    {
        var owner = new Dictionary<CellCoord, int>();
        for (int i = 0; i < rooms.Length; i++)
            foreach (var cell in rooms[i].Cells) owner[cell] = i;

        // aresta compartilhada entre cada par de cômodos
        var shared = new SortedDictionary<(int, int), List<WallEdge>>();
        for (int i = 0; i < rooms.Length; i++)
            foreach (var cell in rooms[i].Cells)
                foreach (var d in Around)
                {
                    var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                    if (owner.TryGetValue(next, out int j) && j > i)
                    {
                        if (!shared.TryGetValue((i, j), out var list)) shared[(i, j)] = list = [];
                        list.Add(WallEdge.Between(cell, next));
                    }
                }

        var doorOf = new Dictionary<(int, int), WallEdge>();
        var seen = new HashSet<int>();
        var queue = new Queue<int>();
        int start = owner.TryGetValue(entry, out int e) ? e : 0;
        seen.Add(start);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int room = queue.Dequeue();
            foreach (var ((a, b), edges) in shared)
            {
                int other = a == room ? b : b == room ? a : -1;
                if (other < 0 || !seen.Add(other)) continue;
                doorOf[(a, b)] = edges.OrderBy(edge => StableHash.Mix(seed ^ ((long)edge.A.X << 40) ^ ((long)edge.A.Y << 20) ^ ((long)edge.B.X << 10) ^ edge.B.Y)).First();
                queue.Enqueue(other);
            }
        }

        var walls = new List<WallSegment>();
        var doors = new List<WallSegment>();
        foreach (var (pair, edges) in shared)
            foreach (var edge in edges.OrderBy(x => x.A.Y).ThenBy(x => x.A.X).ThenBy(x => x.B.Y).ThenBy(x => x.B.X))
                if (doorOf.TryGetValue(pair, out var door) && door == edge) doors.Add(new WallSegment(edge.A, edge.B, BuildingMaterial.Door));
                else walls.Add(new WallSegment(edge.A, edge.B, wall));
        return ([.. walls], [.. doors]);
    }
}
