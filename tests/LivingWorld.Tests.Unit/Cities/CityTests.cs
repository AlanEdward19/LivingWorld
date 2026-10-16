using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>Fase 8, T1 (CITY-01/CITY-04): <see cref="City"/> — construtível só pelo construtor
/// único de reidratação, e Materialize/Dematerialize movem o <see cref="AggregatePopulationPool"/>
/// de forma simétrica.</summary>
public class CityTests
{
    // T50: PoolNpcIds.Count precisa sempre bater com AggregatePool.Count — gera ids sequenciais
    // pra manter a invariante nos cenários de teste que não se importam com QUAIS ids são.
    private static City MakeCity(AggregatePopulationPool? pool = null)
    {
        var resolvedPool = pool ?? new AggregatePopulationPool(5, 500, 400);
        var poolNpcIds = Enumerable.Range(1, (int)resolvedPool.Count).Select(i => new NpcId(i)).ToList();
        return new(
            new CityId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            new CellCoord(1, 2), foundedAtTick: 10, foundedFromCityId: null,
            aggregatePool: resolvedPool, poolNpcIds: poolNpcIds);
    }

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
        var id = city.PoolNpcIds[0];

        var result = city.Materialize(id, wealth: 100, health: 80);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(4, 400, 320), city.AggregatePool);
        Assert.DoesNotContain(id, city.PoolNpcIds);
    }

    [Fact]
    public void Materialize_fails_and_leaves_pool_unchanged_when_count_is_zero()
    {
        var city = MakeCity(AggregatePopulationPool.Empty);

        var result = city.Materialize(new NpcId(1), wealth: 10, health: 10);

        Assert.False(result.IsSuccess);
        Assert.Equal(AggregatePopulationPool.Empty, city.AggregatePool);
    }

    [Fact]
    public void Materialize_fails_and_leaves_pool_unchanged_when_id_is_not_reserved_in_this_pool()
    {
        var city = MakeCity(new AggregatePopulationPool(5, 500, 400));

        var result = city.Materialize(new NpcId(9999), wealth: 10, health: 10);

        Assert.False(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(5, 500, 400), city.AggregatePool);
    }

    [Fact]
    public void Dematerialize_increments_count_and_adds_wealth_and_health_sums()
    {
        var city = MakeCity(new AggregatePopulationPool(4, 400, 320));
        var returningId = new NpcId(999);

        var result = city.Dematerialize(returningId, wealth: 100, health: 80);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(5, 500, 400), city.AggregatePool);
        Assert.Contains(returningId, city.PoolNpcIds);
    }

    [Fact]
    public void Materialize_then_dematerialize_round_trips_pool_and_ids_to_original_state()
    {
        var city = MakeCity(new AggregatePopulationPool(5, 500, 400));
        var originalIds = city.PoolNpcIds.ToList();
        var id = city.PoolNpcIds[^1];

        city.Materialize(id, wealth: 100, health: 80);
        city.Dematerialize(id, wealth: 100, health: 80);

        Assert.Equal(new AggregatePopulationPool(5, 500, 400), city.AggregatePool);
        Assert.Equal(originalIds, city.PoolNpcIds);
    }

    [Fact]
    public void Emigrate_removes_headcount_and_its_per_head_average_wealth_and_health()
    {
        var city = MakeCity(new AggregatePopulationPool(10, 100, 50));

        var result = city.Emigrate(4);

        Assert.True(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(6, 60, 30), city.AggregatePool);
    }

    [Fact]
    public void Emigrate_fails_and_leaves_pool_unchanged_when_headcount_exceeds_the_pool()
    {
        var city = MakeCity(new AggregatePopulationPool(3, 30, 30));

        var result = city.Emigrate(4);

        Assert.False(result.IsSuccess);
        Assert.Equal(new AggregatePopulationPool(3, 30, 30), city.AggregatePool);
    }

    [Fact]
    public void Emigrate_never_creates_an_npc_unlike_materialize()
    {
        var city = MakeCity(new AggregatePopulationPool(5, 500, 400));

        city.Emigrate(2);

        Assert.Equal(3, city.AggregatePool.Count);
    }
}
