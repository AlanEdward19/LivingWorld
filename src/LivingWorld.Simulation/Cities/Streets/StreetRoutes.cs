using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Uma rota resolvida (AD-176): custo a pé (unidade de <c>MovementCost</c>, horas), células d'água atravessadas (o vau),
/// células sem caminho (o que o veículo de terra tem de vencer), as pontas já na soleira e, se foi pela rede de caminhos, onde
/// entrou e saiu dela.</summary>
internal sealed class StreetRoute
{
    public double Cost;
    public int Water;
    public int OffNet;
    public int A = -1, B = -1;
    public int E = -1, X = -1;
    public bool Internal;
}

/// <summary>Trecho pela rede de caminhos entre duas células dela: custo e as células, em ordem.</summary>
internal sealed class NetLeg
{
    public double Cost = double.PositiveInfinity;
    public int[] Path = [];
}

/// <summary>Cache volátil das rotas de um mundo (AD-176), função pura de (mapa, chão em vigor, prédios até
/// <see cref="GroundLedger.ObstacleTick"/>): refeito quando um deles muda, então o mundo reaberto acha as mesmas rotas.</summary>
internal sealed class StreetRouteState
{
    public long ObstacleTick = long.MinValue;
    public int BuildingsSeen = -1;
    public int SurfaceVersion = int.MinValue;
    public double FordPerCell = double.NaN;
    public StreetRules Rules = StreetRules.Default;
    public WorldMap? Map;
    public int W, H;
    public double Base, AltWeight, MinTerrain;
    public double[] Terrain = [];
    public int[] Alt = [];
    public bool[] Water = [];
    public double[] Factor = [];
    public bool[] Net = [];
    /// <summary>AD-185: célula de caminho fora de prédio (onde o trecho da rede anda): uma leitura no A* em vez de duas.</summary>
    public bool[] Walk = [];
    public bool AnyNet;
    public int[] Owner = [];
    public byte[] Kind = [];
    public int[] Door = [];
    public int[] Facing = [];
    public bool[] DoorOver = [];
    public long[] OwnerId = [];
    public int[] Entry = [];
    public readonly Dictionary<long, StreetRoute> Pairs = [];
    public readonly Dictionary<long, NetLeg> Legs = [];
    public double[] G = [];
    public int[] Prev = [];
    public int[] Seen = [];
    public int Stamp;
    /// <summary>AD-185: quantas vezes as rotas guardadas foram jogadas fora (o carimbo dos caches derivados de rota).</summary>
    public long RouteVersion;
    /// <summary>AD-185: a fila do A* e do Dijkstra da rede, reaproveitada (esvaziada a cada uso): a busca não aloca.</summary>
    public readonly PriorityQueue<int, (double, int)> Queue = new();
    /// <summary>AD-185: a rede como lista de vizinhos (CSR): de cada célula andável, os vizinhos andáveis e o custo da passagem, já
    /// calculados quando a rede ou os obstáculos mudam; e o menor custo por unidade de distância da rede (a heurística do A*).</summary>
    public int[] AdjStart = [];
    public int[] AdjCell = [];
    public double[] AdjCost = [];
    public double NetRate;
    /// <summary>AD-185: o heap do A* em vetores (chave = custo estimado, desempate pela célula), reaproveitado.</summary>
    public double[] HeapKey = new double[64];
    public int[] HeapCell = new int[64];
    /// <summary>AD-185: os polos da rede (a célula de caminho onde desemboca a porta de cada oficina e mercado), para onde e de onde
    /// vai quase toda viagem; de cada polo, um campo de distância por toda a rede (Dijkstra), feito na primeira vez que se pede e
    /// guardado até a rede mudar. Com ele o trecho casa↔trabalho é só seguir o campo, sem busca.</summary>
    public HashSet<int> Hubs = [];
    public readonly Dictionary<int, (double[] Dist, int[] Prev)> Fields = [];
    /// <summary>Índice compacto de cada célula andável (−1 fora da rede) e quantas há: o campo guarda só as células da rede. Os vetores
    /// de campos jogados fora voltam para reuso.</summary>
    public int[] NetId = [];
    public int NetCount;
    public readonly Stack<(double[] Dist, int[] Prev)> FieldPool = new();
    public long HubsStamp = long.MinValue;
    /// <summary>AD-185: marcos do A* (as oito pontas da rede): o campo de distância de cada um dá, pela desigualdade
    /// triangular, um limite inferior muito mais justo que a reta (ALT), e a busca abre poucas células.</summary>
    public int[] Landmarks = [];
    /// <summary>AD-185: a geometria do acesso de cada prédio (o que a projeção pede a cada quadro), guardada até as rotas mudarem.</summary>
    public readonly Dictionary<long, (IReadOnlyList<CellCoord> Cells, long? Via, CellCoord? Outlet)> Access = [];
    public long AccessVersion = long.MinValue;
    public Dictionary<long, int>? OwnerIndex;
}

/// <summary>Rota real do NPC (AD-176): da soleira de onde está até a soleira de onde vai, ou em linha reta pelo terreno (pagando
/// cada prédio no caminho: contornar o solto, subir por escada, laje ou telhado no colado), ou indo até o caminho mais perto,
/// seguindo a rede de trilhas e ruas e saindo dela perto do destino — a mais rápida. A superfície (trilha, terra batida,
/// cascalho, pedra) muda o passo. O tempo de viagem sai daqui; o pisoteio, das células da rota.</summary>
public static class StreetRoutes
{
    private const int MaxCachedPairs = 400_000;

    private static readonly (int Dx, int Dy, double Len)[] Steps =
    [
        (1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1),
        (1, 1, Math.Sqrt(2)), (1, -1, Math.Sqrt(2)), (-1, 1, Math.Sqrt(2)), (-1, -1, Math.Sqrt(2)),
    ];

    /// <summary>Horas a pé da rota (sem o vau extra da invenção), arredondadas para cima; 0 no mesmo lugar.</summary>
    internal static long Ticks(WorldState world, CellCoord origin, CellCoord destination)
    {
        if (origin == destination) return 0;
        if (!world.StreetsOn)
            return LivingWorld.Domain.Behavior.TravelResolution.TicksBetween(world.Map, origin, destination);
        return Math.Max(1, (long)Math.Ceiling(Get(world, origin, destination).Cost));
    }

    /// <summary>Carimbo do que as rotas leem (obstáculos, superfície, vau): cache derivado de rota é válido enquanto ele não muda.</summary>
    /// AD-185: com a rua ligada, a versão das rotas guardadas (só muda quando alguma rota pode ter mudado), não o mês dos obstáculos.
    internal static (long Obstacles, int Surfaces, bool Ford) Stamp(WorldState world) =>
        world.StreetsOn ? (StateOf(world).RouteVersion, world.Ground!.SurfaceVersion, world.InventionOn) : (long.MinValue, int.MinValue, world.InventionOn);

    /// <summary>Custo da rota a pé (unidade de <c>MovementCost</c>) e as células d'água dela.</summary>
    internal static (double Cost, int Water) CostOf(WorldState world, CellCoord origin, CellCoord destination)
    {
        if (origin == destination) return (0, 0);
        var route = Get(world, origin, destination);
        return (route.Cost, route.Water);
    }

    /// <summary>Células d'água da rota real (o vau); sem a rua de verdade, as da reta, como antes.</summary>
    public static int WaterCells(WorldState world, CellCoord origin, CellCoord destination) =>
        world.StreetsOn && origin != destination
            ? Get(world, origin, destination).Water
            : LivingWorld.Simulation.Invention.WaterCrossing.CellsBetween(world.Map, origin, destination);

    /// <summary>O veículo de terra da casa cabe na rota: no máximo <see cref="StreetRules.HaulerOffRoadCells"/> células sem caminho.</summary>
    internal static bool CartFits(WorldState world, CellCoord origin, CellCoord destination)
    {
        if (!world.StreetsOn || origin == destination) return true;
        return Get(world, origin, destination).OffNet <= world.StreetRules.HaulerOffRoadCells;
    }

    internal static StreetRoute Get(WorldState world, CellCoord origin, CellCoord destination)
    {
        var state = StateOf(world);
        int o = Index(state, origin), d = Index(state, destination);
        if (o < 0 || d < 0)
        {
            // ponta fora do mapa (não deveria haver): a reta pelo custo base, sem terreno nem água
            double dx = destination.X - origin.X, dy = destination.Y - origin.Y;
            return new StreetRoute { Cost = world.Map.Cost.Base * Math.Sqrt(dx * dx + dy * dy) };
        }
        long key = ((long)o << 32) | (uint)d;
        if (world.RouteCacheOff) { ClearLegs(state); return Compute(state, o, d); }
        if (state.Pairs.TryGetValue(key, out var known)) return known;
        var route = Compute(state, o, d);
        if (state.Pairs.Count >= MaxCachedPairs) state.Pairs.Clear();
        state.Pairs[key] = route;
        return route;
    }

    private static int Index(StreetRouteState state, CellCoord c) =>
        c.X >= 0 && c.Y >= 0 && c.X < state.W && c.Y < state.H ? c.Y * state.W + c.X : -1;

    /// <summary>O estado de rotas em vigor: refeito quando o chão (revisão), os obstáculos (virada do mês) ou o vau mudam.</summary>
    internal static StreetRouteState StateOf(WorldState world)
    {
        var ground = world.Ground!;
        var rules = world.StreetRules;
        double fordPerCell = world.InventionOn ? world.Map.Cost.Base * world.InventionRules!.FordExtraCellsPm / 1000.0 : 0;
        var state = world.RouteState;
        if (state is not null && ReferenceEquals(state.Map, world.Map) && state.ObstacleTick == ground.ObstacleTick
            && state.SurfaceVersion == ground.SurfaceVersion && state.FordPerCell == fordPerCell && ReferenceEquals(state.Rules, rules))
        {
            if (state.BuildingsSeen == world.Buildings.Count) return state;
            // prédio novo depois do carimbo não muda nada (só vira obstáculo na virada do mês); um com data até o carimbo, sim
            bool qualifies = false;
            for (int i = Math.Max(0, state.BuildingsSeen); i < world.Buildings.Count; i++)
                if (world.Buildings[i].Position is not null && world.Buildings[i].CompletedAtTick <= ground.ObstacleTick) { qualifies = true; break; }
            state.BuildingsSeen = world.Buildings.Count;
            if (!qualifies) return state;
            state.ObstacleTick = long.MinValue; // refaz os obstáculos abaixo
        }

        bool fresh = state is null || !ReferenceEquals(state.Map, world.Map);
        if (fresh) state = NewState(world.Map);
        bool surfaces = state!.SurfaceVersion != ground.SurfaceVersion || !ReferenceEquals(state.Rules, rules);
        state.Rules = rules;
        bool obstaclesChanged = false;
        if (state.ObstacleTick != ground.ObstacleTick || state.BuildingsSeen != world.Buildings.Count)
        {
            // AD-185: a virada do mês refaz os obstáculos; se saíram iguais (nenhum prédio novo virou obstáculo), as rotas guardadas
            // continuam valendo e não são recalculadas (antes todo mês esvaziava o cache e cada rota era refeita)
            var owner = fresh ? null : (int[])state.Owner.Clone();
            var kind = fresh ? null : (byte[])state.Kind.Clone();
            var door = state.Door;
            var doorOver = state.DoorOver;
            BuildObstacles(world, state, ground.ObstacleTick);
            obstaclesChanged = owner is null || !owner.AsSpan().SequenceEqual(state.Owner) || !kind.AsSpan().SequenceEqual(state.Kind)
                || !door.AsSpan().SequenceEqual(state.Door) || !doorOver.AsSpan().SequenceEqual(state.DoorOver);
        }
        state.BuildingsSeen = world.Buildings.Count;
        if (surfaces) BuildSurfaces(state, ground);
        state.ObstacleTick = ground.ObstacleTick;
        state.SurfaceVersion = ground.SurfaceVersion;
        bool ford = state.FordPerCell != fordPerCell;
        state.FordPerCell = fordPerCell;
        if (surfaces || obstaclesChanged || fresh)
        {
            for (int i = 0; i < state.Walk.Length; i++) state.Walk[i] = state.Net[i] && state.Owner[i] == 0;
            BuildAdjacency(state);
        }
        if (fresh || surfaces || obstaclesChanged || ford) { state.Pairs.Clear(); state.RouteVersion++; }
        if (obstaclesChanged && !surfaces) ClearLegs(state); // o trecho da rede não entra em célula de prédio
        if (surfaces || obstaclesChanged || fresh || state.HubsStamp != world.Workplaces.Count) BuildHubs(world, state);
        world.RouteState = state;
        return state;
    }

    private static StreetRouteState NewState(WorldMap map)
    {
        int w = map.Width, h = map.Height, n = w * h;
        var state = new StreetRouteState
        {
            Map = map,
            W = w,
            H = h,
            Base = map.Cost.Base,
            AltWeight = map.Cost.AltitudeWeight,
            Terrain = new double[n],
            Alt = new int[n],
            Water = new bool[n],
            Factor = new double[n],
            Net = new bool[n],
            Walk = new bool[n],
            Owner = new int[n],
            Kind = new byte[n],
            Entry = new int[n],
            G = new double[n],
            Prev = new int[n],
            Seen = new int[n],
        };
        double min = double.MaxValue;
        foreach (var cell in map.Cells)
        {
            int i = Index(state, cell.Coord);
            if (i < 0) continue;
            state.Terrain[i] = map.Cost.WeightOf(cell.Terrain);
            state.Alt[i] = cell.Altitude;
            state.Water[i] = cell.HasWater;
            min = Math.Min(min, state.Terrain[i]);
        }
        state.MinTerrain = min == double.MaxValue ? 1 : min;
        Array.Fill(state.Factor, 1.0);
        Array.Fill(state.Entry, -1);
        return state;
    }

    /// <summary>Prédios com posição gravada concluídos até <paramref name="tick"/>: cada célula da planta é obstáculo (contornar o
    /// solto, passar por cima do colado), e a soleira é por onde se entra e sai.</summary>
    private static void BuildObstacles(WorldState world, StreetRouteState state, long tick)
    {
        Array.Clear(state.Owner);
        Array.Clear(state.Kind);
        var placed = new List<(Building Building, CellCoord Origin)>();
        foreach (var building in world.Buildings)
            if (building.Position is { } p && building.CompletedAtTick <= tick) placed.Add((building, p));
        placed.Sort((a, b) => a.Building.Id.Value.CompareTo(b.Building.Id.Value));
        state.Door = new int[placed.Count + 1];
        state.Facing = new int[placed.Count + 1];
        state.DoorOver = new bool[placed.Count + 1];
        state.OwnerId = new long[placed.Count + 1];
        var doorsLocal = new (CellCoord Door, CellCoord Facing)[placed.Count + 1];
        for (int k = 0; k < placed.Count; k++)
        {
            var (building, origin) = placed[k];
            var footprint = BuildingFootprintGenerator.Generate(building);
            state.OwnerId[k + 1] = building.Id.Value;
            foreach (var cell in footprint)
            {
                int i = Index(state, new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
                if (i >= 0 && state.Owner[i] == 0) { state.Owner[i] = k + 1; state.Kind[i] = 1; }
            }
            doorsLocal[k + 1] = LotGeometry.DoorOf(footprint);
        }
        // casa colada (parede-meia): quem encosta noutro prédio é atravessado por cima (escada, laje, telhado), não contornado
        var glued = new bool[placed.Count + 1];
        for (int i = 0; i < state.Owner.Length; i++)
        {
            int owner = state.Owner[i];
            if (owner == 0) continue;
            int x = i % state.W, y = i / state.W;
            if (x + 1 < state.W) Glue(glued, owner, state.Owner[i + 1]);
            if (y + 1 < state.H) Glue(glued, owner, state.Owner[i + state.W]);
        }
        for (int i = 0; i < state.Owner.Length; i++)
            if (state.Owner[i] != 0 && glued[state.Owner[i]]) state.Kind[i] = 2;
        for (int k = 1; k <= placed.Count; k++)
        {
            var origin = placed[k - 1].Origin;
            var (door, facing) = doorsLocal[k];
            int step = Index(state, new CellCoord(origin.X + door.X + facing.X, origin.Y + door.Y + facing.Y));
            state.Door[k] = step;
            state.Facing[k] = facing.X != 0 ? 0 : 1; // 0: a porta abre para leste/oeste; 1: para norte/sul
            state.DoorOver[k] = step >= 0 && state.Owner[step] != 0 && state.Owner[step] != k;
        }
    }

    private static void Glue(bool[] glued, int owner, int other)
    {
        if (other == 0 || other == owner) return;
        glued[owner] = true;
        glued[other] = true;
    }

    /// <summary>Fator de passo por célula pela superfície em vigor, a rede de caminhos e, para cada célula, a célula de caminho mais
    /// perto (Dijkstra de várias fontes, a partir da rede).</summary>
    private static void BuildSurfaces(StreetRouteState state, GroundLedger ground)
    {
        var rules = state.Rules;
        ClearLegs(state);
        state.AnyNet = false;
        var surfaces = ground.Surfaces;
        for (int i = 0; i < state.Factor.Length; i++)
        {
            var surface = (GroundSurface)surfaces[i];
            state.Factor[i] = FactorOf(rules, surface);
            state.Net[i] = surface != GroundSurface.None;
            state.AnyNet |= state.Net[i];
        }
        Array.Fill(state.Entry, -1);
        if (!state.AnyNet) return;
        var dist = state.G;
        Array.Fill(dist, double.PositiveInfinity);
        var queue = state.Queue;
        queue.Clear();
        for (int i = 0; i < state.Net.Length; i++)
            if (state.Net[i]) { dist[i] = 0; state.Entry[i] = i; queue.Enqueue(i, (0, i)); }
        while (queue.TryDequeue(out int at, out var pri))
        {
            if (pri.Item1 > dist[at]) continue;
            int x = at % state.W, y = at / state.W;
            foreach (var (dx, dy, len) in Steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= state.W || ny >= state.H) continue;
                int n = ny * state.W + nx;
                double c = pri.Item1 + state.Base * len * (state.Terrain[at] + state.Terrain[n]) * 0.5 * (state.Factor[at] + state.Factor[n]) * 0.5;
                if (c >= dist[n]) continue;
                dist[n] = c;
                state.Entry[n] = state.Entry[at];
                queue.Enqueue(n, (c, n));
            }
        }
    }

    internal static double FactorOf(StreetRules rules, GroundSurface surface) => surface switch
    {
        GroundSurface.Trail => rules.TrailPm / 1000.0,
        GroundSurface.Dirt => rules.DirtPm / 1000.0,
        GroundSurface.Gravel => rules.GravelPm / 1000.0,
        GroundSurface.Stone => rules.StonePm / 1000.0,
        _ => 1.0,
    };

    /// <summary>A soleira do prédio em que a célula está (por ela se entra e sai); fora de prédio, a própria célula.</summary>
    private static int Snap(StreetRouteState state, int cell, out double extra)
    {
        extra = 0;
        int owner = state.Owner[cell];
        if (owner == 0) return cell;
        int door = state.Door[owner];
        if (door < 0) { extra = state.Base * state.Rules.OverPm / 1000.0; return cell; }
        if (state.DoorOver[owner]) extra = state.Base * state.Rules.OverPm / 1000.0; // porta tapada: escada, laje ou telhado do vizinho
        return door;
    }

    private static StreetRoute Compute(StreetRouteState state, int o, int d)
    {
        int ownerO = state.Owner[o], ownerD = state.Owner[d];
        if (ownerO != 0 && ownerO == ownerD)
            return new StreetRoute { Cost = Movement(state, o, d), Internal = true, A = o, B = d };
        int a = Snap(state, o, out double extraA), b = Snap(state, d, out double extraB);
        var direct = Line(state, a, b);
        var best = (direct.Cost, direct.Water, direct.OffNet, E: -1, X: -1);
        if (state.AnyNet && a != b && state.Entry[a] >= 0 && state.Entry[b] >= 0)
        {
            int e = state.Entry[a], x = state.Entry[b];
            var first = Line(state, a, e);
            var last = Line(state, x, b);
            // AD-185: o trecho pela rede nem é buscado quando nem o limite inferior dele vence a reta (a escolha seria a mesma)
            int ddx = Math.Abs(e % state.W - x % state.W), ddy = Math.Abs(e / state.W - x / state.W);
            double floor = first.Cost + last.Cost + state.NetRate * (Math.Max(ddx, ddy) + 0.41421356237309515 * Math.Min(ddx, ddy))
                + (first.Water + last.Water) * state.FordPerCell;
            var leg = floor >= best.Cost + best.Water * state.FordPerCell ? null
                : e == x ? new NetLeg { Cost = 0, Path = [e] } : Leg(state, e, x);
            if (leg is not null && !double.IsPositiveInfinity(leg.Cost))
            {
                double cost = first.Cost + leg.Cost + last.Cost;
                int water = first.Water + last.Water + WaterOn(state, leg.Path);
                if (cost + water * state.FordPerCell < best.Cost + best.Water * state.FordPerCell)
                    best = (cost, water, first.OffNet + last.OffNet, e, x);
            }
        }
        return new StreetRoute
        {
            Cost = best.Cost + extraA + extraB,
            Water = best.Water,
            OffNet = best.OffNet,
            A = a,
            B = b,
            E = best.E,
            X = best.X,
        };
    }

    private static int WaterOn(StreetRouteState state, int[] path)
    {
        int n = 0;
        for (int i = 1; i < path.Length - 1; i++) if (state.Water[path[i]]) n++;
        return n;
    }

    /// <summary>Custo do mapa entre duas células (o de <c>MovementCost.Between</c>, em grade densa).</summary>
    private static double Movement(StreetRouteState state, int a, int b)
    {
        if (a == b) return 0;
        double dx = b % state.W - a % state.W, dy = b / state.W - a / state.W;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double terrainFactor = (state.Terrain[a] + state.Terrain[b]) / 2.0;
        double climb = Math.Max(0, state.Alt[b] - state.Alt[a]) * state.AltWeight;
        return state.Base * distance * terrainFactor + climb;
    }

    /// <summary>Reta pelo terreno de <paramref name="a"/> a <paramref name="b"/> (supercobertura): o custo do mapa vezes o passo médio
    /// das células (a superfície), mais o contorno ou a subida de cada prédio atravessado; a água do meio e as células sem caminho.</summary>
    private static (double Cost, int Water, int OffNet) Line(StreetRouteState state, int a, int b)
    {
        if (a == b) return (0, 0, state.Net[a] ? 0 : 1);
        int ax = a % state.W, ay = a / state.W, bx = b % state.W, by = b / state.W;
        int dx = Math.Abs(bx - ax), dy = Math.Abs(by - ay);
        int sx = bx > ax ? 1 : -1, sy = by > ay ? 1 : -1;
        int x = ax, y = ay, water = 0, off = (state.Net[a] ? 0 : 1) + (state.Net[b] ? 0 : 1);
        double factor = state.Factor[a] + state.Factor[b];
        int count = 2, overPm = 0, runKind = 0;
        int steps = dx + dy, error = dx - dy;
        dx *= 2; dy *= 2;
        for (int i = 1; i < steps; i++)
        {
            if (error > 0) { x += sx; error -= dy; }
            else { y += sy; error += dx; }
            int c = y * state.W + x;
            factor += state.Factor[c];
            count++;
            if (state.Water[c]) water++;
            // prédio no caminho: cada trecho seguido de prédio custa uma volta (o solto) ou uma subida por cima (o colado)
            int kind = state.Kind[c];
            if (kind != 0 && kind != runKind) overPm += kind == 1 ? state.Rules.DetourPm : state.Rules.OverPm;
            runKind = kind;
            if (kind == 0 && !state.Net[c] && !state.Water[c]) off++;
        }
        double cost = Movement(state, a, b) * (factor / count) + state.Base * overPm / 1000.0;
        return (cost, water, off);
    }

    private static void ClearLegs(StreetRouteState state)
    {
        state.Legs.Clear();
        foreach (var root in state.Fields.Keys.OrderBy(k => k)) state.FieldPool.Push(state.Fields[root]);
        state.Fields.Clear();
    }

    /// <summary>AD-185: os polos — a célula de caminho mais perto da soleira de cada oficina aberta (o que <see cref="Compute"/> usa
    /// como entrada da rede para ela). Mudou o conjunto, os campos e trechos guardados saem.</summary>
    private static void BuildHubs(WorldState world, StreetRouteState state)
    {
        var hubs = new HashSet<int>();
        if (state.AnyNet)
            foreach (var place in world.Workplaces)
            {
                if (place.Closed) continue;
                int i = Index(state, place.Location);
                if (i < 0) continue;
                int door = Snap(state, i, out _);
                if (door >= 0 && state.Entry[door] >= 0) hubs.Add(state.Entry[door]);
            }
        state.HubsStamp = world.Workplaces.Count;
        // os marcos da heurística: as oito pontas da rede (cantos e lados). Antes os polos também eram marcos, e na cidade grande
        // (centenas de oficinas) cada passo da busca somava centenas de campos: a heurística custava mais que a busca
        var marks = new SortedSet<int>();
        int sMin = -1, sMax = -1, dMin = -1, dMax = -1, xMin = -1, xMax = -1, yMin = -1, yMax = -1;
        for (int i = 0; i < state.Walk.Length; i++)
        {
            if (!state.Walk[i]) continue;
            int x = i % state.W, y = i / state.W, sum = x + y, diff = x - y;
            if (sMin < 0 || sum < sMin % state.W + sMin / state.W) sMin = i;
            if (sMax < 0 || sum > sMax % state.W + sMax / state.W) sMax = i;
            if (dMin < 0 || diff < dMin % state.W - dMin / state.W) dMin = i;
            if (dMax < 0 || diff > dMax % state.W - dMax / state.W) dMax = i;
            if (xMin < 0 || x < xMin % state.W) xMin = i;
            if (xMax < 0 || x > xMax % state.W) xMax = i;
            if (yMin < 0) yMin = i; // a primeira célula andável é a de menor y
            yMax = i;
        }
        foreach (int c in new[] { sMin, sMax, dMin, dMax, xMin, xMax, yMin, yMax }) if (c >= 0) marks.Add(c);
        var landmarks = marks.ToArray();
        bool same = hubs.SetEquals(state.Hubs) && landmarks.AsSpan().SequenceEqual(state.Landmarks);
        state.Landmarks = landmarks;
        if (same) return;
        state.Hubs = hubs;
        ClearLegs(state);
        state.Pairs.Clear();
        state.RouteVersion++;
    }

    /// <summary>Campo de distância de um polo por toda a rede (Dijkstra na lista de vizinhos; empate pela célula).</summary>
    private static (double[] Dist, int[] Prev) FieldOf(StreetRouteState state, int root)
    {
        if (state.Fields.TryGetValue(root, out var field)) return field;
        int n = state.NetCount;
        double[] dist; int[] prev;
        if (state.FieldPool.Count > 0 && state.FieldPool.Peek().Dist.Length >= n) (dist, prev) = state.FieldPool.Pop();
        else { state.FieldPool.Clear(); dist = new double[n]; prev = new int[n]; }
        Array.Fill(dist, double.PositiveInfinity, 0, n);
        Array.Fill(prev, -1, 0, n);
        var id = state.NetId;
        var adjStart = state.AdjStart; var adjCell = state.AdjCell; var adjCost = state.AdjCost;
        int size = 0;
        dist[id[root]] = 0;
        Push(state, ref size, 0, root);
        while (size > 0)
        {
            double f = state.HeapKey[0];
            int at = Pop(state, ref size);
            if (f > dist[id[at]]) continue;
            for (int j = adjStart[at], end = adjStart[at + 1]; j < end; j++)
            {
                int m = adjCell[j], mi = id[m];
                double c = f + adjCost[j];
                if (c >= dist[mi]) continue;
                dist[mi] = c; prev[mi] = at;
                Push(state, ref size, c, m);
            }
        }
        field = (dist, prev);
        state.Fields[root] = field;
        return field;
    }

    /// <summary>AD-185: monta a rede como lista de vizinhos (mesma ordem de passos e mesma conta de custo da busca) e a menor taxa de
    /// custo por distância (terreno × passo de cada passagem ÷ o comprimento dela), o limite inferior da heurística.</summary>
    private static void BuildAdjacency(StreetRouteState state)
    {
        int n = state.Walk.Length;
        if (state.AdjStart.Length != n + 1) state.AdjStart = new int[n + 1];
        if (state.NetId.Length != n) state.NetId = new int[n];
        int net = 0;
        for (int i = 0; i < n; i++) state.NetId[i] = state.Walk[i] ? net++ : -1;
        state.NetCount = net;
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            state.AdjStart[i] = count;
            if (!state.Walk[i]) continue;
            int x = i % state.W, y = i / state.W;
            foreach (var (dx, dy, _) in Steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < state.W && ny < state.H && state.Walk[ny * state.W + nx]) count++;
            }
        }
        state.AdjStart[n] = count;
        if (state.AdjCell.Length < count) { state.AdjCell = new int[count]; state.AdjCost = new double[count]; }
        double rate = double.PositiveInfinity;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            if (!state.Walk[i]) continue;
            int x = i % state.W, y = i / state.W;
            foreach (var (dx, dy, len) in Steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= state.W || ny >= state.H) continue;
                int m = ny * state.W + nx;
                if (!state.Walk[m]) continue;
                double w = state.Base * len * (state.Terrain[i] + state.Terrain[m]) / 2.0 * (state.Factor[i] + state.Factor[m]) / 2.0;
                state.AdjCell[k] = m;
                state.AdjCost[k] = w;
                k++;
                rate = Math.Min(rate, w / len);
            }
        }
        state.NetRate = double.IsPositiveInfinity(rate) ? 0 : rate * (1 - 1e-12);
    }

    /// <summary>Trecho pela rede (AD-185): A* sobre a lista de vizinhos, heurística octil × a menor taxa da rede (admissível: nenhum
    /// trecho custa menos por célula andada), heap em vetores; empate pela célula. Em cache até a próxima revisão da superfície.</summary>
    private static NetLeg Leg(StreetRouteState state, int e, int x)
    {
        long key = ((long)e << 32) | (uint)x;
        if (state.Legs.TryGetValue(key, out var known)) return known;
        var leg = new NetLeg();
        // AD-185: trecho para um polo (ou de um polo): segue o campo de distância dele; a passagem custa o mesmo nos dois sentidos
        bool onNet = state.NetId[e] >= 0 && state.NetId[x] >= 0; // a entrada pode ter ficado sob um prédio novo: aí, a busca
        bool toHub = onNet && state.Hubs.Contains(x), fromHub = onNet && !toHub && state.Hubs.Contains(e);
        if (toHub || fromHub)
        {
            var (fieldDist, fieldPrev) = FieldOf(state, toHub ? x : e);
            int from = toHub ? e : x;
            var nid = state.NetId;
            if (!double.IsPositiveInfinity(fieldDist[nid[from]]))
            {
                int length = 0;
                for (int c = from; c >= 0; c = fieldPrev[nid[c]]) length++;
                var path = new int[length];
                if (toHub) { int j = 0; for (int c = from; c >= 0; c = fieldPrev[nid[c]]) path[j++] = c; }
                else { int j = length - 1; for (int c = from; c >= 0; c = fieldPrev[nid[c]]) path[j--] = c; }
                leg.Cost = fieldDist[nid[from]] + Math.Max(0, state.Alt[x] - state.Alt[e]) * state.AltWeight;
                leg.Path = path;
            }
            state.Legs[key] = leg;
            return leg;
        }
        int stamp = ++state.Stamp;
        if (stamp == int.MaxValue) { Array.Clear(state.Seen); state.Stamp = stamp = 1; }
        double hUnit = state.NetRate;
        int w = state.W, tx = x % w, ty = x / w;
        const double Diag = 0.41421356237309515; // √2 − 1
        var nids = state.NetId;
        int lmCount = 0;
        var lmDist = new double[]?[state.Landmarks.Length];
        var lmToX = new double[state.Landmarks.Length];
        if (nids[x] >= 0)
            foreach (int lm in state.Landmarks)
            {
                var d = FieldOf(state, lm).Dist;
                double dx = d[nids[x]];
                if (double.IsPositiveInfinity(dx)) continue;
                lmDist[lmCount] = d; lmToX[lmCount] = dx; lmCount++;
            }
        var seen = state.Seen; var gs = state.G; var prev = state.Prev;
        var adjStart = state.AdjStart; var adjCell = state.AdjCell; var adjCost = state.AdjCost;
        int size = 0;
        seen[e] = stamp; gs[e] = 0; prev[e] = -1;
        Push(state, ref size, Octile(e), e);
        while (size > 0)
        {
            double f = state.HeapKey[0];
            int at = Pop(state, ref size);
            double g = gs[at];
            if (f > g + Octile(at) + 1e-12) continue;
            if (at == x)
            {
                int length = 0;
                for (int c = x; c >= 0; c = prev[c]) length++;
                var path = new int[length];
                for (int c = x, j = length - 1; c >= 0; c = prev[c], j--) path[j] = c;
                leg.Cost = g + Math.Max(0, state.Alt[x] - state.Alt[e]) * state.AltWeight;
                leg.Path = path;
                break;
            }
            for (int j = adjStart[at], end = adjStart[at + 1]; j < end; j++)
            {
                int m = adjCell[j];
                double c = g + adjCost[j];
                if (seen[m] == stamp && c >= gs[m]) continue;
                seen[m] = stamp; gs[m] = c; prev[m] = at;
                Push(state, ref size, c + Octile(m), m);
            }
        }
        state.Legs[key] = leg;
        return leg;

        // limite inferior do custo de c até x: a reta no passo mais barato da rede, ou o dos marcos (o maior dos dois)
        double Octile(int c)
        {
            int ddx = Math.Abs(c % w - tx), ddy = Math.Abs(c / w - ty);
            double h = (Math.Max(ddx, ddy) + Diag * Math.Min(ddx, ddy)) * hUnit;
            int nc = nids[c];
            if (nc >= 0)
                for (int k = 0; k < lmCount; k++)
                {
                    double dc = lmDist[k]![nc];
                    if (double.IsPositiveInfinity(dc)) continue;
                    double b = Math.Abs(lmToX[k] - dc) * (1 - 1e-12);
                    if (b > h) h = b;
                }
            return h;
        }
    }

    /// <summary>Custo do trecho pela rede de hoje (para o teste de equivalência de custo).</summary>
    internal static double LegCost(StreetRouteState state, int e, int x) => Leg(state, e, x).Cost;

    private static bool Before(double ka, int ca, double kb, int cb) => ka < kb || (ka == kb && ca < cb);

    private static void Push(StreetRouteState state, ref int size, double key, int cell)
    {
        if (size == state.HeapKey.Length)
        {
            Array.Resize(ref state.HeapKey, size * 2);
            Array.Resize(ref state.HeapCell, size * 2);
        }
        int i = size++;
        var keys = state.HeapKey; var cells = state.HeapCell;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (!Before(key, cell, keys[parent], cells[parent])) break;
            keys[i] = keys[parent]; cells[i] = cells[parent];
            i = parent;
        }
        keys[i] = key; cells[i] = cell;
    }

    private static int Pop(StreetRouteState state, ref int size)
    {
        var keys = state.HeapKey; var cells = state.HeapCell;
        int top = cells[0];
        size--;
        if (size == 0) return top;
        double key = keys[size]; int cell = cells[size];
        int i = 0;
        while (true)
        {
            int l = 2 * i + 1;
            if (l >= size) break;
            int r = l + 1, child = r < size && Before(keys[r], cells[r], keys[l], cells[l]) ? r : l;
            if (!Before(keys[child], cells[child], key, cell)) break;
            keys[i] = keys[child]; cells[i] = cells[child];
            i = child;
        }
        keys[i] = key; cells[i] = cell;
        return top;
    }

    /// <summary>A busca de antes (AD-176, A* com a fila genérica e a heurística do passo mais rápido do mapa), sem cache: só para o
    /// teste de que o trecho de hoje não custa mais que o de antes.</summary>
    internal static double ReferenceLegCost(StreetRouteState state, int e, int x)
    {
        var leg = new NetLeg();
        int stamp = ++state.Stamp;
        if (stamp == int.MaxValue) { Array.Clear(state.Seen); state.Stamp = stamp = 1; }
        var r = state.Rules;
        double hUnit = state.Base * state.MinTerrain * Math.Min(1000, Math.Min(Math.Min(r.TrailPm, r.DirtPm), Math.Min(r.GravelPm, r.StonePm))) / 1000.0;
        int tx = x % state.W, ty = x / state.W;
        double H(int c)
        {
            int ddx = Math.Abs(c % state.W - tx), ddy = Math.Abs(c / state.W - ty);
            return hUnit * (Math.Max(ddx, ddy) + (Math.Sqrt(2) - 1) * Math.Min(ddx, ddy));
        }
        var queue = state.Queue;
        queue.Clear();
        state.Seen[e] = stamp; state.G[e] = 0; state.Prev[e] = -1;
        queue.Enqueue(e, (H(e), e));
        while (queue.TryDequeue(out int at, out var pri))
        {
            double g = state.G[at];
            if (pri.Item1 > g + H(at) + 1e-12) continue;
            if (at == x)
            {
                int length = 0;
                for (int c = x; c >= 0; c = state.Prev[c]) length++;
                var path = new int[length];
                for (int c = x, k = length - 1; c >= 0; c = state.Prev[c], k--) path[k] = c;
                leg.Cost = g + Math.Max(0, state.Alt[x] - state.Alt[e]) * state.AltWeight;
                leg.Path = path;
                break;
            }
            int ax = at % state.W, ay = at / state.W;
            foreach (var (dx, dy, len) in Steps)
            {
                int nx = ax + dx, ny = ay + dy;
                if (nx < 0 || ny < 0 || nx >= state.W || ny >= state.H) continue;
                int n = ny * state.W + nx;
                if (!state.Walk[n]) continue;
                // a subida conta só o desnível entre as pontas, como a reta (MovementCost): a altitude célula a célula é ruído do mapa
                double c = g + state.Base * len * (state.Terrain[at] + state.Terrain[n]) / 2.0 * (state.Factor[at] + state.Factor[n]) / 2.0;
                if (state.Seen[n] == stamp && c >= state.G[n]) continue;
                state.Seen[n] = stamp; state.G[n] = c; state.Prev[n] = at;
                queue.Enqueue(n, (c + H(n), n));
            }
        }
        return leg.Cost;
    }

    /// <summary>AD-176: as células por onde a pessoa anda, em ordem — a célula onde está, a soleira, a reta ou o caminho pela rede,
    /// a soleira do destino e a célula de chegada (água e prédio atravessado inclusive). Sem a rua de verdade, a reta de antes.</summary>
    public static IReadOnlyList<CellCoord> PathOf(WorldState world, CellCoord origin, CellCoord destination)
    {
        var cells = new List<int>();
        if (!world.StreetsOn || origin == destination)
            return origin == destination ? [origin] : LinePath(world.Map.Width, world.Map.Height, origin, destination);
        var state = StateOf(world);
        var route = Get(world, origin, destination);
        int o = Index(state, origin), d = Index(state, destination);
        if (o < 0 || d < 0) return [origin, destination];
        if (route.Internal) return LinePath(state.W, state.H, origin, destination);
        var all = new List<int> { o };
        void Add(int c) { if (all[^1] != c) all.Add(c); }
        void Line(int a, int b) { var tmp = new List<int>(); LineCells(state, a, b, tmp, all: true); foreach (int c in tmp) Add(c); }
        Line(o, route.A); // de dentro do prédio até a soleira
        if (route.E < 0) Line(route.A, route.B);
        else
        {
            Line(route.A, route.E);
            if (route.E != route.X) foreach (int c in Leg(state, route.E, route.X).Path) Add(c);
            Line(route.X, route.B);
        }
        Line(route.B, d); // da soleira até onde chega
        Add(d);
        return [.. all.Select(c => new CellCoord(c % state.W, c / state.W))];
    }

    private static List<CellCoord> LinePath(int w, int h, CellCoord a, CellCoord b)
    {
        var result = new List<CellCoord> { a };
        int dx = Math.Abs(b.X - a.X), dy = Math.Abs(b.Y - a.Y);
        int sx = b.X > a.X ? 1 : -1, sy = b.Y > a.Y ? 1 : -1;
        int x = a.X, y = a.Y, steps = dx + dy, error = dx - dy;
        dx *= 2; dy *= 2;
        for (int i = 1; i < steps; i++)
        {
            if (error > 0) { x += sx; error -= dy; }
            else { y += sy; error += dx; }
            result.Add(new CellCoord(x, y));
        }
        result.Add(b);
        return result;
    }

    /// <summary>As células por onde a rota passa (soleiras inclusive), sem prédio nem água: onde o pisoteio fica.</summary>
    internal static void CellsOf(StreetRouteState state, StreetRoute route, List<int> into)
    {
        if (route.Internal || route.A < 0) return;
        if (route.E < 0) { LineCells(state, route.A, route.B, into); return; }
        LineCells(state, route.A, route.E, into);
        var leg = route.E == route.X ? null : Leg(state, route.E, route.X);
        if (leg is not null) for (int i = 1; i < leg.Path.Length; i++) Keep(state, leg.Path[i], into);
        LineCells(state, route.X, route.B, into, skipFirst: true);
    }

    private static void Keep(StreetRouteState state, int c, List<int> into, bool all = false)
    {
        if (all || (state.Owner[c] == 0 && !state.Water[c])) into.Add(c);
    }

    private static void LineCells(StreetRouteState state, int a, int b, List<int> into, bool skipFirst = false, bool all = false)
    {
        if (!skipFirst) Keep(state, a, into, all);
        if (a == b) return;
        int ax = a % state.W, ay = a / state.W, bx = b % state.W, by = b / state.W;
        int dx = Math.Abs(bx - ax), dy = Math.Abs(by - ay);
        int sx = bx > ax ? 1 : -1, sy = by > ay ? 1 : -1;
        int x = ax, y = ay, steps = dx + dy, error = dx - dy;
        dx *= 2; dy *= 2;
        for (int i = 1; i < steps; i++)
        {
            if (error > 0) { x += sx; error -= dy; }
            else { y += sy; error += dx; }
            Keep(state, y * state.W + x, into, all);
        }
        Keep(state, b, into, all);
    }

    /// <summary>Tipo de acesso à porta do prédio (AD-176): <c>street</c> (porta em caminho ou rua), <c>path</c> (caminho a pé até
    /// ela), <c>alley</c> (viela entre dois prédios), <c>stair</c>/<c>roof</c> (a soleira é de outro prédio: escada do sobrado
    /// vizinho, laje ou telhado), <c>none</c> (porta para fora do mapa). Nulo sem a rua de verdade ou prédio ainda não obstáculo.</summary>
    public static string? AccessOf(WorldState world, Building building, IReadOnlySet<CellCoord> streets)
    {
        if (!world.StreetsOn || building.Position is null) return null;
        var state = StateOf(world);
        int owner = Array.IndexOf(state.OwnerId, building.Id.Value, 1);
        if (owner <= 0) return null;
        int door = state.Door[owner];
        if (door < 0) return "none";
        if (state.DoorOver[owner])
        {
            long neighbour = state.OwnerId[state.Owner[door]];
            return world.FindBuilding(new LivingWorld.Domain.Shared.BuildingId(neighbour)) is { } other
                && BuildingLayoutCache.Of(other).Layout.Floors > 1 ? "stair" : "roof";
        }
        var doorstep = new CellCoord(door % state.W, door / state.W);
        if (state.Net[door] || streets.Contains(doorstep)) return "street";
        // viela: os dois lados da soleira (perpendiculares à saída da porta) são prédio
        bool alley = state.Facing[owner] == 1
            ? Blocked(state, doorstep.X - 1, doorstep.Y) && Blocked(state, doorstep.X + 1, doorstep.Y)
            : Blocked(state, doorstep.X, doorstep.Y - 1) && Blocked(state, doorstep.X, doorstep.Y + 1);
        return alley ? "alley" : "path";
    }

    /// <summary>AD-185: índices, na rota de <see cref="PathOf"/>, das células por onde a pessoa passa "por cima" (escada, laje ou
    /// telhado de prédio colado: parede-meia), fora o prédio de onde sai e o de onde chega; vazio sem a rua de verdade.</summary>
    public static IReadOnlyList<int> OverIndicesOf(WorldState world, IReadOnlyList<CellCoord> path)
    {
        if (!world.StreetsOn || path.Count < 3) return [];
        var state = StateOf(world);
        int from = Index(state, path[0]), to = Index(state, path[^1]);
        int ownerFrom = from >= 0 ? state.Owner[from] : 0, ownerTo = to >= 0 ? state.Owner[to] : 0;
        List<int>? over = null;
        for (int i = 1; i < path.Count - 1; i++)
        {
            int c = Index(state, path[i]);
            if (c < 0 || state.Kind[c] != 2) continue;
            int owner = state.Owner[c];
            if (owner == ownerFrom || owner == ownerTo) continue;
            (over ??= []).Add(i);
        }
        return over ?? (IReadOnlyList<int>)[];
    }

    /// <summary>AD-185: a geometria do acesso à porta (o que a pessoa anda da soleira até o caminho mais perto, em ordem: a soleira, a
    /// reta pelo terreno, a primeira célula de caminho), o prédio vizinho por onde se entra quando a soleira é dele (escada, laje ou
    /// telhado) e a célula de caminho onde o acesso desemboca (nula sem rede). Nulo sem a rua de verdade ou prédio ainda não obstáculo.</summary>
    public static (IReadOnlyList<CellCoord> Cells, long? Via, CellCoord? Outlet)? AccessGeometryOf(WorldState world, Building building)
    {
        if (!world.StreetsOn || building.Position is null) return null;
        var state = StateOf(world);
        if (state.AccessVersion != state.RouteVersion * 31 + state.ObstacleTick)
        {
            state.Access.Clear();
            state.OwnerIndex = null;
            state.AccessVersion = state.RouteVersion * 31 + state.ObstacleTick;
        }
        if (state.Access.TryGetValue(building.Id.Value, out var known)) return known;
        if (state.OwnerIndex is null)
        {
            state.OwnerIndex = [];
            for (int k = 1; k < state.OwnerId.Length; k++) state.OwnerIndex[state.OwnerId[k]] = k;
        }
        if (!state.OwnerIndex.TryGetValue(building.Id.Value, out int owner) || owner <= 0) return null;
        int door = state.Door[owner];
        if (door < 0) return state.Access[building.Id.Value] = ([], null, null);
        long? via = state.DoorOver[owner] ? state.OwnerId[state.Owner[door]] : null;
        int entry = state.AnyNet ? state.Entry[door] : -1;
        var cells = new List<int>();
        if (entry < 0 || entry == door) cells.Add(door);
        else LineCells(state, door, entry, cells, all: true);
        var coords = new CellCoord[cells.Count];
        for (int i = 0; i < cells.Count; i++) coords[i] = new CellCoord(cells[i] % state.W, cells[i] / state.W);
        return state.Access[building.Id.Value] = (coords, via, entry >= 0 ? new CellCoord(entry % state.W, entry / state.W) : null);
    }

    private static bool Blocked(StreetRouteState state, int x, int y) =>
        x < 0 || y < 0 || x >= state.W || y >= state.H || state.Owner[y * state.W + x] != 0;
}
