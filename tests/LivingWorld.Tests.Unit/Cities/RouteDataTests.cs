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
