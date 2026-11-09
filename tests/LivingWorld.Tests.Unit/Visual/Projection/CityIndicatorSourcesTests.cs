using System.Reflection;
using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Visual.Projection;

/// <summary>AD-123: todo indicador de <see cref="CityIndicators"/> declara de onde vem. A lista de fontes
/// mora em <see cref="CityIndicatorSources"/> (produção); este teste só confere a lista contra o tipo.</summary>
public class CityIndicatorSourcesTests
{
    private static PropertyInfo[] Indicators() =>
        typeof(CityIndicators).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract").ToArray();

    [Fact]
    public void Every_indicator_has_a_declared_source()
    {
        var undeclared = Indicators().Select(p => p.Name)
            .Where(n => !CityIndicatorSources.ByIndicator.ContainsKey(n)).ToList();

        Assert.True(undeclared.Count == 0, "indicador sem fonte declarada: " + string.Join(", ", undeclared));
    }

    [Fact]
    public void No_declared_source_is_orphaned_from_the_indicators_type()
    {
        var names = Indicators().Select(p => p.Name).ToHashSet();

        Assert.All(CityIndicatorSources.ByIndicator.Keys, k => Assert.Contains(k, names));
    }

    [Fact]
    public void A_measured_source_names_a_real_query_and_a_pending_one_names_an_issue_and_a_nullable_field()
    {
        foreach (var property in Indicators())
        {
            var source = CityIndicatorSources.ByIndicator[property.Name];
            if (source.IsMeasured)
            {
                Assert.NotNull(typeof(CityPopulationQuery).GetMethod(source.Query!, BindingFlags.Public | BindingFlags.Static));
                Assert.Null(source.PendingIssue);
            }
            else
            {
                Assert.Matches(@"^#\d+$", source.PendingIssue);
                Assert.NotNull(Nullable.GetUnderlyingType(property.PropertyType));
            }
        }
    }

    [Fact]
    public void Unmeasured_indicators_stay_null_even_in_a_city_with_completed_buildings()
    {
        var world = ScenarioRunner.Create(seed: 31, initialPopulation: 2).World;
        var city = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, new AggregatePopulationPool(2, 100, 120));
        world.AddCity(city);
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0));
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 2, completedAtTick: 0));

        var indicators = CityProjector.Build(world, city.Id).Value!.Indicators;

        foreach (var property in Indicators().Where(p => !CityIndicatorSources.ByIndicator[p.Name].IsMeasured))
            Assert.Null(property.GetValue(indicators));
    }
}
