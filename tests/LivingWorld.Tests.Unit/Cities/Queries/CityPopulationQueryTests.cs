using System.Reflection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Cities.Queries;

/// <summary>Fase 8, T8 (CITY-01, CITY-09): <see cref="CityPopulationQuery"/> — sempre on-demand,
/// nunca cacheado; Population bate com COUNT manual + AggregatePool.Count.</summary>
public class CityPopulationQueryTests
{
    private static (WorldState World, City City, Npc InCity, Npc OutOfCity) MakeWorldWithCity()
    {
        var world = ScenarioRunner.Create(seed: 3, initialPopulation: 3).World;
        var npcs = world.Npcs.ToList();
        var inCity = npcs[0];
        var outOfCity = npcs[1];

        var city = new City(
            world.NextCityId(), inCity.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(5, 500, 400));
        world.AddCity(city);

        var otherCity = new City(
            world.NextCityId(), outOfCity.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(otherCity);

        inCity.JoinCity(city.Id);
        outOfCity.JoinCity(otherCity.Id);
        inCity.CreditWallet(new Money(100));

        return (world, city, inCity, outOfCity);
    }

    [Fact]
    public void Population_matches_manual_alive_npc_count_plus_aggregate_pool_count()
    {
        var (world, city, _, _) = MakeWorldWithCity();

        long manualCount = world.Npcs.Count(n => n.IsAlive && n.City == city.Id);

        Assert.Equal(manualCount + city.AggregatePool.Count, CityPopulationQuery.Population(world, city.Id));
    }

    [Fact]
    public void Population_never_counts_an_npc_assigned_to_a_different_city()
    {
        var (world, city, _, outOfCity) = MakeWorldWithCity();

        long population = CityPopulationQuery.Population(world, city.Id);

        Assert.NotEqual(outOfCity.City, city.Id); // pré-condição do teste
        Assert.Equal(1 + city.AggregatePool.Count, population); // só inCity, não outOfCity
    }

    [Fact]
    public void Wealth_sums_materialized_wallets_plus_aggregate_wealth_sum()
    {
        var (world, city, inCity, _) = MakeWorldWithCity();

        long wealth = CityPopulationQuery.Wealth(world, city.Id);

        Assert.Equal(inCity.Wallet.Amount + city.AggregatePool.WealthSum, wealth);
    }

    [Fact]
    public void Health_sums_materialized_health_plus_aggregate_health_sum()
    {
        var (world, city, inCity, _) = MakeWorldWithCity();

        long health = CityPopulationQuery.Health(world, city.Id);

        Assert.Equal(inCity.Health + city.AggregatePool.HealthSum, health);
    }

    [Fact]
    public void Inequality_is_zero_when_no_one_is_materialized_in_the_city()
    {
        var world = ScenarioRunner.Create(seed: 4, initialPopulation: 1).World;
        var emptyCity = new City(world.NextCityId(), new CellCoord(9, 9), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(emptyCity);

        Assert.Equal(0.0, CityPopulationQuery.Inequality(world, emptyCity.Id));
    }

    [Fact]
    public void Inequality_is_zero_when_every_materialized_wallet_is_equal()
    {
        var world = ScenarioRunner.Create(seed: 5, initialPopulation: 2).World;
        var city = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        foreach (var npc in world.Npcs)
        {
            npc.JoinCity(city.Id);
            npc.CreditWallet(new Money(50));
        }

        Assert.Equal(0.0, CityPopulationQuery.Inequality(world, city.Id));
    }

    [Fact]
    public void Inequality_is_positive_when_wallets_are_unequal()
    {
        var world = ScenarioRunner.Create(seed: 6, initialPopulation: 2).World;
        var city = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var npcs = world.Npcs.ToList();
        foreach (var npc in npcs)
            npc.JoinCity(city.Id);
        npcs[0].CreditWallet(new Money(1000));
        // npcs[1] fica com 0

        Assert.True(CityPopulationQuery.Inequality(world, city.Id) > 0.0);
    }

    [Fact]
    public void Economy_equals_wealth()
    {
        // Fase 8, fix round 1, gap 1 (CITY-01 AC1): "economia" reusa Wealth (SPEC_DEVIATION,
        // sem sinal distinto nesta fase) — o teste prova o alias, não um comportamento novo.
        var (world, city, _, _) = MakeWorldWithCity();

        Assert.Equal(CityPopulationQuery.Wealth(world, city.Id), CityPopulationQuery.Economy(world, city.Id));
    }

    [Fact]
    public void Housing_equals_sum_of_housing_capacity_of_completed_buildings_for_the_city()
    {
        var recipe = BuildingRecipe.Create(new Dictionary<ResourceType, long>(), ticksToBuild: 1, housingCapacityProvided: 4).Value!;
        var catalog = new CityCatalog(new Dictionary<int, BuildingRecipe> { [1] = recipe });
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 12, ScenarioRunner.DefaultMap(12),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityCatalog: catalog);
        var city = new City(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        var otherCity = new City(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        world.AddCity(otherCity);
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0));
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0));
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), otherCity.Id, buildingTypeId: 1, completedAtTick: 0));

        long housing = CityPopulationQuery.Housing(world, city.Id);

        Assert.Equal(8, housing); // 2 buildings da cidade × 4 de capacidade — ignora o building de outra cidade
    }

    [Fact]
    public void Security_education_and_infrastructure_equal_the_completed_building_count_for_the_city()
    {
        var (world, city, _, outOfCity) = MakeWorldWithCity();
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0));
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 2, completedAtTick: 0));
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), outOfCity.City, buildingTypeId: 1, completedAtTick: 0));

        Assert.Equal(2, CityPopulationQuery.Security(world, city.Id));
        Assert.Equal(2, CityPopulationQuery.Education(world, city.Id));
        Assert.Equal(2, CityPopulationQuery.Infrastructure(world, city.Id));
    }

    [Fact]
    public void City_exposes_government_culture_and_technology_as_existing_stub_records()
    {
        // Fase 8, fix round 1, gap 1 (CITY-01 AC1): task 1 só pede que os campos "existam" —
        // design.md Tech Decisions: stub vazio, sem comportamento.
        var (_, city, _, _) = MakeWorldWithCity();

        Assert.NotNull(city.Government);
        Assert.NotNull(city.Culture);
        Assert.NotNull(city.Technology);
    }

    [Fact]
    public void CityPopulationQuery_has_no_mutable_field_backing_the_aggregates()
    {
        // Done-when de T8: "nenhum campo é cacheado" — todo agregado recomputado a cada chamada.
        var fields = typeof(CityPopulationQuery).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        Assert.Empty(fields);
    }
}
