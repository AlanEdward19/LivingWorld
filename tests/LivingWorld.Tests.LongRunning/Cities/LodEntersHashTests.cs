using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.LongRunning.Cities;

/// <summary>Fase 8, T22 (CITY-04): desligar LOD/migração/crescimento/fundação por flag de teste
/// (mesmo padrão de mutação de <c>FamilyRules with { ... }</c> em
/// <c>FamilyPairedScenarioTests.Turning_off_heredity_and_courtship_changes_world_hash_after_ten_years</c>,
/// Fase 7 T31) muda <c>Hash(world)</c> após 10 anos, mesma seed — prova que os sistemas de
/// cidade entram na conta.
///
/// <c>CityRules</c> é <c>[Canonical]</c> em <see cref="WorldState"/>: só alternar
/// <c>Enabled</c> já torna <see cref="WorldSnapshot.CanonicalHash"/> diferente na hora zero,
/// antes de qualquer tick rodar — o mesmo já vale pro precedente de T31 com <c>FamilyRules</c>.
/// Isso prova só que o campo de regra participa do hash, não que o COMPORTAMENTO dos sistemas
/// (materializar/desmaterializar/migrar/emigrar/fundar) entrou na conta ao longo dos 10 anos —
/// um teste mais forte que o mínimo do AC. Por isso este arquivo compara o snapshot inteiro
/// MENOS a própria chave <c>CityRules</c> (que trivialmente difere por construção) — a
/// divergência que sobra só pode vir de Cities/Npcs/Buildings/PendingEvents terem
/// evoluído de forma diferente nos dois braços, isto sim discrimina comportamento real.</summary>
public class LodEntersHashTests
{
    private const int DaysPerYear = 360;
    private const long HorizonHours = 10L * DaysPerYear * 24;
    private static readonly ResourceType Food = new(1);
    private static readonly Personality NeutralPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static CityRules MakeEnabledRules() => CityRules.Create(
        enabled: true, foodShortageThreshold: 100, housingShortageThreshold: 100, securityShortageThreshold: 100,
        emigrationRatePerDeficitUnit: 0, migrationEmploymentWeight: 0, migrationFoodWeight: 1,
        migrationSecurityWeight: 0, migrationFamilyTiesWeight: 0, foundingConcentrationThreshold: 0.01,
        foundingResourceThreshold: 0, foundingRouteThreshold: 0, foundingDefensibilityThreshold: 0,
        foundingLeadershipThreshold: 0, organizationTicks: 240, materializationIdleTicksBeforeEligible: 48)
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

    /// <summary>Mesma cena para os dois braços — só <see cref="CityRules.Enabled"/> muda
    /// (<paramref name="cityRulesEnabled"/>), mesmo espírito de <c>FamilyRules with {...}</c> do
    /// precedente de T31.</summary>
    private static WorldState BuildScenario(ulong seed, bool cityRulesEnabled)
    {
        var rules = MakeEnabledRules() with { Enabled = cityRulesEnabled };
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules,
            economyRules: MakeEconomyRules(), cityRules: rules);

        // T50: afasta o contador dos ids manuais (1, 2) ANTES de reservar o pool — senão a
        // reserva em lote colidiria com head/destinationAnchor abaixo.
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
        household.Deposit(Food, 0);
        world.AddHousehold(household);
        var destinationHousehold = new Household(new HouseholdId(2), cityB.Location, destinationAnchor.Id, [destinationAnchor.Id], city: cityB.Id);
        destinationHousehold.Deposit(Food, 1000);
        world.AddHousehold(destinationHousehold);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        if (cityRulesEnabled)
        {
            // MaterializeOne consome ctx.Rng — só chamado no braço ligado, senão o braço
            // desligado teria uma trilha de RNG diferente por uma razão que nada tem a ver com
            // os sistemas de cidade estarem ligados/desligados (viés na comparação).
            MaterializationSystem.MaterializeOne(world, ctx, cityA.Id);
            MaterializationSystem.MaterializeOne(world, ctx, cityA.Id);
        }

        return world;
    }

    private static string SnapshotExcludingCityRules(WorldState world)
    {
        var node = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        node.Remove("CityRules");
        return node.ToJsonString();
    }

    [Fact]
    public void Turning_off_lod_migration_growth_and_founding_changes_the_world_hash_after_ten_years()
    {
        var worldOn = BuildScenario(seed: 22, cityRulesEnabled: true);
        var worldOff = BuildScenario(seed: 22, cityRulesEnabled: false);

        var systems = new List<ISimulationSystem>
        {
            new MaterializationSystem(), new CityGrowthSystem(), new MigrationSystem(), new SettlementFoundingSystem(),
        };
        new WorldClock(systems).Run(worldOn, HorizonHours);
        new WorldClock(systems).Run(worldOff, HorizonHours);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(worldOn), WorldSnapshot.CanonicalHash(worldOff));

        // Discriminação mais forte que o mínimo do AC (ver doc da classe): a divergência
        // continua existindo mesmo tirando a própria chave CityRules do snapshot — prova que
        // Cities/Npcs/Buildings/PendingEvents de fato evoluíram diferente, não só que a regra
        // em si é um campo do hash.
        Assert.NotEqual(SnapshotExcludingCityRules(worldOn), SnapshotExcludingCityRules(worldOff));

        // Confere que o braço "desligado" realmente ficou parado (nenhuma fundação, nenhuma
        // materializacao alem das manuais do braco ligado) - a divergencia acima vem do braco
        // ligado se mexendo, nao de outra fonte.
        Assert.Equal(2, worldOff.Cities.Count); // as 2 cidades iniciais - nenhuma fundação nova
        Assert.Equal(2, worldOff.Npcs.Count); // só os 2 manuais - MaterializeOne nunca chamado nesse braço
    }
}
