using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>#264: a obra do morador aplicada por cima da planta e dos cômodos do planejador (<see cref="HomeEdit"/>), em funções puras: células de piso novas e as paredes de
/// fora refeitas; paredes e portas de dentro levantadas ou derrubadas; os cômodos de novo como regiões ligadas por aresta aberta (cada um fica com o tipo do cômodo de
/// origem em que mais tem células). Sem edição devolve o que recebeu (mesma instância).</summary>
public static class HomeEditor
{
    public static readonly CellCoord[] Around = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    /// <summary>A planta com as células novas (piso) e as paredes de fora refeitas em volta delas; a porta da rua e o resto ficam como estavam.</summary>
    public static BuildingLayout ApplyLayout(BuildingLayout layout, HomeEdit? edit, BuildingMaterial wall)
    {
        if (edit is null || edit.Cells.Count == 0) return layout;
        var floors = new List<FloorLayout>(layout.FloorLayouts.Count);
        foreach (var floor in layout.FloorLayouts)
        {
            var cells = floor.Cells.ToList();
            var have = cells.Select(c => c.Cell).ToHashSet();
            foreach (var cell in edit.Cells)
                if (have.Add(cell)) cells.Add(new FootprintCell(cell, BuildingMaterial.Floor));
            var doorEdges = (floor.Doors ?? []).Select(d => d.Edge).ToHashSet();
            var material = floor.Walls is { Count: > 0 } existing ? existing[0].Material : wall;
            var walls = new List<WallSegment>();
            foreach (var cell in cells)
                foreach (var d in Around)
                {
                    var outside = new CellCoord(cell.Cell.X + d.X, cell.Cell.Y + d.Y);
                    if (have.Contains(outside)) continue;
                    var edge = WallEdge.Between(cell.Cell, outside);
                    if (!doorEdges.Contains(edge)) walls.Add(new WallSegment(edge.A, edge.B, material));
                }

            floors.Add(new FloorLayout(floor.Level, cells, walls, floor.Doors));
        }

        return new BuildingLayout(layout.Floors, floors);
    }

    /// <summary>Os cômodos do andar depois da obra: <paramref name="baseRooms"/> (o planejador na planta de antes) com as células novas e as arestas editadas.</summary>
    public static FloorRooms ApplyRooms(FloorRooms baseRooms, FloorLayout layout, HomeEdit? edit, int floor, BuildingMaterial wall)
    {
        if (edit is null || (edit.Cells.Count == 0 && !edit.Edges.Any(e => e.Floor == floor))) return baseRooms;
        var cells = layout.Cells.Select(c => c.Cell).ToHashSet();
        var walls = baseRooms.Walls.Select(w => w.Edge).ToHashSet();
        var doors = baseRooms.Doors.Select(w => w.Edge).ToHashSet();
        foreach (var e in edit.Edges)
        {
            if (e.Floor != floor) continue;
            var edge = e.Edge;
            if (!cells.Contains(edge.A) || !cells.Contains(edge.B)) continue;
            walls.Remove(edge);
            doors.Remove(edge);
            if (e.State == "wall") walls.Add(edge);
            else if (e.State == "door") doors.Add(edge);
        }

        var baseRoomOf = new Dictionary<CellCoord, int>();
        for (int i = 0; i < baseRooms.Rooms.Count; i++)
            foreach (var cell in baseRooms.Rooms[i].Cells) baseRoomOf[cell] = i;

        var seen = new HashSet<CellCoord>();
        var parts = new List<(List<CellCoord> Cells, int Origin)>();
        foreach (var start in cells.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            if (!seen.Add(start)) continue;
            var part = new List<CellCoord> { start };
            var queue = new Queue<CellCoord>([start]);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Around)
                {
                    var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                    var edge = WallEdge.Between(cell, next);
                    if (cells.Contains(next) && !walls.Contains(edge) && !doors.Contains(edge) && seen.Add(next)) { part.Add(next); queue.Enqueue(next); }
                }
            }

            part.Sort(static (a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            parts.Add((part, OriginOf(part, baseRoomOf)));
        }

        var rooms = parts.OrderBy(p => p.Origin).ThenBy(p => p.Cells[0].Y).ThenBy(p => p.Cells[0].X)
            .Select(p => new Room(p.Origin >= 0 && p.Origin < baseRooms.Rooms.Count ? baseRooms.Rooms[p.Origin].Kind : baseRooms.Rooms.Count > 0 ? baseRooms.Rooms[0].Kind : null, p.Cells))
            .ToArray();
        return new FloorRooms(rooms, Segments(walls, wall), Segments(doors, BuildingMaterial.Door));
    }

    /// <summary>O cômodo de origem de uma região: o da planta de antes em que ela mais tem células; sem nenhuma (só células novas), o do vizinho de fora mais antigo.</summary>
    private static int OriginOf(List<CellCoord> part, Dictionary<CellCoord, int> baseRoomOf)
    {
        var tally = new Dictionary<int, int>();
        foreach (var cell in part)
            if (baseRoomOf.TryGetValue(cell, out int room)) tally[room] = tally.GetValueOrDefault(room) + 1;
        if (tally.Count > 0) return tally.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        foreach (var cell in part)
            foreach (var d in Around)
                if (baseRoomOf.TryGetValue(new CellCoord(cell.X + d.X, cell.Y + d.Y), out int room)) return room;
        return 0;
    }

    private static WallSegment[] Segments(HashSet<WallEdge> edges, BuildingMaterial material) =>
        [.. edges.OrderBy(e => e.A.Y).ThenBy(e => e.A.X).ThenBy(e => e.B.Y).ThenBy(e => e.B.X).Select(e => new WallSegment(e.A, e.B, material))];

    /// <summary>As células de passagem do andar: a de entrada (à frente da porta da rua) e as duas de cada porta de dentro; nenhuma peça fica nelas.</summary>
    public static HashSet<CellCoord> Doorways(FloorRooms rooms, CellCoord? entry)
    {
        var cells = new HashSet<CellCoord>();
        if (entry is { } e) cells.Add(e);
        foreach (var door in rooms.Doors) { cells.Add(door.A); cells.Add(door.B); }
        return cells;
    }

    /// <summary>Toda célula andável que sobra continua ligada à entrada (porta no térreo, escada nos andares de cima) por aresta aberta, com <paramref name="blocked"/> ocupadas
    /// por peças: nunca prende ninguém.</summary>
    public static bool AllConnected(FloorLayout layout, FloorRooms rooms, IReadOnlySet<CellCoord> blocked)
    {
        var materials = layout.Cells.ToDictionary(c => c.Cell, c => c.Material);
        var walkable = materials.Where(kv => InteriorWalkability.IsWalkable(kv.Value)).Select(kv => kv.Key).ToHashSet();
        if (FurniturePlanner.StartOf(materials, layout.Level) is not { } start) return true;
        return FurniturePlanner.AllReachable(walkable, blocked.ToHashSet(), start, FurniturePlanner.WallsOf(layout, rooms));
    }

    /// <summary>O estado da aresta (vizinhas de 4): <c>outer</c> (parede ou porta de fora), <c>wall</c>, <c>door</c>, <c>open</c> ou <c>none</c> (as duas pontas fora da planta).</summary>
    public static string EdgeState(FloorLayout layout, FloorRooms rooms, WallEdge edge)
    {
        var cells = layout.Cells.Select(c => c.Cell).ToHashSet();
        bool a = cells.Contains(edge.A), b = cells.Contains(edge.B);
        if (!a && !b) return "none";
        if (a != b) return "outer";
        if (rooms.Doors.Any(d => d.Edge == edge)) return "door";
        return rooms.Walls.Any(w => w.Edge == edge) ? "wall" : "open";
    }
}
