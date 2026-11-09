using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Visual.Scope;

/// <summary>AD-123: o indicador <c>health</c> do delta de escopo é o mesmo número de
/// <see cref="CityPopulationQuery.Health"/> (a média), não a soma.</summary>
public class LivingScopeIndicatorsTests
{
    [Fact]
    public void The_health_indicator_of_a_city_scope_equals_the_query_mean_for_a_multi_resident_city()
    {
        // Conta à mão: moradores 70 e 51 + pool (3, soma 200) = 321 / 5 = 64,2 (a soma seria 321).
        var world = ScenarioRunner.Create(seed: 41, initialPopulation: 2).World;
        var city = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, new AggregatePopulationPool(3, 0, 200));
        world.AddCity(city);
        var npcs = world.Npcs.ToList();
        npcs[0].JoinCity(city.Id);
        npcs[0].SetHealth(70);
        npcs[1].JoinCity(city.Id);
        npcs[1].SetHealth(51);

        var state = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()), []);

        double health = state.Indicators.Single(i => i.Key == "health").Value;
        Assert.Equal(CityPopulationQuery.Health(world, city.Id), health);
        Assert.Equal(64.2, health, precision: 9);
    }
}
