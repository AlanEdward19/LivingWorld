using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Shared.Population;

/// <summary>Fase 7, T22 (FAM-24, FAM-25): contrafactual rico/pobre com o mesmo genoma
/// (<c>Vitality</c>/<see cref="RateGene"/>) — só composição de teste, sem mudar produção
/// (AD-059).</summary>
public static class HouseholdCounterfactualHarness
{
    public const double FixedVitality = 60;
    public static readonly RateGene FixedRateGene = RateGene.Create(1.2).Value!;

    public static Household HouseholdWithStock(long stockAmount) => new(
        new HouseholdId(1), new CellCoord(0, 0), new NpcId(1), [new NpcId(1)],
        stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = stockAmount });

    public static (WorldState World, Npc Npc) CreateEmployedAdultWorld(
        ulong seed, double upbringing, double vitality, RateGene rateGene, FamilyRules? familyRules = null)
    {
        var economy = EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: new Dictionary<int, double>(),
            wageByProfession: new Dictionary<int, long> { [1] = 100 },
            priceFloor: new Dictionary<int, long>(),
            priceCeiling: new Dictionary<int, long>(),
            priceSensitivity: 0,
            demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

        var world = new WorldState(
            new WorldCalendar(24, 30, 12), seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: economy,
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>()),
            familyRules: familyRules ?? ScenarioRunner.DefaultFamilyRules);

        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "subject", Sex.Male,
            WorldDate.Epoch(world.Calendar).AddYears(-30), new CultureId(1), new CellCoord(1, 1),
            motherId: null, fatherId: null, household: null, health: 100, personality: personality,
            profession: new ProfessionType(1), currentLocation: new CellCoord(1, 1),
            rateGene: rateGene, vitality: vitality, upbringing: upbringing);
        world.AddNpc(npc);

        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies: 1,
            employees: [npc.Id], stock: new Dictionary<ResourceType, long>(), treasury: new Money(50_000),
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        npc.Hire(workplace.Id);

        return (world, npc);
    }

    public static long RunMonthlyWagesAndReturnWallet(WorldState world, Npc npc, int months)
    {
        var wageSystem = new WagePaymentSystem();
        for (int month = 0; month < months; month++)
            wageSystem.Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        return npc.Wallet.Amount;
    }

    private const int SubjectStartAgeYears = 30;

    /// <summary>T29/T30 (FAM-34/35): carreira até a morte (ou fim do horizonte) — reusa
    /// <see cref="FamilyRules.EffectiveVitalityMultiplier"/>/<see cref="LifeTable.AnnualMortality"/>
    /// (mesmo cálculo de <see cref="MortalitySystem.SchedulePlannedDeath"/>) para decidir quantos
    /// meses de salário o sujeito recebe antes de morrer — só assim o canal genético (Vitality→
    /// mortalidade) e o ambiental (Upbringing→salário) competem no mesmo resultado mensurável
    /// (patrimônio), em vez de o harness de wage-only nunca deixar ninguém morrer (AD-059).
    ///
    /// Rola a morte a partir dos <see cref="SubjectStartAgeYears"/> (30), não dos 0 —
    /// <see cref="MortalityPlanner.RollDeathAge"/> sempre parte do nascimento (certo para
    /// <c>NatalitySystem</c>), mas incoerente aqui: o sujeito já existe como adulto por
    /// construção (<see cref="CreateEmployedAdultWorld"/>), então rolar desde o nascimento
    /// produziria "mortes na infância" que nunca poderiam ter acontecido (ele já sobreviveu até
    /// os 30), contaminando a distribuição com zeros artificiais.</summary>
    public static long RunCareerWithMortalityAndReturnWallet(WorldState world, Npc npc, int horizonYears)
    {
        var rng = new TickContext(world, world.Rng, world.Scheduler).Rng($"mortality-{npc.Id.Value}");
        double multiplier = world.FamilyRules.EffectiveVitalityMultiplier(npc.Vitality);
        var table = world.PopulationRules.LifeTable;
        int deathAge = table.MaxLongevityYears;
        for (int age = SubjectStartAgeYears; age < table.MaxLongevityYears; age++)
        {
            if (rng.NextDouble() < table.AnnualMortality(age, npc.Health, multiplier))
            {
                deathAge = age;
                break;
            }
        }

        int careerYears = Math.Clamp(deathAge - SubjectStartAgeYears, 0, horizonYears);
        return RunMonthlyWagesAndReturnWallet(world, npc, careerYears * 12);
    }
}

public class HouseholdCounterfactualHarnessTests
{
    [Fact]
    public void Same_genome_in_rich_vs_poor_household_yields_different_upbringing()
    {
        var rich = HouseholdCounterfactualHarness.HouseholdWithStock(500);
        var poor = HouseholdCounterfactualHarness.HouseholdWithStock(5);

        double richUpbringing = HeredityService.DeriveUpbringing(rich, ScenarioRunner.DefaultFamilyRules);
        double poorUpbringing = HeredityService.DeriveUpbringing(poor, ScenarioRunner.DefaultFamilyRules);

        Assert.NotEqual(richUpbringing, poorUpbringing);
        Assert.True(richUpbringing > poorUpbringing);
    }

    [Fact]
    public void Same_vitality_and_rate_gene_produce_different_adult_wealth_when_upbringing_differs()
    {
        const ulong seed = 7;
        var richHousehold = HouseholdCounterfactualHarness.HouseholdWithStock(500);
        var poorHousehold = HouseholdCounterfactualHarness.HouseholdWithStock(5);
        var rules = ScenarioRunner.DefaultFamilyRules;

        double richUpbringing = HeredityService.DeriveUpbringing(richHousehold, rules);
        double poorUpbringing = HeredityService.DeriveUpbringing(poorHousehold, rules);

        var (worldRich, npcRich) = HouseholdCounterfactualHarness.CreateEmployedAdultWorld(
            seed, richUpbringing, HouseholdCounterfactualHarness.FixedVitality, HouseholdCounterfactualHarness.FixedRateGene);
        var (worldPoor, npcPoor) = HouseholdCounterfactualHarness.CreateEmployedAdultWorld(
            seed, poorUpbringing, HouseholdCounterfactualHarness.FixedVitality, HouseholdCounterfactualHarness.FixedRateGene);

        long walletRich = HouseholdCounterfactualHarness.RunMonthlyWagesAndReturnWallet(worldRich, npcRich, months: 12);
        long walletPoor = HouseholdCounterfactualHarness.RunMonthlyWagesAndReturnWallet(worldPoor, npcPoor, months: 12);

        Assert.NotEqual(walletRich, walletPoor);
        Assert.True(walletRich > walletPoor);
        Assert.Equal(HouseholdCounterfactualHarness.FixedVitality, npcRich.Vitality);
        Assert.Equal(HouseholdCounterfactualHarness.FixedVitality, npcPoor.Vitality);
        Assert.Equal(HouseholdCounterfactualHarness.FixedRateGene, npcRich.RateGene);
        Assert.Equal(HouseholdCounterfactualHarness.FixedRateGene, npcPoor.RateGene);
    }
}
