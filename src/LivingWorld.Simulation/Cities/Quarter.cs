using System.Runtime.CompilerServices;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>AD-192: o bairro denso visto pelo motor. A <b>sujeira</b> de uma casa (0–1) sai do que o mundo já tem: gente por andar
/// (apinhamento), a volta da casa tomada por outros prédios (parede-meia, viela), o escoamento (sem quintal, mais baixa que a vizinhança,
/// sem calçamento na porta) e a água (longe, ou fonte cercada de casas). A <b>aversão</b> de cada pessoa à parte suja da cidade sai de
/// quem ela é (de lá ou de fora, traços), de onde mora (quem mora na sujeira se acostuma) e dos crimes que conhece de quem mora lá.
/// Tudo função pura do estado (grade de obstáculos da rota, mapa, chão, casas, casos), sem sorteio; a parte fixa do lugar fica num
/// cache refeito quando a grade muda.</summary>
internal static class Quarter
{
    private sealed class Cache
    {
        public object? State;
        public long Version = long.MinValue;
        public readonly Dictionary<long, double> Place = [];
        public readonly Dictionary<int, int> SourceHouses = [];
    }

    private static readonly ConditionalWeakTable<WorldState, Cache> Caches = new();

    private static Cache CacheOf(WorldState world)
    {
        var cache = Caches.GetOrCreateValue(world);
        object? state = world.StreetsOn ? StreetRoutes.StateOf(world) : world.Map;
        long version = world.StreetsOn ? ((StreetRouteState)state).RouteVersion : Building.LayoutVersion;
        if (!ReferenceEquals(cache.State, state) || cache.Version != version)
        {
            cache.State = state;
            cache.Version = version;
            cache.Place.Clear();
            cache.SourceHouses.Clear();
        }
        return cache;
    }

    /// <summary>A sujeira do lugar de um prédio com posição (sem o apinhamento): paredes coladas, escoamento e água.</summary>
    public static double PlaceSqualor(WorldState world, Building building)
    {
        if (building.Position is not { } at) return 0;
        var cache = CacheOf(world);
        if (cache.Place.TryGetValue(building.Id.Value, out double known)) return known;
        return cache.Place[building.Id.Value] = SiteSqualor(world, at, BuildingFootprintGenerator.Generate(building), building.CroftCells ?? 0, building.Id.Value);
    }

    /// <summary>A sujeira de um lote (casa que ainda não existe, ou que existe: <paramref name="self"/> é o id dela).</summary>
    public static double SiteSqualor(WorldState world, CellCoord origin, IReadOnlyList<FootprintCell> footprint, int croft, long self = -1)
    {
        var rules = world.StreetRules;
        double close = 0, drain = croft > 0 ? 0 : 0.5;
        var (door, facing) = Layout.LotGeometry.DoorOf(footprint);
        var doorstep = new CellCoord(origin.X + door.X + facing.X, origin.Y + door.Y + facing.Y);
        if (world.StreetsOn)
        {
            var state = StreetRoutes.StateOf(world);
            int w = state.W, h = state.H;
            var own = new List<int>(footprint.Count);
            int ownAlt = 0;
            foreach (var c in footprint)
            {
                int x = origin.X + c.Cell.X, y = origin.Y + c.Cell.Y;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                own.Add(y * w + x);
                ownAlt += state.Alt[y * w + x];
            }
            double meanAlt = own.Count == 0 ? 0 : ownAlt / (double)own.Count;
            var around = new List<int>(24);
            foreach (var c in footprint)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = origin.X + c.Cell.X + dx, y = origin.Y + c.Cell.Y + dy;
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        int i = y * w + x;
                        if (!own.Contains(i) && !around.Contains(i)) around.Add(i);
                    }
            int ring = around.Count, taken = 0, higher = 0;
            foreach (int i in around)
            {
                int owner = state.Owner[i];
                if (owner != 0 && state.OwnerId[owner] != self) taken++;
                if (state.Alt[i] > meanAlt) higher++;
            }
            if (ring > 0)
            {
                close = taken / (double)ring;
                drain += 0.5 * higher / ring; // a água da chuva e o despejo correm para a casa mais baixa
            }
            int stepCell = doorstep.X >= 0 && doorstep.Y >= 0 && doorstep.X < w && doorstep.Y < h ? doorstep.Y * w + doorstep.X : -1;
            if (stepCell >= 0 && world.Ground!.SurfaceAt(stepCell) >= GroundSurface.Gravel) drain *= 0.5; // calçamento escoa
        }
        double water = Water(world, doorstep);
        double total = rules.SqualorCloseWeight * close + rules.SqualorDrainWeight * Math.Min(1, drain) + rules.SqualorWaterWeight * water;
        double weights = rules.SqualorCloseWeight + rules.SqualorDrainWeight + rules.SqualorWaterWeight + rules.SqualorCrowdWeight;
        return weights <= 0 ? 0 : total / weights;
    }

    /// <summary>A água da casa (0–1): longe (a ida a pé passa das horas de "longe": a água vai toda para beber) e suja (a fonte com
    /// muitas casas em volta: quem tira e quem despeja no mesmo lugar). Sem fonte da natureza ao alcance, 1 na distância.</summary>
    private static double Water(WorldState world, CellCoord doorstep)
    {
        var rules = world.StreetRules;
        var source = Economy.Supply.WaterFetch.SourceFor(world, doorstep);
        if (source is not { } s) return 0.5;
        double far = Math.Clamp(s.Ticks / Math.Max(0.1, rules.SqualorWaterFarHours), 0, 1);
        double dirty = rules.SourceCrowdHouses <= 0 ? 0 : Math.Clamp(HousesAround(world, s.Cell) / (double)rules.SourceCrowdHouses, 0, 1);
        return 0.5 * far + 0.5 * dirty;
    }

    private static int HousesAround(WorldState world, CellCoord cell)
    {
        var cache = CacheOf(world);
        int key = cell.Y * world.Map.Width + cell.X;
        if (cache.SourceHouses.TryGetValue(key, out int known)) return known;
        int r = world.StreetRules.SourceCrowdRadius, count = 0;
        foreach (var b in world.Buildings)
        {
            if (b.Position is not { } p || b.UnderWork is not null || !CityOccupancy.IsDwelling(world, b.BuildingTypeId)) continue;
            if (Math.Max(Math.Abs(p.X - cell.X), Math.Abs(p.Y - cell.Y)) <= r) count++;
        }
        return cache.SourceHouses[key] = count;
    }

    /// <summary>A sujeira da casa de uma família: a do lugar mais o apinhamento (gente de todas as famílias que moram no prédio por
    /// andar, acima de <see cref="StreetRules.CrowdPersonsPerFloor"/>). Sem casa (ao relento), 1.</summary>
    public static double HomeSqualor(WorldState world, Household household)
    {
        if (household.Dwelling is not { } id || world.FindBuilding(id) is not { } house) return household.IsHomeless ? 1 : 0;
        var rules = world.StreetRules;
        int people = 0;
        foreach (var other in world.Households)
            if (other.Dwelling == id) people += other.Members.Count;
        double perFloor = people / (double)Math.Max(1, DwellingCapacity.Of(world, house));
        double crowd = rules.CrowdPersonsPerFloor <= 0 ? 0 : Math.Clamp((perFloor - rules.CrowdPersonsPerFloor) / rules.CrowdPersonsPerFloor, 0, 1);
        double weights = rules.SqualorCloseWeight + rules.SqualorDrainWeight + rules.SqualorWaterWeight + rules.SqualorCrowdWeight;
        return PlaceSqualor(world, house) + (weights <= 0 ? 0 : rules.SqualorCrowdWeight * crowd / weights);
    }

    /// <summary>Fator da higiene sobre os casos do ambiente: 1 + ganho × (sujeira − referência), entre o piso e o teto.</summary>
    public static double HygieneFactor(StreetRules rules, double squalor) =>
        Math.Clamp(1 + rules.HygieneGain * (squalor - rules.HygieneReference), rules.HygieneFloor, rules.HygieneMax);

    /// <summary>Aversão (−1 curiosidade … +1 medo e nojo) de <paramref name="npc"/> à parte suja da cidade <paramref name="city"/>.</summary>
    public static double Aversion(WorldState world, Npc npc, City city, CityBounds bounds)
    {
        var rules = world.StreetRules;
        bool local = bounds.Contains(npc.BirthLocation);
        var p = npc.Personality;
        double value = (local ? rules.RegardLocal : rules.RegardOutsider)
            + rules.RegardTraitWeight * ((p.RiskAversion - 50) / 50.0 + (50 - p.EmotionalStability) / 50.0 - (p.Openness - 50) / 50.0);
        if (npc.Household is { } h && world.FindHousehold(h) is { } home && home.City == city.Id)
            value -= rules.RegardHabit * Math.Min(1, HomeSqualor(world, home));
        value += Math.Min(rules.RegardCrimeMax, rules.RegardPerCrime * KnownCrimesOfDense(world, npc, city));
        return Math.Clamp(value, -1, 1);
    }

    /// <summary>Casos que a pessoa conhece (vítima ou pelo boato) na cidade, cujo acusado (o que ela acredita) mora em casa suja.</summary>
    private static int KnownCrimesOfDense(WorldState world, Npc npc, City city)
    {
        if (world.Crime is not { } ledger) return 0;
        int count = 0;
        long me = npc.Id.Value;
        foreach (var c in ledger.Cases)
        {
            if (c.City != city.Id.Value) continue;
            long accused = c.Victim == me ? c.Accused : -1;
            if (accused < 0)
                foreach (var k in c.Knowers)
                    if (k.Npc == me) { accused = k.Accused; break; }
            if (accused < 0 || world.FindNpc(new LivingWorld.Domain.Shared.NpcId(accused)) is not { Household: { } hid }
                || world.FindHousehold(hid) is not { } home) continue;
            if (HomeSqualor(world, home) >= rules(world).DenseSqualor) count++;
        }
        return count;
        static StreetRules rules(WorldState w) => w.StreetRules;
    }

    /// <summary>Aversão da família: a média dos adultos (12+), sem a curiosidade (ninguém escolhe morar na sujeira por curiosidade).</summary>
    public static double HouseholdAversion(WorldState world, IReadOnlyList<Npc> members, City city, CityBounds bounds)
    {
        double sum = 0;
        int n = 0;
        foreach (var m in members)
        {
            if (m.AgeYears(world.CurrentDate) < 12) continue;
            sum += Aversion(world, m, city, bounds);
            n++;
        }
        return n == 0 ? 0 : Math.Max(0, sum / n);
    }

    /// <summary>Os prédios que encostam neste (parede-meia: alguma célula da volta é deles), em ordem de id.</summary>
    public static void WallNeighbours(WorldState world, Building building, List<long> into)
    {
        into.Clear();
        if (!world.StreetsOn || building.Position is not { } origin) return;
        var state = StreetRoutes.StateOf(world);
        foreach (var c in BuildingFootprintGenerator.Generate(building))
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = origin.X + c.Cell.X + dx, y = origin.Y + c.Cell.Y + dy;
                    if (x < 0 || y < 0 || x >= state.W || y >= state.H) continue;
                    int owner = state.Owner[y * state.W + x];
                    if (owner == 0) continue;
                    long id = state.OwnerId[owner];
                    if (id != building.Id.Value && !into.Contains(id)) into.Add(id);
                }
        into.Sort();
    }
}
