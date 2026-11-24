using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-185: o cache de rotas não muda o mundo; a rota diz onde passa por cima; o lote diz a geometria do acesso e onde ele
/// desemboca; quem procura emprego vai à oficina do ofício mais perto de casa.</summary>
public class RouteDataTests
{
    [Fact]
    public void The_route_cache_does_not_change_the_world()
    {
        var (cached, clockA) = ScenarioRunner.Create(5);
        var (fresh, clockB) = ScenarioRunner.Create(5);
        fresh.RouteCacheOff = true;

        clockA.Run(cached, cached.Calendar.HoursPerMonth + 2 * cached.Calendar.HoursPerDay); // cruza a virada do mês (obstáculos refeitos)
        clockB.Run(fresh, fresh.Calendar.HoursPerMonth + 2 * fresh.Calendar.HoursPerDay);

        Assert.Equal(WorldSnapshot.CanonicalHash(cached), WorldSnapshot.CanonicalHash(fresh));
    }

    [Fact]
    public void The_network_leg_is_never_costlier_than_the_search_of_before()
    {
        var (world, clock) = ScenarioRunner.Create(4);
        clock.Run(world, world.Calendar.HoursPerMonth + 1); // rede da história e prédios já obstáculo
        var state = StreetRoutes.StateOf(world);
        var net = Enumerable.Range(0, state.Walk.Length).Where(i => state.Walk[i]).ToList();
        Assert.True(net.Count > 20);
        int compared = 0, backwards = 0;
        for (int k = 0; k < 200; k++)
        {
            int e = net[(k * 7919) % net.Count], x = net[(k * 104729 + 13) % net.Count];
            var hubs = state.Hubs.Where(h => state.NetId[h] >= 0).OrderBy(h => h).ToList();
            if (hubs.Count > 0 && k % 3 == 0) x = hubs[k % hubs.Count]; // para um polo: pelo campo de distância
            if (hubs.Count > 0 && k % 3 == 1) e = hubs[k % hubs.Count]; // de um polo: o campo ao contrário
            if (e == x) continue;
            double before = StreetRoutes.ReferenceLegCost(state, e, x);
            double now = StreetRoutes.LegCost(state, e, x);
            if (double.IsPositiveInfinity(before)) { Assert.True(double.IsPositiveInfinity(now)); continue; }
            Assert.True(now <= before + 1e-9, $"{e}->{x}: {now} > {before}");
            Assert.True(now >= before - 1e-6, $"{e}->{x}: {now} < {before} (a busca de antes era ótima)");
            compared++;
            if (e > x) backwards++; // AD-187: o trecho de volta é o de ida ao contrário
        }
        Assert.True(compared > 100);
        Assert.True(backwards > 20);
    }

    /// <summary>AD-187: "é longe de casa?" pelo piso do custo dá a mesma resposta que a conta inteira, dos dois lados, perto e longe.</summary>
    [Fact]
    public void Far_from_home_by_the_cost_floor_answers_like_the_full_route()
    {
        var (world, clock) = ScenarioRunner.Create(4, initialPopulation: 300);
        clock.Run(world, world.Calendar.HoursPerMonth + 1);
        int far = 0, near = 0;
        foreach (var npc in world.AliveNpcIndex.Alive.Take(60))
        {
            if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) continue;
            for (int k = 0; k < 12; k++)
            {
                int r = 1 + k * 3;
                var cell = new CellCoord(Math.Clamp(home.Location.X + (k % 2 == 0 ? r : -r), 0, world.Map.Width - 1),
                    Math.Clamp(home.Location.Y + (k % 3 == 0 ? r : -r / 2), 0, world.Map.Height - 1));
                if (cell == home.Location) continue;
                bool quick = LivingWorld.Simulation.Emergence.Mobility.FartherThan(world, npc, cell, home.Location, 1.0, 1);
                bool full = LivingWorld.Simulation.Emergence.Mobility.TravelTicks(world, npc, cell, home.Location, 1.0, weather: false) > 1;
                Assert.Equal(full, quick);
                if (full) far++; else near++;
            }
        }
        Assert.True(far > 20 && near > 20, $"longe {far}, perto {near}");
    }

    /// <summary>AD-187: o trecho de volta é o caminho de ida andado ao contrário: começa e termina nas pontas certas, cada passo é
    /// entre vizinhos da rede, e custa o mesmo que a busca de antes daquele lado.</summary>
    [Fact]
    public void The_way_back_is_the_way_there_walked_backwards()
    {
        var (world, clock) = ScenarioRunner.Create(4);
        clock.Run(world, world.Calendar.HoursPerMonth + 1);
        var state = StreetRoutes.StateOf(world);
        var net = Enumerable.Range(0, state.Walk.Length).Where(i => state.Walk[i]).ToList();
        int checkedLegs = 0;
        for (int k = 0; k < 80 && checkedLegs < 30; k++)
        {
            int a = net[(k * 7919) % net.Count], b = net[(k * 104729 + 13) % net.Count];
            int e = Math.Max(a, b), x = Math.Min(a, b);
            if (e == x || state.Comp[e] != state.Comp[x] || state.Hubs.Contains(e) || state.Hubs.Contains(x)) continue;
            var there = StreetRoutes.LegPath(state, x, e);
            var back = StreetRoutes.LegPath(state, e, x);
            Assert.Equal(Enumerable.Reverse(there), back);
            Assert.Equal(e, back[0]);
            Assert.Equal(x, back[^1]);
            for (int i = 0; i + 1 < back.Length; i++)
                Assert.True(Math.Abs(back[i] % state.W - back[i + 1] % state.W) <= 1 && Math.Abs(back[i] / state.W - back[i + 1] / state.W) <= 1);
            Assert.True(Math.Abs(StreetRoutes.LegCost(state, e, x) - StreetRoutes.ReferenceLegCost(state, e, x)) < 1e-9);
            checkedLegs++;
        }
        Assert.True(checkedLegs >= 10);
    }

    /// <summary>AD-187: pontas em pedaços diferentes da rede (ou fora dela) não têm trecho, sem busca; a busca de antes concorda.</summary>
    [Fact]
    public void Ends_in_different_pieces_of_the_network_have_no_leg()
    {
        var (world, clock) = ScenarioRunner.Create(4);
        clock.Run(world, world.Calendar.HoursPerMonth + 1);
        var state = StreetRoutes.StateOf(world);
        var firsts = Enumerable.Range(0, state.Walk.Length).Where(i => state.Walk[i]).GroupBy(i => state.Comp[i]).Select(g => g.First()).Take(6).ToList();
        if (firsts.Count < 2) return; // rede de um pedaço só: nada a comparar
        for (int i = 0; i + 1 < firsts.Count; i++)
        {
            Assert.True(double.IsPositiveInfinity(StreetRoutes.LegCost(state, firsts[i], firsts[i + 1])));
            Assert.True(double.IsPositiveInfinity(StreetRoutes.ReferenceLegCost(state, firsts[i], firsts[i + 1])));
        }
    }

    /// <summary>AD-187: a célula de caminho mais perto de cada célula do mapa (por onde se entra na rede) é a mesma da busca de antes
    /// (fila genérica, empate pela célula): o heap em vetores só muda a velocidade.</summary>
    [Fact]
    public void The_way_into_the_network_is_the_one_of_before()
    {
        var (world, clock) = ScenarioRunner.Create(4);
        clock.Run(world, world.Calendar.HoursPerMonth + 1);
        var state = StreetRoutes.StateOf(world);
        Assert.Contains(true, state.Net);
        int n = state.Net.Length;
        var dist = Enumerable.Repeat(double.PositiveInfinity, n).ToArray();
        var entry = Enumerable.Repeat(-1, n).ToArray();
        var queue = new PriorityQueue<int, (double, int)>();
        for (int i = 0; i < n; i++) if (state.Net[i]) { dist[i] = 0; entry[i] = i; queue.Enqueue(i, (0, i)); }
        (int, int, double)[] steps = [(1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1), (1, 1, Math.Sqrt(2)), (1, -1, Math.Sqrt(2)), (-1, 1, Math.Sqrt(2)), (-1, -1, Math.Sqrt(2))];
        while (queue.TryDequeue(out int at, out var pri))
        {
            if (pri.Item1 > dist[at]) continue;
            int x = at % state.W, y = at / state.W;
            foreach (var (dx, dy, len) in steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= state.W || ny >= state.H) continue;
                int m = ny * state.W + nx;
                double c = pri.Item1 + state.Base * len * (state.Terrain[at] + state.Terrain[m]) * 0.5 * (state.Factor[at] + state.Factor[m]) * 0.5;
                if (c >= dist[m]) continue;
                dist[m] = c; entry[m] = entry[at];
                queue.Enqueue(m, (c, m));
            }
        }
        Assert.Equal(entry, state.Entry);
    }

    [Fact]
    public void Over_indices_mark_only_cells_of_glued_buildings_crossed_on_the_way()
    {
        var world = ScenarioRunner.Create(3, initialPopulation: 300).World;
        var state = StreetRoutes.StateOf(world);
        // um caminho que atravessa uma célula de prédio colado (Kind 2) entre duas células livres
        int glued = Array.FindIndex(state.Kind, k => k == 2);
        if (glued < 0) return; // vila sem parede-meia: nada a marcar
        var cell = new CellCoord(glued % state.W, glued / state.W);
        var path = new List<CellCoord>();
        for (int x = Math.Max(0, cell.X - 3); x <= Math.Min(state.W - 1, cell.X + 3); x++) path.Add(new CellCoord(x, cell.Y));
        var free = path.Where(c => state.Owner[c.Y * state.W + c.X] == 0).ToList();
        if (free.Count < 2) return;
        var trip = new List<CellCoord> { free[0] };
        trip.AddRange(path.Where(c => c.X > free[0].X && c.X < free[^1].X));
        trip.Add(free[^1]);

        var over = StreetRoutes.OverIndicesOf(world, trip);

        Assert.Contains(trip.IndexOf(cell), over);
        Assert.All(over, i => Assert.Equal(2, state.Kind[trip[i].Y * state.W + trip[i].X]));
    }

    [Fact]
    public void The_lot_tells_the_access_geometry_from_the_doorstep_and_where_it_ends()
    {
        var (world, clock) = ScenarioRunner.Create(4);
        clock.Run(world, world.Calendar.HoursPerMonth + 1); // a rede da história e os prédios já obstáculo
        var city = world.ActiveCities().First();
        int checkedLots = 0;
        foreach (var building in world.Buildings.Where(b => b.City == city.Id && b.Position is not null).OrderBy(b => b.Id.Value).Take(20))
        {
            var lot = CityStreets.LotOf(world, city, building, building.Position!.Value, building.CroftCells ?? 0);
            if (lot.Access is null or "none") continue;
            checkedLots++;
            Assert.NotEmpty(lot.AccessCells);
            Assert.Contains(lot.AccessState, new[] { "reserved", "planned", "trodden" });
            Assert.Equal(lot.Access is "stair" or "roof", lot.AccessViaBuildingId is not null);
            var last = lot.AccessCells[^1];
            if (lot.AccessState is "planned" or "trodden") Assert.NotEqual(GroundSurface.None, world.Ground!.SurfaceAt(last));
        }
        Assert.True(checkedLots > 0);
    }

    [Fact]
    public void A_job_seeker_takes_the_vacancy_nearest_home_not_the_first_one()
    {
        var on = HiredDistances(StreetRules.Default);
        var off = HiredDistances(StreetRules.Default with { NearestJob = false });

        Assert.True(on < off, $"perto {on:F2} h, primeira {off:F2} h");
    }

    /// <summary>Metrópole de 400 com quatro fazendas iguais em cantos opostos: horas médias casa-fazenda dos contratados no mês 1.</summary>
    private static double HiredDistances(StreetRules streets)
    {
        var (world, clock) = ScenarioRunner.Create(2, initialPopulation: 400, cityLayout: CityLayoutRules.Default with { Streets = streets });
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var city = world.ActiveCities().First();
        var bounds = world.Map;
        foreach (var (dx, dy) in new[] { (-1, -1), (1, 1), (1, -1) })
        {
            var at = new CellCoord(Math.Clamp(city.Location.X + dx * 25, 2, bounds.Width - 8), Math.Clamp(city.Location.Y + dy * 25, 2, bounds.Height - 8));
            LivingWorld.Simulation.Economy.WorkplacePlacer.Place(world, new LivingWorld.Simulation.Economy.InitialWorkplace(
                farm.LocationType, at, farm.MaxVacancies, new Dictionary<ResourceType, long>(), farm.Treasury,
                new Dictionary<ResourceType, long>(farm.Prices)));
        }
        clock.Run(world, 3 * world.Calendar.HoursPerDay);
        double sum = 0; int n = 0;
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.Employer is { } e && world.FindWorkplace(e) is { LocationType.Id: 1 } place && npc.Household is { } h && world.FindHousehold(h) is { } home)
            { sum += StreetRoutes.CostOf(world, home.Location, place.Location).Cost; n++; }
        return n == 0 ? double.MaxValue : sum / n;
    }
}
