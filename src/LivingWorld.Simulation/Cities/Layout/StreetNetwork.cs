using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;

namespace LivingWorld.Simulation.Cities.Layout;

/// <summary>Ruas de uma cidade (AD-173), função pura do mapa, do ponto de fundação e de <see cref="CityLayoutRules"/>: um largo
/// no ponto de fundação, uma rua principal que atravessa o largo no eixo com mais terra seca e travessas perpendiculares a
/// cada <see cref="CityLayoutRules.LaneSpacingCells"/> células dela. Cada rua atravessa uma célula de água só (vau) ou a
/// contorna por um passo de lado, e para no
/// fim do mapa, na água sem contorno, perto de outra rua ou no comprimento máximo. Nada é persistido: mesma entrada, mesmas
/// ruas. As células vêm em ordem de profundidade (passos desde o largo), a ordem em que a vila ocupa os lotes.</summary>
internal sealed class StreetNetwork
{
    public static readonly StreetNetwork Empty = new([], [], [], [], default, -1, 0, 0, 0);

    /// <summary>Células de rua em ordem de profundidade, desempate pela ordem em que cada rua cresceu.</summary>
    public CellCoord[] Cells { get; }

    /// <summary>Passos desde o largo (o largo é 0 no centro e 1 em volta).</summary>
    public int[] Depth { get; }

    /// <summary>Índice (em <see cref="Cells"/>) da célula de onde esta saiu; -1 na raiz.</summary>
    public int[] Parent { get; }

    /// <summary>Largo ou rua principal (oficina e mercado procuram aqui primeiro).</summary>
    public bool[] IsMain { get; }

    public HashSet<CellCoord> Set { get; }

    /// <summary>Índices de <see cref="Cells"/>: largo e rua principal primeiro, depois as travessas (cada grupo por distância ao largo).</summary>
    public int[] MainFirst { get; }

    /// <summary>Índices de <see cref="Cells"/> por distância ao largo: a ordem em que as casas ocupam a frente de rua.</summary>
    public int[] Nearest { get; }

    /// <summary>O ponto de fundação.</summary>
    public CellCoord Center { get; }

    /// <summary>Células do mapa em volta do largo (até o comprimento máximo de rua), por distância: onde procurar lote com
    /// caminho a pé até a rua.</summary>
    public CellCoord[] Around { get; }

    private readonly Dictionary<CellCoord, int> _index;

    private readonly CellCoord _center;
    private readonly int _squareRadius;

    private StreetNetwork(CellCoord[] cells, int[] depth, int[] parent, bool[] isMain, CellCoord center, int squareRadius, int maxStreetCells, int mapWidth, int mapHeight)
    {
        _center = center;
        _squareRadius = squareRadius;
        Cells = cells;
        Depth = depth;
        Parent = parent;
        IsMain = isMain;
        Set = [.. cells];
        _index = new Dictionary<CellCoord, int>(cells.Length);
        for (int i = 0; i < cells.Length; i++) _index[cells[i]] = i;
        // A vila ocupa a rua mais perto do largo primeiro (distância em linha reta, depois passos de rua).
        long Dist(int i) => (long)(cells[i].X - center.X) * (cells[i].X - center.X) + (long)(cells[i].Y - center.Y) * (cells[i].Y - center.Y);
        Nearest = Enumerable.Range(0, cells.Length).OrderBy(Dist).ThenBy(i => i).ToArray();
        MainFirst = Nearest.Where(i => isMain[i]).Concat(Nearest.Where(i => !isMain[i])).ToArray();
        Center = center;
        int radius = Math.Max(0, maxStreetCells);
        var around = new List<CellCoord>();
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
            for (int x = center.X - radius; x <= center.X + radius; x++)
                if (x >= 0 && y >= 0 && x < mapWidth && y < mapHeight) around.Add(new CellCoord(x, y));
        Around = cells.Length == 0 ? [] : [.. around.OrderBy(c => (long)(c.X - center.X) * (c.X - center.X) + (long)(c.Y - center.Y) * (c.Y - center.Y)).ThenBy(c => c.Y).ThenBy(c => c.X)];
    }

    public int Count => Cells.Length;

    public bool Contains(CellCoord cell) => Set.Contains(cell);

    public int IndexOf(CellCoord cell) => _index.TryGetValue(cell, out int i) ? i : -1;

    /// <summary>A célula pertence ao largo (o espaço aberto em volta do ponto de fundação).</summary>
    public bool InSquare(int index) =>
        Math.Max(Math.Abs(Cells[index].X - _center.X), Math.Abs(Cells[index].Y - _center.Y)) <= _squareRadius;

    public static StreetNetwork Build(WorldMap map, CellCoord center, CityLayoutRules rules)
    {
        if (!rules.Enabled) return Empty;
        var eastWest = new Builder(map, center, rules, horizontal: true).Build();
        var northSouth = new Builder(map, center, rules, horizontal: false).Build();
        if (eastWest.Count != northSouth.Count) return eastWest.Count > northSouth.Count ? eastWest : northSouth;
        ulong h = StableHash.Mix(((long)center.X << 32) ^ (uint)center.Y);
        return h % 2 == 0 ? eastWest : northSouth;
    }

    private sealed class Builder(WorldMap map, CellCoord center, CityLayoutRules rules, bool horizontal)
    {
        private readonly List<CellCoord> _cells = [];
        private readonly List<int> _parent = [];
        private readonly List<int> _depth = [];
        private readonly List<bool> _main = [];
        private readonly Dictionary<CellCoord, int> _index = [];

        public StreetNetwork Build()
        {
            int r = Math.Max(0, rules.SquareRadiusCells);
            int centerIndex = Land(center) ? Add(center, -1, main: true) : -1;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var cell = new CellCoord(center.X + dx, center.Y + dy);
                    if (cell != center && Land(cell)) Add(cell, centerIndex, main: true);
                }

            var axis = horizontal ? new[] { new CellCoord(1, 0), new CellCoord(-1, 0) } : [new CellCoord(0, 1), new CellCoord(0, -1)];
            var junctions = new List<(int Index, CellCoord Direction)>();
            int spacing = Math.Max(3, rules.LaneSpacingCells);
            foreach (var d in axis)
            {
                // A rua principal sai da borda do largo (ou do ponto mais perto do centro que for terra).
                int from = -1;
                var at = center;
                for (int k = r; k >= 0 && from < 0; k--)
                {
                    at = new CellCoord(center.X + d.X * k, center.Y + d.Y * k);
                    from = _index.GetValueOrDefault(at, -1);
                }
                if (from < 0) at = center;
                int lane = 1;
                Grow(at, from, d, rules.MaxStreetCells, main: true, onStep: (index, length) =>
                {
                    if (length != lane * spacing + Jitter(lane, d)) return;
                    junctions.Add((index, d));
                    lane++;
                });
            }

            // Travessas da principal e, delas, vielas de segunda ordem no mesmo espaçamento (a malha fica mais fechada perto
            // do largo, onde a vila precisa de frente de rua; cada rua para ao encostar em outra).
            var secondOrder = new List<(int Index, CellCoord Direction)>();
            foreach (var (index, d) in junctions)
            {
                var perpendicular = new CellCoord(d.Y, d.X);
                foreach (var side in new[] { perpendicular, new CellCoord(-perpendicular.X, -perpendicular.Y) })
                {
                    int lane = 1;
                    Grow(_cells[index], index, side, rules.MaxStreetCells, main: false, onStep: (at, length) =>
                    {
                        if (length != lane * spacing + Jitter(lane + 100, side)) return;
                        secondOrder.Add((at, side));
                        lane++;
                    });
                }
            }
            foreach (var (index, d) in secondOrder)
            {
                var perpendicular = new CellCoord(d.Y, d.X);
                foreach (var side in new[] { perpendicular, new CellCoord(-perpendicular.X, -perpendicular.Y) })
                    Grow(_cells[index], index, side, rules.MaxStreetCells, main: false, onStep: null);
            }

            var order = Enumerable.Range(0, _cells.Count).OrderBy(i => _depth[i]).ThenBy(i => i).ToArray();
            var newIndex = new int[order.Length];
            for (int i = 0; i < order.Length; i++) newIndex[order[i]] = i;
            return new StreetNetwork(
                order.Select(i => _cells[i]).ToArray(),
                order.Select(i => _depth[i]).ToArray(),
                order.Select(i => _parent[i] < 0 ? -1 : newIndex[_parent[i]]).ToArray(),
                order.Select(i => _main[i]).ToArray(), center, r, rules.MaxStreetCells, map.Width, map.Height);
        }

        /// <summary>-1, 0 ou +1 célula na posição da travessa: a vila não sai em grade.</summary>
        private int Jitter(int lane, CellCoord direction)
        {
            ulong h = StableHash.Mix(((long)center.X * 73856093) ^ ((long)center.Y * 19349663) ^ ((long)lane * 83492791) ^ (direction.X * 3 + direction.Y));
            return (int)(h % 3) - 1;
        }

        private void Grow(CellCoord at, int parent, CellCoord d, int maxLength, bool main, Action<int, int>? onStep)
        {
            int length = 0;
            while (length < maxLength)
            {
                var next = new CellCoord(at.X + d.X, at.Y + d.Y);
                if (CanTake(next, at))
                {
                    parent = Add(next, parent, main);
                    at = next;
                    onStep?.Invoke(parent, ++length);
                    continue;
                }

                // Vau: uma célula de água só, com terra livre logo depois, a rua atravessa (poça, regato estreito).
                var beyond = new CellCoord(next.X + d.X, next.Y + d.Y);
                if (InMap(next) && !Land(next) && !_index.ContainsKey(next) && CanTake(beyond, next))
                {
                    parent = Add(next, parent, main);
                    onStep?.Invoke(parent, ++length);
                    parent = Add(beyond, parent, main);
                    onStep?.Invoke(parent, ++length);
                    at = beyond;
                    continue;
                }

                // Contorna a água (ou o fim de outra rua) com um passo de lado, se o lado e a frente dele forem terra.
                bool stepped = false;
                var left = new CellCoord(-d.Y, d.X);
                var right = new CellCoord(d.Y, -d.X);
                bool leftFirst = (StableHash.Mix(((long)at.X << 32) ^ (uint)at.Y) & 1) == 0;
                foreach (var side in leftFirst ? new[] { left, right } : [right, left])
                {
                    var aside = new CellCoord(at.X + side.X, at.Y + side.Y);
                    var ahead = new CellCoord(aside.X + d.X, aside.Y + d.Y);
                    if (!CanTake(aside, at) || !Land(ahead) || _index.ContainsKey(ahead) || TouchesOther(ahead, aside, at)) continue;
                    parent = Add(aside, parent, main);
                    onStep?.Invoke(parent, ++length);
                    parent = Add(ahead, parent, main);
                    onStep?.Invoke(parent, ++length);
                    at = ahead;
                    stepped = true;
                    break;
                }
                if (!stepped) return;
            }
        }

        private bool CanTake(CellCoord cell, CellCoord from) => Land(cell) && !_index.ContainsKey(cell) && !TouchesOther(cell, from, from);

        /// <summary>Encosta (4 vizinhos) em alguma rua que não seja <paramref name="from"/> nem <paramref name="also"/>.</summary>
        private bool TouchesOther(CellCoord cell, CellCoord from, CellCoord also)
        {
            foreach (var n in (ReadOnlySpan<CellCoord>)[new(cell.X + 1, cell.Y), new(cell.X - 1, cell.Y), new(cell.X, cell.Y + 1), new(cell.X, cell.Y - 1)])
                if (n != from && n != also && _index.ContainsKey(n)) return true;
            return false;
        }

        private bool InMap(CellCoord cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < map.Width && cell.Y < map.Height;

        private bool Land(CellCoord cell) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < map.Width && cell.Y < map.Height && map.TryGetCell(cell, out var m) && !m.HasWater;

        private int Add(CellCoord cell, int parent, bool main)
        {
            _cells.Add(cell);
            _parent.Add(parent);
            _depth.Add(parent < 0 ? 0 : _depth[parent] + 1);
            _main.Add(main);
            _index[cell] = _cells.Count - 1;
            return _cells.Count - 1;
        }
    }
}
