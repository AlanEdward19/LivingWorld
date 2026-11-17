using System.Globalization;
using System.Text;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Tests.Shared.Cities;

/// <summary>Medidas do traçado de uma cidade (AD-173): folga entre prédios, encostados, densidade, porta alcançável a pé,
/// porta para a rua e distância das casas ao trabalho e à água. Usado pelo diagnóstico e pelos testes de traçado.</summary>
public sealed record CityLayoutStats(
    int Buildings, int[] GapHistogram, int Touching, int OnWater, int Overlapping, double FootprintDensity,
    double ExtentDensity, int DoorsBlocked, int DoorsOffStreet, int OnStreet, double MeanHomeToWork, double MeanHomeToWater,
    int StreetCells)
{
    /// <summary>Casas com quintal / casas, por faixa de meios (pobre, meio, remediado) e por ofício (lavrador, artesão).</summary>
    public string CroftShares { get; init; } = "";
    /// <summary>Células do lote (planta + quintal + passagem), média por prédio.</summary>
    public double MeanLotCells { get; init; }
    /// <summary>Células de rua em uso por superfície: trilha/terra/cascalho/pedra.</summary>
    public string Surfaces { get; init; } = "";

    public sealed record Placed(Building Building, CellCoord Origin, HashSet<CellCoord> Cells, CellCoord DoorOutside, int Croft = 0);

    /// <summary>Posição real de cada prédio: a autorada, senão a que o motor deriva (mesmo caminho de
    /// <see cref="CityOccupancy.OwnedBuildingFootprintBoxes"/>).</summary>
    public static List<Placed> PlacedBuildings(WorldState world)
    {
        var result = new List<Placed>();
        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            foreach (var (building, o, croft) in CityStreets.Placed(world, city))
            {
                var footprint = BuildingFootprintGenerator.Generate(building);
                var cells = footprint.Select(c => new CellCoord(c.Cell.X + o.X, c.Cell.Y + o.Y)).ToHashSet();
                var door = footprint.First(c => c.Material == BuildingMaterial.Door).Cell;
                var doorAbs = new CellCoord(door.X + o.X, door.Y + o.Y);
                var outside = Neighbors4(doorAbs).FirstOrDefault(n => !cells.Contains(n), doorAbs);
                result.Add(new Placed(building, o, cells, outside, croft));
            }
        }
        return result;
    }

    public static IEnumerable<CellCoord> Neighbors4(CellCoord c) =>
        [new(c.X, c.Y + 1), new(c.X + 1, c.Y), new(c.X, c.Y - 1), new(c.X - 1, c.Y)];

    /// <summary>Células livres entre dois prédios (Chebyshev entre as plantas, menos um): 0 = encostados.</summary>
    public static int Gap(Placed a, Placed b)
    {
        int best = int.MaxValue;
        foreach (var p in a.Cells)
            foreach (var q in b.Cells)
                best = Math.Min(best, Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y)) - 1);
        return best;
    }

    public static CityLayoutStats Measure(WorldState world)
    {
        var placed = PlacedBuildings(world);
        var map = world.Map;
        var streets = CityStreets.AllStreetCells(world);
        var hist = new int[5];
        int touching = 0, overlapping = 0;
        for (int i = 0; i < placed.Count; i++)
        {
            int nearest = int.MaxValue;
            for (int j = 0; j < placed.Count; j++)
                if (i != j) nearest = Math.Min(nearest, Gap(placed[i], placed[j]));
            if (nearest == int.MaxValue) continue;
            if (nearest < 0) overlapping++;
            else
            {
                hist[Math.Min(4, nearest)]++;
                if (nearest == 0) touching++;
            }
        }

        int onWater = placed.Count(p => p.Cells.Any(c => map.TryGetCell(c, out var m) && m.HasWater));
        int onStreet = placed.Count(p => p.Cells.Any(streets.Contains));
        var blocked = new HashSet<CellCoord>(placed.SelectMany(p => p.Cells));
        foreach (var cell in map.Cells) if (cell.HasWater && !streets.Contains(cell.Coord)) blocked.Add(cell.Coord); // vau da rua passa
        var main = LargestFreeComponent(map.Width, map.Height, blocked);
        int doorsBlocked = placed.Count(p => !main.Contains(p.DoorOutside));
        int doorsOffStreet = streets.Count == 0 ? placed.Count : placed.Count(p => !FrontReachesStreet(p, streets, blocked));

        int footprintCells = placed.Sum(p => p.Cells.Count);
        double area = 0;
        foreach (var city in world.Cities)
        {
            var (b, _) = CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id));
            area += b.Width * b.Height;
        }
        double extent = 0;
        if (placed.Count > 0)
        {
            var all = placed.SelectMany(p => p.Cells).ToList();
            extent = (all.Max(c => c.X) - all.Min(c => c.X) + 1.0) * (all.Max(c => c.Y) - all.Min(c => c.Y) + 1.0);
        }

        var work = world.Workplaces.Select(w => w.Location).ToList();
        var water = map.Cells.Where(c => c.HasWater).Select(c => c.Coord).ToList();
        var homes = world.Households.Select(h => h.Location).ToList();
        double MeanNearest(List<CellCoord> targets) => homes.Count == 0 || targets.Count == 0 ? double.NaN
            : homes.Average(h => targets.Min(t => Math.Sqrt((double)(h.X - t.X) * (h.X - t.X) + (double)(h.Y - t.Y) * (h.Y - t.Y))));

        // Quintal por faixa de meios e por ofício (AD-173)
        int[] withCroft = new int[5], total = new int[5]; // pobre, meio, remediado, lavrador, artesão
        double lotCells = 0;
        foreach (var p in placed)
        {
            lotCells += p.Cells.Count + p.Croft * 3;
            var household = world.Households.FirstOrDefault(h => h.Dwelling == p.Building.Id && !h.IsEmpty);
            if (household is null) continue;
            var members = household.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Select(n => n!).ToList();
            if (members.Count == 0) continue;
            double perHead = members.Sum(n => (double)n.Wallet.Amount) / members.Count;
            double median = LivingWorld.Simulation.Cities.Layout.LotProfiles.MedianMeansPerHead(world, household.City);
            int band = median > 0 && perHead < median * 0.5 ? 0 : median > 0 && perHead > median * 2 ? 2 : 1;
            bool crafts = members.Any(n => LivingWorld.Simulation.Cities.Layout.LotProfiles.IsCraftProfession(world, household.City, n.Profession.Id));
            foreach (int k in new[] { band, crafts ? 4 : 3 })
            {
                total[k]++;
                if (p.Croft > 0) withCroft[k]++;
            }
        }
        string Share(int k) => total[k] == 0 ? "-" : $"{withCroft[k]}/{total[k]}";
        var surfaceCounts = new int[4];
        foreach (var city in world.ActiveCities())
            foreach (var road in CityStreets.RoadsOf(world, city))
                surfaceCounts[(int)road.Surface]++;

        return new CityLayoutStats(
            placed.Count, hist, touching, onWater, overlapping, area == 0 ? 0 : footprintCells / area,
            extent == 0 ? 0 : footprintCells / extent, doorsBlocked, doorsOffStreet, onStreet, MeanNearest(work), MeanNearest(water),
            streets.Count)
        {
            CroftShares = $"pobre={Share(0)} meio={Share(1)} remediado={Share(2)} lavrador={Share(3)} artesao={Share(4)}",
            MeanLotCells = placed.Count == 0 ? 0 : lotCells / placed.Count,
            Surfaces = string.Join('/', surfaceCounts),
        };
    }

    /// <summary>A porta chega a uma rua a pé: da soleira, por células sem prédio nem água, em até 10 passos (quintal da
    /// frente ou caminho).</summary>
    private static bool FrontReachesStreet(Placed p, HashSet<CellCoord> streets, HashSet<CellCoord> blocked)
    {
        if (blocked.Contains(p.DoorOutside)) return false;
        var seen = new HashSet<CellCoord> { p.DoorOutside };
        var frontier = new List<CellCoord> { p.DoorOutside };
        for (int step = 0; step <= 10 && frontier.Count > 0; step++)
        {
            var next = new List<CellCoord>();
            foreach (var cell in frontier)
            {
                if (streets.Contains(cell)) return true;
                foreach (var n in Neighbors4(cell))
                    if (!blocked.Contains(n) && seen.Add(n)) next.Add(n);
            }
            frontier = next;
        }
        return false;
    }

    private static HashSet<CellCoord> LargestFreeComponent(int width, int height, HashSet<CellCoord> blocked)
    {
        var seen = new HashSet<CellCoord>();
        HashSet<CellCoord> best = [];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var start = new CellCoord(x, y);
                if (blocked.Contains(start) || seen.Contains(start)) continue;
                var comp = new HashSet<CellCoord> { start };
                var queue = new Queue<CellCoord>([start]);
                seen.Add(start);
                while (queue.Count > 0)
                    foreach (var n in Neighbors4(queue.Dequeue()))
                        if (n.X >= 0 && n.Y >= 0 && n.X < width && n.Y < height && !blocked.Contains(n) && seen.Add(n))
                        {
                            comp.Add(n);
                            queue.Enqueue(n);
                        }
                if (comp.Count > best.Count) best = comp;
            }
        return best;
    }

    /// <summary>Mapa em texto do entorno da cidade: # planta, D porta, = rua, ~ água, . livre.</summary>
    public static string Ascii(WorldState world, int margin = 2)
    {
        var placed = PlacedBuildings(world);
        if (placed.Count == 0) return "(sem prédios)";
        var streets = CityStreets.AllStreetCells(world);
        var all = placed.SelectMany(p => p.Cells).ToList();
        int minX = Math.Max(0, all.Min(c => c.X) - margin), maxX = Math.Min(world.Map.Width - 1, all.Max(c => c.X) + margin);
        int minY = Math.Max(0, all.Min(c => c.Y) - margin), maxY = Math.Min(world.Map.Height - 1, all.Max(c => c.Y) + margin);
        var doors = new HashSet<CellCoord>();
        foreach (var p in placed)
            doors.Add(p.Cells.First(c => Neighbors4(c).Contains(p.DoorOutside)));
        var cells = placed.SelectMany(p => p.Cells).ToHashSet();
        var sb = new StringBuilder();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var c = new CellCoord(x, y);
                char ch = doors.Contains(c) ? 'D' : cells.Contains(c) ? '#' : streets.Contains(c) ? '='
                    : world.Map.TryGetCell(c, out var m) && m.HasWater ? '~' : '.';
                sb.Append(ch);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public string Line(string label) => string.Create(CultureInfo.InvariantCulture,
        $"{label} predios={Buildings} folga0={GapHistogram[0]} folga1={GapHistogram[1]} folga2={GapHistogram[2]} folga3={GapHistogram[3]} folga4+={GapHistogram[4]} " +
        $"encostados={Touching} sobrepostos={Overlapping} agua={OnWater} densBounds={FootprintDensity:F2} densExtensao={ExtentDensity:F2} " +
        $"portaSemCaminho={DoorsBlocked} portaSemRua={DoorsOffStreet} sobreRua={OnStreet} ruas={StreetCells} casaTrabalho={MeanHomeToWork:F1} casaAgua={MeanHomeToWater:F1} " +
        $"quintal[{CroftShares}] lote={MeanLotCells:F1} ruaTrilha/Terra/Cascalho/Pedra={Surfaces}");
}
