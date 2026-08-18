using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T17 (CITY-04/CITY-09): conservação da LOD contra fonte independente.
///
/// AC4 da spec pede "COUNT(*) de NPCs materializados no store + contador agregado persistido
/// == população total, a cada tick" lidos sem tocar a propriedade derivada — mas
/// <see cref="CityPopulationQuery.Population"/> já É exatamente essa soma (nenhum campo
/// cacheado, T8 Done-when), então recomputar a mesma fórmula por cidade e comparar contra si
/// mesma seria "a + b == a + b" (o problema que a Assumption de T9 nomeia explicitamente). A
/// asserção com poder de discriminação real aqui é outra: o <b>total global</b> (soma de
/// materializados vivos + pool agregado, sobre TODAS as cidades, inclusive as fundadas durante o
/// teste) deve ficar byte-idêntico ao total inicial durante todo o horizonte — migração,
/// materialização/desmaterialização e fundação de assentamento só devem mover massa entre
/// colunas/cidades, nunca criar ou destruir NPC. Emigração agregada (CityGrowthSystem, que
/// remove população do mundo por design — story de crescimento/encolhimento, não de LOD) é
/// neutralizada por limiares nunca cruzados (thresholds 100), isolando o teste ao mecanismo de
/// LOD que é o objeto de CITY-04. Um bug real em qualquer um dos três sistemas (ex.: Materialize
/// sem debitar o pool, Migrate perdendo o NPC no caminho, Founding duplicando o pool) mudaria o
/// total global e derrubaria o teste — isto SIM discrimina, ao contrário da recomputação por
/// cidade (mantida abaixo só para documentar a letra do AC4, comentada como tal).</summary>
public class LodConservationScenarioTests
{
    private const int DaysPerYear = 360;
    private static readonly ResourceType Food = new(1);
    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static CityRules MakeRules() => CityRules.Create(
        enabled: true,
        // Nunca cruzados (déficit máximo teórico é 100): isola o teste do mecanismo de
        // emigração agregada (CityGrowthSystem), que por design tira população do mundo —
        // objeto de CITY-02, não de CITY-04/conservação de LOD.
        foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0,
        migrationEmploymentWeight: 0, migrationFoodWeight: 1, migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0,
        // Concentração baixa (qualquer população > 0 já satisfaz); os outros 4 limiares de
        // fundação ficam vacuamente satisfeitos por design da Fase 8 até hoje (ver
        // SPEC_DEVIATION em SettlementFoundingSystem.cs) — aqui isso é usado deliberadamente
        // para exercitar o split dentro do horizonte do teste, não pra provar os 4 fatores.
        foundingConcentrationThreshold: 0.01, foundingResourceThreshold: 0, foundingRouteThreshold: 0,
        foundingDefensibilityThreshold: 0, foundingLeadershipThreshold: 0,
        organizationTicks: 240, materializationIdleTicksBeforeEligible: 48)
        .Value!;

    private static EconomyRules MakeEconomyRules() => EconomyRules.Create(
        enabled: false, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static Npc MakeNpc(WorldState world, long id, CityId city) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, world.CurrentDate.AddYears(-30), ScenarioRunner.DefaultCulture,
        ScenarioRunner.DefaultVillageLocation, motherId: null, fatherId: null, household: null, health: 80,
        personality: NeutralPersonality, profession: ProfessionType.None, currentLocation: ScenarioRunner.DefaultVillageLocation,
        city: city);

    /// <summary>Long total global: materializados vivos (todos em <see cref="WorldState.Npcs"/>
    /// nesta cena vêm de um pool, nenhum morre — sem MortalitySystem no harness) + pool agregado
    /// de toda cidade existente no momento (inclusive a fundada durante o teste).</summary>
    private static long RawGlobalPopulation(WorldState world) =>
        world.Npcs.Count(n => n.IsAlive) + world.Cities.Sum(c => c.AggregatePool.Count);

    private static (WorldState World, WorldClock Clock, long InitialTotal) BuildScenario(ulong seed)
    {
        var rules = MakeRules();
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: MakeEconomyRules(), cityRules: rules);

        // T50: reserva os ids do pool DEPOIS de afastar o contador dos ids manuais (1, 2) — senão
        // a reserva em lote colidiria com head/destinationAnchor abaixo.
        world.AdvanceNpcIdTo(100);
        var cityAPoolIds = world.ReserveNpcIdBlock(20);
        var cityBPoolIds = world.ReserveNpcIdBlock(15);
        var cityA = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, new AggregatePopulationPool(20, 200, 1000), poolNpcIds: cityAPoolIds);
        var cityB = new City(world.NextCityId(), new CellCoord(1, 1), 0, null, new AggregatePopulationPool(15, 150, 750), poolNpcIds: cityBPoolIds);
        world.AddCity(cityA);
        world.AddCity(cityB);

        var head = MakeNpc(world, 1, cityA.Id);
        var destinationAnchor = MakeNpc(world, 2, cityB.Id);
        world.AddNpc(head);
        world.AddNpc(destinationAnchor);

        var household = new Household(new HouseholdId(1), cityA.Location, head.Id, [head.Id], city: cityA.Id);
        household.Deposit(Food, 0); // sem comida na origem
        world.AddHousehold(household);

        var destinationHousehold = new Household(new HouseholdId(2), cityB.Location, destinationAnchor.Id, [destinationAnchor.Id], city: cityB.Id);
        destinationHousehold.Deposit(Food, 1000); // destino farto — puxa a migração
        world.AddHousehold(destinationHousehold);

        // Materializa 2 NPCs "livres" (sem household/papel formal) do pool de cityA — vão
        // desmaterializar sozinhos depois de MaterializationIdleTicksBeforeEligible, exercitando
        // o ciclo completo materializar->desmaterializar dentro do horizonte do teste.
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        Assert.True(MaterializationSystem.MaterializeOne(world, ctx, cityA.Id).IsSuccess);
        Assert.True(MaterializationSystem.MaterializeOne(world, ctx, cityA.Id).IsSuccess);

        long initialTotal = RawGlobalPopulation(world);

        var systems = new List<ISimulationSystem>
        {
            new MaterializationSystem(),
            new CityGrowthSystem(),
            new MigrationSystem(),
            new SettlementFoundingSystem(),
        };
        var clock = new WorldClock(systems);

        return (world, clock, initialTotal);
    }

    /// <summary>Roda o horizonte hora a hora, verificando a cada tick — R2 de
    /// rules/eval-criteria.md ("invariante a cada tick em horizonte curto").</summary>
    private static void RunAndAssertConservationEveryTick(ulong seed, int years)
    {
        var (world, clock, initialTotal) = BuildScenario(seed);
        long horizonHours = (long)years * DaysPerYear * 24;

        for (long h = 0; h < horizonHours; h++)
        {
            clock.Tick(world);

            // Discriminação real (ver doc da classe): total global nunca diverge do total inicial.
            Assert.Equal(initialTotal, RawGlobalPopulation(world));

            // Letra literal do AC4 (COUNT+pool == "população total"): mantido como documentação
            // do critério, mas por construção de CityPopulationQuery.Population (T8, sem campo
            // cacheado) esta linha é tautológica — não é a fonte de discriminação deste teste.
            foreach (var city in world.Cities)
            {
                long independentCount = world.Npcs.Count(n => n.IsAlive && n.City == city.Id) + city.AggregatePool.Count;
                Assert.Equal(CityPopulationQuery.Population(world, city.Id), independentCount);
            }
        }

        // Prova de que o cenário realmente exercitou os 3 mecanismos, não só ficou parado:
        Assert.True(world.Cities.Count > 2, "fundação de assentamento deveria ter disparado no horizonte do teste");
    }

    [Fact]
    public void Global_population_stays_conserved_across_ten_years_of_lod_churn()
    {
        RunAndAssertConservationEveryTick(seed: 901, years: 10);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Global_population_stays_conserved_across_one_hundred_years_of_lod_churn()
    {
        RunAndAssertConservationEveryTick(seed: 901, years: 100);
    }
}
