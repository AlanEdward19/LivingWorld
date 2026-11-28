using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;

namespace LivingWorld.Simulation.Cities.Layout;

/// <summary>O chão de uma cidade para a colocação (AD-173): plantas, passagens (da porta até a rua) e quintais já
/// tomados. Só cresce dentro de um passe ascendente por Id; derivado, nunca persistido.</summary>
internal sealed class CityGround
{
    public HashSet<CellCoord> Occupied { get; } = [];
    public HashSet<CellCoord> Passages { get; } = [];
    public HashSet<CellCoord> Crofts { get; } = [];

    /// <summary>AD-192 (08d 166, 182): onde o lote novo não pode cair nem dar a porta (o caminho pisado, o chão de outra cidade); nulo
    /// no chão de sempre. Só no chão da busca de quem grava a posição, nunca no plano das posições derivadas.</summary>
    public LotBar? Bar { get; private init; }

    /// <summary>AD-192: o mesmo chão (sem cópia) com o que barra o lote novo.</summary>
    public CityGround WithBar(LotBar bar) => new(Occupied, Passages, Crofts) { Bar = bar };

    public CityGround() { }

    private CityGround(HashSet<CellCoord> occupied, HashSet<CellCoord> passages, HashSet<CellCoord> crofts)
    {
        Occupied = occupied;
        Passages = passages;
        Crofts = crofts;
    }

    /// <summary>Planta, passagem e quintal de um prédio já posicionado. A passagem é a reta da porta até a rua; sem rua à
    /// frente, o caminho mais curto (até <see cref="CityLayoutRules.PathCells"/> passos) da soleira até a rua; sem nenhum, só
    /// a soleira fica livre.</summary>
    public void Add(IReadOnlyList<FootprintCell> footprint, CellCoord origin, int croftCells, StreetNetwork network, WorldMap map, CityLayoutRules rules)
    {
        foreach (var c in footprint) Occupied.Add(new CellCoord(c.Cell.X + origin.X, c.Cell.Y + origin.Y));
        if (network.Count == 0) return;

        var (door, facing) = LotGeometry.DoorOf(footprint);
        var cells = footprint.Select(c => c.Cell).ToArray();
        var croft = croftCells <= 0 ? [] : LotGeometry.CroftOf(cells, cells.ToHashSet(), facing, croftCells)
            .Select(b => new CellCoord(b.X + origin.X, b.Y + origin.Y))
            .Where(abs => !network.Contains(abs) && abs.X >= 0 && abs.Y >= 0 && abs.X < map.Width && abs.Y < map.Height)
            .ToArray();

        var run = new List<CellCoord>();
        bool reached = false;
        for (int j = 1; j <= rules.MaxFrontYardCells() + 1; j++)
        {
            var cell = new CellCoord(origin.X + door.X + facing.X * j, origin.Y + door.Y + facing.Y * j);
            if (network.Contains(cell)) { reached = true; break; }
            run.Add(cell);
        }
        if (reached) Passages.UnionWith(run);
        else if (LotPlanner.PathToStreet(this, network, map, run[0], rules.PathCells, croft.ToHashSet()) is { } path) Passages.UnionWith(path);
        else Passages.Add(run[0]); // sem rua nem caminho, ao menos a soleira fica livre

        Crofts.UnionWith(croft);
    }
}

/// <summary>AD-192 (08d 166, 182): o que barra o lote novo, lido na hora (sem copiar células): o chão pisado (trilha ou mais) e os
/// limites das outras cidades.</summary>
internal sealed class LotBar(LivingWorld.Simulation.Cities.Streets.GroundLedger? trodden, LivingWorld.Domain.Cities.Spatial.CityBounds[] neighbours,
    LivingWorld.Domain.Cities.Spatial.CityBounds? reach = null)
{
    public bool InNeighbour(CellCoord cell)
    {
        foreach (var box in neighbours)
            if (box.Contains(cell)) return true;
        return false;
    }

    public bool Covers(CellCoord cell) =>
        (trodden is not null && trodden.SurfaceAt(cell) != LivingWorld.Simulation.Cities.Streets.GroundSurface.None) || InNeighbour(cell)
        || (reach is { } r && !r.Contains(cell));
}

/// <summary>Quem constrói e o que pode pagar (AD-173). <see cref="Dwelling"/> falso: oficina, mercado, celeiro.</summary>
internal readonly record struct LotProfile(bool Dwelling, bool Crafts, int Members, double MeansPerHead, double MedianMeansPerHead)
{
    public static readonly LotProfile Workshop = new(false, true, 0, 0, 0);

    /// <summary>Casa erguida pela cidade, sem família conhecida ainda: lote de lavrador, sem meios declarados.</summary>
    public static readonly LotProfile UnknownHousehold = new(true, false, 0, 0, 0);
}

/// <summary>O lote que a pessoa escolhe num lugar: quintal da frente, recuo dos vizinhos e quintal de trás.</summary>
internal readonly record struct LotTerms(int FrontYard, int Setback, int Croft)
{
    /// <summary>A escolha (AD-173): oficina abre na rua, sem quintal; pobre ergue casebre encostado, porta na rua; artesão
    /// no centro divide parede e abre na rua; lavrador tem quintal da frente e croft, maior se a família é grande; no centro
    /// (terra cara) só quem tem meios faz quintal.</summary>
    public static LotTerms Choose(CityLayoutRules rules, LotProfile profile, bool central)
    {
        int sidewalk = Math.Clamp(rules.SidewalkCells, 0, rules.MaxFrontYardCells()); // #258: a calçada entre a porta e a rua, o que o cenário declara
        if (!profile.Dwelling) return new LotTerms(sidewalk, rules.SideSetbackCells, 0);
        double median = profile.MedianMeansPerHead;
        bool poor = median > 0 && profile.MeansPerHead < median * rules.PoorMeansFraction;
        bool rich = median > 0 && profile.MeansPerHead > median * rules.RichMeansFactor;
        if (poor) return new LotTerms(sidewalk, 0, 0);
        int croft = profile.Crafts
            ? rules.ArtisanCroftCells
            : rules.FarmerCroftCells + (rules.LargeFamilyMembers > 0 && profile.Members >= rules.LargeFamilyMembers ? 1 : 0);
        if (central && !rich) croft = 0;
        int front = Math.Max(sidewalk, profile.Crafts ? 0 : rules.FrontYardCells);
        int setback = central && profile.Crafts && !rich ? 0 : rules.SideSetbackCells;
        return new LotTerms(front, setback, croft);
    }

    /// <summary>Degraus da rua: 0 = o lote escolhido; 1 = sem quintal de trás; 2 = parede-meia, ainda de frente para a rua.</summary>
    public LotTerms AtLevel(int level) => level switch
    {
        0 => this,
        1 => this with { Croft = 0 },
        _ => this with { Croft = 0, Setback = 0 },
    };
}

/// <summary>Busca de lote ao longo das ruas (AD-173): percorre as células de rua em ordem de profundidade (a vila cresce do
/// largo para fora) e, para cada uma, a origem que põe a porta da planta de frente para ela, com o quintal da frente
/// escolhido. Determinística; <paramref name="start"/>/<c>found</c> são o índice do candidato, para os marcos do passe.</summary>
internal static class LotPlanner
{
    /// <summary>Uma planta possível do prédio (orientação e células). Quem constrói escolhe a orientação que põe a porta para
    /// a rua; prédio derivado (sem posição gravada) só tem a orientação que a identidade dá.</summary>
    public readonly record struct Option(int Orientation, IReadOnlyList<FootprintCell> Footprint);

    public static (CellCoord Origin, int Croft, int Orientation)? Find(
        CityGround ground, StreetNetwork network, WorldMap map, CityLayoutRules rules, IReadOnlyList<Option> options,
        LotProfile profile, int level, int start, out int found, int[]? order = null)
    {
        int perCell = rules.MaxFrontYardCells() + 1;
        int perStreet = perCell * options.Count;
        order ??= profile.Dwelling ? network.Nearest : network.MainFirst; // AD-184: quem escolhe pode olhar a partir de outro lugar
        int total = network.Count * perStreet;
        var central = LotTerms.Choose(rules, profile, central: true).AtLevel(level);
        var outer = LotTerms.Choose(rules, profile, central: false).AtLevel(level);
        var shapes = new LotShape?[options.Count * 2];
        for (int i = Math.Max(0, start); i < total; i++)
        {
            int streetIndex = order[i / perStreet];
            int option = i % perStreet / perCell;
            bool isCentral = network.Depth[streetIndex] <= rules.CentralDepthCells;
            var terms = isCentral ? central : outer;
            int k = perCell - i % perCell; // passos da porta até a rua: o quintal da frente primeiro
            if (k > terms.FrontYard + 1) continue;
            ref var slot = ref shapes[option * 2 + (isCentral ? 0 : 1)];
            var shape = slot ??= LotShape.Of(options[option].Footprint, terms.Setback, terms.Croft);
            var street = network.Cells[streetIndex];
            if (ground.Bar is { } bar && bar.InNeighbour(street)) continue; // AD-192: o traçado cortado no chão da vizinha
            var origin = new CellCoord(street.X - shape.Facing.X * k - shape.Door.X, street.Y - shape.Facing.Y * k - shape.Door.Y);
            if (Fits(ground, network, map, shape, origin, k, level))
            {
                found = i;
                return (origin, terms.Croft, options[option].Orientation);
            }
        }
        found = total;
        return null;
    }

    /// <summary>Caminho a pé (AD-173) da soleira <paramref name="doorstep"/> até a primeira célula de rua, por terra seca livre
    /// (sem planta, quintal nem passagem de outro prédio, nem <paramref name="own"/>), em até <paramref name="maxSteps"/> células;
    /// as células do caminho, sem a rua. Busca em largura com vizinhos em ordem fixa: determinístico. O chão só cresce, então
    /// um lote sem caminho continua sem caminho (os marcos do passe valem).</summary>
    public static List<CellCoord>? PathToStreet(
        CityGround ground, StreetNetwork network, WorldMap map, CellCoord doorstep, int maxSteps, HashSet<CellCoord> own)
    {
        if (!Free(ground, map, doorstep, own)) return null;
        if (network.Contains(doorstep)) return [];
        if (maxSteps <= 0) return null;
        var parent = new Dictionary<CellCoord, CellCoord> { [doorstep] = doorstep };
        var frontier = new List<CellCoord> { doorstep };
        for (int step = 0; step < maxSteps && frontier.Count > 0; step++)
        {
            var next = new List<CellCoord>();
            foreach (var cell in frontier)
                foreach (var n in (ReadOnlySpan<CellCoord>)[new(cell.X, cell.Y + 1), new(cell.X + 1, cell.Y), new(cell.X, cell.Y - 1), new(cell.X - 1, cell.Y)])
                {
                    if (parent.ContainsKey(n)) continue;
                    if (network.Contains(n))
                    {
                        var path = new List<CellCoord>();
                        for (var at = cell; ; at = parent[at])
                        {
                            path.Add(at);
                            if (at == doorstep) break;
                        }
                        path.Reverse();
                        return path;
                    }
                    if (!Free(ground, map, n, own)) continue;
                    parent[n] = cell;
                    next.Add(n);
                }
            frontier = next;
        }
        return null;
    }

    private static bool Free(CityGround ground, WorldMap map, CellCoord cell, HashSet<CellCoord> own) =>
        Dry(map, cell) && !ground.Occupied.Contains(cell) && !ground.Crofts.Contains(cell) && !ground.Passages.Contains(cell) && !own.Contains(cell);

    /// <summary>Lote fora da frente de rua (AD-173): a casa mais perto do largo cuja soleira chega à rua por um caminho a pé
    /// curto. Varre as células em volta do largo por distância; mesmo índice de candidato para os marcos.</summary>
    public static (CellCoord Origin, int Croft, int Orientation)? FindViaPath(
        CityGround ground, StreetNetwork network, WorldMap map, CityLayoutRules rules, IReadOnlyList<Option> options,
        LotProfile profile, int level, int start, out int found)
    {
        var around = network.Around;
        int total = around.Length * options.Count;
        var central = LotTerms.Choose(rules, profile, central: true).AtLevel(level);
        var outer = LotTerms.Choose(rules, profile, central: false).AtLevel(level);
        var shapes = new LotShape?[options.Count * 2];
        int centralSq = rules.CentralDepthCells * rules.CentralDepthCells;
        for (int i = Math.Max(0, start); i < total; i++)
        {
            int option = i % options.Count;
            var origin = around[i / options.Count];
            long dx = origin.X - network.Center.X, dy = origin.Y - network.Center.Y;
            bool isCentral = dx * dx + dy * dy <= centralSq;
            var terms = isCentral ? central : outer;
            ref var slot = ref shapes[option * 2 + (isCentral ? 0 : 1)];
            var shape = slot ??= LotShape.Of(options[option].Footprint, terms.Setback, terms.Croft);
            if (!Fits(ground, network, map, shape, origin, 1, level))
                continue;
            var doorstep = new CellCoord(origin.X + shape.Door.X + shape.Facing.X, origin.Y + shape.Door.Y + shape.Facing.Y);
            var own = new HashSet<CellCoord>();
            foreach (var c in shape.Cells) own.Add(new CellCoord(c.X + origin.X, c.Y + origin.Y));
            foreach (var c in shape.Croft) own.Add(new CellCoord(c.X + origin.X, c.Y + origin.Y));
            if (PathToStreet(ground, network, map, doorstep, rules.PathCells, own) is null) continue;
            found = i;
            return (origin, terms.Croft, options[option].Orientation);
        }
        found = total;
        return null;
    }

    /// <summary>Profundidade (passos de rua desde o largo) do candidato de índice <paramref name="found"/>.</summary>
    public static int DepthOf(StreetNetwork network, CityLayoutRules rules, int optionCount, LotProfile profile, int found)
    {
        int perStreet = (rules.MaxFrontYardCells() + 1) * optionCount;
        int street = (profile.Dwelling ? network.Nearest : network.MainFirst)[found / perStreet];
        return network.Depth[street];
    }

    /// <summary>Planta em terra seca, fora de rua, passagem e (até o degrau 1) quintal alheio; recuo livre de plantas; a
    /// passagem da porta até a rua livre; o quintal de trás livre.</summary>
    internal static bool Fits(
        CityGround ground, StreetNetwork network, WorldMap map, LotShape shape, CellCoord origin, int k, int level)
    {
        var barred = ground.Bar;
        foreach (var c in shape.Cells)
        {
            var abs = new CellCoord(c.X + origin.X, c.Y + origin.Y);
            if (!Dry(map, abs) || network.Contains(abs) || ground.Occupied.Contains(abs) || ground.Passages.Contains(abs)) return false;
            if (level < 2 && ground.Crofts.Contains(abs)) return false;
            if (barred is not null && barred.Covers(abs)) return false;
        }
        foreach (var c in shape.Halo)
            if (ground.Occupied.Contains(new CellCoord(c.X + origin.X, c.Y + origin.Y))) return false;
        // Passagem e quintal podem ter água (vau, poça, regato no fundo); só a casa pede chão seco.
        for (int j = 1; j < k; j++)
        {
            var abs = new CellCoord(origin.X + shape.Door.X + shape.Facing.X * j, origin.Y + shape.Door.Y + shape.Facing.Y * j);
            if (!InMap(map, abs) || network.Contains(abs) || ground.Occupied.Contains(abs) || ground.Crofts.Contains(abs)) return false;
        }
        foreach (var c in shape.Croft)
        {
            var abs = new CellCoord(c.X + origin.X, c.Y + origin.Y);
            if (!InMap(map, abs) || network.Contains(abs) || ground.Occupied.Contains(abs) || ground.Passages.Contains(abs)) return false;
            if (barred is not null && barred.Covers(abs)) return false;
        }
        return true;
    }

    private static bool InMap(WorldMap map, CellCoord cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < map.Width && cell.Y < map.Height;

    private static bool Dry(WorldMap map, CellCoord cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < map.Width && cell.Y < map.Height && map.TryGetCell(cell, out var m) && !m.HasWater;

    /// <summary>Identidade da busca para os marcos: planta, porta, degrau e os termos do lote (no centro e fora dele).</summary>
    public static ulong Key(IReadOnlyList<Option> options, CityLayoutRules rules, LotProfile profile, int level)
    {
        ulong hash = 0xCBF29CE484222325UL ^ (ulong)(level + 101);
        foreach (var c in options.SelectMany(o => o.Footprint))
        {
            hash = (hash ^ (uint)c.Cell.X) * 1099511628211UL;
            hash = (hash ^ (uint)c.Cell.Y) * 1099511628211UL;
            hash = (hash ^ (uint)c.Material) * 1099511628211UL;
        }
        foreach (bool central in new[] { true, false })
        {
            var t = LotTerms.Choose(rules, profile, central).AtLevel(level);
            hash = (hash ^ (uint)(t.FrontYard * 1000 + t.Setback * 100 + t.Croft)) * 1099511628211UL;
        }
        return (hash ^ (profile.Dwelling ? 7UL : 13UL)) * 1099511628211UL;
    }
}
