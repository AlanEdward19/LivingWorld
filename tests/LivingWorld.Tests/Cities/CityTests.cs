using LivingWorld.Domain;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T1 (CITY-01/CITY-04): <see cref="City"/> — construtível só pelo construtor
/// único de reidratação, e Materialize/Dematerialize movem o <see cref="AggregatePopulationPool"/>
/// de forma simétrica.</summary>
public class CityTests
{
    private static City MakeCity(AggregatePopulationPool? pool = null) => new(
        new CityId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
        new CellCoord(1, 2), foundedAtTick: 10, foundedFromCityId: null,
        aggregatePool: pool ?? new AggregatePopulationPool(5, 500, 400));

    [Fact]
    public void Constructor_round_trips_every_field()
    {
        var founderId = new CityId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var city = new City(
            new CityId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            new CellCoord(3, 4), foundedAtTick: 42, foundedFromCityId: founderId,
            aggregatePool: new AggregatePopulationPool(7, 70, 21));

        Assert.Equal(new CellCoord(3, 4), city.Location);
        Assert.Equal(42, city.FoundedAtTick);
        Assert.Equal(founderId, city.FoundedFromCityId);
        Assert.Equal(new AggregatePopulationPool(7, 70, 21), city.AggregatePool);
    }

    [Fact]
    public void Materialize_decrements_count_and_subtracts_wealth_and_health_sums()
    {
        var city = MakeCity(new AggregatePopulationPool(5, 500, 400));

        var result = city.Materialize(wealth: 100, health: 80);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(4, 400, 320), city.AggregatePool);
    }

    [Fact]
    public void Materialize_fails_and_leaves_pool_unchanged_when_count_is_zero()
    {
        var city = MakeCity(AggregatePopulationPool.Empty);

        var result = city.Materialize(wealth: 10, health: 10);

        Assert.False(result.IsSuccess);
        Assert.Equal(AggregatePopulationPool.Empty, city.AggregatePool);
    }

    [Fact]
    public void Dematerialize_increments_count_and_adds_wealth_and_health_sums()
    {
        var city = MakeCity(new AggregatePopulationPool(4, 400, 320));

        var result = city.Dematerialize(wealth: 100, health: 80);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(5, 500, 400), city.AggregatePool);
    }

    [Fact]
    public void Materialize_then_dematerialize_round_trips_pool_to_original_state()
    {
        var city = MakeCity(new AggregatePopulationPool(5, 500, 400));

        city.Materialize(wealth: 100, health: 80);
        city.Dematerialize(wealth: 100, health: 80);

        Assert.Equal(new AggregatePopulationPool(5, 500, 400), city.AggregatePool);
    }
}
