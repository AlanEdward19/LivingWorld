using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

public class PopulationSeederTests
{
    [Fact]
    public void Seeded_households_never_spawn_outside_the_citys_own_computed_bounds()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 0);
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);

        // Raio fixo (2 células) descasava do footprint dinâmico assim que a população ficava
        // pequena o bastante pra produzir um lado menor que 5 (LIVE-POLISH: usuário via NPC "em
        // cima" da cidade no mapa-múndi, sem dar pra clicar — IsNpcInScope só mostra como externo
        // quem está fora dos bounds calculados pra mesma população).
        const int count = 36;
        PopulationSeeder.SeedInitial(world, count, ScenarioRunner.DefaultCulture, city.Location, city.Id);

        var (bounds, _) = SpatialBoundsResolver.ResolveCity(city, count, world.Map.Width, world.Map.Height);
        Assert.All(world.Households, household => Assert.True(
            bounds.Contains(household.Location),
            $"household em {household.Location} fora dos bounds {bounds} calculados pra população {count}"));
    }
}
