using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Performance;

/// <summary>PERF-05 e PERF-06 (phase-09-scale): o índice de mercado e o de população por região são só aceleração, então cada um
/// é comparado com a definição direta que substitui (varredura por NPC) depois de o mundo andar, de gente morrer e de um mercado entrar.</summary>
public class MarketAndRegionIndexTests
{
    private static Workplace? BruteNearest(WorldState world, CellCoord origin)
    {
        Workplace? best = null;
        long bestTicks = long.MaxValue;
        foreach (var market in world.Workplaces.Where(w => world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id)))
        {
            long ticks = TravelResolution.TicksBetween(world.Map, origin, market.Location);
            if (ticks < bestTicks || (ticks == bestTicks && market.Id.Value < best!.Id.Value))
            {
                bestTicks = ticks;
                best = market;
            }
        }
        return best;
    }

    private static void AssertNearestMatches(WorldState world)
    {
        var index = MarketIndex.BuildForTick(world);
        var origins = world.Npcs.Where(n => n.IsAlive).Select(n => n.CurrentLocation).Distinct().ToList();
        Assert.NotEmpty(origins);
        foreach (var origin in origins)
            Assert.Equal(BruteNearest(world, origin)?.Id, index.NearestTo(origin)?.Id);
    }

    [Fact]
    public void The_market_index_names_the_same_nearest_market_as_a_scan_and_follows_a_new_market()
    {
        var (world, clock) = ScenarioRunner.Create(7);
        clock.Run(world, 200 * 24);
        AssertNearestMatches(world);

        var origin = world.Npcs.First(n => n.IsAlive).CurrentLocation;
        var market = world.Workplaces.First(w => world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id));
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), market.LocationType, origin, maxVacancies: 1, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: LivingWorld.Domain.Shared.Money.Zero,
            prices: new Dictionary<ResourceType, long>()));

        AssertNearestMatches(world);
        Assert.Equal(origin, MarketIndex.BuildForTick(world).NearestTo(origin)!.Location); // o novo, na própria célula, é o mais perto
    }

    [Fact]
    public void The_region_population_index_matches_a_scan_before_and_after_deaths()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 120 * 24);
        AssertRegionCountsMatch(world);

        foreach (var npc in world.Npcs.Where(n => n.IsAlive).Take(10).ToList())
            npc.Die(world.CurrentDate);
        AssertRegionCountsMatch(world);
    }

    private static void AssertRegionCountsMatch(WorldState world)
    {
        var index = RegionPopulationIndex.BuildForTick(world);
        foreach (var region in world.Map.Regions)
        {
            long expected = world.Npcs.Count(n => n.IsAlive && world.Map.RegionOf(n.CurrentLocation) == region.Id);
            Assert.Equal(expected, index.AliveInRegion(region.Id));
        }
    }

    [Fact]
    public void The_vacancy_index_names_the_same_first_open_workplace_as_a_scan_by_type_and_city()
    {
        var (world, clock) = ScenarioRunner.Create(5);
        clock.Run(world, 150 * 24);
        var index = LivingWorld.Simulation.Economy.Labor.VacancyIndex.BuildForTick(world);

        foreach (var workplace in world.Workplaces)
        {
            int type = workplace.LocationType.Id;
            var open = world.Workplaces.Where(w => w.LocationType.Id == type && w.MaxVacancies - w.Employees.Count > 0).OrderBy(w => w.Id.Value);
            Assert.Equal(open.FirstOrDefault()?.Id, index.FirstWorkplaceWithVacancy(type)?.Id);
            Assert.Equal(open.FirstOrDefault(w => w.City == workplace.City)?.Id, index.FirstWorkplaceWithVacancy(type, workplace.City)?.Id);
        }
    }
}
