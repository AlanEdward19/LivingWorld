using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities.Construction;

/// <summary>Fase 8, T9 (CITY-04/CITY-05): <see cref="MaterializationSystem"/> — materializar
/// debita exatamente 1 do pool e cria exatamente 1 <see cref="Npc"/>; desmaterializar devolve
/// riqueza/saúde e remove a linha; papel formal nunca é elegível a desmaterialização.</summary>
public class MaterializationSystemTests
{
    private static CityRules MakeRules(long idleTicks = 5) => CityRules.Create(
        enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
        emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
        foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
        foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: idleTicks)
        .Value!;

    private static (WorldState World, City City) MakeWorldWithCity(
        AggregatePopulationPool? pool = null, long idleTicks = 5)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 7, ScenarioRunner.DefaultMap(7),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityRules: MakeRules(idleTicks));

        PopulationSeeder.SeedInitial(world, count: 1, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        // T50: mesma reserva em lote que ScenarioLoaderV2 faz pra cidade autorada com pool
        // não-vazio — sem isso PoolNpcIds ficaria vazio e Materialize/Emigrate falhariam.
        var resolvedPool = pool ?? new AggregatePopulationPool(5, 500, 400);
        var poolNpcIds = world.ReserveNpcIdBlock(resolvedPool.Count);
        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: resolvedPool, poolNpcIds: poolNpcIds);
        world.AddCity(city);
        return (world, city);
    }

    private static TickContext MakeCtx(WorldState world) => new(world, world.Rng, world.Scheduler);

    [Fact]
    public void MaterializeOne_decrements_pool_count_by_exactly_one_and_creates_one_npc()
    {
        var (world, city) = MakeWorldWithCity(new AggregatePopulationPool(5, 500, 400));
        var ctx = MakeCtx(world);
        int npcCountBefore = world.Npcs.Count;

        var result = MaterializationSystem.MaterializeOne(world, ctx, city.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, world.FindCity(city.Id)!.AggregatePool.Count);
        Assert.Equal(npcCountBefore + 1, world.Npcs.Count);
        Assert.Same(result.Value, world.FindNpc(result.Value!.Id));
        Assert.Equal(city.Id, result.Value!.City);
    }

    [Fact]
    public void MaterializeOne_fails_and_leaves_world_unchanged_when_pool_is_empty()
    {
        var (world, city) = MakeWorldWithCity(AggregatePopulationPool.Empty);
        var ctx = MakeCtx(world);
        int npcCountBefore = world.Npcs.Count;

        var result = MaterializationSystem.MaterializeOne(world, ctx, city.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(npcCountBefore, world.Npcs.Count);
    }

    [Fact]
    public void Dematerialize_returns_wealth_and_health_to_pool_and_removes_the_npc_row()
    {
        var (world, city) = MakeWorldWithCity(new AggregatePopulationPool(5, 500, 400));
        var ctx = MakeCtx(world);
        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        var poolAfterMaterialize = world.FindCity(city.Id)!.AggregatePool;

        var result = MaterializationSystem.Dematerialize(world, npc.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(world.FindNpc(npc.Id));
        var poolAfterDematerialize = world.FindCity(city.Id)!.AggregatePool;
        Assert.Equal(poolAfterMaterialize.Count + 1, poolAfterDematerialize.Count);
        Assert.Equal(poolAfterMaterialize.WealthSum + npc.Wallet.Amount, poolAfterDematerialize.WealthSum);
        Assert.Equal(poolAfterMaterialize.HealthSum + npc.Health, poolAfterDematerialize.HealthSum);
    }

    [Fact]
    public void Materialize_then_dematerialize_round_trips_the_pool_to_its_original_state()
    {
        var (world, city) = MakeWorldWithCity(new AggregatePopulationPool(5, 500, 400));
        var ctx = MakeCtx(world);
        var originalPool = world.FindCity(city.Id)!.AggregatePool;

        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        MaterializationSystem.Dematerialize(world, npc.Id);

        Assert.Equal(originalPool, world.FindCity(city.Id)!.AggregatePool);
    }

    [Fact]
    public void Dematerialize_fails_when_npc_occupies_a_household_head_role()
    {
        var (world, city) = MakeWorldWithCity();
        var ctx = MakeCtx(world);
        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        world.AddHousehold(new Household(new HouseholdId(1), city.Location, npc.Id, [npc.Id]));

        var result = MaterializationSystem.Dematerialize(world, npc.Id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(world.FindNpc(npc.Id));
    }

    [Fact]
    public void Dematerialize_fails_for_an_npc_that_never_came_from_the_aggregate_pool()
    {
        var (world, city) = MakeWorldWithCity();
        var original = world.Npcs.First(); // seed original: MaterializedAtTick nulo

        var result = MaterializationSystem.Dematerialize(world, original.Id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(world.FindNpc(original.Id));
    }

    [Fact]
    public void Tick_dematerializes_a_materialized_npc_with_no_formal_role_once_idle_past_the_threshold()
    {
        var (world, city) = MakeWorldWithCity(idleTicks: 5);
        var ctx = MakeCtx(world);
        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;

        AdvanceTicks(world, 6);

        new MaterializationSystem().Tick(world, MakeCtx(world));

        Assert.Null(world.FindNpc(npc.Id));
    }

    [Fact]
    public void Tick_never_dematerializes_an_npc_holding_a_formal_role_even_when_idle_past_the_threshold()
    {
        var (world, city) = MakeWorldWithCity(idleTicks: 5);
        var ctx = MakeCtx(world);
        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        world.AddHousehold(new Household(new HouseholdId(1), city.Location, npc.Id, [npc.Id]));

        AdvanceTicks(world, 6);
        new MaterializationSystem().Tick(world, MakeCtx(world));

        Assert.NotNull(world.FindNpc(npc.Id));
    }

    [Fact]
    public void Tick_does_not_dematerialize_before_the_idle_threshold_is_reached()
    {
        var (world, city) = MakeWorldWithCity(idleTicks: 5);
        var ctx = MakeCtx(world);
        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;

        AdvanceTicks(world, 4);
        new MaterializationSystem().Tick(world, MakeCtx(world));

        Assert.NotNull(world.FindNpc(npc.Id));
    }

    [Fact]
    public void EnsureMaterialized_succeeds_for_an_existing_alive_npc()
    {
        var (world, _) = MakeWorldWithCity();
        var npc = world.Npcs.First();

        var result = MaterializationSystem.EnsureMaterialized(world, npc.Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EnsureMaterialized_fails_for_an_id_that_does_not_exist()
    {
        var (world, _) = MakeWorldWithCity();

        var result = MaterializationSystem.EnsureMaterialized(world, new NpcId(999_999));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EnsureMaterialized_materializes_a_genuine_never_touched_pool_member_addressed_by_its_reserved_id()
    {
        // T50 (reabre CITY-05 AC2): id nunca tocado (nunca existiu como Npc), reservado no
        // próprio PoolNpcIds da cidade — não mais "o próximo NextNpcId" (esse endereçamento foi
        // substituído por identidade estável por membro do pool).
        var (world, city) = MakeWorldWithCity(new AggregatePopulationPool(5, 500, 400));
        var neverTouchedId = city.PoolNpcIds[0];
        Assert.Null(world.FindNpc(neverTouchedId)); // pré-condição: id genuinamente nunca tocado
        long poolCountBefore = world.FindCity(city.Id)!.AggregatePool.Count;

        var result = MaterializationSystem.EnsureMaterialized(world, neverTouchedId);

        Assert.True(result.IsSuccess);
        var materialized = world.FindNpc(neverTouchedId);
        Assert.NotNull(materialized);
        Assert.True(materialized!.IsAlive);
        Assert.Equal(poolCountBefore - 1, world.FindCity(city.Id)!.AggregatePool.Count);
    }

    [Fact]
    public void EnsureMaterialized_fails_for_an_id_reserved_by_no_city()
    {
        var (world, _) = MakeWorldWithCity(AggregatePopulationPool.Empty);
        var unreservedId = new NpcId(world.NextNpcId);

        var result = MaterializationSystem.EnsureMaterialized(world, unreservedId);

        Assert.False(result.IsSuccess);
        Assert.Null(world.FindNpc(unreservedId));
    }

    [Fact]
    public void EnsureMaterialized_only_touches_the_city_that_actually_reserved_the_id()
    {
        // T50: cada id de pool pertence a exatamente 1 cidade (sem ambiguidade/tie-break entre
        // cidades com pool não-vazio, diferente do endereçamento antigo por NextNpcId) —
        // materializar um id da segunda cidade nunca deveria mexer na primeira.
        var (world, firstCity) = MakeWorldWithCity(new AggregatePopulationPool(5, 500, 400));
        var secondCityPoolIds = world.ReserveNpcIdBlock(5);
        var secondCity = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(5, 500, 400), poolNpcIds: secondCityPoolIds);
        world.AddCity(secondCity);
        var idFromSecondCity = secondCity.PoolNpcIds[0];
        long firstPoolBefore = world.FindCity(firstCity.Id)!.AggregatePool.Count;
        long secondPoolBefore = world.FindCity(secondCity.Id)!.AggregatePool.Count;

        var result = MaterializationSystem.EnsureMaterialized(world, idFromSecondCity);

        Assert.True(result.IsSuccess);
        var materialized = world.FindNpc(idFromSecondCity);
        Assert.NotNull(materialized);
        Assert.Equal(secondCity.Id, materialized!.City);
        Assert.Equal(firstPoolBefore, world.FindCity(firstCity.Id)!.AggregatePool.Count);
        Assert.Equal(secondPoolBefore - 1, world.FindCity(secondCity.Id)!.AggregatePool.Count);
    }

    // CurrentDate tem setter `internal` (mesmo padrão de WorldClock) — visível aqui via
    // InternalsVisibleTo("LivingWorld.Tests"). Ticks de MaterializationSystem são medidos em
    // horas (TickContext.CurrentTick == CurrentDate.TotalHours).
    private static void AdvanceTicks(WorldState world, long hours) =>
        world.CurrentDate = world.CurrentDate.AddHours(hours);
}
