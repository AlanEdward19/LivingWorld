using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T19 (CITY-02): par base/tratamento na mesma seed — tratamento zera o
/// estoque de comida da cidade — <c>popTrat &lt; popBase</c>, com a diferença maior que o
/// spread entre seeds do braço base (R4 de rules/eval-criteria.md: causal exige controle).
///
/// Cada seed semeia uma coorte real de NPCs materializados (<see cref="PopulationSeeder"/>,
/// sujeitos a <see cref="MortalitySystem"/> — fonte de ruído demográfico genuíno e dependente de
/// seed via <c>WorldRng</c>) mais um pool agregado grande (só <see cref="CityGrowthSystem"/>
/// mexe nele, via déficit de comida do estoque do household). Isso separa os dois canais: o
/// braço base/tratamento muda só o estoque de comida (canal causal sob teste), enquanto a coorte
/// materializada morre pela mesma tabela de vida/mesma seed nos dois braços — a variação
/// seed-a-seed do baseline mede ruído demográfico real, não é zero por construção.</summary>
public class FoodShortageMigrationScenarioTests
{
    private const int InitialPopulation = 30;
    private const long PoolCount = 100;
    private const int HorizonDays = 360;
    private static readonly ResourceType Food = new(1);

    private static CityRules MakeRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 20, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0.05, migrationEmploymentWeight: 0, migrationFoodWeight: 0,
        migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0, foundingConcentrationThreshold: 1.0,
        foundingResourceThreshold: 1.0, foundingRouteThreshold: 1.0, foundingDefensibilityThreshold: 1.0,
        foundingLeadershipThreshold: 1.0, organizationTicks: 1, materializationIdleTicksBeforeEligible: 1)
        .Value!;

    private static EconomyRules MakeEconomyRules() => EconomyRules.Create(
        enabled: false, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.1, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    /// <summary><paramref name="foodStock"/> ample (baseline) ou zero (tratamento) — único
    /// parâmetro que muda entre os dois braços da mesma seed. O estoque vive num household
    /// "granário" dedicado (não um dos households pareados por <see cref="PopulationSeeder"/>,
    /// cuja formação depende de sorteio e pode não gerar nenhum household pareado para uma
    /// coorte pequena) — garante que o único parâmetro que difere entre os braços é
    /// exatamente o estoque de comida, nunca a presença/ausência de um household.</summary>
    private static long RunArm(ulong seed, long foodStock)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: MakeEconomyRules(), cityRules: MakeRules());

        PopulationSeeder.SeedInitial(world, InitialPopulation, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        var city = new City(
            world.NextCityId(), ScenarioRunner.DefaultVillageLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: new AggregatePopulationPool(PoolCount, PoolCount * 50, PoolCount * 50),
            poolNpcIds: world.ReserveNpcIdBlock(PoolCount));
        world.AddCity(city);

        foreach (var npc in world.Npcs) npc.JoinCity(city.Id);
        foreach (var household in world.Households) household.JoinCity(city.Id);

        var granaryKeeper = new Npc(
            world.NextNpcIdAndAdvance(), "npc-granary-keeper", Sex.Female, world.CurrentDate.AddYears(-30),
            ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation, motherId: null, fatherId: null,
            household: null, health: 80, personality: NeutralPersonality, profession: ProfessionType.None,
            currentLocation: ScenarioRunner.DefaultVillageLocation, city: city.Id);
        world.AddNpc(granaryKeeper);
        var granary = new Household(
            world.NextHouseholdIdAndAdvance(), city.Location, granaryKeeper.Id, [granaryKeeper.Id], city: city.Id);
        granary.Deposit(Food, foodStock);
        world.AddHousehold(granary);

        var systems = new List<ISimulationSystem> { new MortalitySystem(), new CityGrowthSystem() };
        var clock = new WorldClock(systems);
        clock.Run(world, HorizonDays * 24);

        return CityPopulationQuery.Population(world, city.Id);
    }

    [Fact]
    public void Zeroing_food_production_drops_population_more_than_baseline_seed_to_seed_spread_across_10_seeds()
    {
        var basePops = new List<long>();
        var diffs = new List<long>();
        int wins = 0;

        for (ulong seed = 1; seed <= 10; seed++)
        {
            long popBase = RunArm(seed, foodStock: 1_000_000); // farto — nunca cruza o limiar
            long popTrat = RunArm(seed, foodStock: 0); // zerado — cruza o limiar todo dia

            basePops.Add(popBase);
            diffs.Add(popBase - popTrat);
            if (popTrat < popBase) wins++;
        }

        Assert.Equal(10, wins); // contagem de acertos (R4), não magnitude solta

        long spread = basePops.Max() - basePops.Min(); // ruído demográfico seed-a-seed do braço base
        Assert.True(diffs.Min() > spread,
            $"menor diferenca base-tratamento ({diffs.Min()}) deveria exceder o spread do baseline entre seeds ({spread}) " +
            "- senao a queda observada e compativel com ruido demografico normal, nao com o choque de comida");
    }
}
