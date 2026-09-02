using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Economy;

/// <summary>Fase 6, T18 (SKILL-10): cenário pareado base/tratamento — mesma seed, mesma entrada
/// (recipe sem insumo), mesmo número de trabalhadores; tratamento = trabalhadores com habilidade
/// maior. Produção anual do tratamento maior em 10/10 seeds. <c>[Trait("Category","Scenario")]</c>
/// — fora do gate padrão.</summary>
public class ProductionSystemSkillTests
{
    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly EconomyRules Rules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static long AnnualProduction(ulong seed, double workerSkill, int workerCount)
    {
        var recipe = ProductionRecipe.Create(
            new Dictionary<int, long>(), new Dictionary<int, long> { [1] = 10 },
            requiresCellResource: null, maxWorkersPerCycle: workerCount).Value!;
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe> { [1] = recipe }, [], new Dictionary<int, int>());
        var location = new CellCoord(1, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Rules, economyCatalog: catalog);
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), location, maxVacancies: workerCount,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);

        for (int i = 0; i < workerCount; i++)
        {
            var npc = new Npc(
                world.NextNpcIdAndAdvance(), $"worker-{i}", Sex.Male, WorldDate.Epoch(world.Calendar).AddYears(-30),
                new CultureId(1), location, motherId: null, fatherId: null, household: null, health: 100,
                personality: SomePersonality, profession: new ProfessionType(1), currentLocation: location,
                skills: SkillSet.Empty.WithGain(new SkillType(0), workerSkill, cap: 100));
            world.AddNpc(npc);
            workplace.Hire(npc.Id);
            npc.Hire(workplace.Id);
        }

        var system = new ProductionSystem(ScenarioRunner.DefaultSkillsRules);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        long total = 0;
        for (int day = 0; day < 360; day++)
        {
            system.Tick(world, ctx);
            total = workplace.Stock.GetValueOrDefault(new ResourceType(1));
        }
        return total;
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Treatment_workshop_with_higher_skill_workers_produces_more_per_year_in_10_of_10_seeds()
    {
        int wins = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            long baseProduction = AnnualProduction(seed, workerSkill: 10, workerCount: 5);
            long treatmentProduction = AnnualProduction(seed, workerSkill: 90, workerCount: 5);
            if (treatmentProduction > baseProduction) wins++;
        }

        Assert.Equal(10, wins);
    }
}
