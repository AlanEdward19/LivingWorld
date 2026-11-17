using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-179, critério de aceite no jogo: os três mundos do menu (seed 2) andam de verdade. Em 3 meses há rede de trilhas que
/// liga as casas ao trabalho e ao mercado; em 1 ano parte virou terra batida; há gente em rota nas horas de ida e de volta; ninguém
/// morre de fome nem de sede; toda casa tem rota. Antes (fixture sem economia) a cidade média tinha ~1 viagem por pessoa-mês, chão
/// nenhum, e a metrópole morria de sede em dois meses.</summary>
[Trait("Category", "Scenario")]
public class MenuWorldsLiveTests
{
    [Theory]
    [InlineData("vila-pequena")]
    [InlineData("cidade-media")]
    [InlineData("grande-metropole")]
    public void Menu_world_walks_wears_trails_and_nobody_dies_of_the_distances(string period)
    {
        var (world, clock) = MenuWorlds.Load(period, 2);
        var probe = new MenuWorldProbe(world);
        var sampled = new WorldClock(clock.Systems, clock.MaxIterationsPerTick, probe);
        long hoursPerMonth = world.Calendar.HoursPerMonth;
        Assert.True(world.EconomyRules.Enabled, "o período do menu tem economia");
        Assert.True(world.EmergenceRules.Enabled, "o período do menu tem tecnologia e descoberta");
        Assert.NotEmpty(world.Workplaces);

        for (long h = 0; h < 3 * hoursPerMonth; h++) { sampled.Run(world, 1); probe.SampleHour(); }
        var ground = world.Ground!;
        int trails = 0;
        for (int i = 0; i < ground.Count; i++) if (ground.SurfaceAt(i) >= GroundSurface.Trail) trails++;
        Assert.True(trails >= 20, $"{period}: só {trails} células de trilha em 3 meses");
        // a rede liga as casas ao trabalho: quase toda ida de casa ao trabalho passa por caminho (trilha ou melhor)
        var (touching, legs) = LegsOnNetwork(world);
        Assert.True(legs > 0 && touching >= 0.8 * legs, $"{period}: {touching} de {legs} idas passam por caminho");
        Assert.True(probe.RouteMorning > 0 && probe.RouteEvening > 0, $"{period}: ida {probe.RouteMorning}, volta {probe.RouteEvening}");

        for (long h = 0; h < 9 * hoursPerMonth; h++) { sampled.Run(world, 1); probe.SampleHour(); }
        int dirt = 0;
        for (int i = 0; i < ground.Count; i++) if (ground.SurfaceAt(i) >= GroundSurface.Dirt) dirt++;
        Assert.True(dirt > 0, $"{period}: nenhuma terra batida em 1 ano");
        Assert.Equal(0, probe.Starved);
        Assert.Equal(0, probe.Dehydrated);
        Assert.Equal(0, HousesWithoutRoute(world));
        Assert.True(world.AliveNpcIndex.Alive.Count > 0);
    }

    /// <summary>Idas casa → trabalho (rota de hoje) que passam por ao menos uma célula de caminho (trilha ou melhor).</summary>
    private static (int Touching, int Legs) LegsOnNetwork(WorldState world)
    {
        var ground = world.Ground!;
        int touching = 0, legs = 0;
        foreach (var npc in world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value))
        {
            if (npc.Household is not { } h || world.FindHousehold(h) is not { } home || npc.Employer is not { } e
                || world.FindWorkplace(e) is not { } work || work.Location == home.Location) continue;
            legs++;
            if (StreetRoutes.PathOf(world, home.Location, work.Location).Any(c => ground.SurfaceAt(c) >= GroundSurface.Trail)) touching++;
        }
        return (touching, legs);
    }

    private static int HousesWithoutRoute(WorldState world)
    {
        var planned = CityStreets.AllStreetCells(world);
        int none = 0;
        foreach (var building in world.Buildings)
        {
            if (building.Position is null) continue;
            var kind = StreetRoutes.AccessOf(world, building, planned);
            if (kind == "none") none++;
            else if (world.FindActiveCity(building.City) is { } city && !double.IsFinite(StreetRoutes.CostOf(world, building.Position.Value, city.Location).Cost)) none++;
        }
        return none;
    }
}
