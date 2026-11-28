using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Objects;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Onde fica a peça: andar, célula local da planta e giro em graus em torno de Y (0, 90, 180, 270) com o fundo da peça (-z) para a parede.</summary>
public readonly record struct FurnitureSpot(int Floor, CellCoord Cell, int Facing);

/// <summary>#237/#226: posição da mobília na planta, só pela planta, pelos cômodos e uma semente (função pura, sem RNG, sem tick). Regras: só piso (nunca porta
/// nem escada); cada peça só nas células dos cômodos que ela pode ocupar (<c>allowed</c>, pelo uso declarado no catálogo; nulo = qualquer); sempre com
/// caminho: depois de posta a peça, toda célula andável que sobra continua ligada à entrada (a porta no térreo, a escada nos andares de cima) sem
/// atravessar parede e sem passar por peça, ou seja, cada cômodo segue ligado à porta. Prefere célula encostada em parede (de fora ou de cômodo) e
/// vira o fundo da peça para ela; deixa por último as células de passagem (à frente das portas). Peça que não cabe sem fechar o caminho fica sem lugar (nulo).</summary>
public static class FurniturePlanner
{
    private static readonly CellCoord[] Around = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    /// <summary>Lugar de cada uma das <paramref name="count"/> peças do andar <paramref name="floor"/>, na ordem pedida; nulo = sem lugar. Sem cômodos.</summary>
    public static IReadOnlyList<FurnitureSpot?> Plan(BuildingLayout layout, int floor, int count, long seed) =>
        Plan(layout, null, floor, count, seed, null);

    /// <summary>#266: como o plano com cômodos, mas cada peça tem a sua pegada (<paramref name="sizes"/>, largura × profundidade em células) e o giro vale para toda ela:
    /// a pegada inteira em piso, sem célula de outra peça nem passagem de porta, sem atravessar aresta de parede, com um lado livre para usar (também depois das peças seguintes),
    /// o fundo na parede quando houver lugar assim (senão em qualquer lugar) e sempre com caminho. Primeiro as peças pedidas, na ordem dada. <paramref name="near"/> (por peça, os
    /// índices de peças anteriores) faz a peça preferir as células coladas nelas, com a frente voltada para elas (a cadeira junto da mesa, de frente para ela).</summary>
    public static IReadOnlyList<FurnitureSpot?> Plan(
        BuildingLayout layout, FloorRooms? rooms, int floor, int count, long seed, IReadOnlyList<IReadOnlySet<CellCoord>?>? allowed, IReadOnlyList<(int W, int D)> sizes, IReadOnlyList<IReadOnlyList<int>?>? near = null)
    {
        var spots = new FurnitureSpot?[Math.Max(count, 0)];
        if (count <= 0 || floor < 0 || floor >= layout.FloorLayouts.Count) return spots;

        var materials = layout.FloorLayouts[floor].Cells.ToDictionary(c => c.Cell, c => c.Material);
        var walkable = materials.Where(kv => InteriorWalkability.IsWalkable(kv.Value)).Select(kv => kv.Key).ToHashSet();
        var start = StartOf(materials, floor);
        if (start is not { } from) return spots;

        var walls = WallsOf(layout.FloorLayouts[floor], rooms);
        var entry = EntryOf(materials, from, floor);
        var doorways = DoorwaysOf(rooms, entry);
        var floorCells = materials.Where(kv => kv.Value == BuildingMaterial.Floor).Select(kv => kv.Key).ToHashSet();
        var candidates = floorCells
            .OrderBy(cell => doorways.Contains(cell) ? 1 : 0)
            .ThenBy(cell => FacingOf(walls, cell) is null ? 1 : 0)
            .ThenBy(cell => StableHash.Mix(seed ^ ((long)cell.X << 24) ^ ((long)cell.Y << 8) ^ floor))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .ToList();

        var taken = new HashSet<CellCoord>();
        var placed = new List<FurnitureBody>();
        var placedOf = new Dictionary<int, FurnitureBody>();
        int[] facings = [0, 90, 270, 180];
        for (int i = 0; i < spots.Length; i++)
        {
            var only = allowed is not null && i < allowed.Count ? allowed[i] : null;
            var (w, d) = sizes is not null && i < sizes.Count ? sizes[i] : (1, 1);
            var around = new HashSet<CellCoord>();
            if (near is not null && i < near.Count && near[i] is { } wanted)
                foreach (int other in wanted)
                    if (other >= 0 && other < i && spots[other] is { } at && placedOf.TryGetValue(other, out var target))
                        foreach (var c in target.Cells)
                            foreach (var d4 in Around) around.Add(new CellCoord(c.X + d4.X, c.Y + d4.Y));
            var targets = around.Count > 0 ? placedCells(near![i]!) : null;
            (bool Close, bool Wall)[] passes = around.Count > 0 ? [(true, false), (false, true), (false, false)] : [(false, true), (false, false)];
            foreach (var (close, wall) in passes)
            {
                if (spots[i] is not null) break;
                foreach (var cell in close ? candidates.Where(around.Contains) : candidates)
                {
                    if (spots[i] is not null) break;
                    if (taken.Contains(cell) || (only is not null && !only.Contains(cell))) continue;
                    foreach (int facing in targets is null ? facings : facings.OrderBy(f => FrontTouches(cell, f, w, d, targets) ? 0 : 1).ToArray())
                    {
                        var body = new FurnitureBody(cell, w, d, facing);
                        if (only is not null && body.Cells.Any(c => !only.Contains(c))) continue;
                        if (FurniturePlacement.Refusal(body, floorCells, taken, doorways, walls, walkable, needWall: wall) is not null) continue;
                        var added = body.Cells.ToList();
                        foreach (var c in added) taken.Add(c);
                        if (AllReachable(walkable, taken, from, walls) && placedOf.All(p => FurniturePlacement.HasAccess(p.Value, BlockersFor(p.Key), walls, walkable)))
                        {
                            placed.Add(body);
                            placedOf[i] = body;
                            spots[i] = new FurnitureSpot(floor, cell, facing);
                            break;
                        }

                        foreach (var c in added) taken.Remove(c);
                    }
                }
            }
        }

        // as peças que o catálogo põe "junto de" outra (a cadeira da mesa) não tiram o acesso dela: sentam-se nela
        IReadOnlySet<CellCoord> BlockersFor(int index)
        {
            if (near is null) return taken;
            HashSet<CellCoord>? lighter = null;
            foreach (var (other, body) in placedOf)
                if (other < near.Count && near[other] is { } wants && wants.Contains(index))
                    foreach (var c in body.Cells) (lighter ??= [.. taken]).Remove(c);
            return lighter ?? taken;
        }

        HashSet<CellCoord> placedCells(IReadOnlyList<int> indices)
        {
            var cells = new HashSet<CellCoord>();
            foreach (int index in indices)
                if (placedOf.TryGetValue(index, out var body))
                    foreach (var c in body.Cells) cells.Add(c);
            return cells;
        }

        return spots;
    }

    /// <summary>A frente da peça (o lado oposto ao fundo) toca alguma das células de <paramref name="targets"/>.</summary>
    private static bool FrontTouches(CellCoord origin, int facing, int w, int d, HashSet<CellCoord> targets)
    {
        var (bx, by) = ObjectFootprint.Back(facing);
        var body = new FurnitureBody(origin, w, d, facing);
        var cells = body.Cells.ToHashSet();
        foreach (var cell in cells)
        {
            var front = new CellCoord(cell.X - bx, cell.Y - by);
            if (!cells.Contains(front) && targets.Contains(front)) return true;
        }

        return false;
    }

    /// <summary>Como <see cref="Plan(BuildingLayout, int, int, long)"/>, com as paredes de dentro de <paramref name="rooms"/> e, por peça, as células permitidas.</summary>
    public static IReadOnlyList<FurnitureSpot?> Plan(
        BuildingLayout layout, FloorRooms? rooms, int floor, int count, long seed, IReadOnlyList<IReadOnlySet<CellCoord>?>? allowed)
    {
        var spots = new FurnitureSpot?[Math.Max(count, 0)];
        if (count <= 0 || floor < 0 || floor >= layout.FloorLayouts.Count) return spots;

        var materials = layout.FloorLayouts[floor].Cells.ToDictionary(c => c.Cell, c => c.Material);
        var walkable = materials.Where(kv => InteriorWalkability.IsWalkable(kv.Value)).Select(kv => kv.Key).ToHashSet();
        var start = StartOf(materials, floor);
        if (start is not { } from) return spots;

        var walls = WallsOf(layout.FloorLayouts[floor], rooms);
        var entry = EntryOf(materials, from, floor);
        var doorways = DoorwaysOf(rooms, entry);
        var candidates = materials.Where(kv => kv.Value == BuildingMaterial.Floor)
            .Select(kv => kv.Key)
            .OrderBy(cell => doorways.Contains(cell) ? 1 : 0)
            .ThenBy(cell => FacingOf(walls, cell) is null ? 1 : 0)
            .ThenBy(cell => StableHash.Mix(seed ^ ((long)cell.X << 24) ^ ((long)cell.Y << 8) ^ floor))
            .ThenBy(cell => cell.Y).ThenBy(cell => cell.X)
            .ToList();

        var taken = new HashSet<CellCoord>();
        for (int i = 0; i < spots.Length; i++)
        {
            var only = allowed is not null && i < allowed.Count ? allowed[i] : null;
            foreach (var cell in candidates)
            {
                if (taken.Contains(cell) || (only is not null && !only.Contains(cell))) continue;
                taken.Add(cell);
                if (!AllReachable(walkable, taken, from, walls)) { taken.Remove(cell); continue; }
                spots[i] = new FurnitureSpot(floor, cell, FacingOf(walls, cell) ?? 0);
                break;
            }
        }

        return spots;
    }

    /// <summary>As arestas fechadas do andar: as paredes de fora e as de dentro (as portas ficam abertas).</summary>
    public static HashSet<WallEdge> WallsOf(FloorLayout floor, FloorRooms? rooms)
    {
        var walls = new HashSet<WallEdge>();
        if (floor.Walls is not null) foreach (var w in floor.Walls) walls.Add(w.Edge);
        if (rooms is not null) foreach (var w in rooms.Walls) walls.Add(w.Edge);
        return walls;
    }

    private static HashSet<CellCoord> DoorwaysOf(FloorRooms? rooms, CellCoord? entry)
    {
        var cells = new HashSet<CellCoord>();
        if (entry is { } e) cells.Add(e);
        if (rooms is not null)
            foreach (var door in rooms.Doors) { cells.Add(door.A); cells.Add(door.B); }
        return cells;
    }

    /// <summary>A célula de piso logo à frente da porta (térreo: a de dentro, em linha reta com a abertura) ou a escada (andares de cima), de onde se
    /// entra; nula se não há.</summary>
    public static CellCoord? EntryOf(IReadOnlyDictionary<CellCoord, BuildingMaterial> materials, CellCoord start, int floor)
    {
        if (floor > 0) return start;
        var outward = new[] { new CellCoord(0, 1), new CellCoord(1, 0), new CellCoord(0, -1), new CellCoord(-1, 0) }
            .FirstOrDefault(d => !materials.ContainsKey(new CellCoord(start.X + d.X, start.Y + d.Y)));
        var straight = new CellCoord(start.X - outward.X, start.Y - outward.Y);
        if (materials.TryGetValue(straight, out var m) && m == BuildingMaterial.Floor) return straight;
        foreach (var d in Around)
        {
            var next = new CellCoord(start.X + d.X, start.Y + d.Y);
            if (materials.TryGetValue(next, out var n) && n == BuildingMaterial.Floor) return next;
        }

        return null;
    }

    /// <summary>De onde o caminho começa: a porta no térreo; a escada nos andares de cima. Nulo se a planta não tem.</summary>
    public static CellCoord? StartOf(IReadOnlyDictionary<CellCoord, BuildingMaterial> materials, int floor)
    {
        var wanted = floor == 0 ? BuildingMaterial.Door : BuildingMaterial.Stair;
        foreach (var kv in materials.OrderBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X))
            if (kv.Value == wanted) return kv.Key;
        return null;
    }

    /// <summary>Giro (graus) que põe o fundo da peça (-z) numa parede da célula (norte, oeste, leste, sul, nessa ordem); nulo se nenhum lado tem parede.</summary>
    public static int? FacingOf(IReadOnlySet<WallEdge> walls, CellCoord cell)
    {
        (CellCoord Side, int Facing)[] sides = [(new(0, -1), 0), (new(-1, 0), 90), (new(1, 0), 270), (new(0, 1), 180)];
        foreach (var (side, facing) in sides)
            if (walls.Contains(WallEdge.Between(cell, new CellCoord(cell.X + side.X, cell.Y + side.Y))))
                return facing;
        return null;
    }

    /// <summary>Toda célula andável fora de <paramref name="blocked"/> é alcançada de <paramref name="from"/> sem passar por <paramref name="blocked"/> nem
    /// por uma aresta de <paramref name="walls"/>.</summary>
    public static bool AllReachable(HashSet<CellCoord> walkable, HashSet<CellCoord> blocked, CellCoord from, IReadOnlySet<WallEdge>? walls = null)
    {
        var seen = new HashSet<CellCoord> { from };
        var queue = new Queue<CellCoord>([from]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in Around)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (walkable.Contains(next) && !blocked.Contains(next) && (walls is null || !walls.Contains(WallEdge.Between(cell, next))) && seen.Add(next))
                    queue.Enqueue(next);
            }
        }

        return walkable.All(cell => blocked.Contains(cell) || seen.Contains(cell));
    }
}
