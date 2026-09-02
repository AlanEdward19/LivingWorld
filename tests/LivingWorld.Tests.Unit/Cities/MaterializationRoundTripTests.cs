using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T18 (CITY-04): materializar e desmaterializar o mesmo NPC, sem nenhuma
/// outra mudança, deve deixar o mundo observável byte-idêntico.
///
/// SPEC_DEVIATION: a AC pede "Hash(world) byte-idêntico" via <see
/// cref="WorldSnapshot.CanonicalHash"/> — mas <see cref="MaterializationSystem.MaterializeOne"/>
/// consome <c>NextNpcId</c> (contador monotônico, nunca decrementado por <see
/// cref="MaterializationSystem.Dematerialize"/> — reciclar um id exigiria uma lista de ids
/// livres, fora do escopo de T18/CITY-04, e arriscaria colisão com <c>rules/simulation-determinism.md</c>)
/// e várias chaves de <c>WorldRng</c> (<c>materialize-sex-N</c>/<c>materialize-age-N</c>/etc,
/// Fase 8/T9) — RNG streams só avançam, nunca retrocedem (o mesmo princípio que sustenta
/// determinismo/replay no resto do motor). Os dois são bookkeeping monotônico, nunca "mundo"
/// no sentido de população/riqueza/saúde/edifício/evento que a AC quer proteger — confirmado
/// empiricamente (spike descartado) que <c>CanonicalHash</c> completo diverge só por causa
/// desses dois campos. Este teste compara o snapshot inteiro (todo campo canônico + volátil,
/// via <see cref="WorldSnapshot.Serialize"/>) MENOS exatamente esses dois — a asserção mais
/// forte que a arquitetura atual permite sem inventar reciclagem de id/rewind de RNG (fora do
/// pedido de T18): qualquer outra mudança (população, riqueza, saúde, cidade, edifício, evento
/// agendado, household) derruba o teste.</summary>
public class MaterializationRoundTripTests
{
    private static CityRules MakeRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 20, securityShortageThreshold: 20,
        emigrationRatePerDeficitUnit: 0.1, migrationEmploymentWeight: 1, migrationFoodWeight: 1,
        migrationSecurityWeight: 1, migrationFamilyTiesWeight: 1, foundingConcentrationThreshold: 0.5,
        foundingResourceThreshold: 0.5, foundingRouteThreshold: 0.5, foundingDefensibilityThreshold: 0.5,
        foundingLeadershipThreshold: 0.5, organizationTicks: 10, materializationIdleTicksBeforeEligible: 5)
        .Value!;

    private static (WorldState World, City City) MakeWorldWithCity() =>
        MakeWorldWithCityAndPool(new AggregatePopulationPool(5, 500, 400));

    private static (WorldState World, City City) MakeWorldWithCityAndPool(AggregatePopulationPool pool)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 31, ScenarioRunner.DefaultMap(31),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            cityRules: MakeRules());
        // T50: NextNpcId na verdade não muda mais nesse round-trip (id vem de PoolNpcIds, já
        // reservado aqui) — a exclusão abaixo continua inofensiva, só deixou de ser necessária
        // pra essa chave especificamente (RngStreams ainda avança de verdade).
        var city = new City(world.NextCityId(), ScenarioRunner.DefaultVillageLocation, 0, null, pool, poolNpcIds: world.ReserveNpcIdBlock(pool.Count));
        world.AddCity(city);
        return (world, city);
    }

    /// <summary>Snapshot completo (canônico + volátil) menos os 2 contadores monotônicos que a
    /// materialização legitimamente avança (ver SPEC_DEVIATION da classe) — a comparação mais
    /// forte disponível sem reciclar id/rewindar RNG.</summary>
    private static string SnapshotExcludingMonotonicCounters(WorldState world)
    {
        var node = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        node.Remove("NextNpcId");
        node.Remove("RngStreams");
        return node.ToJsonString();
    }

    [Fact]
    public void Materialize_then_dematerialize_leaves_the_world_snapshot_unchanged_outside_monotonic_counters()
    {
        var (world, city) = MakeWorldWithCity();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        string before = SnapshotExcludingMonotonicCounters(world);

        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        MaterializationSystem.Dematerialize(world, npc.Id);

        string after = SnapshotExcludingMonotonicCounters(world);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Materialize_then_dematerialize_round_trips_population_wealth_health_and_inequality()
    {
        var (world, city) = MakeWorldWithCity();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        long populationBefore = CityPopulationQuery.Population(world, city.Id);
        long wealthBefore = CityPopulationQuery.Wealth(world, city.Id);
        long healthBefore = CityPopulationQuery.Health(world, city.Id);
        double inequalityBefore = CityPopulationQuery.Inequality(world, city.Id);

        var npc = MaterializationSystem.MaterializeOne(world, ctx, city.Id).Value!;
        MaterializationSystem.Dematerialize(world, npc.Id);

        Assert.Equal(populationBefore, CityPopulationQuery.Population(world, city.Id));
        Assert.Equal(wealthBefore, CityPopulationQuery.Wealth(world, city.Id));
        Assert.Equal(healthBefore, CityPopulationQuery.Health(world, city.Id));
        Assert.Equal(inequalityBefore, CityPopulationQuery.Inequality(world, city.Id));
    }
}
